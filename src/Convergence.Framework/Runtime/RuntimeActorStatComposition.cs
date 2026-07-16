using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

public enum RuntimeActorStatCompositionStatus
{
    Applied,
    Rejected
}

public enum RuntimeActorStatCompositionDiagnosticCode
{
    MissingActiveHostedEntity,
    ActiveHostedEntityStateMissing,
    ActiveHostedEntityStateUnexpected,
    ActiveHostedEntityIdentityMismatch,
    StatResolutionFailed,
    ResourceRecalculationFailed,
    MissingPartyRoster,
    RosterInvariantViolation,
    CommitFailed
}

public sealed record RuntimeActorStatCompositionDiagnostic(
    RuntimeActorStatCompositionDiagnosticCode Code,
    string Message,
    ContentId? StatId = null,
    RuntimeInstanceId? InstanceId = null);

public sealed record RuntimeActorStatCompositionRequest
{
    public RuntimeActorStatCompositionRequest(
        RuntimeActorState actor,
        RuntimeStatSourceKind sourceKind,
        MissingHostedEntityBehavior missingHostedEntityBehavior,
        RuntimeActorState? activeHostedEntity = null,
        RuntimePartyRosterSnapshot? partyRoster = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers = null)
    {
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), "Stat source kind is not supported.");
        }
        if (!Enum.IsDefined(missingHostedEntityBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingHostedEntityBehavior),
                "Missing hosted-entity behavior is not supported.");
        }

        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        SourceKind = sourceKind;
        MissingHostedEntityBehavior = missingHostedEntityBehavior;
        ActiveHostedEntity = activeHostedEntity;
        PartyRoster = partyRoster;
        EquipmentStatModifiers = RuntimeSnapshotCollections.Dictionary(equipmentStatModifiers);
    }

    public RuntimeActorState Actor { get; }
    public RuntimeStatSourceKind SourceKind { get; }
    public MissingHostedEntityBehavior MissingHostedEntityBehavior { get; }
    public RuntimeActorState? ActiveHostedEntity { get; }
    public RuntimePartyRosterSnapshot? PartyRoster { get; }
    public IReadOnlyDictionary<ContentId, decimal> EquipmentStatModifiers { get; }
}

public sealed record RuntimeActorStatCompositionResult
{
    public RuntimeActorStatCompositionResult(
        RuntimeActorStatCompositionStatus status,
        RuntimeActorSnapshot before,
        RuntimeActorSnapshot after,
        RuntimeStatSourceKind resolvedSourceKind,
        IEnumerable<StatResolutionResult>? statResolutions = null,
        IEnumerable<RuntimeActorStatCompositionDiagnostic>? diagnostics = null)
    {
        Status = status;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        ResolvedSourceKind = resolvedSourceKind;
        StatResolutions = RuntimeSnapshotCollections.List(statResolutions);
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeActorStatCompositionStatus Status { get; }
    public bool Applied => Status == RuntimeActorStatCompositionStatus.Applied;
    public RuntimeActorSnapshot Before { get; }
    public RuntimeActorSnapshot After { get; }
    public RuntimeStatSourceKind ResolvedSourceKind { get; }
    public IReadOnlyList<StatResolutionResult> StatResolutions { get; }
    public IReadOnlyList<RuntimeActorStatCompositionDiagnostic> Diagnostics { get; }
}

public interface IRuntimeActorStatCompositionService
{
    RuntimeActorStatCompositionResult Compose(RuntimeActorStatCompositionRequest request);
}

public sealed class RuntimeActorStatCompositionService : IRuntimeActorStatCompositionService
{
    private readonly IStatResolutionPolicy _statResolution;
    private readonly IResourceGrowthPolicy _resourceGrowth;

    public RuntimeActorStatCompositionService(
        IStatResolutionPolicy? statResolution = null,
        IResourceGrowthPolicy? resourceGrowth = null)
    {
        _statResolution = statResolution ?? new StandardStatResolutionPolicy();
        _resourceGrowth = resourceGrowth ?? new StandardResourceGrowthPolicy();
    }

    public RuntimeActorStatCompositionResult Compose(RuntimeActorStatCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuntimeActorState actor = request.Actor;
        RuntimeActorSnapshot before = actor.ToSnapshot();
        RuntimeStatSourceKind resolvedSource = request.SourceKind;
        IReadOnlyDictionary<ContentId, decimal> hostedStats =
            RuntimeSnapshotCollections.Dictionary<ContentId, decimal>();

        RuntimePartyRosterSnapshot? partyRoster = request.PartyRoster;
        if (partyRoster is not null)
        {
            IReadOnlyList<RuntimePartyRosterInvariantDiagnostic> rosterDiagnostics =
                RuntimePartyRosterInvariantRules.Validate(partyRoster);
            if (rosterDiagnostics.Count > 0)
            {
                RuntimePartyRosterInvariantDiagnostic first = rosterDiagnostics[0];
                return Rejected(
                    before,
                    resolvedSource,
                    RuntimeActorStatCompositionDiagnosticCode.RosterInvariantViolation,
                    $"Party roster is invalid at '{first.Path}': {first.Message}",
                    instanceId: first.InstanceId);
            }
        }

        if (request.SourceKind == RuntimeStatSourceKind.ActiveHostedEntity)
        {
            if (partyRoster is null)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    RuntimeActorStatCompositionDiagnosticCode.MissingPartyRoster,
                    "Active hosted-entity stat composition requires the canonical party roster.",
                    instanceId: actor.InstanceId);
            }

