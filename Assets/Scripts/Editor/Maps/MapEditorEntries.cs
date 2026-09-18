using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public sealed partial class MapEditorWindow
    {
        [SerializeField, TabGroup("入口"), LabelText("显示入口")] bool _showEntries = true;
        [SerializeField, TabGroup("入口"), LabelText("锁定入口")] bool _lockEntries;
        [SerializeField, TabGroup("入口"), LabelText("入口 ID")] string _entryId = "arrival";
        [SerializeField, TabGroup("入口"), LabelText("入口用途")] MapEntryKind _entryKind;
        [SerializeField, TabGroup("入口"), LabelText("位置（局部世界）")] Vector2 _entryPosition;
        string _selectedEntry;

        JObject EntryOperation()
        {
            return new JObject
            {
                ["type"] = "UpsertEntry", ["record"] = new JObject
                {
                    ["entryId"] = _entryId, ["kind"] = _entryKind.ToString(),
                    ["position"] = new JObject { ["x"] = _entryPosition.x, ["y"] = _entryPosition.y }
                }
            };
        }

        [TabGroup("入口"), Button("应用入口属性")]
        void UpsertEntry()
        {
            Execute(() =>
            {
                if (_lockEntries) { throw new InvalidOperationException("入口层已锁定"); }
                Apply(EntryOperation());
                _selectedEntry = _entryId;
            });
        }

        [TabGroup("入口"), Button("删除当前入口")]
        void RemoveEntry()
        {
            Execute(() =>
            {
                if (_lockEntries) { throw new InvalidOperationException("入口层已锁定"); }
                Apply(new JObject { ["type"] = "RemoveEntry", ["id"] = _entryId });
                _selectedEntry = null;
            });
        }

        bool SelectEntry(Vector2 world)
        {
            if (!_showEntries || _lockEntries) { return false; }
            MapEntryRecord entry = _viewDocument.Entries.FirstOrDefault(item => Vector2.Distance(world, EntryWorld(item)) < 0.3f);
            if (entry == null) { return false; }
            _selectedEntry = _entryId = entry.EntryId;
            _entryKind = entry.Kind;
            _entryPosition = new Vector2((float)entry.Position.X, (float)entry.Position.Y);
            Repaint();
            return true;
        }

        Vector2 EntryWorld(MapEntryRecord entry)
        {
            return new Vector2((float)(_session.Current.OriginX + entry.Position.X), (float)(_session.Current.OriginY + entry.Position.Y));
        }

        void DrawEntries()
        {
            if (!_showEntries) { return; }
            foreach (MapEntryRecord entry in _viewDocument.Entries)
            {
                Handles.color = entry.EntryId == _selectedEntry ? Color.yellow : Color.cyan;
                Handles.DrawWireDisc(EntryWorld(entry), Vector3.forward, 0.3f);
                Handles.Label(EntryWorld(entry), entry.EntryId + " · " + entry.Kind);
                if (entry.EntryId != _selectedEntry || _lockEntries || _tool != MapPaintTool.Select) { continue; }
                EditorGUI.BeginChangeCheck();
                Vector3 next = Handles.PositionHandle(EntryWorld(entry), Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    _entryPosition = (Vector2)next - new Vector2((float)_session.Current.OriginX, (float)_session.Current.OriginY);
                }
                if (Event.current.rawType == EventType.MouseUp && Vector2.Distance(_entryPosition, new Vector2((float)entry.Position.X, (float)entry.Position.Y)) > 0.0001f)
                {
                    UpsertEntry();
                }
            }
        }
    }
}
