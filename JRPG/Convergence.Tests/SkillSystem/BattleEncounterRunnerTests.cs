using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class BattleEncounterRunnerTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId Kind = Id("normal_battle");
    private static readonly ContentId Moon = Id("new_moon");
    private static readonly ContentId Hp = Id("hp");
    private static readonly ContentId Sp = Id("sp");
    private static readonly ContentId PlayerTeam = Id("player_team");
    private static readonly ContentId EnemyTeam = Id("enemy_team");

    [Fact]
    public void Runner_UsesInitiativeAndOrdersLifecycleEvents()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();
        var handler = new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal));

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(EnemyTeam, PlayerTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(BattleEncounterOutcome.Draw, result.Outcome);
        Assert.Equal([EnemyTeam, PlayerTeam], lifecycle.BattleStartTeamOrder);
        Assert.True(Index(result, BattleEncounterEventKind.BattleStarted) <
                    Index(result, BattleEncounterEventKind.RoundStarted));
        Assert.True(Index(result, BattleEncounterEventKind.RoundStarted) <
                    Index(result, BattleEncounterEventKind.PhaseStarted));
        Assert.Equal(enemy.InstanceId, handler.Requests.Single().Actor.InstanceId);
    }

    [Theory]
    [InlineData(PressTurnOutcome.Normal, false, false, 1, 0)]
    [InlineData(PressTurnOutcome.Weakness, false, false, 1, 1)]
    [InlineData(PressTurnOutcome.Critical, true, false, 1, 1)]
    [InlineData(PressTurnOutcome.Miss, false, false, 0, 0)]
    [InlineData(PressTurnOutcome.Null, false, false, 0, 0)]
    [InlineData(PressTurnOutcome.Repel, false, true, 0, 0)]
    [InlineData(PressTurnOutcome.Absorb, false, true, 0, 0)]
    public void Runner_AppliesEveryPressTurnOutcome(
        PressTurnOutcome outcome,
        bool critical,
        bool terminates,
        int expectedFullIcons,
        int expectedBlinkingIcons)
    {
        BattleEncounterParticipant first = Participant("first", PlayerTeam);
        BattleEncounterParticipant second = Participant("second", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var handler = new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
            ActionTurnConsumption.FromPressTurn(new PressTurnResolution(outcome, critical, terminates))));

        BattleEncounterResult result = Run(
            [first, second, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new CompleteAfterTurnsPolicy(1));

        BattleEncounterEvent changed = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.PressTurnChanged);
        Assert.NotNull(changed.PressTurnState);
        Assert.Equal(expectedFullIcons, changed.PressTurnState!.FullIcons);
        Assert.Equal(expectedBlinkingIcons, changed.PressTurnState.BlinkingIcons);
    }

    [Fact]
    public void Runner_DispatchesTurnEndAfterCommittedRestrictionsAndActions()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        var lifecycle = new RecordingLifecycle
        {
            TurnStartOutcome = BattleTurnStartOutcome.Skip
        };
        var handler = new QueueTurnHandler(request =>
        {
            Assert.Equal(BattleTurnStartOutcome.Skip, request.TurnStartOutcome);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        Run(
            [player, Participant("enemy", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            handler,
            new CompleteAfterTurnsPolicy(1));

        Assert.Equal(1, lifecycle.TurnStartCalls);
        Assert.Equal(1, lifecycle.TurnEndCalls);
    }

    [Fact]
    public void Runner_RefreshesStateAndCompletesWhenAStandingTeamRemains()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var handler = new QueueTurnHandler(_ =>
        {
            enemy.State.SetResource(Hp, 0);
            return BattleEncounterCommandResult.Executed(ActionTurnConsumption.Normal);
        });

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            handler,
            new LastTeamStandingCompletionPolicy());

        Assert.Equal(BattleEncounterOutcome.Victory, result.Outcome);
        Assert.Equal(PlayerTeam, result.WinningTeamId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ActorDefeated &&
            battleEvent.ActorId == enemy.InstanceId);
    }

    [Fact]
    public void Runner_StopsOnFaultCancellationAndEscape()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);

        BattleEncounterResult fault = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Faulted("bad command")),
            new CompleteAfterTurnsPolicy(99));
        BattleEncounterResult cancelled = Run(
            [Participant("player_cancel", PlayerTeam), Participant("enemy_cancel", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Cancelled()),
            new CompleteAfterTurnsPolicy(99));
        BattleEncounterResult escaped = Run(
            [Participant("player_escape", PlayerTeam), Participant("enemy_escape", EnemyTeam)],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            new RecordingLifecycle(),
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.None,
                requestedOutcome: BattleEncounterOutcome.Escape)),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, fault.Outcome);
        Assert.Equal("bad command", fault.FaultMessage);
        Assert.Equal(BattleEncounterOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(BattleEncounterOutcome.Escape, escaped.Outcome);
    }

    [Fact]
    public void Runner_TreatsCommandRejectionAsFaultWithoutTurnConsumption()
    {
        BattleEncounterParticipant player = Participant("player", PlayerTeam);
        BattleEncounterParticipant enemy = Participant("enemy", EnemyTeam);
        var lifecycle = new RecordingLifecycle();

        BattleEncounterResult result = Run(
            [player, enemy],
            new FixedInitiative(PlayerTeam, EnemyTeam),
            lifecycle,
            new QueueTurnHandler(_ => BattleEncounterCommandResult.Rejected("selection became invalid")),
            new CompleteAfterTurnsPolicy(99));

        Assert.Equal(BattleEncounterOutcome.Faulted, result.Outcome);
        Assert.Equal("selection became invalid", result.FaultMessage);
        Assert.Equal(0, lifecycle.TurnEndCalls);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ActionRejected &&
            battleEvent.Message == "selection became invalid");
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.PressTurnChanged);
    }

    [Fact]
    public void RuntimePublicApi_DoesNotExposeHostSerializerFilesystemOrLegacyTypes()
    {
        Type[] publicTypes = typeof(BattleEncounterRunner).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "JRPGPrototype.Logic.Battle.Runtime")
            .ToArray();
        string[] forbidden =
        [
            "Newtonsoft", "System.Text.Json", "Godot", "System.IO.File", "Database",
            "Combatant", "SkillData", "PersonaData", "ItemData", "IGameIO"
        ];

        IEnumerable<Type> signatures = publicTypes.SelectMany(PublicSignatureTypes);

        Assert.DoesNotContain(signatures, type =>
            forbidden.Any(token => (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal)));
    }

    private static BattleEncounterResult Run(
        IReadOnlyList<BattleEncounterParticipant> participants,
        IBattleEncounterInitiativePolicy initiative,
        IBattleEncounterLifecyclePort lifecycle,
        IBattleEncounterTurnHandler handler,
        IBattleEncounterCompletionPolicy completion) =>
        new BattleEncounterRunner().Run(
            new BattleEncounterRequest(participants, Battle, Kind, Moon, 5),
            new BattleEncounterServices(initiative, lifecycle, handler, completion));

    private static int Index(BattleEncounterResult result, BattleEncounterEventKind kind) =>
        result.Events.First(battleEvent => battleEvent.Kind == kind).Sequence;

    private static BattleEncounterParticipant Participant(string id, ContentId teamId)
    {
        var state = new RuntimeActorState(
            RuntimeInstanceId.Parse(id),
            Id(id + "_entity"),
            teamId,
            Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(Hp, 10, 10),
                new BattleResourceState(Sp, 5, 5)
            ],
            isActive: true);
        return new BattleEncounterParticipant(state, id);
    }

    private static IEnumerable<Type> PublicSignatureTypes(Type type)
    {
        yield return type;
        foreach (System.Reflection.PropertyInfo property in type.GetProperties(
                     System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.Instance |
                     System.Reflection.BindingFlags.Static))
        {
            foreach (Type nested in Flatten(property.PropertyType)) yield return nested;
        }

        foreach (System.Reflection.MethodInfo method in type.GetMethods(
                     System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.Instance |
                     System.Reflection.BindingFlags.Static |
                     System.Reflection.BindingFlags.DeclaredOnly))
        {
            foreach (Type nested in Flatten(method.ReturnType)) yield return nested;
            foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
            {
                foreach (Type nested in Flatten(parameter.ParameterType)) yield return nested;
            }
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.IsArray)
        {
            foreach (Type nested in Flatten(type.GetElementType()!)) yield return nested;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Flatten(argument)) yield return nested;
        }
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class FixedInitiative(params ContentId[] teamOrder) : IBattleEncounterInitiativePolicy
    {
        public IReadOnlyList<ContentId> DetermineTeamOrder(BattleEncounterInitiativeRequest request) => teamOrder;
    }

    private sealed class QueueTurnHandler(Func<BattleEncounterTurnRequest, BattleEncounterCommandResult> handler)
        : IBattleEncounterTurnHandler
    {
        public List<BattleEncounterTurnRequest> Requests { get; } = [];

        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return new ValueTask<BattleEncounterCommandResult>(handler(request));
        }
    }

    private sealed class CompleteAfterTurnsPolicy(int count) : IBattleEncounterCompletionPolicy
    {
        private int _turns;

        public BattleEncounterCompletion Evaluate(BattleEncounterCompletionRequest request)
        {
            if (request.LastActor is null)
            {
                return new BattleEncounterCompletion(false);
            }

            _turns++;
            return _turns >= count
                ? new BattleEncounterCompletion(true, BattleEncounterOutcome.Draw)
                : new BattleEncounterCompletion(false);
        }
    }

    private sealed class RecordingLifecycle : IBattleEncounterLifecyclePort
    {
        public IReadOnlyList<ContentId> BattleStartTeamOrder { get; private set; } = [];
        public BattleTurnStartOutcome TurnStartOutcome { get; init; } = BattleTurnStartOutcome.CanAct;
        public int TurnStartCalls { get; private set; }
        public int TurnEndCalls { get; private set; }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            BattleStartTeamOrder = request.TeamOrder;
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            TurnStartCalls++;
            return new ValueTask<BattleTurnStartLifecycleResult>(
                new BattleTurnStartLifecycleResult(TurnStartOutcome, []));
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            TurnEndCalls++;
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(Array.Empty<BattleEncounterEvent>());
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
    }
}
