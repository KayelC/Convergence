using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Knowledge;

public enum BattleAnalysisDisclosureStatus
{
    Disclosed,
    Unknown,
    Unavailable
}

public sealed record BattleAnalysisFieldDisclosure
{
    public BattleAnalysisFieldDisclosure(
        BattleAnalysisField field,
        BattleAnalysisDisclosureStatus status)
    {
        if (!Enum.IsDefined(field))
        {
            throw new ArgumentOutOfRangeException(nameof(field));
        }
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Field = field;
        Status = status;
    }

    public BattleAnalysisField Field { get; }
    public BattleAnalysisDisclosureStatus Status { get; }
}

public sealed class BattleAnalysisDisclosurePolicyRequest
{
    public BattleAnalysisDisclosurePolicyRequest(
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        RuntimeCombatProfileIdentitySnapshot targetProfileIdentity,
        IEnumerable<BattleAnalysisField> requestedFields)
    {
        ArgumentNullException.ThrowIfNull(targetProfileIdentity);
        if (!actorId.IsValid ||
            !targetId.IsValid ||
            !targetProfileIdentity.SourceActorInstanceId.IsValid ||
            !targetProfileIdentity.SourceEntityDefinitionId.IsValid)
        {
            throw new ArgumentException(
                "Analysis policy requests require valid actor, target, and combat-profile IDs.");
        }

        BattleAnalysisField[] fields = SnapshotFields(requestedFields, nameof(requestedFields));
        ActorId = actorId;
        TargetId = targetId;
        TargetProfileIdentity = targetProfileIdentity;
        RequestedFields = Array.AsReadOnly(fields);
    }

    public RuntimeInstanceId ActorId { get; }
    public RuntimeInstanceId TargetId { get; }
    public RuntimeCombatProfileIdentitySnapshot TargetProfileIdentity { get; }
    public ContentId TargetEntityId => TargetProfileIdentity.SourceEntityDefinitionId;
    public IReadOnlyList<BattleAnalysisField> RequestedFields { get; }

    internal static BattleAnalysisField[] SnapshotFields(
        IEnumerable<BattleAnalysisField> fields,
        string parameterName)
    {
        BattleAnalysisField[] snapshot = (fields ??
            throw new ArgumentNullException(parameterName)).Distinct().Order().ToArray();
        if (snapshot.Length == 0 || snapshot.Any(field => !Enum.IsDefined(field)))
        {
            throw new ArgumentException(
                "Analysis requires at least one defined field.",
                parameterName);
        }

        return snapshot;
    }
}

public interface IBattleAnalysisDisclosurePolicy
{
    IReadOnlyList<BattleAnalysisFieldDisclosure> Resolve(
        BattleAnalysisDisclosurePolicyRequest request);
}

public sealed class StandardBattleAnalysisDisclosurePolicy : IBattleAnalysisDisclosurePolicy
{
    public static StandardBattleAnalysisDisclosurePolicy Instance { get; } = new();

    private StandardBattleAnalysisDisclosurePolicy()
    {
    }

    public IReadOnlyList<BattleAnalysisFieldDisclosure> Resolve(
        BattleAnalysisDisclosurePolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Array.AsReadOnly(request.RequestedFields
            .Select(field => new BattleAnalysisFieldDisclosure(
                field,
                BattleAnalysisDisclosureStatus.Disclosed))
            .ToArray());
    }
}

/// <summary>
/// Hides explicitly selected analysis fields without inferring restrictions from names or IDs.
/// </summary>
public sealed class RestrictedBattleAnalysisDisclosurePolicy : IBattleAnalysisDisclosurePolicy
{
    private readonly IReadOnlySet<BattleAnalysisField> _hiddenFields;

    public RestrictedBattleAnalysisDisclosurePolicy(
        IEnumerable<BattleAnalysisField>? hiddenFields = null)
    {
        BattleAnalysisField[] fields = (hiddenFields ??
            Enum.GetValues<BattleAnalysisField>()).Distinct().ToArray();
        if (fields.Any(field => !Enum.IsDefined(field)))
        {
            throw new ArgumentException("Restricted analysis fields must be defined.", nameof(hiddenFields));
        }

        _hiddenFields = new HashSet<BattleAnalysisField>(fields);
    }

