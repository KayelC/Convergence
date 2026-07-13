using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Bridges;
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
            return StartNegotiationDetailed(actor, target, enemies).LegacyResult;
        }

        internal NegotiationSessionPresentationResult StartNegotiationDetailed(
            Combatant actor,
            Combatant target,
            List<Combatant> enemies)
        {
            NegotiationSessionRequest request = CreateRequest(actor, target, enemies);
            var service = new NegotiationSessionService(
                new LegacyRandomSource(_rnd),
                new LegacyNegotiationSessionPolicy());
            var presentation = new LegacyNegotiationPresentationAdapter(_io, target.Name);
            NegotiationSessionResult result = service.RunAsync(
                    request,
                    presentation,
                    presentation)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            ApplyResult(result);
            return new NegotiationSessionPresentationResult(
                ToLegacyResult(result),
                result,
                new NegotiationMutationPresentationResult(
                    result.CurrencySpent,
                    result.ItemSpentId,
                    result.FamiliarGift),
                presentation.AnswerPrompts,
                presentation.DemandPrompts,
                presentation.Events);
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
                activeOpponentCount: Math.Max(1, enemies.Count(e => !e.IsDead)),
                contextIds: MoonPhaseSystem.IsNegotiationBlocked()
                    ? [LegacyNegotiationSessionPolicy.BlockedContextId]
                    : [],
                isTargetFamiliar: _party.IsDemonOwned(actor, target.SourceId),
                hasRecruitmentCapacity: _party.HasOpenDemonStockSlot(actor),
                currentCurrency: _economy.Macca,
                questionPool.Select(question => new NegotiationQuestionPrompt(
                    question.Text,
                    question.Answers.Select(answer => new NegotiationAnswerOption(answer.Text, answer.Value)))),
                familiarDialogue,
                specificFamiliarDialogue,
                healingItems);
        }

        private void ApplyResult(NegotiationSessionResult result)
        {
            if (result.CurrencySpent > 0)
            {
                _economy.SpendMacca(result.CurrencySpent);
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
                case NegotiationFamiliarGiftKind.Currency:
                    _economy.AddMacca(gift.Currency);
                    break;
                case NegotiationFamiliarGiftKind.RestoreParty:
                    foreach (var member in _party.GetAliveMembers())
                    {
                        member.CurrentHP = (int)Math.Min(
                            member.MaxHP,
                            member.CurrentHP + (member.MaxHP * (double)gift.RestorePercent));
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

        private static NegotiationResult ToLegacyResult(NegotiationSessionResult result) =>
            result.Outcome switch
            {
                NegotiationOutcomeKind.Success => NegotiationResult.Success,
                NegotiationOutcomeKind.Trick => NegotiationResult.Trick,
                NegotiationOutcomeKind.Flee => NegotiationResult.Flee,
                NegotiationOutcomeKind.FamiliarFlee => NegotiationResult.FamiliarFlee,
                _ => NegotiationResult.Failure
            };
    }

}
