using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexManualBattleSummary(
    bool Started,
    BattleEncounterOutcome Outcome,
    ContentId? WinningTeamId,
    IReadOnlyList<ContentId> ExecutedActionIds,
    IReadOnlyList<TrainingAnnexTypedEffectEvidence> ExecutedEffectEvidence,
    IReadOnlyList<TrainingAnnexCombatResolutionEvidence> CombatResolutionEvidence,
    IReadOnlyList<TrainingAnnexPressTurnEvidence> PressTurnEvidence,
    IReadOnlyList<TrainingAnnexLifecycleEvidence> LifecycleEvidence,
    IReadOnlyList<TrainingAnnexAiDecisionEvidence> AiDecisionEvidence,
    IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> BattleKnowledgeEvidence,
    IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> EncounterAiKnowledgeEvidence,
    RuntimeKnowledgeSnapshot EncounterAiKnowledge,
    BattleRewardResult? RewardPreview,
    int CancelledSelections,
    int EventCount);

internal sealed record TrainingAnnexTypedEffectEvidence(
    ContentId SourceActionId,
    int EffectIndex,
    string EffectKind,
    DamageElement? DamageElement = null,
    ContentId? ResourceId = null,
    ContentId? RelatedContentId = null);

internal sealed record TrainingAnnexCombatResolutionEvidence(
    ContentId SourceActionId,
    int EffectIndex,
    RuntimeInstanceId? TargetId,
    DamageElement? DamageElement,
    int? Power,
    int? Accuracy,
    CriticalMode? CriticalMode,
    bool? Hit,
    bool IsCritical,
    ElementalAffinity? ResolvedAffinity,
    decimal? Value,
    EffectExecutionOutcome Outcome,
    PressTurnOutcome PressTurnOutcome);

internal sealed record TrainingAnnexPressTurnEvidence(
    RuntimeInstanceId ActorId,
    ContentId? ActionId,
    int BeforeFullIcons,
    int BeforeBlinkingIcons,
    ActionTurnConsumptionKind TurnConsumptionKind,
    PressTurnOutcome? PressTurnOutcome,
    int AfterFullIcons,
    int AfterBlinkingIcons);

internal sealed record TrainingAnnexLifecycleEvidence(
    RuntimeInstanceId ActorId,
    BattleStatusLifecycleEventKind EventKind,
    ContentId? RelatedContentId = null,
    decimal? Value = null,
    string? Detail = null,
    BattleTurnStartOutcome? TurnStartOutcome = null,
    ContentId? SourceActionId = null);

internal enum TrainingAnnexBattleKnowledgeChannel
{
    ElementalAffinity,
    AilmentResistance,
    InstantDeathResistance
}

internal sealed record TrainingAnnexBattleKnowledgeEvidence(
    ContentId SourceActionId,
    int EffectIndex,
    RuntimeInstanceId TargetInstanceId,
    ContentId TargetEntityId,
    TrainingAnnexBattleKnowledgeChannel Channel,
    DamageElement? Element = null,
    ContentId? AilmentId = null,
    InstantDeathChannel? InstantDeathChannel = null,
    ElementalAffinity? Affinity = null,
    ResistanceLevel? Resistance = null,
    bool WasNewDiscovery = false);

internal sealed record TrainingAnnexAiDecisionEvidence
{
    public TrainingAnnexAiDecisionEvidence(
        RuntimeInstanceId actorInstanceId,
        ContentId actorEntityId,
        BattleActionSelectionStatus status,
        ContentId selectedActionId,
        IEnumerable<RuntimeInstanceId> targetIds,
        bool? assessmentCanExecute)
    {
        ActorInstanceId = actorInstanceId;
        ActorEntityId = actorEntityId;
        Status = status;
        SelectedActionId = selectedActionId;
        TargetIds = Array.AsReadOnly(
            targetIds?.ToArray() ?? throw new ArgumentNullException(nameof(targetIds)));
        AssessmentCanExecute = assessmentCanExecute;
    }

    public RuntimeInstanceId ActorInstanceId { get; }
    public ContentId ActorEntityId { get; }
    public BattleActionSelectionStatus Status { get; }
    public ContentId SelectedActionId { get; }
    public IReadOnlyList<RuntimeInstanceId> TargetIds { get; }
    public bool? AssessmentCanExecute { get; }
}

internal sealed class TrainingAnnexBattleKnowledgeState
{
    public ElementalAffinityKnowledge ElementalAffinities { get; } = new();
    public AilmentResistanceKnowledge AilmentResistances { get; } = new();
    public InstantDeathResistanceKnowledge InstantDeathResistances { get; } = new();

    public static TrainingAnnexBattleKnowledgeState FromSnapshot(RuntimeKnowledgeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var state = new TrainingAnnexBattleKnowledgeState();
        foreach (RuntimeElementalAffinityKnowledgeSnapshot entry in snapshot.ElementalAffinities)
        {
            state.ElementalAffinities.Learn(entry.EntityId, entry.Element, entry.Affinity);
        }

        foreach (RuntimeAilmentResistanceKnowledgeSnapshot entry in snapshot.AilmentResistances)
        {
            state.AilmentResistances.Learn(entry.EntityId, entry.AilmentId, entry.Resistance);
        }

        foreach (RuntimeInstantDeathResistanceKnowledgeSnapshot entry in snapshot.InstantDeathResistances)
        {
            state.InstantDeathResistances.Learn(entry.EntityId, entry.Channel, entry.Resistance);
        }

        return state;
    }

