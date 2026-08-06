using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [Serializable]
    public class MonsterSpawnRule
    {
        [SerializeField]
        [LabelText("怪物")]
        MonsterDefinition _monster;

        [SerializeField]
        [MinValue(0)]
        [LabelText("权重")]
        int _weight = 100;

        public MonsterDefinition Monster => _monster;

        public int Weight => _weight;
    }

    [CreateAssetMenu(menuName = "DarkFlare/Data/Monsters/Monster Spawn Definition", fileName = "MonsterSpawnDefinition")]
    public class MonsterSpawnDefinition : ScriptableObject
    {
        [SerializeField]
        [MinValue(0.1f)]
        [LabelText("生成间隔")]
        float _spawnInterval = 1.5f;

        [SerializeField]
        [MinValue(1)]
        [LabelText("最大存活数量")]
        int _maxAliveCount = 12;

        [SerializeField]
        [MinValue(0.1f)]
        [LabelText("生成半径")]
        float _spawnRadius = 8f;

        [SerializeField]
        [LabelText("怪物池")]
        List<MonsterSpawnRule> _rules = new List<MonsterSpawnRule>();

        public float SpawnInterval => _spawnInterval;

        public int MaxAliveCount => _maxAliveCount;

        public float SpawnRadius => _spawnRadius;

        public IReadOnlyList<MonsterSpawnRule> Rules => _rules;

        public IEnumerable<MonsterDefinition> AllMonsters
        {
            get
            {
                for (int i = 0; i < _rules.Count; i++)
                {
                    if (_rules[i].Monster != null)
                    {
                        yield return _rules[i].Monster;
                    }
                }
            }
        }

        public MonsterDefinition PickMonster(System.Random random)
        {
            int totalWeight = 0;

            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].Monster != null && _rules[i].Weight > 0)
                {
                    totalWeight += _rules[i].Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = random.Next(0, totalWeight);

            for (int i = 0; i < _rules.Count; i++)
            {
                MonsterSpawnRule rule = _rules[i];

                if (rule.Monster == null || rule.Weight <= 0)
                {
                    continue;
                }

                if (roll < rule.Weight)
                {
                    return rule.Monster;
                }

                roll -= rule.Weight;
            }

            return null;
        }
    }
}
