using Convergence.Content;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Encounters;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

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
                "Glow Wisp",
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
    public async Task NegotiationSession_UsesAuthoredDemandInsteadOfCalculatedCurrencyFormula()
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
                "Glow Wisp",
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
                "Glow Wisp",
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
                "Glow Wisp",
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
    public async Task NegotiationSession_AnswerMenuCancellationReturnsCancelledOutcome()
    {
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0]),
            new TestNegotiationPolicy());
        var commands = new CancellationNegotiationCommands(cancelAnswer: true);
        var events = new RecordingEventSink<NegotiationEvent>();

        NegotiationSessionResult result = await service.RunAsync(
            PositiveNegotiationRequest(CreateDemand(NegotiationDemandKind.Currency)),
            commands,
            events);

        Assert.Equal(NegotiationOutcomeKind.Cancelled, result.Outcome);
        Assert.Equal(NegotiationOutcomeReason.Cancelled, result.Reason);
        Assert.Equal(0, result.CurrencySpent);
        Assert.Null(result.ItemSpentId);
        Assert.Equal(1, commands.AnswerCalls);
        Assert.Equal(0, commands.DemandCalls);
        NegotiationEvent cancellation = Assert.Single(
            result.Events,
            negotiationEvent => negotiationEvent.Code == NegotiationEventCode.Cancelled);
        Assert.Equal(NegotiationEventKind.Information, cancellation.Kind);
        Assert.Equal(result.Events, events.Events);
    }

    [Theory]
    [InlineData(NegotiationDemandKind.Currency)]
    [InlineData(NegotiationDemandKind.Item)]
    public async Task NegotiationSession_DemandMenuCancellationReturnsCancelledWithoutStagedMutation(
        NegotiationDemandKind demandKind)
    {
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 0, 0]),
            new TestNegotiationPolicy());
        var commands = new CancellationNegotiationCommands(cancelDemand: true);

        NegotiationSessionResult result = await service.RunAsync(
            PositiveNegotiationRequest(CreateDemand(demandKind)),
            commands);

        Assert.Equal(NegotiationOutcomeKind.Cancelled, result.Outcome);
        Assert.Equal(NegotiationOutcomeReason.Cancelled, result.Reason);
        Assert.Equal(0, result.CurrencySpent);
        Assert.Null(result.ItemSpentId);
        Assert.Equal(2, commands.AnswerCalls);
        Assert.Equal(1, commands.DemandCalls);
        NegotiationEvent cancellation = Assert.Single(
            result.Events,
            negotiationEvent => negotiationEvent.Code == NegotiationEventCode.Cancelled);
        Assert.Equal(NegotiationEventKind.Information, cancellation.Kind);
    }

    [Fact]
    public async Task NegotiationSession_LaterDemandCancellationClearsEarlierStagedConcessions()
    {
        var policy = new TestNegotiationPolicy(
            fallbackDemands:
            [
                CreateDemand(NegotiationDemandKind.Currency),
                CreateDemand(NegotiationDemandKind.Item)
            ]);
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 0]),
            policy);
        var commands = new CancellationNegotiationCommands(
            cancelDemand: true,
            cancelDemandOnCall: 2);

        NegotiationSessionResult result = await service.RunAsync(
            PositiveNegotiationRequestWithoutAuthoredDemands(),
            commands);

        Assert.Equal(NegotiationOutcomeKind.Cancelled, result.Outcome);
        Assert.Equal(NegotiationOutcomeReason.Cancelled, result.Reason);
        Assert.Equal(0, result.CurrencySpent);
        Assert.Null(result.ItemSpentId);
        Assert.Equal(2, commands.DemandCalls);
    }

    [Theory]
    [InlineData(NegotiationDemandKind.Currency, NegotiationOutcomeReason.CurrencyRefused)]
    [InlineData(NegotiationDemandKind.Item, NegotiationOutcomeReason.ItemRefused)]
    public async Task NegotiationSession_ExplicitDemandRefusalRemainsGameplayFailure(
        NegotiationDemandKind demandKind,
        NegotiationOutcomeReason expectedReason)
    {
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 0, 0]),
            new TestNegotiationPolicy());
        var commands = new QueueNegotiationCommands(
            answers: [0, 0],
            demands: [NegotiationDemandDecision.Refuse]);

        NegotiationSessionResult result = await service.RunAsync(
            PositiveNegotiationRequest(CreateDemand(demandKind)),
            commands);

        Assert.Equal(NegotiationOutcomeKind.Failure, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.DoesNotContain(
            result.Events,
            negotiationEvent => negotiationEvent.Code == NegotiationEventCode.Cancelled);
    }

    [Fact]
    public async Task NegotiationSession_PreCancelledTokenStopsBeforePolicyEvaluation()
    {
        var policy = new TestNegotiationPolicy();
        var service = new NegotiationSessionService(new SequenceRandomSource(), policy);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.RunAsync(
                PositiveNegotiationRequest(CreateDemand(NegotiationDemandKind.Currency)),
                new CancellationNegotiationCommands(),
                cancellationToken: cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, policy.GateCalls);
        Assert.Equal(0, policy.CanBeginCalls);
    }

    [Fact]
    public async Task NegotiationSession_TokenCancellationDuringAnswerSelectionThrows()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0]),
            new TestNegotiationPolicy());
        var commands = new CancellationNegotiationCommands(
            cancelAnswer: true,
            cancellationSource: cancellation);
        var events = new RecordingEventSink<NegotiationEvent>();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.RunAsync(
                PositiveNegotiationRequest(CreateDemand(NegotiationDemandKind.Currency)),
                commands,
                events,
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, commands.AnswerCalls);
        Assert.Empty(events.Events);
    }

    [Theory]
    [InlineData(NegotiationDemandKind.Currency)]
    [InlineData(NegotiationDemandKind.Item)]
    public async Task NegotiationSession_TokenCancellationDuringDemandSelectionThrows(
        NegotiationDemandKind demandKind)
    {
        using var cancellation = new CancellationTokenSource();
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 0, 0]),
            new TestNegotiationPolicy());
        var commands = new CancellationNegotiationCommands(
            cancelDemand: true,
            cancellationSource: cancellation);
        var events = new RecordingEventSink<NegotiationEvent>();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.RunAsync(
                PositiveNegotiationRequest(CreateDemand(demandKind)),
                commands,
                events,
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(2, commands.AnswerCalls);
        Assert.Equal(1, commands.DemandCalls);
        Assert.DoesNotContain(
            events.Events,
            negotiationEvent => negotiationEvent.Code == NegotiationEventCode.Cancelled);
    }

    [Fact]
    public async Task NegotiationSession_TokenCancellationDuringEventPublicationStopsBeforeNextPolicyCall()
    {
        using var cancellation = new CancellationTokenSource();
        var policy = new TestNegotiationPolicy();
        var service = new NegotiationSessionService(new SequenceRandomSource(), policy);
        var events = new CancellingEventSink<NegotiationEvent>(cancellation);

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.RunAsync(
                new NegotiationSessionRequest(
                    "Familiar Target",
                    actorLevel: 1,
                    targetLevel: 1,
                    actorLuck: 0,
                    activeOpponentCount: 1,
                    contextIds: [],
                    isTargetFamiliar: true,
                    hasRecruitmentCapacity: true,
                    currentCurrency: 0),
                new CancellationNegotiationCommands(),
                events,
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Single(events.Events);
        Assert.Equal(0, policy.FamiliarGiftCalls);
    }

    [Fact]
    public void RecruitmentTransaction_ValidatesSessionOwnershipStockAndTarget()
    {
        var service = new RecruitmentTransactionService();
        ContentId glowWisp = ContentId.Parse("glow_wisp");

        Assert.True(service.Validate(new RecruitmentTransactionRequest(
            glowWisp,
            AlreadyRecruitedThisBattle: false,
            AlreadyOwned: false,
            HasOpenRosterSlot: true)).Applied);
        Assert.Equal(RecruitmentTransactionErrorCode.AlreadyRecruitedThisBattle, service.Validate(
            new RecruitmentTransactionRequest(glowWisp, true, false, true)).ErrorCode);
        Assert.Equal(RecruitmentTransactionErrorCode.AlreadyOwned, service.Validate(
            new RecruitmentTransactionRequest(glowWisp, false, true, true)).ErrorCode);
        Assert.Equal(RecruitmentTransactionErrorCode.RosterFull, service.Validate(
            new RecruitmentTransactionRequest(glowWisp, false, false, false)).ErrorCode);
        Assert.Equal(RecruitmentTransactionErrorCode.InvalidTarget, service.Validate(
            new RecruitmentTransactionRequest(glowWisp, false, false, true, IsValidTarget: false)).ErrorCode);
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
                    ContentId.Parse("glow_wisp"),
                    Level: 10,
                    Strength: 20,
                    Magic: 20,
                    Vitality: 20,
                    Agility: 20,
                    Luck: 20)
            ],
            recipients:
            [
                new BattleRewardRecipientSnapshot(ContentId.Parse("hero"), IsAlive: true, HasActiveHostedEntity: true),
                new BattleRewardRecipientSnapshot(ContentId.Parse("fallen"), IsAlive: false, HasActiveHostedEntity: true)
            ]));

        Assert.Equal(46, result.TotalExperience);
        Assert.Equal(125, result.TotalCurrency);
        Assert.Equal(
            [
                new BattleRewardApplication(ContentId.Parse("hero"), BattleRewardRecipientKind.Actor, 46),
                new BattleRewardApplication(ContentId.Parse("hero"), BattleRewardRecipientKind.ActiveHostedEntity, 46)
            ],
            result.Applications);
        Assert.Throws<NotSupportedException>(() => ((IList<BattleRewardApplication>)result.Applications).Add(
            new BattleRewardApplication(ContentId.Parse("x"), BattleRewardRecipientKind.Actor, 1)));
    }

    [Fact]
    public async Task NegotiationSession_SaturatesMoodAndAcceptsMaximumDemandWeightTotal()
    {
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 0, int.MaxValue - 1]),
            new TestNegotiationPolicy());
        var commands = new QueueNegotiationCommands(
            answers: [0, 0],
            demands: [NegotiationDemandDecision.Accept]);

        NegotiationSessionResult result = await service.RunAsync(
            new NegotiationSessionRequest(
                "Boundary Target",
                actorLevel: 1,
                targetLevel: 1,
                actorLuck: 0,
                activeOpponentCount: 1,
                contextIds: [],
                isTargetFamiliar: false,
                hasRecruitmentCapacity: true,
                currentCurrency: 1,
                questions:
                [
                    Question("Maximum adjustment?", int.MaxValue),
                    Question("One more?", 1)
                ],
                demands:
                [
                    new NegotiationRuntimeDemand(
                        ContentId.Parse("maximum_weight"),
                        NegotiationDemandKind.Currency,
                        int.MaxValue,
                        currencyAmount: 1)
                ]),
            commands);

        Assert.Equal(NegotiationOutcomeKind.Success, result.Outcome);
        Assert.Equal(NegotiationNumericDomain.MaximumMoodScore, result.MoodScore);
        Assert.Equal(1, result.CurrencySpent);
        Assert.Single(commands.DemandPrompts);
    }

    [Fact]
    public async Task NegotiationSession_SaturatesNegativeMoodWithoutWrappingPositive()
    {
        var service = new NegotiationSessionService(
            new SequenceRandomSource(ints: [0, 0]),
            new TestNegotiationPolicy());

        NegotiationSessionResult result = await service.RunAsync(
            new NegotiationSessionRequest(
                "Boundary Target",
                actorLevel: 1,
                targetLevel: 1,
                actorLuck: 0,
                activeOpponentCount: 1,
                contextIds: [],
                isTargetFamiliar: false,
                hasRecruitmentCapacity: true,
                currentCurrency: 0,
                questions:
                [
                    Question("Minimum adjustment?", int.MinValue),
                    Question("One less?", -1)
                ]),
            new QueueNegotiationCommands(answers: [0, 0], demands: []));

        Assert.Equal(NegotiationOutcomeKind.Failure, result.Outcome);
        Assert.Equal(NegotiationOutcomeReason.MoodFailure, result.Reason);
        Assert.Equal(NegotiationNumericDomain.MinimumMoodScore, result.MoodScore);
    }

    [Fact]
    public void NegotiationSessionRequest_RejectsDemandWeightAggregateBeyondSupportedDomain()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NegotiationSessionRequest(
                "Boundary Target",
                actorLevel: 1,
                targetLevel: 1,
                actorLuck: 0,
                activeOpponentCount: 1,
                contextIds: [],
                isTargetFamiliar: false,
                hasRecruitmentCapacity: true,
                currentCurrency: 0,
                demands:
                [
                    new NegotiationRuntimeDemand(
                        ContentId.Parse("maximum_weight"),
                        NegotiationDemandKind.Currency,
                        int.MaxValue,
                        currencyAmount: 1),
                    new NegotiationRuntimeDemand(
                        ContentId.Parse("overflow_weight"),
                        NegotiationDemandKind.Currency,
                        1,
                        currencyAmount: 1)
                ]));

        Assert.Equal("demands", exception.ParamName);
        Assert.Contains(int.MaxValue.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BattleRewardService_SaturatesMultiEnemyTotalsAndApplications()
    {
        var service = new BattleRewardService(new ProductionCombatRuleset(
            new SequenceRandomSource(units: [0.5m, 0.5m])));
        BattleRewardEnemySnapshot maximumEnemy = new(
            ContentId.Parse("maximum_enemy"),
            int.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue,
            decimal.MaxValue);

        BattleRewardResult result = service.Calculate(new BattleRewardRequest(
            enemies: [maximumEnemy, maximumEnemy with { EnemyId = ContentId.Parse("second_enemy") }],
            recipients:
            [
                new BattleRewardRecipientSnapshot(
                    ContentId.Parse("hero"),
                    IsAlive: true,
                    HasActiveHostedEntity: false)
            ]));

        Assert.Equal(int.MaxValue, result.TotalExperience);
        Assert.Equal(int.MaxValue, result.TotalCurrency);
        Assert.Equal(int.MaxValue, Assert.Single(result.Applications).Experience);
    }

    private static NegotiationQuestionPrompt Question(string text, int score) =>
        new(text, [new NegotiationAnswerOption("Yes", score)]);

    private static NegotiationSessionRequest PositiveNegotiationRequest(
        NegotiationRuntimeDemand demand) =>
        new(
            "Glow Wisp",
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
            demands: [demand]);

    private static NegotiationSessionRequest PositiveNegotiationRequestWithoutAuthoredDemands() =>
        new(
            "Glow Wisp",
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
            ]);

    private static NegotiationRuntimeDemand CreateDemand(NegotiationDemandKind kind) => kind switch
    {
        NegotiationDemandKind.Currency => new NegotiationRuntimeDemand(
            ContentId.Parse("sample_currency"),
            kind,
            weight: 1,
            currencyAmount: 25),
        NegotiationDemandKind.Item => new NegotiationRuntimeDemand(
            ContentId.Parse("sample_item"),
            kind,
            weight: 1,
            item: new NegotiationAvailableItem("sample_item", "Sample Item")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported test demand kind.")
    };

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

    private sealed class CancellationNegotiationCommands(
        bool cancelAnswer = false,
        bool cancelDemand = false,
        int cancelDemandOnCall = 1,
        CancellationTokenSource? cancellationSource = null) : INegotiationCommandSource
    {
        public int AnswerCalls { get; private set; }
        public int DemandCalls { get; private set; }

        public ValueTask<NegotiationAnswerSelection> ReadAnswerAsync(
            NegotiationQuestionPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AnswerCalls++;
            if (!cancelAnswer)
            {
                return ValueTask.FromResult(NegotiationAnswerSelection.Selected(0));
            }

            cancellationSource?.Cancel();
            return ValueTask.FromResult(NegotiationAnswerSelection.Cancel());
        }

        public ValueTask<NegotiationDemandSelection> ReadDemandAsync(
            NegotiationDemandPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DemandCalls++;
            if (!cancelDemand || DemandCalls != cancelDemandOnCall)
            {
                return ValueTask.FromResult(
                    NegotiationDemandSelection.Selected(NegotiationDemandDecision.Accept));
            }

            cancellationSource?.Cancel();
            return ValueTask.FromResult(NegotiationDemandSelection.Cancel());
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

    private sealed class CancellingEventSink<TEvent>(CancellationTokenSource cancellationSource)
        : IHostEventSink<TEvent>
    {
        private readonly List<TEvent> _events = [];

        public IReadOnlyList<TEvent> Events => _events;

        public ValueTask PublishAsync(TEvent hostEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add(hostEvent);
            cancellationSource.Cancel();
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
        public int GateCalls { get; private set; }
        public int CanBeginCalls { get; private set; }
        public int FamiliarGiftCalls { get; private set; }
        public int FallbackDemandCalls { get; private set; }

        public NegotiationGateDecision EvaluateGate(NegotiationSessionRequest request)
        {
            GateCalls++;
            return new NegotiationGateDecision(true);
        }

        public bool CanBegin(NegotiationSessionRequest request, IRandomSource random)
        {
            CanBeginCalls++;
            return true;
        }

        public NegotiationFamiliarGift SelectFamiliarGift(
            NegotiationSessionRequest request,
            IRandomSource random)
        {
            FamiliarGiftCalls++;
            return _familiarGift;
        }

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
