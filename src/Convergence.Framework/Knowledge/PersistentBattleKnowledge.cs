using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Internal;
using Convergence.Runtime;

namespace Convergence.Knowledge;

public enum BattleKnowledgeTransitionStatus
{
    Applied,
    Unchanged,
    Rejected
}

public enum BattleKnowledgeTransitionDiagnosticCode
{
    InvalidEntityId,
    InvalidAilmentId,
    DuplicateCurrentEntry,
    DuplicateDiscoveryEntry,
    DuplicateAnalyzedDefenseEntity
}

public sealed class BattleKnowledgeTransitionDiagnostic
{
    public BattleKnowledgeTransitionDiagnostic(
        BattleKnowledgeTransitionDiagnosticCode code,
        string message,
        string path)
    {
        Code = EnumDomain.RequireDefined(code, nameof(code));
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Knowledge diagnostics require a message.", nameof(message))
            : message;
        Path = string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("Knowledge diagnostics require a path.", nameof(path))
            : path;
    }

    public BattleKnowledgeTransitionDiagnosticCode Code { get; }
    public string Message { get; }
    public string Path { get; }
}

public sealed class BattleKnowledgeTransitionRequest
{
    public BattleKnowledgeTransitionRequest(
        RuntimeKnowledgeSnapshot before,
        RuntimeKnowledgeSnapshot discoveries)
    {
        Before = before ?? throw new ArgumentNullException(nameof(before));
        Discoveries = discoveries ?? throw new ArgumentNullException(nameof(discoveries));
    }

    public RuntimeKnowledgeSnapshot Before { get; }
    public RuntimeKnowledgeSnapshot Discoveries { get; }
}

public sealed class BattleKnowledgeTransitionResult
{
    private readonly IReadOnlyList<BattleKnowledgeTransitionDiagnostic> _diagnostics =
        Array.Empty<BattleKnowledgeTransitionDiagnostic>();

    public BattleKnowledgeTransitionResult(
        BattleKnowledgeTransitionStatus status,
        RuntimeKnowledgeSnapshot before,
        RuntimeKnowledgeSnapshot after,
        RuntimeKnowledgeSnapshot appliedDiscoveries,
        IEnumerable<BattleKnowledgeTransitionDiagnostic>? diagnostics = null)
    {
        Status = EnumDomain.RequireDefined(status, nameof(status));
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        AppliedDiscoveries = appliedDiscoveries ?? throw new ArgumentNullException(nameof(appliedDiscoveries));
        Diagnostics = diagnostics?.ToArray() ?? [];
    }

    public BattleKnowledgeTransitionStatus Status { get; }
    public RuntimeKnowledgeSnapshot Before { get; }
    public RuntimeKnowledgeSnapshot After { get; }
    public RuntimeKnowledgeSnapshot AppliedDiscoveries { get; }
    public IReadOnlyList<BattleKnowledgeTransitionDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init => _diagnostics = Array.AsReadOnly(value?.ToArray() ?? []);
    }

    public bool Applied => Status == BattleKnowledgeTransitionStatus.Applied;
}

public interface IPersistentBattleKnowledgeView
{
    bool TryGetElementalAffinity(
        ContentId entityId,
        DamageElement element,
        out ElementalAffinity affinity);

    bool TryGetAilmentResistance(
        ContentId entityId,
        ContentId ailmentId,
        out ResistanceLevel resistance);

    bool TryGetInstantDeathResistance(
        ContentId entityId,
        InstantDeathChannel channel,
        out ResistanceLevel resistance);

    bool IsDefenseProfileDisclosed(ContentId entityId, BattleAnalysisField field);
}

public sealed class PersistentBattleKnowledgeView : IPersistentBattleKnowledgeView
{
    private readonly IReadOnlyDictionary<(ContentId EntityId, DamageElement Element), ElementalAffinity> _elemental;
    private readonly IReadOnlyDictionary<(ContentId EntityId, ContentId AilmentId), ResistanceLevel> _ailments;
    private readonly IReadOnlyDictionary<(ContentId EntityId, InstantDeathChannel Channel), ResistanceLevel> _instantDeath;
    private readonly IReadOnlyDictionary<ContentId, IReadOnlySet<BattleAnalysisField>> _analyzedDefenses;

