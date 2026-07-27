using Convergence.Content;
using Convergence.Catalog;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Fusion;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal enum CleanTrainingAnnexPlayCommand
{
    InspectSession,
    InspectActor,
    InspectParty,
    InspectRoster,
    OpenPartyRosterOperations,
    PartySelectActiveHostedEntity,
    PartyDeployAshling,
    PartySwapDeployedCompanion,
    PartyRecallActiveCompanion,
    PartyReplaceWardShell,
    PartyDismissAshling,
    PartyConsumeBrambleRunner,
    OpenNegotiation,
    SelectNegotiationTarget,
    SelectNegotiationAnswer,
    SelectNegotiationDemand,
    ResolveStats,
    RecalculateResources,
    ApplyVictoryExperience,
    SelectSkillToReplace,
    ForgetPendingSkill,
    DeferPendingSkillChoice,
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
    CalculateFusionResults,
    PreviewFusionResult,
    SelectFusionInheritedSkill,
    BuildFusionPreview,
    ConfirmFusionPreview,
    CommitFusionTransaction,
    ConfirmFusionTransaction,
    OpenCompendium,
    CompendiumRegister,
    CompendiumRecall,
    SelectCompendiumActor,
    SelectCompendiumEntry,
    Back,
    Exit
}

internal sealed record CleanTrainingAnnexPlaySummary(
    IReadOnlyList<string> RequestedManifestPaths,
    IReadOnlyList<string> RequestedDocumentPaths,
    ContentId PlayerEntityId,
    ContentId PlayerActorKindId,
    int PlayerLevel,
    int ActorCount,
    int EnemyActorCount,
    IReadOnlyList<ContentId> ActorEntityIds,
    IReadOnlyList<RuntimeInstanceId> ActorInstanceIds,
    RuntimePartyRosterSnapshot PartyRoster,
    IReadOnlyList<TrainingAnnexPartyTransitionEvidence> PartyTransitions,
    IReadOnlyList<TrainingAnnexNegotiationEvidence> Negotiations,
    IReadOnlyList<TrainingAnnexFusionResultEvidence> FusionResults,
    IReadOnlyList<TrainingAnnexFusionPlanningEvidence> FusionPlanning,
    IReadOnlyList<TrainingAnnexFusionPreviewEvidence> FusionPreviews,
    IReadOnlyList<TrainingAnnexFusionTransactionEvidence> FusionTransactions,
    CompendiumStateSnapshot Compendium,
    IReadOnlyList<TrainingAnnexCompendiumEvidence> CompendiumEvidence,
    IReadOnlyList<RuntimeResourceSnapshot> PlayerResources,
    RuntimeStatBlockSnapshot PlayerStats,
    RuntimeProgressionSnapshot PlayerProgression,
    RuntimeProgressionSnapshot? ActiveHostedEntityProgression,
    RuntimeSkillStateSnapshot? ActiveHostedEntitySkills,
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
    IReadOnlyList<TrainingAnnexTurnEconomyEvidence> TurnEconomyEvidence,
    IReadOnlyList<TrainingAnnexLifecycleEvidence> LifecycleEvidence,
    IReadOnlyList<TrainingAnnexAiDecisionEvidence> AiDecisionEvidence,
    IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> BattleKnowledgeEvidence,
    IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> EncounterAiKnowledgeEvidence,
    RuntimeKnowledgeSnapshot BattleKnowledge,
    RuntimeEncounterKnowledgeSnapshot EncounterAiKnowledge,
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
    private readonly Func<
        ISkillDefinitionRepository,
        IStatResolutionPolicy,
        IResourceGrowthPolicy,
        IRosterCapacityPolicy,
        IRuntimeActorCombatProfileCompositionService>
        _combatProfileCompositionFactory;

    internal CleanTrainingAnnexPlayHost(
        IContentPackTextSource contentSource,
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        IRandomSource? randomSource = null,
        TrainingAnnexSaveSlotStore? saveSlots = null,
        RuntimeInventorySnapshot? initialInventory = null,
        RuntimeEquipmentSnapshot? initialEquipment = null,
        RuntimeWalletSnapshot? initialWallet = null,
        Func<
            ISkillDefinitionRepository,
            IStatResolutionPolicy,
            IResourceGrowthPolicy,
            IRosterCapacityPolicy,
            IRuntimeActorCombatProfileCompositionService>?
            combatProfileCompositionFactory = null)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
        _randomSource = randomSource ?? new TrainingAnnexMinimumRandomSource();
        _saveSlots = saveSlots ?? new TrainingAnnexSaveSlotStore();
        _initialInventory = initialInventory;
        _initialEquipment = initialEquipment;
        _initialWallet = initialWallet;
        _combatProfileCompositionFactory = combatProfileCompositionFactory ??
            ((skills, stats, resources, rosterCapacity) =>
                new RuntimeActorCombatProfileCompositionService(
                    stats,
                    resources,
                    skills,
                    rosterCapacity));
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
        var rulesetResolver = new RuntimeRulesetBindingResolver(
            RuntimeRulesetPolicyFactoryRegistry.CreateStandard());
        RulesetBindingResult<StatRulesetServices> statBinding =
            rulesetResolver.BindStatServices(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_stat"));
        if (!statBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("stat", statBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        StatRulesetServices statServices = statBinding.RequireService();
        RulesetBindingResult<IStatModifierPolicyService> statModifierBinding =
            rulesetResolver.BindStatModifierPolicy(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_stat_modifiers"));
        if (!statModifierBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync(
                    "stat_modifier",
                    statModifierBinding.Diagnostics,
                    cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        IStatModifierPolicyService statModifiers = statModifierBinding.RequireService();
        RulesetBindingResult<CombatExecutionPolicySet> combatBinding =
            rulesetResolver.BindCombatPolicies(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_damage"),
                _randomSource,
                statServices.StageScalingPolicy);
        if (!combatBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("damage", combatBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        CombatExecutionPolicySet combatPolicies = combatBinding.RequireService();
        RulesetBindingResult<IBattleRewardService> rewardBinding =
            rulesetResolver.BindBattleRewardService(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_reward"),
                _randomSource);
        if (!rewardBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("reward", rewardBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        IBattleRewardService rewardService = rewardBinding.RequireService();
        RulesetBindingResult<BattleTurnEconomyRuleset> turnEconomyBinding =
            rulesetResolver.BindTurnEconomy(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_action_token"));
        if (!turnEconomyBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("action_token", turnEconomyBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        BattleTurnEconomyRuleset turnEconomy = turnEconomyBinding.RequireService();
        RulesetBindingResult<IRosterCapacityPolicy> rosterCapacityBinding =
            rulesetResolver.BindRosterCapacityPolicy(
                catalog,
                TrainingAnnexHostSupport.Qualified("standard_roster_capacity"));
        if (!rosterCapacityBinding.IsSuccess)
        {
            await PublishRulesetDiagnosticsAsync("roster_capacity", rosterCapacityBinding.Diagnostics, cancellationToken)
                .ConfigureAwait(false);
            return 4;
        }

        IRosterCapacityPolicy rosterCapacityPolicy = rosterCapacityBinding.RequireService();

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
            TrainingAnnexHostSupport.CreateExecutionServices(catalog, combatPolicies, statModifiers);
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
        IStatResolutionPolicy statPolicy = statServices.StatResolutionPolicy;
        IRuntimeActorCombatProfileCompositionService combatProfileCompositionService =
            _combatProfileCompositionFactory(
                catalog,
                statPolicy,
                growthServices.ResourceGrowthPolicy,
                rosterCapacityPolicy) ??
            throw new InvalidOperationException("The combat-profile composition factory returned no service.");
        var navigation = new RuntimeNavigationService(new TrainingAnnexNavigationPolicy());
        var dungeonTraversal = new RuntimeDungeonTraversalService(new TrainingAnnexDungeonPolicy());
        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new TrainingAnnexResourceInitializationPolicy(growthServices.ResourceGrowthPolicy),
            catalog,
            combatProfileCompositionService);
        var encounterPreparation = new CatalogEncounterPreparationService(
            new CatalogEncounterStartPlanner(catalog),
            actorFactory);
        var fieldActions = new TrainingAnnexFieldActionAdapter(
            executionServices,
            catalog);
        var fieldPresenter = new TrainingAnnexFieldPresenter(_eventSink);
        var rewardApplicator = new TrainingAnnexBattleRewardApplicator(_eventSink, _randomSource);
        IInventoryTransitionService inventoryTransitions = resourceManagement.Inventory;
        IEquipmentTransitionService equipmentTransitions = resourceManagement.Equipment;
        IEconomyTransactionService economy = resourceManagement.Economy;
        var equipmentProfileResolver = new RuntimeEquipmentProfileResolver();
        IPartyRosterTransitionService partyRosterTransitions = new PartyRosterTransitionService(
            rosterCapacityPolicy);
        IFusionTransactionService fusionTransactionService = new FusionTransactionService(
            actorFactory,
            partyRosterTransitions);
        var partyController = new TrainingAnnexPartyController(partyRosterTransitions);
        var compendiumRuntime = new CompendiumRuntimeService(
            catalog,
            catalog,
            actorFactory,
            growthServices.ResourceGrowthPolicy,
            compendium: new CompendiumService(new LinearCompendiumRecallPricingPolicy(
                defaultBasePrice: 2000,
                levelFactor: 100,
                statPointFactor: 50,
                skillFactor: 200)),
            partyRoster: partyRosterTransitions,
            economy: economy);
        var familiarKnowledge = new FamiliarEntityKnowledgeService(
            catalog,
            new StandardFamiliarKnowledgeImportPolicy());
        var acquisitionRegistrar = new TrainingAnnexAcquisitionRegistrar(
            compendiumRuntime,
            familiarKnowledge,
            _eventSink);
        TrainingAnnexPartySetupResult partySetup = partyController.CreateInitialParty(roster);
        RuntimePartyRosterSnapshot partyRoster = partySetup.Snapshot;
        var partyTransitions = new List<TrainingAnnexPartyTransitionEvidence>(partySetup.Transitions);
        var negotiations = new List<TrainingAnnexNegotiationEvidence>();
        var fusionResults = new List<TrainingAnnexFusionResultEvidence>();
        var fusionPlanning = new List<TrainingAnnexFusionPlanningEvidence>();
        var fusionPreviews = new List<TrainingAnnexFusionPreviewEvidence>();
        var fusionTransactions = new List<TrainingAnnexFusionTransactionEvidence>();
        CompendiumStateSnapshot compendium = new();
        var compendiumEvidence = new List<TrainingAnnexCompendiumEvidence>();
        var recruitedThisSession = new HashSet<ContentId>();
        var inventory = new TrainingAnnexItemActionInventory(
            BuildInitialInventory(_initialInventory, inventoryTransitions),
            inventoryTransitions);
        roster.Player.Actor.State.ReplaceEquipment(BuildInitialEquipment(
            catalog,
            inventory.Snapshot,
            _initialEquipment,
            equipmentTransitions));
        if (!await ComposePlayerStateAsync(
                roster,
                partyRoster,
                combatProfileCompositionService,
                equipmentProfileResolver,
                catalog,
                cancellationToken,
                initializeResourcesToMaximum: true).ConfigureAwait(false))
        {
            return 4;
        }
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
        var persistence = new TrainingAnnexPersistenceController(
            _saveSlots,
            _eventSink,
            rulesetResolver,
            rosterCapacityPolicy);
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
        var turnEconomyEvidence = new List<TrainingAnnexTurnEconomyEvidence>();
        var lifecycleEvidence = new List<TrainingAnnexLifecycleEvidence>();
        var aiDecisionEvidence = new List<TrainingAnnexAiDecisionEvidence>();
        var playerBattleKnowledge = new TrainingAnnexBattleKnowledgeState();
        var battleKnowledgeEvidence = new List<TrainingAnnexBattleKnowledgeEvidence>();
        var encounterAiKnowledgeEvidence = new List<TrainingAnnexBattleKnowledgeEvidence>();
        RuntimeEncounterKnowledgeSnapshot lastEncounterAiKnowledge =
            RuntimeEncounterKnowledgeSnapshot.Empty;
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
            $"Loaded {TrainingAnnexHostSupport.PackId} through the clean catalog pipeline.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Hydrated {player.Entity.DisplayName} at level {player.Entity.BaseLevel}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Hydrated clean actor roster with {roster.AllActors.Count} actor(s): {roster.Enemies.Count} enemy model(s).",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Party setup: {partyRoster.ActiveParty.Count} active, {partyRoster.ReserveMembers.Count} reserve.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Roster setup: active hosted entity {(partyRoster.ActiveHostedEntity is null ? 0 : 1)}, Hosted Entity roster {partyRoster.HostedEntityRoster.Count}, Companion roster {partyRoster.CompanionRoster.Count}.",
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
                    partyRoster,
                    partyTransitions,
                    negotiations,
                    fusionResults,
                    fusionPlanning,
                    fusionPreviews,
                    fusionTransactions,
                    compendium,
                    compendiumEvidence,
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
                    turnEconomyEvidence,
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
            bool composeAfterCommand = true;
            switch (command)
            {
                case CleanTrainingAnnexPlayCommand.InspectSession:
                    await fieldPresenter.PrintSessionAsync(catalog, field, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectActor:
                    await PrintActorsAsync(roster, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectParty:
                    await partyController.PrintPartyAsync(partyRoster, _eventSink, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectRoster:
                    await partyController.PrintRosterAsync(partyRoster, _eventSink, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.OpenPartyRosterOperations:
                    {
                        await partyController.PrintPartyAsync(partyRoster, _eventSink, cancellationToken)
                            .ConfigureAwait(false);
                        await partyController.PrintRosterAsync(partyRoster, _eventSink, cancellationToken)
                            .ConfigureAwait(false);
                        HostCommandReadResult<CleanTrainingAnnexPlayCommand> operationSelection =
                            await _commandSource.ReadAsync(
                                CreatePartyRosterOperationMenu(partyRoster),
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
                        PartyRosterTransitionResult operationResult = partyController.ExecuteOperation(
                            operation,
                            partyRoster,
                            roster);
                        bool committed = false;
                        if (operationResult.Applied)
                        {
                            committed = await ComposePlayerStateAsync(
                                    roster,
                                    operationResult.After,
                                    combatProfileCompositionService,
                                    equipmentProfileResolver,
                                    catalog,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            if (committed)
                            {
                                partyRoster = operationResult.After;
                            }
                            else
                            {
                                await _eventSink.PublishAsync(
                                    $"Party roster operation not committed: {operationName}; player stat composition was rejected.",
                                    cancellationToken).ConfigureAwait(false);
                            }
                        }

                        partyTransitions.Add(TrainingAnnexPartyTransitionEvidence.From(
                            operationName,
                            operationResult,
                            committed));
                        if (!operationResult.Applied || committed)
                        {
                            await partyController.PrintOperationAsync(
                                operationName,
                                operationResult,
                                _eventSink,
                                cancellationToken).ConfigureAwait(false);
                        }

                        composeAfterCommand = false;

                        break;
                    }
                case CleanTrainingAnnexPlayCommand.OpenNegotiation:
                    {
                        TrainingAnnexNegotiationInteractionResult negotiation =
                            await new TrainingAnnexNegotiationController(
                                    _eventSink,
                                    _commandSource,
                                    _randomSource)
                                .OpenAsync(
                                    catalog,
                                    roster,
                                    partyRoster,
                                    wallet,
                                    economy,
                                    recruitedThisSession,
                                    commands,
                                    cancellationToken).ConfigureAwait(false);
                        partyRoster = negotiation.PartyRoster;
                        wallet = negotiation.Wallet;
                        negotiations.AddRange(negotiation.Evidence);
                        foreach (TrainingAnnexNegotiationEvidence recruited in
                                 negotiation.Evidence.Where(evidence => evidence.Recruited))
                        {
                            TrainingAnnexRuntimeActor acquiredActor = roster.AllActors.FirstOrDefault(actor =>
                                    actor.Actor.State.InstanceId == recruited.TargetInstanceId)
                                ?? throw new InvalidOperationException(
                                    $"Recruited runtime actor '{recruited.TargetInstanceId}' is not in the host roster.");
                            TrainingAnnexAcquisitionRegistrationResult acquisition =
                                await acquisitionRegistrar.RecordAsync(
                                    compendium,
                                    playerBattleKnowledge,
                                    acquiredActor,
                                    partyRoster,
                                    wallet,
                                    TrainingAnnexHostSupport.NegotiationAcquisitionSource,
                                    cancellationToken).ConfigureAwait(false);
                            compendium = acquisition.Compendium;
                            playerBattleKnowledge = acquisition.PlayerKnowledge;
                            compendiumEvidence.Add(acquisition.Evidence);
                        }
                        break;
                    }
                case CleanTrainingAnnexPlayCommand.CalculateFusionResults:
                    {
                        TrainingAnnexFusionCalculationResult calculated =
                            await new TrainingAnnexFusionController(_eventSink)
                                .CalculateAsync(catalog, roster, cancellationToken).ConfigureAwait(false);
                        fusionResults.AddRange(calculated.Results);
                        fusionPlanning.AddRange(calculated.Planning);
                        break;
                    }
                case CleanTrainingAnnexPlayCommand.PreviewFusionResult:
                    {
                        TrainingAnnexFusionPreviewEvidence? preview =
                            await new TrainingAnnexFusionController(_eventSink)
                                .PreviewAsync(
                                    catalog,
                                    roster,
                                    _commandSource,
                                    commands,
                                    cancellationToken).ConfigureAwait(false);
                        if (preview is not null)
                        {
                            fusionPreviews.Add(preview);
                        }

                        break;
                    }
                case CleanTrainingAnnexPlayCommand.CommitFusionTransaction:
                    {
                        TrainingAnnexFusionTransactionResult transaction =
                            await new TrainingAnnexFusionController(_eventSink)
                                .CommitAsync(
                                     catalog,
                                     roster,
                                     partyRoster,
                                     fusionTransactionService,
                                     _commandSource,
                                    commands,
                                    cancellationToken).ConfigureAwait(false);
                        partyRoster = transaction.PartyRoster;
                        fusionTransactions.Add(transaction.Evidence);
                        if (transaction.ResultActor is not null)
                        {
                            roster = roster.WithDynamicMember(transaction.ResultActor);
                            TrainingAnnexAcquisitionRegistrationResult acquisition =
                                await acquisitionRegistrar.RecordAsync(
                                    compendium,
                                    playerBattleKnowledge,
                                    transaction.ResultActor,
                                    partyRoster,
                                    wallet,
                                    TrainingAnnexHostSupport.FusionAcquisitionSource,
                                    cancellationToken).ConfigureAwait(false);
                            compendium = acquisition.Compendium;
                            playerBattleKnowledge = acquisition.PlayerKnowledge;
                            compendiumEvidence.Add(acquisition.Evidence);
                        }

                        break;
                    }
                case CleanTrainingAnnexPlayCommand.OpenCompendium:
                    {
                        TrainingAnnexCompendiumInteractionResult interaction =
                            await new TrainingAnnexCompendiumController(
                                    _eventSink,
                                    _commandSource,
                                    compendiumRuntime,
                                    familiarKnowledge)
                                .OpenAsync(
                                    compendium,
                                    partyRoster,
                                    wallet,
                                    roster,
                                    playerBattleKnowledge,
                                    commands,
                                    cancellationToken).ConfigureAwait(false);
                        compendium = interaction.Compendium;
                        partyRoster = interaction.PartyRoster;
                        wallet = interaction.Wallet;
                        roster = interaction.Roster;
                        playerBattleKnowledge = interaction.PlayerKnowledge;
                        compendiumEvidence.AddRange(interaction.Evidence);
                        break;
                    }
                case CleanTrainingAnnexPlayCommand.ResolveStats:
                    statPreview = await ResolvePlayerStatsAsync(
                        roster.Player,
                        statPolicy,
                        statServices.StageScalingPolicy,
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
                    (LevelGrowthResult growth, RuntimeActorGrowthCompositionResult? growthTransaction) =
                        await ApplyVictoryExperienceAsync(
                        roster,
                        partyRoster,
                        growthServices,
                        combatProfileCompositionService,
                        equipmentProfileResolver,
                        catalog,
                        cancellationToken).ConfigureAwait(false);
                    bool growthCommitted = growthTransaction?.Applied == true;
                    growthApplied = growthCommitted;
                    levelUpCount = growthCommitted ? growth.LevelUps.Count : 0;
                    if (growthCommitted)
                    {
                        await ResolvePendingSkillChoicesAsync(
                            roster,
                            partyRoster,
                            combatProfileCompositionService,
                            equipmentProfileResolver,
                            catalog,
                            commands,
                            cancellationToken).ConfigureAwait(false);
                    }
                    composeAfterCommand = false;
                    break;
                case CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot:
                    RuntimeSaveValidationResult validation = new RuntimeSaveValidator(
                        rosterCapacityPolicy,
                        rulesetBindings: rulesetResolver,
                        chargePolicies: ChargePolicyRegistry.CreateStandard()).Validate(
                        TrainingAnnexPersistenceController.BuildCurrentSaveSnapshot(
                            roster,
                            partyRoster,
                            field,
                            playerBattleKnowledge.ToSnapshot(),
                            inventory.Snapshot,
                            wallet,
                            sessionProgress,
                            encounterTriggerConsumed,
                            preparedBattleStarted,
                            preparedBattleOutcome,
                            preparedBattleWinningTeamId,
                            compendium),
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
                            partyRoster,
                            partyTransitions,
                            negotiations,
                            fusionResults,
                            fusionPlanning,
                            fusionPreviews,
                            fusionTransactions,
                            compendium,
                            compendiumEvidence,
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
                            turnEconomyEvidence,
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
                                turnEconomy,
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
                        turnEconomyEvidence.AddRange(battle.TurnEconomyEvidence);
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
                                    roster,
                                    partyRoster,
                                    battle.RewardPreview,
                                    growthServices,
                                    combatProfileCompositionService,
                                    catalog,
                                    equipmentProfileResolver.Resolve(
                                        roster.Player.Actor.State.Equipment,
                                        catalog),
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
                                partyRoster,
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
                                cancellationToken,
                                compendium).ConfigureAwait(false);
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
                                equipmentProfileResolver,
                                roster,
                                partyRoster,
                                field,
                                preparedEncounter is not null && !preparedBattleStarted,
                                cancellationToken).ConfigureAwait(false);
                            saveDiagnosticCount += loadResult.DiagnosticCount;
                            if (loadResult.Restored is TrainingAnnexRestoredSession restored)
                            {
                                roster = restored.Roster;
                                partyRoster = restored.PartyRoster;
                                field = restored.Field;
                                inventory = new TrainingAnnexItemActionInventory(restored.Inventory, inventoryTransitions);
                                wallet = restored.Wallet;
                                sessionProgress = restored.SessionProgress;
                                compendium = restored.Compendium;
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
                                composeAfterCommand = false;
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
                            await new TrainingAnnexRecoveryFacilityController(
                                    _eventSink,
                                    _commandSource,
                                    statModifiers)
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

            if (composeAfterCommand && !await ComposePlayerStateAsync(
                    roster,
                    partyRoster,
                    combatProfileCompositionService,
                    equipmentProfileResolver,
                    catalog,
                    cancellationToken).ConfigureAwait(false))
            {
                return 4;
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
            CleanTrainingAnnexPlayCommand.InspectRoster,
            "Inspect Roster"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenPartyRosterOperations,
            "Party / Roster Operations"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenNegotiation,
            "Negotiate / Recruit"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.CalculateFusionResults,
            "Calculate Fusion Results"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.PreviewFusionResult,
            "Preview Fusion Result"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.CommitFusionTransaction,
            "Commit Fusion Transaction"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.OpenCompendium,
            "Compendium"));

        string locationLabel = locationId == TrainingAnnexHostSupport.StagingArea
            ? TrainingAnnexFieldPresenter.FieldLabel(locationId)
            : TrainingAnnexFieldPresenter.DungeonNodeLabel(field.DungeonTraversal?.CurrentNodeId);
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            $"Training Annex Clean Session - {locationLabel}",
            options);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreatePartyRosterOperationMenu(
        RuntimePartyRosterSnapshot party)
    {
        bool hasHostedEntityRoster = party.HostedEntityRoster.Any(hostedEntity =>
            hostedEntity.InstanceId == TrainingAnnexHostSupport.HostedBrambleRunnerInstance);
        bool ashlingOwned = party.CompanionRoster.Any(companion =>
            companion.InstanceId == TrainingAnnexHostSupport.CompanionAshlingInstance);
        bool ashlingActive = party.ActiveParty.Any(actor =>
            actor.InstanceId == TrainingAnnexHostSupport.CompanionAshlingInstance);
        bool wardOwned = party.CompanionRoster.Any(companion =>
            companion.InstanceId == TrainingAnnexHostSupport.CompanionWardShellInstance);
        bool wardActive = party.ActiveParty.Any(actor =>
            actor.InstanceId == TrainingAnnexHostSupport.CompanionWardShellInstance);
        bool brambleOwned = party.CompanionRoster.Any(companion =>
            companion.InstanceId == TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance);
        bool activeCompanion = party.ActiveParty.Any(actor =>
            party.CompanionRoster.Any(companion => companion.InstanceId == actor.InstanceId));

        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Clean Party / Roster Operations",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartySelectActiveHostedEntity,
                    "Swap Active Hosted Entity",
                    hasHostedEntityRoster,
                    "Exchanges the active hosted entity with the Hosted Entity roster entry."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyDeployAshling,
                    "Deploy Ashling",
                    ashlingOwned && !ashlingActive && party.ActiveParty.Count < party.MaxActivePartySize,
                    "Adds the owned Ashling to the active party while keeping it in Companion roster."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartySwapDeployedCompanion,
                    "Swap Active Companion to Ward Shell",
                    ashlingActive && wardOwned && !wardActive,
                    "Replaces the active Ashling with owned Ward Shell."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyRecallActiveCompanion,
                    "Return Active Companion",
                    activeCompanion,
                    "Removes the active companion from the party while keeping it owned."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyReplaceWardShell,
                    "Replace Ward Shell with Bramble Runner",
                    wardOwned && !brambleOwned,
                    "Replaces an owned Ward Shell with a prepared Bramble Runner candidate."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyDismissAshling,
                    "Dismiss Ashling",
                    ashlingOwned,
                    "Removes Ashling from the active party and Companion roster."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.PartyConsumeBrambleRunner,
                    "Consume Bramble Runner",
                    brambleOwned,
                    "Consumes Bramble Runner from active party and Companion roster."),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);
    }

    private static TrainingAnnexPartyOperation ToPartyOperation(CleanTrainingAnnexPlayCommand command) =>
        command switch
        {
            CleanTrainingAnnexPlayCommand.PartySelectActiveHostedEntity => TrainingAnnexPartyOperation.SelectActiveHostedEntity,
            CleanTrainingAnnexPlayCommand.PartyDeployAshling => TrainingAnnexPartyOperation.DeployAshling,
            CleanTrainingAnnexPlayCommand.PartySwapDeployedCompanion => TrainingAnnexPartyOperation.SwapDeployedCompanionToWardShell,
            CleanTrainingAnnexPlayCommand.PartyRecallActiveCompanion => TrainingAnnexPartyOperation.RecallActiveCompanion,
            CleanTrainingAnnexPlayCommand.PartyReplaceWardShell => TrainingAnnexPartyOperation.ReplaceWardShellWithBrambleRunner,
            CleanTrainingAnnexPlayCommand.PartyDismissAshling => TrainingAnnexPartyOperation.DismissAshling,
            CleanTrainingAnnexPlayCommand.PartyConsumeBrambleRunner => TrainingAnnexPartyOperation.ConsumeBrambleRunner,
            _ => throw new InvalidOperationException($"'{command}' is not a Training Annex party operation.")
        };

    private static string PartyOperationName(TrainingAnnexPartyOperation operation) =>
        operation switch
        {
            TrainingAnnexPartyOperation.SelectActiveHostedEntity => "select_active_hosted_entity",
            TrainingAnnexPartyOperation.DeployAshling => "deploy_companion",
            TrainingAnnexPartyOperation.SwapDeployedCompanionToWardShell => "swap_deployed_companion",
            TrainingAnnexPartyOperation.RecallActiveCompanion => "recall_active_companion",
            TrainingAnnexPartyOperation.ReplaceWardShellWithBrambleRunner => "replace_companion",
            TrainingAnnexPartyOperation.DismissAshling => "dismiss_companion",
            TrainingAnnexPartyOperation.ConsumeBrambleRunner => "consume_companion",
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
        RuntimePartyRosterSnapshot partyRoster,
        IReadOnlyList<TrainingAnnexPartyTransitionEvidence> partyTransitions,
        IReadOnlyList<TrainingAnnexNegotiationEvidence> negotiations,
        IReadOnlyList<TrainingAnnexFusionResultEvidence> fusionResults,
        IReadOnlyList<TrainingAnnexFusionPlanningEvidence> fusionPlanning,
        IReadOnlyList<TrainingAnnexFusionPreviewEvidence> fusionPreviews,
        IReadOnlyList<TrainingAnnexFusionTransactionEvidence> fusionTransactions,
        CompendiumStateSnapshot compendium,
        IReadOnlyList<TrainingAnnexCompendiumEvidence> compendiumEvidence,
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
        IReadOnlyList<TrainingAnnexTurnEconomyEvidence> turnEconomyEvidence,
        IReadOnlyList<TrainingAnnexLifecycleEvidence> lifecycleEvidence,
        IReadOnlyList<TrainingAnnexAiDecisionEvidence> aiDecisionEvidence,
        IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> battleKnowledgeEvidence,
        IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> encounterAiKnowledgeEvidence,
        RuntimeKnowledgeSnapshot battleKnowledge,
        RuntimeEncounterKnowledgeSnapshot encounterAiKnowledge,
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
        RuntimeActorSnapshot? activeHostedEntitySnapshot = partyRoster.ActiveHostedEntity is null
            ? null
            : roster.AllActors
                .First(actor =>
                    actor.Actor.State.InstanceId ==
                    partyRoster.ActiveHostedEntity.InstanceId)
                .Actor.State.ToSnapshot();
        RuntimeEquipmentSnapshot equipment = playerSnapshot.Equipment;
        return new(
            [request.ManifestPath],
            request.DocumentPaths,
            player.Entity.Id,
            playerSnapshot.Identity.ActorKindId,
            roster.Player.Level,
            roster.AllActors.Count,
            roster.Enemies.Count,
            roster.AllActors.Select(actor => actor.Actor.Entity.Id).ToArray(),
            roster.AllActors.Select(actor => actor.Actor.State.InstanceId).ToArray(),
            partyRoster,
            partyTransitions.ToArray(),
            negotiations.ToArray(),
            fusionResults.ToArray(),
            fusionPlanning.ToArray(),
            fusionPreviews.ToArray(),
            fusionTransactions.ToArray(),
            compendium,
            compendiumEvidence.ToArray(),
            playerSnapshot.Resources,
            playerSnapshot.Stats,
            playerSnapshot.Progression,
            activeHostedEntitySnapshot?.Progression,
            activeHostedEntitySnapshot?.Skills,
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
            turnEconomyEvidence.ToArray(),
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

    private async ValueTask<bool> ComposePlayerStateAsync(
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        IRuntimeActorCombatProfileCompositionService compositionService,
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        IEquipmentDefinitionRepository equipmentRepository,
        CancellationToken cancellationToken,
        bool initializeResourcesToMaximum = false)
    {
        RuntimeEquipmentProfile equipmentProfile = equipmentProfileResolver.Resolve(
            roster.Player.Actor.State.ToSnapshot().Equipment,
            equipmentRepository);
        if (equipmentProfile.Diagnostics.Count > 0)
        {
            foreach (RuntimeEquipmentProfileDiagnostic diagnostic in equipmentProfile.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[equipment:{diagnostic.Code}] {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        RuntimeActorCombatProfileCompositionResult composition = TrainingAnnexHostSupport.ComposePlayerCombatProfile(
            roster,
            partyRoster,
            compositionService,
            equipmentProfile);
        if (composition.Applied)
        {
            if (initializeResourcesToMaximum)
            {
                foreach (BattleResourceState resource in roster.Player.Actor.State.Resources.Values)
                {
                    roster.Player.Actor.State.SetResource(resource.Id, resource.Maximum);
                }
            }

            return true;
        }

        foreach (RuntimeActorCombatProfileCompositionDiagnostic diagnostic in composition.Diagnostics)
        {
            await _eventSink.PublishAsync(
                $"[combat_profile_composition:{diagnostic.Code}] {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private async ValueTask<IReadOnlyList<StatResolutionResult>> ResolvePlayerStatsAsync(
        TrainingAnnexRuntimeActor player,
        IStatResolutionPolicy statPolicy,
        IStatStageScalingPolicy stageScalingPolicy,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot snapshot = player.Actor.State.ToSnapshot();
        RuntimeStatStageSnapshot attackStage = new(StandardProgressionIds.Attack, 1);
        var results = new List<StatResolutionResult>();
        var messages = new List<string>();

        foreach (ContentId statId in StandardProgressionIds.CoreStats)
        {
            StatResolutionResult unmodified = statPolicy.Resolve(new StatResolutionRequest(
                RuntimeStatSourceKind.Actor,
                statId,
                snapshot.Stats.EffectiveStats));
            results.Add(unmodified);
            messages.Add($"{statId} {unmodified.FinalValue}");
        }

        await _eventSink.PublishAsync(
            $"Stat policy: standard_stat resolved raw stats for {player.Actor.Entity.DisplayName}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Resolved stats: {string.Join(", ", messages)}.",
            cancellationToken).ConfigureAwait(false);
        StatStageScalingResult physical = stageScalingPolicy.Resolve(new StatStageScalingRequest(
            StatStageScalingChannel.PhysicalDamageDealt,
            [attackStage]));
        StatStageScalingResult magical = stageScalingPolicy.Resolve(new StatStageScalingRequest(
            StatStageScalingChannel.MagicalDamageDealt,
            [attackStage]));
        await _eventSink.PublishAsync(
            $"Stage policy: attack +1 resolves physical x{physical.Multiplier:0.###} and magical x{magical.Multiplier:0.###}.",
            cancellationToken).ConfigureAwait(false);

        return results.ToArray();
    }

    private async ValueTask<(
        LevelGrowthResult Growth,
        RuntimeActorGrowthCompositionResult? Transaction)> ApplyVictoryExperienceAsync(
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        GrowthRulesetServices growthServices,
        IRuntimeActorCombatProfileCompositionService combatProfileCompositionService,
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        GameDataCatalog catalog,
        CancellationToken cancellationToken)
    {
        TrainingAnnexRuntimeActor player = roster.Player;
        RuntimeActorReferenceSnapshot activeReference = partyRoster.ActiveHostedEntity ??
            throw new InvalidOperationException(
                "Training Annex victory growth requires an active Hosted Entity.");
        TrainingAnnexRuntimeActor growthActor = roster.AllActors.Single(actor =>
            actor.Actor.State.InstanceId == activeReference.InstanceId);
        RuntimeActorSnapshot sourceBefore = growthActor.Actor.State.ToSnapshot();
        RuntimeActorSnapshot playerBefore = player.Actor.State.ToSnapshot();
        long requiredExperience = growthServices.ExperienceCurve.GetRequiredExperience(
            sourceBefore.Progression.Level);
        long award = Math.Max(0, requiredExperience - sourceBefore.Progression.Experience);
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            sourceBefore.Progression,
            sourceBefore.Stats,
            StandardLevelGrowthProfiles.OwnedEntity,
            award,
            new TrainingAnnexMinimumRandomSource(),
            resources: sourceBefore.Resources,
            baseResourceValues: sourceBefore.BaseResourceValues));
        RuntimeEquipmentProfile equipmentProfile = equipmentProfileResolver.Resolve(
            playerBefore.Equipment,
            catalog);
        if (equipmentProfile.Diagnostics.Count > 0)
        {
            foreach (RuntimeEquipmentProfileDiagnostic diagnostic in equipmentProfile.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[equipment:{diagnostic.Code}] {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return (growth, null);
        }

        RuntimeActorGrowthCompositionResult transaction = new RuntimeActorGrowthCompositionService(
            combatProfileCompositionService,
            catalog).Apply(new RuntimeActorGrowthCompositionRequest(
                growthActor.Actor.State,
                growthActor.Actor.Entity,
                growth,
                new SharedRuntimeMoveListCapacityPolicy(),
                TrainingAnnexHostSupport.CreatePlayerCombatProfileCompositionRequest(
                    roster,
                    partyRoster,
                    equipmentProfile)));
        if (!transaction.Applied)
        {
            foreach (RuntimeActorGrowthCompositionDiagnostic diagnostic in transaction.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return (growth, null);
        }

        RuntimeActorSnapshot sourceAfter = transaction.GrowthActorAfter;
        RuntimeActorSnapshot playerAfter = transaction.ComposedActorAfter;
        foreach (RuntimeSkillUnlockPlanEntry entry in
                 transaction.SkillUnlockPlan?.Entries ?? [])
        {
            SkillDefinition skill = catalog.GetRequiredSkill(entry.SkillId);
            string message = entry.Disposition ==
                RuntimeSkillUnlockDisposition.AutomaticallyEquipped
                ? $"Skill unlocked: {skill.DisplayName} joined " +
                  $"{growthActor.Actor.Entity.DisplayName}'s move list at level " +
                  $"{entry.UnlockLevel}."
                : $"Move list full: {skill.DisplayName} is pending for " +
                  $"{growthActor.Actor.Entity.DisplayName} at level " +
                  $"{entry.UnlockLevel}.";
            await _eventSink.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        }
        await _eventSink.PublishAsync(
            $"Victory EXP: awarded {award} EXP through standard_growth.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Growth result: {growthActor.Actor.Entity.DisplayName} level " +
            $"{sourceBefore.Progression.Level}->{sourceAfter.Progression.Level}; exp " +
            $"{sourceBefore.Progression.Experience}->{sourceAfter.Progression.Experience}; " +
            $"lifetime {sourceBefore.Progression.LifetimeExperience}->" +
            $"{sourceAfter.Progression.LifetimeExperience}; Vessel profile source " +
            $"{transaction.CombatProfileComposition?.SourceActorId}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Vessel combat profile: {player.Actor.Entity.DisplayName} remains level " +
            $"{playerAfter.Progression.Level} and now exposes " +
            $"{playerAfter.Skills.EquippedSkillIds.Count} equipped skill(s).",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Level-up events: {(growth.LevelUps.Count == 0 ? "none" : string.Join(", ", growth.LevelUps.Select(levelUp => levelUp.Level.ToString())))}.",
            cancellationToken).ConfigureAwait(false);

        return (growth, transaction);
    }

    private async ValueTask ResolvePendingSkillChoicesAsync(
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        IRuntimeActorCombatProfileCompositionService combatProfileCompositionService,
        IRuntimeEquipmentProfileResolver equipmentProfileResolver,
        GameDataCatalog catalog,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        RuntimeActorReferenceSnapshot activeReference = partyRoster.ActiveHostedEntity ??
            throw new InvalidOperationException(
                "Training Annex move-list decisions require an active Hosted Entity.");
        TrainingAnnexRuntimeActor source = roster.AllActors.Single(actor =>
            actor.Actor.State.InstanceId == activeReference.InstanceId);

        while (source.Actor.State.Skills.PendingChoices.Count > 0)
        {
            RuntimePendingSkillChoiceSnapshot pending =
                source.Actor.State.Skills.PendingChoices[0];
            SkillDefinition pendingSkill = catalog.GetRequiredSkill(pending.SkillId);
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
                await _commandSource.ReadAsync(
                    CreatePendingSkillChoiceMenu(source, pendingSkill),
                    cancellationToken).ConfigureAwait(false);
            if (!selection.IsSelected)
            {
                commands.Add(CleanTrainingAnnexPlayCommand.Back);
                await _eventSink.PublishAsync(
                    $"Move-list decision deferred: {pendingSkill.DisplayName} remains " +
                    $"pending for {source.Actor.Entity.DisplayName}.",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            CleanTrainingAnnexPlayCommand selected = selection.Command;
            commands.Add(selected);
            if (selected == CleanTrainingAnnexPlayCommand.DeferPendingSkillChoice)
            {
                await _eventSink.PublishAsync(
                    $"Move-list decision deferred: {pendingSkill.DisplayName} remains " +
                    $"pending for {source.Actor.Entity.DisplayName}.",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            RuntimeSkillStateSnapshot current = source.Actor.State.Skills;
            RuntimeSkillChoiceCommand choice;
            ContentId? replacedSkillId = null;
            if (selected == CleanTrainingAnnexPlayCommand.SelectSkillToReplace &&
                selection.SelectionIdentity?.ContentId is ContentId selectedSkillId)
            {
                replacedSkillId = selectedSkillId;
                choice = new ReplacePendingSkillCommand(
                    pending.Token,
                    source.Actor.State.Progression.Level,
                    current.Revision,
                    selectedSkillId);
            }
            else if (selected == CleanTrainingAnnexPlayCommand.ForgetPendingSkill)
            {
                choice = new ForgetPendingSkillCommand(
                    pending.Token,
                    source.Actor.State.Progression.Level,
                    current.Revision);
            }
            else
            {
                await _eventSink.PublishAsync(
                    "Move-list selection was not recognized; the pending choice was preserved.",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            RuntimeEquipmentProfile equipmentProfile = equipmentProfileResolver.Resolve(
                roster.Player.Actor.State.Equipment,
                catalog);
            if (equipmentProfile.Diagnostics.Count > 0)
            {
                foreach (RuntimeEquipmentProfileDiagnostic diagnostic in
                         equipmentProfile.Diagnostics)
                {
                    await _eventSink.PublishAsync(
                        $"[equipment:{diagnostic.Code}] {diagnostic.Message}",
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            RuntimeSkillChoiceTransactionResult result =
                new RuntimeSkillChoiceTransactionService(
                    catalog,
                    combatProfileCompositionService).Apply(
                    new RuntimeSkillChoiceTransactionRequest(
                        source.Actor.State,
                        choice,
                        TrainingAnnexHostSupport.CreatePlayerCombatProfileCompositionRequest(
                            roster,
                            partyRoster,
                            equipmentProfile)));
            if (!result.Applied)
            {
                foreach (RuntimeSkillChoiceDiagnostic diagnostic in result.Diagnostics)
                {
                    await _eventSink.PublishAsync(
                        $"[skill-choice:{diagnostic.Code}] {diagnostic.Message}",
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            string message = replacedSkillId is ContentId replaced
                ? $"Move-list decision applied: {source.Actor.Entity.DisplayName} replaced " +
                  $"{catalog.GetRequiredSkill(replaced).DisplayName} with " +
                  $"{pendingSkill.DisplayName}."
                : $"Move-list decision applied: {source.Actor.Entity.DisplayName} forgot " +
                  $"{pendingSkill.DisplayName} and retained the current move list.";
            await _eventSink.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand>
        CreatePendingSkillChoiceMenu(
            TrainingAnnexRuntimeActor source,
            SkillDefinition pendingSkill)
    {
        List<HostCommandOption<CleanTrainingAnnexPlayCommand>> options =
            source.Actor.SkillLoadout.Select(skill =>
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SelectSkillToReplace,
                    $"Replace {skill.DisplayName}",
                    Description: $"Forget {skill.DisplayName} and learn " +
                                 $"{pendingSkill.DisplayName}.",
                    SelectionIdentity:
                        HostCommandSelectionIdentity.ForContent(skill.Id)))
            .ToList();
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.ForgetPendingSkill,
            $"Forget {pendingSkill.DisplayName}",
            Description: "Keep the current move list and discard the new skill."));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.DeferPendingSkillChoice,
            "Decide Later",
            Description: "Leave the choice pending in the Hosted Entity snapshot."));
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            $"{source.Actor.Entity.DisplayName} move list is full: learn " +
            $"{pendingSkill.DisplayName}",
            options);
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
