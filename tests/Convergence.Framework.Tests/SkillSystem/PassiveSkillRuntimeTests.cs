using Convergence.Content;
using Convergence.Catalog;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class PassiveSkillRuntimeTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId NewMoon = ContentId.Parse("new_moon");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId Poison = ContentId.Parse("poison");

    [Fact]
    public void PassiveCollection_RejectsActiveAndDuplicateSkillsAndMutatesImmediately()
    {
        SkillDefinition passive = PassiveSkill("passive");
        SkillDefinition active = ActiveDamageSkill("active", DamageElement.Fire);
        var collection = new BattlePassiveCollection([passive]);

        Assert.Throws<ArgumentException>(() => collection.Add(active));
        Assert.Throws<InvalidOperationException>(() => collection.Add(passive));
        Assert.True(collection.Disable(passive.Id));
        Assert.False(Assert.Single(collection.Entries).IsEnabled);
        Assert.True(collection.Enable(passive.Id));
        Assert.True(Assert.Single(collection.Entries).IsEnabled);
        Assert.True(collection.Remove(passive.Id));
        Assert.Empty(collection.Entries);
        Assert.False(collection.Remove(passive.Id));
    }

    [Fact]
    public void IceBoostFixture_ChangesIceDamageButNotFireDamage()
    {
        string path = TestContentPath.Resolve(
            Path.Combine(AppContext.BaseDirectory, "Content"),
            "reference/skill-system-redesign/skill_system_redesign.skills.sample.json");
        SkillDefinition iceBoost = Assert.Single(
            new SkillSystemJsonDeserializer().DeserializeSkills(File.ReadAllText(path), path).Records);
        RuntimeActorState actor = Actor("actor", PlayerTeam, passiveSkills: [iceBoost]);
        RuntimeActorState iceTarget = Actor("ice_target", EnemyTeam);
        RuntimeActorState fireTarget = Actor("fire_target", EnemyTeam);
        BattleExecutionServices services = Services(damage: _ => [new DamageHitResolution(true, 20)]);
        var executor = new SkillExecutor(services);

        SkillExecutionResult iceResult = executor.Execute(Request(
            ActiveDamageSkill("ice_skill", DamageElement.Ice),
            actor,
            [actor, iceTarget],
            iceTarget));
        SkillExecutionResult fireResult = executor.Execute(Request(
            ActiveDamageSkill("fire_skill", DamageElement.Fire),
            actor,
            [actor, fireTarget],
            fireTarget));

        Assert.Equal(25, iceResult.Effects[0].Value);
        Assert.Equal(20, fireResult.Effects[0].Value);
        Assert.Equal(75, iceTarget.GetRequiredResource(Hp).Current);
        Assert.Equal(80, fireTarget.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void NumericModifiers_AlwaysResolveAddThenMultiplyRegardlessOfLoadoutOrder()
    {
        SkillDefinition add = PassiveSkill(
            "add",
            modifiers: [new NumericRuleModifierDefinition(NumericRuleModifierType.DamageDealt, ModifierOperation.Add, 10)]);
        SkillDefinition doubleValue = PassiveSkill(
            "double",
            modifiers: [new NumericRuleModifierDefinition(NumericRuleModifierType.DamageDealt, ModifierOperation.Multiply, 2)]);
        BattleExecutionServices services = Services();

        decimal first = ResolveDamage([add, doubleValue], services);
        decimal second = ResolveDamage([doubleValue, add], services);

        Assert.Equal(40, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void DamageExecution_ForwardsApplicableAccuracyAndEvasionPassivesToTheHitAuthority()
    {
        SkillDefinition accuracy = PassiveSkill(
            "accuracy",
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.Accuracy,
                    ModifierOperation.Add,
                    12m)
            ]);
        SkillDefinition evasion = PassiveSkill(
            "evasion",
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.Evasion,
                    ModifierOperation.Multiply,
                    1.25m)
            ]);
        RuntimeActorState actor = Actor("actor", PlayerTeam, passiveSkills: [accuracy]);
        RuntimeActorState target = Actor("target", EnemyTeam, passiveSkills: [evasion]);
        DamagePolicyRequest? captured = null;
        BattleExecutionServices services = Services(request =>
        {
            captured = request;
            return [new DamageHitResolution(true, 10)];
        });

        SkillExecutionResult result = new SkillExecutor(services).Execute(Request(
            ActiveDamageSkill("attack", DamageElement.Physical),
            actor,
            [actor, target],
            target));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.NotNull(captured);
        Assert.Equal(
            NumericRuleModifierType.Accuracy,
            Assert.Single(captured.AccuracyModifiers).ModifierType);
        Assert.Equal(
            NumericRuleModifierType.Evasion,
            Assert.Single(captured.EvasionModifiers).ModifierType);
    }

    [Fact]
    public void NumericModifiers_SaturateExtremeAddAndMultiplyStacks()
    {
        SkillDefinition firstAdd = PassiveSkill(
            "first_add",
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.DamageDealt,
                    ModifierOperation.Add,
                    decimal.MaxValue)
            ]);
        SkillDefinition secondAdd = PassiveSkill(
            "second_add",
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.DamageDealt,
                    ModifierOperation.Add,
                    decimal.MaxValue)
            ]);
        SkillDefinition multiply = PassiveSkill(
            "multiply",
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.DamageDealt,
                    ModifierOperation.Multiply,
                    decimal.MaxValue)
            ]);

        decimal result = ResolveDamage([firstAdd, secondAdd, multiply], Services());

        Assert.Equal(decimal.MaxValue, result);
    }

    [Fact]
    public void ArmsMaster_UsesEveryTypedSkillElementWhenResolvingCosts()
    {
        SkillDefinition armsMaster = PassiveSkill(
            "arms_master",
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.ResourceCost,
                    ModifierOperation.Multiply,
                    0.5m,
                    new EffectElementConditionDefinition(DamageElement.Physical))
            ]);
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 100, passiveSkills: [armsMaster]);
        RuntimeActorState target = Actor("target", EnemyTeam);
        SkillDefinition mixedSkill = new(
            ContentId.Parse("mixed_skill"),
            "Mixed Skill",
            "Contains Physical and Fire damage.",
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true),
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(10))],
            targeting: SingleEnemy(),
            effects:
            [
                Damage(DamageElement.Fire),
                Damage(DamageElement.Physical)
            ],
            availability: new SkillAvailabilityDefinition([Battle]));

        new SkillExecutor(Services()).Execute(Request(mixedSkill, actor, [actor, target], target));

        Assert.Equal(95, actor.GetRequiredResource(Sp).Current);
    }

    [Fact]
    public void TypedAilmentReplacement_ChangesOnlyTheRequestedAilmentResistance()
    {
        var resistancePolicy = new RecordingAilmentPolicy();
        SkillDefinition resistPoison = PassiveSkill(
            "resist_poison",
            modifiers: [new AilmentResistanceRuleModifierDefinition(Poison, ResistanceLevel.Resistant)]);
        var defense = new CombatDefenseProfile(
            ailmentResistances: [new KeyValuePair<ContentId, ResistanceLevel>(Poison, ResistanceLevel.Vulnerable)]);
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, defense: defense, passiveSkills: [resistPoison]);
        SkillDefinition poisonSkill = new(
            ContentId.Parse("poison_skill"),
            "Poison Skill",
            "Applies Poison.",
            SkillActivation.Active,
            SkillMenuGroup.Ailment,
            InheritanceGroup.Ailment,
            new SkillInheritanceDefinition(true),
            targeting: SingleEnemy(),
            effects: [new ApplyAilmentEffectDefinition(Poison, 100)],
            availability: new SkillAvailabilityDefinition([Battle]));

        new SkillExecutor(Services(ailmentPolicy: resistancePolicy)).Execute(
            Request(poisonSkill, actor, [actor, target], target));

        Assert.Equal(ResistanceLevel.Resistant, resistancePolicy.LastResistance);
        Assert.Equal(ResistanceLevel.Normal, target.DefenseProfile.GetAilmentResistance(ContentId.Parse("sleep")));
    }

    [Fact]
    public void TurnAndBattleEvents_DispatchRegenerateAndOpeningFocus()
    {
        ContentId turnEnd = ContentId.Parse("owner_turn_end");
        ContentId battleStart = ContentId.Parse("battle_start");
        ContentId attack = ContentId.Parse("attack");
        SkillDefinition regenerate = PassiveSkill(
            "regenerate",
            triggers:
            [
                new PassiveTriggerDefinition(
                    turnEnd,
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))])
            ]);
        SkillDefinition openingFocus = PassiveSkill(
            "opening_focus",
            triggers:
            [
                new PassiveTriggerDefinition(
                    battleStart,
                    [new ModifyStatStageEffectDefinition([attack], 1)])
            ]);
        RuntimeActorState actor = Actor(
            "actor",
            PlayerTeam,
            hp: 50,
            passiveSkills: [regenerate, openingFocus]);
        BattleExecutionServices services = Services();

        PassiveTriggerDispatchResult turnResult = Dispatch(turnEnd, actor, [actor], [actor], services);
        PassiveTriggerDispatchResult startResult = Dispatch(battleStart, actor, [actor], [actor], services);

        Assert.Equal(60, actor.GetRequiredResource(Hp).Current);
        Assert.Equal(1, actor.StatStages[attack].Stage);
        Assert.Equal(regenerate.Id, Assert.Single(turnResult.Activations).SkillId);
        Assert.Equal(openingFocus.Id, Assert.Single(startResult.Activations).SkillId);
    }

    [Fact]
    public void TriggerDispatch_IsOrderedByLoadoutThenTriggerThenTargetThenEffect()
    {
        ContentId eventId = ContentId.Parse("ordered_event");
        SkillDefinition first = PassiveSkill(
            "first",
            triggers:
            [
                new PassiveTriggerDefinition(
                    eventId,
                    [
                        new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(1)),
                        new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(2))
                    ])
            ]);
        SkillDefinition second = PassiveSkill(
            "second",
            triggers: [new PassiveTriggerDefinition(eventId, [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(3))])]);
        RuntimeActorState owner = Actor("owner", PlayerTeam, passiveSkills: [first, second]);
        RuntimeActorState targetA = Actor("target_a", PlayerTeam, hp: 50);
        RuntimeActorState targetB = Actor("target_b", PlayerTeam, hp: 50);

        PassiveTriggerDispatchResult result = Dispatch(
            eventId,
            owner,
            [owner, targetA, targetB],
            [targetA, targetB],
            Services());

        Assert.Equal(
            [
                (first.Id, targetA.InstanceId),
                (first.Id, targetB.InstanceId),
                (second.Id, targetA.InstanceId),
                (second.Id, targetB.InstanceId)
            ],
            result.Activations.Select(activation => (activation.SkillId, activation.TargetId)));
        Assert.Equal([0, 1], result.Activations[0].Effects.Select(effect => effect.EffectIndex));
        Assert.Equal(56, targetA.GetRequiredResource(Hp).Current);
        Assert.Equal(56, targetB.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void DisabledOrRemovedPassive_DoesNotDispatchOrMutateState()
    {
        ContentId eventId = ContentId.Parse("owner_turn_end");
        SkillDefinition passive = PassiveSkill(
            "regenerate",
            triggers: [new PassiveTriggerDefinition(eventId, [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))])]);
        RuntimeActorState owner = Actor("owner", PlayerTeam, hp: 50, passiveSkills: [passive]);
        BattleExecutionServices services = Services();

        owner.Passives.Disable(passive.Id);
        Assert.Empty(Dispatch(eventId, owner, [owner], [owner], services).Activations);
        Assert.Equal(50, owner.GetRequiredResource(Hp).Current);

        owner.Passives.Enable(passive.Id);
        Assert.Single(Dispatch(eventId, owner, [owner], [owner], services).Activations);
        Assert.Equal(60, owner.GetRequiredResource(Hp).Current);

        owner.Passives.Remove(passive.Id);
        Assert.Empty(Dispatch(eventId, owner, [owner], [owner], services).Activations);
        Assert.Equal(60, owner.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void TriggerDispatch_SuppressesRecursiveActivationOfTheSameTrigger()
    {
        ContentId eventId = ContentId.Parse("recursive_event");
        ContentId handlerId = ContentId.Parse("redispatch");
        SkillDefinition passive = PassiveSkill(
            "recursive_passive",
            triggers: [new PassiveTriggerDefinition(eventId, [new CustomEffectDefinition(handlerId)])]);
        RuntimeActorState owner = Actor("owner", PlayerTeam, passiveSkills: [passive]);
        var handler = new RedispatchingEffectHandler(eventId);
        BattleExecutionServices services = Services(
            customEffects: [new KeyValuePair<ContentId, ICustomEffectHandler>(handlerId, handler)]);

        PassiveTriggerDispatchResult result = Dispatch(eventId, owner, [owner], [owner], services);

        PassiveTriggerExecutionResult activation = Assert.Single(result.Activations);
        Assert.Equal(PassiveTriggerOutcome.Executed, activation.Outcome);
        PassiveTriggerExecutionResult nested = Assert.Single(Assert.Single(activation.Effects).PassiveActivations!);
        Assert.Equal(PassiveTriggerOutcome.RecursionSuppressed, nested.Outcome);
    }

    [Fact]
    public void TriggerDispatch_StopActionStopsRemainingTargetsAndEffects()
    {
        ContentId eventId = ContentId.Parse("failure_event");
        ContentId handlerId = ContentId.Parse("fail");
        SkillDefinition passive = PassiveSkill(
            "failure_passive",
            triggers:
            [
                new PassiveTriggerDefinition(
                    eventId,
                    [
                        new CustomEffectDefinition(handlerId, onFailure: EffectFailurePolicy.StopAction),
                        new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))
                    ])
            ]);
        RuntimeActorState owner = Actor("owner", PlayerTeam, passiveSkills: [passive]);
        RuntimeActorState first = Actor("first", PlayerTeam, hp: 50);
        RuntimeActorState second = Actor("second", PlayerTeam, hp: 50);
        BattleExecutionServices services = Services(
            customEffects:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(handlerId, new FailingEffectHandler())
            ]);

        PassiveTriggerDispatchResult result = Dispatch(
            eventId,
            owner,
            [owner, first, second],
            [first, second],
            services);

        PassiveTriggerExecutionResult activation = Assert.Single(result.Activations);
        Assert.Equal(first.InstanceId, activation.TargetId);
        Assert.Single(activation.Effects);
        Assert.Equal(EffectExecutionOutcome.Failure, activation.Effects[0].Outcome);
        Assert.Equal(50, first.GetRequiredResource(Hp).Current);
        Assert.Equal(50, second.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void TriggerDispatch_ThrowingHandlerRollsBackEffectsAndActivationBookkeeping()
    {
        ContentId eventId = ContentId.Parse("atomic_event");
        ContentId handlerId = ContentId.Parse("mutate_then_throw");
        SkillDefinition passive = PassiveSkill(
            "atomic_passive",
            triggers:
            [
                new PassiveTriggerDefinition(
                    eventId,
                    [
                        new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10)),
                        new CustomEffectDefinition(handlerId)
                    ])
            ]);
        RuntimeActorState owner = Actor("atomic_owner", PlayerTeam, hp: 50, passiveSkills: [passive]);
        BattleExecutionServices services = Services(
            customEffects:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    handlerId,
                    new MutatingThrowingEffectHandler())
            ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Dispatch(eventId, owner, [owner], [owner], services));

        Assert.Equal("Deliberate custom-effect failure.", exception.Message);
        Assert.Equal(50, owner.GetRequiredResource(Hp).Current);
        Assert.Empty(owner.ToSnapshot().BattleActivations.PassiveActivations);
    }

    [Fact]
    public void LastStand_RestoresAfterFirstLethalHitButSecondLethalHitDefeatsOwner()
    {
        SkillDefinition lastStand = PassiveSkill(
            "last_stand",
            triggers:
            [
                new PassiveTriggerDefinition(
                    ContentId.Parse("owner_would_be_defeated"),
                    [new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(1))])
            ]);
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, hp: 10, passiveSkills: [lastStand]);
        BattleExecutionServices services = Services(damage: _ => [new DamageHitResolution(true, 20)]);
        var executor = new SkillExecutor(services);
        SkillDefinition attack = ActiveDamageSkill("attack", DamageElement.Physical);

        SkillExecutionResult first = executor.Execute(Request(attack, actor, [actor, target], target));
        Assert.False(target.IsDefeated);
        Assert.Equal(1, target.GetRequiredResource(Hp).Current);
        SkillExecutionResult second = executor.Execute(Request(attack, actor, [actor, target], target));

        Assert.Equal(PassiveTriggerOutcome.Executed, Assert.Single(first.PassiveActivations).Outcome);
        Assert.Equal(PassiveTriggerOutcome.ActivationLimitReached, Assert.Single(second.PassiveActivations).Outcome);
        Assert.Equal(0, target.GetRequiredResource(Hp).Current);
        Assert.True(target.IsDefeated);
        Assert.Equal(first.TurnEconomy.Outcome, second.TurnEconomy.Outcome);
    }

    [Fact]
    public void AffinityReplacements_RespectShieldBreakOverrideAndAlmightyRules()
    {
        SkillDefinition nullFire = PassiveSkill(
            "null_fire",
            modifiers: [new ElementalAffinityRuleModifierDefinition(DamageElement.Fire, ElementalAffinity.Null)]);
        var defense = new CombatDefenseProfile(
            [new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Fire, ElementalAffinity.Weak)]);
        RuntimeActorState owner = Actor("owner", PlayerTeam, defense: defense, passiveSkills: [nullFire]);
        BattleExecutionServices services = Services();
        var conditionContext = new BattleConditionContext(
            owner,
            owner,
            [owner],
            NormalBattle,
            NewMoon,
            services,
            [DamageElement.Fire]);
        IReadOnlyList<ElementalAffinity> replacements = services.RuleModifiers.ResolveElementalAffinityReplacements(
            owner,
            DamageElement.Fire,
            new RuleModifierContext(conditionContext));

        Assert.Equal(ElementalAffinity.Null, owner.GetElementalAffinity(DamageElement.Fire, replacements));
        owner.GrantShield(ShieldKind.Magical, null);
        Assert.Equal(ElementalAffinity.Repel, owner.GetElementalAffinity(DamageElement.Fire, replacements));
        owner.RemoveNonModifierStatuses(new HashSet<StatusEffectKind> { StatusEffectKind.Shield }, []);
        owner.OverrideAffinity(DamageElement.Fire, ElementalAffinity.Resist, new BattleDurationDefinition());
        Assert.Equal(ElementalAffinity.Resist, owner.GetElementalAffinity(DamageElement.Fire, replacements));
        Assert.Equal(
            ElementalAffinity.Normal,
            ElementalAffinityResolver.Resolve(defense, DamageElement.Fire, replacements, isBroken: true));
        Assert.Equal(ElementalAffinity.Normal, owner.GetElementalAffinity(DamageElement.Almighty, [ElementalAffinity.Absorb]));
    }

    [Fact]
    public void AffinityConditions_UseTheSamePassiveResolvedAffinityAsDamage()
    {
        SkillDefinition nullFire = PassiveSkill(
            "null_fire",
            modifiers: [new ElementalAffinityRuleModifierDefinition(DamageElement.Fire, ElementalAffinity.Null)]);
        RuntimeActorState attacker = Actor("attacker", EnemyTeam);
        RuntimeActorState owner = Actor(
            "owner",
            PlayerTeam,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]),
            passiveSkills: [nullFire]);
        BattleExecutionServices services = Services();
        var context = new BattleConditionContext(
            attacker,
            owner,
            [attacker, owner],
            NormalBattle,
            NewMoon,
            services,
            [DamageElement.Fire]);

        Assert.True(BattleConditionEvaluator.Evaluate(
            new HasAffinityConditionDefinition(
                ConditionSubject.Target,
                DamageElement.Fire,
                ElementalAffinity.Null),
            context));
        Assert.Equal(
            ElementalAffinity.Null,
            services.RuleModifiers.ResolveElementalAffinity(
                owner,
                DamageElement.Fire,
                new RuleModifierContext(context)));
    }

    [Fact]
    public void ConditionalAffinityReplacement_UsesBaseAffinityToBreakSelfReference()
    {
        var condition = new HasAffinityConditionDefinition(
            ConditionSubject.Actor,
            DamageElement.Fire,
            ElementalAffinity.Weak);
        SkillDefinition conditionalNull = PassiveSkill(
            "conditional_null_fire",
            modifiers:
            [
                new ElementalAffinityRuleModifierDefinition(
                    DamageElement.Fire,
                    ElementalAffinity.Null,
                    condition)
            ]);
        RuntimeActorState owner = Actor(
            "owner",
            PlayerTeam,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]),
            passiveSkills: [conditionalNull]);
        BattleExecutionServices services = Services();
        var context = new BattleConditionContext(
            owner,
            owner,
            [owner],
            NormalBattle,
            NewMoon,
            services,
            [DamageElement.Fire]);

        Assert.Equal(
            ElementalAffinity.Null,
            services.RuleModifiers.ResolveElementalAffinity(
                owner,
                DamageElement.Fire,
                new RuleModifierContext(context)));
    }

    [Fact]
    public void AffinityConditions_EvaluateReplacementConditionsFromThePassiveOwnersPerspective()
    {
        var lowHealthCondition = new ResourcePercentageConditionDefinition(
            ConditionSubject.Actor,
            Hp,
            NumericComparison.LessThanOrEqual,
            50);
        SkillDefinition conditionalNull = PassiveSkill(
            "low_health_null_fire",
            modifiers:
            [
                new ElementalAffinityRuleModifierDefinition(
                    DamageElement.Fire,
                    ElementalAffinity.Null,
                    lowHealthCondition)
            ]);
        RuntimeActorState attacker = Actor("attacker", EnemyTeam, hp: 100);
        RuntimeActorState owner = Actor(
            "owner",
            PlayerTeam,
            hp: 25,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]),
            passiveSkills: [conditionalNull]);
        BattleExecutionServices services = Services();
        var context = new BattleConditionContext(
            attacker,
            owner,
            [attacker, owner],
            NormalBattle,
            NewMoon,
            services,
            [DamageElement.Fire]);

        Assert.True(BattleConditionEvaluator.Evaluate(
            new HasAffinityConditionDefinition(
                ConditionSubject.Target,
                DamageElement.Fire,
                ElementalAffinity.Null),
            context));
    }

    private static decimal ResolveDamage(
        IEnumerable<SkillDefinition> passives,
        BattleExecutionServices services)
    {
        RuntimeActorState owner = Actor("owner", PlayerTeam, passiveSkills: passives);
        var conditions = new BattleConditionContext(
            owner,
            owner,
            [owner],
            NormalBattle,
            NewMoon,
            services,
            [DamageElement.Fire]);
        return services.RuleModifiers.ResolveNumeric(
            owner,
            NumericRuleModifierType.DamageDealt,
            10,
            new RuleModifierContext(conditions));
    }

    private static PassiveTriggerDispatchResult Dispatch(
        ContentId eventId,
        RuntimeActorState owner,
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<RuntimeActorState> targets,
        BattleExecutionServices services) =>
        services.PassiveTriggers.Dispatch(
            new PassiveTriggerDispatchRequest(
                eventId,
                owner,
                participants,
                targets,
                Battle,
                NormalBattle,
                NewMoon),
            services);

    private static SkillDefinition PassiveSkill(
        string id,
        IEnumerable<PassiveTriggerDefinition>? triggers = null,
        IEnumerable<RuleModifierDefinition>? modifiers = null) =>
        new(
            ContentId.Parse(id),
            id,
            "Test passive.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers: triggers,
            modifiers: modifiers);

    private static SkillDefinition ActiveDamageSkill(string id, DamageElement element) =>
        new(
            ContentId.Parse(id),
            id,
            "Test active skill.",
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            element == DamageElement.Physical ? InheritanceGroup.Physical : InheritanceGroup.Fire,
            new SkillInheritanceDefinition(true),
            targeting: SingleEnemy(),
            effects: [Damage(element)],
            availability: new SkillAvailabilityDefinition([Battle]));

    private static DamageEffectDefinition Damage(DamageElement element) =>
        new(element, 10, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1));

    private static TargetingDefinition SingleEnemy() =>
        new(TargetRelation.Enemy, TargetSelection.Single, TargetLifeState.Alive, false);

    private static SkillExecutionRequest Request(
        SkillDefinition skill,
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        RuntimeActorState target) =>
        new(skill, actor, participants, Battle, NormalBattle, NewMoon, [target.InstanceId]);

    private static RuntimeActorState Actor(
        string id,
        ContentId team,
        decimal hp = 100,
        decimal sp = 100,
        CombatDefenseProfile? defense = null,
        IEnumerable<SkillDefinition>? passiveSkills = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            team,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 100)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), team),
            passiveSkills: passiveSkills);

    private static AilmentDefinition PoisonDefinition() =>
        new(
            Poison,
            "Poison",
            "Test poison.",
            new TurnDurationDefinition(3, ContentId.Parse("owner_turn_end"), false),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            new AilmentRecoveryDefinition());

    private static BattleExecutionServices Services(
        Func<DamagePolicyRequest, IReadOnlyList<DamageHitResolution>>? damage = null,
        IAilmentApplicationPolicy? ailmentPolicy = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffects = null) =>
        new(
            new TestAilmentRepository([PoisonDefinition()]),
            new DelegateDamagePolicy(damage ?? (_ => [new DamageHitResolution(true, 10)])),
            new AlwaysInstantDeathPolicy(),
            ailmentPolicy ?? new AlwaysAilmentPolicy(),
            new AlwaysChancePolicy(),
            new FlatPowerPolicy(),
            new FirstTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            TestStatModifierPolicy.CreatePersistent(),
            new SplitChargePolicy(),
            customEffectHandlers: customEffects);

    private sealed class TestAilmentRepository(IEnumerable<AilmentDefinition> ailments)
        : IAilmentDefinitionRepository
    {
        private readonly Dictionary<ContentId, AilmentDefinition> _ailments =
            ailments.ToDictionary(ailment => ailment.Id);

        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition) =>
            _ailments.TryGetValue(id, out definition);

        public AilmentDefinition GetRequiredAilment(ContentId id) => _ailments[id];
    }

    private sealed class DelegateDamagePolicy(
        Func<DamagePolicyRequest, IReadOnlyList<DamageHitResolution>> resolve) : IDamageExecutionPolicy
    {
        public DamagePolicyResolution Resolve(DamagePolicyRequest request) =>
            new(resolve(request), request.Affinity);
    }

    private sealed class AlwaysInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => true;
    }

    private sealed class AlwaysAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => true;
    }

    private sealed class RecordingAilmentPolicy : IAilmentApplicationPolicy
    {
        public ResistanceLevel? LastResistance { get; private set; }

        public bool ShouldApply(AilmentApplicationPolicyRequest request)
        {
            LastResistance = request.Resistance;
            return true;
        }
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => true;
    }

    private sealed class FlatPowerPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
    }

    private sealed class FirstTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            Array.AsReadOnly(candidates.Take(count.Minimum).ToArray());
    }

    private sealed class RedispatchingEffectHandler(ContentId eventId) : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context)
        {
            PassiveTriggerDispatchResult nested = context.Services.PassiveTriggers.Dispatch(
                new PassiveTriggerDispatchRequest(
                    eventId,
                    context.Actor,
                    context.Request.Participants,
                    [context.Target ?? context.Actor],
                    context.Request.ContextId,
                    context.Request.BattleKindId,
                    context.Request.MoonPhaseId),
                context.Services);
            return new EffectExecutionResult(
                context.EffectIndex,
                context.Target?.InstanceId,
                EffectExecutionOutcome.Success,
                PassiveActivations: nested.Activations);
        }
    }

    private sealed class FailingEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context) =>
            new(context.EffectIndex, context.Target?.InstanceId, EffectExecutionOutcome.Failure);
    }

    private sealed class MutatingThrowingEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context)
        {
            (context.Target ?? context.Actor).SetResource(Hp, 1);
            throw new InvalidOperationException("Deliberate custom-effect failure.");
        }
    }
}
