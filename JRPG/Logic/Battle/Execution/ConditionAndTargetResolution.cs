using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Battle.Execution;

internal static class BattleAmountResolver
{
    public static decimal Resolve(
        AmountDefinition amount,
        AmountResolutionContext context,
        BattleExecutionServices services)
    {
        BattleResourceState resource = context.Target.GetRequiredResource(context.ResourceId);
        decimal value = amount switch
        {
            FlatAmountDefinition flat => flat.Value,
            PercentMaximumAmountDefinition percent => resource.Maximum * percent.Value / 100m,
            PercentCurrentAmountDefinition percent => resource.Current * percent.Value / 100m,
            FullAmountDefinition => resource.Maximum,
            PowerAmountDefinition power => services.PowerAmountPolicy.Resolve(power, context),
            FormulaAmountDefinition formula when services.FormulaHandlers.TryGetValue(
                formula.FormulaId,
                out IFormulaAmountHandler? handler) => handler.Resolve(formula, context),
            FormulaAmountDefinition formula => throw new KeyNotFoundException(
                $"No formula handler is registered for '{formula.FormulaId}'."),
            _ => throw new ArgumentOutOfRangeException(nameof(amount), amount, "Unsupported amount definition.")
        };

        return Math.Max(0, value);
    }
}

internal static class BattleConditionEvaluator
{
    public static bool Evaluate(ConditionDefinition? condition, EffectExecutionContext context)
    {
        return condition is null || condition switch
        {
            AllConditionDefinition all => all.Conditions.All(child => Evaluate(child, context)),
            AnyConditionDefinition any => any.Conditions.Any(child => Evaluate(child, context)),
            NotConditionDefinition not => !Evaluate(not.Condition, context),
            ResourcePercentageConditionDefinition resource => EvaluateResource(resource, context),
            HasAilmentConditionDefinition ailment => Subject(ailment.Subject, context) is BattleActorState actor &&
                ailment.AilmentIds.Any(actor.HasAilment),
            HasSkillConditionDefinition skill => Subject(skill.Subject, context)?.HasSkill(skill.SkillId) == true,
            HasBuffConditionDefinition buff => Subject(buff.Subject, context)?.HasBuff(buff.ModifierTrackId) == true,
            HasAffinityConditionDefinition affinity =>
                Subject(affinity.Subject, context)?.GetElementalAffinity(affinity.Element) == affinity.Affinity,
            HasCapabilityConditionDefinition capability =>
                Subject(capability.Subject, context)?.HasCapability(capability.CapabilityId) == true,
            LifeStateConditionDefinition life => EvaluateLifeState(Subject(life.Subject, context), life.LifeState),
            BattleKindConditionDefinition battle => battle.AllowedBattleKindIds.Contains(context.Request.BattleKindId),
            MoonPhaseConditionDefinition moon => moon.AllowedMoonPhaseIds.Contains(context.Request.MoonPhaseId),
            PartySizeConditionDefinition party => Compare(
                context.Request.Participants.Count(candidate =>
                    candidate.IsActive && candidate.TeamId == context.Actor.TeamId && !candidate.IsDefeated),
                party.Comparison,
                party.Value),
            ChanceConditionDefinition chance => context.Services.ChancePolicy.Roll(
                new ChancePolicyRequest(chance.Chance, context.Actor, context.Target, "condition")),
            EffectElementConditionDefinition element => context.EffectElement == element.Element,
            CustomConditionDefinition custom when context.Services.CustomConditionHandlers.TryGetValue(
                custom.HandlerId,
                out ICustomConditionHandler? handler) => handler.Evaluate(custom, context),
            CustomConditionDefinition custom => throw new KeyNotFoundException(
                $"No custom condition handler is registered for '{custom.HandlerId}'."),
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unsupported condition definition.")
        };
    }

    private static bool EvaluateResource(ResourcePercentageConditionDefinition condition, EffectExecutionContext context)
    {
        BattleActorState? actor = Subject(condition.Subject, context);
        if (actor is null || !actor.TryGetResource(condition.ResourceId, out BattleResourceState? resource) || resource is null)
        {
            return false;
        }

        decimal percentage = resource.Maximum == 0 ? 0 : resource.Current * 100m / resource.Maximum;
        return Compare(percentage, condition.Comparison, condition.Value);
    }

    private static BattleActorState? Subject(ConditionSubject subject, EffectExecutionContext context) =>
        subject == ConditionSubject.Actor ? context.Actor : context.Target;

    private static bool EvaluateLifeState(BattleActorState? actor, TargetLifeState lifeState) =>
        actor is not null && lifeState switch
        {
            TargetLifeState.Alive => !actor.IsDefeated,
            TargetLifeState.Dead => actor.IsDefeated,
            TargetLifeState.Any => true,
            _ => false
        };

    private static bool Compare(decimal actual, NumericComparison comparison, decimal expected) => comparison switch
    {
        NumericComparison.LessThan => actual < expected,
        NumericComparison.LessThanOrEqual => actual <= expected,
        NumericComparison.Equal => actual == expected,
        NumericComparison.GreaterThanOrEqual => actual >= expected,
        NumericComparison.GreaterThan => actual > expected,
        _ => false
    };
}

