using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;

namespace JRPGPrototype.Host;

internal sealed class FileContentPackSource
{
    private readonly string _root;

    public FileContentPackSource(string root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public ContentPackTextBundle Read(string manifestPath, params string[] documentPaths)
    {
        string manifestFile = Path.Combine(_root, manifestPath);
        ContentDocumentText[] documents = documentPaths.Select(path =>
        {
            string file = Path.Combine(_root, path);
            return new ContentDocumentText(path, file, File.ReadAllText(file));
        }).ToArray();
        return new ContentPackTextBundle(manifestFile, File.ReadAllText(manifestFile), documents);
    }
}

internal sealed class DemoBattleActorInitializationPolicy : IBattleActorInitializationPolicy
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Magic = ContentId.Parse("magic");
    private static readonly ContentId Vitality = ContentId.Parse("vitality");

    public BattleActorInitialization Initialize(EntityDefinition entity, int level)
    {
        decimal vitality = entity.Stats.GetValueOrDefault(Vitality);
        decimal magic = entity.Stats.GetValueOrDefault(Magic);
        decimal hp = 40 + level * 5 + vitality * 3;
        decimal sp = 10 + level * 2 + magic * 2;
        return new BattleActorInitialization(Hp,
        [
            new BattleResourceState(Hp, hp, hp),
            new BattleResourceState(Sp, sp, sp)
        ]);
    }
}

internal sealed class DemoDamageExecutionPolicy : IDamageExecutionPolicy
{
    private static readonly ContentId Magic = ContentId.Parse("magic");
    private static readonly ContentId Vitality = ContentId.Parse("vitality");

    public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request)
    {
        decimal damage = Math.Max(
            1,
            request.Effect.Power +
            request.Actor.Stats.GetValueOrDefault(Magic) -
            request.Target.Stats.GetValueOrDefault(Vitality));
        damage *= request.Affinity switch
        {
            ElementalAffinity.Weak => 1.5m,
            ElementalAffinity.Resist => 0.5m,
            _ => 1m
        };
        return [new DamageHitResolution(true, damage)];
    }
}

internal sealed class DemoInstantDeathPolicy : IInstantDeathExecutionPolicy
{
    public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
}

internal sealed class DemoAilmentPolicy : IAilmentApplicationPolicy
{
    public bool ShouldApply(AilmentApplicationPolicyRequest request) =>
        request.Resistance != ResistanceLevel.Immune;
}

internal sealed class DemoChancePolicy : IChanceExecutionPolicy
{
    public bool Roll(ChancePolicyRequest request) => request.Chance > 0;
}

internal sealed class DemoPowerAmountPolicy : IPowerAmountPolicy
{
    public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
}

internal sealed class DemoRandomTargetPolicy : IRandomTargetSelectionPolicy
{
    public IReadOnlyList<BattleActorState> Select(
        IReadOnlyList<BattleActorState> candidates,
        TargetCountDefinition count,
        SkillExecutionRequest request) =>
        Array.AsReadOnly(candidates.Take(count.Minimum).ToArray());
}

internal sealed class CleanBattleDemoHost
{
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId NewMoon = ContentId.Parse("new_moon");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");

    private readonly TextWriter _output;
    private readonly string _contentRoot;

    public CleanBattleDemoHost(TextWriter output, string? contentRoot = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _contentRoot = contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Data", "Jsons");
    }

    public int Run()
    {
        ContentPackTextBundle[] bundles;
        try
        {
            var source = new FileContentPackSource(_contentRoot);
            bundles =
            [
                source.Read(
                    "skill_system_redesign.manifest.sample.json",
                    "skill_system_redesign.races.sample.json",
                    "skill_system_redesign.skills.sample.json",
                    "skill_system_redesign.entities.sample.json"),
                source.Read(
                    "clean_battle_demo.manifest.json",
                    "clean_battle_demo.races.json",
                    "clean_battle_demo.skills.json",
                    "clean_battle_demo.entities.json")
            ];
        }
        catch (Exception exception)
        {
            _output.WriteLine($"Content read failed: {exception.Message}");
            return 2;
        }

        CatalogLoadResult load = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(BuildRegistrations(), bundles));
        if (!load.IsSuccess || load.Catalog is null)
        {
            foreach (CatalogLoadDiagnostic diagnostic in load.Diagnostics)
            {
                _output.WriteLine($"[{diagnostic.Code}] {diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}");
            }
            return 3;
        }

        GameDataCatalog catalog = load.Catalog;
        var factory = new CatalogBattleActorFactory(catalog, catalog, new DemoBattleActorInitializationPolicy());
        CatalogBattleActorCreationResult frostResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"),
            ContentId.Parse("frost_duelist"),
            PlayerTeam,
            5));
        CatalogBattleActorCreationResult emberResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo"),
            ContentId.Parse("ember_duelist"),
            EnemyTeam,
            5));
        if (!frostResult.IsSuccess || !emberResult.IsSuccess)
        {
            foreach (CatalogBattleActorDiagnostic diagnostic in frostResult.Diagnostics.Concat(emberResult.Diagnostics))
            {
                _output.WriteLine($"[{diagnostic.Code}] {diagnostic.Message}");
            }
            return 4;
        }

        BattleExecutionServices services = CreateExecutionServices(catalog);
        var executor = new SkillExecutor(services);
        var runner = new AutomatedBattleRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services);
        AutomatedBattleResult battle = runner.Run(new AutomatedBattleRequest(
            [frostResult.RequireActor(), emberResult.RequireActor()],
            Battle,
            NormalBattle,
            NewMoon,
            10));

        foreach (BattleRuntimeEvent battleEvent in battle.Events)
        {
            _output.WriteLine($"{battleEvent.Sequence:D3} [{battleEvent.Kind}] {battleEvent.Message}");
        }
        _output.WriteLine(battle.WinningTeamId is ContentId winner
            ? $"Outcome: {battle.Outcome}; winner: {winner}"
            : $"Outcome: {battle.Outcome}");
        return battle.Outcome == AutomatedBattleOutcome.Faulted ? 5 : 0;
    }

    private static SkillSystemRegistrationSnapshot BuildRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("demon")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .Build();

    private static BattleExecutionServices CreateExecutionServices(GameDataCatalog catalog) =>
        new(
            catalog,
            new DemoDamageExecutionPolicy(),
            new DemoInstantDeathPolicy(),
            new DemoAilmentPolicy(),
            new DemoChancePolicy(),
            new DemoPowerAmountPolicy(),
            new DemoRandomTargetPolicy());
}
