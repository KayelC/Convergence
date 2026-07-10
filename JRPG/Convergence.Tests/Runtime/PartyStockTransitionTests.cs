using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class PartyStockTransitionTests
{
    private readonly PartyStockTransitionService _service = new();

    [Theory]
    [InlineData(1, 3)]
    [InlineData(9, 3)]
    [InlineData(10, 5)]
    [InlineData(19, 5)]
    [InlineData(20, 7)]
    [InlineData(29, 7)]
    [InlineData(30, 10)]
    [InlineData(39, 10)]
    [InlineData(40, 12)]
    public void LegacyStockCapacityPolicy_PreservesLevelThresholds(int level, int expected)
    {
        var policy = new LegacyStockCapacityPolicy();

        Assert.Equal(expected, policy.GetCapacity(level));
    }

    [Fact]
    public void AddAndSwapPartyMembers_PreserveActiveLimitAndReserveOrder()
    {
        RuntimePartyStockSnapshot snapshot = Snapshot(activeParty: [Actor("hero"), Actor("a"), Actor("b"), Actor("c")]);

        PartyStockTransitionResult add = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, Actor("reserve")));

        Assert.True(add.Applied);
        Assert.Equal(4, add.After.ActiveParty.Count);
        Assert.Equal("reserve", Assert.Single(add.After.ReserveMembers).InstanceId.ToString());

        PartyStockTransitionResult swap = _service.SwapPartyMember(new SwapPartyMemberRequest(add.After, ActiveIndex: 2, ReserveIndex: 0));

        Assert.True(swap.Applied);
        Assert.Equal(["hero", "a", "reserve", "c"], swap.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["b"], swap.After.ReserveMembers.Select(actor => actor.InstanceId.ToString()));
    }

    [Fact]
    public void DemonCommands_PreserveUnifiedActiveAndOwnedStock()
    {
        RuntimeActorReferenceSnapshot pixie = Actor("pixie");
        RuntimeActorReferenceSnapshot jack = Actor("jack");
        RuntimePartyStockSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero")],
            demonStock: [pixie, jack]);

        PartyStockTransitionResult summon = _service.SummonDemon(new SummonDemonRequest(snapshot, pixie.InstanceId));

        Assert.True(summon.Applied);
        Assert.Equal(["hero", "pixie"], summon.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie", "jack"], summon.After.DemonStock.Select(actor => actor.InstanceId.ToString()));

        PartyStockTransitionResult swap = _service.SwapActiveDemon(new SwapActiveDemonRequest(summon.After, pixie.InstanceId, jack.InstanceId));

        Assert.True(swap.Applied);
        Assert.Equal(["hero", "jack"], swap.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie", "jack"], swap.After.DemonStock.Select(actor => actor.InstanceId.ToString()));

        PartyStockTransitionResult returned = _service.ReturnDemon(new ReturnDemonRequest(swap.After, jack.InstanceId));

        Assert.True(returned.Applied);
        Assert.Equal(["hero"], returned.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie", "jack"], returned.After.DemonStock.Select(actor => actor.InstanceId.ToString()));

        PartyStockTransitionResult dismissed = _service.DismissDemon(new DismissDemonRequest(returned.After, jack.InstanceId));

        Assert.True(dismissed.Applied);
        Assert.Equal(["pixie"], dismissed.After.DemonStock.Select(actor => actor.InstanceId.ToString()));
    }

    [Fact]
    public void AddDemonToStock_AppendsOwnedDemonAndRejectsDuplicateOrFullStock()
    {
        RuntimeActorReferenceSnapshot pixie = Actor("pixie");
        RuntimePartyStockSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero")],
            demonStock: [Actor("jack"), Actor("angel")]);

        PartyStockTransitionResult added = _service.AddDemonToStock(new AddDemonToStockRequest(snapshot, pixie));

        Assert.True(added.Applied);
        Assert.Equal(["jack", "angel", "pixie"], added.After.DemonStock.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie"], added.AffectedInstanceIds.Select(id => id.ToString()));

        PartyStockTransitionResult duplicate = _service.AddDemonToStock(new AddDemonToStockRequest(added.After, pixie));

        Assert.False(duplicate.Applied);
        Assert.Equal(PartyStockTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Same(added.After, duplicate.After);

        RuntimePartyStockSnapshot fullSnapshot = Snapshot(
            ownerLevel: 1,
            activeParty: [Actor("hero")],
            demonStock: [Actor("a"), Actor("b"), Actor("c")]);

        PartyStockTransitionResult full = _service.AddDemonToStock(new AddDemonToStockRequest(
            fullSnapshot,
            Actor("full_candidate")));

        Assert.False(full.Applied);
        Assert.Equal(PartyStockTransitionCode.StockFull, full.Code);
        Assert.Same(fullSnapshot, full.After);
    }

    [Fact]
    public void DemonReplacementAndConsumption_UpdateActiveAndStockReferencesAtomically()
    {
        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimeActorReferenceSnapshot newDemon = Actor("new_demon");
        RuntimePartyStockSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero"), oldDemon],
            demonStock: [oldDemon]);

        PartyStockTransitionResult replaced = _service.ReplaceDemon(new ReplaceDemonRequest(
            snapshot,
            oldDemon.InstanceId,
            newDemon));

        Assert.True(replaced.Applied);
        Assert.Equal(["hero", "new_demon"], replaced.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["new_demon"], replaced.After.DemonStock.Select(actor => actor.InstanceId.ToString()));

        PartyStockTransitionResult consumed = _service.ConsumeDemon(new ConsumeDemonRequest(replaced.After, newDemon.InstanceId));

        Assert.True(consumed.Applied);
        Assert.Equal(["hero"], consumed.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Empty(consumed.After.DemonStock);
    }

    [Fact]
    public void PersonaCommands_ExchangeConsumeAndReplaceActiveFormAndStock()
    {
        RuntimeActorReferenceSnapshot active = Actor("orpheus");
        RuntimeActorReferenceSnapshot stock = Actor("pixie");
        RuntimeActorReferenceSnapshot replacement = Actor("jack_frost");
        RuntimePartyStockSnapshot snapshot = Snapshot(activeForm: active, personaStock: [stock]);

        PartyStockTransitionResult swapped = _service.SwapActivePersona(new SwapActivePersonaRequest(snapshot, stock.InstanceId));

        Assert.True(swapped.Applied);
        Assert.Equal("pixie", swapped.After.ActiveForm?.InstanceId.ToString());
        Assert.Equal(["orpheus"], swapped.After.PersonaStock.Select(persona => persona.InstanceId.ToString()));

        PartyStockTransitionResult replaced = _service.ReplacePersona(new ReplacePersonaRequest(
            swapped.After,
            active.InstanceId,
            replacement));

        Assert.True(replaced.Applied);
        Assert.Equal(["jack_frost"], replaced.After.PersonaStock.Select(persona => persona.InstanceId.ToString()));

        PartyStockTransitionResult consumed = _service.ConsumePersona(new ConsumePersonaRequest(replaced.After, stock.InstanceId));

        Assert.True(consumed.Applied);
        Assert.Null(consumed.After.ActiveForm);
        Assert.Equal(["jack_frost"], consumed.After.PersonaStock.Select(persona => persona.InstanceId.ToString()));
    }

    [Fact]
    public void RejectedCommands_ReturnStableCodesAndUnchangedSnapshots()
    {
        RuntimePartyStockSnapshot snapshot = Snapshot(activeParty: [Actor("hero")]);
        RuntimeInstanceId missing = RuntimeInstanceId.Parse("missing");

        PartyStockTransitionResult result = _service.SummonDemon(new SummonDemonRequest(snapshot, missing));

        Assert.False(result.Applied);
        Assert.Equal(PartyStockTransitionCode.NotOwned, result.Code);
        Assert.Same(result.Before, result.After);
        PartyStockTransitionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PartyStockTransitionCode.NotOwned, diagnostic.Code);
        Assert.Equal(missing, diagnostic.SubjectInstanceId);
    }

    [Fact]
    public void DuplicateOwnershipAndFullStockFailures_DoNotMutate()
    {
        RuntimeActorReferenceSnapshot demon = Actor("pixie");
        RuntimePartyStockSnapshot duplicateSnapshot = Snapshot(activeParty: [Actor("hero")], demonStock: [demon]);

        PartyStockTransitionResult duplicate = _service.ReplaceDemon(new ReplaceDemonRequest(
            duplicateSnapshot,
            demon.InstanceId,
            demon));

        Assert.Equal(PartyStockTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Equal(duplicateSnapshot, duplicate.After);

        RuntimeActorReferenceSnapshot activeOnly = Actor("active_only");
        RuntimePartyStockSnapshot fullSnapshot = Snapshot(
            ownerLevel: 1,
            activeParty: [Actor("hero"), activeOnly],
            demonStock: [Actor("a"), Actor("b"), Actor("c")]);

        PartyStockTransitionResult full = _service.ReplaceDemon(new ReplaceDemonRequest(
            fullSnapshot,
            activeOnly.InstanceId,
            Actor("overflow")));

        Assert.Equal(PartyStockTransitionCode.StockFull, full.Code);
        Assert.Equal(fullSnapshot, full.After);
    }

    [Fact]
    public void SnapshotsDefensivelyCopyInputCollections()
    {
        var active = new List<RuntimeActorReferenceSnapshot> { Actor("hero") };
        RuntimePartyStockSnapshot snapshot = Snapshot(activeParty: active);

        active.Add(Actor("late"));

        Assert.Equal(["hero"], snapshot.ActiveParty.Select(actor => actor.InstanceId.ToString()));
    }

    private static RuntimePartyStockSnapshot Snapshot(
        int ownerLevel = 40,
        IEnumerable<RuntimeActorReferenceSnapshot>? activeParty = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? reserveMembers = null,
        RuntimeActorReferenceSnapshot? activeForm = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? personaStock = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? demonStock = null) =>
        new(
            Actor("hero"),
            ownerLevel,
            activeParty ?? [Actor("hero")],
            reserveMembers,
            activeForm,
            personaStock,
            demonStock);

    private static RuntimeActorReferenceSnapshot Actor(string id) =>
        new(RuntimeInstanceId.Parse(id), ContentId.Parse(id), id);
}
