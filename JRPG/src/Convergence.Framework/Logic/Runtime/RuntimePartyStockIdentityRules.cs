namespace JRPGPrototype.Logic.Runtime;

internal enum RuntimePartyStockReferenceRole
{
    Owner,
    ActiveParty,
    ReserveMember,
    ActiveForm,
    PersonaStock,
    DemonStock
}

internal readonly record struct RuntimePartyStockReferenceOccurrence(
    RuntimePartyStockReferenceRole Role,
    RuntimeActorReferenceSnapshot Reference,
    string Path);

internal static class RuntimePartyStockIdentityRules
{
    public static bool ContainsInstanceId(
        RuntimePartyStockSnapshot snapshot,
        RuntimeInstanceId instanceId) =>
        Enumerate(snapshot).Any(occurrence => occurrence.Reference.InstanceId == instanceId);

    public static IEnumerable<RuntimePartyStockReferenceOccurrence> Enumerate(
        RuntimePartyStockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        yield return new RuntimePartyStockReferenceOccurrence(
            RuntimePartyStockReferenceRole.Owner,
            snapshot.Owner,
            "$.partyStock.owner");

        for (int index = 0; index < snapshot.ActiveParty.Count; index++)
        {
            yield return new RuntimePartyStockReferenceOccurrence(
                RuntimePartyStockReferenceRole.ActiveParty,
                snapshot.ActiveParty[index],
                $"$.partyStock.activeParty[{index}]");
        }

        for (int index = 0; index < snapshot.ReserveMembers.Count; index++)
        {
            yield return new RuntimePartyStockReferenceOccurrence(
                RuntimePartyStockReferenceRole.ReserveMember,
                snapshot.ReserveMembers[index],
                $"$.partyStock.reserveMembers[{index}]");
        }

        if (snapshot.ActiveForm is not null)
        {
            yield return new RuntimePartyStockReferenceOccurrence(
                RuntimePartyStockReferenceRole.ActiveForm,
                snapshot.ActiveForm,
                "$.partyStock.activeForm");
        }

        for (int index = 0; index < snapshot.PersonaStock.Count; index++)
        {
            yield return new RuntimePartyStockReferenceOccurrence(
                RuntimePartyStockReferenceRole.PersonaStock,
                snapshot.PersonaStock[index],
                $"$.partyStock.personaStock[{index}]");
        }

        for (int index = 0; index < snapshot.DemonStock.Count; index++)
        {
            yield return new RuntimePartyStockReferenceOccurrence(
                RuntimePartyStockReferenceRole.DemonStock,
                snapshot.DemonStock[index],
                $"$.partyStock.demonStock[{index}]");
        }
    }

    public static bool IsIntentionalOverlap(
        RuntimePartyStockReferenceRole first,
        RuntimePartyStockReferenceRole second,
        IReadOnlySet<RuntimePartyStockReferenceRole> roles)
    {
        if (IsPair(first, second, RuntimePartyStockReferenceRole.Owner, RuntimePartyStockReferenceRole.ActiveParty) ||
            IsPair(first, second, RuntimePartyStockReferenceRole.ActiveParty, RuntimePartyStockReferenceRole.DemonStock))
        {
            return true;
        }

        return IsPair(first, second, RuntimePartyStockReferenceRole.Owner, RuntimePartyStockReferenceRole.DemonStock) &&
               roles.Contains(RuntimePartyStockReferenceRole.ActiveParty);
    }

    private static bool IsPair(
        RuntimePartyStockReferenceRole first,
        RuntimePartyStockReferenceRole second,
        RuntimePartyStockReferenceRole expectedFirst,
        RuntimePartyStockReferenceRole expectedSecond) =>
        (first == expectedFirst && second == expectedSecond) ||
        (first == expectedSecond && second == expectedFirst);
}
