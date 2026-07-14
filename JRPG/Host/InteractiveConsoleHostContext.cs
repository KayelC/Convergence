using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Hosting;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host;

internal sealed record ConsoleCatalogDiagnostic(string Code, string SourceName, string JsonPath, string Message)
{
    public override string ToString() =>
        string.IsNullOrWhiteSpace(JsonPath)
            ? $"[{Code}] {SourceName}: {Message}"
            : $"[{Code}] {SourceName} {JsonPath}: {Message}";
}

internal sealed record InteractiveConsoleHostContext(
    IGameIO Io,
    IContentPackTextSource ContentSource,
    IHostEventSink<string> Events,
    GameDataCatalog? CleanCatalog,
    IReadOnlyList<ConsoleCatalogDiagnostic> CatalogDiagnostics)
{
    public bool HasCleanCatalog => CleanCatalog is not null && CatalogDiagnostics.Count == 0;
}

internal sealed class InteractiveConsoleHostContextFactory
{
    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _events;
    private readonly ISkillSystemCatalogLoader _catalogLoader;

    public InteractiveConsoleHostContextFactory(
        IContentPackTextSource contentSource,
        IHostEventSink<string> events,
        ISkillSystemCatalogLoader? catalogLoader = null)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _catalogLoader = catalogLoader ?? new SkillSystemCatalogLoader();
    }

    public async ValueTask<InteractiveConsoleHostContext> CreateAsync(
        IGameIO io,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(io);

        (GameDataCatalog? catalog, IReadOnlyList<ConsoleCatalogDiagnostic> diagnostics) =
            await TryLoadCleanCatalogAsync(cancellationToken).ConfigureAwait(false);

        var context = new InteractiveConsoleHostContext(
            io,
            _contentSource,
            _events,
            catalog,
            diagnostics);

        if (!context.HasCleanCatalog)
        {
            foreach (ConsoleCatalogDiagnostic diagnostic in context.CatalogDiagnostics)
            {
                await _events.PublishAsync($"[Clean Catalog Warning] {diagnostic}", cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return context;
    }

    private async ValueTask<(GameDataCatalog? Catalog, IReadOnlyList<ConsoleCatalogDiagnostic> Diagnostics)> TryLoadCleanCatalogAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            ContentPackTextBundle[] bundles =
            [
                await ReadAsync(
                    "skill_system_redesign.manifest.sample.json",
                    cancellationToken,
                    "skill_system_redesign.races.sample.json",
                    "skill_system_redesign.skills.sample.json",
                    "skill_system_redesign.entities.sample.json").ConfigureAwait(false),
                await ReadAsync(
                    "clean_battle_demo.manifest.json",
                    cancellationToken,
                    "clean_battle_demo.races.json",
                    "clean_battle_demo.skills.json",
                    "clean_battle_demo.entities.json").ConfigureAwait(false),
                await ReadAsync(
                    "shared_effects_demo.manifest.json",
                    cancellationToken,
                    "shared_effects_demo.ailments.json",
                    "shared_effects_demo.skills.json",
                    "shared_effects_demo.entities.json",
                    "shared_effects_demo.items.json").ConfigureAwait(false),
                await ReadAsync(
                    "catalog_surface_sample.manifest.json",
                    cancellationToken,
                    "catalog_surface_sample.equipment.json",
                    "catalog_surface_sample.shops.json",
                    "catalog_surface_sample.negotiations.json",
                    "catalog_surface_sample.encounters.json",
                    "catalog_surface_sample.dungeons.json",
                    "catalog_surface_sample.fusion.json",
                    "catalog_surface_sample.rulesets.json").ConfigureAwait(false),
                await ReadAsync(
                    "status_lifecycle_demo.manifest.json",
                    cancellationToken,
                    "status_lifecycle_demo.ailments.json").ConfigureAwait(false)
            ];

            CatalogLoadResult result = _catalogLoader.Load(
                new SkillSystemCatalogLoadRequest(BuildRegistrations(), bundles));
            if (result.IsSuccess && result.Catalog is not null)
            {
                return (result.Catalog, Array.AsReadOnly(Array.Empty<ConsoleCatalogDiagnostic>()));
            }

            return (null, Array.AsReadOnly(result.Diagnostics.Select(ToDiagnostic).ToArray()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (null, Array.AsReadOnly(new[]
            {
                new ConsoleCatalogDiagnostic(
                    "ContentReadFailed",
                    "Data/Jsons",
                    string.Empty,
                    exception.Message)
            }));
        }
    }

    private ValueTask<ContentPackTextBundle> ReadAsync(
        string manifestPath,
        CancellationToken cancellationToken,
        params string[] documentPaths) =>
        _contentSource.ReadAsync(new ContentPackTextRequest(manifestPath, documentPaths), cancellationToken);

    private static ConsoleCatalogDiagnostic ToDiagnostic(CatalogLoadDiagnostic diagnostic) =>
        new(
            diagnostic.Code.ToString(),
            diagnostic.SourceName,
            diagnostic.JsonPath,
            diagnostic.Message);

    private static SkillSystemRegistrationSnapshot BuildRegistrations()
    {
        var validator = new LegacyAcceptAnyParametersValidator();
        return new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEntityKind("demon")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterAction("basic_attack", "guard", "pass")
            .RegisterAilmentGroup("major_ailment", "poison", "immobilize", "mental")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .RegisterEscapeRule("standard_escape")
            .RegisterFormula("legacy_poison_damage", validator)
            .RegisterCustomEffect("request_dungeon_exit", validator)
            .RegisterShopCategory("weapon_shop")
            .RegisterNegotiationPersonality("childlike")
            .RegisterNegotiationDemand("macca")
            .RegisterEncounterEnvironment("thebel")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "standard_press_turn",
                "standard_stock_capacity",
                "standard_economy",
                "standard_moon_phase",
                "return_to_lobby",
                "standard_accident",
                "standard_mutation")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<ReduceResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<EscapeEffectDefinition>()
            .SupportEffect<CustomEffectDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<LimitedActionsAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipOrFleeAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ForcedBasicAttackAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ConfusedActionAilmentTurnBehaviorDefinition>()
            .Build();
    }
}

internal sealed class LegacyAcceptAnyParametersValidator : IContentParameterValidator
{
    public IReadOnlyList<ContentParameterValidationIssue> Validate(
        IReadOnlyDictionary<string, object?> parameters) => [];
}