            RuntimeActorReferenceSnapshot? activeReference = partyRoster.ActiveHostedEntity;
            if (activeReference is null)
            {
                if (request.ActiveHostedEntity is not null)
                {
                    return Rejected(
                        before,
                        resolvedSource,
                        RuntimeActorStatCompositionDiagnosticCode.ActiveHostedEntityStateUnexpected,
                        "An active hosted-entity state was supplied without an active hosted-entity roster reference.",
                        instanceId: request.ActiveHostedEntity.InstanceId);
                }

                if (request.MissingHostedEntityBehavior == MissingHostedEntityBehavior.RejectStatResolution)
                {
                    return Rejected(
                        before,
                        resolvedSource,
                        RuntimeActorStatCompositionDiagnosticCode.MissingActiveHostedEntity,
                        $"Vessel '{actor.InstanceId}' has no active hosted entity.",
                        instanceId: actor.InstanceId);
                }

                resolvedSource = RuntimeStatSourceKind.Actor;
            }
            else if (request.ActiveHostedEntity is null)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    RuntimeActorStatCompositionDiagnosticCode.ActiveHostedEntityStateMissing,
                    $"Active hosted entity '{activeReference.InstanceId}' has no supplied runtime state.",
                    instanceId: activeReference.InstanceId);
            }
            else if (request.ActiveHostedEntity.InstanceId != activeReference.InstanceId ||
                     request.ActiveHostedEntity.EntityId != activeReference.EntityDefinitionId)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    RuntimeActorStatCompositionDiagnosticCode.ActiveHostedEntityIdentityMismatch,
                    $"Supplied hosted entity '{request.ActiveHostedEntity.InstanceId}' does not match " +
                    $"roster reference '{activeReference.InstanceId}'.",
                    instanceId: activeReference.InstanceId);
            }
            else
            {
                hostedStats = request.ActiveHostedEntity.BaseStats;
            }
        }

        var resolutions = new List<StatResolutionResult>(StandardProgressionIds.CoreStats.Count);
        HashSet<ContentId> composedStatIds = [.. StandardProgressionIds.CoreStats];
        var effectiveStats = before.Stats.EffectiveStats
            .Where(pair => !composedStatIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (ContentId statId in StandardProgressionIds.CoreStats)
        {
            StatResolutionResult resolution;
            try
            {
                resolution = _statResolution.Resolve(new StatResolutionRequest(
                    resolvedSource,
                    statId,
                    actor.BaseStats,
                    hostedStats,
                    request.EquipmentStatModifiers));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                return Rejected(
                    before,
                    resolvedSource,
                    RuntimeActorStatCompositionDiagnosticCode.StatResolutionFailed,
                    $"Stat '{statId}' could not be resolved: {exception.Message}",
                    statId);
            }

            resolutions.Add(resolution);
            effectiveStats.Add(statId, resolution.FinalValue);
        }

        ResourceRecalculationResult resources;
        try
        {
            resources = _resourceGrowth.Recalculate(new ResourceRecalculationRequest(
                before.Resources,
                before.BaseResourceValues,
                effectiveStats,
                ResourceCurrentAdjustmentMode.PreserveCurrent));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Rejected(
                before,
                resolvedSource,
                RuntimeActorStatCompositionDiagnosticCode.ResourceRecalculationFailed,
                $"Resources could not be recalculated: {exception.Message}");
        }

        try
        {
            actor.ApplyStatComposition(effectiveStats, resources.Resources);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Rejected(
                before,
                resolvedSource,
                RuntimeActorStatCompositionDiagnosticCode.CommitFailed,
                $"Composed actor state could not be committed: {exception.Message}");
        }

        return new RuntimeActorStatCompositionResult(
            RuntimeActorStatCompositionStatus.Applied,
            before,
            actor.ToSnapshot(),
            resolvedSource,
            resolutions);
    }

    private static RuntimeActorStatCompositionResult Rejected(
        RuntimeActorSnapshot before,
        RuntimeStatSourceKind resolvedSource,
        RuntimeActorStatCompositionDiagnosticCode code,
        string message,
        ContentId? statId = null,
        RuntimeInstanceId? instanceId = null) =>
        new(
            RuntimeActorStatCompositionStatus.Rejected,
            before,
            before,
            resolvedSource,
            diagnostics: [new RuntimeActorStatCompositionDiagnostic(code, message, statId, instanceId)]);
}
