namespace Convergence.Runtime;

public enum RuntimePartyRosterInvariantCode
{
    InvalidReferenceInstanceId,
    InvalidReferenceEntityDefinitionId,
    PartyRosterOwnerMismatch,
    DuplicateActivePartyReference,
    DuplicateReserveReference,
    DuplicateHostedEntityReference,
    DuplicateCompanionReference,
    ActiveReserveRoleCollision,
    ActiveHostedEntityNotOwned,
    ActiveHostedEntityReferenceMismatch,
    HostedEntityCompanionRoleCollision,
    PartyRosterIdentityCollision,
    ActivePartyCapacityExceeded,
    HostedEntityRosterCapacityExceeded,
    CompanionRosterCapacityExceeded
}

public sealed record RuntimePartyRosterInvariantDiagnostic(
    RuntimePartyRosterInvariantCode Code,
    RuntimeInstanceId InstanceId,
    string Path,
    string Message);

public static class RuntimePartyRosterInvariantRules
{
    public static IReadOnlyList<RuntimePartyRosterInvariantDiagnostic> Validate(
        RuntimePartyRosterSnapshot roster,
        RuntimeActorSnapshot? ownerActor,
        IRosterCapacityPolicy rosterCapacityPolicy)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(rosterCapacityPolicy);

        var diagnostics = new List<RuntimePartyRosterInvariantDiagnostic>();
        ValidateReference(roster.Owner, "$.owner", diagnostics);
        ValidateReferences(roster.ActiveParty, "$.activeParty", diagnostics);
        ValidateReferences(roster.ReserveMembers, "$.reserveMembers", diagnostics);
        if (roster.ActiveHostedEntity is RuntimeActorReferenceSnapshot activeReference)
        {
            ValidateReference(activeReference, "$.activeHostedEntity", diagnostics);
        }
        ValidateReferences(roster.HostedEntityRoster, "$.hostedEntityRoster", diagnostics);
        ValidateReferences(roster.CompanionRoster, "$.companionRoster", diagnostics);

