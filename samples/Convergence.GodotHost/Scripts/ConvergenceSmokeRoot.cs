using System.Text.Json.Nodes;
using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.GodotHost.Infrastructure;
using Convergence.Hosting;
using Convergence.Runtime;
using Convergence.Validation;
using Godot;

namespace Convergence.GodotHost;

public partial class ConvergenceSmokeRoot : Node
{
    private const string PackId = "convergence.training_annex_slice";
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");

    public override async void _Ready()
    {
        if (!OS.GetCmdlineUserArgs().Contains("--convergence-smoke", StringComparer.Ordinal))
        {
            GD.Print("Convergence Godot Host is ready. Pass --convergence-smoke for the noninteractive proof.");
            return;
        }

        int exitCode;
        try
        {
            await RunSmokeAsync();
            exitCode = 0;
        }
        catch (Exception exception)
        {
            GD.PushError($"CONVERGENCE_GODOT_SMOKE_FAILED: {exception}");
            exitCode = 1;
        }

        GetTree().Quit(exitCode);
    }

    private async Task RunSmokeAsync()
    {
        GD.Print("CONVERGENCE_GODOT_SMOKE_BEGIN");
        var source = new GodotResourceContentSource("res://Content/training-annex");
        ContentPackTextBundle bundle = await source.ReadAsync(CreateContentRequest());
        CatalogLoadResult loaded = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(BuildRegistrations(), [bundle]));
        if (!loaded.IsSuccess || loaded.Catalog is null)
        {
            throw new InvalidDataException(string.Join(
                System.Environment.NewLine,
                loaded.Diagnostics.Select(diagnostic =>
                    $"[{diagnostic.Code}] {diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}")));
        }

        GameDataCatalog catalog = loaded.Catalog;
        GD.Print($"GODOT_CONTENT_OK packs={catalog.ContentPacks.Count} entities={catalog.Entities.Count}");

