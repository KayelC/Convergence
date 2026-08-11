using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

public readonly record struct RuntimeSkillChoiceToken
{
    public RuntimeSkillChoiceToken(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Skill-choice tokens must be positive.");
        }

        Value = value;
    }

    public long Value { get; }
    public bool IsValid => Value > 0;
    public override string ToString() => Value.ToString(
        System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record RuntimePendingSkillChoiceSnapshot
{
    public RuntimePendingSkillChoiceSnapshot(
        RuntimeSkillChoiceToken token,
        int unlockLevel,
        ContentId skillId)
    {
        if (!token.IsValid)
        {
            throw new ArgumentException(
                "Pending skill-choice token cannot be empty.",
                nameof(token));
        }
        if (unlockLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unlockLevel),
                "Pending skill unlock level must be positive.");
        }
        if (!skillId.IsValid)
        {
            throw new ArgumentException(
                "Pending skill ID cannot be empty.",
                nameof(skillId));
        }

        Token = token;
        UnlockLevel = unlockLevel;
        SkillId = skillId;
    }

    public RuntimeSkillChoiceToken Token { get; }
    public int UnlockLevel { get; }
    public ContentId SkillId { get; }
}

public sealed record RuntimeMoveListCapacityRequest
{
    public RuntimeMoveListCapacityRequest(
        RuntimeActorIdentitySnapshot actor,
        IEnumerable<SkillDefinition> equippedSkills,
        SkillDefinition candidate)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        EquippedSkills = Array.AsReadOnly(
            (equippedSkills ?? throw new ArgumentNullException(nameof(equippedSkills)))
            .Select(skill => skill ?? throw new ArgumentException(
                "Equipped skill definitions cannot contain null entries.",
                nameof(equippedSkills)))
            .ToArray());
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    public RuntimeActorIdentitySnapshot Actor { get; }
    public IReadOnlyList<SkillDefinition> EquippedSkills { get; }
    public SkillDefinition Candidate { get; }
}

public sealed record RuntimeMoveListCapacityAssessment
{
    public RuntimeMoveListCapacityAssessment(
        ContentId capacityGroupId,
        int capacity,
        int occupiedSlots)
    {
        if (!capacityGroupId.IsValid)
        {
            throw new ArgumentException(
                "Move-list capacity group ID cannot be empty.",
                nameof(capacityGroupId));
        }
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Move-list capacity cannot be negative.");
        }
        if (occupiedSlots < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occupiedSlots),
                "Occupied move-list slots cannot be negative.");
        }

        CapacityGroupId = capacityGroupId;
        Capacity = capacity;
        OccupiedSlots = occupiedSlots;
    }

    public ContentId CapacityGroupId { get; }
    public int Capacity { get; }
    public int OccupiedSlots { get; }
    public bool HasAvailableSlot => OccupiedSlots < Capacity;
}

public interface IRuntimeMoveListCapacityPolicy
{
    RuntimeMoveListCapacityAssessment Assess(RuntimeMoveListCapacityRequest request);
}

public sealed class SharedRuntimeMoveListCapacityPolicy : IRuntimeMoveListCapacityPolicy
{
    public const int StandardCapacity = 8;
    private static readonly ContentId SharedGroup = ContentId.Parse("shared");
    private readonly int _capacity;

    public SharedRuntimeMoveListCapacityPolicy(int capacity = StandardCapacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Move-list capacity cannot be negative.");
        }

        _capacity = capacity;
    }

    public RuntimeMoveListCapacityAssessment Assess(RuntimeMoveListCapacityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new RuntimeMoveListCapacityAssessment(
            SharedGroup,
            _capacity,
            request.EquippedSkills.Count);
    }
}

public sealed class SeparatedRuntimeMoveListCapacityPolicy : IRuntimeMoveListCapacityPolicy
{
    private static readonly ContentId ActiveGroup = ContentId.Parse("active");
    private static readonly ContentId PassiveGroup = ContentId.Parse("passive");
    private readonly int _activeCapacity;
    private readonly int _passiveCapacity;

    public SeparatedRuntimeMoveListCapacityPolicy(
        int activeCapacity,
        int passiveCapacity)
    {
        if (activeCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeCapacity),
                "Active move-list capacity cannot be negative.");
        }
        if (passiveCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passiveCapacity),
                "Passive move-list capacity cannot be negative.");
        }

        _activeCapacity = activeCapacity;
        _passiveCapacity = passiveCapacity;
    }

    public RuntimeMoveListCapacityAssessment Assess(RuntimeMoveListCapacityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SkillActivation activation = request.Candidate.Activation;
        int occupied = request.EquippedSkills.Count(skill =>
            skill.Activation == activation);
        return activation switch
        {
            SkillActivation.Active => new RuntimeMoveListCapacityAssessment(
                ActiveGroup,
                _activeCapacity,
                occupied),
            SkillActivation.Passive => new RuntimeMoveListCapacityAssessment(
                PassiveGroup,
                _passiveCapacity,
                occupied),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                activation,
                "Skill activation is not supported by the separated capacity policy.")
        };
    }
}

