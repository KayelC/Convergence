using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JRPGPrototype.Entities;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle.Bridges
{
    public enum NegotiationPresentationKind
    {
        Selected,
        Back,
        Shown,
        Suppressed,
        HostOwned
    }

    public sealed record NegotiationAnswerPromptPresentationResult
    {
        public NegotiationAnswerPromptPresentationResult(
            NegotiationPresentationKind kind,
            string header,
            IEnumerable<string> options,
            NegotiationAnswerSelection selection,
            int? selectedIndex = null)
        {
            Kind = kind;
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Options = Array.AsReadOnly((options ?? throw new ArgumentNullException(nameof(options))).ToArray());
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            SelectedIndex = selectedIndex;
        }

        public NegotiationPresentationKind Kind { get; }
        public string Header { get; }
        public IReadOnlyList<string> Options { get; }
        public NegotiationAnswerSelection Selection { get; }
        public int? SelectedIndex { get; }
    }

    public sealed record NegotiationDemandPromptPresentationResult
    {
        public NegotiationDemandPromptPresentationResult(
            NegotiationPresentationKind kind,
            string header,
            IEnumerable<string> options,
            NegotiationDemandSelection selection,
            int? selectedIndex = null)
        {
            Kind = kind;
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Options = Array.AsReadOnly((options ?? throw new ArgumentNullException(nameof(options))).ToArray());
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            SelectedIndex = selectedIndex;
        }

        public NegotiationPresentationKind Kind { get; }
        public string Header { get; }
        public IReadOnlyList<string> Options { get; }
        public NegotiationDemandSelection Selection { get; }
        public int? SelectedIndex { get; }
    }

    public sealed record NegotiationEventPresentationResult
    {
        public NegotiationEventPresentationResult(
            NegotiationPresentationKind kind,
            NegotiationEvent sourceEvent,
            string message,
            ConsoleColor color = ConsoleColor.White,
            int delay = 0)
        {
            Kind = kind;
            SourceEvent = sourceEvent ?? throw new ArgumentNullException(nameof(sourceEvent));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Color = color;
            Delay = delay;
        }

        public NegotiationPresentationKind Kind { get; }
        public NegotiationEvent SourceEvent { get; }
        public string Message { get; }
        public ConsoleColor Color { get; }
        public int Delay { get; }
    }

    public sealed record NegotiationMutationPresentationResult(
        int MaccaSpent,
        string? ItemSpentId,
        NegotiationFamiliarGift FamiliarGift);

    public sealed record NegotiationSessionPresentationResult
    {
        public NegotiationSessionPresentationResult(
            NegotiationResult legacyResult,
            NegotiationSessionResult sessionResult,
            NegotiationMutationPresentationResult mutation,
            IEnumerable<NegotiationAnswerPromptPresentationResult> answerPrompts,
            IEnumerable<NegotiationDemandPromptPresentationResult> demandPrompts,
            IEnumerable<NegotiationEventPresentationResult> events)
        {
            LegacyResult = legacyResult;
            SessionResult = sessionResult ?? throw new ArgumentNullException(nameof(sessionResult));
            Mutation = mutation ?? throw new ArgumentNullException(nameof(mutation));
            AnswerPrompts = Array.AsReadOnly((answerPrompts ?? throw new ArgumentNullException(nameof(answerPrompts))).ToArray());
            DemandPrompts = Array.AsReadOnly((demandPrompts ?? throw new ArgumentNullException(nameof(demandPrompts))).ToArray());
            Events = Array.AsReadOnly((events ?? throw new ArgumentNullException(nameof(events))).ToArray());
        }

        public NegotiationResult LegacyResult { get; }
        public NegotiationSessionResult SessionResult { get; }
        public NegotiationMutationPresentationResult Mutation { get; }
        public IReadOnlyList<NegotiationAnswerPromptPresentationResult> AnswerPrompts { get; }
        public IReadOnlyList<NegotiationDemandPromptPresentationResult> DemandPrompts { get; }
        public IReadOnlyList<NegotiationEventPresentationResult> Events { get; }
    }

    public enum BattleNegotiationTurnEffect
    {
        None,
        Normal,
        TerminatePhase,
        Miss
    }

    public sealed record BattleNegotiationPresentationResult(
        NegotiationPresentationKind Kind,
        string? Message,
        ConsoleColor Color,
        int Delay,
        BattleNegotiationTurnEffect TurnEffect,
        bool RemoveTarget = false)
    {
        public static BattleNegotiationPresentationResult AlreadySpoken(Combatant target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return new(
                NegotiationPresentationKind.Shown,
                $"{target.Name} has already been spoken to.",
                ConsoleColor.Gray,
                800,
                BattleNegotiationTurnEffect.None);
        }

        public static BattleNegotiationPresentationResult Joined(Combatant target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return new(
                NegotiationPresentationKind.Shown,
                $"{target.Name} joined your party!",
                ConsoleColor.Green,
                0,
                BattleNegotiationTurnEffect.Normal);
        }

        public static BattleNegotiationPresentationResult FailedEndsTurn() =>
            new(
                NegotiationPresentationKind.Shown,
                "Negotiation failed! Your turn ends.",
                ConsoleColor.Red,
                0,
                BattleNegotiationTurnEffect.TerminatePhase);

        public static BattleNegotiationPresentationResult LeftBattle(Combatant target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return new(
                NegotiationPresentationKind.Shown,
                $"{target.Name} left the battle.",
                ConsoleColor.Gray,
                0,
                BattleNegotiationTurnEffect.Miss,
                RemoveTarget: true);
        }
    }

    public sealed record BattleRewardPresentationResult(
        NegotiationPresentationKind Kind,
        BattleRewardResult SourceResult,
        string Message,
        ConsoleColor Color = ConsoleColor.Gray,
        int Delay = 800)
    {
        public static BattleRewardPresentationResult Shown(BattleRewardResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return new(
                NegotiationPresentationKind.Shown,
                result,
                $"Gained {result.TotalExperience} EXP and {result.TotalMacca} Macca.");
        }
    }

    internal sealed class LegacyNegotiationPresentationAdapter :
        INegotiationCommandSource,
        IHostEventSink<NegotiationEvent>
    {
        private readonly IGameIO _io;
        private readonly string _targetName;
        private readonly List<NegotiationAnswerPromptPresentationResult> _answerPrompts = [];
        private readonly List<NegotiationDemandPromptPresentationResult> _demandPrompts = [];
        private readonly List<NegotiationEventPresentationResult> _events = [];

        public LegacyNegotiationPresentationAdapter(IGameIO io, string targetName)
        {
            _io = io ?? throw new ArgumentNullException(nameof(io));
            _targetName = targetName ?? throw new ArgumentNullException(nameof(targetName));
        }

        public IReadOnlyList<NegotiationAnswerPromptPresentationResult> AnswerPrompts => _answerPrompts.AsReadOnly();
        public IReadOnlyList<NegotiationDemandPromptPresentationResult> DemandPrompts => _demandPrompts.AsReadOnly();
        public IReadOnlyList<NegotiationEventPresentationResult> Events => _events.AsReadOnly();

        public ValueTask<NegotiationAnswerSelection> ReadAnswerAsync(
            NegotiationQuestionPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string header = $"{_targetName}: \"{prompt.Text}\"";
            var options = prompt.Answers.Select(answer => answer.Text).ToArray();
            int choice = _io.RenderMenu(header, options.ToList(), 0);
            NegotiationAnswerSelection selection = choice < 0
                ? NegotiationAnswerSelection.Cancel()
                : NegotiationAnswerSelection.Selected(choice);
            _answerPrompts.Add(new NegotiationAnswerPromptPresentationResult(
                choice < 0 ? NegotiationPresentationKind.Back : NegotiationPresentationKind.Selected,
                header,
                options,
                selection,
                choice < 0 ? null : choice));
            return ValueTask.FromResult(selection);
        }

        public ValueTask<NegotiationDemandSelection> ReadDemandAsync(
            NegotiationDemandPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = prompt.Options.Select(option => option.Label).ToArray();
            int choice = _io.RenderMenu(prompt.Prompt, options.ToList(), 0);
            NegotiationDemandSelection selection = choice < 0
                ? NegotiationDemandSelection.Cancel()
                : NegotiationDemandSelection.Selected(prompt.Options[choice].Decision);
            _demandPrompts.Add(new NegotiationDemandPromptPresentationResult(
                choice < 0 ? NegotiationPresentationKind.Back : NegotiationPresentationKind.Selected,
                prompt.Prompt,
                options,
                selection,
                choice < 0 ? null : choice));
            return ValueTask.FromResult(selection);
        }

        public ValueTask PublishAsync(
            NegotiationEvent hostEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NegotiationEventPresentationResult presentation = PresentEvent(hostEvent);
            _events.Add(presentation);
            if (presentation.Kind == NegotiationPresentationKind.Shown)
            {
                _io.WriteLine(presentation.Message, presentation.Color);
                if (presentation.Delay > 0)
                {
                    _io.Wait(presentation.Delay);
                }
            }

            return ValueTask.CompletedTask;
        }

        public static NegotiationEventPresentationResult PresentEvent(NegotiationEvent hostEvent)
        {
            ArgumentNullException.ThrowIfNull(hostEvent);
            return new NegotiationEventPresentationResult(
                NegotiationPresentationKind.Shown,
                hostEvent,
                hostEvent.Message,
                Color(hostEvent),
                WaitMilliseconds(hostEvent));
        }

        private static ConsoleColor Color(NegotiationEvent hostEvent) => hostEvent.Kind switch
        {
            NegotiationEventKind.FamiliarDialogue => ConsoleColor.Cyan,
            NegotiationEventKind.MoodNegative => ConsoleColor.Red,
            NegotiationEventKind.Failure when hostEvent.Message.Contains("Full Moon", StringComparison.Ordinal) ||
                hostEvent.Message.Contains("required donation", StringComparison.Ordinal) => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        private static int WaitMilliseconds(NegotiationEvent hostEvent)
        {
            if (hostEvent.Kind == NegotiationEventKind.DemandIntro ||
                hostEvent.Kind == NegotiationEventKind.MoodNegative ||
                hostEvent.ReasonlessMessageIsUnresponsive())
            {
                return 800;
            }

            if (hostEvent.Message.Contains("Full Moon", StringComparison.Ordinal) ||
                hostEvent.Message.Contains("Demon Stock is full", StringComparison.Ordinal) ||
                hostEvent.Message.Contains("refuses to talk", StringComparison.Ordinal) ||
                hostEvent.Message.Contains("required donation", StringComparison.Ordinal))
            {
                return 1000;
            }

            return 0;
        }
    }
}