    public RuntimeKnowledgeSnapshot ToSnapshot() =>
        new(
            ElementalAffinities.Snapshot()
                .OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.Element.ToString(), StringComparer.Ordinal)
                .Select(entry => new RuntimeElementalAffinityKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.Element,
                    entry.Value)),
            AilmentResistances.Snapshot()
                .OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.AilmentId.ToString(), StringComparer.Ordinal)
                .Select(entry => new RuntimeAilmentResistanceKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.AilmentId,
                    entry.Value)),
            InstantDeathResistances.Snapshot()
                .OrderBy(entry => entry.Key.EntityId.ToString(), StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.Channel.ToString(), StringComparer.Ordinal)
                .Select(entry => new RuntimeInstantDeathResistanceKnowledgeSnapshot(
                    entry.Key.EntityId,
                    entry.Key.Channel,
                    entry.Value)));

    public IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> LearnFromExecution(
        BattleActionCommand command,
        ContentId actionId,
        BattleActionExecutionResult execution,
        IReadOnlyList<CatalogBattleActor> actors,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(catalog);

        var evidence = new List<TrainingAnnexBattleKnowledgeEvidence>();
        foreach (EffectExecutionResult effect in execution.Effects)
        {
            if (effect.TargetId is not RuntimeInstanceId targetId)
            {
                continue;
            }

            CatalogBattleActor? target = actors.FirstOrDefault(actor => actor.State.InstanceId == targetId);
            if (target is null)
            {
                continue;
            }

            if (command is AnalyzeBattleActionCommand analyze &&
                effect.Outcome == EffectExecutionOutcome.Success)
            {
                evidence.AddRange(LearnFromAnalyze(actionId, effect.EffectIndex, target, analyze.Layers, catalog));
                continue;
            }

            if (DamageElementFor(command, effect.EffectIndex) is DamageElement element &&
                element != DamageElement.Almighty &&
                effect.ResolvedAffinity is ElementalAffinity affinity)
            {
                evidence.Add(LearnElemental(actionId, effect.EffectIndex, target, element, affinity));
            }

            if (AilmentIdFor(command, effect.EffectIndex) is ContentId ailmentId &&
                effect.Outcome is EffectExecutionOutcome.Success or EffectExecutionOutcome.Failure)
            {
                evidence.Add(LearnAilment(
                    actionId,
                    effect.EffectIndex,
                    target,
                    ailmentId,
                    target.State.DefenseProfile.GetAilmentResistance(ailmentId)));
            }

            if (InstantDeathChannelFor(command, effect.EffectIndex) is InstantDeathChannel channel &&
                effect.Outcome is EffectExecutionOutcome.Success or EffectExecutionOutcome.Failure)
            {
                evidence.Add(LearnInstantDeath(
                    actionId,
                    effect.EffectIndex,
                    target,
                    channel,
                    target.State.DefenseProfile.GetInstantDeathResistance(channel)));
            }
        }

        return evidence;
    }

    private IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> LearnFromAnalyze(
        ContentId actionId,
        int effectIndex,
        CatalogBattleActor target,
        IReadOnlyList<AnalysisLayer> layers,
        GameDataCatalog catalog)
    {
        bool full = layers.Contains(AnalysisLayer.Full);
        var evidence = new List<TrainingAnnexBattleKnowledgeEvidence>();

        if (full || layers.Contains(AnalysisLayer.Affinities))
        {
            foreach (DamageElement element in Enum.GetValues<DamageElement>()
                         .Where(element => element != DamageElement.Almighty))
            {
                evidence.Add(LearnElemental(
                    actionId,
                    effectIndex,
                    target,
                    element,
                    target.State.DefenseProfile.GetElementalAffinity(element)));
            }
        }

        if (full || layers.Contains(AnalysisLayer.Ailments))
        {
            foreach (ContentId ailmentId in catalog.Ailments.Keys.OrderBy(id => id.ToString(), StringComparer.Ordinal))
            {
                evidence.Add(LearnAilment(
                    actionId,
                    effectIndex,
                    target,
                    ailmentId,
                    target.State.DefenseProfile.GetAilmentResistance(ailmentId)));
            }
        }

        if (full)
        {
            foreach (InstantDeathChannel channel in Enum.GetValues<InstantDeathChannel>())
            {
                evidence.Add(LearnInstantDeath(
                    actionId,
                    effectIndex,
                    target,
                    channel,
                    target.State.DefenseProfile.GetInstantDeathResistance(channel)));
            }
        }

        return evidence;
    }

    private TrainingAnnexBattleKnowledgeEvidence LearnElemental(
        ContentId actionId,
        int effectIndex,
        CatalogBattleActor target,
        DamageElement element,
        ElementalAffinity affinity)
    {
        bool changed = !ElementalAffinities.TryGet(target.Entity.Id, element, out ElementalAffinity existing) ||
            existing != affinity;
        ElementalAffinities.Learn(target.Entity.Id, element, affinity);
        return new TrainingAnnexBattleKnowledgeEvidence(
            actionId,
            effectIndex,
            target.State.InstanceId,
            target.Entity.Id,
            TrainingAnnexBattleKnowledgeChannel.ElementalAffinity,
            Element: element,
            Affinity: affinity,
            WasNewDiscovery: changed);
    }

    private TrainingAnnexBattleKnowledgeEvidence LearnAilment(
        ContentId actionId,
        int effectIndex,
        CatalogBattleActor target,
        ContentId ailmentId,
        ResistanceLevel resistance)
    {
        bool changed = !AilmentResistances.TryGet(target.Entity.Id, ailmentId, out ResistanceLevel existing) ||
            existing != resistance;
        AilmentResistances.Learn(target.Entity.Id, ailmentId, resistance);
        return new TrainingAnnexBattleKnowledgeEvidence(
            actionId,
            effectIndex,
            target.State.InstanceId,
            target.Entity.Id,
            TrainingAnnexBattleKnowledgeChannel.AilmentResistance,
            AilmentId: ailmentId,
            Resistance: resistance,
            WasNewDiscovery: changed);
    }

    private TrainingAnnexBattleKnowledgeEvidence LearnInstantDeath(
        ContentId actionId,
        int effectIndex,
        CatalogBattleActor target,
        InstantDeathChannel channel,
        ResistanceLevel resistance)
    {
        bool changed = !InstantDeathResistances.TryGet(target.Entity.Id, channel, out ResistanceLevel existing) ||
            existing != resistance;
        InstantDeathResistances.Learn(target.Entity.Id, channel, resistance);
        return new TrainingAnnexBattleKnowledgeEvidence(
            actionId,
            effectIndex,
            target.State.InstanceId,
            target.Entity.Id,
            TrainingAnnexBattleKnowledgeChannel.InstantDeathResistance,
            InstantDeathChannel: channel,
            Resistance: resistance,
            WasNewDiscovery: changed);
    }

    private static DamageElement? DamageElementFor(BattleActionCommand command, int effectIndex) =>
        command switch
        {
            BasicAttackBattleActionCommand basic when effectIndex == 0 => basic.BasicAttack.Element,
            SkillBattleActionCommand skill when effectIndex >= 0 && effectIndex < skill.Skill.Effects.Count &&
                skill.Skill.Effects[effectIndex] is DamageEffectDefinition damage => damage.Element,
            ItemBattleActionCommand item when item.Item.Usage is not null &&
                effectIndex >= 0 && effectIndex < item.Item.Usage.Effects.Count &&
                item.Item.Usage.Effects[effectIndex] is DamageEffectDefinition damage => damage.Element,
            _ => null
        };

    private static ContentId? AilmentIdFor(BattleActionCommand command, int effectIndex) =>
        command switch
        {
            SkillBattleActionCommand skill when effectIndex >= 0 && effectIndex < skill.Skill.Effects.Count &&
                skill.Skill.Effects[effectIndex] is ApplyAilmentEffectDefinition ailment => ailment.AilmentId,
            ItemBattleActionCommand item when item.Item.Usage is not null &&
                effectIndex >= 0 && effectIndex < item.Item.Usage.Effects.Count &&
                item.Item.Usage.Effects[effectIndex] is ApplyAilmentEffectDefinition ailment => ailment.AilmentId,
            _ => null
        };

    private static InstantDeathChannel? InstantDeathChannelFor(BattleActionCommand command, int effectIndex)
    {
        InstantKillEffectDefinition? instant = command switch
        {
            SkillBattleActionCommand skill when effectIndex >= 0 && effectIndex < skill.Skill.Effects.Count =>
                skill.Skill.Effects[effectIndex] as InstantKillEffectDefinition,
            ItemBattleActionCommand item when item.Item.Usage is not null &&
                effectIndex >= 0 && effectIndex < item.Item.Usage.Effects.Count =>
                item.Item.Usage.Effects[effectIndex] as InstantKillEffectDefinition,
            _ => null
        };

        return instant?.ResistanceCheck is ChannelInstantDeathResistanceCheckDefinition channel
            ? channel.Channel
            : null;
    }
}

internal sealed class TrainingAnnexBattleActionAdapter
{
    private static readonly ContentId GuardAction = ContentId.Parse("guard");
    private static readonly ContentId PassAction = ContentId.Parse("pass");
    private static readonly ContentId AnalyzeAction = ContentId.Parse("analyze");

    private readonly GameDataCatalog _catalog;
    private readonly IHostEventSink<string> _events;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commands;
    private readonly BattleExecutionServices _services;
    private readonly IBattleRewardService _rewardService;
    private readonly Func<PressTurnEngine> _pressTurnFactory;
    private readonly IBattleStatusLifecycleService _statusLifecycle;

