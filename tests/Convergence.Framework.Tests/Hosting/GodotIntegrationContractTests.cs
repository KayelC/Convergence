using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Hosting;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Hosting;

public sealed class GodotIntegrationContractTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId NormalBattle = Id("normal_battle");
    private static readonly ContentId NewMoon = Id("new_moon");
    private static readonly ContentId PlayerTeam = Id("player_team");
    private static readonly ContentId EnemyTeam = Id("enemy_team");
    private static readonly ContentId Hp = Id("hp");
    private static readonly ContentId Sp = Id("sp");

    [Fact]
    public async Task GodotResourceContentSource_PreservesLogicalPathsResourceSourcesAndCancellation()
    {
        var source = new GodotResourceContentSource(
            "res://packs",
            new Dictionary<string, string>
            {
                ["pack.manifest.json"] = "{\"manifest\":true}",
                ["second.json"] = "{\"order\":2}",
                ["first.json"] = "{\"order\":1}"
            });

        ContentPackTextBundle bundle = await source.ReadAsync(new ContentPackTextRequest(
            "pack.manifest.json",
            ["second.json", "first.json"]));

        Assert.Equal("res://packs/pack.manifest.json", bundle.ManifestSourceName);
        Assert.Equal(["second.json", "first.json"], bundle.Documents.Select(document => document.Path));
        Assert.Equal(
            ["res://packs/second.json", "res://packs/first.json"],
            bundle.Documents.Select(document => document.SourceName));
        Assert.Equal(["{\"order\":2}", "{\"order\":1}"], bundle.Documents.Select(document => document.Json));

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await source.ReadAsync(new ContentPackTextRequest("missing.manifest.json", [])));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await source.ReadAsync(new ContentPackTextRequest("missing.manifest.json", []), cancellation.Token));
    }

    [Fact]
    public async Task GodotHostContract_LoadsCatalogRunsBattleMapsEventsAndRestoresSnapshots()
    {
        GodotResourceContentSource source = GodotResourceContentSource.FromDataJsons(
            "res://convergence/content",
            "skill_system_redesign.manifest.sample.json",
            "skill_system_redesign.races.sample.json",
            "skill_system_redesign.skills.sample.json",
            "skill_system_redesign.entities.sample.json",
            "clean_battle_demo.manifest.json",
            "clean_battle_demo.races.json",
            "clean_battle_demo.skills.json",
            "clean_battle_demo.entities.json");

        ContentPackTextBundle reference = await source.ReadAsync(new ContentPackTextRequest(
            "skill_system_redesign.manifest.sample.json",
            [
                "skill_system_redesign.races.sample.json",
                "skill_system_redesign.skills.sample.json",
                "skill_system_redesign.entities.sample.json"
            ]));
        ContentPackTextBundle battleDemo = await source.ReadAsync(new ContentPackTextRequest(
            "clean_battle_demo.manifest.json",
            [
                "clean_battle_demo.races.json",
                "clean_battle_demo.skills.json",
                "clean_battle_demo.entities.json"
            ]));

        GameDataCatalog catalog = new SkillSystemCatalogLoader()
            .Load(new SkillSystemCatalogLoadRequest(Registrations(), [reference, battleDemo]))
            .RequireCatalog();

        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        var sceneRegistry = new GodotSceneInstanceRegistry();
        sceneRegistry.Attach(frost.State.InstanceId, new GodotSceneHandle(
            "res://scenes/battle/frost_duelist.tscn",
            "/root/Battle/Frost"));
        sceneRegistry.Attach(ember.State.InstanceId, new GodotSceneHandle(
            "res://scenes/battle/ember_duelist.tscn",
            "/root/Battle/Ember"));

        var commandSource = new GodotSignalCommandSource<string>()
            .QueueSelected("skill")
            .QueueCancelled();
        HostCommandReadResult<string> selected = await commandSource.ReadAsync(new HostCommandRequest<string>(
            "Battle command",
            [
                new HostCommandOption<string>("skill", "Skill"),
                new HostCommandOption<string>("guard", "Guard")
            ]));
        HostCommandReadResult<string> cancelled = await commandSource.ReadAsync(new HostCommandRequest<string>(
            "Battle command",
            [new HostCommandOption<string>("skill", "Skill")]));

        Assert.True(selected.IsSelected);
        Assert.Equal("skill", selected.Command);
        Assert.Equal(HostCommandReadStatus.Cancelled, cancelled.Status);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await commandSource.ReadAsync(
                new HostCommandRequest<string>("Battle command", [new HostCommandOption<string>("skill", "Skill")]),
                cancellation.Token));

        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var lifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            Id("battle_start"),
            Id("owner_turn_end"));
        var turnEconomy = new BattleTurnEconomyRuleset(
            () => new ActionTokenTurnEconomy(),
            new BattlePhaseProgressPolicy(256, 32));
        AutomatedBattleResult battle = new AutomatedBattleRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services,
            lifecycle,
            turnEconomy,
            new AutomatedBattleTurnRestrictionResolver()).Run(new AutomatedBattleRequest(
            [frost, ember],
            Battle,
            NormalBattle,
            NewMoon,
            roundLimit: 10));

        Assert.Equal(AutomatedBattleOutcome.Victory, battle.Outcome);
        Assert.Equal(PlayerTeam, battle.WinningTeamId);
        Assert.Contains(battle.Events, battleEvent => battleEvent.Kind == BattleRuntimeEventKind.SkillSelected);
        Assert.Contains(battle.Events, battleEvent => battleEvent.Kind == BattleRuntimeEventKind.EffectResolved);
        Assert.All(
            battle.Events.Where(battleEvent => battleEvent.ActorId is not null),
            battleEvent => Assert.True(sceneRegistry.TryGet(battleEvent.ActorId!.Value, out _)));

        CatalogBattleActor eventFrost = CreateDemoActor(catalog, "frost_duelist_demo", "event_frost", PlayerTeam);
        CatalogBattleActor eventEmber = CreateDemoActor(catalog, "ember_duelist_demo", "event_ember", EnemyTeam);
        sceneRegistry.Attach(eventFrost.State.InstanceId, new GodotSceneHandle(
            "res://scenes/battle/frost_duelist.tscn",
            "/root/Battle/EventFrost"));
        sceneRegistry.Attach(eventEmber.State.InstanceId, new GodotSceneHandle(
            "res://scenes/battle/ember_duelist.tscn",
            "/root/Battle/EventEmber"));
        var eventSink = new GodotBattleEncounterEventSink(sceneRegistry);

        BattleEncounterResult encounter = await new BattleEncounterRunner().RunAsync(
            new BattleEncounterRequest(
                [
                    new BattleEncounterParticipant(eventFrost.State, eventFrost.Entity.DisplayName),
                    new BattleEncounterParticipant(eventEmber.State, eventEmber.Entity.DisplayName)
                ],
                Battle,
                NormalBattle,
                NewMoon,
                roundLimit: 1),
            new BattleEncounterServices(
                new ParticipantOrderInitiativePolicy(),
                 NoopBattleEncounterLifecyclePort.Instance,
                 new PassTurnHandler(),
                 new LastTeamStandingCompletionPolicy(),
                 () => new StandardActionTurnEconomy(),
                 new BattlePhaseProgressPolicy(8, 1),
                 events: eventSink));

        Assert.Equal(BattleEncounterOutcome.Draw, encounter.Outcome);
        Assert.Contains(eventSink.Events, mapped => mapped.Kind == BattleEncounterEventKind.BattleStarted);
        Assert.Contains(eventSink.Events, mapped => mapped.Kind == BattleEncounterEventKind.CommandPassed);
        Assert.Equal(
            eventSink.Events.Select(mapped => mapped.Sequence).OrderBy(sequence => sequence),
            eventSink.Events.Select(mapped => mapped.Sequence));
        Assert.All(
            eventSink.Events.Where(mapped => mapped.ActorId is not null),
            mapped => Assert.NotNull(mapped.ActorHandle));

        RuntimeActorSnapshot actorSnapshot = ToRuntimeSnapshot(frost, level: 5);
        var fieldSnapshot = new RuntimeFieldSnapshot(
            new RuntimeNavigationSnapshot(Id("sample_depths_floor_7")),
            new RuntimeDungeonTraversalSnapshot(
                Id("sample_depths"),
                Id("floor_7"),
                visitedNodeIds: [Id("floor_1"), Id("floor_5"), Id("floor_7")],
                unlockedCheckpointIds: [Id("terminal_1"), Id("terminal_5"), Id("terminal_7")],
                defeatedBossIds: [Id("demo_guardian")]));
        var saveStore = new GodotSaveSnapshotStore();
        saveStore.Save(
            "slot_01",
            [actorSnapshot],
            fieldSnapshot,
            sceneRegistry.Snapshot());

        GodotSaveSnapshot restored = saveStore.Load("slot_01");
        RuntimeActorSnapshot restoredActor = Assert.Single(restored.Actors);
        RuntimeActorSnapshot roundTripActor = restoredActor;

        Assert.Equal(actorSnapshot.Identity, roundTripActor.Identity);
        Assert.Equal(actorSnapshot.Resources.Select(resource => resource.ResourceId), roundTripActor.Resources.Select(resource => resource.ResourceId));
        Assert.Equal(actorSnapshot.Resources.Select(resource => resource.Current), roundTripActor.Resources.Select(resource => resource.Current));
        Assert.Equal(
            fieldSnapshot.Navigation.CurrentLocationId,
            restored.Field.Navigation.CurrentLocationId);
        Assert.Equal(Id("floor_7"), restored.Field.DungeonTraversal!.CurrentNodeId);
        Assert.Equal(
            [Id("terminal_1"), Id("terminal_5"), Id("terminal_7")],
            restored.Field.DungeonTraversal.UnlockedCheckpointIds);
        Assert.True(restored.SceneHandles.ContainsKey(RuntimeInstanceId.Parse("frost")));
    }

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("companion")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .Build();

    private static CatalogBattleActor CreateDemoActor(
        GameDataCatalog catalog,
        string entityId,
        string instanceId,
        ContentId teamId) =>
        new CatalogBattleActorFactory(catalog, catalog, new TestInitializationPolicy()).Create(
            new CatalogBattleActorCreationRequest(
                Id($"convergence.clean_battle_demo:{entityId}"),
                RuntimeInstanceId.Parse(instanceId),
                teamId,
                5)).RequireActor();

    private static BattleExecutionServices Services(GameDataCatalog catalog) => new(
        catalog,
        new TestDamagePolicy(),
        new NeverInstantDeathPolicy(),
        new TestAilmentPolicy(),
        new AlwaysChancePolicy(),
        new TestPowerPolicy(),
        new FirstRandomTargetPolicy(),
        new OrderedRuntimeTargetSelectionPolicy());

    private static RuntimeActorSnapshot ToRuntimeSnapshot(CatalogBattleActor actor, int level)
    {
        RuntimeActorState state = actor.State;
        RuntimeResourceSnapshot[] resources = state.Resources.Values
            .Select(resource => new RuntimeResourceSnapshot(resource.Id, resource.Current, resource.Maximum))
            .ToArray();

        return new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse(state.InstanceId.ToString()),
                actor.Entity.Id,
                actor.Entity.EntityKindId,
                actor.Entity.DisplayName),
            new RuntimeActorOwnershipSnapshot(ContentId.Parse("godot_host"), state.TeamId),
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Deployed, state.IsActive),
            new RuntimeProgressionSnapshot(level, experience: 0, lifetimeExperience: 0, unspentStatPoints: 0),
            resources,
            new RuntimeStatBlockSnapshot(state.Stats, state.Stats),
            new RuntimeSkillStateSnapshot(state.SkillIds, state.SkillIds),
            new RuntimeActorRosterSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            resources.Select(resource => new KeyValuePair<ContentId, decimal>(resource.ResourceId, resource.Maximum)),
            state.VitalResourceId);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Convergence.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class GodotResourceContentSource : IContentPackTextSource
    {
        private readonly string _resourceRoot;
        private readonly IReadOnlyDictionary<string, string> _resources;

        public GodotResourceContentSource(string resourceRoot, IReadOnlyDictionary<string, string> resources)
        {
            _resourceRoot = resourceRoot.TrimEnd('/');
            _resources = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(resources));
        }

        public static GodotResourceContentSource FromDataJsons(string resourceRoot, params string[] paths)
        {
            string root = Path.Combine(AppContext.BaseDirectory, "Content");
            return new GodotResourceContentSource(
                resourceRoot,
                paths.ToDictionary(
                    path => path,
                    path => File.ReadAllText(Path.Combine(root, path))));
        }

        public ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string manifestJson = ReadResource(request.ManifestPath, cancellationToken);
            ContentDocumentText[] documents = request.DocumentPaths
                .Select(path =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ContentDocumentText(path, SourceName(path), ReadResource(path, cancellationToken));
                })
                .ToArray();

            return new ValueTask<ContentPackTextBundle>(
                new ContentPackTextBundle(SourceName(request.ManifestPath), manifestJson, documents));
        }

        private string ReadResource(string logicalPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_resources.TryGetValue(logicalPath, out string? json))
            {
                throw new FileNotFoundException($"Godot resource '{SourceName(logicalPath)}' was not supplied.");
            }

            return json;
        }

        private string SourceName(string logicalPath) => $"{_resourceRoot}/{logicalPath}";
    }

    private sealed class GodotSignalCommandSource<TCommand> : IHostCommandSource<TCommand>
    {
        private readonly Queue<HostCommandReadResult<TCommand>> _signals = new();

        public GodotSignalCommandSource<TCommand> QueueSelected(TCommand command)
        {
            _signals.Enqueue(HostCommandReadResult<TCommand>.Selected(command));
            return this;
        }

        public GodotSignalCommandSource<TCommand> QueueCancelled()
        {
            _signals.Enqueue(HostCommandReadResult<TCommand>.Cancelled());
            return this;
        }

        public ValueTask<HostCommandReadResult<TCommand>> ReadAsync(
            HostCommandRequest<TCommand> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_signals.Count == 0)
            {
                throw new InvalidOperationException("No Godot signal was queued for the command request.");
            }

            HostCommandReadResult<TCommand> result = _signals.Dequeue();
            if (result.IsSelected &&
                !request.Options.Any(option => option.IsEnabled && EqualityComparer<TCommand>.Default.Equals(option.Command, result.Command)))
            {
                throw new InvalidOperationException("The queued Godot signal selected an unavailable command.");
            }

            return new ValueTask<HostCommandReadResult<TCommand>>(result);
        }
    }

    private sealed record GodotSceneHandle(string PackedScenePath, string NodePath);

    private sealed class GodotSceneInstanceRegistry
    {
        private readonly Dictionary<RuntimeInstanceId, GodotSceneHandle> _handles = [];

        public void Attach(RuntimeInstanceId instanceId, GodotSceneHandle handle) =>
            _handles[instanceId] = handle;

        public bool TryGet(RuntimeInstanceId instanceId, out GodotSceneHandle? handle) =>
            _handles.TryGetValue(instanceId, out handle);

        public IReadOnlyDictionary<RuntimeInstanceId, GodotSceneHandle> Snapshot() =>
            new ReadOnlyDictionary<RuntimeInstanceId, GodotSceneHandle>(new Dictionary<RuntimeInstanceId, GodotSceneHandle>(_handles));
    }

    private sealed record GodotMappedBattleEvent(
        int Sequence,
        BattleEncounterEventKind Kind,
        RuntimeInstanceId? ActorId,
        GodotSceneHandle? ActorHandle,
        string Message);

    private sealed class GodotBattleEncounterEventSink : IBattleEncounterEventSink
    {
        private readonly GodotSceneInstanceRegistry _sceneRegistry;
        private readonly List<GodotMappedBattleEvent> _events = [];

        public GodotBattleEncounterEventSink(GodotSceneInstanceRegistry sceneRegistry)
        {
            _sceneRegistry = sceneRegistry;
        }

        public IReadOnlyList<GodotMappedBattleEvent> Events => _events;

        public ValueTask PublishAsync(BattleEncounterEvent battleEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GodotSceneHandle? actorHandle = null;
            if (battleEvent.ActorId is RuntimeInstanceId actorId)
            {
                _sceneRegistry.TryGet(actorId, out actorHandle);
            }

            _events.Add(new GodotMappedBattleEvent(
                battleEvent.Sequence,
                battleEvent.Kind,
                battleEvent.ActorId,
                actorHandle,
                battleEvent.Message));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PassTurnHandler : IBattleEncounterTurnHandler
    {
        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<BattleEncounterCommandResult>(
                BattleEncounterCommandResult.Executed(
                    ActionTurnConsumption.Pass,
                    [
                        new BattleEncounterEvent(
                            0,
                            BattleEncounterEventKind.CommandPassed,
                            $"{request.Actor.InstanceId} passed.",
                            request.Actor.InstanceId)
                    ]));
        }
    }

    private sealed record GodotSaveSnapshot(
        IReadOnlyList<RuntimeActorSnapshot> Actors,
        RuntimeFieldSnapshot Field,
        IReadOnlyDictionary<RuntimeInstanceId, GodotSceneHandle> SceneHandles);

    private sealed class GodotSaveSnapshotStore
    {
        private readonly Dictionary<string, GodotSaveSnapshot> _slots = [];

        public void Save(
            string slot,
            IEnumerable<RuntimeActorSnapshot> actors,
            RuntimeFieldSnapshot field,
            IReadOnlyDictionary<RuntimeInstanceId, GodotSceneHandle> sceneHandles)
        {
            _slots[slot] = new GodotSaveSnapshot(
                Array.AsReadOnly(actors.ToArray()),
                field,
                new ReadOnlyDictionary<RuntimeInstanceId, GodotSceneHandle>(
                    new Dictionary<RuntimeInstanceId, GodotSceneHandle>(sceneHandles)));
        }

        public GodotSaveSnapshot Load(string slot) => _slots[slot];
    }

    private sealed class TestInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level)
        {
            decimal vitality = entity.Stats.GetValueOrDefault(Id("vitality"));
            decimal magic = entity.Stats.GetValueOrDefault(Id("magic"));
            decimal hp = 40 + level * 5 + vitality * 3;
            decimal sp = 10 + level * 2 + magic * 2;
            return new BattleActorInitialization(Hp,
            [
                new BattleResourceState(Hp, hp, hp),
                new BattleResourceState(Sp, sp, sp)
            ]);
        }
    }

    private sealed class TestDamagePolicy : IDamageExecutionPolicy
    {
        public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request)
        {
            decimal damage = Math.Max(1,
                request.Effect.Power + request.Actor.Stats.GetValueOrDefault(Id("magic")) -
                request.Target.Stats.GetValueOrDefault(Id("vitality")));
            damage *= request.Affinity switch
            {
                ElementalAffinity.Weak => 1.5m,
                ElementalAffinity.Resist => 0.5m,
                _ => 1m
            };
            return [new DamageHitResolution(true, damage)];
        }
    }

    private sealed class NeverInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
    }

    private sealed class TestAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => request.Resistance != ResistanceLevel.Immune;
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => request.Chance > 0;
    }

    private sealed class TestPowerPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
    }

    private sealed class FirstRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) => candidates.Take(count.Minimum).ToArray();
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => 0m;
    }
}