    public PersistentBattleKnowledgeView(RuntimeKnowledgeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        BattleKnowledgeTransitionDiagnostic[] diagnostics =
            PersistentBattleKnowledgeTransitionService.ValidateSnapshot(snapshot, isDiscovery: false).ToArray();
        if (diagnostics.Length > 0)
        {
            throw new ArgumentException(
                "Persistent battle knowledge must contain valid unique entries.",
                nameof(snapshot));
        }

        _elemental = ReadOnly(snapshot.ElementalAffinities.ToDictionary(
            entry => (entry.EntityId, entry.Element),
            entry => entry.Affinity));
        _ailments = ReadOnly(snapshot.AilmentResistances.ToDictionary(
            entry => (entry.EntityId, entry.AilmentId),
            entry => entry.Resistance));
        _instantDeath = ReadOnly(snapshot.InstantDeathResistances.ToDictionary(
            entry => (entry.EntityId, entry.Channel),
            entry => entry.Resistance));
        _analyzedDefenses = new ReadOnlyDictionary<ContentId, IReadOnlySet<BattleAnalysisField>>(
            snapshot.AnalyzedDefenses.ToDictionary(
                entry => entry.EntityId,
                entry => (IReadOnlySet<BattleAnalysisField>)new HashSet<BattleAnalysisField>(entry.DisclosedFields)));
    }

    public bool TryGetElementalAffinity(
        ContentId entityId,
        DamageElement element,
        out ElementalAffinity affinity)
    {
        RequireValidEntityId(entityId);
        EnumDomain.RequireDefined(element, nameof(element));
        if (_elemental.TryGetValue((entityId, element), out affinity))
        {
            return true;
        }

        affinity = ElementalAffinity.Normal;
        return IsDefenseProfileDisclosed(entityId, BattleAnalysisField.ElementalAffinities);
    }

    public bool TryGetAilmentResistance(
        ContentId entityId,
        ContentId ailmentId,
        out ResistanceLevel resistance)
    {
        RequireValidEntityId(entityId);
        if (!ailmentId.IsValid)
        {
            throw new ArgumentException("Knowledge queries require a valid ailment ID.", nameof(ailmentId));
        }
        if (_ailments.TryGetValue((entityId, ailmentId), out resistance))
        {
            return true;
        }

        resistance = ResistanceLevel.Normal;
        return IsDefenseProfileDisclosed(entityId, BattleAnalysisField.AilmentResistances);
    }

    public bool TryGetInstantDeathResistance(
        ContentId entityId,
        InstantDeathChannel channel,
        out ResistanceLevel resistance)
    {
        RequireValidEntityId(entityId);
        EnumDomain.RequireDefined(channel, nameof(channel));
        if (_instantDeath.TryGetValue((entityId, channel), out resistance))
        {
            return true;
        }

        resistance = ResistanceLevel.Normal;
        return IsDefenseProfileDisclosed(entityId, BattleAnalysisField.InstantDeathResistances);
    }

    public bool IsDefenseProfileDisclosed(ContentId entityId, BattleAnalysisField field)
    {
        RequireValidEntityId(entityId);
        if (!IsDefenseField(field))
        {
            throw new ArgumentOutOfRangeException(nameof(field), field, "The field is not persistent defense knowledge.");
        }

        return _analyzedDefenses.TryGetValue(entityId, out IReadOnlySet<BattleAnalysisField>? fields) &&
               fields.Contains(field);
    }

    private static void RequireValidEntityId(ContentId entityId)
    {
        if (!entityId.IsValid)
        {
            throw new ArgumentException("Knowledge queries require a valid entity ID.", nameof(entityId));
        }
    }

    private static bool IsDefenseField(BattleAnalysisField field) => field is
        BattleAnalysisField.ElementalAffinities or
        BattleAnalysisField.AilmentResistances or
        BattleAnalysisField.InstantDeathResistances;

