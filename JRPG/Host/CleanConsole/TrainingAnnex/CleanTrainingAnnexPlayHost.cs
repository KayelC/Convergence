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
    InspectParty,
    InspectStock,
    OpenPartyStockOperations,
    PartySwapActiveForm,
    PartySummonAshling,
    PartySwapActiveDemon,
    PartyReturnActiveDemon,
    PartyReplaceWardShell,
    PartyDismissAshling,
    PartyConsumeBrambleRunner,
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
    OpenShop,
    ShopBuy,
    ShopSell,
    SelectShopOffer,
    SelectSellOffer,
    EquipPurchasedEquipment,
    OpenRecoveryFacility,
    RecoveryTreat,
    BattleAttack,
    OpenBattleSkills,
    OpenBattleItems,
    BattleGuard,
    BattlePass,
    BattleAnalyze,
    SelectBattleSkill,
    SelectBattleItem,
    SelectFieldItem,
    SelectBattleTarget,
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
    RuntimePartyStockSnapshot PartyStock,
    IReadOnlyList<TrainingAnnexPartyTransitionEvidence> PartyTransitions,
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
    WalletTransactionResult? AppliedWalletTransaction,
    IReadOnlyList<TrainingAnnexShopTransactionEvidence> ShopTransactions,
    IReadOnlyList<TrainingAnnexEquipmentChangeEvidence> ShopEquipmentChanges,
    IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> ShopOfferDiagnostics,
    IReadOnlyList<TrainingAnnexHospitalRestorationEvidence> HospitalRestorations,
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
    RuntimeEquipmentSnapshot Equipment,
    RuntimeEquipmentProfile EquipmentProfile,
    IReadOnlyList<ContentId> ExecutedFieldActionIds,
    int CancelledFieldTargetSelections,
    IReadOnlyList<CleanTrainingAnnexPlayCommand> Commands);