    public TrainingAnnexBattleActionAdapter(
        GameDataCatalog catalog,
        IHostEventSink<string> events,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commands,
        BattleExecutionServices services,
        IBattleRewardService rewardService,
        Func<PressTurnEngine> pressTurnFactory,
        IBattleStatusLifecycleService statusLifecycle)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
        _pressTurnFactory = pressTurnFactory ?? throw new ArgumentNullException(nameof(pressTurnFactory));
        _statusLifecycle = statusLifecycle ?? throw new ArgumentNullException(nameof(statusLifecycle));
    }

    public async ValueTask<TrainingAnnexManualBattleSummary> RunAsync(
        TrainingAnnexRuntimeActor player,
        PreparedEncounter prepared,
        TrainingAnnexItemActionInventory inventory,
        TrainingAnnexBattleKnowledgeState playerBattleKnowledge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(playerBattleKnowledge);

        var encounterAiKnowledge = new TrainingAnnexBattleKnowledgeState();

        CatalogBattleActor[] actors = [player.Actor, .. prepared.Actors];
        BattleEncounterParticipant[] participants = actors
            .Select(actor => new BattleEncounterParticipant(actor.State, actor.Entity.DisplayName))
            .ToArray();
        var pressTurns = new TrainingAnnexPressTurnTracker();
        var lifecycle = new TrainingAnnexLifecycleTracker();
        var lifecyclePort = new TrainingAnnexBattleLifecyclePort(_statusLifecycle, _services, lifecycle);
        var skillExecutor = new SkillExecutor(_services);
        var enemySelector = new DeterministicBattleActionSelector(skillExecutor);

        var turnHandler = new TrainingAnnexManualBattleTurnHandler(
            _catalog,
            _events,
            _commands,
            new BattleActionExecutor(skillExecutor, new ItemExecutor(_services), _services),
            enemySelector,
            playerBattleKnowledge,
            encounterAiKnowledge,
            actors,
            player,
            inventory,
            pressTurns,
            lifecycle);
        var services = new BattleEncounterServices(
            new ParticipantOrderInitiativePolicy(),
            lifecyclePort,
            turnHandler,
            new LastTeamStandingCompletionPolicy(),
            events: new TrainingAnnexPressTurnEventSink(_events, pressTurns),
            pressTurnFactory: _pressTurnFactory);

        await _events.PublishAsync(
            $"Clean battle started: {prepared.Encounter.DisplayName}.",
            cancellationToken).ConfigureAwait(false);
        BattleEncounterResult result = await new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest(
                participants,
                TrainingAnnexHostSupport.Battle,
                TrainingAnnexHostSupport.NormalBattle,
                null,
                roundLimit: 10),
            services,
            cancellationToken).ConfigureAwait(false);

        await _events.PublishAsync(
            result.WinningTeamId is ContentId winner
                ? $"Clean battle ended: {result.Outcome}; winner {winner}."
                : $"Clean battle ended: {result.Outcome}.",
            cancellationToken).ConfigureAwait(false);

        BattleRewardResult? rewardPreview = result.Outcome == BattleEncounterOutcome.Victory &&
            result.WinningTeamId == TrainingAnnexHostSupport.PlayerTeam
                ? CalculateRewardPreview(player, prepared)
                : null;

        return new TrainingAnnexManualBattleSummary(
            true,
            result.Outcome,
            result.WinningTeamId,
            turnHandler.ExecutedActionIds,
            turnHandler.ExecutedEffectEvidence,
            turnHandler.CombatResolutionEvidence,
            pressTurns.Evidence,
            lifecycle.Evidence,
            turnHandler.AiDecisionEvidence,
            turnHandler.BattleKnowledgeEvidence,
            turnHandler.EncounterAiKnowledgeEvidence,
            encounterAiKnowledge.ToSnapshot(),
            rewardPreview,
            turnHandler.CancelledSelections,
            result.Events.Count);
    }

    private BattleRewardResult CalculateRewardPreview(
        TrainingAnnexRuntimeActor player,
        PreparedEncounter prepared)
    {
        BattleRewardEnemySnapshot[] enemies = prepared.StartPlan.ActorRequests
            .Zip(prepared.Actors, (request, actor) => new BattleRewardEnemySnapshot(
                actor.Entity.Id,
                request.Level,
                actor.Entity.Stats.GetValueOrDefault(StandardProgressionIds.Strength),
                actor.Entity.Stats.GetValueOrDefault(StandardProgressionIds.Magic),
                actor.Entity.Stats.GetValueOrDefault(StandardProgressionIds.Vitality),
                actor.Entity.Stats.GetValueOrDefault(StandardProgressionIds.Agility),
                actor.Entity.Stats.GetValueOrDefault(StandardProgressionIds.Luck)))
            .ToArray();
        return _rewardService.Calculate(new BattleRewardRequest(
            enemies,
            [new BattleRewardRecipientSnapshot(
                player.Actor.Entity.Id,
                IsAlive: !player.Actor.State.IsDefeated,
                HasActiveForm: false)]));
    }

    private sealed class TrainingAnnexManualBattleTurnHandler : IBattleEncounterTurnHandler
    {
        private readonly GameDataCatalog _catalog;
        private readonly IHostEventSink<string> _events;
        private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commands;
        private readonly IBattleActionExecutor _actions;
        private readonly IBattleActionSelector _enemySelector;
        private readonly TrainingAnnexBattleKnowledgeState _playerBattleKnowledge;
        private readonly TrainingAnnexBattleKnowledgeState _encounterAiKnowledge;
        private readonly IReadOnlyList<CatalogBattleActor> _actors;
        private readonly TrainingAnnexRuntimeActor _player;
        private readonly TrainingAnnexItemActionInventory _inventory;
        private readonly TrainingAnnexPressTurnTracker _pressTurns;
        private readonly TrainingAnnexLifecycleTracker _lifecycle;
        private readonly List<ContentId> _executedActionIds = [];
        private readonly List<TrainingAnnexTypedEffectEvidence> _executedEffectEvidence = [];
        private readonly List<TrainingAnnexCombatResolutionEvidence> _combatResolutionEvidence = [];
        private readonly List<TrainingAnnexAiDecisionEvidence> _aiDecisionEvidence = [];
        private readonly List<TrainingAnnexBattleKnowledgeEvidence> _battleKnowledgeEvidence = [];
        private readonly List<TrainingAnnexBattleKnowledgeEvidence> _encounterAiKnowledgeEvidence = [];

        public TrainingAnnexManualBattleTurnHandler(
            GameDataCatalog catalog,
            IHostEventSink<string> events,
            IHostCommandSource<CleanTrainingAnnexPlayCommand> commands,
            IBattleActionExecutor actions,
            IBattleActionSelector enemySelector,
            TrainingAnnexBattleKnowledgeState playerBattleKnowledge,
            TrainingAnnexBattleKnowledgeState encounterAiKnowledge,
            IReadOnlyList<CatalogBattleActor> actors,
            TrainingAnnexRuntimeActor player,
            TrainingAnnexItemActionInventory inventory,
            TrainingAnnexPressTurnTracker pressTurns,
            TrainingAnnexLifecycleTracker lifecycle)
        {
            _catalog = catalog;
            _events = events;
            _commands = commands;
            _actions = actions;
            _enemySelector = enemySelector ?? throw new ArgumentNullException(nameof(enemySelector));
            _playerBattleKnowledge = playerBattleKnowledge ?? throw new ArgumentNullException(nameof(playerBattleKnowledge));
            _encounterAiKnowledge = encounterAiKnowledge ?? throw new ArgumentNullException(nameof(encounterAiKnowledge));
            _actors = actors;
            _player = player;
            _inventory = inventory;
            _pressTurns = pressTurns;
            _lifecycle = lifecycle;
        }

        public IReadOnlyList<ContentId> ExecutedActionIds => _executedActionIds;
        public IReadOnlyList<TrainingAnnexTypedEffectEvidence> ExecutedEffectEvidence => _executedEffectEvidence;
        public IReadOnlyList<TrainingAnnexCombatResolutionEvidence> CombatResolutionEvidence =>
            _combatResolutionEvidence;
        public IReadOnlyList<TrainingAnnexAiDecisionEvidence> AiDecisionEvidence =>
            _aiDecisionEvidence.ToArray();
        public IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> BattleKnowledgeEvidence =>
            _battleKnowledgeEvidence.ToArray();
        public IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> EncounterAiKnowledgeEvidence =>
            _encounterAiKnowledgeEvidence.ToArray();
        public int CancelledSelections { get; private set; }

        public async ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CatalogBattleActor actor = _actors.Single(candidate =>
                candidate.State.InstanceId == request.Actor.InstanceId);

            if (request.TurnStartOutcome != BattleTurnStartOutcome.CanAct)
            {
                return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
            }

            return actor.State.TeamId == TrainingAnnexHostSupport.PlayerTeam
                ? await ExecutePlayerTurnAsync(request, actor, cancellationToken).ConfigureAwait(false)
                : await ExecuteEnemyTurnAsync(request, actor, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<BattleEncounterCommandResult> ExecutePlayerTurnAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                await PublishCurrentPressTurnAsync(request, cancellationToken).ConfigureAwait(false);
                HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
                    await _commands.ReadAsync(CreateBattleCommandMenu(actor), cancellationToken)
                        .ConfigureAwait(false);
                if (!selection.IsSelected)
                {
                    CancelledSelections++;
                    return BattleEncounterCommandResult.Cancelled();
                }

                BattleActionCommand? command = selection.Command switch
                {
                    CleanTrainingAnnexPlayCommand.BattleAttack =>
                        await SelectBasicAttackAsync(request, actor, cancellationToken).ConfigureAwait(false),
                    CleanTrainingAnnexPlayCommand.OpenBattleSkills =>
                        await SelectSkillAsync(request, actor, cancellationToken).ConfigureAwait(false),
                    CleanTrainingAnnexPlayCommand.OpenBattleItems =>
                        await SelectItemAsync(request, actor, cancellationToken).ConfigureAwait(false),
                    CleanTrainingAnnexPlayCommand.BattleGuard => new GuardBattleActionCommand(),
                    CleanTrainingAnnexPlayCommand.BattlePass => new PassBattleActionCommand(),
                    CleanTrainingAnnexPlayCommand.BattleAnalyze =>
                        await SelectAnalyzeAsync(request, actor, cancellationToken).ConfigureAwait(false),
                    _ => null
                };
                if (command is null)
                {
                    continue;
                }

                BattleEncounterCommandResult result = await ExecuteCommandAsync(
                    request,
                    actor,
                    command,
                    cancellationToken).ConfigureAwait(false);
                if (result.Status == BattleEncounterCommandStatus.Rejected)
                {
                    await _events.PublishAsync(
                        result.FaultMessage ?? "Battle action rejected.",
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return result;
            }
        }

        private async ValueTask<BattleEncounterCommandResult> ExecuteEnemyTurnAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            CancellationToken cancellationToken)
        {
            BattleActionSelection selection = _enemySelector.Select(new BattleActionSelectionRequest(
                actor,
                _actors,
                request.Encounter.ContextId,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId,
                _encounterAiKnowledge.ElementalAffinities));
            if (selection.Status == BattleActionSelectionStatus.Selected && selection.Skill is SkillDefinition skill)
            {
                var command = new SkillBattleActionCommand(
                    skill,
                    selection.SelectedTargetIds);
                RecordAiDecision(actor, selection, skill.Id);
                await PublishAiDecisionAsync(actor, skill.DisplayName, cancellationToken).ConfigureAwait(false);
                return await ExecuteCommandAsync(request, actor, command, cancellationToken)
                    .ConfigureAwait(false);
            }

            RecordAiDecision(actor, selection, PassAction);
            await PublishAiDecisionAsync(actor, "Pass", cancellationToken).ConfigureAwait(false);
            return await ExecuteCommandAsync(
                request,
                actor,
                new PassBattleActionCommand(),
                cancellationToken).ConfigureAwait(false);
        }

        private void RecordAiDecision(
            CatalogBattleActor actor,
            BattleActionSelection selection,
            ContentId actionId) =>
            _aiDecisionEvidence.Add(new TrainingAnnexAiDecisionEvidence(
                actor.State.InstanceId,
                actor.Entity.Id,
                selection.Status,
                actionId,
                selection.SelectedTargetIds,
                selection.Assessment?.CanExecute));

        private ValueTask PublishAiDecisionAsync(
            CatalogBattleActor actor,
            string actionLabel,
            CancellationToken cancellationToken) =>
            _events.PublishAsync(
                $"Framework AI selected: {actor.Entity.DisplayName} -> {actionLabel}.",
                cancellationToken);

        private async ValueTask<BattleActionCommand?> SelectBasicAttackAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            CancellationToken cancellationToken)
        {
            RuntimeInstanceId? target = await SelectTargetAsync(
                "Select Battle Target",
                request,
                actor.State,
                TargetRelation.Enemy,
                cancellationToken).ConfigureAwait(false);
            if (target is null)
            {
                return null;
            }

            EquipmentDefinition weapon = _catalog.GetRequiredEquipment(TrainingAnnexHostSupport.PracticeBlade);
            if (weapon.Weapon is null)
            {
                throw new InvalidOperationException("Practice Blade must define a weapon profile.");
            }

            return new BasicAttackBattleActionCommand(
                weapon.Weapon.BasicAttack,
                new TargetingDefinition(
                    TargetRelation.Enemy,
                    TargetSelection.Single,
                    TargetLifeState.Alive,
                    AllowSelf: false),
                [target.Value],
                weapon.Id);
        }

        private async ValueTask<BattleActionCommand?> SelectSkillAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<SkillDefinition> skills = KnownBattleSkills(actor);
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
                await _commands.ReadAsync(CreateBattleSkillMenu(skills), cancellationToken)
                    .ConfigureAwait(false);
            if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
            {
                CancelledSelections++;
                return null;
            }

            SkillDefinition? skill = SkillForCommand(selection.Command, skills);
            if (skill is null)
            {
                return null;
            }

            RuntimeInstanceId? target = await SelectTargetForTargetingAsync(
                request,
                actor.State,
                skill.Targeting,
                cancellationToken).ConfigureAwait(false);
            return target is null && skill.Targeting?.Selection == TargetSelection.Single
                ? null
                : new SkillBattleActionCommand(skill, target is RuntimeInstanceId selected ? [selected] : []);
        }

        private async ValueTask<BattleActionCommand?> SelectItemAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            CancellationToken cancellationToken)
        {
            ItemDefinition tonic = _catalog.GetRequiredItem(TrainingAnnexHostSupport.AnnexTonic);
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
                await _commands.ReadAsync(CreateBattleItemMenu(tonic), cancellationToken)
                    .ConfigureAwait(false);
            if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
            {
                CancelledSelections++;
                return null;
            }

            RuntimeInstanceId? target = await SelectTargetForTargetingAsync(
                request,
                actor.State,
                tonic.Usage?.Targeting,
                cancellationToken).ConfigureAwait(false);
            return target is null
                ? null
                : new ItemBattleActionCommand(tonic, [target.Value]);
        }

        private async ValueTask<BattleActionCommand?> SelectAnalyzeAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            CancellationToken cancellationToken)
        {
            RuntimeInstanceId? target = await SelectTargetAsync(
                "Select Analyze Target",
                request,
                actor.State,
                TargetRelation.Enemy,
                cancellationToken).ConfigureAwait(false);
            return target is null
                ? null
                : new AnalyzeBattleActionCommand(target.Value, [AnalysisLayer.Full]);
        }

        private async ValueTask<RuntimeInstanceId?> SelectTargetForTargetingAsync(
            BattleEncounterTurnRequest request,
            RuntimeActorState actor,
            TargetingDefinition? targeting,
            CancellationToken cancellationToken)
        {
            if (targeting is null || targeting.Selection is TargetSelection.None or TargetSelection.All or TargetSelection.Random)
            {
                return null;
            }

            return await SelectTargetAsync(
                targeting.Relation == TargetRelation.Enemy ? "Select Battle Target" : "Select Ally Target",
                request,
                actor,
                targeting.Relation,
                cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<RuntimeInstanceId?> SelectTargetAsync(
            string prompt,
            BattleEncounterTurnRequest request,
            RuntimeActorState actor,
            TargetRelation relation,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<BattleEncounterParticipant> eligible = EligibleTargets(
                request.Participants,
                actor,
                relation);
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
                await _commands.ReadAsync(CreateTargetMenu(prompt, eligible, relation), cancellationToken)
                    .ConfigureAwait(false);
            if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
            {
                CancelledSelections++;
                return null;
            }

            return eligible.Count == 0 ? null : eligible[0].InstanceId;
        }

        private async ValueTask<BattleEncounterCommandResult> ExecuteCommandAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            BattleActionCommand command,
            CancellationToken cancellationToken)
        {
            BattleActionExecutionRequest actionRequest = CreateActionRequest(request, actor, command);
            BattleActionAssessment assessment = _actions.Assess(actionRequest);
            if (!assessment.CanExecute)
            {
                string diagnostics = string.Join("; ", assessment.Diagnostics.Select(diagnostic => diagnostic.Message));
                return BattleEncounterCommandResult.Rejected(
                    $"Battle action rejected: {ActionLabel(command)}; {diagnostics}");
            }

            BattleActionExecutionResult execution =
                await _actions.ExecuteAsync(actionRequest, cancellationToken).ConfigureAwait(false);
            if (execution.Status == BattleActionExecutionStatus.Rejected)
            {
                string diagnostics = string.Join("; ", execution.Diagnostics.Select(diagnostic => diagnostic.Message));
                return BattleEncounterCommandResult.Rejected(
                    $"Battle action rejected: {ActionLabel(command)}; {diagnostics}");
            }

            ContentId actionId = ActionId(command);
            _executedActionIds.Add(actionId);
            _executedEffectEvidence.AddRange(TypedEffectEvidence(command, actionId));
            _combatResolutionEvidence.AddRange(BuildCombatResolutionEvidence(command, actionId, execution));
            bool playerOwnedAction = actor.State.TeamId == TrainingAnnexHostSupport.PlayerTeam;
            TrainingAnnexBattleKnowledgeState knowledge = playerOwnedAction
                ? _playerBattleKnowledge
                : _encounterAiKnowledge;
            IReadOnlyList<TrainingAnnexBattleKnowledgeEvidence> learned =
                knowledge.LearnFromExecution(command, actionId, execution, _actors, _catalog);
            if (playerOwnedAction)
            {
                _battleKnowledgeEvidence.AddRange(learned);
            }
            else
            {
                _encounterAiKnowledgeEvidence.AddRange(learned);
            }
            _lifecycle.RecordActionEffects(actor.State.InstanceId, actionId, command, execution);
            _pressTurns.RecordBefore(
                actor.State.InstanceId,
                actionId,
                request.FullPressTurnIcons,
                request.BlinkingPressTurnIcons,
                execution.TurnConsumption);
            await _events.PublishAsync(
                $"Battle action executed: {actor.Entity.DisplayName} used {ActionLabel(command)}.",
                cancellationToken).ConfigureAwait(false);
            if (playerOwnedAction && learned.Count > 0)
            {
                await _events.PublishAsync(
                    $"Battle knowledge updated: {learned.Count} discover{(learned.Count == 1 ? "y" : "ies")}.",
                    cancellationToken).ConfigureAwait(false);
            }

            return BattleEncounterCommandResult.Executed(
                execution.TurnConsumption,
                MapExecutionEvents(actor, actionId, execution),
                execution.EscapeRequested ? BattleEncounterOutcome.Escape : null);
        }

        private BattleActionExecutionRequest CreateActionRequest(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            BattleActionCommand command) =>
            new(
                command,
                actor.State,
                request.Participants.Select(participant => participant.State),
                new EffectExecutionEnvironment(
                    request.Encounter.ContextId,
                    request.Encounter.BattleKindId,
                    request.Encounter.MoonPhaseId),
                command is ItemBattleActionCommand ? _inventory : null);

        private ValueTask PublishCurrentPressTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken) =>
            _events.PublishAsync(
                $"Press Turn before command: {request.FullPressTurnIcons} full, {request.BlinkingPressTurnIcons} blinking.",
                cancellationToken);

        private IReadOnlyList<SkillDefinition> KnownBattleSkills(CatalogBattleActor actor)
        {
            int level = _player.Actor.State.ToSnapshot().Progression.Level;
            return actor.SkillLoadout
                .Concat(actor.Entity.SkillUnlocks
                    .Where(unlock => unlock.Level <= level)
                    .Select(unlock => _catalog.GetRequiredSkill(unlock.SkillId)))
                .Where(skill => skill.Activation == SkillActivation.Active)
                .Where(IsBattleAvailable)
                .GroupBy(skill => skill.Id)
                .Select(group => group.First())
                .ToArray();
        }

        private static bool IsBattleAvailable(SkillDefinition skill) =>
            skill.Availability?.ContextIds.Contains(TrainingAnnexHostSupport.Battle) == true;

        private static SkillDefinition? SkillForCommand(
            CleanTrainingAnnexPlayCommand command,
            IReadOnlyList<SkillDefinition> skills) =>
            command switch
            {
                CleanTrainingAnnexPlayCommand.UseFrostTip =>
                    skills.FirstOrDefault(skill => skill.Id == TrainingAnnexHostSupport.FrostTip),
                CleanTrainingAnnexPlayCommand.UseEchoStrike =>
                    skills.FirstOrDefault(skill => skill.Id == TrainingAnnexHostSupport.EchoStrike),
                CleanTrainingAnnexPlayCommand.UseMend =>
                    skills.FirstOrDefault(skill => skill.Id == TrainingAnnexHostSupport.Mend),
                CleanTrainingAnnexPlayCommand.UseToxinTouch =>
                    skills.FirstOrDefault(skill => skill.Id == TrainingAnnexHostSupport.ToxinTouch),
                CleanTrainingAnnexPlayCommand.UseClearToxin =>
                    skills.FirstOrDefault(skill => skill.Id == TrainingAnnexHostSupport.ClearToxin),
                _ => null
            };

        private static RuntimeInstanceId? FirstEligibleTarget(
            RuntimeActorState actor,
            IEnumerable<BattleEncounterParticipant> participants,
            TargetingDefinition? targeting)
        {
            if (targeting is null || targeting.Selection is TargetSelection.None or TargetSelection.All or TargetSelection.Random)
            {
                return null;
            }

            return EligibleTargets(participants, actor, targeting.Relation)
                .FirstOrDefault()
                ?.InstanceId;
        }

        private static IReadOnlyList<BattleEncounterParticipant> EligibleTargets(
            IEnumerable<BattleEncounterParticipant> participants,
            RuntimeActorState actor,
            TargetRelation relation) =>
            participants
                .Where(participant => participant.State.IsActive && !participant.State.IsDefeated)
                .Where(participant => relation switch
                {
                    TargetRelation.Enemy => participant.TeamId != actor.TeamId,
                    TargetRelation.Ally => participant.TeamId == actor.TeamId,
                    TargetRelation.Self => participant.InstanceId == actor.InstanceId,
                    TargetRelation.Any => true,
                    _ => false
                })
                .ToArray();

        private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateBattleCommandMenu(
            CatalogBattleActor actor) =>
            new(
                $"Clean Battle - {actor.Entity.DisplayName}",
                [
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.BattleAttack,
                        "Attack"),
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.OpenBattleSkills,
                        "Skills"),
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.OpenBattleItems,
                        "Item"),
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.BattleGuard,
                        "Guard"),
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.BattlePass,
                        "Pass"),
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.BattleAnalyze,
                        "Analyze")
                ]);

        private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateBattleSkillMenu(
            IReadOnlyList<SkillDefinition> skills)
        {
            var options = new List<HostCommandOption<CleanTrainingAnnexPlayCommand>>();
            foreach (SkillDefinition skill in skills)
            {
                CleanTrainingAnnexPlayCommand command =
                    skill.Id == TrainingAnnexHostSupport.FrostTip
                        ? CleanTrainingAnnexPlayCommand.UseFrostTip
                        : skill.Id == TrainingAnnexHostSupport.EchoStrike
                            ? CleanTrainingAnnexPlayCommand.UseEchoStrike
                            : skill.Id == TrainingAnnexHostSupport.Mend
                                ? CleanTrainingAnnexPlayCommand.UseMend
                                : skill.Id == TrainingAnnexHostSupport.ToxinTouch
                                    ? CleanTrainingAnnexPlayCommand.UseToxinTouch
                                    : skill.Id == TrainingAnnexHostSupport.ClearToxin
                                        ? CleanTrainingAnnexPlayCommand.UseClearToxin
                                        : CleanTrainingAnnexPlayCommand.Back;
                options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    command,
                    skill.DisplayName,
                    command != CleanTrainingAnnexPlayCommand.Back,
                    skill.Description));
            }

            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.Back,
                "Back"));
            return new HostCommandRequest<CleanTrainingAnnexPlayCommand>("Clean Battle Skills", options);
        }

        private HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateBattleItemMenu(ItemDefinition tonic) =>
            new(
                "Clean Battle Items",
                [
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.UseAnnexTonic,
                        $"{tonic.DisplayName} x{_inventory.Snapshot.GetQuantity(tonic.Id)}",
                        _inventory.Snapshot.GetQuantity(tonic.Id) > 0,
                        tonic.Description),
                    new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.Back,
                        "Back")
                ]);

        private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateTargetMenu(
            string prompt,
            IReadOnlyList<BattleEncounterParticipant> eligible,
            TargetRelation relation)
        {
            var options = eligible
                .Select(participant => new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    relation == TargetRelation.Enemy
                        ? CleanTrainingAnnexPlayCommand.TargetEnemy
                        : CleanTrainingAnnexPlayCommand.TargetPlayer,
                    participant.DisplayName))
                .ToList();
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.Back,
                "Back"));
            return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(prompt, options);
        }

        private static ContentId ActionId(BattleActionCommand command) =>
            command switch
            {
                BasicAttackBattleActionCommand basic => basic.ActionId,
                SkillBattleActionCommand skill => skill.Skill.Id,
                ItemBattleActionCommand item => item.Item.Id,
                GuardBattleActionCommand => GuardAction,
                PassBattleActionCommand => PassAction,
                AnalyzeBattleActionCommand => AnalyzeAction,
                _ => ContentId.Parse(command.Kind.ToString().ToLowerInvariant())
            };

        private static IReadOnlyList<TrainingAnnexTypedEffectEvidence> TypedEffectEvidence(
            BattleActionCommand command,
            ContentId actionId) =>
            command switch
            {
                BasicAttackBattleActionCommand basic =>
                    [new TrainingAnnexTypedEffectEvidence(actionId, 0, "damage", basic.BasicAttack.Element)],
                SkillBattleActionCommand skill => skill.Skill.Effects
                    .Select((effect, index) => TypedEffectEvidence(actionId, index, effect))
                    .ToArray(),
                ItemBattleActionCommand item => item.Item.Usage?.Effects
                    .Select((effect, index) => TypedEffectEvidence(actionId, index, effect))
                    .ToArray() ?? [],
                AnalyzeBattleActionCommand => [new TrainingAnnexTypedEffectEvidence(actionId, 0, "analyze")],
                _ => []
            };

        private static IReadOnlyList<TrainingAnnexCombatResolutionEvidence> BuildCombatResolutionEvidence(
            BattleActionCommand command,
            ContentId actionId,
            BattleActionExecutionResult execution) =>
            execution.Effects
                .Select(effect => BuildCombatResolutionEvidence(command, actionId, effect))
                .ToArray();

        private static TrainingAnnexCombatResolutionEvidence BuildCombatResolutionEvidence(
            BattleActionCommand command,
            ContentId actionId,
            EffectExecutionResult result)
        {
            DamageEffectDefinition? damage = DamageEffect(command, result.EffectIndex);
            bool? hit = damage is null || result.Outcome == EffectExecutionOutcome.Skipped
                ? null
                : !(result.Outcome == EffectExecutionOutcome.Failure &&
                    result.PressTurnOutcome == PressTurnOutcome.Miss);
            return new TrainingAnnexCombatResolutionEvidence(
                actionId,
                result.EffectIndex,
                result.TargetId,
                damage?.Element,
                damage?.Power,
                damage?.Accuracy,
                damage?.Critical.Mode,
                hit,
                result.IsCritical,
                result.ResolvedAffinity,
                result.Value,
                result.Outcome,
                result.PressTurnOutcome);
        }

        private static DamageEffectDefinition? DamageEffect(BattleActionCommand command, int effectIndex) =>
            command switch
            {
                BasicAttackBattleActionCommand basic when effectIndex == 0 => new DamageEffectDefinition(
                    basic.BasicAttack.Element,
                    basic.BasicAttack.Power,
                    basic.BasicAttack.Accuracy,
                    new NeverCriticalDefinition(),
                    new HitCountDefinition(1, 1)),
                SkillBattleActionCommand skill when effectIndex >= 0 && effectIndex < skill.Skill.Effects.Count =>
                    skill.Skill.Effects[effectIndex] as DamageEffectDefinition,
                ItemBattleActionCommand item when item.Item.Usage is not null &&
                    effectIndex >= 0 && effectIndex < item.Item.Usage.Effects.Count =>
                    item.Item.Usage.Effects[effectIndex] as DamageEffectDefinition,
                _ => null
            };

        private static TrainingAnnexTypedEffectEvidence TypedEffectEvidence(
            ContentId actionId,
            int index,
            EffectDefinition effect) =>
            effect switch
            {
                DamageEffectDefinition damage =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "damage", damage.Element),
                InstantKillEffectDefinition =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "instant_kill"),
                ApplyAilmentEffectDefinition apply =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "apply_ailment", RelatedContentId: apply.AilmentId),
                RestoreResourceEffectDefinition restore =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "restore_resource", ResourceId: restore.ResourceId),
                RemoveAilmentEffectDefinition remove =>
                    new TrainingAnnexTypedEffectEvidence(
                        actionId,
                        index,
                        "remove_ailment",
                        RelatedContentId: remove.AilmentIds.FirstOrDefault()),
                ReviveEffectDefinition revive =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "revive", ResourceId: revive.ResourceId),
                ModifyStatStageEffectDefinition modify =>
                    new TrainingAnnexTypedEffectEvidence(
                        actionId,
                        index,
                        "modify_stat_stage",
                        RelatedContentId: modify.ModifierTrackIds.FirstOrDefault()),
                GrantChargeEffectDefinition =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "grant_charge"),
                GrantShieldEffectDefinition =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "grant_shield"),
                OverrideAffinityEffectDefinition affinity =>
                    new TrainingAnnexTypedEffectEvidence(
                        actionId,
                        index,
                        "override_affinity",
                        affinity.Elements.FirstOrDefault()),
                RemoveStatusEffectDefinition remove =>
                    new TrainingAnnexTypedEffectEvidence(
                        actionId,
                        index,
                        "remove_status",
                        RelatedContentId: remove.StatusIds.FirstOrDefault()),
                ReduceResourceEffectDefinition reduce =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "reduce_resource", ResourceId: reduce.ResourceId),
                SetResourceEffectDefinition set =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "set_resource", ResourceId: set.ResourceId),
                AnalyzeEffectDefinition =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "analyze"),
                EscapeEffectDefinition escape =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "escape", RelatedContentId: escape.EligibilityRuleId),
                CustomEffectDefinition custom =>
                    new TrainingAnnexTypedEffectEvidence(actionId, index, "custom", RelatedContentId: custom.HandlerId),
                _ => throw new InvalidOperationException($"Unsupported typed effect '{effect.GetType().Name}'.")
            };

        private static string ActionLabel(BattleActionCommand command) =>
            command switch
            {
                BasicAttackBattleActionCommand => "Practice Blade",
                SkillBattleActionCommand skill => skill.Skill.DisplayName,
                ItemBattleActionCommand item => item.Item.DisplayName,
                GuardBattleActionCommand => "Guard",
                PassBattleActionCommand => "Pass",
                AnalyzeBattleActionCommand => "Analyze",
                _ => command.Kind.ToString()
            };

        private static IReadOnlyList<BattleEncounterEvent> MapExecutionEvents(
            CatalogBattleActor actor,
            ContentId actionId,
            BattleActionExecutionResult execution)
        {
            var events = new List<BattleEncounterEvent>
            {
                new(
                    0,
                    BattleEncounterEventKind.CommandSelected,
                    $"{actor.State.InstanceId} selected {actionId}.",
                    actor.State.InstanceId,
                    SourceId: actionId)
            };

            foreach (EffectExecutionResult effect in execution.Effects)
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.EffectResolved,
                    $"Effect {effect.EffectIndex} resolved as {effect.Outcome} ({effect.PressTurnOutcome}).",
                    actor.State.InstanceId,
                    effect.TargetId,
                    actionId,
                    effect.Value));
                if (effect.Value is decimal value)
                {
                    events.Add(new BattleEncounterEvent(
                        0,
                        BattleEncounterEventKind.ResourceChanged,
                        $"Resource changed by {value}.",
                        actor.State.InstanceId,
                        effect.TargetId,
                        actionId,
                        value));
                }
            }

            foreach (BattleActionEvent actionEvent in execution.Events)
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.ActionExecuted,
                    actionEvent.Message,
                    actionEvent.ActorId,
                    actionEvent.TargetId,
                    actionEvent.SourceId,
                    actionEvent.Value));
            }

            return events;
        }
    }
}

