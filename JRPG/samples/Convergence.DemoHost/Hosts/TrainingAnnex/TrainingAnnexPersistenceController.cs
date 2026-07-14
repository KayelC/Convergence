using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed class TrainingAnnexPersistenceController
{
    private static readonly ContentId AshlingTriggerConsumedHostKey = ContentId.Parse("ashling_trigger_consumed");
    private static readonly ContentId PreparedBattleStartedHostKey = ContentId.Parse("prepared_battle_started");
    private static readonly ContentId PreparedBattleOutcomeHostKey = ContentId.Parse("prepared_battle_outcome");
    private static readonly ContentId PreparedBattleWinningTeamHostKey = ContentId.Parse("prepared_battle_winning_team");

    private readonly TrainingAnnexSaveSlotStore _saveSlots;
    private readonly IHostEventSink<string> _eventSink;
    private readonly IStockCapacityPolicy _stockCapacityPolicy;

    public TrainingAnnexPersistenceController(
        TrainingAnnexSaveSlotStore saveSlots,
        IHostEventSink<string> eventSink,
        IStockCapacityPolicy? stockCapacityPolicy = null)
    {
        _saveSlots = saveSlots ?? throw new ArgumentNullException(nameof(saveSlots));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _stockCapacityPolicy = stockCapacityPolicy ?? NoLimitStockCapacityPolicy.Instance;
    }

    public async ValueTask<TrainingAnnexSaveActionResult> SaveCurrentSessionAsync(
        RuntimeSaveKind kind,
        IRuntimeSavePolicyService savePolicy,
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        RuntimePartyStockSnapshot partyStock,
        RuntimeFieldSnapshot field,
        RuntimeKnowledgeSnapshot knowledge,
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeSessionProgressSnapshot session,
        bool encounterTriggerConsumed,
        bool preparedBattleStarted,
        BattleEncounterOutcome? preparedBattleOutcome,
        ContentId? preparedBattleWinningTeamId,
        bool hasPendingHostAction,
        long sequence,
        CancellationToken cancellationToken,
        CompendiumStateSnapshot? compendium = null)
    {
        RuntimeSaveContextSnapshot context = CurrentSaveContext(field, hasPendingHostAction);
        RuntimeSavePolicyAssessment assessment = savePolicy.AssessSave(kind, context);
        if (!assessment.IsAllowed)
        {
            await PublishSavePolicyDiagnosticsAsync($"{KindLabel(kind)} save", assessment, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexSaveActionResult(false, assessment.Diagnostics.Count);
        }

        RuntimeSaveGameSnapshot snapshot = BuildCurrentSaveSnapshot(
            roster,
            partyStock,
            field,
            knowledge,
            inventory,
            wallet,
            session,
            encounterTriggerConsumed,
            preparedBattleStarted,
            preparedBattleOutcome,
            preparedBattleWinningTeamId,
            compendium);
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator(_stockCapacityPolicy)
            .Validate(snapshot, catalog);
        if (!validation.IsValid)
        {
            await PublishSaveValidationDiagnosticsAsync($"{KindLabel(kind)} save", validation, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexSaveActionResult(false, validation.Diagnostics.Count);
        }

        _saveSlots.Save(new RuntimeSaveRecord(kind, validation.RequireValidSnapshot(), context, sequence));
        await _eventSink.PublishAsync(
            $"{KindLabel(kind)} save created in {context.ContextId} (sequence {sequence}).",
            cancellationToken).ConfigureAwait(false);
        return new TrainingAnnexSaveActionResult(true, 0);
    }

    public async ValueTask<TrainingAnnexLoadActionResult> LoadCurrentSessionAsync(
        RuntimeSaveKind kind,
        IRuntimeSavePolicyService savePolicy,
        GameDataCatalog catalog,
        ICatalogBattleActorFactory actorFactory,
        TrainingAnnexActorRoster roster,
        RuntimePartyStockSnapshot partyStock,
        RuntimeFieldSnapshot field,
        bool hasPendingHostAction,
        CancellationToken cancellationToken)
    {
        RuntimeSaveContextSnapshot context = CurrentSaveContext(field, hasPendingHostAction);
        RuntimeSaveRecord? record = null;
        string? json = _saveSlots.GetRaw(kind);
        if (json is not null)
        {
            try
            {
                record = CleanSaveJsonCodec.DeserializeRecord(json);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await _eventSink.PublishAsync(
                    $"{KindLabel(kind)} load rejected: save JSON could not be read ({exception.Message}).",
                    cancellationToken).ConfigureAwait(false);
                return new TrainingAnnexLoadActionResult(null, 1, false);
            }
        }

        RuntimeSavePolicyAssessment assessment = savePolicy.AssessLoad(record, kind, context);
        if (!assessment.IsAllowed)
        {
            await PublishSavePolicyDiagnosticsAsync($"{KindLabel(kind)} load", assessment, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexLoadActionResult(null, assessment.Diagnostics.Count, false);
        }

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator(_stockCapacityPolicy)
            .Validate(record!.Snapshot, catalog);
        if (!validation.IsValid)
        {
            await PublishSaveValidationDiagnosticsAsync($"{KindLabel(kind)} load", validation, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexLoadActionResult(null, validation.Diagnostics.Count, false);
        }

        TrainingAnnexSessionRestoreResult restore =
            RestoreTrainingAnnexSession(validation.RequireValidSnapshot(), roster, partyStock, actorFactory);
        if (restore.Restored is null)
        {
            foreach (string diagnostic in restore.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"{KindLabel(kind)} load rejected: {diagnostic}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexLoadActionResult(null, restore.Diagnostics.Count, false);
        }

        bool consume = assessment.ConsumeAfterSuccessfulRestore;
        if (consume)
        {
            _saveSlots.Consume(kind);
        }

        await _eventSink.PublishAsync(
            $"{KindLabel(kind)} save restored from {record.Context.ContextId} (sequence {record.Sequence}).",
            cancellationToken).ConfigureAwait(false);
        if (consume)
        {
            await _eventSink.PublishAsync(
                "Suspend save consumed after successful restore.",
                cancellationToken).ConfigureAwait(false);
        }

        return new TrainingAnnexLoadActionResult(restore.Restored, 0, consume);
    }

    public static RuntimeSaveContextSnapshot CurrentSaveContext(
        RuntimeFieldSnapshot field,
        bool hasPendingHostAction) =>
        new(
            field.DungeonTraversal is null
                ? TrainingAnnexHostSupport.FieldMenuSaveContext
                : TrainingAnnexHostSupport.DungeonMenuSaveContext,
            hasPendingHostAction);

    public static RuntimeSaveGameSnapshot BuildCurrentSaveSnapshot(
        TrainingAnnexActorRoster roster,
        RuntimePartyStockSnapshot partyStock,
        RuntimeFieldSnapshot field,
        RuntimeKnowledgeSnapshot knowledge,
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeSessionProgressSnapshot session,
        bool encounterTriggerConsumed,
        bool preparedBattleStarted,
        BattleEncounterOutcome? preparedBattleOutcome,
        ContentId? preparedBattleWinningTeamId,
        CompendiumStateSnapshot? compendium = null)
    {
        var hostContext = new List<KeyValuePair<ContentId, string>>
        {
            new(AshlingTriggerConsumedHostKey, encounterTriggerConsumed.ToString()),
            new(PreparedBattleStartedHostKey, preparedBattleStarted.ToString())
        };
        if (preparedBattleOutcome is BattleEncounterOutcome outcome)
        {
            hostContext.Add(new KeyValuePair<ContentId, string>(
                PreparedBattleOutcomeHostKey,
                outcome.ToString()));
        }

        if (preparedBattleWinningTeamId is ContentId winningTeam)
        {
            hostContext.Add(new KeyValuePair<ContentId, string>(
                PreparedBattleWinningTeamHostKey,
                winningTeam.ToString()));
        }

        return TrainingAnnexHostSupport.BuildStartupSaveSnapshot(
            roster,
            partyStock,
            field,
            knowledge,
            inventory,
            wallet,
            session,
            hostContext,
            compendium);
    }

    private static TrainingAnnexSessionRestoreResult RestoreTrainingAnnexSession(
        RuntimeSaveGameSnapshot snapshot,
        TrainingAnnexActorRoster currentRoster,
        RuntimePartyStockSnapshot currentPartyStock,
        ICatalogBattleActorFactory actorFactory)
    {
        var diagnostics = new List<string>();
        ValidateTrainingAnnexField(snapshot.Field, diagnostics);

        Dictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors = snapshot.Actors
            .ToDictionary(actor => actor.Identity.InstanceId, actor => actor);
        ValidateTrainingAnnexParty(snapshot.PartyStock, currentPartyStock, actors, diagnostics);

        if (!TryRestoreActor(currentRoster.Player, actors, actorFactory, out TrainingAnnexRuntimeActor player, out string? playerDiagnostic))
        {
            diagnostics.Add(playerDiagnostic ?? "Saved player actor could not be restored.");
        }

        var supportMembers = new List<TrainingAnnexRuntimeActor>();
        foreach (TrainingAnnexRuntimeActor support in currentRoster.SupportMembers)
        {
            if (!TryRestoreActor(support, actors, actorFactory, out TrainingAnnexRuntimeActor restoredSupport, out string? supportDiagnostic))
            {
                diagnostics.Add(supportDiagnostic ?? $"Saved support actor '{support.Actor.State.InstanceId}' could not be restored.");
                continue;
            }

            supportMembers.Add(restoredSupport);
        }

        var stockMembers = new List<TrainingAnnexRuntimeActor>();
        foreach (TrainingAnnexRuntimeActor stock in currentRoster.StockMembers)
        {
            if (!TryRestoreActor(stock, actors, actorFactory, out TrainingAnnexRuntimeActor restoredStock, out string? stockDiagnostic))
            {
                diagnostics.Add(stockDiagnostic ?? $"Saved stock actor '{stock.Actor.State.InstanceId}' could not be restored.");
                continue;
            }

            stockMembers.Add(restoredStock);
        }

        var enemies = new List<TrainingAnnexRuntimeActor>();
        foreach (TrainingAnnexRuntimeActor enemy in currentRoster.Enemies)
        {
            if (!TryRestoreActor(enemy, actors, actorFactory, out TrainingAnnexRuntimeActor restoredEnemy, out string? enemyDiagnostic))
            {
                diagnostics.Add(enemyDiagnostic ?? $"Saved enemy actor '{enemy.Actor.State.InstanceId}' could not be restored.");
                continue;
            }

            enemies.Add(restoredEnemy);
        }

        var dynamicMembers = new List<TrainingAnnexRuntimeActor>();
        var knownActorIds = currentRoster.AllActors
            .Select(actor => actor.Actor.State.InstanceId)
            .ToHashSet();
        foreach (TrainingAnnexRuntimeActor dynamic in currentRoster.DynamicMembers)
        {
            if (!actors.ContainsKey(dynamic.Actor.State.InstanceId))
            {
                continue;
            }

            if (!TryRestoreActor(dynamic, actors, actorFactory, out TrainingAnnexRuntimeActor restoredDynamic, out string? dynamicDiagnostic))
            {
                diagnostics.Add(dynamicDiagnostic ?? $"Saved dynamic actor '{dynamic.Actor.State.InstanceId}' could not be restored.");
                continue;
            }

            dynamicMembers.Add(restoredDynamic);
        }

        foreach (RuntimeActorSnapshot savedActor in actors.Values.OrderBy(actor => actor.Identity.InstanceId.ToString(), StringComparer.Ordinal))
        {
            if (knownActorIds.Contains(savedActor.Identity.InstanceId))
            {
                continue;
            }

            bool fusionActor = savedActor.Identity.InstanceId.ToString()
                .StartsWith("fusion_", StringComparison.Ordinal);
            bool recalledActor = savedActor.Identity.InstanceId.ToString()
                .StartsWith("recall_", StringComparison.Ordinal);
            if (!fusionActor && !recalledActor)
            {
                diagnostics.Add($"Saved session contains unexpected actor '{savedActor.Identity.InstanceId}'.");
                continue;
            }

            CatalogBattleActorCreationResult restored = actorFactory.Restore(savedActor);
            if (!restored.IsSuccess)
            {
                string restoreDiagnostics = string.Join("; ", restored.Diagnostics.Select(item => item.Message));
                diagnostics.Add($"Saved dynamic actor '{savedActor.Identity.InstanceId}' could not be restored: {restoreDiagnostics}");
                continue;
            }

            dynamicMembers.Add(new TrainingAnnexRuntimeActor(
                fusionActor ? "Fused Result" : "Compendium Recall",
                restored.RequireActor()));
        }

        if (diagnostics.Count > 0)
        {
            return new TrainingAnnexSessionRestoreResult(null, diagnostics);
        }

        TrainingAnnexActorRoster roster = new(player, supportMembers, stockMembers, enemies, dynamicMembers);

        RuntimeFieldSnapshot field = snapshot.Field ??
            new RuntimeFieldSnapshot(new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.StagingArea));
        bool ashlingCleared = snapshot.Session.Flags.Contains(TrainingAnnexHostSupport.AshlingDrillClearedFlag);
        bool triggerConsumed = HostFlag(snapshot, AshlingTriggerConsumedHostKey) || ashlingCleared;
        bool battleStarted = HostFlag(snapshot, PreparedBattleStartedHostKey) || ashlingCleared;
        BattleEncounterOutcome? outcome = HostEnum<BattleEncounterOutcome>(
            snapshot,
            PreparedBattleOutcomeHostKey) ?? (ashlingCleared ? BattleEncounterOutcome.Victory : null);
        ContentId? winningTeam = HostContentId(snapshot, PreparedBattleWinningTeamHostKey) ??
            (ashlingCleared ? TrainingAnnexHostSupport.PlayerTeam : null);
        IReadOnlyList<ContentId> preparedEncounterIds = triggerConsumed
            ? [TrainingAnnexHostSupport.ReviewHallAshlingTrigger.EncounterId]
            : [];

        return new TrainingAnnexSessionRestoreResult(
            new TrainingAnnexRestoredSession(
                roster,
                snapshot.PartyStock,
                field,
                snapshot.Inventory,
                snapshot.Wallet,
                snapshot.Session,
                snapshot.Compendium,
                TrainingAnnexBattleKnowledgeState.FromSnapshot(snapshot.Knowledge),
                triggerConsumed,
                battleStarted,
                outcome,
                winningTeam,
                preparedEncounterIds),
            []);
    }

    private static void ValidateTrainingAnnexParty(
        RuntimePartyStockSnapshot partyStock,
        RuntimePartyStockSnapshot currentPartyStock,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<string> diagnostics)
    {
        if (partyStock.Owner.InstanceId != currentPartyStock.Owner.InstanceId)
        {
            diagnostics.Add(
                $"Saved party owner '{partyStock.Owner.InstanceId}' does not match expected owner '{currentPartyStock.Owner.InstanceId}'.");
        }

        if (partyStock.MaxActivePartySize != currentPartyStock.MaxActivePartySize)
        {
            diagnostics.Add(
                $"Saved active party limit '{partyStock.MaxActivePartySize}' does not match expected limit '{currentPartyStock.MaxActivePartySize}'.");
        }

        if (!actors.TryGetValue(partyStock.Owner.InstanceId, out RuntimeActorSnapshot? owner))
        {
            return;
        }

        ValidateTrainingAnnexPartyTeam("active party", partyStock.ActiveParty, owner.Ownership.TeamId, actors, diagnostics);
        ValidateTrainingAnnexPartyTeam("reserve party", partyStock.ReserveMembers, owner.Ownership.TeamId, actors, diagnostics);
        if (partyStock.ActiveForm is RuntimeActorReferenceSnapshot activeForm)
        {
            ValidateTrainingAnnexPartyTeam(
                "active form",
                [activeForm],
                owner.Ownership.TeamId,
                actors,
                diagnostics);
        }

        ValidateTrainingAnnexPartyTeam("Persona stock", partyStock.PersonaStock, owner.Ownership.TeamId, actors, diagnostics);
        ValidateTrainingAnnexPartyTeam("Demon stock", partyStock.DemonStock, owner.Ownership.TeamId, actors, diagnostics);
    }

    private static void ValidateTrainingAnnexPartyTeam(
        string listName,
        IEnumerable<RuntimeActorReferenceSnapshot> references,
        ContentId expectedTeamId,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<string> diagnostics)
    {
        foreach (RuntimeActorReferenceSnapshot reference in references)
        {
            if (!actors.TryGetValue(reference.InstanceId, out RuntimeActorSnapshot? actor))
            {
                continue;
            }

            if (actor.Ownership.TeamId != expectedTeamId)
            {
                diagnostics.Add(
                    $"Saved {listName} actor '{reference.InstanceId}' belongs to team '{actor.Ownership.TeamId}', expected '{expectedTeamId}'.");
            }
        }
    }

    private static void ValidateTrainingAnnexField(
        RuntimeFieldSnapshot? field,
        ICollection<string> diagnostics)
    {
        if (field is null)
        {
            return;
        }

        if (field.Navigation.CurrentLocationId != TrainingAnnexHostSupport.StagingArea &&
            field.Navigation.CurrentLocationId != TrainingAnnexHostSupport.TrainingAnnexEntrance)
        {
            diagnostics.Add(
                $"Saved location '{field.Navigation.CurrentLocationId}' is not a Training Annex play location.");
        }

        RuntimeDungeonTraversalSnapshot? dungeon = field.DungeonTraversal;
        if (dungeon is null)
        {
            return;
        }

        if (dungeon.DungeonId != TrainingAnnexHostSupport.TrainingAnnexDungeon)
        {
            diagnostics.Add(
                $"Saved dungeon '{dungeon.DungeonId}' is not the Training Annex dungeon.");
        }

        ContentId[] allowedNodes =
        [
            TrainingAnnexHostSupport.TrainingAnnexEntrance,
            TrainingAnnexHostSupport.ReviewHall,
            TrainingAnnexHostSupport.ReviewAlcove
        ];
        if (!allowedNodes.Contains(dungeon.CurrentNodeId))
        {
            diagnostics.Add(
                $"Saved dungeon node '{dungeon.CurrentNodeId}' is not recognized by the Training Annex host.");
        }

        foreach (ContentId nodeId in dungeon.VisitedNodeIds)
        {
            if (!allowedNodes.Contains(nodeId))
            {
                diagnostics.Add(
                    $"Saved visited dungeon node '{nodeId}' is not recognized by the Training Annex host.");
            }
        }

        foreach (ContentId checkpointId in dungeon.UnlockedCheckpointIds)
        {
            if (checkpointId != TrainingAnnexHostSupport.ReviewCheckpoint)
            {
                diagnostics.Add(
                    $"Saved checkpoint '{checkpointId}' is not recognized by the Training Annex host.");
            }
        }

        foreach (ContentId bossId in dungeon.DefeatedBossIds)
        {
            diagnostics.Add(
                $"Saved defeated boss '{bossId}' is not recognized by the Training Annex host.");
        }
    }

    private static bool TryRestoreActor(
        TrainingAnnexRuntimeActor current,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICatalogBattleActorFactory actorFactory,
        out TrainingAnnexRuntimeActor restored,
        out string? diagnostic)
    {
        if (!actors.TryGetValue(current.Actor.State.InstanceId, out RuntimeActorSnapshot? snapshot))
        {
            restored = current;
            diagnostic = $"Saved session has no actor '{current.Actor.State.InstanceId}'.";
            return false;
        }

        RuntimeActorSnapshot expected = current.Actor.State.ToSnapshot();
        if (snapshot.Identity.EntityDefinitionId != expected.Identity.EntityDefinitionId)
        {
            restored = current;
            diagnostic =
                $"Saved actor '{snapshot.Identity.InstanceId}' has entity '{snapshot.Identity.EntityDefinitionId}', expected '{expected.Identity.EntityDefinitionId}' for {current.Role}.";
            return false;
        }

        if (snapshot.Identity.ActorKindId != expected.Identity.ActorKindId)
        {
            restored = current;
            diagnostic =
                $"Saved actor '{snapshot.Identity.InstanceId}' has kind '{snapshot.Identity.ActorKindId}', expected '{expected.Identity.ActorKindId}' for {current.Role}.";
            return false;
        }

        if (snapshot.Ownership.TeamId != expected.Ownership.TeamId)
        {
            restored = current;
            diagnostic =
                $"Saved actor '{snapshot.Identity.InstanceId}' has team '{snapshot.Ownership.TeamId}', expected '{expected.Ownership.TeamId}' for {current.Role}.";
            return false;
        }

        CatalogBattleActorCreationResult result = actorFactory.Restore(snapshot);
        if (!result.IsSuccess)
        {
            restored = current;
            diagnostic = string.Join("; ", result.Diagnostics.Select(item => item.Message));
            return false;
        }

        restored = new TrainingAnnexRuntimeActor(current.Role, result.RequireActor());
        diagnostic = null;
        return true;
    }

    private async ValueTask PublishSavePolicyDiagnosticsAsync(
        string actionLabel,
        RuntimeSavePolicyAssessment assessment,
        CancellationToken cancellationToken)
    {
        foreach (RuntimeSavePolicyDiagnostic diagnostic in assessment.Diagnostics)
        {
            await _eventSink.PublishAsync(
                $"{actionLabel} rejected [{diagnostic.Code}]: {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask PublishSaveValidationDiagnosticsAsync(
        string actionLabel,
        RuntimeSaveValidationResult validation,
        CancellationToken cancellationToken)
    {
        foreach (RuntimeSaveValidationDiagnostic diagnostic in validation.Diagnostics)
        {
            await _eventSink.PublishAsync(
                $"{actionLabel} rejected [{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool HostFlag(RuntimeSaveGameSnapshot snapshot, ContentId key) =>
        snapshot.HostContext.TryGetValue(key, out string? value) &&
        bool.TryParse(value, out bool result) &&
        result;

    private static TEnum? HostEnum<TEnum>(RuntimeSaveGameSnapshot snapshot, ContentId key)
        where TEnum : struct
    {
        return snapshot.HostContext.TryGetValue(key, out string? value) &&
            Enum.TryParse(value, out TEnum result)
                ? result
                : null;
    }

    private static ContentId? HostContentId(RuntimeSaveGameSnapshot snapshot, ContentId key) =>
        snapshot.HostContext.TryGetValue(key, out string? value) &&
        ContentId.TryParse(value, out ContentId contentId)
            ? contentId
            : null;

    private static string KindLabel(RuntimeSaveKind kind) =>
        kind == RuntimeSaveKind.Manual ? "Manual" : "Suspend";
}

internal sealed record TrainingAnnexSaveActionResult(
    bool Applied,
    int DiagnosticCount);

internal sealed record TrainingAnnexLoadActionResult(
    TrainingAnnexRestoredSession? Restored,
    int DiagnosticCount,
    bool ConsumedRecord);

internal sealed record TrainingAnnexSessionRestoreResult
{
    public TrainingAnnexSessionRestoreResult(
        TrainingAnnexRestoredSession? restored,
        IEnumerable<string>? diagnostics = null)
    {
        Restored = restored;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public TrainingAnnexRestoredSession? Restored { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}

internal sealed record TrainingAnnexRestoredSession(
    TrainingAnnexActorRoster Roster,
    RuntimePartyStockSnapshot PartyStock,
    RuntimeFieldSnapshot Field,
    RuntimeInventorySnapshot Inventory,
    RuntimeWalletSnapshot Wallet,
    RuntimeSessionProgressSnapshot SessionProgress,
    CompendiumStateSnapshot Compendium,
    TrainingAnnexBattleKnowledgeState PlayerBattleKnowledge,
    bool EncounterTriggerConsumed,
    bool PreparedBattleStarted,
    BattleEncounterOutcome? PreparedBattleOutcome,
    ContentId? PreparedBattleWinningTeamId,
    IReadOnlyList<ContentId> PreparedEncounterIds);
