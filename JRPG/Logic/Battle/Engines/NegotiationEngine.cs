using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Runtime;

namespace JRPGPrototype.Logic.Battle.Engines
{
    /// <summary>
    /// Represents the possible outcomes of a negotiation attempt.
    /// </summary>
    public enum NegotiationResult
    {
        InProgress,
        Success,
        Failure,
        Trick, // Demon took items/Macca but didn't join
        Flee, // Demon ran away
        FamiliarFlee // Familiar demon gave gift and ran
    }

    /// <summary>
    /// Manages the state and flow of the negotiation mini-game.
    /// Uses Race-driven personalities and a global question pool.
    /// </summary>
    public class NegotiationEngine
    {
        private readonly IGameIO _io;
        private readonly PartyManager _party;
        private readonly InventoryManager _inventory;
        private readonly EconomyManager _economy;
        private readonly Random _rnd;

        // Mapped all 32 Races to the 8 Personality Types
        private static readonly Dictionary<string, PersonalityType> RaceToPersonality =
            new Dictionary<string, PersonalityType>(StringComparer.OrdinalIgnoreCase)
        {
            // --- 1. Dark ---
            { "Foul", PersonalityType.Gloomy },
            { "Haunt", PersonalityType.Gloomy },
            { "Raptor", PersonalityType.Childlike },
            { "Tyrant", PersonalityType.Arrogant },
            { "Vile", PersonalityType.Arrogant },
            { "Wilder", PersonalityType.Timid },

            // --- 2. Light ---
            { "Avatar", PersonalityType.Honorable },
            { "Avian", PersonalityType.Upbeat },
            { "Deity", PersonalityType.Arrogant },
            { "Dragon", PersonalityType.Arrogant },
            { "Element", PersonalityType.Formal },
            { "Mitama", PersonalityType.Childlike },
            { "Entity", PersonalityType.Formal },
            { "Fury", PersonalityType.Arrogant },
            { "Genma", PersonalityType.Honorable },
            { "Holy", PersonalityType.Formal },
            { "Kishin", PersonalityType.Honorable },
            { "Lady", PersonalityType.Sultry },
            { "Megami", PersonalityType.Formal },
            { "Seraph", PersonalityType.Honorable },
            { "Wargod", PersonalityType.Upbeat },

            // --- 3. Neutral ---
            { "Beast", PersonalityType.Upbeat },
            { "Brute", PersonalityType.Timid },
            { "Divine", PersonalityType.Honorable },
            { "Fairy", PersonalityType.Childlike },
            { "Fallen", PersonalityType.Gloomy },
            { "Femme", PersonalityType.Sultry },
            { "Jirae", PersonalityType.Gloomy },
            { "Night", PersonalityType.Sultry },
            { "Snake", PersonalityType.Gloomy },
            { "Yoma", PersonalityType.Upbeat },

            // --- 4. Unclassified ---
            { "Fiend", PersonalityType.Arrogant }
        };

        public NegotiationEngine(IGameIO io, PartyManager party, InventoryManager inventory, EconomyManager economy)
            : this(io, party, inventory, economy, new Random())
        {
        }

        internal NegotiationEngine(
            IGameIO io,
            PartyManager party,
            InventoryManager inventory,
            EconomyManager economy,
            Random random)
        {
            _io = io;
            _party = party;
            _inventory = inventory;
            _economy = economy;
            _rnd = random ?? throw new ArgumentNullException(nameof(random));
        }

        public NegotiationResult StartNegotiation(Combatant actor, Combatant target, List<Combatant> enemies)
        {
            NegotiationSessionRequest request = CreateRequest(actor, target, enemies);
            var service = new NegotiationSessionService(new LegacyRandomSource(_rnd));
            NegotiationSessionResult result = service.RunAsync(
                    request,
                    new LegacyNegotiationCommandSource(_io, target.Name),
                    new LegacyNegotiationEventSink(_io))
                .AsTask()
                .GetAwaiter()
                .GetResult();

            ApplyResult(result);
            return result.Outcome switch
            {
                NegotiationOutcomeKind.Success => NegotiationResult.Success,
                NegotiationOutcomeKind.Trick => NegotiationResult.Trick,
                NegotiationOutcomeKind.Flee => NegotiationResult.Flee,
                NegotiationOutcomeKind.FamiliarFlee => NegotiationResult.FamiliarFlee,
                _ => NegotiationResult.Failure
            };
        }

