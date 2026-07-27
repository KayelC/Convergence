using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Knowledge;

public enum BattleKnowledgePersistenceScope
{
    EncounterOnly,
    EncounterAndPersistent
}

public enum BattleKnowledgeFactSource
{
    Encounter,
    Persistent
}

public enum BattleKnowledgeObservationDiagnosticCode
{
    TargetIdentityConflict,
    PersistentTransitionRejected
}

/// <summary>Identifies encounter-only information that Analyze may make visible.</summary>
public enum BattleAnalysisField
{
    CurrentHp,
    CurrentSp,
    CoreStats,
    Skills,
    ElementalAffinities,
    AilmentResistances,
    InstantDeathResistances
}

public sealed class EncounterAnalysisKnowledgeEntry
{
    public EncounterAnalysisKnowledgeEntry(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        IEnumerable<BattleAnalysisField> disclosedFields)
    {
        if (!targetInstanceId.IsValid || !targetEntityId.IsValid)
        {
            throw new ArgumentException("Encounter analysis requires valid target IDs.");
        }
        BattleAnalysisField[] fields =
            (disclosedFields ?? throw new ArgumentNullException(nameof(disclosedFields))).ToArray();
        if (fields.Length == 0 || fields.Any(field => !Enum.IsDefined(field)) ||
            fields.Distinct().Count() != fields.Length)
        {
            throw new ArgumentException(
                "Encounter analysis requires at least one unique defined field.",
                nameof(disclosedFields));
        }

        TargetInstanceId = targetInstanceId;
        TargetEntityId = targetEntityId;
        DisclosedFields = Array.AsReadOnly(fields.Order().ToArray());
    }

    public RuntimeInstanceId TargetInstanceId { get; }
    public ContentId TargetEntityId { get; }
    public IReadOnlyList<BattleAnalysisField> DisclosedFields { get; }
}

public sealed record EncounterElementalKnowledgeEntry
{
    public EncounterElementalKnowledgeEntry(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        DamageElement element,
        ElementalAffinity affinity,
        BattleDefenseInfluence temporaryInfluences = BattleDefenseInfluence.None)
    {
        RequireIdentity(targetInstanceId, targetEntityId);
        RequireDefined(element, nameof(element));
        RequireDefined(affinity, nameof(affinity));
        TargetInstanceId = targetInstanceId;
        TargetEntityId = targetEntityId;
        Element = element;
        Affinity = affinity;
        TemporaryInfluences = RequireInfluences(temporaryInfluences);
    }

    public RuntimeInstanceId TargetInstanceId { get; }
    public ContentId TargetEntityId { get; }
    public DamageElement Element { get; }
    public ElementalAffinity Affinity { get; }
    public BattleDefenseInfluence TemporaryInfluences { get; }

    private static void RequireIdentity(RuntimeInstanceId instanceId, ContentId entityId)
    {
        if (!instanceId.IsValid || !entityId.IsValid)
        {
            throw new ArgumentException("Encounter knowledge requires valid runtime and entity IDs.");
        }
    }

    private static void RequireDefined<T>(T value, string name) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Encounter knowledge values must be defined.");
        }
    }

    private static BattleDefenseInfluence RequireInfluences(BattleDefenseInfluence value)
    {
        const BattleDefenseInfluence all = BattleDefenseInfluence.Guard |
            BattleDefenseInfluence.Shield |
            BattleDefenseInfluence.AffinityBreak |
            BattleDefenseInfluence.AffinityOverride |
            BattleDefenseInfluence.PassiveModifier;
        if ((value & ~all) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }
}

public sealed record EncounterAilmentKnowledgeEntry
{
    public EncounterAilmentKnowledgeEntry(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        ContentId ailmentId,
        ResistanceLevel resistance,
        BattleDefenseInfluence temporaryInfluences = BattleDefenseInfluence.None)
    {
        if (!targetInstanceId.IsValid || !targetEntityId.IsValid || !ailmentId.IsValid)
        {
            throw new ArgumentException("Encounter ailment knowledge requires valid IDs.");
        }
        if (!Enum.IsDefined(resistance))
        {
            throw new ArgumentOutOfRangeException(nameof(resistance));
        }

        TargetInstanceId = targetInstanceId;
        TargetEntityId = targetEntityId;
        AilmentId = ailmentId;
        Resistance = resistance;
        TemporaryInfluences = RequireInfluences(temporaryInfluences);
    }

    public RuntimeInstanceId TargetInstanceId { get; }
    public ContentId TargetEntityId { get; }
    public ContentId AilmentId { get; }
    public ResistanceLevel Resistance { get; }
    public BattleDefenseInfluence TemporaryInfluences { get; }