internal sealed class TrainingAnnexLifecycleTracker
{
    private readonly List<TrainingAnnexLifecycleEvidence> _evidence = [];

    public IReadOnlyList<TrainingAnnexLifecycleEvidence> Evidence => _evidence.ToArray();

    public void RecordStatusEvents(
        IEnumerable<BattleStatusLifecycleEvent> events,
        BattleTurnStartOutcome? turnStartOutcome = null)
    {
        foreach (BattleStatusLifecycleEvent statusEvent in events)
        {
            _evidence.Add(new TrainingAnnexLifecycleEvidence(
                statusEvent.ActorId,
                statusEvent.Kind,
                statusEvent.RelatedId,
                statusEvent.Value,
                statusEvent.Detail,
                turnStartOutcome));
        }
    }

    public void RecordActionEffects(
        RuntimeInstanceId actorId,
        ContentId actionId,
        BattleActionCommand command,
        BattleActionExecutionResult execution)
    {
        foreach (EffectExecutionResult result in execution.Effects
                     .Where(result => result.Outcome == EffectExecutionOutcome.Success))
        {
            EffectDefinition? definition = EffectDefinition(command, result.EffectIndex);
            if (definition is ApplyAilmentEffectDefinition apply)
            {
                _evidence.Add(new TrainingAnnexLifecycleEvidence(
                    result.TargetId ?? actorId,
                    BattleStatusLifecycleEventKind.AilmentApplied,
                    apply.AilmentId,
                    SourceActionId: actionId));
            }
            else if (definition is RemoveAilmentEffectDefinition remove &&
                     result.Value is decimal removedCount &&
                     removedCount > 0)
            {
                _evidence.Add(new TrainingAnnexLifecycleEvidence(
                    result.TargetId ?? actorId,
                    BattleStatusLifecycleEventKind.AilmentRemoved,
                    remove.AilmentIds.FirstOrDefault(),
                    removedCount,
                    result.Detail,
                    SourceActionId: actionId));
            }
        }
    }