internal sealed record RuntimeMoveListCapacityViolation(
    string Message,
    ContentId? SkillId = null);

internal static class RuntimeMoveListCapacityValidation
{
    public static RuntimeMoveListCapacityViolation? ValidateCurrent(
        RuntimeActorIdentitySnapshot actor,
        IReadOnlyList<SkillDefinition> equippedSkills,
        IRuntimeMoveListCapacityPolicy capacityPolicy)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(equippedSkills);
        ArgumentNullException.ThrowIfNull(capacityPolicy);

        foreach (SkillDefinition skill in equippedSkills)
        {
            RuntimeMoveListCapacityAssessment? assessment =
                TryAssess(actor, equippedSkills, skill, capacityPolicy, out string? failure);
            if (assessment is null)
            {
                return new RuntimeMoveListCapacityViolation(failure!, skill.Id);
            }
            if (assessment.OccupiedSlots > assessment.Capacity)
            {
                return new RuntimeMoveListCapacityViolation(
                    $"Equipped move-list group '{assessment.CapacityGroupId}' has " +
                    $"{assessment.OccupiedSlots} skills, exceeding its capacity of " +
                    $"{assessment.Capacity}.",
                    skill.Id);
            }
        }

        return null;
    }

    public static RuntimeMoveListCapacityViolation? ValidateAddition(
        RuntimeActorIdentitySnapshot actor,
        IReadOnlyList<SkillDefinition> equippedSkills,
        SkillDefinition candidate,
        IRuntimeMoveListCapacityPolicy capacityPolicy)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(equippedSkills);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(capacityPolicy);

        RuntimeMoveListCapacityAssessment? assessment =
            TryAssess(actor, equippedSkills, candidate, capacityPolicy, out string? failure);
        if (assessment is null)
        {
            return new RuntimeMoveListCapacityViolation(failure!, candidate.Id);
        }

        return assessment.HasAvailableSlot
            ? null
            : new RuntimeMoveListCapacityViolation(
                $"Skill '{candidate.Id}' cannot be equipped because move-list group " +
                $"'{assessment.CapacityGroupId}' is at its capacity of {assessment.Capacity}.",
                candidate.Id);
    }

    private static RuntimeMoveListCapacityAssessment? TryAssess(
        RuntimeActorIdentitySnapshot actor,
        IReadOnlyList<SkillDefinition> equippedSkills,
        SkillDefinition candidate,
        IRuntimeMoveListCapacityPolicy capacityPolicy,
        out string? failure)
    {
        try
        {
            RuntimeMoveListCapacityAssessment? assessment = capacityPolicy.Assess(
                new RuntimeMoveListCapacityRequest(actor, equippedSkills, candidate));
            failure = assessment is null
                ? $"Move-list capacity policy returned no assessment for skill '{candidate.Id}'."
                : null;
            return assessment;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            failure =
                $"Move-list capacity policy rejected skill '{candidate.Id}': {exception.Message}";
            return null;
        }
    }
}

public enum RuntimeSkillUnlockDisposition
{
    AutomaticallyEquipped,
    PendingChoice
}

public sealed record RuntimeSkillUnlockPlanEntry(
    int UnlockLevel,
    ContentId SkillId,
    RuntimeSkillUnlockDisposition Disposition,
    RuntimeSkillChoiceToken? PendingChoiceToken = null);

public enum RuntimeSkillUnlockPlanStatus
{
    Planned,
    Rejected
}

public enum RuntimeSkillUnlockPlanDiagnosticCode
{
    EntityIdentityMismatch,
    InvalidLevelRange,
    InvalidSkillState,
    SkillDefinitionMissing,
    CapacityPolicyFailed,
    TokenRangeExceeded
}

public sealed record RuntimeSkillUnlockPlanDiagnostic(
    RuntimeSkillUnlockPlanDiagnosticCode Code,
    string Message,
    ContentId? SkillId = null);

