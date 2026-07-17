using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

internal sealed record StatModifierApplicationEvaluation(
    bool Accepted,
    bool StateChanged,
    RuntimeStatModifierStateSnapshot Before,
    RuntimeStatModifierStateSnapshot After,
    IReadOnlyList<StatModifierTransitionResult> Transitions,
    decimal AggregateStageDelta,
    string? RejectionDetail);

internal static class StatModifierExecution
{
    internal static StatModifierApplicationEvaluation AssessApplication(
        RuntimeActorState target,
        ModifyStatStageEffectDefinition definition,
        EffectExecutionEnvironment environment,
        IStatModifierPolicyService service) =>
        EvaluateApplication(target, definition, environment, service, execute: false);

    internal static StatModifierApplicationEvaluation Apply(
        RuntimeActorState target,
        ModifyStatStageEffectDefinition definition,
        EffectExecutionEnvironment environment,
        IStatModifierPolicyService service)
    {
        StatModifierApplicationEvaluation evaluation = EvaluateApplication(
            target,
            definition,
            environment,
            service,
            execute: true);
        if (evaluation.Accepted && evaluation.StateChanged)
        {
            target.ReplaceStatModifierState(service, evaluation.After);
        }

        return evaluation;
    }

    private static StatModifierApplicationEvaluation EvaluateApplication(
        RuntimeActorState target,
        ModifyStatStageEffectDefinition definition,
        EffectExecutionEnvironment environment,
        IStatModifierPolicyService service,
        bool execute)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(service);

        RuntimeStatModifierStateSnapshot before = target.ResolveStatModifierState(service);
        RuntimeStatModifierStateSnapshot current = before;
        var transitions = new List<StatModifierTransitionResult>();
        decimal aggregateDelta = 0;
        foreach (ContentId trackId in definition.ModifierTrackIds)
        {
            int previousStage = current.TryGetTrack(trackId, out RuntimeStatModifierTrackSnapshot? previous)
                ? previous!.ResolvedStage
                : 0;
            var request = new StatModifierApplicationRequest(
                current,
                trackId,
                definition.StageDelta,
                definition.Duration,
                target.IsDeployed,
                environment.FindStatModifierBoundary(definition.Duration));
            StatModifierTransitionResult transition = execute
                ? service.Apply(request)
                : service.AssessApplication(request);
            transitions.Add(transition);
            if (!transition.Accepted)
            {
                return new StatModifierApplicationEvaluation(
                    Accepted: false,
                    StateChanged: false,
                    before,
                    before,
                    Array.AsReadOnly(transitions.ToArray()),
                    AggregateStageDelta: 0,
                    RejectionDetail: string.Join("; ", transition.Diagnostics.Select(value => value.Message)));
            }

            current = transition.After;
            int currentStage = current.TryGetTrack(trackId, out RuntimeStatModifierTrackSnapshot? after)
                ? after!.ResolvedStage
                : 0;
            aggregateDelta += currentStage - previousStage;
        }

        return new StatModifierApplicationEvaluation(
            Accepted: true,
            StateChanged: transitions.Any(transition => transition.StateChanged),
            before,
            current,
            Array.AsReadOnly(transitions.ToArray()),
            aggregateDelta,
            RejectionDetail: null);
    }

    internal static StatModifierTransitionResult? AssessRemoval(
        RuntimeActorState target,
        IReadOnlySet<StatusEffectKind> kinds,
        IStatModifierPolicyService service)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(service);

        bool removePositive = kinds.Contains(StatusEffectKind.Buff);
        bool removeNegative = kinds.Contains(StatusEffectKind.Debuff);
        if (!removePositive && !removeNegative)
        {
            return null;
        }

        RuntimeStatModifierStateSnapshot state = target.ResolveStatModifierState(service);
        StatModifierRemovalMode mode = removePositive && removeNegative
            ? StatModifierRemovalMode.All
            : removePositive
                ? StatModifierRemovalMode.Positive
                : StatModifierRemovalMode.Negative;
        return service.Remove(new StatModifierRemovalRequest(state, mode));
    }

    internal static StatModifierTransitionResult? Remove(
        RuntimeActorState target,
        IReadOnlySet<StatusEffectKind> kinds,
        IStatModifierPolicyService service)
    {
        StatModifierTransitionResult? transition = AssessRemoval(target, kinds, service);
        if (transition is { Accepted: true, StateChanged: true })
        {
            target.ReplaceStatModifierState(service, transition.After);
        }

        return transition;
    }
}
