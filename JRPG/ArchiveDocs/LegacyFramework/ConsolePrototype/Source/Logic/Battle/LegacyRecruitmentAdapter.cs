using System.Text;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Fusion;

namespace JRPGPrototype.Logic.Battle
{
    internal sealed class LegacyRecruitmentAdapter
    {
        public static LegacyRecruitmentAdapter Shared { get; } = new(new RecruitmentTransactionService());

        private readonly IRecruitmentTransactionService _transactions;

        public LegacyRecruitmentAdapter(IRecruitmentTransactionService transactions)
        {
            _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        }

        public LegacyRecruitmentResult TryRecruit(
            Combatant owner,
            Combatant target,
            ICollection<string> sessionRecruitedIds,
            IList<Combatant> enemies,
            PartyManager party,
            CompendiumRegistry compendium)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(sessionRecruitedIds);
            ArgumentNullException.ThrowIfNull(enemies);
            ArgumentNullException.ThrowIfNull(party);
            ArgumentNullException.ThrowIfNull(compendium);

            RecruitmentTransactionResult validation = _transactions.Validate(new RecruitmentTransactionRequest(
                ToContentId(target.SourceId, target.Name, "recruitment_target"),
                sessionRecruitedIds.Contains(target.SourceId),
                party.IsDemonOwned(owner, target.SourceId),
                party.HasOpenDemonStockSlot(owner),
                !string.IsNullOrWhiteSpace(target.SourceId)));

            if (!validation.Applied)
            {
                return new LegacyRecruitmentResult(false, validation, null);
            }

            Combatant newDemon = CombatantFactory.CreateEnemy(target.SourceId);
            if (!compendium.HasEntry(newDemon.SourceId))
            {
                compendium.RegisterDemon(newDemon);
            }

            owner.DemonStock.Add(newDemon);
            sessionRecruitedIds.Add(target.SourceId);
            enemies.Remove(target);
            return new LegacyRecruitmentResult(true, validation, newDemon);
        }

        private static ContentId ToContentId(string? preferred, string? fallback, string defaultValue)
        {
            string raw = !string.IsNullOrWhiteSpace(preferred)
                ? preferred
                : !string.IsNullOrWhiteSpace(fallback)
                    ? fallback
                    : defaultValue;
            var builder = new StringBuilder(raw.Length);
            bool previousUnderscore = false;
            foreach (char character in raw.Trim().ToLowerInvariant())
            {
                bool valid = character is >= 'a' and <= 'z' or >= '0' and <= '9';
                if (valid)
                {
                    builder.Append(character);
                    previousUnderscore = false;
                }
                else if (!previousUnderscore)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }
            }

            string normalized = builder.ToString().Trim('_');
            return ContentId.Parse(string.IsNullOrWhiteSpace(normalized) ? defaultValue : normalized);
        }
    }

    internal sealed record LegacyRecruitmentResult(
        bool Applied,
        RecruitmentTransactionResult Transaction,
        Combatant? RecruitedDemon);
}
