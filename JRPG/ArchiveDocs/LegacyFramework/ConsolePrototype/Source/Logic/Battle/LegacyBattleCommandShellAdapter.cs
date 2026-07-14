using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Core;

namespace JRPGPrototype.Logic.Battle
{
    internal sealed class LegacyBattleCommandShellAdapter
    {
        public static readonly ContentId LegacyBasicAttackActionId = ContentId.Parse("legacy_basic_attack");
        public static readonly ContentId LegacySkillActionId = ContentId.Parse("legacy_skill");
        public static readonly ContentId LegacyItemActionId = ContentId.Parse("legacy_item");
        public static readonly ContentId LegacyEscapeAttemptActionId = ContentId.Parse("legacy_escape_attempt");
        public static readonly ContentId LegacyTacticsStrategyActionId = ContentId.Parse("legacy_tactics_strategy");
        public static readonly ContentId LegacyNegotiationActionId = ContentId.Parse("legacy_negotiation");

        private static readonly ActionTurnConsumption LegacyPressTurnIntent =
            ActionTurnConsumption.FromPressTurn(new PressTurnResolution(PressTurnOutcome.Normal, false, false));

        private readonly LegacyBattleActionAdapter _actions;
        private readonly LegacyPartyStockAdapter _stock;
        private readonly LegacyRuntimeIdentityRegistry _ids;

        public LegacyBattleCommandShellAdapter()
            : this(
                new LegacyBattleActionAdapter(),
                LegacyPartyStockAdapter.Shared,
                LegacyRuntimeIdentityRegistry.Shared)
        {
        }

        public LegacyBattleCommandShellAdapter(
            LegacyBattleActionAdapter actions,
            LegacyPartyStockAdapter stock,
            LegacyRuntimeIdentityRegistry ids)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        }

        public BattleCommandShellResult CreateBasicAttack(Combatant actor, IReadOnlyList<Combatant> targets) =>
            Selected(
                actor,
                new HostMediatedBattleActionCommand(
                    BattleActionKind.HostSpecial,
                    LegacyBasicAttackActionId,
                    LegacyPressTurnIntent,
                    TargetParameters(targets)),
                BattleCommandShellPayloadKind.BasicAttack,
                targets: targets,
                participants: targets);

        public BattleCommandShellResult CreateLegacySkill(Combatant actor, SkillData skill, IReadOnlyList<Combatant> targets) =>
            Selected(
                actor,
                new HostMediatedBattleActionCommand(
                    BattleActionKind.HostSpecial,
                    LegacySkillActionId,
                    LegacyPressTurnIntent,
                    TargetParameters(targets).Append(new KeyValuePair<string, object?>("skill", skill.Name))),
                BattleCommandShellPayloadKind.LegacySkill,
                skill: skill,
                targets: targets,
                participants: targets);

        public BattleCommandShellResult CreateLegacyItem(Combatant actor, ItemData item, IReadOnlyList<Combatant> targets)
        {
            ActionTurnConsumption expectedTurn = item.Name == "Traesto Gem"
                ? ActionTurnConsumption.None
                : ActionTurnConsumption.Normal;
            return Selected(
                actor,
                new HostMediatedBattleActionCommand(
                    BattleActionKind.HostSpecial,
                    LegacyItemActionId,
                    expectedTurn,
                    TargetParameters(targets).Append(new KeyValuePair<string, object?>("item", item.Id))),
                BattleCommandShellPayloadKind.LegacyItem,
                item: item,
                targets: targets,
                participants: targets);
        }

        public BattleCommandShellResult CreateGuard(Combatant actor) =>
            Selected(actor, new GuardBattleActionCommand(), BattleCommandShellPayloadKind.Guard);

        public BattleCommandShellResult CreatePass(Combatant actor) =>
            Selected(actor, new PassBattleActionCommand(), BattleCommandShellPayloadKind.Pass);

        public BattleCommandShellResult CreateAnalyze(Combatant actor, Combatant target) =>
            Selected(
                actor,
                new AnalyzeBattleActionCommand(_actions.GetRuntimeActorId(target), [AnalysisLayer.Full]),
                BattleCommandShellPayloadKind.Analyze,
                targets: [target],
                participants: [target]);

