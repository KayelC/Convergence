using System.Text.Json;
using System.Text.Json.Serialization;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Schemas;

namespace JRPGPrototype.Data.SkillSystem;

public sealed class SkillSystemJsonDeserializer : ISkillSystemDocumentDeserializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public ContentPackManifest DeserializeManifest(string json, string sourceName) =>
        Deserialize(json, sourceName, TypeInfo<ManifestDto>(), SkillSystemDtoMapper.Map);

    public DeserializedContentDocument<SkillDefinition> DeserializeSkills(string json, string sourceName) =>
        Deserialize(json, sourceName, TypeInfo<SkillDocumentDto>(), SkillSystemDtoMapper.Map);

    public DeserializedContentDocument<EntityDefinition> DeserializeEntities(string json, string sourceName) =>
        Deserialize(json, sourceName, TypeInfo<EntityDocumentDto>(), SkillSystemDtoMapper.Map);

    public DeserializedContentDocument<RaceDefinition> DeserializeRaces(string json, string sourceName) =>
        Deserialize(json, sourceName, TypeInfo<RaceDocumentDto>(), SkillSystemDtoMapper.Map);

    public DeserializedContentDocument<AilmentDefinition> DeserializeAilments(string json, string sourceName) =>
        Deserialize(json, sourceName, TypeInfo<AilmentDocumentDto>(), SkillSystemDtoMapper.Map);

    public DeserializedContentDocument<ItemDefinition> DeserializeItems(string json, string sourceName) =>
        Deserialize(json, sourceName, TypeInfo<ItemDocumentDto>(), SkillSystemDtoMapper.Map);

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> TypeInfo<T>()
    {
        return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
    }

    private static TResult Deserialize<TDto, TResult>(
        string json,
        string sourceName,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TDto> typeInfo,
        Func<TDto, TResult> mapper)
        where TDto : class
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        try
        {
            TDto dto = JsonSerializer.Deserialize(json, typeInfo)
                ?? throw new JsonException("The document contains JSON null instead of an object.");
            return mapper(dto);
        }
        catch (SchemaMappingException exception)
        {
            throw new ContentDeserializationException(
                sourceName,
                exception.Message,
                exception.Path,
                discriminator: exception.Discriminator,
                innerException: exception);
        }
        catch (JsonException exception)
        {
            SchemaDiscriminatorException? discriminatorException = FindDiscriminatorException(exception);
            throw new ContentDeserializationException(
                sourceName,
                exception.Message,
                exception.Path,
                exception.LineNumber,
                exception.BytePositionInLine,
                discriminatorException?.Discriminator,
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new ContentDeserializationException(sourceName, exception.Message, innerException: exception);
        }
    }

    private static SchemaDiscriminatorException? FindDiscriminatorException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SchemaDiscriminatorException discriminatorException)
            {
                return discriminatorException;
            }
        }

        return null;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = SkillSystemJsonContext.Default,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false
        };

        options.Converters.Add(new AmountDtoConverter());
        options.Converters.Add(new DurationDtoConverter());
        options.Converters.Add(new CriticalDtoConverter());
        options.Converters.Add(new InstantDeathResistanceCheckDtoConverter());
        options.Converters.Add(new ConditionDtoConverter());
        options.Converters.Add(new EffectDtoConverter());
        options.Converters.Add(new RuleModifierDtoConverter());
        options.Converters.Add(new AilmentTurnBehaviorDtoConverter());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<DamageElement>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<ElementalAffinity>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<ResistanceLevel>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<SkillActivation>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<SkillMenuGroup>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<InheritanceGroup>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<EffectFailurePolicy>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<InstantDeathChannel>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<TargetRelation>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<TargetSelection>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<TargetLifeState>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<HitDistribution>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<DamageDrainMode>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<ChargeKind>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<ShieldKind>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<ModifierOperation>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<NumericComparison>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<AilmentRemovalScope>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<StatusEffectKind>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<AnalysisLayer>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<InheritanceGroupPolicyMode>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<DemonFleeOutcome>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<ItemKind>());
        options.Converters.Add(new StrictSnakeCaseEnumConverter<ItemConsumptionMode>());
        return options;
    }
}
