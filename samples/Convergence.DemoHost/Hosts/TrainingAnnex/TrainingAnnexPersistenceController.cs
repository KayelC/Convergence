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
    private readonly IRuntimeRulesetBindingResolver _rulesetBindings;

    public TrainingAnnexPersistenceController(
        TrainingAnnexSaveSlotStore saveSlots,
        IHostEventSink<string> eventSink,
        IRuntimeRulesetBindingResolver rulesetBindings,
        IRosterCapacityPolicy? rosterCapacityPolicy = null)
    {
        _saveSlots = saveSlots ?? throw new ArgumentNullException(nameof(saveSlots));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _rulesetBindings = rulesetBindings ?? throw new ArgumentNullException(nameof(rulesetBindings));
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
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot shopStock,
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
            shopStock,
            session,
            encounterTriggerConsumed,
            preparedBattleStarted,
            preparedBattleOutcome,
            preparedBattleWinningTeamId,
            compendium);
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator(
                _rosterCapacityPolicy,
                rulesetBindings: _rulesetBindings,
                chargePolicies: ChargePolicyRegistry.CreateStandard())
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

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator(
                _rosterCapacityPolicy,
                rulesetBindings: _rulesetBindings,
                chargePolicies: ChargePolicyRegistry.CreateStandard())
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
        RuntimeCurrencyLedgerSnapshot wallet,
        RuntimeShopStockSnapshot shopStock,
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
            shopStock,
            session,
            hostContext,
            compendium);
    }

    private TrainingAnnexSessionRestoreResult RestoreTrainingAnnexSession(
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
        foreach (TrainingAnnexRuntimeActor current in currentRoster.AllActors)
        {
            if (!TryGetCompatibleSnapshot(current, actors, out _, out string? diagnostic))
            {
                diagnostics.Add(diagnostic ?? $"Saved actor '{current.Actor.State.InstanceId}' is incompatible.");
            }
        }
        if (diagnostics.Count > 0)
        {
            return new TrainingAnnexSessionRestoreResult(null, diagnostics);
        }

        HashSet<RuntimeInstanceId> dynamicActorIds = currentRoster.DynamicMembers
            .Select(member => member.Actor.State.InstanceId)
            .ToHashSet();
        HashSet<RuntimeInstanceId> ownedActorIds = OwnedActorReferences(currentPartyRoster)
            .Select(reference => reference.InstanceId)
            .ToHashSet();
        var knownActorIds = currentRoster.AllActors
            .Select(actor => actor.Actor.State.InstanceId)
            .ToHashSet();
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
            }
        }
        if (diagnostics.Count > 0)
        {
            return new TrainingAnnexSessionRestoreResult(null, diagnostics);
        }

        var profileResolver = new TrainingAnnexActorRestoreProfileResolver(
            currentRoster.Player.Actor.State.InstanceId,
            snapshot.Inventory,
            equipmentProfileResolver);
        RuntimeSessionRestoreResult aggregate = new RuntimeSessionRestoreService(
                new RuntimeSaveValidator(
                    _rosterCapacityPolicy,
                    rulesetBindings: _rulesetBindings,
                    chargePolicies: ChargePolicyRegistry.CreateStandard()),
                actorFactory,
                profileResolver,
                rulesetBindings: _rulesetBindings,
                chargePolicies: ChargePolicyRegistry.CreateStandard())
            .Restore(snapshot, catalog);
        if (!aggregate.IsSuccess)
        {
            diagnostics.AddRange(aggregate.Diagnostics.Select(diagnostic => diagnostic.Message));
            return new TrainingAnnexSessionRestoreResult(null, diagnostics);
        }

        IReadOnlyDictionary<RuntimeInstanceId, CatalogBattleActor> restoredActors =
            aggregate.RequireSession().ActorsByInstanceId;
        TrainingAnnexRuntimeActor Wrap(TrainingAnnexRuntimeActor current) =>
            new(current.Role, restoredActors[current.Actor.State.InstanceId]);

        TrainingAnnexRuntimeActor player = Wrap(currentRoster.Player);
        TrainingAnnexRuntimeActor[] supportMembers = currentRoster.SupportMembers
            .Select(Wrap)
            .ToArray();
        TrainingAnnexRuntimeActor[] ownedActors = currentRoster.AllActors
            .Where(member =>
                ownedActorIds.Contains(member.Actor.State.InstanceId) &&
                !dynamicActorIds.Contains(member.Actor.State.InstanceId))
            .Select(Wrap)
            .ToArray();
        TrainingAnnexRuntimeActor[] enemies = currentRoster.Enemies
            .Select(Wrap)
            .ToArray();
        var dynamicMembers = currentRoster.DynamicMembers
            .Where(member => actors.ContainsKey(member.Actor.State.InstanceId))
            .Select(Wrap)
            .ToList();
        foreach (RuntimeActorSnapshot savedActor in actors.Values.OrderBy(
                     actor => actor.Identity.InstanceId.ToString(),
                     StringComparer.Ordinal))
        {
            if (knownActorIds.Contains(savedActor.Identity.InstanceId))
            {
                continue;
            }

            bool fusionActor = savedActor.Identity.InstanceId.ToString()
                .StartsWith("fusion_", StringComparison.Ordinal);
            dynamicMembers.Add(new TrainingAnnexRuntimeActor(
                fusionActor ? "Fused Result" : "Compendium Recall",
                restoredActors[savedActor.Identity.InstanceId]));
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
                snapshot.CurrencyLedger,
                snapshot.ShopStock,
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

        ValidateTrainingAnnexPartyTeam("active party", partyRoster.ActiveParty, owner.Affiliation.TeamId, actors, diagnostics);
        ValidateTrainingAnnexPartyTeam("reserve party", partyRoster.ReserveMembers, owner.Affiliation.TeamId, actors, diagnostics);
        if (partyRoster.ActiveHostedEntity is RuntimeActorReferenceSnapshot activeHostedEntity)
        {
            ValidateTrainingAnnexPartyTeam(
                "active hosted entity",
                [activeHostedEntity],
                owner.Affiliation.TeamId,
                actors,
                diagnostics);
        }

        ValidateTrainingAnnexPartyTeam("Hosted Entity roster", partyRoster.HostedEntityRoster, owner.Affiliation.TeamId, actors, diagnostics);
        ValidateTrainingAnnexPartyTeam("Companion roster", partyRoster.CompanionRoster, owner.Affiliation.TeamId, actors, diagnostics);
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

            if (actor.Affiliation.TeamId != expectedTeamId)
            {
                diagnostics.Add(
                    $"Saved {listName} actor '{reference.InstanceId}' belongs to team '{actor.Affiliation.TeamId}', expected '{expectedTeamId}'.");
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

        if (snapshot.Affiliation.TeamId != expected.Affiliation.TeamId)
        {
            diagnostic =
                $"Saved actor '{snapshot.Identity.InstanceId}' has team '{snapshot.Affiliation.TeamId}', expected '{expected.Affiliation.TeamId}' for {current.Role}.";
            return false;
        }

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

internal sealed class TrainingAnnexActorRestoreProfileResolver(
    RuntimeInstanceId playerInstanceId,
    RuntimeInventorySnapshot inventory,
    IRuntimeEquipmentProfileResolver equipmentProfileResolver) : IRuntimeActorRestoreProfileResolver
{
    public RuntimeActorRestoreProfile Resolve(RuntimeActorRestoreProfileRequest request)
    {
        if (request.Actor.Identity.InstanceId != playerInstanceId)
        {
            return new RuntimeActorRestoreProfile(
                RuntimeStatSourceKind.Actor,
                MissingHostedEntityBehavior.UseActorBaseStats);
        }

        RuntimeEquipmentProfile equipment = equipmentProfileResolver.Resolve(
            inventory,
            request.Actor.Equipment,
            request.Catalog);
        if (equipment.Diagnostics.Count > 0)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                equipment.Diagnostics.Select(diagnostic => $"[{diagnostic.Code}] {diagnostic.Message}")));
        }

        return new RuntimeActorRestoreProfile(
            RuntimeStatSourceKind.ActiveHostedEntity,
            MissingHostedEntityBehavior.RejectStatResolution,
            equipment.StatModifiers,
            equipment.GrantedSkillIds);
    }
}

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
    RuntimeCurrencyLedgerSnapshot CurrencyLedger,
    RuntimeShopStockSnapshot ShopStock,
    RuntimeSessionProgressSnapshot SessionProgress,
    CompendiumStateSnapshot Compendium,
    TrainingAnnexBattleKnowledgeState PlayerBattleKnowledge,
    bool EncounterTriggerConsumed,
    bool PreparedBattleStarted,
    BattleEncounterOutcome? PreparedBattleOutcome,
    ContentId? PreparedBattleWinningTeamId,
    IReadOnlyList<ContentId> PreparedEncounterIds);
