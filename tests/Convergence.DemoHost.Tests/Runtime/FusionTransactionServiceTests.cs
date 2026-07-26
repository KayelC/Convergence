using Convergence.Content;
using Convergence.Catalog;
using Convergence.Battle;
using Convergence.DemoHost.TrainingAnnex;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Fusion;
using Convergence.Inheritance;
using Convergence.Runtime;
using Xunit;

namespace Convergence.DemoHost.Tests.Runtime;

public sealed class FusionTransactionServiceTests
{
    private const string Pack = "convergence.training_annex_slice";

    [Fact]
    public void Prepare_IsPureAndCommitAppliesOneAtomicCompanionTransaction()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        var actorFactory = new CountingActorFactory(context.ActorFactory);
        var service = new FusionTransactionService(
            actorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));

        FusionTransactionAssessment assessment = service.Prepare(Request(
            context,
            party,
            FusionParticipantRosterKind.Companion,
            "fused_result"));

        Assert.True(assessment.CanCommit);
        Assert.Same(party, assessment.BeforePartyRoster);
        Assert.Equal(2, party.CompanionRoster.Count);
        Assert.Equal(
            [RuntimeInstanceId.Parse("fused_result")],
            assessment.AfterPartyRoster.CompanionRoster.Select(actor => actor.InstanceId));
        Assert.Equal(3, assessment.RosterTransitions.Count);
        Assert.All(assessment.RosterTransitions, transition => Assert.True(transition.Applied));
        Assert.Equal(0, actorFactory.CreateCount);
        Assert.Equal(0, actorFactory.RestoreCount);

        PreparedFusionTransaction prepared = assessment.RequirePreparedTransaction();
        RuntimePartyRosterSnapshot changedParty = party.With(reserveMembers:
        [
            new RuntimeActorReferenceSnapshot(
                RuntimeInstanceId.Parse("late_reserve"),
                Qualified("annex_mentor"),
                "Late Reserve")
        ]);
        FusionTransactionCommitResult stale = service.Commit(new FusionTransactionCommitRequest(
            prepared,
            changedParty));
        Assert.False(stale.Applied);
        Assert.Equal(FusionTransactionCommitCode.PreparationStale, stale.Code);
        Assert.Same(changedParty, stale.BeforePartyRoster);
        Assert.Same(changedParty, stale.AfterPartyRoster);
        Assert.Equal(
            FusionRuntimeDiagnosticCode.TransactionStateChanged,
            Assert.Single(stale.Diagnostics).Code);
        Assert.Empty(stale.ConsumedParticipantIds);
        Assert.Empty(stale.RosterTransitions);
        Assert.Equal(prepared.ConsumedParticipantIds, stale.PlannedConsumedParticipantIds);
        Assert.Equal(prepared.RosterTransitions, stale.PlannedRosterTransitions);
        Assert.Equal(0, actorFactory.CreateCount);

        FusionTransactionCommitResult result = service.Commit(new FusionTransactionCommitRequest(
            prepared,
            party));

        Assert.True(result.Applied);
        Assert.Equal(FusionTransactionCommitCode.Applied, result.Code);
        Assert.Same(party, result.BeforePartyRoster);
        Assert.Same(prepared.AfterPartyRoster, result.AfterPartyRoster);
        Assert.Equal(1, actorFactory.CreateCount);
        Assert.Equal(1, actorFactory.RestoreCount);
        Assert.Equal(Qualified("ward_shell"), result.ResultActor?.Entity.Id);
        Assert.Equal(RuntimeInstanceId.Parse("fused_result"), result.ResultActorSnapshot?.Identity.InstanceId);
        Assert.Equal(prepared.ResultLearnedSkillIds, result.ResultActorSnapshot?.Skills.LearnedSkillIds);
        Assert.Equal(
            [context.FirstParent.InstanceId, context.SecondParent.InstanceId],
            result.ConsumedParticipantIds);
    }

    [Fact]
    public void Prepare_HonorsHostedEntityOwnershipForConsumptionAndPlacement()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.HostedEntity);
        party = party.With(
            activeHostedEntity: party.HostedEntityRoster[0],
            replaceActiveHostedEntity: true);
        var service = new FusionTransactionService(
            context.ActorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));

        FusionTransactionAssessment assessment = service.Prepare(Request(
            context,
            party,
            FusionParticipantRosterKind.HostedEntity,
            "hosted_entity_result"));

        Assert.True(assessment.CanCommit);
        Assert.Null(assessment.AfterPartyRoster.ActiveHostedEntity);
        Assert.Empty(assessment.AfterPartyRoster.CompanionRoster);
        RuntimeActorReferenceSnapshot hostedEntity = Assert.Single(assessment.AfterPartyRoster.HostedEntityRoster);
        Assert.Equal(RuntimeInstanceId.Parse("hosted_entity_result"), hostedEntity.InstanceId);
        Assert.Equal(Qualified("ward_shell"), hostedEntity.EntityDefinitionId);
        Assert.Equal(3, assessment.RosterTransitions.Count);
        Assert.Equal(
            [context.FirstParent.InstanceId, context.SecondParent.InstanceId, RuntimeInstanceId.Parse("hosted_entity_result")],
            assessment.RosterTransitions.SelectMany(transition => transition.AffectedInstanceIds));
    }

    [Fact]
    public void Commit_StructuredRankOperationUsesCatalogStateRegardlessOfParentOrder()
    {
        TransactionContext context = CreateContext();
        FusionParticipantSnapshot target = Participant(
            context.Catalog.GetRequiredEntity(Qualified("ashling")),
            "rank_shift_target");
        FusionParticipantSnapshot catalyst = Participant(
            context.Catalog.GetRequiredEntity(Qualified("prism_catalyst")),
            "rank_shift_catalyst");
        var owner = new RuntimeActorReferenceSnapshot(
            RuntimeInstanceId.Parse("owner"),
            Qualified("echo_adept"),
            "Owner");
        var service = new FusionTransactionService(
            context.ActorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));
        EntityDefinition expectedEntity = context.Catalog.GetRequiredEntity(Qualified("glimmer_guard"));

        (FusionPlanningResult forwardPlan, PreparedFusionTransaction forwardPrepared, FusionTransactionCommitResult forward) =
            Execute(target, catalyst, "rank_result_forward");
        (FusionPlanningResult reversedPlan, PreparedFusionTransaction reversedPrepared, FusionTransactionCommitResult reversed) =
            Execute(catalyst, target, "rank_result_reversed");

        Assert.Equal(FusionRuntimeOperation.RankUpParent, forwardPlan.Result.Operation);
        Assert.Equal(FusionRuntimeOperation.RankUpParent, reversedPlan.Result.Operation);
        Assert.Null(forwardPlan.PreviewBaseline);
        Assert.Null(reversedPlan.PreviewBaseline);
        Assert.Equal(forwardPrepared.Preview.EntityId, reversedPrepared.Preview.EntityId);
        Assert.Equal(forwardPrepared.Preview.Stats, reversedPrepared.Preview.Stats);
        AssertCatalogStats(forwardPrepared.Preview.Stats);
        AssertCatalogStats(reversedPrepared.Preview.Stats);
        Assert.True(forward.Applied);
        Assert.True(reversed.Applied);
        Assert.Equal(expectedEntity.Id, forward.ResultActor?.Entity.Id);
        Assert.Equal(expectedEntity.Id, reversed.ResultActor?.Entity.Id);
        Assert.Equal(
            RuntimeInstanceId.Parse("rank_result_forward"),
            forward.ResultActorSnapshot!.Identity.InstanceId);
        Assert.Equal(
            RuntimeInstanceId.Parse("rank_result_reversed"),
            reversed.ResultActorSnapshot!.Identity.InstanceId);
        Assert.Equal(
            Id("test_controller"),
            forward.ResultActorSnapshot.Affiliation.CommandAuthorityId);
        Assert.Equal(Id("player_team"), forward.ResultActorSnapshot.Affiliation.TeamId);
        AssertCatalogDecimalStats(forward.ResultActorSnapshot!.Stats.BaseStats);
        AssertCatalogDecimalStats(reversed.ResultActorSnapshot!.Stats.BaseStats);
        Assert.Equal([target.InstanceId, catalyst.InstanceId], forward.ConsumedParticipantIds);
        Assert.Equal([catalyst.InstanceId, target.InstanceId], reversed.ConsumedParticipantIds);
        Assert.Equal(Qualified("glimmer_guard"), Assert.Single(forward.AfterPartyRoster.CompanionRoster).EntityDefinitionId);
        Assert.Equal(Qualified("glimmer_guard"), Assert.Single(reversed.AfterPartyRoster.CompanionRoster).EntityDefinitionId);

        (FusionPlanningResult Plan, PreparedFusionTransaction Prepared, FusionTransactionCommitResult Result) Execute(
            FusionParticipantSnapshot first,
            FusionParticipantSnapshot second,
            string resultInstanceId)
        {
            FusionPlanningResult plan = context.Planner.CreatePlan(new FusionPlanningRequest(
                first,
                second,
                Sacrifice: null,
                IsSacrificial: false));
            ValidatedFusionInheritanceSelection selection = Selection(
                context.Planner,
                plan,
                selectedSkillIds: null);
            RuntimePartyRosterSnapshot party = new(
                owner,
                companionRoster: [Reference(first), Reference(second)]);
            FusionTransactionAssessment assessment = service.Prepare(new FusionTransactionPreparationRequest(
                FusionParticipantRosterKind.Companion,
                plan,
                selection,
                party,
                OwnerActor(party),
                RuntimeInstanceId.Parse(resultInstanceId),
                Id("player_team"),
                Id("test_controller")));
            PreparedFusionTransaction prepared = assessment.RequirePreparedTransaction();
            return (plan, prepared, service.Commit(new FusionTransactionCommitRequest(prepared, party)));
        }

        void AssertCatalogStats(IReadOnlyDictionary<ContentId, int> stats)
        {
            Assert.Equal(expectedEntity.Stats.Count, stats.Count);
            Assert.All(expectedEntity.Stats, expected => Assert.Equal(expected.Value, stats[expected.Key]));
        }

        void AssertCatalogDecimalStats(IReadOnlyDictionary<ContentId, decimal> stats)
        {
            Assert.Equal(expectedEntity.Stats.Count, stats.Count);
            Assert.All(expectedEntity.Stats, expected => Assert.Equal((decimal)expected.Value, stats[expected.Key]));
        }
    }

    [Fact]
    public void Prepare_RejectsDuplicateParticipantIdentityBeforeSimulatingStockChanges()
    {
        TransactionContext context = CreateContext();
        FusionParticipantSnapshot duplicateSecondParent = Participant(
            context.Catalog.GetRequiredEntity(Qualified("bramble_runner")),
            context.FirstParent.InstanceId.ToString());
        var malformedPlan = new FusionPlanningResult(
            context.Plan.Result,
            context.Plan.ResultEntity,
            context.Plan.PreviewBaseline,
            context.FirstParent,
            duplicateSecondParent,
            sacrifice: null,
            context.Plan.NaturalSkillIds,
            context.Plan.PickableSkillIds,
            context.Plan.ExclusiveSkillIds,
            context.Plan.DisplaySkills,
            context.Plan.MaximumInheritanceSlots,
            context.Plan.SacrificeDecision,
            context.Plan.PolicyContext);
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        var service = new FusionTransactionService(
            context.ActorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));

        FusionTransactionAssessment assessment = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            malformedPlan,
            context.Selection,
            party,
            OwnerActor(party),
            RuntimeInstanceId.Parse("duplicate_parent_result"),
            Id("player_team"),
            Id("test_controller")));

        Assert.False(assessment.CanCommit);
        FusionRuntimeDiagnostic diagnostic = Assert.Single(assessment.Diagnostics);
        Assert.Equal(FusionRuntimeDiagnosticCode.DuplicateParticipant, diagnostic.Code);
        Assert.Equal(context.FirstParent.InstanceId, diagnostic.InstanceId);
        Assert.Same(party, assessment.BeforePartyRoster);
        Assert.Same(party, assessment.AfterPartyRoster);
        Assert.Empty(assessment.RosterTransitions);
    }

    [Fact]
    public void Prepare_ValidatesEveryIntentionalCompanionOverlapReference()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        RuntimeActorReferenceSnapshot firstReference = Reference(context.FirstParent);
        RuntimePartyRosterSnapshot validOverlap = party.With(activeParty: [firstReference]);
        var service = new FusionTransactionService(
            context.ActorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));

        FusionTransactionAssessment valid = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            context.Plan,
            context.Selection,
            validOverlap,
            OwnerActor(validOverlap),
            RuntimeInstanceId.Parse("overlap_result"),
            Id("player_team"),
            Id("test_controller")));

        Assert.True(valid.CanCommit);
        Assert.DoesNotContain(
            valid.AfterPartyRoster.ActiveParty,
            actor => actor.InstanceId == context.FirstParent.InstanceId);

        RuntimePartyRosterSnapshot mismatchedOverlap = party.With(
            activeParty: [firstReference],
            companionRoster:
            [
                new RuntimeActorReferenceSnapshot(
                    context.FirstParent.InstanceId,
                    Qualified("annex_mentor"),
                    "Mismatched overlap"),
                Reference(context.SecondParent)
            ]);
        FusionTransactionAssessment rejected = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            context.Plan,
            context.Selection,
            mismatchedOverlap,
            OwnerActor(mismatchedOverlap),
            RuntimeInstanceId.Parse("mismatched_overlap_result"),
            Id("player_team"),
            Id("test_controller")));

        Assert.False(rejected.CanCommit);
        FusionRuntimeDiagnostic diagnostic = Assert.Single(rejected.Diagnostics);
        Assert.Equal(FusionRuntimeDiagnosticCode.RosterTransitionRejected, diagnostic.Code);
        Assert.Equal(context.FirstParent.InstanceId, diagnostic.InstanceId);
        Assert.Same(mismatchedOverlap, rejected.AfterPartyRoster);
        Assert.Empty(rejected.RosterTransitions);
    }

    [Fact]
    public void Commit_UsesValidatedSacrificialSelectionAndPreservesAuthoredSkillOrder()
    {
        TransactionContext context = CreateContext();
        FusionParticipantSnapshot echo = Participant(
            context.Catalog.GetRequiredEntity(Qualified("echo_adept")),
            "echo_parent");
        FusionParticipantSnapshot sacrifice = Participant(
            context.Catalog.GetRequiredEntity(Qualified("ashling")),
            "ashling_sacrifice");
        FusionPlanningResult plan = context.Planner.CreatePlan(new FusionPlanningRequest(
            echo,
            context.SecondParent,
            sacrifice,
            IsSacrificial: true));
        ValidatedFusionInheritanceSelection selection = Selection(
            context.Planner,
            plan,
            [Qualified("frost_tip"), Qualified("echo_strike"), Qualified("steady_breath")]);
        var owner = new RuntimeActorReferenceSnapshot(
            RuntimeInstanceId.Parse("owner"),
            Qualified("echo_adept"),
            "Owner");
        RuntimePartyRosterSnapshot party = new(
            owner,
            companionRoster: [Reference(echo), Reference(context.SecondParent), Reference(sacrifice)]);
        var service = new FusionTransactionService(
            context.ActorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));

        FusionTransactionAssessment assessment = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            plan,
            selection,
            party,
            OwnerActor(party),
            RuntimeInstanceId.Parse("sacrificial_result"),
            Id("player_team"),
            Id("test_controller")));
        FusionTransactionCommitResult result = service.Commit(new FusionTransactionCommitRequest(
            assessment.RequirePreparedTransaction(),
            party));

        Assert.True(result.Applied);
        Assert.Equal(
            [echo.InstanceId, context.SecondParent.InstanceId, sacrifice.InstanceId],
            result.ConsumedParticipantIds);
        Assert.Equal(
            [
                Qualified("shell_bash"),
                Qualified("soften_guard"),
                Qualified("frost_tip"),
                Qualified("echo_strike"),
                Qualified("steady_breath")
            ],
            result.ResultActorSnapshot?.Skills.LearnedSkillIds);
        RuntimePassiveSkillStateSnapshot passiveState = Assert.Single(
            result.ResultActorSnapshot!.BattleActivations.PassiveSkillStates);
        Assert.Equal(Qualified("steady_breath"), passiveState.SkillId);
        Assert.True(passiveState.IsEnabled);
        Assert.Equal(4, result.RosterTransitions.Count);
    }

    [Fact]
    public void Commit_StatBoostRetainsTargetIdentityAndConsumesOnlyCatalyst()
    {
        TransactionContext context = CreateContext();
        EntityDefinition targetDefinition = context.Catalog.GetRequiredEntity(Qualified("ward_shell"));
        FusionParticipantSnapshot target = Participant(targetDefinition, "boost_target");
        FusionParticipantSnapshot catalyst = Participant(
            context.Catalog.GetRequiredEntity(Qualified("ashling")),
            "boost_catalyst");
        Dictionary<ContentId, int> boostedStats = target.Stats.ToDictionary(entry => entry.Key, entry => entry.Value);
        boostedStats[Id("strength")] = boostedStats.GetValueOrDefault(Id("strength")) + 2;
        var resolution = new FusionResolvedResult(
            FusionRuntimeOperation.StatBoost,
            target.EntityId,
            isAccident: false,
            target,
            catalyst,
            matchedRecipe: null,
            resultPolicyId: Id("test_stat_boost"),
            boostedStats,
            Array.Empty<FusionRuntimeDiagnostic>());
        var resultEntity = new FusionEntitySnapshot(targetDefinition);
        FusionInheritancePlan selectionPlan = new FusionInheritancePlanner().CreatePlan(
            new FusionInheritancePlanRequest(
                targetDefinition,
                Array.Empty<SkillDefinition>(),
                target.SkillIds,
                maximumSelections: 0));
        var plan = new FusionPlanningResult(
            resolution,
            resultEntity,
            target,
            catalyst,
            target,
            sacrifice: null,
            target.SkillIds,
            Array.Empty<ContentId>(),
            Array.Empty<ContentId>(),
            Array.Empty<FusionInheritanceEntry>(),
            maximumInheritanceSlots: 0,
            sacrificeDecision: null,
            policyContext: FusionPolicyContext.Empty,
            inheritancePlan: selectionPlan);
        ValidatedFusionInheritanceSelection selection = new FusionInheritanceSelectionValidator()
            .Validate(selectionPlan, [])
            .RequireValidSelection();
        var owner = new RuntimeActorReferenceSnapshot(
            RuntimeInstanceId.Parse("owner"),
            Qualified("echo_adept"),
            "Owner");
        RuntimePartyRosterSnapshot party = new(
            owner,
            companionRoster: [Reference(target), Reference(catalyst)]);
        RuntimeActorSnapshot initializedTarget = context.ActorFactory.Create(new CatalogBattleActorCreationRequest(
                target.EntityId,
                target.InstanceId,
                Id("player_team"),
                target.Level,
                CommandAuthorityId: Id("test_controller"),
                IsDeployed: false))
            .RequireActor()
            .State
            .ToSnapshot();
        RuntimeActorSnapshot existingTarget = WithSkills(
            initializedTarget,
            [Qualified("shell_bash"), Qualified("soften_guard"), Qualified("frost_tip")],
            [Qualified("shell_bash")]);
        var actorFactory = new CountingActorFactory(context.ActorFactory);
        var service = new FusionTransactionService(
            actorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));

        FusionTransactionAssessment missingActor = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            plan,
            selection,
            party,
            OwnerActor(party),
            target.InstanceId,
            Id("player_team"),
            Id("test_controller")));
        Assert.False(missingActor.CanCommit);
        Assert.Equal(
            FusionRuntimeDiagnosticCode.ResultActorSnapshotInvalid,
            Assert.Single(missingActor.Diagnostics).Code);
        Assert.Empty(missingActor.RosterTransitions);

        FusionTransactionAssessment assessment = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            plan,
            selection,
            party,
            OwnerActor(party),
            target.InstanceId,
            Id("player_team"),
            Id("test_controller"),
            existingTarget));
        RuntimeActorSnapshot equivalentButStaleActor = existingTarget.WithResources(existingTarget.Resources);
        FusionTransactionCommitResult stale = service.Commit(new FusionTransactionCommitRequest(
            assessment.RequirePreparedTransaction(),
            party,
            currentResultActor: equivalentButStaleActor));
        Assert.Equal(FusionTransactionCommitCode.PreparationStale, stale.Code);
        Assert.Equal(0, actorFactory.CreateCount);
        Assert.Equal(0, actorFactory.RestoreCount);

        FusionTransactionCommitResult result = service.Commit(new FusionTransactionCommitRequest(
            assessment.RequirePreparedTransaction(),
            party,
            currentResultActor: existingTarget));

        Assert.True(result.Applied);
        Assert.Equal([catalyst.InstanceId], result.ConsumedParticipantIds);
        RuntimeActorReferenceSnapshot remaining = Assert.Single(result.AfterPartyRoster.CompanionRoster);
        Assert.Equal(target.InstanceId, remaining.InstanceId);
        Assert.Equal(target.EntityId, remaining.EntityDefinitionId);
        Assert.Single(result.RosterTransitions);
        Assert.Equal(boostedStats[Id("strength")], result.ResultActorSnapshot?.Stats.BaseStats[Id("strength")]);
        Assert.Equal(existingTarget.Resources, result.ResultActorSnapshot?.Resources);
        Assert.Equal(existingTarget.Skills.LearnedSkillIds, result.ResultActorSnapshot?.Skills.LearnedSkillIds);
        Assert.Equal(existingTarget.Skills.EquippedSkillIds, result.ResultActorSnapshot?.Skills.EquippedSkillIds);
        Assert.Equal(0, actorFactory.CreateCount);
        Assert.Equal(1, actorFactory.RestoreCount);
    }

    [Fact]
    public void Prepare_RejectsAnIncomingRosterThatViolatesInjectedCapacityAtomically()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        var actorFactory = new CountingActorFactory(context.ActorFactory);
        var service = new FusionTransactionService(
            actorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(0)));

        FusionTransactionAssessment assessment = service.Prepare(Request(
            context,
            party,
            FusionParticipantRosterKind.Companion,
            "capacity_result"));

        Assert.False(assessment.CanCommit);
        Assert.Same(party, assessment.BeforePartyRoster);
        Assert.Same(party, assessment.AfterPartyRoster);
        Assert.Equal(
            FusionRuntimeDiagnosticCode.RosterTransitionRejected,
            Assert.Single(assessment.Diagnostics).Code);
        PartyRosterTransitionResult transition = Assert.Single(assessment.RosterTransitions);
        Assert.False(transition.Applied);
        Assert.Equal(PartyRosterTransitionCode.InvalidSnapshot, transition.Code);
        Assert.Equal(2, party.CompanionRoster.Count);
        Assert.Equal(0, actorFactory.CreateCount);
        Assert.Equal(0, actorFactory.RestoreCount);
    }

    [Fact]
    public void Prepare_RejectsReusedIdentityAndSelectionTokenFromAnotherPlan()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        var service = new FusionTransactionService(
            context.ActorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));

        FusionTransactionAssessment reusedIdentity = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            context.Plan,
            context.Selection,
            party,
            OwnerActor(party),
            party.Owner.InstanceId,
            Id("player_team"),
            Id("test_controller")));

        Assert.False(reusedIdentity.CanCommit);
        Assert.Equal(
            FusionRuntimeDiagnosticCode.ResultIdentityInUse,
            Assert.Single(reusedIdentity.Diagnostics).Code);
        Assert.Empty(reusedIdentity.RosterTransitions);

        FusionParticipantSnapshot echo = Participant(
            context.Catalog.GetRequiredEntity(Qualified("echo_adept")),
            "echo_sacrificial");
        FusionPlanningResult otherPlan = context.Planner.CreatePlan(new FusionPlanningRequest(
            echo,
            context.SecondParent,
            context.FirstParent,
            IsSacrificial: true));
        ValidatedFusionInheritanceSelection otherSelection = Selection(
            context.Planner,
            otherPlan,
            selectedSkillIds: null);
        Assert.NotEqual(context.Plan.MaximumInheritanceSlots, otherSelection.MaximumSelections);

        FusionTransactionAssessment wrongSelection = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            context.Plan,
            otherSelection,
            party,
            OwnerActor(party),
            RuntimeInstanceId.Parse("other_result"),
            Id("player_team"),
            Id("test_controller")));

        Assert.False(wrongSelection.CanCommit);
        Assert.Equal(
            FusionRuntimeDiagnosticCode.InvalidSelection,
            Assert.Single(wrongSelection.Diagnostics).Code);
        Assert.Same(party, wrongSelection.AfterPartyRoster);

        RuntimePartyRosterSnapshot mismatchedParent = party.With(companionRoster:
        [
            new RuntimeActorReferenceSnapshot(
                context.FirstParent.InstanceId,
                Qualified("annex_mentor"),
                "Wrong Entity"),
            Reference(context.SecondParent)
        ]);
        FusionTransactionAssessment wrongParent = service.Prepare(new FusionTransactionPreparationRequest(
            FusionParticipantRosterKind.Companion,
            context.Plan,
            context.Selection,
            mismatchedParent,
            OwnerActor(mismatchedParent),
            RuntimeInstanceId.Parse("validated_result"),
            Id("player_team"),
            Id("test_controller")));
        Assert.False(wrongParent.CanCommit);
        FusionRuntimeDiagnostic wrongParentDiagnostic = Assert.Single(wrongParent.Diagnostics);
        Assert.Equal(FusionRuntimeDiagnosticCode.RosterTransitionRejected, wrongParentDiagnostic.Code);
        Assert.Equal(context.FirstParent.InstanceId, wrongParentDiagnostic.InstanceId);
        Assert.Empty(wrongParent.RosterTransitions);
        Assert.Same(mismatchedParent, wrongParent.AfterPartyRoster);
    }

    [Fact]
    public void Commit_ActorCreationFailureDoesNotPublishPreparedStockState()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        var rejectingFactory = new RejectingActorFactory();
        var service = new FusionTransactionService(
            rejectingFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));
        PreparedFusionTransaction prepared = service.Prepare(Request(
                context,
                party,
                FusionParticipantRosterKind.Companion,
                "rejected_result"))
            .RequirePreparedTransaction();

        FusionTransactionCommitResult result = service.Commit(new FusionTransactionCommitRequest(
            prepared,
            party));

        Assert.False(result.Applied);
        Assert.Equal(FusionTransactionCommitCode.ActorCreationRejected, result.Code);
        Assert.Same(party, result.BeforePartyRoster);
        Assert.Same(party, result.AfterPartyRoster);
        Assert.Null(result.ResultActor);
        Assert.Null(result.ResultActorSnapshot);
        Assert.Empty(result.ConsumedParticipantIds);
        Assert.Empty(result.RosterTransitions);
        Assert.Equal(prepared.ConsumedParticipantIds, result.PlannedConsumedParticipantIds);
        Assert.Equal(prepared.RosterTransitions, result.PlannedRosterTransitions);
        Assert.Equal(
            FusionRuntimeDiagnosticCode.ActorCreationFailed,
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal(1, rejectingFactory.CreateCount);
        Assert.Equal(0, rejectingFactory.RestoreCount);
    }

    [Fact]
    public void Prepare_RejectsPreviewThatDoesNotMatchValidatedPlan()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        var actorFactory = new CountingActorFactory(context.ActorFactory);
        var service = new FusionTransactionService(
            actorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)),
            new MismatchedPreviewService());

        FusionTransactionAssessment assessment = service.Prepare(Request(
            context,
            party,
            FusionParticipantRosterKind.Companion,
            "mismatched_preview_result"));

        Assert.False(assessment.CanCommit);
        Assert.Equal(FusionRuntimeDiagnosticCode.InvalidPreview, Assert.Single(assessment.Diagnostics).Code);
        Assert.Empty(assessment.RosterTransitions);
        Assert.Same(party, assessment.AfterPartyRoster);
        Assert.Equal(0, actorFactory.CreateCount);
        Assert.Equal(0, actorFactory.RestoreCount);
    }

    [Fact]
    public void Commit_RejectsActorFactoryOutputThatDoesNotMatchPreparedOwnership()
    {
        TransactionContext context = CreateContext();
        RuntimePartyRosterSnapshot party = Party(context, FusionParticipantRosterKind.Companion);
        var actorFactory = new WrongOwnershipActorFactory(context.ActorFactory);
        var service = new FusionTransactionService(
            actorFactory,
            new PartyRosterTransitionService(new FixedCapacityPolicy(12)));
        PreparedFusionTransaction prepared = service.Prepare(Request(
                context,
                party,
                FusionParticipantRosterKind.Companion,
                "wrong_ownership_result"))
            .RequirePreparedTransaction();

        FusionTransactionCommitResult result = service.Commit(new FusionTransactionCommitRequest(
            prepared,
            party));

        Assert.Equal(FusionTransactionCommitCode.ActorCreationRejected, result.Code);
        Assert.False(result.Applied);
        Assert.Same(party, result.AfterPartyRoster);
        Assert.Null(result.ResultActor);
        Assert.Null(result.ResultActorSnapshot);
        Assert.Empty(result.ConsumedParticipantIds);
        Assert.Empty(result.RosterTransitions);
        Assert.Equal(prepared.ConsumedParticipantIds, result.PlannedConsumedParticipantIds);
        Assert.Equal(prepared.RosterTransitions, result.PlannedRosterTransitions);
        Assert.Equal(FusionRuntimeDiagnosticCode.ActorCreationFailed, Assert.Single(result.Diagnostics).Code);
    }

    private static TransactionContext CreateContext()
    {
        GameDataCatalog catalog = LoadCatalog();
        var repository = new CatalogFusionContentRepository(catalog);
        FusionPolicyRegistry policies = Policies();
        var planner = new FusionPlanningService(
            repository,
            new FusionResultResolver(repository, new MaximumRandomSource(), policies),
            new MaximumRandomSource(),
            policies);
        FusionParticipantSnapshot first = Participant(
            catalog.GetRequiredEntity(Qualified("ashling")),
            "ashling_parent");
        FusionParticipantSnapshot second = Participant(
            catalog.GetRequiredEntity(Qualified("bramble_runner")),
            "bramble_parent");
        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            first,
            second,
            Sacrifice: null,
            IsSacrificial: false));
        ValidatedFusionInheritanceSelection selection = Selection(planner, plan, selectedSkillIds: null);
        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new TestInitializationPolicy());
        return new TransactionContext(
            catalog,
            repository,
            planner,
            plan,
            selection,
            first,
            second,
            actorFactory);
    }

    private static FusionTransactionPreparationRequest Request(
        TransactionContext context,
        RuntimePartyRosterSnapshot party,
        FusionParticipantRosterKind ownerKind,
        string resultInstanceId) =>
        new(
            ownerKind,
            context.Plan,
            context.Selection,
            party,
            OwnerActor(party),
            RuntimeInstanceId.Parse(resultInstanceId),
            Id("player_team"),
            Id("test_controller"));

    private static RuntimeActorSnapshot OwnerActor(RuntimePartyRosterSnapshot party) =>
        new(
            new RuntimeActorIdentitySnapshot(
                party.Owner.InstanceId,
                party.Owner.EntityDefinitionId,
                Id("independent_actor"),
                party.Owner.DisplayName),
            new RuntimeActorAffiliationSnapshot(
                Id("test_controller"),
                Id("player_team")),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            new RuntimeProgressionSnapshot(20, 0, 0, 0),
            [new RuntimeResourceSnapshot(Id("hp"), 1, 1)],
            new RuntimeStatBlockSnapshot(),
            new RuntimeSkillStateSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(Id("hp"), 1)],
            Id("hp"));

    private static RuntimePartyRosterSnapshot Party(
        TransactionContext context,
        FusionParticipantRosterKind ownerKind)
    {
        var owner = new RuntimeActorReferenceSnapshot(
            RuntimeInstanceId.Parse("owner"),
            Qualified("echo_adept"),
            "Owner");
        RuntimeActorReferenceSnapshot[] parents =
        [
            Reference(context.FirstParent),
            Reference(context.SecondParent)
        ];
        return ownerKind == FusionParticipantRosterKind.Companion
            ? new RuntimePartyRosterSnapshot(owner, companionRoster: parents)
            : new RuntimePartyRosterSnapshot(owner, hostedEntityRoster: parents);
    }

    private static ValidatedFusionInheritanceSelection Selection(
        FusionPlanningService planner,
        FusionPlanningResult plan,
        IEnumerable<ContentId>? selectedSkillIds = null)
    {
        return planner
            .ValidateInheritanceSelection(plan, selectedSkillIds ?? [])
            .RequireValidSelection();
    }

    private static FusionParticipantSnapshot Participant(EntityDefinition entity, string instanceId) =>
        new(
            RuntimeInstanceId.Parse(instanceId),
            entity.Id,
            entity.DisplayName,
            entity.RaceId,
            entity.Rank,
            entity.BaseLevel,
            entity.BaseSkillIds,
            entity.Stats);

    private static RuntimeActorReferenceSnapshot Reference(FusionParticipantSnapshot participant) =>
        new(participant.InstanceId, participant.EntityId, participant.DisplayName);

    private static RuntimeActorSnapshot WithSkills(
        RuntimeActorSnapshot snapshot,
        IEnumerable<ContentId> learnedSkillIds,
        IEnumerable<ContentId> equippedSkillIds) =>
        new(
            snapshot.Identity,
            snapshot.Affiliation,
            snapshot.EncounterPresence,
            snapshot.Progression,
            snapshot.Resources,
            snapshot.Stats,
            new RuntimeSkillStateSnapshot(learnedSkillIds, equippedSkillIds),
            snapshot.Equipment,
            snapshot.BattleStatus,
            snapshot.BattleActivations,
            snapshot.BaseResourceValues,
            snapshot.VitalResourceId,
            snapshot.CapabilityIds);

    private static FusionPolicyRegistry Policies() =>
        new(
            new TieredFusionInheritanceSlotPolicy(
                [new FusionInheritanceSlotTier(0, 1)],
                maximumSlots: 8),
            new FixedFusionSacrificePolicy(true, 2),
            accidentPolicies:
            [
                new PercentageFusionAccidentPolicy(Id("standard_accident"), chancePercent: 1)
            ],
            mutationPolicies:
            [
                new AdjacentTierFusionMutationPolicy(Id("standard_mutation"), chancePercent: 20)
            ]);

    private static GameDataCatalog LoadCatalog()
    {
        ContentPackTextRequest request = TrainingAnnexHostSupport.CreateContentRequest();
        string root = FindJsonRoot();
        var bundle = new ContentPackTextBundle(
            request.ManifestPath,
            File.ReadAllText(TestContentPath.ResolveManifest(root, request.ManifestPath)),
            request.DocumentPaths.Select(path => new ContentDocumentText(
                path,
                path,
                File.ReadAllText(TestContentPath.ResolveDocument(root, request.ManifestPath, path)))));
        CatalogLoadResult result = new SkillSystemCatalogLoader().Load(new SkillSystemCatalogLoadRequest(
            TrainingAnnexHostSupport.BuildRegistrations(),
            [bundle]));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return result.RequireCatalog();
    }

    private static string FindJsonRoot() => Path.Combine(AppContext.BaseDirectory, "Content");

    private static ContentId Qualified(string localId) => Id($"{Pack}:{localId}");
    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed record TransactionContext(
        GameDataCatalog Catalog,
        IFusionContentRepository Repository,
        FusionPlanningService Planner,
        FusionPlanningResult Plan,
        ValidatedFusionInheritanceSelection Selection,
        FusionParticipantSnapshot FirstParent,
        FusionParticipantSnapshot SecondParent,
        ICatalogBattleActorFactory ActorFactory);

    private sealed class FixedCapacityPolicy(int capacity) : IRosterCapacityPolicy
    {
        public int GetCapacity(RuntimeRosterKind rosterKind, int ownerLevel) => capacity;
    }

    private sealed class CountingActorFactory(ICatalogBattleActorFactory inner) : ICatalogBattleActorFactory
    {
        public int CreateCount { get; private set; }
        public int RestoreCount { get; private set; }

        public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request)
        {
            CreateCount++;
            return inner.Create(request);
        }

        public CatalogBattleActorCreationResult Restore(CatalogBattleActorRestoreRequest request)
        {
            RestoreCount++;
            return inner.Restore(request);
        }
    }

    private sealed class RejectingActorFactory : ICatalogBattleActorFactory
    {
        public int CreateCount { get; private set; }
        public int RestoreCount { get; private set; }

        public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request)
        {
            CreateCount++;
            return new CatalogBattleActorCreationResult(
                null,
                [
                    new CatalogBattleActorDiagnostic(
                        CatalogBattleActorDiagnosticCode.InitializationFailed,
                        "Rejected by test actor factory.",
                        request.EntityId)
                ]);
        }

        public CatalogBattleActorCreationResult Restore(CatalogBattleActorRestoreRequest request)
        {
            RestoreCount++;
            throw new InvalidOperationException("Restore must not be called after creation rejection.");
        }
    }

    private sealed class WrongOwnershipActorFactory(ICatalogBattleActorFactory inner)
        : ICatalogBattleActorFactory
    {
        public CatalogBattleActorCreationResult Create(CatalogBattleActorCreationRequest request) =>
            inner.Create(request with { TeamId = Id("wrong_team") });

        public CatalogBattleActorCreationResult Restore(CatalogBattleActorRestoreRequest request) =>
            inner.Restore(request);
    }

    private sealed class MismatchedPreviewService : IFusionPreviewService
    {
        public FusionPreviewSnapshot? CreatePreview(FusionPreviewRequest request)
        {
            FusionPreviewSnapshot? preview = new FusionPreviewService().CreatePreview(request);
            return preview is null
                ? null
                : new FusionPreviewSnapshot(
                    Qualified("annex_mentor"),
                    preview.DisplayName,
                    preview.RaceId,
                    preview.Rank,
                    preview.Level,
                    preview.NaturalSkillIds,
                    preview.InheritedSkillIds,
                    preview.Stats,
                    preview.Experience,
                    preview.LifetimeExperience);
        }
    }

    private sealed class TestInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) =>
            new(
                Id("hp"),
                [
                    new BattleResourceState(Id("hp"), 100, 100),
                    new BattleResourceState(Id("sp"), 50, 50)
                ]);
    }

    private sealed class MaximumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => maximumExclusive - 1;
        public decimal NextUnitDecimal() => 0.99m;
    }
}
