using Convergence.Content;
using Convergence.Hosting;
using Convergence.Encounters;
using Convergence.Runtime;
using Convergence.Catalog;

namespace Convergence.DemoHost.TrainingAnnex;

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
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        BattleRewardResult reward,
        GrowthRulesetServices growthServices,
        IRuntimeActorCombatProfileCompositionService combatProfileComposition,
        GameDataCatalog catalog,
        RuntimeEquipmentProfile equipmentProfile,
        IEconomyTransactionService economy,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        CancellationToken cancellationToken)
    {
        TrainingAnnexRuntimeActor player = roster.Player;
        RuntimeActorReferenceSnapshot activeReference = partyRoster.ActiveHostedEntity ??
            throw new InvalidOperationException(
                "Training Annex battle rewards require an active Hosted Entity.");
        TrainingAnnexRuntimeActor growthActor = roster.AllActors.Single(actor =>
            actor.Actor.State.InstanceId == activeReference.InstanceId);
        RuntimeActorSnapshot sourceBefore = growthActor.Actor.State.ToSnapshot();
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            sourceBefore.Progression,
            sourceBefore.Stats,
            StandardLevelGrowthProfiles.OwnedEntity,
            reward.TotalExperience,
            _randomSource,
            resources: sourceBefore.Resources,
            baseResourceValues: sourceBefore.BaseResourceValues));

        CurrencyTransactionResult currencyMutation = economy.Credit(
            currencyLedger,
            TrainingAnnexHostSupport.CreditsCurrency,
            reward.TotalCurrency);

        if (!currencyMutation.Applied)
        {
            foreach (ResourceTransactionDiagnostic diagnostic in currencyMutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}]: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexBattleRewardApplication(
                false,
                growth,
                currencyLedger,
                currencyMutation);
        }

        RuntimeActorGrowthCompositionResult progressionMutation =
            new RuntimeActorGrowthCompositionService(
                combatProfileComposition,
                catalog).Apply(new RuntimeActorGrowthCompositionRequest(
                    growthActor.Actor.State,
                    growthActor.Actor.Entity,
                    growth,
                    new SharedRuntimeMoveListCapacityPolicy(),
                    TrainingAnnexHostSupport.CreatePlayerCombatProfileCompositionRequest(
                        roster,
                        partyRoster,
                        equipmentProfile)));
        if (!progressionMutation.Applied)
        {
            foreach (RuntimeActorGrowthCompositionDiagnostic diagnostic in
                     progressionMutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexBattleRewardApplication(
                false,
                growth,
                currencyLedger,
                currencyMutation);
        }

        RuntimeActorSnapshot sourceAfter = progressionMutation.GrowthActorAfter;
        RuntimeActorSnapshot playerAfter = progressionMutation.ComposedActorAfter;
        await _eventSink.PublishAsync(
            $"Battle rewards applied: +{reward.TotalExperience} EXP, +{reward.TotalCurrency} Credits.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Reward progression: {growthActor.Actor.Entity.DisplayName} level " +
            $"{sourceBefore.Progression.Level}->{sourceAfter.Progression.Level}; exp " +
            $"{sourceBefore.Progression.Experience}->{sourceAfter.Progression.Experience}; " +
            $"lifetime {sourceBefore.Progression.LifetimeExperience}->" +
            $"{sourceAfter.Progression.LifetimeExperience}; Vessel " +
            $"{player.Actor.Entity.DisplayName} remains level {playerAfter.Progression.Level}; " +
            $"wallet {TrainingAnnexHostSupport.GetCreditsBalance(currencyLedger)}->" +
            $"{TrainingAnnexHostSupport.GetCreditsBalance(currencyMutation.After)}.",
            cancellationToken).ConfigureAwait(false);

        return new TrainingAnnexBattleRewardApplication(
            true,
            growth,
            currencyMutation.After,
            currencyMutation);
    }

    public static RuntimeSessionProgressSnapshot RecordSessionProgress(
        RuntimeSessionProgressSnapshot before,
        BattleRewardResult reward)
    {
        var counters = before.Counters.ToDictionary(pair => pair.Key, pair => pair.Value);
        AddCounter(counters, ContentId.Parse("training_annex_victories"), 1);
        AddCounter(counters, ContentId.Parse("training_annex_exp"), reward.TotalExperience);
        AddCounter(counters, ContentId.Parse("training_annex_credits"), reward.TotalCurrency);
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
    RuntimeCurrencyLedgerSnapshot CurrencyLedger,
    CurrencyTransactionResult CurrencyTransaction);
