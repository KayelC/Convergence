using System.Collections.ObjectModel;
using System.Text.Json;
using Convergence.Content;

namespace Convergence.Serialization;

internal sealed class SchemaMappingException(string path, string message, string? discriminator = null)
    : Exception(message)
{
    public string Path { get; } = path;
    public string? Discriminator { get; } = discriminator;
}

internal static class SkillSystemDtoMapper
{
    public static ContentPackManifest Map(ManifestDto dto)
    {
        return new ContentPackManifest(
            dto.SchemaVersion,
            NormalizePackId(dto.Id),
            SemanticVersion.Parse(dto.Version),
            dto.DisplayName,
            dto.Description,
            (dto.Dependencies ?? []).Select(dependency => new ContentPackDependency(
                NormalizePackId(dependency.Id),
                SemanticVersion.Parse(dependency.Version))),
            dto.Documents.Select(document => new ContentPackDocumentReference(document.Type, document.Path)));
    }

    public static DeserializedContentDocument<SkillDefinition> Map(SkillDocumentDto dto)
    {
        return new DeserializedContentDocument<SkillDefinition>(
            dto.SchemaVersion,
            dto.Skills.Select((record, index) => MapSkill(record, $"$.skills[{index}]")));
    }

    public static DeserializedContentDocument<EntityDefinition> Map(EntityDocumentDto dto)
    {
        return new DeserializedContentDocument<EntityDefinition>(dto.SchemaVersion, dto.Entities.Select(MapEntity));
    }

    public static DeserializedContentDocument<RaceDefinition> Map(RaceDocumentDto dto)
    {
        return new DeserializedContentDocument<RaceDefinition>(dto.SchemaVersion, dto.Races.Select(MapRace));
    }

    public static DeserializedContentDocument<AilmentDefinition> Map(AilmentDocumentDto dto)
    {
        return new DeserializedContentDocument<AilmentDefinition>(dto.SchemaVersion, dto.Ailments.Select(MapAilment));
    }

    public static DeserializedContentDocument<ItemDefinition> Map(ItemDocumentDto dto)
    {
        return new DeserializedContentDocument<ItemDefinition>(
            dto.SchemaVersion,
            dto.Items.Select((record, index) => MapItem(record, $"$.items[{index}]")));
    }

    public static DeserializedContentDocument<EquipmentDefinition> Map(EquipmentDocumentDto dto)
    {
        return new DeserializedContentDocument<EquipmentDefinition>(
            dto.SchemaVersion,
            dto.Equipment.Select((record, index) => MapEquipment(record, $"$.equipment[{index}]")));
    }

    public static DeserializedContentDocument<ShopCatalogDefinition> Map(ShopDocumentDto dto)
    {
        return new DeserializedContentDocument<ShopCatalogDefinition>(
            dto.SchemaVersion,
            dto.Shops.Select((record, index) => MapShop(record, $"$.shops[{index}]")));
    }

    public static DeserializedContentDocument<NegotiationDefinition> Map(NegotiationDocumentDto dto)
    {
        return new DeserializedContentDocument<NegotiationDefinition>(
            dto.SchemaVersion,
            dto.Negotiations.Select((record, index) => MapNegotiation(record, $"$.negotiations[{index}]")));
    }

    public static DeserializedContentDocument<EncounterDefinition> Map(EncounterDocumentDto dto)
    {
        return new DeserializedContentDocument<EncounterDefinition>(
            dto.SchemaVersion,
            dto.Encounters.Select((record, index) => MapEncounter(record, $"$.encounters[{index}]")));
    }

    public static DeserializedContentDocument<DungeonDefinition> Map(DungeonDocumentDto dto)
    {
        return new DeserializedContentDocument<DungeonDefinition>(
            dto.SchemaVersion,
            dto.Dungeons.Select((record, index) => MapDungeon(record, $"$.dungeons[{index}]")));
    }

    public static DeserializedContentDocument<FusionRecipeDefinition> Map(FusionDocumentDto dto)
    {
        return new DeserializedContentDocument<FusionRecipeDefinition>(
            dto.SchemaVersion,
            dto.FusionRecipes.Select((record, index) => MapFusionRecipe(record, $"$.fusionRecipes[{index}]")));
    }

