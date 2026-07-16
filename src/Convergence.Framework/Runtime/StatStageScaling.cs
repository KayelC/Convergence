using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

public enum StatStageScalingChannel
{
    PhysicalDamageDealt,
    MagicalDamageDealt,
    DamageTaken,
    HitChance,
    Evasion
}

public sealed record StatStageMultiplier
{
    public StatStageMultiplier(int stage, decimal multiplier)
    {
        if (stage is < BattleStatStageRange.Minimum or > BattleStatStageRange.Maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                $"Stage must be between {BattleStatStageRange.Minimum} and {BattleStatStageRange.Maximum}.");
        }
        if (multiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                multiplier,
                "Stage multiplier must be positive.");
        }

        Stage = stage;
        Multiplier = multiplier;
    }

    public int Stage { get; }
    public decimal Multiplier { get; }
}

public sealed record StatStageScalingTable
{
    public StatStageScalingTable(
        ContentId trackId,
        StatStageScalingChannel channel,
        IEnumerable<StatStageMultiplier> multipliers)
    {
        if (!trackId.IsValid || trackId.IsQualified)
        {
            throw new ArgumentException(
                "Stage-scaling track ID must be a valid unqualified ID.",
                nameof(trackId));
        }
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Stage-scaling channel is not supported.");
        }

        ArgumentNullException.ThrowIfNull(multipliers);
        StatStageMultiplier[] snapshot = multipliers.ToArray();
        if (snapshot.Any(multiplier => multiplier is null))
        {
            throw new ArgumentException("Stage-scaling tables cannot contain null rows.", nameof(multipliers));
        }
        if (snapshot.Select(multiplier => multiplier.Stage).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Stage-scaling tables cannot contain duplicate stages.", nameof(multipliers));
        }

        int[] expectedStages = Enumerable
            .Range(
                BattleStatStageRange.Minimum,
                BattleStatStageRange.Maximum - BattleStatStageRange.Minimum + 1)
            .ToArray();
        if (!snapshot.Select(multiplier => multiplier.Stage).Order().SequenceEqual(expectedStages))
        {
            throw new ArgumentException(
                $"Stage-scaling tables must define every stage from {BattleStatStageRange.Minimum} " +
                $"through {BattleStatStageRange.Maximum}.",
                nameof(multipliers));
        }

        TrackId = trackId;
        Channel = channel;
        Multipliers = Array.AsReadOnly(snapshot.OrderBy(multiplier => multiplier.Stage).ToArray());
    }

    public ContentId TrackId { get; }
    public StatStageScalingChannel Channel { get; }
    public IReadOnlyList<StatStageMultiplier> Multipliers { get; }

    public decimal GetMultiplier(int stage) =>
        Multipliers.First(multiplier => multiplier.Stage == stage).Multiplier;
}

public sealed record StatStageScalingRequest
{
    public StatStageScalingRequest(
        StatStageScalingChannel channel,
        IEnumerable<RuntimeStatStageSnapshot>? stages = null)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Stage-scaling channel is not supported.");
        }

        RuntimeStatStageSnapshot[] snapshot = (stages ?? []).ToArray();
        if (snapshot.Any(stage => stage is null))
        {
            throw new ArgumentException("Stage-scaling requests cannot contain null stage entries.", nameof(stages));
        }
        if (snapshot.Select(stage => stage.ModifierTrackId).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Stage-scaling requests cannot contain duplicate modifier tracks.",
                nameof(stages));
        }
        foreach (RuntimeStatStageSnapshot stage in snapshot)
        {
            if (stage.Stage is < BattleStatStageRange.Minimum or > BattleStatStageRange.Maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stages),
                    stage.Stage,
                    $"Stage must be between {BattleStatStageRange.Minimum} and {BattleStatStageRange.Maximum}.");
            }
        }

        Channel = channel;
        Stages = Array.AsReadOnly(snapshot);
    }

    public StatStageScalingChannel Channel { get; }
    public IReadOnlyList<RuntimeStatStageSnapshot> Stages { get; }
}

public sealed record AppliedStatStageMultiplier(
    ContentId TrackId,
    int Stage,
    StatStageScalingChannel Channel,
    decimal Multiplier);

public sealed record StatStageScalingResult
{
    public StatStageScalingResult(
        StatStageScalingChannel channel,
        decimal multiplier,
        IEnumerable<AppliedStatStageMultiplier>? appliedMultipliers = null)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Stage-scaling channel is not supported.");
        }
        if (multiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "Resolved multiplier must be positive.");
        }

        Channel = channel;
        Multiplier = multiplier;
        AppliedMultipliers = Array.AsReadOnly((appliedMultipliers ?? []).ToArray());
    }

    public StatStageScalingChannel Channel { get; }
    public decimal Multiplier { get; }
    public IReadOnlyList<AppliedStatStageMultiplier> AppliedMultipliers { get; }
}

public interface IStatStageScalingPolicy
{
    StatStageScalingResult Resolve(StatStageScalingRequest request);
}

