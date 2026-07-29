using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Internal;
using Convergence.Runtime;

namespace Convergence.Knowledge;

public enum BattleKnowledgeExecutionDiagnosticCode
{
    ObservationRejected,
    AnalysisRejected,
    ObservationEffectIndexMismatch,
    ObservationTargetMismatch,
    AnalysisTargetMismatch,
    ObservationSourceActionMismatch,
    ObservationActorMismatch,
    ObservationTargetProfileMismatch,
    AnalysisActorMismatch,
    AnalysisTargetProfileMismatch
}

public sealed class BattleKnowledgeExecutionDiagnostic
{
    public BattleKnowledgeExecutionDiagnostic(
        BattleKnowledgeExecutionDiagnosticCode code,
        int effectIndex,
        string message)
    {
        Code = EnumDomain.RequireDefined(code, nameof(code));
        ArgumentOutOfRangeException.ThrowIfNegative(effectIndex);
        EffectIndex = effectIndex;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Knowledge execution diagnostics require a message.", nameof(message))
            : message;
    }

    public BattleKnowledgeExecutionDiagnosticCode Code { get; }
    public int EffectIndex { get; }
    public string Message { get; }
}

public sealed class BattleKnowledgeAnalysisEvidence
{
    public BattleKnowledgeAnalysisEvidence(int effectIndex, BattleAnalysisResult analysis)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(effectIndex);
        EffectIndex = effectIndex;
        Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
    }

    public int EffectIndex { get; }
    public BattleAnalysisResult Analysis { get; }
}

/// <summary>
/// Identifies the accepted action, acting runtime actor, and target combat profiles
/// against which execution-produced knowledge evidence is validated.
/// </summary>
public sealed class BattleKnowledgeExecutionAuthority
{
    public BattleKnowledgeExecutionAuthority(
        ContentId sourceActionId,
        RuntimeInstanceId actorId,
        IEnumerable<KeyValuePair<RuntimeInstanceId, RuntimeCombatProfileIdentitySnapshot>> targetProfiles)
    {
        if (!sourceActionId.IsValid)
        {
            throw new ArgumentException("Knowledge execution authority requires a valid source action ID.", nameof(sourceActionId));
        }
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Knowledge execution authority requires a valid acting runtime ID.", nameof(actorId));
        }

        KeyValuePair<RuntimeInstanceId, RuntimeCombatProfileIdentitySnapshot>[] targets =
            (targetProfiles ?? throw new ArgumentNullException(nameof(targetProfiles))).ToArray();
        if (targets.Any(target =>
                !target.Key.IsValid ||
                target.Value is null ||
                !target.Value.SourceActorInstanceId.IsValid ||
                !target.Value.SourceEntityDefinitionId.IsValid))
        {
            throw new ArgumentException(
                "Knowledge execution targets require valid runtime and combat-profile IDs.",
                nameof(targetProfiles));
        }

        RuntimeInstanceId? duplicate = targets
            .GroupBy(target => target.Key)
            .Where(group => group.Count() > 1)
            .Select(group => (RuntimeInstanceId?)group.Key)
            .FirstOrDefault();
        if (duplicate is RuntimeInstanceId duplicateId)
        {
            throw new ArgumentException(
                $"Knowledge execution target runtime ID '{duplicateId}' is duplicated.",
                nameof(targetProfiles));
        }

        SourceActionId = sourceActionId;
        ActorId = actorId;
        TargetProfiles = new ReadOnlyDictionary<RuntimeInstanceId, RuntimeCombatProfileIdentitySnapshot>(
            targets.ToDictionary(target => target.Key, target => target.Value));
    }

    public ContentId SourceActionId { get; }
    public RuntimeInstanceId ActorId { get; }
    public IReadOnlyDictionary<RuntimeInstanceId, RuntimeCombatProfileIdentitySnapshot> TargetProfiles { get; }
}

public sealed class BattleKnowledgeExecutionTransitionRequest
{
    public BattleKnowledgeExecutionTransitionRequest(
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        BattleKnowledgeExecutionAuthority authority,
        IEnumerable<EffectExecutionResult> effects,
        BattleKnowledgePersistenceScope persistenceScope)
    {
        PersistentBefore = persistentBefore ?? throw new ArgumentNullException(nameof(persistentBefore));
        EncounterBefore = encounterBefore ?? throw new ArgumentNullException(nameof(encounterBefore));
        Authority = authority ?? throw new ArgumentNullException(nameof(authority));
        EffectExecutionResult[] effectSnapshot =
            (effects ?? throw new ArgumentNullException(nameof(effects))).ToArray();
        if (effectSnapshot.Any(effect => effect is null))
        {
            throw new ArgumentException("Knowledge execution effects cannot contain null entries.", nameof(effects));
        }
        Effects = Array.AsReadOnly(effectSnapshot);
        PersistenceScope = EnumDomain.RequireDefined(persistenceScope, nameof(persistenceScope));
    }

