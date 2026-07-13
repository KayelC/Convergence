using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Battle.Messaging;
using JRPGPrototype.Logic.Battle.Runtime;

namespace JRPGPrototype.Logic.Battle.Bridges
{
    public enum BattleEventPresentationKind
    {
        Shown,
        Suppressed,
        HostOwned
    }

    public sealed record BattleEventPresentationResult
    {
        public BattleEventPresentationResult(
            BattleEventPresentationKind kind,
            BattleEncounterEventKind eventKind,
            BattleEncounterEvent? sourceEvent = null,
            string? message = null,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false)
        {
            Kind = kind;
            EventKind = eventKind;
            SourceEvent = sourceEvent;
            Message = message;
            Color = color;
            Delay = delay;
            WaitForInput = waitForInput;
            ClearScreen = clearScreen;
        }

        public BattleEventPresentationKind Kind { get; }
        public BattleEncounterEventKind EventKind { get; }
        public BattleEncounterEvent? SourceEvent { get; }
        public string? Message { get; }
        public ConsoleColor Color { get; }
        public int Delay { get; }
        public bool WaitForInput { get; }
        public bool ClearScreen { get; }
    }

    internal sealed class LegacyBattleEventPresentationAdapter : IBattleEncounterEventSink
    {
        private readonly IBattleMessenger _messenger;
        private readonly List<BattleEventPresentationResult> _presentations = [];

        public LegacyBattleEventPresentationAdapter(IBattleMessenger messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }

        public IReadOnlyList<BattleEventPresentationResult> Presentations => _presentations.AsReadOnly();

        public ValueTask PublishAsync(BattleEncounterEvent battleEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Publish(Present(battleEvent));
            return ValueTask.CompletedTask;
        }

        public BattleEventPresentationResult Publish(BattleEventPresentationResult presentation)
        {
            ArgumentNullException.ThrowIfNull(presentation);
            _presentations.Add(presentation);
            if (presentation.Kind == BattleEventPresentationKind.Shown &&
                !string.IsNullOrEmpty(presentation.Message))
            {
                _messenger.Publish(
                    presentation.Message,
                    presentation.Color,
                    presentation.Delay,
                    presentation.WaitForInput,
                    clearScreen: presentation.ClearScreen);
            }

            return presentation;
        }

        public BattleEventPresentationResult Present(BattleEncounterEvent battleEvent) =>
            battleEvent.Kind switch
            {
                BattleEncounterEventKind.ActorCreated => Suppressed(battleEvent),
                BattleEncounterEventKind.BattleStarted => Suppressed(battleEvent),
                BattleEncounterEventKind.InitiativeRolled => Suppressed(battleEvent),
                BattleEncounterEventKind.RoundStarted => Suppressed(battleEvent),
                BattleEncounterEventKind.PhaseStarted => Suppressed(battleEvent),
                BattleEncounterEventKind.TurnStarted => Suppressed(battleEvent),
                BattleEncounterEventKind.TurnEconomyChanged => Suppressed(battleEvent),
                BattleEncounterEventKind.PhaseEnded => Suppressed(battleEvent),

                BattleEncounterEventKind.TurnRestricted => HostOwned(battleEvent),
                BattleEncounterEventKind.CommandSelected => HostOwned(battleEvent),
                BattleEncounterEventKind.CommandPassed => HostOwned(battleEvent),
                BattleEncounterEventKind.ActionExecuted => HostOwned(battleEvent),
                BattleEncounterEventKind.ActionRejected => HostOwned(battleEvent),
                BattleEncounterEventKind.EffectResolved => HostOwned(battleEvent),
                BattleEncounterEventKind.PassiveActivated => HostOwned(battleEvent),
                BattleEncounterEventKind.StatusChanged => HostOwned(battleEvent),
                BattleEncounterEventKind.ResourceChanged => HostOwned(battleEvent),
                BattleEncounterEventKind.DeploymentChanged => HostOwned(battleEvent),
                BattleEncounterEventKind.ActorDefeated => HostOwned(battleEvent),
                BattleEncounterEventKind.BattleFaulted => HostOwned(battleEvent),
                BattleEncounterEventKind.BattleEnded => HostOwned(battleEvent),
                BattleEncounterEventKind.HostActionRequested => HostOwned(battleEvent),
                _ => HostOwned(battleEvent)
            };

        public BattleEventPresentationResult PresentTurnRestriction(
            Combatant actor,
            TurnStartResult result,
            bool isPlayerSide)
        {
            ArgumentNullException.ThrowIfNull(actor);
            return result switch
            {
                TurnStartResult.Skip => Shown(
                    BattleEncounterEventKind.TurnRestricted,
                    $"{actor.Name} is unable to move!",
                    ConsoleColor.Magenta,
                    delay: 800),
                TurnStartResult.FleeBattle => Shown(
                    BattleEncounterEventKind.TurnRestricted,
                    $"{actor.Name} fled in fear!",
                    ConsoleColor.Red,
                    delay: 1000),
                TurnStartResult.ReturnToCOMP when isPlayerSide => Shown(
                    BattleEncounterEventKind.TurnRestricted,
                    $"{actor.Name} returned to COMP in terror!",
                    ConsoleColor.Red,
                    delay: 400),
                TurnStartResult.ReturnToCOMP => Shown(
                    BattleEncounterEventKind.TurnRestricted,
                    $"{actor.Name} has fled!",
                    ConsoleColor.Yellow,
                    delay: 400),
                _ => Suppressed(BattleEncounterEventKind.TurnRestricted)
            };
        }

        public BattleEventPresentationResult PresentDemonReturnedToStock(Combatant demon)
        {
            ArgumentNullException.ThrowIfNull(demon);
            return Shown(
                BattleEncounterEventKind.DeploymentChanged,
                $"{demon.Name} faded away and returned to stock...");
        }

        private static BattleEventPresentationResult Suppressed(BattleEncounterEvent sourceEvent) =>
            new(BattleEventPresentationKind.Suppressed, sourceEvent.Kind, sourceEvent);

        private static BattleEventPresentationResult HostOwned(BattleEncounterEvent sourceEvent) =>
            new(BattleEventPresentationKind.HostOwned, sourceEvent.Kind, sourceEvent);

        private static BattleEventPresentationResult Suppressed(BattleEncounterEventKind eventKind) =>
            new(BattleEventPresentationKind.Suppressed, eventKind);

        private static BattleEventPresentationResult Shown(
            BattleEncounterEventKind eventKind,
            string message,
            ConsoleColor color = ConsoleColor.Gray,
            int delay = 0,
            bool waitForInput = false,
            bool clearScreen = false) =>
            new(
                BattleEventPresentationKind.Shown,
                eventKind,
                message: message,
                color: color,
                delay: delay,
                waitForInput: waitForInput,
                clearScreen: clearScreen);
    }
}