internal static class BattleTargetResolver
{
    public static bool TryResolve(
        SkillExecutionRequest request,
        BattleExecutionServices services,
        out ResolvedTargetSet? resolved,
        out SkillExecutionDiagnostic? diagnostic)
    {
        TargetingDefinition? targeting = request.Skill.Targeting;
        if (request.Participants.Select(participant => participant.InstanceId).Distinct().Count() !=
            request.Participants.Count)
        {
            resolved = null;
            diagnostic = new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.TargetSelectionInvalid,
                "Battle participant instance IDs must be unique.");
            return false;
        }

        if (targeting is null)
        {
            resolved = null;
            diagnostic = new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.TargetingInvalid,
                "Active skill targeting is missing.");
            return false;
        }

        if (targeting.Relation == TargetRelation.None && targeting.Selection == TargetSelection.None)
        {
            resolved = new ResolvedTargetSet([], true);
            diagnostic = null;
            return true;
        }

        BattleActorState[] eligible = request.Participants
            .Where(candidate => candidate.IsActive)
            .Where(candidate => RelationMatches(request.Actor, candidate, targeting.Relation))
            .Where(candidate => targeting.Relation == TargetRelation.Self ||
                                targeting.AllowSelf ||
                                candidate.InstanceId != request.Actor.InstanceId)
            .Where(candidate => LifeStateMatches(candidate, targeting.LifeState))
            .ToArray();

        IReadOnlyList<BattleActorState> targets;
        switch (targeting.Selection)
        {
            case TargetSelection.Single:
                targets = ResolveSelected(request, eligible);
                if (request.SelectedTargetIds.Count != request.SelectedTargetIds.Distinct().Count() ||
                    targets.Count != request.SelectedTargetIds.Count)
                {
                    resolved = null;
                    diagnostic = new SkillExecutionDiagnostic(
                        SkillExecutionDiagnosticCode.TargetSelectionInvalid,
                        "Every selected target must be unique and eligible for the skill's targeting rules.");
                    return false;
                }
                break;
            case TargetSelection.All:
                targets = Array.AsReadOnly(eligible);
                break;
            case TargetSelection.Random:
                TargetCountDefinition count = targeting.Count ?? new TargetCountDefinition(1, 1);
                targets = services.RandomTargetPolicy.Select(Array.AsReadOnly(eligible), count, request);
                if (targets.Any(target => !eligible.Contains(target)) || targets.Select(target => target.InstanceId).Distinct().Count() != targets.Count)
                {
                    resolved = null;
                    diagnostic = new SkillExecutionDiagnostic(
                        SkillExecutionDiagnosticCode.TargetSelectionInvalid,
                        "The random-target policy returned an ineligible or duplicate target.");
                    return false;
                }
                break;
            default:
                resolved = null;
                diagnostic = new SkillExecutionDiagnostic(
                    SkillExecutionDiagnosticCode.TargetingInvalid,
                    "The targeting relation and selection are incompatible.");
                return false;
        }

        TargetCountDefinition expected = targeting.Count ?? new TargetCountDefinition(
            1,
            targeting.Selection == TargetSelection.All ? int.MaxValue : 1);
        if (targets.Count < expected.Minimum || targets.Count > expected.Maximum)
        {
            resolved = null;
            diagnostic = new SkillExecutionDiagnostic(
                SkillExecutionDiagnosticCode.TargetSelectionInvalid,
                $"Target selection produced {targets.Count} target(s); expected {expected.Minimum} through {expected.Maximum}.");
            return false;
        }

        resolved = new ResolvedTargetSet(targets);
        diagnostic = null;
        return true;
    }

    private static IReadOnlyList<BattleActorState> ResolveSelected(
        SkillExecutionRequest request,
        IReadOnlyList<BattleActorState> eligible)
    {
        var byId = eligible.ToDictionary(target => target.InstanceId);
        var targets = new List<BattleActorState>();
        var seen = new HashSet<ContentId>();
        foreach (ContentId selectedId in request.SelectedTargetIds)
        {
            if (seen.Add(selectedId) && byId.TryGetValue(selectedId, out BattleActorState? target))
            {
                targets.Add(target);
            }
        }

        return Array.AsReadOnly(targets.ToArray());
    }

    private static bool RelationMatches(BattleActorState actor, BattleActorState candidate, TargetRelation relation) =>
        relation switch
        {
            TargetRelation.Self => candidate.InstanceId == actor.InstanceId,
            TargetRelation.Ally => candidate.TeamId == actor.TeamId,
            TargetRelation.Enemy => candidate.TeamId != actor.TeamId,
            TargetRelation.Any => true,
            _ => false
        };

    private static bool LifeStateMatches(BattleActorState actor, TargetLifeState lifeState) => lifeState switch
    {
        TargetLifeState.Alive => !actor.IsDefeated,
        TargetLifeState.Dead => actor.IsDefeated,
        TargetLifeState.Any => true,
        _ => false
    };
}
