using Convergence.Runtime;
using Convergence.Hosting;

namespace Convergence.DemoHost.TrainingAnnex;

internal enum TrainingAnnexPartyOperation
{
    SelectActiveHostedEntity,
    DeployAshling,
    SwapDeployedCompanionToWardShell,
    RecallActiveCompanion,
    ReplaceWardShellWithBrambleRunner,
    DismissAshling,
    ConsumeBrambleRunner
}

internal sealed record TrainingAnnexPartyTransitionEvidence(
    string Operation,
    PartyRosterTransitionCode Code,
    bool Committed,
    IReadOnlyList<RuntimeInstanceId> AffectedInstanceIds,
    int ActiveCountBefore,
    int ReserveCountBefore,
    int ActiveCountAfter,
    int ReserveCountAfter,
    RuntimeInstanceId? ActiveHostedEntityBefore,
    RuntimeInstanceId? ActiveHostedEntityAfter,
    int HostedEntityRosterCountBefore,
    int HostedEntityRosterCountAfter,
    int CompanionRosterCountBefore,
    int CompanionRosterCountAfter)
{
    public static TrainingAnnexPartyTransitionEvidence From(
        string operation,
        PartyRosterTransitionResult result,
        bool committed) =>
        new(
            operation,
            result.Code,
            committed,
            result.AffectedInstanceIds,
            result.Before.ActiveParty.Count,
            result.Before.ReserveMembers.Count,
            result.After.ActiveParty.Count,
            result.After.ReserveMembers.Count,
            result.Before.ActiveHostedEntity?.InstanceId,
            result.After.ActiveHostedEntity?.InstanceId,
            result.Before.HostedEntityRoster.Count,
            result.After.HostedEntityRoster.Count,
            result.Before.CompanionRoster.Count,
            result.After.CompanionRoster.Count);
}

internal sealed record TrainingAnnexPartySetupResult(
    RuntimePartyRosterSnapshot Snapshot,
    IReadOnlyList<TrainingAnnexPartyTransitionEvidence> Transitions);

internal sealed class TrainingAnnexPartyController
{
    private const int TrainingAnnexActivePartyLimit = 2;

    private readonly IPartyRosterTransitionService _transitions;

    public TrainingAnnexPartyController(IPartyRosterTransitionService? transitions = null)
    {
        _transitions = transitions ?? new PartyRosterTransitionService();
    }

    public TrainingAnnexPartySetupResult CreateInitialParty(TrainingAnnexActorRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        RuntimeActorReferenceSnapshot player = TrainingAnnexHostSupport.Reference(roster.Player);
        RuntimeActorReferenceSnapshot? activeHostedEntity = roster.OwnedActors
            .Where(member => member.Role == "Active Hosted Entity")
            .Select(TrainingAnnexHostSupport.Reference)
            .FirstOrDefault();
        RuntimeActorReferenceSnapshot[] hostedEntityRoster = roster.OwnedActors
            .Where(member => member.Role is "Active Hosted Entity" or "Hosted Entity roster")
            .Select(TrainingAnnexHostSupport.Reference)
            .ToArray();
        RuntimeActorReferenceSnapshot[] companionRoster = roster.OwnedActors
            .Where(member => member.Role == "Companion roster")
            .Select(TrainingAnnexHostSupport.Reference)
            .ToArray();
        RuntimeActorReferenceSnapshot[] reserveMembers = roster.SupportMembers
            .Select(TrainingAnnexHostSupport.Reference)
            .ToArray();
        var snapshot = new RuntimePartyRosterSnapshot(
            player,
            activeParty: [player],
            reserveMembers: reserveMembers,
            activeHostedEntity: activeHostedEntity,
            hostedEntityRoster: hostedEntityRoster,
            companionRoster: companionRoster,
            maxActivePartySize: TrainingAnnexActivePartyLimit);
        return new TrainingAnnexPartySetupResult(snapshot, []);
    }

    public PartyRosterTransitionResult ExecuteOperation(
        TrainingAnnexPartyOperation operation,
        RuntimePartyRosterSnapshot party,
        TrainingAnnexActorRoster roster)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(roster);
        RuntimeActorSnapshot owner = roster.Player.Actor.State.ToSnapshot();

