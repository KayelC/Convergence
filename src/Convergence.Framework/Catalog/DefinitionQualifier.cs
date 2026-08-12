using Convergence.Content;

namespace Convergence.Catalog;

internal static class DefinitionQualifier
{
    public static ContentId ContentReference(string packId, ContentId id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("Content reference ID cannot be empty.", nameof(id));
        }

        return id.IsQualified ? id : ContentId.Parse($"{packId}:{id}");
    }

    public static SkillDefinition Skill(string packId, SkillDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.Activation,
            definition.MenuGroup,
            definition.InheritanceGroup,
            new SkillInheritanceDefinition(
                definition.Inheritance.IsInheritable,
                definition.Inheritance.ExclusiveOwnerEntityIds.Select(id => ContentReference(packId, id))),
            definition.Mutation is null
                ? null
                : new SkillMutationDefinition(
                    ContentReference(packId, definition.Mutation.FamilyId),
                    definition.Mutation.Tier),
            definition.Costs.Select(Cost),
            definition.Targeting,
            definition.Effects.Select(effect => Effect(packId, effect)),
            definition.Triggers.Select(trigger => Trigger(packId, trigger)),
            definition.Modifiers.Select(modifier => Modifier(packId, modifier)),
            definition.Availability is null
                ? null
                : new SkillAvailabilityDefinition(definition.Availability.ContextIds));

    public static EntityDefinition Entity(string packId, EntityDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.EntityKindId,
            ContentReference(packId, definition.RaceId),
            definition.Rank,
            definition.BaseLevel,
            definition.Capabilities,
            new EntityInheritanceRulesDefinition(
                definition.InheritanceRules.GroupPolicy,
                definition.InheritanceRules.BlockedSkillIds.Select(id => ContentReference(packId, id)),
                definition.InheritanceRules.AllowedSkillIds.Select(id => ContentReference(packId, id))),
            definition.Stats,
            definition.ElementalAffinities,
            definition.AilmentResistances.Select(pair =>
                new KeyValuePair<ContentId, ResistanceLevel>(ContentReference(packId, pair.Key), pair.Value)),
            definition.InstantDeathResistances,
            definition.BaseSkillIds.Select(id => ContentReference(packId, id)),
            definition.SkillUnlocks.Select(unlock =>
                new SkillUnlockDefinition(unlock.Level, ContentReference(packId, unlock.SkillId))));

    public static RaceDefinition Race(string packId, RaceDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.AlignmentIds,
            definition.NegotiationPersonalityId);

    public static AilmentDefinition Ailment(string packId, AilmentDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            Lifetime(definition.DefaultLifetime),
            AilmentBehavior(definition.TurnBehavior),
            definition.Modifiers,
            new AilmentRecoveryDefinition(
                definition.Recovery.Natural,
                definition.Recovery.RemoveOnEventIds),
            definition.GroupIds,
            definition.ExclusivityGroupId,
            definition.Triggers.Select(trigger => Trigger(packId, trigger)));

    public static ItemDefinition Item(string packId, ItemDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.ItemKind,
            definition.StackLimit,
            definition.BaseValue,
            definition.Usage is null
                ? null
                : new ItemUsageDefinition(
                    definition.Usage.ContextIds,
                    definition.Usage.Targeting,
                    definition.Usage.Effects.Select(effect => Effect(packId, effect)),
                    definition.Usage.ConsumptionMode));

    public static EquipmentDefinition Equipment(string packId, EquipmentDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.SlotId,
            definition.BaseValue,
            definition.GrantedSkillIds.Select(id => ContentReference(packId, id)),
            definition.Weapon is null
                ? null
                : new EquipmentWeaponProfileDefinition(
                    definition.Weapon.BasicAttack with
                    {
                        SecondaryEffects = definition.Weapon.BasicAttack.SecondaryEffects
                            .Select(effect => Effect(packId, effect))
                            .ToArray()
                    }),
            definition.Armor,
            definition.Boots,
            definition.Accessory);

    public static ShopCatalogDefinition Shop(string packId, ShopCatalogDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.CategoryId,
            definition.AvailabilityContextIds,
            definition.Offers.Select(offer => new ShopOfferDefinition(
                offer.Id,
                offer.ContentKind,
                ContentReference(packId, offer.ContentId),
                offer.Price,
                offer.Stock)));

    public static NegotiationDefinition Negotiation(string packId, NegotiationDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.PersonalityId,
            definition.Questions,
            definition.FamiliarDialogueLines,
            definition.Demands,
            definition.DefaultRaceIds.Select(id => ContentReference(packId, id)),
            definition.DefaultEntityIds.Select(id => ContentReference(packId, id)));

    public static EncounterDefinition Encounter(string packId, EncounterDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.EnvironmentId,
            definition.Formations.Select(formation => new EncounterFormationDefinition(
                formation.Weight,
                formation.IsBoss,
                formation.Members.Select(member => new EncounterMemberDefinition(
                    ContentReference(packId, member.EntityId), member.Level, member.Count)),
                formation.RewardPolicyId,
                formation.RewardParameters)));

    public static DungeonDefinition Dungeon(string packId, DungeonDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.Blocks.Select(block => new DungeonBlockDefinition(
                ContentReference(packId, block.Id),
                block.DisplayName,
                block.StartFloor,
                block.EndFloor,
                block.EncounterPoolIds.Select(id => ContentReference(packId, id)),
                block.FixedFloors.Select(floor => new DungeonFixedFloorDefinition(
                    floor.Floor,
                    floor.Kind,
                    floor.Description,
                    floor.EncounterId is null ? null : ContentReference(packId, floor.EncounterId.Value),
                    floor.TransitionRuleId,
                    floor.BarrierRuleId,
                    floor.HasTerminal)))));

    public static FusionRecipeDefinition FusionRecipe(string packId, FusionRecipeDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.Parents.Select(parent => new FusionParentSelectorDefinition(
                parent.Kind,
                ContentReference(packId, parent.Id),
                parent.Role)),
            new FusionResultDefinition(
                definition.Result.Operation,
                definition.Result.ResultEntityId is null
                    ? null
                    : ContentReference(packId, definition.Result.ResultEntityId.Value),
                definition.Result.RankShift,
                definition.Result.PolicyId,
                definition.Result.Parameters),
            definition.AccidentPolicyId,
            definition.MutationPolicyId);

    public static RulesetDefinition Ruleset(string packId, RulesetDefinition definition) =>
        new(
            ContentReference(packId, definition.Id),
            definition.DisplayName,
            definition.Description,
            definition.Category,
            definition.PolicyId,
            definition.Parameters);

    private static SkillCostDefinition Cost(SkillCostDefinition definition) =>
        new(definition.ResourceId, Amount(definition.Amount), definition.CanReduceToZero);

    private static PassiveTriggerDefinition Trigger(string packId, PassiveTriggerDefinition definition) =>
        new(
            definition.EventId,
            definition.Effects.Select(effect => Effect(packId, effect)),
            definition.Targeting,
            definition.When is null ? null : Condition(packId, definition.When));

    private static EffectDefinition Effect(string packId, EffectDefinition definition)
    {
        EffectDefinition qualified = definition switch
        {
            DamageEffectDefinition effect => new DamageEffectDefinition(
            effect.Element, effect.Power, effect.Accuracy, Critical(effect.Critical), effect.Hits, effect.Drain,
            OptionalCondition(packId, effect.When), effect.OnFailure)
            {
                ContactMode = effect.ContactMode
            },
            InstantKillEffectDefinition effect => new InstantKillEffectDefinition(
                effect.Chance, effect.ResistanceCheck, OptionalCondition(packId, effect.When), effect.OnFailure),
            ApplyAilmentEffectDefinition effect => new ApplyAilmentEffectDefinition(
                ContentReference(packId, effect.AilmentId), effect.Chance,
                effect.Lifetime is null ? null : Lifetime(effect.Lifetime),
                OptionalCondition(packId, effect.When), effect.OnFailure),
            RestoreResourceEffectDefinition effect => new RestoreResourceEffectDefinition(
                effect.ResourceId, Amount(effect.Amount), OptionalCondition(packId, effect.When), effect.OnFailure),
            RemoveAilmentEffectDefinition effect => new RemoveAilmentEffectDefinition(
                effect.Scope,
                effect.AilmentIds.Select(id => ContentReference(packId, id)),
                effect.AilmentGroupIds,
                OptionalCondition(packId, effect.When),
                effect.OnFailure),
            ReviveEffectDefinition effect => new ReviveEffectDefinition(
                effect.ResourceId, Amount(effect.Amount), OptionalCondition(packId, effect.When), effect.OnFailure),
            ModifyStatStageEffectDefinition effect => new ModifyStatStageEffectDefinition(
                effect.ModifierTrackIds, effect.StageDelta,
                effect.Duration is null ? null : Duration(effect.Duration),
                OptionalCondition(packId, effect.When), effect.OnFailure),
            GrantChargeEffectDefinition effect => new GrantChargeEffectDefinition(
                effect.Charge, effect.Multiplier,
                effect.Lifetime is null ? null : Lifetime(effect.Lifetime),
                OptionalCondition(packId, effect.When), effect.OnFailure),
            GrantShieldEffectDefinition effect => new GrantShieldEffectDefinition(
                effect.Shield,
                effect.Lifetime is null ? null : Lifetime(effect.Lifetime),
                OptionalCondition(packId, effect.When), effect.OnFailure),
            BreakAffinityEffectDefinition effect => new BreakAffinityEffectDefinition(
                effect.Elements, Lifetime(effect.Lifetime),
                OptionalCondition(packId, effect.When), effect.OnFailure),
            OverrideAffinityEffectDefinition effect => new OverrideAffinityEffectDefinition(
                effect.Elements, effect.Affinity, Lifetime(effect.Lifetime),
                OptionalCondition(packId, effect.When), effect.OnFailure),
            RemoveStatusEffectDefinition effect => new RemoveStatusEffectDefinition(
                effect.StatusKinds, effect.StatusIds, OptionalCondition(packId, effect.When), effect.OnFailure),
            ReduceResourceEffectDefinition effect => new ReduceResourceEffectDefinition(
                effect.ResourceId, Amount(effect.Amount), effect.CanReduceToZero,
                OptionalCondition(packId, effect.When), effect.OnFailure),
            SetResourceEffectDefinition effect => new SetResourceEffectDefinition(
                effect.ResourceId, Amount(effect.Amount), OptionalCondition(packId, effect.When), effect.OnFailure),
            AnalyzeEffectDefinition effect => new AnalyzeEffectDefinition(
                effect.Layers, OptionalCondition(packId, effect.When), effect.OnFailure),
            EscapeEffectDefinition effect => new EscapeEffectDefinition(
                effect.EligibilityRuleId, effect.Chance, OptionalCondition(packId, effect.When), effect.OnFailure),
            CustomEffectDefinition effect => new CustomEffectDefinition(
                effect.HandlerId, effect.Parameters, OptionalCondition(packId, effect.When), effect.OnFailure),
            _ => throw new InvalidOperationException($"Unsupported effect definition '{definition.GetType().Name}'.")
        };

        return qualified with
        {
            EffectId = definition.EffectId,
            Dependency = definition.Dependency
        };
    }

    private static RuleModifierDefinition Modifier(string packId, RuleModifierDefinition definition) => definition switch
    {
        NumericRuleModifierDefinition modifier => new NumericRuleModifierDefinition(
            modifier.ModifierType, modifier.Operation, modifier.Value, OptionalCondition(packId, modifier.When)),
        ElementalAffinityRuleModifierDefinition modifier => new ElementalAffinityRuleModifierDefinition(
            modifier.Element, modifier.Affinity, OptionalCondition(packId, modifier.When)),
        AilmentResistanceRuleModifierDefinition modifier => new AilmentResistanceRuleModifierDefinition(
            ContentReference(packId, modifier.AilmentId), modifier.Resistance,
            OptionalCondition(packId, modifier.When)),
        BasicAttackRuleModifierDefinition modifier => new BasicAttackRuleModifierDefinition(
            modifier.Element, modifier.Targeting, modifier.Drain, OptionalCondition(packId, modifier.When)),
        _ => throw new InvalidOperationException($"Unsupported modifier definition '{definition.GetType().Name}'.")
    };

    private static ConditionDefinition? OptionalCondition(string packId, ConditionDefinition? definition) =>
        definition is null ? null : Condition(packId, definition);

    private static ConditionDefinition Condition(string packId, ConditionDefinition definition) => definition switch
    {
        AllConditionDefinition condition => new AllConditionDefinition(
            condition.Conditions.Select(item => Condition(packId, item))),
        AnyConditionDefinition condition => new AnyConditionDefinition(
            condition.Conditions.Select(item => Condition(packId, item))),
        NotConditionDefinition condition => new NotConditionDefinition(Condition(packId, condition.Condition)),
        ResourcePercentageConditionDefinition condition => condition,
        HasAilmentConditionDefinition condition => new HasAilmentConditionDefinition(
            condition.Subject, condition.AilmentIds.Select(id => ContentReference(packId, id))),
        HasSkillConditionDefinition condition => new HasSkillConditionDefinition(
            condition.Subject, ContentReference(packId, condition.SkillId)),
        HasBuffConditionDefinition condition => condition,
        HasAffinityConditionDefinition condition => condition,
        HasCapabilityConditionDefinition condition => condition,
        LifeStateConditionDefinition condition => condition,
        BattleKindConditionDefinition condition => new BattleKindConditionDefinition(condition.AllowedBattleKindIds),
        MoonPhaseConditionDefinition condition => new MoonPhaseConditionDefinition(condition.AllowedMoonPhaseIds),
        PartySizeConditionDefinition condition => condition,
        ChanceConditionDefinition condition => condition,
        EffectElementConditionDefinition condition => condition,
        CustomConditionDefinition condition => new CustomConditionDefinition(condition.HandlerId, condition.Parameters),
        _ => throw new InvalidOperationException($"Unsupported condition definition '{definition.GetType().Name}'.")
    };

    private static AmountDefinition Amount(AmountDefinition definition) => definition switch
    {
        FlatAmountDefinition amount => amount,
        PercentMaximumAmountDefinition amount => amount,
        PercentCurrentAmountDefinition amount => amount,
        FullAmountDefinition amount => amount,
        PowerAmountDefinition amount => amount,
        FormulaAmountDefinition amount => new FormulaAmountDefinition(amount.FormulaId, amount.Parameters),
        _ => throw new InvalidOperationException($"Unsupported amount definition '{definition.GetType().Name}'.")
    };

    private static DurationDefinition Duration(DurationDefinition definition) => definition switch
    {
        InstantDurationDefinition duration => duration,
        TurnDurationDefinition duration => duration,
        PhaseDurationDefinition duration => duration,
        BattleDurationDefinition duration => duration,
        PermanentDurationDefinition duration => duration,
        _ => throw new InvalidOperationException($"Unsupported duration definition '{definition.GetType().Name}'.")
    };

    private static StatusLifetimeDefinition Lifetime(StatusLifetimeDefinition definition) =>
        new(Duration(definition.Expiration), definition.RemovalProfile);

    private static CriticalDefinition Critical(CriticalDefinition definition) => definition switch
    {
        NeverCriticalDefinition critical => critical,
        ChanceCriticalDefinition critical => critical,
        _ => throw new InvalidOperationException($"Unsupported critical definition '{definition.GetType().Name}'.")
    };

    private static AilmentTurnBehaviorDefinition AilmentBehavior(AilmentTurnBehaviorDefinition definition) =>
        definition switch
        {
            NormalAilmentTurnBehaviorDefinition behavior => behavior,
            SkipAilmentTurnBehaviorDefinition behavior => behavior,
            LimitedActionsAilmentTurnBehaviorDefinition behavior =>
                new LimitedActionsAilmentTurnBehaviorDefinition(behavior.AllowedActionIds),
            ChanceSkipAilmentTurnBehaviorDefinition behavior => behavior,
            ChanceSkipOrFleeAilmentTurnBehaviorDefinition behavior => behavior,
            ForcedBasicAttackAilmentTurnBehaviorDefinition behavior => behavior,
            ConfusedActionAilmentTurnBehaviorDefinition behavior => behavior,
            CustomAilmentTurnBehaviorDefinition behavior =>
                new CustomAilmentTurnBehaviorDefinition(behavior.HandlerId, behavior.Parameters),
            _ => throw new InvalidOperationException(
                $"Unsupported ailment turn behavior '{definition.GetType().Name}'.")
        };
}
