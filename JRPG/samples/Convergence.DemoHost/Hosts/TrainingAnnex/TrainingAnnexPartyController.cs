using Convergence.Runtime;
using Convergence.Hosting;

namespace Convergence.DemoHost.TrainingAnnex;

internal enum TrainingAnnexPartyOperation
{
    SwapActiveForm,
    SummonAshling,
    SwapActiveDemonToWardShell,
    ReturnActiveDemon,
    ReplaceWardShellWithBrambleRunner,
    DismissAshling,
    ConsumeBrambleRunner
}

internal sealed record TrainingAnnexPartyTransitionEvidence(
    string Operation,
    PartyStockTransitionCode Code,
    IReadOnlyList<RuntimeInstanceId> AffectedInstanceIds,
    int ActiveCountBefore,
    int ReserveCountBefore,
    int ActiveCountAfter,
    int ReserveCountAfter,
    RuntimeInstanceId? ActiveFormBefore,
    RuntimeInstanceId? ActiveFormAfter,
    int PersonaStockCountBefore,
    int PersonaStockCountAfter,
    int DemonStockCountBefore,
    int DemonStockCountAfter)
{
    public static TrainingAnnexPartyTransitionEvidence From(
        string operation,
        PartyStockTransitionResult result) =>
        new(
            operation,
            result.Code,
            result.AffectedInstanceIds,
            result.Before.ActiveParty.Count,
            result.Before.ReserveMembers.Count,
            result.After.ActiveParty.Count,
            result.After.ReserveMembers.Count,
            result.Before.ActiveForm?.InstanceId,
            result.After.ActiveForm?.InstanceId,
            result.Before.PersonaStock.Count,
            result.After.PersonaStock.Count,
            result.Before.DemonStock.Count,
            result.After.DemonStock.Count);
}

internal sealed record TrainingAnnexPartySetupResult(
    RuntimePartyStockSnapshot Snapshot,
    IReadOnlyList<TrainingAnnexPartyTransitionEvidence> Transitions);

internal sealed class TrainingAnnexPartyController
{
    private const int TrainingAnnexActivePartyLimit = 2;

    private readonly IPartyStockTransitionService _transitions;

    public TrainingAnnexPartyController(IPartyStockTransitionService? transitions = null)
    {
        _transitions = transitions ?? new PartyStockTransitionService();
    }

    public TrainingAnnexPartySetupResult CreateInitialParty(TrainingAnnexActorRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        RuntimeActorReferenceSnapshot player = TrainingAnnexHostSupport.Reference(roster.Player);
        RuntimeActorReferenceSnapshot? activeForm = roster.StockMembers
            .Where(member => member.Role == "Active Form")
            .Select(TrainingAnnexHostSupport.Reference)
            .FirstOrDefault();
        RuntimeActorReferenceSnapshot[] personaStock = roster.StockMembers
            .Where(member => member.Role == "Persona Stock")
            .Select(TrainingAnnexHostSupport.Reference)
            .ToArray();
        RuntimeActorReferenceSnapshot[] demonStock = roster.StockMembers
            .Where(member => member.Role == "Demon Stock")
            .Select(TrainingAnnexHostSupport.Reference)
            .ToArray();
        RuntimeActorReferenceSnapshot[] reserveMembers = roster.SupportMembers
            .Select(TrainingAnnexHostSupport.Reference)
            .ToArray();
        var snapshot = new RuntimePartyStockSnapshot(
            player,
            roster.Player.Level,
            activeParty: [player],
            reserveMembers: reserveMembers,
            activeForm: activeForm,
            personaStock: personaStock,
            demonStock: demonStock,
            maxActivePartySize: TrainingAnnexActivePartyLimit);
        return new TrainingAnnexPartySetupResult(snapshot, []);
    }

    public PartyStockTransitionResult ExecuteOperation(
        TrainingAnnexPartyOperation operation,
        RuntimePartyStockSnapshot party,
        TrainingAnnexActorRoster roster)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(roster);

        return operation switch
        {
            TrainingAnnexPartyOperation.SwapActiveForm => _transitions.SwapActivePersona(
                new SwapActivePersonaRequest(party, TrainingAnnexHostSupport.PersonaBrambleRunnerInstance)),
            TrainingAnnexPartyOperation.SummonAshling => _transitions.SummonDemon(
                new SummonDemonRequest(party, TrainingAnnexHostSupport.DemonAshlingInstance)),
            TrainingAnnexPartyOperation.SwapActiveDemonToWardShell => _transitions.SwapActiveDemon(
                new SwapActiveDemonRequest(
                    party,
                    TrainingAnnexHostSupport.DemonAshlingInstance,
                    TrainingAnnexHostSupport.DemonWardShellInstance)),
            TrainingAnnexPartyOperation.ReturnActiveDemon => ReturnActiveDemon(party),
            TrainingAnnexPartyOperation.ReplaceWardShellWithBrambleRunner => _transitions.ReplaceDemon(
                new ReplaceDemonRequest(
                    party,
                    TrainingAnnexHostSupport.DemonWardShellInstance,
                    FindRosterReference(roster, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance))),
            TrainingAnnexPartyOperation.DismissAshling => _transitions.DismissDemon(
                new DismissDemonRequest(party, TrainingAnnexHostSupport.DemonAshlingInstance)),
            TrainingAnnexPartyOperation.ConsumeBrambleRunner => _transitions.ConsumeDemon(
                new ConsumeDemonRequest(party, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported party operation.")
        };
    }

    public async ValueTask PrintPartyAsync(
        RuntimePartyStockSnapshot party,
        IHostEventSink<string> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(eventSink);

        await eventSink.PublishAsync(
            $"Party: active [{FormatActors(party.ActiveParty)}]; reserve [{FormatActors(party.ReserveMembers)}].",
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PrintStockAsync(
        RuntimePartyStockSnapshot party,
        IHostEventSink<string> eventSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(eventSink);

        await eventSink.PublishAsync(
            $"Stock: active form [{FormatActor(party.ActiveForm)}]; Persona stock [{FormatActors(party.PersonaStock)}]; Demon stock [{FormatActors(party.DemonStock)}].",
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PrintOperationAsync(
        string operation,
        PartyStockTransitionResult result,
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
                $"Party stock operation rejected: {operation}; {diagnostics}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await eventSink.PublishAsync(
            $"Party stock operation applied: {operation}; active {result.Before.ActiveParty.Count}->{result.After.ActiveParty.Count}; reserve {result.Before.ReserveMembers.Count}->{result.After.ReserveMembers.Count}; active form {FormatInstance(result.Before.ActiveForm)}->{FormatInstance(result.After.ActiveForm)}; Persona stock {result.Before.PersonaStock.Count}->{result.After.PersonaStock.Count}; Demon stock {result.Before.DemonStock.Count}->{result.After.DemonStock.Count}.",
            cancellationToken).ConfigureAwait(false);
    }

    private PartyStockTransitionResult ReturnActiveDemon(RuntimePartyStockSnapshot party)
    {
        RuntimeActorReferenceSnapshot? activeDemon = party.ActiveParty.FirstOrDefault(actor =>
            party.DemonStock.Any(demon => demon.InstanceId == actor.InstanceId));
        if (activeDemon is null)
        {
            return new PartyStockTransitionResult(
                PartyStockTransitionCode.NotActive,
                party,
                party,
                diagnostics:
                [
                    new PartyStockTransitionDiagnostic(
                        PartyStockTransitionCode.NotActive,
                        "No active demon is in the party.")
                ]);
        }

        return _transitions.ReturnDemon(new ReturnDemonRequest(party, activeDemon.InstanceId));
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