    public static DeserializedContentDocument<RulesetDefinition> Map(RulesetDocumentDto dto)
    {
        return new DeserializedContentDocument<RulesetDefinition>(
            dto.SchemaVersion,
            dto.Rulesets.Select((record, index) => MapRuleset(record, $"$.rulesets[{index}]")));
    }

    private static ItemDefinition MapItem(ItemDto dto, string path)
    {
        if (dto.ItemKind == ItemKind.Consumable && dto.Usage is null)
        {
            throw new SchemaMappingException(path + ".usage", "Consumable items require usage.");
        }

        if (dto.ItemKind != ItemKind.Consumable && dto.Usage is not null)
        {
            throw new SchemaMappingException(path + ".usage", "Only consumable items may declare usage.");
        }

        return new ItemDefinition(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            dto.ItemKind,
            dto.StackLimit,
            dto.BaseValue,
            dto.Usage is null
                ? null
                : new ItemUsageDefinition(
                    dto.Usage.Contexts.Select(Id),
                    MapTargeting(dto.Usage.Targeting),
                    dto.Usage.Effects.Select(MapEffect),
                    dto.Usage.ConsumeOn));
    }

    private static EquipmentDefinition MapEquipment(EquipmentDto dto, string path)
    {
        int profileCount =
            (dto.Weapon is null ? 0 : 1) +
            (dto.Armor is null ? 0 : 1) +
            (dto.Boots is null ? 0 : 1) +
            (dto.Accessory is null ? 0 : 1);
        if (profileCount != 1)
        {
            throw new SchemaMappingException(path, "Equipment records require exactly one slot profile.");
        }

        if ((dto.Slot == EquipmentSlot.Weapon && dto.Weapon is null) ||
            (dto.Slot == EquipmentSlot.Armor && dto.Armor is null) ||
            (dto.Slot == EquipmentSlot.Boots && dto.Boots is null) ||
            (dto.Slot == EquipmentSlot.Accessory && dto.Accessory is null))
        {
            throw new SchemaMappingException(path, "Equipment slot must match its declared profile.");
        }

        return new EquipmentDefinition(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            dto.Slot,
            dto.BaseValue,
            (dto.GrantedSkillIds ?? []).Select(Id),
            dto.Weapon is null
                ? null
                : new EquipmentWeaponProfileDefinition(new EquipmentBasicAttackDefinition(
                    dto.Weapon.BasicAttack.Element,
                    dto.Weapon.BasicAttack.Power,
                    dto.Weapon.BasicAttack.Accuracy,
                    dto.Weapon.BasicAttack.IsLongRange)),
            dto.Armor is null
                ? null
                : new EquipmentArmorProfileDefinition(dto.Armor.Defense, dto.Armor.Evasion),
            dto.Boots is null
                ? null
                : new EquipmentBootsProfileDefinition(dto.Boots.Evasion),
            dto.Accessory is null
                ? null
                : new EquipmentAccessoryProfileDefinition(
                    (dto.Accessory.StatModifiers ?? []).Select(modifier =>
                        new StatModifierDefinition(Id(modifier.StatId), modifier.Value))));
    }

    private static ShopCatalogDefinition MapShop(ShopCatalogDto dto, string path) =>
        new(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            Id(dto.CategoryId),
            (dto.AvailabilityContexts ?? []).Select(Id),
            dto.Offers.Select((offer, index) => MapShopOffer(offer, $"{path}.offers[{index}]")));

    private static ShopOfferDefinition MapShopOffer(ShopOfferDto dto, string path) =>
        new(dto.ContentKind, Id(dto.ContentId), MapShopPrice(dto.Price, path + ".price"),
            MapShopStock(dto.Stock, path + ".stock"));

    private static ShopPriceDefinition MapShopPrice(ShopPriceDto dto, string path) => dto.Kind switch
    {
        ShopPriceKind.Fixed when dto.BasePrice is decimal value && dto.PricingPolicyId is null =>
            new FixedShopPriceDefinition(value),
        ShopPriceKind.Policy when dto.PricingPolicyId is not null && dto.BasePrice is null =>
            new PolicyShopPriceDefinition(Id(dto.PricingPolicyId), MapParameters(dto.Parameters)),
        ShopPriceKind.Fixed => throw new SchemaMappingException(
            path, "Fixed shop prices require basePrice and must omit pricingPolicyId."),
        ShopPriceKind.Policy => throw new SchemaMappingException(
            path, "Policy shop prices require pricingPolicyId and must omit basePrice."),
        _ => throw new InvalidOperationException($"Unsupported shop price kind '{dto.Kind}'.")
    };