public sealed class StandardStatStageScalingPolicy : IStatStageScalingPolicy
{
    private static readonly IReadOnlySet<(ContentId TrackId, StatStageScalingChannel Channel)>
        SupportedMappings = new HashSet<(ContentId, StatStageScalingChannel)>
        {
            (StandardProgressionIds.PhysicalAttack, StatStageScalingChannel.PhysicalDamageDealt),
            (StandardProgressionIds.MagicalAttack, StatStageScalingChannel.MagicalDamageDealt),
            (StandardProgressionIds.Attack, StatStageScalingChannel.PhysicalDamageDealt),
            (StandardProgressionIds.Attack, StatStageScalingChannel.MagicalDamageDealt),
            (StandardProgressionIds.Defense, StatStageScalingChannel.DamageTaken),
            (StandardProgressionIds.AgilityTrack, StatStageScalingChannel.HitChance),
            (StandardProgressionIds.AgilityTrack, StatStageScalingChannel.Evasion)
        };

    private readonly IReadOnlyDictionary<(ContentId TrackId, StatStageScalingChannel Channel), StatStageScalingTable>
        _tables;

    public StandardStatStageScalingPolicy(IEnumerable<StatStageScalingTable>? tableOverrides = null)
    {
        Dictionary<(ContentId, StatStageScalingChannel), StatStageScalingTable> tables =
            DefaultTables().ToDictionary(table => (table.TrackId, table.Channel));
        var overridden = new HashSet<(ContentId, StatStageScalingChannel)>();

        foreach (StatStageScalingTable table in tableOverrides ?? [])
        {
            ArgumentNullException.ThrowIfNull(table);
            var key = (table.TrackId, table.Channel);
            if (!SupportedMappings.Contains(key))
            {
                throw new ArgumentException(
                    $"Track '{table.TrackId}' does not support stage-scaling channel '{table.Channel}'.",
                    nameof(tableOverrides));
            }
            if (!overridden.Add(key))
            {
                throw new ArgumentException(
                    $"Track '{table.TrackId}' and channel '{table.Channel}' are overridden more than once.",
                    nameof(tableOverrides));
            }

            tables[key] = table;
        }

        _tables =
            new System.Collections.ObjectModel.ReadOnlyDictionary<
                (ContentId TrackId, StatStageScalingChannel Channel),
                StatStageScalingTable>(tables);
    }

    public IReadOnlyList<StatStageScalingTable> Tables =>
        Array.AsReadOnly(_tables.Values
            .OrderBy(table => table.TrackId.Value, StringComparer.Ordinal)
            .ThenBy(table => table.Channel)
            .ToArray());

    public StatStageScalingResult Resolve(StatStageScalingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        decimal multiplier = 1m;
        var applied = new List<AppliedStatStageMultiplier>();
        foreach (RuntimeStatStageSnapshot stage in request.Stages)
        {
            if (stage.Stage == 0 ||
                !_tables.TryGetValue((stage.ModifierTrackId, request.Channel), out StatStageScalingTable? table))
            {
                continue;
            }

            decimal stageMultiplier = table.GetMultiplier(stage.Stage);
            multiplier = SaturatingMultiply(multiplier, stageMultiplier);
            applied.Add(new AppliedStatStageMultiplier(
                stage.ModifierTrackId,
                stage.Stage,
                request.Channel,
                stageMultiplier));
        }

        return new StatStageScalingResult(request.Channel, multiplier, applied);
    }

    private static IEnumerable<StatStageScalingTable> DefaultTables()
    {
        StatStageMultiplier[] offense = Multipliers(
            0.50m,
            0.625m,
            0.75m,
            0.875m,
            1.00m,
            1.25m,
            1.50m,
            1.75m,
            2.00m);
        StatStageMultiplier[] damageTaken = Multipliers(
            2.00m,
            1.75m,
            1.50m,
            1.25m,
            1.00m,
            0.875m,
            0.75m,
            0.625m,
            0.50m);

        yield return new(
            StandardProgressionIds.PhysicalAttack,
            StatStageScalingChannel.PhysicalDamageDealt,
            offense);
        yield return new(
            StandardProgressionIds.MagicalAttack,
            StatStageScalingChannel.MagicalDamageDealt,
            offense);
        yield return new(
            StandardProgressionIds.Attack,
            StatStageScalingChannel.PhysicalDamageDealt,
            offense);
        yield return new(
            StandardProgressionIds.Attack,
            StatStageScalingChannel.MagicalDamageDealt,
            offense);
        yield return new(
            StandardProgressionIds.Defense,
            StatStageScalingChannel.DamageTaken,
            damageTaken);
        yield return new(
            StandardProgressionIds.AgilityTrack,
            StatStageScalingChannel.HitChance,
            offense);
        yield return new(
            StandardProgressionIds.AgilityTrack,
            StatStageScalingChannel.Evasion,
            offense);
    }

    private static StatStageMultiplier[] Multipliers(params decimal[] values) =>
        values.Select((multiplier, index) =>
            new StatStageMultiplier(BattleStatStageRange.Minimum + index, multiplier)).ToArray();

    private static decimal SaturatingMultiply(decimal left, decimal right)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            return decimal.MaxValue;
        }
    }
}
