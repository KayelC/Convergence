using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using static JRPGPrototype.Logic.Runtime.ProgressionCollections;

namespace JRPGPrototype.Logic.Runtime;

public static class StandardProgressionIds
{
    public static ContentId Strength { get; } = ContentId.Parse("strength");
    public static ContentId Magic { get; } = ContentId.Parse("magic");
    public static ContentId Vitality { get; } = ContentId.Parse("vitality");
    public static ContentId Agility { get; } = ContentId.Parse("agility");
    public static ContentId Luck { get; } = ContentId.Parse("luck");
    public static ContentId Hp { get; } = ContentId.Parse("hp");
    public static ContentId Sp { get; } = ContentId.Parse("sp");
    public static ContentId Human { get; } = ContentId.Parse("human");
    public static ContentId PersonaUser { get; } = ContentId.Parse("persona_user");
    public static ContentId WildCard { get; } = ContentId.Parse("wild_card");
    public static ContentId Operator { get; } = ContentId.Parse("operator");
    public static ContentId Demon { get; } = ContentId.Parse("demon");
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

public enum ProgressionSubjectKind
{
    Actor,
    Form
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

public sealed record StatModifierTrackAlias(ContentId TrackId, ContentId StatId);

public sealed record StandardStatPolicyConfig
{
    public StandardStatPolicyConfig(
        int statCap = 40,
        decimal buffMultiplier = 1.4m,
        decimal debuffMultiplier = 0.6m,
        IEnumerable<KeyValuePair<ContentId, decimal>>? activeFormWeights = null,
        IEnumerable<StatModifierTrackAlias>? modifierTrackAliases = null)
    {
        if (statCap <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(statCap), "Stat cap must be positive.");
        }
        if (buffMultiplier <= 0 || debuffMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(buffMultiplier), "Stat multipliers must be positive.");
        }

        StatCap = statCap;
        BuffMultiplier = buffMultiplier;
        DebuffMultiplier = debuffMultiplier;
        ActiveFormWeights = SnapshotDictionary(activeFormWeights ?? DefaultWeights());
        ModifierTrackAliases = SnapshotList(modifierTrackAliases ?? DefaultAliases());
    }

    public int StatCap { get; }
    public decimal BuffMultiplier { get; }
    public decimal DebuffMultiplier { get; }
    public IReadOnlyDictionary<ContentId, decimal> ActiveFormWeights { get; }
    public IReadOnlyList<StatModifierTrackAlias> ModifierTrackAliases { get; }

    public static StandardStatPolicyConfig LegacyDefault { get; } = new();

    private static IEnumerable<KeyValuePair<ContentId, decimal>> DefaultWeights()
    {
        yield return new(StandardProgressionIds.Strength, 0.4m);
        yield return new(StandardProgressionIds.Magic, 0.4m);
        yield return new(StandardProgressionIds.Vitality, 0.25m);
        yield return new(StandardProgressionIds.Agility, 0.25m);
        yield return new(StandardProgressionIds.Luck, 0.5m);
    }

    private static IEnumerable<StatModifierTrackAlias> DefaultAliases()
    {
        yield return new(StandardProgressionIds.PhysicalAttack, StandardProgressionIds.Strength);
        yield return new(StandardProgressionIds.MagicalAttack, StandardProgressionIds.Magic);
        yield return new(StandardProgressionIds.Attack, StandardProgressionIds.Strength);
        yield return new(StandardProgressionIds.Attack, StandardProgressionIds.Magic);
        yield return new(StandardProgressionIds.Defense, StandardProgressionIds.Vitality);
        yield return new(StandardProgressionIds.AgilityTrack, StandardProgressionIds.Agility);
    }

    private static IReadOnlyDictionary<TKey, TValue> SnapshotDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(values.ToDictionary(pair => pair.Key, pair => pair.Value));

    private static IReadOnlyList<T> SnapshotList<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());
}

public sealed record StatResolutionRequest
{
    public StatResolutionRequest(
        ContentId actorKindId,
        ContentId statId,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseStats = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? activeFormStats = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers = null,
        IEnumerable<RuntimeStatStageSnapshot>? statStages = null)
    {
        ActorKindId = actorKindId;
        StatId = statId;
        BaseStats = SnapshotDictionary(baseStats);
        ActiveFormStats = SnapshotDictionary(activeFormStats);
        EquipmentStatModifiers = SnapshotDictionary(equipmentStatModifiers);
        StatStages = SnapshotList(statStages);
    }

    public ContentId ActorKindId { get; }
    public ContentId StatId { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseStats { get; }
    public IReadOnlyDictionary<ContentId, decimal> ActiveFormStats { get; }
    public IReadOnlyDictionary<ContentId, decimal> EquipmentStatModifiers { get; }
    public IReadOnlyList<RuntimeStatStageSnapshot> StatStages { get; }
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
        _config = config ?? StandardStatPolicyConfig.LegacyDefault;
    }

