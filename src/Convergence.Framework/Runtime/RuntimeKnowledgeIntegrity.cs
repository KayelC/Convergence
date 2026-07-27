using Convergence.Content;

namespace Convergence.Runtime;

internal enum RuntimeKnowledgeCollection
{
    ElementalAffinities,
    AilmentResistances,
    InstantDeathResistances,
    AnalyzedDefenses
}

internal sealed record RuntimeKnowledgeDuplicate(
    RuntimeKnowledgeCollection Collection,
    int Index,
    ContentId EntityId,
    DamageElement? Element = null,
    ContentId? AilmentId = null,
    InstantDeathChannel? DeathChannel = null)
{
    public string SavePath => Collection switch
    {
        RuntimeKnowledgeCollection.ElementalAffinities => $"$.knowledge.elementalAffinities[{Index}]",
        RuntimeKnowledgeCollection.AilmentResistances => $"$.knowledge.ailmentResistances[{Index}]",
        RuntimeKnowledgeCollection.InstantDeathResistances => $"$.knowledge.instantDeathResistances[{Index}]",
        RuntimeKnowledgeCollection.AnalyzedDefenses => $"$.knowledge.analyzedDefenses[{Index}]",
        _ => throw new InvalidOperationException($"Unsupported knowledge collection '{Collection}'.")
    };

    public string KeyDescription => Collection switch
    {
        RuntimeKnowledgeCollection.ElementalAffinities =>
            $"entity '{EntityId}' and element '{Element}'",
        RuntimeKnowledgeCollection.AilmentResistances =>
            $"entity '{EntityId}' and ailment '{AilmentId}'",
        RuntimeKnowledgeCollection.InstantDeathResistances =>
            $"entity '{EntityId}' and instant-death channel '{DeathChannel}'",
        RuntimeKnowledgeCollection.AnalyzedDefenses => $"entity '{EntityId}'",
        _ => throw new InvalidOperationException($"Unsupported knowledge collection '{Collection}'.")
    };
}

internal static class RuntimeKnowledgeIntegrity
{
    public static IReadOnlyList<RuntimeKnowledgeDuplicate> FindDuplicates(
        RuntimeKnowledgeSnapshot knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);

        var duplicates = new List<RuntimeKnowledgeDuplicate>();
        var elementalKeys = new HashSet<(ContentId EntityId, DamageElement Element)>();
        for (int index = 0; index < knowledge.ElementalAffinities.Count; index++)
        {
            RuntimeElementalAffinityKnowledgeSnapshot entry = knowledge.ElementalAffinities[index];
            if (!elementalKeys.Add((entry.EntityId, entry.Element)))
            {
                duplicates.Add(new RuntimeKnowledgeDuplicate(
                    RuntimeKnowledgeCollection.ElementalAffinities,
                    index,
                    entry.EntityId,
                    Element: entry.Element));
            }
        }

        var ailmentKeys = new HashSet<(ContentId EntityId, ContentId AilmentId)>();
        for (int index = 0; index < knowledge.AilmentResistances.Count; index++)
        {
            RuntimeAilmentResistanceKnowledgeSnapshot entry = knowledge.AilmentResistances[index];
            if (!ailmentKeys.Add((entry.EntityId, entry.AilmentId)))
            {
                duplicates.Add(new RuntimeKnowledgeDuplicate(
                    RuntimeKnowledgeCollection.AilmentResistances,
                    index,
                    entry.EntityId,
                    AilmentId: entry.AilmentId));
            }
        }

        var instantDeathKeys = new HashSet<(ContentId EntityId, InstantDeathChannel Channel)>();
        for (int index = 0; index < knowledge.InstantDeathResistances.Count; index++)
        {
            RuntimeInstantDeathResistanceKnowledgeSnapshot entry = knowledge.InstantDeathResistances[index];
            if (!instantDeathKeys.Add((entry.EntityId, entry.Channel)))
            {
                duplicates.Add(new RuntimeKnowledgeDuplicate(
                    RuntimeKnowledgeCollection.InstantDeathResistances,
                    index,
                    entry.EntityId,
                    DeathChannel: entry.Channel));
            }
        }

        var analyzedDefenseKeys = new HashSet<ContentId>();
        for (int index = 0; index < knowledge.AnalyzedDefenses.Count; index++)
        {
            RuntimeAnalyzedDefenseKnowledgeSnapshot entry = knowledge.AnalyzedDefenses[index];
            if (!analyzedDefenseKeys.Add(entry.EntityId))
            {
                duplicates.Add(new RuntimeKnowledgeDuplicate(
                    RuntimeKnowledgeCollection.AnalyzedDefenses,
                    index,
                    entry.EntityId));
            }
        }

        return Array.AsReadOnly(duplicates.ToArray());
    }
}
