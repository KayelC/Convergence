using System.Text;
using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Bridges;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Services;

namespace JRPGPrototype.Logic.Battle
{
    internal sealed class LegacyBattleRewardAdapter
    {
        public static LegacyBattleRewardAdapter Shared { get; } =
            new(new BattleRewardService(new ProductionCombatRuleset(new LegacyRewardRandomSource())));

        private readonly IBattleRewardService _rewards;

        public LegacyBattleRewardAdapter(IBattleRewardService rewards)
        {
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        }

        public LegacyBattleRewardCalculation Calculate(
            IEnumerable<Combatant> enemies,
            IEnumerable<Combatant> recipients)
        {
            ArgumentNullException.ThrowIfNull(enemies);
            ArgumentNullException.ThrowIfNull(recipients);

            var recipientMap = new Dictionary<ContentId, Combatant>();
            var recipientSnapshots = new List<BattleRewardRecipientSnapshot>();
            foreach (Combatant recipient in recipients)
            {
                ContentId id = ToContentId(
                    LegacyRuntimeIdentityRegistry.Shared.GetActorId(recipient).ToString(),
                    recipient.SourceId,
                    "reward_recipient");
                recipientMap[id] = recipient;
                recipientSnapshots.Add(new BattleRewardRecipientSnapshot(
                    id,
                    !recipient.IsDead,
                    recipient.ActivePersona is not null));
            }

            BattleRewardResult result = _rewards.Calculate(new BattleRewardRequest(
                enemies.Select(enemy => new BattleRewardEnemySnapshot(
                    ToContentId(enemy.SourceId, enemy.Name, "reward_enemy"),
                    Math.Max(1, enemy.Level),
                    enemy.GetStat(StatType.St),
                    enemy.GetStat(StatType.Ma),
                    enemy.GetStat(StatType.Vi),
                    enemy.GetStat(StatType.Ag),
                    enemy.GetStat(StatType.Lu),
                    enemy.GetDefense())),
                recipientSnapshots));
            return new LegacyBattleRewardCalculation(result, recipientMap);
        }

        public BattleRewardPresentationResult Present(LegacyBattleRewardCalculation calculation)
        {
            ArgumentNullException.ThrowIfNull(calculation);
            return BattleRewardPresentationResult.Shown(calculation.Result);
        }

        public void Apply(LegacyBattleRewardCalculation calculation, EconomyManager economy, IGameIO io)
        {
            ArgumentNullException.ThrowIfNull(calculation);
            ArgumentNullException.ThrowIfNull(economy);
            ArgumentNullException.ThrowIfNull(io);

            foreach (BattleRewardApplication application in calculation.Result.Applications)
            {
                if (!calculation.Recipients.TryGetValue(application.RecipientId, out Combatant? recipient))
                {
                    continue;
                }

                if (application.Kind == BattleRewardRecipientKind.Actor)
                {
                    recipient.GainExp(application.Experience);
                }
                else
                {
                    recipient.ActivePersona?.GainExp(application.Experience, io);
                }
            }

            economy.AddMacca(calculation.Result.TotalMacca);
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

        private sealed class LegacyRewardRandomSource : IRandomSource
        {
            private readonly Random _random = new();

            public int NextInt32(int minimumInclusive, int maximumExclusive) =>
                _random.Next(minimumInclusive, maximumExclusive);

            public decimal NextUnitDecimal() => (decimal)_random.NextDouble();
        }
    }

    internal sealed record LegacyBattleRewardCalculation(
        BattleRewardResult Result,
        IReadOnlyDictionary<ContentId, Combatant> Recipients);
}