    public RuntimeKnowledgeSnapshot PersistentBefore { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterBefore { get; }
    public BattleKnowledgeExecutionAuthority Authority { get; }
    public IReadOnlyList<EffectExecutionResult> Effects { get; }
    public BattleKnowledgePersistenceScope PersistenceScope { get; }
}

public sealed class BattleKnowledgeExecutionTransitionResult
{
    public BattleKnowledgeExecutionTransitionResult(
        BattleKnowledgeTransitionStatus status,
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeKnowledgeSnapshot persistentAfter,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        RuntimeEncounterKnowledgeSnapshot encounterAfter,
        IEnumerable<BattleKnowledgeObservation>? acceptedObservations = null,
        IEnumerable<BattleKnowledgeAnalysisEvidence>? processedAnalyses = null,
        IEnumerable<BattleKnowledgeExecutionDiagnostic>? diagnostics = null)
    {
        Status = EnumDomain.RequireDefined(status, nameof(status));
        PersistentBefore = persistentBefore ?? throw new ArgumentNullException(nameof(persistentBefore));
        PersistentAfter = persistentAfter ?? throw new ArgumentNullException(nameof(persistentAfter));
        EncounterBefore = encounterBefore ?? throw new ArgumentNullException(nameof(encounterBefore));
        EncounterAfter = encounterAfter ?? throw new ArgumentNullException(nameof(encounterAfter));
        AcceptedObservations = Snapshot(acceptedObservations);
        ProcessedAnalyses = Snapshot(processedAnalyses);
        Diagnostics = Snapshot(diagnostics);
    }

    public BattleKnowledgeTransitionStatus Status { get; }
    public bool Applied => Status == BattleKnowledgeTransitionStatus.Applied;
    public RuntimeKnowledgeSnapshot PersistentBefore { get; }
    public RuntimeKnowledgeSnapshot PersistentAfter { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterBefore { get; }
    public RuntimeEncounterKnowledgeSnapshot EncounterAfter { get; }
    public IReadOnlyList<BattleKnowledgeObservation> AcceptedObservations { get; }
    public IReadOnlyList<BattleKnowledgeAnalysisEvidence> ProcessedAnalyses { get; }
    public IReadOnlyList<BattleKnowledgeExecutionDiagnostic> Diagnostics { get; }

    private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T>? values)
    {
        T[] snapshot = (values ?? []).ToArray();
        if (snapshot.Any(value => value is null))
        {
            throw new ArgumentException("Knowledge execution result collections cannot contain null entries.", nameof(values));
        }
        return Array.AsReadOnly(snapshot);
    }
}

public interface IBattleKnowledgeExecutionTransitionService
{
    BattleKnowledgeExecutionTransitionResult Apply(
        BattleKnowledgeExecutionTransitionRequest request);
}

public sealed class BattleKnowledgeExecutionTransitionService : IBattleKnowledgeExecutionTransitionService
{
    private readonly IBattleKnowledgeObservationTransitionService _observations;
    private readonly IBattleAnalysisKnowledgeTransitionService _analysis;
    private readonly IBattleKnowledgeTargetProfileTransitionService _profiles;

    public BattleKnowledgeExecutionTransitionService(
        IBattleKnowledgeObservationTransitionService? observations = null,
        IBattleAnalysisKnowledgeTransitionService? analysis = null,
        IBattleKnowledgeTargetProfileTransitionService? profiles = null)
    {
        _profiles = profiles ?? new BattleKnowledgeTargetProfileTransitionService();
        _observations = observations ?? new BattleKnowledgeObservationTransitionService(
            profileTransitions: _profiles);
        _analysis = analysis ?? new BattleAnalysisKnowledgeTransitionService(
            profileTransitions: _profiles);
    }