        private NegotiationSessionRequest CreateRequest(Combatant actor, Combatant target, List<Combatant> enemies)
        {
            string race = target.ActivePersona?.Race ?? "Fairy";
            PersonalityType personality = RaceToPersonality.GetValueOrDefault(race, PersonalityType.Childlike);
            var questionPool = Database.NegotiationQuestions.Questions.GetValueOrDefault(
                personality,
                new List<NegotiationQuestion>());
            string? specificFamiliarDialogue = null;
            string lookupId = target.SourceId.ToLower();
            if (Database.Personas.TryGetValue(lookupId, out var pData) &&
                !string.IsNullOrEmpty(pData.FamiliarDialogue))
            {
                specificFamiliarDialogue = pData.FamiliarDialogue;
            }

            var familiarDialogue = Database.NegotiationQuestions
                .FamiliarDialogues.GetValueOrDefault(personality, new List<string>());
            var healingItems = _inventory.GetAllItemIds()
                .Where(id => Database.Items.TryGetValue(id, out var item) && item.Type == "Healing")
                .Select(id => new NegotiationAvailableItem(id, Database.Items[id].Name));

            return new NegotiationSessionRequest(
                target.Name,
                Math.Max(1, actor.Level),
                Math.Max(1, target.Level),
                actor.GetStat(StatType.Lu),
                Math.Max(1, enemies.Count(e => !e.IsDead)),
                MoonPhaseSystem.IsNegotiationBlocked(),
                _party.IsDemonOwned(actor, target.SourceId),
                _party.HasOpenDemonStockSlot(actor),
                _economy.Macca,
                questionPool.Select(question => new NegotiationQuestionPrompt(
                    question.Text,
                    question.Answers.Select(answer => new NegotiationAnswerOption(answer.Text, answer.Value)))),
                familiarDialogue,
                specificFamiliarDialogue,
                healingItems);
        }

        private void ApplyResult(NegotiationSessionResult result)
        {
            if (result.MaccaSpent > 0)
            {
                _economy.SpendMacca(result.MaccaSpent);
            }
            if (result.ItemSpentId is string itemId)
            {
                _inventory.RemoveItem(itemId, 1);
            }

            NegotiationFamiliarGift gift = result.FamiliarGift;
            switch (gift.Kind)
            {
                case NegotiationFamiliarGiftKind.Item when gift.ItemId is string giftItem:
                    _inventory.AddItem(giftItem, gift.Quantity);
                    break;
                case NegotiationFamiliarGiftKind.Macca:
                    _economy.AddMacca(gift.Macca);
                    break;
                case NegotiationFamiliarGiftKind.HealParty:
                    foreach (var member in _party.GetAliveMembers())
                    {
                        member.CurrentHP = (int)Math.Min(
                            member.MaxHP,
                            member.CurrentHP + (member.MaxHP * (double)gift.HealPercent));
                    }
                    break;
            }
        }

        private sealed class LegacyRandomSource : IRandomSource
        {
            private readonly Random _random;

            public LegacyRandomSource(Random random)
            {
                _random = random ?? throw new ArgumentNullException(nameof(random));
            }

            public int NextInt32(int minimumInclusive, int maximumExclusive) =>
                _random.Next(minimumInclusive, maximumExclusive);

            public decimal NextUnitDecimal() => (decimal)_random.NextDouble();
        }

        private sealed class LegacyNegotiationCommandSource : INegotiationCommandSource
        {
            private readonly IGameIO _io;
            private readonly string _targetName;

            public LegacyNegotiationCommandSource(IGameIO io, string targetName)
            {
                _io = io ?? throw new ArgumentNullException(nameof(io));
                _targetName = targetName;
            }

            public ValueTask<NegotiationAnswerSelection> ReadAnswerAsync(
                NegotiationQuestionPrompt prompt,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int choice = _io.RenderMenu(
                    $"{_targetName}: \"{prompt.Text}\"",
                    prompt.Answers.Select(answer => answer.Text).ToList(),
                    0);
                return ValueTask.FromResult(choice < 0
                    ? NegotiationAnswerSelection.Cancel()
                    : NegotiationAnswerSelection.Selected(choice));
            }

            public ValueTask<NegotiationDemandSelection> ReadDemandAsync(
                NegotiationDemandPrompt prompt,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int choice = _io.RenderMenu(
                    prompt.Prompt,
                    prompt.Options.Select(option => option.Label).ToList(),
                    0);
                if (choice < 0)
                {
                    return ValueTask.FromResult(NegotiationDemandSelection.Cancel());
                }

                return ValueTask.FromResult(NegotiationDemandSelection.Selected(prompt.Options[choice].Decision));
            }
        }

        private sealed class LegacyNegotiationEventSink : IHostEventSink<NegotiationEvent>
        {
            private readonly IGameIO _io;

            public LegacyNegotiationEventSink(IGameIO io)
            {
                _io = io ?? throw new ArgumentNullException(nameof(io));
            }

            public ValueTask PublishAsync(
                NegotiationEvent hostEvent,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _io.WriteLine(hostEvent.Message, Color(hostEvent));

                int wait = WaitMilliseconds(hostEvent);
                if (wait > 0)
                {
                    _io.Wait(wait);
                }

                return ValueTask.CompletedTask;
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

    internal static class NegotiationEventExtensions
    {
        public static bool ReasonlessMessageIsUnresponsive(this NegotiationEvent hostEvent) =>
            hostEvent.Message.Contains("seems unresponsive", StringComparison.Ordinal);
    }
}
