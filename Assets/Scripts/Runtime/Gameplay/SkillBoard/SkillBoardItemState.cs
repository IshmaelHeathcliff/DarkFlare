using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class SkillBoardTalentState
    {
        public string NodeId { get; }
        public int Radius { get; }
        public bool IsGlobal => Radius == 0;
        public IReadOnlyList<ModifierInstance> Modifiers { get; }

        public SkillBoardTalentState(string nodeId, int radius, IEnumerable<ModifierInstance> modifiers)
        {
            NodeId = nodeId;
            Radius = radius;
            Modifiers = Array.AsReadOnly((modifiers ?? throw new ArgumentNullException(nameof(modifiers))).ToArray());
        }
    }

    public sealed class SkillBoardItemState
    {
        public FirewoodLayout Layout { get; }
        public IReadOnlyList<SkillBoardTalentState> Talents { get; }
        public ProjectileSkillDefinition FlameSkill { get; }
        public int EmberRadius { get; }
        public int Energy { get; private set; }
        double _cooldownEnd;
        float? _preparedCooldown;
        public float CooldownRemainingSeconds => _preparedCooldown ?? (float)Math.Max(0, _cooldownEnd - UnityEngine.Time.timeAsDouble);

        internal void FreezePreparedCooldown() { _preparedCooldown = CooldownRemainingSeconds; }
        internal void StartPreparedCooldown()
        {
            if (_preparedCooldown.HasValue) { RestoreCooldown(_preparedCooldown.Value); }
        }

        internal void RestoreCooldown(float remaining)
        {
            if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0 || remaining > 0 && FlameSkill == null)
            {
                throw new ArgumentException("火焰冷却非法");
            }
            _cooldownEnd = UnityEngine.Time.timeAsDouble + remaining;
            _preparedCooldown = null;
        }

        public SkillBoardItemState(FirewoodLayout layout = null, IEnumerable<SkillBoardTalentState> talents = null,
            ProjectileSkillDefinition flameSkill = null, int emberRadius = 0, int energy = 0)
        {
            Layout = layout;
            Talents = Array.AsReadOnly((talents ?? Array.Empty<SkillBoardTalentState>()).ToArray());
            FlameSkill = flameSkill;
            EmberRadius = emberRadius;
            Energy = energy;
        }

        internal void AddEnergy(int amount)
        {
            Energy = checked(Energy + amount);
        }

        public bool IsValidFor(ItemBaseDefinition definition)
        {
            if (definition == null || !definition.IsSkillBoardItem || definition.SkillBoard == null) { return false; }
            if (definition.ItemType != ItemType.Firewood && (Layout != null || Talents.Count != 0)) { return false; }
            if (definition.ItemType != ItemType.Flame && FlameSkill != null) { return false; }
            if (definition.ItemType != ItemType.Ember && EmberRadius != 0) { return false; }
            if (definition.ItemType != ItemType.EnergyBottle && Energy != 0) { return false; }
            switch (definition.ItemType)
            {
                case ItemType.Firewood:
                    if (Layout == null || definition.SkillBoard.Firewood == null
                        || Layout.Size != definition.SkillBoard.Firewood.Size || Layout.Validate().Count != 0) { return false; }
                    var ids = new HashSet<string>(Layout.Nodes.Where(n => n.Kind == FirewoodNodeKind.Talent).Select(n => n.Id));
                    foreach (SkillBoardTalentState talent in Talents)
                    {
                        if (talent == null || !ids.Remove(talent.NodeId) || talent.Radius < 0
                            || talent.Modifiers.Count == 0 || talent.Modifiers.Any(m => m == null)) { return false; }
                    }
                    return ids.Count == 0;
                case ItemType.Flame: return FlameSkill != null;
                case ItemType.Ember: return EmberRadius > 0;
                case ItemType.EnergyBottle: return definition.SkillBoard.EnergyCapacity > 0 && Energy >= 0 && Energy <= definition.SkillBoard.EnergyCapacity;
                default: return false;
            }
        }
    }
}
