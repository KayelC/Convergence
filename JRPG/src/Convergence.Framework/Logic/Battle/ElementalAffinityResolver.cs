using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities.Components;

namespace JRPGPrototype.Logic.Battle;

public static class ElementalAffinityResolver
{
    public static ElementalAffinity Resolve(
        CombatDefenseProfile defenseProfile,
        DamageElement element,
        IEnumerable<ElementalAffinity>? passiveReplacements = null,
        IEnumerable<ShieldKind>? activeShields = null,
        bool isBroken = false,
        ElementalAffinity? activeOverride = null)
    {
        ArgumentNullException.ThrowIfNull(defenseProfile);

        if (element == DamageElement.Almighty)
        {
            return ElementalAffinity.Normal;
        }

        if (HasMatchingShield(element, activeShields))
        {
            return ElementalAffinity.Repel;
        }

        if (isBroken)
        {
            return ElementalAffinity.Normal;
        }

        if (activeOverride is ElementalAffinity overrideAffinity)
        {
            return overrideAffinity;
        }

        ElementalAffinity resolved = defenseProfile.GetElementalAffinity(element);
        if (passiveReplacements is null)
        {
            return resolved;
        }

        foreach (ElementalAffinity replacement in passiveReplacements)
        {
            if (GetPrecedence(replacement) > GetPrecedence(resolved))
            {
                resolved = replacement;
            }
        }

        return resolved;
    }

    private static bool HasMatchingShield(DamageElement element, IEnumerable<ShieldKind>? activeShields)
    {
        if (activeShields is null)
        {
            return false;
        }

        ShieldKind expected = element == DamageElement.Physical
            ? ShieldKind.Physical
            : ShieldKind.Magical;

        return activeShields.Contains(expected);
    }

    private static int GetPrecedence(ElementalAffinity affinity)
    {
        return affinity switch
        {
            ElementalAffinity.Weak => 0,
            ElementalAffinity.Normal => 1,
            ElementalAffinity.Resist => 2,
            ElementalAffinity.Null => 3,
            ElementalAffinity.Repel => 4,
            ElementalAffinity.Absorb => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(affinity), affinity, "Unsupported elemental affinity.")
        };
    }
}
