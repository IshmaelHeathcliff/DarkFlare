using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    public enum CombatTagDomain
    {
        [LabelText("物品生成")]
        ItemSpawn,

        [LabelText("词缀自身")]
        Modifier,

        [LabelText("角色")]
        Actor,

        [LabelText("技能")]
        Skill,

        [LabelText("本次攻击")]
        Attack,

        [LabelText("伤害")]
        Damage
    }

    public enum CombatTagUsage
    {
        [LabelText("正式使用")]
        Active,

        [LabelText("预留")]
        Reserved
    }

    [ContentDefinition(ContentNamespaces.Tag)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Tags/Tag Definition", fileName = "TagDefinition")]
    public class TagDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("本地化名称")]
        LocalizedContentReference _localizedName = new LocalizedContentReference("items", string.Empty);

        [SerializeField]
        [LabelText("标签域")]
        CombatTagDomain _domain;

        [SerializeField]
        [LabelText("使用状态")]
        CombatTagUsage _usage;

        [SerializeField]
        [TextArea]
        [LabelText("说明")]
        string _description = string.Empty;

        public string Id => _id;

        public string DisplayName => _displayName;

        public LocalizedContentReference LocalizedName => _localizedName;

        public CombatTagDomain Domain => _domain;

        public CombatTagUsage Usage => _usage;

        public string Description => _description;
    }
}