public sealed record RuntimeSkillUnlockPlanRequest
{
    public RuntimeSkillUnlockPlanRequest(
        RuntimeActorSnapshot actor,
        EntityDefinition entity,
        int previousLevel,
        IRuntimeMoveListCapacityPolicy capacityPolicy)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        PreviousLevel = previousLevel;
        CapacityPolicy = capacityPolicy ?? throw new ArgumentNullException(nameof(capacityPolicy));
    }

    public RuntimeActorSnapshot Actor { get; }
    public EntityDefinition Entity { get; }
    public int PreviousLevel { get; }
    public IRuntimeMoveListCapacityPolicy CapacityPolicy { get; }
}

public sealed record RuntimeSkillUnlockPlanResult
{
    public RuntimeSkillUnlockPlanResult(
        RuntimeSkillUnlockPlanStatus status,
        RuntimeSkillStateSnapshot before,
        RuntimeSkillStateSnapshot after,
        IEnumerable<RuntimeSkillUnlockPlanEntry>? entries = null,
        IEnumerable<RuntimeSkillUnlockPlanDiagnostic>? diagnostics = null)
    {
        Status = status;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Entries = RuntimeSnapshotCollections.List(entries);
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeSkillUnlockPlanStatus Status { get; }
    public bool Planned => Status == RuntimeSkillUnlockPlanStatus.Planned;
    public RuntimeSkillStateSnapshot Before { get; }
    public RuntimeSkillStateSnapshot After { get; }
    public IReadOnlyList<RuntimeSkillUnlockPlanEntry> Entries { get; }
    public IReadOnlyList<RuntimeSkillUnlockPlanDiagnostic> Diagnostics { get; }
}

public interface IRuntimeSkillUnlockPlanner
{
    RuntimeSkillUnlockPlanResult Plan(RuntimeSkillUnlockPlanRequest request);
}

public sealed class RuntimeSkillUnlockPlanner : IRuntimeSkillUnlockPlanner
{
    private readonly ISkillDefinitionRepository _skills;

    public RuntimeSkillUnlockPlanner(ISkillDefinitionRepository skills)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    }