    public IReadOnlyList<BattleAnalysisFieldDisclosure> Resolve(
        BattleAnalysisDisclosurePolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Array.AsReadOnly(request.RequestedFields
            .Select(field => new BattleAnalysisFieldDisclosure(
                field,
                _hiddenFields.Contains(field)
                    ? BattleAnalysisDisclosureStatus.Unknown
                    : BattleAnalysisDisclosureStatus.Disclosed))
            .ToArray());
    }
}

public sealed class BattleAnalysisDataSnapshot
{
    internal BattleAnalysisDataSnapshot(
        decimal? currentHp,
        decimal? currentSp,
        IEnumerable<KeyValuePair<ContentId, decimal>>? coreStats,
        IEnumerable<ContentId>? skillIds,
        IEnumerable<KeyValuePair<DamageElement, ElementalAffinity>>? elementalAffinities,
        IEnumerable<KeyValuePair<ContentId, ResistanceLevel>>? ailmentResistances,
        IEnumerable<KeyValuePair<InstantDeathChannel, ResistanceLevel>>? instantDeathResistances)
    {
        CurrentHp = currentHp;
        CurrentSp = currentSp;
        CoreStats = Snapshot(coreStats);
        SkillIds = Array.AsReadOnly((skillIds ?? []).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray());
        ElementalAffinities = Snapshot(elementalAffinities);
        AilmentResistances = Snapshot(ailmentResistances);
        InstantDeathResistances = Snapshot(instantDeathResistances);
    }

    public decimal? CurrentHp { get; }
    public decimal? CurrentSp { get; }
    public IReadOnlyDictionary<ContentId, decimal> CoreStats { get; }
    public IReadOnlyList<ContentId> SkillIds { get; }
    public IReadOnlyDictionary<DamageElement, ElementalAffinity> ElementalAffinities { get; }
    public IReadOnlyDictionary<ContentId, ResistanceLevel> AilmentResistances { get; }
    public IReadOnlyDictionary<InstantDeathChannel, ResistanceLevel> InstantDeathResistances { get; }

    private static IReadOnlyDictionary<TKey, TValue> Snapshot<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values) where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>((values ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));
}

public sealed class BattleAnalysisResult
{
    internal BattleAnalysisResult(
        RuntimeInstanceId actorId,
        RuntimeInstanceId targetId,
        RuntimeCombatProfileIdentitySnapshot targetProfileIdentity,
        IEnumerable<BattleAnalysisFieldDisclosure> disclosures,
        BattleAnalysisDataSnapshot data)
    {
        ArgumentNullException.ThrowIfNull(targetProfileIdentity);
        if (!actorId.IsValid ||
            !targetId.IsValid ||
            !targetProfileIdentity.SourceActorInstanceId.IsValid ||
            !targetProfileIdentity.SourceEntityDefinitionId.IsValid)
        {
            throw new ArgumentException(
                "Analysis results require valid actor, target, and combat-profile IDs.");
        }

        BattleAnalysisFieldDisclosure[] snapshot = (disclosures ??
            throw new ArgumentNullException(nameof(disclosures))).ToArray();
        if (snapshot.Length == 0 || snapshot.Any(disclosure => disclosure is null) ||
            snapshot.Select(disclosure => disclosure.Field).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Analysis results require unique field disclosures.", nameof(disclosures));
        }

        ActorId = actorId;
        TargetId = targetId;
        TargetProfileIdentity = targetProfileIdentity;
        Disclosures = Array.AsReadOnly(snapshot.OrderBy(disclosure => disclosure.Field).ToArray());
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public RuntimeInstanceId ActorId { get; }
    public RuntimeInstanceId TargetId { get; }
    public RuntimeCombatProfileIdentitySnapshot TargetProfileIdentity { get; }
    public ContentId TargetEntityId => TargetProfileIdentity.SourceEntityDefinitionId;
    public IReadOnlyList<BattleAnalysisFieldDisclosure> Disclosures { get; }
    public BattleAnalysisDataSnapshot Data { get; }
    public IReadOnlyList<BattleAnalysisField> DisclosedFields => Array.AsReadOnly(
        Disclosures.Where(disclosure => disclosure.Status == BattleAnalysisDisclosureStatus.Disclosed)
            .Select(disclosure => disclosure.Field)
            .ToArray());
}

public sealed class BattleAnalysisRequest
{
    public BattleAnalysisRequest(
        RuntimeActorState actor,
        RuntimeActorState target,
        IEnumerable<AnalysisLayer> layers,
        ContentId spResourceId)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        AnalysisLayer[] layerSnapshot = (layers ?? throw new ArgumentNullException(nameof(layers)))
            .Distinct().ToArray();
        if (layerSnapshot.Length == 0 || layerSnapshot.Any(layer => !Enum.IsDefined(layer)))
        {
            throw new ArgumentException("Analysis requires at least one defined layer.", nameof(layers));
        }
        if (!spResourceId.IsValid)
        {
            throw new ArgumentException("Analysis requires a valid SP resource ID.", nameof(spResourceId));
        }