    public BattleKnowledgeExecutionTransitionResult Apply(
        BattleKnowledgeExecutionTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        BattleKnowledgeExecutionTransitionResult? provenanceFailure = ValidateProvenance(request);
        if (provenanceFailure is not null)
        {
            return provenanceFailure;
        }

        RuntimeKnowledgeSnapshot persistent = request.PersistentBefore;
        RuntimeEncounterKnowledgeSnapshot encounter = request.EncounterBefore;
        var acceptedObservations = new List<BattleKnowledgeObservation>();
        var processedAnalyses = new List<BattleKnowledgeAnalysisEvidence>();
        bool changed = false;

        foreach ((RuntimeInstanceId targetId, RuntimeCombatProfileIdentitySnapshot profile) in
                 request.Authority.TargetProfiles)
        {
            BattleKnowledgeTargetProfileChangeResult rebound = _profiles.RebindTargetProfile(
                encounter,
                targetId,
                profile);
            encounter = rebound.After;
            changed |= rebound.Invalidated;
        }

        foreach (EffectExecutionResult effect in request.Effects)
        {
            if (effect.KnowledgeObservations.Count > 0)
            {
                BattleKnowledgeObservationTransitionResult observation = _observations.Apply(
                    new BattleKnowledgeObservationTransitionRequest(
                        persistent,
                        encounter,
                        effect.KnowledgeObservations,
                        request.PersistenceScope));
                if (observation.Status == BattleKnowledgeTransitionStatus.Rejected)
                {
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.ObservationRejected,
                        effect.EffectIndex,
                        observation.Diagnostics.Select(diagnostic => diagnostic.Message));
                }

                persistent = observation.PersistentAfter;
                encounter = observation.EncounterAfter;
                changed |= observation.Status == BattleKnowledgeTransitionStatus.Applied;
                acceptedObservations.AddRange(observation.AcceptedObservations);
            }

            if (effect.Analysis is BattleAnalysisResult analysis)
            {
                BattleAnalysisKnowledgeTransitionResult analyzed = _analysis.Apply(
                    persistent,
                    encounter,
                    analysis,
                    request.PersistenceScope);
                if (analyzed.Status == BattleKnowledgeTransitionStatus.Rejected)
                {
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.AnalysisRejected,
                        effect.EffectIndex,
                        analyzed.Diagnostics.Select(diagnostic => diagnostic.Message));
                }

                persistent = analyzed.PersistentAfter;
                encounter = analyzed.EncounterAfter;
                changed |= analyzed.Status == BattleKnowledgeTransitionStatus.Applied;
                processedAnalyses.Add(new BattleKnowledgeAnalysisEvidence(effect.EffectIndex, analysis));
            }
        }