    private static BattleDefenseInfluence RequireInfluences(BattleDefenseInfluence value)
    {
        const BattleDefenseInfluence all = BattleDefenseInfluence.Guard |
            BattleDefenseInfluence.Shield |
            BattleDefenseInfluence.AffinityBreak |
            BattleDefenseInfluence.AffinityOverride |
            BattleDefenseInfluence.PassiveModifier;
        return (value & ~all) == 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public sealed record EncounterInstantDeathKnowledgeEntry
{
    public EncounterInstantDeathKnowledgeEntry(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        InstantDeathChannel channel,
        ResistanceLevel resistance,
        BattleDefenseInfluence temporaryInfluences = BattleDefenseInfluence.None)
    {
        if (!targetInstanceId.IsValid || !targetEntityId.IsValid)
        {
            throw new ArgumentException("Encounter instant-death knowledge requires valid IDs.");
        }
        if (!Enum.IsDefined(channel) || !Enum.IsDefined(resistance))
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        TargetInstanceId = targetInstanceId;
        TargetEntityId = targetEntityId;
        Channel = channel;
        Resistance = resistance;
        TemporaryInfluences = RequireInfluences(temporaryInfluences);
    }

    public RuntimeInstanceId TargetInstanceId { get; }
    public ContentId TargetEntityId { get; }
    public InstantDeathChannel Channel { get; }
    public ResistanceLevel Resistance { get; }
    public BattleDefenseInfluence TemporaryInfluences { get; }

    private static BattleDefenseInfluence RequireInfluences(BattleDefenseInfluence value)
    {
        const BattleDefenseInfluence all = BattleDefenseInfluence.Guard |
            BattleDefenseInfluence.Shield |
            BattleDefenseInfluence.AffinityBreak |
            BattleDefenseInfluence.AffinityOverride |
            BattleDefenseInfluence.PassiveModifier;
        return (value & ~all) == 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public sealed class RuntimeEncounterKnowledgeSnapshot
{
    public static RuntimeEncounterKnowledgeSnapshot Empty { get; } = new();

    public RuntimeEncounterKnowledgeSnapshot(
        IEnumerable<EncounterElementalKnowledgeEntry>? elemental = null,
        IEnumerable<EncounterAilmentKnowledgeEntry>? ailments = null,
        IEnumerable<EncounterInstantDeathKnowledgeEntry>? instantDeath = null,
        IEnumerable<EncounterAnalysisKnowledgeEntry>? analysis = null)
    {
        EncounterElementalKnowledgeEntry[] elementalSnapshot = Snapshot(elemental, nameof(elemental));
        EncounterAilmentKnowledgeEntry[] ailmentSnapshot = Snapshot(ailments, nameof(ailments));
        EncounterInstantDeathKnowledgeEntry[] instantDeathSnapshot = Snapshot(instantDeath, nameof(instantDeath));
        EncounterAnalysisKnowledgeEntry[] analysisSnapshot = Snapshot(analysis, nameof(analysis));
        RequireUnique(elementalSnapshot, entry => (entry.TargetInstanceId, entry.Element), nameof(elemental));
        RequireUnique(ailmentSnapshot, entry => (entry.TargetInstanceId, entry.AilmentId), nameof(ailments));
        RequireUnique(instantDeathSnapshot, entry => (entry.TargetInstanceId, entry.Channel), nameof(instantDeath));
        RequireUnique(analysisSnapshot, entry => entry.TargetInstanceId, nameof(analysis));
        RequireConsistentTargetIdentities(
            elementalSnapshot.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId))
                .Concat(ailmentSnapshot.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
                .Concat(instantDeathSnapshot.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
                .Concat(analysisSnapshot.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId))));

        Elemental = Array.AsReadOnly(elementalSnapshot.OrderBy(entry => $"{entry.TargetInstanceId}|{entry.Element}", StringComparer.Ordinal).ToArray());
        Ailments = Array.AsReadOnly(ailmentSnapshot.OrderBy(entry => $"{entry.TargetInstanceId}|{entry.AilmentId}", StringComparer.Ordinal).ToArray());
        InstantDeath = Array.AsReadOnly(instantDeathSnapshot.OrderBy(entry => $"{entry.TargetInstanceId}|{entry.Channel}", StringComparer.Ordinal).ToArray());
        Analysis = Array.AsReadOnly(analysisSnapshot.OrderBy(entry => entry.TargetInstanceId.ToString(), StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<EncounterElementalKnowledgeEntry> Elemental { get; }
    public IReadOnlyList<EncounterAilmentKnowledgeEntry> Ailments { get; }
    public IReadOnlyList<EncounterInstantDeathKnowledgeEntry> InstantDeath { get; }
    public IReadOnlyList<EncounterAnalysisKnowledgeEntry> Analysis { get; }
    public bool IsEmpty => Elemental.Count == 0 && Ailments.Count == 0 && InstantDeath.Count == 0 && Analysis.Count == 0;

    private static T[] Snapshot<T>(IEnumerable<T>? source, string name) where T : class
    {
        T[] snapshot = source?.ToArray() ?? [];
        if (snapshot.Any(entry => entry is null))
        {
            throw new ArgumentException("Encounter knowledge cannot contain null entries.", name);
        }

        return snapshot;
    }

    private static void RequireUnique<TEntry, TKey>(
        IEnumerable<TEntry> entries,
        Func<TEntry, TKey> key,
        string name) where TKey : notnull
    {
        if (entries.Select(key).Distinct().Count() != entries.Count())
        {
            throw new ArgumentException("Encounter knowledge cannot contain duplicate fact keys.", name);
        }
    }

    private static void RequireConsistentTargetIdentities(
        IEnumerable<(RuntimeInstanceId InstanceId, ContentId EntityId)> identities)
    {
        bool conflict = identities
            .GroupBy(identity => identity.InstanceId)
            .Any(group => group.Select(identity => identity.EntityId).Distinct().Skip(1).Any());
        if (conflict)
        {
            throw new ArgumentException(
                "One runtime target cannot identify multiple entity definitions in encounter knowledge.");
        }
    }
}

public interface IEncounterBattleKnowledgeView
{
    bool TryGetElementalAffinity(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        DamageElement element,
        out ElementalAffinity affinity,
        out BattleDefenseInfluence temporaryInfluences);

    bool TryGetAilmentResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        ContentId ailmentId,
        out ResistanceLevel resistance,
        out BattleDefenseInfluence temporaryInfluences);

    bool TryGetInstantDeathResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        InstantDeathChannel channel,
        out ResistanceLevel resistance,
        out BattleDefenseInfluence temporaryInfluences);

    bool IsAnalysisDisclosed(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        BattleAnalysisField field);
}

public sealed class EncounterBattleKnowledgeView : IEncounterBattleKnowledgeView
{
    private readonly IReadOnlyDictionary<(RuntimeInstanceId, DamageElement), EncounterElementalKnowledgeEntry> _elemental;
    private readonly IReadOnlyDictionary<(RuntimeInstanceId, ContentId), EncounterAilmentKnowledgeEntry> _ailments;
    private readonly IReadOnlyDictionary<(RuntimeInstanceId, InstantDeathChannel), EncounterInstantDeathKnowledgeEntry> _instantDeath;
    private readonly IReadOnlyDictionary<RuntimeInstanceId, ContentId> _targetEntities;
    private readonly IReadOnlyDictionary<RuntimeInstanceId, EncounterAnalysisKnowledgeEntry> _analysis;

    public EncounterBattleKnowledgeView(RuntimeEncounterKnowledgeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _elemental = snapshot.Elemental.ToDictionary(
            entry => (entry.TargetInstanceId, entry.Element),
            entry => entry);
        _ailments = snapshot.Ailments.ToDictionary(
            entry => (entry.TargetInstanceId, entry.AilmentId),
            entry => entry);
        _instantDeath = snapshot.InstantDeath.ToDictionary(
            entry => (entry.TargetInstanceId, entry.Channel),
            entry => entry);
        _targetEntities = snapshot.Elemental.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId))
            .Concat(snapshot.Ailments.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
            .Concat(snapshot.InstantDeath.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
            .Concat(snapshot.Analysis.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
            .Distinct()
            .ToDictionary(pair => pair.TargetInstanceId, pair => pair.TargetEntityId);
        _analysis = snapshot.Analysis.ToDictionary(entry => entry.TargetInstanceId);
    }

    public bool TryGetElementalAffinity(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        DamageElement element,
        out ElementalAffinity affinity,
        out BattleDefenseInfluence temporaryInfluences)
    {
        RequireQuery(targetInstanceId, targetEntityId);
        RequireDefined(element, nameof(element));
        if (_elemental.TryGetValue((targetInstanceId, element), out EncounterElementalKnowledgeEntry? entry))
        {
            affinity = entry.Affinity;
            temporaryInfluences = entry.TemporaryInfluences;
            return true;
        }

        affinity = default;
        temporaryInfluences = BattleDefenseInfluence.None;
        if (IsAnalysisDisclosed(
                targetInstanceId,
                targetEntityId,
                BattleAnalysisField.ElementalAffinities))
        {
            affinity = ElementalAffinity.Normal;
            return true;
        }
        return false;
    }

    public bool TryGetAilmentResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        ContentId ailmentId,
        out ResistanceLevel resistance,
        out BattleDefenseInfluence temporaryInfluences)
    {
        RequireQuery(targetInstanceId, targetEntityId);
        if (!ailmentId.IsValid)
        {
            throw new ArgumentException("Ailment ID must be valid.", nameof(ailmentId));
        }
        if (_ailments.TryGetValue((targetInstanceId, ailmentId), out EncounterAilmentKnowledgeEntry? entry))
        {
            resistance = entry.Resistance;
            temporaryInfluences = entry.TemporaryInfluences;
            return true;
        }

        resistance = default;
        temporaryInfluences = BattleDefenseInfluence.None;
        if (IsAnalysisDisclosed(
                targetInstanceId,
                targetEntityId,
                BattleAnalysisField.AilmentResistances))
        {
            resistance = ResistanceLevel.Normal;
            return true;
        }
        return false;
    }

    public bool TryGetInstantDeathResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        InstantDeathChannel channel,
        out ResistanceLevel resistance,
        out BattleDefenseInfluence temporaryInfluences)
    {
        RequireQuery(targetInstanceId, targetEntityId);
        RequireDefined(channel, nameof(channel));
        if (_instantDeath.TryGetValue((targetInstanceId, channel), out EncounterInstantDeathKnowledgeEntry? entry))
        {
            resistance = entry.Resistance;
            temporaryInfluences = entry.TemporaryInfluences;
            return true;
        }

        resistance = default;
        temporaryInfluences = BattleDefenseInfluence.None;
        if (IsAnalysisDisclosed(
                targetInstanceId,
                targetEntityId,
                BattleAnalysisField.InstantDeathResistances))
        {
            resistance = ResistanceLevel.Normal;
            return true;
        }
        return false;
    }

    public bool IsAnalysisDisclosed(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        BattleAnalysisField field)
    {
        RequireQuery(targetInstanceId, targetEntityId);
        RequireDefined(field, nameof(field));
        return _analysis.TryGetValue(targetInstanceId, out EncounterAnalysisKnowledgeEntry? entry) &&
               entry.DisclosedFields.Contains(field);
    }

    private void RequireQuery(RuntimeInstanceId targetInstanceId, ContentId targetEntityId)
    {
        if (!targetInstanceId.IsValid || !targetEntityId.IsValid)
        {
            throw new ArgumentException("Knowledge queries require valid target runtime and entity IDs.");
        }
        if (_targetEntities.TryGetValue(targetInstanceId, out ContentId knownEntityId) &&
            knownEntityId != targetEntityId)
        {
            throw new InvalidOperationException(
                $"Target '{targetInstanceId}' belongs to entity '{knownEntityId}', not '{targetEntityId}'.");
        }
    }

    private static void RequireDefined<T>(T value, string name) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Knowledge query values must be defined.");
        }
    }
}

