using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkFlare
{
    public sealed class PlayerSkillsDto : IPersistenceDto
    {
        [JsonProperty(Required = Required.Always, ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> FlameSlots { get; set; } = Enumerable.Repeat(string.Empty, 6).ToList();
        [JsonProperty(Required = Required.Always)]
        public float NormalCooldownRemainingSeconds { get; set; }
    }

    public sealed class ManualSkillsSaveMigration : IJsonMigrationStep
    {
        public string Id => "save_11_to_12_manual_skills";
        public MigrationDataDomain Domain => MigrationDataDomain.Save;
        public int FromVersion => 11;
        public int ToVersion => 12;
        public void Apply(JObject document)
        {
            if (document["payload"]?["run"]?["player"] is not JObject player)
            { throw new ArgumentException("历史玩家技能缺失"); }
            string legacySkill = player.Value<string>("skillContentId");
            if (!ContentId.TryParse(legacySkill, out ContentId id) || id.Namespace != ContentNamespaces.Skill)
            { throw new ArgumentException("历史技能身份非法"); }
            // 已发布单技能版本只有此技能；未知历史能力必须先提供迁移映射，不能静默丢弃。
            if (legacySkill != "skill:basic_projectile")
            { throw new ArgumentException("尚无历史技能到武器普通技能的迁移映射：" + legacySkill); }
            player["skills"] = new JObject
            {
                ["flameSlots"] = new JArray(Enumerable.Repeat(string.Empty, 6)),
                ["normalCooldownRemainingSeconds"] = player["autoCastCooldownRemainingSeconds"]?.DeepClone() ?? new JValue(0)
            };
            player.Remove("skillContentId");
            player.Remove("autoCastCooldownRemainingSeconds");
            if (document["payload"]?["items"] is JArray items)
            {
                foreach (JObject item in items.OfType<JObject>())
                {
                    if (item["skillBoard"] is JObject board) { board["cooldownRemainingSeconds"] = 0; }
                }
            }
        }
    }

    public static partial class SaveDataValidator
    {
        static void ValidatePlayerSkills(PlayerSkillsDto skills, ProfileSaveData profile,
            IReadOnlyDictionary<ItemInstanceId, ItemInstanceDto> items, string path, List<SaveDataIssue> issues)
        {
            if (skills?.FlameSlots == null || skills.FlameSlots.Count != 6
                || !PlayerSkillStateRegistry.FiniteNonnegative(skills.NormalCooldownRemainingSeconds))
            { Add(issues, SaveDataIssueCode.InvalidValue, path, "六槽集合或普通技能冷却非法"); return; }
            var unique = new HashSet<string>();
            foreach (string id in skills.FlameSlots)
            {
                if (id == string.Empty) { continue; }
                SkillBoardEmbeddingDto embedding = profile?.SkillBoard?.Embeddings?.FirstOrDefault(e => e?.ItemInstanceId == id);
                if (id == null || !unique.Add(id) || embedding == null || embedding.NodeId != SkillBoardGeometry.CenterNodeId
                    || !ItemInstanceId.TryParse(id, out ItemInstanceId itemId) || !items.TryGetValue(itemId, out ItemInstanceDto item)
                    || string.IsNullOrEmpty(item.SkillBoard?.FlameSkillContentId)
                    || profile.Growth?.Activations?.Any(a => a != null && a.FirewoodId == embedding.FirewoodInstanceId && a.NodeId == "") != true)
                { Add(issues, SaveDataIssueCode.InvalidValue, path, "火焰重复分配、无所有权或未激活"); }
            }
        }
    }
}
