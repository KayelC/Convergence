using Convergence.Content;
using Convergence.Hosting;
using Convergence.Fusion;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal enum TrainingAnnexCompendiumAction
{
    Register,
    Recall,
    Acquisition
}

internal sealed record TrainingAnnexCompendiumEvidence(
    TrainingAnnexCompendiumAction Action,
    ContentId EntityId,
    bool Applied,
    CompendiumRegistrationCode? RegistrationCode,
    CompendiumRecallTransactionCode? RecallCode,
    int Cost,
    int WalletBefore,
    int WalletAfter,
    int DemonStockBefore,
    int DemonStockAfter,
    int ImportedElementalAffinities,
    int ImportedAilmentResistances,
    int ImportedInstantDeathResistances,
    IReadOnlyList<CompendiumRuntimeDiagnosticCode> DiagnosticCodes,
    ContentId? AcquisitionSourceId);

internal sealed record TrainingAnnexCompendiumInteractionResult(
    CompendiumStateSnapshot Compendium,
    RuntimePartyStockSnapshot PartyStock,
    RuntimeWalletSnapshot Wallet,
    TrainingAnnexActorRoster Roster,
    TrainingAnnexBattleKnowledgeState PlayerKnowledge,
    IReadOnlyList<TrainingAnnexCompendiumEvidence> Evidence);

internal sealed class TrainingAnnexCompendiumController
{
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;
    private readonly ICompendiumRuntimeService _compendium;
    private readonly IFamiliarEntityKnowledgeService _familiarKnowledge;