        Layers = Array.AsReadOnly(layerSnapshot);
        SpResourceId = spResourceId;
    }

    public RuntimeActorState Actor { get; }
    public RuntimeActorState Target { get; }
    public IReadOnlyList<AnalysisLayer> Layers { get; }
    public ContentId SpResourceId { get; }
}

public interface IBattleAnalysisService
{
    BattleAnalysisResult Analyze(BattleAnalysisRequest request);
}

public sealed class BattleAnalysisService : IBattleAnalysisService
{
    private readonly IBattleAnalysisDisclosurePolicy _policy;

    public BattleAnalysisService(IBattleAnalysisDisclosurePolicy? policy = null)
    {
        _policy = policy ?? StandardBattleAnalysisDisclosurePolicy.Instance;
    }

    public BattleAnalysisResult Analyze(BattleAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        BattleAnalysisField[] requestedFields = Expand(request.Layers);
        var policyRequest = new BattleAnalysisDisclosurePolicyRequest(
            request.Actor.InstanceId,
            request.Target.InstanceId,
            request.Target.CombatProfileIdentity,
            requestedFields);
        BattleAnalysisFieldDisclosure[] decisions = (_policy.Resolve(policyRequest) ??
            throw new InvalidOperationException("The analysis policy returned no decisions.")).ToArray();
        ValidatePolicyDecisions(requestedFields, decisions);

        var resolved = new List<BattleAnalysisFieldDisclosure>(decisions.Length);
        foreach (BattleAnalysisFieldDisclosure decision in decisions)
        {
            BattleAnalysisDisclosureStatus status = decision.Status;
            if (decision.Field == BattleAnalysisField.CurrentSp &&
                status == BattleAnalysisDisclosureStatus.Disclosed &&
                !request.Target.TryGetResource(request.SpResourceId, out _))
            {
                status = BattleAnalysisDisclosureStatus.Unavailable;
            }
            resolved.Add(new BattleAnalysisFieldDisclosure(decision.Field, status));
        }

        bool Shows(BattleAnalysisField field) => resolved.Any(
            disclosure => disclosure.Field == field &&
                          disclosure.Status == BattleAnalysisDisclosureStatus.Disclosed);

        RuntimeActorState target = request.Target;
        var data = new BattleAnalysisDataSnapshot(
            Shows(BattleAnalysisField.CurrentHp)
                ? target.GetRequiredResource(target.VitalResourceId).Current
                : null,
            Shows(BattleAnalysisField.CurrentSp)
                ? target.GetRequiredResource(request.SpResourceId).Current
                : null,
            Shows(BattleAnalysisField.CoreStats) ? target.Stats : null,
            Shows(BattleAnalysisField.Skills) ? target.SkillIds : null,
            Shows(BattleAnalysisField.ElementalAffinities)
                ? Enum.GetValues<DamageElement>().Select(element =>
                    KeyValuePair.Create(element, target.DefenseProfile.GetElementalAffinity(element)))
                : null,
            Shows(BattleAnalysisField.AilmentResistances)
                ? target.DefenseProfile.AilmentResistances
                : null,
            Shows(BattleAnalysisField.InstantDeathResistances)
                ? Enum.GetValues<InstantDeathChannel>().Select(channel =>
                    KeyValuePair.Create(channel, target.DefenseProfile.GetInstantDeathResistance(channel)))
                : null);

        return new BattleAnalysisResult(
            request.Actor.InstanceId,
            target.InstanceId,
            target.CombatProfileIdentity,
            resolved,
            data);
    }

