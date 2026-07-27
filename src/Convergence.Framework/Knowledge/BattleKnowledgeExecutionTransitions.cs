using Convergence.Execution;
using Convergence.Internal;
using Convergence.Runtime;

namespace Convergence.Knowledge;

public enum BattleKnowledgeExecutionDiagnosticCode
{
    ObservationRejected,
    AnalysisRejected,
    ObservationEffectIndexMismatch
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

public sealed class BattleKnowledgeExecutionTransitionRequest
{
    public BattleKnowledgeExecutionTransitionRequest(
        RuntimeKnowledgeSnapshot persistentBefore,
        RuntimeEncounterKnowledgeSnapshot encounterBefore,
        IEnumerable<EffectExecutionResult> effects,
        BattleKnowledgePersistenceScope persistenceScope)
    {
        PersistentBefore = persistentBefore ?? throw new ArgumentNullException(nameof(persistentBefore));
        EncounterBefore = encounterBefore ?? throw new ArgumentNullException(nameof(encounterBefore));
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

    public BattleKnowledgeExecutionTransitionService(
        IBattleKnowledgeObservationTransitionService? observations = null,
        IBattleAnalysisKnowledgeTransitionService? analysis = null)
    {
        _observations = observations ?? new BattleKnowledgeObservationTransitionService();
        _analysis = analysis ?? new BattleAnalysisKnowledgeTransitionService();
    }

    public BattleKnowledgeExecutionTransitionResult Apply(
        BattleKnowledgeExecutionTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuntimeKnowledgeSnapshot persistent = request.PersistentBefore;
        RuntimeEncounterKnowledgeSnapshot encounter = request.EncounterBefore;
        var acceptedObservations = new List<BattleKnowledgeObservation>();
        var processedAnalyses = new List<BattleKnowledgeAnalysisEvidence>();
        bool changed = false;

        foreach (EffectExecutionResult effect in request.Effects)
        {
            if (effect.KnowledgeObservations.Count > 0)
            {
                BattleKnowledgeObservation? mismatchedObservation =
                    effect.KnowledgeObservations.FirstOrDefault(
                        observation => observation.EffectIndex != effect.EffectIndex);
                if (mismatchedObservation is not null)
                {
                    return Rejected(
                        request,
                        BattleKnowledgeExecutionDiagnosticCode.ObservationEffectIndexMismatch,
                        effect.EffectIndex,
                        [
                            $"Knowledge observation effect index {mismatchedObservation.EffectIndex} " +
                            $"does not match enclosing execution effect index {effect.EffectIndex}."
                        ]);
                }

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
}
