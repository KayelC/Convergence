using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed class TrainingAnnexBattleRewardApplicator
{
    private readonly IHostEventSink<string> _eventSink;
    private readonly IRandomSource _randomSource;

    public TrainingAnnexBattleRewardApplicator(
        IHostEventSink<string> eventSink,
        IRandomSource randomSource)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
    }

    public async ValueTask<TrainingAnnexBattleRewardApplication> ApplyAsync(
        TrainingAnnexRuntimeActor player,
        BattleRewardResult reward,
        GrowthRulesetServices growthServices,
        IEconomyTransactionService economy,
        RuntimeWalletSnapshot wallet,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot before = player.Actor.State.ToSnapshot();
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            before.Progression,
            before.Stats,
            before.Identity.ActorKindId,
            reward.TotalExperience,
            _randomSource,
            resources: before.Resources,
            baseResourceValues: before.BaseResourceValues));

        WalletTransactionResult walletMutation;
        try
        {
            walletMutation = economy.AddMacca(wallet, reward.TotalMacca);
        }
        catch (OverflowException exception)
        {
            await _eventSink.PublishAsync(
                $"[InvalidCurrencyAmount]: {exception.Message}",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexBattleRewardApplication(false, growth, wallet);
        }

        if (!walletMutation.Applied)
        {
            foreach (ResourceTransactionDiagnostic diagnostic in walletMutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}]: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexBattleRewardApplication(false, growth, wallet);
        }

        RuntimeMutationResult progressionMutation = new RuntimeProgressionTransactionService().ApplyLevelGrowth(
            player.Actor.State,
            growth);
        if (!progressionMutation.Applied)
        {
            foreach (RuntimeMutationDiagnostic diagnostic in progressionMutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexBattleRewardApplication(false, growth, wallet);
        }

        RuntimeActorSnapshot after = progressionMutation.After;
        await _eventSink.PublishAsync(
            $"Battle rewards applied: +{reward.TotalExperience} EXP, +{reward.TotalMacca} Macca.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Reward progression: {player.Actor.Entity.DisplayName} level {before.Progression.Level}->{after.Progression.Level}; exp {before.Progression.Experience}->{after.Progression.Experience}; lifetime {before.Progression.LifetimeExperience}->{after.Progression.LifetimeExperience}; wallet {wallet.Macca}->{walletMutation.After.Macca}.",
            cancellationToken).ConfigureAwait(false);

        return new TrainingAnnexBattleRewardApplication(true, growth, walletMutation.After);
    }

    public static RuntimeSessionProgressSnapshot RecordSessionProgress(
        RuntimeSessionProgressSnapshot before,
        BattleRewardResult reward)
    {
        var counters = before.Counters.ToDictionary(pair => pair.Key, pair => pair.Value);
        AddCounter(counters, ContentId.Parse("training_annex_victories"), 1);
        AddCounter(counters, ContentId.Parse("training_annex_exp"), reward.TotalExperience);
        AddCounter(counters, ContentId.Parse("training_annex_macca"), reward.TotalMacca);
        return new RuntimeSessionProgressSnapshot(
            before.MoonPhaseId,
            before.ElapsedTicks,
            counters,
            before.Flags.Append(TrainingAnnexHostSupport.AshlingDrillClearedFlag).Distinct());
    }

    private static void AddCounter(Dictionary<ContentId, long> counters, ContentId id, long value)
    {
        counters[id] = counters.GetValueOrDefault(id) + value;
    }
}

internal sealed record TrainingAnnexBattleRewardApplication(
    bool Applied,
    LevelGrowthResult Growth,
    RuntimeWalletSnapshot Wallet);
