using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class MonsterAffixInstance
    {
        readonly IReadOnlyList<ModifierInstance> _modifiers;

        public MonsterAffixDefinition Definition { get; }

        public int ValueSeed { get; }

        public IReadOnlyList<ModifierInstance> Modifiers => _modifiers;

        public MonsterAffixInstance(
            MonsterAffixDefinition definition,
            int valueSeed,
            IEnumerable<ModifierInstance> modifiers)
        {
            Definition = definition;
            ValueSeed = valueSeed;
            List<ModifierInstance> copiedModifiers = modifiers != null
                ? new List<ModifierInstance>(modifiers)
                : new List<ModifierInstance>();
            _modifiers = copiedModifiers.AsReadOnly();
        }
    }
}
