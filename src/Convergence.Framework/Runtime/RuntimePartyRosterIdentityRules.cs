namespace Convergence.Runtime;

internal enum RuntimePartyRosterReferenceRole
{
    Owner,
    ActiveParty,
    ReserveMember,
    ActiveHostedEntity,
    HostedEntityRoster,
    CompanionRoster
}

internal readonly record struct RuntimePartyRosterReferenceOccurrence(
    RuntimePartyRosterReferenceRole Role,
    RuntimeActorReferenceSnapshot Reference,
    string Path);

internal static class RuntimePartyRosterIdentityRules
{
    public static bool ContainsInstanceId(
        RuntimePartyRosterSnapshot snapshot,
        RuntimeInstanceId instanceId) =>
        Enumerate(snapshot).Any(occurrence => occurrence.Reference.InstanceId == instanceId);

    public static IEnumerable<RuntimePartyRosterReferenceOccurrence> Enumerate(
        RuntimePartyRosterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        yield return new RuntimePartyRosterReferenceOccurrence(
            RuntimePartyRosterReferenceRole.Owner,
            snapshot.Owner,
            "$.partyRoster.owner");

        for (int index = 0; index < snapshot.ActiveParty.Count; index++)
        {
            yield return new RuntimePartyRosterReferenceOccurrence(
                RuntimePartyRosterReferenceRole.ActiveParty,
                snapshot.ActiveParty[index],
                $"$.partyRoster.activeParty[{index}]");
        }

        for (int index = 0; index < snapshot.ReserveMembers.Count; index++)
        {
            yield return new RuntimePartyRosterReferenceOccurrence(
                RuntimePartyRosterReferenceRole.ReserveMember,
                snapshot.ReserveMembers[index],
                $"$.partyRoster.reserveMembers[{index}]");
        }

        if (snapshot.ActiveHostedEntity is not null)
        {
            yield return new RuntimePartyRosterReferenceOccurrence(
                RuntimePartyRosterReferenceRole.ActiveHostedEntity,
                snapshot.ActiveHostedEntity,
                "$.partyRoster.activeHostedEntity");
        }

        for (int index = 0; index < snapshot.HostedEntityRoster.Count; index++)
        {
            yield return new RuntimePartyRosterReferenceOccurrence(
                RuntimePartyRosterReferenceRole.HostedEntityRoster,
                snapshot.HostedEntityRoster[index],
                $"$.partyRoster.hostedEntityRoster[{index}]");
        }

        for (int index = 0; index < snapshot.CompanionRoster.Count; index++)
        {
            yield return new RuntimePartyRosterReferenceOccurrence(
                RuntimePartyRosterReferenceRole.CompanionRoster,
                snapshot.CompanionRoster[index],
                $"$.partyRoster.companionRoster[{index}]");
        }
    }

    public static bool IsIntentionalOverlap(
        RuntimePartyRosterReferenceRole first,
        RuntimePartyRosterReferenceRole second,
        IReadOnlySet<RuntimePartyRosterReferenceRole> roles)
    {
        if (IsPair(first, second, RuntimePartyRosterReferenceRole.Owner, RuntimePartyRosterReferenceRole.ActiveParty) ||
            IsPair(first, second, RuntimePartyRosterReferenceRole.ActiveParty, RuntimePartyRosterReferenceRole.CompanionRoster) ||
            IsPair(first, second, RuntimePartyRosterReferenceRole.ActiveHostedEntity, RuntimePartyRosterReferenceRole.HostedEntityRoster))
        {
            return true;
        }

        return IsPair(first, second, RuntimePartyRosterReferenceRole.Owner, RuntimePartyRosterReferenceRole.CompanionRoster) &&
               roles.Contains(RuntimePartyRosterReferenceRole.ActiveParty);
    }

    private static bool IsPair(
        RuntimePartyRosterReferenceRole first,
        RuntimePartyRosterReferenceRole second,
        RuntimePartyRosterReferenceRole expectedFirst,
        RuntimePartyRosterReferenceRole expectedSecond) =>
        (first == expectedFirst && second == expectedSecond) ||
        (first == expectedSecond && second == expectedFirst);
}