    private static ShopStockDefinition MapShopStock(ShopStockDto dto, string path) => dto.Kind switch
    {
        ShopStockKind.Unlimited when dto.Quantity is null && dto.StockPolicyId is null =>
            new UnlimitedShopStockDefinition(),
        ShopStockKind.Limited when dto.Quantity is int value && dto.StockPolicyId is null =>
            new LimitedShopStockDefinition(value),
        ShopStockKind.Policy when dto.StockPolicyId is not null && dto.Quantity is null =>
            new PolicyShopStockDefinition(Id(dto.StockPolicyId), MapParameters(dto.Parameters)),
        ShopStockKind.Unlimited => throw new SchemaMappingException(
            path, "Unlimited shop stock must omit quantity and stockPolicyId."),
        ShopStockKind.Limited => throw new SchemaMappingException(
            path, "Limited shop stock requires quantity and must omit stockPolicyId."),
        ShopStockKind.Policy => throw new SchemaMappingException(
            path, "Policy shop stock requires stockPolicyId and must omit quantity."),
        _ => throw new InvalidOperationException($"Unsupported shop stock kind '{dto.Kind}'.")
    };

    private static NegotiationDefinition MapNegotiation(NegotiationDto dto, string path) =>
        new(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            Id(dto.PersonalityId),
            dto.Questions.Select((question, index) => new NegotiationQuestionDefinition(
                question.Text,
                question.Answers.Select(answer => new NegotiationAnswerDefinition(answer.Text, answer.Score)))),
            dto.FamiliarDialogueLines,
            (dto.Demands ?? []).Select(demand => new NegotiationDemandDefinition(
                Id(demand.DemandId), demand.Weight, MapParameters(demand.Parameters))),
            (dto.DefaultRaceIds ?? []).Select(Id),
            (dto.DefaultEntityIds ?? []).Select(Id));

    private static EncounterDefinition MapEncounter(EncounterDto dto, string path) =>
        new(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            dto.EnvironmentId is null ? null : Id(dto.EnvironmentId),
            dto.Formations.Select((formation, index) => new EncounterFormationDefinition(
                formation.Weight,
                formation.IsBoss,
                formation.Members.Select(member => new EncounterMemberDefinition(
                    Id(member.EntityId), member.Level, member.Count)),
                formation.RewardPolicyId is null ? null : Id(formation.RewardPolicyId),
                MapParameters(formation.RewardParameters))));

    private static DungeonDefinition MapDungeon(DungeonDto dto, string path) =>
        new(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            dto.Blocks.Select(block => new DungeonBlockDefinition(
                Id(block.Id),
                block.DisplayName,
                block.StartFloor,
                block.EndFloor,
                (block.EncounterPoolIds ?? []).Select(Id),
                (block.FixedFloors ?? []).Select(floor => new DungeonFixedFloorDefinition(
                    floor.Floor,
                    floor.Kind,
                    floor.Description,
                    floor.EncounterId is null ? null : Id(floor.EncounterId),
                    floor.TransitionRuleId is null ? null : Id(floor.TransitionRuleId),
                    floor.BarrierRuleId is null ? null : Id(floor.BarrierRuleId),
                    floor.HasTerminal)))));

    private static FusionRecipeDefinition MapFusionRecipe(FusionRecipeDto dto, string path) =>
        new(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            dto.Parents.Select(parent => new FusionParentSelectorDefinition(parent.Kind, Id(parent.Id))),
            new FusionResultDefinition(
                dto.Result.Operation,
                dto.Result.ResultEntityId is null ? null : Id(dto.Result.ResultEntityId),
                dto.Result.ResultRaceId is null ? null : Id(dto.Result.ResultRaceId),
                dto.Result.RankOffset,
                dto.Result.PolicyId is null ? null : Id(dto.Result.PolicyId),
                MapParameters(dto.Result.Parameters)),
            dto.AccidentPolicyId is null ? null : Id(dto.AccidentPolicyId),
            dto.MutationPolicyId is null ? null : Id(dto.MutationPolicyId));

    private static RulesetDefinition MapRuleset(RulesetDto dto, string path) =>
        new(Id(dto.Id), dto.DisplayName, dto.Description, dto.Category, Id(dto.PolicyId), MapParameters(dto.Parameters));

