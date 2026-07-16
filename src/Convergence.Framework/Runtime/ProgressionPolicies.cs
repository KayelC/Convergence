using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Hosting;
using static Convergence.Runtime.ProgressionCollections;

namespace Convergence.Runtime;

public static class StandardProgressionIds
{
    public static ContentId Strength { get; } = ContentId.Parse("strength");
    public static ContentId Magic { get; } = ContentId.Parse("magic");
    public static ContentId Vitality { get; } = ContentId.Parse("vitality");
    public static ContentId Agility { get; } = ContentId.Parse("agility");
    public static ContentId Luck { get; } = ContentId.Parse("luck");
    public static ContentId Hp { get; } = ContentId.Parse("hp");
    public static ContentId Sp { get; } = ContentId.Parse("sp");
    public static ContentId IndependentActor { get; } = ContentId.Parse("independent_actor");
    public static ContentId Vessel { get; } = ContentId.Parse("vessel");
    public static ContentId Companion { get; } = ContentId.Parse("companion");
    public static ContentId PhysicalAttack { get; } = ContentId.Parse("physical_attack");
    public static ContentId MagicalAttack { get; } = ContentId.Parse("magical_attack");
    public static ContentId Attack { get; } = ContentId.Parse("attack");
    public static ContentId Defense { get; } = ContentId.Parse("defense");
    public static ContentId AgilityTrack { get; } = ContentId.Parse("agility");

    public static IReadOnlyList<ContentId> CoreStats { get; } = Array.AsReadOnly(
    [
        Strength,
        Magic,
        Vitality,
        Agility,
        Luck
    ]);
}

public enum ResourceCurrentAdjustmentMode
{
    PreserveCurrent,
    LevelUpDelta
}

public enum RuntimeStatSourceKind
{
    Actor,
    ActiveHostedEntity
}

public enum MissingHostedEntityBehavior
{
    RejectStatResolution,
    UseActorBaseStats
}

public enum ProgressionMutationStatus
{
    Applied,
    Rejected
}

public enum ProgressionMutationErrorCode
{
    NegativeExperience,
    MissingStatPoints,
    StatAtCap,
    MissingResource,
    InvalidLevel,
    NumericOverflow,
    InvalidExperienceRequirement
}

public sealed record StandardStatPolicyConfig
{
    public StandardStatPolicyConfig(int statCap = 40)
    {
        if (statCap <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(statCap), "Stat cap must be positive.");
        }

        StatCap = statCap;
    }

    public int StatCap { get; }

    public static StandardStatPolicyConfig Default { get; } = new();
}

public sealed record StatResolutionRequest
{
    public StatResolutionRequest(
        RuntimeStatSourceKind sourceKind,
        ContentId statId,
        IEnumerable<KeyValuePair<ContentId, decimal>>? actorStats = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? activeHostedEntityStats = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers = null)
    {
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), "Stat source kind is not supported.");
        }

        SourceKind = sourceKind;
        StatId = statId;
        ActorStats = SnapshotDictionary(actorStats);
        ActiveHostedEntityStats = SnapshotDictionary(activeHostedEntityStats);
        EquipmentStatModifiers = SnapshotDictionary(equipmentStatModifiers);
    }

    public RuntimeStatSourceKind SourceKind { get; }
    public ContentId StatId { get; }
    public IReadOnlyDictionary<ContentId, decimal> ActorStats { get; }
    public IReadOnlyDictionary<ContentId, decimal> ActiveHostedEntityStats { get; }
    public IReadOnlyDictionary<ContentId, decimal> EquipmentStatModifiers { get; }
}

public sealed record StatResolutionResult(
    ContentId StatId,
    decimal RawValue,
    int CappedValue,
    int FinalValue);

public interface IStatResolutionPolicy
{
    StatResolutionResult Resolve(StatResolutionRequest request);
}

public sealed class StandardStatResolutionPolicy : IStatResolutionPolicy
{
    private readonly StandardStatPolicyConfig _config;

