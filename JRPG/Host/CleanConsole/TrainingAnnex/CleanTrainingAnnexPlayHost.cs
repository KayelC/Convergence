using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal enum CleanTrainingAnnexPlayCommand
{
    InspectSession,
    InspectActor,
    ResolveStats,
    RecalculateResources,
    ApplyVictoryExperience,
    ValidateStartupSnapshot,
    EnterTrainingAnnex,
    ReturnToStagingArea,
    EnterReviewHall,
    EnterReviewAlcove,
    ReturnToReviewHall,
    ReturnToAnnexEntrance,
    InspectTrainingBarrier,
    UnlockReviewCheckpoint,
    ActivateAshlingEncounterTrigger,
    StartPreparedBattle,
    OpenInventory,
    OpenFieldSkills,
    OpenSaveLoad,
    ManualSave,
    ManualLoad,
    SuspendSave,
    SuspendLoad,
    BattleAttack,
    OpenBattleSkills,
    OpenBattleItems,
    BattleGuard,
    BattlePass,
    BattleAnalyze,
    SelectBattleSkill,
    SelectBattleItem,
    SelectBattleTarget,
    UseAnnexTonic,
    UseMend,
    TargetPlayer,
    Back,
    Exit
}

internal sealed record CleanTrainingAnnexPlaySummary(
    IReadOnlyList<string> RequestedManifestPaths,
    IReadOnlyList<string> RequestedDocumentPaths,
    ContentId PlayerEntityId,
    int PlayerLevel,
    int ActorCount,
    int EnemyActorCount,
    IReadOnlyList<ContentId> ActorEntityIds,
    IReadOnlyList<RuntimeInstanceId> ActorInstanceIds,
    IReadOnlyList<RuntimeResourceSnapshot> PlayerResources,
    RuntimeProgressionSnapshot PlayerProgression,
    IReadOnlyList<StatResolutionResult> PlayerResolvedStats,
    int ActiveSkillCount,
    int PassiveSkillCount,
    bool StatResolutionPreviewed,
    bool ResourceRecalculationApplied,
    bool GrowthApplied,
    int LevelUpCount,
    bool StartupSnapshotValidated,
    int StartupSnapshotDiagnosticCount,
    ContentId FinalLocationId,
    IReadOnlyList<ContentId> LocationHistory,
    ContentId? FinalDungeonNodeId,
    IReadOnlyList<ContentId> VisitedDungeonNodeIds,
    IReadOnlyList<ContentId> UnlockedCheckpointIds,
    bool BarrierRejected,
    bool EncounterTriggerConsumed,
    IReadOnlyList<ContentId> PreparedEncounterIds,
    IReadOnlyList<RuntimeInstanceId> PreparedEncounterActorInstanceIds,
    bool PreparedBattleStarted,
    BattleEncounterOutcome? PreparedBattleOutcome,
    ContentId? PreparedBattleWinningTeamId,
    IReadOnlyList<ContentId> ExecutedBattleActionIds,
    IReadOnlyList<TrainingAnnexTypedEffectEvidence> ExecutedBattleEffectEvidence,
    IReadOnlyList<TrainingAnnexCombatResolutionEvidence> CombatResolutionEvidence,
    IReadOnlyList<TrainingAnnexPressTurnEvidence> PressTurnEvidence,
    IReadOnlyList<TrainingAnnexLifecycleEvidence> LifecycleEvidence,
    IReadOnlyList<TrainingAnnexAiDecisionEvidence> AiDecisionEvidence,
    IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> BattleKnowledgeEvidence,
    IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> EncounterAiKnowledgeEvidence,
    RuntimeKnowledgeSnapshot BattleKnowledge,
    RuntimeKnowledgeSnapshot EncounterAiKnowledge,
    BattleRewardResult? PreparedBattleRewardPreview,
    BattleRewardResult? AppliedBattleReward,
    int AppliedBattleRewardLevelUpCount,
    RuntimeWalletSnapshot Wallet,
    RuntimeSessionProgressSnapshot SessionProgress,
    int ManualSaveCount,
    int ManualLoadCount,
    int SuspendSaveCount,
    int SuspendLoadCount,
    bool SuspendSaveConsumed,
    bool HasManualSave,
    bool HasSuspendSave,
    int SaveDiagnosticCount,
    int CancelledBattleCommandSelections,
    int PreparedBattleEventCount,
    RuntimeInventorySnapshot Inventory,
    IReadOnlyList<ContentId> ExecutedFieldActionIds,
    int CancelledFieldTargetSelections,
    IReadOnlyList<CleanTrainingAnnexPlayCommand> Commands);

internal sealed class CleanTrainingAnnexPlayHost
{
    private static readonly ContentId AshlingDrillClearedFlag = ContentId.Parse("ashling_drill_cleared");
    private static readonly ContentId AshlingTriggerConsumedHostKey = ContentId.Parse("ashling_trigger_consumed");
    private static readonly ContentId PreparedBattleStartedHostKey = ContentId.Parse("prepared_battle_started");
    private static readonly ContentId PreparedBattleOutcomeHostKey = ContentId.Parse("prepared_battle_outcome");
    private static readonly ContentId PreparedBattleWinningTeamHostKey = ContentId.Parse("prepared_battle_winning_team");

    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;
    private readonly IRandomSource _randomSource;
    private readonly TrainingAnnexSaveSlotStore _saveSlots;
    private readonly RuntimeInventorySnapshot? _initialInventory;

