using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class NegotiationRewardRuntimeTests
{
    [Fact]
    public async Task NegotiationSession_PreservesDemandFlowAndDeferredMutation()
    {
        var random = new SequenceRandomSource(ints: [0, 0, 0, 0]);
        var policy = new TestNegotiationPolicy(
            fallbackDemands:
            [
                new NegotiationRuntimeDemand(
                    ContentId.Parse("currency"),
                    NegotiationDemandKind.Currency,
                    weight: 1,
                    currencyAmount: 40),
                new NegotiationRuntimeDemand(
                    ContentId.Parse("healing_item"),
                    NegotiationDemandKind.Item,
                    weight: 1,
                    item: new NegotiationAvailableItem("101", "Medicine"))
            ]);
        var service = new NegotiationSessionService(random, policy);
        var commands = new QueueNegotiationCommands(
            answers: [0, 0, 0],
            demands: [NegotiationDemandDecision.Accept, NegotiationDemandDecision.Refuse]);
        var events = new RecordingEventSink<NegotiationEvent>();

        NegotiationSessionResult result = await service.RunAsync(
            new NegotiationSessionRequest(
                "Pixie",
                actorLevel: 50,
                targetLevel: 2,
                actorLuck: 0,
                activeOpponentCount: 1,
                contextIds: [],
                isTargetFamiliar: false,
                hasRecruitmentCapacity: true,
                currentCurrency: 1000,
                questions:
                [
                    Question("Do you like me?", 2),
                    Question("Do you trust me?", 2),
                    Question("Will you join?", 2)
                ],
                availableHealingItems: [new NegotiationAvailableItem("101", "Medicine")]),
            commands,
            events);

        Assert.Equal(NegotiationOutcomeKind.Failure, result.Outcome);
        Assert.Equal(NegotiationOutcomeReason.ItemRefused, result.Reason);
        Assert.Equal(6, result.MoodScore);
        Assert.Equal(40, result.CurrencySpent);
        Assert.Null(result.ItemSpentId);
        Assert.Contains(result.Events, ev => ev.Kind == NegotiationEventKind.DemandIntro);
        Assert.Equal(result.Events, events.Events);
    }

    [Fact]
    public async Task NegotiationSession_UsesAuthoredDemandInsteadOfCalculatedMaccaFormula()
    {
        var random = new SequenceRandomSource(ints: [0, 0, 0]);
        var policy = new TestNegotiationPolicy(
            fallbackDemands:
            [
                new NegotiationRuntimeDemand(
                    ContentId.Parse("fallback"),
                    NegotiationDemandKind.Currency,
                    weight: 1,
                    currencyAmount: 99)
            ]);
        var service = new NegotiationSessionService(random, policy);
        var commands = new QueueNegotiationCommands(
            answers: [0, 0],
            demands: [NegotiationDemandDecision.Accept]);

        NegotiationSessionResult result = await service.RunAsync(
            new NegotiationSessionRequest(
                "Pixie",
                actorLevel: 50,
                targetLevel: 9,
                actorLuck: 0,
                activeOpponentCount: 1,
                contextIds: [],
                isTargetFamiliar: false,
                hasRecruitmentCapacity: true,
                currentCurrency: 100,
                questions:
                [
                    Question("Do you like me?", 2),
                    Question("Do you trust me?", 2)
                ],
                demands:
                [
                    new NegotiationRuntimeDemand(
                        ContentId.Parse("sample_currency"),
                        NegotiationDemandKind.Currency,
                        weight: 1,
                        currencyAmount: 25)
                ]),
            commands);

        Assert.Equal(NegotiationOutcomeKind.Success, result.Outcome);
        Assert.Equal(25, result.CurrencySpent);
        Assert.Equal(0, policy.FallbackDemandCalls);
        NegotiationDemandPrompt prompt = Assert.Single(commands.DemandPrompts);
        Assert.Equal(NegotiationDemandKind.Currency, prompt.Kind);
        Assert.Equal("Provide 25 currency?", prompt.Prompt);
        Assert.Equal(["Provide 25", "Refuse"], prompt.Options.Select(option => option.Label));
    }

    [Fact]
    public async Task NegotiationSession_RejectsUnaffordableAuthoredDemandBeforePrompting()
    {
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 0, 0]),
            new TestNegotiationPolicy());
        var commands = new QueueNegotiationCommands(
            answers: [0, 0],
            demands: [NegotiationDemandDecision.Accept]);

        NegotiationSessionResult result = await service.RunAsync(
            new NegotiationSessionRequest(
                "Pixie",
                actorLevel: 50,
                targetLevel: 9,
                actorLuck: 0,
                activeOpponentCount: 1,
                contextIds: [],
                isTargetFamiliar: false,
                hasRecruitmentCapacity: true,
                currentCurrency: 24,
                questions:
                [
                    Question("Do you like me?", 2),
                    Question("Do you trust me?", 2)
                ],
                demands:
                [
                    new NegotiationRuntimeDemand(
                        ContentId.Parse("sample_currency"),
                        NegotiationDemandKind.Currency,
                        weight: 1,
                        currencyAmount: 25)
                ]),
            commands);

        Assert.Equal(NegotiationOutcomeKind.Failure, result.Outcome);
        Assert.Equal(NegotiationOutcomeReason.InsufficientCurrency, result.Reason);
        Assert.Equal(0, result.CurrencySpent);
        Assert.Empty(commands.DemandPrompts);
        Assert.Contains(
            result.Events,
            negotiationEvent => negotiationEvent.Kind == NegotiationEventKind.Failure &&
                negotiationEvent.Code == NegotiationEventCode.InsufficientCurrency &&
                negotiationEvent.Amount == 25);
    }

    [Fact]
    public async Task NegotiationSession_FamiliarRewardsAreReturnedWithoutHostMutation()
    {
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 75]),
            new TestNegotiationPolicy(
                familiarGift: new NegotiationFamiliarGift(
                    NegotiationFamiliarGiftKind.Currency,
                    Currency: 60)));

        NegotiationSessionResult result = await service.RunAsync(
            new NegotiationSessionRequest(
                "Pixie",
                actorLevel: 50,
                targetLevel: 3,
                actorLuck: 0,
                activeOpponentCount: 1,
                contextIds: [],
                isTargetFamiliar: true,
                hasRecruitmentCapacity: true,
                currentCurrency: 0,
                familiarDialogueLines: ["We meet again."]),
            new QueueNegotiationCommands([], []));

        Assert.Equal(NegotiationOutcomeKind.FamiliarFlee, result.Outcome);
        Assert.Equal(NegotiationOutcomeReason.FamiliarTarget, result.Reason);
        Assert.Equal(NegotiationFamiliarGiftKind.Currency, result.FamiliarGift.Kind);
        Assert.Equal(60, result.FamiliarGift.Currency);
        Assert.Contains(result.Events, ev => ev.Kind == NegotiationEventKind.FamiliarDialogue);
    }

    [Fact]
    public void RecruitmentTransaction_ValidatesSessionOwnershipStockAndTarget()
    {
        var service = new RecruitmentTransactionService();
        ContentId pixie = ContentId.Parse("pixie");

        Assert.True(service.Validate(new RecruitmentTransactionRequest(
            pixie,
            AlreadyRecruitedThisBattle: false,
            AlreadyOwned: false,
            HasOpenStockSlot: true)).Applied);
        Assert.Equal(RecruitmentTransactionErrorCode.AlreadyRecruitedThisBattle, service.Validate(
            new RecruitmentTransactionRequest(pixie, true, false, true)).ErrorCode);
        Assert.Equal(RecruitmentTransactionErrorCode.AlreadyOwned, service.Validate(
            new RecruitmentTransactionRequest(pixie, false, true, true)).ErrorCode);
        Assert.Equal(RecruitmentTransactionErrorCode.StockFull, service.Validate(
            new RecruitmentTransactionRequest(pixie, false, false, false)).ErrorCode);
        Assert.Equal(RecruitmentTransactionErrorCode.InvalidTarget, service.Validate(
            new RecruitmentTransactionRequest(pixie, false, false, true, IsValidTarget: false)).ErrorCode);
    }

    [Fact]
    public void BattleRewardService_ComputesImmutableTotalsAndApplications()
    {
        var ruleset = new ProductionCombatRuleset(new SequenceRandomSource(units: [0.5m]));
        var service = new BattleRewardService(ruleset);

        BattleRewardResult result = service.Calculate(new BattleRewardRequest(
            enemies:
            [
                new BattleRewardEnemySnapshot(
                    ContentId.Parse("pixie"),
                    Level: 10,
                    Strength: 20,
                    Magic: 20,
                    Vitality: 20,
                    Agility: 20,
                    Luck: 20)
            ],
            recipients:
            [
                new BattleRewardRecipientSnapshot(ContentId.Parse("hero"), IsAlive: true, HasActiveForm: true),
                new BattleRewardRecipientSnapshot(ContentId.Parse("fallen"), IsAlive: false, HasActiveForm: true)
            ]));

        Assert.Equal(46, result.TotalExperience);
        Assert.Equal(125, result.TotalCurrency);
        Assert.Equal(
            [
                new BattleRewardApplication(ContentId.Parse("hero"), BattleRewardRecipientKind.Actor, 46),
                new BattleRewardApplication(ContentId.Parse("hero"), BattleRewardRecipientKind.ActiveForm, 46)
            ],
            result.Applications);
        Assert.Throws<NotSupportedException>(() => ((IList<BattleRewardApplication>)result.Applications).Add(
            new BattleRewardApplication(ContentId.Parse("x"), BattleRewardRecipientKind.Actor, 1)));
    }

    private static NegotiationQuestionPrompt Question(string text, int score) =>
        new(text, [new NegotiationAnswerOption("Yes", score)]);

    private sealed class QueueNegotiationCommands : INegotiationCommandSource
    {
        private readonly Queue<int> _answers;
        private readonly Queue<NegotiationDemandDecision> _demands;
        private readonly List<NegotiationDemandPrompt> _demandPrompts = [];

        public QueueNegotiationCommands(
            IEnumerable<int> answers,
            IEnumerable<NegotiationDemandDecision> demands)
        {
            _answers = new Queue<int>(answers);
            _demands = new Queue<NegotiationDemandDecision>(demands);
        }

        public IReadOnlyList<NegotiationDemandPrompt> DemandPrompts => _demandPrompts;

        public ValueTask<NegotiationAnswerSelection> ReadAnswerAsync(
            NegotiationQuestionPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NegotiationAnswerSelection.Selected(_answers.Dequeue()));
        }

        public ValueTask<NegotiationDemandSelection> ReadDemandAsync(
            NegotiationDemandPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _demandPrompts.Add(prompt);
            return ValueTask.FromResult(NegotiationDemandSelection.Selected(_demands.Dequeue()));
        }
    }

    private sealed class RecordingEventSink<TEvent> : IHostEventSink<TEvent>
    {
        private readonly List<TEvent> _events = [];

        public IReadOnlyList<TEvent> Events => _events;

        public ValueTask PublishAsync(TEvent hostEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add(hostEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestNegotiationPolicy : INegotiationSessionPolicy
    {
        private readonly IReadOnlyList<NegotiationRuntimeDemand> _fallbackDemands;
        private readonly NegotiationFamiliarGift _familiarGift;

        public TestNegotiationPolicy(
            IEnumerable<NegotiationRuntimeDemand>? fallbackDemands = null,
            NegotiationFamiliarGift? familiarGift = null)
        {
            _fallbackDemands = Array.AsReadOnly((fallbackDemands ?? []).ToArray());
            _familiarGift = familiarGift ?? NegotiationFamiliarGift.None;
        }

        public int QuestionLimit => 3;
        public int PositiveMoodThreshold => 4;
        public int NeutralMoodThreshold => 1;
        public int FallbackDemandCalls { get; private set; }

        public NegotiationGateDecision EvaluateGate(NegotiationSessionRequest request) => new(true);

        public bool CanBegin(NegotiationSessionRequest request, IRandomSource random) => true;

        public NegotiationFamiliarGift SelectFamiliarGift(
            NegotiationSessionRequest request,
            IRandomSource random) => _familiarGift;

        public IReadOnlyList<NegotiationRuntimeDemand> CreateFallbackDemands(
            NegotiationSessionRequest request,
            IRandomSource random)
        {
            FallbackDemandCalls++;
            return _fallbackDemands;
        }

        public bool ResolveDemandlessSuccess(NegotiationSessionRequest request, IRandomSource random) => true;
    }

    private sealed class SequenceRandomSource : IRandomSource
    {
        private readonly Queue<int> _ints;
        private readonly Queue<decimal> _units;

        public SequenceRandomSource(IEnumerable<int>? ints = null, IEnumerable<decimal>? units = null)
        {
            _ints = new Queue<int>(ints ?? []);
            _units = new Queue<decimal>(units ?? []);
        }

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            if (_ints.Count == 0)
            {
                return minimumInclusive;
            }

            int value = _ints.Dequeue();
            Assert.InRange(value, minimumInclusive, maximumExclusive - 1);
            return value;
        }

        public decimal NextUnitDecimal() => _units.Count == 0 ? 0.5m : _units.Dequeue();
    }
}