public interface IBattleKnowledgeView
{
    bool TryGetElementalAffinity(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        DamageElement element,
        out ElementalAffinity affinity,
        out BattleKnowledgeFactSource source,
        out BattleDefenseInfluence temporaryInfluences);

    bool TryGetAilmentResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        ContentId ailmentId,
        out ResistanceLevel resistance,
        out BattleKnowledgeFactSource source,
        out BattleDefenseInfluence temporaryInfluences);

    bool TryGetInstantDeathResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        InstantDeathChannel channel,
        out ResistanceLevel resistance,
        out BattleKnowledgeFactSource source,
        out BattleDefenseInfluence temporaryInfluences);

    bool IsAnalysisDisclosed(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        BattleAnalysisField field);
}

public sealed class BattleKnowledgeView : IBattleKnowledgeView
{
    private readonly IEncounterBattleKnowledgeView _encounter;
    private readonly IPersistentBattleKnowledgeView _persistent;

    public BattleKnowledgeView(
        RuntimeKnowledgeSnapshot persistent,
        RuntimeEncounterKnowledgeSnapshot encounter)
    {
        _persistent = new PersistentBattleKnowledgeView(
            persistent ?? throw new ArgumentNullException(nameof(persistent)));
        _encounter = new EncounterBattleKnowledgeView(
            encounter ?? throw new ArgumentNullException(nameof(encounter)));
    }