    public RuntimeSkillUnlockPlanResult Plan(RuntimeSkillUnlockPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeSkillStateSnapshot before = request.Actor.Skills;
        if (request.Actor.Identity.EntityDefinitionId != request.Entity.Id)
        {
            return Rejected(
                before,
                RuntimeSkillUnlockPlanDiagnosticCode.EntityIdentityMismatch,
                $"Actor entity '{request.Actor.Identity.EntityDefinitionId}' does not match " +
                $"unlock source '{request.Entity.Id}'.");
        }
        if (request.PreviousLevel < 0 ||
            request.PreviousLevel > request.Actor.Progression.Level)
        {
            return Rejected(
                before,
                RuntimeSkillUnlockPlanDiagnosticCode.InvalidLevelRange,
                $"Previous level {request.PreviousLevel} must be nonnegative and no greater than " +
                $"current level {request.Actor.Progression.Level}.");
        }

        RuntimeSkillUnlockPlanDiagnostic? stateDiagnostic = ValidateState(before);
        if (stateDiagnostic is not null)
        {
            return new RuntimeSkillUnlockPlanResult(
                RuntimeSkillUnlockPlanStatus.Rejected,
                before,
                before,
                diagnostics: [stateDiagnostic]);
        }

        var learned = before.LearnedSkillIds.ToList();
        var equipped = before.EquippedSkillIds.ToList();
        var pending = before.PendingChoices.ToList();
        var knownOrPending = new HashSet<ContentId>(learned);
        knownOrPending.UnionWith(pending.Select(choice => choice.SkillId));
        var discovered = new HashSet<ContentId>();
        var entries = new List<RuntimeSkillUnlockPlanEntry>();
        long nextTokenValue;
        try
        {
            nextTokenValue = Math.Max(
                before.Revision,
                pending.Count == 0 ? 0 : pending.Max(choice => choice.Token.Value));
        }
        catch (InvalidOperationException)
        {
            nextTokenValue = before.Revision;
        }

        foreach (SkillUnlockDefinition unlock in request.Entity.SkillUnlocks)
        {
            if (unlock.Level <= request.PreviousLevel ||
                unlock.Level > request.Actor.Progression.Level ||
                !discovered.Add(unlock.SkillId) ||
                knownOrPending.Contains(unlock.SkillId))
            {
                continue;
            }

            if (!_skills.TryGetSkill(unlock.SkillId, out SkillDefinition? skill) ||
                skill is null)
            {
                return Rejected(
                    before,
                    RuntimeSkillUnlockPlanDiagnosticCode.SkillDefinitionMissing,
                    $"Unlock skill '{unlock.SkillId}' does not exist.",
                    unlock.SkillId);
            }

            SkillDefinition[] equippedDefinitions = new SkillDefinition[equipped.Count];
            for (int index = 0; index < equipped.Count; index++)
            {
                ContentId equippedId = equipped[index];
                if (!_skills.TryGetSkill(equippedId, out SkillDefinition? equippedSkill) ||
                    equippedSkill is null)
                {
                    return Rejected(
                        before,
                        RuntimeSkillUnlockPlanDiagnosticCode.SkillDefinitionMissing,
                        $"Equipped skill '{equippedId}' does not exist.",
                        equippedId);
                }

                equippedDefinitions[index] = equippedSkill;
            }

            RuntimeMoveListCapacityAssessment? capacity;
            try
            {
                capacity = request.CapacityPolicy.Assess(new RuntimeMoveListCapacityRequest(
                    request.Actor.Identity,
                    equippedDefinitions,
                    skill));
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                return Rejected(
                    before,
                    RuntimeSkillUnlockPlanDiagnosticCode.CapacityPolicyFailed,
                    $"Move-list capacity policy rejected skill '{unlock.SkillId}': " +
                    exception.Message,
                    unlock.SkillId);
            }
            if (capacity is null)
            {
                return Rejected(
                    before,
                    RuntimeSkillUnlockPlanDiagnosticCode.CapacityPolicyFailed,
                    $"Move-list capacity policy returned no assessment for skill " +
                    $"'{unlock.SkillId}'.",
                    unlock.SkillId);
            }

            if (capacity.HasAvailableSlot)
            {
                learned.Add(unlock.SkillId);
                equipped.Add(unlock.SkillId);
                knownOrPending.Add(unlock.SkillId);
                entries.Add(new RuntimeSkillUnlockPlanEntry(
                    unlock.Level,
                    unlock.SkillId,
                    RuntimeSkillUnlockDisposition.AutomaticallyEquipped));
                continue;
            }

            try
            {
                nextTokenValue = checked(nextTokenValue + 1);
            }
            catch (OverflowException)
            {
                return Rejected(
                    before,
                    RuntimeSkillUnlockPlanDiagnosticCode.TokenRangeExceeded,
                    "Pending skill-choice token range is exhausted.",
                    unlock.SkillId);
            }

            var token = new RuntimeSkillChoiceToken(nextTokenValue);
            pending.Add(new RuntimePendingSkillChoiceSnapshot(
                token,
                unlock.Level,
                unlock.SkillId));
            knownOrPending.Add(unlock.SkillId);
            entries.Add(new RuntimeSkillUnlockPlanEntry(
                unlock.Level,
                unlock.SkillId,
                RuntimeSkillUnlockDisposition.PendingChoice,
                token));
        }

        if (entries.Count == 0)
        {
            return new RuntimeSkillUnlockPlanResult(
                RuntimeSkillUnlockPlanStatus.Planned,
                before,
                before);
        }

        long revision;
        try
        {
            revision = checked(before.Revision + 1);
        }
        catch (OverflowException)
        {
            return Rejected(
                before,
                RuntimeSkillUnlockPlanDiagnosticCode.TokenRangeExceeded,
                "Skill-state revision range is exhausted.");
        }

        return new RuntimeSkillUnlockPlanResult(
            RuntimeSkillUnlockPlanStatus.Planned,
            before,
            new RuntimeSkillStateSnapshot(learned, equipped, pending, revision),
            entries);
    }

    private static RuntimeSkillUnlockPlanDiagnostic? ValidateState(
        RuntimeSkillStateSnapshot state)
    {
        if (state.LearnedSkillIds.Any(id => !id.IsValid) ||
            state.EquippedSkillIds.Any(id => !id.IsValid) ||
            state.LearnedSkillIds.Distinct().Count() != state.LearnedSkillIds.Count ||
            state.EquippedSkillIds.Distinct().Count() != state.EquippedSkillIds.Count ||
            state.EquippedSkillIds.Except(state.LearnedSkillIds).Any() ||
            state.PendingChoices.Any(choice => !choice.Token.IsValid || !choice.SkillId.IsValid) ||
            state.PendingChoices.Select(choice => choice.Token).Distinct().Count() !=
            state.PendingChoices.Count ||
            state.PendingChoices.Select(choice => choice.SkillId).Distinct().Count() !=
            state.PendingChoices.Count ||
            state.PendingChoices.Any(choice => state.LearnedSkillIds.Contains(choice.SkillId)))
        {
            return new RuntimeSkillUnlockPlanDiagnostic(
                RuntimeSkillUnlockPlanDiagnosticCode.InvalidSkillState,
                "Actor skill state contains invalid IDs, duplicates, an unlearned equipped skill, " +
                "or an invalid pending choice.");
        }

        return null;
    }