    public StandardStatResolutionPolicy(StandardStatPolicyConfig? config = null)
    {
        _config = config ?? StandardStatPolicyConfig.Default;
    }

    public StatResolutionResult Resolve(StatResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        decimal raw = ResolveRawValue(request);
        int capped = SaturatingFloorToInt(Math.Min(_config.StatCap, Math.Floor(raw)));
        return new StatResolutionResult(request.StatId, raw, capped, capped);
    }

    private decimal ResolveRawValue(StatResolutionRequest request)
    {
        IReadOnlyDictionary<ContentId, decimal> sourceStats = request.SourceKind switch
        {
            RuntimeStatSourceKind.Actor => request.ActorStats,
            RuntimeStatSourceKind.ActiveHostedEntity => request.ActiveHostedEntityStats,
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Stat source kind is not supported.")
        };

        return SaturatingAdd(
            ValueOrZero(sourceStats, request.StatId),
            ValueOrZero(request.EquipmentStatModifiers, request.StatId));
    }

    private static decimal ValueOrZero(IReadOnlyDictionary<ContentId, decimal> values, ContentId id) =>
        values.TryGetValue(id, out decimal value) ? value : 0m;

    private static decimal SaturatingAdd(decimal left, decimal right)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            return left >= 0m && right >= 0m ? decimal.MaxValue : decimal.MinValue;
        }
    }

    private static int SaturatingFloorToInt(decimal value)
    {
        decimal floored = Math.Floor(value);
        if (floored >= int.MaxValue)
        {
            return int.MaxValue;
        }
        if (floored <= int.MinValue)
        {
            return int.MinValue;
        }

        return decimal.ToInt32(floored);
    }
}

public sealed record ResourceRecalculationRequest
{
    public ResourceRecalculationRequest(
        IEnumerable<RuntimeResourceSnapshot> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>> baseResourceValues,
        IEnumerable<KeyValuePair<ContentId, decimal>> effectiveStats,
        ResourceCurrentAdjustmentMode adjustmentMode = ResourceCurrentAdjustmentMode.PreserveCurrent)
    {
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
        EffectiveStats = SnapshotDictionary(effectiveStats);
        AdjustmentMode = adjustmentMode;
    }

    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
    public IReadOnlyDictionary<ContentId, decimal> EffectiveStats { get; }
    public ResourceCurrentAdjustmentMode AdjustmentMode { get; }
}

public sealed record ResourceRecalculationResult
{
    public ResourceRecalculationResult(IEnumerable<RuntimeResourceSnapshot> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        RuntimeResourceSnapshot[] snapshot = resources.ToArray();
        if (snapshot.Any(resource => resource is null))
        {
            throw new ArgumentException("Recalculated resources cannot contain null entries.", nameof(resources));
        }

        if (snapshot.Select(resource => resource.ResourceId).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Recalculated resources must have unique resource IDs.", nameof(resources));
        }

        Resources = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }

    public RuntimeResourceSnapshot GetRequired(ContentId resourceId) =>
        Resources.FirstOrDefault(resource => resource.ResourceId == resourceId)
        ?? throw new KeyNotFoundException($"Resource '{resourceId}' was not recalculated.");
}

public interface IResourceGrowthPolicy
{
    ResourceRecalculationResult Recalculate(ResourceRecalculationRequest request);
}

