using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed class MapEditUndoState : ScriptableObject
    {
        [SerializeField] string _json;
        public string Json { get => _json; set => _json = value; }
    }

    public static class MapEditFiles
    {
        public static string Hash(string path)
        {
            if (!File.Exists(path)) { return null; }
            using var stream = File.OpenRead(path);
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static void Write(string path, string json, string expectedHash)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + "~";
            try
            {
                File.WriteAllBytes(temporary, MapDocumentJson.Encode(json));
                if (Hash(fullPath) != expectedHash) { throw new MapEditException("Conflict", "$file", "磁盘文件已修改或删除；重新打开后合并，禁止覆盖"); }
                if (File.Exists(fullPath)) { File.Replace(temporary, fullPath, null); }
                else { File.Move(temporary, fullPath); }
            }
            finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
        }

        public static string AssetPath(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !full.EndsWith(".dfmap", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("地图源必须位于 Assets 内，后缀为 .dfmap");
            }
            return "Assets/" + full.Substring(root.Length).Replace('\\', '/');
        }
    }

    public sealed class MapEditSession : IDisposable
    {
        readonly Func<IMapDefinitionLookup> _definitions;
        readonly MapEditUndoState _undo;
        readonly Dictionary<string, (string Hash, JObject Receipt)> _receipts = new();
        readonly Queue<string> _receiptOrder = new();
        string _savedSemantic;
        string _currentSemantic;
        string _diskHash;
        string _path;
        readonly string _guid;
        long _undoBytes;
        int _undoCount;
        bool _disposed;

        public CompiledMapDocument Current { get; private set; }
        public string SourcePath => string.IsNullOrEmpty(_guid) ? _path : AssetDatabase.GUIDToAssetPath(_guid);
        public string SourceGuid => _guid;
        public bool IsDirty => _currentSemantic != _savedSemantic;
        public string Notice { get; private set; } = "";
        public long EstimatedUndoBytes => _undoBytes;
        public int UndoTransactionCount => _undoCount;
        public event Action Changed;
        public event Action Closed;

        public MapEditSession(CompiledMapDocument map, string path, Func<IMapDefinitionLookup> definitions)
        {
            Current = map;
            _path = path;
            _guid = AssetDatabase.AssetPathToGUID(path);
            _definitions = definitions;
            _savedSemantic = MapEditService.SemanticHash(map);
            _currentSemantic = _savedSemantic;
            _diskHash = MapEditFiles.Hash(path);
            _undo = ScriptableObject.CreateInstance<MapEditUndoState>();
            _undo.hideFlags = HideFlags.HideAndDontSave;
            _undo.name = "地图语义 " + map.MapId;
            _undo.Json = map.CanonicalJson;
            Undo.undoRedoPerformed += OnUndo;
        }

        public CompiledMapDocument Preview(JObject patch)
        {
            EnsureAlive();
            return MapEditService.Preview(Current, patch, _definitions());
        }

        public JObject Apply(JObject request)
        {
            EnsureAlive();
            JObject patch = MapEditService.ReadPatch(request.ToString(Formatting.None));
            string id = MapEditService.Text(patch, "operationId");
            string payload = MapEditService.Hash(Canonical(patch));
            if (_receipts.TryGetValue(id, out var existing))
            {
                if (existing.Hash != payload) { throw new MapEditException("Conflict", "$.operationId", "operationId 已用于不同内容"); }
                var retry = (JObject)existing.Receipt.DeepClone();
                retry["currentRevision"] = Current.Revision; retry["currentHash"] = Current.SourceHash; retry["replayed"] = true;
                return retry;
            }
            // 同步主线程事务：重新捕获定义并完成校验，期间不 yield、不发布部分状态。
            CompiledMapDocument candidate = Preview(patch);
            bool changed = candidate.SourceHash != Current.SourceHash;
            if (changed) { Publish(candidate, "编辑地图"); }
            var receipt = new JObject
            {
                ["code"] = "Ok", ["changed"] = changed, ["revision"] = Current.Revision, ["hash"] = Current.SourceHash,
                ["currentRevision"] = Current.Revision, ["currentHash"] = Current.SourceHash, ["replayed"] = false
            };
            _receipts.Add(id, (payload, receipt)); _receiptOrder.Enqueue(id);
            if (_receiptOrder.Count > 256) { _receipts.Remove(_receiptOrder.Dequeue()); }
            return (JObject)receipt.DeepClone();
        }

        public void Import(string json)
        {
            EnsureAlive();
            MapDocument copy = MapDocumentJson.Read(json);
            if (copy.MapId != Current.MapId) { throw new MapEditException("Conflict", "$.mapId", "导入必须保持当前 MapId；复制地图请使用另存为"); }
            copy.Revision = Current.Revision;
            CompiledMapDocument candidate = MapDocumentCompiler.Compile(copy, _definitions());
            if (MapEditService.SemanticHash(candidate) == MapEditService.SemanticHash(Current)) { return; }
            copy.Revision = checked(Current.Revision + 1);
            Publish(MapDocumentCompiler.Compile(copy, _definitions()), "导入地图");
        }

        void Publish(CompiledMapDocument candidate, string label)
        {
            long bytes = Math.Max(MapDocumentJson.Encode(_undo.Json).LongLength, _undo.Json.Length * 2L)
                + Math.Max(MapDocumentJson.Encode(candidate.CanonicalJson).LongLength, candidate.CanonicalJson.Length * 2L);
            if (_undoCount >= 64 || _undoBytes + bytes > 64 * 1048576L)
            {
                // Unity 不公开逐条移除本对象历史；只清除本载体，绝不清空全局 Undo。
                Undo.ClearUndo(_undo); _undoCount = 0; _undoBytes = 0;
                Notice = "地图撤销历史已达到预算，旧历史已清理；当前笔画仍可撤销。";
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(_undo, label);
            _undo.Json = candidate.CanonicalJson;
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            _undoBytes += bytes; _undoCount++;
            Current = candidate;
            _currentSemantic = MapEditService.SemanticHash(candidate);
            Checkpoint(); Changed?.Invoke();
        }

        void OnUndo()
        {
            if (_disposed || _undo.Json == Current.CanonicalJson) { return; }
            try
            {
                MapDocument copy = MapDocumentJson.Read(_undo.Json);
                copy.Revision = checked(Current.Revision + 1);
                Current = MapDocumentCompiler.Compile(copy, _definitions());
                _currentSemantic = MapEditService.SemanticHash(Current);
                _undo.Json = Current.CanonicalJson;
                Checkpoint(); Changed?.Invoke();
            }
            catch (Exception error)
            {
                _undo.Json = Current.CanonicalJson;
                Undo.ClearUndo(_undo);
                Notice = "定义改变导致撤销无效，已保留当前地图：" + error.Message;
                Changed?.Invoke();
            }
        }

        public void Save()
        {
            EnsureAlive();
            string path = SourcePath;
            if (string.IsNullOrEmpty(path)) { throw new MapEditException("Conflict", "$file", "原地图源已删除"); }
            path = MapEditFiles.AssetPath(path);
            MapDocumentCompiler.Compile(Current.CreateEditableCopy(), _definitions());
            MapEditSessions.RequireUnique(Current.MapId, path, this);
            MapEditFiles.Write(path, Current.CanonicalJson, _diskHash);
            _path = path;
            _diskHash = MapEditFiles.Hash(path);
            try
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<MapDefinition>(path) == null) { throw new InvalidOperationException("地图导入产物无效"); }
            }
            catch (Exception error) { throw new MapEditException("ImportFailed", "$file", "文件已写入，导入失败：" + error.Message); }
            _savedSemantic = MapEditService.SemanticHash(Current);
            Checkpoint(); Changed?.Invoke();
        }

        public void Checkpoint()
        {
            if (_disposed) { return; }
            string key = RecoveryPath;
            var recovery = new JObject
            {
                ["path"] = _path, ["guid"] = _guid, ["diskHash"] = _diskHash, ["savedSemantic"] = _savedSemantic,
                ["revision"] = Current.Revision, ["document"] = IsDirty ? JObject.Parse(Current.CanonicalJson) : null
            };
            string json = recovery.ToString(Formatting.None);
            Directory.CreateDirectory(MapEditSessions.RecoveryRoot);
            long total = Directory.EnumerateFiles(MapEditSessions.RecoveryRoot, "*.json").Where(file => Path.GetFullPath(file) != Path.GetFullPath(key)).Sum(file => new FileInfo(file).Length);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > 8 * 1048576 || total + System.Text.Encoding.UTF8.GetByteCount(json) > 64 * 1048576)
            {
                Notice = "草稿恢复预算耗尽，请保存地图；当前工作副本仍有效。"; return;
            }
            try { MapEditFiles.Write(key, json, MapEditFiles.Hash(key)); }
            catch (Exception error) { Notice = "草稿检查点失败，请保存地图：" + error.Message; }
        }

        public bool TryRestore()
        {
            if (!File.Exists(RecoveryPath)) { return false; }
            if (new FileInfo(RecoveryPath).Length > 8 * 1048576) { throw new MapEditException("BudgetExceeded", "$recovery", "恢复记录过大"); }
            JObject recovery = JObject.Parse(File.ReadAllText(RecoveryPath));
            if (recovery["revision"]?.Type != JTokenType.Integer || recovery["savedSemantic"]?.Type != JTokenType.String ||
                (recovery["document"] is not JObject && recovery["document"]?.Type != JTokenType.Null))
            { throw MapEditService.Invalid("$recovery", "恢复记录字段损坏"); }
            if ((string)recovery["guid"] != _guid || (string)recovery["diskHash"] != _diskHash)
            {
                PreserveRecovery("源身份 / 磁盘已改变"); return false;
            }
            MapDocument document = recovery["document"].Type == JTokenType.Null ? Current.CreateEditableCopy() : MapDocumentJson.Read(recovery["document"].ToString());
            if (document.MapId != Current.MapId) { throw new MapEditException("Conflict", "$recovery", "草稿地图身份失配"); }
            document.Revision = checked(Math.Max(Current.Revision, (int)recovery["revision"]) + 1);
            Current = MapDocumentCompiler.Compile(document, _definitions());
            _currentSemantic = MapEditService.SemanticHash(Current);
            _savedSemantic = (string)recovery["savedSemantic"];
            _undo.Json = Current.CanonicalJson;
            Notice = "已恢复编辑修订及有效草稿；域重载前的 Undo 历史不恢复。";
            return true;
        }

        public void PreserveRecovery(string reason)
        {
            string backup = RecoveryPath + ".conflict-" + Guid.NewGuid().ToString("N") + ".json";
            if (File.Exists(RecoveryPath)) { File.Move(RecoveryPath, backup); }
            Notice = "恢复记录未应用（" + reason + "）；已保留供手动合并：" + backup;
        }

        string RecoveryPath => Path.Combine(MapEditSessions.RecoveryRoot, MapEditService.Hash(string.IsNullOrEmpty(_guid) ? _path : _guid) + ".json");

        public void Discard()
        {
            MapDocument document = MapDocumentImporter.ReadSource(SourcePath);
            document.Revision = checked(Math.Max(document.Revision, Current.Revision) + 1);
            Current = MapDocumentCompiler.Compile(document, _definitions());
            _diskHash = MapEditFiles.Hash(SourcePath); _savedSemantic = MapEditService.SemanticHash(Current);
            _currentSemantic = _savedSemantic;
            _undo.Json = Current.CanonicalJson; Undo.ClearUndo(_undo);
            Checkpoint();
            Changed?.Invoke();
        }

        static string Canonical(JToken token)
        {
            if (token is JObject obj) { return "{" + string.Join(",", obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal).Select(item => JsonConvert.SerializeObject(item.Name) + ":" + Canonical(item.Value))) + "}"; }
            if (token is JArray array) { return "[" + string.Join(",", array.Select(Canonical)) + "]"; }
            return token.ToString(Formatting.None);
        }

        void EnsureAlive()
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(MapEditSession)); }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new MapEditException("Cancelled", "$", "Play 模式期间暂停地图编辑"); }
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            Checkpoint(); _disposed = true;
            Undo.undoRedoPerformed -= OnUndo;
            Undo.ClearUndo(_undo);
            UnityEngine.Object.DestroyImmediate(_undo);
            Closed?.Invoke(); Closed = null;
            Changed = null;
        }
    }

    [InitializeOnLoad]
    public static class MapEditSessions
    {
        public const string RecoveryRoot = "Library/DarkFlare/MapDrafts";
        static readonly Dictionary<string, MapEditSession> Sessions = new();

        static MapEditSessions()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CloseAll;
            EditorApplication.quitting += CloseAll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) { foreach (MapEditSession session in Sessions.Values) { session.Checkpoint(); } }
            };
        }

        public static MapEditSession Open(string path)
        {
            path = MapEditFiles.AssetPath(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) { throw new MapEditException("InvalidDocument", "$file", "地图源尚未导入"); }
            if (Sessions.TryGetValue(guid, out MapEditSession session)) { return session; }
            CompiledMapDocument map = MapDocumentCompiler.Compile(MapDocumentImporter.ReadSource(path), MapEditorDefinitionIndex.Capture());
            RequireUnique(map.MapId, path);
            session = new MapEditSession(map, path, () => MapEditorDefinitionIndex.Capture());
            try { session.TryRestore(); }
            catch (Exception error) when (error is JsonException || error is MapDocumentException || error is MapEditException || error is OverflowException)
            { session.PreserveRecovery(error.Message); }
            Sessions.Add(guid, session);
            return session;
        }

        public static void RequireUnique(string id, string exceptPath, MapEditSession self = null)
        {
            foreach (MapEditSession session in Sessions.Values)
            {
                if (session != self && session.Current.MapId == id && session.SourcePath != exceptPath) { throw new MapEditException("Conflict", "$.mapId", "另一编辑会话持有相同 MapId"); }
            }
            foreach (string path in Directory.EnumerateFiles("Assets", "*.dfmap", SearchOption.AllDirectories))
            {
                if (Path.GetFullPath(path).Equals(Path.GetFullPath(exceptPath), StringComparison.OrdinalIgnoreCase)) { continue; }
                try
                {
                    if (MapDocumentImporter.ReadSource(path).MapId == id) { throw new MapEditException("Conflict", "$.mapId", "重复 MapId：" + path); }
                }
                catch (MapDocumentException) { /* 其他无效源由导入器报告。 */ }
            }
        }

        public static bool HasOpen(string path)
        {
            return Sessions.ContainsKey(AssetDatabase.AssetPathToGUID(path));
        }

        public static void Close(string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!Sessions.Remove(guid, out MapEditSession session)) { return; }
            session.Dispose();
        }

        public static MapEditSession Create(string path, MapDocument document)
        {
            path = MapEditFiles.AssetPath(path);
            if (File.Exists(path)) { throw new MapEditException("Conflict", "$file", "新地图不得覆盖已有文件"); }
            document = MapDocumentJson.Read(MapDocumentJson.Write(document));
            RequireUnique(document.MapId, path);
            CompiledMapDocument map = MapDocumentCompiler.Compile(document, MapEditorDefinitionIndex.Capture());
            MapEditFiles.Write(path, map.CanonicalJson, null);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return Open(path);
        }

        public static void CloseAll()
        {
            foreach (MapEditSession session in Sessions.Values) { session.Dispose(); }
            Sessions.Clear();
        }
    }
}