    public TrainingAnnexCompendiumController(
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        ICompendiumRuntimeService compendium,
        IFamiliarEntityKnowledgeService familiarKnowledge)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
        _compendium = compendium ?? throw new ArgumentNullException(nameof(compendium));
        _familiarKnowledge = familiarKnowledge ?? throw new ArgumentNullException(nameof(familiarKnowledge));
    }

    public async ValueTask<TrainingAnnexCompendiumInteractionResult> OpenAsync(
        CompendiumStateSnapshot compendium,
        RuntimePartyStockSnapshot partyStock,
        RuntimeWalletSnapshot wallet,
        TrainingAnnexActorRoster roster,
        TrainingAnnexBattleKnowledgeState playerKnowledge,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
            await _commandSource.ReadAsync(CreateRootMenu(compendium), cancellationToken).ConfigureAwait(false);
        if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            return Unchanged(compendium, partyStock, wallet, roster, playerKnowledge);
        }

        commands.Add(selection.Command);
        return selection.Command switch
        {
            CleanTrainingAnnexPlayCommand.CompendiumRegister => await RegisterAsync(
                compendium,
                partyStock,
                wallet,
                roster,
                playerKnowledge,
                commands,
                cancellationToken).ConfigureAwait(false),
            CleanTrainingAnnexPlayCommand.CompendiumRecall => await RecallAsync(
                compendium,
                partyStock,
                wallet,
                roster,
                playerKnowledge,
                commands,
                cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported Compendium command '{selection.Command}'.")
        };
    }

    private async ValueTask<TrainingAnnexCompendiumInteractionResult> RegisterAsync(
        CompendiumStateSnapshot compendium,
        RuntimePartyStockSnapshot partyStock,
        RuntimeWalletSnapshot wallet,
        TrainingAnnexActorRoster roster,
        TrainingAnnexBattleKnowledgeState playerKnowledge,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TrainingAnnexRuntimeActor> candidates = RegistrationCandidates(partyStock, roster);
        if (candidates.Count == 0)
        {
            await _eventSink.PublishAsync(
                "Compendium registration unavailable: no owned eligible actors were found.",
                cancellationToken).ConfigureAwait(false);
            return Unchanged(compendium, partyStock, wallet, roster, playerKnowledge);
        }

        HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection = await _commandSource.ReadAsync(
            CreateRegistrationMenu(candidates, compendium),
            cancellationToken).ConfigureAwait(false);
        TrainingAnnexRuntimeActor? selected = selection.SelectionIdentity?.RuntimeInstanceId is RuntimeInstanceId id
            ? candidates.FirstOrDefault(candidate => candidate.Actor.State.InstanceId == id)
            : null;
        if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back || selected is null)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            return Unchanged(compendium, partyStock, wallet, roster, playerKnowledge);
        }

        commands.Add(selection.Command);
        CompendiumActorRegistrationResult registration = _compendium.RegisterActor(
            compendium,
            selected.Actor.State.ToSnapshot());
        if (!registration.Applied || registration.Entry is null)
        {
            await PublishDiagnosticsAsync(registration.Diagnostics, cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexCompendiumInteractionResult(
                compendium,
                partyStock,
                wallet,
                roster,
                playerKnowledge,
                [Evidence(selected.Actor.Entity.Id, registration, partyStock, wallet)]);
        }

        FamiliarKnowledgeImportResult imported = _familiarKnowledge.Import(
            playerKnowledge.ToSnapshot(),
            [registration.Entry.EntityId]);
        TrainingAnnexBattleKnowledgeState nextKnowledge =
            TrainingAnnexBattleKnowledgeState.FromSnapshot(imported.After);
        await _eventSink.PublishAsync(
            $"Compendium {registration.Code.ToString().ToLowerInvariant()}: {registration.Entry.DisplayName}; familiar defense knowledge imported for the player only.",
            cancellationToken).ConfigureAwait(false);
        return new TrainingAnnexCompendiumInteractionResult(
            registration.After,
            partyStock,
            wallet,
            roster,
            nextKnowledge,
            [Evidence(selected.Actor.Entity.Id, registration, partyStock, wallet, imported)]);
    }

    private async ValueTask<TrainingAnnexCompendiumInteractionResult> RecallAsync(
        CompendiumStateSnapshot compendium,
        RuntimePartyStockSnapshot partyStock,
        RuntimeWalletSnapshot wallet,
        TrainingAnnexActorRoster roster,
        TrainingAnnexBattleKnowledgeState playerKnowledge,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        if (compendium.Entries.Count == 0)
        {
            await _eventSink.PublishAsync(
                "Compendium recall unavailable: no entries are registered.",
                cancellationToken).ConfigureAwait(false);
            return Unchanged(compendium, partyStock, wallet, roster, playerKnowledge);
        }

        HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection = await _commandSource.ReadAsync(
            CreateRecallMenu(compendium),
            cancellationToken).ConfigureAwait(false);
        ContentId? selectedId = selection.SelectionIdentity?.ContentId;
        if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back || selectedId is null)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            return Unchanged(compendium, partyStock, wallet, roster, playerKnowledge);
        }

        commands.Add(selection.Command);
        RuntimeActorSnapshot owner = roster.Player.Actor.State.ToSnapshot();
        RuntimeInstanceId instanceId = NextRecallInstanceId(selectedId.Value, roster);
        CompendiumRecallTransactionResult recall = _compendium.Recall(new CompendiumRecallTransactionRequest(
            compendium,
            partyStock,
            wallet,
            selectedId.Value,
            instanceId,
            owner.Ownership.ControllerId,
            owner.Ownership.TeamId,
            CompendiumRecallStockKind.Demon));
        if (!recall.Applied || recall.Actor is null)
        {
            await PublishDiagnosticsAsync(recall.Diagnostics, cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexCompendiumInteractionResult(
                compendium,
                partyStock,
                wallet,
                roster,
                playerKnowledge,
                [Evidence(selectedId.Value, recall)]);
        }

        var recalledActor = new TrainingAnnexRuntimeActor("Compendium Recall", recall.Actor);
        TrainingAnnexActorRoster nextRoster = roster.WithDynamicMember(recalledActor);
        FamiliarKnowledgeImportResult imported = _familiarKnowledge.Import(
            playerKnowledge.ToSnapshot(),
            [recall.Entry!.EntityId]);
        TrainingAnnexBattleKnowledgeState nextKnowledge =
            TrainingAnnexBattleKnowledgeState.FromSnapshot(imported.After);
        await _eventSink.PublishAsync(
            $"Compendium recall applied: {recall.Entry.DisplayName}; wallet {recall.BeforeWallet.Balance}->{recall.AfterWallet.Balance} M; Demon stock {recall.BeforePartyStock.DemonStock.Count}->{recall.AfterPartyStock.DemonStock.Count}.",
            cancellationToken).ConfigureAwait(false);
        return new TrainingAnnexCompendiumInteractionResult(
            compendium,
            recall.AfterPartyStock,
            recall.AfterWallet,
            nextRoster,
            nextKnowledge,
            [Evidence(selectedId.Value, recall, imported)]);
    }

    private async ValueTask PublishDiagnosticsAsync(
        IEnumerable<CompendiumRuntimeDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (CompendiumRuntimeDiagnostic diagnostic in diagnostics)
        {
            await _eventSink.PublishAsync(
                $"Compendium rejected [{diagnostic.Code}]: {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<TrainingAnnexRuntimeActor> RegistrationCandidates(
        RuntimePartyStockSnapshot partyStock,
        TrainingAnnexActorRoster roster)
    {
        HashSet<RuntimeInstanceId> ownedIds = OwnedReferences(partyStock)
            .Select(reference => reference.InstanceId)
            .ToHashSet();
        return roster.AllActors
            .Where(actor => ownedIds.Contains(actor.Actor.State.InstanceId))
            .Where(actor => actor.Actor.Entity.Capabilities.CompendiumEligible)
            .GroupBy(actor => actor.Actor.State.InstanceId)
            .Select(group => group.First())
            .OrderBy(actor => actor.Actor.Entity.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<RuntimeActorReferenceSnapshot> OwnedReferences(
        RuntimePartyStockSnapshot partyStock)
    {
        yield return partyStock.Owner;
        foreach (RuntimeActorReferenceSnapshot actor in partyStock.ActiveParty) yield return actor;
        foreach (RuntimeActorReferenceSnapshot actor in partyStock.ReserveMembers) yield return actor;
        if (partyStock.ActiveForm is not null) yield return partyStock.ActiveForm;
        foreach (RuntimeActorReferenceSnapshot actor in partyStock.PersonaStock) yield return actor;
        foreach (RuntimeActorReferenceSnapshot actor in partyStock.DemonStock) yield return actor;
    }

    private static RuntimeInstanceId NextRecallInstanceId(ContentId entityId, TrainingAnnexActorRoster roster)
    {
        string localId = entityId.ToString().Split(':').Last();
        HashSet<RuntimeInstanceId> existing = roster.AllActors
            .Select(actor => actor.Actor.State.InstanceId)
            .ToHashSet();
        for (int suffix = 1; ; suffix++)
        {
            RuntimeInstanceId candidate = RuntimeInstanceId.Parse($"recall_{localId}_{suffix}");
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateRootMenu(
        CompendiumStateSnapshot compendium) =>
        new(
            $"Clean Compendium - {compendium.Entries.Count} entr{(compendium.Entries.Count == 1 ? "y" : "ies")}",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.CompendiumRegister,
                    "Register Owned Actor"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.CompendiumRecall,
                    "Recall Registered Actor",
                    compendium.Entries.Count > 0),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateRegistrationMenu(
        IReadOnlyList<TrainingAnnexRuntimeActor> candidates,
        CompendiumStateSnapshot compendium) =>
        new(
            "Register Owned Actor",
            candidates.Select(candidate =>
                {
                    bool update = compendium.TryGet(candidate.Actor.Entity.Id, out _);
                    return new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.SelectCompendiumActor,
                        update
                            ? $"{candidate.Actor.Entity.DisplayName} [Update]"
                            : candidate.Actor.Entity.DisplayName,
                        SelectionIdentity: HostCommandSelectionIdentity.ForRuntimeInstance(
                            candidate.Actor.State.InstanceId));
                })
                .Append(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back"))
                .ToArray());

    private HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateRecallMenu(
        CompendiumStateSnapshot compendium) =>
        new(
            "Recall Registered Actor",
            compendium.Entries.Select(entry => new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SelectCompendiumEntry,
                    RecallLabel(entry),
                    SelectionIdentity: HostCommandSelectionIdentity.ForContent(entry.EntityId)))
                .Append(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back"))
                .ToArray());

    private string RecallLabel(CompendiumEntrySnapshot entry)
    {
        CompendiumRecallPricingDecision pricing = _compendium.GetRecallPricing(entry);
        return pricing.IsAvailable
            ? $"{entry.DisplayName} - {pricing.Cost} M"
            : $"{entry.DisplayName} - [Recall unavailable]";
    }

    private static TrainingAnnexCompendiumInteractionResult Unchanged(
        CompendiumStateSnapshot compendium,
        RuntimePartyStockSnapshot partyStock,
        RuntimeWalletSnapshot wallet,
        TrainingAnnexActorRoster roster,
        TrainingAnnexBattleKnowledgeState playerKnowledge) =>
        new(compendium, partyStock, wallet, roster, playerKnowledge, []);

    private static TrainingAnnexCompendiumEvidence Evidence(
        ContentId entityId,
        CompendiumActorRegistrationResult registration,
        RuntimePartyStockSnapshot partyStock,
        RuntimeWalletSnapshot wallet,
        FamiliarKnowledgeImportResult? imported = null) =>
        new(
            TrainingAnnexCompendiumAction.Register,
            entityId,
            registration.Applied,
            registration.Code,
            null,
            0,
            wallet.Balance,
            wallet.Balance,
            partyStock.DemonStock.Count,
            partyStock.DemonStock.Count,
            ImportedElementCount(imported, entityId),
            ImportedAilmentCount(imported, entityId),
            ImportedInstantDeathCount(imported, entityId),
            registration.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            null);

    private static TrainingAnnexCompendiumEvidence Evidence(
        ContentId entityId,
        CompendiumRecallTransactionResult recall,
        FamiliarKnowledgeImportResult? imported = null) =>
        new(
            TrainingAnnexCompendiumAction.Recall,
            entityId,
            recall.Applied,
            null,
            recall.Code,
            recall.Cost,
            recall.BeforeWallet.Balance,
            recall.AfterWallet.Balance,
            recall.BeforePartyStock.DemonStock.Count,
            recall.AfterPartyStock.DemonStock.Count,
            ImportedElementCount(imported, entityId),
            ImportedAilmentCount(imported, entityId),
            ImportedInstantDeathCount(imported, entityId),
            recall.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            null);

    private static int ImportedElementCount(FamiliarKnowledgeImportResult? result, ContentId entityId) =>
        result?.After.ElementalAffinities.Count(entry => entry.EntityId == entityId) ?? 0;

    private static int ImportedAilmentCount(FamiliarKnowledgeImportResult? result, ContentId entityId) =>
        result?.After.AilmentResistances.Count(entry => entry.EntityId == entityId) ?? 0;

    private static int ImportedInstantDeathCount(FamiliarKnowledgeImportResult? result, ContentId entityId) =>
        result?.After.InstantDeathResistances.Count(entry => entry.EntityId == entityId) ?? 0;
}
