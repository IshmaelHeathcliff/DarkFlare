using System.Collections.Generic;

namespace DarkFlare
{
    public sealed class AffixInstance
    {
        readonly List<ModifierInstance> _modifiers;

        public AffixDefinition Definition { get; }

        public IReadOnlyList<ModifierInstance> Modifiers => _modifiers;

        public AffixInstance(AffixDefinition definition, IEnumerable<ModifierInstance> modifiers)
        {
            Definition = definition;
            _modifiers = new List<ModifierInstance>(modifiers);
        }
    }
}