    private static EffectDefinition? EffectDefinition(BattleActionCommand command, int effectIndex) =>
        command switch
        {
            SkillBattleActionCommand skill when effectIndex >= 0 && effectIndex < skill.Skill.Effects.Count =>
                skill.Skill.Effects[effectIndex],
            ItemBattleActionCommand item when item.Item.Usage is not null &&
                effectIndex >= 0 &&
                effectIndex < item.Item.Usage.Effects.Count =>
                item.Item.Usage.Effects[effectIndex],
            _ => null
        };
}

internal sealed class TrainingAnnexBattleLifecyclePort : IBattleEncounterLifecyclePort
{
    private static readonly ContentId BattleStart = ContentId.Parse("battle_start");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");

    private readonly IBattleStatusLifecycleService _lifecycle;
    private readonly BattleExecutionServices _services;
    private readonly TrainingAnnexLifecycleTracker _tracker;

    public TrainingAnnexBattleLifecyclePort(
        IBattleStatusLifecycleService lifecycle,
        BattleExecutionServices services,
        TrainingAnnexLifecycleTracker tracker)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
        BattleEncounterLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RuntimeActorState[] participants = request.Participants
            .Select(participant => participant.State)
            .ToArray();
        var events = new List<BattleEncounterEvent>();
        foreach (RuntimeActorState actor in participants)
        {
            PassiveTriggerDispatchResult dispatch = _services.PassiveTriggers.Dispatch(
                new PassiveTriggerDispatchRequest(
                    BattleStart,
                    actor,
                    participants,
                    [actor],
                    request.Encounter.ContextId,
                    request.Encounter.BattleKindId,
                    request.Encounter.MoonPhaseId),
                _services);
            BattleStatusLifecycleEvent[] statusEvents = MapPassiveActivations(actor, dispatch);
            _tracker.RecordStatusEvents(statusEvents);
            events.AddRange(MapStatusEvents(statusEvents));
        }

        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.AsReadOnly(events.ToArray()));
    }

    public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BattleTurnStartLifecycleResult result = _lifecycle.ProcessTurnStart(new(
            request.Actor.State,
            request.CanReturnToStock));
        _tracker.RecordStatusEvents(result.Events, result.Outcome);
        return new ValueTask<BattleTurnStartLifecycleResult>(result);
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
        BattleEncounterTurnLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RuntimeActorState[] participants = request.Participants
            .Select(participant => participant.State)
            .ToArray();
        BattleTurnEndLifecycleResult result = _lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                request.Actor.State,
                participants,
                request.Encounter.ContextId,
                OwnerTurnEnd,
                request.Encounter.BattleKindId,
                request.Encounter.MoonPhaseId),
            _services);
        _tracker.RecordStatusEvents(result.Events);
        return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(MapStatusEvents(result.Events));
    }

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
        BattleEncounterLifecycleRequest request,
        ContentId teamId,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());

    public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
        BattleEncounterLifecycleRequest request,
        BattleEncounterOutcome outcome,
        CancellationToken cancellationToken = default) =>
        new(Array.Empty<BattleEncounterEvent>());

    private static IReadOnlyList<BattleEncounterEvent> MapStatusEvents(
        IEnumerable<BattleStatusLifecycleEvent> events) =>
        Array.AsReadOnly(events.Select(statusEvent => new BattleEncounterEvent(
            0,
            EncounterEventKind(statusEvent.Kind),
            StatusMessage(statusEvent),
            statusEvent.ActorId,
            SourceId: statusEvent.RelatedId,
            Value: statusEvent.Value)).ToArray());

    private static BattleEncounterEventKind EncounterEventKind(BattleStatusLifecycleEventKind kind) =>
        kind switch
        {
            BattleStatusLifecycleEventKind.ResourceChanged => BattleEncounterEventKind.ResourceChanged,
            BattleStatusLifecycleEventKind.PassiveTriggered => BattleEncounterEventKind.PassiveActivated,
            _ => BattleEncounterEventKind.StatusChanged
        };

    private static string StatusMessage(BattleStatusLifecycleEvent statusEvent) =>
        statusEvent.Kind switch
        {
            BattleStatusLifecycleEventKind.ResourceChanged when statusEvent.RelatedId is ContentId resource =>
                $"Lifecycle resource changed: {resource} {statusEvent.Value:+0.##;-0.##;0}.",
            BattleStatusLifecycleEventKind.AilmentRecovered =>
                $"Lifecycle ailment recovered: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.AilmentExpired =>
                $"Lifecycle ailment expired: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.AilmentRemoved =>
                $"Lifecycle ailment removed: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.PassiveTriggered =>
                $"Lifecycle passive triggered: {statusEvent.RelatedId}.",
            BattleStatusLifecycleEventKind.StatusExpired =>
                $"Lifecycle status expired: {statusEvent.RelatedId}.",
            _ => $"Lifecycle status changed: {statusEvent.Kind}."
        };

    private static BattleStatusLifecycleEvent[] MapPassiveActivations(
        RuntimeActorState actor,
        PassiveTriggerDispatchResult dispatch)
    {
        var events = new List<BattleStatusLifecycleEvent>();
        foreach (PassiveTriggerExecutionResult activation in dispatch.Activations
                     .Where(activation => activation.Outcome == PassiveTriggerOutcome.Executed))
        {
            events.Add(new BattleStatusLifecycleEvent(
                BattleStatusLifecycleEventKind.PassiveTriggered,
                actor.InstanceId,
                activation.SkillId,
                Detail: activation.EventId.ToString()));
            foreach (EffectExecutionResult effect in activation.Effects)
            {
                if (effect.RelatedId is ContentId relatedId && effect.Value is decimal value)
                {
                    events.Add(new BattleStatusLifecycleEvent(
                        BattleStatusLifecycleEventKind.ResourceChanged,
                        effect.TargetId ?? actor.InstanceId,
                        RelatedId: relatedId,
                        Value: value,
                        Detail: effect.Detail));
                }
            }
        }

        return events.ToArray();
    }
}

