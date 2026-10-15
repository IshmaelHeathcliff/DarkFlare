using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class GrowthActivation
    {
        public string FirewoodId { get; }
        // 空字符串表示柴薪本身，不是中柴薪的空白中心插槽。
        public string NodeId { get; }
        public int Cost { get; }
        public bool IsFirewood => NodeId == string.Empty;
        public FirewoodPortReference Key => new FirewoodPortReference(FirewoodId, NodeId);

        public GrowthActivation(string firewoodId, string nodeId, int cost)
        {
            FirewoodId = firewoodId;
            NodeId = nodeId;
            Cost = cost;
        }
    }

    public sealed class PlayerGrowthState
    {
        public GrowthRules Rules { get; }
        public long Energy { get; }
        public int EarnedPoints { get; }
        public int TalentPoints { get; }
        public int AttributePoints { get; }
        public int Strength { get; }
        public int Dexterity { get; }
        public int Intelligence { get; }
        public IReadOnlyList<GrowthActivation> Activations { get; }

        public PlayerGrowthState(GrowthRules? rules = null, long energy = 0, int earnedPoints = 0,
            int talentPoints = 0, int attributePoints = 0, int strength = 0, int dexterity = 0,
            int intelligence = 0, IEnumerable<GrowthActivation> activations = null)
        {
            Rules = rules ?? GrowthRules.Default;
            Energy = energy;
            EarnedPoints = earnedPoints;
            TalentPoints = talentPoints;
            AttributePoints = attributePoints;
            Strength = strength;
            Dexterity = dexterity;
            Intelligence = intelligence;
            Activations = Array.AsReadOnly((activations ?? Array.Empty<GrowthActivation>()).ToArray());
        }

        public bool IsActive(string woodId, string nodeId = "")
        {
            return Activations.Any(a => a != null && a.FirewoodId == woodId && a.NodeId == nodeId);
        }

        public string Validate()
        {
            if (!Rules.IsValid || Energy < 0 || EarnedPoints < 0 || EarnedPoints > GrowthRules.MaximumPoints
                || TalentPoints < 0 || AttributePoints < 0 || Strength < 0 || Dexterity < 0 || Intelligence < 0)
            {
                return "成长配置或余额非法";
            }
            if (Energy >= Rules.NextCost(EarnedPoints)) { return "成长池仍有应兑换的能量"; }
            if ((long)AttributePoints + Strength + Dexterity + Intelligence != EarnedPoints)
            {
                return "属性点账目不守恒";
            }
            if ((long)Math.Max(Strength, Math.Max(Dexterity, Intelligence)) * Rules.AttributeValue > GrowthRules.MaximumPoints)
            {
                return "属性分配超出精确整数范围";
            }
            long spent = 0;
            var keys = new HashSet<FirewoodPortReference>();
            foreach (GrowthActivation activation in Activations)
            {
                if (activation == null || !ItemInstanceId.TryParse(activation.FirewoodId, out _)
                    || activation.NodeId == null || !keys.Add(activation.Key)
                    || activation.Cost != (activation.IsFirewood ? Rules.FirewoodCost : Rules.TalentCost))
                {
                    return "激活身份、实花点数或重复记录非法";
                }
                spent += activation.Cost;
            }
            return spent + TalentPoints == EarnedPoints ? null : "天赋点账目不守恒";
        }

        public PlayerGrowthState Charge(int energy)
        {
            string error = Validate();
            if (error != null) { throw new ArgumentException(error); }
            if (energy <= 0) { throw new ArgumentException("空能量瓶不能充能"); }
            long available = checked(Energy + energy);
            int low = 0;
            int high = GrowthRules.MaximumPoints - EarnedPoints;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                bool affordable;
                try { affordable = CostOf(middle) <= available; }
                catch (OverflowException) { affordable = false; }
                if (affordable) { low = middle; }
                else { high = middle - 1; }
            }
            long remainder = available - CostOf(low);
            var result = new PlayerGrowthState(Rules, remainder, checked(EarnedPoints + low),
                checked(TalentPoints + low), checked(AttributePoints + low), Strength, Dexterity, Intelligence, Activations);
            error = result.Validate();
            if (error != null) { throw new ArgumentException(error); }
            return result;
        }

        long CostOf(int additional)
        {
            long firstCost = Rules.NextCost(EarnedPoints);
            long first = additional;
            long second = additional - 1L;
            if (first % 2 == 0) { first /= 2; }
            else { second /= 2; }
            return checked(firstCost * additional + checked(first * second * Rules.CostIncrement));
        }

        public PlayerGrowthState Allocate(string statId, int count)
        {
            if (count <= 0 || count > AttributePoints) { throw new ArgumentException("属性点不足或数量非法"); }
            if (statId != StatIds.Strength && statId != StatIds.Dexterity && statId != StatIds.Intelligence)
            {
                throw new ArgumentException("只能分配力量、敏捷或智力");
            }
            var result = new PlayerGrowthState(Rules, Energy, EarnedPoints, TalentPoints, AttributePoints - count,
                checked(Strength + (statId == StatIds.Strength ? count : 0)),
                checked(Dexterity + (statId == StatIds.Dexterity ? count : 0)),
                checked(Intelligence + (statId == StatIds.Intelligence ? count : 0)), Activations);
            string error = result.Validate();
            if (error != null) { throw new ArgumentException(error); }
            return result;
        }

        public PlayerGrowthState Activate(string woodId, string nodeId)
        {
            int cost = string.IsNullOrEmpty(nodeId) ? Rules.FirewoodCost : Rules.TalentCost;
            if (TalentPoints < cost || IsActive(woodId, nodeId)) { throw new ArgumentException("天赋点不足或已经点亮"); }
            return new PlayerGrowthState(Rules, Energy, EarnedPoints, TalentPoints - cost, AttributePoints,
                Strength, Dexterity, Intelligence, Activations.Append(new GrowthActivation(woodId, nodeId, cost)));
        }

        internal PlayerGrowthState ReturnFirewoods(ISet<string> remaining)
        {
            GrowthActivation[] removed = Activations.Where(a => !remaining.Contains(a.FirewoodId)).ToArray();
            if (removed.Length == 0) { return this; }
            return new PlayerGrowthState(Rules, Energy, EarnedPoints,
                checked(TalentPoints + removed.Sum(a => a.Cost)), AttributePoints, Strength, Dexterity, Intelligence,
                Activations.Where(a => remaining.Contains(a.FirewoodId)));
        }

        internal ModifierInstance[] CreateModifiers()
        {
            var origin = new ModifierOrigin(ModifierOriginKind.Growth);
            return new[]
            {
                new ModifierInstance(StatIds.Strength, ModifierOperation.Flat, ModifierScope.GlobalActor, Strength * (float)Rules.AttributeValue, default, default, TagQuery.Empty).WithOrigin(origin),
                new ModifierInstance(StatIds.Dexterity, ModifierOperation.Flat, ModifierScope.GlobalActor, Dexterity * (float)Rules.AttributeValue, default, default, TagQuery.Empty).WithOrigin(origin),
                new ModifierInstance(StatIds.Intelligence, ModifierOperation.Flat, ModifierScope.GlobalActor, Intelligence * (float)Rules.AttributeValue, default, default, TagQuery.Empty).WithOrigin(origin)
            }.Where(modifier => modifier.Value != 0f).ToArray();
        }
    }

    public sealed class PlayerGrowthModel : AbstractModel
    {
        public string PlayerId { get; internal set; } = string.Empty;
        public PlayerGrowthState State { get; internal set; } = new PlayerGrowthState();
        protected override void OnInit() { }
    }

    public readonly struct PlayerGrowthChangedEvent
    {
        public string PlayerId { get; }
        public PlayerGrowthChangedEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }
}
