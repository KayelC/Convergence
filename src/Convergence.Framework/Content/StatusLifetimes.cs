namespace Convergence.Content;

/// <summary>Identifies why active runtime status state is being removed.</summary>
public enum StatusRemovalCause
{
    CureEffect,
    DispelEffect,
    NaturalRecovery,
    RecoveryEvent,
    DurationExpired,
    ExclusivityReplacement,
    DeploymentSwap,
    Defeat,
    Flee,
    RosterRecall,
    BattleEnd,
    FieldTransition,
    Consumed,
    ScriptedRemoval
}

/// <summary>
/// Defines the exact causes permitted to remove one active status. Expiration,
/// recovery, and encounter cleanup remain independent decisions.
/// </summary>
public sealed record StatusRemovalProfileDefinition : IEquatable<StatusRemovalProfileDefinition>
{
    public StatusRemovalProfileDefinition(IEnumerable<StatusRemovalCause> allowedCauses)
    {
        StatusRemovalCause[] snapshot =
            (allowedCauses ?? throw new ArgumentNullException(nameof(allowedCauses)))
            .Distinct()
            .Order()
            .ToArray();
        foreach (StatusRemovalCause cause in snapshot)
        {
            if (!Enum.IsDefined(cause))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(allowedCauses),
                    cause,
                    "Status removal causes must be defined.");
            }
        }

        AllowedCauses = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<StatusRemovalCause> AllowedCauses { get; }

    public bool Allows(StatusRemovalCause cause)
    {
        if (!Enum.IsDefined(cause))
        {
            throw new ArgumentOutOfRangeException(nameof(cause));
        }

        return AllowedCauses.Contains(cause);
    }

    public StatusRemovalProfileDefinition Without(params StatusRemovalCause[] causes)
    {
        ArgumentNullException.ThrowIfNull(causes);
        foreach (StatusRemovalCause cause in causes)
        {
            if (!Enum.IsDefined(cause))
            {
                throw new ArgumentOutOfRangeException(nameof(causes), cause, "Status removal causes must be defined.");
            }
        }

        HashSet<StatusRemovalCause> excluded = causes.ToHashSet();
        return new StatusRemovalProfileDefinition(AllowedCauses.Where(cause => !excluded.Contains(cause)));
    }

    public bool Equals(StatusRemovalProfileDefinition? other) =>
        other is not null && AllowedCauses.SequenceEqual(other.AllowedCauses);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (StatusRemovalCause cause in AllowedCauses)
        {
            hash.Add(cause);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Supplied removal profiles that games may use or replace.</summary>
public static class StatusRemovalProfiles
{
    public static StatusRemovalProfileDefinition Standard { get; } =
        new(Enum.GetValues<StatusRemovalCause>());

    public static StatusRemovalProfileDefinition Uncurable { get; } =
        Standard.Without(
            StatusRemovalCause.CureEffect,
            StatusRemovalCause.NaturalRecovery,
            StatusRemovalCause.RecoveryEvent);

    public static StatusRemovalProfileDefinition Protected { get; } =
        new([
            StatusRemovalCause.DurationExpired,
            StatusRemovalCause.ScriptedRemoval
        ]);
}

/// <summary>Supplied lifetime values for common runtime status behavior.</summary>
public static class StandardStatusLifetimes
{
    public static StatusLifetimeDefinition Deployment(DurationDefinition expiration) =>
        new(
            expiration ?? throw new ArgumentNullException(nameof(expiration)),
            StatusRemovalProfiles.Standard);

    public static StatusLifetimeDefinition Encounter(DurationDefinition expiration) =>
        new(
            expiration ?? throw new ArgumentNullException(nameof(expiration)),
            StatusRemovalProfiles.Standard.Without(
                StatusRemovalCause.DeploymentSwap,
                StatusRemovalCause.RosterRecall));

    public static StatusLifetimeDefinition Field(DurationDefinition expiration) =>
        new(
            expiration ?? throw new ArgumentNullException(nameof(expiration)),
            StatusRemovalProfiles.Standard.Without(
                StatusRemovalCause.DeploymentSwap,
                StatusRemovalCause.Defeat,
                StatusRemovalCause.Flee,
                StatusRemovalCause.RosterRecall,
                StatusRemovalCause.BattleEnd,
                StatusRemovalCause.FieldTransition));

    public static StatusLifetimeDefinition DeploymentTransient { get; } =
        Deployment(new PermanentDurationDefinition());

    public static StatusLifetimeDefinition Persistent { get; } =
        Field(new PermanentDurationDefinition());

    public static StatusLifetimeDefinition ProtectedPersistent { get; } =
        new(new PermanentDurationDefinition(), StatusRemovalProfiles.Protected);

    public static StatusLifetimeDefinition PersistentAilment(DurationDefinition expiration) =>
        Field(expiration);
}

/// <summary>
/// Combines one expiration rule with an independent set of allowed removal
/// causes. The duration never implies battle or field persistence.
/// </summary>
public sealed record StatusLifetimeDefinition
{
    public StatusLifetimeDefinition(
        DurationDefinition expiration,
        StatusRemovalProfileDefinition removalProfile)
    {
        Expiration = expiration ?? throw new ArgumentNullException(nameof(expiration));
        RemovalProfile = removalProfile ?? throw new ArgumentNullException(nameof(removalProfile));
        if (expiration is InstantDurationDefinition or TurnDurationDefinition or PhaseDurationDefinition &&
            !removalProfile.Allows(StatusRemovalCause.DurationExpired))
        {
            throw new ArgumentException(
                "A status with a clock-driven expiration must permit duration expiry.",
                nameof(removalProfile));
        }
    }

    public DurationDefinition Expiration { get; } = null!;
    public StatusRemovalProfileDefinition RemovalProfile { get; } = null!;

    public bool Allows(StatusRemovalCause cause) => RemovalProfile.Allows(cause);

    internal StatusLifetimeDefinition WithExpiration(DurationDefinition expiration) =>
        new(expiration, RemovalProfile);
}
