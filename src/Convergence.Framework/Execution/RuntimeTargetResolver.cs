using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

internal static class RuntimeTargetResolver
{
    public static bool TryResolve(
        EffectActionExecutionRequest request,
        BattleExecutionServices services,
        out ResolvedRuntimeTargetSet? resolved,
        out string? diagnostic)
    {
        if (request.Participants.Select(participant => participant.InstanceId).Distinct().Count() !=
            request.Participants.Count)
        {
            resolved = null;
            diagnostic = "Runtime participant instance IDs must be unique.";
            return false;
        }

        TargetingDefinition targeting = request.Targeting;
        if (targeting.Relation == TargetRelation.None && targeting.Selection == TargetSelection.None)
        {
            resolved = new ResolvedRuntimeTargetSet([], true);
            diagnostic = null;
            return true;
        }

        RuntimeActorState[] eligible = GetEligibleTargets(request, targeting);

        IReadOnlyList<RuntimeActorState> targets;
        switch (targeting.Selection)
        {
            case TargetSelection.Single:
                targets = ResolveSelected(request.SelectedTargetIds, eligible);
                if (request.SelectedTargetIds.Count != request.SelectedTargetIds.Distinct().Count() ||
                    targets.Count != request.SelectedTargetIds.Count)
                {
                    resolved = null;
                    diagnostic = "Every selected target must be unique and eligible for the targeting rules.";
                    return false;
                }
                break;
            case TargetSelection.All:
                targets = Array.AsReadOnly(eligible);
                break;
            case TargetSelection.Random:
                TargetCountDefinition randomCount = targeting.Count ?? new TargetCountDefinition(1, 1);
                targets = services.RuntimeRandomTargetPolicy.Select(Array.AsReadOnly(eligible), randomCount, request);
                if (targets.Any(target => !eligible.Contains(target)) ||
                    targets.Select(target => target.InstanceId).Distinct().Count() != targets.Count)
                {
                    resolved = null;
                    diagnostic = "The random-target policy returned an ineligible or duplicate target.";
                    return false;
                }
                break;
            default:
                resolved = null;
                diagnostic = "The targeting relation and selection are incompatible.";
                return false;
        }

        TargetCountDefinition expected = targeting.Count ?? new TargetCountDefinition(
            1,
            targeting.Selection == TargetSelection.All ? int.MaxValue : 1);
        if (targets.Count < expected.Minimum || targets.Count > expected.Maximum)
        {
            resolved = null;
            diagnostic = $"Target selection produced {targets.Count} target(s); expected {expected.Minimum} through {expected.Maximum}.";
            return false;
        }

        resolved = new ResolvedRuntimeTargetSet(targets);
        diagnostic = null;
        return true;
    }

    public static bool TryValidatePreparedTargets(
        EffectActionExecutionRequest request,
        ResolvedRuntimeTargetSet prepared,
        out string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(prepared);

        if (request.Participants.Select(participant => participant.InstanceId).Distinct().Count() !=
            request.Participants.Count)
        {
            diagnostic = "Runtime participant instance IDs must be unique.";
            return false;
        }

        TargetingDefinition targeting = request.Targeting;
        if (targeting.Relation == TargetRelation.None && targeting.Selection == TargetSelection.None)
        {
            bool validUntargeted = prepared.IsUntargeted && prepared.Targets.Count == 0;
            diagnostic = validUntargeted
                ? null
                : "The prepared targets no longer match the untargeted action rules.";
            return validUntargeted;
        }

        if (prepared.IsUntargeted)
        {
            diagnostic = "A targeted action cannot execute with an untargeted assessment.";
            return false;
        }

        RuntimeInstanceId[] eligibleIds = GetEligibleTargets(request, targeting)
            .Select(target => target.InstanceId)
            .ToArray();
        RuntimeInstanceId[] preparedIds = prepared.Targets
            .Select(target => target.InstanceId)
            .ToArray();
        if (preparedIds.Distinct().Count() != preparedIds.Length ||
            preparedIds.Any(targetId => !eligibleIds.Contains(targetId)))
        {
            diagnostic = "One or more prepared targets are no longer eligible for the action's targeting rules.";
            return false;
        }

        bool selectionMatches = targeting.Selection switch
        {
            TargetSelection.Single => preparedIds.SequenceEqual(request.SelectedTargetIds),
            TargetSelection.All => preparedIds.SequenceEqual(eligibleIds),
            TargetSelection.Random => true,
            _ => false
        };
        if (!selectionMatches)
        {
            diagnostic = "The prepared targets no longer match the authored selection rule.";
            return false;
        }

        TargetCountDefinition expected = targeting.Count ?? new TargetCountDefinition(
            1,
            targeting.Selection == TargetSelection.All ? int.MaxValue : 1);
        if (preparedIds.Length < expected.Minimum || preparedIds.Length > expected.Maximum)
        {
            diagnostic =
                $"Prepared target selection contains {preparedIds.Length} target(s); " +
                $"expected {expected.Minimum} through {expected.Maximum}.";
            return false;
        }

        diagnostic = null;
        return true;
    }

    private static IReadOnlyList<RuntimeActorState> ResolveSelected(
        IEnumerable<RuntimeInstanceId> selectedTargetIds,
        IReadOnlyList<RuntimeActorState> eligible)
    {
        var byId = eligible.ToDictionary(target => target.InstanceId);
        var targets = new List<RuntimeActorState>();
        var seen = new HashSet<RuntimeInstanceId>();
        foreach (RuntimeInstanceId selectedId in selectedTargetIds)
        {
            if (seen.Add(selectedId) && byId.TryGetValue(selectedId, out RuntimeActorState? target))
            {
                targets.Add(target);
            }
        }

        return Array.AsReadOnly(targets.ToArray());
    }

    private static RuntimeActorState[] GetEligibleTargets(
        EffectActionExecutionRequest request,
        TargetingDefinition targeting) =>
        request.Participants
            .Where(candidate => candidate.IsDeployed)
            .Where(candidate => RelationMatches(request.Actor, candidate, targeting.Relation))
            .Where(candidate => targeting.Relation == TargetRelation.Self ||
                                targeting.AllowSelf ||
                                candidate.InstanceId != request.Actor.InstanceId)
            .Where(candidate => LifeStateMatches(candidate, targeting.LifeState))
            .ToArray();

    private static bool RelationMatches(RuntimeActorState actor, RuntimeActorState candidate, TargetRelation relation) =>
        relation switch
        {
            TargetRelation.Self => candidate.InstanceId == actor.InstanceId,
            TargetRelation.Ally => candidate.TeamId == actor.TeamId,
            TargetRelation.Enemy => candidate.TeamId != actor.TeamId,
            TargetRelation.Any => true,
            _ => false
        };

    private static bool LifeStateMatches(RuntimeActorState actor, TargetLifeState lifeState) => lifeState switch
    {
        TargetLifeState.Alive => !actor.IsDefeated,
        TargetLifeState.Dead => actor.IsDefeated,
        TargetLifeState.Any => true,
        _ => false
    };
}
