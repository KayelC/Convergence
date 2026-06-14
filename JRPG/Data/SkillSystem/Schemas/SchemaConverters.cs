using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JRPGPrototype.Data.SkillSystem.Schemas;

internal sealed class SchemaDiscriminatorException : JsonException
{
    public SchemaDiscriminatorException(string unionName, string? discriminator)
        : base(discriminator is null
            ? $"{unionName} requires a string discriminator."
            : $"Unknown {unionName} discriminator '{discriminator}'.")
    {
        Discriminator = discriminator;
    }

    public string? Discriminator { get; }
}

internal sealed class StrictSnakeCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, TEnum> Values = Enum.GetValues<TEnum>()
        .ToDictionary(
            value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()),
            value => value,
            StringComparer.Ordinal);

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"{typeof(TEnum).Name} must be a string.");
        }

        string? value = reader.GetString();
        if (value is null || !Values.TryGetValue(value, out TEnum result))
        {
            throw new JsonException($"Unknown {typeof(TEnum).Name} value '{value}'.");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        throw new NotSupportedException("The redesign schema boundary is read-only.");
}

internal abstract class DiscriminatedDtoConverter<TBase> : JsonConverter<TBase>
    where TBase : class
{
    protected abstract string UnionName { get; }
    protected abstract Type ResolveType(JsonElement element, out string? discriminator);

    public sealed override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TBase);

    public sealed override TBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"{UnionName} must be a JSON object.");
        }

        Type concreteType = ResolveType(element, out string? discriminator);
        JsonTypeInfo typeInfo = options.GetTypeInfo(concreteType);
        object? result = JsonSerializer.Deserialize(element, typeInfo);
        return result as TBase
            ?? throw new JsonException($"{UnionName} '{discriminator}' produced an invalid value.");
    }

    public sealed override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("The redesign schema boundary is read-only.");
    }

    protected Type ResolveByTypeProperty(
        JsonElement element,
        IReadOnlyDictionary<string, Type> types,
        out string? discriminator)
    {
        discriminator = ReadString(element, "type");
        if (discriminator is null || !types.TryGetValue(discriminator, out Type? concreteType))
        {
            throw new SchemaDiscriminatorException(UnionName, discriminator);
        }

        return concreteType;
    }

    protected static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Property '{propertyName}' must be a string.");
        }

        return property.GetString();
    }
}

internal sealed class AmountDtoConverter : DiscriminatedDtoConverter<AmountDto>
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["flat"] = typeof(ValueAmountDto),
        ["percent_max"] = typeof(ValueAmountDto),
        ["percent_current"] = typeof(ValueAmountDto),
        ["full"] = typeof(MarkerAmountDto),
        ["power"] = typeof(PowerAmountDto),
        ["formula"] = typeof(FormulaAmountDto)
    };

    protected override string UnionName => "amount type";
    protected override Type ResolveType(JsonElement element, out string? discriminator) =>
        ResolveByTypeProperty(element, Types, out discriminator);
}

internal sealed class DurationDtoConverter : DiscriminatedDtoConverter<DurationDto>
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["instant"] = typeof(MarkerDurationDto),
        ["turns"] = typeof(TurnDurationDto),
        ["phase"] = typeof(PhaseDurationDto),
        ["battle"] = typeof(MarkerDurationDto),
        ["permanent"] = typeof(MarkerDurationDto)
    };

    protected override string UnionName => "duration type";
    protected override Type ResolveType(JsonElement element, out string? discriminator) =>
        ResolveByTypeProperty(element, Types, out discriminator);
}

internal sealed class CriticalDtoConverter : DiscriminatedDtoConverter<CriticalDto>
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["never"] = typeof(NeverCriticalDto),
        ["chance"] = typeof(ChanceCriticalDto)
    };

    protected override string UnionName => "critical mode";

    protected override Type ResolveType(JsonElement element, out string? discriminator)
    {
        discriminator = ReadString(element, "mode");
        if (discriminator is null || !Types.TryGetValue(discriminator, out Type? concreteType))
        {
            throw new SchemaDiscriminatorException(UnionName, discriminator);
        }

        return concreteType;
    }
}

internal sealed class InstantDeathResistanceCheckDtoConverter
    : DiscriminatedDtoConverter<InstantDeathResistanceCheckDto>
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["channel"] = typeof(ChannelInstantDeathResistanceCheckDto),
        ["none"] = typeof(NoInstantDeathResistanceCheckDto)
    };

    protected override string UnionName => "instant-death resistance mode";

    protected override Type ResolveType(JsonElement element, out string? discriminator)
    {
        discriminator = ReadString(element, "mode");
        if (discriminator is null || !Types.TryGetValue(discriminator, out Type? concreteType))
        {
            throw new SchemaDiscriminatorException(UnionName, discriminator);
        }

        return concreteType;
    }
}

