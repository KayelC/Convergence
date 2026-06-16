using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle
{
    internal sealed class LegacyBattleActionAdapter
    {
        private static readonly ContentId BattleContext = ContentId.Parse("battle");
        private static readonly ContentId LegacyBattleKind = ContentId.Parse("legacy_battle");
        private static readonly ContentId LegacyMoonPhase = ContentId.Parse("legacy_moon_phase");
        private readonly IBattleActionExecutor _actions;

        public LegacyBattleActionAdapter()
            : this(CreateActionExecutor())
        {
        }

        public LegacyBattleActionAdapter(IBattleActionExecutor actions)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public BattleActionExecutionResult ExecuteGuard(Combatant actor)
        {
            RuntimeActorState state = ToRuntimeActor(actor);
            BattleActionExecutionResult result = Execute(
                new GuardBattleActionCommand(),
                state,
                [state]);
            if (result.Status == BattleActionExecutionStatus.Executed)
            {
                actor.IsGuarding = state.IsGuarding;
            }

            return result;
        }

        public BattleActionExecutionResult ExecutePass(Combatant actor)
        {
            RuntimeActorState state = ToRuntimeActor(actor);
            return Execute(new PassBattleActionCommand(), state, [state]);
        }

        public BattleActionExecutionResult ExecuteAnalyze(Combatant actor, Combatant target)
        {
            bool sameReference = ReferenceEquals(actor, target);
            RuntimeActorState actorState = ToRuntimeActor(actor, sameReference ? "analyzer" : null);
            RuntimeActorState targetState = ToRuntimeActor(target, sameReference ? "target" : null);
            return Execute(
                new AnalyzeBattleActionCommand(targetState.InstanceId, [AnalysisLayer.Full]),
                actorState,
                [actorState, targetState]);
        }

        public BattleActionExecutionResult RequestTacticsChange(Combatant actor, ContentId tacticId)
        {
            RuntimeActorState state = ToRuntimeActor(actor);
            return Execute(
                new HostMediatedBattleActionCommand(BattleActionKind.TacticsChange, tacticId, ActionTurnConsumption.None),
                state,
                [state]);
        }

        public BattleActionExecutionResult RequestNegotiation(Combatant actor, Combatant target)
        {
            RuntimeActorState actorState = ToRuntimeActor(actor);
            RuntimeActorState targetState = ToRuntimeActor(target);
            return Execute(
                new HostMediatedBattleActionCommand(
                    BattleActionKind.Negotiation,
                    ContentId.Parse("legacy_negotiation"),
                    ActionTurnConsumption.Normal,
                    [new KeyValuePair<string, object?>("target", targetState.InstanceId.ToString())]),
                actorState,
                [actorState, targetState]);
        }

        private BattleActionExecutionResult Execute(
            BattleActionCommand command,
            RuntimeActorState actor,
            IReadOnlyList<RuntimeActorState> participants) =>
            _actions.ExecuteAsync(
                new BattleActionExecutionRequest(
                    command,
                    actor,
                    participants,
                    new EffectExecutionEnvironment(BattleContext, LegacyBattleKind, LegacyMoonPhase)))
                .AsTask()
                .GetAwaiter()
                .GetResult();

        private static RuntimeActorState ToRuntimeActor(Combatant combatant, string? suffix = null)
        {
            var state = new RuntimeActorState(
                RuntimeId(combatant, suffix),
                ContentId.Parse("legacy_runtime_actor"),
                ContentId.Parse(combatant.Controller == ControllerType.LocalPlayer ? "player" : "legacy_ai"),
                StandardProgressionIds.Hp,
                CombatDefenseProfile.Empty,
                [
                    new BattleResourceState(StandardProgressionIds.Hp, combatant.CurrentHP, Math.Max(combatant.MaxHP, combatant.CurrentHP)),
                    new BattleResourceState(StandardProgressionIds.Sp, combatant.CurrentSP, Math.Max(combatant.MaxSP, combatant.CurrentSP))
                ],
                [
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Strength, combatant.GetStat(StatType.St)),
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Magic, combatant.GetStat(StatType.Ma)),
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Vitality, combatant.GetStat(StatType.Vi)),
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Agility, combatant.GetStat(StatType.Ag)),
                    new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Luck, combatant.GetStat(StatType.Lu))
                ]);
            state.SetGuarding(combatant.IsGuarding);
            return state;
        }

        private static ContentId RuntimeId(Combatant combatant, string? suffix = null)
        {
            string raw = !string.IsNullOrWhiteSpace(combatant.SourceId)
                ? combatant.SourceId
                : !string.IsNullOrWhiteSpace(combatant.Name)
                    ? combatant.Name
                    : "combatant";
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                raw += "_" + suffix;
            }

            string normalized = new string(raw.Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray())
                .Trim('_');
            return ContentId.Parse(string.IsNullOrWhiteSpace(normalized) ? "combatant" : normalized);
        }

        private static IBattleActionExecutor CreateActionExecutor()
        {
            BattleExecutionServices services = CreateServices();
            return new BattleActionExecutor(
                new SkillExecutor(services),
                new ItemExecutor(services),
                services);
        }

        private static BattleExecutionServices CreateServices()
        {
            var policy = new LegacyNoopPolicy();
            return new BattleExecutionServices(
                EmptyAilments.Instance,
                policy,
                policy,
                policy,
                policy,
                policy,
                new LegacyRandomTargetPolicy());
        }

        private sealed class EmptyAilments : IAilmentDefinitionRepository
        {
            public static EmptyAilments Instance { get; } = new();
            public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
            {
                definition = null;
                return false;
            }

            public AilmentDefinition GetRequiredAilment(ContentId id) =>
                throw new KeyNotFoundException($"No ailment '{id}' is available in the legacy action adapter.");
        }

        private sealed class LegacyRandomTargetPolicy : IRandomTargetSelectionPolicy
        {
            public IReadOnlyList<BattleActorState> Select(
                IReadOnlyList<BattleActorState> candidates,
                TargetCountDefinition count,
                SkillExecutionRequest request) =>
                Array.AsReadOnly(candidates.Take(count.Maximum).ToArray());
        }

        private sealed class LegacyNoopPolicy :
            IDamageExecutionPolicy,
            IInstantDeathExecutionPolicy,
            IAilmentApplicationPolicy,
            IChanceExecutionPolicy,
            IPowerAmountPolicy
        {
            public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request) =>
                [new DamageHitResolution(true, request.Effect.Power)];

            public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
            public bool ShouldApply(AilmentApplicationPolicyRequest request) => false;
            public bool Roll(ChancePolicyRequest request) => request.Chance >= 100;
            public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
        }
    }
}
