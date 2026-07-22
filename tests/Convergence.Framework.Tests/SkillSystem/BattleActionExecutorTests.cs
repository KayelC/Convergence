using Convergence.Content;
using Convergence.Catalog;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class BattleActionExecutorTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId TeamA = Id("team_a");
    private static readonly ContentId TeamB = Id("team_b");
    private static readonly ContentId Hp = StandardProgressionIds.Hp;
    private static readonly ContentId Sp = StandardProgressionIds.Sp;

    [Fact]
    public async Task GuardAndPass_ReturnTypedTurnConsumptionWithoutPresentationCoupling()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);

        BattleActionExecutionResult guard = await Execute(executor, new GuardBattleActionCommand(), actor, [actor]);
        BattleActionExecutionResult pass = await Execute(executor, new PassBattleActionCommand(), actor, [actor]);

        Assert.Equal(BattleActionExecutionStatus.Executed, guard.Status);
        Assert.True(actor.IsGuarding);
        Assert.Equal(ActionTurnConsumptionKind.Normal, guard.TurnConsumption.Kind);
        Assert.Equal(ActionTurnConsumptionKind.Pass, pass.TurnConsumption.Kind);
        Assert.DoesNotContain(
            guard.Events.Concat(pass.Events),
            battleEvent => battleEvent.Message.Contains("Console", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BasicAttack_ExecutesTypedDamageAndReturnsTurnEconomyOutcome()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor(
            "target",
            TeamB,
            hp: 40,
            defense: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Physical, ElementalAffinity.Weak)]));
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false),
            SingleEnemy(),
            [target.InstanceId]);

        BattleActionExecutionResult result = await Execute(executor, command, actor, [actor, target]);

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(30, target.GetRequiredResource(Hp).Current);
        Assert.Equal(ActionTurnConsumptionKind.TurnEconomy, result.TurnConsumption.Kind);
        Assert.Equal(TurnEconomyOutcome.Weakness, result.TurnConsumption.TurnEconomy!.Outcome);
        Assert.Equal(ElementalAffinity.Weak, Assert.Single(result.Effects).ResolvedAffinity);
    }

    [Fact]
    public async Task BasicAttack_UsesTheInjectedActionOutcomeAggregationPolicy()
    {
        var policy = new RecordingActionOutcomePolicy(
            new TurnEconomyResolution(TurnEconomyOutcome.Absorb, false, true));
        BattleActionExecutor executor = Executor(actionOutcomes: policy);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(
                DamageElement.Physical,
                15,
                100,
                new NeverCriticalDefinition(),
                false),
            SingleEnemy(),
            [target.InstanceId]);

        BattleActionExecutionResult result = await Execute(executor, command, actor, [actor, target]);

        Assert.Equal(1, policy.CallCount);
        Assert.Equal(TurnEconomyOutcome.Absorb, result.TurnConsumption.TurnEconomy!.Outcome);
        Assert.True(result.TurnConsumption.TurnEconomy.TerminatesPhase);
    }

    [Fact]
    public async Task BasicAttack_ForwardsAuthoredAccuracyAndCriticalDefinitionToDamageResolution()
    {
        var damage = new RecordingDamagePolicy();
        BattleActionExecutor executor = Executor(damagePolicy: damage);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(
                DamageElement.Physical,
                15,
                37,
                new ChanceCriticalDefinition(23),
                false),
            SingleEnemy(),
            [target.InstanceId]);

        BattleActionExecutionResult result = await Execute(executor, command, actor, [actor, target]);

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        DamageEffectDefinition effect = Assert.Single(damage.Requests).Effect;
        Assert.Equal(37, effect.Accuracy);
        Assert.Equal(23, Assert.IsType<ChanceCriticalDefinition>(effect.Critical).Chance);
    }

    [Fact]
    public async Task BasicAttack_FireOnlyProfileUsesFireAsItsPrimaryDamage()
    {
        var damage = new RecordingDamagePolicy();
        BattleActionExecutor executor = Executor(damagePolicy: damage);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor(
            "target",
            TeamB,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]));
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(
                DamageElement.Fire,
                15,
                100,
                new NeverCriticalDefinition(),
                false),
            SingleEnemy(),
            [target.InstanceId]);

        BattleActionExecutionResult result = await Execute(executor, command, actor, [actor, target]);

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(DamageElement.Fire, Assert.Single(damage.Requests).Effect.Element);
        Assert.Equal(TurnEconomyOutcome.Weakness, result.TurnConsumption.TurnEconomy!.Outcome);
    }

    [Fact]
    public async Task BasicAttack_PhysicalContactCanApplyAHandledAilmentRider()
    {
        ContentId burnId = Id("burn");
        var burn = new AilmentDefinition(
            burnId,
            "Burn",
            "Test ailment.",
            new BattleDurationDefinition(),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition());
        BattleActionExecutor executor = Executor(
            ailments: new TestAilmentRepository(burn),
            ailmentPolicy: new AlwaysAilmentPolicy());
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        EffectLocalId primaryId = EffectLocalId.Parse("weapon_contact");
        var basicAttack = new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            15,
            100,
            new NeverCriticalDefinition(),
            false)
        {
            PrimaryEffectId = primaryId,
            SecondaryEffects =
            [
                new ApplyAilmentEffectDefinition(burnId, 100)
                {
                    Dependency = new EffectDependencyDefinition(
                        primaryId,
                        EffectDependencyRequirement.PositiveDamage,
                        EffectDependencyScope.SameTarget)
                }
            ]
        };

        BattleActionExecutionResult result = await Execute(
            executor,
            new BasicAttackBattleActionCommand(basicAttack, SingleEnemy(), [target.InstanceId]),
            actor,
            [actor, target]);

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(2, result.Effects.Count);
        Assert.True(target.HasAilment(burnId));
        Assert.True(result.Effects[1].DependencyEvaluation!.Satisfied);
    }

    [Fact]
    public async Task BasicAttack_PhysicalAndFireComponentsShareOrderedEffectExecution()
    {
        var damage = new RecordingDamagePolicy();
        BattleActionExecutor executor = Executor(damagePolicy: damage);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor(
            "target",
            TeamB,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]));
        EffectLocalId primaryId = EffectLocalId.Parse("weapon_contact");
        var basicAttack = new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            15,
            100,
            new NeverCriticalDefinition(),
            false)
        {
            PrimaryEffectId = primaryId,
            SecondaryEffects =
            [
                new DamageEffectDefinition(
                    DamageElement.Fire,
                    5,
                    20,
                    new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1))
                {
                    ContactMode = DamageContactMode.SharedContact,
                    Dependency = new EffectDependencyDefinition(
                        primaryId,
                        EffectDependencyRequirement.PositiveDamage,
                        EffectDependencyScope.SameTarget)
                }
            ]
        };

        BattleActionExecutionResult result = await Execute(
            executor,
            new BasicAttackBattleActionCommand(basicAttack, SingleEnemy(), [target.InstanceId]),
            actor,
            [actor, target]);

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal([DamageElement.Physical, DamageElement.Fire],
            damage.Requests.Select(request => request.Effect.Element));
        Assert.Equal(2, result.Effects.Count);
        DamageHitExecutionEvidence fire = Assert.Single(result.Effects[1].DamageHits);
        Assert.Equal(DamageContactMode.SharedContact, fire.ContactMode);
        Assert.Equal(primaryId, fire.ContactSourceEffectId);
        Assert.Equal(TurnEconomyOutcome.Weakness, result.TurnConsumption.TurnEconomy!.Outcome);
    }

    [Fact]
    public async Task SkillAction_SharesAssessmentWithExecutionAndCommitsCosts()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA, sp: 10);
        RuntimeActorState target = Actor("target", TeamB);
        SkillDefinition skill = ActiveSkill(
            "frost",
            [new SkillCostDefinition(Sp, new FlatAmountDefinition(3))],
            [new DamageEffectDefinition(DamageElement.Ice, 7, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1))]);
        var command = new SkillBattleActionCommand(skill, [target.InstanceId]);
        var request = Request(command, actor, [actor, target]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(7, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.TurnEconomy, result.TurnConsumption.Kind);
    }

    [Fact]
    public async Task CatalogAuthorization_AllowsCanonicalEquippedSkill()
    {
        SkillDefinition skill = ActiveSkill(
            "frost",
            [],
            [new DamageEffectDefinition(
                DamageElement.Ice,
                7,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))]);
        RuntimeActorState actor = Actor("actor", TeamA, skillIds: [skill.Id]);
        RuntimeActorState target = Actor("target", TeamB);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([skill]),
            new TestItemRepository([]),
            NoBattleBasicAttackProfileSource.Instance);
        BattleActionExecutor executor = Executor(authorization: authorization);

        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            new SkillBattleActionCommand(skill, [target.InstanceId]),
            actor,
            [actor, target]));

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(90, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task CatalogAuthorization_RejectsSkillThatIsNotEquipped()
    {
        SkillDefinition skill = ActiveSkill(
            "frost",
            [],
            [new DamageEffectDefinition(
                DamageElement.Ice,
                7,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))]);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([skill]),
            new TestItemRepository([]),
            NoBattleBasicAttackProfileSource.Instance);
        BattleActionExecutor executor = Executor(authorization: authorization);

        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            new SkillBattleActionCommand(skill, [target.InstanceId]),
            actor,
            [actor, target]));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.ActionNotAuthorized, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task CatalogAuthorization_RejectsSubstitutedSkillDefinitionWithEquippedId()
    {
        SkillDefinition canonical = ActiveSkill(
            "frost",
            [],
            [new DamageEffectDefinition(
                DamageElement.Ice,
                7,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))]);
        SkillDefinition substituted = ActiveSkill(
            "frost",
            [],
            [new DamageEffectDefinition(
                DamageElement.Ice,
                999,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))]);
        RuntimeActorState actor = Actor("actor", TeamA, skillIds: [canonical.Id]);
        RuntimeActorState target = Actor("target", TeamB);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([canonical]),
            new TestItemRepository([]),
            NoBattleBasicAttackProfileSource.Instance);
        BattleActionAuthorizationResult direct = authorization.Authorize(
            actor,
            new SkillBattleActionCommand(substituted, [target.InstanceId]));
        BattleActionExecutor executor = Executor(authorization: authorization);

        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            new SkillBattleActionCommand(substituted, [target.InstanceId]),
            actor,
            [actor, target]));

        Assert.Equal(
            BattleActionAuthorizationDiagnosticCode.SkillDefinitionSubstituted,
            Assert.Single(direct.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.ActionNotAuthorized, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task CatalogAuthorization_RevalidatesEquippedSkillBeforeExecution()
    {
        SkillDefinition skill = ActiveSkill(
            "frost",
            [],
            [new DamageEffectDefinition(
                DamageElement.Ice,
                7,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))]);
        RuntimeActorState actor = Actor("actor", TeamA, skillIds: [skill.Id]);
        RuntimeActorState target = Actor("target", TeamB);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([skill]),
            new TestItemRepository([]),
            NoBattleBasicAttackProfileSource.Instance);
        BattleActionExecutor executor = Executor(authorization: authorization);
        BattleActionExecutionRequest request = Request(
            new SkillBattleActionCommand(skill, [target.InstanceId]),
            actor,
            [actor, target]);
        BattleActionAssessment assessment = executor.Assess(request);
        actor.ApplySkillState(new RuntimeSkillStateSnapshot(), []);

        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.ActionNotAuthorized, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task CatalogAuthorization_AllowsCanonicalOwnedItem()
    {
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([medicine]),
            NoBattleBasicAttackProfileSource.Instance);
        BattleActionExecutor executor = Executor(authorization: authorization);

        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            new ItemBattleActionCommand(medicine, [target.InstanceId]),
            actor,
            [actor, target],
            inventory));

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(40, target.GetRequiredResource(Hp).Current);
        Assert.Equal(0, inventory.Quantity);
    }

    [Fact]
    public async Task CatalogAuthorization_RejectsItemMissingFromCatalog()
    {
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([]),
            NoBattleBasicAttackProfileSource.Instance);
        var command = new ItemBattleActionCommand(medicine, [target.InstanceId]);

        BattleActionAuthorizationResult direct = authorization.Authorize(actor, command);
        BattleActionExecutionResult result = await Executor(authorization: authorization).ExecuteAsync(
            Request(command, actor, [actor, target], inventory));

        Assert.Equal(
            BattleActionAuthorizationDiagnosticCode.ItemDefinitionMissing,
            Assert.Single(direct.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public async Task CatalogAuthorization_RejectsSubstitutedItemDefinitionWithOwnedId()
    {
        ItemDefinition canonical = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        ItemDefinition substituted = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(999)));
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        var inventory = new TestItemInventory(canonical.Id, quantity: 1);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([canonical]),
            NoBattleBasicAttackProfileSource.Instance);
        var command = new ItemBattleActionCommand(substituted, [target.InstanceId]);

        BattleActionAuthorizationResult direct = authorization.Authorize(actor, command);
        BattleActionExecutionResult result = await Executor(authorization: authorization).ExecuteAsync(
            Request(command, actor, [actor, target], inventory));

        Assert.Equal(
            BattleActionAuthorizationDiagnosticCode.ItemDefinitionSubstituted,
            Assert.Single(direct.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public async Task CatalogAuthorization_RevalidatesCanonicalItemBeforeExecution()
    {
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        var repository = new MutableItemRepository(medicine);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            repository,
            NoBattleBasicAttackProfileSource.Instance);
        var command = new ItemBattleActionCommand(medicine, [target.InstanceId]);
        BattleActionExecutionRequest request = Request(command, actor, [actor, target], inventory);
        BattleActionExecutor executor = Executor(authorization: authorization);
        BattleActionAssessment assessment = executor.Assess(request);
        repository.Item = null;

        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public async Task CatalogAuthorization_AllowsExplicitNaturalBasicAttackProfile()
    {
        var basicAttack = new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false);
        TargetingDefinition targeting = SingleEnemy();
        var profile = new BattleBasicAttackProfile(Id("natural_attack"), basicAttack, targeting);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([]),
            new FixedBasicAttackProfileSource(profile));
        BattleActionExecutor executor = Executor(authorization: authorization);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);

        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            new BasicAttackBattleActionCommand(
                basicAttack,
                targeting,
                [target.InstanceId],
                profile.ActionId),
            actor,
            [actor, target]));

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(90, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task CatalogAuthorization_RejectsSubstitutedBasicAttackDefinition()
    {
        var canonical = new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false);
        TargetingDefinition targeting = SingleEnemy();
        var profile = new BattleBasicAttackProfile(Id("natural_attack"), canonical, targeting);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([]),
            new FixedBasicAttackProfileSource(profile));
        BattleActionExecutor executor = Executor(authorization: authorization);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var substituted = new EquipmentBasicAttackDefinition(DamageElement.Physical, 999, 100, new NeverCriticalDefinition(), false);
        var command = new BasicAttackBattleActionCommand(
            substituted,
            targeting,
            [target.InstanceId],
            profile.ActionId);

        BattleActionAuthorizationResult direct = authorization.Authorize(actor, command);
        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            command,
            actor,
            [actor, target]));

        Assert.Equal(
            BattleActionAuthorizationDiagnosticCode.BasicAttackDefinitionMismatch,
            Assert.Single(direct.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void CatalogAuthorization_RejectsSubstitutedBasicAttackSecondaryEffects()
    {
        var canonical = new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            15,
            100,
            new NeverCriticalDefinition(),
            false);
        var substituted = canonical with
        {
            SecondaryEffects = [new AnalyzeEffectDefinition([AnalysisLayer.Stats])]
        };
        TargetingDefinition targeting = SingleEnemy();
        var profile = new BattleBasicAttackProfile(Id("natural_attack"), canonical, targeting);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([]),
            new FixedBasicAttackProfileSource(profile));
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);

        BattleActionAuthorizationResult result = authorization.Authorize(
            actor,
            new BasicAttackBattleActionCommand(
                substituted,
                targeting,
                [target.InstanceId],
                profile.ActionId));

        Assert.Equal(
            BattleActionAuthorizationDiagnosticCode.BasicAttackDefinitionMismatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task CatalogAuthorization_RejectsBasicAttackWithoutResolvedProfile()
    {
        var basicAttack = new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false);
        TargetingDefinition targeting = SingleEnemy();
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([]),
            NoBattleBasicAttackProfileSource.Instance);
        BattleActionExecutor executor = Executor(authorization: authorization);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);

        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            new BasicAttackBattleActionCommand(
                basicAttack,
                targeting,
                [target.InstanceId],
                Id("natural_attack")),
            actor,
            [actor, target]));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.ActionNotAuthorized, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task CatalogAuthorization_RejectsBasicAttackTargetingSubstitution()
    {
        var basicAttack = new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false);
        TargetingDefinition targeting = SingleEnemy();
        var profile = new BattleBasicAttackProfile(Id("natural_attack"), basicAttack, targeting);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([]),
            new FixedBasicAttackProfileSource(profile));
        BattleActionExecutor executor = Executor(authorization: authorization);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState first = Actor("first", TeamB);
        RuntimeActorState second = Actor("second", TeamB);
        var command = new BasicAttackBattleActionCommand(
            basicAttack,
            new TargetingDefinition(
                TargetRelation.Enemy,
                TargetSelection.All,
                TargetLifeState.Alive,
                AllowSelf: false),
            actionId: profile.ActionId);

        BattleActionAuthorizationResult direct = authorization.Authorize(actor, command);
        BattleActionExecutionResult result = await executor.ExecuteAsync(Request(
            command,
            actor,
            [actor, first, second]));

        Assert.Equal(
            BattleActionAuthorizationDiagnosticCode.BasicAttackTargetingMismatch,
            Assert.Single(direct.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(100, first.GetRequiredResource(Hp).Current);
        Assert.Equal(100, second.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task CatalogAuthorization_RevalidatesBasicAttackProfileBeforeExecution()
    {
        var basicAttack = new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false);
        TargetingDefinition targeting = SingleEnemy();
        var profile = new BattleBasicAttackProfile(Id("natural_attack"), basicAttack, targeting);
        var profileSource = new MutableBasicAttackProfileSource(profile);
        var authorization = new CatalogBattleActionAuthorizationPolicy(
            new TestSkillRepository([]),
            new TestItemRepository([]),
            profileSource);
        BattleActionExecutor executor = Executor(authorization: authorization);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var command = new BasicAttackBattleActionCommand(
            basicAttack,
            targeting,
            [target.InstanceId],
            profile.ActionId);
        BattleActionExecutionRequest request = Request(command, actor, [actor, target]);
        BattleActionAssessment assessment = executor.Assess(request);
        profileSource.Profile = null;

        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.ActionNotAuthorized, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task SkillAction_RejectsStalePreparedCostWithoutMutationOrTurnConsumption()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA, sp: 10);
        RuntimeActorState target = Actor("target", TeamB);
        SkillDefinition skill = ActiveSkill(
            "frost",
            [new SkillCostDefinition(Sp, new FlatAmountDefinition(10), CanReduceToZero: true)],
            [new DamageEffectDefinition(DamageElement.Ice, 7, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1))]);
        var command = new SkillBattleActionCommand(skill, [target.InstanceId]);
        var request = Request(command, actor, [actor, target]);
        BattleActionAssessment assessment = executor.Assess(request);
        actor.SetResource(Sp, 0);

        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.SkillRejected, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Empty(result.Effects);
        Assert.Equal(0, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task RandomSkill_OneStepExecutionResolvesAndMutatesExactlyOneTarget()
    {
        var randomTargets = new AlternatingSkillRandomTargetPolicy();
        BattleActionExecutor executor = Executor(randomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState first = Actor("first", TeamB);
        RuntimeActorState second = Actor("second", TeamB);
        SkillDefinition skill = ActiveSkill(
            "random_frost",
            [],
            [new DamageEffectDefinition(
                DamageElement.Ice,
                7,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))],
            RandomEnemy());

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new SkillBattleActionCommand(skill), actor, [actor, first, second]));

        Assert.Equal(1, randomTargets.CallCount);
        Assert.Equal(first.InstanceId, Assert.Single(result.Effects).TargetId);
        Assert.Equal(90, first.GetRequiredResource(Hp).Current);
        Assert.Equal(100, second.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task RandomItem_PreparedAssessmentExecutesTheDisplayedTargetWithoutRerolling()
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState first = Actor("first", TeamA, hp: 20);
        RuntimeActorState second = Actor("second", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem(
            "random_medicine",
            [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20))],
            RandomAlly());
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        BattleActionExecutionRequest request = Request(
            new ItemBattleActionCommand(medicine),
            actor,
            [actor, first, second],
            inventory);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.Equal([first.InstanceId], assessment.TargetIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeInstanceId>)assessment.TargetIds).Add(second.InstanceId));
        Assert.Equal(1, randomTargets.CallCount);
        Assert.Equal(first.InstanceId, Assert.Single(result.Effects).TargetId);
        Assert.Equal(40, first.GetRequiredResource(Hp).Current);
        Assert.Equal(20, second.GetRequiredResource(Hp).Current);
        Assert.Equal(0, inventory.Quantity);
    }

    [Fact]
    public async Task RandomBasicAttack_PreparedAssessmentIsSingleUseAndNeverRerolls()
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState first = Actor("first", TeamB);
        RuntimeActorState second = Actor("second", TeamB);
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false),
            RandomEnemy());
        BattleActionExecutionRequest request = Request(command, actor, [actor, first, second]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult executed = await executor.ExecuteAsync(request, assessment);
        BattleActionExecutionResult reused = await executor.ExecuteAsync(request, assessment);

        Assert.Equal([first.InstanceId], assessment.TargetIds);
        Assert.Equal(1, randomTargets.CallCount);
        Assert.Equal(first.InstanceId, Assert.Single(executed.Effects).TargetId);
        Assert.Equal(90, first.GetRequiredResource(Hp).Current);
        Assert.Equal(100, second.GetRequiredResource(Hp).Current);
        Assert.Equal(BattleActionExecutionStatus.Rejected, reused.Status);
        Assert.Equal(BattleActionDiagnosticCode.AssessmentInvalid, Assert.Single(reused.Diagnostics).Code);
    }

    [Fact]
    public async Task RandomBasicAttack_RejectsAStalePreparedTargetWithoutRerolling()
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState first = Actor("first", TeamB);
        RuntimeActorState second = Actor("second", TeamB);
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false),
            RandomEnemy());
        BattleActionExecutionRequest request = Request(command, actor, [actor, first, second]);
        BattleActionAssessment assessment = executor.Assess(request);
        first.SetResource(Hp, 0m);

        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.Equal([first.InstanceId], assessment.TargetIds);
        Assert.Equal(1, randomTargets.CallCount);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.AssessmentInvalid, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(result.Effects);
        Assert.Equal(100m, second.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task PreparedItem_RejectsAStaleTargetBeforeInventoryReservation()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20m);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20m)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        BattleActionExecutionRequest request = Request(
            new ItemBattleActionCommand(medicine, [target.InstanceId]),
            actor,
            [actor, target],
            inventory);
        BattleActionAssessment assessment = executor.Assess(request);
        target.SetResource(Hp, 0m);

        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(BattleActionDiagnosticCode.AssessmentInvalid, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public void ItemExecutor_RejectsPreparedTargetsThatBecomeIneligible()
    {
        BattleExecutionServices services = ExecutionServices();
        var executor = new ItemExecutor(services);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20m);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20m)));
        var request = new ItemExecutionRequest(
            medicine,
            actor,
            [actor, target],
            new EffectExecutionEnvironment(Battle),
            [target.InstanceId]);
        ItemExecutionAssessment assessment = executor.Assess(request);
        target.SetResource(Hp, 0m);

        ItemExecutionResult result = executor.Execute(request, assessment);

        Assert.Equal(ItemExecutionStatus.Rejected, result.Status);
        Assert.Equal(ItemExecutionDiagnosticCode.AssessmentInvalid, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void ItemExecutor_RejectsPreparedUseThatNoLongerHasAMeaningfulEffect()
    {
        var executor = new ItemExecutor(ExecutionServices());
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20m);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20m)));
        var request = new ItemExecutionRequest(
            medicine,
            actor,
            [actor, target],
            new EffectExecutionEnvironment(Battle),
            [target.InstanceId]);
        ItemExecutionAssessment assessment = executor.Assess(request);
        target.SetResource(Hp, 100m);

        ItemExecutionResult result = executor.Execute(request, assessment);

        Assert.Equal(ItemExecutionStatus.Rejected, result.Status);
        Assert.Equal(ItemExecutionDiagnosticCode.NoApplicableEffect, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(ItemConsumptionDecision.None, result.Consumption);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void ItemExecutor_AssessmentRejectsInvalidProgrammaticEffectSequence()
    {
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var invalidSharedContact = new DamageEffectDefinition(
            DamageElement.Fire,
            10,
            100,
            new NeverCriticalDefinition(),
            new HitCountDefinition(1, 1))
        {
            ContactMode = DamageContactMode.SharedContact
        };
        ItemDefinition item = ConsumableItem(
            "invalid_item",
            [invalidSharedContact],
            SingleEnemy());
        var executor = new ItemExecutor(ExecutionServices());

        ItemExecutionAssessment assessment = executor.Assess(new ItemExecutionRequest(
            item,
            actor,
            [actor, target],
            new EffectExecutionEnvironment(Battle),
            [target.InstanceId]));

        Assert.False(assessment.CanExecute);
        ItemExecutionDiagnostic diagnostic = Assert.Single(assessment.Diagnostics);
        Assert.Equal(ItemExecutionDiagnosticCode.ExecutionFailed, diagnostic.Code);
        Assert.Contains("same-target positive-damage dependency", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ItemActionRejectsInvalidAuthoredPercentageBeforeTargetingOrReservation()
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState ally = Actor("ally", TeamA, hp: 50);
        ItemDefinition item = ConsumableItem(
            "invalid_item",
            [new ApplyAilmentEffectDefinition(Id("test_ailment"), 101)],
            RandomAlly());
        var inventory = new TestItemInventory(item.Id, 1);
        var request = Request(
            new ItemBattleActionCommand(item),
            actor,
            [actor, ally],
            inventory);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.False(assessment.CanExecute);
        Assert.Equal(ActionTurnConsumptionKind.None, assessment.TurnConsumption.Kind);
        Assert.Equal(
            BattleActionDiagnosticCode.AuthoredPercentageOutOfRange,
            Assert.Single(assessment.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Equal(0, randomTargets.CallCount);
        Assert.Equal(0, inventory.ReservationsCreated);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(50, ally.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void BasicAttackAssessmentRejectsInvalidProgrammaticEffectSequence()
    {
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var basicAttack = new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            10,
            100,
            new NeverCriticalDefinition(),
            false)
        {
            SecondaryEffects =
            [
                new DamageEffectDefinition(
                    DamageElement.Fire,
                    5,
                    100,
                    new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1))
                {
                    ContactMode = DamageContactMode.SharedContact
                }
            ]
        };
        var request = Request(
            new BasicAttackBattleActionCommand(basicAttack, SingleEnemy(), [target.InstanceId]),
            actor,
            [actor, target]);

        BattleActionAssessment assessment = Executor().Assess(request);

        Assert.False(assessment.CanExecute);
        BattleActionDiagnostic diagnostic = Assert.Single(assessment.Diagnostics);
        Assert.Equal(BattleActionDiagnosticCode.ExecutionFailed, diagnostic.Code);
        Assert.Contains("same-target positive-damage dependency", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicAttackRejectsInvalidPrimaryOrSecondaryPercentageBeforeTargeting(bool secondary)
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var basicAttack = new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            10,
            secondary ? 100 : 101,
            new NeverCriticalDefinition(),
            false)
        {
            SecondaryEffects = secondary
                ? [new ApplyAilmentEffectDefinition(Id("test_ailment"), -1)]
                : []
        };
        var request = Request(
            new BasicAttackBattleActionCommand(basicAttack, RandomEnemy()),
            actor,
            [actor, target]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.False(assessment.CanExecute);
        Assert.Equal(ActionTurnConsumptionKind.None, assessment.TurnConsumption.Kind);
        Assert.Equal(
            BattleActionDiagnosticCode.AuthoredPercentageOutOfRange,
            Assert.Single(assessment.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Equal(0, randomTargets.CallCount);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task EscapeRejectsInvalidAuthoredPercentageWithoutTurnConsumption()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        var request = Request(
            new EscapeAttemptBattleActionCommand(Id("escape_rule"), -1),
            actor,
            [actor]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.False(assessment.CanExecute);
        Assert.Equal(ActionTurnConsumptionKind.None, assessment.TurnConsumption.Kind);
        Assert.Equal(
            BattleActionDiagnosticCode.AuthoredPercentageOutOfRange,
            Assert.Single(assessment.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.False(result.EscapeRequested);
    }

    [Theory]
    [InlineData(MissingEffectConfiguration.Ailment, "Ailment")]
    [InlineData(MissingEffectConfiguration.Formula, "formula handler")]
    [InlineData(MissingEffectConfiguration.CustomCondition, "custom condition handler")]
    [InlineData(MissingEffectConfiguration.CustomEffect, "custom effect handler")]
    public async Task BasicAttackRejectsMissingEffectConfigurationBeforeRandomTargeting(
        MissingEffectConfiguration missing,
        string expectedMessage)
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        EffectDefinition secondary = missing switch
        {
            MissingEffectConfiguration.Ailment =>
                new ApplyAilmentEffectDefinition(Id("missing_ailment"), 100),
            MissingEffectConfiguration.Formula =>
                new ReduceResourceEffectDefinition(
                    Hp,
                    new FormulaAmountDefinition(Id("missing_formula")),
                    true),
            MissingEffectConfiguration.CustomCondition =>
                new ReduceResourceEffectDefinition(
                    Hp,
                    new FlatAmountDefinition(1),
                    true,
                    new AllConditionDefinition(
                    [
                        new NotConditionDefinition(
                            new CustomConditionDefinition(Id("missing_condition")))
                    ])),
            MissingEffectConfiguration.CustomEffect =>
                new CustomEffectDefinition(Id("missing_effect")),
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
        var basicAttack = new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            15,
            100,
            new NeverCriticalDefinition(),
            false)
        {
            SecondaryEffects = [secondary]
        };
        var request = Request(
            new BasicAttackBattleActionCommand(basicAttack, RandomEnemy()),
            actor,
            [actor, target]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.False(assessment.CanExecute);
        Assert.Equal(ActionTurnConsumptionKind.None, assessment.TurnConsumption.Kind);
        BattleActionDiagnostic diagnostic = Assert.Single(assessment.Diagnostics);
        Assert.Equal(BattleActionDiagnosticCode.EffectConfigurationInvalid, diagnostic.Code);
        Assert.Equal(1, diagnostic.EffectIndex);
        Assert.Contains(expectedMessage, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, randomTargets.CallCount);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Empty(result.Effects);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task EscapeRejectsMissingRuleDuringAssessmentWithoutTurnConsumption()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        var request = Request(
            new EscapeAttemptBattleActionCommand(Id("missing_escape_rule"), 100),
            actor,
            [actor]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.False(assessment.CanExecute);
        Assert.Equal(ActionTurnConsumptionKind.None, assessment.TurnConsumption.Kind);
        BattleActionDiagnostic diagnostic = Assert.Single(assessment.Diagnostics);
        Assert.Equal(BattleActionDiagnosticCode.EffectConfigurationInvalid, diagnostic.Code);
        Assert.Equal(0, diagnostic.EffectIndex);
        Assert.Contains("escape rule handler", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Empty(result.Effects);
        Assert.False(result.EscapeRequested);
    }

    [Fact]
    public void SkillAndItemRejectMissingFormulaBeforeRandomTargeting()
    {
        var skillTargets = new AlternatingSkillRandomTargetPolicy();
        var itemTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleExecutionServices services = ExecutionServices(
            randomTargetPolicy: skillTargets,
            runtimeRandomTargetPolicy: itemTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var formulaEffect = new ReduceResourceEffectDefinition(
            Hp,
            new FormulaAmountDefinition(Id("missing_formula")),
            true);
        SkillDefinition skill = ActiveSkill(
            "formula_skill",
            [],
            [formulaEffect],
            RandomEnemy());
        ItemDefinition item = ConsumableItem(
            "formula_item",
            [formulaEffect],
            RandomEnemy());

        SkillExecutionAssessment skillAssessment = new SkillExecutor(services).Assess(
            new SkillExecutionRequest(
                skill,
                actor,
                [actor, target],
                new EffectExecutionEnvironment(Battle)));
        ItemExecutionAssessment itemAssessment = new ItemExecutor(services).Assess(
            new ItemExecutionRequest(
                item,
                actor,
                [actor, target],
                new EffectExecutionEnvironment(Battle)));

        Assert.False(skillAssessment.CanExecute);
        Assert.Equal(
            SkillExecutionDiagnosticCode.FormulaHandlerMissing,
            Assert.Single(skillAssessment.Diagnostics).Code);
        Assert.False(itemAssessment.CanExecute);
        Assert.Equal(
            ItemExecutionDiagnosticCode.FormulaHandlerMissing,
            Assert.Single(itemAssessment.Diagnostics).Code);
        Assert.Equal(0, skillTargets.CallCount);
        Assert.Equal(0, itemTargets.CallCount);
    }

    [Fact]
    public async Task Analyze_PreparedAssessmentExecutesItsDisplayedTargetWithoutRandomSelection()
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        var command = new AnalyzeBattleActionCommand(target.InstanceId, [AnalysisLayer.Affinities]);
        BattleActionExecutionRequest request = Request(command, actor, [actor, target]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.Equal([target.InstanceId], assessment.TargetIds);
        Assert.Equal(0, randomTargets.CallCount);
        Assert.Equal(target.InstanceId, Assert.Single(result.Effects).TargetId);
        Assert.Contains(AnalysisLayer.Affinities, actor.GetAnalysis(target.InstanceId));
    }

    [Fact]
    public async Task PreparedAssessment_RejectsAnotherRequestWithoutConsumptionOrMutation()
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        BattleActionExecutor otherExecutor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState first = Actor("first", TeamB);
        RuntimeActorState second = Actor("second", TeamB);
        var command = new BasicAttackBattleActionCommand(
            new EquipmentBasicAttackDefinition(DamageElement.Physical, 15, 100, new NeverCriticalDefinition(), false),
            RandomEnemy());
        BattleActionExecutionRequest assessedRequest = Request(command, actor, [actor, first, second]);
        BattleActionExecutionRequest differentRequest = Request(command, actor, [actor, first, second]);

        BattleActionAssessment assessment = executor.Assess(assessedRequest);
        BattleActionExecutionResult wrongExecutor = await otherExecutor.ExecuteAsync(assessedRequest, assessment);
        BattleActionExecutionResult mismatch = await executor.ExecuteAsync(differentRequest, assessment);
        BattleActionExecutionResult executed = await executor.ExecuteAsync(assessedRequest, assessment);

        Assert.Equal(BattleActionExecutionStatus.Rejected, wrongExecutor.Status);
        Assert.Equal(BattleActionDiagnosticCode.AssessmentInvalid, Assert.Single(wrongExecutor.Diagnostics).Code);
        Assert.Equal(BattleActionExecutionStatus.Rejected, mismatch.Status);
        Assert.Equal(BattleActionDiagnosticCode.AssessmentInvalid, Assert.Single(mismatch.Diagnostics).Code);
        Assert.Equal(1, randomTargets.CallCount);
        Assert.Equal(first.InstanceId, Assert.Single(executed.Effects).TargetId);
        Assert.Equal(90, first.GetRequiredResource(Hp).Current);
        Assert.Equal(100, second.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task ItemAction_ReservesAndCommitsOnlyWhenConsumptionSucceeds()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem("medicine", new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.ItemConsumption);
        Assert.True(result.ItemConsumptionCommitted);
        Assert.Equal(0, inventory.Quantity);
        Assert.Equal(40, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task ItemAction_AlwaysReservesAndCommitsExactlyOneItem()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 2);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.ItemConsumption);
        Assert.True(result.ItemConsumptionCommitted);
        Assert.Equal(1, inventory.LastReservedQuantity);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(40, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task ItemAction_DefaultOutcomePolicySpendsNormalTurnAndPreservesWeaknessEvidence()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor(
            "target",
            TeamB,
            defense: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Ice,
                    ElementalAffinity.Weak)]));
        ItemDefinition item = ConsumableItem(
            "ice_capsule",
            [new DamageEffectDefinition(
                DamageElement.Ice,
                10,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))],
            SingleEnemy());
        var inventory = new TestItemInventory(item.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(item, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Executed, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.TurnEconomy, result.TurnConsumption.Kind);
        Assert.Equal(TurnEconomyOutcome.Normal, result.TurnConsumption.TurnEconomy!.Outcome);
        Assert.False(result.TurnConsumption.TurnEconomy.AnyCritical);
        Assert.Equal(TurnEconomyOutcome.Weakness, Assert.Single(result.Effects).TurnEconomyOutcome);
        Assert.Equal(90, target.GetRequiredResource(Hp).Current);
        Assert.Equal(0, inventory.Quantity);
    }

    [Fact]
    public async Task ItemAction_EffectDrivenPolicyUsesTypedDamageOutcome()
    {
        var outcomes = new StandardActionOutcomeAggregationPolicy(
            new StandardActionOutcomeAggregationPolicyConfig(
                ItemActionOutcomeBehavior.EffectDriven));
        BattleActionExecutor executor = Executor(actionOutcomes: outcomes);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor(
            "target",
            TeamB,
            defense: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Ice,
                    ElementalAffinity.Weak)]));
        ItemDefinition item = ConsumableItem(
            "ice_capsule",
            [new DamageEffectDefinition(
                DamageElement.Ice,
                10,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))],
            SingleEnemy());
        var inventory = new TestItemInventory(item.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(item, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(TurnEconomyOutcome.Weakness, result.TurnConsumption.TurnEconomy!.Outcome);
        Assert.Equal(TurnEconomyOutcome.Weakness, Assert.Single(result.Effects).TurnEconomyOutcome);
        Assert.Equal(0, inventory.Quantity);
    }

    [Fact]
    public async Task ItemAction_RequiresInventoryPortBeforeExecution()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        BattleActionExecutionRequest request = Request(
            new ItemBattleActionCommand(medicine, [target.InstanceId]),
            actor,
            [actor, target]);

        BattleActionAssessment assessment = executor.Assess(request);
        BattleActionExecutionResult result = await executor.ExecuteAsync(request, assessment);

        Assert.False(assessment.CanExecute);
        Assert.Contains(assessment.Diagnostics, diagnostic =>
            diagnostic.Code == BattleActionDiagnosticCode.ItemInventoryRequired);
        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Empty(result.Effects);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
        Assert.False(result.ItemConsumptionCommitted);
    }

    [Fact]
    public async Task ItemAction_DoesNotReserveWhenAssessmentRejects()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 100);
        ItemDefinition medicine = ConsumableItem("medicine", new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public async Task ItemAction_ThrowingEffectRollsBackReservationAndActorState()
    {
        ContentId handlerId = Id("throwing_item_effect");
        BattleActionExecutor executor = Executor(
            customEffects: [new(handlerId, new ThrowingCustomEffectHandler())]);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition item = ConsumableItem(
            "unstable_medicine",
            [
                new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)),
                new CustomEffectDefinition(handlerId)
            ]);
        var inventory = new TestItemInventory(item.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(item, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == BattleActionDiagnosticCode.ItemRejected &&
            diagnostic.Message.Contains("failed before commit", StringComparison.Ordinal));
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
        Assert.Equal(1, inventory.Quantity);
        Assert.False(result.ItemConsumptionCommitted);
        Assert.Contains(result.Events, battleEvent => battleEvent.Kind == BattleActionEventKind.ItemRolledBack);
    }

    [Fact]
    public async Task SkillAction_MalformedCustomEffectResultRejectsBeforeCommitOrTurnUse()
    {
        ContentId handlerId = Id("malformed_custom_effect");
        BattleActionExecutor executor = Executor(
            customEffects: [new(handlerId, new MalformedCustomEffectHandler())]);
        RuntimeActorState actor = Actor("actor", TeamA, sp: 10);
        RuntimeActorState target = Actor("target", TeamB, hp: 20);
        SkillDefinition skill = ActiveSkill(
            "malformed_chain",
            [new SkillCostDefinition(Sp, new FlatAmountDefinition(3))],
            [
                new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)),
                new CustomEffectDefinition(handlerId),
                new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20))
            ]);

        BattleActionExecutionResult result = await Execute(
            executor,
            new SkillBattleActionCommand(skill, [target.InstanceId]),
            actor,
            [actor, target]);

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(ActionTurnConsumptionKind.None, result.TurnConsumption.Kind);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == BattleActionDiagnosticCode.SkillRejected &&
            diagnostic.Message.Contains("Effect outcome must be defined", StringComparison.Ordinal));
        Assert.Empty(result.Effects);
        Assert.Equal(10, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task ItemAction_ThrowingOutcomePolicyRollsBackReservationAndActorState()
    {
        BattleActionExecutor executor = Executor(actionOutcomes: new ThrowingActionOutcomePolicy());
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        ItemDefinition item = ConsumableItem(
            "unstable_capsule",
            [new DamageEffectDefinition(
                DamageElement.Physical,
                10,
                100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))],
            SingleEnemy());
        var inventory = new TestItemInventory(item.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(item, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
        Assert.Equal(1, inventory.Quantity);
        Assert.False(result.ItemConsumptionCommitted);
        Assert.Contains(result.Events, battleEvent => battleEvent.Kind == BattleActionEventKind.ItemRolledBack);
    }

    [Fact]
    public async Task ItemAction_RejectedCommitLeavesStagedEffectsUnpublished()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new RejectingCommitInventory(medicine.Id, quantity: 1);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(
            BattleActionDiagnosticCode.ItemCommitFailed,
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
        Assert.Equal(1, inventory.Quantity);
        Assert.True(inventory.WasRolledBack);
        Assert.False(result.ItemConsumptionCommitted);
    }

    [Fact]
    public async Task ItemAction_ThrowingReservationReturnsTypedFailureWithoutMutation()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new ThrowingReserveInventory(medicine.Id);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Equal(
            BattleActionDiagnosticCode.ItemReservationFailed,
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
    }

    [Theory]
    [InlineData(MalformedReservationKind.Null)]
    [InlineData(MalformedReservationKind.WrongItem)]
    [InlineData(MalformedReservationKind.WrongQuantity)]
    [InlineData(MalformedReservationKind.AlreadyCommitted)]
    [InlineData(MalformedReservationKind.AlreadyRolledBack)]
    public async Task ItemAction_RejectsMalformedReservationBeforeActorMutation(
        MalformedReservationKind malformedKind)
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem(
            "medicine",
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new MalformedReservationInventory(medicine.Id, malformedKind);

        BattleActionExecutionResult result = await executor.ExecuteAsync(
            Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory));

        Assert.Equal(BattleActionExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == BattleActionDiagnosticCode.ItemReservationInvalid);
        Assert.Equal(20, target.GetRequiredResource(Hp).Current);
        Assert.False(result.ItemConsumptionCommitted);
        Assert.Equal(0, inventory.CommitCalls);
        Assert.Equal(
            malformedKind is MalformedReservationKind.WrongItem or MalformedReservationKind.WrongQuantity ? 1 : 0,
            inventory.RollbackCalls);
    }

    [Fact]
    public async Task ItemAction_CancellationOccursBeforeReservation()
    {
        BattleActionExecutor executor = Executor();
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem("medicine", new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)));
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await executor.ExecuteAsync(
                Request(new ItemBattleActionCommand(medicine, [target.InstanceId]), actor, [actor, target], inventory),
                cancellation.Token));
        Assert.Equal(1, inventory.Quantity);
        Assert.Equal(0, inventory.ReservationsCreated);
    }

    [Fact]
    public async Task PreparedItemCancellationDoesNotConsumeAssessmentOrReserveInventory()
    {
        var randomTargets = new AlternatingRuntimeRandomTargetPolicy();
        BattleActionExecutor executor = Executor(runtimeRandomTargetPolicy: randomTargets);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState first = Actor("first", TeamA, hp: 20);
        RuntimeActorState second = Actor("second", TeamA, hp: 20);
        ItemDefinition medicine = ConsumableItem(
            "random_medicine",
            [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20))],
            RandomAlly());
        var inventory = new TestItemInventory(medicine.Id, quantity: 1);
        BattleActionExecutionRequest request = Request(
            new ItemBattleActionCommand(medicine),
            actor,
            [actor, first, second],
            inventory);
        BattleActionAssessment assessment = executor.Assess(request);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await executor.ExecuteAsync(request, assessment, cancellation.Token));
        BattleActionExecutionResult retried = await executor.ExecuteAsync(request, assessment);

        Assert.Equal(1, randomTargets.CallCount);
        Assert.Equal(1, inventory.ReservationsCreated);
        Assert.Equal(BattleActionExecutionStatus.Executed, retried.Status);
        Assert.Equal(40, first.GetRequiredResource(Hp).Current);
        Assert.Equal(20, second.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public async Task AnalyzeEscapeHostAndPartyCommands_ReturnStructuredResults()
    {
        ContentId escapeRule = Id("standard_escape");
        BattleActionExecutor executor = Executor(escapeRules: [new(escapeRule, new AlwaysEscapeRule())]);
        RuntimeActorState actor = Actor("actor", TeamA);
        RuntimeActorState target = Actor("target", TeamB);
        RuntimePartyRosterSnapshot roster = PartyRoster(actor);

        BattleActionExecutionResult analyze = await Execute(
            executor,
            new AnalyzeBattleActionCommand(target.InstanceId, [AnalysisLayer.Stats]),
            actor,
            [actor, target]);
        BattleActionExecutionResult escape = await Execute(
            executor,
            new EscapeAttemptBattleActionCommand(escapeRule, 100),
            actor,
            [actor, target]);
        BattleActionExecutionResult host = await Execute(
            executor,
            new HostMediatedBattleActionCommand(BattleActionKind.TacticsChange, Id("change_strategy"), ActionTurnConsumption.None),
            actor,
            [actor]);
        BattleActionExecutionResult deploy = await Execute(
            executor,
            new CompanionDeployBattleActionCommand(roster, RuntimeInstanceId.Parse("companion:glow_wisp")),
            actor,
            [actor]);

        Assert.Contains(AnalysisLayer.Stats, actor.GetAnalysis(target.InstanceId));
        Assert.True(escape.EscapeRequested);
        Assert.Equal(ActionTurnConsumptionKind.None, escape.TurnConsumption.Kind);
        Assert.Equal([Id("change_strategy")], host.HostActionRequestIds);
        Assert.NotNull(deploy.PartyRosterTransition);
        Assert.True(deploy.PartyRosterTransition.Applied);
    }

    private static BattleActionExecutionRequest Request(
        BattleActionCommand command,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        IItemActionInventory? inventory = null) =>
        new(command, actor, participants, new EffectExecutionEnvironment(Battle), inventory);

    private static Task<BattleActionExecutionResult> Execute(
        BattleActionExecutor executor,
        BattleActionCommand command,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants) =>
        executor.ExecuteAsync(Request(command, actor, participants)).AsTask();

    private static BattleActionExecutor Executor(
        IEnumerable<KeyValuePair<ContentId, IEscapeRuleHandler>>? escapeRules = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffects = null,
        IRandomTargetSelectionPolicy? randomTargetPolicy = null,
        IRuntimeRandomTargetSelectionPolicy? runtimeRandomTargetPolicy = null,
        IBattleActionAuthorizationPolicy? authorization = null,
        IDamageExecutionPolicy? damagePolicy = null,
        IActionOutcomeAggregationPolicy? actionOutcomes = null,
        IAilmentDefinitionRepository? ailments = null,
        IAilmentApplicationPolicy? ailmentPolicy = null)
    {
        BattleExecutionServices services = ExecutionServices(
            escapeRules,
            customEffects,
            randomTargetPolicy,
            runtimeRandomTargetPolicy,
            damagePolicy,
            actionOutcomes,
            ailments,
            ailmentPolicy);
        return new BattleActionExecutor(
            new SkillExecutor(services),
            new ItemExecutor(services),
            services,
            authorization ?? AllowAllBattleActionAuthorizationPolicy.Instance);
    }

    private static BattleExecutionServices ExecutionServices(
        IEnumerable<KeyValuePair<ContentId, IEscapeRuleHandler>>? escapeRules = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffects = null,
        IRandomTargetSelectionPolicy? randomTargetPolicy = null,
        IRuntimeRandomTargetSelectionPolicy? runtimeRandomTargetPolicy = null,
        IDamageExecutionPolicy? damagePolicy = null,
        IActionOutcomeAggregationPolicy? actionOutcomes = null,
        IAilmentDefinitionRepository? ailments = null,
        IAilmentApplicationPolicy? ailmentPolicy = null) =>
        new(
            ailments ?? EmptyAilments.Instance,
            damagePolicy ?? new FixedDamagePolicy(),
            new NeverInstantDeathPolicy(),
            ailmentPolicy ?? new NeverAilmentPolicy(),
            new AlwaysChancePolicy(),
            new PowerAmountPolicy(),
            randomTargetPolicy ?? new OrderedRandomTargetPolicy(),
            runtimeRandomTargetPolicy ?? new OrderedRuntimeTargetSelectionPolicy(),
            TestStatModifierPolicy.CreatePersistent(),
            new SplitChargePolicy(),
            escapeRuleHandlers: escapeRules,
            customEffectHandlers: customEffects,
            actionOutcomes: actionOutcomes);

    private static RuntimeActorState Actor(
        string id,
        ContentId team,
        decimal hp = 100,
        decimal sp = 20,
        CombatDefenseProfile? defense = null,
        IEnumerable<ContentId>? skillIds = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            Id(id + "_entity"),
            team,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 20)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), team),
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Agility, 10),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Luck, 10)
            ],
            skillIds: skillIds);

    private static SkillDefinition ActiveSkill(
        string id,
        IEnumerable<SkillCostDefinition> costs,
        IEnumerable<EffectDefinition> effects,
        TargetingDefinition? targeting = null) =>
        new(
            Id(id),
            id,
            id,
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Ice,
            new SkillInheritanceDefinition(true),
            costs: costs,
            targeting: targeting ?? SingleEnemy(),
            effects: effects,
            availability: new SkillAvailabilityDefinition([Battle]));

    private static ItemDefinition ConsumableItem(string id, EffectDefinition effect) =>
        ConsumableItem(id, [effect]);

    private static ItemDefinition ConsumableItem(string id, IEnumerable<EffectDefinition> effects) =>
        ConsumableItem(id, effects, SingleAlly());

    private static ItemDefinition ConsumableItem(
        string id,
        IEnumerable<EffectDefinition> effects,
        TargetingDefinition targeting) =>
        new(
            Id(id),
            id,
            id,
            ItemKind.Consumable,
            99,
            10,
            new ItemUsageDefinition([Battle], targeting, effects));

    private static TargetingDefinition SingleEnemy() =>
        new(TargetRelation.Enemy, TargetSelection.Single, TargetLifeState.Alive, false);

    private static TargetingDefinition SingleAlly() =>
        new(TargetRelation.Ally, TargetSelection.Single, TargetLifeState.Alive, true);

    private static TargetingDefinition RandomEnemy() =>
        new(
            TargetRelation.Enemy,
            TargetSelection.Random,
            TargetLifeState.Alive,
            false,
            new TargetCountDefinition(1, 1));

    private static TargetingDefinition RandomAlly() =>
        new(
            TargetRelation.Ally,
            TargetSelection.Random,
            TargetLifeState.Alive,
            false,
            new TargetCountDefinition(1, 1));

    private static RuntimePartyRosterSnapshot PartyRoster(RuntimeActorState owner) =>
        new(
            new RuntimeActorReferenceSnapshot(
                owner.InstanceId,
                owner.EntityId,
                owner.Identity.DisplayName),
            [new RuntimeActorReferenceSnapshot(
                owner.InstanceId,
                owner.EntityId,
                owner.Identity.DisplayName)],
            companionRoster:
            [
                new RuntimeActorReferenceSnapshot(RuntimeInstanceId.Parse("companion:glow_wisp"), Id("glow_wisp"), "Glow Wisp")
            ]);

    private static ContentId Id(string value) => ContentId.Parse(value);

    public enum MissingEffectConfiguration
    {
        Ailment,
        Formula,
        CustomCondition,
        CustomEffect
    }

    private sealed class EmptyAilments : IAilmentDefinitionRepository
    {
        public static EmptyAilments Instance { get; } = new();
        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            throw new KeyNotFoundException(id.ToString());
    }

    private sealed class FixedDamagePolicy : IDamageExecutionPolicy
    {
        public DamagePolicyResolution Resolve(DamagePolicyRequest request) =>
            new([new DamageHitResolution(true, 10)], request.Affinity);
    }

    private sealed class NeverInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
    }

    private sealed class NeverAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => false;
    }

    private sealed class AlwaysAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => true;
    }

    private sealed class TestAilmentRepository(params AilmentDefinition[] ailments)
        : IAilmentDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, AilmentDefinition> _ailments =
            ailments.ToDictionary(ailment => ailment.Id);

        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition) =>
            _ailments.TryGetValue(id, out definition);

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            TryGetAilment(id, out AilmentDefinition? definition)
                ? definition!
                : throw new KeyNotFoundException(id.ToString());
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => true;
    }

    private sealed class PowerAmountPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
    }

    private sealed class RecordingActionOutcomePolicy(TurnEconomyResolution result)
        : IActionOutcomeAggregationPolicy
    {
        public int CallCount { get; private set; }

        public TurnEconomyResolution Aggregate(IReadOnlyList<EffectExecutionResult> effects)
        {
            CallCount++;
            return result;
        }
    }

    private sealed class ThrowingActionOutcomePolicy : IActionOutcomeAggregationPolicy
    {
        public TurnEconomyResolution Aggregate(IReadOnlyList<EffectExecutionResult> effects) =>
            throw new InvalidOperationException("Outcome aggregation failed deliberately.");
    }

    private sealed class OrderedRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            Array.AsReadOnly(candidates.Take(count.Maximum).ToArray());
    }

    private sealed class RecordingDamagePolicy : IDamageExecutionPolicy
    {
        private readonly List<DamagePolicyRequest> _requests = [];

        public IReadOnlyList<DamagePolicyRequest> Requests => _requests.AsReadOnly();

        public DamagePolicyResolution Resolve(DamagePolicyRequest request)
        {
            _requests.Add(request);
            return new DamagePolicyResolution([new DamageHitResolution(true, 10)], request.Affinity);
        }
    }

    private sealed class AlternatingSkillRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request)
        {
            int index = CallCount++ % candidates.Count;
            return [candidates[index]];
        }
    }

    private sealed class AlternatingRuntimeRandomTargetPolicy : IRuntimeRandomTargetSelectionPolicy
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            EffectActionExecutionRequest request)
        {
            int index = CallCount++ % candidates.Count;
            return [candidates[index]];
        }
    }

    private sealed class AlwaysEscapeRule : IEscapeRuleHandler
    {
        public bool CanEscape(EscapeEffectDefinition effect, EffectExecutionContext context) => true;
    }

    private sealed class ThrowingCustomEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context) =>
            throw new InvalidOperationException("Custom item effect failed deliberately.");
    }

    private sealed class MalformedCustomEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context) =>
            new(
                context.EffectIndex,
                context.Target?.InstanceId,
                (EffectExecutionOutcome)999,
                (TurnEconomyOutcome)999);
    }

    private sealed class AllowAllBattleActionAuthorizationPolicy : IBattleActionAuthorizationPolicy
    {
        private AllowAllBattleActionAuthorizationPolicy()
        {
        }

        public static AllowAllBattleActionAuthorizationPolicy Instance { get; } = new();

        public BattleActionAuthorizationResult Authorize(
            RuntimeActorState actor,
            BattleActionCommand command) =>
            BattleActionAuthorizationResult.Authorized;
    }

    private sealed class TestSkillRepository(IEnumerable<SkillDefinition> skills)
        : ISkillDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _skills =
            skills.ToDictionary(skill => skill.Id);

        public bool TryGetSkill(ContentId id, out SkillDefinition? definition) =>
            _skills.TryGetValue(id, out definition);

        public SkillDefinition GetRequiredSkill(ContentId id) =>
            _skills.TryGetValue(id, out SkillDefinition? definition)
                ? definition
                : throw new KeyNotFoundException($"Skill '{id}' was not found.");
    }

    private sealed class TestItemRepository(IEnumerable<ItemDefinition> items)
        : IItemDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, ItemDefinition> _items =
            items.ToDictionary(item => item.Id);

        public bool TryGetItem(ContentId id, out ItemDefinition? definition) =>
            _items.TryGetValue(id, out definition);

        public ItemDefinition GetRequiredItem(ContentId id) =>
            _items.TryGetValue(id, out ItemDefinition? definition)
                ? definition
                : throw new KeyNotFoundException($"Item '{id}' was not found.");
    }

    private sealed class MutableItemRepository(ItemDefinition? item) : IItemDefinitionRepository
    {
        public ItemDefinition? Item { get; set; } = item;

        public bool TryGetItem(ContentId id, out ItemDefinition? definition)
        {
            definition = Item?.Id == id ? Item : null;
            return definition is not null;
        }

        public ItemDefinition GetRequiredItem(ContentId id) =>
            TryGetItem(id, out ItemDefinition? definition)
                ? definition!
                : throw new KeyNotFoundException($"Item '{id}' was not found.");
    }

    private sealed class FixedBasicAttackProfileSource(BattleBasicAttackProfile? profile)
        : IBattleBasicAttackProfileSource
    {
        public BattleBasicAttackProfile? Resolve(RuntimeActorState actor) => profile;
    }

    private sealed class MutableBasicAttackProfileSource(BattleBasicAttackProfile? profile)
        : IBattleBasicAttackProfileSource
    {
        public BattleBasicAttackProfile? Profile { get; set; } = profile;

        public BattleBasicAttackProfile? Resolve(RuntimeActorState actor) => Profile;
    }

    private sealed class TestItemInventory(ContentId itemId, int quantity) : IItemActionInventory
    {
        public int Quantity { get; private set; } = quantity;
        public int ReservationsCreated { get; private set; }
        public int? LastReservedQuantity { get; private set; }

        public bool HasAvailable(ContentId requestedItemId, int requestedQuantity) =>
            requestedItemId == itemId && Quantity >= requestedQuantity;

        public IItemActionReservation Reserve(ContentId requestedItemId, int requestedQuantity)
        {
            if (!HasAvailable(requestedItemId, requestedQuantity))
            {
                throw new InvalidOperationException("Item is unavailable.");
            }

            ReservationsCreated++;
            LastReservedQuantity = requestedQuantity;
            return new Reservation(this, requestedItemId, requestedQuantity);
        }

        private sealed class Reservation(TestItemInventory inventory, ContentId itemId, int quantity) : IItemActionReservation
        {
            public ContentId ItemId { get; } = itemId;
            public int Quantity { get; } = quantity;
            public bool IsCommitted { get; private set; }
            public bool IsRolledBack { get; private set; }

            public ItemActionReservationTransitionResult Commit()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "Item reservation has already been completed.");
                }

                inventory.Quantity -= Quantity;
                IsCommitted = true;
                return ItemActionReservationTransitionResult.Success;
            }

            public ItemActionReservationTransitionResult Rollback()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "Item reservation has already been completed.");
                }

                IsRolledBack = true;
                return ItemActionReservationTransitionResult.Success;
            }
        }
    }

    private sealed class RejectingCommitInventory(ContentId itemId, int quantity) : IItemActionInventory
    {
        public int Quantity { get; private set; } = quantity;
        public bool WasRolledBack { get; private set; }

        public bool HasAvailable(ContentId requestedItemId, int requestedQuantity) =>
            requestedItemId == itemId && Quantity >= requestedQuantity;

        public IItemActionReservation Reserve(ContentId requestedItemId, int requestedQuantity) =>
            new Reservation(this, requestedItemId, requestedQuantity);

        private sealed class Reservation(
            RejectingCommitInventory inventory,
            ContentId itemId,
            int quantity) : IItemActionReservation
        {
            public ContentId ItemId { get; } = itemId;
            public int Quantity { get; } = quantity;
            public bool IsCommitted { get; private set; }
            public bool IsRolledBack { get; private set; }

            public ItemActionReservationTransitionResult Commit() =>
                ItemActionReservationTransitionResult.Rejected("Host inventory rejected the commit.");

            public ItemActionReservationTransitionResult Rollback()
            {
                IsRolledBack = true;
                inventory.WasRolledBack = true;
                return ItemActionReservationTransitionResult.Success;
            }
        }
    }

    private sealed class ThrowingReserveInventory(ContentId itemId) : IItemActionInventory
    {
        public bool HasAvailable(ContentId requestedItemId, int requestedQuantity) =>
            requestedItemId == itemId && requestedQuantity == 1;

        public IItemActionReservation Reserve(ContentId requestedItemId, int requestedQuantity) =>
            throw new InvalidOperationException("Host inventory failed during reservation.");
    }

    public enum MalformedReservationKind
    {
        Null,
        WrongItem,
        WrongQuantity,
        AlreadyCommitted,
        AlreadyRolledBack
    }

    private sealed class MalformedReservationInventory(
        ContentId requestedItemId,
        MalformedReservationKind malformedKind) : IItemActionInventory
    {
        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public bool HasAvailable(ContentId itemId, int quantity) =>
            itemId == requestedItemId && quantity == 1;

        public IItemActionReservation Reserve(ContentId itemId, int quantity) =>
            malformedKind == MalformedReservationKind.Null
                ? null!
                : new Reservation(this, itemId, quantity, malformedKind);

        private sealed class Reservation(
            MalformedReservationInventory inventory,
            ContentId requestedItemId,
            int requestedQuantity,
            MalformedReservationKind malformedKind) : IItemActionReservation
        {
            public ContentId ItemId { get; } = malformedKind == MalformedReservationKind.WrongItem
                ? Id("different_item")
                : requestedItemId;

            public int Quantity { get; } = malformedKind == MalformedReservationKind.WrongQuantity
                ? requestedQuantity + 1
                : requestedQuantity;

            public bool IsCommitted { get; private set; } =
                malformedKind == MalformedReservationKind.AlreadyCommitted;

            public bool IsRolledBack { get; private set; } =
                malformedKind == MalformedReservationKind.AlreadyRolledBack;

            public ItemActionReservationTransitionResult Commit()
            {
                inventory.CommitCalls++;
                IsCommitted = true;
                return ItemActionReservationTransitionResult.Success;
            }

            public ItemActionReservationTransitionResult Rollback()
            {
                inventory.RollbackCalls++;
                IsRolledBack = true;
                return ItemActionReservationTransitionResult.Success;
            }
        }
    }
}