    public bool TryGetElementalAffinity(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        DamageElement element,
        out ElementalAffinity affinity,
        out BattleKnowledgeFactSource source,
        out BattleDefenseInfluence temporaryInfluences)
    {
        if (_encounter.TryGetElementalAffinity(
                targetInstanceId,
                targetEntityId,
                element,
                out affinity,
                out temporaryInfluences))
        {
            source = BattleKnowledgeFactSource.Encounter;
            return true;
        }

        source = BattleKnowledgeFactSource.Persistent;
        temporaryInfluences = BattleDefenseInfluence.None;
        return _persistent.TryGetElementalAffinity(targetEntityId, element, out affinity);
    }

    public bool TryGetAilmentResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        ContentId ailmentId,
        out ResistanceLevel resistance,
        out BattleKnowledgeFactSource source,
        out BattleDefenseInfluence temporaryInfluences)
    {
        if (_encounter.TryGetAilmentResistance(
                targetInstanceId,
                targetEntityId,
                ailmentId,
                out resistance,
                out temporaryInfluences))
        {
            source = BattleKnowledgeFactSource.Encounter;
            return true;
        }

        source = BattleKnowledgeFactSource.Persistent;
        temporaryInfluences = BattleDefenseInfluence.None;
        return _persistent.TryGetAilmentResistance(targetEntityId, ailmentId, out resistance);
    }

    public bool TryGetInstantDeathResistance(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        InstantDeathChannel channel,
        out ResistanceLevel resistance,
        out BattleKnowledgeFactSource source,
        out BattleDefenseInfluence temporaryInfluences)
    {
        if (_encounter.TryGetInstantDeathResistance(
                targetInstanceId,
                targetEntityId,
                channel,
                out resistance,
                out temporaryInfluences))
        {
            source = BattleKnowledgeFactSource.Encounter;
            return true;
        }

        source = BattleKnowledgeFactSource.Persistent;
        temporaryInfluences = BattleDefenseInfluence.None;
        return _persistent.TryGetInstantDeathResistance(targetEntityId, channel, out resistance);
    }

    public bool IsAnalysisDisclosed(
        RuntimeInstanceId targetInstanceId,
        ContentId targetEntityId,
        BattleAnalysisField field) =>
        _encounter.IsAnalysisDisclosed(targetInstanceId, targetEntityId, field);
}