    private static SkillDefinition MapSkill(SkillDto dto, string path)
    {
        List<PassiveTriggerDto> triggers = dto.Triggers ?? [];
        List<RuleModifierDto> modifiers = dto.Modifiers ?? [];
        List<EffectDto> effects = dto.Effects ?? [];
        List<SkillCostDto> costs = dto.Costs ?? [];

        if (dto.Activation == SkillActivation.Active && (triggers.Count > 0 || modifiers.Count > 0))
        {
            throw new SchemaMappingException(path, "Active skills cannot declare passive triggers or modifiers.");
        }

        if (dto.Activation == SkillActivation.Passive && (dto.Targeting is not null || effects.Count > 0))
        {
            throw new SchemaMappingException(path, "Passive skills cannot declare active targeting or effects.");
        }

        if (dto.Activation == SkillActivation.Passive && dto.Availability is not null)
        {
            throw new SchemaMappingException(path + ".availability", "Passive skills omit availability; their triggers define runtime context.");
        }

        return new SkillDefinition(
            Id(dto.Id),
            dto.DisplayName,
            dto.Description,
            dto.Activation,
            dto.MenuGroup,
            dto.InheritanceGroupId,
            new SkillInheritanceDefinition(
                dto.Inheritance.IsInheritable,
                (dto.Inheritance.ExclusiveOwnerEntityIds ?? []).Select(Id)),
            dto.Mutation is null ? null : new SkillMutationDefinition(Id(dto.Mutation.FamilyId), dto.Mutation.Tier),
            costs.Select(MapCost),
            dto.Targeting is null ? null : MapTargeting(dto.Targeting),
            effects.Select(MapEffect),
            triggers.Select(MapTrigger),
            modifiers.Select(MapModifier),
            dto.Availability is null
                ? null
                : new SkillAvailabilityDefinition(dto.Availability.Contexts.Select(Id)));
    }

    private static SkillCostDefinition MapCost(SkillCostDto dto) =>
        new(Id(dto.ResourceId), MapAmount(dto.Amount), dto.CanReduceToZero);

    private static TargetingDefinition MapTargeting(TargetingDto dto) =>
        new(
            dto.Relation,
            dto.Selection,
            dto.LifeState,
            dto.AllowSelf,
            dto.Count is null ? null : new TargetCountDefinition(dto.Count.Minimum, dto.Count.Maximum));

    private static AmountDefinition MapAmount(AmountDto dto) => dto.Type switch
    {
        "flat" => new FlatAmountDefinition(((ValueAmountDto)dto).Value),
        "percent_max" => new PercentMaximumAmountDefinition(((ValueAmountDto)dto).Value),
        "percent_current" => new PercentCurrentAmountDefinition(((ValueAmountDto)dto).Value),
        "full" => new FullAmountDefinition(),
        "power" => new PowerAmountDefinition(((PowerAmountDto)dto).Power),
        "formula" => new FormulaAmountDefinition(
            Id(((FormulaAmountDto)dto).FormulaId),
            MapParameters(((FormulaAmountDto)dto).Parameters)),
        _ => throw new InvalidOperationException($"Unsupported amount type '{dto.Type}'.")
    };

    private static DurationDefinition MapDuration(DurationDto dto) => dto.Type switch
    {
        "instant" => new InstantDurationDefinition(),
        "turns" => new TurnDurationDefinition(
            ((TurnDurationDto)dto).Value,
            Id(((TurnDurationDto)dto).Tick),
            ((TurnDurationDto)dto).SuspendWhileReserve),
        "phase" => new PhaseDurationDefinition(Id(((PhaseDurationDto)dto).PhaseId)),
        "battle" => new BattleDurationDefinition(),
        "permanent" => new PermanentDurationDefinition(),
        _ => throw new InvalidOperationException($"Unsupported duration type '{dto.Type}'.")
    };

    private static CriticalDefinition MapCritical(CriticalDto dto) => dto.Mode switch
    {
        "never" => new NeverCriticalDefinition(),
        "chance" => new ChanceCriticalDefinition(((ChanceCriticalDto)dto).Chance),
        _ => throw new InvalidOperationException($"Unsupported critical mode '{dto.Mode}'.")
    };