    private static RuntimeSkillUnlockPlanResult Rejected(
        RuntimeSkillStateSnapshot before,
        RuntimeSkillUnlockPlanDiagnosticCode code,
        string message,
        ContentId? skillId = null) =>
        new(
            RuntimeSkillUnlockPlanStatus.Rejected,
            before,
            before,
            diagnostics: [new RuntimeSkillUnlockPlanDiagnostic(code, message, skillId)]);
}

public enum RuntimeSkillChoiceDecisionKind
{
    Replace,
    ForgetNew
}

public abstract record RuntimeSkillChoiceCommand
{
    protected RuntimeSkillChoiceCommand(
        RuntimeSkillChoiceToken token,
        int expectedSourceLevel,
        long expectedSkillRevision)
    {
        if (!token.IsValid)
        {
            throw new ArgumentException("Skill-choice token cannot be empty.", nameof(token));
        }
        if (expectedSourceLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedSourceLevel),
                "Expected source level must be positive.");
        }
        if (expectedSkillRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedSkillRevision),
                "Expected skill revision cannot be negative.");
        }

        Token = token;
        ExpectedSourceLevel = expectedSourceLevel;
        ExpectedSkillRevision = expectedSkillRevision;
    }

    public RuntimeSkillChoiceToken Token { get; }
    public int ExpectedSourceLevel { get; }
    public long ExpectedSkillRevision { get; }
    public abstract RuntimeSkillChoiceDecisionKind Kind { get; }
}

public sealed record ReplacePendingSkillCommand : RuntimeSkillChoiceCommand
{
    public ReplacePendingSkillCommand(
        RuntimeSkillChoiceToken token,
        int expectedSourceLevel,
        long expectedSkillRevision,
        ContentId replacedSkillId)
        : base(token, expectedSourceLevel, expectedSkillRevision)
    {
        if (!replacedSkillId.IsValid)
        {
            throw new ArgumentException(
                "Replacement skill ID cannot be empty.",
                nameof(replacedSkillId));
        }

        ReplacedSkillId = replacedSkillId;
    }

    public override RuntimeSkillChoiceDecisionKind Kind =>
        RuntimeSkillChoiceDecisionKind.Replace;
    public ContentId ReplacedSkillId { get; }
}

public sealed record ForgetPendingSkillCommand : RuntimeSkillChoiceCommand
{
    public ForgetPendingSkillCommand(
        RuntimeSkillChoiceToken token,
        int expectedSourceLevel,
        long expectedSkillRevision)
        : base(token, expectedSourceLevel, expectedSkillRevision)
    {
    }

    public override RuntimeSkillChoiceDecisionKind Kind =>
        RuntimeSkillChoiceDecisionKind.ForgetNew;
}

public sealed record RuntimeSkillReplacementRequest(
    RuntimeSkillStateSnapshot Current,
    ContentId ReplacedSkillId,
    ContentId NewSkillId);

public sealed record RuntimeSkillReplacementResult
{
    public RuntimeSkillReplacementResult(
        IEnumerable<ContentId> learnedSkillIds,
        IEnumerable<ContentId> equippedSkillIds)
    {
        LearnedSkillIds = RuntimeSnapshotCollections.List(learnedSkillIds);
        EquippedSkillIds = RuntimeSnapshotCollections.List(equippedSkillIds);
    }

    public IReadOnlyList<ContentId> LearnedSkillIds { get; }
    public IReadOnlyList<ContentId> EquippedSkillIds { get; }
}

public interface IRuntimeSkillRetentionPolicy
{
    RuntimeSkillReplacementResult Replace(RuntimeSkillReplacementRequest request);
}

public sealed class StandardRuntimeSkillRetentionPolicy : IRuntimeSkillRetentionPolicy
{
    public RuntimeSkillReplacementResult Replace(RuntimeSkillReplacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<ContentId> learned = request.Current.LearnedSkillIds
            .Where(skillId => skillId != request.ReplacedSkillId)
            .ToList();
        learned.Add(request.NewSkillId);
        List<ContentId> equipped = request.Current.EquippedSkillIds
            .Select(skillId => skillId == request.ReplacedSkillId
                ? request.NewSkillId
                : skillId)
            .ToList();
        return new RuntimeSkillReplacementResult(
            Array.AsReadOnly(learned.ToArray()),
            Array.AsReadOnly(equipped.ToArray()));
    }
}

public sealed class RetainLearnedRuntimeSkillPolicy : IRuntimeSkillRetentionPolicy
{
    public RuntimeSkillReplacementResult Replace(RuntimeSkillReplacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<ContentId> learned = request.Current.LearnedSkillIds.ToList();
        learned.Add(request.NewSkillId);
        List<ContentId> equipped = request.Current.EquippedSkillIds
            .Select(skillId => skillId == request.ReplacedSkillId
                ? request.NewSkillId
                : skillId)
            .ToList();
        return new RuntimeSkillReplacementResult(
            Array.AsReadOnly(learned.ToArray()),
            Array.AsReadOnly(equipped.ToArray()));
    }
}