public sealed record BattleKnowledgeObservationDiagnostic
{
    public BattleKnowledgeObservationDiagnostic(
        BattleKnowledgeObservationDiagnosticCode code,
        int observationIndex,
        string message)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }
        if (observationIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(observationIndex));
        }
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Knowledge diagnostics require a message.", nameof(message));
        }

        Code = code;
        ObservationIndex = observationIndex;
        Message = message;
    }

    public BattleKnowledgeObservationDiagnosticCode Code { get; }
    public int ObservationIndex { get; }
    public string Message { get; }
}

public sealed class BattleKnowledgeObservationTransitionRequest
{
    public BattleKnowledgeObservationTransitionRequest(
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        IEnumerable<BattleKnowledgeObservation> observations,
        BattleKnowledgePersistenceScope persistenceScope)
    {
        PersistentBefore = persistentBefore ?? throw new ArgumentNullException(nameof(persistentBefore));
        EncounterBefore = encounterBefore ?? throw new ArgumentNullException(nameof(encounterBefore));
        BattleKnowledgeObservation[] snapshot =
            (observations ?? throw new ArgumentNullException(nameof(observations))).ToArray();
        if (snapshot.Any(observation => observation is null))
        {
            throw new ArgumentException("Knowledge observations cannot contain null entries.", nameof(observations));
        }
        if (!Enum.IsDefined(persistenceScope))
        {
            throw new ArgumentOutOfRangeException(nameof(persistenceScope));
        }

        Observations = Array.AsReadOnly(snapshot);
        PersistenceScope = persistenceScope;
    }

    public RuntimeKnowledgeSnapshot PersistentBefore { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterBefore { get; }
    public IReadOnlyList<BattleKnowledgeObservation> Observations { get; }
    public BattleKnowledgePersistenceScope PersistenceScope { get; }
}

public sealed class BattleKnowledgeObservationTransitionResult
{
    public BattleKnowledgeObservationTransitionResult(
        BattleKnowledgeTransitionStatus status,
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeKnowledgeSnapshot persistentAfter,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        RuntimeEncounterKnowledgeSnapshot encounterAfter,
        IEnumerable<BattleKnowledgeObservation>? acceptedObservations = null,
        IEnumerable<BattleKnowledgeObservationDiagnostic>? diagnostics = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        PersistentBefore = persistentBefore ?? throw new ArgumentNullException(nameof(persistentBefore));
        PersistentAfter = persistentAfter ?? throw new ArgumentNullException(nameof(persistentAfter));
        EncounterBefore = encounterBefore ?? throw new ArgumentNullException(nameof(encounterBefore));
        EncounterAfter = encounterAfter ?? throw new ArgumentNullException(nameof(encounterAfter));
        AcceptedObservations = Snapshot(acceptedObservations, nameof(acceptedObservations));
        Diagnostics = Snapshot(diagnostics, nameof(diagnostics));
    }

