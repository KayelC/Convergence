using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion
{
    public sealed class FusionOwnershipRules
    {
        private readonly PartyManager _partyManager;

        public FusionOwnershipRules(PartyManager partyManager)
        {
            _partyManager = partyManager;
        }

        public bool TryGetOwnedCreateResult(Combatant owner, string resultId, out FusionOwnedResult ownedResult)
        {
            string lookupId = resultId.ToLower();
            string displayName = resultId;
            Database.Personas.TryGetValue(lookupId, out PersonaData? template);
            if (template != null)
            {
                displayName = template.Name;
            }

            if (owner.Class == ClassType.Operator && _partyManager.IsDemonOwned(owner, lookupId))
            {
                ownedResult = new FusionOwnedResult(
                    lookupId,
                    displayName,
                    $"Owned Result: {displayName}",
                    "Fusion aborted: that demon is already in your party or COMP.");
                return true;
            }

            if (owner.Class == ClassType.WildCard &&
                template != null &&
                _partyManager.IsPersonaOwned(owner, template.Name))
            {
                ownedResult = new FusionOwnedResult(
                    lookupId,
                    template.Name,
                    $"Owned Result: {template.Name}",
                    "Fusion aborted: that Persona is already in your stock.");
                return true;
            }

            ownedResult = FusionOwnedResult.Empty;
            return false;
        }

        public Dictionary<object, string> BuildOwnedDuplicateResultReasons(
            Combatant owner,
            IEnumerable<object> pool,
            object firstParent,
            IEnumerable<object> exclusions)
        {
            var disabledReasons = new Dictionary<object, string>();
            var excluded = exclusions.ToHashSet();
            FusionParticipant parentA = FusionParticipant.From(firstParent);

            foreach (object candidate in pool)
            {
                if (excluded.Contains(candidate)) continue;

                FusionParticipant parentB = FusionParticipant.From(candidate);
                // This preview pass only blocks guaranteed direct recipe results. The full calculator
                // can trigger accidents, so calling it here would make menu navigation mutate probability.
                if (!TryGetDirectFusionResultId(parentA.CombatantView, parentB.CombatantView, out string? resultId)) continue;
                if (resultId == null) continue;
                if (!TryGetOwnedCreateResult(owner, resultId, out FusionOwnedResult ownedResult)) continue;

                disabledReasons[candidate] = ownedResult.DisabledReason;
            }

            return disabledReasons;
        }

        public static bool TryGetDirectFusionResultId(Combatant parentA, Combatant parentB, out string? resultId)
        {
            resultId = null;

            if (parentA.ActivePersona == null || parentB.ActivePersona == null)
            {
                return false;
            }

            // Direct pair recipes may be authored either against exact source ids or against races.
            // Special fusion rules such as Mitama boosts and Element rank shifts are intentionally left
            // to the transaction guard because they are not ordinary "create a new demon" results.
            string? resultString =
                FindFusionRecipeResult(parentA.SourceId, parentB.SourceId) ??
                FindFusionRecipeResult(parentA.ActivePersona.Race, parentB.ActivePersona.Race);

            if (string.IsNullOrEmpty(resultString))
            {
                return false;
            }

            string lookupId = resultString.ToLower();
            if (!Database.Personas.ContainsKey(lookupId))
            {
                return false;
            }

            resultId = lookupId;
            return true;
        }

        private static string? FindFusionRecipeResult(string parentA, string parentB)
        {
            var recipe = Database.FusionRecipes.FirstOrDefault(r =>
                (r.ParentA.Equals(parentA, StringComparison.OrdinalIgnoreCase) &&
                 r.ParentB.Equals(parentB, StringComparison.OrdinalIgnoreCase)) ||
                (r.ParentA.Equals(parentB, StringComparison.OrdinalIgnoreCase) &&
                 r.ParentB.Equals(parentA, StringComparison.OrdinalIgnoreCase)));

            return recipe?.Result;
        }
    }

    public readonly struct FusionOwnedResult
    {
        public static FusionOwnedResult Empty { get; } = new FusionOwnedResult(string.Empty, string.Empty, string.Empty, string.Empty);

        public string ResultId { get; }
        public string DisplayName { get; }
        public string DisabledReason { get; }
        public string TransactionAbortMessage { get; }

        public FusionOwnedResult(string resultId, string displayName, string disabledReason, string transactionAbortMessage)
        {
            ResultId = resultId;
            DisplayName = displayName;
            DisabledReason = disabledReason;
            TransactionAbortMessage = transactionAbortMessage;
        }
    }
}