    private static InstantDeathResistanceCheckDefinition MapResistanceCheck(InstantDeathResistanceCheckDto dto) =>
        dto.Mode switch
        {
            "channel" => new ChannelInstantDeathResistanceCheckDefinition(
                ((ChannelInstantDeathResistanceCheckDto)dto).ChannelId),
            "none" => new NoInstantDeathResistanceCheckDefinition(),
            _ => throw new InvalidOperationException($"Unsupported instant-death mode '{dto.Mode}'.")
        };

    private static ConditionDefinition MapCondition(ConditionDto dto)
    {
        if (dto is AllConditionDto all)
        {
            return new AllConditionDefinition(all.All.Select(MapCondition));
        }

        if (dto is AnyConditionDto any)
        {
            return new AnyConditionDefinition(any.Any.Select(MapCondition));
        }

        if (dto is NotConditionDto not)
        {
            return new NotConditionDefinition(MapCondition(not.Not));
        }

        string type = dto.Type ?? throw new InvalidOperationException("Leaf condition has no type.");
        ConditionSubject subject = type.StartsWith("actor_", StringComparison.Ordinal)
            ? ConditionSubject.Actor
            : ConditionSubject.Target;

        return dto switch
        {
            ResourcePercentageConditionDto resource => new ResourcePercentageConditionDefinition(
                subject, Id(resource.ResourceId), resource.Comparison, resource.Value),
            HasAilmentConditionDto ailment => new HasAilmentConditionDefinition(
                subject, ailment.AilmentIds.Select(Id)),
            HasSkillConditionDto skill => new HasSkillConditionDefinition(subject, Id(skill.SkillId)),
            HasBuffConditionDto buff => new HasBuffConditionDefinition(subject, Id(buff.ModifierTrackId)),
            HasAffinityConditionDto affinity => new HasAffinityConditionDefinition(
                subject, affinity.ElementId, affinity.AffinityId),
            HasCapabilityConditionDto capability => new HasCapabilityConditionDefinition(
                subject, Id(capability.CapabilityId)),
            LifeStateConditionDto life => new LifeStateConditionDefinition(subject, life.LifeState),
            AllowedIdsConditionDto allowed when type == "battle_kind" =>
                new BattleKindConditionDefinition(allowed.Allowed.Select(Id)),
            AllowedIdsConditionDto allowed when type == "moon_phase" =>
                new MoonPhaseConditionDefinition(allowed.Allowed.Select(Id)),
            PartySizeConditionDto party => new PartySizeConditionDefinition(party.Comparison, party.Value),
            ChanceConditionDto chance => new ChanceConditionDefinition(chance.Chance),
            EffectElementConditionDto element => new EffectElementConditionDefinition(element.ElementId),
            CustomConditionDto custom => new CustomConditionDefinition(
                Id(custom.HandlerId), MapParameters(custom.Parameters)),
            _ => throw new InvalidOperationException($"Unsupported condition type '{type}'.")
        };
    }

    private static EffectDefinition MapEffect(EffectDto dto)
    {
        ConditionDefinition? when = dto.When is null ? null : MapCondition(dto.When);
        return dto.Type switch
        {
            "damage" => MapDamage((DamageEffectDto)dto, when),
            "instant_kill" => new InstantKillEffectDefinition(
                ((InstantKillEffectDto)dto).Chance,
                MapResistanceCheck(((InstantKillEffectDto)dto).ResistanceCheck),
                when,
                dto.OnFailure),
            "apply_ailment" => new ApplyAilmentEffectDefinition(
                Id(((ApplyAilmentEffectDto)dto).AilmentId),
                ((ApplyAilmentEffectDto)dto).Chance,
                ((ApplyAilmentEffectDto)dto).Duration is null
                    ? null
                    : MapDuration(((ApplyAilmentEffectDto)dto).Duration!),
                when,
                dto.OnFailure),
            "restore_resource" => MapRestore((ResourceAmountEffectDto)dto, when),
            "remove_ailment" => MapRemoveAilment((RemoveAilmentEffectDto)dto, when),
            "revive" => MapRevive((ResourceAmountEffectDto)dto, when),
            "modify_stat_stage" => MapStatStage((ModifyStatStageEffectDto)dto, when),
            "grant_charge" => MapCharge((GrantChargeEffectDto)dto, when),
            "grant_shield" => MapShield((GrantShieldEffectDto)dto, when),
            "break_affinity" => MapBreakAffinity((BreakAffinityEffectDto)dto, when),
            "override_affinity" => MapAffinity((OverrideAffinityEffectDto)dto, when),
            "remove_status_effect" => MapRemoveStatus((RemoveStatusEffectDto)dto, when),
            "reduce_resource" => MapReduce((ResourceAmountEffectDto)dto, when),
            "set_resource" => MapSet((ResourceAmountEffectDto)dto, when),
            "analyze" => new AnalyzeEffectDefinition(((AnalyzeEffectDto)dto).Layers, when, dto.OnFailure),
            "escape" => new EscapeEffectDefinition(
                Id(((EscapeEffectDto)dto).EligibilityRuleId), ((EscapeEffectDto)dto).Chance, when, dto.OnFailure),
            "custom" => new CustomEffectDefinition(
                Id(((CustomEffectDto)dto).HandlerId),
                MapParameters(((CustomEffectDto)dto).Parameters),
                when,
                dto.OnFailure),
            _ => throw new InvalidOperationException($"Unsupported effect type '{dto.Type}'.")
        };
    }

