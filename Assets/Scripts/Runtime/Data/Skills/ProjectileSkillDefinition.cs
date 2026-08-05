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

    [CreateAssetMenu(menuName = "DarkFlare/Data/Skills/Projectile Skill Definition", fileName = "ProjectileSkillDefinition")]
    public class ProjectileSkillDefinition : ScriptableObject
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
        [LabelText("自动释放间隔")]
        float _cooldown = 0.6f;

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

        [SerializeField]
        [LabelText("技能标签")]
        List<TagDefinition> _tags = new List<TagDefinition>();

        [SerializeField]
        [LabelText("伤害来源")]
        ProjectileDamageSource _damageSource;

        [SerializeField]
        [LabelText("基础伤害")]
        List<DamageRollDefinition> _baseDamages = new List<DamageRollDefinition>();

        public string Id => _id;

        public string DisplayName => _displayName;

        public AssetReferenceGameObject Prefab => _projectilePrefab;

        public float Cooldown => _cooldown;

        public float TargetRange => _targetRange;

        public float ProjectileSpeed => _projectileSpeed;

        public float ProjectileRadius => _projectileRadius;

        public float ProjectileLifetime => _projectileLifetime;

        public TagSet RuntimeTags => TagSet.FromDefinitions(_tags);

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

            if (packets.Count == 0)
            {
                packets.Add(new DamagePacket(DamageType.Physical, 12f, TagSet.Empty));
            }

            return packets;
        }
    }
}
