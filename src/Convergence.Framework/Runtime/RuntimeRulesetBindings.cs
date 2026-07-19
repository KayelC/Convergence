using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.TurnEconomy;

namespace Convergence.Runtime;

public static class StandardRulesetPolicyIds
{
    public static ContentId StandardDamage { get; } = ContentId.Parse("standard_damage");
    public static ContentId StandardReward { get; } = ContentId.Parse("standard_reward");
    public static ContentId StandardGrowth { get; } = ContentId.Parse("standard_growth");
    public static ContentId StandardStat { get; } = ContentId.Parse("standard_stat");
    public static ContentId PersistentStagedStatModifier { get; } = ContentId.Parse("persistent_staged");
    public static ContentId TimedExclusiveStatModifier { get; } = ContentId.Parse("timed_exclusive");
    public static ContentId TimedContributionStatModifier { get; } = ContentId.Parse("timed_contribution");
    public static ContentId StandardActionToken { get; } = ContentId.Parse("standard_action_token");
    public static ContentId StandardRosterCapacity { get; } = ContentId.Parse("standard_roster_capacity");
    public static ContentId StandardEconomy { get; } = ContentId.Parse("standard_economy");
}

public enum RulesetBindingDiagnosticCode
{
    MissingRuleset,
    CategoryMismatch,
    UnsupportedPolicy,
    MissingParameter,
    UnknownParameter,
    InvalidParameterType,
    InvalidParameterValue,
    InvalidIdentifier,
    PolicyFactoryFailure
}

public sealed record RulesetBindingDiagnostic(
    RulesetBindingDiagnosticCode Code,
    ContentId RulesetId,
    string Message,
    string? ParameterName = null,
    RulesetCategory? ExpectedCategory = null,
    RulesetCategory? ActualCategory = null,
    ContentId? PolicyId = null);

