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
        RuntimeActorReferenceSnapshot[] roster = Enumerable.Range(0, 20)
            .Select(index => Actor($"companion_{index}"))
            .ToArray();
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: roster);

        PartyRosterTransitionResult result = service.AddCompanionToRoster(
            new AddCompanionToRosterRequest(snapshot, OwnerActor(snapshot), Actor("companion_20")));

        Assert.True(result.Applied);
        Assert.Equal(21, result.After.CompanionRoster.Count);
    }

    [Fact]
    public void RosterCapacity_DerivesTheCurrentOwnerActorLevelWithoutRosterReconstruction()
    {
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            companionRoster: [Actor("a"), Actor("b"), Actor("c")]);
        RuntimeActorReferenceSnapshot candidate = Actor("new_companion");

        PartyRosterTransitionResult beforeThreshold = _service.AddCompanionToRoster(
            new AddCompanionToRosterRequest(
                snapshot,
                OwnerActor(snapshot, level: 9),
                candidate));
        PartyRosterTransitionResult afterThreshold = _service.AddCompanionToRoster(
            new AddCompanionToRosterRequest(
                snapshot,
                OwnerActor(snapshot, level: 10),
                candidate));

        Assert.Equal(PartyRosterTransitionCode.RosterFull, beforeThreshold.Code);
        Assert.Same(snapshot, beforeThreshold.After);
        Assert.True(afterThreshold.Applied);
        Assert.Equal(4, afterThreshold.After.CompanionRoster.Count);
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

        PartyRosterTransitionResult add = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, OwnerActor(snapshot), Actor("reserve")));

        Assert.True(add.Applied);
        Assert.Equal(4, add.After.ActiveParty.Count);
        Assert.Equal("reserve", Assert.Single(add.After.ReserveMembers).InstanceId.ToString());

        PartyRosterTransitionResult swap = _service.SwapPartyMember(new SwapPartyMemberRequest(add.After, OwnerActor(add.After), ActiveIndex: 2, ReserveIndex: 0));

        Assert.True(swap.Applied);
        Assert.Equal(["hero", "a", "reserve", "c"], swap.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["b"], swap.After.ReserveMembers.Select(actor => actor.InstanceId.ToString()));
    }

    [Fact]
    public void AddPartyMember_RejectsRuntimeIdsUsedByHostedEntityOrRosterRoles()
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
                _service.AddPartyMember(new AddPartyMemberRequest(snapshot, OwnerActor(snapshot), collision)),
                collision.InstanceId);
        }
    }

    [Fact]
    public void AddPartyMember_AllowsExactOwnerReferenceToEnterAnOpenActiveSlot()
    {
        RuntimeActorReferenceSnapshot owner = Actor("owner");
        RuntimePartyRosterSnapshot snapshot = new(owner);

        PartyRosterTransitionResult result = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, OwnerActor(snapshot), owner));

        Assert.True(result.Applied);
        Assert.Equal(owner, Assert.Single(result.After.ActiveParty));
        Assert.Empty(result.After.ReserveMembers);
    }

    [Fact]
    public void AddPartyMember_RejectsOwnedCompanionIdWhileDeploymentPreservesIntentionalOverlap()
    {
        RuntimeActorReferenceSnapshot companion = Actor("owned_companion");
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: [companion]);

        PartyRosterTransitionResult add = _service.AddPartyMember(new AddPartyMemberRequest(snapshot, OwnerActor(snapshot), companion));
        PartyRosterTransitionResult deploy = _service.DeployCompanion(new DeployCompanionRequest(snapshot, OwnerActor(snapshot), companion.InstanceId));

        AssertIdentityCollision(snapshot, add, companion.InstanceId);
        Assert.True(deploy.Applied);
        Assert.Contains(deploy.After.ActiveParty, actor => actor.InstanceId == companion.InstanceId);
        Assert.Contains(deploy.After.CompanionRoster, actor => actor.InstanceId == companion.InstanceId);
    }

    [Fact]
    public void CompanionCommands_PreserveUnifiedActiveAndOwnedRoster()
    {
        RuntimeActorReferenceSnapshot glowWisp = Actor("glow_wisp");
        RuntimeActorReferenceSnapshot sparkShell = Actor("spark_shell");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero")],
            companionRoster: [glowWisp, sparkShell]);

        PartyRosterTransitionResult deploy = _service.DeployCompanion(new DeployCompanionRequest(snapshot, OwnerActor(snapshot), glowWisp.InstanceId));

        Assert.True(deploy.Applied);
        Assert.Equal(["hero", "glow_wisp"], deploy.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["glow_wisp", "spark_shell"], deploy.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult swap = _service.SwapDeployedCompanion(
            new SwapDeployedCompanionRequest(deploy.After, OwnerActor(deploy.After), glowWisp.InstanceId, sparkShell.InstanceId));

        Assert.True(swap.Applied);
        Assert.Equal(["hero", "spark_shell"], swap.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["glow_wisp", "spark_shell"], swap.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult returned = _service.RecallCompanion(new RecallCompanionRequest(swap.After, OwnerActor(swap.After), sparkShell.InstanceId));

        Assert.True(returned.Applied);
        Assert.Equal(["hero"], returned.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["glow_wisp", "spark_shell"], returned.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult dismissed = _service.DismissCompanion(new DismissCompanionRequest(returned.After, OwnerActor(returned.After), sparkShell.InstanceId));

        Assert.True(dismissed.Applied);
        Assert.Equal(["glow_wisp"], dismissed.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("ally")]
    public void CompanionCommands_RejectActiveActorsWithoutCompanionRosterOwnership(string subjectId)
    {
        RuntimeActorReferenceSnapshot owner = Actor("owner");
        RuntimeActorReferenceSnapshot ally = Actor("ally");
        RuntimeActorReferenceSnapshot standby = Actor("owned_companion");
        RuntimeActorReferenceSnapshot subject = subjectId == "owner" ? owner : ally;
        var snapshot = new RuntimePartyRosterSnapshot(
            owner,
            activeParty: [owner, ally],
            companionRoster: [standby]);

        PartyRosterTransitionResult[] results =
        [
            _service.SwapDeployedCompanion(new SwapDeployedCompanionRequest(
                snapshot,
                OwnerActor(snapshot),
                subject.InstanceId,
                standby.InstanceId)),
            _service.RecallCompanion(new RecallCompanionRequest(snapshot, OwnerActor(snapshot), subject.InstanceId)),
            _service.ReplaceCompanion(new ReplaceCompanionRequest(
                snapshot,
                OwnerActor(snapshot),
                subject.InstanceId,
                Actor($"replacement_{subjectId}"))),
            _service.ConsumeCompanion(new ConsumeCompanionRequest(snapshot, OwnerActor(snapshot), subject.InstanceId))
        ];

        foreach (PartyRosterTransitionResult result in results)
        {
            AssertRoleRejection(snapshot, result, subject.InstanceId, PartyRosterTransitionCode.NotOwned);
        }
    }

    [Fact]
    public void CompanionDeploymentCommands_RequireBothOwnershipAndActiveMembership()
    {
        RuntimeActorReferenceSnapshot standby = Actor("standby");
        RuntimeActorReferenceSnapshot replacement = Actor("replacement");
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: [standby, replacement]);

        PartyRosterTransitionResult swap = _service.SwapDeployedCompanion(new SwapDeployedCompanionRequest(
            snapshot,
            OwnerActor(snapshot),
            standby.InstanceId,
            replacement.InstanceId));
        PartyRosterTransitionResult returned = _service.RecallCompanion(new RecallCompanionRequest(
            snapshot,
            OwnerActor(snapshot),
            standby.InstanceId));

        AssertRoleRejection(snapshot, swap, standby.InstanceId, PartyRosterTransitionCode.NotActive);
        AssertRoleRejection(snapshot, returned, standby.InstanceId, PartyRosterTransitionCode.NotActive);
    }

    [Fact]
    public void CompanionRosterReplacementAndConsumption_DoNotRequireActiveDeployment()
    {
        RuntimeActorReferenceSnapshot oldCompanion = Actor("old_companion");
        RuntimeActorReferenceSnapshot consumedCompanion = Actor("consumed_companion");
        RuntimeActorReferenceSnapshot newCompanion = Actor("new_companion");
        RuntimePartyRosterSnapshot snapshot = Snapshot(companionRoster: [oldCompanion, consumedCompanion]);

        PartyRosterTransitionResult replaced = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            snapshot,
            OwnerActor(snapshot),
            oldCompanion.InstanceId,
            newCompanion));
        PartyRosterTransitionResult consumed = _service.ConsumeCompanion(new ConsumeCompanionRequest(
            replaced.After,
            OwnerActor(replaced.After),
            consumedCompanion.InstanceId));

        Assert.True(replaced.Applied);
        Assert.Equal(["new_companion", "consumed_companion"], replaced.After.CompanionRoster.Select(ActorId));
        Assert.Equal(["hero"], replaced.After.ActiveParty.Select(ActorId));
        Assert.True(consumed.Applied);
        Assert.Equal(["new_companion"], consumed.After.CompanionRoster.Select(ActorId));
        Assert.Equal(["hero"], consumed.After.ActiveParty.Select(ActorId));
    }

    [Fact]
    public void AddCompanionToRoster_AppendsOwnedCompanionAndRejectsDuplicateOrFullRoster()
    {
        RuntimeActorReferenceSnapshot glowWisp = Actor("glow_wisp");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero")],
            companionRoster: [Actor("spark_shell"), Actor("winged_sentinel")]);

        PartyRosterTransitionResult added = _service.AddCompanionToRoster(
            new AddCompanionToRosterRequest(snapshot, OwnerActor(snapshot), glowWisp));

        Assert.True(added.Applied);
        Assert.Equal(["spark_shell", "winged_sentinel", "glow_wisp"], added.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["glow_wisp"], added.AffectedInstanceIds.Select(id => id.ToString()));

        PartyRosterTransitionResult duplicate = _service.AddCompanionToRoster(
            new AddCompanionToRosterRequest(added.After, OwnerActor(added.After), glowWisp));

        Assert.False(duplicate.Applied);
        Assert.Equal(PartyRosterTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Same(added.After, duplicate.After);

        RuntimePartyRosterSnapshot fullSnapshot = Snapshot(
            activeParty: [Actor("hero")],
            companionRoster: [Actor("a"), Actor("b"), Actor("c")]);

        PartyRosterTransitionResult full = _service.AddCompanionToRoster(new AddCompanionToRosterRequest(
            fullSnapshot,
            OwnerActor(fullSnapshot, level: 1),
            Actor("full_candidate")));

        Assert.False(full.Applied);
        Assert.Equal(PartyRosterTransitionCode.RosterFull, full.Code);
        Assert.Same(fullSnapshot, full.After);
    }

    [Fact]
    public void CompanionReplacementAndConsumption_UpdateActiveAndRosterReferencesAtomically()
    {
        RuntimeActorReferenceSnapshot oldCompanion = Actor("old_companion");
        RuntimeActorReferenceSnapshot newCompanion = Actor("new_companion");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeParty: [Actor("hero"), oldCompanion],
            companionRoster: [oldCompanion]);

        PartyRosterTransitionResult replaced = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            snapshot,
            OwnerActor(snapshot),
            oldCompanion.InstanceId,
            newCompanion));

        Assert.True(replaced.Applied);
        Assert.Equal(["hero", "new_companion"], replaced.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Equal(["new_companion"], replaced.After.CompanionRoster.Select(actor => actor.InstanceId.ToString()));

        PartyRosterTransitionResult consumed = _service.ConsumeCompanion(new ConsumeCompanionRequest(replaced.After, OwnerActor(replaced.After), newCompanion.InstanceId));

        Assert.True(consumed.Applied);
        Assert.Equal(["hero"], consumed.After.ActiveParty.Select(actor => actor.InstanceId.ToString()));
        Assert.Empty(consumed.After.CompanionRoster);
    }

    [Fact]
    public void HostedEntityCommands_SelectConsumeAndReplaceOwnedHostedEntitiesAtomically()
    {
        RuntimeActorReferenceSnapshot active = Actor("annex_mentor");
        RuntimeActorReferenceSnapshot rosterEntry = Actor("glow_wisp");
        RuntimeActorReferenceSnapshot replacement = Actor("frostling");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeHostedEntity: active,
            hostedEntityRoster: [active, rosterEntry]);

        PartyRosterTransitionResult selected = _service.SelectActiveHostedEntity(
            new SelectActiveHostedEntityRequest(snapshot, OwnerActor(snapshot), rosterEntry.InstanceId));

        Assert.True(selected.Applied);
        Assert.Equal("glow_wisp", selected.After.ActiveHostedEntity?.InstanceId.ToString());
        Assert.Equal(
            ["annex_mentor", "glow_wisp"],
            selected.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));

        PartyRosterTransitionResult replaced = _service.ReplaceHostedEntity(new ReplaceHostedEntityRequest(
            selected.After,
            OwnerActor(selected.After),
            active.InstanceId,
            replacement));

        Assert.True(replaced.Applied);
        Assert.Equal(
            ["frostling", "glow_wisp"],
            replaced.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));

        PartyRosterTransitionResult consumed = _service.ConsumeHostedEntity(new ConsumeHostedEntityRequest(replaced.After, OwnerActor(replaced.After), rosterEntry.InstanceId));

        Assert.True(consumed.Applied);
        Assert.Null(consumed.After.ActiveHostedEntity);
        Assert.Equal(["frostling"], consumed.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));
    }

    [Fact]
    public void HostedEntitySelectionAndClear_ChangeOnlyTheActiveReference()
    {
        RuntimeActorReferenceSnapshot first = Actor("annex_mentor");
        RuntimeActorReferenceSnapshot second = Actor("glow_wisp");
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeHostedEntity: first,
            hostedEntityRoster: [first, second]);

        PartyRosterTransitionResult selected = _service.SelectActiveHostedEntity(
            new SelectActiveHostedEntityRequest(snapshot, OwnerActor(snapshot), second.InstanceId));
        PartyRosterTransitionResult cleared = _service.ClearActiveHostedEntity(
            new ClearActiveHostedEntityRequest(
                selected.After,
                OwnerActor(selected.After)));

        Assert.True(selected.Applied);
        Assert.Equal(second, selected.After.ActiveHostedEntity);
        Assert.Equal(snapshot.HostedEntityRoster, selected.After.HostedEntityRoster);
        Assert.True(cleared.Applied);
        Assert.Null(cleared.After.ActiveHostedEntity);
        Assert.Equal(snapshot.HostedEntityRoster, cleared.After.HostedEntityRoster);
    }

    [Fact]
    public void InvalidCanonicalRoster_IsRejectedWithoutMutation()
    {
        RuntimeActorReferenceSnapshot owner = Actor("hero");
        RuntimeActorReferenceSnapshot unownedActive = Actor("unowned");
        var invalid = new RuntimePartyRosterSnapshot(
            owner,
            activeParty: [owner],
            activeHostedEntity: unownedActive);

        PartyRosterTransitionResult result = _service.ClearActiveHostedEntity(
            new ClearActiveHostedEntityRequest(invalid, OwnerActor(invalid)));

        Assert.False(result.Applied);
        Assert.Equal(PartyRosterTransitionCode.InvalidSnapshot, result.Code);
        Assert.Same(invalid, result.Before);
        Assert.Same(invalid, result.After);
    }

    [Fact]
    public void AddHostedEntityToRoster_AppendsAndRejectsDuplicateOrFullRosterWithoutMutation()
    {
        RuntimePartyRosterSnapshot snapshot = Snapshot(
            activeHostedEntity: Actor("annex_mentor"),
            hostedEntityRoster: [Actor("glow_wisp"), Actor("winged_sentinel")]);
        RuntimeActorReferenceSnapshot candidate = Actor("frostling");

        PartyRosterTransitionResult added = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(
                snapshot,
                OwnerActor(snapshot, level: 10),
                candidate));

        Assert.True(added.Applied);
        Assert.Equal(
            ["annex_mentor", "glow_wisp", "winged_sentinel", "frostling"],
            added.After.HostedEntityRoster.Select(hostedEntity => hostedEntity.InstanceId.ToString()));

        PartyRosterTransitionResult duplicate = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(
                added.After,
                OwnerActor(added.After, level: 10),
                candidate));
        Assert.Equal(PartyRosterTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Same(added.After, duplicate.After);

        PartyRosterTransitionResult filled = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(
                added.After,
                OwnerActor(added.After, level: 10),
                Actor("final_slot")));
        Assert.True(filled.Applied);
        PartyRosterTransitionResult full = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(
                filled.After,
                OwnerActor(filled.After, level: 10),
                Actor("overflow")));
        Assert.Equal(PartyRosterTransitionCode.RosterFull, full.Code);
        Assert.Same(filled.After, full.After);
    }

    [Fact]
    public void RosterAdditions_RejectRuntimeIdsUsedByAnyOtherOwnershipRole()
    {
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyRosterSnapshot[] companionCollisions =
        [
            new RuntimePartyRosterSnapshot(collision),
            Snapshot(reserveMembers: [collision]),
            Snapshot(activeHostedEntity: collision),
            Snapshot(hostedEntityRoster: [collision])
        ];
        RuntimePartyRosterSnapshot[] hostedEntityCollisions =
        [
            new RuntimePartyRosterSnapshot(collision),
            Snapshot(activeParty: [Actor("hero"), collision]),
            Snapshot(reserveMembers: [collision]),
            Snapshot(companionRoster: [collision])
        ];

        foreach (RuntimePartyRosterSnapshot snapshot in companionCollisions)
        {
            AssertIdentityCollision(
                snapshot,
                _service.AddCompanionToRoster(new AddCompanionToRosterRequest(snapshot, OwnerActor(snapshot), collision)),
                collision.InstanceId);
        }

        foreach (RuntimePartyRosterSnapshot snapshot in hostedEntityCollisions)
        {
            AssertIdentityCollision(
                snapshot,
                _service.AddHostedEntityToRoster(new AddHostedEntityToRosterRequest(snapshot, OwnerActor(snapshot), collision)),
                collision.InstanceId);
        }
    }

    [Fact]
    public void RosterReplacements_RejectRuntimeIdsUsedByTheOppositeRosterFamily()
    {
        RuntimeActorReferenceSnapshot oldCompanion = Actor("old_companion");
        RuntimeActorReferenceSnapshot oldHostedEntity = Actor("old_hosted_entity");
        RuntimeActorReferenceSnapshot collision = Actor("collision");
        RuntimePartyRosterSnapshot companionSnapshot = Snapshot(
            hostedEntityRoster: [collision],
            companionRoster: [oldCompanion]);
        RuntimePartyRosterSnapshot hostedEntitySnapshot = Snapshot(
            hostedEntityRoster: [oldHostedEntity],
            companionRoster: [collision]);

        PartyRosterTransitionResult companion = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            companionSnapshot,
            OwnerActor(companionSnapshot),
            oldCompanion.InstanceId,
            collision));
        PartyRosterTransitionResult hostedEntity = _service.ReplaceHostedEntity(new ReplaceHostedEntityRequest(
            hostedEntitySnapshot,
            OwnerActor(hostedEntitySnapshot),
            oldHostedEntity.InstanceId,
            collision));

        AssertIdentityCollision(companionSnapshot, companion, collision.InstanceId);
        AssertIdentityCollision(hostedEntitySnapshot, hostedEntity, collision.InstanceId);
    }

    [Fact]
    public void RejectedCommands_ReturnStableCodesAndUnchangedSnapshots()
    {
        RuntimePartyRosterSnapshot snapshot = Snapshot(activeParty: [Actor("hero")]);
        RuntimeInstanceId missing = RuntimeInstanceId.Parse("missing");

        PartyRosterTransitionResult result = _service.DeployCompanion(new DeployCompanionRequest(snapshot, OwnerActor(snapshot), missing));

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
        RuntimeActorReferenceSnapshot companion = Actor("glow_wisp");
        RuntimePartyRosterSnapshot duplicateSnapshot = Snapshot(activeParty: [Actor("hero")], companionRoster: [companion]);

        PartyRosterTransitionResult duplicate = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            duplicateSnapshot,
            OwnerActor(duplicateSnapshot),
            companion.InstanceId,
            companion));

        Assert.Equal(PartyRosterTransitionCode.DuplicateOwned, duplicate.Code);
        Assert.Equal(duplicateSnapshot, duplicate.After);

        RuntimeActorReferenceSnapshot oldCompanion = Actor("old_companion");
        RuntimePartyRosterSnapshot fullSnapshot = Snapshot(
            activeParty: [Actor("hero")],
            companionRoster: [oldCompanion, Actor("a"), Actor("b"), Actor("c")]);

        PartyRosterTransitionResult full = _service.ReplaceCompanion(new ReplaceCompanionRequest(
            fullSnapshot,
            OwnerActor(fullSnapshot, level: 1),
            oldCompanion.InstanceId,
            Actor("overflow")));

        Assert.Equal(PartyRosterTransitionCode.InvalidSnapshot, full.Code);
        Assert.Equal(fullSnapshot, full.After);
    }

    [Fact]
    public void Transitions_RejectMalformedInputAndInvalidProposedStateAtomically()
    {
        RuntimeActorReferenceSnapshot hero = Actor("hero");
        RuntimePartyRosterSnapshot duplicateActive = Snapshot(
            activeParty: [hero, hero]);

        PartyRosterTransitionResult malformedInput = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(
                duplicateActive,
                OwnerActor(duplicateActive),
                Actor("valid_hosted_entity")));

        Assert.Equal(PartyRosterTransitionCode.InvalidSnapshot, malformedInput.Code);
        Assert.Same(duplicateActive, malformedInput.Before);
        Assert.Same(duplicateActive, malformedInput.After);
        Assert.Contains("$.activeParty[1]", Assert.Single(malformedInput.Diagnostics).Message);

        RuntimePartyRosterSnapshot valid = Snapshot();
        var invalidReference = new RuntimeActorReferenceSnapshot(
            default,
            default,
            "Invalid reference");

        PartyRosterTransitionResult invalidProposedState = _service.AddHostedEntityToRoster(
            new AddHostedEntityToRosterRequest(
                valid,
                OwnerActor(valid),
                invalidReference));

        Assert.Equal(PartyRosterTransitionCode.InvalidSnapshot, invalidProposedState.Code);
        Assert.Same(valid, invalidProposedState.Before);
        Assert.Same(valid, invalidProposedState.After);
        Assert.Contains(
            "Proposed party roster is invalid",
            Assert.Single(invalidProposedState.Diagnostics).Message,
            StringComparison.Ordinal);
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
        IEnumerable<RuntimeActorReferenceSnapshot>? activeParty = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? reserveMembers = null,
        RuntimeActorReferenceSnapshot? activeHostedEntity = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? hostedEntityRoster = null,
        IEnumerable<RuntimeActorReferenceSnapshot>? companionRoster = null)
    {
        RuntimeActorReferenceSnapshot[] hosted = (hostedEntityRoster ?? []).ToArray();
        if (activeHostedEntity is not null &&
            !hosted.Any(actor => actor.InstanceId == activeHostedEntity.InstanceId))
        {
            hosted = [activeHostedEntity, .. hosted];
        }

        return new(
            Actor("hero"),
            activeParty ?? [Actor("hero")],
            reserveMembers,
            activeHostedEntity,
            hosted,
            companionRoster);
    }

    private static RuntimeActorSnapshot OwnerActor(
        RuntimePartyRosterSnapshot snapshot,
        int level = 40) =>
        new(
            new RuntimeActorIdentitySnapshot(
                snapshot.Owner.InstanceId,
                snapshot.Owner.EntityDefinitionId,
                ContentId.Parse("independent_actor"),
                snapshot.Owner.DisplayName),
            new RuntimeActorAffiliationSnapshot(
                ContentId.Parse("test_controller"),
                ContentId.Parse("test_team")),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            new RuntimeProgressionSnapshot(level, 0, 0, 0),
            [new RuntimeResourceSnapshot(ContentId.Parse("hp"), 1, 1)],
            new RuntimeStatBlockSnapshot(),
            new RuntimeSkillStateSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(ContentId.Parse("hp"), 1)],
            ContentId.Parse("hp"));

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

    private static string ActorId(RuntimeActorReferenceSnapshot actor) => actor.InstanceId.ToString();
}