        public BattleCommandShellResult CreatePersonaSwap(Combatant actor, Persona persona) =>
            Selected(
                actor,
                new PersonaSwapBattleActionCommand(
                    _stock.Snapshot(actor),
                    _ids.GetPersonaId(persona)),
                BattleCommandShellPayloadKind.PersonaSwap,
                persona: persona,
                expectedTurnConsumption: ActionTurnConsumption.None);

        public BattleCommandShellResult CreateDemonSummon(PartyManager party, Combatant actor, Combatant standby)
        {
            BattleCompActionResult comp = BattleCompActionResult.Summon(standby);
            return Selected(
                actor,
                new DemonSummonBattleActionCommand(
                    _stock.Snapshot(party, actor),
                    _ids.GetActorId(standby)),
                BattleCommandShellPayloadKind.DemonSummon,
                compAction: comp);
        }

        public BattleCommandShellResult CreateDemonReturn(PartyManager party, Combatant actor, Combatant active)
        {
            BattleCompActionResult comp = BattleCompActionResult.Return(active);
            return Selected(
                actor,
                new DemonReturnBattleActionCommand(
                    _stock.Snapshot(party, actor),
                    _ids.GetActorId(active)),
                BattleCommandShellPayloadKind.DemonReturn,
                compAction: comp);
        }

        public BattleCommandShellResult CreateDemonSwap(PartyManager party, Combatant actor, Combatant active, Combatant standby)
        {
            BattleCompActionResult comp = BattleCompActionResult.Swap(standby, active);
            return Selected(
                actor,
                new DemonSwapBattleActionCommand(
                    _stock.Snapshot(party, actor),
                    _ids.GetActorId(active),
                    _ids.GetActorId(standby)),
                BattleCommandShellPayloadKind.DemonSwap,
                compAction: comp);
        }

        public BattleCommandShellResult CreateTacticsEscape(Combatant actor) =>
            Selected(
                actor,
                new HostMediatedBattleActionCommand(
                    BattleActionKind.HostSpecial,
                    LegacyEscapeAttemptActionId,
                    ActionTurnConsumption.Normal),
                BattleCommandShellPayloadKind.TacticsEscape,
                tacticsAction: BattleTacticsAction.Escape);

        public BattleCommandShellResult CreateTacticsStrategy(Combatant actor, Combatant target) =>
            Selected(
                actor,
                new HostMediatedBattleActionCommand(
                    BattleActionKind.TacticsChange,
                    LegacyTacticsStrategyActionId,
                    ActionTurnConsumption.None,
                    [new KeyValuePair<string, object?>("target", _actions.GetRuntimeActorId(target).ToString())]),
                BattleCommandShellPayloadKind.TacticsStrategy,
                targets: [target],
                tacticsAction: BattleTacticsAction.Strategy,
                participants: [target]);

        public BattleCommandShellResult CreateNegotiation(Combatant actor, Combatant target) =>
            Selected(
                actor,
                new HostMediatedBattleActionCommand(
                    BattleActionKind.Negotiation,
                    LegacyNegotiationActionId,
                    ActionTurnConsumption.Normal,
                    [new KeyValuePair<string, object?>("target", _actions.GetRuntimeActorId(target).ToString())]),
                BattleCommandShellPayloadKind.Negotiation,
                targets: [target],
                participants: [target]);

        private BattleCommandShellResult Selected(
            Combatant actor,
            BattleActionCommand command,
            BattleCommandShellPayloadKind payloadKind,
            SkillData? skill = null,
            ItemData? item = null,
            IEnumerable<Combatant>? targets = null,
            Persona? persona = null,
            BattleCompActionResult? compAction = null,
            BattleTacticsAction? tacticsAction = null,
            IEnumerable<Combatant>? participants = null,
            ActionTurnConsumption? expectedTurnConsumption = null)
        {
            BattleActionAssessment assessment = _actions.Assess(command, actor, participants);
            return new BattleCommandShellResult(
                BattleSelectionResultKind.Selected,
                payloadKind,
                command,
                assessment,
                expectedTurnConsumption ?? assessment.TurnConsumption,
                skill,
                item,
                targets,
                persona,
                compAction,
                tacticsAction);
        }

        private IEnumerable<KeyValuePair<string, object?>> TargetParameters(IEnumerable<Combatant> targets) =>
            [new KeyValuePair<string, object?>(
                "targets",
                string.Join(",", targets.Select(target => _actions.GetRuntimeActorId(target).ToString())))];
    }
}