public sealed record RulesetBindingResult<TService>
    where TService : class
{
    public RulesetBindingResult(
        TService? service,
        IEnumerable<RulesetBindingDiagnostic>? diagnostics = null)
    {
        Service = service;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public TService? Service { get; }
    public IReadOnlyList<RulesetBindingDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Service is not null && Diagnostics.Count == 0;

    public TService RequireService() =>
        IsSuccess && Service is not null
            ? Service
            : throw new InvalidOperationException(
                "Ruleset binding failed: " + string.Join("; ", Diagnostics.Select(diagnostic => diagnostic.Message)));
}

public sealed record GrowthRulesetServices(
    IResourceGrowthPolicy ResourceGrowthPolicy,
    IExperienceCurve ExperienceCurve,
    ILevelGrowthPolicy LevelGrowthPolicy,
    IStatAllocationService StatAllocationService);

public sealed record StatRulesetServices(
    IStatResolutionPolicy StatResolutionPolicy,
    IStatStageScalingPolicy StageScalingPolicy);

public sealed record ResourceManagementRulesetServices(
    IInventoryTransitionService Inventory,
    IEquipmentTransitionService Equipment,
    IEconomyTransactionService Economy,
    IShopTransactionService Shop,
    IHospitalRestorationService Hospital);

public sealed record BattleTurnEconomyRuleset
{
    public BattleTurnEconomyRuleset(
        Func<IBattleTurnEconomy> createEconomy,
        BattlePhaseProgressPolicy phaseProgress)
    {
        CreateEconomy = createEconomy ?? throw new ArgumentNullException(nameof(createEconomy));
        PhaseProgress = phaseProgress ?? throw new ArgumentNullException(nameof(phaseProgress));
    }

    public Func<IBattleTurnEconomy> CreateEconomy { get; }
    public BattlePhaseProgressPolicy PhaseProgress { get; }
}

public interface IRuntimeRulesetBindingResolver
{
    RulesetBindingResult<CombatExecutionPolicySet> BindCombatPolicies(
        GameDataCatalog catalog,
        ContentId rulesetId,
        IRandomSource random,
        IStatStageScalingPolicy stageScalingPolicy);

    RulesetBindingResult<IBattleRewardService> BindBattleRewardService(
        GameDataCatalog catalog,
        ContentId rulesetId,
        IRandomSource random);

    RulesetBindingResult<StatRulesetServices> BindStatServices(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<IStatModifierPolicyService> BindStatModifierPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<GrowthRulesetServices> BindGrowthServices(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<IRosterCapacityPolicy> BindRosterCapacityPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<ResourceManagementRulesetServices> BindResourceManagementServices(
        GameDataCatalog catalog,
        ContentId rulesetId);

    RulesetBindingResult<BattleTurnEconomyRuleset> BindTurnEconomy(
        GameDataCatalog catalog,
        ContentId rulesetId);
}

/// <summary>Resolves authored ruleset records through host-supplied typed policy factories.</summary>
public sealed class RuntimeRulesetBindingResolver : IRuntimeRulesetBindingResolver
{
    private readonly RuntimeRulesetPolicyFactoryRegistry _factories;

    public RuntimeRulesetBindingResolver(RuntimeRulesetPolicyFactoryRegistry factories)
    {
        _factories = factories ?? throw new ArgumentNullException(nameof(factories));
    }

    public RulesetBindingResult<CombatExecutionPolicySet> BindCombatPolicies(
        GameDataCatalog catalog,
        ContentId rulesetId,
        IRandomSource random,
        IStatStageScalingPolicy stageScalingPolicy)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(stageScalingPolicy);
        return Bind<IRuntimeCombatRulesetPolicyFactory, CombatExecutionPolicySet>(
            catalog,
            rulesetId,
            RulesetCategory.Damage,
            _factories.FindCombat,
            (factory, definition) => factory.Create(definition, random, stageScalingPolicy));
    }

    public RulesetBindingResult<IBattleRewardService> BindBattleRewardService(
        GameDataCatalog catalog,
        ContentId rulesetId,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Bind<IRuntimeRewardRulesetPolicyFactory, IBattleRewardService>(
            catalog,
            rulesetId,
            RulesetCategory.Reward,
            _factories.FindReward,
            (factory, definition) => factory.Create(definition, random));
    }

    public RulesetBindingResult<StatRulesetServices> BindStatServices(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IRuntimeStatRulesetPolicyFactory, StatRulesetServices>(
            catalog,
            rulesetId,
            RulesetCategory.Stat,
            _factories.FindStat,
            static (factory, definition) => factory.Create(definition));

    public RulesetBindingResult<IStatModifierPolicyService> BindStatModifierPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IRuntimeStatModifierRulesetPolicyFactory, IStatModifierPolicyService>(
            catalog,
            rulesetId,
            RulesetCategory.StatModifier,
            _factories.FindStatModifier,
            static (factory, definition) => factory.Create(definition));

    public RulesetBindingResult<GrowthRulesetServices> BindGrowthServices(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IRuntimeGrowthRulesetPolicyFactory, GrowthRulesetServices>(
            catalog,
            rulesetId,
            RulesetCategory.Growth,
            _factories.FindGrowth,
            static (factory, definition) => factory.Create(definition));

    public RulesetBindingResult<IRosterCapacityPolicy> BindRosterCapacityPolicy(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IRuntimeRosterCapacityRulesetPolicyFactory, IRosterCapacityPolicy>(
            catalog,
            rulesetId,
            RulesetCategory.RosterCapacity,
            _factories.FindRosterCapacity,
            static (factory, definition) => factory.Create(definition));

    public RulesetBindingResult<ResourceManagementRulesetServices> BindResourceManagementServices(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IRuntimeEconomyRulesetPolicyFactory, ResourceManagementRulesetServices>(
            catalog,
            rulesetId,
            RulesetCategory.Economy,
            _factories.FindEconomy,
            static (factory, definition) => factory.Create(definition));

    public RulesetBindingResult<BattleTurnEconomyRuleset> BindTurnEconomy(
        GameDataCatalog catalog,
        ContentId rulesetId) =>
        Bind<IRuntimeTurnEconomyRulesetPolicyFactory, BattleTurnEconomyRuleset>(
            catalog,
            rulesetId,
            RulesetCategory.TurnEconomy,
            _factories.FindTurnEconomy,
            static (factory, definition) => factory.Create(definition));

    private static RulesetBindingResult<TService> Bind<TFactory, TService>(
        GameDataCatalog catalog,
        ContentId rulesetId,
        RulesetCategory expectedCategory,
        Func<ContentId, TFactory?> findFactory,
        Func<TFactory, RulesetDefinition, RulesetBindingResult<TService>> create)
        where TFactory : class
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(findFactory);
        ArgumentNullException.ThrowIfNull(create);

        if (!rulesetId.IsValid)
        {
            return Failure<TService>(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.InvalidIdentifier,
                rulesetId,
                "Ruleset ID cannot be empty."));
        }

        if (!catalog.TryGetRuleset(rulesetId, out RulesetDefinition? definition) || definition is null)
        {
            return Failure<TService>(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.MissingRuleset,
                rulesetId,
                $"Ruleset '{rulesetId}' was not found."));
        }

        if (definition.Category != expectedCategory)
        {
            return Failure<TService>(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.CategoryMismatch,
                rulesetId,
                $"Ruleset '{rulesetId}' has category '{definition.Category}', but '{expectedCategory}' was required.",
                ExpectedCategory: expectedCategory,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }

        TFactory? factory = findFactory(definition.PolicyId);
        if (factory is null)
        {
            return Failure<TService>(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.UnsupportedPolicy,
                rulesetId,
                $"Ruleset '{rulesetId}' uses unregistered {expectedCategory} policy '{definition.PolicyId}'.",
                ExpectedCategory: expectedCategory,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }

        RulesetBindingResult<TService>? result;
        try
        {
            result = create(factory, definition);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure<TService>(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.PolicyFactoryFailure,
                rulesetId,
                $"Ruleset policy factory '{definition.PolicyId}' failed: {exception.Message}",
                ExpectedCategory: expectedCategory,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }

        if (result is null || (result.Service is null && result.Diagnostics.Count == 0))
        {
            return Failure<TService>(new RulesetBindingDiagnostic(
                RulesetBindingDiagnosticCode.PolicyFactoryFailure,
                rulesetId,
                $"Ruleset policy factory '{definition.PolicyId}' returned no service or diagnostic.",
                ExpectedCategory: expectedCategory,
                ActualCategory: definition.Category,
                PolicyId: definition.PolicyId));
        }

        return result.Diagnostics.Count == 0
            ? result
            : new RulesetBindingResult<TService>(null, result.Diagnostics);
    }

    private static RulesetBindingResult<TService> Failure<TService>(RulesetBindingDiagnostic diagnostic)
        where TService : class =>
        new(null, [diagnostic]);
}