internal sealed class TrainingAnnexPressTurnTracker
{
    private readonly List<TrainingAnnexPressTurnEvidence> _evidence = [];

    public IReadOnlyList<TrainingAnnexPressTurnEvidence> Evidence => _evidence.ToArray();

    public void RecordBefore(
        RuntimeInstanceId actorId,
        ContentId actionId,
        int beforeFullIcons,
        int beforeBlinkingIcons,
        ActionTurnConsumption turnConsumption)
    {
        _evidence.Add(new TrainingAnnexPressTurnEvidence(
            actorId,
            actionId,
            beforeFullIcons,
            beforeBlinkingIcons,
            turnConsumption.Kind,
            turnConsumption.PressTurn?.Outcome,
            AfterFullIcons: -1,
            AfterBlinkingIcons: -1));
    }

    public bool TryRecordAfter(RuntimeInstanceId? actorId, int afterFullIcons, int afterBlinkingIcons)
    {
        int index = _evidence.FindIndex(record =>
            record.AfterFullIcons < 0 &&
            (actorId is null || record.ActorId == actorId));
        if (index < 0)
        {
            return false;
        }

        _evidence[index] = _evidence[index] with
        {
            AfterFullIcons = afterFullIcons,
            AfterBlinkingIcons = afterBlinkingIcons
        };
        return true;
    }
}

