using Convergence.Content;
using Convergence.Fusion;
using Convergence.Hosting;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed record TrainingAnnexAcquisitionRegistrationResult(
    CompendiumStateSnapshot Compendium,
    TrainingAnnexBattleKnowledgeState PlayerKnowledge,
    TrainingAnnexCompendiumEvidence Evidence);

internal sealed class TrainingAnnexAcquisitionRegistrar
{
    private readonly ICompendiumRuntimeService _compendium;
    private readonly IFamiliarEntityKnowledgeService _familiarKnowledge;
    private readonly IHostEventSink<string> _eventSink;

    public TrainingAnnexAcquisitionRegistrar(
        ICompendiumRuntimeService compendium,
        IFamiliarEntityKnowledgeService familiarKnowledge,
        IHostEventSink<string> eventSink)
    {
        _compendium = compendium ?? throw new ArgumentNullException(nameof(compendium));
        _familiarKnowledge = familiarKnowledge ?? throw new ArgumentNullException(nameof(familiarKnowledge));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public async ValueTask<TrainingAnnexAcquisitionRegistrationResult> RecordAsync(
        CompendiumStateSnapshot compendium,
        TrainingAnnexBattleKnowledgeState playerKnowledge,
        TrainingAnnexRuntimeActor acquiredActor,
        RuntimePartyRosterSnapshot partyRoster,
        RuntimeWalletSnapshot wallet,
        ContentId acquisitionSourceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compendium);
        ArgumentNullException.ThrowIfNull(playerKnowledge);
        ArgumentNullException.ThrowIfNull(acquiredActor);
        ArgumentNullException.ThrowIfNull(partyRoster);
        ArgumentNullException.ThrowIfNull(wallet);
        if (!acquisitionSourceId.IsValid)
        {
            throw new ArgumentException("Acquisition source ID must be valid.", nameof(acquisitionSourceId));
        }

        CompendiumActorRegistrationResult registration = _compendium.RecordAcquisition(
            compendium,
            acquiredActor.Actor.State.ToSnapshot());
        if (!registration.Accepted || registration.Entry is null)
        {
            foreach (CompendiumRuntimeDiagnostic diagnostic in registration.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"Compendium acquisition rejected [{diagnostic.Code}]: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexAcquisitionRegistrationResult(
                compendium,
                playerKnowledge,
                Evidence(
                    acquiredActor.Actor.Entity.Id,
                    acquisitionSourceId,
                    registration,
                    partyRoster,
                    wallet));
        }

        FamiliarKnowledgeImportResult imported = _familiarKnowledge.Import(
            playerKnowledge.ToSnapshot(),
            [registration.Entry.EntityId],
            FamiliarKnowledgeImportSource.Acquisition);
        TrainingAnnexBattleKnowledgeState nextKnowledge =
            TrainingAnnexBattleKnowledgeState.FromSnapshot(imported.After);

        string message = registration.Code switch
        {
            CompendiumRegistrationCode.Added =>
                $"Compendium first-acquisition record added: {registration.Entry.DisplayName} ({acquisitionSourceId}).",
            CompendiumRegistrationCode.AlreadyRegistered =>
                $"Compendium record preserved: {registration.Entry.DisplayName} was already registered; {acquisitionSourceId} did not overwrite it.",
            _ => throw new InvalidOperationException(
                $"Acquisition recording returned unexpected code '{registration.Code}'.")
        };
        await _eventSink.PublishAsync(message, cancellationToken).ConfigureAwait(false);

        return new TrainingAnnexAcquisitionRegistrationResult(
            registration.After,
            nextKnowledge,
            Evidence(
                registration.Entry.EntityId,
                acquisitionSourceId,
                registration,
                partyRoster,
                wallet,
                imported));
    }

    private static TrainingAnnexCompendiumEvidence Evidence(
        ContentId entityId,
        ContentId acquisitionSourceId,
        CompendiumActorRegistrationResult registration,
        RuntimePartyRosterSnapshot partyRoster,
        RuntimeWalletSnapshot wallet,
        FamiliarKnowledgeImportResult? imported = null) =>
        new(
            TrainingAnnexCompendiumAction.Acquisition,
            entityId,
            registration.Applied,
            registration.Code,
            null,
            0,
            wallet.Balance,
            wallet.Balance,
            partyRoster.CompanionRoster.Count,
            partyRoster.CompanionRoster.Count,
            imported?.After.ElementalAffinities.Count(entry => entry.EntityId == entityId) ?? 0,
            imported?.After.AilmentResistances.Count(entry => entry.EntityId == entityId) ?? 0,
            imported?.After.InstantDeathResistances.Count(entry => entry.EntityId == entityId) ?? 0,
            registration.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            acquisitionSourceId);
}
