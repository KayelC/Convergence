using System.Collections.ObjectModel;
using Convergence.Content;

namespace Convergence.Battle;

public sealed class CombatDefenseProfile
{
    public static CombatDefenseProfile Empty { get; } = new();

    public CombatDefenseProfile(
        IEnumerable<KeyValuePair<DamageElement, ElementalAffinity>>? elementalAffinities = null,
        IEnumerable<KeyValuePair<ContentId, ResistanceLevel>>? ailmentResistances = null,
        IEnumerable<KeyValuePair<InstantDeathChannel, ResistanceLevel>>? instantDeathResistances = null)
    {
        ElementalAffinities = Snapshot(elementalAffinities);
        AilmentResistances = Snapshot(ailmentResistances);
        InstantDeathResistances = Snapshot(instantDeathResistances);
    }

    public IReadOnlyDictionary<DamageElement, ElementalAffinity> ElementalAffinities { get; }
    public IReadOnlyDictionary<ContentId, ResistanceLevel> AilmentResistances { get; }
    public IReadOnlyDictionary<InstantDeathChannel, ResistanceLevel> InstantDeathResistances { get; }

    public static CombatDefenseProfile FromEntityDefinition(EntityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new CombatDefenseProfile(
            definition.ElementalAffinities,
            definition.AilmentResistances,
            definition.InstantDeathResistances);
    }

    public ElementalAffinity GetElementalAffinity(DamageElement element)
    {
        if (element == DamageElement.Almighty)
        {
            return ElementalAffinity.Normal;
        }

        return ElementalAffinities.TryGetValue(element, out ElementalAffinity affinity)
            ? affinity
            : ElementalAffinity.Normal;
    }

    public ResistanceLevel GetAilmentResistance(ContentId ailmentId)
    {
        return AilmentResistances.TryGetValue(ailmentId, out ResistanceLevel resistance)
            ? resistance
            : ResistanceLevel.Normal;
    }

    public ResistanceLevel GetInstantDeathResistance(InstantDeathChannel channel)
    {
        return InstantDeathResistances.TryGetValue(channel, out ResistanceLevel resistance)
            ? resistance
            : ResistanceLevel.Normal;
    }

    private static IReadOnlyDictionary<TKey, TValue> Snapshot<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values)
        where TKey : notnull
    {
        var copy = new Dictionary<TKey, TValue>();
        if (values is not null)
        {
            foreach ((TKey key, TValue value) in values)
            {
                copy.Add(key, value);
            }
        }

        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }
}