public sealed class StandardResourceGrowthPolicy : IResourceGrowthPolicy
{
    public ResourceRecalculationResult Recalculate(ResourceRecalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuntimeResourceSnapshot hp = RecalculateResource(
            request,
            StandardProgressionIds.Hp,
            StandardProgressionIds.Vitality,
            statMultiplier: 5,
            maximumCap: 666);
        RuntimeResourceSnapshot sp = RecalculateResource(
            request,
            StandardProgressionIds.Sp,
            StandardProgressionIds.Magic,
            statMultiplier: 3,
            maximumCap: 333);

        List<RuntimeResourceSnapshot> resources = [];
        foreach (RuntimeResourceSnapshot resource in request.Resources)
        {
            if (resource.ResourceId == StandardProgressionIds.Hp)
            {
                resources.Add(hp);
            }
            else if (resource.ResourceId == StandardProgressionIds.Sp)
            {
                resources.Add(sp);
            }
            else
            {
                resources.Add(resource);
            }
        }

        if (!resources.Any(resource => resource.ResourceId == StandardProgressionIds.Hp))
        {
            resources.Add(hp);
        }
        if (!resources.Any(resource => resource.ResourceId == StandardProgressionIds.Sp))
        {
            resources.Add(sp);
        }

        return new ResourceRecalculationResult(resources);
    }

    private static RuntimeResourceSnapshot RecalculateResource(
        ResourceRecalculationRequest request,
        ContentId resourceId,
        ContentId statId,
        int statMultiplier,
        int maximumCap)
    {
        RuntimeResourceSnapshot? current = request.Resources.FirstOrDefault(resource => resource.ResourceId == resourceId);
        decimal oldCurrent = current?.Current ?? 0m;
        decimal oldMaximum = current?.Maximum ?? 0m;
        decimal baseValue = request.BaseResourceValues.TryGetValue(resourceId, out decimal configuredBase)
            ? configuredBase
            : 0m;
        decimal statValue = request.EffectiveStats.TryGetValue(statId, out decimal configuredStat)
            ? configuredStat
            : 0m;
        if (!RuntimeActorNumericDomain.IsValidBaseResourceValue(baseValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                baseValue,
                $"Base resource '{resourceId}' cannot be negative.");
        }
        if (!RuntimeActorNumericDomain.IsValidStatValue(statValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                statValue,
                $"Stat '{statId}' must be between {RuntimeActorNumericDomain.MinimumStatValue} and " +
                $"{RuntimeActorNumericDomain.MaximumStatValue} inclusive.");
        }

        decimal newMaximum = CalculateCappedMaximum(
            baseValue,
            statValue,
            statMultiplier,
            maximumCap);
        decimal newCurrent = request.AdjustmentMode switch
        {
            ResourceCurrentAdjustmentMode.LevelUpDelta => oldCurrent + (newMaximum - oldMaximum),
            _ => oldCurrent
        };

        newCurrent = Math.Clamp(newCurrent, 0m, newMaximum);
        return new RuntimeResourceSnapshot(resourceId, newCurrent, newMaximum);
    }

    private static decimal CalculateCappedMaximum(
        decimal baseValue,
        decimal statValue,
        int statMultiplier,
        int maximumCap)
    {
        decimal cap = maximumCap;
        if (baseValue >= cap)
        {
            return cap;
        }
        if (statValue == 0m)
        {
            return baseValue;
        }

        decimal remaining = cap - baseValue;
        if (statValue >= remaining / statMultiplier)
        {
            return cap;
        }

        return Math.Min(cap, baseValue + (statValue * statMultiplier));
    }
}

public interface IExperienceCurve
{
    long GetRequiredExperience(int level);
}

public sealed class CubicExperienceCurve : IExperienceCurve
{
    public long GetRequiredExperience(int level)
    {
        if (level <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be positive.");
        }

        decimal scaled = checked(1.5m * level * level * level);
        return scaled >= long.MaxValue
            ? long.MaxValue
            : decimal.ToInt64(Math.Floor(scaled));
    }
}

public sealed record ProgressionMutationDiagnostic(
    ProgressionMutationErrorCode Code,
    string Message);