        var random = new MinimumRandomSource();
        var rulesets = new RuntimeRulesetBindingResolver(RuntimeRulesetPolicyFactoryRegistry.CreateStandard());
        StatRulesetServices statServices = rulesets.BindStatServices(
            catalog,
            Qualified("standard_stat")).RequireService();
        IStatModifierPolicyService statModifiers = rulesets.BindStatModifierPolicy(
            catalog,
            Qualified("standard_stat_modifiers")).RequireService();
        CombatExecutionPolicySet combat = rulesets.BindCombatPolicies(
            catalog,
            Qualified("standard_damage"),
            random,
            statServices.StageScalingPolicy).RequireService();
        BattleTurnEconomyRuleset turnEconomy = rulesets.BindTurnEconomy(
            catalog,
            Qualified("standard_action_token")).RequireService();
        GrowthRulesetServices growthServices = rulesets.BindGrowthServices(
            catalog,
            Qualified("standard_growth")).RequireService();
        IRosterCapacityPolicy rosterCapacity = rulesets.BindRosterCapacityPolicy(
            catalog,
            Qualified("standard_roster_capacity")).RequireService();
        var moveListCapacity = new SharedRuntimeMoveListCapacityPolicy();
        var composition = new RuntimeActorCombatProfileCompositionService(
            statServices.StatResolutionPolicy,
            growthServices.ResourceGrowthPolicy,
            catalog,
            rosterCapacity);
        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new GodotActorInitializationPolicy(),
            catalog,
            composition,
            moveListCapacity);
        CatalogBattleActor player = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("echo_adept"),
            RuntimeInstanceId.Parse("godot_echo_adept"),
            PlayerTeam,
            3,
            IsDeployed: true,
            ContentId.Parse("godot_player"))).RequireActor();
        CatalogBattleActor enemy = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("ashling"),
            RuntimeInstanceId.Parse("godot_ashling"),
            EnemyTeam,
            2,
            IsDeployed: true,
            ContentId.Parse("godot_ai"))).RequireActor();
        CatalogBattleActor hostedEntity = actorFactory.Create(
            new CatalogBattleActorCreationRequest(
                Qualified("annex_mentor"),
                RuntimeInstanceId.Parse("godot_annex_mentor"),
                PlayerTeam,
                5,
                IsDeployed: false,
                ContentId.Parse("godot_player"))).RequireActor();
        RuntimeActorReferenceSnapshot playerReference = Reference(player);
        RuntimeActorReferenceSnapshot hostedEntityReference = Reference(hostedEntity);
        var partyRoster = new RuntimePartyRosterSnapshot(
            playerReference,
            activeParty: [playerReference],
            activeHostedEntity: hostedEntityReference,
            hostedEntityRoster: [hostedEntityReference]);
        RuntimeActorCombatProfileCompositionResult composed = composition.Compose(
            new RuntimeActorCombatProfileCompositionRequest(
                player.State,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster,
                [hostedEntity.State]));
        if (!composed.Applied)
        {
            throw new InvalidOperationException(
                "The Godot Vessel could not compose its Active Hosted Entity profile: " +
                string.Join("; ", composed.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        Node actorRoot = GetNode<Node>("ActorScenes");
        var playerNode = new Node { Name = "EchoAdept" };
        var enemyNode = new Node { Name = "Ashling" };
        var hostedEntityNode = new Node { Name = "AnnexMentor" };
        actorRoot.AddChild(playerNode);
        actorRoot.AddChild(enemyNode);
        actorRoot.AddChild(hostedEntityNode);
        var sceneInstances = new GodotSceneInstanceRegistry();
        sceneInstances.Attach(player.State.InstanceId, playerNode);
        sceneInstances.Attach(enemy.State.InstanceId, enemyNode);
        sceneInstances.Attach(hostedEntity.State.InstanceId, hostedEntityNode);
        GD.Print("GODOT_SCENE_MAP_OK count=3");

        BattleExecutionServices executionServices = new(
            catalog,
            combat.Damage,
            combat.InstantDefeat,
            combat.Ailments,
            combat.Chance,
            combat.Amounts,
            new FirstSkillTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            statModifiers,
            combat.Charges,
            actionOutcomes: combat.ActionOutcomes);
        var actionExecutor = new BattleActionExecutor(
            new SkillExecutor(executionServices),
            new ItemExecutor(executionServices),
            executionServices,
            new CatalogBattleActionAuthorizationPolicy(
                catalog,
                catalog,
                NoBattleBasicAttackProfileSource.Instance));
        SkillDefinition frostTip = player.ActiveSkills.Single(skill => skill.Id == Qualified("frost_tip"));
        BattleActionCommand skillCommand = new SkillBattleActionCommand(
            frostTip,
            [enemy.State.InstanceId]);
        var commandSource = new GodotCommandSource<BattleActionCommand>();
        commandSource.Submit(
            skillCommand,
            HostCommandSelectionIdentity.ForContent(frostTip.Id));
        HostCommandReadResult<BattleActionCommand> selected = await commandSource.ReadAsync(
            new HostCommandRequest<BattleActionCommand>(
                "Choose an action",
                [
                    new HostCommandOption<BattleActionCommand>(
                        skillCommand,
                        frostTip.DisplayName,
                        SelectionIdentity: HostCommandSelectionIdentity.ForContent(frostTip.Id))
                ]));
        BattleActionExecutionResult action = await actionExecutor.ExecuteAsync(
            new BattleActionExecutionRequest(
                selected.Command ?? throw new InvalidOperationException("Godot command selection was empty."),
                player.State,
                [player.State, enemy.State],
                new EffectExecutionEnvironment(Battle, NormalBattle)));
        if (action.Status != BattleActionExecutionStatus.Executed || action.Effects.Count == 0)
        {
            throw new InvalidOperationException("The Godot-selected framework action did not execute.");
        }

        GD.Print($"GODOT_ACTION_OK kind={action.Kind} effects={action.Effects.Count}");

        SkillDefinition focusCall = player.ActiveSkills.Single(skill =>
            skill.Id == Qualified("focus_call"));
        BattleActionExecutionResult modifierAction = await actionExecutor.ExecuteAsync(
            new BattleActionExecutionRequest(
                new SkillBattleActionCommand(focusCall, [player.State.InstanceId]),
                player.State,
                [player.State, enemy.State],
                new EffectExecutionEnvironment(Battle, NormalBattle)));
        RuntimeStatModifierStateSnapshot savedModifierState =
            player.State.StatModifierState ??
            throw new InvalidOperationException(
                "The Godot-selected stat-modifier action produced no retained policy state.");
        if (modifierAction.Status != BattleActionExecutionStatus.Executed ||
            modifierAction.Effects.Count == 0)
        {
            throw new InvalidOperationException(
                "The Godot-selected stat-modifier action did not execute.");
        }

        GD.Print(
            $"GODOT_MODIFIER_OK policy={savedModifierState.PolicyId} " +
            $"tracks={savedModifierState.Tracks.Count}");

        var eventSink = new GodotEncounterEventSink(sceneInstances);
        BattleEncounterResult encounter = await new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest(
                [
                    new BattleEncounterParticipant(player.State, player.Entity.DisplayName),
                    new BattleEncounterParticipant(enemy.State, enemy.Entity.DisplayName)
                ],
                Battle,
                NormalBattle,
                moonPhaseId: null,
                roundLimit: 1),
            new BattleEncounterServices(
                new ParticipantOrderInitiativePolicy(),
                NoopBattleEncounterLifecyclePort.Instance,
                new PassTurnHandler(),
                new LastTeamStandingCompletionPolicy(),
                turnEconomy.CreateEconomy,
                turnEconomy.PhaseProgress,
                events: eventSink));
        if (encounter.Outcome != BattleEncounterOutcome.Draw || eventSink.Events.Count == 0)
        {
            throw new InvalidOperationException("The framework encounter did not reach the expected bounded outcome.");
        }

        if (eventSink.Events.Any(mapped => mapped.ActorId is not null && mapped.ActorNode is null))
        {
            throw new InvalidOperationException("An actor encounter event could not be mapped to its Godot Node.");
        }

        GD.Print($"GODOT_ENCOUNTER_OK outcome={encounter.Outcome} events={eventSink.Events.Count}");

        string saveJson = GodotSaveCodec.Serialize(
            [player, enemy, hostedEntity],
            partyRoster,
            new ContentPackIdentity(PackId, SemanticVersion.Parse("0.5.0")),
            sceneInstances);
        ChargePolicyRegistry chargePolicies = ChargePolicyRegistry.CreateStandard();
        var restoreService = new RuntimeSessionRestoreService(
            new RuntimeSaveValidator(
                rosterCapacity,
                moveListCapacity,
                rulesets,
                chargePolicies),
            actorFactory,
            GodotActorRestoreProfileResolver.Instance,
            rulesetBindings: rulesets,
            chargePolicies: chargePolicies);
        GodotSaveRestoreResult restored = GodotSaveCodec.DeserializeAndRestore(
            saveJson,
            catalog,
            restoreService);
        RuntimeRestoredSession session = restored.RequireSession();
        CatalogBattleActor restoredPlayer = session.Actors.Single(actor =>
            actor.State.InstanceId == player.State.InstanceId);
        CatalogBattleActor restoredHostedEntity = session.Actors.Single(actor =>
            actor.State.InstanceId == hostedEntity.State.InstanceId);
        RuntimeStatModifierStateSnapshot? restoredModifierState =
            restoredPlayer.State.StatModifierState;
        if (restoredPlayer.State.GetRequiredResource(Sp).Current !=
            player.State.GetRequiredResource(Sp).Current ||
            !restoredPlayer.State.Skills.LearnedSkillIds.SequenceEqual(
                player.State.Skills.LearnedSkillIds) ||
            !restoredPlayer.State.Skills.EquippedSkillIds.SequenceEqual(
                player.State.Skills.EquippedSkillIds) ||
            restoredPlayer.State.Skills.Revision != player.State.Skills.Revision ||
            !restoredPlayer.State.Skills.PendingChoices.SequenceEqual(
                player.State.Skills.PendingChoices) ||
            restoredPlayer.State.Stats[StandardProgressionIds.Strength] !=
            restoredHostedEntity.State.Stats[StandardProgressionIds.Strength] ||
            !EquivalentModifierState(savedModifierState, restoredModifierState) ||
            restored.SceneInstances.Count != 3)
        {
            throw new InvalidOperationException("The Godot-owned save did not preserve runtime and scene state.");
        }

        GD.Print(
            $"GODOT_SAVE_OK actors={session.Snapshot.Actors.Count} " +
            $"contract={session.Snapshot.ContractVersion} aggregate_restore=true");

        JsonObject invalidDocument = JsonNode.Parse(saveJson)?.AsObject() ??
            throw new InvalidDataException("Godot save JSON could not be parsed for rejection proof.");
        JsonObject invalidOwner = invalidDocument["partyRoster"]?["owner"]?.AsObject() ??
            throw new InvalidDataException("Godot save JSON did not contain its party owner.");
        invalidOwner["instanceId"] = "missing_godot_owner";
        GodotSaveRestoreResult rejected = GodotSaveCodec.DeserializeAndRestore(
            invalidDocument.ToJsonString(),
            catalog,
            restoreService);
        if (rejected.IsSuccess ||
            rejected.RestoreResult.Session is not null ||
            rejected.RestoreResult.Diagnostics.Count == 0 ||
            rejected.SceneInstances.Count != 0)
        {
            throw new InvalidOperationException(
                "Rejected aggregate save state exposed a live Godot session.");
        }

        GD.Print(
            $"GODOT_SAVE_REJECTION_OK actors_exposed=0 scene_metadata_exposed=0 " +
            $"diagnostics={rejected.RestoreResult.Diagnostics.Count}");
        GD.Print("CONVERGENCE_GODOT_SMOKE_OK");
    }

    private static bool EquivalentModifierState(
        RuntimeStatModifierStateSnapshot expected,
        RuntimeStatModifierStateSnapshot? actual)
    {
        if (actual is null ||
            actual.PolicyId != expected.PolicyId ||
            actual.Tracks.Count != expected.Tracks.Count)
        {
            return false;
        }

        for (int trackIndex = 0; trackIndex < expected.Tracks.Count; trackIndex++)
        {
            RuntimeStatModifierTrackSnapshot expectedTrack = expected.Tracks[trackIndex];
            RuntimeStatModifierTrackSnapshot actualTrack = actual.Tracks[trackIndex];
            if (actualTrack.ModifierTrackId != expectedTrack.ModifierTrackId ||
                actualTrack.ResolvedStage != expectedTrack.ResolvedStage ||
                actualTrack.Contributions.Count != expectedTrack.Contributions.Count)
            {
                return false;
            }

            for (int contributionIndex = 0;
                 contributionIndex < expectedTrack.Contributions.Count;
                 contributionIndex++)
            {
                RuntimeStatModifierContributionSnapshot expectedContribution =
                    expectedTrack.Contributions[contributionIndex];
                RuntimeStatModifierContributionSnapshot actualContribution =
                    actualTrack.Contributions[contributionIndex];
                if (actualContribution.Sequence != expectedContribution.Sequence ||
                    actualContribution.StageDelta != expectedContribution.StageDelta ||
                    !Equals(actualContribution.Duration, expectedContribution.Duration) ||
                    !EquivalentBoundary(
                        expectedContribution.LastLifecycleBoundary,
                        actualContribution.LastLifecycleBoundary))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool EquivalentBoundary(
        StatModifierLifecycleBoundary? expected,
        StatModifierLifecycleBoundary? actual) =>
        expected is null
            ? actual is null
            : actual is not null &&
              actual.EventId == expected.EventId &&
              actual.Sequence == expected.Sequence;

    private static ContentPackTextRequest CreateContentRequest() => new(
        "training_annex_slice.manifest.json",
        [
            "training_annex_slice.races.json",
            "training_annex_slice.ailments.json",
            "training_annex_slice.skills.json",
            "training_annex_slice.entities.json",
            "training_annex_slice.items.json",
            "training_annex_slice.equipment.json",
            "training_annex_slice.shops.json",
            "training_annex_slice.negotiations.json",
            "training_annex_slice.encounters.json",
            "training_annex_slice.dungeons.json",
            "training_annex_slice.fusion.json",
            "training_annex_slice.rulesets.json"
        ]);

    private static SkillSystemRegistrationSnapshot BuildRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterModifierTrack("attack", "defense")
            .RegisterEntityKind("vessel", "companion")
            .RegisterAlignment("neutral")
            .RegisterNegotiationPersonality("steady_sample")
            .RegisterAilmentGroup("major_ailment", "toxin", "rest", "immobilize")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterBattleKind("normal_battle")
            .RegisterShopCategory("training_supply")
            .RegisterNegotiationDemand("sample_credits")
            .RegisterEncounterEnvironment("training_annex")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "persistent_staged",
                "timed_exclusive",
                "timed_contribution",
                "standard_action_token",
                "standard_roster_capacity",
                "standard_economy",
                "return_to_lobby",
                "training_barrier",
                "standard_accident",
                "standard_mutation")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<ReduceResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<ApplyAilmentEffectDefinition>()
            .SupportEffect<ModifyStatStageEffectDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
            .Build();

    private static ContentId Qualified(string localId) => ContentId.Parse($"{PackId}:{localId}");

    private static RuntimeActorReferenceSnapshot Reference(CatalogBattleActor actor)
    {
        RuntimeActorSnapshot snapshot = actor.State.ToSnapshot();
        return new RuntimeActorReferenceSnapshot(
            snapshot.Identity.InstanceId,
            snapshot.Identity.EntityDefinitionId,
            snapshot.Identity.DisplayName);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class GodotActorRestoreProfileResolver : IRuntimeActorRestoreProfileResolver
    {
        public static GodotActorRestoreProfileResolver Instance { get; } = new();

        public RuntimeActorRestoreProfile Resolve(RuntimeActorRestoreProfileRequest request)
        {
            bool isVesselOwner =
                request.Actor.Identity.InstanceId == request.Session.PartyRoster.Owner.InstanceId &&
                request.Actor.Identity.ActorKindId == StandardProgressionIds.Vessel;
            return isVesselOwner
                ? new RuntimeActorRestoreProfile(
                    RuntimeStatSourceKind.ActiveHostedEntity,
                    MissingHostedEntityBehavior.RejectStatResolution)
                : new RuntimeActorRestoreProfile(
                    RuntimeStatSourceKind.Actor,
                    MissingHostedEntityBehavior.UseActorBaseStats);
        }
    }

    private sealed class FirstSkillTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            Array.AsReadOnly(candidates.Take(count.Minimum).ToArray());
    }

    private sealed class GodotActorInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level)
        {
            decimal vitality = entity.Stats.GetValueOrDefault(ContentId.Parse("vitality"));
            decimal magic = entity.Stats.GetValueOrDefault(ContentId.Parse("magic"));
            decimal hp = 40 + level * 5 + vitality * 5;
            decimal sp = 10 + level * 2 + magic * 3;
            return new BattleActorInitialization(
                Hp,
                [
                    new BattleResourceState(Hp, hp, hp),
                    new BattleResourceState(Sp, sp, sp)
                ],
                [
                    new KeyValuePair<ContentId, decimal>(Hp, 40 + level * 5),
                    new KeyValuePair<ContentId, decimal>(Sp, 10 + level * 2)
                ]);
        }
    }

    private sealed class PassTurnHandler : IBattleEncounterTurnHandler
    {
        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(BattleEncounterCommandResult.Executed(ActionTurnConsumption.Pass));
        }
    }
}
