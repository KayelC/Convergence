using Convergence.Content;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class PartyRosterTransitionTests
{
    private readonly PartyRosterTransitionService _service = new(new TieredRosterCapacityPolicy(
    [
        new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 1, 3),
        new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 10, 5),
        new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 20, 7),
        new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 30, 10),
        new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 40, 12),
        new RosterCapacityTier(RuntimeRosterKind.Companion, 1, 3),
        new RosterCapacityTier(RuntimeRosterKind.Companion, 10, 5),
        new RosterCapacityTier(RuntimeRosterKind.Companion, 20, 7),
        new RosterCapacityTier(RuntimeRosterKind.Companion, 30, 10),
        new RosterCapacityTier(RuntimeRosterKind.Companion, 40, 12)
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
    public void TieredRosterCapacityPolicy_UsesDeveloperAuthoredThresholds(int level, int expected)
    {
        var policy = new TieredRosterCapacityPolicy(
        [
            new RosterCapacityTier(RuntimeRosterKind.Companion, 1, 3),
            new RosterCapacityTier(RuntimeRosterKind.Companion, 10, 5),
            new RosterCapacityTier(RuntimeRosterKind.Companion, 20, 7),
            new RosterCapacityTier(RuntimeRosterKind.Companion, 30, 10),
            new RosterCapacityTier(RuntimeRosterKind.Companion, 40, 12)
        ]);

        Assert.Equal(expected, policy.GetCapacity(RuntimeRosterKind.Companion, level));
        Assert.Equal(0, policy.GetCapacity(RuntimeRosterKind.HostedEntity, level));
    }

    [Fact]
    public void DefaultTransitionService_DoesNotImposeARosterCapacityCurve()
    {
        var service = new PartyRosterTransitionService();
        RuntimeActorReferenceSnapshot[] stock = Enumerable.Range(0, 20)
            .Select(index => Actor($"demon_{index}"))
            .ToArray();
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: stock);

        PartyRosterTransitionResult result = service.AddCompanionToRoster(
            new AddCompanionToRosterRequest(snapshot, Actor("demon_20")));

        Assert.True(result.Applied);
        Assert.Equal(21, result.After.CompanionRoster.Count);
    }

    [Fact]
    public void TieredRosterCapacityPolicy_CopiesAndValidatesAuthoredTiers()
    {
        var authored = new List<RosterCapacityTier>
        {
            new(RuntimeRosterKind.HostedEntity, 1, 2),
            new(RuntimeRosterKind.HostedEntity, 10, 4)
        };
        var policy = new TieredRosterCapacityPolicy(authored);

        authored.Clear();

        Assert.Equal(2, policy.GetCapacity(RuntimeRosterKind.HostedEntity, 1));
        Assert.Equal(4, policy.GetCapacity(RuntimeRosterKind.HostedEntity, 10));
        Assert.Throws<ArgumentException>(() => new TieredRosterCapacityPolicy([new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 2, 1)]));
        Assert.Throws<ArgumentException>(() => new TieredRosterCapacityPolicy(
        [
            new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 1, 1),
            new RosterCapacityTier(RuntimeRosterKind.HostedEntity, 1, 2)
        ]));
        Assert.Throws<NotSupportedException>(() => ((IList<RosterCapacityTier>)policy.Tiers).Add(new(RuntimeRosterKind.HostedEntity, 20, 6)));
    }

    [Fact]
    public void AddAndSwapPartyMembers_PreserveActiveLimitAndReserveOrder()
    {
        RuntimePartyRosterSnapshot snapshot = Snapshot(activeParty: [Actor("hero"), Actor("a"), Actor("b"), Actor("c")]);

        PartyRosterTransitionResult add = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, Actor("reserve")));

        Assert.True(add.Applied);
        Assert.Equal(4, add.After.ActiveParty.Count);
        Assert.Equal("reserve", Assert.Single(add.After.ReserveMembers).InstanceId.ToString());

        PartyRosterTransitionResult swap = _service.SwapPartyMember(new SwapPartyMemberRequest(add.After, ActiveIndex: 2, ReserveIndex: 0));

        Assert.True(swap.Applied);
        Assert.Equal(["hero", "a", "reserve", "c"], swap.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["b"], swap.After.ReserveMembers.Select(actor => actor.InstanceId.ToString()));
    }

    [Fact]
    public void AddPartyMember_RejectsRuntimeIdsUsedByFormOrStockRoles()
    {
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyRosterSnapshot[] snapshots =
        [
            Snapshot(activeHostedEntity: collision),
            Snapshot(hostedEntityRoster: [collision]),
            Snapshot(companionRoster: [collision])
        ];

        foreach (RuntimePartyRosterSnapshot snapshot in snapshots)
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
        RuntimePartyRosterSnapshot snapshot = new(owner, ownerLevel: 40);

        PartyRosterTransitionResult result = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, owner));

        Assert.True(result.Applied);
        Assert.Equal(owner, Assert.Single(result.After.ActiveParty));
        Assert.Empty(result.After.ReserveMembers);
    }

    [Fact]
    public void AddPartyMember_RejectsOwnedDemonIdWhileSummonPreservesIntentionalOverlap()
    {
        RuntimeActorReferenceSnapshot companion = Actor("owned_demon");
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: [companion]);

        PartyRosterTransitionResult add = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, companion));
        PartyRosterTransitionResult summon = _service.DeployCompanion(new DeployCompanionRequest(snapshot, companion.InstanceId));

        AssertIdentityCollision(snapshot, add, companion.InstanceId);
        Assert.True(summon.Applied);
        Assert.Contains(summon.After.ActiveParty, actor => actor.InstanceId == companion.InstanceId);
        Assert.Contains(summon.After.CompanionRoster, actor => actor.InstanceId == companion.InstanceId);
    }

    [Fact]
    public void DemonCommands_PreserveUnifiedActiveAndOwnedStock()
    {
        RuntimeActorReferenceSnapshot pixie = Actor("pixie");
        RuntimeActorReferenceSnapshot jack = Actor("jack");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero")],
            companionRoster: [pixie, jack]);

        PartyRosterTransitionResult summon = _service.DeployCompanion(new DeployCompanionRequest(snapshot, pixie.InstanceId));

        Assert.True(summon.Applied);
        Assert.Equal(["hero", "pixie"], summon.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie", "jack"], summon.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult swap = _service.SwapDeployedCompanion(new SwapDeployedCompanionRequest(summon.After, pixie.InstanceId, jack.InstanceId));

        Assert.True(swap.Applied);
        Assert.Equal(["hero", "jack"], swap.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie", "jack"], swap.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult returned = _service.RecallCompanion(new RecallCompanionRequest(swap.After, jack.InstanceId));

        Assert.True(returned.Applied);
        Assert.Equal(["hero"], returned.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie", "jack"], returned.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult dismissed = _service.DismissCompanion(new DismissCompanionRequest(returned.After, jack.InstanceId));

        Assert.True(dismissed.Applied);
        Assert.Equal(["pixie"], dismissed.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("ally")]
    public void DemonCommands_RejectActiveActorsWithoutCompanionRosterOwnership(string subjectId)
    {
        RuntimeActorReferenceSnapshot owner = Actor("owner");
        RuntimeActorReferenceSnapshot ally = Actor("ally");
        RuntimeActorReferenceSnapshot standby = Actor("owned_demon");
        RuntimeActorReferenceSnapshot subject = subjectId == "owner" ? owner : ally;
        var snapshot = new RuntimePartyRosterSnapshot(
            owner,
            ownerLevel: 40,
            activeParty: [owner, ally],
            companionRoster: [standby]);

        PartyRosterTransitionResult[] results =
        [
            _service.SwapDeployedCompanion(new SwapDeployedCompanionRequest(
                snapshot,
                subject.InstanceId,
                standby.InstanceId)),
            _service.RecallCompanion(new RecallCompanionRequest(snapshot, subject.InstanceId)),
            _service.ReplaceCompanion(new ReplaceCompanionRequest(
                snapshot,
                subject.InstanceId,
                Actor($"replacement_{subjectId}"))),
            _service.ConsumeCompanion(new ConsumeCompanionRequest(snapshot, subject.InstanceId))
        ];

        foreach (PartyRosterTransitionResult result in results)
        {
            AssertRoleRejection(snapshot, result, subject.InstanceId, PartyRosterTransitionCode.NotOwned);
        }
    }

    [Fact]
    public void DemonDeploymentCommands_RequireBothOwnershipAndActiveMembership()
    {
        RuntimeActorReferenceSnapshot standby = Actor("standby");
        RuntimeActorReferenceSnapshot replacement = Actor("replacement");
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: [standby, replacement]);

        PartyRosterTransitionResult swap = _service.SwapDeployedCompanion(new SwapDeployedCompanionRequest(
            snapshot,
            standby.InstanceId,
            replacement.InstanceId));
        PartyRosterTransitionResult returned = _service.RecallCompanion(new RecallCompanionRequest(
            snapshot,
            standby.InstanceId));

        AssertRoleRejection(snapshot, swap, standby.InstanceId, PartyRosterTransitionCode.NotActive);
        AssertRoleRejection(snapshot, returned, standby.InstanceId, PartyRosterTransitionCode.NotActive);
    }

    [Fact]
    public void CompanionRosterReplacementAndConsumption_DoNotRequireActiveDeployment()
    {
        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimeActorReferenceSnapshot consumedDemon = Actor("consumed_demon");
        RuntimeActorReferenceSnapshot newCompanion = Actor("new_demon");
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: [oldDemon, consumedDemon]);

        PartyRosterTransitionResult replaced = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            snapshot,
            oldDemon.InstanceId,
            newCompanion));
        PartyRosterTransitionResult consumed = _service.ConsumeCompanion(new ConsumeCompanionRequest(
            replaced.After,
            consumedDemon.InstanceId));

        Assert.True(replaced.Applied);
        Assert.Equal(["new_demon", "consumed_demon"], replaced.After.CompanionRoster.Select(DemonId));
        Assert.Equal(["hero"], replaced.After.ActiveParty.Select(DemonId));
        Assert.True(consumed.Applied);
        Assert.Equal(["new_demon"], consumed.After.CompanionRoster.Select(DemonId));
        Assert.Equal(["hero"], consumed.After.ActiveParty.Select(DemonId));
    }

    [Fact]
    public void AddCompanionToRoster_AppendsOwnedDemonAndRejectsDuplicateOrFullStock()
    {
        RuntimeActorReferenceSnapshot pixie = Actor("pixie");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero")],
            companionRoster: [Actor("jack"), Actor("angel")]);

        PartyRosterTransitionResult added = _service.AddCompanionToRoster(new AddCompanionToRosterRequest(snapshot, pixie));

        Assert.True(added.Applied);
        Assert.Equal(["jack", "angel", "pixie"], added.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["pixie"], added.AffectedInstanceIds.Select(id => id.ToString()));

        PartyRosterTransitionResult duplicate = _service.AddCompanionToRoster(new AddCompanionToRosterRequest(added.After, pixie));

        Assert.False(duplicate.Applied);
        Assert.Equal(PartyRosterTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Same(added.After, duplicate.After);

        RuntimePartyRosterSnapshot fullSnapshot = Snapshot(
            ownerLevel: 1,
            activeParty: [Actor("hero")],
            companionRoster: [Actor("a"), Actor("b"), Actor("c")]);

        PartyRosterTransitionResult full = _service.AddCompanionToRoster(new AddCompanionToRosterRequest(
            fullSnapshot,
            Actor("full_candidate")));

        Assert.False(full.Applied);
        Assert.Equal(PartyRosterTransitionCode.RosterFull, full.Code);
        Assert.Same(fullSnapshot, full.After);
    }

    [Fact]
    public void DemonReplacementAndConsumption_UpdateActiveAndStockReferencesAtomically()
    {
        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimeActorReferenceSnapshot newCompanion = Actor("new_demon");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero"), oldDemon],
            companionRoster: [oldDemon]);

        PartyRosterTransitionResult replaced = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            snapshot,
            oldDemon.InstanceId,
            newCompanion));

        Assert.True(replaced.Applied);
        Assert.Equal(["hero", "new_demon"], replaced.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["new_demon"], replaced.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult consumed = _service.ConsumeCompanion(new ConsumeCompanionRequest(replaced.After, newCompanion.InstanceId));

        Assert.True(consumed.Applied);
        Assert.Equal(["hero"], consumed.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Empty(consumed.After.CompanionRoster);
    }

    [Fact]
    public void PersonaCommands_ExchangeConsumeAndReplaceActiveHostedEntityAndStock()
    {
        RuntimeActorReferenceSnapshot active = Actor("orpheus");
        RuntimeActorReferenceSnapshot stock = Actor("pixie");
        RuntimeActorReferenceSnapshot replacement = Actor("jack_frost");
        RuntimePartyRosterSnapshot snapshot = Snapshot(activeHostedEntity: active, hostedEntityRoster: [stock]);

        PartyRosterTransitionResult swapped = _service.SwapActiveHostedEntity(new SwapActiveHostedEntityRequest(snapshot, stock.InstanceId));

        Assert.True(swapped.Applied);
        Assert.Equal("pixie", swapped.After.ActiveHostedEntity?.InstanceId.ToString());
        Assert.Equal(["orpheus"], swapped.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));

        PartyRosterTransitionResult replaced = _service.ReplaceHostedEntity(new ReplaceHostedEntityRequest(
            swapped.After,
            active.InstanceId,
            replacement));

        Assert.True(replaced.Applied);
        Assert.Equal(["jack_frost"], replaced.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));

        PartyRosterTransitionResult consumed = _service.ConsumeHostedEntity(new ConsumeHostedEntityRequest(replaced.After, stock.InstanceId));

        Assert.True(consumed.Applied);
        Assert.Null(consumed.After.ActiveHostedEntity);
        Assert.Equal(["jack_frost"], consumed.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));
    }

    [Fact]
    public void AddHostedEntityToRoster_AppendsAndRejectsDuplicateOrFullStockWithoutMutation()
    {
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            ownerLevel: 1,
            activeHostedEntity: Actor("orpheus"),
            hostedEntityRoster: [Actor("pixie"), Actor("angel")]);
        RuntimeActorReferenceSnapshot candidate = Actor("jack_frost");

        PartyRosterTransitionResult added = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(snapshot, candidate));

        Assert.True(added.Applied);
        Assert.Equal(
            ["pixie", "angel", "jack_frost"],
            added.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));

        PartyRosterTransitionResult duplicate = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(added.After, candidate));
        Assert.Equal(PartyRosterTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Same(added.After, duplicate.After);

        PartyRosterTransitionResult full = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(added.After, Actor("overflow")));
        Assert.Equal(PartyRosterTransitionCode.RosterFull, full.Code);
        Assert.Same(added.After, full.After);
    }

    [Fact]
    public void StockAdditions_RejectRuntimeIdsUsedByAnyOtherOwnershipRole()
    {
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyRosterSnapshot[] demonCollisions =
        [
            new RuntimePartyRosterSnapshot(collision, 40),
            Snapshot(reserveMembers: [collision]),
            Snapshot(activeHostedEntity: collision),
            Snapshot(hostedEntityRoster: [collision])
        ];
        RuntimePartyRosterSnapshot[] personaCollisions =
        [
            new RuntimePartyRosterSnapshot(collision, 40),
            Snapshot(activeParty: [Actor("hero"), collision]),
            Snapshot(reserveMembers: [collision]),
            Snapshot(companionRoster: [collision])
        ];

        foreach (RuntimePartyRosterSnapshot snapshot in demonCollisions)
        {
            AssertIdentityCollision(
                snapshot,
                _service.AddCompanionToRoster(new AddCompanionToRosterRequest(snapshot, collision)),
                collision.InstanceId);
        }

        foreach (RuntimePartyRosterSnapshot snapshot in personaCollisions)
        {
            AssertIdentityCollision(
                snapshot,
                _service.AddHostedEntityToRoster(new AddHostedEntityToRosterRequest(snapshot, collision)),
                collision.InstanceId);
        }
    }

    [Fact]
    public void StockReplacements_RejectRuntimeIdsUsedByTheOppositeStockFamily()
    {
        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimeActorReferenceSnapshot oldPersona = Actor("old_persona");
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyRosterSnapshot demonSnapshot = Snapshot(
            hostedEntityRoster: [collision],
            companionRoster: [oldDemon]);
        RuntimePartyRosterSnapshot personaSnapshot = Snapshot(
            hostedEntityRoster: [oldPersona],
            companionRoster: [collision]);

        PartyRosterTransitionResult companion = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            demonSnapshot,
            oldDemon.InstanceId,
            collision));
        PartyRosterTransitionResult hostedEntity = _service.ReplaceHostedEntity(new ReplaceHostedEntityRequest(
            personaSnapshot,
            oldPersona.InstanceId,
            collision));

        AssertIdentityCollision(demonSnapshot, companion, collision.InstanceId);
        AssertIdentityCollision(personaSnapshot, hostedEntity, collision.InstanceId);
    }

    [Fact]
    public void RejectedCommands_ReturnStableCodesAndUnchangedSnapshots()
    {
        RuntimePartyRosterSnapshot snapshot = Snapshot(activeParty: [Actor("hero")]);
        RuntimeInstanceId missing = RuntimeInstanceId.Parse("missing");

        PartyRosterTransitionResult result = _service.DeployCompanion(new DeployCompanionRequest(snapshot, missing));

        Assert.False(result.Applied);
        Assert.Equal(PartyRosterTransitionCode.NotOwned, result.Code);
        Assert.Same(result.Before, result.After);
        PartyRosterTransitionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PartyRosterTransitionCode.NotOwned, diagnostic.Code);
        Assert.Equal(missing, diagnostic.SubjectInstanceId);
    }

    [Fact]
    public void DuplicateOwnershipAndMalformedOverCapacityFailures_DoNotMutate()
    {
        RuntimeActorReferenceSnapshot companion = Actor("pixie");
        RuntimePartyRosterSnapshot duplicateSnapshot = Snapshot(activeParty: [Actor("hero")], companionRoster: [companion]);

        PartyRosterTransitionResult duplicate = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            duplicateSnapshot,
            companion.InstanceId,
            companion));

        Assert.Equal(PartyRosterTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Equal(duplicateSnapshot, duplicate.After);

        RuntimeActorReferenceSnapshot oldDemon = Actor("old_demon");
        RuntimePartyRosterSnapshot fullSnapshot = Snapshot(
            ownerLevel: 1,
            activeParty: [Actor("hero")],
            companionRoster: [oldDemon, Actor("a"), Actor("b"), Actor("c")]);

        PartyRosterTransitionResult full = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            fullSnapshot,
            oldDemon.InstanceId,
            Actor("overflow")));

        Assert.Equal(PartyRosterTransitionCode.RosterFull, full.Code);
        Assert.Equal(fullSnapshot, full.After);
    }

    [Fact]
    public void SnapshotsDefensivelyCopyInputCollections()
    {
        var active = new List<RuntimeActorReferenceSnapshot> { Actor("hero") };
        RuntimePartyRosterSnapshot snapshot = Snapshot(activeParty: active);

        active.Add(Actor("late"));

        Assert.Equal(["hero"], snapshot.ActiveParty.Select(actor => actor.InstanceId.ToString()));
    }

    private static RuntimePartyRosterSnapshot Snapshot(
        int ownerLevel = 40,
        IEnumerable<RuntimeActorReferenceSnapshot>? activeParty = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? reserveMembers = null,
        RuntimeActorReferenceSnapshot? activeHostedEntity = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? hostedEntityRoster = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? companionRoster = null) =>
        new(
            Actor("hero"),
            ownerLevel,
            activeParty ?? [Actor("hero")],
            reserveMembers,
            activeHostedEntity,
            hostedEntityRoster,
            companionRoster);

    private static RuntimeActorReferenceSnapshot Actor(string id) =>
        new(RuntimeInstanceId.Parse(id), ContentId.Parse(id), id);

    private static void AssertIdentityCollision(
        RuntimePartyRosterSnapshot expectedSnapshot,
        PartyRosterTransitionResult result,
        RuntimeInstanceId instanceId)
    {
        Assert.False(result.Applied);
        Assert.Equal(PartyRosterTransitionCode.RuntimeInstanceIdInUse, result.Code);
        Assert.Same(expectedSnapshot, result.Before);
        Assert.Same(expectedSnapshot, result.After);
        PartyRosterTransitionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PartyRosterTransitionCode.RuntimeInstanceIdInUse, diagnostic.Code);
        Assert.Equal(instanceId, diagnostic.SubjectInstanceId);
    }

    private static void AssertRoleRejection(
        RuntimePartyRosterSnapshot expectedSnapshot,
        PartyRosterTransitionResult result,
        RuntimeInstanceId instanceId,
        PartyRosterTransitionCode expectedCode)
    {
        Assert.False(result.Applied);
        Assert.Equal(expectedCode, result.Code);
        Assert.Same(expectedSnapshot, result.Before);
        Assert.Same(expectedSnapshot, result.After);
        PartyRosterTransitionDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(instanceId, diagnostic.SubjectInstanceId);
    }

    private static string DemonId(RuntimeActorReferenceSnapshot actor) => actor.InstanceId.ToString();
}