public sealed record RuntimeLevelGrowthProfile
{
    public RuntimeLevelGrowthProfile(
        int manualStatPointsPerLevel,
        bool growsBaseResources,
        int randomCoreStatIncreasesPerLevel)
    {
        if (manualStatPointsPerLevel < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(manualStatPointsPerLevel),
                "Manual stat points per level cannot be negative.");
        }
        if (randomCoreStatIncreasesPerLevel < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(randomCoreStatIncreasesPerLevel),
                "Random core-stat increases per level cannot be negative.");
        }

        ManualStatPointsPerLevel = manualStatPointsPerLevel;
        GrowsBaseResources = growsBaseResources;
        RandomCoreStatIncreasesPerLevel = randomCoreStatIncreasesPerLevel;
    }

    public int ManualStatPointsPerLevel { get; }
    public bool GrowsBaseResources { get; }
    public int RandomCoreStatIncreasesPerLevel { get; }
}

public static class StandardLevelGrowthProfiles
{
    public static RuntimeLevelGrowthProfile IndependentActor { get; } = new(
        manualStatPointsPerLevel: 1,
        growsBaseResources: true,
        randomCoreStatIncreasesPerLevel: 0);

    public static RuntimeLevelGrowthProfile Vessel { get; } = new(
        manualStatPointsPerLevel: 0,
        growsBaseResources: true,
        randomCoreStatIncreasesPerLevel: 0);

    public static RuntimeLevelGrowthProfile OwnedEntity { get; } = new(
        manualStatPointsPerLevel: 0,
        growsBaseResources: false,
        randomCoreStatIncreasesPerLevel: 1);
}

public sealed record LevelGrowthRequest
{
    public LevelGrowthRequest(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        RuntimeLevelGrowthProfile growthProfile,
        long experienceAward,
        IRandomSource randomSource,
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null)
    {
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        GrowthProfile = growthProfile ?? throw new ArgumentNullException(nameof(growthProfile));
        ExperienceAward = experienceAward;
        RandomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
    }

