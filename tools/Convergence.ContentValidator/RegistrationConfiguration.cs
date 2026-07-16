using System.Text.Json;
using System.Text.Json.Serialization;
using Convergence.Content;
using Convergence.Validation;

namespace Convergence.ContentValidator;

internal static class RegistrationConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static SkillSystemRegistrationSnapshot Load(string path)
    {
        string source = Path.GetFullPath(path);
        RegistrationDocument document = JsonSerializer.Deserialize<RegistrationDocument>(
            File.ReadAllText(source),
            JsonOptions) ?? throw new InvalidDataException("Registration profile is empty.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Registration profile schema version must be 1, but was {document.SchemaVersion}.");
        }

        RegistrationVocabulary vocabulary = document.Registrations;
        var builder = new SkillSystemRegistrationBuilder()
            .RegisterContext(vocabulary.Contexts.ToArray())
            .RegisterResource(vocabulary.Resources.ToArray())
            .RegisterStat(vocabulary.Stats.ToArray())
            .RegisterModifierTrack(vocabulary.ModifierTracks.ToArray())
            .RegisterEvent(vocabulary.Events.ToArray())
            .RegisterPhase(vocabulary.Phases.ToArray())
            .RegisterEntityKind(vocabulary.EntityKinds.ToArray())
            .RegisterAlignment(vocabulary.Alignments.ToArray())
            .RegisterNegotiationPersonality(vocabulary.NegotiationPersonalities.ToArray())
            .RegisterAilmentGroup(vocabulary.AilmentGroups.ToArray())
            .RegisterBattleKind(vocabulary.BattleKinds.ToArray())
            .RegisterMoonPhase(vocabulary.MoonPhases.ToArray())
            .RegisterCapability(vocabulary.Capabilities.ToArray())
            .RegisterAction(vocabulary.Actions.ToArray())
            .RegisterStatus(vocabulary.Statuses.ToArray())
            .RegisterEscapeRule(vocabulary.EscapeRules.ToArray())
            .RegisterShopCategory(vocabulary.ShopCategories.ToArray())
            .RegisterNegotiationDemand(vocabulary.NegotiationDemands.ToArray())
            .RegisterEncounterEnvironment(vocabulary.EncounterEnvironments.ToArray())
            .RegisterPolicy(vocabulary.Policies.ToArray());

        SupportBuiltInDefinitions(builder);
        foreach (string id in vocabulary.Formulas)
        {
            builder.RegisterFormula(id, AcceptAnyParameters.Instance);
        }
        foreach (string id in vocabulary.CustomEffects)
        {
            builder.RegisterCustomEffect(id, AcceptAnyParameters.Instance);
        }
        foreach (string id in vocabulary.CustomConditions)
        {
            builder.RegisterCustomCondition(id, AcceptAnyParameters.Instance);
        }
        foreach (string id in vocabulary.CustomAilmentBehaviors)
        {
            builder.RegisterCustomAilmentBehavior(id, AcceptAnyParameters.Instance);
        }

        return builder.Build();
    }

    private static void SupportBuiltInDefinitions(SkillSystemRegistrationBuilder builder)
    {
        builder
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<InstantKillEffectDefinition>()
            .SupportEffect<ApplyAilmentEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<ModifyStatStageEffectDefinition>()
            .SupportEffect<GrantChargeEffectDefinition>()
            .SupportEffect<GrantShieldEffectDefinition>()
            .SupportEffect<BreakAffinityEffectDefinition>()
            .SupportEffect<OverrideAffinityEffectDefinition>()
            .SupportEffect<RemoveStatusEffectDefinition>()
            .SupportEffect<ReduceResourceEffectDefinition>()
            .SupportEffect<SetResourceEffectDefinition>()
            .SupportEffect<AnalyzeEffectDefinition>()
            .SupportEffect<EscapeEffectDefinition>()
            .SupportEffect<CustomEffectDefinition>()
            .SupportCondition<AllConditionDefinition>()
            .SupportCondition<AnyConditionDefinition>()
            .SupportCondition<NotConditionDefinition>()
            .SupportCondition<ResourcePercentageConditionDefinition>()
            .SupportCondition<HasAilmentConditionDefinition>()
            .SupportCondition<HasSkillConditionDefinition>()
            .SupportCondition<HasBuffConditionDefinition>()
            .SupportCondition<HasAffinityConditionDefinition>()
            .SupportCondition<HasCapabilityConditionDefinition>()
            .SupportCondition<LifeStateConditionDefinition>()
            .SupportCondition<BattleKindConditionDefinition>()
            .SupportCondition<MoonPhaseConditionDefinition>()
            .SupportCondition<PartySizeConditionDefinition>()
            .SupportCondition<ChanceConditionDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportCondition<CustomConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportModifier<ElementalAffinityRuleModifierDefinition>()
            .SupportModifier<AilmentResistanceRuleModifierDefinition>()
            .SupportModifier<BasicAttackRuleModifierDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<LimitedActionsAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipOrFleeAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ForcedBasicAttackAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ConfusedActionAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<CustomAilmentTurnBehaviorDefinition>();
    }

    private sealed class AcceptAnyParameters : IContentParameterValidator
    {
        public static AcceptAnyParameters Instance { get; } = new();

        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) => [];
    }

    private sealed record RegistrationDocument
    {
        public required int SchemaVersion { get; init; }
        public required RegistrationVocabulary Registrations { get; init; }
    }

    private sealed record RegistrationVocabulary
    {
        public List<string> Contexts { get; init; } = [];
        public List<string> Resources { get; init; } = [];
        public List<string> Stats { get; init; } = [];
        public List<string> ModifierTracks { get; init; } = [];
        public List<string> Events { get; init; } = [];
        public List<string> Phases { get; init; } = [];
        public List<string> EntityKinds { get; init; } = [];
        public List<string> Alignments { get; init; } = [];
        public List<string> NegotiationPersonalities { get; init; } = [];
        public List<string> AilmentGroups { get; init; } = [];
        public List<string> BattleKinds { get; init; } = [];
        public List<string> MoonPhases { get; init; } = [];
        public List<string> Capabilities { get; init; } = [];
        public List<string> Actions { get; init; } = [];
        public List<string> Statuses { get; init; } = [];
        public List<string> EscapeRules { get; init; } = [];
        public List<string> ShopCategories { get; init; } = [];
        public List<string> NegotiationDemands { get; init; } = [];
        public List<string> EncounterEnvironments { get; init; } = [];
        public List<string> Policies { get; init; } = [];
        public List<string> Formulas { get; init; } = [];
        public List<string> CustomEffects { get; init; } = [];
        public List<string> CustomConditions { get; init; } = [];
        public List<string> CustomAilmentBehaviors { get; init; } = [];
    }
}
