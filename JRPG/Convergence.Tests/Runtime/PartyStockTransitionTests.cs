using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class PartyStockTransitionTests
{
    private readonly PartyStockTransitionService _service = new(new TieredStockCapacityPolicy(
    [
        new StockCapacityTier(1, 3),
        new StockCapacityTier(10, 5),
        new StockCapacityTier(20, 7),
        new StockCapacityTier(30, 10),
        new StockCapacityTier(40, 12)
    ]));

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
    public void TieredStockCapacityPolicy_UsesDeveloperAuthoredThresholds(int level, int expected)
    {
        var policy = new TieredStockCapacityPolicy(
        [
            new StockCapacityTier(1, 3),
            new StockCapacityTier(10, 5),
            new StockCapacityTier(20, 7),
            new StockCapacityTier(30, 10),
            new StockCapacityTier(40, 12)
        ]);

        Assert.Equal(expected, policy.GetCapacity(level));
    }

    [Fact]
    public void DefaultTransitionService_DoesNotImposeAStockCapacityCurve()
    {
        var service = new PartyStockTransitionService();
        RuntimeActorReferenceSnapshot[] stock = Enumerable.Range(0, 20)
            .Select(index => Actor($"demon_{index}"))
            .ToArray();
        RuntimePartyStockSnapshot snapshot = Snapshot(demonStock: stock);

        PartyStockTransitionResult result = service.AddDemonToStock(
            new AddDemonToStockRequest(snapshot, Actor("demon_20")));

        Assert.True(result.Applied);
        Assert.Equal(21, result.After.DemonStock.Count);
    }

    [Fact]
    public void TieredStockCapacityPolicy_CopiesAndValidatesAuthoredTiers()
    {
        var authored = new List<StockCapacityTier>
        {
            new(1, 2),
            new(10, 4)
        };
        var policy = new TieredStockCapacityPolicy(authored);

        authored.Clear();

        Assert.Equal(2, policy.GetCapacity(1));
        Assert.Equal(4, policy.GetCapacity(10));
        Assert.Throws<ArgumentException>(() => new TieredStockCapacityPolicy([new StockCapacityTier(2, 1)]));
        Assert.Throws<ArgumentException>(() => new TieredStockCapacityPolicy(
        [
            new StockCapacityTier(1, 1),
            new StockCapacityTier(1, 2)
        ]));
        Assert.Throws<NotSupportedException>(() => ((IList<StockCapacityTier>)policy.Tiers).Add(new(20, 6)));
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
    public void AddPartyMember_RejectsRuntimeIdsUsedByFormOrStockRoles()
    {
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyStockSnapshot[] snapshots =
        [
            Snapshot(activeForm: collision),
            Snapshot(personaStock: [collision]),
            Snapshot(demonStock: [collision])
        ];

        foreach (RuntimePartyStockSnapshot snapshot in snapshots)
        {
            AssertIdentityCollision(
                snapshot,
                _service.AddPartyMember(new AddPartyMemberRequest(snapshot, collision)),
                collision.InstanceId);
        }
    }

    [Fact]
    public void AddPartyMember_AllowsExactOwnerReferenceToEnterAnOpenActiveSlot()
    {
        RuntimeActorReferenceSnapshot owner = Actor("owner");
        RuntimePartyStockSnapshot snapshot = new(owner, ownerLevel: 40);

        PartyStockTransitionResult result = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, owner));

        Assert.True(result.Applied);
        Assert.Equal(owner, Assert.Single(result.After.ActiveParty));
        Assert.Empty(result.After.ReserveMembers);
    }

    [Fact]
    public void AddPartyMember_RejectsOwnedDemonIdWhileSummonPreservesIntentionalOverlap()
    {
        RuntimeActorReferenceSnapshot demon = Actor("owned_demon");
        RuntimePartyStockSnapshot snapshot = Snapshot(demonStock: [demon]);

        PartyStockTransitionResult add = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, demon));
        PartyStockTransitionResult summon = _service.SummonDemon(new SummonDemonRequest(snapshot, demon.InstanceId));

        AssertIdentityCollision(snapshot, add, demon.InstanceId);
        Assert.True(summon.Applied);
        Assert.Contains(summon.After.ActiveParty, actor => actor.InstanceId == demon.InstanceId);
        Assert.Contains(summon.After.DemonStock, actor => actor.InstanceId == demon.InstanceId);
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

    [Theory]
    [InlineData("owner")]
    [InlineData("ally")]
    public void DemonCommands_RejectActiveActorsWithoutDemonStockOwnership(string subjectId)
    {
        RuntimeActorReferenceSnapshot owner = Actor("owner");
        RuntimeActorReferenceSnapshot ally = Actor("ally");
        RuntimeActorReferenceSnapshot standby = Actor("owned_demon");
        RuntimeActorReferenceSnapshot subject = subjectId == "owner" ? owner : ally;
        var snapshot = new RuntimePartyStockSnapshot(
            owner,
            ownerLevel: 40,
            activeParty: [owner, ally],
            demonStock: [standby]);

        PartyStockTransitionResult[] results =
        [
            _service.SwapActiveDemon(new SwapActiveDemonRequest(
                snapshot,
                subject.InstanceId,
                standby.InstanceId)),
            _service.ReturnDemon(new ReturnDemonRequest(snapshot, subject.InstanceId)),
            _service.ReplaceDemon(new ReplaceDemonRequest(
                snapshot,
                subject.InstanceId,
                Actor($"replacement_{subjectId}"))),
            _service.ConsumeDemon(new ConsumeDemonRequest(snapshot, subject.InstanceId))
        ];

        foreach (PartyStockTransitionResult result in results)
        {
            AssertRoleRejection(snapshot, result, subject.InstanceId, PartyStockTransitionCode.NotOwned);
        }
    }

    [Fact]
    public void DemonDeploymentCommands_RequireBothOwnershipAndActiveMembership()
    {
        RuntimeActorReferenceSnapshot standby = Actor("standby");
        RuntimeActorReferenceSnapshot replacement = Actor("replacement");
        RuntimePartyStockSnapshot snapshot = Snapshot(demonStock: [standby, replacement]);

        PartyStockTransitionResult swap = _service.SwapActiveDemon(new SwapActiveDemonRequest(
            snapshot,
            standby.InstanceId,
            replacement.InstanceId));
        PartyStockTransitionResult returned = _service.ReturnDemon(new ReturnDemonRequest(
            snapshot,
            standby.InstanceId));

        AssertRoleRejection(snapshot, swap, standby.InstanceId, PartyStockTransitionCode.NotActive);
        AssertRoleRejection(snapshot, returned, standby.InstanceId, PartyStockTransitionCode.NotActive);
    }

    [Fact]
    public void DemonStockReplacementAndConsumption_DoNotRequireActiveDeployment()
    {
        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimeActorReferenceSnapshot consumedDemon = Actor("consumed_demon");
        RuntimeActorReferenceSnapshot newDemon = Actor("new_demon");
        RuntimePartyStockSnapshot snapshot = Snapshot(demonStock: [oldDemon, consumedDemon]);

        PartyStockTransitionResult replaced = _service.ReplaceDemon(new ReplaceDemonRequest(
            snapshot,
            oldDemon.InstanceId,
            newDemon));
        PartyStockTransitionResult consumed = _service.ConsumeDemon(new ConsumeDemonRequest(
            replaced.After,
            consumedDemon.InstanceId));

        Assert.True(replaced.Applied);
        Assert.Equal(["new_demon", "consumed_demon"], replaced.After.DemonStock.Select(DemonId));
        Assert.Equal(["hero"], replaced.After.ActiveParty.Select(DemonId));
        Assert.True(consumed.Applied);
        Assert.Equal(["new_demon"], consumed.After.DemonStock.Select(DemonId));
        Assert.Equal(["hero"], consumed.After.ActiveParty.Select(DemonId));
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
    public void AddPersonaToStock_AppendsAndRejectsDuplicateOrFullStockWithoutMutation()
    {
        RuntimePartyStockSnapshot snapshot = Snapshot(
            ownerLevel: 1,
            activeForm: Actor("orpheus"),
            personaStock: [Actor("pixie"), Actor("angel")]);
        RuntimeActorReferenceSnapshot candidate = Actor("jack_frost");

        PartyStockTransitionResult added = _service.AddPersonaToStock(
            new AddPersonaToStockRequest(snapshot, candidate));

        Assert.True(added.Applied);
        Assert.Equal(
            ["pixie", "angel", "jack_frost"],
            added.After.PersonaStock.Select(persona => persona.InstanceId.ToString()));

        PartyStockTransitionResult duplicate = _service.AddPersonaToStock(
            new AddPersonaToStockRequest(added.After, candidate));
        Assert.Equal(PartyStockTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Same(added.After, duplicate.After);

        PartyStockTransitionResult full = _service.AddPersonaToStock(
            new AddPersonaToStockRequest(added.After, Actor("overflow")));
        Assert.Equal(PartyStockTransitionCode.StockFull, full.Code);
        Assert.Same(added.After, full.After);
    }

    [Fact]
    public void StockAdditions_RejectRuntimeIdsUsedByAnyOtherOwnershipRole()
    {
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyStockSnapshot[] demonCollisions =
        [
            new RuntimePartyStockSnapshot(collision, 40),
            Snapshot(reserveMembers: [collision]),
            Snapshot(activeForm: collision),
            Snapshot(personaStock: [collision])
        ];
        RuntimePartyStockSnapshot[] personaCollisions =
        [
            new RuntimePartyStockSnapshot(collision, 40),
            Snapshot(activeParty: [Actor("hero"), collision]),
            Snapshot(reserveMembers: [collision]),
            Snapshot(demonStock: [collision])
        ];

        foreach (RuntimePartyStockSnapshot snapshot in demonCollisions)
        {
            AssertIdentityCollision(
                snapshot,
                _service.AddDemonToStock(new AddDemonToStockRequest(snapshot, collision)),
                collision.InstanceId);
        }

        foreach (RuntimePartyStockSnapshot snapshot in personaCollisions)
        {
            AssertIdentityCollision(
                snapshot,
                _service.AddPersonaToStock(new AddPersonaToStockRequest(snapshot, collision)),
                collision.InstanceId);
        }
    }

    [Fact]
    public void StockReplacements_RejectRuntimeIdsUsedByTheOppositeStockFamily()
    {
        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimeActorReferenceSnapshot oldPersona = Actor("old_persona");
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyStockSnapshot demonSnapshot = Snapshot(
            personaStock: [collision],
            demonStock: [oldDemon]);
        RuntimePartyStockSnapshot personaSnapshot = Snapshot(
            personaStock: [oldPersona],
            demonStock: [collision]);

        PartyStockTransitionResult demon = _service.ReplaceDemon(new ReplaceDemonRequest(
            demonSnapshot,
            oldDemon.InstanceId,
            collision));
        PartyStockTransitionResult persona = _service.ReplacePersona(new ReplacePersonaRequest(
            personaSnapshot,
            oldPersona.InstanceId,
            collision));

        AssertIdentityCollision(demonSnapshot, demon, collision.InstanceId);
        AssertIdentityCollision(personaSnapshot, persona, collision.InstanceId);
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
    public void DuplicateOwnershipAndMalformedOverCapacityFailures_DoNotMutate()
    {
        RuntimeActorReferenceSnapshot demon = Actor("pixie");
        RuntimePartyStockSnapshot duplicateSnapshot = Snapshot(activeParty: [Actor("hero")], demonStock: [demon]);

        PartyStockTransitionResult duplicate = _service.ReplaceDemon(new ReplaceDemonRequest(
            duplicateSnapshot,
            demon.InstanceId,
            demon));

        Assert.Equal(PartyStockTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Equal(duplicateSnapshot, duplicate.After);

        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimePartyStockSnapshot fullSnapshot = Snapshot(
            ownerLevel: 1,
            activeParty: [Actor("hero")],
            demonStock: [oldDemon, Actor("a"), Actor("b"), Actor("c")]);

        PartyStockTransitionResult full = _service.ReplaceDemon(new ReplaceDemonRequest(
            fullSnapshot,
            oldDemon.InstanceId,
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

    private static void AssertIdentityCollision(
        RuntimePartyStockSnapshot expectedSnapshot,
        PartyStockTransitionResult result,
        RuntimeInstanceId instanceId)
    {
        Assert.False(result.Applied);
        Assert.Equal(PartyStockTransitionCode.RuntimeInstanceIdInUse, result.Code);
        Assert.Same(expectedSnapshot, result.Before);
        Assert.Same(expectedSnapshot, result.After);
        PartyStockTransitionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PartyStockTransitionCode.RuntimeInstanceIdInUse, diagnostic.Code);
        Assert.Equal(instanceId, diagnostic.SubjectInstanceId);
    }

    private static void AssertRoleRejection(
        RuntimePartyStockSnapshot expectedSnapshot,
        PartyStockTransitionResult result,
        RuntimeInstanceId instanceId,
        PartyStockTransitionCode expectedCode)
    {
        Assert.False(result.Applied);
        Assert.Equal(expectedCode, result.Code);
        Assert.Same(expectedSnapshot, result.Before);
        Assert.Same(expectedSnapshot, result.After);
        PartyStockTransitionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(instanceId, diagnostic.SubjectInstanceId);
    }

    private static string DemonId(RuntimeActorReferenceSnapshot actor) => actor.InstanceId.ToString();
}