    private static BattleAnalysisField[] Expand(IEnumerable<AnalysisLayer> layers)
    {
        var fields = new HashSet<BattleAnalysisField>();
        foreach (AnalysisLayer layer in layers)
        {
            switch (layer)
            {
                case AnalysisLayer.Stats:
                    fields.UnionWith([
                        BattleAnalysisField.CurrentHp,
                        BattleAnalysisField.CurrentSp,
                        BattleAnalysisField.CoreStats]);
                    break;
                case AnalysisLayer.Affinities:
                    fields.Add(BattleAnalysisField.ElementalAffinities);
                    break;
                case AnalysisLayer.Skills:
                    fields.Add(BattleAnalysisField.Skills);
                    break;
                case AnalysisLayer.Ailments:
                    fields.UnionWith([
                        BattleAnalysisField.AilmentResistances,
                        BattleAnalysisField.InstantDeathResistances]);
                    break;
                case AnalysisLayer.Full:
                    fields.UnionWith(Enum.GetValues<BattleAnalysisField>());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layers), layer, "Analysis layer must be defined.");
            }
        }

        return fields.Order().ToArray();
    }

    private static void ValidatePolicyDecisions(
        IReadOnlyCollection<BattleAnalysisField> requested,
        IReadOnlyCollection<BattleAnalysisFieldDisclosure> decisions)
    {
        if (decisions.Any(decision => decision is null) ||
            decisions.Count != requested.Count ||
            !decisions.Select(decision => decision.Field).ToHashSet().SetEquals(requested) ||
            decisions.Any(decision => decision.Status == BattleAnalysisDisclosureStatus.Unavailable))
        {
            throw new InvalidOperationException(
                "The analysis policy must return one Disclosed or Unknown decision for every requested field.");
        }
    }
}

public enum BattleAnalysisKnowledgeDiagnosticCode
{
    PersistentTransitionRejected
}

public sealed record BattleAnalysisKnowledgeDiagnostic
{
    public BattleAnalysisKnowledgeDiagnostic(
        BattleAnalysisKnowledgeDiagnosticCode code,
        string message)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        Code = code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Analysis diagnostics require a message.", nameof(message))
            : message;
    }

    public BattleAnalysisKnowledgeDiagnosticCode Code { get; }
    public string Message { get; }
}