    public RuntimeProgressionSnapshot Progression { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public RuntimeLevelGrowthProfile GrowthProfile { get; }
    public long ExperienceAward { get; }
    public IRandomSource RandomSource { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
}

public sealed record LevelUpEvent
{
    public LevelUpEvent(
        int level,
        IEnumerable<KeyValuePair<ContentId, decimal>>? statIncreases = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceIncreases = null,
        int statPointsAwarded = 0,
        IReadOnlyList<RuntimeResourceSnapshot>? resourcesBefore = null,
        IReadOnlyList<RuntimeResourceSnapshot>? resourcesAfter = null)
    {
        Level = level;
        StatIncreases = SnapshotDictionary(statIncreases);
        BaseResourceIncreases = SnapshotDictionary(baseResourceIncreases);
        StatPointsAwarded = statPointsAwarded;
        ResourcesBefore = SnapshotList(resourcesBefore);
        ResourcesAfter = SnapshotList(resourcesAfter);
    }

    public int Level { get; }
    public IReadOnlyDictionary<ContentId, decimal> StatIncreases { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceIncreases { get; }
    public int StatPointsAwarded { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> ResourcesBefore { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> ResourcesAfter { get; }
}

public sealed record LevelGrowthResult
{
    public LevelGrowthResult(
        ProgressionMutationStatus status,
        LevelGrowthSourceSnapshot source,
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null,
        IEnumerable<LevelUpEvent>? levelUps = null,
        IEnumerable<ProgressionMutationDiagnostic>? diagnostics = null)
    {
        Status = status;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
        LevelUps = SnapshotList(levelUps);
        Diagnostics = SnapshotList(diagnostics);
    }

    public ProgressionMutationStatus Status { get; }
    public bool Applied => Status == ProgressionMutationStatus.Applied;
    public LevelGrowthSourceSnapshot Source { get; }
    public RuntimeProgressionSnapshot Progression { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
    public IReadOnlyList<LevelUpEvent> LevelUps { get; }
    public IReadOnlyList<ProgressionMutationDiagnostic> Diagnostics { get; }
}

public sealed record LevelGrowthSourceSnapshot
{
    public LevelGrowthSourceSnapshot(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null)
    {
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
    }

    public RuntimeProgressionSnapshot Progression { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
}

public interface ILevelGrowthPolicy
{
    LevelGrowthResult ApplyExperience(LevelGrowthRequest request);
}

public sealed class StandardLevelGrowthPolicy : ILevelGrowthPolicy
{
    private readonly IExperienceCurve _experienceCurve;
    private readonly IResourceGrowthPolicy _resourceGrowthPolicy;
    private readonly int _statCap;

    public StandardLevelGrowthPolicy(
        IExperienceCurve? experienceCurve = null,
        IResourceGrowthPolicy? resourceGrowthPolicy = null,
        int statCap = 40)
    {
        _experienceCurve = experienceCurve ?? new CubicExperienceCurve();
        _resourceGrowthPolicy = resourceGrowthPolicy ?? new StandardResourceGrowthPolicy();
        if (statCap <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(statCap), "Stat cap must be positive.");
        }

        _statCap = statCap;
    }

    public LevelGrowthResult ApplyExperience(LevelGrowthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExperienceAward < 0)
        {
            return Rejected(
                request,
                ProgressionMutationErrorCode.NegativeExperience,
                "Experience awards cannot be negative.");
        }

        try
        {
            int level = request.Progression.Level;
            long experience = checked(request.Progression.Experience + request.ExperienceAward);
            long lifetimeExperience = checked(
                request.Progression.LifetimeExperience + request.ExperienceAward);
            int statPoints = request.Progression.UnspentStatPoints;
            var baseStats = request.Stats.BaseStats.ToDictionary(pair => pair.Key, pair => pair.Value);
            var effectiveStats = request.Stats.EffectiveStats.ToDictionary(pair => pair.Key, pair => pair.Value);
            var baseResources = request.BaseResourceValues.ToDictionary(pair => pair.Key, pair => pair.Value);
            IReadOnlyList<RuntimeResourceSnapshot> resources = request.Resources;
            List<LevelUpEvent> levelUps = [];

            while (true)
            {
                long requiredExperience = _experienceCurve.GetRequiredExperience(level);
                if (requiredExperience <= 0)
                {
                    return Rejected(
                        request,
                        ProgressionMutationErrorCode.InvalidExperienceRequirement,
                        $"Experience requirement for level {level} must be positive.");
                }
                if (experience < requiredExperience)
                {
                    break;
                }
                if (level == int.MaxValue)
                {
                    return Rejected(
                        request,
                        ProgressionMutationErrorCode.NumericOverflow,
                        "Level cannot exceed the supported integer range.");
                }

                experience -= requiredExperience;
                level = checked(level + 1);

                Dictionary<ContentId, decimal> statIncreases = [];
                for (int increaseIndex = 0;
                     increaseIndex < request.GrowthProfile.RandomCoreStatIncreasesPerLevel;
                     increaseIndex++)
                {
                    ContentId stat = StandardProgressionIds.CoreStats[
                        request.RandomSource.NextInt32(0, StandardProgressionIds.CoreStats.Count)];
                    decimal current = baseStats.GetValueOrDefault(stat);
                    decimal increase = current < _statCap ? 1m : 0m;
                    if (increase > 0)
                    {
                        baseStats[stat] = checked(current + increase);
                        effectiveStats[stat] = checked(
                            effectiveStats.GetValueOrDefault(stat) + increase);
                        statIncreases[stat] = statIncreases.GetValueOrDefault(stat) + increase;
                    }
                }

                statPoints = checked(
                    statPoints + request.GrowthProfile.ManualStatPointsPerLevel);
                Dictionary<ContentId, decimal> baseResourceIncreases = [];
                if (request.GrowthProfile.GrowsBaseResources)
                {
                    decimal hpIncrease = request.RandomSource.NextInt32(6, 11);
                    decimal spIncrease = request.RandomSource.NextInt32(3, 8);
                    baseResources[StandardProgressionIds.Hp] = checked(
                        baseResources.GetValueOrDefault(StandardProgressionIds.Hp) + hpIncrease);
                    baseResources[StandardProgressionIds.Sp] = checked(
                        baseResources.GetValueOrDefault(StandardProgressionIds.Sp) + spIncrease);
                    baseResourceIncreases[StandardProgressionIds.Hp] = hpIncrease;
                    baseResourceIncreases[StandardProgressionIds.Sp] = spIncrease;
                }

                IReadOnlyList<RuntimeResourceSnapshot> before = resources;
                if (resources.Count > 0)
                {
                    ResourceRecalculationResult recalculated = _resourceGrowthPolicy.Recalculate(
                        new ResourceRecalculationRequest(
                            resources,
                            baseResources,
                            effectiveStats,
                            ResourceCurrentAdjustmentMode.LevelUpDelta));
                    resources = recalculated.Resources;
                }
                levelUps.Add(new LevelUpEvent(
                    level,
                    statIncreases: statIncreases,
                    baseResourceIncreases: baseResourceIncreases,
                    statPointsAwarded: request.GrowthProfile.ManualStatPointsPerLevel,
                    resourcesBefore: before,
                    resourcesAfter: resources));
            }

            var progression = new RuntimeProgressionSnapshot(level, experience, lifetimeExperience, statPoints);
            var stats = new RuntimeStatBlockSnapshot(baseStats, effectiveStats);
            return new LevelGrowthResult(
                ProgressionMutationStatus.Applied,
                Source(request),
                progression,
                stats,
                resources,
                baseResources,
                levelUps);
        }
        catch (OverflowException)
        {
            return Rejected(
                request,
                ProgressionMutationErrorCode.NumericOverflow,
                "Progression arithmetic exceeded the supported numeric range.");
        }
    }

    private static LevelGrowthResult Rejected(
        LevelGrowthRequest request,
        ProgressionMutationErrorCode code,
        string message) =>
        new(
            ProgressionMutationStatus.Rejected,
            Source(request),
            request.Progression,
            request.Stats,
            request.Resources,
            request.BaseResourceValues,
            diagnostics: [new ProgressionMutationDiagnostic(code, message)]);

    private static LevelGrowthSourceSnapshot Source(LevelGrowthRequest request) =>
        new(
            request.Progression,
            request.Stats,
            request.Resources,
            request.BaseResourceValues);
}

public sealed record StatAllocationRequest
{
    public StatAllocationRequest(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        ContentId statId,
        IEnumerable<RuntimeResourceSnapshot> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>> baseResourceValues,
        int statCap = 40)
    {
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        StatId = statId;
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
        StatCap = statCap;
    }

    public RuntimeProgressionSnapshot Progression { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public ContentId StatId { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
    public int StatCap { get; }
}

public sealed record StatRollbackRequest
{
    public StatRollbackRequest(
        RuntimeProgressionSnapshot currentProgression,
        RuntimeProgressionSnapshot rollbackProgression,
        RuntimeStatBlockSnapshot rollbackStats,
        IEnumerable<RuntimeResourceSnapshot> resources,
        IEnumerable<KeyValuePair<ContentId, decimal>> baseResourceValues)
    {
        CurrentProgression = currentProgression ?? throw new ArgumentNullException(nameof(currentProgression));
        RollbackProgression = rollbackProgression ?? throw new ArgumentNullException(nameof(rollbackProgression));
        RollbackStats = rollbackStats ?? throw new ArgumentNullException(nameof(rollbackStats));
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
    }

    public RuntimeProgressionSnapshot CurrentProgression { get; }
    public RuntimeProgressionSnapshot RollbackProgression { get; }
    public RuntimeStatBlockSnapshot RollbackStats { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
}

public sealed record StatAllocationResult
{
    public StatAllocationResult(
        ProgressionMutationStatus status,
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IEnumerable<RuntimeResourceSnapshot> resources,
        IEnumerable<ProgressionMutationDiagnostic>? diagnostics = null)
    {
        Status = status;
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Resources = SnapshotList(resources);
        Diagnostics = SnapshotList(diagnostics);
    }

    public ProgressionMutationStatus Status { get; }
    public bool Applied => Status == ProgressionMutationStatus.Applied;
    public RuntimeProgressionSnapshot Progression { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyList<ProgressionMutationDiagnostic> Diagnostics { get; }
}

public interface IStatAllocationService
{
    StatAllocationResult Allocate(StatAllocationRequest request);
    StatAllocationResult Rollback(StatRollbackRequest request);
}

public sealed class StatAllocationService : IStatAllocationService
{
    private readonly IResourceGrowthPolicy _resourceGrowthPolicy;

    public StatAllocationService(IResourceGrowthPolicy? resourceGrowthPolicy = null)
    {
        _resourceGrowthPolicy = resourceGrowthPolicy ?? new StandardResourceGrowthPolicy();
    }

    public StatAllocationResult Allocate(StatAllocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Progression.UnspentStatPoints <= 0)
        {
            return Rejected(
                request.Progression,
                request.Stats,
                request.Resources,
                ProgressionMutationErrorCode.MissingStatPoints,
                "No unspent stat points are available.");
        }

        decimal currentBase = request.Stats.BaseStats.GetValueOrDefault(request.StatId);
        if (currentBase >= request.StatCap)
        {
            return Rejected(
                request.Progression,
                request.Stats,
                request.Resources,
                ProgressionMutationErrorCode.StatAtCap,
                $"Stat '{request.StatId}' has reached the cap of {request.StatCap}.");
        }

        Dictionary<ContentId, decimal> baseStats = request.Stats.BaseStats.ToDictionary(pair => pair.Key, pair => pair.Value);
        Dictionary<ContentId, decimal> effectiveStats = request.Stats.EffectiveStats.ToDictionary(pair => pair.Key, pair => pair.Value);
        baseStats[request.StatId] = currentBase + 1;
        effectiveStats[request.StatId] = effectiveStats.GetValueOrDefault(request.StatId) + 1;

        var progression = new RuntimeProgressionSnapshot(
            request.Progression.Level,
            request.Progression.Experience,
            request.Progression.LifetimeExperience,
            request.Progression.UnspentStatPoints - 1);
        var stats = new RuntimeStatBlockSnapshot(baseStats, effectiveStats);
        ResourceRecalculationResult resources = _resourceGrowthPolicy.Recalculate(
            new ResourceRecalculationRequest(
                request.Resources,
                request.BaseResourceValues,
                stats.EffectiveStats,
                ResourceCurrentAdjustmentMode.PreserveCurrent));
        return new StatAllocationResult(ProgressionMutationStatus.Applied, progression, stats, resources.Resources);
    }

    public StatAllocationResult Rollback(StatRollbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResourceRecalculationResult resources = _resourceGrowthPolicy.Recalculate(
            new ResourceRecalculationRequest(
                request.Resources,
                request.BaseResourceValues,
                request.RollbackStats.EffectiveStats,
                ResourceCurrentAdjustmentMode.PreserveCurrent));
        return new StatAllocationResult(
            ProgressionMutationStatus.Applied,
            request.RollbackProgression,
            request.RollbackStats,
            resources.Resources);
    }

    private static StatAllocationResult Rejected(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IReadOnlyList<RuntimeResourceSnapshot> resources,
        ProgressionMutationErrorCode code,
        string message) =>
        new(
            ProgressionMutationStatus.Rejected,
            progression,
            stats,
            resources,
            [new ProgressionMutationDiagnostic(code, message)]);
}

internal static class ProgressionCollections
{
    public static IReadOnlyDictionary<TKey, TValue> SnapshotDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>((values ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));

    public static IReadOnlyList<T> SnapshotList<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly((values ?? []).ToArray());
}
