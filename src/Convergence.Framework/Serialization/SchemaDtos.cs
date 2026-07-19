using System.Text.Json;
using System.Text.Json.Serialization;
using Convergence.Content;

namespace Convergence.Serialization;

internal sealed class ManifestDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public List<ManifestDependencyDto> Dependencies { get; init; } = [];
    public required List<ManifestDocumentDto> Documents { get; init; }
}

internal sealed class ManifestDependencyDto
{
    public required string Id { get; init; }
    public required string Version { get; init; }
}

internal sealed class ManifestDocumentDto
{
    public required string Type { get; init; }
    public required string Path { get; init; }
}

internal sealed class SkillDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<SkillDto> Skills { get; init; }
}

internal sealed class EntityDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<EntityDto> Entities { get; init; }
}

internal sealed class RaceDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<RaceDto> Races { get; init; }
}

internal sealed class AilmentDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<AilmentDto> Ailments { get; init; }
}

internal sealed class ItemDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<ItemDto> Items { get; init; }
}

internal sealed class EquipmentDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<EquipmentDto> Equipment { get; init; }
}

internal sealed class ShopDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<ShopCatalogDto> Shops { get; init; }
}

internal sealed class NegotiationDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<NegotiationDto> Negotiations { get; init; }
}

internal sealed class EncounterDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<EncounterDto> Encounters { get; init; }
}

internal sealed class DungeonDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<DungeonDto> Dungeons { get; init; }
}

internal sealed class FusionDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<FusionRecipeDto> FusionRecipes { get; init; }
}

internal sealed class RulesetDocumentDto
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }
    public required int SchemaVersion { get; init; }
    public required List<RulesetDto> Rulesets { get; init; }
}

internal sealed class ItemDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required ItemKind ItemKind { get; init; }
    public required int StackLimit { get; init; }
    public required decimal BaseValue { get; init; }
    public ItemUsageDto? Usage { get; init; }
}

internal sealed class ItemUsageDto
{
    public required List<string> Contexts { get; init; }
    public required ItemConsumptionMode ConsumeOn { get; init; }
    public required TargetingDto Targeting { get; init; }
    public required List<EffectDto> Effects { get; init; }
}

internal sealed class EquipmentDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required EquipmentSlot Slot { get; init; }
    public required decimal BaseValue { get; init; }
    public List<string> GrantedSkillIds { get; init; } = [];
    public EquipmentWeaponProfileDto? Weapon { get; init; }
    public EquipmentArmorProfileDto? Armor { get; init; }
    public EquipmentBootsProfileDto? Boots { get; init; }
    public EquipmentAccessoryProfileDto? Accessory { get; init; }
}

internal sealed class EquipmentWeaponProfileDto
{
    public required EquipmentBasicAttackDto BasicAttack { get; init; }
}

internal sealed class EquipmentBasicAttackDto
{
    public required DamageElement Element { get; init; }
    public required int Power { get; init; }
    public required int Accuracy { get; init; }
    public required CriticalDto Critical { get; init; }
    public required bool IsLongRange { get; init; }
}

internal sealed class EquipmentArmorProfileDto
{
    public required int Defense { get; init; }
    public required int Evasion { get; init; }
}

internal sealed class EquipmentBootsProfileDto
{
    public required int Evasion { get; init; }
}

internal sealed class EquipmentAccessoryProfileDto
{
    public List<StatModifierDto> StatModifiers { get; init; } = [];
}

internal sealed class StatModifierDto
{
    public required string StatId { get; init; }
    public required int Value { get; init; }
}

internal sealed class ShopCatalogDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string CategoryId { get; init; }
    public List<string> AvailabilityContexts { get; init; } = [];
    public required List<ShopOfferDto> Offers { get; init; }
}

internal sealed class ShopOfferDto
{
    public required ShopContentKind ContentKind { get; init; }
    public required string ContentId { get; init; }
    public required ShopPriceDto Price { get; init; }
    public required ShopStockDto Stock { get; init; }
}