public enum RuntimeSkillChoiceTransactionStatus
{
    Applied,
    Rejected,
    CombatProfileCompositionRejected,
    CommitRejected
}

public enum RuntimeSkillChoiceDiagnosticCode
{
    PendingChoiceMissing,
    StaleSourceLevel,
    StaleSkillRevision,
    ReplacementSkillMissing,
    ReplacementSkillInvalid,
    SkillDefinitionMissing,
    RetentionPolicyFailed,
    SkillStateInvalid,
    CombatProfileCompositionRejected,
    CommitFailed
}

public sealed record RuntimeSkillChoiceDiagnostic(
    RuntimeSkillChoiceDiagnosticCode Code,
    string Message,
    ContentId? SkillId = null);

public sealed record RuntimeSkillChoiceTransactionRequest
{
    public RuntimeSkillChoiceTransactionRequest(
        RuntimeActorState sourceActor,
        RuntimeSkillChoiceCommand command,
        RuntimeActorCombatProfileCompositionRequest? dependentCombatProfileComposition = null)
    {
        SourceActor = sourceActor ?? throw new ArgumentNullException(nameof(sourceActor));
        Command = command ?? throw new ArgumentNullException(nameof(command));
        DependentCombatProfileComposition = dependentCombatProfileComposition;
    }

    public RuntimeActorState SourceActor { get; }
    public RuntimeSkillChoiceCommand Command { get; }
    public RuntimeActorCombatProfileCompositionRequest? DependentCombatProfileComposition { get; }
}

public sealed record RuntimeSkillChoiceTransactionResult
{
    public RuntimeSkillChoiceTransactionResult(
        RuntimeSkillChoiceTransactionStatus status,
        RuntimeActorSnapshot sourceBefore,
        RuntimeActorSnapshot sourceAfter,
        RuntimeActorSnapshot? dependentActorBefore = null,
        RuntimeActorSnapshot? dependentActorAfter = null,
        RuntimeActorCombatProfileCompositionResult? combatProfileComposition = null,
        IEnumerable<RuntimeSkillChoiceDiagnostic>? diagnostics = null)
    {
        Status = status;
        SourceBefore = sourceBefore ?? throw new ArgumentNullException(nameof(sourceBefore));
        SourceAfter = sourceAfter ?? throw new ArgumentNullException(nameof(sourceAfter));
        DependentActorBefore = dependentActorBefore;
        DependentActorAfter = dependentActorAfter;
        CombatProfileComposition = combatProfileComposition;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public RuntimeSkillChoiceTransactionStatus Status { get; }
    public bool Applied => Status == RuntimeSkillChoiceTransactionStatus.Applied;
    public RuntimeActorSnapshot SourceBefore { get; }
    public RuntimeActorSnapshot SourceAfter { get; }
    public RuntimeActorSnapshot? DependentActorBefore { get; }
    public RuntimeActorSnapshot? DependentActorAfter { get; }
    public RuntimeActorCombatProfileCompositionResult? CombatProfileComposition { get; }
    public IReadOnlyList<RuntimeSkillChoiceDiagnostic> Diagnostics { get; }
}

public interface IRuntimeSkillChoiceTransactionService
{
    RuntimeSkillChoiceTransactionResult Apply(RuntimeSkillChoiceTransactionRequest request);
}

public sealed class RuntimeSkillChoiceTransactionService :
    IRuntimeSkillChoiceTransactionService
{
    private readonly ISkillDefinitionRepository _skills;
    private readonly IRuntimeActorCombatProfileCompositionService _composition;
    private readonly IRuntimeSkillRetentionPolicy _retention;

    public RuntimeSkillChoiceTransactionService(
        ISkillDefinitionRepository skills,
        IRuntimeActorCombatProfileCompositionService composition,
        IRuntimeSkillRetentionPolicy? retention = null)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        _retention = retention ?? new StandardRuntimeSkillRetentionPolicy();
    }

