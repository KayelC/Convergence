using Convergence.Content;
using Convergence.Catalog;
using Convergence.Hosting;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Fusion;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed class TrainingAnnexPersistenceController
{
    private static readonly ContentId AshlingTriggerConsumedHostKey = ContentId.Parse("ashling_trigger_consumed");
    private static readonly ContentId PreparedBattleStartedHostKey = ContentId.Parse("prepared_battle_started");
    private static readonly ContentId PreparedBattleOutcomeHostKey = ContentId.Parse("prepared_battle_outcome");
    private static readonly ContentId PreparedBattleWinningTeamHostKey = ContentId.Parse("prepared_battle_winning_team");

    private readonly TrainingAnnexSaveSlotStore _saveSlots;
    private readonly IHostEventSink<string> _eventSink;
    private readonly IRosterCapacityPolicy _rosterCapacityPolicy;

    public TrainingAnnexPersistenceController(
        TrainingAnnexSaveSlotStore saveSlots,
        IHostEventSink<string> eventSink,
        IRosterCapacityPolicy? rosterCapacityPolicy = null)
    {
        _saveSlots = saveSlots ?? throw new ArgumentNullException(nameof(saveSlots));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _rosterCapacityPolicy = rosterCapacityPolicy ?? NoLimitRosterCapacityPolicy.Instance;
    }

    public async ValueTask<TrainingAnnexSaveActionResult> SaveCurrentSessionAsync(
        RuntimeSaveKind kind,
        IRuntimeSavePolicyService savePolicy,
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
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
            partyRoster,
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
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator(_rosterCapacityPolicy)
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
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
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

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator(_rosterCapacityPolicy)
            .Validate(record!.Snapshot, catalog);
        if (!validation.IsValid)
        {
            await PublishSaveValidationDiagnosticsAsync($"{KindLabel(kind)} load", validation, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexLoadActionResult(null, validation.Diagnostics.Count, false);
        }

        TrainingAnnexSessionRestoreResult restore =
            RestoreTrainingAnnexSession(
                validation.RequireValidSnapshot(),
                roster,
                partyRoster,
                catalog,
                actorFactory,
                equipmentProfileResolver);
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
        RuntimePartyRosterSnapshot partyRoster,
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
            partyRoster,
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
        RuntimePartyRosterSnapshot currentPartyRoster,
        GameDataCatalog catalog,
        ICatalogBattleActorFactory actorFactory,
        IRuntimeEquipmentProfileResolver equipmentProfileResolver)
    {
        var diagnostics = new List<string>();
        ValidateTrainingAnnexField(snapshot.Field, diagnostics);

        Dictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors = snapshot.Actors
            .ToDictionary(actor => actor.Identity.InstanceId, actor => actor);
        ValidateTrainingAnnexParty(snapshot.PartyRoster, currentPartyRoster, actors, diagnostics);

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

        HashSet<RuntimeInstanceId> dynamicActorIds = currentRoster.DynamicMembers
            .Select(member => member.Actor.State.InstanceId)
            .ToHashSet();
        HashSet<RuntimeInstanceId> ownedActorIds = OwnedActorReferences(currentPartyRoster)
            .Select(reference => reference.InstanceId)
            .ToHashSet();
        var ownedActors = new List<TrainingAnnexRuntimeActor>();
        foreach (TrainingAnnexRuntimeActor ownedActor in currentRoster.AllActors.Where(member =>
                     ownedActorIds.Contains(member.Actor.State.InstanceId) &&
                     !dynamicActorIds.Contains(member.Actor.State.InstanceId)))
        {
            if (!TryRestoreActor(
                    ownedActor,
                    actors,
                    actorFactory,
                    out TrainingAnnexRuntimeActor restoredOwnedActor,
                    out string? ownedActorDiagnostic))
            {
                diagnostics.Add(
                    ownedActorDiagnostic ??
                    $"Saved owned actor '{ownedActor.Actor.State.InstanceId}' could not be restored.");
                continue;
            }

            ownedActors.Add(restoredOwnedActor);
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

            CatalogBattleActorCreationResult restored = actorFactory.Restore(
                ActorStatRestoreRequest(savedActor));
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

        TrainingAnnexRuntimeActor player = currentRoster.Player;
        if (!TryGetCompatibleSnapshot(
                currentRoster.Player,
                actors,
                out RuntimeActorSnapshot playerSnapshot,
                out string? playerDiagnostic))
        {
            diagnostics.Add(playerDiagnostic ?? "Saved player actor could not be restored.");
        }
        else
        {
            RuntimeEquipmentProfile equipmentProfile = equipmentProfileResolver.Resolve(
                playerSnapshot.Equipment,
                catalog);
            foreach (RuntimeEquipmentProfileDiagnostic diagnostic in equipmentProfile.Diagnostics)
            {
                diagnostics.Add($"Player equipment [{diagnostic.Code}]: {diagnostic.Message}");
            }

            RuntimeActorReferenceSnapshot? activeReference = snapshot.PartyRoster.ActiveHostedEntity;
            RuntimeActorState? activeHostedEntity = activeReference is null
                ? null
                : supportMembers
                    .Concat(ownedActors)
                    .Concat(enemies)
                    .Concat(dynamicMembers)
                    .Select(member => member.Actor.State)
                    .FirstOrDefault(state =>
                        state.InstanceId == activeReference.InstanceId &&
                        state.EntityId == activeReference.EntityDefinitionId);

            if (equipmentProfile.Diagnostics.Count == 0 &&
                !TryRestoreValidatedActor(
                    currentRoster.Player,
                    playerSnapshot,
                    actorFactory,
                    new CatalogBattleActorRestoreRequest(
                        playerSnapshot,
                        RuntimeStatSourceKind.ActiveHostedEntity,
                        MissingHostedEntityBehavior.RejectStatResolution,
                        activeHostedEntity,
                        equipmentProfile.StatModifiers),
                    out player,
                    out playerDiagnostic))
            {
                diagnostics.Add(playerDiagnostic ?? "Saved player actor could not be restored.");
            }
        }

        if (diagnostics.Count > 0)
        {
            return new TrainingAnnexSessionRestoreResult(null, diagnostics);
        }

        TrainingAnnexActorRoster roster = new(player, supportMembers, ownedActors, enemies, dynamicMembers);

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
                snapshot.PartyRoster,
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

    private static IEnumerable<RuntimeActorReferenceSnapshot> OwnedActorReferences(
        RuntimePartyRosterSnapshot partyRoster)
    {
        if (partyRoster.ActiveHostedEntity is not null)
        {
            yield return partyRoster.ActiveHostedEntity;
        }

        foreach (RuntimeActorReferenceSnapshot actor in partyRoster.HostedEntityRoster)
        {
            yield return actor;
        }

        foreach (RuntimeActorReferenceSnapshot actor in partyRoster.CompanionRoster)
        {
            yield return actor;
        }
    }

    private static void ValidateTrainingAnnexParty(
        RuntimePartyRosterSnapshot partyRoster,
        RuntimePartyRosterSnapshot currentPartyRoster,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        ICollection<string> diagnostics)
    {
        if (partyRoster.Owner.InstanceId != currentPartyRoster.Owner.InstanceId)
        {
            diagnostics.Add(
                $"Saved party owner '{partyRoster.Owner.InstanceId}' does not match expected owner '{currentPartyRoster.Owner.InstanceId}'.");
        }

        if (partyRoster.MaxActivePartySize != currentPartyRoster.MaxActivePartySize)
        {
            diagnostics.Add(
                $"Saved active party limit '{partyRoster.MaxActivePartySize}' does not match expected limit '{currentPartyRoster.MaxActivePartySize}'.");
        }

        if (!actors.TryGetValue(partyRoster.Owner.InstanceId, out RuntimeActorSnapshot? owner))
        {
            return;
        }

        ValidateTrainingAnnexPartyTeam("active party", partyRoster.ActiveParty, owner.Ownership.TeamId, actors, diagnostics);
        ValidateTrainingAnnexPartyTeam("reserve party", partyRoster.ReserveMembers, owner.Ownership.TeamId, actors, diagnostics);
        if (partyRoster.ActiveHostedEntity is RuntimeActorReferenceSnapshot activeHostedEntity)
        {
            ValidateTrainingAnnexPartyTeam(
                "active form",
                [activeHostedEntity],
                owner.Ownership.TeamId,
                actors,
                diagnostics);
        }

        ValidateTrainingAnnexPartyTeam("HostedEntity roster", partyRoster.HostedEntityRoster, owner.Ownership.TeamId, actors, diagnostics);
        ValidateTrainingAnnexPartyTeam("Companion roster", partyRoster.CompanionRoster, owner.Ownership.TeamId, actors, diagnostics);
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
        if (!TryGetCompatibleSnapshot(current, actors, out RuntimeActorSnapshot snapshot, out diagnostic))
        {
            restored = current;
            return false;
        }

        return TryRestoreValidatedActor(
            current,
            snapshot,
            actorFactory,
            ActorStatRestoreRequest(snapshot),
            out restored,
            out diagnostic);
    }

    private static bool TryGetCompatibleSnapshot(
        TrainingAnnexRuntimeActor current,
        IReadOnlyDictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors,
        out RuntimeActorSnapshot snapshot,
        out string? diagnostic)
    {
        if (!actors.TryGetValue(current.Actor.State.InstanceId, out RuntimeActorSnapshot? savedSnapshot))
        {
            snapshot = current.Actor.State.ToSnapshot();
            diagnostic = $"Saved session has no actor '{current.Actor.State.InstanceId}'.";
            return false;
        }

        snapshot = savedSnapshot;
        RuntimeActorSnapshot expected = current.Actor.State.ToSnapshot();
        if (snapshot.Identity.EntityDefinitionId != expected.Identity.EntityDefinitionId)
        {
            diagnostic =
                $"Saved actor '{snapshot.Identity.InstanceId}' has entity '{snapshot.Identity.EntityDefinitionId}', expected '{expected.Identity.EntityDefinitionId}' for {current.Role}.";
            return false;
        }

        if (snapshot.Identity.ActorKindId != expected.Identity.ActorKindId)
        {
            diagnostic =
                $"Saved actor '{snapshot.Identity.InstanceId}' has kind '{snapshot.Identity.ActorKindId}', expected '{expected.Identity.ActorKindId}' for {current.Role}.";
            return false;
        }

        if (snapshot.Ownership.TeamId != expected.Ownership.TeamId)
        {
            diagnostic =
                $"Saved actor '{snapshot.Identity.InstanceId}' has team '{snapshot.Ownership.TeamId}', expected '{expected.Ownership.TeamId}' for {current.Role}.";
            return false;
        }

        diagnostic = null;
        return true;
    }

    private static bool TryRestoreValidatedActor(
        TrainingAnnexRuntimeActor current,
        RuntimeActorSnapshot snapshot,
        ICatalogBattleActorFactory actorFactory,
        CatalogBattleActorRestoreRequest request,
        out TrainingAnnexRuntimeActor restored,
        out string? diagnostic)
    {
        CatalogBattleActorCreationResult result = actorFactory.Restore(request);
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

    private static CatalogBattleActorRestoreRequest ActorStatRestoreRequest(
        RuntimeActorSnapshot snapshot) =>
        new(
            snapshot,
            RuntimeStatSourceKind.Actor,
            MissingHostedEntityBehavior.UseActorBaseStats);

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
    RuntimePartyRosterSnapshot PartyRoster,
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
