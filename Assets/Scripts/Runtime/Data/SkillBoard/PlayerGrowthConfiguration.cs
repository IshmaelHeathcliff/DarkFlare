using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/技能面板/玩家成长配置", fileName = "PlayerGrowth")]
    public sealed class PlayerGrowthConfiguration : ScriptableObject
    {
        [SerializeField, MinValue(1), LabelText("首个天赋点能量")] int _firstCost = 100;
        [SerializeField, MinValue(1), LabelText("每点增加能量")] int _costIncrement = 25;
        [SerializeField, MinValue(1), LabelText("点亮柴薪消耗点数")] int _firewoodCost = 1;
        [SerializeField, MinValue(1), LabelText("点亮普通天赋消耗点数")] int _talentCost = 1;
        [SerializeField, MinValue(1), LabelText("每属性点增加主属性")] int _attributeValue = 1;

        public GrowthRules Rules => new GrowthRules(_firstCost, _costIncrement, _firewoodCost, _talentCost, _attributeValue);
    }

    public readonly struct GrowthRules : IEquatable<GrowthRules>
    {
        public const int ContractVersion = 1;
        // 整数分配进入 Single 属性消费者时仍可精确表达的技术边界。
        public const int MaximumPoints = 16777215;
        public static GrowthRules Default => new GrowthRules(100, 25, 1, 1, 1);
        public int FirstCost { get; }
        public int CostIncrement { get; }
        public int FirewoodCost { get; }
        public int TalentCost { get; }
        public int AttributeValue { get; }
        public bool IsValid => FirstCost > 0 && CostIncrement > 0 && FirewoodCost > 0
            && TalentCost > 0 && AttributeValue > 0;

        public GrowthRules(int firstCost, int costIncrement, int firewoodCost, int talentCost, int attributeValue)
        {
            FirstCost = firstCost;
            CostIncrement = costIncrement;
            FirewoodCost = firewoodCost;
            TalentCost = talentCost;
            AttributeValue = attributeValue;
        }

        public long NextCost(int earned)
        {
            return checked(FirstCost + (long)CostIncrement * earned);
        }

        public long TotalCost(int points)
        {
            long first = points;
            long second = points - 1L;
            if (first % 2 == 0) { first /= 2; }
            else { second /= 2; }
            return checked((long)points * FirstCost + checked(first * second * CostIncrement));
        }

        public bool Equals(GrowthRules other)
        {
            return FirstCost == other.FirstCost && CostIncrement == other.CostIncrement
                && FirewoodCost == other.FirewoodCost && TalentCost == other.TalentCost && AttributeValue == other.AttributeValue;
        }
        public override bool Equals(object obj)
        {
            return obj is GrowthRules other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(FirstCost, CostIncrement, FirewoodCost, TalentCost, AttributeValue);
        }
    }
}
