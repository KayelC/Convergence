using JRPGPrototype.Logic.Runtime;
using JRPGPrototype.Hosting;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexPartyTransitionEvidence(
    string Operation,
    PartyStockTransitionCode Code,
    IReadOnlyList<RuntimeInstanceId> AffectedInstanceIds,
    int ActiveCountBefore,
    int ReserveCountBefore,
    int ActiveCountAfter,
    int ReserveCountAfter)
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
            result.After.ReserveMembers.Count);
}

internal sealed record TrainingAnnexPartySetupResult(
    RuntimePartyStockSnapshot Snapshot,
    IReadOnlyList<TrainingAnnexPartyTransitionEvidence> Transitions);

internal sealed class TrainingAnnexPartyController
{
    private const int TrainingAnnexActivePartyLimit = 1;

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
        var snapshot = new RuntimePartyStockSnapshot(
            player,
            roster.Player.Level,
            activeParty: [player],
            activeForm: activeForm,
            personaStock: personaStock,
            demonStock: demonStock,
            maxActivePartySize: TrainingAnnexActivePartyLimit);
        var evidence = new List<TrainingAnnexPartyTransitionEvidence>();

        foreach (TrainingAnnexRuntimeActor member in roster.SupportMembers)
        {
            PartyStockTransitionResult result = _transitions.AddPartyMember(
                new AddPartyMemberRequest(snapshot, TrainingAnnexHostSupport.Reference(member)));
            evidence.Add(TrainingAnnexPartyTransitionEvidence.From("add_party_member", result));
            snapshot = result.After;
        }

        return new TrainingAnnexPartySetupResult(snapshot, evidence);
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

    private static string FormatActor(RuntimeActorReferenceSnapshot? actor) =>
        actor is null ? "none" : $"{actor.DisplayName} ({actor.InstanceId})";

    private static string FormatActors(IReadOnlyList<RuntimeActorReferenceSnapshot> actors) =>
        actors.Count == 0
            ? "none"
            : string.Join(", ", actors.Select(actor => $"{actor.DisplayName} ({actor.InstanceId})"));
}