    private static IReadOnlyDictionary<TKey, TValue> ReadOnly<TKey, TValue>(Dictionary<TKey, TValue> values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(values);
}

public interface IPersistentBattleKnowledgeTransitionService
{
    BattleKnowledgeTransitionResult Apply(BattleKnowledgeTransitionRequest request);
}

public sealed class PersistentBattleKnowledgeTransitionService : IPersistentBattleKnowledgeTransitionService
{
    public BattleKnowledgeTransitionResult Apply(BattleKnowledgeTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        BattleKnowledgeTransitionDiagnostic[] diagnostics = ValidateSnapshot(request.Before, isDiscovery: false)
            .Concat(ValidateSnapshot(request.Discoveries, isDiscovery: true))
            .ToArray();
        if (diagnostics.Length > 0)
        {
            return new BattleKnowledgeTransitionResult(
                BattleKnowledgeTransitionStatus.Rejected,
                request.Before,
                request.Before,
                new RuntimeKnowledgeSnapshot(),
                diagnostics);
        }

        Dictionary<(ContentId EntityId, DamageElement Element), ElementalAffinity> elemental =
            request.Before.ElementalAffinities.ToDictionary(
                entry => (entry.EntityId, entry.Element),
                entry => entry.Affinity);
        Dictionary<(ContentId EntityId, ContentId AilmentId), ResistanceLevel> ailments =
            request.Before.AilmentResistances.ToDictionary(
                entry => (entry.EntityId, entry.AilmentId),
                entry => entry.Resistance);
        Dictionary<(ContentId EntityId, InstantDeathChannel Channel), ResistanceLevel> instantDeath =
            request.Before.InstantDeathResistances.ToDictionary(
                entry => (entry.EntityId, entry.Channel),
                entry => entry.Resistance);
        Dictionary<ContentId, HashSet<BattleAnalysisField>> analyzedDefenses =
            request.Before.AnalyzedDefenses.ToDictionary(
                entry => entry.EntityId,
                entry => entry.DisclosedFields.ToHashSet());

        var appliedElemental = new List<RuntimeElementalAffinityKnowledgeSnapshot>();
        foreach (RuntimeElementalAffinityKnowledgeSnapshot discovery in request.Discoveries.ElementalAffinities)
        {
            if (discovery.Element == DamageElement.Almighty)
            {
                continue;
            }

            var key = (discovery.EntityId, discovery.Element);
            if (!elemental.TryGetValue(key, out ElementalAffinity current) || current != discovery.Affinity)
            {
                elemental[key] = discovery.Affinity;
                appliedElemental.Add(discovery);
            }
        }

        var appliedAilments = new List<RuntimeAilmentResistanceKnowledgeSnapshot>();
        foreach (RuntimeAilmentResistanceKnowledgeSnapshot discovery in request.Discoveries.AilmentResistances)
        {
            var key = (discovery.EntityId, discovery.AilmentId);
            if (!ailments.TryGetValue(key, out ResistanceLevel current) || current != discovery.Resistance)
            {
                ailments[key] = discovery.Resistance;
                appliedAilments.Add(discovery);
            }
        }

        var appliedInstantDeath = new List<RuntimeInstantDeathResistanceKnowledgeSnapshot>();
        foreach (RuntimeInstantDeathResistanceKnowledgeSnapshot discovery in request.Discoveries.InstantDeathResistances)
        {
            var key = (discovery.EntityId, discovery.Channel);
            if (!instantDeath.TryGetValue(key, out ResistanceLevel current) || current != discovery.Resistance)
            {
                instantDeath[key] = discovery.Resistance;
                appliedInstantDeath.Add(discovery);
            }
        }

        var appliedAnalyzedDefenses = new List<RuntimeAnalyzedDefenseKnowledgeSnapshot>();
        foreach (RuntimeAnalyzedDefenseKnowledgeSnapshot discovery in request.Discoveries.AnalyzedDefenses)
        {
            if (!analyzedDefenses.TryGetValue(discovery.EntityId, out HashSet<BattleAnalysisField>? current))
            {
                current = [];
                analyzedDefenses.Add(discovery.EntityId, current);
            }

            BattleAnalysisField[] newlyDisclosed = discovery.DisclosedFields
                .Where(current.Add)
                .Order()
                .ToArray();
            if (newlyDisclosed.Length > 0)
            {
                appliedAnalyzedDefenses.Add(
                    new RuntimeAnalyzedDefenseKnowledgeSnapshot(discovery.EntityId, newlyDisclosed));
            }
        }

        var applied = new RuntimeKnowledgeSnapshot(
            appliedElemental,
            appliedAilments,
            appliedInstantDeath,
            appliedAnalyzedDefenses);
        bool changed = appliedElemental.Count > 0 ||
                       appliedAilments.Count > 0 ||
                       appliedInstantDeath.Count > 0 ||
                       appliedAnalyzedDefenses.Count > 0;
        RuntimeKnowledgeSnapshot after = changed
            ? Snapshot(elemental, ailments, instantDeath, analyzedDefenses)
            : request.Before;
        return new BattleKnowledgeTransitionResult(
            changed ? BattleKnowledgeTransitionStatus.Applied : BattleKnowledgeTransitionStatus.Unchanged,
            request.Before,
            after,
            applied);
    }

    internal static IEnumerable<BattleKnowledgeTransitionDiagnostic> ValidateSnapshot(
        RuntimeKnowledgeSnapshot snapshot,
        bool isDiscovery)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string root = isDiscovery ? "$.discoveries" : "$.before";
        BattleKnowledgeTransitionDiagnosticCode duplicateCode = isDiscovery
            ? BattleKnowledgeTransitionDiagnosticCode.DuplicateDiscoveryEntry
            : BattleKnowledgeTransitionDiagnosticCode.DuplicateCurrentEntry;

