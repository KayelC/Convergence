using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

public enum RuntimeActorGrowthCompositionStatus
{
    Applied,
    GrowthRejected,
    SkillUnlockPlanningRejected,
    CombatProfileCompositionRejected,
    CommitRejected
}

public enum RuntimeActorGrowthCompositionDiagnosticCode
{
    GrowthRejected,
    SkillUnlockPlanningRejected,
    SkillStateRejected,
    CombatProfileCompositionRejected,
    CommitFailed
}

public sealed record RuntimeActorGrowthCompositionDiagnostic(
    RuntimeActorGrowthCompositionDiagnosticCode Code,
    string Message,
    string Path);

public sealed record RuntimeActorGrowthCompositionRequest
{
    public RuntimeActorGrowthCompositionRequest(
        RuntimeActorState growthActor,
        EntityDefinition growthEntity,
        LevelGrowthResult growth,
        IRuntimeMoveListCapacityPolicy moveListCapacityPolicy,
        RuntimeActorCombatProfileCompositionRequest combatProfileComposition)
    {
        GrowthActor = growthActor ?? throw new ArgumentNullException(nameof(growthActor));
        GrowthEntity = growthEntity ?? throw new ArgumentNullException(nameof(growthEntity));
        Growth = growth ?? throw new ArgumentNullException(nameof(growth));
        MoveListCapacityPolicy = moveListCapacityPolicy ??
            throw new ArgumentNullException(nameof(moveListCapacityPolicy));
        CombatProfileComposition = combatProfileComposition ??
            throw new ArgumentNullException(nameof(combatProfileComposition));
    }

    public RuntimeActorState GrowthActor { get; }
    public EntityDefinition GrowthEntity { get; }
    public LevelGrowthResult Growth { get; }
    public IRuntimeMoveListCapacityPolicy MoveListCapacityPolicy { get; }
    public RuntimeActorCombatProfileCompositionRequest CombatProfileComposition { get; }
}

