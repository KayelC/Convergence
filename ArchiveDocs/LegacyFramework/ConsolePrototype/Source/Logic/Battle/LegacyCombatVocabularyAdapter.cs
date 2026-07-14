using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Battle;

public static class LegacyCombatVocabularyAdapter
{
    public static bool TryToDamageElement(Element legacyElement, out DamageElement element)
    {
        switch (legacyElement)
        {
            case Element.Slash:
            case Element.Strike:
            case Element.Pierce:
                element = DamageElement.Physical;
                return true;
            case Element.Fire:
                element = DamageElement.Fire;
                return true;
            case Element.Ice:
                element = DamageElement.Ice;
                return true;
            case Element.Elec:
                element = DamageElement.Electric;
                return true;
            case Element.Wind:
                element = DamageElement.Wind;
                return true;
            case Element.Light:
                element = DamageElement.Light;
                return true;
            case Element.Dark:
                element = DamageElement.Dark;
                return true;
            case Element.Almighty:
                element = DamageElement.Almighty;
                return true;
            default:
                element = default;
                return false;
        }
    }

    public static ElementalAffinity ToElementalAffinity(Affinity legacyAffinity)
    {
        return legacyAffinity switch
        {
            Affinity.Weak => ElementalAffinity.Weak,
            Affinity.Normal => ElementalAffinity.Normal,
            Affinity.Resist => ElementalAffinity.Resist,
            Affinity.Null => ElementalAffinity.Null,
            Affinity.Repel => ElementalAffinity.Repel,
            Affinity.Absorb => ElementalAffinity.Absorb,
            _ => throw new ArgumentOutOfRangeException(nameof(legacyAffinity), legacyAffinity, "Unsupported legacy affinity.")
        };
    }

    public static Affinity ToLegacyAffinity(ElementalAffinity affinity)
    {
        return affinity switch
        {
            ElementalAffinity.Weak => Affinity.Weak,
            ElementalAffinity.Normal => Affinity.Normal,
            ElementalAffinity.Resist => Affinity.Resist,
            ElementalAffinity.Null => Affinity.Null,
            ElementalAffinity.Repel => Affinity.Repel,
            ElementalAffinity.Absorb => Affinity.Absorb,
            _ => throw new ArgumentOutOfRangeException(nameof(affinity), affinity, "Unsupported elemental affinity.")
        };
    }
}