internal sealed class ConditionDtoConverter : DiscriminatedDtoConverter<ConditionDto>
{
    private static readonly IReadOnlyDictionary<string, Type> LeafTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["actor_resource_percentage"] = typeof(ResourcePercentageConditionDto),
        ["target_resource_percentage"] = typeof(ResourcePercentageConditionDto),
        ["actor_has_ailment"] = typeof(HasAilmentConditionDto),
        ["target_has_ailment"] = typeof(HasAilmentConditionDto),
        ["actor_has_skill"] = typeof(HasSkillConditionDto),
        ["target_has_skill"] = typeof(HasSkillConditionDto),
        ["actor_has_buff"] = typeof(HasBuffConditionDto),
        ["target_has_buff"] = typeof(HasBuffConditionDto),
        ["actor_has_affinity"] = typeof(HasAffinityConditionDto),
        ["target_has_affinity"] = typeof(HasAffinityConditionDto),
        ["actor_has_capability"] = typeof(HasCapabilityConditionDto),
        ["target_has_capability"] = typeof(HasCapabilityConditionDto),
        ["actor_life_state"] = typeof(LifeStateConditionDto),
        ["target_life_state"] = typeof(LifeStateConditionDto),
        ["battle_kind"] = typeof(AllowedIdsConditionDto),
        ["moon_phase"] = typeof(AllowedIdsConditionDto),
        ["party_size"] = typeof(PartySizeConditionDto),
        ["chance"] = typeof(ChanceConditionDto),
        ["effect_element_is"] = typeof(EffectElementConditionDto),
        ["custom"] = typeof(CustomConditionDto)
    };

    protected override string UnionName => "condition type";

    protected override Type ResolveType(JsonElement element, out string? discriminator)
    {
        bool hasAll = element.TryGetProperty("all", out _);
        bool hasAny = element.TryGetProperty("any", out _);
        bool hasNot = element.TryGetProperty("not", out _);
        bool hasType = element.TryGetProperty("type", out _);
        int shapeCount = (hasAll ? 1 : 0) + (hasAny ? 1 : 0) + (hasNot ? 1 : 0) + (hasType ? 1 : 0);
        if (shapeCount != 1)
        {
            throw new JsonException("A condition must contain exactly one of 'all', 'any', 'not', or 'type'.");
        }

        if (hasAll)
        {
            discriminator = "all";
            return typeof(AllConditionDto);
        }

        if (hasAny)
        {
            discriminator = "any";
            return typeof(AnyConditionDto);
        }

        if (hasNot)
        {
            discriminator = "not";
            return typeof(NotConditionDto);
        }

        return ResolveByTypeProperty(element, LeafTypes, out discriminator);
    }
}

internal sealed class EffectDtoConverter : DiscriminatedDtoConverter<EffectDto>
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["damage"] = typeof(DamageEffectDto),
        ["instant_kill"] = typeof(InstantKillEffectDto),
        ["apply_ailment"] = typeof(ApplyAilmentEffectDto),
        ["restore_resource"] = typeof(ResourceAmountEffectDto),
        ["remove_ailment"] = typeof(RemoveAilmentEffectDto),
        ["revive"] = typeof(ResourceAmountEffectDto),
        ["modify_stat_stage"] = typeof(ModifyStatStageEffectDto),
        ["grant_charge"] = typeof(GrantChargeEffectDto),
        ["grant_shield"] = typeof(GrantShieldEffectDto),
        ["override_affinity"] = typeof(OverrideAffinityEffectDto),
        ["remove_status_effect"] = typeof(RemoveStatusEffectDto),
        ["reduce_resource"] = typeof(ResourceAmountEffectDto),
        ["set_resource"] = typeof(ResourceAmountEffectDto),
        ["analyze"] = typeof(AnalyzeEffectDto),
        ["escape"] = typeof(EscapeEffectDto),
        ["custom"] = typeof(CustomEffectDto)
    };

    protected override string UnionName => "effect type";
    protected override Type ResolveType(JsonElement element, out string? discriminator) =>
        ResolveByTypeProperty(element, Types, out discriminator);
}

internal sealed class RuleModifierDtoConverter : DiscriminatedDtoConverter<RuleModifierDto>
{
    private static readonly HashSet<string> NumericTypes =
    [
        "damage_dealt", "damage_taken", "accuracy", "evasion", "critical_chance",
        "ailment_infliction", "healing_received", "healing_given",
        "resource_cost", "maximum_resource", "experience_gain"
    ];

    protected override string UnionName => "rule modifier type";

    protected override Type ResolveType(JsonElement element, out string? discriminator)
    {
        discriminator = ReadString(element, "type");
        if (discriminator is not null && NumericTypes.Contains(discriminator))
        {
            return typeof(NumericRuleModifierDto);
        }

        return discriminator switch
        {
            "elemental_affinity" => typeof(ElementalAffinityRuleModifierDto),
            "ailment_resistance" => typeof(AilmentResistanceRuleModifierDto),
            "basic_attack" => typeof(BasicAttackRuleModifierDto),
            _ => throw new SchemaDiscriminatorException(UnionName, discriminator)
        };
    }
}

internal sealed class AilmentTurnBehaviorDtoConverter
    : DiscriminatedDtoConverter<AilmentTurnBehaviorDto>
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["normal"] = typeof(MarkerAilmentTurnBehaviorDto),
        ["skip"] = typeof(MarkerAilmentTurnBehaviorDto),
        ["limited_actions"] = typeof(LimitedActionsAilmentTurnBehaviorDto),
        ["chance_skip"] = typeof(ChanceSkipAilmentTurnBehaviorDto),
        ["chance_skip_or_flee"] = typeof(ChanceSkipOrFleeAilmentTurnBehaviorDto),
        ["forced_basic_attack"] = typeof(MarkerAilmentTurnBehaviorDto),
        ["confused_action"] = typeof(MarkerAilmentTurnBehaviorDto),
        ["custom"] = typeof(CustomAilmentTurnBehaviorDto)
    };

    protected override string UnionName => "ailment turn-behaviour type";
    protected override Type ResolveType(JsonElement element, out string? discriminator) =>
        ResolveByTypeProperty(element, Types, out discriminator);
}