public sealed record RuntimeActorGrowthCompositionResult
{
    public RuntimeActorGrowthCompositionResult(
        RuntimeActorGrowthCompositionStatus status,
        RuntimeActorSnapshot growthActorBefore,
        RuntimeActorSnapshot growthActorAfter,
        RuntimeActorSnapshot composedActorBefore,
        RuntimeActorSnapshot composedActorAfter,
        RuntimeMutationResult growthMutation,
        RuntimeSkillUnlockPlanResult? skillUnlockPlan = null,
        RuntimeActorCombatProfileCompositionResult? combatProfileComposition = null,
        IEnumerable<RuntimeActorGrowthCompositionDiagnostic>? diagnostics = null)
    {
        Status = status;
        GrowthActorBefore = growthActorBefore ??
            throw new ArgumentNullException(nameof(growthActorBefore));
        GrowthActorAfter = growthActorAfter ??
            throw new ArgumentNullException(nameof(growthActorAfter));
        ComposedActorBefore = composedActorBefore ??
            throw new ArgumentNullException(nameof(composedActorBefore));
        ComposedActorAfter = composedActorAfter ??
            throw new ArgumentNullException(nameof(composedActorAfter));
        GrowthMutation = growthMutation ?? throw new ArgumentNullException(nameof(growthMutation));
        SkillUnlockPlan = skillUnlockPlan;
        CombatProfileComposition = combatProfileComposition;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeActorGrowthCompositionStatus Status { get; }
    public bool Applied => Status == RuntimeActorGrowthCompositionStatus.Applied;
    public RuntimeActorSnapshot GrowthActorBefore { get; }
    public RuntimeActorSnapshot GrowthActorAfter { get; }
    public RuntimeActorSnapshot ComposedActorBefore { get; }
    public RuntimeActorSnapshot ComposedActorAfter { get; }
    public RuntimeMutationResult GrowthMutation { get; }
    public RuntimeSkillUnlockPlanResult? SkillUnlockPlan { get; }
    public RuntimeActorCombatProfileCompositionResult? CombatProfileComposition { get; }
    public IReadOnlyList<RuntimeActorGrowthCompositionDiagnostic> Diagnostics { get; }
}

public interface IRuntimeActorGrowthCompositionService
{
    RuntimeActorGrowthCompositionResult Apply(RuntimeActorGrowthCompositionRequest request);
}

public sealed class RuntimeActorGrowthCompositionService :
    IRuntimeActorGrowthCompositionService
{
    private readonly IRuntimeActorCombatProfileCompositionService _combatProfileComposition;
    private readonly IRuntimeSkillUnlockPlanner _skillUnlockPlanner;
    private readonly ISkillDefinitionRepository _skills;
    private readonly RuntimeProgressionTransactionService _progression = new();

    public RuntimeActorGrowthCompositionService(
        IRuntimeActorCombatProfileCompositionService combatProfileComposition,
        ISkillDefinitionRepository skills,
        IRuntimeSkillUnlockPlanner? skillUnlockPlanner = null)
    {
        _combatProfileComposition = combatProfileComposition ??
            throw new ArgumentNullException(nameof(combatProfileComposition));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _skillUnlockPlanner = skillUnlockPlanner ?? new RuntimeSkillUnlockPlanner(skills);
    }

    public RuntimeActorGrowthCompositionResult Apply(
        RuntimeActorGrowthCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuntimeActorState growthActor = request.GrowthActor;
        RuntimeActorState composedActor = request.CombatProfileComposition.Actor;
        RuntimeActorSnapshot growthBefore = growthActor.ToSnapshot();
        RuntimeActorSnapshot composedBefore = composedActor.ToSnapshot();
        RuntimeActorState stagedGrowth = growthActor.CreateExecutionClone();
        RuntimeMutationResult growthMutation = StageGrowth(
            stagedGrowth,
            request.Growth,
            growthBefore);
        if (!growthMutation.Applied)
        {
            return RejectedGrowth(
                growthBefore,
                composedBefore,
                growthMutation);
        }

        RuntimeSkillUnlockPlanResult unlockPlan = _skillUnlockPlanner.Plan(
            new RuntimeSkillUnlockPlanRequest(
                stagedGrowth.ToSnapshot(),
                request.GrowthEntity,
                growthBefore.Progression.Level,
                request.MoveListCapacityPolicy));
        if (!unlockPlan.Planned)
        {
            return new RuntimeActorGrowthCompositionResult(
                RuntimeActorGrowthCompositionStatus.SkillUnlockPlanningRejected,
                growthBefore,
                growthBefore,
                composedBefore,
                composedBefore,
                growthMutation,
                unlockPlan,
                diagnostics: unlockPlan.Diagnostics.Select(diagnostic =>
                    new RuntimeActorGrowthCompositionDiagnostic(
                        RuntimeActorGrowthCompositionDiagnosticCode.SkillUnlockPlanningRejected,
                        diagnostic.Message,
                        diagnostic.SkillId is ContentId skillId
                            ? $"$.skills['{skillId}']"
                            : "$.skills")));
        }

        try
        {
            SkillDefinition[] definitions = unlockPlan.After.EquippedSkillIds
                .Select(_skills.GetRequiredSkill)
                .ToArray();
            stagedGrowth.ApplySkillState(unlockPlan.After, definitions);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return new RuntimeActorGrowthCompositionResult(
                RuntimeActorGrowthCompositionStatus.SkillUnlockPlanningRejected,
                growthBefore,
                growthBefore,
                composedBefore,
                composedBefore,
                growthMutation,
                unlockPlan,
                diagnostics:
                [
                    new RuntimeActorGrowthCompositionDiagnostic(
                        RuntimeActorGrowthCompositionDiagnosticCode.SkillStateRejected,
                        $"Growth skill state could not be staged: {exception.Message}",
                        "$.skills")
                ]);
        }

        RuntimeActorState stagedComposed = composedActor.InstanceId == growthActor.InstanceId
            ? stagedGrowth
            : composedActor.CreateExecutionClone();
        RuntimeActorCombatProfileCompositionRequest stagedCompositionRequest =
            RuntimeSkillProgressionTransactionSupport.StageCompositionRequest(
                request.CombatProfileComposition,
                stagedComposed,
                stagedGrowth);
        RuntimeActorCombatProfileCompositionResult composition =
            _combatProfileComposition.Compose(stagedCompositionRequest);
        if (!composition.Applied)
        {
            return RejectedComposition(
                growthBefore,
                composedBefore,
                growthMutation,
                unlockPlan,
                composition);
        }

        try
        {
            growthActor.ApplyExecutionStateFrom(stagedGrowth);
            if (composedActor.InstanceId != growthActor.InstanceId)
            {
                composedActor.ApplyExecutionStateFrom(stagedComposed);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return new RuntimeActorGrowthCompositionResult(
                RuntimeActorGrowthCompositionStatus.CommitRejected,
                growthBefore,
                growthBefore,
                composedBefore,
                composedBefore,
                growthMutation,
                unlockPlan,
                composition,
                [
                    new RuntimeActorGrowthCompositionDiagnostic(
                        RuntimeActorGrowthCompositionDiagnosticCode.CommitFailed,
                        $"Composed growth could not be committed: {exception.Message}",
                        "$")
                ]);
        }

        return new RuntimeActorGrowthCompositionResult(
            RuntimeActorGrowthCompositionStatus.Applied,
            growthBefore,
            growthActor.ToSnapshot(),
            composedBefore,
            composedActor.ToSnapshot(),
            growthMutation,
            unlockPlan,
            composition);
    }

    private RuntimeMutationResult StageGrowth(
        RuntimeActorState stagedGrowth,
        LevelGrowthResult growth,
        RuntimeActorSnapshot before)
    {
        try
        {
            return _progression.ApplyLevelGrowth(stagedGrowth, growth);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return new RuntimeMutationResult(
                RuntimeMutationStatus.Rejected,
                before,
                before,
                [
                    new RuntimeMutationDiagnostic(
                        RuntimeMutationErrorCode.ProgressionMutationRejected,
                        $"Growth could not be staged: {exception.Message}",
                        "$.progression")
                ]);
        }
    }

    private static RuntimeActorGrowthCompositionResult RejectedGrowth(
        RuntimeActorSnapshot growthBefore,
        RuntimeActorSnapshot composedBefore,
        RuntimeMutationResult growthMutation) =>
        new(
            RuntimeActorGrowthCompositionStatus.GrowthRejected,
            growthBefore,
            growthBefore,
            composedBefore,
            composedBefore,
            growthMutation,
            diagnostics: growthMutation.Diagnostics.Select(diagnostic =>
                new RuntimeActorGrowthCompositionDiagnostic(
                    RuntimeActorGrowthCompositionDiagnosticCode.GrowthRejected,
                    diagnostic.Message,
                    diagnostic.Path ?? "$.progression")));

    private static RuntimeActorGrowthCompositionResult RejectedComposition(
        RuntimeActorSnapshot growthBefore,
        RuntimeActorSnapshot composedBefore,
        RuntimeMutationResult growthMutation,
        RuntimeSkillUnlockPlanResult unlockPlan,
        RuntimeActorCombatProfileCompositionResult composition) =>
        new(
            RuntimeActorGrowthCompositionStatus.CombatProfileCompositionRejected,
            growthBefore,
            growthBefore,
            composedBefore,
            composedBefore,
            growthMutation,
            unlockPlan,
            composition,
            composition.Diagnostics.Select(diagnostic =>
                new RuntimeActorGrowthCompositionDiagnostic(
                    RuntimeActorGrowthCompositionDiagnosticCode.CombatProfileCompositionRejected,
                    diagnostic.Message,
                    diagnostic.StatId is ContentId statId
                        ? $"$.stats.effectiveStats['{statId}']"
                        : diagnostic.SkillId is ContentId skillId
                            ? $"$.skills['{skillId}']"
                            : "$.combatProfile")));
}