internal sealed class CleanTrainingAnnexPlayHost
{
    private static readonly ContentId Field = ContentId.Parse("field");

    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;
    private readonly IRandomSource _randomSource;
    private readonly TrainingAnnexSaveSlotStore _saveSlots;
    private readonly RuntimeInventorySnapshot? _initialInventory;
    private readonly RuntimeEquipmentSnapshot? _initialEquipment;
    private readonly RuntimeWalletSnapshot? _initialWallet;

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
        RuntimeInventorySnapshot? initialInventory = null,
        RuntimeEquipmentSnapshot? initialEquipment = null,
        RuntimeWalletSnapshot? initialWallet = null)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
        _randomSource = randomSource ?? new TrainingAnnexMinimumRandomSource();
        _saveSlots = saveSlots ?? new TrainingAnnexSaveSlotStore();
        _initialInventory = initialInventory;
        _initialEquipment = initialEquipment;
        _initialWallet = initialWallet;
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
        RulesetBindingResult<ResourceManagementRulesetServices> resourceManagementBinding =
            rulesetResolver.BindResourceManagementServices(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_economy"));
        if (!resourceManagementBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("economy", resourceManagementBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        ResourceManagementRulesetServices resourceManagement = resourceManagementBinding.RequireService();
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
        var fieldPresenter = new TrainingAnnexFieldPresenter(_eventSink);
        var rewardApplicator = new TrainingAnnexBattleRewardApplicator(_eventSink, _randomSource);
        IInventoryTransitionService inventoryTransitions = resourceManagement.Inventory;
        IEquipmentTransitionService equipmentTransitions = resourceManagement.Equipment;
        IEconomyTransactionService economy = resourceManagement.Economy;
        var equipmentProfileResolver = new RuntimeEquipmentProfileResolver();
        var partyController = new TrainingAnnexPartyController();
        TrainingAnnexPartySetupResult partySetup = partyController.CreateInitialParty(roster);
        RuntimePartyStockSnapshot partyStock = partySetup.Snapshot;
        var partyTransitions = new List<TrainingAnnexPartyTransitionEvidence>(partySetup.Transitions);
        var inventory = new TrainingAnnexItemActionInventory(
            BuildInitialInventory(_initialInventory, inventoryTransitions),
            inventoryTransitions);
        roster.Player.Actor.State.ReplaceEquipment(BuildInitialEquipment(
            catalog,
            inventory.Snapshot,
            _initialEquipment,
            equipmentTransitions));
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
        var persistence = new TrainingAnnexPersistenceController(_saveSlots, _eventSink);
        RuntimeWalletSnapshot wallet = _initialWallet ?? new RuntimeWalletSnapshot(0);
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
        WalletTransactionResult? appliedWalletTransaction = null;
        var shopTransactions = new List<TrainingAnnexShopTransactionEvidence>();
        var shopEquipmentChanges = new List<TrainingAnnexEquipmentChangeEvidence>();
        var shopOfferDiagnostics = new List<RuntimeShopOfferResolutionDiagnostic>();
        var hospitalRestorations = new List<TrainingAnnexHospitalRestorationEvidence>();
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
        await _eventSink.PublishAsync(
            $"Party setup: {partyStock.ActiveParty.Count} active, {partyStock.ReserveMembers.Count} reserve.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Stock setup: active form {(partyStock.ActiveForm is null ? 0 : 1)}, Persona stock {partyStock.PersonaStock.Count}, Demon stock {partyStock.DemonStock.Count}.",
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
                    partyStock,
                    partyTransitions,
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
                    appliedWalletTransaction,
                    shopTransactions,
                    shopEquipmentChanges,
                    shopOfferDiagnostics,
                    hospitalRestorations,
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
                    equipmentProfileResolver,
                    catalog,
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
                    await fieldPresenter.PrintSessionAsync(catalog, field, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectActor:
                    await PrintActorsAsync(roster, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectParty:
                    await partyController.PrintPartyAsync(partyStock, _eventSink, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectStock:
                    await partyController.PrintStockAsync(partyStock, _eventSink, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.OpenPartyStockOperations:
                {
                    await partyController.PrintPartyAsync(partyStock, _eventSink, cancellationToken)
                        .ConfigureAwait(false);
                    await partyController.PrintStockAsync(partyStock, _eventSink, cancellationToken)
                        .ConfigureAwait(false);
                    HostCommandReadResult<CleanTrainingAnnexPlayCommand> operationSelection =
                        await _commandSource.ReadAsync(
                            CreatePartyStockOperationMenu(partyStock),
                            cancellationToken).ConfigureAwait(false);
                    if (!operationSelection.IsSelected ||
                        operationSelection.Command == CleanTrainingAnnexPlayCommand.Back)
                    {
                        commands.Add(CleanTrainingAnnexPlayCommand.Back);
                        break;
                    }

                    commands.Add(operationSelection.Command);
                    TrainingAnnexPartyOperation operation = ToPartyOperation(operationSelection.Command);
                    string operationName = PartyOperationName(operation);
                    PartyStockTransitionResult operationResult = partyController.ExecuteOperation(
                        operation,
                        partyStock,
                        roster);
                    partyTransitions.Add(TrainingAnnexPartyTransitionEvidence.From(operationName, operationResult));
                    await partyController.PrintOperationAsync(
                        operationName,
                        operationResult,
                        _eventSink,
                        cancellationToken).ConfigureAwait(false);
                    if (operationResult.Applied)
                    {
                        partyStock = operationResult.After;
                    }

                    break;
                }
                case CleanTrainingAnnexPlayCommand.ResolveStats:
                    RuntimeEquipmentProfile equipmentProfile = equipmentProfileResolver.Resolve(
                        roster.Player.Actor.State.ToSnapshot().Equipment,
                        catalog);
                    statPreview = await ResolvePlayerStatsAsync(
                        roster.Player,
                        statPolicy,
                        equipmentProfile,
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
                        TrainingAnnexPersistenceController.BuildCurrentSaveSnapshot(
                            roster,
                            partyStock,
                            field,
                            playerBattleKnowledge.ToSnapshot(),
                            inventory.Snapshot,
                            wallet,
                            sessionProgress,
                            encounterTriggerConsumed,
                            preparedBattleStarted,
                            preparedBattleOutcome,
                            preparedBattleWinningTeamId),
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
                            partyStock,
                            partyTransitions,
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
                            appliedWalletTransaction,
                            shopTransactions,
                            shopEquipmentChanges,
                            shopOfferDiagnostics,
                            hospitalRestorations,
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
                            equipmentProfileResolver,
                            catalog,
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
                    field = await fieldPresenter.ApplyNavigationAsync(
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
                    field = await fieldPresenter.ApplyNavigationAsync(
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
                    field = await fieldPresenter.ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                                TrainingAnnexFieldPresenter.RequireDungeonTraversal(field),
                                TrainingAnnexHostSupport.EnterReviewHallTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.EnterReviewAlcove:
                    field = await fieldPresenter.ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                                TrainingAnnexFieldPresenter.RequireDungeonTraversal(field),
                                TrainingAnnexHostSupport.EnterReviewAlcoveTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ReturnToReviewHall:
                    field = await fieldPresenter.ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                                TrainingAnnexFieldPresenter.RequireDungeonTraversal(field),
                                TrainingAnnexHostSupport.ReturnToReviewHallTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ReturnToAnnexEntrance:
                    field = await fieldPresenter.ApplyDungeonTraversalAsync(
                        field,
                        dungeonTraversal.Traverse(
                                TrainingAnnexFieldPresenter.RequireDungeonTraversal(field),
                                TrainingAnnexHostSupport.ReturnToEntranceTransition),
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectTrainingBarrier:
                {
                    RuntimeDungeonTraversalResult traversal = dungeonTraversal.Traverse(
                        TrainingAnnexFieldPresenter.RequireDungeonTraversal(field),
                        TrainingAnnexHostSupport.InspectBarrierTransition);
                    field = await fieldPresenter.ApplyDungeonTraversalAsync(
                        field,
                        traversal,
                        cancellationToken).ConfigureAwait(false);
                    barrierRejected = traversal.Code == RuntimeDungeonTraversalCode.PolicyRejected;
                    break;
                }
                case CleanTrainingAnnexPlayCommand.UnlockReviewCheckpoint:
                    field = await fieldPresenter.ApplyDungeonStateChangeAsync(
                        field,
                        dungeonTraversal.UnlockCheckpoint(
                            TrainingAnnexFieldPresenter.RequireDungeonTraversal(field),
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
                            new BattleStatusLifecycleService(_randomSource),
                            equipmentProfileResolver)
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
                            await rewardApplicator.ApplyAsync(
                                roster.Player,
                                battle.RewardPreview,
                                growthServices,
                                economy,
                                wallet,
                                cancellationToken).ConfigureAwait(false);
                        if (rewardApplication.Applied)
                        {
                            wallet = rewardApplication.Wallet;
                            appliedWalletTransaction = rewardApplication.WalletTransaction;
                            appliedBattleReward = battle.RewardPreview;
                            appliedBattleRewardLevelUpCount = rewardApplication.Growth.LevelUps.Count;
                            growthApplied = true;
                            levelUpCount += rewardApplication.Growth.LevelUps.Count;
                            sessionProgress = TrainingAnnexBattleRewardApplicator.RecordSessionProgress(
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
                    ItemDefinition? item = itemSelection.SelectionIdentity?.ContentId is ContentId itemId
                        ? GetKnownFieldItems(catalog, inventory.Snapshot)
                            .FirstOrDefault(candidate => candidate.Id == itemId)
                        : null;
                    if (item is null)
                    {
                        await _eventSink.PublishAsync(
                            "Field item selection rejected; inventory and actor state are unchanged.",
                            cancellationToken).ConfigureAwait(false);
                        break;
                    }

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
                    TrainingAnnexFieldActionResult action = await fieldActions.UseItemAsync(
                        roster.Player,
                        item,
                        inventory,
                        cancellationToken).ConfigureAwait(false);
                    await PresentFieldActionAsync(
                            action,
                            item.DisplayName,
                            inventory.Snapshot,
                            cancellationToken,
                            item.Id)
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
                        TrainingAnnexSaveActionResult save = await persistence.SaveCurrentSessionAsync(
                            kind,
                            savePolicy,
                            catalog,
                            roster,
                            partyStock,
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
                        TrainingAnnexLoadActionResult loadResult = await persistence.LoadCurrentSessionAsync(
                            kind,
                            savePolicy,
                            catalog,
                            actorFactory,
                            roster,
                            partyStock,
                            field,
                            preparedEncounter is not null && !preparedBattleStarted,
                            cancellationToken).ConfigureAwait(false);
                        saveDiagnosticCount += loadResult.DiagnosticCount;
                        if (loadResult.Restored is TrainingAnnexRestoredSession restored)
                        {
                            roster = restored.Roster;
                            partyStock = restored.PartyStock;
                            field = restored.Field;
                            inventory = new TrainingAnnexItemActionInventory(restored.Inventory, inventoryTransitions);
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
                case CleanTrainingAnnexPlayCommand.OpenShop:
                {
                    var shopResult = await new TrainingAnnexShopController(_eventSink, _commandSource)
                        .OpenTrainingSupplyAsync(
                        catalog,
                        resourceManagement.Shop,
                        equipmentTransitions,
                        equipmentProfileResolver,
                        roster.Player,
                        inventory,
                        wallet,
                        commands,
                        cancellationToken).ConfigureAwait(false);
                    wallet = shopResult.Wallet;
                    shopTransactions.AddRange(shopResult.Transactions);
                    shopEquipmentChanges.AddRange(shopResult.EquipmentChanges);
                    shopOfferDiagnostics.AddRange(shopResult.OfferDiagnostics);
                    break;
                }
                case CleanTrainingAnnexPlayCommand.OpenRecoveryFacility:
                {
                    TrainingAnnexRecoveryFacilityResult recoveryResult =
                        await new TrainingAnnexRecoveryFacilityController(_eventSink, _commandSource)
                            .OpenAsync(
                                resourceManagement.Hospital,
                                roster.Player,
                                wallet,
                                commands,
                                cancellationToken).ConfigureAwait(false);
                    wallet = recoveryResult.Wallet;
                    hospitalRestorations.AddRange(recoveryResult.Restorations);
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
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenShop,
            "Training Supply"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenRecoveryFacility,
            "Recovery Facility"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.InspectParty,
            "Inspect Party"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.InspectStock,
            "Inspect Stock"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenPartyStockOperations,
            "Party / Stock Operations"));

        string locationLabel = locationId == TrainingAnnexHostSupport.StagingArea
            ? TrainingAnnexFieldPresenter.FieldLabel(locationId)
            : TrainingAnnexFieldPresenter.DungeonNodeLabel(field.DungeonTraversal?.CurrentNodeId);
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            $"Training Annex Clean Session - {locationLabel}",
            options);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreatePartyStockOperationMenu(
        RuntimePartyStockSnapshot party)
    {
        bool hasPersonaStock = party.PersonaStock.Any(persona =>
            persona.InstanceId == TrainingAnnexHostSupport.PersonaBrambleRunnerInstance);
        bool ashlingOwned = party.DemonStock.Any(demon =>
            demon.InstanceId == TrainingAnnexHostSupport.DemonAshlingInstance);
        bool ashlingActive = party.ActiveParty.Any(actor =>
            actor.InstanceId == TrainingAnnexHostSupport.DemonAshlingInstance);
        bool wardOwned = party.DemonStock.Any(demon =>
            demon.InstanceId == TrainingAnnexHostSupport.DemonWardShellInstance);
        bool wardActive = party.ActiveParty.Any(actor =>
            actor.InstanceId == TrainingAnnexHostSupport.DemonWardShellInstance);
        bool brambleOwned = party.DemonStock.Any(demon =>
            demon.InstanceId == TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance);
        bool activeDemon = party.ActiveParty.Any(actor =>
            party.DemonStock.Any(demon => demon.InstanceId == actor.InstanceId));

        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Clean Party / Stock Operations",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartySwapActiveForm,
                    "Swap Active Form",
                    hasPersonaStock,
                    "Exchanges the active form with the Persona stock entry."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartySummonAshling,
                    "Summon Ashling",
                    ashlingOwned && !ashlingActive && party.ActiveParty.Count < party.MaxActivePartySize,
                    "Adds the owned Ashling to the active party while keeping it in Demon stock."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartySwapActiveDemon,
                    "Swap Active Demon to Ward Shell",
                    ashlingActive && wardOwned && !wardActive,
                    "Replaces the active Ashling with owned Ward Shell."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyReturnActiveDemon,
                    "Return Active Demon",
                    activeDemon,
                    "Removes the active demon from the party while keeping it owned."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyReplaceWardShell,
                    "Replace Ward Shell with Bramble Runner",
                    wardOwned && !brambleOwned,
                    "Replaces an owned Ward Shell with a prepared Bramble Runner candidate."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyDismissAshling,
                    "Dismiss Ashling",
                    ashlingOwned,
                    "Removes Ashling from the active party and Demon stock."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyConsumeBrambleRunner,
                    "Consume Bramble Runner",
                    brambleOwned,
                    "Consumes Bramble Runner from active party and Demon stock."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);
    }

    private static TrainingAnnexPartyOperation ToPartyOperation(CleanTrainingAnnexPlayCommand command) =>
        command switch
        {
            CleanTrainingAnnexPlayCommand.PartySwapActiveForm => TrainingAnnexPartyOperation.SwapActiveForm,
            CleanTrainingAnnexPlayCommand.PartySummonAshling => TrainingAnnexPartyOperation.SummonAshling,
            CleanTrainingAnnexPlayCommand.PartySwapActiveDemon => TrainingAnnexPartyOperation.SwapActiveDemonToWardShell,
            CleanTrainingAnnexPlayCommand.PartyReturnActiveDemon => TrainingAnnexPartyOperation.ReturnActiveDemon,
            CleanTrainingAnnexPlayCommand.PartyReplaceWardShell => TrainingAnnexPartyOperation.ReplaceWardShellWithBrambleRunner,
            CleanTrainingAnnexPlayCommand.PartyDismissAshling => TrainingAnnexPartyOperation.DismissAshling,
            CleanTrainingAnnexPlayCommand.PartyConsumeBrambleRunner => TrainingAnnexPartyOperation.ConsumeBrambleRunner,
            _ => throw new InvalidOperationException($"'{command}' is not a Training Annex party operation.")
        };

    private static string PartyOperationName(TrainingAnnexPartyOperation operation) =>
        operation switch
        {
            TrainingAnnexPartyOperation.SwapActiveForm => "swap_active_form",
            TrainingAnnexPartyOperation.SummonAshling => "summon_demon",
            TrainingAnnexPartyOperation.SwapActiveDemonToWardShell => "swap_active_demon",
            TrainingAnnexPartyOperation.ReturnActiveDemon => "return_active_demon",
            TrainingAnnexPartyOperation.ReplaceWardShellWithBrambleRunner => "replace_demon",
            TrainingAnnexPartyOperation.DismissAshling => "dismiss_demon",
            TrainingAnnexPartyOperation.ConsumeBrambleRunner => "consume_demon",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported party operation.")
        };

    private static RuntimeInventorySnapshot BuildInitialInventory(
        RuntimeInventorySnapshot? supplied,
        IInventoryTransitionService inventoryTransitions)
    {
        RuntimeInventorySnapshot inventory = supplied ?? new RuntimeInventorySnapshot(
            [KeyValuePair.Create(TrainingAnnexHostSupport.AnnexTonic, 1)]);

        inventory = AddEquipmentIfMissing(
            inventory,
            inventoryTransitions,
            TrainingAnnexHostSupport.PracticeBlade,
            EquipmentSlot.Weapon);
        inventory = AddEquipmentIfMissing(
            inventory,
            inventoryTransitions,
            TrainingAnnexHostSupport.FocusCharm,
            EquipmentSlot.Accessory);

        return inventory;
    }

    private static RuntimeInventorySnapshot AddEquipmentIfMissing(
        RuntimeInventorySnapshot inventory,
        IInventoryTransitionService inventoryTransitions,
        ContentId equipmentId,
        EquipmentSlot slot)
    {
        if (inventory.OwnsEquipment(equipmentId, slot))
        {
            return inventory;
        }

        InventoryTransitionResult result = inventoryTransitions.AddEquipment(inventory, equipmentId, slot);
        return result.Applied ? result.After : inventory;
    }

    private static RuntimeEquipmentSnapshot BuildInitialEquipment(
        GameDataCatalog catalog,
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot? supplied,
        IEquipmentTransitionService equipmentTransitions)
    {
        if (supplied is not null)
        {
            return EquipRequestedEquipment(catalog, inventory, supplied, equipmentTransitions);
        }

        RuntimeEquipmentSnapshot equipment = new();
        equipment = EquipIfOwned(
            catalog,
            inventory,
            equipmentTransitions,
            equipment,
            TrainingAnnexHostSupport.PracticeBlade);
        equipment = EquipIfOwned(
            catalog,
            inventory,
            equipmentTransitions,
            equipment,
            TrainingAnnexHostSupport.FocusCharm);
        return equipment;
    }

    private static RuntimeEquipmentSnapshot EquipRequestedEquipment(
        GameDataCatalog catalog,
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot requested,
        IEquipmentTransitionService equipmentTransitions)
    {
        RuntimeEquipmentSnapshot equipment = new();
        foreach ((EquipmentSlot slot, ContentId equipmentId) in requested.EquippedItemIds.OrderBy(pair => pair.Key))
        {
            if (!catalog.TryGetEquipment(equipmentId, out EquipmentDefinition? definition) || definition is null)
            {
                continue;
            }

            EquipmentTransitionResult result = equipmentTransitions.Equip(
                inventory,
                equipment,
                equipmentId,
                definition.Slot,
                slot);
            if (result.Applied)
            {
                equipment = result.After;
            }
        }

        return equipment;
    }

    private static RuntimeEquipmentSnapshot EquipIfOwned(
        GameDataCatalog catalog,
        RuntimeInventorySnapshot inventory,
        IEquipmentTransitionService equipmentTransitions,
        RuntimeEquipmentSnapshot equipment,
        ContentId equipmentId)
    {
        EquipmentDefinition definition = catalog.GetRequiredEquipment(equipmentId);
        EquipmentTransitionResult result = equipmentTransitions.Equip(
            inventory,
            equipment,
            equipmentId,
            definition.Slot,
            definition.Slot);
        return result.Applied ? result.After : equipment;
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
        List<HostCommandOption<CleanTrainingAnnexPlayCommand>> options = GetKnownFieldItems(catalog, inventory)
            .Select(item => new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.SelectFieldItem,
                $"{item.DisplayName} x{inventory.GetQuantity(item.Id)}",
                Description: item.Description,
                SelectionIdentity: HostCommandSelectionIdentity.ForContent(item.Id)))
            .ToList();
        if (options.Count == 0)
        {
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.SelectFieldItem,
                "No usable field items",
                false));
        }

        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.Back,
            "Back"));
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Clean Inventory",
            options);
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

    private ValueTask PrintInventoryAsync(
        GameDataCatalog catalog,
        RuntimeInventorySnapshot inventory,
        CancellationToken cancellationToken)
    {
        string summary = string.Join(
            ", ",
            inventory.ItemQuantities
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                .Select(pair =>
                {
                    if (catalog.TryGetItem(pair.Key, out ItemDefinition? item) && item is not null)
                    {
                        return $"{item.DisplayName} x{pair.Value}";
                    }

                    return $"{pair.Key} x{pair.Value}";
                }));
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "empty";
        }

        return _eventSink.PublishAsync(
            $"Inventory: {summary}.",
            cancellationToken);
    }

    private async ValueTask PresentFieldActionAsync(
        TrainingAnnexFieldActionResult action,
        string displayName,
        RuntimeInventorySnapshot inventory,
        CancellationToken cancellationToken,
        ContentId? inventoryItemId = null)
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

        string inventorySummary = inventoryItemId is ContentId itemId
            ? $"inventory {itemId} x{inventory.GetQuantity(itemId)}"
            : "inventory unchanged";
        await _eventSink.PublishAsync(
            $"Field action executed: {displayName}; HP {action.HpBefore.Current}->{action.HpAfter.Current}/{action.HpAfter.Maximum}; SP {action.SpBefore.Current}->{action.SpAfter.Current}/{action.SpAfter.Maximum}; {inventorySummary}.",
            cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<ItemDefinition> GetKnownFieldItems(
        GameDataCatalog catalog,
        RuntimeInventorySnapshot inventory)
    {
        return inventory.ItemQuantities
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => catalog.TryGetItem(pair.Key, out ItemDefinition? item) ? item : null)
            .OfType<ItemDefinition>()
            .Where(IsFieldUsableItem)
            .ToArray();
    }

    private static bool IsFieldUsableItem(ItemDefinition item) =>
        item.Usage?.ContextIds.Contains(Field) == true;

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
        RuntimePartyStockSnapshot partyStock,
        IReadOnlyList<TrainingAnnexPartyTransitionEvidence> partyTransitions,
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
        WalletTransactionResult? appliedWalletTransaction,
        IReadOnlyList<TrainingAnnexShopTransactionEvidence> shopTransactions,
        IReadOnlyList<TrainingAnnexEquipmentChangeEvidence> shopEquipmentChanges,
        IReadOnlyList<RuntimeShopOfferResolutionDiagnostic> shopOfferDiagnostics,
        IReadOnlyList<TrainingAnnexHospitalRestorationEvidence> hospitalRestorations,
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
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        IEquipmentDefinitionRepository equipmentRepository,
        IReadOnlyList<ContentId> executedFieldActionIds,
        int cancelledFieldTargetSelections,
        IReadOnlyList<CleanTrainingAnnexPlayCommand> commands)
    {
        CatalogBattleActor player = roster.Player.Actor;
        RuntimeActorSnapshot playerSnapshot = roster.Player.Actor.State.ToSnapshot();
        RuntimeEquipmentSnapshot equipment = playerSnapshot.Equipment;
        return new(
            [request.ManifestPath],
            request.DocumentPaths,
            player.Entity.Id,
            roster.Player.Level,
            roster.AllActors.Count,
            roster.Enemies.Count,
            roster.AllActors.Select(actor => actor.Actor.Entity.Id).ToArray(),
            roster.AllActors.Select(actor => actor.Actor.State.InstanceId).ToArray(),
            partyStock,
            partyTransitions.ToArray(),
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
            appliedWalletTransaction,
            shopTransactions.ToArray(),
            shopEquipmentChanges.ToArray(),
            shopOfferDiagnostics.ToArray(),
            hospitalRestorations.ToArray(),
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
            equipment,
            equipmentProfileResolver.Resolve(equipment, equipmentRepository),
            executedFieldActionIds.ToArray(),
            cancelledFieldTargetSelections,
            commands.ToArray());
    }

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
        RuntimeEquipmentProfile equipmentProfile,
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
                activeFormStats,
                equipmentStatModifiers: equipmentProfile.StatModifiers));
            StatResolutionResult boosted = statPolicy.Resolve(new StatResolutionRequest(
                snapshot.Identity.ActorKindId,
                statId,
                snapshot.Stats.BaseStats,
                activeFormStats,
                equipmentStatModifiers: equipmentProfile.StatModifiers,
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
