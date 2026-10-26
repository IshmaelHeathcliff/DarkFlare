using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkFlare
{
    public enum ProjectileDamageSource
    {
        Skill,
        EquippedWeapon
    }

    [ContentDefinition(ContentNamespaces.Skill)]
    [CreateAssetMenu(menuName = "DarkFlare/Data/Skills/Projectile Skill Definition", fileName = "ProjectileSkillDefinition")]
    public class ProjectileSkillDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [LabelText("投射物 Prefab")]
        AssetReferenceGameObject _projectilePrefab;

        [SerializeField]
        [MinValue(0.05f)]
        [LabelText("施法冷却")]
        float _cooldown = 0.6f;

        [SerializeField]
        [MinValue(0)]
        [LabelText("法力消耗")]
        float _manaCost;

        [SerializeField]
        [MinValue(0.1f)]
        [LabelText("锁敌范围")]
        float _targetRange = 10f;

        [SerializeField]
        [MinValue(0.1f)]
        [LabelText("投射物速度")]
        float _projectileSpeed = 12f;

        [SerializeField]
        [MinValue(0.05f)]
        [LabelText("投射物半径")]
        float _projectileRadius = 0.16f;

        [SerializeField]
        [MinValue(0.1f)]
        [LabelText("投射物寿命")]
        float _projectileLifetime = 2f;

        [SerializeField, PropertyRange(1, 32), LabelText("投射物数量")]
        int _projectileCount = 1;

        [SerializeField, PropertyRange(0, 360), LabelText("总散射角度")]
        float _spreadAngleDegrees;

        [SerializeField, PropertyRange(0, 32), LabelText("额外穿透目标数")]
        int _pierceCount;

        [SerializeField]
        [LabelText("自定义技能标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();

        [SerializeField]
        [LabelText("伤害来源")]
        ProjectileDamageSource _damageSource;

        [SerializeField]
        [LabelText("基础伤害")]
        List<DamageRollDefinition> _baseDamages = new List<DamageRollDefinition>();
        [SerializeField, LabelText("命中附加状态（显式启用）")]
        StatusDefinition _onHitStatus;
        public StatusDefinition OnHitStatus => _onHitStatus;

        public string Id => _id;

        public string DisplayName => _displayName;

        public AssetReferenceGameObject Prefab => _projectilePrefab;

        public float Cooldown => _cooldown;

        public float ManaCost => _manaCost;

        public float TargetRange => _targetRange;

        public float ProjectileSpeed => _projectileSpeed;

        public float ProjectileRadius => _projectileRadius;

        public float ProjectileLifetime => _projectileLifetime;

        public int ProjectileCount => _projectileCount;
        public float SpreadAngleDegrees => _spreadAngleDegrees;
        public int PierceCount => _pierceCount;
        public bool HasValidProjectileSettings => _projectileCount >= 1 && _projectileCount <= 32
            && _pierceCount >= 0 && _pierceCount <= 32
            && !float.IsNaN(_spreadAngleDegrees) && !float.IsInfinity(_spreadAngleDegrees)
            && _spreadAngleDegrees >= 0 && _spreadAngleDegrees <= 360;

        public float GetProjectileAngle(int index)
        {
            if (!HasValidProjectileSettings || index < 0 || index >= _projectileCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (_projectileCount == 1) { return 0; }
            int slot = index;
            if ((_projectileCount & 1) != 0)
            {
                int middle = _projectileCount / 2;
                slot = index == 0 ? middle : index <= middle ? middle - index : index;
            }
            return -_spreadAngleDegrees * .5f + _spreadAngleDegrees * slot / (_projectileCount - 1);
        }

        public IReadOnlyList<TagDefinition> Tags => _tags;

        public TagSet RuntimeTags => CombatTagResolver.ResolveProjectileSkillTags(TagSet.FromDefinitions(_tags));

        public ProjectileDamageSource DamageSource => _damageSource;

        public IReadOnlyList<DamageRollDefinition> BaseDamages => _baseDamages;

        public List<DamagePacket> CreateDamagePackets(int seed)
        {
            List<DamagePacket> packets = new List<DamagePacket>(_baseDamages.Count);
            System.Random random = new System.Random(seed);

            for (int i = 0; i < _baseDamages.Count; i++)
            {
                packets.Add(_baseDamages[i].CreatePacket(random));
            }

            return packets;
        }

        void OnValidate()
        {
            List<string> issues = RandomizationConfigurationValidator.Validate(this);

            for (int i = 0; i < issues.Count; i++)
            {
                ApplicationLog.Warning(LogEventIds.DataValidation, $"[ProjectileSkillDefinition] {name}: {issues[i]}", this);
            }
        }
    }
}