    private static DamageEffectDefinition MapDamage(DamageEffectDto dto, ConditionDefinition? when) =>
        new(dto.ElementId, dto.Power, dto.Accuracy, MapCritical(dto.Critical),
            new HitCountDefinition(dto.Hits.Minimum, dto.Hits.Maximum, dto.Hits.Distribution),
            dto.Drain, when, dto.OnFailure);

    private static RestoreResourceEffectDefinition MapRestore(ResourceAmountEffectDto dto, ConditionDefinition? when) =>
        new(Id(dto.ResourceId), MapAmount(dto.Amount), when, dto.OnFailure);

    private static RemoveAilmentEffectDefinition MapRemoveAilment(RemoveAilmentEffectDto dto, ConditionDefinition? when) =>
        new(dto.Scope, (dto.AilmentIds ?? []).Select(Id), (dto.AilmentGroupIds ?? []).Select(Id), when, dto.OnFailure);

    private static ReviveEffectDefinition MapRevive(ResourceAmountEffectDto dto, ConditionDefinition? when) =>
        new(Id(dto.ResourceId), MapAmount(dto.Amount), when, dto.OnFailure);

    private static ModifyStatStageEffectDefinition MapStatStage(
        ModifyStatStageEffectDto dto, ConditionDefinition? when) =>
        new(dto.ModifierTrackIds.Select(Id), dto.StageDelta,
            dto.Duration is null ? null : MapDuration(dto.Duration), when, dto.OnFailure);

    private static GrantChargeEffectDefinition MapCharge(GrantChargeEffectDto dto, ConditionDefinition? when) =>
        new(dto.Charge, dto.Multiplier, dto.Duration is null ? null : MapDuration(dto.Duration), when, dto.OnFailure);

    private static GrantShieldEffectDefinition MapShield(GrantShieldEffectDto dto, ConditionDefinition? when) =>
        new(dto.Shield, dto.Duration is null ? null : MapDuration(dto.Duration), when, dto.OnFailure);

    private static BreakAffinityEffectDefinition MapBreakAffinity(
        BreakAffinityEffectDto dto, ConditionDefinition? when) =>
        new(dto.ElementIds, MapDuration(dto.Duration), when, dto.OnFailure);

    private static OverrideAffinityEffectDefinition MapAffinity(
        OverrideAffinityEffectDto dto, ConditionDefinition? when) =>
        new(dto.ElementIds, dto.AffinityId, MapDuration(dto.Duration), when, dto.OnFailure);

    private static RemoveStatusEffectDefinition MapRemoveStatus(
        RemoveStatusEffectDto dto, ConditionDefinition? when) =>
        new(dto.StatusKinds, dto.StatusIds.Select(Id), when, dto.OnFailure);

    private static ReduceResourceEffectDefinition MapReduce(ResourceAmountEffectDto dto, ConditionDefinition? when) =>
        new(Id(dto.ResourceId), MapAmount(dto.Amount), dto.CanReduceToZero, when, dto.OnFailure);

    private static SetResourceEffectDefinition MapSet(ResourceAmountEffectDto dto, ConditionDefinition? when) =>
        new(Id(dto.ResourceId), MapAmount(dto.Amount), when, dto.OnFailure);

    private static PassiveTriggerDefinition MapTrigger(PassiveTriggerDto dto) =>
        new(Id(dto.EventId), dto.Effects.Select(MapEffect), dto.When is null ? null : MapCondition(dto.When));