    public CleanTrainingAnnexPlayHost(IGameIO io, string? contentRoot = null)
        : this(
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Data", "Jsons")),
            new GameIoEventSink(io),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io),
            new TrainingAnnexMinimumRandomSource())
    {
    }

    internal CleanTrainingAnnexPlayHost(
        IContentPackTextSource contentSource,
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        IRandomSource? randomSource = null,
        TrainingAnnexSaveSlotStore? saveSlots = null,
        RuntimeInventorySnapshot? initialInventory = null)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
        _randomSource = randomSource ?? new TrainingAnnexMinimumRandomSource();
        _saveSlots = saveSlots ?? new TrainingAnnexSaveSlotStore();
        _initialInventory = initialInventory;
    }

    internal CleanTrainingAnnexPlaySummary? LastSummary { get; private set; }

    public int Run() => RunAsync().GetAwaiter().GetResult();

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        LastSummary = null;
        ContentPackTextRequest request = TrainingAnnexHostSupport.CreateContentRequest();
        ContentPackTextBundle bundle;
        try
        {
            bundle = await _contentSource.ReadAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _eventSink.PublishAsync(
                $"Content read failed for {request.ManifestPath}: {exception.Message}",
                cancellationToken).ConfigureAwait(false);
            return 2;
        }

        CatalogLoadResult load = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(TrainingAnnexHostSupport.BuildRegistrations(), [bundle]));
        if (!load.IsSuccess || load.Catalog is null)
        {
            foreach (CatalogLoadDiagnostic diagnostic in load.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return 3;
        }

        GameDataCatalog catalog = load.Catalog;
        var rulesetResolver = new RuntimeRulesetBindingResolver();
        RulesetBindingResult<ProductionCombatRuleset> combatBinding =
            rulesetResolver.BindProductionCombatRuleset(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_damage"),
                _randomSource);
        if (!combatBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("damage", combatBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        ProductionCombatRuleset combatRuleset = combatBinding.RequireService();
        RulesetBindingResult<IBattleRewardService> rewardBinding =
            rulesetResolver.BindBattleRewardService(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_reward"),
                combatRuleset);
        if (!rewardBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("reward", rewardBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        IBattleRewardService rewardService = rewardBinding.RequireService();
        RulesetBindingResult<Func<PressTurnEngine>> pressTurnBinding =
            rulesetResolver.BindPressTurnFactory(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_press_turn"));
        if (!pressTurnBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("press_turn", pressTurnBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        Func<PressTurnEngine> pressTurnFactory = pressTurnBinding.RequireService();
        BattleExecutionServices executionServices =
            TrainingAnnexHostSupport.CreateExecutionServices(catalog, combatRuleset);
        TrainingAnnexActorRosterResult rosterResult = TrainingAnnexHostSupport.CreateActorRoster(catalog);
        if (!rosterResult.IsSuccess)
        {
            foreach (string diagnostic in rosterResult.Diagnostics)
            {
                await _eventSink.PublishAsync(diagnostic, cancellationToken).ConfigureAwait(false);
            }

            return 4;
        }

        TrainingAnnexActorRoster roster = rosterResult.RequireRoster();
        CatalogBattleActor player = roster.Player.Actor;
        GrowthRulesetServices growthServices = rulesetResolver
            .BindGrowthServices(catalog, TrainingAnnexHostSupport.Qualified("standard_growth"))
            .RequireService();
        IStatResolutionPolicy statPolicy = rulesetResolver
            .BindStatResolutionPolicy(catalog, TrainingAnnexHostSupport.Qualified("standard_stat"))
            .RequireService();
        var navigation = new RuntimeNavigationService(new TrainingAnnexNavigationPolicy());
        var dungeonTraversal = new RuntimeDungeonTraversalService(new TrainingAnnexDungeonPolicy());
        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new TrainingAnnexResourceInitializationPolicy(growthServices.ResourceGrowthPolicy));
        var encounterPreparation = new CatalogEncounterPreparationService(
            new CatalogEncounterStartPlanner(catalog),
            actorFactory);
        var fieldActions = new TrainingAnnexFieldActionAdapter(
            executionServices);
        var economy = new EconomyTransactionService();
        var inventory = new TrainingAnnexItemActionInventory(
            _initialInventory ?? new RuntimeInventorySnapshot(
                [KeyValuePair.Create(TrainingAnnexHostSupport.AnnexTonic, 1)]));
        var savePolicy = new RuntimeSavePolicyService(new RuntimeSavePolicyOptions(
            manualAllowedContextIds:
            [
                TrainingAnnexHostSupport.FieldMenuSaveContext,
                TrainingAnnexHostSupport.DungeonMenuSaveContext
            ],
            suspendAllowedContextIds:
            [
                TrainingAnnexHostSupport.FieldMenuSaveContext,
                TrainingAnnexHostSupport.DungeonMenuSaveContext
            ]));
        RuntimeWalletSnapshot wallet = new(0);
        RuntimeSessionProgressSnapshot sessionProgress = new();
        RuntimeFieldSnapshot field = new(
            new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.StagingArea));
        var locationHistory = new List<ContentId> { field.Navigation.CurrentLocationId };
        var commands = new List<CleanTrainingAnnexPlayCommand>();
        IReadOnlyList<StatResolutionResult> statPreview = [];
        bool statResolutionPreviewed = false;
        bool resourceRecalculationApplied = false;
        bool growthApplied = false;
        int levelUpCount = 0;
        bool snapshotValidated = false;
        int snapshotDiagnosticCount = -1;
        bool barrierRejected = false;
        bool encounterTriggerConsumed = false;
        PreparedEncounter? preparedEncounter = null;
        bool preparedBattleStarted = false;
        BattleEncounterOutcome? preparedBattleOutcome = null;
        ContentId? preparedBattleWinningTeamId = null;
        var preparedEncounterIds = new List<ContentId>();
        var preparedEncounterActorInstanceIds = new List<RuntimeInstanceId>();
        var executedBattleActionIds = new List<ContentId>();
        var executedBattleEffectEvidence = new List<TrainingAnnexTypedEffectEvidence>();
        var combatResolutionEvidence = new List<TrainingAnnexCombatResolutionEvidence>();
        var pressTurnEvidence = new List<TrainingAnnexPressTurnEvidence>();
        var lifecycleEvidence = new List<TrainingAnnexLifecycleEvidence>();
        var aiDecisionEvidence = new List<TrainingAnnexAiDecisionEvidence>();
        var playerBattleKnowledge = new TrainingAnnexBattleKnowledgeState();
        var battleKnowledgeEvidence = new List<TrainingAnnexBattleKnowledgeEvidence>();
        var encounterAiKnowledgeEvidence = new List<TrainingAnnexBattleKnowledgeEvidence>();
        var lastEncounterAiKnowledge = new RuntimeKnowledgeSnapshot();
        BattleRewardResult? preparedBattleRewardPreview = null;
        BattleRewardResult? appliedBattleReward = null;
        int appliedBattleRewardLevelUpCount = 0;
        long saveSequence = 0;
        int manualSaveCount = 0;
        int manualLoadCount = 0;
        int suspendSaveCount = 0;
        int suspendLoadCount = 0;
        bool suspendSaveConsumed = false;
        int saveDiagnosticCount = 0;
        int cancelledBattleCommandSelections = 0;
        int preparedBattleEventCount = 0;
        var executedFieldActionIds = new List<ContentId>();
        int cancelledFieldTargetSelections = 0;

        await _eventSink.PublishAsync("Clean Training Annex session booted.", cancellationToken)
            .ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Loaded {TrainingAnnexHostSupport.PackId} without legacy Database startup.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Hydrated {player.Entity.DisplayName} at level {player.Entity.BaseLevel}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Hydrated clean actor roster with {roster.AllActors.Count} actor(s): {roster.Enemies.Count} enemy model(s).",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync("Field location: Staging Area.", cancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> result =
                await _commandSource.ReadAsync(
                    CreateMenu(field, encounterTriggerConsumed, preparedBattleStarted),
                    cancellationToken).ConfigureAwait(false);
            if (!result.IsSelected || result.Command == CleanTrainingAnnexPlayCommand.Exit)
            {
                commands.Add(CleanTrainingAnnexPlayCommand.Exit);
                LastSummary = CreateSummary(
                    request,
                    roster,
                    statPreview,
                    statResolutionPreviewed,
                    resourceRecalculationApplied,
                    growthApplied,
                    levelUpCount,
                    snapshotValidated,
                    snapshotDiagnosticCount,
                    field,
                    locationHistory,
                    barrierRejected,
                    encounterTriggerConsumed,
                    preparedEncounterIds,
                    preparedEncounterActorInstanceIds,
                    preparedBattleStarted,
                    preparedBattleOutcome,
                    preparedBattleWinningTeamId,
                    executedBattleActionIds,
                    executedBattleEffectEvidence,
                    combatResolutionEvidence,
                    pressTurnEvidence,
                    lifecycleEvidence,
                    aiDecisionEvidence,
                    battleKnowledgeEvidence,
                    encounterAiKnowledgeEvidence,
                    playerBattleKnowledge.ToSnapshot(),
                    lastEncounterAiKnowledge,
                    preparedBattleRewardPreview,
                    appliedBattleReward,
                    appliedBattleRewardLevelUpCount,
                    wallet,
                    sessionProgress,
                    manualSaveCount,
                    manualLoadCount,
                    suspendSaveCount,
                    suspendLoadCount,
                    suspendSaveConsumed,
                    _saveSlots.Has(RuntimeSaveKind.Manual),
                    _saveSlots.Has(RuntimeSaveKind.Suspend),
                    saveDiagnosticCount,
                    cancelledBattleCommandSelections,
                    preparedBattleEventCount,
                    inventory.Snapshot,
                    executedFieldActionIds,
                    cancelledFieldTargetSelections,
                    commands);
                await _eventSink.PublishAsync("Clean Training Annex session exited.", cancellationToken)
                    .ConfigureAwait(false);
                return 0;
            }

            CleanTrainingAnnexPlayCommand command = result.Command;
            commands.Add(command);
            switch (command)
            {
                case CleanTrainingAnnexPlayCommand.InspectSession:
                    await PrintSessionAsync(catalog, field, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectActor:
                    await PrintActorsAsync(roster, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ResolveStats:
                    statPreview = await ResolvePlayerStatsAsync(
                        roster.Player,
                        statPolicy,
                        cancellationToken).ConfigureAwait(false);
                    statResolutionPreviewed = true;
                    break;
                case CleanTrainingAnnexPlayCommand.RecalculateResources:
                    resourceRecalculationApplied = await RecalculatePlayerResourcesAsync(
                        roster.Player,
                        growthServices.ResourceGrowthPolicy,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ApplyVictoryExperience:
                    LevelGrowthResult growth = await ApplyVictoryExperienceAsync(
                        roster.Player,
                        growthServices,
                        cancellationToken).ConfigureAwait(false);
                    growthApplied = growth.Applied;
                    levelUpCount = growth.LevelUps.Count;
                    break;
                case CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot:
                    RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
                        TrainingAnnexHostSupport.BuildStartupSaveSnapshot(
                            roster,
                            field,
                            playerBattleKnowledge.ToSnapshot(),
                            inventory.Snapshot,
                            wallet,
                            sessionProgress),
                        catalog);
                    snapshotValidated = validation.IsValid;
                    snapshotDiagnosticCount = validation.Diagnostics.Count;
                    await _eventSink.PublishAsync(
                        $"Startup snapshot validation: {validation.Diagnostics.Count} diagnostic(s).",
                        cancellationToken).ConfigureAwait(false);
                    if (!validation.IsValid)
                    {
                        foreach (RuntimeSaveValidationDiagnostic diagnostic in validation.Diagnostics)
                        {
                            await _eventSink.PublishAsync(
                                $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                                cancellationToken).ConfigureAwait(false);
                        }

                        LastSummary = CreateSummary(
                            request,
                            roster,
                            statPreview,
                            statResolutionPreviewed,
                            resourceRecalculationApplied,
                            growthApplied,
                            levelUpCount,
                            snapshotValidated,
                            snapshotDiagnosticCount,
                            field,
                            locationHistory,
                            barrierRejected,
                            encounterTriggerConsumed,
                            preparedEncounterIds,
                            preparedEncounterActorInstanceIds,
                            preparedBattleStarted,
                            preparedBattleOutcome,
                            preparedBattleWinningTeamId,
                            executedBattleActionIds,
                            executedBattleEffectEvidence,
                            combatResolutionEvidence,
                            pressTurnEvidence,
                            lifecycleEvidence,
                            aiDecisionEvidence,
                            battleKnowledgeEvidence,
                            encounterAiKnowledgeEvidence,
                            playerBattleKnowledge.ToSnapshot(),
                            lastEncounterAiKnowledge,
                            preparedBattleRewardPreview,
                            appliedBattleReward,
                            appliedBattleRewardLevelUpCount,
                            wallet,
                            sessionProgress,
                            manualSaveCount,
                            manualLoadCount,
                            suspendSaveCount,
                            suspendLoadCount,
                            suspendSaveConsumed,
                            _saveSlots.Has(RuntimeSaveKind.Manual),
                            _saveSlots.Has(RuntimeSaveKind.Suspend),
                            saveDiagnosticCount,
                            cancelledBattleCommandSelections,
                            preparedBattleEventCount,
                            inventory.Snapshot,
                            executedFieldActionIds,
                            cancelledFieldTargetSelections,
                            commands);
                        return 5;
                    }
                    break;
                case CleanTrainingAnnexPlayCommand.EnterTrainingAnnex:
                {
                    RuntimeNavigationResult navigationResult = navigation.Navigate(
                        field.Navigation,
                        TrainingAnnexHostSupport.EnterTrainingAnnexTransition);
                    field = await ApplyNavigationAsync(
                        field,
                        navigationResult,
                        "entered Training Annex",
                        cancellationToken).ConfigureAwait(false);
                    if (navigationResult.Applied)
                    {
                        locationHistory.Add(field.Navigation.CurrentLocationId);
                        field = new RuntimeFieldSnapshot(
                            field.Navigation,
                            field.DungeonTraversal ?? new RuntimeDungeonTraversalSnapshot(
                                TrainingAnnexHostSupport.TrainingAnnexDungeon,
                                TrainingAnnexHostSupport.TrainingAnnexEntrance));
                    }
                    break;
                }
                case CleanTrainingAnnexPlayCommand.ReturnToStagingArea:
                {
                    RuntimeNavigationResult navigationResult = navigation.Navigate(
                        field.Navigation,
                        TrainingAnnexHostSupport.LeaveTrainingAnnexTransition);
                    field = await ApplyNavigationAsync(
                        field,
                        navigationResult,
                        "returned to Staging Area",
                        cancellationToken).ConfigureAwait(false);
                    if (navigationResult.Applied)
                    {
                        locationHistory.Add(field.Navigation.CurrentLocationId);
                    }
                    break;
                }
                case CleanTrainingAnnexPlayCommand.EnterReviewHall:
                    field = await ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                            RequireDungeonTraversal(field),
                            TrainingAnnexHostSupport.EnterReviewHallTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.EnterReviewAlcove:
                    field = await ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                            RequireDungeonTraversal(field),
                            TrainingAnnexHostSupport.EnterReviewAlcoveTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ReturnToReviewHall:
                    field = await ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                            RequireDungeonTraversal(field),
                            TrainingAnnexHostSupport.ReturnToReviewHallTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ReturnToAnnexEntrance:
                    field = await ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                            RequireDungeonTraversal(field),
                            TrainingAnnexHostSupport.ReturnToEntranceTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectTrainingBarrier:
                {
                    RuntimeDungeonTraversalResult traversal = dungeonTraversal.Traverse(
                        RequireDungeonTraversal(field),
                        TrainingAnnexHostSupport.InspectBarrierTransition);
                    field = await ApplyDungeonTraversalAsync(
                        field,
                        traversal,
                        cancellationToken).ConfigureAwait(false);
                    barrierRejected = traversal.Code == RuntimeDungeonTraversalCode.PolicyRejected;
                    break;
                }
                case CleanTrainingAnnexPlayCommand.UnlockReviewCheckpoint:
                    field = await ApplyDungeonStateChangeAsync(
                        field,
                        dungeonTraversal.UnlockCheckpoint(
                            RequireDungeonTraversal(field),
                            TrainingAnnexHostSupport.ReviewCheckpoint),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ActivateAshlingEncounterTrigger:
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EncounterPreparationResult preparation = encounterPreparation.Prepare(
                        TrainingAnnexHostSupport.ReviewHallAshlingTrigger);
                    encounterTriggerConsumed = await PresentEncounterPreparationAsync(
                        preparation,
                        cancellationToken).ConfigureAwait(false);
                    if (preparation.IsSuccess)
                    {
                        preparedEncounter = preparation.RequirePreparedEncounter();
                        preparedEncounterIds.Add(preparedEncounter.Encounter.Id);
                        preparedEncounterActorInstanceIds.AddRange(
                            preparedEncounter.Actors.Select(actor => actor.State.InstanceId));
                    }
                    break;
                }
                case CleanTrainingAnnexPlayCommand.StartPreparedBattle:
                {
                    if (preparedEncounter is null)
                    {
                        await _eventSink.PublishAsync(
                            "No prepared encounter is available for battle.",
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    if (preparedBattleStarted)
                    {
                        await _eventSink.PublishAsync(
                            "Prepared battle has already been resolved.",
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    var battle = await new TrainingAnnexBattleActionAdapter(
                            catalog,
                            _eventSink,
                            _commandSource,
                            executionServices,
                            rewardService,
                            pressTurnFactory,
                            new BattleStatusLifecycleService(_randomSource))
                        .RunAsync(
                            roster.Player,
                            preparedEncounter,
                            inventory,
                            playerBattleKnowledge,
                            cancellationToken)
                        .ConfigureAwait(false);
                    preparedBattleStarted = battle.Started;
                    preparedBattleOutcome = battle.Outcome;
                    preparedBattleWinningTeamId = battle.WinningTeamId;
                    executedBattleActionIds.AddRange(battle.ExecutedActionIds);
                    executedBattleEffectEvidence.AddRange(battle.ExecutedEffectEvidence);
                    combatResolutionEvidence.AddRange(battle.CombatResolutionEvidence);
                    pressTurnEvidence.AddRange(battle.PressTurnEvidence);
                    lifecycleEvidence.AddRange(battle.LifecycleEvidence);
                    aiDecisionEvidence.AddRange(battle.AiDecisionEvidence);
                    battleKnowledgeEvidence.AddRange(battle.BattleKnowledgeEvidence);
                    encounterAiKnowledgeEvidence.AddRange(battle.EncounterAiKnowledgeEvidence);
                    lastEncounterAiKnowledge = battle.EncounterAiKnowledge;
                    preparedBattleRewardPreview = battle.RewardPreview;
                    if (battle.RewardPreview is not null && appliedBattleReward is null)
                    {
                        TrainingAnnexBattleRewardApplication rewardApplication =
                            await ApplyPreparedBattleRewardAsync(
                                roster.Player,
                                battle.RewardPreview,
                                growthServices,
                                economy,
                                wallet,
                                cancellationToken).ConfigureAwait(false);
                        if (rewardApplication.Applied)
                        {
                            wallet = rewardApplication.Wallet;
                            appliedBattleReward = battle.RewardPreview;
                            appliedBattleRewardLevelUpCount = rewardApplication.Growth.LevelUps.Count;
                            growthApplied = true;
                            levelUpCount += rewardApplication.Growth.LevelUps.Count;
                            sessionProgress = RecordBattleRewardSessionProgress(
                                sessionProgress,
                                battle.RewardPreview);
                        }
                    }
                    cancelledBattleCommandSelections += battle.CancelledSelections;
                    preparedBattleEventCount += battle.EventCount;
                    break;
                }
                case CleanTrainingAnnexPlayCommand.OpenInventory:
                {
                    await PrintInventoryAsync(catalog, inventory.Snapshot, cancellationToken)
                        .ConfigureAwait(false);
                    HostCommandReadResult<CleanTrainingAnnexPlayCommand> itemSelection =
                        await _commandSource.ReadAsync(
                            CreateItemMenu(catalog, inventory.Snapshot),
                            cancellationToken).ConfigureAwait(false);
                    if (!itemSelection.IsSelected || itemSelection.Command == CleanTrainingAnnexPlayCommand.Back)
                    {
                        commands.Add(CleanTrainingAnnexPlayCommand.Back);
                        break;
                    }

                    commands.Add(itemSelection.Command);
                    HostCommandReadResult<CleanTrainingAnnexPlayCommand> targetSelection =
                        await _commandSource.ReadAsync(
                            CreateTargetMenu(roster.Player),
                            cancellationToken).ConfigureAwait(false);
                    if (!targetSelection.IsSelected || targetSelection.Command == CleanTrainingAnnexPlayCommand.Back)
                    {
                        commands.Add(CleanTrainingAnnexPlayCommand.Back);
                        cancelledFieldTargetSelections++;
                        await _eventSink.PublishAsync(
                            "Field item target selection canceled; inventory and actor state are unchanged.",
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    commands.Add(targetSelection.Command);
                    ItemDefinition item = catalog.GetRequiredItem(TrainingAnnexHostSupport.AnnexTonic);
                    TrainingAnnexFieldActionResult action = await fieldActions.UseItemAsync(
                        roster.Player,
                        item,
                        inventory,
                        cancellationToken).ConfigureAwait(false);
                    await PresentFieldActionAsync(action, item.DisplayName, inventory.Snapshot, cancellationToken)
                        .ConfigureAwait(false);
                    if (action.Applied)
                    {
                        executedFieldActionIds.Add(action.ActionId);
                    }
                    break;
                }
                case CleanTrainingAnnexPlayCommand.OpenFieldSkills:
                {
                    HostCommandReadResult<CleanTrainingAnnexPlayCommand> skillSelection =
                        await _commandSource.ReadAsync(
                            CreateFieldSkillMenu(catalog, roster.Player),
                            cancellationToken).ConfigureAwait(false);
                    if (!skillSelection.IsSelected || skillSelection.Command == CleanTrainingAnnexPlayCommand.Back)
                    {
                        commands.Add(CleanTrainingAnnexPlayCommand.Back);
                        break;
                    }

                    commands.Add(skillSelection.Command);
                    HostCommandReadResult<CleanTrainingAnnexPlayCommand> targetSelection =
                        await _commandSource.ReadAsync(
                            CreateTargetMenu(roster.Player),
                            cancellationToken).ConfigureAwait(false);
                    if (!targetSelection.IsSelected || targetSelection.Command == CleanTrainingAnnexPlayCommand.Back)
                    {
                        commands.Add(CleanTrainingAnnexPlayCommand.Back);
                        cancelledFieldTargetSelections++;
                        await _eventSink.PublishAsync(
                            "Field skill target selection canceled; resources are unchanged.",
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    commands.Add(targetSelection.Command);
                    SkillDefinition skill = catalog.GetRequiredSkill(TrainingAnnexHostSupport.Mend);
                    TrainingAnnexFieldActionResult action = await fieldActions.UseSkillAsync(
                        roster.Player,
                        skill,
                        cancellationToken).ConfigureAwait(false);
                    await PresentFieldActionAsync(action, skill.DisplayName, inventory.Snapshot, cancellationToken)
                        .ConfigureAwait(false);
                    if (action.Applied)
                    {
                        executedFieldActionIds.Add(action.ActionId);
                    }
                    break;
                }
                case CleanTrainingAnnexPlayCommand.OpenSaveLoad:
                {
                    HostCommandReadResult<CleanTrainingAnnexPlayCommand> saveSelection =
                        await _commandSource.ReadAsync(
                            CreateSaveLoadMenu(_saveSlots),
                            cancellationToken).ConfigureAwait(false);
                    if (!saveSelection.IsSelected || saveSelection.Command == CleanTrainingAnnexPlayCommand.Back)
                    {
                        commands.Add(CleanTrainingAnnexPlayCommand.Back);
                        break;
                    }

                    commands.Add(saveSelection.Command);
                    if (saveSelection.Command is CleanTrainingAnnexPlayCommand.ManualSave
                        or CleanTrainingAnnexPlayCommand.SuspendSave)
                    {
                        RuntimeSaveKind kind = saveSelection.Command == CleanTrainingAnnexPlayCommand.ManualSave
                            ? RuntimeSaveKind.Manual
                            : RuntimeSaveKind.Suspend;
                        TrainingAnnexSaveActionResult save = await SaveCurrentSessionAsync(
                            kind,
                            savePolicy,
                            catalog,
                            actorFactory,
                            roster,
                            field,
                            playerBattleKnowledge.ToSnapshot(),
                            inventory.Snapshot,
                            wallet,
                            sessionProgress,
                            encounterTriggerConsumed,
                            preparedBattleStarted,
                            preparedBattleOutcome,
                            preparedBattleWinningTeamId,
                            preparedEncounter is not null && !preparedBattleStarted,
                            saveSequence,
                            cancellationToken).ConfigureAwait(false);
                        saveDiagnosticCount += save.DiagnosticCount;
                        if (save.Applied)
                        {
                            saveSequence++;
                            if (kind == RuntimeSaveKind.Manual)
                            {
                                manualSaveCount++;
                            }
                            else
                            {
                                suspendSaveCount++;
                            }
                        }
                    }
                    else if (saveSelection.Command is CleanTrainingAnnexPlayCommand.ManualLoad
                             or CleanTrainingAnnexPlayCommand.SuspendLoad)
                    {
                        RuntimeSaveKind kind = saveSelection.Command == CleanTrainingAnnexPlayCommand.ManualLoad
                            ? RuntimeSaveKind.Manual
                            : RuntimeSaveKind.Suspend;
                        TrainingAnnexLoadActionResult loadResult = await LoadCurrentSessionAsync(
                            kind,
                            savePolicy,
                            catalog,
                            actorFactory,
                            roster,
                            field,
                            preparedEncounter is not null && !preparedBattleStarted,
                            cancellationToken).ConfigureAwait(false);
                        saveDiagnosticCount += loadResult.DiagnosticCount;
                        if (loadResult.Restored is TrainingAnnexRestoredSession restored)
                        {
                            roster = restored.Roster;
                            field = restored.Field;
                            inventory = new TrainingAnnexItemActionInventory(restored.Inventory);
                            wallet = restored.Wallet;
                            sessionProgress = restored.SessionProgress;
                            playerBattleKnowledge = restored.PlayerBattleKnowledge;
                            locationHistory.Clear();
                            locationHistory.Add(field.Navigation.CurrentLocationId);
                            encounterTriggerConsumed = restored.EncounterTriggerConsumed;
                            preparedEncounter = null;
                            preparedBattleStarted = restored.PreparedBattleStarted;
                            preparedBattleOutcome = restored.PreparedBattleOutcome;
                            preparedBattleWinningTeamId = restored.PreparedBattleWinningTeamId;
                            preparedEncounterIds.Clear();
                            preparedEncounterIds.AddRange(restored.PreparedEncounterIds);
                            preparedEncounterActorInstanceIds.Clear();
                            if (!restored.PreparedBattleStarted)
                            {
                                preparedBattleRewardPreview = null;
                                appliedBattleReward = null;
                                appliedBattleRewardLevelUpCount = 0;
                            }

                            if (kind == RuntimeSaveKind.Manual)
                            {
                                manualLoadCount++;
                            }
                            else
                            {
                                suspendLoadCount++;
                            }

                            suspendSaveConsumed |= loadResult.ConsumedRecord;
                        }
                    }
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unknown Training Annex command '{command}'.");
            }
        }
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateMenu(
        RuntimeFieldSnapshot field,
        bool encounterTriggerConsumed,
        bool preparedBattleStarted)
    {
        var options = new List<HostCommandOption<CleanTrainingAnnexPlayCommand>>
        {
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.InspectSession,
                    "Inspect Session"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.InspectActor,
                    "Inspect Actors"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ResolveStats,
                    "Resolve Stats"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.RecalculateResources,
                    "Recalculate Resources"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ApplyVictoryExperience,
                    "Apply Victory EXP"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot,
                    "Validate Startup Snapshot")
        };

        ContentId locationId = field.Navigation.CurrentLocationId;
        if (locationId == TrainingAnnexHostSupport.StagingArea)
        {
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.EnterTrainingAnnex,
                "Enter Training Annex"));
        }
        else if (field.DungeonTraversal?.CurrentNodeId == TrainingAnnexHostSupport.TrainingAnnexEntrance)
        {
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.EnterReviewHall,
                "Enter Review Hall"));
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.ReturnToStagingArea,
                "Return to Staging Area"));
        }
        else if (field.DungeonTraversal?.CurrentNodeId == TrainingAnnexHostSupport.ReviewHall)
        {
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.EnterReviewAlcove,
                "Enter Review Alcove"));
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.ReturnToAnnexEntrance,
                "Return to Annex Entrance"));
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.InspectTrainingBarrier,
                "Inspect Sealed Wing"));
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.ActivateAshlingEncounterTrigger,
                encounterTriggerConsumed
                    ? "Ashling Encounter Trigger (Resolved)"
                    : "Activate Ashling Encounter Trigger",
                !encounterTriggerConsumed,
                "Host-owned trigger for the ashling_drill catalog encounter."));
            if (encounterTriggerConsumed)
            {
                options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.StartPreparedBattle,
                    preparedBattleStarted
                        ? "Prepared Battle (Resolved)"
                        : "Start Prepared Battle",
                    !preparedBattleStarted,
                    "Runs the prepared encounter through clean battle actions."));
            }
        }
        else if (field.DungeonTraversal?.CurrentNodeId == TrainingAnnexHostSupport.ReviewAlcove)
        {
            string checkpointLabel = field.DungeonTraversal.IsCheckpointUnlocked(
                TrainingAnnexHostSupport.ReviewCheckpoint)
                ? "Review Checkpoint (Unlocked)"
                : "Unlock Review Checkpoint";
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.UnlockReviewCheckpoint,
                checkpointLabel));
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.ReturnToReviewHall,
                "Return to Review Hall"));
        }

        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenInventory,
            "Inventory"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenFieldSkills,
            "Field Skills"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.Exit,
            "Exit"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenSaveLoad,
            "Save / Load"));

        string locationLabel = locationId == TrainingAnnexHostSupport.StagingArea
            ? FieldLabel(locationId)
            : DungeonNodeLabel(field.DungeonTraversal?.CurrentNodeId);
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            $"Training Annex Clean Session - {locationLabel}",
            options);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateSaveLoadMenu(
        TrainingAnnexSaveSlotStore saveSlots) =>
        new(
            "Clean Save / Load",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ManualSave,
                    "Manual Save"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ManualLoad,
                    "Manual Load",
                    saveSlots.Has(RuntimeSaveKind.Manual),
                    "Load the manual Training Annex demo slot."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SuspendSave,
                    "Suspend Save"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SuspendLoad,
                    "Suspend Load",
                    saveSlots.Has(RuntimeSaveKind.Suspend),
                    "Load and consume the suspend Training Annex demo slot."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateItemMenu(
        GameDataCatalog catalog,
        RuntimeInventorySnapshot inventory)
    {
        ItemDefinition tonic = catalog.GetRequiredItem(TrainingAnnexHostSupport.AnnexTonic);
        int quantity = inventory.GetQuantity(tonic.Id);
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Clean Inventory",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.UseAnnexTonic,
                    $"{tonic.DisplayName} x{quantity}",
                    quantity > 0,
                    tonic.Description),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateFieldSkillMenu(
        GameDataCatalog catalog,
        TrainingAnnexRuntimeActor player)
    {
        SkillDefinition mend = catalog.GetRequiredSkill(TrainingAnnexHostSupport.Mend);
        int level = player.Actor.State.ToSnapshot().Progression.Level;
        bool known = player.Actor.SkillLoadout.Any(skill => skill.Id == mend.Id) ||
            player.Actor.Entity.SkillUnlocks.Any(unlock =>
                unlock.SkillId == mend.Id && unlock.Level <= level);
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Clean Field Skills",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.UseMend,
                    mend.DisplayName,
                    known,
                    mend.Description),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateTargetMenu(
        TrainingAnnexRuntimeActor player) =>
        new(
            "Select Field Target",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.TargetPlayer,
                    player.Actor.Entity.DisplayName),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);

    private async ValueTask<TrainingAnnexSaveActionResult> SaveCurrentSessionAsync(
        RuntimeSaveKind kind,
        IRuntimeSavePolicyService savePolicy,
        GameDataCatalog catalog,
        ICatalogBattleActorFactory actorFactory,
        TrainingAnnexActorRoster roster,
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
        CancellationToken cancellationToken)
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
            field,
            knowledge,
            inventory,
            wallet,
            session,
            encounterTriggerConsumed,
            preparedBattleStarted,
            preparedBattleOutcome,
            preparedBattleWinningTeamId);
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(snapshot, catalog);
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

    private async ValueTask<TrainingAnnexLoadActionResult> LoadCurrentSessionAsync(
        RuntimeSaveKind kind,
        IRuntimeSavePolicyService savePolicy,
        GameDataCatalog catalog,
        ICatalogBattleActorFactory actorFactory,
        TrainingAnnexActorRoster roster,
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

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(record!.Snapshot, catalog);
        if (!validation.IsValid)
        {
            await PublishSaveValidationDiagnosticsAsync($"{KindLabel(kind)} load", validation, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingAnnexLoadActionResult(null, validation.Diagnostics.Count, false);
        }

        TrainingAnnexSessionRestoreResult restore =
            RestoreTrainingAnnexSession(validation.RequireValidSnapshot(), roster, actorFactory);
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

    private static RuntimeSaveContextSnapshot CurrentSaveContext(
        RuntimeFieldSnapshot field,
        bool hasPendingHostAction) =>
        new(
            field.DungeonTraversal is null
                ? TrainingAnnexHostSupport.FieldMenuSaveContext
                : TrainingAnnexHostSupport.DungeonMenuSaveContext,
            hasPendingHostAction);

    private static RuntimeSaveGameSnapshot BuildCurrentSaveSnapshot(
        TrainingAnnexActorRoster roster,
        RuntimeFieldSnapshot field,
        RuntimeKnowledgeSnapshot knowledge,
        RuntimeInventorySnapshot inventory,
        RuntimeWalletSnapshot wallet,
        RuntimeSessionProgressSnapshot session,
        bool encounterTriggerConsumed,
        bool preparedBattleStarted,
        BattleEncounterOutcome? preparedBattleOutcome,
        ContentId? preparedBattleWinningTeamId)
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
            field,
            knowledge,
            inventory,
            wallet,
            session,
            hostContext);
    }

    private static TrainingAnnexSessionRestoreResult RestoreTrainingAnnexSession(
        RuntimeSaveGameSnapshot snapshot,
        TrainingAnnexActorRoster currentRoster,
        ICatalogBattleActorFactory actorFactory)
    {
        Dictionary<RuntimeInstanceId, RuntimeActorSnapshot> actors = snapshot.Actors
            .ToDictionary(actor => actor.Identity.InstanceId, actor => actor);
        if (!TryRestoreActor(currentRoster.Player, actors, actorFactory, out TrainingAnnexRuntimeActor player, out string? playerDiagnostic))
        {
            return TrainingAnnexSessionRestoreResult.Failed(playerDiagnostic);
        }

        var enemies = new List<TrainingAnnexRuntimeActor>();
        foreach (TrainingAnnexRuntimeActor enemy in currentRoster.Enemies)
        {
            if (!TryRestoreActor(enemy, actors, actorFactory, out TrainingAnnexRuntimeActor restoredEnemy, out string? enemyDiagnostic))
            {
                return TrainingAnnexSessionRestoreResult.Failed(enemyDiagnostic);
            }

            enemies.Add(restoredEnemy);
        }

        TrainingAnnexActorRoster roster = new(player, enemies);

        RuntimeFieldSnapshot field = snapshot.Field ??
            new RuntimeFieldSnapshot(new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.StagingArea));
        bool ashlingCleared = snapshot.Session.Flags.Contains(AshlingDrillClearedFlag);
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
                field,
                snapshot.Inventory,
                snapshot.Wallet,
                snapshot.Session,
                TrainingAnnexBattleKnowledgeState.FromSnapshot(snapshot.Knowledge),
                triggerConsumed,
                battleStarted,
                outcome,
                winningTeam,
                preparedEncounterIds),
            []);
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

    private ValueTask PrintInventoryAsync(
        GameDataCatalog catalog,
        RuntimeInventorySnapshot inventory,
        CancellationToken cancellationToken)
    {
        ItemDefinition tonic = catalog.GetRequiredItem(TrainingAnnexHostSupport.AnnexTonic);
        return _eventSink.PublishAsync(
            $"Inventory: {tonic.DisplayName} x{inventory.GetQuantity(tonic.Id)}.",
            cancellationToken);
    }

    private async ValueTask PresentFieldActionAsync(
        TrainingAnnexFieldActionResult action,
        string displayName,
        RuntimeInventorySnapshot inventory,
        CancellationToken cancellationToken)
    {
        if (!action.Assessment.CanExecute)
        {
            string diagnostics = string.Join(
                "; ",
                action.Assessment.Diagnostics.Select(diagnostic => diagnostic.Message));
            await _eventSink.PublishAsync(
                $"Field action rejected: {displayName}; {diagnostics}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!action.Applied || action.Execution is null)
        {
            string diagnostics = string.Join(
                "; ",
                action.Execution?.Diagnostics.Select(diagnostic => diagnostic.Message) ?? []);
            await _eventSink.PublishAsync(
                $"Field action failed: {displayName}; {diagnostics}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await _eventSink.PublishAsync(
            $"Field action executed: {displayName}; HP {action.HpBefore.Current}->{action.HpAfter.Current}/{action.HpAfter.Maximum}; SP {action.SpBefore.Current}->{action.SpAfter.Current}/{action.SpAfter.Maximum}; inventory {TrainingAnnexHostSupport.AnnexTonic} x{inventory.GetQuantity(TrainingAnnexHostSupport.AnnexTonic)}.",
            cancellationToken).ConfigureAwait(false);
    }

    private ValueTask PrintSessionAsync(
        GameDataCatalog catalog,
        RuntimeFieldSnapshot field,
        CancellationToken cancellationToken) =>
        _eventSink.PublishAsync(
            $"Session: {TrainingAnnexHostSupport.PackId}; {catalog.Entities.Count} entities, {catalog.Skills.Count} skills, {catalog.Items.Count} items, {catalog.Encounters.Count} encounters, {catalog.Dungeons.Count} dungeons. Location: {FieldLabel(field.Navigation.CurrentLocationId)} ({field.Navigation.CurrentLocationId}); dungeon state: {(field.DungeonTraversal is null ? "not active" : field.DungeonTraversal.CurrentNodeId.ToString())}.",
            cancellationToken);

    private async ValueTask<RuntimeFieldSnapshot> ApplyNavigationAsync(
        RuntimeFieldSnapshot field,
        RuntimeNavigationResult navigation,
        string appliedDescription,
        CancellationToken cancellationToken)
    {
        if (!navigation.Applied)
        {
            await _eventSink.PublishAsync(
                $"Field navigation rejected: {navigation.Message}",
                cancellationToken).ConfigureAwait(false);
            return field;
        }

        await _eventSink.PublishAsync(
            $"Field navigation: {appliedDescription}; location {FieldLabel(navigation.After.CurrentLocationId)} ({navigation.After.CurrentLocationId}).",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeFieldSnapshot(navigation.After, field.DungeonTraversal);
    }

    private async ValueTask<RuntimeFieldSnapshot> ApplyDungeonTraversalAsync(
        RuntimeFieldSnapshot field,
        RuntimeDungeonTraversalResult traversal,
        CancellationToken cancellationToken)
    {
        if (!traversal.Applied)
        {
            await _eventSink.PublishAsync(
                $"Dungeon traversal rejected: {traversal.Message}",
                cancellationToken).ConfigureAwait(false);
            return field;
        }

        await _eventSink.PublishAsync(
            $"Dungeon traversal: {DungeonNodeLabel(traversal.Before.CurrentNodeId)} -> {DungeonNodeLabel(traversal.After.CurrentNodeId)}.",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeFieldSnapshot(field.Navigation, traversal.After);
    }

    private async ValueTask<RuntimeFieldSnapshot> ApplyDungeonStateChangeAsync(
        RuntimeFieldSnapshot field,
        RuntimeDungeonStateChangeResult change,
        CancellationToken cancellationToken)
    {
        if (!change.Applied)
        {
            await _eventSink.PublishAsync(
                "Dungeon state unchanged: checkpoint was already unlocked.",
                cancellationToken).ConfigureAwait(false);
            return field;
        }

        RuntimeDungeonTraversalEvent dungeonEvent = RequireSingleEvent(change.Events);
        await _eventSink.PublishAsync(
            $"Dungeon checkpoint unlocked: {dungeonEvent.ContentId}.",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeFieldSnapshot(field.Navigation, change.After);
    }

    private static RuntimeDungeonTraversalSnapshot RequireDungeonTraversal(RuntimeFieldSnapshot field) =>
        field.DungeonTraversal ?? throw new InvalidOperationException(
            "The Training Annex dungeon traversal state is not active.");

    private static RuntimeDungeonTraversalEvent RequireSingleEvent(
        IReadOnlyList<RuntimeDungeonTraversalEvent> events) =>
        events.Count == 1
            ? events[0]
            : throw new InvalidOperationException("Expected one dungeon state event.");

    private async ValueTask<bool> PresentEncounterPreparationAsync(
        EncounterPreparationResult preparation,
        CancellationToken cancellationToken)
    {
        if (!preparation.IsSuccess)
        {
            foreach (EncounterPreparationDiagnostic diagnostic in preparation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"Encounter preparation rejected [{diagnostic.Code}]: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        PreparedEncounter prepared = preparation.RequirePreparedEncounter();
        string actors = string.Join(
            ", ",
            prepared.Actors.Select(actor =>
                $"{actor.Entity.DisplayName} ({actor.State.InstanceId})"));
        await _eventSink.PublishAsync(
            $"Encounter trigger {prepared.TriggerId} prepared {prepared.Encounter.DisplayName}: {actors}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            "Encounter actors are ready for a host-owned battle handoff; traversal did not start this encounter.",
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string FieldLabel(ContentId locationId) =>
        locationId == TrainingAnnexHostSupport.StagingArea
            ? "Staging Area"
            : locationId == TrainingAnnexHostSupport.TrainingAnnexEntrance
                ? "Training Annex Entrance"
                : locationId.ToString();

    private static string DungeonNodeLabel(ContentId? nodeId) =>
        nodeId == TrainingAnnexHostSupport.TrainingAnnexEntrance
            ? "Training Annex Entrance"
            : nodeId == TrainingAnnexHostSupport.ReviewHall
                ? "Review Hall"
                : nodeId == TrainingAnnexHostSupport.ReviewAlcove
                    ? "Review Alcove"
                    : nodeId?.ToString() ?? "Unknown Dungeon Node";

    private async ValueTask PrintActorsAsync(TrainingAnnexActorRoster roster, CancellationToken cancellationToken)
    {
        await _eventSink.PublishAsync(
            $"Actor roster: {roster.AllActors.Count} actor(s).",
            cancellationToken).ConfigureAwait(false);
        foreach (TrainingAnnexRuntimeActor actor in roster.AllActors)
        {
            await PrintActorAsync(actor, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask PrintActorAsync(TrainingAnnexRuntimeActor runtimeActor, CancellationToken cancellationToken)
    {
        CatalogBattleActor actor = runtimeActor.Actor;
        RuntimeActorSnapshot snapshot = runtimeActor.Actor.State.ToSnapshot();
        string resources = string.Join(
            ", ",
            snapshot.Resources.Select(resource => $"{resource.ResourceId} {resource.Current}/{resource.Maximum}"));
        string baseStats = string.Join(
            ", ",
            snapshot.Stats.BaseStats.Select(pair => $"{pair.Key} {pair.Value}"));
        string effectiveStats = string.Join(
            ", ",
            snapshot.Stats.EffectiveStats.Select(pair => $"{pair.Key} {pair.Value}"));
        string activeSkills = string.Join(", ", actor.ActiveSkills.Select(skill => skill.DisplayName));
        string passiveSkills = string.Join(
            ", ",
            actor.SkillLoadout
                .Where(skill => skill.Activation == SkillActivation.Passive)
                .Select(skill => skill.DisplayName));

        await _eventSink.PublishAsync(
            $"{runtimeActor.Role}: {actor.Entity.DisplayName}; instance {snapshot.Identity.InstanceId}; level {runtimeActor.Level}; resources: {resources}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync($"Base stats: {baseStats}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync($"Effective stats: {effectiveStats}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync($"Active skills: {activeSkills}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Passive skills: {(string.IsNullOrWhiteSpace(passiveSkills) ? "none" : passiveSkills)}.",
            cancellationToken).ConfigureAwait(false);
    }

    private static CleanTrainingAnnexPlaySummary CreateSummary(
        ContentPackTextRequest request,
        TrainingAnnexActorRoster roster,
        IReadOnlyList<StatResolutionResult> statPreview,
        bool statResolutionPreviewed,
        bool resourceRecalculationApplied,
        bool growthApplied,
        int levelUpCount,
        bool snapshotValidated,
        int snapshotDiagnosticCount,
        RuntimeFieldSnapshot field,
        IReadOnlyList<ContentId> locationHistory,
        bool barrierRejected,
        bool encounterTriggerConsumed,
        IReadOnlyList<ContentId> preparedEncounterIds,
        IReadOnlyList<RuntimeInstanceId> preparedEncounterActorInstanceIds,
        bool preparedBattleStarted,
        BattleEncounterOutcome? preparedBattleOutcome,
        ContentId? preparedBattleWinningTeamId,
        IReadOnlyList<ContentId> executedBattleActionIds,
        IReadOnlyList<TrainingAnnexTypedEffectEvidence> executedBattleEffectEvidence,
        IReadOnlyList<TrainingAnnexCombatResolutionEvidence> combatResolutionEvidence,
        IReadOnlyList<TrainingAnnexPressTurnEvidence> pressTurnEvidence,
        IReadOnlyList<TrainingAnnexLifecycleEvidence> lifecycleEvidence,
        IReadOnlyList<TrainingAnnexAiDecisionEvidence> aiDecisionEvidence,
        IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> battleKnowledgeEvidence,
        IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> encounterAiKnowledgeEvidence,
        RuntimeKnowledgeSnapshot battleKnowledge,
        RuntimeKnowledgeSnapshot encounterAiKnowledge,
        BattleRewardResult? preparedBattleRewardPreview,
        BattleRewardResult? appliedBattleReward,
        int appliedBattleRewardLevelUpCount,
        RuntimeWalletSnapshot wallet,
        RuntimeSessionProgressSnapshot sessionProgress,
        int manualSaveCount,
        int manualLoadCount,
        int suspendSaveCount,
        int suspendLoadCount,
        bool suspendSaveConsumed,
        bool hasManualSave,
        bool hasSuspendSave,
        int saveDiagnosticCount,
        int cancelledBattleCommandSelections,
        int preparedBattleEventCount,
        RuntimeInventorySnapshot inventory,
        IReadOnlyList<ContentId> executedFieldActionIds,
        int cancelledFieldTargetSelections,
        IReadOnlyList<CleanTrainingAnnexPlayCommand> commands)
    {
        CatalogBattleActor player = roster.Player.Actor;
        RuntimeActorSnapshot playerSnapshot = roster.Player.Actor.State.ToSnapshot();
        return new(
            [request.ManifestPath],
            request.DocumentPaths,
            player.Entity.Id,
            roster.Player.Level,
            roster.AllActors.Count,
            roster.Enemies.Count,
            roster.AllActors.Select(actor => actor.Actor.Entity.Id).ToArray(),
            roster.AllActors.Select(actor => actor.Actor.State.InstanceId).ToArray(),
            playerSnapshot.Resources,
            playerSnapshot.Progression,
            statPreview.ToArray(),
            player.ActiveSkills.Count,
            player.SkillLoadout.Count(skill => skill.Activation == SkillActivation.Passive),
            statResolutionPreviewed,
            resourceRecalculationApplied,
            growthApplied,
            levelUpCount,
            snapshotValidated,
            snapshotDiagnosticCount,
            field.Navigation.CurrentLocationId,
            locationHistory.ToArray(),
            field.DungeonTraversal?.CurrentNodeId,
            field.DungeonTraversal?.VisitedNodeIds ?? [],
            field.DungeonTraversal?.UnlockedCheckpointIds ?? [],
            barrierRejected,
            encounterTriggerConsumed,
            preparedEncounterIds.ToArray(),
            preparedEncounterActorInstanceIds.ToArray(),
            preparedBattleStarted,
            preparedBattleOutcome,
            preparedBattleWinningTeamId,
            executedBattleActionIds.ToArray(),
            executedBattleEffectEvidence.ToArray(),
            combatResolutionEvidence.ToArray(),
            pressTurnEvidence.ToArray(),
            lifecycleEvidence.ToArray(),
            aiDecisionEvidence.ToArray(),
            battleKnowledgeEvidence.ToArray(),
            encounterAiKnowledgeEvidence.ToArray(),
            battleKnowledge,
            encounterAiKnowledge,
            preparedBattleRewardPreview,
            appliedBattleReward,
            appliedBattleRewardLevelUpCount,
            wallet,
            sessionProgress,
            manualSaveCount,
            manualLoadCount,
            suspendSaveCount,
            suspendLoadCount,
            suspendSaveConsumed,
            hasManualSave,
            hasSuspendSave,
            saveDiagnosticCount,
            cancelledBattleCommandSelections,
            preparedBattleEventCount,
            inventory,
            executedFieldActionIds.ToArray(),
            cancelledFieldTargetSelections,
            commands.ToArray());
    }

    private sealed record TrainingAnnexBattleRewardApplication(
        bool Applied,
        LevelGrowthResult Growth,
        RuntimeWalletSnapshot Wallet);

    private sealed record TrainingAnnexSaveActionResult(
        bool Applied,
        int DiagnosticCount);

    private sealed record TrainingAnnexLoadActionResult(
        TrainingAnnexRestoredSession? Restored,
        int DiagnosticCount,
        bool ConsumedRecord);

    private sealed record TrainingAnnexSessionRestoreResult
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

        public static TrainingAnnexSessionRestoreResult Failed(string? diagnostic) =>
            new(null, [diagnostic ?? "Saved session could not be restored."]);
    }

    private sealed record TrainingAnnexRestoredSession(
        TrainingAnnexActorRoster Roster,
        RuntimeFieldSnapshot Field,
        RuntimeInventorySnapshot Inventory,
        RuntimeWalletSnapshot Wallet,
        RuntimeSessionProgressSnapshot SessionProgress,
        TrainingAnnexBattleKnowledgeState PlayerBattleKnowledge,
        bool EncounterTriggerConsumed,
        bool PreparedBattleStarted,
        BattleEncounterOutcome? PreparedBattleOutcome,
        ContentId? PreparedBattleWinningTeamId,
        IReadOnlyList<ContentId> PreparedEncounterIds);

    private async ValueTask PublishRulesetDiagnosticsAsync(
        string category,
        IEnumerable<RulesetBindingDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (RulesetBindingDiagnostic diagnostic in diagnostics)
        {
            await _eventSink.PublishAsync(
                $"[{category}:{diagnostic.Code}] {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<IReadOnlyList<StatResolutionResult>> ResolvePlayerStatsAsync(
        TrainingAnnexRuntimeActor player,
        IStatResolutionPolicy statPolicy,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot snapshot = player.Actor.State.ToSnapshot();
        RuntimeStatStageSnapshot attackStage = new(StandardProgressionIds.Attack, 1);
        IEnumerable<KeyValuePair<ContentId, decimal>> activeFormStats =
            snapshot.Identity.ActorKindId == StandardProgressionIds.Demon
                ? snapshot.Stats.BaseStats
                : [];
        var results = new List<StatResolutionResult>();
        var messages = new List<string>();

        foreach (ContentId statId in StandardProgressionIds.CoreStats)
        {
            StatResolutionResult unmodified = statPolicy.Resolve(new StatResolutionRequest(
                snapshot.Identity.ActorKindId,
                statId,
                snapshot.Stats.BaseStats,
                activeFormStats));
            StatResolutionResult boosted = statPolicy.Resolve(new StatResolutionRequest(
                snapshot.Identity.ActorKindId,
                statId,
                snapshot.Stats.BaseStats,
                activeFormStats,
                statStages: [attackStage]));
            results.Add(boosted);
            messages.Add($"{statId} {unmodified.FinalValue}->{boosted.FinalValue}");
        }

        await _eventSink.PublishAsync(
            $"Stat policy: standard_stat resolved {player.Actor.Entity.DisplayName} with attack stage +1.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Resolved stats: {string.Join(", ", messages)}.",
            cancellationToken).ConfigureAwait(false);

        return results.ToArray();
    }

    private async ValueTask<LevelGrowthResult> ApplyVictoryExperienceAsync(
        TrainingAnnexRuntimeActor player,
        GrowthRulesetServices growthServices,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot before = player.Actor.State.ToSnapshot();
        long requiredExperience = growthServices.ExperienceCurve.GetRequiredExperience(before.Progression.Level);
        long award = Math.Max(0, requiredExperience - before.Progression.Experience);
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            before.Progression,
            before.Stats,
            before.Identity.ActorKindId,
            award,
            new TrainingAnnexMinimumRandomSource(),
            resources: before.Resources,
            baseResourceValues: before.BaseResourceValues));
        RuntimeMutationResult mutation = new RuntimeProgressionTransactionService().ApplyLevelGrowth(
            player.Actor.State,
            growth);
        if (!mutation.Applied)
        {
            foreach (RuntimeMutationDiagnostic diagnostic in mutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return growth;
        }

        RuntimeActorSnapshot after = mutation.After;
        await _eventSink.PublishAsync(
            $"Victory EXP: awarded {award} EXP through standard_growth.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Growth result: {player.Actor.Entity.DisplayName} level {before.Progression.Level}->{after.Progression.Level}; exp {before.Progression.Experience}->{after.Progression.Experience}; lifetime {before.Progression.LifetimeExperience}->{after.Progression.LifetimeExperience}; stat points {before.Progression.UnspentStatPoints}->{after.Progression.UnspentStatPoints}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Level-up events: {(growth.LevelUps.Count == 0 ? "none" : string.Join(", ", growth.LevelUps.Select(levelUp => levelUp.Level.ToString())))}.",
            cancellationToken).ConfigureAwait(false);

        return growth;
    }

    private async ValueTask<TrainingAnnexBattleRewardApplication> ApplyPreparedBattleRewardAsync(
        TrainingAnnexRuntimeActor player,
        BattleRewardResult reward,
        GrowthRulesetServices growthServices,
        IEconomyTransactionService economy,
        RuntimeWalletSnapshot wallet,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot before = player.Actor.State.ToSnapshot();
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            before.Progression,
            before.Stats,
            before.Identity.ActorKindId,
            reward.TotalExperience,
            _randomSource,
            resources: before.Resources,
            baseResourceValues: before.BaseResourceValues));
        RuntimeMutationResult progressionMutation = new RuntimeProgressionTransactionService().ApplyLevelGrowth(
            player.Actor.State,
            growth);
        if (!progressionMutation.Applied)
        {
            foreach (RuntimeMutationDiagnostic diagnostic in progressionMutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexBattleRewardApplication(false, growth, wallet);
        }

        WalletTransactionResult walletMutation = economy.AddMacca(wallet, reward.TotalMacca);
        if (!walletMutation.Applied)
        {
            foreach (ResourceTransactionDiagnostic diagnostic in walletMutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}]: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexBattleRewardApplication(false, growth, wallet);
        }

        RuntimeActorSnapshot after = progressionMutation.After;
        await _eventSink.PublishAsync(
            $"Battle rewards applied: +{reward.TotalExperience} EXP, +{reward.TotalMacca} Macca.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Reward progression: {player.Actor.Entity.DisplayName} level {before.Progression.Level}->{after.Progression.Level}; exp {before.Progression.Experience}->{after.Progression.Experience}; lifetime {before.Progression.LifetimeExperience}->{after.Progression.LifetimeExperience}; wallet {wallet.Macca}->{walletMutation.After.Macca}.",
            cancellationToken).ConfigureAwait(false);

        return new TrainingAnnexBattleRewardApplication(true, growth, walletMutation.After);
    }

    private static RuntimeSessionProgressSnapshot RecordBattleRewardSessionProgress(
        RuntimeSessionProgressSnapshot before,
        BattleRewardResult reward)
    {
        var counters = before.Counters.ToDictionary(pair => pair.Key, pair => pair.Value);
        AddCounter(counters, ContentId.Parse("training_annex_victories"), 1);
        AddCounter(counters, ContentId.Parse("training_annex_exp"), reward.TotalExperience);
        AddCounter(counters, ContentId.Parse("training_annex_macca"), reward.TotalMacca);
        return new RuntimeSessionProgressSnapshot(
            before.MoonPhaseId,
            before.ElapsedTicks,
            counters,
            before.Flags.Append(AshlingDrillClearedFlag).Distinct());
    }

    private static void AddCounter(Dictionary<ContentId, long> counters, ContentId id, long value)
    {
        counters[id] = counters.GetValueOrDefault(id) + value;
    }

    private async ValueTask<bool> RecalculatePlayerResourcesAsync(
        TrainingAnnexRuntimeActor player,
        IResourceGrowthPolicy resourceGrowthPolicy,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot before = player.Actor.State.ToSnapshot();
        RuntimeResourceSnapshot beforeHp = before.Resources.Single(resource =>
            resource.ResourceId == TrainingAnnexHostSupport.Hp);
        RuntimeMutationResult mutation = new RuntimeResourceTransactionService().AddResource(
            player.Actor.State,
            TrainingAnnexHostSupport.Hp,
            -10);
        if (!mutation.Applied)
        {
            foreach (RuntimeMutationDiagnostic diagnostic in mutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        ResourceRecalculationResult recalculated = resourceGrowthPolicy.Recalculate(
            new ResourceRecalculationRequest(
                mutation.After.Resources,
                mutation.After.BaseResourceValues,
                mutation.After.Stats.EffectiveStats,
                ResourceCurrentAdjustmentMode.PreserveCurrent));
        RuntimeMutationResult recalculationMutation =
            new RuntimeResourceTransactionService().ApplyRecalculation(
                player.Actor.State,
                recalculated);
        if (!recalculationMutation.Applied)
        {
            foreach (RuntimeMutationDiagnostic diagnostic in recalculationMutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        RuntimeResourceSnapshot afterHp = recalculationMutation.After.Resources.Single(resource =>
            resource.ResourceId == TrainingAnnexHostSupport.Hp);
        await _eventSink.PublishAsync(
            $"Resource recalculation: {player.Actor.Entity.DisplayName} hp {beforeHp.Current}/{beforeHp.Maximum} -> {afterHp.Current}/{afterHp.Maximum}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Resource policy: standard_growth preserved current hp and recalculated maximum {afterHp.Maximum}.",
            cancellationToken).ConfigureAwait(false);
        return true;
    }
}
