namespace Convergence.Runtime;

public enum RuntimeActorRosterInvariantCode
{
    DuplicateHostedEntityReference,
    DuplicateCompanionReference,
    ActiveHostedEntityDuplicatedInRoster,
    HostedEntityCompanionRoleCollision
}

public sealed record RuntimeActorRosterInvariantDiagnostic(
    RuntimeActorRosterInvariantCode Code,
    RuntimeInstanceId InstanceId,
    string Path,
    string Message);

public static class RuntimeActorRosterInvariantRules
{
    public static IReadOnlyList<RuntimeActorRosterInvariantDiagnostic> Validate(
        RuntimeActorRosterSnapshot roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        var diagnostics = new List<RuntimeActorRosterInvariantDiagnostic>();
        HashSet<RuntimeInstanceId> hostedEntityIds = ValidateDuplicates(
            roster.HostedEntityRoster,
            "$.hostedEntityRoster",
            RuntimeActorRosterInvariantCode.DuplicateHostedEntityReference,
            diagnostics);
        ValidateDuplicates(
            roster.CompanionRoster,
            "$.companionRoster",
            RuntimeActorRosterInvariantCode.DuplicateCompanionReference,
            diagnostics);

        if (roster.ActiveHostedEntity is RuntimeActorReferenceSnapshot activeHostedEntity)
        {
            for (int index = 0; index < roster.HostedEntityRoster.Count; index++)
            {
                RuntimeActorReferenceSnapshot reference = roster.HostedEntityRoster[index];
                if (reference.InstanceId != activeHostedEntity.InstanceId)
                {
                    continue;
                }

                diagnostics.Add(new RuntimeActorRosterInvariantDiagnostic(
                    RuntimeActorRosterInvariantCode.ActiveHostedEntityDuplicatedInRoster,
                    reference.InstanceId,
                    $"$.hostedEntityRoster[{index}]",
                    $"Active hosted entity '{reference.InstanceId}' cannot also appear in the hosted-entity roster."));
            }
        }

        for (int index = 0; index < roster.CompanionRoster.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = roster.CompanionRoster[index];
            if (!hostedEntityIds.Contains(reference.InstanceId))
            {
                continue;
            }

            diagnostics.Add(new RuntimeActorRosterInvariantDiagnostic(
                RuntimeActorRosterInvariantCode.HostedEntityCompanionRoleCollision,
                reference.InstanceId,
                $"$.companionRoster[{index}]",
                $"Runtime actor '{reference.InstanceId}' cannot occupy hosted-entity and companion roster roles simultaneously."));
        }

        return RuntimeSnapshotCollections.List(diagnostics);
    }

    private static HashSet<RuntimeInstanceId> ValidateDuplicates(
        IReadOnlyList<RuntimeActorReferenceSnapshot> references,
        string path,
        RuntimeActorRosterInvariantCode code,
        ICollection<RuntimeActorRosterInvariantDiagnostic> diagnostics)
    {
        var seen = new HashSet<RuntimeInstanceId>();
        for (int index = 0; index < references.Count; index++)
        {
            RuntimeActorReferenceSnapshot reference = references[index];
            if (seen.Add(reference.InstanceId))
            {
                continue;
            }

            diagnostics.Add(new RuntimeActorRosterInvariantDiagnostic(
                code,
                reference.InstanceId,
                $"{path}[{index}]",
                $"Runtime actor '{reference.InstanceId}' appears more than once in '{path}'."));
        }

        return seen;
    }
}
