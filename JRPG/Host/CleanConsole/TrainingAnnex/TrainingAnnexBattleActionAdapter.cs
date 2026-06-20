using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
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
    int CancelledSelections,
    int EventCount);

internal sealed record TrainingAnnexTypedEffectEvidence(
    ContentId SourceActionId,
    int EffectIndex,
    string EffectKind,
    DamageElement? DamageElement = null,
    ContentId? ResourceId = null,
    ContentId? RelatedContentId = null);

internal sealed class TrainingAnnexBattleActionAdapter
{
    private static readonly ContentId GuardAction = ContentId.Parse("guard");
    private static readonly ContentId PassAction = ContentId.Parse("pass");
    private static readonly ContentId AnalyzeAction = ContentId.Parse("analyze");

    private readonly GameDataCatalog _catalog;
    private readonly IHostEventSink<string> _events;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commands;
    private readonly BattleExecutionServices _services;

    public TrainingAnnexBattleActionAdapter(
        GameDataCatalog catalog,
        IHostEventSink<string> events,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commands,
        BattleExecutionServices services)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public async ValueTask<TrainingAnnexManualBattleSummary> RunAsync(
        TrainingAnnexRuntimeActor player,
        PreparedEncounter prepared,
        TrainingAnnexItemActionInventory inventory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(inventory);

        SynchronizeActionResources(player);

        CatalogBattleActor[] actors = [player.Actor, .. prepared.Actors];
        BattleEncounterParticipant[] participants = actors
            .Select(actor => new BattleEncounterParticipant(actor.State, actor.Entity.DisplayName))
            .ToArray();

        var turnHandler = new TrainingAnnexManualBattleTurnHandler(
            _catalog,
            _events,
            _commands,
            new BattleActionExecutor(new SkillExecutor(_services), new ItemExecutor(_services), _services),
            actors,
            player,
            inventory);
        var services = new BattleEncounterServices(
            new ParticipantOrderInitiativePolicy(),
            NoopBattleEncounterLifecyclePort.Instance,
            turnHandler,
            new LastTeamStandingCompletionPolicy());

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

        SynchronizePersistentResources(player);

        await _events.PublishAsync(
            result.WinningTeamId is ContentId winner
                ? $"Clean battle ended: {result.Outcome}; winner {winner}."
                : $"Clean battle ended: {result.Outcome}.",
            cancellationToken).ConfigureAwait(false);

        return new TrainingAnnexManualBattleSummary(
            true,
            result.Outcome,
            result.WinningTeamId,
            turnHandler.ExecutedActionIds,
            turnHandler.ExecutedEffectEvidence,
            turnHandler.CancelledSelections,
            result.Events.Count);
    }

    private static void SynchronizeActionResources(TrainingAnnexRuntimeActor actor)
    {
        foreach (RuntimeResourceSnapshot resource in actor.RuntimeState.ToSnapshot().Resources)
        {
            actor.Actor.State.SetResource(resource.ResourceId, resource.Current);
        }
    }

    private static void SynchronizePersistentResources(TrainingAnnexRuntimeActor actor)
    {
        var resources = new RuntimeResourceTransactionService();
        foreach (BattleResourceState resource in actor.Actor.State.Resources.Values)
        {
            RuntimeMutationResult result = resources.SetResource(
                actor.RuntimeState,
                resource.Id,
                resource.Current);
            if (!result.Applied)
            {
                throw new InvalidOperationException(
                    $"Could not synchronize battle resource '{resource.Id}'.");
            }
        }
    }

    private sealed class TrainingAnnexManualBattleTurnHandler : IBattleEncounterTurnHandler
    {
        private readonly GameDataCatalog _catalog;
        private readonly IHostEventSink<string> _events;
        private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commands;
        private readonly IBattleActionExecutor _actions;
        private readonly IReadOnlyList<CatalogBattleActor> _actors;
        private readonly TrainingAnnexRuntimeActor _player;
        private readonly TrainingAnnexItemActionInventory _inventory;
        private readonly List<ContentId> _executedActionIds = [];
        private readonly List<TrainingAnnexTypedEffectEvidence> _executedEffectEvidence = [];

        public TrainingAnnexManualBattleTurnHandler(
            GameDataCatalog catalog,
            IHostEventSink<string> events,
            IHostCommandSource<CleanTrainingAnnexPlayCommand> commands,
            IBattleActionExecutor actions,
            IReadOnlyList<CatalogBattleActor> actors,
            TrainingAnnexRuntimeActor player,
            TrainingAnnexItemActionInventory inventory)
        {
            _catalog = catalog;
            _events = events;
            _commands = commands;
            _actions = actions;
            _actors = actors;
            _player = player;
            _inventory = inventory;
        }

        public IReadOnlyList<ContentId> ExecutedActionIds => _executedActionIds;
        public IReadOnlyList<TrainingAnnexTypedEffectEvidence> ExecutedEffectEvidence => _executedEffectEvidence;
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
            foreach (SkillDefinition skill in actor.ActiveSkills.Where(IsBattleAvailable))
            {
                ContentId? target = FirstEligibleTarget(actor.State, request.Participants, skill.Targeting);
                if (target is null && skill.Targeting?.Selection == TargetSelection.Single)
                {
                    continue;
                }

                var command = new SkillBattleActionCommand(
                    skill,
                    target is ContentId selected ? [selected] : []);
                BattleActionExecutionRequest actionRequest = CreateActionRequest(request, actor, command);
                if (!_actions.Assess(actionRequest).CanExecute)
                {
                    continue;
                }

                return await ExecuteCommandAsync(request, actor, command, cancellationToken)
                    .ConfigureAwait(false);
            }

            return await ExecuteCommandAsync(
                request,
                actor,
                new PassBattleActionCommand(),
                cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<BattleActionCommand?> SelectBasicAttackAsync(
            BattleEncounterTurnRequest request,
            CatalogBattleActor actor,
            CancellationToken cancellationToken)
        {
            ContentId? target = await SelectTargetAsync(
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

            ContentId? target = await SelectTargetForTargetingAsync(
                request,
                actor.State,
                skill.Targeting,
                cancellationToken).ConfigureAwait(false);
            return target is null && skill.Targeting?.Selection == TargetSelection.Single
                ? null
                : new SkillBattleActionCommand(skill, target is ContentId selected ? [selected] : []);
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

            ContentId? target = await SelectTargetForTargetingAsync(
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
            ContentId? target = await SelectTargetAsync(
                "Select Analyze Target",
                request,
                actor.State,
                TargetRelation.Enemy,
                cancellationToken).ConfigureAwait(false);
            return target is null
                ? null
                : new AnalyzeBattleActionCommand(target.Value, [AnalysisLayer.Full]);
        }

        private async ValueTask<ContentId?> SelectTargetForTargetingAsync(
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

        private async ValueTask<ContentId?> SelectTargetAsync(
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
            await _events.PublishAsync(
                $"Battle action executed: {actor.Entity.DisplayName} used {ActionLabel(command)}.",
                cancellationToken).ConfigureAwait(false);

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

        private IReadOnlyList<SkillDefinition> KnownBattleSkills(CatalogBattleActor actor)
        {
            int level = _player.RuntimeState.ToSnapshot().Progression.Level;
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
                _ => null
            };

        private static ContentId? FirstEligibleTarget(
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
