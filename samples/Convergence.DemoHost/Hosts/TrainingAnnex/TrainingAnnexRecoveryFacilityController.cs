using Convergence.Content;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed record TrainingAnnexHospitalRestorationEvidence(
    RuntimeInstanceId PatientId,
    ResourceTransactionCode Code,
    int Cost,
    int WalletBefore,
    int WalletAfter,
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
    RuntimeWalletSnapshot Wallet,
    IReadOnlyList<TrainingAnnexHospitalRestorationEvidence> Restorations);

internal sealed class TrainingAnnexRecoveryFacilityController
{
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;

    public TrainingAnnexRecoveryFacilityController(
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
    }

    public async ValueTask<TrainingAnnexRecoveryFacilityResult> OpenAsync(
        IHospitalRestorationService hospital,
        TrainingAnnexRuntimeActor patient,
        RuntimeWalletSnapshot wallet,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hospital);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(commands);

        RuntimeHospitalPatientSnapshot patientSnapshot = CapturePatient(patient.Actor.State);
        HospitalRestorationResult assessment = hospital.Restore(patientSnapshot, wallet);
        var evidence = new List<TrainingAnnexHospitalRestorationEvidence>();

        await _eventSink.PublishAsync(
            $"Recovery facility opened: {patient.Actor.Entity.DisplayName}; wallet {wallet.Balance} C.",
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
            return new TrainingAnnexRecoveryFacilityResult(wallet, evidence);
        }

        commands.Add(selection.Command);
        HospitalRestorationResult restoration = hospital.Restore(CapturePatient(patient.Actor.State), wallet);
        evidence.Add(ToEvidence(restoration));
        if (!restoration.Applied)
        {
            await PublishFailureAsync(patient.Actor.Entity.DisplayName, restoration, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexRecoveryFacilityResult(wallet, evidence);
        }

        ApplyRestoration(patient.Actor.State, restoration.AfterPatient);
        wallet = restoration.AfterWallet;
        await _eventSink.PublishAsync(
            $"Recovery complete: {patient.Actor.Entity.DisplayName}; HP {restoration.BeforePatient.CurrentHp}->{restoration.AfterPatient.CurrentHp}/{restoration.AfterPatient.MaxHp}; SP {restoration.BeforePatient.CurrentSp}->{restoration.AfterPatient.CurrentSp}/{restoration.AfterPatient.MaxSp}; wallet {restoration.BeforeWallet.Balance}->{restoration.AfterWallet.Balance}.",
            cancellationToken).ConfigureAwait(false);
        return new TrainingAnnexRecoveryFacilityResult(wallet, evidence);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateRecoveryMenu(
        string displayName,
        HospitalRestorationResult assessment)
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
                    "Restore HP/SP, remove ailments, and clear encounter-persistent state."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);
    }

    private static string TreatmentLabel(HospitalRestorationResult result) =>
        result.Code switch
        {
            ResourceTransactionCode.Applied => string.Empty,
            ResourceTransactionCode.InsufficientCurrency => " [Not enough Credits]",
            ResourceTransactionCode.NoRestorationNeeded => " [No restoration needed]",
            _ => $" [{result.Code}]"
        };

    private static RuntimeHospitalPatientSnapshot CapturePatient(RuntimeActorState actor)
    {
        BattleResourceState hp = actor.GetRequiredResource(StandardProgressionIds.Hp);
        BattleResourceState sp = actor.GetRequiredResource(StandardProgressionIds.Sp);
        return new RuntimeHospitalPatientSnapshot(
            actor.InstanceId,
            ToWholeResource(hp.Current, "current HP"),
            ToWholeResource(hp.Maximum, "maximum HP"),
            ToWholeResource(sp.Current, "current SP"),
            ToWholeResource(sp.Maximum, "maximum SP"),
            actor.Ailments.Count > 0,
            HasEncounterPersistence(actor));
    }

    private static bool HasEncounterPersistence(RuntimeActorState actor) =>
        actor.IsGuarding ||
        actor.StatStages.Count > 0 ||
        actor.Charges.Count > 0 ||
        actor.Shields.Count > 0 ||
        actor.AffinityBreaks.Count > 0 ||
        actor.AffinityOverrides.Count > 0 ||
        actor.OtherStatuses.Count > 0;

    private static int ToWholeResource(decimal value, string label)
    {
        if (value < 0 || value > int.MaxValue || decimal.Truncate(value) != value)
        {
            throw new InvalidOperationException(
                $"Training Annex recovery requires whole-number {label}; found {value}.");
        }

        return (int)value;
    }

    private static void ApplyRestoration(RuntimeActorState actor, RuntimeHospitalPatientSnapshot after)
    {
        actor.SetResource(StandardProgressionIds.Hp, after.CurrentHp);
        actor.SetResource(StandardProgressionIds.Sp, after.CurrentSp);
        actor.RemoveAilments(_ => true);
        new BattleStatusLifecycleService(new TrainingAnnexMinimumRandomSource()).Cleanup(
            new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.FieldTransition),
            DemoStatModifierPolicy.CreatePersistent());
    }

    private static TrainingAnnexHospitalRestorationEvidence ToEvidence(HospitalRestorationResult result) =>
        new(
            result.BeforePatient.PatientId,
            result.Code,
            result.Cost,
            result.BeforeWallet.Balance,
            result.AfterWallet.Balance,
            result.BeforePatient.CurrentHp,
            result.AfterPatient.CurrentHp,
            result.BeforePatient.MaxHp,
            result.BeforePatient.CurrentSp,
            result.AfterPatient.CurrentSp,
            result.BeforePatient.MaxSp,
            result.BeforePatient.HasAilment,
            result.AfterPatient.HasAilment,
            result.BeforePatient.HasEncounterPersistence,
            result.AfterPatient.HasEncounterPersistence);

    private async ValueTask PublishFailureAsync(
        string displayName,
        HospitalRestorationResult result,
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