    public BattleKnowledgeTransitionStatus Status { get; }
    public RuntimeKnowledgeSnapshot PersistentBefore { get; }
    public RuntimeKnowledgeSnapshot PersistentAfter { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterBefore { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterAfter { get; }
    public IReadOnlyList<BattleKnowledgeObservation> AcceptedObservations { get; }
    public IReadOnlyList<BattleKnowledgeObservationDiagnostic> Diagnostics { get; }
    public bool Applied => Status == BattleKnowledgeTransitionStatus.Applied;

    private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T>? source, string name) where T : class
    {
        T[] values = source?.ToArray() ?? [];
        if (values.Any(value => value is null))
        {
            throw new ArgumentException("Knowledge transition collections cannot contain null entries.", name);
        }

        return Array.AsReadOnly(values);
    }
}

public sealed class BattleKnowledgeEncounterCleanupResult
{
    public BattleKnowledgeEncounterCleanupResult(
        BattleKnowledgeTransitionStatus status,
        RuntimeEncounterKnowledgeSnapshot before,
        RuntimeEncounterKnowledgeSnapshot after)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        if (!After.IsEmpty)
        {
            throw new ArgumentException("Encounter cleanup must produce empty encounter knowledge.", nameof(after));
        }
        BattleKnowledgeTransitionStatus expected = Before.IsEmpty
            ? BattleKnowledgeTransitionStatus.Unchanged
            : BattleKnowledgeTransitionStatus.Applied;
        if (Status != expected)
        {
            throw new ArgumentException("Encounter cleanup status must agree with its before snapshot.", nameof(status));
        }
    }

    public BattleKnowledgeTransitionStatus Status { get; }
    public RuntimeEncounterKnowledgeSnapshot Before { get; }
    public RuntimeEncounterKnowledgeSnapshot After { get; }
}

public interface IBattleKnowledgeObservationTransitionService
{
    BattleKnowledgeObservationTransitionResult Apply(BattleKnowledgeObservationTransitionRequest request);
    BattleKnowledgeEncounterCleanupResult ClearEncounter(RuntimeEncounterKnowledgeSnapshot before);
}

public sealed class BattleKnowledgeObservationTransitionService : IBattleKnowledgeObservationTransitionService
{
    private readonly IPersistentBattleKnowledgeTransitionService _persistentTransitions;

    public BattleKnowledgeObservationTransitionService(
        IPersistentBattleKnowledgeTransitionService? persistentTransitions = null)
    {
        _persistentTransitions = persistentTransitions ?? new PersistentBattleKnowledgeTransitionService();
    }

    public BattleKnowledgeObservationTransitionResult Apply(BattleKnowledgeObservationTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<RuntimeInstanceId, ContentId> identities = TargetIdentities(request.EncounterBefore);
        for (int index = 0; index < request.Observations.Count; index++)
        {
            BattleKnowledgeObservation observation = request.Observations[index];
            if (identities.TryGetValue(observation.TargetId, out ContentId existing) &&
                existing != observation.TargetEntityId)
            {
                return Rejected(
                    request,
                    new BattleKnowledgeObservationDiagnostic(
                        BattleKnowledgeObservationDiagnosticCode.TargetIdentityConflict,
                        index,
                        $"Target '{observation.TargetId}' was already associated with entity '{existing}'."));
            }

            identities[observation.TargetId] = observation.TargetEntityId;
        }

        var elemental = request.EncounterBefore.Elemental.ToDictionary(
            entry => (entry.TargetInstanceId, entry.Element));
        var ailments = request.EncounterBefore.Ailments.ToDictionary(
            entry => (entry.TargetInstanceId, entry.AilmentId));
        var instantDeath = request.EncounterBefore.InstantDeath.ToDictionary(
            entry => (entry.TargetInstanceId, entry.Channel));
        var persistentElemental = new Dictionary<(ContentId, DamageElement), RuntimeElementalAffinityKnowledgeSnapshot>();
        var persistentAilments = new Dictionary<(ContentId, ContentId), RuntimeAilmentResistanceKnowledgeSnapshot>();
        var persistentInstantDeath = new Dictionary<(ContentId, InstantDeathChannel), RuntimeInstantDeathResistanceKnowledgeSnapshot>();
        var applied = new List<BattleKnowledgeObservation>();

        foreach (BattleKnowledgeObservation observation in request.Observations)
        {
            bool changed = observation.Kind switch
            {
                BattleKnowledgeObservationKind.ElementalAffinity => ApplyElemental(
                    request.PersistenceScope,
                    observation,
                    elemental,
                    persistentElemental),
                BattleKnowledgeObservationKind.AilmentResistance => ApplyAilment(
                    request.PersistenceScope,
                    observation,
                    ailments,
                    persistentAilments),
                BattleKnowledgeObservationKind.InstantDeathResistance => ApplyInstantDeath(
                    request.PersistenceScope,
                    observation,
                    instantDeath,
                    persistentInstantDeath),
                _ => throw new InvalidOperationException("Unsupported knowledge observation kind.")
            };
            if (changed)
            {
                applied.Add(observation);
            }
        }

        var encounterAfter = new RuntimeEncounterKnowledgeSnapshot(
            elemental.Values.OrderBy(EntrySortKey),
            ailments.Values.OrderBy(EntrySortKey),
            instantDeath.Values.OrderBy(EntrySortKey),
            request.EncounterBefore.Analysis);
        var discovery = new RuntimeKnowledgeSnapshot(
            persistentElemental.Values,
            persistentAilments.Values,
            persistentInstantDeath.Values);
        BattleKnowledgeTransitionResult persistentResult = _persistentTransitions.Apply(
            new BattleKnowledgeTransitionRequest(request.PersistentBefore, discovery));
        if (persistentResult.Status == BattleKnowledgeTransitionStatus.Rejected)
        {
            return Rejected(
                request,
                new BattleKnowledgeObservationDiagnostic(
                    BattleKnowledgeObservationDiagnosticCode.PersistentTransitionRejected,
                    -1,
                    string.Join(" ", persistentResult.Diagnostics.Select(diagnostic => diagnostic.Message))));
        }

        bool persistentChanged = persistentResult.Status == BattleKnowledgeTransitionStatus.Applied;
        bool encounterChanged = !EncounterEquivalent(request.EncounterBefore, encounterAfter);
        return new BattleKnowledgeObservationTransitionResult(
            persistentChanged || encounterChanged
                ? BattleKnowledgeTransitionStatus.Applied
                : BattleKnowledgeTransitionStatus.Unchanged,
            request.PersistentBefore,
            persistentResult.After,
            request.EncounterBefore,
            encounterAfter,
            applied);
    }