    private static RuleModifierDefinition MapModifier(RuleModifierDto dto)
    {
        ConditionDefinition? when = dto.When is null ? null : MapCondition(dto.When);
        return dto switch
        {
            NumericRuleModifierDto numeric => new NumericRuleModifierDefinition(
                ParseNumericModifierType(numeric.Type), numeric.Operation, numeric.Value, when),
            ElementalAffinityRuleModifierDto affinity => new ElementalAffinityRuleModifierDefinition(
                affinity.ElementId, affinity.AffinityId, when),
            AilmentResistanceRuleModifierDto resistance => new AilmentResistanceRuleModifierDefinition(
                Id(resistance.AilmentId), resistance.Resistance, when),
            BasicAttackRuleModifierDto attack => new BasicAttackRuleModifierDefinition(
                attack.ElementId,
                attack.Targeting is null ? null : MapTargeting(attack.Targeting),
                attack.Drain,
                when),
            _ => throw new InvalidOperationException($"Unsupported modifier type '{dto.Type}'.")
        };
    }

    private static NumericRuleModifierType ParseNumericModifierType(string type) => type switch
    {
        "damage_dealt" => NumericRuleModifierType.DamageDealt,
        "damage_taken" => NumericRuleModifierType.DamageTaken,
        "accuracy" => NumericRuleModifierType.Accuracy,
        "evasion" => NumericRuleModifierType.Evasion,
        "critical_chance" => NumericRuleModifierType.CriticalChance,
        "ailment_infliction" => NumericRuleModifierType.AilmentInfliction,
        "healing_received" => NumericRuleModifierType.HealingReceived,
        "healing_given" => NumericRuleModifierType.HealingGiven,
        "resource_cost" => NumericRuleModifierType.ResourceCost,
        "maximum_resource" => NumericRuleModifierType.MaximumResource,
        "experience_gain" => NumericRuleModifierType.ExperienceGain,
        _ => throw new InvalidOperationException($"Unsupported numeric modifier type '{type}'.")
    };

