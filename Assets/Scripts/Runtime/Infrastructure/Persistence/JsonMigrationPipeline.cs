using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DarkFlare
{
    public enum MigrationDataDomain
    {
        Save,
        Settings,
    }

    public interface IJsonMigrationStep
    {
        string Id { get; }

        MigrationDataDomain Domain { get; }

        int FromVersion { get; }

        int ToVersion { get; }

        void Apply(JObject document);
    }

    public enum MigrationRegistrationIssueCode
    {
        CurrentVersionInvalid,
        StepMissing,
        StepIdInvalid,
        StepIdDuplicate,
        DomainMismatch,
        VersionInvalid,
        StepMustBeIncremental,
        DuplicateFromVersion,
        MigrationPathMissing,
    }

    public sealed class MigrationRegistrationIssue
    {
        public MigrationRegistrationIssueCode Code { get; }

        public string Message { get; }

        public MigrationRegistrationIssue(MigrationRegistrationIssueCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }
    }

    public sealed class JsonMigrationRegistryBuildResult
    {
        public JsonMigrationRegistry Registry { get; }

        public IReadOnlyList<MigrationRegistrationIssue> Issues { get; }

        public bool Succeeded => Registry != null && Issues.Count == 0;

        internal JsonMigrationRegistryBuildResult(
            JsonMigrationRegistry registry,
            IReadOnlyList<MigrationRegistrationIssue> issues)
        {
            Registry = registry;
            Issues = issues;
        }
    }

    public sealed class JsonMigrationRegistry
    {
        readonly IReadOnlyDictionary<int, IJsonMigrationStep> _stepsByFromVersion;

        public MigrationDataDomain Domain { get; }

        public int CurrentVersion { get; }

        JsonMigrationRegistry(
            MigrationDataDomain domain,
            int currentVersion,
            IReadOnlyDictionary<int, IJsonMigrationStep> stepsByFromVersion)
        {
            Domain = domain;
            CurrentVersion = currentVersion;
            _stepsByFromVersion = stepsByFromVersion;
        }

        public static JsonMigrationRegistryBuildResult Build(
            MigrationDataDomain domain,
            int currentVersion,
            IEnumerable<IJsonMigrationStep> steps)
        {
            List<MigrationRegistrationIssue> issues = new List<MigrationRegistrationIssue>();
            Dictionary<int, IJsonMigrationStep> byFromVersion = new Dictionary<int, IJsonMigrationStep>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            if (currentVersion <= 0)
            {
                issues.Add(new MigrationRegistrationIssue(
                    MigrationRegistrationIssueCode.CurrentVersionInvalid,
                    "当前迁移版本必须大于 0"));
            }

            if (steps == null)
            {
                issues.Add(new MigrationRegistrationIssue(
                    MigrationRegistrationIssueCode.StepMissing,
                    "迁移步骤集合为空"));
                return Failed(issues);
            }

            foreach (IJsonMigrationStep step in steps)
            {
                if (step == null)
                {
                    issues.Add(new MigrationRegistrationIssue(
                        MigrationRegistrationIssueCode.StepMissing,
                        "迁移步骤为空"));
                    continue;
                }

                if (!ContentId.IsValidSegment(step.Id))
                {
                    issues.Add(new MigrationRegistrationIssue(
                        MigrationRegistrationIssueCode.StepIdInvalid,
                        $"迁移步骤 ID 非法：{step.Id}"));
                }
                else if (!ids.Add(step.Id))
                {
                    issues.Add(new MigrationRegistrationIssue(
                        MigrationRegistrationIssueCode.StepIdDuplicate,
                        $"迁移步骤 ID 重复：{step.Id}"));
                }

                if (step.Domain != domain)
                {
                    issues.Add(new MigrationRegistrationIssue(
                        MigrationRegistrationIssueCode.DomainMismatch,
                        $"迁移步骤 {step.Id} 属于 {step.Domain}，期望 {domain}"));
                }

                if (step.FromVersion < 0
                    || step.ToVersion <= 0
                    || currentVersion > 0
                    && (step.FromVersion >= currentVersion || step.ToVersion > currentVersion))
                {
                    issues.Add(new MigrationRegistrationIssue(
                        MigrationRegistrationIssueCode.VersionInvalid,
                        $"迁移步骤 {step.Id} 超出 0 → {currentVersion} 的当前迁移边界："
                        + $"{step.FromVersion} → {step.ToVersion}"));
                }

                if (step.ToVersion != step.FromVersion + 1)
                {
                    issues.Add(new MigrationRegistrationIssue(
                        MigrationRegistrationIssueCode.StepMustBeIncremental,
                        $"迁移步骤 {step.Id} 必须只执行 N → N+1"));
                }

                if (!byFromVersion.TryAdd(step.FromVersion, step))
                {
                    issues.Add(new MigrationRegistrationIssue(
                        MigrationRegistrationIssueCode.DuplicateFromVersion,
                        $"版本 {step.FromVersion} 存在多个迁移步骤"));
                }
            }

            if (currentVersion > 0)
            {
                for (int version = 0; version < currentVersion; version++)
                {
                    if (!byFromVersion.TryGetValue(version, out IJsonMigrationStep step)
                        || step.ToVersion != version + 1)
                    {
                        issues.Add(new MigrationRegistrationIssue(
                            MigrationRegistrationIssueCode.MigrationPathMissing,
                            $"缺少迁移链：{version} → {version + 1}"));
                    }
                }
            }

            if (issues.Count > 0)
            {
                return Failed(issues);
            }

            return new JsonMigrationRegistryBuildResult(
                new JsonMigrationRegistry(domain, currentVersion, byFromVersion),
                Array.Empty<MigrationRegistrationIssue>());
        }

        internal bool TryGetStep(int fromVersion, out IJsonMigrationStep step)
        {
            return _stepsByFromVersion.TryGetValue(fromVersion, out step);
        }

        static JsonMigrationRegistryBuildResult Failed(List<MigrationRegistrationIssue> issues)
        {
            return new JsonMigrationRegistryBuildResult(null, issues.AsReadOnly());
        }
    }

    public enum MigrationResultCode
    {
        Success,
        InputMissing,
        SourceVersionInvalid,
        FutureVersionUnsupported,
        MigrationPathMissing,
        StepFailed,
    }

    public sealed class JsonMigrationResult
    {
        public MigrationResultCode Code { get; }

        public JObject Document { get; }

        public int OriginalVersion { get; }

        public int FinalVersion { get; }

        public string FailedStepId { get; }

        public Exception Exception { get; }

        public IReadOnlyList<string> CompletedStepIds { get; }

        public bool Succeeded => Code == MigrationResultCode.Success && Document != null;

        internal JsonMigrationResult(
            MigrationResultCode code,
            JObject document,
            int originalVersion,
            int finalVersion,
            string failedStepId,
            Exception exception,
            IReadOnlyList<string> completedStepIds)
        {
            Code = code;
            Document = document;
            OriginalVersion = originalVersion;
            FinalVersion = finalVersion;
            FailedStepId = failedStepId ?? string.Empty;
            Exception = exception;
            CompletedStepIds = completedStepIds;
        }
    }

    public sealed class JsonMigrationPipeline
    {
        readonly JsonMigrationRegistry _registry;
        readonly string _versionPropertyName;

        public JsonMigrationPipeline(
            JsonMigrationRegistry registry,
            string versionPropertyName = "schemaVersion")
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            if (string.IsNullOrWhiteSpace(versionPropertyName))
            {
                throw new ArgumentException("版本字段名不能为空", nameof(versionPropertyName));
            }

            _versionPropertyName = versionPropertyName;
        }

        public JsonMigrationResult Migrate(JObject input, int sourceVersion)
        {
            if (input == null)
            {
                return Failure(MigrationResultCode.InputMissing, sourceVersion, sourceVersion);
            }

            if (sourceVersion < 0)
            {
                return Failure(MigrationResultCode.SourceVersionInvalid, sourceVersion, sourceVersion);
            }

            if (sourceVersion > _registry.CurrentVersion)
            {
                return Failure(
                    MigrationResultCode.FutureVersionUnsupported,
                    sourceVersion,
                    sourceVersion);
            }

            JObject working = (JObject)input.DeepClone();
            List<string> completed = new List<string>();
            int version = sourceVersion;

            while (version < _registry.CurrentVersion)
            {
                if (!_registry.TryGetStep(version, out IJsonMigrationStep step))
                {
                    return Failure(
                        MigrationResultCode.MigrationPathMissing,
                        sourceVersion,
                        version,
                        completedStepIds: completed.AsReadOnly());
                }

                try
                {
                    step.Apply(working);
                    version = step.ToVersion;
                    SetVersion(working, version);
                    completed.Add(step.Id);
                }
                catch (Exception exception)
                {
                    return Failure(
                        MigrationResultCode.StepFailed,
                        sourceVersion,
                        version,
                        step.Id,
                        exception,
                        completed.AsReadOnly());
                }
            }

            return new JsonMigrationResult(
                MigrationResultCode.Success,
                working,
                sourceVersion,
                version,
                string.Empty,
                null,
                completed.AsReadOnly());
        }

        void SetVersion(JObject document, int version)
        {
            string[] segments = _versionPropertyName.Split('.');
            JObject owner = document;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (owner[segments[i]] is JObject child)
                {
                    owner = child;
                    continue;
                }

                child = new JObject();
                owner[segments[i]] = child;
                owner = child;
            }

            owner[segments[segments.Length - 1]] = version;
        }

        static JsonMigrationResult Failure(
            MigrationResultCode code,
            int originalVersion,
            int finalVersion,
            string failedStepId = null,
            Exception exception = null,
            IReadOnlyList<string> completedStepIds = null)
        {
            return new JsonMigrationResult(
                code,
                null,
                originalVersion,
                finalVersion,
                failedStepId,
                exception,
                completedStepIds ?? Array.Empty<string>());
        }
    }

    public sealed class BossEncounterSaveMigration : IJsonMigrationStep
    {
        public string Id => "save_6_to_7_boss_encounter";
        public MigrationDataDomain Domain => MigrationDataDomain.Save;
        public int FromVersion => 6;
        public int ToVersion => 7;
        public void Apply(JObject document)
        {
            if (document["payload"]?["run"] is not JObject run) { throw new InvalidOperationException("缺少 Run 数据"); }
            AddLegacy(run);
            if (run["monsters"] is JArray active)
            { foreach (JToken monster in active) { monster["boss"] = JValue.CreateNull(); } }
            if (run["frozenMaps"] is JArray maps)
            {
                foreach (JToken map in maps)
                {
                    if (map is not JObject frozen) { throw new InvalidOperationException("冻结地图非法"); }
                    AddLegacy(frozen);
                    if (frozen["monsters"] is not JArray monsters) { throw new InvalidOperationException("缺少冻结怪物集合"); }
                    foreach (JToken monster in monsters) { monster["boss"] = JValue.CreateNull(); }
                }
            }
        }
        static void AddLegacy(JObject map)
        {
            map["boss"] = new JObject
            {
                ["status"] = (int)BossEncounterStatus.LegacyAbsent,
                ["spawnId"] = "", ["monsterDefinitionId"] = "", ["instanceId"] = ""
            };
        }
    }

    public sealed class MonsterBehaviorSaveMigration : IJsonMigrationStep
    {
        public string Id => "save_5_to_6_monster_behavior";
        public MigrationDataDomain Domain => MigrationDataDomain.Save;
        public int FromVersion => 5;
        public int ToVersion => 6;
        public void Apply(JObject document)
        {
            if (document["payload"]?["run"] is not JObject run) { throw new InvalidOperationException("缺少 Run 数据"); }
            Initialize(run["monsters"]);
            if (run["frozenMaps"] is JArray maps)
            { foreach (JToken map in maps) { Initialize(map["monsters"]); } }
        }
        static void Initialize(JToken monsters)
        {
            if (monsters is not JArray array) { throw new InvalidOperationException("缺少怪物集合"); }
            var serializer = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
                { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() });
            foreach (JToken monster in array)
            {
                if (monster["position"] is not JObject position) { throw new InvalidOperationException("缺少怪物位置"); }
                var state = MonsterBehaviorStateDto.Create(position.ToObject<Vector3Dto>(), monster["randomSeeds"]?["rootSeed"]?.Value<int>() ?? 0);
                monster["behavior"] = JObject.FromObject(state, serializer);
            }
        }
    }

    public sealed class NavigationSaveMigration : IJsonMigrationStep
    {
        public string Id => "save_4_to_5_navigation_intent";
        public MigrationDataDomain Domain => MigrationDataDomain.Save;
        public int FromVersion => 4;
        public int ToVersion => 5;
        public void Apply(JObject document)
        {
            if (document["payload"]?["run"] is not JObject run) { throw new InvalidOperationException("缺少 Run 数据"); }
            Initialize(run["monsters"]);
            if (run["frozenMaps"] is JArray maps)
            { foreach (JToken map in maps) { Initialize(map["monsters"]); } }
        }
        static void Initialize(JToken monsters)
        {
            if (monsters is not JArray array) { throw new InvalidOperationException("缺少怪物集合"); }
            foreach (JToken monster in array)
            {
                monster["navigation"] = new JObject { ["mode"] = null, ["target"] = 0, ["actorId"] = "", ["mapId"] = "",
                    ["point"] = new JObject { ["x"] = 0, ["y"] = 0, ["z"] = 0 }, ["stopDistance"] = null };
            }
        }
    }

    public sealed class WorldMapSaveMigration : IJsonMigrationStep
    {
        public string Id => "save_3_to_4_world_maps";
        public MigrationDataDomain Domain => MigrationDataDomain.Save;
        public int FromVersion => 3;
        public int ToVersion => 4;
        public void Apply(JObject document)
        {
            if (document["payload"]?["run"] is not JObject run) { throw new InvalidOperationException("缺少 Run 数据"); }
            run["activeMapId"] = "map:wilderness";
            run["mapRevision"] = 0;
            run["frozenMaps"] = new JArray();
            run["mapRandom"] = run["random"]?.DeepClone();
        }
    }

    public sealed class StatusSaveMigration : IJsonMigrationStep
    {
        public string Id => "save_2_to_3_statuses";
        public MigrationDataDomain Domain => MigrationDataDomain.Save;
        public int FromVersion => 2;
        public int ToVersion => 3;
        public void Apply(JObject document)
        {
            if (document["payload"]?["run"] is JObject run)
            {
                run["statuses"] = new JObject { ["time"] = 0, ["actors"] = new JArray() };
            }
        }
    }

    public sealed class ItemQuantitySaveMigration : IJsonMigrationStep
    {
        public string Id => "save_1_to_2_item_quantity";
        public MigrationDataDomain Domain => MigrationDataDomain.Save;
        public int FromVersion => 1;
        public int ToVersion => 2;

        public void Apply(JObject document)
        {
            if (document["payload"]?["items"] is not JArray items) { return; }
            foreach (JToken token in items)
            {
                if (token is JObject item) { item["quantity"] = 1; }
            }
        }
    }

    public sealed class LegacySaveV0ToV1Migration : IJsonMigrationStep
    {
        public string Id => "save_0_to_1_content_ids";

        public MigrationDataDomain Domain => MigrationDataDomain.Save;

        public int FromVersion => 0;

        public int ToVersion => 1;

        public void Apply(JObject document)
        {
            JArray items = document["items"] as JArray
                ?? document["payload"]?["items"] as JArray;

            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is not JObject item)
                {
                    continue;
                }

                JToken legacy = item["baseItemId"];

                if (legacy == null)
                {
                    continue;
                }

                string localId = legacy.Type == JTokenType.String
                    ? legacy.Value<string>()
                    : string.Empty;

                if (!ContentId.TryCreate(ContentNamespaces.Item, localId, out ContentId contentId))
                {
                    throw new FormatException($"旧物品 ID 无法迁移：{localId}");
                }

                item.Remove("baseItemId");
                item["baseContentId"] = contentId.ToString();
            }
        }
    }
}