    public BattleKnowledgeEncounterCleanupResult ClearEncounter(RuntimeEncounterKnowledgeSnapshot before)
    {
        ArgumentNullException.ThrowIfNull(before);
        return new BattleKnowledgeEncounterCleanupResult(
            before.IsEmpty ? BattleKnowledgeTransitionStatus.Unchanged : BattleKnowledgeTransitionStatus.Applied,
            before,
            RuntimeEncounterKnowledgeSnapshot.Empty);
    }

    private static bool ApplyElemental(
        BattleKnowledgePersistenceScope scope,
        BattleKnowledgeObservation observation,
        IDictionary<(RuntimeInstanceId, DamageElement), EncounterElementalKnowledgeEntry> encounter,
        IDictionary<(ContentId, DamageElement), RuntimeElementalAffinityKnowledgeSnapshot> persistent)
    {
        if (observation.Outcome != BattleKnowledgeObservationOutcome.Contacted ||
            observation.Element is not DamageElement element ||
            element == DamageElement.Almighty ||
            observation.EffectiveAffinity is not ElementalAffinity effective ||
            observation.AuthoredAffinity is not ElementalAffinity authored)
        {
            return false;
        }

        var entry = new EncounterElementalKnowledgeEntry(
            observation.TargetId,
            observation.TargetEntityId,
            element,
            effective,
            observation.TemporaryInfluences);
        bool changed = !encounter.TryGetValue((observation.TargetId, element), out EncounterElementalKnowledgeEntry? before) ||
                       before.Affinity != effective ||
                       before.TemporaryInfluences != observation.TemporaryInfluences;
        encounter[(observation.TargetId, element)] = entry;
        if (scope == BattleKnowledgePersistenceScope.EncounterAndPersistent && !observation.HasTemporaryInfluence)
        {
            persistent[(observation.TargetEntityId, element)] =
                new RuntimeElementalAffinityKnowledgeSnapshot(observation.TargetEntityId, element, authored);
        }

        return changed || (scope == BattleKnowledgePersistenceScope.EncounterAndPersistent &&
                           !observation.HasTemporaryInfluence);
    }

