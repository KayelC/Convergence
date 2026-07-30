using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Hosting;
using Convergence.Runtime;
using Convergence.TurnEconomy;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class StatModifierExecutionIntegrationTests
{
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Attack = ContentId.Parse("attack");
    private static readonly ContentId Defense = ContentId.Parse("defense");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");
    private static readonly ContentId PhaseEnd = ContentId.Parse("phase_end");
    private static readonly ContentId PlayerPhase = ContentId.Parse("player_phase");

    [Theory]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task EncounterRunner_AnchorsTimedApplicationToItsCurrentOwnerTurnBoundary(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        SkillDefinition skill = ActiveSkill(
            "encounter_boundary_focus",
            [new ModifyStatStageEffectDefinition([Attack], 1, Turns(3))]);
        RuntimeActorState actor = Actor("encounter_actor", skillIds: [skill.Id]);
        BattleExecutionServices executionServices = Services(policy);
        var actionExecutor = new BattleActionExecutor(
            new SkillExecutor(executionServices),
            new ItemExecutor(executionServices),
            executionServices,
            new CatalogBattleActionAuthorizationPolicy(
                new SkillRepository([skill]),
                new ItemRepository([]),
                NoBattleBasicAttackProfileSource.Instance));
        var turnHandler = new TimedModifierTurnHandler(actionExecutor, skill);
        var completion = new CaptureTimedModifierCompletion();
        var lifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            executionServices,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            TestEncounterClocks.Standard(PlayerTeam, ContentId.Parse("enemy_team")));
        var participant = new BattleEncounterParticipant(actor, "Encounter Actor");

        BattleEncounterResult result = await new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest([participant], Battle, NormalBattle, null, roundLimit: 3),
            new BattleEncounterServices(
                new ParticipantOrderInitiativePolicy(),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                lifecycle,
                turnHandler,
                completion,
                () => new StandardActionTurnEconomy(),
                new BattlePhaseProgressPolicy(16, 4),
                new BattleEncounterProgressPolicy(256)));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal([3, 2], completion.RemainingDurations);
        Assert.Equal(3, turnHandler.DurationBeforeSecondAction);
        Assert.All(turnHandler.Boundaries, boundary =>
            Assert.Equal(OwnerTurnEnd, Assert.Single(boundary).EventId));
        Assert.Equal([1L, 2L], turnHandler.Boundaries.Select(boundary => boundary.Single().Sequence));
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.TimedExclusive, TargetRelation.Ally)]
    [InlineData(SuppliedPolicyKind.TimedExclusive, TargetRelation.Enemy)]
    [InlineData(SuppliedPolicyKind.TimedContribution, TargetRelation.Ally)]
    [InlineData(SuppliedPolicyKind.TimedContribution, TargetRelation.Enemy)]
    public async Task EncounterLifecycle_CrossTargetModifierUsesOneOwnerTurnEventSequence(
        SuppliedPolicyKind policyKind,
        TargetRelation relation)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        ContentId enemyTeam = ContentId.Parse("enemy_team");
        ContentId targetTeam = relation == TargetRelation.Ally ? PlayerTeam : enemyTeam;
        SkillDefinition skill = ActiveSkill(
            $"{policyKind}_{relation}_focus",
            [new ModifyStatStageEffectDefinition([Attack], 1, Turns(3))],
            targeting: new TargetingDefinition(
                relation,
                TargetSelection.Single,
                TargetLifeState.Alive,
                AllowSelf: false));
        RuntimeActorState source = Actor("sequence_source");
        RuntimeActorState target = Actor("sequence_target", teamId: targetTeam);
        RuntimeActorState intervening = Actor("sequence_intervening");
        BattleExecutionServices services = Services(policy);
        var port = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            TestEncounterClocks.Standard(PlayerTeam, enemyTeam));
        BattleEncounterParticipant[] participants =
        [
            new(source, "Sequence Source"),
            new(target, "Sequence Target"),
            new(intervening, "Sequence Intervening")
        ];
        var encounter = new BattleEncounterRequest(
            participants,
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 3);

        await port.ProcessTurnEndAsync(TurnRequest(encounter, participants[0], participants));
        StatModifierLifecycleBoundary applicationBoundary = Assert.Single(
            port.GetActiveStatModifierBoundaries(TurnRequest(encounter, participants[0], participants)));
        SkillExecutionResult application = new SkillExecutor(services).Execute(
            new SkillExecutionRequest(
                skill,
                source,
                [source, target],
                new EffectExecutionEnvironment(
                    Battle,
                    NormalBattle,
                    activeStatModifierBoundaries: [applicationBoundary]),
                [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, application.Status);
        Assert.Equal(2, applicationBoundary.Sequence);
        Assert.Equal(3, RemainingDuration(target));
        await port.ProcessTurnEndAsync(TurnRequest(encounter, participants[0], participants));
        Assert.Equal(3, RemainingDuration(target));

        await port.ProcessTurnEndAsync(TurnRequest(encounter, participants[2], participants));
        Assert.Equal(3, RemainingDuration(target));

        await port.ProcessTurnEndAsync(TurnRequest(encounter, participants[1], participants));
        Assert.Equal(2, RemainingDuration(target));
        await port.ProcessTurnEndAsync(TurnRequest(encounter, participants[1], participants));
        Assert.Equal(1, RemainingDuration(target));
        await port.ProcessTurnEndAsync(TurnRequest(encounter, participants[1], participants));

        Assert.Empty(target.StatStages);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task EncounterLifecycle_SharedTeamPhaseEventAdvancesEveryOccurrenceExactlyOnce(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        ContentId enemyTeam = ContentId.Parse("enemy_team");
        ContentId sharedPhaseEnd = ContentId.Parse("shared_phase_end");
        RuntimeActorState actor = Actor("shared_phase_actor");
        ContentId statusId = ContentId.Parse("shared_phase_status");
        actor.AddOtherStatus(
            statusId,
            StandardStatusLifetimes.Encounter(
                new TurnDurationDefinition(2, sharedPhaseEnd, SuspendWhileReserve: false)));
        ApplyModifier(
            actor,
            policy,
            new TurnDurationDefinition(2, sharedPhaseEnd, SuspendWhileReserve: false));
        RuntimeActorState enemy = Actor("shared_phase_enemy", teamId: enemyTeam);
        BattleExecutionServices services = Services(policy);
        var port = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            new ExplicitBattleEncounterLifecycleClockPolicy(
            [
                new BattleTeamPhaseClockDefinition(PlayerTeam, PlayerPhase, sharedPhaseEnd),
                new BattleTeamPhaseClockDefinition(
                    enemyTeam,
                    ContentId.Parse("enemy_phase"),
                    sharedPhaseEnd)
            ],
            ContentId.Parse("round_end")));
        BattleEncounterParticipant[] participants =
        [
            new(actor, "Shared Phase Actor"),
            new(enemy, "Shared Phase Enemy")
        ];
        var encounter = new BattleEncounterRequest(
            participants,
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 3);
        var request = new BattleEncounterLifecycleRequest(
            encounter,
            participants,
            [PlayerTeam, enemyTeam]);

        await port.ProcessPhaseEndAsync(request, PlayerTeam);

        Assert.Equal(1, RemainingStatusDuration(actor, statusId));
        Assert.Equal(1, RemainingDuration(actor));

        await port.ProcessPhaseEndAsync(request, enemyTeam);

        Assert.DoesNotContain(statusId, actor.OtherStatuses);
        Assert.Empty(actor.StatStages);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task EncounterLifecycle_SharedPhaseAndRoundEventUseOneSequenceStream(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        ContentId enemyTeam = ContentId.Parse("enemy_team");
        ContentId sharedEvent = ContentId.Parse("shared_phase_round_end");
        RuntimeActorState actor = Actor("shared_round_actor");
        ContentId statusId = ContentId.Parse("shared_round_status");
        actor.AddOtherStatus(
            statusId,
            StandardStatusLifetimes.Encounter(
                new TurnDurationDefinition(2, sharedEvent, SuspendWhileReserve: false)));
        ApplyModifier(
            actor,
            policy,
            new TurnDurationDefinition(2, sharedEvent, SuspendWhileReserve: false));
        RuntimeActorState enemy = Actor("shared_round_enemy", teamId: enemyTeam);
        BattleExecutionServices services = Services(policy);
        var port = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            new ExplicitBattleEncounterLifecycleClockPolicy(
            [
                new BattleTeamPhaseClockDefinition(PlayerTeam, PlayerPhase, sharedEvent),
                new BattleTeamPhaseClockDefinition(
                    enemyTeam,
                    ContentId.Parse("enemy_phase"),
                    ContentId.Parse("enemy_phase_end"))
            ],
            sharedEvent));
        BattleEncounterParticipant[] participants =
        [
            new(actor, "Shared Round Actor"),
            new(enemy, "Shared Round Enemy")
        ];
        var encounter = new BattleEncounterRequest(
            participants,
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 3);
        var request = new BattleEncounterLifecycleRequest(
            encounter,
            participants,
            [PlayerTeam, enemyTeam]);

        await port.ProcessPhaseEndAsync(request, PlayerTeam);
        await port.ProcessRoundEndAsync(request, roundNumber: 1);

        Assert.DoesNotContain(statusId, actor.OtherStatuses);
        Assert.Empty(actor.StatStages);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task EncounterLifecycle_CancellationDoesNotConsumePendingOwnerTurnBoundary(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        RuntimeActorState actor = Actor("cancelled_boundary_actor");
        BattleExecutionServices services = Services(policy);
        var port = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            TestEncounterClocks.Standard(PlayerTeam, ContentId.Parse("enemy_team")));
        BattleEncounterParticipant[] participants = [new(actor, "Cancelled Boundary Actor")];
        var encounter = new BattleEncounterRequest(
            participants,
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 3);
        BattleEncounterTurnLifecycleRequest request = TurnRequest(
            encounter,
            participants[0],
            participants);
        StatModifierLifecycleBoundary before = Assert.Single(
            port.GetActiveStatModifierBoundaries(request));
        ApplyModifier(actor, policy, Turns(3), before);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            port.ProcessTurnEndAsync(request, cancellation.Token).AsTask());

        Assert.Equal(3, RemainingDuration(actor));
        StatModifierLifecycleBoundary afterCancellation = Assert.Single(
            port.GetActiveStatModifierBoundaries(request));
        Assert.Equal(before.EventId, afterCancellation.EventId);
        Assert.Equal(before.Sequence, afterCancellation.Sequence);
        await port.ProcessTurnEndAsync(request);
        Assert.Equal(3, RemainingDuration(actor));
        Assert.Equal(2, Assert.Single(port.GetActiveStatModifierBoundaries(request)).Sequence);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task EncounterLifecycle_CancellationDuringProcessingRollsBackActorAndClock(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        RuntimeActorState actor = Actor("mid_processing_cancel_actor");
        BattleExecutionServices services = Services(policy);
        using var cancellation = new CancellationTokenSource();
        var lifecycle = new CancellingLifecycleService(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            cancellation,
            LifecycleCancellationPoint.TurnEnd);
        var port = new BattleStatusEncounterLifecyclePort(
            lifecycle,
            services,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            TestEncounterClocks.Standard(
                PlayerTeam,
                ContentId.Parse("enemy_team")));
        BattleEncounterParticipant[] participants =
            [new(actor, "Mid Processing Cancel Actor")];
        var encounter = new BattleEncounterRequest(
            participants,
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 3);
        BattleEncounterTurnLifecycleRequest request = TurnRequest(
            encounter,
            participants[0],
            participants);
        StatModifierLifecycleBoundary boundary = Assert.Single(
            port.GetActiveStatModifierBoundaries(request));
        ApplyModifier(actor, policy, Turns(3), boundary);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            port.ProcessTurnEndAsync(request, cancellation.Token).AsTask());

        Assert.Equal(3, RemainingDuration(actor));
        StatModifierLifecycleBoundary afterCancellation = Assert.Single(
            port.GetActiveStatModifierBoundaries(request));
        Assert.Equal(boundary.EventId, afterCancellation.EventId);
        Assert.Equal(boundary.Sequence, afterCancellation.Sequence);

        await port.ProcessTurnEndAsync(request);

        Assert.Equal(3, RemainingDuration(actor));
        Assert.Equal(
            2,
            Assert.Single(port.GetActiveStatModifierBoundaries(request)).Sequence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EncounterLifecycle_CancellationDuringClockProcessingRollsBackActorAndSequence(
        bool processRound)
    {
        IStatModifierPolicyService policy = TimedExclusivePolicy();
        ContentId playerPhaseEnd = ContentId.Parse("cancel_player_phase_end");
        ContentId roundEnd = ContentId.Parse("cancel_round_end");
        ContentId tickEvent = processRound ? roundEnd : playerPhaseEnd;
        RuntimeActorState actor = Actor("mid_clock_cancel_actor");
        ApplyModifier(
            actor,
            policy,
            new TurnDurationDefinition(3, tickEvent, SuspendWhileReserve: false));
        BattleExecutionServices services = Services(policy);
        using var cancellation = new CancellationTokenSource();
        var lifecycle = new CancellingLifecycleService(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            cancellation,
            LifecycleCancellationPoint.Clock);
        var port = new BattleStatusEncounterLifecyclePort(
            lifecycle,
            services,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            new ExplicitBattleEncounterLifecycleClockPolicy(
            [
                new BattleTeamPhaseClockDefinition(
                    PlayerTeam,
                    PlayerPhase,
                    playerPhaseEnd)
            ],
            roundEnd));
        BattleEncounterParticipant[] participants =
            [new(actor, "Mid Clock Cancel Actor")];
        var encounter = new BattleEncounterRequest(
            participants,
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 3);
        var request = new BattleEncounterLifecycleRequest(
            encounter,
            participants,
            [PlayerTeam]);

        async ValueTask ProcessAsync(CancellationToken token)
        {
            if (processRound)
            {
                await port.ProcessRoundEndAsync(request, roundNumber: 1, token);
            }
            else
            {
                await port.ProcessPhaseEndAsync(request, PlayerTeam, token);
            }
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessAsync(cancellation.Token).AsTask());

        Assert.Equal(3, RemainingDuration(actor));
        Assert.Null(LastModifierBoundary(actor));

        await ProcessAsync(CancellationToken.None);

        Assert.Equal(2, RemainingDuration(actor));
        StatModifierLifecycleBoundary appliedBoundary =
            Assert.IsType<StatModifierLifecycleBoundary>(LastModifierBoundary(actor));
        Assert.Equal(tickEvent, appliedBoundary.EventId);
        Assert.Equal(1, appliedBoundary.Sequence);
    }

    [Fact]
    public async Task EncounterLifecycle_CancellationDuringBattleCleanupRollsBackActor()
    {
        IStatModifierPolicyService policy = TimedExclusivePolicy();
        RuntimeActorState actor = Actor("mid_battle_cleanup_cancel_actor");
        ApplyModifier(actor, policy, Turns(3));
        BattleExecutionServices services = Services(policy);
        using var cancellation = new CancellationTokenSource();
        var lifecycle = new CancellingLifecycleService(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            cancellation,
            LifecycleCancellationPoint.Cleanup);
        var port = new BattleStatusEncounterLifecyclePort(
            lifecycle,
            services,
            ContentId.Parse("battle_start"),
            OwnerTurnEnd,
            TestEncounterClocks.Standard(
                PlayerTeam,
                ContentId.Parse("enemy_team")));
        BattleEncounterParticipant[] participants =
            [new(actor, "Mid Battle Cleanup Cancel Actor")];
        var encounter = new BattleEncounterRequest(
            participants,
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 3);
        var request = new BattleEncounterLifecycleRequest(
            encounter,
            participants,
            [PlayerTeam]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            port.ProcessBattleEndAsync(
                    request,
                    BattleEncounterOutcome.Victory,
                    cancellation.Token)
                .AsTask());

        Assert.Equal(3, RemainingDuration(actor));

        await port.ProcessBattleEndAsync(request, BattleEncounterOutcome.Victory);

        Assert.Empty(actor.StatStages);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.PersistentStaged)]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task SuppliedPolicies_PreserveSkillAuthorizationTargetingAndCostCommit(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        SkillDefinition skill = ActiveSkill(
            $"{policyKind}_authorized_focus",
            [new ModifyStatStageEffectDefinition([Attack], 1, Turns(3))],
            [new SkillCostDefinition(Sp, new FlatAmountDefinition(3))]);
        RuntimeActorState actor = Actor("actor", sp: 20, skillIds: [skill.Id]);
        BattleActionExecutor executor = ActionExecutor(policy, [skill]);
        var request = new BattleActionExecutionRequest(
            new SkillBattleActionCommand(skill, [actor.InstanceId]),
            actor,
            [actor],
            Environment(new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)));

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal([actor.InstanceId], assessment.TargetIds);
        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(17, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(-3, Assert.Single(result.CommittedCostChanges).Delta);
        EffectExecutionResult effect = Assert.Single(result.Effects);
        Assert.Equal(actor.InstanceId, effect.TargetId);
        Assert.Equal(StatModifierTransitionCode.Applied,
            Assert.Single(effect.StatModifierTransitions).Code);
        Assert.Equal(1, actor.StatStages[Attack].Stage);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.PersistentStaged)]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task SuppliedPolicies_CommitExactlyOneItemAfterMeaningfulApplication(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        RuntimeActorState actor = Actor("actor");
        ItemDefinition item = Consumable(
            $"{policyKind}_focus_tonic",
            new ModifyStatStageEffectDefinition([Attack], 1, Turns(3)));
        var inventory = new TestItemInventory(item.Id, 2);
        BattleActionExecutor executor = ActionExecutor(policy, items: [item]);
        var request = new BattleActionExecutionRequest(
            new ItemBattleActionCommand(item, [actor.InstanceId]),
            actor,
            [actor],
            Environment(new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)),
            inventory);

        BattleActionExecutionResult result = await executor.ExecuteAsync(request);

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.ItemConsumption);
        Assert.True(result.ItemConsumptionCommitted);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(1, actor.StatStages[Attack].Stage);
        Assert.Equal(
            [
                BattleActionEventKind.ItemReserved,
                BattleActionEventKind.ItemCommitted,
                BattleActionEventKind.EffectResolved
            ],
            result.Events.Select(value => value.Kind));
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.PersistentStaged)]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public async Task SuppliedPolicies_RollBackItemAndActorWhenInventoryCommitRejects(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        RuntimeActorState actor = Actor("actor");
        ItemDefinition item = Consumable(
            $"{policyKind}_rejected_tonic",
            new ModifyStatStageEffectDefinition([Attack], 1, Turns(3)));
        var inventory = new TestItemInventory(item.Id, 2, rejectCommit: true);
        BattleActionExecutor executor = ActionExecutor(policy, items: [item]);
        var request = new BattleActionExecutionRequest(
            new ItemBattleActionCommand(item, [actor.InstanceId]),
            actor,
            [actor],
            Environment(new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)),
            inventory);

        BattleActionExecutionResult result = await executor.ExecuteAsync(request);

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.False(result.ItemConsumptionCommitted);
        Assert.Equal(2, inventory.Quantity);
        Assert.Null(actor.StatModifierState);
        Assert.Equal(
            [BattleActionEventKind.ItemReserved, BattleActionEventKind.ItemRolledBack],
            result.Events.Select(value => value.Kind));
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.PersistentStaged, false)]
    [InlineData(SuppliedPolicyKind.TimedExclusive, false)]
    [InlineData(SuppliedPolicyKind.TimedContribution, false)]
    [InlineData(SuppliedPolicyKind.PersistentStaged, true)]
    [InlineData(SuppliedPolicyKind.TimedExclusive, true)]
    [InlineData(SuppliedPolicyKind.TimedContribution, true)]
    public void SuppliedPolicies_UseTypedOwnerTurnAndPhaseBoundariesForPassiveLifecycle(
        SuppliedPolicyKind policyKind,
        bool usePhaseBoundary)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        ContentId tickEvent = usePhaseBoundary ? PhaseEnd : OwnerTurnEnd;
        SkillDefinition passive = PassiveSkill(
            $"{policyKind}_{tickEvent}_focus",
            ContentId.Parse("battle_start"),
            new ModifyStatStageEffectDefinition(
                [Attack],
                1,
                new TurnDurationDefinition(1, tickEvent, true)));
        RuntimeActorState actor = Actor("actor", passiveSkills: [passive]);
        BattleExecutionServices services = Services(policy);
        var lifecycle = new BattleStatusLifecycleService(new MinimumRandomSource());
        StatModifierLifecycleBoundary appliedAt = new(tickEvent, 1);

        PassiveTriggerDispatchResult dispatch = services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                ContentId.Parse("battle_start"),
                actor,
                [actor],
                [actor],
                Battle,
                NormalBattle,
                moonPhaseId: null,
                [appliedAt]),
            services);
        BattleStatusLifecycleResult sameBoundary = ProcessBoundary(
            lifecycle,
            services,
            actor,
            tickEvent,
            appliedAt,
            usePhaseBoundary);
        BattleStatusLifecycleResult nextBoundary = ProcessBoundary(
            lifecycle,
            services,
            actor,
            tickEvent,
            new StatModifierLifecycleBoundary(tickEvent, 2),
            usePhaseBoundary);

        Assert.Equal(PassiveTriggerOutcome.Executed, Assert.Single(dispatch.Activations).Outcome);
        Assert.DoesNotContain(sameBoundary.Events, value =>
            value.ModifierEvent?.Kind == StatModifierEventKind.ContributionExpired);
        if (policyKind == SuppliedPolicyKind.PersistentStaged)
        {
            Assert.Equal(1, actor.StatStages[Attack].Stage);
            Assert.DoesNotContain(nextBoundary.Events, value => value.ModifierEvent is not null);
        }
        else
        {
            Assert.Empty(actor.StatStages);
            Assert.Contains(nextBoundary.Events, value =>
                value.ModifierEvent?.Kind == StatModifierEventKind.ContributionExpired);
        }

        lifecycle.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.BattleEnd),
            policy);
        Assert.Empty(actor.StatStages);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.PersistentStaged)]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public void SuppliedPolicies_RemovePositiveAndNegativeStateThroughTypedStatusEffects(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        var executor = new SkillExecutor(Services(policy));
        RuntimeActorState actor = Actor("actor");
        executor.Execute(Request(ActiveSkill(
            $"{policyKind}_mixed_state",
            [
                new ModifyStatStageEffectDefinition([Attack], 1, Turns(3)),
                new ModifyStatStageEffectDefinition([Defense], -1, Turns(3))
            ]), actor, new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)));

        SkillExecutionResult positive = executor.Execute(Request(ActiveSkill(
            $"{policyKind}_clear_positive",
            [new RemoveStatusEffectDefinition([StatusEffectKind.Buff], [])]), actor));

        Assert.Equal(EffectExecutionOutcome.Success, Assert.Single(positive.Effects).Outcome);
        Assert.DoesNotContain(Attack, actor.StatStages.Keys);
        Assert.Equal(-1, actor.StatStages[Defense].Stage);

        SkillExecutionResult negative = executor.Execute(Request(ActiveSkill(
            $"{policyKind}_clear_negative",
            [new RemoveStatusEffectDefinition([StatusEffectKind.Debuff], [])]), actor));

        Assert.Equal(EffectExecutionOutcome.Success, Assert.Single(negative.Effects).Outcome);
        Assert.Empty(actor.StatStages);
    }

    [Theory]
    [InlineData(SuppliedPolicyKind.PersistentStaged)]
    [InlineData(SuppliedPolicyKind.TimedExclusive)]
    [InlineData(SuppliedPolicyKind.TimedContribution)]
    public void SuppliedPolicies_HonorReserveSuspensionWithoutLosingBoundaryOrder(
        SuppliedPolicyKind policyKind)
    {
        IStatModifierPolicyService policy = Policy(policyKind);
        RuntimeActorState activeActor = Actor("reserve_actor");
        SkillDefinition skill = ActiveSkill(
            $"{policyKind}_reserve_focus",
            [new ModifyStatStageEffectDefinition(
                [Attack],
                1,
                new TurnDurationDefinition(3, PhaseEnd, true))]);
        var executor = new SkillExecutor(Services(policy));
        executor.Execute(Request(
            skill,
            activeActor,
            new StatModifierLifecycleBoundary(PhaseEnd, 1)));
        RuntimeActorSnapshot activeSnapshot = activeActor.ToSnapshot();
        var reserveSnapshot = new RuntimeActorSnapshot(
            activeSnapshot.Identity,
            activeSnapshot.Affiliation,
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            activeSnapshot.Progression,
            activeSnapshot.Resources,
            activeSnapshot.Stats,
            activeSnapshot.Skills,
            activeSnapshot.Equipment,
            activeSnapshot.BattleStatus,
            activeSnapshot.BattleActivations,
            activeSnapshot.BaseResourceValues,
            activeSnapshot.VitalResourceId,
            activeSnapshot.CapabilityIds);
        RuntimeActorState actor = RuntimeActorState.Restore(
            reserveSnapshot,
            CombatDefenseProfile.Empty,
            statModifierPolicy: policy);

        var lifecycle = new BattleStatusLifecycleService(new MinimumRandomSource());
        lifecycle.ProcessClock(
            new BattleLifecycleClockRequest(
                [actor],
                new TeamPhaseLifecycleClockBoundary(PhaseEnd, PlayerTeam, PlayerPhase, 2),
                [new StatModifierLifecycleBoundary(PhaseEnd, 2)]),
            policy);

        RuntimeStatModifierContributionSnapshot contribution = Assert.Single(
            Assert.Single(actor.StatModifierState!.Tracks).Contributions);
        Assert.Equal(1, actor.StatStages[Attack].Stage);
        if (policyKind == SuppliedPolicyKind.PersistentStaged)
        {
            Assert.Null(contribution.Duration);
            Assert.Null(contribution.LastLifecycleBoundary);
        }
        else
        {
            Assert.Equal(
                new TurnDurationDefinition(3, PhaseEnd, true),
                contribution.Duration);
            Assert.Equal(1, contribution.LastLifecycleBoundary?.Sequence);
        }
    }

    [Fact]
    public void SkillExecution_UsesTheSelectedApplyPathAndReturnsTypedTransitions()
    {
        var policy = new RecordingStatModifierPolicyService(PersistentPolicy());
        RuntimeActorState actor = Actor("actor");
        SkillDefinition skill = ActiveSkill(
            "dual_focus",
            [new ModifyStatStageEffectDefinition([Attack, Defense], 1)]);
        var executor = new SkillExecutor(Services(policy));

        SkillExecutionResult result = executor.Execute(Request(skill, actor));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        EffectExecutionResult effect = Assert.Single(result.Effects);
        Assert.Equal(EffectExecutionOutcome.Success, effect.Outcome);
        Assert.Equal(2, effect.StatModifierTransitions.Count);
        Assert.All(effect.StatModifierTransitions, transition =>
        {
            Assert.Equal(StatModifierTransitionCode.Applied, transition.Code);
            Assert.Contains(transition.Events, value =>
                value.Kind == StatModifierEventKind.AggregateStageChanged);
        });
        Assert.Equal(2, policy.ApplyCalls);
        Assert.True(policy.AssessCalls >= 2);
        Assert.Equal(policy.PolicyId, actor.StatModifierState?.PolicyId);
        Assert.Equal(1, actor.StatStages[Attack].Stage);
        Assert.Equal(1, actor.StatStages[Defense].Stage);
    }

    [Fact]
    public void MultiTrackExecution_RejectionDoesNotCommitAnEarlierAcceptedTrack()
    {
        var policy = new RejectSecondApplyPolicyService(PersistentPolicy(), Defense);
        RuntimeActorState actor = Actor("actor");
        SkillDefinition skill = ActiveSkill(
            "atomic_focus",
            [new ModifyStatStageEffectDefinition([Attack, Defense], 1)]);

        SkillExecutionResult result = new SkillExecutor(Services(policy)).Execute(Request(skill, actor));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        EffectExecutionResult effect = Assert.Single(result.Effects);
        Assert.Equal(EffectExecutionOutcome.Failure, effect.Outcome);
        Assert.Equal(2, policy.ApplyCalls);
        Assert.Null(actor.StatModifierState);
        Assert.Empty(actor.StatStages);
    }

    [Fact]
    public void TimedExclusive_WeakerSkillIsRejectedBeforeItsCostCanCommit()
    {
        IStatModifierPolicyService policy = TimedExclusivePolicy();
        RuntimeActorState actor = Actor("actor", sp: 20);
        var executor = new SkillExecutor(Services(policy));
        SkillDefinition strong = ActiveSkill(
            "strong_focus",
            [new ModifyStatStageEffectDefinition([Attack], 2, Turns(3))]);
        SkillDefinition weak = ActiveSkill(
            "weak_focus",
            [new ModifyStatStageEffectDefinition([Attack], 1, Turns(3))],
            [new SkillCostDefinition(Sp, new FlatAmountDefinition(5))]);

        SkillExecutionResult applied = executor.Execute(Request(
            strong,
            actor,
            new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)));
        decimal beforeSp = actor.GetRequiredResource(Sp).Current;
        SkillExecutionRequest weakRequest = Request(
            weak,
            actor,
            new StatModifierLifecycleBoundary(OwnerTurnEnd, 2));
        SkillExecutionAssessment assessment = executor.Assess(weakRequest);
        SkillExecutionResult rejected = executor.Execute(weakRequest, assessment);

        Assert.Equal(SkillExecutionStatus.Executed, applied.Status);
        Assert.False(assessment.CanExecute);
        Assert.Contains(assessment.Diagnostics, value =>
            value.Code == SkillExecutionDiagnosticCode.NoApplicableEffect);
        Assert.Equal(SkillExecutionStatus.Rejected, rejected.Status);
        Assert.Equal(beforeSp, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(2, actor.StatStages[Attack].Stage);
    }

    [Fact]
    public void TimedExclusive_EqualItemRefreshIsMeaningfulAndRequestsConsumption()
    {
        IStatModifierPolicyService policy = TimedExclusivePolicy();
        RuntimeActorState actor = Actor("actor");
        var skillExecutor = new SkillExecutor(Services(policy));
        SkillDefinition initial = ActiveSkill(
            "initial_focus",
            [new ModifyStatStageEffectDefinition([Attack], 1, Turns(2))]);
        skillExecutor.Execute(Request(
            initial,
            actor,
            new StatModifierLifecycleBoundary(OwnerTurnEnd, 1)));
        ItemDefinition item = Consumable(
            "focus_tonic",
            new ModifyStatStageEffectDefinition([Attack], 1, Turns(3)));
        var itemExecutor = new ItemExecutor(Services(policy));
        var request = new ItemExecutionRequest(
            item,
            actor,
            [actor],
            Environment(new StatModifierLifecycleBoundary(OwnerTurnEnd, 2)),
            [actor.InstanceId]);

        ItemExecutionAssessment assessment = itemExecutor.Assess(request);
        ItemExecutionResult result = itemExecutor.Execute(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(ItemExecutionStatus.Executed, result.Status);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.Consumption);
        Assert.Equal(1, actor.StatStages[Attack].Stage);
        RuntimeStatModifierContributionSnapshot contribution = Assert.Single(
            Assert.Single(actor.StatModifierState!.Tracks).Contributions);
        Assert.Equal(Turns(3), contribution.Duration);
        Assert.Equal(2, contribution.LastLifecycleBoundary?.Sequence);
        Assert.Contains(
            Assert.Single(result.Effects).StatModifierTransitions.SelectMany(value => value.Events),
            value => value.Kind == StatModifierEventKind.ContributionUpdated);
    }

    [Fact]
    public void PassiveApplication_UsesItsBoundaryAndExpiresOnTheNextOwnerTurn()
    {
        IStatModifierPolicyService policy = TimedContributionPolicy();
        SkillDefinition passive = PassiveSkill(
            "opening_focus",
            ContentId.Parse("battle_start"),
            new ModifyStatStageEffectDefinition([Attack], 1, Turns(1)));
        RuntimeActorState actor = Actor("actor", passiveSkills: [passive]);
        BattleExecutionServices services = Services(policy);
        StatModifierLifecycleBoundary appliedAt = new(OwnerTurnEnd, 1);

        PassiveTriggerDispatchResult dispatch = services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                ContentId.Parse("battle_start"),
                actor,
                [actor],
                [actor],
                Battle,
                NormalBattle,
                moonPhaseId: null,
                [appliedAt]),
            services);
        var lifecycle = new BattleStatusLifecycleService(new MinimumRandomSource());
        BattleTurnEndLifecycleResult sameBoundary = lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                actor,
                [actor],
                Battle,
                OwnerTurnEnd,
                NormalBattle,
                statModifierBoundary: appliedAt),
            services);
        int stageAfterSameBoundary = actor.StatStages[Attack].Stage;
        BattleTurnEndLifecycleResult nextBoundary = lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                actor,
                [actor],
                Battle,
                OwnerTurnEnd,
                NormalBattle,
                statModifierBoundary: new StatModifierLifecycleBoundary(OwnerTurnEnd, 2)),
            services);

        Assert.Equal(PassiveTriggerOutcome.Executed, Assert.Single(dispatch.Activations).Outcome);
        Assert.Equal(1, stageAfterSameBoundary);
        Assert.DoesNotContain(sameBoundary.Events, value => value.ModifierEvent is not null);
        Assert.Empty(actor.StatStages);
        Assert.Contains(nextBoundary.Events, value =>
            value.ModifierEvent?.Kind == StatModifierEventKind.ContributionExpired);
    }

    [Fact]
    public void StatusRemovalAndCleanup_UseTheSelectedPolicyAuthority()
    {
        IStatModifierPolicyService policy = PersistentPolicy();
        BattleExecutionServices services = Services(policy);
        var executor = new SkillExecutor(services);
        RuntimeActorState actor = Actor("actor");
        executor.Execute(Request(ActiveSkill(
            "mixed_stages",
            [
                new ModifyStatStageEffectDefinition([Attack], 2),
                new ModifyStatStageEffectDefinition([Defense], -1)
            ]), actor));

        SkillExecutionResult removePositive = executor.Execute(Request(ActiveSkill(
            "clear_positive",
            [new RemoveStatusEffectDefinition([StatusEffectKind.Buff], [])]), actor));

        Assert.DoesNotContain(Attack, actor.StatStages.Keys);
        Assert.Equal(-1, actor.StatStages[Defense].Stage);
        Assert.Contains(
            Assert.Single(removePositive.Effects).StatModifierTransitions.SelectMany(value => value.Events),
            value => value.Kind == StatModifierEventKind.ContributionRemoved);

        var lifecycle = new BattleStatusLifecycleService(new MinimumRandomSource());
        lifecycle.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.DeploymentSwap),
            policy);
        Assert.Equal(-1, actor.StatStages[Defense].Stage);
        lifecycle.Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusDepartureReason.BattleEnd),
            policy);
        Assert.Empty(actor.StatStages);
    }

    [Fact]
    public void PassiveDispatchRequest_RejectsInvalidOrDuplicateModifierBoundaries()
    {
        RuntimeActorState actor = Actor("actor");
        StatModifierLifecycleBoundary boundary = new(OwnerTurnEnd, 1);

        Assert.Throws<ArgumentException>(() => new PassiveTriggerDispatchRequest(
            ContentId.Parse("battle_start"),
            actor,
            [actor],
            [actor],
            Battle,
            NormalBattle,
            moonPhaseId: null,
            [boundary, boundary]));
    }

    private static SkillExecutionRequest Request(
        SkillDefinition skill,
        RuntimeActorState actor,
        StatModifierLifecycleBoundary? boundary = null) =>
        new(
            skill,
            actor,
            [actor],
            Environment(boundary),
            [actor.InstanceId]);

    private static EffectExecutionEnvironment Environment(
        StatModifierLifecycleBoundary? boundary = null) =>
        new(
            Battle,
            NormalBattle,
            activeStatModifierBoundaries: boundary is null ? [] : [boundary]);

    private static SkillDefinition ActiveSkill(
        string id,
        IEnumerable<EffectDefinition> effects,
        IEnumerable<SkillCostDefinition>? costs = null,
        TargetingDefinition? targeting = null) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            SkillActivation.Active,
            SkillMenuGroup.Buff,
            InheritanceGroup.Support,
            new SkillInheritanceDefinition(true),
            costs: costs,
            targeting: targeting ?? SelfTargeting(),
            effects: effects,
            availability: new SkillAvailabilityDefinition([Battle]));

    private static SkillDefinition PassiveSkill(
        string id,
        ContentId eventId,
        EffectDefinition effect) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers: [new PassiveTriggerDefinition(eventId, [effect])]);

    private static ItemDefinition Consumable(string id, EffectDefinition effect) =>
        new(
            ContentId.Parse(id),
            id,
            id,
            ItemKind.Consumable,
            99,
            1,
            new ItemUsageDefinition([Battle], SelfTargeting(), [effect]));

    private static TargetingDefinition SelfTargeting() =>
        new(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Alive, true);

    private static RuntimeActorState Actor(
        string id,
        decimal sp = 20,
        IEnumerable<SkillDefinition>? passiveSkills = null,
        IEnumerable<ContentId>? skillIds = null,
        bool isDeployed = true,
        ContentId? teamId = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            teamId ?? PlayerTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, 100, 100), new BattleResourceState(Sp, sp, 100)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: isDeployed),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), teamId ?? PlayerTeam),
            skillIds: skillIds,
            passiveSkills: passiveSkills);

    private static BattleEncounterTurnLifecycleRequest TurnRequest(
        BattleEncounterRequest encounter,
        BattleEncounterParticipant actor,
        IReadOnlyList<BattleEncounterParticipant> participants) =>
        new(encounter, actor, participants, CanRecallToRoster: false);

    private static void ApplyModifier(
        RuntimeActorState actor,
        IStatModifierPolicyService policy,
        TurnDurationDefinition duration,
        StatModifierLifecycleBoundary? activeBoundary = null)
    {
        RuntimeStatModifierStateSnapshot before = actor.ResolveStatModifierState(policy);
        StatModifierTransitionResult result = policy.Apply(
            new StatModifierApplicationRequest(
                before,
                Attack,
                stageDelta: 1,
                duration,
                actor.IsDeployed,
                activeBoundary));
        Assert.True(result.Accepted, string.Join("; ", result.Diagnostics.Select(value => value.Message)));
        actor.ReplaceStatModifierState(policy, result.After);
    }

    private static int RemainingStatusDuration(RuntimeActorState actor, ContentId statusId) =>
        Assert.IsType<TurnDurationDefinition>(
            actor.ToSnapshot().BattleStatus.Statuses.Single(status => status.Id == statusId).Duration).Value;

    private static BattleActionExecutor ActionExecutor(
        IStatModifierPolicyService statModifiers,
        IEnumerable<SkillDefinition>? skills = null,
        IEnumerable<ItemDefinition>? items = null)
    {
        BattleExecutionServices services = Services(statModifiers);
        return new BattleActionExecutor(
            new SkillExecutor(services),
            new ItemExecutor(services),
            services,
            new CatalogBattleActionAuthorizationPolicy(
                new SkillRepository(skills),
                new ItemRepository(items),
                NoBattleBasicAttackProfileSource.Instance));
    }

    private static BattleStatusLifecycleResult ProcessBoundary(
        BattleStatusLifecycleService lifecycle,
        BattleExecutionServices services,
        RuntimeActorState actor,
        ContentId tickEvent,
        StatModifierLifecycleBoundary boundary,
        bool usePhaseBoundary)
    {
        if (usePhaseBoundary)
        {
            return lifecycle.ProcessClock(
                new BattleLifecycleClockRequest(
                    [actor],
                    new TeamPhaseLifecycleClockBoundary(
                        tickEvent,
                        PlayerTeam,
                        PlayerPhase,
                        boundary.Sequence),
                    [boundary]),
                services.StatModifiers);
        }

        BattleTurnEndLifecycleResult result = lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                actor,
                [actor],
                Battle,
                tickEvent,
                NormalBattle,
                statModifierBoundary: boundary),
            services);
        return new BattleStatusLifecycleResult(result.Events);
    }

    private static BattleExecutionServices Services(IStatModifierPolicyService statModifiers)
    {
        var ruleset = new ProductionCombatRuleset(new MinimumRandomSource());
        return new BattleExecutionServices(
            EmptyAilments.Instance,
            ruleset,
            ruleset,
            ruleset,
            ruleset,
            ruleset,
            new FirstSkillTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            statModifiers,
            new SplitChargePolicy());
    }

    private static IStatModifierPolicyService PersistentPolicy() =>
        new StatModifierPolicyService(new PersistentStagedStatModifierPolicy(
            ContentId.Parse("integration_persistent")));

    private static IStatModifierPolicyService TimedExclusivePolicy() =>
        new StatModifierPolicyService(new TimedExclusiveStatModifierPolicy(
            ContentId.Parse("integration_timed_exclusive")));

    private static IStatModifierPolicyService TimedContributionPolicy() =>
        new StatModifierPolicyService(new TimedContributionStatModifierPolicy(
            ContentId.Parse("integration_timed_contribution")));

    private static IStatModifierPolicyService Policy(SuppliedPolicyKind kind) => kind switch
    {
        SuppliedPolicyKind.PersistentStaged => PersistentPolicy(),
        SuppliedPolicyKind.TimedExclusive => TimedExclusivePolicy(),
        SuppliedPolicyKind.TimedContribution => TimedContributionPolicy(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static TurnDurationDefinition Turns(int value) =>
        new(value, OwnerTurnEnd, SuspendWhileReserve: true);

    private sealed class EmptyAilments : IAilmentDefinitionRepository
    {
        internal static EmptyAilments Instance { get; } = new();

        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            throw new KeyNotFoundException(id.ToString());
    }

    private sealed class TimedModifierTurnHandler(
        BattleActionExecutor executor,
        SkillDefinition skill) : IBattleEncounterTurnHandler
    {
        private int _turn;

        internal List<IReadOnlyList<StatModifierLifecycleBoundary>> Boundaries { get; } = [];
        internal int? DurationBeforeSecondAction { get; private set; }

        public async ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            Boundaries.Add(request.ActiveStatModifierBoundaries);
            _turn++;
            if (_turn > 1)
            {
                DurationBeforeSecondAction = RemainingDuration(request.Actor.State);
                return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
            }

            var executionRequest = new BattleActionExecutionRequest(
                new SkillBattleActionCommand(skill, [request.Actor.InstanceId]),
                request.Actor.State,
                request.Participants.Select(participant => participant.State),
                new EffectExecutionEnvironment(
                    request.Encounter.ContextId,
                    request.Encounter.BattleKindId,
                    request.Encounter.MoonPhaseId,
                    request.ActiveStatModifierBoundaries));
            BattleActionExecutionResult execution = await executor.ExecuteAsync(
                executionRequest,
                cancellationToken: cancellationToken);
            Assert.Equal(BattleActionExecutionStatus.Executed, execution.Status);
            return BattleEncounterCommandResult.Executed(execution.TurnConsumption);
        }
    }

    private sealed class CaptureTimedModifierCompletion : IBattleEncounterCompletionPolicy
    {
        internal List<int> RemainingDurations { get; } = [];

        public BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request)
        {
            if (request.LastActor is null)
            {
                return new BattleEncounterCompletion(false);
            }

            RemainingDurations.Add(RemainingDuration(request.LastActor.State));
            return RemainingDurations.Count == 2
                ? new BattleEncounterCompletion(true, BattleEncounterOutcome.Draw)
                : new BattleEncounterCompletion(false);
        }
    }

    private static int RemainingDuration(RuntimeActorState actor) =>
        Assert.IsType<TurnDurationDefinition>(
            Assert.Single(
                Assert.Single(actor.StatModifierState!.Tracks)
                    .Contributions).Duration).Value;

    private static StatModifierLifecycleBoundary? LastModifierBoundary(RuntimeActorState actor) =>
        Assert.Single(
            Assert.Single(actor.StatModifierState!.Tracks)
                .Contributions).LastLifecycleBoundary;

    private static int RemainingDuration(RuntimeActorSnapshot actor) =>
        Assert.IsType<TurnDurationDefinition>(
            Assert.Single(
                Assert.Single(actor.BattleStatus.StatModifiers!.Tracks)
                    .Contributions).Duration).Value;

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class CancellingLifecycleService(
        IBattleStatusLifecycleService inner,
        CancellationTokenSource cancellation,
        LifecycleCancellationPoint cancellationPoint) : IBattleStatusLifecycleService
    {
        public BattleTurnStartLifecycleResult ProcessTurnStart(
            BattleTurnStartLifecycleRequest request) =>
            inner.ProcessTurnStart(request);

        public BattleTurnEndLifecycleResult ProcessTurnEnd(
            BattleTurnEndLifecycleRequest request,
            BattleExecutionServices services)
        {
            BattleTurnEndLifecycleResult result = inner.ProcessTurnEnd(request, services);
            CancelAt(LifecycleCancellationPoint.TurnEnd);
            return result;
        }

        public BattleAilmentApplicationResult TryApplyAilment(
            BattleAilmentApplicationRequest request,
            BattleExecutionServices services) =>
            inner.TryApplyAilment(request, services);

        public BattleStatusLifecycleResult ApplyStatStage(
            RuntimeActorState target,
            ContentId modifierTrackId,
            int delta,
            BattleExecutionServices services,
            DurationDefinition? duration = null,
            StatModifierLifecycleBoundary? activeBoundary = null) =>
            inner.ApplyStatStage(
                target,
                modifierTrackId,
                delta,
                services,
                duration,
                activeBoundary);

        public BattleStatusLifecycleResult ProcessClock(
            BattleLifecycleClockRequest request,
            IStatModifierPolicyService statModifiers)
        {
            BattleStatusLifecycleResult result = inner.ProcessClock(request, statModifiers);
            CancelAt(LifecycleCancellationPoint.Clock);
            return result;
        }

        public BattleStatusLifecycleResult ProcessActionEnd(
            BattleActionEndLifecycleRequest request,
            IStatModifierPolicyService statModifiers) =>
            inner.ProcessActionEnd(request, statModifiers);

        public BattleStatusLifecycleResult Cleanup(
            BattleStatusCleanupRequest request,
            IStatModifierPolicyService statModifiers)
        {
            BattleStatusLifecycleResult result = inner.Cleanup(request, statModifiers);
            CancelAt(LifecycleCancellationPoint.Cleanup);
            return result;
        }

        private void CancelAt(LifecycleCancellationPoint currentPoint)
        {
            if (cancellationPoint == currentPoint)
            {
                cancellation.Cancel();
            }
        }
    }

    private enum LifecycleCancellationPoint
    {
        TurnEnd,
        Clock,
        Cleanup
    }

    private sealed class SkillRepository : ISkillDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _skills;

        internal SkillRepository(IEnumerable<SkillDefinition>? skills)
        {
            _skills = (skills ?? []).ToDictionary(skill => skill.Id);
        }

        public bool TryGetSkill(ContentId id, out SkillDefinition? definition) =>
            _skills.TryGetValue(id, out definition);

        public SkillDefinition GetRequiredSkill(ContentId id) =>
            _skills.TryGetValue(id, out SkillDefinition? definition)
                ? definition
                : throw new KeyNotFoundException(id.ToString());
    }

    private sealed class ItemRepository : IItemDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, ItemDefinition> _items;

        internal ItemRepository(IEnumerable<ItemDefinition>? items)
        {
            _items = (items ?? []).ToDictionary(item => item.Id);
        }

        public bool TryGetItem(ContentId id, out ItemDefinition? definition) =>
            _items.TryGetValue(id, out definition);

        public ItemDefinition GetRequiredItem(ContentId id) =>
            _items.TryGetValue(id, out ItemDefinition? definition)
                ? definition
                : throw new KeyNotFoundException(id.ToString());
    }

    private sealed class TestItemInventory : IItemActionInventory
    {
        private readonly ContentId _itemId;
        private readonly bool _rejectCommit;

        internal TestItemInventory(ContentId itemId, int quantity, bool rejectCommit = false)
        {
            _itemId = itemId;
            Quantity = quantity;
            _rejectCommit = rejectCommit;
        }

        internal int Quantity { get; private set; }

        public bool HasAvailable(ContentId itemId, int quantity) =>
            itemId == _itemId && quantity > 0 && Quantity >= quantity;

        public IItemActionReservation Reserve(ContentId itemId, int quantity)
        {
            if (!HasAvailable(itemId, quantity))
            {
                throw new InvalidOperationException("The requested item quantity is unavailable.");
            }

            Quantity -= quantity;
            return new Reservation(this, itemId, quantity, _rejectCommit);
        }

        private sealed class Reservation(
            TestItemInventory inventory,
            ContentId itemId,
            int quantity,
            bool rejectCommit) : IItemActionReservation
        {
            public ContentId ItemId { get; } = itemId;
            public int Quantity { get; } = quantity;
            public bool IsCommitted { get; private set; }
            public bool IsRolledBack { get; private set; }

            public ItemActionReservationTransitionResult Commit()
            {
                if (rejectCommit)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "Deliberate inventory commit rejection.");
                }

                IsCommitted = true;
                return ItemActionReservationTransitionResult.Success;
            }

            public ItemActionReservationTransitionResult Rollback()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "The reservation is no longer live.");
                }

                inventory.Quantity += Quantity;
                IsRolledBack = true;
                return ItemActionReservationTransitionResult.Success;
            }
        }
    }

    private sealed class FirstSkillTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            Array.AsReadOnly(candidates.Take(count.Minimum).ToArray());
    }

    private class RecordingStatModifierPolicyService : IStatModifierPolicyService
    {
        protected readonly IStatModifierPolicyService Inner;

        internal RecordingStatModifierPolicyService(IStatModifierPolicyService inner)
        {
            Inner = inner;
        }

        internal int AssessCalls { get; private set; }
        internal int ApplyCalls { get; private set; }
        public ContentId PolicyId => Inner.PolicyId;
        public StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state) =>
            Inner.ValidateState(state);

        public StatModifierTransitionResult AssessApplication(StatModifierApplicationRequest request)
        {
            AssessCalls++;
            return Inner.AssessApplication(request);
        }

        public virtual StatModifierTransitionResult Apply(StatModifierApplicationRequest request)
        {
            ApplyCalls++;
            return Inner.Apply(request);
        }

        public StatModifierTransitionResult Tick(StatModifierTickRequest request) => Inner.Tick(request);
        public StatModifierTransitionResult Remove(StatModifierRemovalRequest request) => Inner.Remove(request);
        public StatModifierTransitionResult Cleanup(StatModifierCleanupRequest request) => Inner.Cleanup(request);
    }

    private sealed class RejectSecondApplyPolicyService : RecordingStatModifierPolicyService
    {
        private readonly ContentId _rejectedTrack;

        internal RejectSecondApplyPolicyService(
            IStatModifierPolicyService inner,
            ContentId rejectedTrack)
            : base(inner)
        {
            _rejectedTrack = rejectedTrack;
        }

        public override StatModifierTransitionResult Apply(StatModifierApplicationRequest request)
        {
            if (request.ModifierTrackId != _rejectedTrack)
            {
                return base.Apply(request);
            }

            _ = base.Apply(request);
            return Inner.Apply(new StatModifierApplicationRequest(
                request.State,
                request.ModifierTrackId,
                stageDelta: 0,
                request.Duration,
                request.IsActorDeployed,
                request.ActiveLifecycleBoundary));
        }
    }

    public enum SuppliedPolicyKind
    {
        PersistentStaged,
        TimedExclusive,
        TimedContribution
    }
}
