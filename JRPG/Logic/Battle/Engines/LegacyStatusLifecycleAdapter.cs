using System.Text.RegularExpressions;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Messaging;

namespace JRPGPrototype.Logic.Battle.Engines;

internal sealed class LegacyStatusLifecycleAdapter
{
    private static readonly ContentId HpId = ContentId.Parse("hp");
    private static readonly ContentId SpId = ContentId.Parse("sp");
    private static readonly ContentId LuckId = ContentId.Parse("luck");
    private static readonly ContentId BattleContextId = ContentId.Parse("battle");
    private static readonly ContentId OwnerTurnEndEventId = ContentId.Parse("owner_turn_end");
    private static readonly ContentId LegacyPoisonFormulaId = ContentId.Parse("legacy_poison_damage");
    private static readonly ContentId LegacyTeamId = ContentId.Parse("legacy_team");
    private static readonly ContentId LegacyEntityId = ContentId.Parse("legacy_combatant");

    private readonly IBattleStatusLifecycleService _lifecycle;
    private readonly BattleExecutionServices _services;

    public LegacyStatusLifecycleAdapter()
        : this(new BattleStatusLifecycleService(new LegacyRandomSource()))
    {
    }

    internal LegacyStatusLifecycleAdapter(IBattleStatusLifecycleService lifecycle)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _services = CreateServices();
    }

    public bool TryInflict(
        Combatant attacker,
        Combatant target,
        string skillEffect,
        IBattleMessenger? messenger)
    {
        if (string.IsNullOrEmpty(skillEffect) || target.IsDead)
        {
            return false;
        }

        if (target.GetConsolidatedSkills().Contains("Unshaken Will"))
        {
            return false;
        }

        AilmentData? legacyAilment = Database.Ailments.Values.FirstOrDefault(ailment =>
            skillEffect.Contains(ailment.Name, StringComparison.OrdinalIgnoreCase));
        if (legacyAilment is null)
        {
            return false;
        }

        int baseChance = 100;
        Match match = Regex.Match(skillEffect, @"\((\d+)%");
        if (match.Success)
        {
            baseChance = int.Parse(match.Groups[1].Value);
        }

        int finalChance = Math.Clamp(
            baseChance + attacker.GetStat(StatType.Lu) - target.GetStat(StatType.Lu),
            5,
            95);

        BattleActorState attackerState = CreateState(attacker);
        BattleActorState targetState = CreateState(target);
        AilmentDefinition definition = LegacyAilmentDefinitions.Create(legacyAilment);
        BattleAilmentApplicationResult result = _lifecycle.TryApplyAilment(
            new BattleAilmentApplicationRequest(
                attackerState,
                targetState,
                definition,
                finalChance,
                new TurnDurationDefinition(3, OwnerTurnEndEventId, true)));

        if (!result.Applied)
        {
            return false;
        }

        bool success = target.InflictAilment(legacyAilment, 3);
        if (success)
        {
            messenger?.Publish($"{target.Name} was inflicted with {legacyAilment.Name}!", ConsoleColor.Magenta);
        }

        return success;
    }

    public TurnStartResult ProcessTurnStart(Combatant actor)
    {
        BattleActorState state = CreateState(actor);
        BattleTurnStartLifecycleResult result = _lifecycle.ProcessTurnStart(
            new BattleTurnStartLifecycleRequest(
                state,
                actor.Class == ClassType.Demon));
        actor.IsGuarding = state.IsGuarding;
        return result.Outcome switch
        {
            BattleTurnStartOutcome.Skip => TurnStartResult.Skip,
            BattleTurnStartOutcome.LimitedAction => TurnStartResult.LimitedAction,
            BattleTurnStartOutcome.ForcedPhysical => TurnStartResult.ForcedPhysical,
            BattleTurnStartOutcome.ForcedConfusion => TurnStartResult.ForcedConfusion,
            BattleTurnStartOutcome.FleeBattle => TurnStartResult.FleeBattle,
            BattleTurnStartOutcome.ReturnToStock => TurnStartResult.ReturnToCOMP,
            _ => TurnStartResult.CanAct
        };
    }

    public void ProcessTurnEnd(Combatant actor, IBattleMessenger? messenger)
    {
        BattleActorState state = CreateState(actor, includeLegacyTurnEndPassives: true);
        BattleTurnEndLifecycleResult result = _lifecycle.ProcessTurnEnd(
            new BattleTurnEndLifecycleRequest(
                state,
                [state],
                BattleContextId,
                OwnerTurnEndEventId),
            _services);
        PublishTurnEndMessages(actor, result, messenger);
        CopyStateBack(actor, state);
    }

    public void ApplyStatChange(string skillName, Combatant target)
    {
        string skill = skillName.ToLower();
        bool isBuff = skill.EndsWith("kaja") || skill == "heat riser";
        bool isDebuff = skill.EndsWith("nda") || skill == "debilitate";
        if (!isBuff && !isDebuff)
        {
            return;
        }

        int delta = isBuff ? 1 : -1;
        BattleActorState state = CreateState(target);
        foreach (string track in ResolveLegacyTracks(skill))
        {
            _ = _lifecycle.ApplyStatStage(state, ContentId.Parse(ToFrameworkTrack(track)), delta);
            ChangeBuff(target, track, delta);
        }
    }

    private static IEnumerable<string> ResolveLegacyTracks(string skill)
    {
        if (skill == "heat riser" || skill == "debilitate")
        {
            return ["PhysAtk", "MagAtk", "Defense", "Agility"];
        }

        var tracks = new List<string>();
        if (skill.Contains("taru"))
        {
            tracks.Add("PhysAtk");
        }

        if (skill.Contains("maka"))
        {
            tracks.Add("MagAtk");
        }

        if (skill.Contains("raku"))
        {
            tracks.Add("Defense");
        }

        if (skill.Contains("suku"))
        {
            tracks.Add("Agility");
        }

        return tracks;
    }

    private static string ToFrameworkTrack(string legacyTrack) => legacyTrack switch
    {
        "PhysAtk" => "physical_attack",
        "MagAtk" => "magical_attack",
        "Defense" => "defense",
        "Agility" => "agility",
        _ => legacyTrack.ToLowerInvariant()
    };

    private static void ChangeBuff(Combatant target, string stat, int delta)
    {
        int current = target.Buffs.GetValueOrDefault(stat, 0);
        target.Buffs[stat] = Math.Clamp(current + delta, -4, 4);
    }

    private static BattleActorState CreateState(
        Combatant actor,
        bool includeLegacyTurnEndPassives = false)
    {
        var state = new BattleActorState(
            LegacyId(actor),
            LegacyEntityId,
            LegacyTeamId,
            HpId,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(HpId, Math.Max(0, actor.CurrentHP), Math.Max(0, actor.MaxHP)),
                new BattleResourceState(SpId, Math.Max(0, actor.CurrentSP), Math.Max(0, actor.MaxSP))
            ],
            [
                new KeyValuePair<ContentId, decimal>(LuckId, actor.GetStat(StatType.Lu))
            ],
            passiveSkills: includeLegacyTurnEndPassives ? CreateTurnEndPassives(actor) : [],
            isActive: actor.PartySlot != -1);
        state.SetGuarding(actor.IsGuarding);

        if (actor.CurrentAilment is not null)
        {
            state.ApplyAilment(
                LegacyAilmentDefinitions.Create(actor.CurrentAilment),
                new TurnDurationDefinition(Math.Max(1, actor.AilmentDuration), OwnerTurnEndEventId, true));
        }

        return state;
    }

    private static IReadOnlyList<SkillDefinition> CreateTurnEndPassives(Combatant actor)
    {
        List<EffectDefinition> effects = [];
        List<string> skills = actor.GetConsolidatedSkills();
        if (skills.Contains("Spring of Life"))
        {
            effects.Add(new RestoreResourceEffectDefinition(HpId, new PercentMaximumAmountDefinition(8)));
        }

        if (skills.Contains("Regenerate 3"))
        {
            effects.Add(new RestoreResourceEffectDefinition(HpId, new PercentMaximumAmountDefinition(6)));
        }
        else if (skills.Contains("Regenerate 2"))
        {
            effects.Add(new RestoreResourceEffectDefinition(HpId, new PercentMaximumAmountDefinition(4)));
        }
        else if (skills.Contains("Regenerate 1"))
        {
            effects.Add(new RestoreResourceEffectDefinition(HpId, new PercentMaximumAmountDefinition(2)));
        }

        if (skills.Contains("Invigorate 3"))
        {
            effects.Add(new RestoreResourceEffectDefinition(SpId, new FlatAmountDefinition(7)));
        }
        else if (skills.Contains("Invigorate 2"))
        {
            effects.Add(new RestoreResourceEffectDefinition(SpId, new FlatAmountDefinition(5)));
        }
        else if (skills.Contains("Invigorate 1"))
        {
            effects.Add(new RestoreResourceEffectDefinition(SpId, new FlatAmountDefinition(3)));
        }

        if (effects.Count == 0)
        {
            return [];
        }

        return
        [
            new SkillDefinition(
                ContentId.Parse("legacy_turn_end_restoration"),
                "Legacy Turn-End Restoration",
                "Adapter-owned passive bundle for legacy regeneration skills.",
                SkillActivation.Passive,
                null,
                InheritanceGroup.Passive,
                new SkillInheritanceDefinition(false),
                triggers:
                [
                    new PassiveTriggerDefinition(
                        OwnerTurnEndEventId,
                        effects)
                ])
        ];
    }

    private static void CopyStateBack(Combatant actor, BattleActorState state)
    {
        actor.CurrentHP = (int)state.GetRequiredResource(HpId).Current;
        actor.CurrentSP = (int)state.GetRequiredResource(SpId).Current;
        actor.IsGuarding = state.IsGuarding;

        if (actor.CurrentAilment is null)
        {
            return;
        }

        if (!state.Ailments.TryGetValue(ContentId.Parse(ToLocalId(actor.CurrentAilment.Name)), out ActiveAilmentState? active))
        {
            actor.RemoveAilment();
            return;
        }

        actor.AilmentDuration = active.Duration is TurnDurationDefinition turns
            ? turns.Value
            : actor.AilmentDuration;
    }

    private static void PublishTurnEndMessages(
        Combatant actor,
        BattleTurnEndLifecycleResult result,
        IBattleMessenger? messenger)
    {
        if (messenger is null)
        {
            return;
        }

        foreach (BattleStatusLifecycleEvent ev in result.Events)
        {
            if (ev.Kind == BattleStatusLifecycleEventKind.ResourceChanged &&
                ev.RelatedId == HpId &&
                ev.Value > 0)
            {
                messenger.Publish($"{actor.Name} restored {(int)ev.Value.Value} HP.");
            }
            else if (ev.Kind == BattleStatusLifecycleEventKind.ResourceChanged &&
                     ev.RelatedId == SpId &&
                     ev.Value > 0)
            {
                messenger.Publish($"{actor.Name} restored {(int)ev.Value.Value} SP via passives.");
            }
            else if (ev.Kind == BattleStatusLifecycleEventKind.ResourceChanged &&
                     ev.RelatedId == HpId &&
                     ev.Value < 0 &&
                     actor.CurrentAilment is not null)
            {
                messenger.Publish($"{actor.Name} is hurt by {actor.CurrentAilment.Name}! ({Math.Abs((int)ev.Value.Value)} DMG)");
            }
            else if (ev.Kind == BattleStatusLifecycleEventKind.AilmentRemoved &&
                     actor.CurrentAilment is not null)
            {
                messenger.Publish($"{actor.Name} is no longer {actor.CurrentAilment.Name}.");
            }
            else if (ev.Kind == BattleStatusLifecycleEventKind.AilmentRecovered &&
                     actor.CurrentAilment is not null)
            {
                messenger.Publish($"{actor.Name} recovered from {actor.CurrentAilment.Name}!");
            }
            else if (ev.Kind == BattleStatusLifecycleEventKind.AilmentExpired &&
                     actor.CurrentAilment is not null)
            {
                messenger.Publish($"{actor.Name}'s {actor.CurrentAilment.Name} wore off.");
            }
        }
    }

    private static ContentId LegacyId(Combatant actor)
    {
        string source = string.IsNullOrWhiteSpace(actor.SourceId) ? actor.Name : actor.SourceId;
        return ContentId.Parse(ToLocalId(source));
    }

    private static string ToLocalId(string value)
    {
        string sanitized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(sanitized) ? "legacy_actor" : sanitized;
    }

    private static BattleExecutionServices CreateServices() =>
        new(
            new LegacyAilmentRepository(),
            new NoDamagePolicy(),
            new NoInstantDeathPolicy(),
            new AlwaysAilmentPolicy(),
            new AlwaysChancePolicy(),
            new ZeroPowerPolicy(),
            new OrderedRandomTargetPolicy(),
            formulaHandlers:
            [
                new KeyValuePair<ContentId, IFormulaAmountHandler>(
                    LegacyPoisonFormulaId,
                    new LegacyPoisonFormula())
            ]);

    private sealed class LegacyRandomSource : IRandomSource
    {
        private readonly Random _random = new();
        public int NextInt32(int minimumInclusive, int maximumExclusive) =>
            _random.Next(minimumInclusive, maximumExclusive);

        public decimal NextUnitDecimal() => (decimal)_random.NextDouble();
    }

    private sealed class LegacyPoisonFormula : IFormulaAmountHandler
    {
        public decimal Resolve(FormulaAmountDefinition amount, AmountResolutionContext context) =>
            Math.Max(1, Math.Floor(context.Target.GetRequiredResource(HpId).Maximum * 0.13m));
    }

    private sealed class LegacyAilmentRepository : IAilmentDefinitionRepository
    {
        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            AilmentData? data = Database.Ailments.Values.FirstOrDefault(ailment =>
                ContentId.Parse(ToLocalId(ailment.Name)) == id);
            definition = data is null ? null : LegacyAilmentDefinitions.Create(data);
            return definition is not null;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            TryGetAilment(id, out AilmentDefinition? definition) && definition is not null
                ? definition
                : throw new KeyNotFoundException($"Legacy ailment '{id}' is not loaded.");
    }

    private sealed class NoDamagePolicy : IDamageExecutionPolicy
    {
        public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request) => [];
    }

    private sealed class NoInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
    }

    private sealed class AlwaysAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => true;
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => true;
    }

    private sealed class ZeroPowerPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => 0;
    }

    private sealed class OrderedRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<BattleActorState> Select(
            IReadOnlyList<BattleActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            candidates.Take(count.Maximum).ToArray();
    }

    private static class LegacyAilmentDefinitions
    {
        public static AilmentDefinition Create(AilmentData data)
        {
            ContentId id = ContentId.Parse(ToLocalId(data.Name));
            return new AilmentDefinition(
                id,
                data.Name,
                data.Description,
                new TurnDurationDefinition(3, OwnerTurnEndEventId, true),
                ToBehavior(data.ActionRestriction),
                new AilmentModifiersDefinition(
                    (decimal)data.EvasionMult,
                    (int)(data.CritBonusChance * 100),
                    (decimal)data.DamageTakenMult,
                    (decimal)data.DamageDealMult,
                    data.Name.Equals("Freeze", StringComparison.OrdinalIgnoreCase) ||
                    data.Name.Equals("Shock", StringComparison.OrdinalIgnoreCase) ||
                    data.Name.Equals("Bind", StringComparison.OrdinalIgnoreCase) ||
                    data.Name.Equals("Stun", StringComparison.OrdinalIgnoreCase)),
                ToRecovery(data),
                [ContentId.Parse("major_ailment")],
                ContentId.Parse("major_ailment"),
                CreateTriggers(data));
        }

        private static AilmentTurnBehaviorDefinition ToBehavior(string restriction) => restriction switch
        {
            "SkipTurn" => new SkipAilmentTurnBehaviorDefinition(),
            "LimitedAction" => new LimitedActionsAilmentTurnBehaviorDefinition(
                [ContentId.Parse("basic_attack"), ContentId.Parse("guard"), ContentId.Parse("pass")]),
            "ChanceSkip" => new ChanceSkipAilmentTurnBehaviorDefinition(50),
            "ChanceSkipOrFlee" => new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(
                40,
                15,
                DemonFleeOutcome.ReturnToStock),
            "ConfusedAction" => new ConfusedActionAilmentTurnBehaviorDefinition(),
            "ForceAttack" => new ForcedBasicAttackAilmentTurnBehaviorDefinition(),
            _ => new NormalAilmentTurnBehaviorDefinition()
        };

        private static AilmentRecoveryDefinition ToRecovery(AilmentData data)
        {
            var removeOn = new List<ContentId>();
            if (data.RemovalTriggers.Contains("OneTurn"))
            {
                removeOn.Add(OwnerTurnEndEventId);
            }

            NaturalAilmentRecoveryDefinition? natural = data.RemovalTriggers.Contains("NaturalRoll")
                ? new NaturalAilmentRecoveryDefinition(20, LuckId, 0.5m)
                : null;
            return new AilmentRecoveryDefinition(natural, removeOn);
        }

        private static IReadOnlyList<PassiveTriggerDefinition> CreateTriggers(AilmentData data)
        {
            var effects = new List<EffectDefinition>();
            if (data.DotPercent > 0)
            {
                effects.Add(new ReduceResourceEffectDefinition(
                    HpId,
                    new FormulaAmountDefinition(LegacyPoisonFormulaId),
                    true));
            }

            if (data.Name.Equals("Sleep", StringComparison.OrdinalIgnoreCase))
            {
                effects.Add(new RestoreResourceEffectDefinition(HpId, new PercentMaximumAmountDefinition(10)));
                effects.Add(new RestoreResourceEffectDefinition(SpId, new PercentMaximumAmountDefinition(10)));
            }

            return effects.Count == 0
                ? []
                :
                [
                    new PassiveTriggerDefinition(OwnerTurnEndEventId, effects)
                ];
        }
    }
}