        var elemental = new HashSet<(ContentId EntityId, DamageElement Element)>();
        for (int index = 0; index < snapshot.ElementalAffinities.Count; index++)
        {
            RuntimeElementalAffinityKnowledgeSnapshot entry = snapshot.ElementalAffinities[index];
            string path = $"{root}.elementalAffinities[{index}]";
            if (!entry.EntityId.IsValid)
            {
                yield return InvalidEntity(path);
            }
            if (!elemental.Add((entry.EntityId, entry.Element)))
            {
                yield return Duplicate(duplicateCode, path);
            }
        }

        var ailments = new HashSet<(ContentId EntityId, ContentId AilmentId)>();
        for (int index = 0; index < snapshot.AilmentResistances.Count; index++)
        {
            RuntimeAilmentResistanceKnowledgeSnapshot entry = snapshot.AilmentResistances[index];
            string path = $"{root}.ailmentResistances[{index}]";
            if (!entry.EntityId.IsValid)
            {
                yield return InvalidEntity(path);
            }
            if (!entry.AilmentId.IsValid)
            {
                yield return new BattleKnowledgeTransitionDiagnostic(
                    BattleKnowledgeTransitionDiagnosticCode.InvalidAilmentId,
                    "Knowledge ailment IDs must be valid.",
                    path + ".ailmentId");
            }
            if (!ailments.Add((entry.EntityId, entry.AilmentId)))
            {
                yield return Duplicate(duplicateCode, path);
            }
        }

        var instantDeath = new HashSet<(ContentId EntityId, InstantDeathChannel Channel)>();
        for (int index = 0; index < snapshot.InstantDeathResistances.Count; index++)
        {
            RuntimeInstantDeathResistanceKnowledgeSnapshot entry = snapshot.InstantDeathResistances[index];
            string path = $"{root}.instantDeathResistances[{index}]";
            if (!entry.EntityId.IsValid)
            {
                yield return InvalidEntity(path);
            }
            if (!instantDeath.Add((entry.EntityId, entry.Channel)))
            {
                yield return Duplicate(duplicateCode, path);
            }
        }

        var analyzedDefenseEntities = new HashSet<ContentId>();
        for (int index = 0; index < snapshot.AnalyzedDefenses.Count; index++)
        {
            RuntimeAnalyzedDefenseKnowledgeSnapshot entry = snapshot.AnalyzedDefenses[index];
            string path = $"{root}.analyzedDefenses[{index}]";
            if (!entry.EntityId.IsValid)
            {
                yield return InvalidEntity(path);
            }
            if (!analyzedDefenseEntities.Add(entry.EntityId))
            {
                yield return new BattleKnowledgeTransitionDiagnostic(
                    BattleKnowledgeTransitionDiagnosticCode.DuplicateAnalyzedDefenseEntity,
                    "Analyzed defense entries must have unique entity IDs.",
                    path);
            }
        }
    }

    private static BattleKnowledgeTransitionDiagnostic InvalidEntity(string path) =>
        new(
            BattleKnowledgeTransitionDiagnosticCode.InvalidEntityId,
            "Knowledge entity IDs must be valid.",
            path + ".entityId");

    private static BattleKnowledgeTransitionDiagnostic Duplicate(
        BattleKnowledgeTransitionDiagnosticCode code,
        string path) =>
        new(code, "Knowledge entries must have unique typed keys.", path);

    private static RuntimeKnowledgeSnapshot Snapshot(
        IReadOnlyDictionary<(ContentId EntityId, DamageElement Element), ElementalAffinity> elemental,
        IReadOnlyDictionary<(ContentId EntityId, ContentId AilmentId), ResistanceLevel> ailments,
        IReadOnlyDictionary<(ContentId EntityId, InstantDeathChannel Channel), ResistanceLevel> instantDeath,
        IReadOnlyDictionary<ContentId, HashSet<BattleAnalysisField>> analyzedDefenses) =>
        new(
            elemental.OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.Element)
                .Select(entry => new RuntimeElementalAffinityKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.Element,
                    entry.Value)),
            ailments.OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.AilmentId.ToString(), StringComparer.Ordinal)
                .Select(entry => new RuntimeAilmentResistanceKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.AilmentId,
                    entry.Value)),
            instantDeath.OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.Channel)
                .Select(entry => new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.Channel,
                    entry.Value)),
            analyzedDefenses.OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => new RuntimeAnalyzedDefenseKnowledgeSnapshot(
                    entry.Key,
                    entry.Value.Order())));
}