public sealed class BattleAnalysisKnowledgeTransitionResult
{
    public BattleAnalysisKnowledgeTransitionResult(
        BattleKnowledgeTransitionStatus status,
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeKnowledgeSnapshot persistentAfter,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        RuntimeEncounterKnowledgeSnapshot encounterAfter,
        IEnumerable<BattleAnalysisKnowledgeDiagnostic>? diagnostics = null)
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
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public BattleKnowledgeTransitionStatus Status { get; }
    public RuntimeKnowledgeSnapshot PersistentBefore { get; }
    public RuntimeKnowledgeSnapshot PersistentAfter { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterBefore { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterAfter { get; }
    public IReadOnlyList<BattleAnalysisKnowledgeDiagnostic> Diagnostics { get; }
    public bool Applied => Status == BattleKnowledgeTransitionStatus.Applied;
}

public interface IBattleAnalysisKnowledgeTransitionService
{
    BattleAnalysisKnowledgeTransitionResult Apply(
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        BattleAnalysisResult analysis);

    BattleAnalysisKnowledgeTransitionResult Apply(
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        BattleAnalysisResult analysis,
        BattleKnowledgePersistenceScope persistenceScope);
}

public sealed class BattleAnalysisKnowledgeTransitionService : IBattleAnalysisKnowledgeTransitionService
{
    private readonly IPersistentBattleKnowledgeTransitionService _persistentTransitions;
    private readonly IBattleKnowledgeTargetProfileTransitionService _profileTransitions;

    public BattleAnalysisKnowledgeTransitionService(
        IPersistentBattleKnowledgeTransitionService? persistentTransitions = null,
        IBattleKnowledgeTargetProfileTransitionService? profileTransitions = null)
    {
        _persistentTransitions = persistentTransitions ?? new PersistentBattleKnowledgeTransitionService();
        _profileTransitions = profileTransitions ?? new BattleKnowledgeTargetProfileTransitionService();
    }

    public BattleAnalysisKnowledgeTransitionResult Apply(
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        BattleAnalysisResult analysis) =>
        Apply(
            persistentBefore,
            encounterBefore,
            analysis,
            BattleKnowledgePersistenceScope.EncounterAndPersistent);

    public BattleAnalysisKnowledgeTransitionResult Apply(
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        BattleAnalysisResult analysis,
        BattleKnowledgePersistenceScope persistenceScope)
    {
        ArgumentNullException.ThrowIfNull(persistentBefore);
        ArgumentNullException.ThrowIfNull(encounterBefore);
        ArgumentNullException.ThrowIfNull(analysis);
        if (!Enum.IsDefined(persistenceScope))
        {
            throw new ArgumentOutOfRangeException(nameof(persistenceScope));
        }

        RuntimeEncounterKnowledgeSnapshot encounterBaseline = _profileTransitions.RebindTargetProfile(
            encounterBefore,
            analysis.TargetId,
            analysis.TargetProfileIdentity).After;
        EncounterAnalysisKnowledgeEntry? existing = encounterBaseline.Analysis
            .SingleOrDefault(entry => entry.TargetInstanceId == analysis.TargetId);

        BattleAnalysisField[] disclosed = analysis.DisclosedFields.ToArray();
        RuntimeEncounterKnowledgeSnapshot encounterAfter = encounterBaseline;
        if (disclosed.Length > 0)
        {
            var elemental = encounterBaseline.Elemental.ToDictionary(
                entry => (entry.TargetInstanceId, entry.Element));
            var ailments = encounterBaseline.Ailments.ToDictionary(
                entry => (entry.TargetInstanceId, entry.AilmentId));
            var instantDeath = encounterBaseline.InstantDeath.ToDictionary(
                entry => (entry.TargetInstanceId, entry.Channel));
            if (disclosed.Contains(BattleAnalysisField.ElementalAffinities))
            {
                foreach ((DamageElement element, ElementalAffinity affinity) in analysis.Data.ElementalAffinities)
                {
                    if (element != DamageElement.Almighty)
                    {
                        elemental.TryAdd(
                            (analysis.TargetId, element),
                            new EncounterElementalKnowledgeEntry(
                                analysis.TargetId,
                                analysis.TargetProfileIdentity,
                                element,
                                affinity));
                    }
                }
            }
            if (disclosed.Contains(BattleAnalysisField.AilmentResistances))
            {
                foreach ((ContentId ailmentId, ResistanceLevel resistance) in analysis.Data.AilmentResistances)
                {
                    ailments.TryAdd(
                        (analysis.TargetId, ailmentId),
                        new EncounterAilmentKnowledgeEntry(
                            analysis.TargetId,
                            analysis.TargetProfileIdentity,
                            ailmentId,
                            resistance));
                }
            }
            if (disclosed.Contains(BattleAnalysisField.InstantDeathResistances))
            {
                foreach ((InstantDeathChannel channel, ResistanceLevel resistance) in analysis.Data.InstantDeathResistances)
                {
                    instantDeath.TryAdd(
                        (analysis.TargetId, channel),
                        new EncounterInstantDeathKnowledgeEntry(
                            analysis.TargetId,
                            analysis.TargetProfileIdentity,
                            channel,
                            resistance));
                }
            }

            BattleAnalysisField[] merged = (existing?.DisclosedFields ?? [])
                .Concat(disclosed)
                .Distinct()
                .Order()
                .ToArray();
            IEnumerable<EncounterAnalysisKnowledgeEntry> analysisEntries = encounterBaseline.Analysis
                .Where(entry => entry.TargetInstanceId != analysis.TargetId)
                .Append(new EncounterAnalysisKnowledgeEntry(
                    analysis.TargetId,
                    analysis.TargetProfileIdentity,
                    merged));
            encounterAfter = new RuntimeEncounterKnowledgeSnapshot(
                elemental.Values,
                ailments.Values,
                instantDeath.Values,
                analysisEntries);
        }

        BattleAnalysisField[] persistentFields = disclosed.Where(IsDefenseField).ToArray();
        RuntimeKnowledgeSnapshot discoveries = persistenceScope == BattleKnowledgePersistenceScope.EncounterAndPersistent
            ? BuildDiscoveries(analysis, persistentFields)
            : new RuntimeKnowledgeSnapshot();
        BattleKnowledgeTransitionResult persistentResult = _persistentTransitions.Apply(
            new BattleKnowledgeTransitionRequest(persistentBefore, discoveries));
        if (persistentResult.Status == BattleKnowledgeTransitionStatus.Rejected)
        {
            return Rejected(
                persistentBefore,
                encounterBefore,
                BattleAnalysisKnowledgeDiagnosticCode.PersistentTransitionRejected,
                string.Join(" ", persistentResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        bool encounterChanged = !EncounterEquivalent(encounterBefore, encounterAfter);
        return new BattleAnalysisKnowledgeTransitionResult(
            persistentResult.Applied || encounterChanged
                ? BattleKnowledgeTransitionStatus.Applied
                : BattleKnowledgeTransitionStatus.Unchanged,
            persistentBefore,
            persistentResult.After,
            encounterBefore,
            encounterAfter);
    }

    private static RuntimeKnowledgeSnapshot BuildDiscoveries(
        BattleAnalysisResult analysis,
        IReadOnlyCollection<BattleAnalysisField> fields)
    {
        bool elemental = fields.Contains(BattleAnalysisField.ElementalAffinities);
        bool ailments = fields.Contains(BattleAnalysisField.AilmentResistances);
        bool instantDeath = fields.Contains(BattleAnalysisField.InstantDeathResistances);
        return new RuntimeKnowledgeSnapshot(
            elemental
                ? analysis.Data.ElementalAffinities
                    .Where(entry => entry.Key != DamageElement.Almighty)
                    .Select(entry => new RuntimeElementalAffinityKnowledgeSnapshot(
                        analysis.TargetEntityId,
                        entry.Key,
                        entry.Value))
                : null,
            ailments
                ? analysis.Data.AilmentResistances.Select(entry =>
                    new RuntimeAilmentResistanceKnowledgeSnapshot(
                        analysis.TargetEntityId,
                        entry.Key,
                        entry.Value))
                : null,
            instantDeath
                ? analysis.Data.InstantDeathResistances.Select(entry =>
                    new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                        analysis.TargetEntityId,
                        entry.Key,
                        entry.Value))
                : null,
            fields.Count > 0
                ? [new RuntimeAnalyzedDefenseKnowledgeSnapshot(analysis.TargetEntityId, fields)]
                : null);
    }

    private static bool IsDefenseField(BattleAnalysisField field) => field is
        BattleAnalysisField.ElementalAffinities or
        BattleAnalysisField.AilmentResistances or
        BattleAnalysisField.InstantDeathResistances;

    private static BattleAnalysisKnowledgeTransitionResult Rejected(
        RuntimeKnowledgeSnapshot persistent,
        RuntimeEncounterKnowledgeSnapshot encounter,
        BattleAnalysisKnowledgeDiagnosticCode code,
        string message) =>
        new(
            BattleKnowledgeTransitionStatus.Rejected,
            persistent,
            persistent,
            encounter,
            encounter,
            [new BattleAnalysisKnowledgeDiagnostic(code, message)]);

    private static bool EncounterEquivalent(
        RuntimeEncounterKnowledgeSnapshot left,
        RuntimeEncounterKnowledgeSnapshot right) =>
        left.Elemental.SequenceEqual(right.Elemental) &&
        left.Ailments.SequenceEqual(right.Ailments) &&
        left.InstantDeath.SequenceEqual(right.InstantDeath) &&
        AnalysisEquivalent(left.Analysis, right.Analysis);

    private static bool AnalysisEquivalent(
        IReadOnlyList<EncounterAnalysisKnowledgeEntry> left,
        IReadOnlyList<EncounterAnalysisKnowledgeEntry> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.TargetInstanceId == pair.Second.TargetInstanceId &&
            pair.First.TargetProfileIdentity == pair.Second.TargetProfileIdentity &&
            pair.First.DisclosedFields.SequenceEqual(pair.Second.DisclosedFields));
}