    public RuntimeSkillChoiceTransactionResult Apply(
        RuntimeSkillChoiceTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeActorState sourceActor = request.SourceActor;
        RuntimeActorSnapshot sourceBefore = sourceActor.ToSnapshot();
        RuntimeSkillStateSnapshot current = sourceBefore.Skills;
        RuntimePendingSkillChoiceSnapshot[] matchingChoices = current.PendingChoices
            .Where(choice => choice.Token == request.Command.Token)
            .Take(2)
            .ToArray();
        if (matchingChoices.Length != 1)
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.PendingChoiceMissing,
                matchingChoices.Length == 0
                    ? $"Pending skill choice '{request.Command.Token}' does not exist."
                    : $"Pending skill choice '{request.Command.Token}' is duplicated.");
        }
        RuntimePendingSkillChoiceSnapshot pending = matchingChoices[0];
        if (request.Command.ExpectedSourceLevel != sourceBefore.Progression.Level)
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.StaleSourceLevel,
                $"Skill choice expected source level {request.Command.ExpectedSourceLevel}, " +
                $"but the actor is level {sourceBefore.Progression.Level}.",
                pending.SkillId);
        }
        if (request.Command.ExpectedSkillRevision != current.Revision)
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.StaleSkillRevision,
                $"Skill choice expected revision {request.Command.ExpectedSkillRevision}, " +
                $"but the actor is at revision {current.Revision}.",
                pending.SkillId);
        }
        if (!_skills.TryGetSkill(pending.SkillId, out SkillDefinition? pendingSkill) ||
            pendingSkill is null)
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.SkillDefinitionMissing,
                $"Pending skill '{pending.SkillId}' does not exist.",
                pending.SkillId);
        }

        IReadOnlyList<ContentId> learned;
        IReadOnlyList<ContentId> equipped;
        if (request.Command is ReplacePendingSkillCommand replace)
        {
            if (!current.EquippedSkillIds.Contains(replace.ReplacedSkillId))
            {
                return Rejected(
                    sourceBefore,
                    request,
                    RuntimeSkillChoiceDiagnosticCode.ReplacementSkillMissing,
                    $"Replacement skill '{replace.ReplacedSkillId}' is not equipped.",
                    replace.ReplacedSkillId);
            }
            if (replace.ReplacedSkillId == pending.SkillId ||
                current.LearnedSkillIds.Contains(pending.SkillId))
            {
                return Rejected(
                    sourceBefore,
                    request,
                    RuntimeSkillChoiceDiagnosticCode.ReplacementSkillInvalid,
                    "Replacement must remove a different equipped skill and add a new skill.",
                    replace.ReplacedSkillId);
            }

            RuntimeSkillReplacementResult replacement;
            try
            {
                replacement = _retention.Replace(new RuntimeSkillReplacementRequest(
                    current,
                    replace.ReplacedSkillId,
                    pending.SkillId));
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                return Rejected(
                    sourceBefore,
                    request,
                    RuntimeSkillChoiceDiagnosticCode.RetentionPolicyFailed,
                    $"Skill retention policy rejected the replacement: {exception.Message}",
                    pending.SkillId);
            }
            if (replacement is null ||
                replacement.LearnedSkillIds is null ||
                replacement.EquippedSkillIds is null)
            {
                return Rejected(
                    sourceBefore,
                    request,
                    RuntimeSkillChoiceDiagnosticCode.RetentionPolicyFailed,
                    "Skill retention policy returned an incomplete replacement state.",
                    pending.SkillId);
            }

            learned = replacement.LearnedSkillIds;
            equipped = replacement.EquippedSkillIds;
        }
        else if (request.Command is ForgetPendingSkillCommand)
        {
            learned = current.LearnedSkillIds;
            equipped = current.EquippedSkillIds;
        }
        else
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.SkillStateInvalid,
                $"Skill choice command '{request.Command.GetType().Name}' is unsupported.",
                pending.SkillId);
        }

        long revision;
        try
        {
            revision = checked(current.Revision + 1);
        }
        catch (OverflowException)
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.SkillStateInvalid,
                "Skill-state revision range is exhausted.",
                pending.SkillId);
        }

        RuntimePendingSkillChoiceSnapshot[] remaining = current.PendingChoices
            .Where(choice => choice.Token != pending.Token)
            .ToArray();
        RuntimeSkillStateSnapshot next;
        try
        {
            next = new RuntimeSkillStateSnapshot(
                learned,
                equipped,
                remaining,
                revision);
        }
        catch (ArgumentException exception)
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.SkillStateInvalid,
                exception.Message,
                pending.SkillId);
        }

        RuntimeActorState stagedSource = sourceActor.CreateExecutionClone();
        SkillDefinition[] definitions;
        try
        {
            definitions = ResolveEquippedDefinitions(next);
            stagedSource.ApplySkillState(next, definitions);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return Rejected(
                sourceBefore,
                request,
                RuntimeSkillChoiceDiagnosticCode.SkillStateInvalid,
                $"Skill choice could not be staged: {exception.Message}",
                pending.SkillId);
        }

        RuntimeActorState? dependentLive =
            request.DependentCombatProfileComposition?.Actor;
        RuntimeActorSnapshot? dependentBefore = dependentLive?.ToSnapshot();
        RuntimeActorState? stagedDependent = null;
        RuntimeActorCombatProfileCompositionResult? composition = null;
        if (request.DependentCombatProfileComposition is not null)
        {
            stagedDependent = dependentLive!.InstanceId == sourceActor.InstanceId
                ? stagedSource
                : dependentLive.CreateExecutionClone();
            RuntimeActorCombatProfileCompositionRequest stagedRequest =
                RuntimeSkillProgressionTransactionSupport.StageCompositionRequest(
                    request.DependentCombatProfileComposition,
                    stagedDependent,
                    stagedSource);
            composition = _composition.Compose(stagedRequest);
            if (!composition.Applied)
            {
                return new RuntimeSkillChoiceTransactionResult(
                    RuntimeSkillChoiceTransactionStatus.CombatProfileCompositionRejected,
                    sourceBefore,
                    sourceBefore,
                    dependentBefore,
                    dependentBefore,
                    composition,
                    composition.Diagnostics.Select(diagnostic =>
                        new RuntimeSkillChoiceDiagnostic(
                            RuntimeSkillChoiceDiagnosticCode.CombatProfileCompositionRejected,
                            diagnostic.Message,
                            diagnostic.SkillId)));
            }
        }

        try
        {
            sourceActor.ApplyExecutionStateFrom(stagedSource);
            if (dependentLive is not null &&
                dependentLive.InstanceId != sourceActor.InstanceId)
            {
                dependentLive.ApplyExecutionStateFrom(stagedDependent!);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return new RuntimeSkillChoiceTransactionResult(
                RuntimeSkillChoiceTransactionStatus.CommitRejected,
                sourceBefore,
                sourceBefore,
                dependentBefore,
                dependentBefore,
                composition,
                [
                    new RuntimeSkillChoiceDiagnostic(
                        RuntimeSkillChoiceDiagnosticCode.CommitFailed,
                        $"Skill choice could not be committed: {exception.Message}",
                        pending.SkillId)
                ]);
        }

        return new RuntimeSkillChoiceTransactionResult(
            RuntimeSkillChoiceTransactionStatus.Applied,
            sourceBefore,
            sourceActor.ToSnapshot(),
            dependentBefore,
            dependentLive?.ToSnapshot(),
            composition);
    }

    private SkillDefinition[] ResolveEquippedDefinitions(
        RuntimeSkillStateSnapshot skills) =>
        skills.EquippedSkillIds.Select(_skills.GetRequiredSkill).ToArray();

    private static RuntimeSkillChoiceTransactionResult Rejected(
        RuntimeActorSnapshot sourceBefore,
        RuntimeSkillChoiceTransactionRequest request,
        RuntimeSkillChoiceDiagnosticCode code,
        string message,
        ContentId? skillId = null)
    {
        RuntimeActorSnapshot? dependent =
            request.DependentCombatProfileComposition?.Actor.ToSnapshot();
        return new RuntimeSkillChoiceTransactionResult(
            RuntimeSkillChoiceTransactionStatus.Rejected,
            sourceBefore,
            sourceBefore,
            dependent,
            dependent,
            diagnostics: [new RuntimeSkillChoiceDiagnostic(code, message, skillId)]);
    }
}

internal static class RuntimeSkillProgressionTransactionSupport
{
    public static RuntimeActorCombatProfileCompositionRequest StageCompositionRequest(
        RuntimeActorCombatProfileCompositionRequest request,
        RuntimeActorState stagedTarget,
        RuntimeActorState stagedSource)
    {
        var runtimeActors = new List<RuntimeActorState>();
        foreach (RuntimeActorState candidate in request.RuntimeActors)
        {
            RuntimeActorState staged = candidate.InstanceId switch
            {
                var id when id == stagedSource.InstanceId => stagedSource,
                var id when id == stagedTarget.InstanceId => stagedTarget,
                _ => candidate
            };
            if (runtimeActors.All(existing => existing.InstanceId != staged.InstanceId))
            {
                runtimeActors.Add(staged);
            }
        }

        if (runtimeActors.All(candidate => candidate.InstanceId != stagedSource.InstanceId))
        {
            runtimeActors.Add(stagedSource);
        }

        return new RuntimeActorCombatProfileCompositionRequest(
            stagedTarget,
            request.SourceKind,
            request.MissingHostedEntityBehavior,
            request.PartyRoster,
            runtimeActors,
            request.EquipmentStatModifiers,
            request.EquipmentGrantedSkillIds);
    }
}