        if (ownerActor is not null &&
            (roster.Owner.InstanceId != ownerActor.Identity.InstanceId ||
             roster.Owner.EntityDefinitionId != ownerActor.Identity.EntityDefinitionId))
        {
            diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                RuntimePartyRosterInvariantCode.PartyRosterOwnerMismatch,
                roster.Owner.InstanceId,
                "$.owner",
                $"Party roster owner '{roster.Owner.InstanceId}' does not match supplied actor " +
                $"'{ownerActor.Identity.InstanceId}'."));
        }

        HashSet<RuntimeInstanceId> activeIds = ValidateDuplicates(
            roster.ActiveParty,
            "$.activeParty",
            RuntimePartyRosterInvariantCode.DuplicateActivePartyReference,
            diagnostics);
        ValidateDuplicates(
            roster.ReserveMembers,
            "$.reserveMembers",
            RuntimePartyRosterInvariantCode.DuplicateReserveReference,
            diagnostics);
        HashSet<RuntimeInstanceId> hostedEntityIds = ValidateDuplicates(
            roster.HostedEntityRoster,
            "$.hostedEntityRoster",
            RuntimePartyRosterInvariantCode.DuplicateHostedEntityReference,
            diagnostics);
        ValidateDuplicates(
            roster.CompanionRoster,
            "$.companionRoster",
            RuntimePartyRosterInvariantCode.DuplicateCompanionReference,
            diagnostics);

        for (int index = 0; index < roster.ReserveMembers.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = roster.ReserveMembers[index];
            if (!activeIds.Contains(reference.InstanceId))
            {
                continue;
            }

            diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                RuntimePartyRosterInvariantCode.ActiveReserveRoleCollision,
                reference.InstanceId,
                $"$.reserveMembers[{index}]",
                $"Runtime actor '{reference.InstanceId}' cannot be active and reserve simultaneously."));
        }

        if (roster.ActiveHostedEntity is RuntimeActorReferenceSnapshot activeHostedEntity)
        {
            int ownedIndex = -1;
            for (int index = 0; index < roster.HostedEntityRoster.Count; index++)
            {
                RuntimeActorReferenceSnapshot reference = roster.HostedEntityRoster[index];
                if (reference.InstanceId != activeHostedEntity.InstanceId)
                {
                    continue;
                }

                ownedIndex = index;
                if (reference != activeHostedEntity)
                {
                    diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                        RuntimePartyRosterInvariantCode.ActiveHostedEntityReferenceMismatch,
                        reference.InstanceId,
                        "$.activeHostedEntity",
                        $"Active hosted entity '{reference.InstanceId}' does not match its owned roster reference."));
                }

                break;
            }

            if (ownedIndex < 0)
            {
                diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                    RuntimePartyRosterInvariantCode.ActiveHostedEntityNotOwned,
                    activeHostedEntity.InstanceId,
                    "$.activeHostedEntity",
                    $"Active hosted entity '{activeHostedEntity.InstanceId}' must exist in the hosted-entity roster."));
            }
        }

        for (int index = 0; index < roster.CompanionRoster.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = roster.CompanionRoster[index];
            if (!hostedEntityIds.Contains(reference.InstanceId))
            {
                continue;
            }

            diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                RuntimePartyRosterInvariantCode.HostedEntityCompanionRoleCollision,
                reference.InstanceId,
                $"$.companionRoster[{index}]",
                $"Runtime actor '{reference.InstanceId}' cannot occupy hosted-entity and companion roster roles simultaneously."));
        }

        ValidateIdentityCollisions(roster, diagnostics);

        if (roster.ActiveParty.Count > roster.MaxActivePartySize)
        {
            diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                RuntimePartyRosterInvariantCode.ActivePartyCapacityExceeded,
                roster.Owner.InstanceId,
                "$.activeParty",
                $"Active party has {roster.ActiveParty.Count} members, exceeding the maximum of " +
                $"{roster.MaxActivePartySize}."));
        }

        if (ownerActor is not null)
        {
            int ownerLevel = ownerActor.Progression.Level;
            ValidateRosterCapacity(
                roster.HostedEntityRoster,
                RuntimeRosterKind.HostedEntity,
                ownerLevel,
                rosterCapacityPolicy,
                "$.hostedEntityRoster",
                RuntimePartyRosterInvariantCode.HostedEntityRosterCapacityExceeded,
                roster.Owner.InstanceId,
                diagnostics);
            ValidateRosterCapacity(
                roster.CompanionRoster,
                RuntimeRosterKind.Companion,
                ownerLevel,
                rosterCapacityPolicy,
                "$.companionRoster",
                RuntimePartyRosterInvariantCode.CompanionRosterCapacityExceeded,
                roster.Owner.InstanceId,
                diagnostics);
        }

        return RuntimeSnapshotCollections.List(diagnostics);
    }

    private static void ValidateReference(
        RuntimeActorReferenceSnapshot reference,
        string path,
        ICollection<RuntimePartyRosterInvariantDiagnostic> diagnostics)
    {
        if (!reference.InstanceId.IsValid)
        {
            diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                RuntimePartyRosterInvariantCode.InvalidReferenceInstanceId,
                reference.InstanceId,
                path + ".instanceId",
                $"Actor reference at '{path}' has an invalid runtime instance ID."));
        }

        if (!reference.EntityDefinitionId.IsValid)
        {
            diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                RuntimePartyRosterInvariantCode.InvalidReferenceEntityDefinitionId,
                reference.InstanceId,
                path + ".entityDefinitionId",
                $"Actor reference at '{path}' has an invalid entity definition ID."));
        }
    }

    private static void ValidateReferences(
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        string path,
        ICollection<RuntimePartyRosterInvariantDiagnostic> diagnostics)
    {
        for (int index = 0; index < references.Count; index++)
        {
            ValidateReference(references[index], $"{path}[{index}]", diagnostics);
        }
    }

    private static void ValidateIdentityCollisions(
        RuntimePartyRosterSnapshot roster,
        ICollection<RuntimePartyRosterInvariantDiagnostic> diagnostics)
    {
        foreach (IGrouping<RuntimeInstanceId, RuntimePartyRosterReferenceOccurrence> group in
                 RuntimePartyRosterIdentityRules.Enumerate(roster)
                     .Where(occurrence => occurrence.Reference.InstanceId.IsValid)
                     .GroupBy(occurrence => occurrence.Reference.InstanceId))
        {
            RuntimePartyRosterReferenceOccurrence[] occurrences = group.ToArray();
            HashSet<RuntimePartyRosterReferenceRole> roles = occurrences
                .Select(occurrence => occurrence.Role)
                .ToHashSet();
            for (int currentIndex = 1; currentIndex < occurrences.Length; currentIndex++)
            {
                RuntimePartyRosterReferenceOccurrence current = occurrences[currentIndex];
                RuntimePartyRosterReferenceOccurrence? conflict = null;
                for (int previousIndex = 0; previousIndex < currentIndex; previousIndex++)
                {
                    RuntimePartyRosterReferenceOccurrence previous = occurrences[previousIndex];
                    if (HasDedicatedDiagnostic(previous.Role, current.Role) ||
                        RuntimePartyRosterIdentityRules.IsIntentionalOverlap(previous.Role, current.Role, roles))
                    {
                        continue;
                    }

                    conflict = previous;
                    break;
                }

                if (conflict is not RuntimePartyRosterReferenceOccurrence conflicting)
                {
                    continue;
                }

                diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                    RuntimePartyRosterInvariantCode.PartyRosterIdentityCollision,
                    group.Key,
                    RelativePath(current.Path),
                    $"Runtime actor '{group.Key}' is referenced as both '{conflicting.Role}' and " +
                    $"'{current.Role}', which is not an allowed party/roster overlap."));
            }
        }
    }

    private static bool HasDedicatedDiagnostic(
        RuntimePartyRosterReferenceRole first,
        RuntimePartyRosterReferenceRole second) =>
        first == second ||
        IsRolePair(
            first,
            second,
            RuntimePartyRosterReferenceRole.ActiveParty,
            RuntimePartyRosterReferenceRole.ReserveMember) ||
        IsRolePair(
            first,
            second,
            RuntimePartyRosterReferenceRole.ActiveHostedEntity,
            RuntimePartyRosterReferenceRole.HostedEntityRoster) ||
        IsRolePair(
            first,
            second,
            RuntimePartyRosterReferenceRole.ActiveHostedEntity,
            RuntimePartyRosterReferenceRole.CompanionRoster) ||
        IsRolePair(
            first,
            second,
            RuntimePartyRosterReferenceRole.HostedEntityRoster,
            RuntimePartyRosterReferenceRole.CompanionRoster);

    private static bool IsRolePair(
        RuntimePartyRosterReferenceRole first,
        RuntimePartyRosterReferenceRole second,
        RuntimePartyRosterReferenceRole expectedFirst,
        RuntimePartyRosterReferenceRole expectedSecond) =>
        (first == expectedFirst && second == expectedSecond) ||
        (first == expectedSecond && second == expectedFirst);

    private static string RelativePath(string path)
    {
        const string prefix = "$.partyRoster";
        return path.StartsWith(prefix, StringComparison.Ordinal)
            ? "$" + path[prefix.Length..]
            : path;
    }

    private static void ValidateRosterCapacity(
        IReadOnlyCollection<RuntimeActorReferenceSnapshot> roster,
        RuntimeRosterKind rosterKind,
        int ownerLevel,
        IRosterCapacityPolicy rosterCapacityPolicy,
        string path,
        RuntimePartyRosterInvariantCode code,
        RuntimeInstanceId ownerInstanceId,
        ICollection<RuntimePartyRosterInvariantDiagnostic> diagnostics)
    {
        int capacity = rosterCapacityPolicy.GetCapacity(rosterKind, ownerLevel);
        if (roster.Count <= capacity)
        {
            return;
        }

        diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
            code,
            ownerInstanceId,
            path,
            $"{rosterKind} roster has {roster.Count} entries, exceeding the capacity of {capacity}."));
    }

    private static HashSet<RuntimeInstanceId> ValidateDuplicates(
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        string path,
        RuntimePartyRosterInvariantCode code,
        ICollection<RuntimePartyRosterInvariantDiagnostic> diagnostics)
    {
        var seen = new HashSet<RuntimeInstanceId>();
        for (int index = 0; index < references.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = references[index];
            if (seen.Add(reference.InstanceId))
            {
                continue;
            }

            diagnostics.Add(new RuntimePartyRosterInvariantDiagnostic(
                code,
                reference.InstanceId,
                $"{path}[{index}]",
                $"Runtime actor '{reference.InstanceId}' appears more than once in '{path}'."));
        }

        return seen;
    }
}
