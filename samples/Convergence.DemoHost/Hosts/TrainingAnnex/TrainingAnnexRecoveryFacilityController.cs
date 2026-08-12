using Convergence.Content;
using Convergence.Hosting;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed record TrainingAnnexHospitalRestorationEvidence(
    RuntimeInstanceId PatientId,
    RecoveryTransactionCode Code,
    int Cost,
    int CurrencyLedgerBefore,
    int CurrencyLedgerAfter,
    int HpBefore,
    int HpAfter,
    int MaxHp,
    int SpBefore,
    int SpAfter,
    int MaxSp,
    bool HadAilmentBefore,
    bool HasAilmentAfter,
    bool HadEncounterPersistenceBefore,
    bool HasEncounterPersistenceAfter);

internal sealed record TrainingAnnexRecoveryFacilityResult(
    RuntimeCurrencyLedgerSnapshot CurrencyLedger,
    IReadOnlyList<TrainingAnnexHospitalRestorationEvidence> Restorations);

internal sealed class TrainingAnnexRecoveryFacilityController
{
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;
    private readonly IStatModifierPolicyService _statModifiers;

    public TrainingAnnexRecoveryFacilityController(
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        IStatModifierPolicyService statModifiers)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
        _statModifiers = statModifiers ?? throw new ArgumentNullException(nameof(statModifiers));
    }

    public async ValueTask<TrainingAnnexRecoveryFacilityResult> OpenAsync(
        IRecoveryService recovery,
        TrainingAnnexRuntimeActor patient,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(currencyLedger);
        ArgumentNullException.ThrowIfNull(commands);

        RecoveryTransactionResult assessment = recovery.Assess(
            patient.Actor.State,
            currencyLedger,
            _statModifiers);
        var evidence = new List<TrainingAnnexHospitalRestorationEvidence>();

        await _eventSink.PublishAsync(
            $"Recovery facility opened: {patient.Actor.Entity.DisplayName}; wallet " +
            $"{TrainingAnnexHostSupport.GetCreditsBalance(currencyLedger)} C.",
            cancellationToken).ConfigureAwait(false);
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
            await _commandSource.ReadAsync(
                CreateRecoveryMenu(patient.Actor.Entity.DisplayName, assessment),
                cancellationToken).ConfigureAwait(false);
        if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync(
                "Recovery canceled; wallet and actor state are unchanged.",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexRecoveryFacilityResult(currencyLedger, evidence);
        }

        commands.Add(selection.Command);
        RecoveryTransactionResult restoration = recovery.Recover(
            patient.Actor.State,
            currencyLedger,
            _statModifiers);
        evidence.Add(ToEvidence(restoration));
        if (!restoration.Applied)
        {
            await PublishFailureAsync(patient.Actor.Entity.DisplayName, restoration, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexRecoveryFacilityResult(currencyLedger, evidence);
        }

        currencyLedger = restoration.AfterCurrencyLedger;
        RuntimeResourceSnapshot beforeHp = Resource(restoration.BeforeActor, StandardProgressionIds.Hp);
        RuntimeResourceSnapshot afterHp = Resource(restoration.AfterActor, StandardProgressionIds.Hp);
        RuntimeResourceSnapshot beforeSp = Resource(restoration.BeforeActor, StandardProgressionIds.Sp);
        RuntimeResourceSnapshot afterSp = Resource(restoration.AfterActor, StandardProgressionIds.Sp);
        await _eventSink.PublishAsync(
            $"Recovery complete: {patient.Actor.Entity.DisplayName}; HP " +
            $"{Whole(beforeHp.Current, "current HP")}->{Whole(afterHp.Current, "current HP")}/" +
            $"{Whole(afterHp.Maximum, "maximum HP")}; SP " +
            $"{Whole(beforeSp.Current, "current SP")}->{Whole(afterSp.Current, "current SP")}/" +
            $"{Whole(afterSp.Maximum, "maximum SP")}; wallet " +
            $"{TrainingAnnexHostSupport.GetCreditsBalance(restoration.BeforeCurrencyLedger)}->" +
            $"{TrainingAnnexHostSupport.GetCreditsBalance(restoration.AfterCurrencyLedger)}.",
            cancellationToken).ConfigureAwait(false);
        return new TrainingAnnexRecoveryFacilityResult(currencyLedger, evidence);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateRecoveryMenu(
        string displayName,
        RecoveryTransactionResult assessment)
    {
        string treatmentLabel =
            $"Treat {displayName} - {assessment.Cost} C{TreatmentLabel(assessment)}";
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Recovery Facility",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.RecoveryTreat,
                    treatmentLabel,
                    assessment.Applied,
                    "Restore configured resources and clear policy-selected recoverable state."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);
    }

    private static string TreatmentLabel(RecoveryTransactionResult result) =>
        result.Code switch
        {
            RecoveryTransactionCode.Applied => string.Empty,
            RecoveryTransactionCode.InsufficientCurrency => " [Not enough Credits]",
            RecoveryTransactionCode.NoRecoveryNeeded => " [No restoration needed]",
            _ => $" [{result.Code}]"
        };

    private static TrainingAnnexHospitalRestorationEvidence ToEvidence(
        RecoveryTransactionResult result)
    {
        RuntimeResourceSnapshot beforeHp = Resource(result.BeforeActor, StandardProgressionIds.Hp);
        RuntimeResourceSnapshot afterHp = Resource(result.AfterActor, StandardProgressionIds.Hp);
        RuntimeResourceSnapshot beforeSp = Resource(result.BeforeActor, StandardProgressionIds.Sp);
        RuntimeResourceSnapshot afterSp = Resource(result.AfterActor, StandardProgressionIds.Sp);
        return new TrainingAnnexHospitalRestorationEvidence(
            result.BeforeActor.Identity.InstanceId,
            result.Code,
            result.Cost,
            TrainingAnnexHostSupport.GetCreditsBalance(result.BeforeCurrencyLedger),
            TrainingAnnexHostSupport.GetCreditsBalance(result.AfterCurrencyLedger),
            Whole(beforeHp.Current, "current HP"),
            Whole(afterHp.Current, "current HP"),
            Whole(afterHp.Maximum, "maximum HP"),
            Whole(beforeSp.Current, "current SP"),
            Whole(afterSp.Current, "current SP"),
            Whole(afterSp.Maximum, "maximum SP"),
            result.BeforeActor.BattleStatus.Ailments.Count > 0,
            result.AfterActor.BattleStatus.Ailments.Count > 0,
            HasEncounterPersistence(result.BeforeActor),
            HasEncounterPersistence(result.AfterActor));
    }

    private static RuntimeResourceSnapshot Resource(RuntimeActorSnapshot actor, ContentId resourceId) =>
        actor.Resources.Single(resource => resource.ResourceId == resourceId);

    private static bool HasEncounterPersistence(RuntimeActorSnapshot actor) =>
        actor.BattleStatus.IsGuarding ||
        actor.BattleStatus.StatModifiers?.Tracks.Count > 0 ||
        actor.BattleStatus.Charges.Count > 0 ||
        actor.BattleStatus.Shields.Count > 0 ||
        actor.BattleStatus.AffinityBreaks.Count > 0 ||
        actor.BattleStatus.AffinityOverrides.Count > 0 ||
        actor.BattleStatus.Statuses.Count > 0;

    private static int Whole(decimal value, string label)
    {
        if (value < 0m || value > int.MaxValue || decimal.Truncate(value) != value)
        {
            throw new InvalidOperationException(
                $"Training Annex recovery requires whole-number {label}; found {value}.");
        }

        return decimal.ToInt32(value);
    }

    private async ValueTask PublishFailureAsync(
        string displayName,
        RecoveryTransactionResult result,
        CancellationToken cancellationToken)
    {
        string diagnostics = string.Join(
            "; ",
            result.Diagnostics.Select(diagnostic => diagnostic.Message));
        await _eventSink.PublishAsync(
            $"Recovery rejected: {displayName}; {diagnostics}",
            cancellationToken).ConfigureAwait(false);
    }
}