    private static EntityDefinition MapEntity(EntityDto dto) =>
        new(
            Id(dto.Id), dto.DisplayName, dto.Description, Id(dto.EntityKind), Id(dto.RaceId), dto.Rank, dto.BaseLevel,
            new EntityCapabilitiesDefinition(
                dto.Capabilities.Recruitable, dto.Capabilities.FusionEligible, dto.Capabilities.CompendiumEligible),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(
                    dto.InheritanceRules.GroupPolicy.Mode, dto.InheritanceRules.GroupPolicy.GroupIds),
                (dto.InheritanceRules.BlockedSkillIds ?? []).Select(Id),
                (dto.InheritanceRules.AllowedSkillIds ?? []).Select(Id)),
            dto.Stats.Select(pair => KeyValuePair.Create(Id(pair.Key), pair.Value)),
            (dto.ElementalAffinities ?? []).Select(pair => KeyValuePair.Create(ParseElement(pair.Key), pair.Value)),
            (dto.AilmentResistances ?? []).Select(pair => KeyValuePair.Create(Id(pair.Key), pair.Value)),
            (dto.InstantDeathResistances ?? []).Select(
                pair => KeyValuePair.Create(ParseInstantDeathChannel(pair.Key), pair.Value)),
            (dto.BaseSkillIds ?? []).Select(Id),
            (dto.SkillUnlocks ?? []).Select(unlock => new SkillUnlockDefinition(unlock.Level, Id(unlock.SkillId))));

    private static RaceDefinition MapRace(RaceDto dto) =>
        new(Id(dto.Id), dto.DisplayName, (dto.AlignmentIds ?? []).Select(Id),
            dto.NegotiationPersonalityId is null ? null : Id(dto.NegotiationPersonalityId));

    private static AilmentDefinition MapAilment(AilmentDto dto) =>
        new(
            Id(dto.Id), dto.DisplayName, dto.Description, MapDuration(dto.DefaultDuration),
            MapTurnBehavior(dto.TurnBehavior),
            new AilmentModifiersDefinition(
                dto.Modifiers.EvasionMultiplier,
                dto.Modifiers.CriticalChanceTakenBonus,
                dto.Modifiers.DamageTakenMultiplier,
                dto.Modifiers.DamageDealtMultiplier,
                dto.Modifiers.IsRigidBody),
            new AilmentRecoveryDefinition(
                dto.Recovery.Natural is null
                    ? null
                    : new NaturalAilmentRecoveryDefinition(
                        dto.Recovery.Natural.BaseChance,
                        Id(dto.Recovery.Natural.StatId),
                        dto.Recovery.Natural.StatMultiplier),
                (dto.Recovery.RemoveOnEvents ?? []).Select(Id)),
            (dto.GroupIds ?? []).Select(Id),
            dto.ExclusivityGroupId is null ? null : Id(dto.ExclusivityGroupId),
            (dto.Triggers ?? []).Select(MapTrigger));

    private static AilmentTurnBehaviorDefinition MapTurnBehavior(AilmentTurnBehaviorDto dto) => dto.Type switch
    {
        "normal" => new NormalAilmentTurnBehaviorDefinition(),
        "skip" => new SkipAilmentTurnBehaviorDefinition(),
        "limited_actions" => new LimitedActionsAilmentTurnBehaviorDefinition(
            (((LimitedActionsAilmentTurnBehaviorDto)dto).AllowedActionIds ?? []).Select(Id)),
        "chance_skip" => new ChanceSkipAilmentTurnBehaviorDefinition(
            ((ChanceSkipAilmentTurnBehaviorDto)dto).SkipChance),
        "chance_skip_or_flee" => new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(
            ((ChanceSkipOrFleeAilmentTurnBehaviorDto)dto).SkipChance,
            ((ChanceSkipOrFleeAilmentTurnBehaviorDto)dto).FleeChance,
            ((ChanceSkipOrFleeAilmentTurnBehaviorDto)dto).DemonFleeOutcome),
        "forced_basic_attack" => new ForcedBasicAttackAilmentTurnBehaviorDefinition(),
        "confused_action" => new ConfusedActionAilmentTurnBehaviorDefinition(),
        "custom" => new CustomAilmentTurnBehaviorDefinition(
            Id(((CustomAilmentTurnBehaviorDto)dto).HandlerId),
            MapParameters(((CustomAilmentTurnBehaviorDto)dto).Parameters)),
        _ => throw new InvalidOperationException($"Unsupported ailment turn behaviour '{dto.Type}'.")
    };

    private static IEnumerable<KeyValuePair<string, object?>> MapParameters(
        IReadOnlyDictionary<string, JsonElement>? parameters) =>
        (parameters ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal))
            .Select(pair => KeyValuePair.Create(pair.Key, MapJsonValue(pair.Value)));

    private static object? MapJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out long integer) => integer,
        JsonValueKind.Number => MapJsonNumber(value),
        JsonValueKind.Array => Array.AsReadOnly(value.EnumerateArray().Select(MapJsonValue).ToArray()),
        JsonValueKind.Object => new ReadOnlyDictionary<string, object?>(
            value.EnumerateObject().ToDictionary(
                property => property.Name,
                property => MapJsonValue(property.Value),
                StringComparer.Ordinal)),
        _ => throw new InvalidOperationException($"Unsupported custom parameter token '{value.ValueKind}'.")
    };

    private static decimal MapJsonNumber(JsonElement value)
    {
        if (value.TryGetDecimal(out decimal number))
        {
            return number;
        }

        throw new ArgumentException("Custom parameter numbers must fit in a signed 64-bit integer or decimal value.");
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static string NormalizePackId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Content pack ID cannot be empty.", nameof(value));
        }

        string normalized = value.Trim().ToLowerInvariant();
        foreach (string segment in normalized.Split('.'))
        {
            _ = ContentId.Parse(segment);
        }

        return normalized;
    }

    private static DamageElement ParseElement(string value) => value switch
    {
        "physical" => DamageElement.Physical,
        "fire" => DamageElement.Fire,
        "ice" => DamageElement.Ice,
        "electric" => DamageElement.Electric,
        "wind" => DamageElement.Wind,
        "light" => DamageElement.Light,
        "dark" => DamageElement.Dark,
        "almighty" => DamageElement.Almighty,
        _ => throw new ArgumentException($"Unknown damage element '{value}'.", nameof(value))
    };

    private static InstantDeathChannel ParseInstantDeathChannel(string value) => value switch
    {
        "light" => InstantDeathChannel.Light,
        "dark" => InstantDeathChannel.Dark,
        _ => throw new ArgumentException($"Unknown instant-death channel '{value}'.", nameof(value))
    };
}