        return operation switch
        {
            TrainingAnnexPartyOperation.SelectActiveHostedEntity => _transitions.SelectActiveHostedEntity(
                new SelectActiveHostedEntityRequest(
                    party,
                    owner,
                    TrainingAnnexHostSupport.HostedBrambleRunnerInstance)),
            TrainingAnnexPartyOperation.DeployAshling => _transitions.DeployCompanion(
                new DeployCompanionRequest(
                    party,
                    owner,
                    TrainingAnnexHostSupport.CompanionAshlingInstance)),
            TrainingAnnexPartyOperation.SwapDeployedCompanionToWardShell => _transitions.SwapDeployedCompanion(
                new SwapDeployedCompanionRequest(
                    party,
                    owner,
                    TrainingAnnexHostSupport.CompanionAshlingInstance,
                    TrainingAnnexHostSupport.CompanionWardShellInstance)),
            TrainingAnnexPartyOperation.RecallActiveCompanion => RecallActiveCompanion(
                party,
                owner),
            TrainingAnnexPartyOperation.ReplaceWardShellWithBrambleRunner => _transitions.ReplaceCompanion(
                new ReplaceCompanionRequest(
                    party,
                    owner,
                    TrainingAnnexHostSupport.CompanionWardShellInstance,
                    FindRosterReference(roster, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance))),
            TrainingAnnexPartyOperation.DismissAshling => _transitions.DismissCompanion(
                new DismissCompanionRequest(
                    party,
                    owner,
                    TrainingAnnexHostSupport.CompanionAshlingInstance)),
            TrainingAnnexPartyOperation.ConsumeBrambleRunner => _transitions.ConsumeCompanion(
                new ConsumeCompanionRequest(
                    party,
                    owner,
                    TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported party operation.")
        };
    }

    public async ValueTask PrintPartyAsync(
        RuntimePartyRosterSnapshot party,
        IHostEventSink<string> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(eventSink);

        await eventSink.PublishAsync(
            $"Party: active [{FormatActors(party.ActiveParty)}]; reserve [{FormatActors(party.ReserveMembers)}].",
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PrintRosterAsync(
        RuntimePartyRosterSnapshot party,
        IHostEventSink<string> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(eventSink);

        await eventSink.PublishAsync(
            $"Roster: active hosted entity [{FormatActor(party.ActiveHostedEntity)}]; Hosted Entity roster [{FormatActors(party.HostedEntityRoster)}]; Companion roster [{FormatActors(party.CompanionRoster)}].",
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PrintOperationAsync(
        string operation,
        PartyRosterTransitionResult result,
        IHostEventSink<string> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(eventSink);

        if (!result.Applied)
        {
            string diagnostics = string.Join(
                "; ",
                result.Diagnostics.Select(diagnostic => $"[{diagnostic.Code}] {diagnostic.Message}"));
            await eventSink.PublishAsync(
                $"Party roster operation rejected: {operation}; {diagnostics}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await eventSink.PublishAsync(
            $"Party roster operation applied: {operation}; active {result.Before.ActiveParty.Count}->{result.After.ActiveParty.Count}; reserve {result.Before.ReserveMembers.Count}->{result.After.ReserveMembers.Count}; active hosted entity {FormatInstance(result.Before.ActiveHostedEntity)}->{FormatInstance(result.After.ActiveHostedEntity)}; Hosted Entity roster {result.Before.HostedEntityRoster.Count}->{result.After.HostedEntityRoster.Count}; Companion roster {result.Before.CompanionRoster.Count}->{result.After.CompanionRoster.Count}.",
            cancellationToken).ConfigureAwait(false);
    }

    private PartyRosterTransitionResult RecallActiveCompanion(
        RuntimePartyRosterSnapshot party,
        RuntimeActorSnapshot owner)
    {
        RuntimeActorReferenceSnapshot? activeCompanion = party.ActiveParty.FirstOrDefault(actor =>
            party.CompanionRoster.Any(companion => companion.InstanceId == actor.InstanceId));
        if (activeCompanion is null)
        {
            return new PartyRosterTransitionResult(
                PartyRosterTransitionCode.NotActive,
                party,
                party,
                diagnostics:
                [
                    new PartyRosterTransitionDiagnostic(
                        PartyRosterTransitionCode.NotActive,
                        "No active companion is in the party.")
                ]);
        }

        return _transitions.RecallCompanion(
            new RecallCompanionRequest(party, owner, activeCompanion.InstanceId));
    }

    private static RuntimeActorReferenceSnapshot FindRosterReference(
        TrainingAnnexActorRoster roster,
        RuntimeInstanceId instanceId)
    {
        TrainingAnnexRuntimeActor? actor = roster.AllActors.FirstOrDefault(candidate =>
            candidate.Actor.State.InstanceId == instanceId);
        if (actor is null)
        {
            throw new InvalidOperationException(
                $"Training Annex operation candidate '{instanceId}' was not hydrated.");
        }

        return TrainingAnnexHostSupport.Reference(actor);
    }

    private static string FormatActor(RuntimeActorReferenceSnapshot? actor) =>
        actor is null ? "none" : $"{actor.DisplayName} ({actor.InstanceId})";

    private static string FormatInstance(RuntimeActorReferenceSnapshot? actor) =>
        actor?.InstanceId.ToString() ?? "none";

    private static string FormatActors(IReadOnlyList<RuntimeActorReferenceSnapshot> actors) =>
        actors.Count == 0
            ? "none"
            : string.Join(", ", actors.Select(actor => $"{actor.DisplayName} ({actor.InstanceId})"));
}