internal sealed class ShopPriceDto
{
    public required ShopPriceKind Kind { get; init; }
    public decimal? BasePrice { get; init; }
    public string? PricingPolicyId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal sealed class ShopStockDto
{
    public required ShopStockKind Kind { get; init; }
    public int? Quantity { get; init; }
    public string? StockPolicyId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal sealed class NegotiationDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string PersonalityId { get; init; }
    public required List<NegotiationQuestionDto> Questions { get; init; }
    public List<string> FamiliarDialogueLines { get; init; } = [];
    public List<NegotiationDemandDto> Demands { get; init; } = [];
    public List<string> DefaultRaceIds { get; init; } = [];
    public List<string> DefaultEntityIds { get; init; } = [];
}

internal sealed class NegotiationQuestionDto
{
    public required string Text { get; init; }
    public required List<NegotiationAnswerDto> Answers { get; init; }
}

internal sealed class NegotiationAnswerDto
{
    public required string Text { get; init; }
    public required int Score { get; init; }
}

internal sealed class NegotiationDemandDto
{
    public required string DemandId { get; init; }
    public required int Weight { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal sealed class EncounterDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public string? EnvironmentId { get; init; }
    public required List<EncounterFormationDto> Formations { get; init; }
}

internal sealed class EncounterFormationDto
{
    public required int Weight { get; init; }
    public required bool IsBoss { get; init; }
    public required List<EncounterMemberDto> Members { get; init; }
    public string? RewardPolicyId { get; init; }
    public Dictionary<string, JsonElement> RewardParameters { get; init; } = [];
}

internal sealed class EncounterMemberDto
{
    public required string EntityId { get; init; }
    public required int Level { get; init; }
    public int Count { get; init; } = 1;
}

internal sealed class DungeonDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required List<DungeonBlockDto> Blocks { get; init; }
}

internal sealed class DungeonBlockDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required int StartFloor { get; init; }
    public required int EndFloor { get; init; }
    public List<string> EncounterPoolIds { get; init; } = [];
    public List<DungeonFixedFloorDto> FixedFloors { get; init; } = [];
}

internal sealed class DungeonFixedFloorDto
{
    public required int Floor { get; init; }
    public required DungeonFixedFloorKind Kind { get; init; }
    public required string Description { get; init; }
    public string? EncounterId { get; init; }
    public string? TransitionRuleId { get; init; }
    public string? BarrierRuleId { get; init; }
    public bool HasTerminal { get; init; }
}

internal sealed class FusionRecipeDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required List<FusionParentSelectorDto> Parents { get; init; }
    public required FusionResultDto Result { get; init; }
    public string? AccidentPolicyId { get; init; }
    public string? MutationPolicyId { get; init; }
}

internal sealed class FusionParentSelectorDto
{
    public required FusionParentSelectorKind Kind { get; init; }
    public required string Id { get; init; }
    public required FusionParentRole Role { get; init; }
}

internal sealed class FusionResultDto
{
    public required FusionResultOperationKind Operation { get; init; }
    public string? ResultEntityId { get; init; }
    public int? RankShift { get; init; }
    public string? PolicyId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal sealed class RulesetDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required RulesetCategory Category { get; init; }
    public required string PolicyId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal sealed class SkillDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required SkillActivation Activation { get; init; }
    public SkillMenuGroup? MenuGroup { get; init; }
    public required InheritanceGroup InheritanceGroupId { get; init; }
    public required SkillInheritanceDto Inheritance { get; init; }
    public SkillMutationDto? Mutation { get; init; }
    public List<SkillCostDto> Costs { get; init; } = [];
    public TargetingDto? Targeting { get; init; }
    public List<EffectDto> Effects { get; init; } = [];
    public List<PassiveTriggerDto> Triggers { get; init; } = [];
    public List<RuleModifierDto> Modifiers { get; init; } = [];
    public SkillAvailabilityDto? Availability { get; init; }
}

internal sealed class SkillInheritanceDto
{
    public required bool IsInheritable { get; init; }
    public List<string> ExclusiveOwnerEntityIds { get; init; } = [];
}

internal sealed class SkillMutationDto
{
    public required string FamilyId { get; init; }
    public required int Tier { get; init; }
}

