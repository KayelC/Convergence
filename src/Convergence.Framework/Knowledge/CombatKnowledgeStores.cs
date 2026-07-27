using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Internal;

namespace Convergence.Knowledge;

public readonly record struct ElementalAffinityKnowledgeKey(ContentId EntityId, DamageElement Element);
public readonly record struct AilmentResistanceKnowledgeKey(ContentId EntityId, ContentId AilmentId);
public readonly record struct InstantDeathResistanceKnowledgeKey(ContentId EntityId, InstantDeathChannel Channel);

public sealed class ElementalAffinityKnowledge
{
    private readonly Dictionary<ElementalAffinityKnowledgeKey, ElementalAffinity> _entries = new();

    public void Learn(ContentId entityId, DamageElement element, ElementalAffinity affinity)
    {
        RequireValid(entityId, nameof(entityId));
        EnumDomain.RequireDefined(element, nameof(element));
        EnumDomain.RequireDefined(affinity, nameof(affinity));
        if (element == DamageElement.Almighty)
        {
            return;
        }

        _entries[new ElementalAffinityKnowledgeKey(entityId, element)] = affinity;
    }

    public bool TryGet(
        ContentId entityId,
        DamageElement element,
        out ElementalAffinity affinity)
    {
        return _entries.TryGetValue(new ElementalAffinityKnowledgeKey(entityId, element), out affinity);
    }

    public bool HasDiscovery(ContentId entityId, DamageElement element)
    {
        return _entries.ContainsKey(new ElementalAffinityKnowledgeKey(entityId, element));
    }

    public IReadOnlyDictionary<ElementalAffinityKnowledgeKey, ElementalAffinity> Snapshot()
    {
        return Snapshot(_entries);
    }

    private static IReadOnlyDictionary<TKey, TValue> Snapshot<TKey, TValue>(Dictionary<TKey, TValue> entries)
        where TKey : notnull
    {
        return new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(entries));
    }

    private static void RequireValid(ContentId id, string parameterName)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("Knowledge entity IDs must be valid.", parameterName);
        }
    }
}

public sealed class AilmentResistanceKnowledge
{
    private readonly Dictionary<AilmentResistanceKnowledgeKey, ResistanceLevel> _entries = new();

    public void Learn(ContentId entityId, ContentId ailmentId, ResistanceLevel resistance)
    {
        RequireValid(entityId, nameof(entityId), "Knowledge entity IDs must be valid.");
        RequireValid(ailmentId, nameof(ailmentId), "Knowledge ailment IDs must be valid.");
        EnumDomain.RequireDefined(resistance, nameof(resistance));
        _entries[new AilmentResistanceKnowledgeKey(entityId, ailmentId)] = resistance;
    }

    public bool TryGet(ContentId entityId, ContentId ailmentId, out ResistanceLevel resistance)
    {
        return _entries.TryGetValue(new AilmentResistanceKnowledgeKey(entityId, ailmentId), out resistance);
    }

    public bool HasDiscovery(ContentId entityId, ContentId ailmentId)
    {
        return _entries.ContainsKey(new AilmentResistanceKnowledgeKey(entityId, ailmentId));
    }

    public IReadOnlyDictionary<AilmentResistanceKnowledgeKey, ResistanceLevel> Snapshot()
    {
        return new ReadOnlyDictionary<AilmentResistanceKnowledgeKey, ResistanceLevel>(
            new Dictionary<AilmentResistanceKnowledgeKey, ResistanceLevel>(_entries));
    }

    private static void RequireValid(ContentId id, string parameterName, string message)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}

public sealed class InstantDeathResistanceKnowledge
{
    private readonly Dictionary<InstantDeathResistanceKnowledgeKey, ResistanceLevel> _entries = new();

    public void Learn(ContentId entityId, InstantDeathChannel channel, ResistanceLevel resistance)
    {
        if (!entityId.IsValid)
        {
            throw new ArgumentException("Knowledge entity IDs must be valid.", nameof(entityId));
        }

        EnumDomain.RequireDefined(channel, nameof(channel));
        EnumDomain.RequireDefined(resistance, nameof(resistance));
        _entries[new InstantDeathResistanceKnowledgeKey(entityId, channel)] = resistance;
    }

    public bool TryGet(ContentId entityId, InstantDeathChannel channel, out ResistanceLevel resistance)
    {
        return _entries.TryGetValue(new InstantDeathResistanceKnowledgeKey(entityId, channel), out resistance);
    }

    public bool HasDiscovery(ContentId entityId, InstantDeathChannel channel)
    {
        return _entries.ContainsKey(new InstantDeathResistanceKnowledgeKey(entityId, channel));
    }

    public IReadOnlyDictionary<InstantDeathResistanceKnowledgeKey, ResistanceLevel> Snapshot()
    {
        return new ReadOnlyDictionary<InstantDeathResistanceKnowledgeKey, ResistanceLevel>(
            new Dictionary<InstantDeathResistanceKnowledgeKey, ResistanceLevel>(_entries));
    }
}