    private static bool ApplyAilment(
        BattleKnowledgePersistenceScope scope,
        BattleKnowledgeObservation observation,
        IDictionary<(RuntimeInstanceId, ContentId), EncounterAilmentKnowledgeEntry> encounter,
        IDictionary<(ContentId, ContentId), RuntimeAilmentResistanceKnowledgeSnapshot> persistent)
    {
        if (observation.Outcome != BattleKnowledgeObservationOutcome.Immune ||
            observation.AilmentId is not ContentId ailmentId ||
            observation.EffectiveResistance != ResistanceLevel.Immune)
        {
            return false;
        }

        var entry = new EncounterAilmentKnowledgeEntry(
            observation.TargetId,
            observation.TargetEntityId,
            ailmentId,
            ResistanceLevel.Immune,
            observation.TemporaryInfluences);
        bool changed = !encounter.TryGetValue((observation.TargetId, ailmentId), out EncounterAilmentKnowledgeEntry? before) ||
                       before.Resistance != ResistanceLevel.Immune ||
                       before.TemporaryInfluences != observation.TemporaryInfluences;
        encounter[(observation.TargetId, ailmentId)] = entry;
        if (scope == BattleKnowledgePersistenceScope.EncounterAndPersistent &&
            !observation.HasTemporaryInfluence &&
            observation.AuthoredResistance == ResistanceLevel.Immune)
        {
            persistent[(observation.TargetEntityId, ailmentId)] = new RuntimeAilmentResistanceKnowledgeSnapshot(
                observation.TargetEntityId,
                ailmentId,
                ResistanceLevel.Immune);
        }

        return changed || (scope == BattleKnowledgePersistenceScope.EncounterAndPersistent &&
                           !observation.HasTemporaryInfluence &&
                           observation.AuthoredResistance == ResistanceLevel.Immune);
    }

    private static bool ApplyInstantDeath(
        BattleKnowledgePersistenceScope scope,
        BattleKnowledgeObservation observation,
        IDictionary<(RuntimeInstanceId, InstantDeathChannel), EncounterInstantDeathKnowledgeEntry> encounter,
        IDictionary<(ContentId, InstantDeathChannel), RuntimeInstantDeathResistanceKnowledgeSnapshot> persistent)
    {
        if (observation.ResistanceBypassed ||
            observation.Outcome != BattleKnowledgeObservationOutcome.Immune ||
            observation.InstantDeathChannel is not InstantDeathChannel channel ||
            observation.EffectiveResistance != ResistanceLevel.Immune)
        {
            return false;
        }

        var entry = new EncounterInstantDeathKnowledgeEntry(
            observation.TargetId,
            observation.TargetEntityId,
            channel,
            ResistanceLevel.Immune,
            observation.TemporaryInfluences);
        bool changed = !encounter.TryGetValue((observation.TargetId, channel), out EncounterInstantDeathKnowledgeEntry? before) ||
                       before.Resistance != ResistanceLevel.Immune ||
                       before.TemporaryInfluences != observation.TemporaryInfluences;
        encounter[(observation.TargetId, channel)] = entry;
        if (scope == BattleKnowledgePersistenceScope.EncounterAndPersistent &&
            !observation.HasTemporaryInfluence &&
            observation.AuthoredResistance == ResistanceLevel.Immune)
        {
            persistent[(observation.TargetEntityId, channel)] = new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                observation.TargetEntityId,
                channel,
                ResistanceLevel.Immune);
        }

        return changed || (scope == BattleKnowledgePersistenceScope.EncounterAndPersistent &&
                           !observation.HasTemporaryInfluence &&
                           observation.AuthoredResistance == ResistanceLevel.Immune);
    }

    private static BattleKnowledgeObservationTransitionResult Rejected(
        BattleKnowledgeObservationTransitionRequest request,
        BattleKnowledgeObservationDiagnostic diagnostic) =>
        new(
            BattleKnowledgeTransitionStatus.Rejected,
            request.PersistentBefore,
            request.PersistentBefore,
            request.EncounterBefore,
            request.EncounterBefore,
            diagnostics: [diagnostic]);

    private static Dictionary<RuntimeInstanceId, ContentId> TargetIdentities(
        RuntimeEncounterKnowledgeSnapshot snapshot) =>
        snapshot.Elemental.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId))
            .Concat(snapshot.Ailments.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
            .Concat(snapshot.InstantDeath.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
            .Concat(snapshot.Analysis.Select(entry => (entry.TargetInstanceId, entry.TargetEntityId)))
            .Distinct()
            .ToDictionary(pair => pair.TargetInstanceId, pair => pair.TargetEntityId);

    private static string EntrySortKey(EncounterElementalKnowledgeEntry entry) =>
        $"{entry.TargetInstanceId}|{entry.Element}";

    private static string EntrySortKey(EncounterAilmentKnowledgeEntry entry) =>
        $"{entry.TargetInstanceId}|{entry.AilmentId}";

    private static string EntrySortKey(EncounterInstantDeathKnowledgeEntry entry) =>
        $"{entry.TargetInstanceId}|{entry.Channel}";

    private static bool EncounterEquivalent(
        RuntimeEncounterKnowledgeSnapshot left,
        RuntimeEncounterKnowledgeSnapshot right) =>
        left.Elemental.SequenceEqual(right.Elemental) &&
        left.Ailments.SequenceEqual(right.Ailments) &&
        left.InstantDeath.SequenceEqual(right.InstantDeath) &&
        left.Analysis.SequenceEqual(right.Analysis);
}
