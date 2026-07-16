using Convergence.Content;
using Convergence.Encounters;

namespace Convergence.Runtime;

/// <summary>
/// Connects a host-owned encounter trigger to an authored encounter and explicit actor authority.
/// </summary>
/// <param name="TriggerId">The host-owned trigger or scene-object ID.</param>
/// <param name="EncounterId">The pack-qualified encounter selected by the trigger.</param>
/// <param name="OpponentTeamId">The team assigned to prepared encounter actors.</param>
/// <param name="OpponentCommandAuthorityId">
/// The opaque host-routing key assigned to actors prepared for this trigger.
/// </param>
/// <param name="InstanceIdPrefix">The host-owned prefix used to create runtime actor IDs.</param>
/// <param name="FormationIndex">The authored formation index to prepare.</param>
public sealed record RuntimeEncounterTriggerRequest(
    ContentId TriggerId,
    ContentId EncounterId,
    ContentId OpponentTeamId,
    ContentId OpponentCommandAuthorityId,
    RuntimeInstanceId InstanceIdPrefix,
    int FormationIndex = 0);

public enum EncounterPreparationDiagnosticCode
{
    StartPlanningFailed,
    ActorCreationFailed
}

public sealed record EncounterPreparationDiagnostic(
    EncounterPreparationDiagnosticCode Code,
    string Message,
    ContentId TriggerId,
    ContentId? ContentId = null,
    EncounterStartDiagnosticCode? StartPlanningCode = null,
    CatalogBattleActorDiagnosticCode? ActorCreationCode = null);

public enum EncounterPreparationEventKind
{
    TriggerReceived,
    ActorPrepared,
    EncounterPrepared,
    EncounterRejected
}

public sealed record EncounterPreparationEvent(
    EncounterPreparationEventKind Kind,
    ContentId TriggerId,
    ContentId EncounterId,
    RuntimeInstanceId? ActorInstanceId = null,
    string? Message = null);

public sealed record PreparedEncounter
{
    public PreparedEncounter(
        ContentId triggerId,
        EncounterStartPlan startPlan,
        IEnumerable<CatalogBattleActor> actors)
    {
        TriggerId = triggerId;
        StartPlan = startPlan ?? throw new ArgumentNullException(nameof(startPlan));
        Actors = Array.AsReadOnly(
            (actors ?? throw new ArgumentNullException(nameof(actors))).ToArray());
    }

    public ContentId TriggerId { get; }
    public EncounterStartPlan StartPlan { get; }
    public EncounterDefinition Encounter => StartPlan.Encounter;
    public EncounterFormationDefinition Formation => StartPlan.Formation;
    public IReadOnlyList<CatalogBattleActor> Actors { get; }
}

public sealed record EncounterPreparationResult
{
    internal EncounterPreparationResult(
        PreparedEncounter? preparedEncounter,
        IEnumerable<EncounterPreparationDiagnostic>? diagnostics,
        IEnumerable<EncounterPreparationEvent>? events)
    {
        PreparedEncounter = preparedEncounter;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
        Events = Array.AsReadOnly((events ?? []).ToArray());
    }

    public PreparedEncounter? PreparedEncounter { get; }
    public IReadOnlyList<EncounterPreparationDiagnostic> Diagnostics { get; }
    public IReadOnlyList<EncounterPreparationEvent> Events { get; }
    public bool IsSuccess => PreparedEncounter is not null && Diagnostics.Count == 0;

    public PreparedEncounter RequirePreparedEncounter() =>
        PreparedEncounter ?? throw new EncounterPreparationException(Diagnostics);
}

public sealed class EncounterPreparationException : Exception
{
    public EncounterPreparationException(IEnumerable<EncounterPreparationDiagnostic> diagnostics)
        : this(Array.AsReadOnly(
            (diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).ToArray()))
    {
    }

    private EncounterPreparationException(IReadOnlyList<EncounterPreparationDiagnostic> diagnostics)
        : base($"Encounter preparation failed with {diagnostics.Count} diagnostic(s).")
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<EncounterPreparationDiagnostic> Diagnostics { get; }
}

public interface IEncounterPreparationService
{
    EncounterPreparationResult Prepare(RuntimeEncounterTriggerRequest request);
}

public sealed class CatalogEncounterPreparationService : IEncounterPreparationService
{
    private readonly IEncounterStartPlanner _planner;
    private readonly ICatalogBattleActorFactory _actorFactory;

    public CatalogEncounterPreparationService(
        IEncounterStartPlanner planner,
        ICatalogBattleActorFactory actorFactory)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _actorFactory = actorFactory ?? throw new ArgumentNullException(nameof(actorFactory));
    }

    public EncounterPreparationResult Prepare(RuntimeEncounterTriggerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var events = new List<EncounterPreparationEvent>
        {
            new(
                EncounterPreparationEventKind.TriggerReceived,
                request.TriggerId,
                request.EncounterId)
        };

        EncounterStartPlanResult planning = _planner.Plan(new EncounterStartRequest(
            request.EncounterId,
            request.OpponentTeamId,
            request.OpponentCommandAuthorityId,
            request.InstanceIdPrefix,
            request.FormationIndex));
        if (!planning.IsSuccess)
        {
            EncounterPreparationDiagnostic[] diagnostics = planning.Diagnostics
                .Select(diagnostic => new EncounterPreparationDiagnostic(
                    EncounterPreparationDiagnosticCode.StartPlanningFailed,
                    diagnostic.Message,
                    request.TriggerId,
                    diagnostic.ContentId,
                    StartPlanningCode: diagnostic.Code))
                .ToArray();
            events.Add(Rejected(request, "Encounter start planning was rejected."));
            return new EncounterPreparationResult(null, diagnostics, events);
        }

        EncounterStartPlan startPlan = planning.RequirePlan();
        var actors = new List<CatalogBattleActor>();
        var actorDiagnostics = new List<EncounterPreparationDiagnostic>();
        foreach (CatalogBattleActorCreationRequest actorRequest in startPlan.ActorRequests)
        {
            CatalogBattleActorCreationResult actorResult = _actorFactory.Create(actorRequest);
            if (!actorResult.IsSuccess)
            {
                actorDiagnostics.AddRange(actorResult.Diagnostics.Select(diagnostic =>
                    new EncounterPreparationDiagnostic(
                        EncounterPreparationDiagnosticCode.ActorCreationFailed,
                        diagnostic.Message,
                        request.TriggerId,
                        diagnostic.EntityId ?? diagnostic.SkillId,
                        ActorCreationCode: diagnostic.Code)));
                continue;
            }

            actors.Add(actorResult.RequireActor());
        }

        if (actorDiagnostics.Count > 0)
        {
            events.Add(Rejected(request, "One or more encounter actors could not be created."));
            return new EncounterPreparationResult(null, actorDiagnostics, events);
        }

        events.AddRange(actors.Select(actor => new EncounterPreparationEvent(
            EncounterPreparationEventKind.ActorPrepared,
            request.TriggerId,
            request.EncounterId,
            actor.State.InstanceId)));
        events.Add(new EncounterPreparationEvent(
            EncounterPreparationEventKind.EncounterPrepared,
            request.TriggerId,
            request.EncounterId,
            Message: $"Prepared {actors.Count} encounter actor(s)."));

        return new EncounterPreparationResult(
            new PreparedEncounter(request.TriggerId, startPlan, actors),
            [],
            events);
    }

    private static EncounterPreparationEvent Rejected(
        RuntimeEncounterTriggerRequest request,
        string message) =>
        new(
            EncounterPreparationEventKind.EncounterRejected,
            request.TriggerId,
            request.EncounterId,
            Message: message);
}
