namespace Convergence.Runtime;

public enum RuntimePartyRosterInvariantCode
{
    DuplicateHostedEntityReference,
    DuplicateCompanionReference,
    ActiveHostedEntityNotOwned,
    ActiveHostedEntityReferenceMismatch,
    HostedEntityCompanionRoleCollision
}

public sealed record RuntimePartyRosterInvariantDiagnostic(
    RuntimePartyRosterInvariantCode Code,
    RuntimeInstanceId InstanceId,
    string Path,
    string Message);

public static class RuntimePartyRosterInvariantRules
{
    public static IReadOnlyList<RuntimePartyRosterInvariantDiagnostic> Validate(
        RuntimePartyRosterSnapshot roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        var diagnostics = new List<RuntimePartyRosterInvariantDiagnostic>();
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

        return RuntimeSnapshotCollections.List(diagnostics);
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