    public StatResolutionResult Resolve(StatResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        decimal raw = ResolveRawValue(request);
        int capped = SaturatingFloorToInt(Math.Min(_config.StatCap, Math.Floor(raw)));
        decimal final = capped;

        foreach (RuntimeStatStageSnapshot stage in request.StatStages)
        {
            if (stage.Stage == 0 || !AffectsStat(stage.ModifierTrackId, request.StatId))
            {
                continue;
            }

            final = SaturatingMultiply(
                final,
                stage.Stage > 0 ? _config.BuffMultiplier : _config.DebuffMultiplier);
        }

        return new StatResolutionResult(request.StatId, raw, capped, SaturatingFloorToInt(final));
    }

    private decimal ResolveRawValue(StatResolutionRequest request)
    {
        if (request.ActorKindId == StandardProgressionIds.Demon)
        {
            return ValueOrZero(request.ActiveFormStats, request.StatId);
        }

        decimal baseValue = SaturatingAdd(
            ValueOrZero(request.BaseStats, request.StatId),
            ValueOrZero(request.EquipmentStatModifiers, request.StatId));

        if (request.ActorKindId == StandardProgressionIds.Human ||
            request.ActorKindId == StandardProgressionIds.Operator ||
            !request.ActiveFormStats.ContainsKey(request.StatId))
        {
            return baseValue;
        }

        decimal weight = _config.ActiveFormWeights.TryGetValue(request.StatId, out decimal configured)
            ? configured
            : 0m;
        return SaturatingAdd(
            baseValue,
            SaturatingMultiply(ValueOrZero(request.ActiveFormStats, request.StatId), weight));
    }

    private bool AffectsStat(ContentId trackId, ContentId statId) =>
        _config.ModifierTrackAliases.Any(alias => alias.TrackId == trackId && alias.StatId == statId);

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

    private static decimal SaturatingMultiply(decimal left, decimal right)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            return Math.Sign(left) == Math.Sign(right) ? decimal.MaxValue : decimal.MinValue;
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
        Resources = SnapshotList(resources);
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

public sealed record LevelGrowthRequest
{
    public LevelGrowthRequest(
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        ContentId subjectKindId,
        long experienceAward,
        IRandomSource randomSource,
        ProgressionSubjectKind subjectKind = ProgressionSubjectKind.Actor,
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null)
    {
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        SubjectKindId = subjectKindId;
        ExperienceAward = experienceAward;
        RandomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        SubjectKind = subjectKind;
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
    }

    public RuntimeProgressionSnapshot Progression { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public ContentId SubjectKindId { get; }
    public long ExperienceAward { get; }
    public IRandomSource RandomSource { get; }
    public ProgressionSubjectKind SubjectKind { get; }
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
        RuntimeProgressionSnapshot progression,
        RuntimeStatBlockSnapshot stats,
        IEnumerable<RuntimeResourceSnapshot>? resources = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? baseResourceValues = null,
        IEnumerable<LevelUpEvent>? levelUps = null,
        IEnumerable<ProgressionMutationDiagnostic>? diagnostics = null)
    {
        Status = status;
        Progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Resources = SnapshotList(resources);
        BaseResourceValues = SnapshotDictionary(baseResourceValues);
        LevelUps = SnapshotList(levelUps);
        Diagnostics = SnapshotList(diagnostics);
    }

    public ProgressionMutationStatus Status { get; }
    public bool Applied => Status == ProgressionMutationStatus.Applied;
    public RuntimeProgressionSnapshot Progression { get; }
    public RuntimeStatBlockSnapshot Stats { get; }
    public IReadOnlyList<RuntimeResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<ContentId, decimal> BaseResourceValues { get; }
    public IReadOnlyList<LevelUpEvent> LevelUps { get; }
    public IReadOnlyList<ProgressionMutationDiagnostic> Diagnostics { get; }
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

                if (request.SubjectKind == ProgressionSubjectKind.Form)
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
                    }

                    levelUps.Add(new LevelUpEvent(
                        level,
                        statIncreases: increase > 0
                            ? [new KeyValuePair<ContentId, decimal>(stat, increase)]
                            : []));
                    continue;
                }

                statPoints = checked(statPoints + 1);
                Dictionary<ContentId, decimal> baseResourceIncreases = [];
                if (request.SubjectKindId != StandardProgressionIds.Demon)
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
                ResourceRecalculationResult recalculated = _resourceGrowthPolicy.Recalculate(
                    new ResourceRecalculationRequest(
                        resources,
                        baseResources,
                        effectiveStats,
                        ResourceCurrentAdjustmentMode.LevelUpDelta));
                resources = recalculated.Resources;
                levelUps.Add(new LevelUpEvent(
                    level,
                    baseResourceIncreases: baseResourceIncreases,
                    statPointsAwarded: 1,
                    resourcesBefore: before,
                    resourcesAfter: resources));
            }

            var progression = new RuntimeProgressionSnapshot(level, experience, lifetimeExperience, statPoints);
            var stats = new RuntimeStatBlockSnapshot(baseStats, effectiveStats);
            return new LevelGrowthResult(
                ProgressionMutationStatus.Applied,
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
            request.Progression,
            request.Stats,
            request.Resources,
            request.BaseResourceValues,
            diagnostics: [new ProgressionMutationDiagnostic(code, message)]);
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
