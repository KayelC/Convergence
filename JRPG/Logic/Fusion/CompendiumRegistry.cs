using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The persistent storage authority for the Demonic Compendium.
    /// Handles deep-cloning of demon states and dynamic recall cost calculation.
    /// Uses normalized Species IDs (Persona IDs) as the unique registry keys.
    /// </summary>
    public class CompendiumRegistry
    {
        // Key: Normalized Species ID (matching the final_unified_database IDs)
        // Value: The snapshot of the Combatant
        private readonly Dictionary<string, Combatant> _demonEntries;
        private readonly ICompendiumService _service;
        private CompendiumStateSnapshot _state;

        private readonly IGameIO _io;

        public CompendiumRegistry(IGameIO io)
        {
            _io = io;
            _demonEntries = new Dictionary<string, Combatant>(StringComparer.OrdinalIgnoreCase);
            _service = new CompendiumService();
            _state = new CompendiumStateSnapshot();
        }

        #region Registration Logic

        /// <summary>
        /// Saves a permanent deep-copy snapshot of a demon's current state.
        /// Feature: Normalizes the ID to ensure species-level uniqueness.
        /// </summary>
        public void RegisterDemon(Combatant demon)
            => RegisterDemonDetailed(demon);

        internal CompendiumRegistrationPresentationResult RegisterDemonDetailed(Combatant demon)
        {
            if (demon == null || demon.Class != ClassType.Demon)
            {
                string message = "Invalid entity. Only demons can be registered in the Compendium.";
                _io.WriteLine(message, ConsoleColor.Red);
                return new CompendiumRegistrationPresentationResult(
                    FusionPresentationResultKind.Rejected,
                    demon,
                    null,
                    new FusionPresentationEvent(FusionPresentationResultKind.Shown, message, ConsoleColor.Red));
            }

            // Ensure we use the canonical ID for registration
            string speciesId = ResolveSpeciesId(demon);

            // Create an immutable snapshot
            Combatant snapshot = CloneCombatant(demon);
            snapshot.SourceId = speciesId; // Ensure the snapshot itself is normalized

            CompendiumRegistrationResult result = _service.Register(_state, ToEntry(snapshot));
            _state = result.After;
            _demonEntries[speciesId] = snapshot;

            if (result.Code == CompendiumRegistrationCode.Updated)
            {
                string message = $"{demon.Name} data has been updated in the registry.";
                _io.WriteLine(message, ConsoleColor.Cyan);
                _io.Wait(600);
                return new CompendiumRegistrationPresentationResult(
                    FusionPresentationResultKind.Applied,
                    demon,
                    result,
                    new FusionPresentationEvent(FusionPresentationResultKind.Shown, message, ConsoleColor.Cyan, 600));
            }
            else
            {
                string message = $"{demon.Name} has been recorded in the Compendium.";
                _io.WriteLine(message, ConsoleColor.Green);
                _io.Wait(600);
                return new CompendiumRegistrationPresentationResult(
                    FusionPresentationResultKind.Applied,
                    demon,
                    result,
                    new FusionPresentationEvent(FusionPresentationResultKind.Shown, message, ConsoleColor.Green, 600));
            }
        }

        #endregion

        #region Recall and Cost Logic

        /// <summary>
        /// Calculates the Macca cost to recall a demon using the power-scaling formula.
        /// </summary>
        public int CalculateRecallCost(string speciesId)
        {
            string cleanId = speciesId.ToLower();

            ContentId species = LegacyFusionContentAdapter.ToContentId(cleanId);
            if (!_state.TryGet(species, out CompendiumEntrySnapshot? entry) || entry is null)
            {
                return 0;
            }

            return _service.CalculateRecallCost(entry, ResolveRecallBasePrice(cleanId));
        }

        internal CompendiumRecallAssessment AssessRecall(
            Combatant owner,
            string speciesId,
            int currentMacca,
            bool alreadyOwned,
            bool hasOpenStockSlot)
        {
            ArgumentNullException.ThrowIfNull(owner);
            string cleanId = speciesId.ToLower();
            return _service.AssessRecall(
                _state,
                LegacyFusionContentAdapter.ToContentId(cleanId),
                currentMacca,
                alreadyOwned,
                hasOpenStockSlot,
                ResolveRecallBasePrice(cleanId));
        }

        /// <summary>
        /// Retrieves a deep-copy of a registered demon for recruitment.
        /// </summary>
        public Combatant GetRecallEntry(string speciesId)
        {
            string cleanId = speciesId.ToLower();

            if (_demonEntries.TryGetValue(cleanId, out var snapshot))
            {
                return CloneCombatant(snapshot);
            }

            return null!;
        }

        #endregion

        #region Retrieval and Metadata

        public List<Combatant> GetAllRegisteredDemons()
        {
            return _demonEntries.Values
                .OrderBy(d => d.Level)
                .ThenBy(d => d.Name)
                .Select(CloneCombatant)
                .ToList();
        }

        public bool HasEntry(string speciesId)
        {
            return _state.TryGet(LegacyFusionContentAdapter.ToContentId(speciesId), out _);
        }

        #endregion

        #region Normalization and Cloning Kernels

        /// <summary>
        /// Resolves the base species ID for a combatant. 
        /// Prefers the ActivePersona's identity over the instance SourceId.
        /// </summary>
        private string ResolveSpeciesId(Combatant c)
        {
            return c.SourceId.ToLower();
        }

        private static int ResolveRecallBasePrice(string cleanId)
        {
            var shopEntry = Database.ShopInventory.FirstOrDefault(s => s.Id.Equals(cleanId, StringComparison.OrdinalIgnoreCase));
            return shopEntry?.BasePrice ?? 2000;
        }

        private Combatant CloneCombatant(Combatant original)
        {
            Combatant clone = new Combatant(original.Name, original.Class)
            {
                SourceId = original.SourceId.ToLower(),
                Level = original.Level,
                Exp = original.Exp,
                StatPoints = original.StatPoints,
                BaseHP = original.BaseHP,
                BaseSP = original.BaseSP,
                OwnerId = original.OwnerId,
                BattleControl = original.BattleControl,
                Controller = original.Controller,
                ActivePersona = original.ActivePersona == null ? null : ClonePersona(original.ActivePersona)
            };

            foreach (var stat in original.CharacterStats)
            {
                if (clone.CharacterStats.ContainsKey(stat.Key))
                    clone.CharacterStats[stat.Key] = stat.Value;
                else
                    clone.CharacterStats.Add(stat.Key, stat.Value);
            }

            foreach (var skill in original.ExtraSkills)
            {
                clone.ExtraSkills.Add(skill);
            }

            clone.RecalculateResources();
            clone.CurrentHP = clone.MaxHP;
            clone.CurrentSP = clone.MaxSP;

            return clone;
        }

        private static Persona ClonePersona(Persona original)
        {
            var clone = new Persona
            {
                Name = original.Name,
                Level = original.Level,
                Race = original.Race,
                Rank = original.Rank,
                InheritanceType = original.InheritanceType,
                Exp = original.Exp,
                LifetimeEarnedExp = original.LifetimeEarnedExp,
                CombatDefenseProfile = original.CombatDefenseProfile
            };

            foreach (var affinity in original.AffinityMap)
            {
                clone.AffinityMap[affinity.Key] = affinity.Value;
            }

            foreach (var stat in original.StatModifiers)
            {
                clone.StatModifiers[stat.Key] = stat.Value;
            }

            clone.SkillSet.AddRange(original.SkillSet);
            foreach (var learned in original.SkillsToLearn)
            {
                clone.SkillsToLearn[learned.Key] = learned.Value;
            }

            return clone;
        }

        private static CompendiumEntrySnapshot ToEntry(Combatant snapshot) =>
            new(
                LegacyFusionContentAdapter.ToContentId(snapshot.SourceId),
                snapshot.Name,
                Math.Max(1, snapshot.Level),
                snapshot.CharacterStats.Select(stat => new KeyValuePair<ContentId, int>(
                    stat.Key switch
                    {
                        StatType.St => ContentId.Parse("strength"),
                        StatType.Ma => ContentId.Parse("magic"),
                        StatType.Vi => ContentId.Parse("vitality"),
                        StatType.Ag => ContentId.Parse("agility"),
                        StatType.Lu => ContentId.Parse("luck"),
                        _ => LegacyFusionContentAdapter.ToContentId(stat.Key.ToString())
                    },
                    stat.Value)),
                snapshot.GetConsolidatedSkills().Select(LegacyFusionContentAdapter.ToContentId),
                snapshot.Exp,
                snapshot.LifetimeEarnedExp);

        #endregion
    }
}