        return new BattleKnowledgeExecutionTransitionResult(
            changed ? BattleKnowledgeTransitionStatus.Applied : BattleKnowledgeTransitionStatus.Unchanged,
            request.PersistentBefore,
            persistent,
            request.EncounterBefore,
            encounter,
            acceptedObservations,
            processedAnalyses);
    }

    private static BattleKnowledgeExecutionTransitionResult? ValidateProvenance(
        BattleKnowledgeExecutionTransitionRequest request)
    {
        foreach (EffectExecutionResult effect in request.Effects)
        {
            foreach (BattleKnowledgeObservation observation in effect.KnowledgeObservations)
            {
                if (observation.EffectIndex != effect.EffectIndex)
                {
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.ObservationEffectIndexMismatch,
                        effect.EffectIndex,
                        [
                            $"Knowledge observation effect index {observation.EffectIndex} " +
                            $"does not match enclosing execution effect index {effect.EffectIndex}."
                        ]);
                }
                if (effect.TargetId is not RuntimeInstanceId targetId || observation.TargetId != targetId)
                {
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.ObservationTargetMismatch,
                        effect.EffectIndex,
                        [
                            $"Knowledge observation target '{observation.TargetId}' " +
                            $"does not match enclosing execution target '{effect.TargetId}'."
                        ]);
                }
                if (observation.SourceActionId != request.Authority.SourceActionId)
                {
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.ObservationSourceActionMismatch,
                        effect.EffectIndex,
                        [
                            $"Knowledge observation source action '{observation.SourceActionId}' " +
                            $"does not match accepted action '{request.Authority.SourceActionId}'."
                        ]);
                }
                if (observation.ActorId != request.Authority.ActorId)
                {
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.ObservationActorMismatch,
                        effect.EffectIndex,
                        [
                            $"Knowledge observation actor '{observation.ActorId}' " +
                            $"does not match acting runtime actor '{request.Authority.ActorId}'."
                        ]);
                }
                bool targetProfileKnown = request.Authority.TargetProfiles.TryGetValue(
                    targetId,
                    out RuntimeCombatProfileIdentitySnapshot? targetProfile);
                if (!targetProfileKnown ||
                    observation.TargetProfileIdentity != targetProfile)
                {
                    string expectedProfile = targetProfileKnown
                        ? Describe(targetProfile!)
                        : "<unbound>";
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.ObservationTargetProfileMismatch,
                        effect.EffectIndex,
                        [
                            $"Knowledge observation target profile " +
                            $"'{Describe(observation.TargetProfileIdentity)}' does not match " +
                            $"authoritative profile '{expectedProfile}' " +
                            $"for runtime target '{targetId}'."
                        ]);
                }
            }

            if (effect.Analysis is not BattleAnalysisResult analysis)
            {
                continue;
            }
            if (effect.TargetId is not RuntimeInstanceId analysisTargetId ||
                analysis.TargetId != analysisTargetId)
            {
                return Rejected(
                    request,
                    BattleKnowledgeExecutionDiagnosticCode.AnalysisTargetMismatch,
                    effect.EffectIndex,
                    [
                        $"Analyze target '{analysis.TargetId}' does not match enclosing " +
                        $"execution target '{effect.TargetId}'."
                    ]);
            }
            if (analysis.ActorId != request.Authority.ActorId)
            {
                return Rejected(
                    request,
                    BattleKnowledgeExecutionDiagnosticCode.AnalysisActorMismatch,
                    effect.EffectIndex,
                    [
                        $"Analyze actor '{analysis.ActorId}' does not match acting runtime actor " +
                        $"'{request.Authority.ActorId}'."
                    ]);
            }
            bool analysisTargetProfileKnown = request.Authority.TargetProfiles.TryGetValue(
                analysisTargetId,
                out RuntimeCombatProfileIdentitySnapshot? analysisTargetProfile);
            if (!analysisTargetProfileKnown ||
                analysis.TargetProfileIdentity != analysisTargetProfile)
            {
                string expectedProfile = analysisTargetProfileKnown
                    ? Describe(analysisTargetProfile!)
                    : "<unbound>";
                return Rejected(
                    request,
                    BattleKnowledgeExecutionDiagnosticCode.AnalysisTargetProfileMismatch,
                    effect.EffectIndex,
                    [
                        $"Analyze target profile '{Describe(analysis.TargetProfileIdentity)}' does not match " +
                        $"authoritative profile '{expectedProfile}' " +
                        $"for runtime target '{analysisTargetId}'."
                    ]);
            }
        }

        return null;
    }

    private static BattleKnowledgeExecutionTransitionResult Rejected(
        BattleKnowledgeExecutionTransitionRequest request,
        BattleKnowledgeExecutionDiagnosticCode code,
        int effectIndex,
        IEnumerable<string> messages)
    {
        string message = string.Join(
            " ",
            (messages ?? []).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (message.Length == 0)
        {
            message = code switch
            {
                BattleKnowledgeExecutionDiagnosticCode.ObservationRejected =>
                    "The observation transition rejected the executed effect without diagnostics.",
                BattleKnowledgeExecutionDiagnosticCode.AnalysisRejected =>
                    "The analysis transition rejected the executed effect without diagnostics.",
                BattleKnowledgeExecutionDiagnosticCode.ObservationEffectIndexMismatch =>
                    "The executed effect contained knowledge evidence with a mismatched effect index.",
                BattleKnowledgeExecutionDiagnosticCode.ObservationTargetMismatch =>
                    "The executed effect contained knowledge evidence for a different target.",
                BattleKnowledgeExecutionDiagnosticCode.AnalysisTargetMismatch =>
                    "The executed effect contained Analyze evidence for a different target.",
                BattleKnowledgeExecutionDiagnosticCode.ObservationSourceActionMismatch =>
                    "The executed effect contained knowledge evidence for a different source action.",
                BattleKnowledgeExecutionDiagnosticCode.ObservationActorMismatch =>
                    "The executed effect contained knowledge evidence for a different acting runtime actor.",
                BattleKnowledgeExecutionDiagnosticCode.ObservationTargetProfileMismatch =>
                    "The executed effect contained knowledge evidence for a different target combat profile.",
                BattleKnowledgeExecutionDiagnosticCode.AnalysisActorMismatch =>
                    "The executed effect contained Analyze evidence for a different acting runtime actor.",
                BattleKnowledgeExecutionDiagnosticCode.AnalysisTargetProfileMismatch =>
                    "The executed effect contained Analyze evidence for a different target combat profile.",
                _ => "The battle-knowledge execution transition was rejected."
            };
        }

        return new BattleKnowledgeExecutionTransitionResult(
            BattleKnowledgeTransitionStatus.Rejected,
            request.PersistentBefore,
            request.PersistentBefore,
            request.EncounterBefore,
            request.EncounterBefore,
            diagnostics:
            [
                new BattleKnowledgeExecutionDiagnostic(
                    code,
                    effectIndex,
                    message)
            ]);
    }

    private static string Describe(RuntimeCombatProfileIdentitySnapshot profile) =>
        $"{profile.SourceActorInstanceId}/{profile.SourceEntityDefinitionId}@{profile.Revision}";
}