internal sealed class SkillAvailabilityDto
{
    public required List<string> Contexts { get; init; }
}

internal sealed class SkillCostDto
{
    public required string ResourceId { get; init; }
    public required AmountDto Amount { get; init; }
    public bool CanReduceToZero { get; init; }
}

internal sealed class TargetingDto
{
    public required TargetRelation Relation { get; init; }
    public required TargetSelection Selection { get; init; }
    public required TargetLifeState LifeState { get; init; }
    public required bool AllowSelf { get; init; }
    public TargetCountDto? Count { get; init; }
}

internal sealed class TargetCountDto
{
    public required int Minimum { get; init; }
    public required int Maximum { get; init; }
}

internal abstract class AmountDto
{
    public required string Type { get; init; }
}

internal sealed class ValueAmountDto : AmountDto
{
    public required decimal Value { get; init; }
}

internal sealed class PowerAmountDto : AmountDto
{
    public required int Power { get; init; }
}

internal sealed class MarkerAmountDto : AmountDto;

internal sealed class FormulaAmountDto : AmountDto
{
    public required string FormulaId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal abstract class DurationDto
{
    public required string Type { get; init; }
}

internal sealed class MarkerDurationDto : DurationDto;

internal sealed class TurnDurationDto : DurationDto
{
    public required int Value { get; init; }
    public required string Tick { get; init; }
    public required bool SuspendWhileReserve { get; init; }
}

internal sealed class PhaseDurationDto : DurationDto
{
    public required string PhaseId { get; init; }
}

internal abstract class CriticalDto
{
    public required string Mode { get; init; }
}

internal sealed class NeverCriticalDto : CriticalDto;

internal sealed class ChanceCriticalDto : CriticalDto
{
    public required int Chance { get; init; }
}

internal sealed class HitCountDto
{
    public required int Minimum { get; init; }
    public required int Maximum { get; init; }
    public HitDistribution Distribution { get; init; } = HitDistribution.Fixed;
}

internal abstract class InstantDeathResistanceCheckDto
{
    public required string Mode { get; init; }
}

internal sealed class ChannelInstantDeathResistanceCheckDto : InstantDeathResistanceCheckDto
{
    public required InstantDeathChannel ChannelId { get; init; }
}

internal sealed class NoInstantDeathResistanceCheckDto : InstantDeathResistanceCheckDto;

internal abstract class ConditionDto
{
    public string? Type { get; init; }
}

internal sealed class AllConditionDto : ConditionDto
{
    public required List<ConditionDto> All { get; init; }
}

internal sealed class AnyConditionDto : ConditionDto
{
    public required List<ConditionDto> Any { get; init; }
}

internal sealed class NotConditionDto : ConditionDto
{
    public required ConditionDto Not { get; init; }
}

internal sealed class ResourcePercentageConditionDto : ConditionDto
{
    public required string ResourceId { get; init; }
    public required NumericComparison Comparison { get; init; }
    public required decimal Value { get; init; }
}

internal sealed class HasAilmentConditionDto : ConditionDto
{
    public required List<string> AilmentIds { get; init; }
}

internal sealed class HasSkillConditionDto : ConditionDto
{
    public required string SkillId { get; init; }
}

internal sealed class HasBuffConditionDto : ConditionDto
{
    public required string ModifierTrackId { get; init; }
}

internal sealed class HasAffinityConditionDto : ConditionDto
{
    public required DamageElement ElementId { get; init; }
    public required ElementalAffinity AffinityId { get; init; }
}

internal sealed class HasCapabilityConditionDto : ConditionDto
{
    public required string CapabilityId { get; init; }
}

internal sealed class LifeStateConditionDto : ConditionDto
{
    public required TargetLifeState LifeState { get; init; }
}

internal sealed class AllowedIdsConditionDto : ConditionDto
{
    public required List<string> Allowed { get; init; }
}

internal sealed class PartySizeConditionDto : ConditionDto
{
    public required NumericComparison Comparison { get; init; }
    public required int Value { get; init; }
}

internal sealed class ChanceConditionDto : ConditionDto
{
    public required int Chance { get; init; }
}

internal sealed class EffectElementConditionDto : ConditionDto
{
    public required DamageElement ElementId { get; init; }
}

internal sealed class CustomConditionDto : ConditionDto
{
    public required string HandlerId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal abstract class EffectDto
{
    public required string Type { get; init; }
    public ConditionDto? When { get; init; }
    public EffectFailurePolicy OnFailure { get; init; } = EffectFailurePolicy.Continue;
}

internal sealed class DamageEffectDto : EffectDto
{
    public required DamageElement ElementId { get; init; }
    public required int Power { get; init; }
    public required int Accuracy { get; init; }
    public required CriticalDto Critical { get; init; }
    public required HitCountDto Hits { get; init; }
    public DamageDrainMode Drain { get; init; } = DamageDrainMode.None;
}

internal sealed class InstantKillEffectDto : EffectDto
{
    public required int Chance { get; init; }
    public required InstantDeathResistanceCheckDto ResistanceCheck { get; init; }
}

internal sealed class ApplyAilmentEffectDto : EffectDto
{
    public required string AilmentId { get; init; }
    public required int Chance { get; init; }
    public DurationDto? Duration { get; init; }
}

internal sealed class ResourceAmountEffectDto : EffectDto
{
    public required string ResourceId { get; init; }
    public required AmountDto Amount { get; init; }
    public bool CanReduceToZero { get; init; }
}

internal sealed class RemoveAilmentEffectDto : EffectDto
{
    public required AilmentRemovalScope Scope { get; init; }
    public List<string> AilmentIds { get; init; } = [];
    public List<string> AilmentGroupIds { get; init; } = [];
}

internal sealed class ModifyStatStageEffectDto : EffectDto
{
    public required List<string> ModifierTrackIds { get; init; }
    public required int StageDelta { get; init; }
    public DurationDto? Duration { get; init; }
}

internal sealed class GrantChargeEffectDto : EffectDto
{
    public required ChargeKind Charge { get; init; }
    public required decimal Multiplier { get; init; }
    public DurationDto? Duration { get; init; }
}

internal sealed class GrantShieldEffectDto : EffectDto
{
    public required ShieldKind Shield { get; init; }
    public DurationDto? Duration { get; init; }
}

internal sealed class BreakAffinityEffectDto : EffectDto
{
    public required List<DamageElement> ElementIds { get; init; }
    public required DurationDto Duration { get; init; }
}

internal sealed class OverrideAffinityEffectDto : EffectDto
{
    public required List<DamageElement> ElementIds { get; init; }
    public required ElementalAffinity AffinityId { get; init; }
    public required DurationDto Duration { get; init; }
}

internal sealed class RemoveStatusEffectDto : EffectDto
{
    public required List<StatusEffectKind> StatusKinds { get; init; }
    public List<string> StatusIds { get; init; } = [];
}

internal sealed class AnalyzeEffectDto : EffectDto
{
    public required List<AnalysisLayer> Layers { get; init; }
}

internal sealed class EscapeEffectDto : EffectDto
{
    public required string EligibilityRuleId { get; init; }
    public int? Chance { get; init; }
}

internal sealed class CustomEffectDto : EffectDto
{
    public required string HandlerId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}

internal sealed class PassiveTriggerDto
{
    [JsonPropertyName("event")]
    public required string EventId { get; init; }
    public ConditionDto? When { get; init; }
    public required List<EffectDto> Effects { get; init; }
}

internal abstract class RuleModifierDto
{
    public required string Type { get; init; }
    public ConditionDto? When { get; init; }
}

internal sealed class NumericRuleModifierDto : RuleModifierDto
{
    public required ModifierOperation Operation { get; init; }
    public required decimal Value { get; init; }
}

internal sealed class ElementalAffinityRuleModifierDto : RuleModifierDto
{
    public required DamageElement ElementId { get; init; }
    public required ElementalAffinity AffinityId { get; init; }
}

internal sealed class AilmentResistanceRuleModifierDto : RuleModifierDto
{
    public required string AilmentId { get; init; }
    public required ResistanceLevel Resistance { get; init; }
}

internal sealed class BasicAttackRuleModifierDto : RuleModifierDto
{
    public DamageElement? ElementId { get; init; }
    public TargetingDto? Targeting { get; init; }
    public DamageDrainMode? Drain { get; init; }
}

internal sealed class EntityDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string EntityKind { get; init; }
    public required string RaceId { get; init; }
    public required int Rank { get; init; }
    public required int BaseLevel { get; init; }
    public required EntityCapabilitiesDto Capabilities { get; init; }
    public required EntityInheritanceRulesDto InheritanceRules { get; init; }
    public required Dictionary<string, int> Stats { get; init; }
    public Dictionary<string, ElementalAffinity> ElementalAffinities { get; init; } = [];
    public Dictionary<string, ResistanceLevel> AilmentResistances { get; init; } = [];
    public Dictionary<string, ResistanceLevel> InstantDeathResistances { get; init; } = [];
    public List<string> BaseSkillIds { get; init; } = [];
    public List<SkillUnlockDto> SkillUnlocks { get; init; } = [];
}

internal sealed class EntityCapabilitiesDto
{
    public required bool Recruitable { get; init; }
    public required bool FusionEligible { get; init; }
    public required bool CompendiumEligible { get; init; }
}

internal sealed class EntityInheritanceRulesDto
{
    public required InheritanceGroupPolicyDto GroupPolicy { get; init; }
    public List<string> BlockedSkillIds { get; init; } = [];
    public List<string> AllowedSkillIds { get; init; } = [];
}

internal sealed class InheritanceGroupPolicyDto
{
    public required InheritanceGroupPolicyMode Mode { get; init; }
    public required List<InheritanceGroup> GroupIds { get; init; }
}

internal sealed class SkillUnlockDto
{
    public required int Level { get; init; }
    public required string SkillId { get; init; }
}

internal sealed class RaceDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public List<string> AlignmentIds { get; init; } = [];
    public string? NegotiationPersonalityId { get; init; }
}