internal sealed class TrainingAnnexPressTurnEventSink(
    IHostEventSink<string> events,
    TrainingAnnexPressTurnTracker tracker) : IBattleEncounterEventSink
{
    public async ValueTask PublishAsync(
        BattleEncounterEvent battleEvent,
        CancellationToken cancellationToken = default)
    {
        if (battleEvent.Kind == BattleEncounterEventKind.PressTurnChanged &&
            TryParsePressTurnCounts(battleEvent.Message, out int fullIcons, out int blinkingIcons))
        {
            tracker.TryRecordAfter(battleEvent.ActorId, fullIcons, blinkingIcons);
            await events.PublishAsync(
                $"Press Turn updated: {fullIcons} full, {blinkingIcons} blinking.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (battleEvent.Kind is BattleEncounterEventKind.PassiveActivated or
            BattleEncounterEventKind.StatusChanged or
            BattleEncounterEventKind.ResourceChanged or
            BattleEncounterEventKind.TurnRestricted)
        {
            await events.PublishAsync(battleEvent.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool TryParsePressTurnCounts(string message, out int fullIcons, out int blinkingIcons)
    {
        fullIcons = 0;
        blinkingIcons = 0;
        const string prefix = "Press Turn: ";
        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string[] parts = message[prefix.Length..].TrimEnd('.').Split(", ");
        return parts.Length == 2 &&
               int.TryParse(parts[0].Replace(" full", "", StringComparison.Ordinal), out fullIcons) &&
               int.TryParse(parts[1].Replace(" blinking", "", StringComparison.Ordinal), out blinkingIcons);
    }
}