internal sealed class AilmentDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public List<string> GroupIds { get; init; } = [];
    public string? ExclusivityGroupId { get; init; }
    public required DurationDto DefaultDuration { get; init; }
    public required AilmentTurnBehaviorDto TurnBehavior { get; init; }
    public required AilmentModifiersDto Modifiers { get; init; }
    public List<PassiveTriggerDto> Triggers { get; init; } = [];
    public required AilmentRecoveryDto Recovery { get; init; }
}

internal sealed class AilmentModifiersDto
{
    public required decimal EvasionMultiplier { get; init; }
    public required int CriticalChanceTakenBonus { get; init; }
    public required decimal DamageTakenMultiplier { get; init; }
    public required decimal DamageDealtMultiplier { get; init; }
    public required bool IsRigidBody { get; init; }
}

internal sealed class AilmentRecoveryDto
{
    public NaturalAilmentRecoveryDto? Natural { get; init; }
    public List<string> RemoveOnEvents { get; init; } = [];
}

internal sealed class NaturalAilmentRecoveryDto
{
    public required int BaseChance { get; init; }
    public required string StatId { get; init; }
    public required decimal StatMultiplier { get; init; }
}

internal abstract class AilmentTurnBehaviorDto
{
    public required string Type { get; init; }
}

internal sealed class MarkerAilmentTurnBehaviorDto : AilmentTurnBehaviorDto;

internal sealed class LimitedActionsAilmentTurnBehaviorDto : AilmentTurnBehaviorDto
{
    public required List<string> AllowedActionIds { get; init; }
}

internal sealed class ChanceSkipAilmentTurnBehaviorDto : AilmentTurnBehaviorDto
{
    public required int SkipChance { get; init; }
}

internal sealed class ChanceSkipOrFleeAilmentTurnBehaviorDto : AilmentTurnBehaviorDto
{
    public required int SkipChance { get; init; }
    public required int FleeChance { get; init; }
    public required CompanionFleeOutcome CompanionFleeOutcome { get; init; }
}

internal sealed class CustomAilmentTurnBehaviorDto : AilmentTurnBehaviorDto
{
    public required string HandlerId { get; init; }
    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}
