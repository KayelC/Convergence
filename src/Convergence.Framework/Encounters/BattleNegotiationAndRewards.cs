using Convergence.Content;
using Convergence.Hosting;
using Convergence.Battle;

namespace Convergence.Encounters;

public enum NegotiationOutcomeKind
{
    Success,
    Failure,
    Trick,
    Flee,
    FamiliarFlee,
    Cancelled
}

public enum NegotiationOutcomeReason
{
    None,
    PolicyBlocked,
    FamiliarTarget,
    CapacityUnavailable,
    GuardRefusal,
    MissingQuestions,
    Cancelled,
    MoodFailure,
    MoodFlee,
    TargetLevelTooHigh,
    InsufficientCurrency,
    CurrencyRefused,
    ItemRefused,
    Trick
}

public enum NegotiationEventKind
{
    Information,
    Warning,
    Failure,
    FamiliarDialogue,
    DemandIntro,
    MoodPositive,
    MoodNeutral,
    MoodNegative
}

public enum NegotiationEventCode
{
    Generic,
    PolicyBlocked,
    FamiliarDialogue,
    FamiliarGift,
    CapacityUnavailable,
    OpeningRefused,
    MissingQuestions,
    Cancelled,
    MoodPositive,
    MoodNeutral,
    MoodNegative,
    TargetLevelTooHigh,
    DemandIntro,
    InsufficientCurrency,
    DemandlessRejected
}

public sealed record NegotiationEvent(
    NegotiationEventKind Kind,
    NegotiationEventCode Code,
    string Message,
    NegotiationFamiliarGift? FamiliarGift = null,
    int Amount = 0);

public sealed record NegotiationAnswerOption(string Text, int Score);

public sealed record NegotiationQuestionPrompt
{
    public NegotiationQuestionPrompt(string text, IEnumerable<NegotiationAnswerOption> answers)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Answers = Array.AsReadOnly((answers ?? throw new ArgumentNullException(nameof(answers))).ToArray());
    }

    public string Text { get; }
    public IReadOnlyList<NegotiationAnswerOption> Answers { get; }
}

public sealed record NegotiationAvailableItem(string ItemId, string DisplayName);

public enum NegotiationDemandKind
{
    Currency,
    Item
}

public enum NegotiationDemandDecision
{
    Accept,
    Refuse
}

public sealed record NegotiationDemandOption(NegotiationDemandDecision Decision, string Label);

public sealed record NegotiationDemandPrompt
{
    public NegotiationDemandPrompt(
        NegotiationRuntimeDemand demand,
        string prompt,
        IEnumerable<NegotiationDemandOption> options)
    {
        Demand = demand ?? throw new ArgumentNullException(nameof(demand));
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        Options = Array.AsReadOnly((options ?? throw new ArgumentNullException(nameof(options))).ToArray());
    }

    public NegotiationRuntimeDemand Demand { get; }
    public NegotiationDemandKind Kind => Demand.Kind;
    public string Prompt { get; }
    public IReadOnlyList<NegotiationDemandOption> Options { get; }
}

public sealed record NegotiationRuntimeDemand
{
    public NegotiationRuntimeDemand(
        ContentId demandId,
        NegotiationDemandKind kind,
        int weight,
        int? currencyAmount = null,
        NegotiationAvailableItem? item = null)
    {
        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Negotiation demand weight must be positive.");
        }
        if (kind == NegotiationDemandKind.Currency && currencyAmount is not > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currencyAmount), "Currency demands require a positive amount.");
        }
        if (kind == NegotiationDemandKind.Item && item is null)
        {
            throw new ArgumentNullException(nameof(item), "Item demands require an item.");
        }

        DemandId = demandId;
        Kind = kind;
        Weight = weight;
        CurrencyAmount = currencyAmount;
        Item = item;
    }

    public ContentId DemandId { get; }
    public NegotiationDemandKind Kind { get; }
    public int Weight { get; }
    public int? CurrencyAmount { get; }
    public NegotiationAvailableItem? Item { get; }
}

public sealed record NegotiationAnswerSelection
{
    private NegotiationAnswerSelection(bool cancelled, int selectedIndex)
    {
        Cancelled = cancelled;
        SelectedIndex = selectedIndex;
    }

    public bool Cancelled { get; }
    public int SelectedIndex { get; }

    public static NegotiationAnswerSelection Selected(int selectedIndex) => new(false, selectedIndex);
    public static NegotiationAnswerSelection Cancel() => new(true, -1);
}

public sealed record NegotiationDemandSelection
{
    private NegotiationDemandSelection(bool cancelled, NegotiationDemandDecision decision)
    {
        Cancelled = cancelled;
        Decision = decision;
    }

    public bool Cancelled { get; }
    public NegotiationDemandDecision Decision { get; }

    public static NegotiationDemandSelection Selected(NegotiationDemandDecision decision) => new(false, decision);
    public static NegotiationDemandSelection Cancel() => new(true, NegotiationDemandDecision.Refuse);
}

public interface INegotiationCommandSource
{
    ValueTask<NegotiationAnswerSelection> ReadAnswerAsync(
        NegotiationQuestionPrompt prompt,
        CancellationToken cancellationToken = default);

    ValueTask<NegotiationDemandSelection> ReadDemandAsync(
        NegotiationDemandPrompt prompt,
        CancellationToken cancellationToken = default);
}

public enum NegotiationFamiliarGiftKind
{
    None,
    Item,
    Currency,
    RestoreParty
}

public sealed record NegotiationFamiliarGift(
    NegotiationFamiliarGiftKind Kind,
    string? ItemId = null,
    int Quantity = 0,
    int Currency = 0,
    decimal RestorePercent = 0m)
{
    public static NegotiationFamiliarGift None { get; } = new(NegotiationFamiliarGiftKind.None);
}

public sealed record NegotiationSessionRequest
{
    public NegotiationSessionRequest(
        string targetName,
        int actorLevel,
        int targetLevel,
        int actorLuck,
        int activeOpponentCount,
        IEnumerable<ContentId>? contextIds,
        bool isTargetFamiliar,
        bool hasRecruitmentCapacity,
        int currentCurrency,
        IEnumerable<NegotiationQuestionPrompt>? questions = null,
        IEnumerable<string>? familiarDialogueLines = null,
        string? specificFamiliarDialogue = null,
        IEnumerable<NegotiationAvailableItem>? availableHealingItems = null,
        IEnumerable<NegotiationRuntimeDemand>? demands = null)
    {
        if (actorLevel <= 0) throw new ArgumentOutOfRangeException(nameof(actorLevel));
        if (targetLevel <= 0) throw new ArgumentOutOfRangeException(nameof(targetLevel));
        if (activeOpponentCount <= 0) throw new ArgumentOutOfRangeException(nameof(activeOpponentCount));
        if (currentCurrency < 0) throw new ArgumentOutOfRangeException(nameof(currentCurrency));

        TargetName = string.IsNullOrWhiteSpace(targetName) ? "Target" : targetName;
        ActorLevel = actorLevel;
        TargetLevel = targetLevel;
        ActorLuck = actorLuck;
        ActiveOpponentCount = activeOpponentCount;
        ContextIds = Array.AsReadOnly((contextIds ?? []).Distinct().ToArray());
        IsTargetFamiliar = isTargetFamiliar;
        HasRecruitmentCapacity = hasRecruitmentCapacity;
        CurrentCurrency = currentCurrency;
        Questions = Array.AsReadOnly((questions ?? []).ToArray());
        FamiliarDialogueLines = Array.AsReadOnly((familiarDialogueLines ?? []).ToArray());
        SpecificFamiliarDialogue = specificFamiliarDialogue;
        AvailableHealingItems = Array.AsReadOnly((availableHealingItems ?? []).ToArray());
        NegotiationRuntimeDemand[] demandSnapshot = (demands ?? []).ToArray();
        if (!NegotiationNumericDomain.TrySumDemandWeights(
                demandSnapshot.Select(demand => demand.Weight),
                out _))
        {
            throw new ArgumentOutOfRangeException(
                nameof(demands),
                $"The aggregate authored demand weight must be positive and no greater than " +
                $"{NegotiationNumericDomain.MaximumDemandWeightTotal}.");
        }

        Demands = Array.AsReadOnly(demandSnapshot);
    }

    public string TargetName { get; }
    public int ActorLevel { get; }
    public int TargetLevel { get; }
    public int ActorLuck { get; }
    public int ActiveOpponentCount { get; }
    public IReadOnlyList<ContentId> ContextIds { get; }
    public bool IsTargetFamiliar { get; }
    public bool HasRecruitmentCapacity { get; }
    public int CurrentCurrency { get; }
    public IReadOnlyList<NegotiationQuestionPrompt> Questions { get; }
    public IReadOnlyList<string> FamiliarDialogueLines { get; }
    public string? SpecificFamiliarDialogue { get; }
    public IReadOnlyList<NegotiationAvailableItem> AvailableHealingItems { get; }
    public IReadOnlyList<NegotiationRuntimeDemand> Demands { get; }
}

public sealed record NegotiationSessionResult
{
    public NegotiationSessionResult(
        NegotiationOutcomeKind outcome,
        NegotiationOutcomeReason reason = NegotiationOutcomeReason.None,
        int moodScore = 0,
        int currencySpent = 0,
        string? itemSpentId = null,
        NegotiationFamiliarGift? familiarGift = null,
        IEnumerable<NegotiationEvent>? events = null)
    {
        Outcome = outcome;
        Reason = reason;
        MoodScore = moodScore;
        CurrencySpent = currencySpent;
        ItemSpentId = itemSpentId;
        FamiliarGift = familiarGift ?? NegotiationFamiliarGift.None;
        Events = Array.AsReadOnly((events ?? []).ToArray());
    }

    public NegotiationOutcomeKind Outcome { get; }
    public NegotiationOutcomeReason Reason { get; }
    public int MoodScore { get; }
    public int CurrencySpent { get; }
    public string? ItemSpentId { get; }
    public NegotiationFamiliarGift FamiliarGift { get; }
    public IReadOnlyList<NegotiationEvent> Events { get; }
}

public interface INegotiationSessionService
{
    ValueTask<NegotiationSessionResult> RunAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        IHostEventSink<NegotiationEvent>? events = null,
        CancellationToken cancellationToken = default);
}

public sealed record NegotiationGateDecision(
    bool IsAllowed,
    NegotiationOutcomeReason RejectionReason = NegotiationOutcomeReason.PolicyBlocked);

public interface INegotiationSessionPolicy
{
    int QuestionLimit { get; }
    int PositiveMoodThreshold { get; }
    int NeutralMoodThreshold { get; }

    NegotiationGateDecision EvaluateGate(NegotiationSessionRequest request);
    bool CanBegin(NegotiationSessionRequest request, IRandomSource random);
    NegotiationFamiliarGift SelectFamiliarGift(NegotiationSessionRequest request, IRandomSource random);
    IReadOnlyList<NegotiationRuntimeDemand> CreateFallbackDemands(
        NegotiationSessionRequest request,
        IRandomSource random);
    bool ResolveDemandlessSuccess(NegotiationSessionRequest request, IRandomSource random);
}

public sealed class NegotiationSessionService : INegotiationSessionService
{
    private readonly IRandomSource _random;
    private readonly INegotiationSessionPolicy _policy;

    public NegotiationSessionService(IRandomSource random, INegotiationSessionPolicy policy)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (_policy.QuestionLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Negotiation question limit must be positive.");
        }
        if (_policy.PositiveMoodThreshold <= _policy.NeutralMoodThreshold)
        {
            throw new ArgumentException("Positive mood threshold must exceed the neutral mood threshold.", nameof(policy));
        }
    }

    public async ValueTask<NegotiationSessionResult> RunAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        IHostEventSink<NegotiationEvent>? events = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(commands);
        cancellationToken.ThrowIfCancellationRequested();

        var emitted = new List<NegotiationEvent>();
        async ValueTask EmitAsync(NegotiationEvent negotiationEvent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            emitted.Add(negotiationEvent);
            cancellationToken.ThrowIfCancellationRequested();
            if (events is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await events.PublishAsync(negotiationEvent, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        NegotiationGateDecision gate = _policy.EvaluateGate(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!gate.IsAllowed)
        {
            await EmitAsync(new NegotiationEvent(
                NegotiationEventKind.Failure,
                NegotiationEventCode.PolicyBlocked,
                "Negotiation is unavailable under the active host policy."));
            return Result(NegotiationOutcomeKind.Failure, gate.RejectionReason);
        }

        if (request.IsTargetFamiliar)
        {
            return await ResolveFamiliarAsync(request, emitted, EmitAsync, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!request.HasRecruitmentCapacity)
        {
            await EmitAsync(new NegotiationEvent(
                NegotiationEventKind.Failure,
                NegotiationEventCode.CapacityUnavailable,
                "Recruitment capacity is unavailable."));
            return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.CapacityUnavailable);
        }

        cancellationToken.ThrowIfCancellationRequested();
        bool canBegin = _policy.CanBegin(request, _random);
        cancellationToken.ThrowIfCancellationRequested();
        if (!canBegin)
        {
            await EmitAsync(new NegotiationEvent(
                NegotiationEventKind.Failure,
                NegotiationEventCode.OpeningRefused,
                "The target refused to begin negotiations."));
            return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.GuardRefusal);
        }

        if (request.Questions.Count == 0)
        {
            await EmitAsync(new NegotiationEvent(
                NegotiationEventKind.Failure,
                NegotiationEventCode.MissingQuestions,
                "No negotiation questions are available."));
            return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.MissingQuestions);
        }

        int moodScore = 0;
        var questions = request.Questions.ToList();
        for (int i = 0; i < _policy.QuestionLimit && questions.Count > 0; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int questionIndex = _random.NextInt32(0, questions.Count);
            cancellationToken.ThrowIfCancellationRequested();
            NegotiationQuestionPrompt question = questions[questionIndex];
            questions.RemoveAt(questionIndex);

            NegotiationAnswerSelection answer = await commands.ReadAnswerAsync(question, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (answer.Cancelled)
            {
                await EmitAsync(CreateCancellationEvent());
                return Result(NegotiationOutcomeKind.Cancelled, NegotiationOutcomeReason.Cancelled, moodScore);
            }

            if (answer.SelectedIndex < 0 || answer.SelectedIndex >= question.Answers.Count)
            {
                throw new InvalidOperationException("Negotiation answer selection was outside the prompt options.");
            }

            moodScore = NegotiationNumericDomain.AddMoodScore(
                moodScore,
                question.Answers[answer.SelectedIndex].Score);
        }

        if (moodScore >= _policy.PositiveMoodThreshold)
        {
            await EmitAsync(new NegotiationEvent(
                NegotiationEventKind.MoodPositive,
                NegotiationEventCode.MoodPositive,
                "The target responded positively."));
            return await ResolveDemandsAsync(request, commands, emitted, EmitAsync, moodScore, cancellationToken)
                .ConfigureAwait(false);
        }

        if (moodScore >= _policy.NeutralMoodThreshold)
        {
            await EmitAsync(new NegotiationEvent(
                NegotiationEventKind.MoodNeutral,
                NegotiationEventCode.MoodNeutral,
                "The target ended the exchange without joining."));
            return Result(NegotiationOutcomeKind.Flee, NegotiationOutcomeReason.MoodFlee, moodScore);
        }

        await EmitAsync(new NegotiationEvent(
            NegotiationEventKind.MoodNegative,
            NegotiationEventCode.MoodNegative,
            "The target responded negatively."));
        return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.MoodFailure, moodScore);

        NegotiationSessionResult Result(
            NegotiationOutcomeKind outcome,
            NegotiationOutcomeReason reason,
            int score = 0,
            int currencySpent = 0,
            string? itemSpent = null,
            NegotiationFamiliarGift? gift = null) =>
            new(outcome, reason, score, currencySpent, itemSpent, gift, emitted);
    }

    private async ValueTask<NegotiationSessionResult> ResolveFamiliarAsync(
        NegotiationSessionRequest request,
        List<NegotiationEvent> emitted,
        Func<NegotiationEvent, ValueTask> emit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string dialogue = $"{request.TargetName} looks at you with a sense of familiarity...";
        if (!string.IsNullOrWhiteSpace(request.SpecificFamiliarDialogue))
        {
            dialogue = $"{request.TargetName}: \"{request.SpecificFamiliarDialogue}\"";
        }
        else if (request.FamiliarDialogueLines.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            dialogue = $"{request.TargetName}: \"{request.FamiliarDialogueLines[_random.NextInt32(0, request.FamiliarDialogueLines.Count)]}\"";
            cancellationToken.ThrowIfCancellationRequested();
        }

        await emit(new NegotiationEvent(
            NegotiationEventKind.FamiliarDialogue,
            NegotiationEventCode.FamiliarDialogue,
            dialogue)).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        NegotiationFamiliarGift gift = _policy.SelectFamiliarGift(request, _random);
        cancellationToken.ThrowIfCancellationRequested();
        string giftMessage = gift.Kind switch
        {
            NegotiationFamiliarGiftKind.Item => "The familiar target provided an item and departed.",
            NegotiationFamiliarGiftKind.Currency => "The familiar target provided currency and departed.",
            NegotiationFamiliarGiftKind.RestoreParty => "The familiar target restored the party and departed.",
            _ => "The familiar target departed."
        };
        await emit(new NegotiationEvent(
            NegotiationEventKind.Information,
            NegotiationEventCode.FamiliarGift,
            giftMessage,
            FamiliarGift: gift)).ConfigureAwait(false);

        return new NegotiationSessionResult(
            NegotiationOutcomeKind.FamiliarFlee,
            NegotiationOutcomeReason.FamiliarTarget,
            familiarGift: gift,
            events: emitted);
    }

    private async ValueTask<NegotiationSessionResult> ResolveDemandsAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        List<NegotiationEvent> emitted,
        Func<NegotiationEvent, ValueTask> emit,
        int moodScore,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.TargetLevel > request.ActorLevel)
        {
            await emit(new NegotiationEvent(
                    NegotiationEventKind.Warning,
                    NegotiationEventCode.TargetLevelTooHigh,
                    "The target cannot be recruited at the actor's current level."))
                .ConfigureAwait(false);
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Flee,
                NegotiationOutcomeReason.TargetLevelTooHigh,
                moodScore,
                events: emitted);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<NegotiationRuntimeDemand> demands = request.Demands.Count > 0
            ? [SelectAuthoredDemand(request.Demands)]
            : _policy.CreateFallbackDemands(request, _random);
        cancellationToken.ThrowIfCancellationRequested();
        if (demands.Count > 0)
        {
            await emit(new NegotiationEvent(
                    NegotiationEventKind.DemandIntro,
                    NegotiationEventCode.DemandIntro,
                    "The target requests a concession before recruitment."))
                .ConfigureAwait(false);

            int currencySpent = 0;
            string? itemSpentId = null;
            foreach (NegotiationRuntimeDemand demand in demands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NegotiationSessionResult resolution = demand.Kind switch
                {
                    NegotiationDemandKind.Currency => await ResolveCurrencyDemandAsync(
                        request,
                        commands,
                        emit,
                        emitted,
                        demand,
                        moodScore,
                        currencySpent,
                        cancellationToken).ConfigureAwait(false),
                    NegotiationDemandKind.Item => await ResolveItemDemandAsync(
                        request,
                        commands,
                        emit,
                        emitted,
                        demand,
                        moodScore,
                        cancellationToken).ConfigureAwait(false),
                    _ => throw new InvalidOperationException($"Unsupported negotiation demand kind '{demand.Kind}'.")
                };
                cancellationToken.ThrowIfCancellationRequested();

                if (resolution.Outcome == NegotiationOutcomeKind.Cancelled)
                {
                    return new NegotiationSessionResult(
                        NegotiationOutcomeKind.Cancelled,
                        NegotiationOutcomeReason.Cancelled,
                        moodScore,
                        events: emitted);
                }
                currencySpent = checked(currencySpent + resolution.CurrencySpent);
                itemSpentId = resolution.ItemSpentId ?? itemSpentId;
                if (resolution.Outcome != NegotiationOutcomeKind.Success)
                {
                    return new NegotiationSessionResult(
                        resolution.Outcome,
                        resolution.Reason,
                        moodScore,
                        currencySpent,
                        itemSpentId,
                        events: emitted);
                }
            }

            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Success,
                NegotiationOutcomeReason.None,
                moodScore,
                currencySpent,
                itemSpentId,
                events: emitted);
        }

        cancellationToken.ThrowIfCancellationRequested();
        bool demandlessSuccess = _policy.ResolveDemandlessSuccess(request, _random);
        cancellationToken.ThrowIfCancellationRequested();
        if (demandlessSuccess)
        {
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Success,
                NegotiationOutcomeReason.None,
                moodScore,
                events: emitted);
        }

        await emit(new NegotiationEvent(
                NegotiationEventKind.Warning,
                NegotiationEventCode.DemandlessRejected,
                "The target ended negotiations without an agreement."))
            .ConfigureAwait(false);
        return new NegotiationSessionResult(
            NegotiationOutcomeKind.Trick,
            NegotiationOutcomeReason.Trick,
            moodScore,
            events: emitted);
    }

    private async ValueTask<NegotiationSessionResult> ResolveCurrencyDemandAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        Func<NegotiationEvent, ValueTask> emit,
        IReadOnlyList<NegotiationEvent> emitted,
        NegotiationRuntimeDemand demand,
        int moodScore,
        int alreadyCommittedCurrency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int currencyDemand = demand.CurrencyAmount!.Value;
        if (currencyDemand > request.CurrentCurrency - alreadyCommittedCurrency)
        {
            await emit(new NegotiationEvent(
                NegotiationEventKind.Failure,
                NegotiationEventCode.InsufficientCurrency,
                $"The required currency amount of {currencyDemand} is unavailable.",
                Amount: currencyDemand)).ConfigureAwait(false);
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Failure,
                NegotiationOutcomeReason.InsufficientCurrency,
                moodScore,
                events: emitted);
        }

        var prompt = new NegotiationDemandPrompt(
            demand,
            $"Provide {currencyDemand} currency?",
            [
                new NegotiationDemandOption(NegotiationDemandDecision.Accept, $"Provide {currencyDemand}"),
                new NegotiationDemandOption(NegotiationDemandDecision.Refuse, "Refuse")
            ]);
        NegotiationDemandSelection choice = await commands.ReadDemandAsync(prompt, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (choice.Cancelled)
        {
            await emit(CreateCancellationEvent()).ConfigureAwait(false);
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Cancelled,
                NegotiationOutcomeReason.Cancelled,
                moodScore,
                events: emitted);
        }

        if (choice.Decision != NegotiationDemandDecision.Accept)
        {
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Failure,
                NegotiationOutcomeReason.CurrencyRefused,
                moodScore,
                events: emitted);
        }

        return new NegotiationSessionResult(
            NegotiationOutcomeKind.Success,
            NegotiationOutcomeReason.None,
            moodScore,
            currencyDemand,
            events: emitted);
    }

    private async ValueTask<NegotiationSessionResult> ResolveItemDemandAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        Func<NegotiationEvent, ValueTask> emit,
        IReadOnlyList<NegotiationEvent> emitted,
        NegotiationRuntimeDemand demand,
        int moodScore,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NegotiationAvailableItem itemDemand = demand.Item!;
        var prompt = new NegotiationDemandPrompt(
            demand,
            $"{request.TargetName}: \"A {itemDemand.DisplayName} would be lovely.\"",
            [
                new NegotiationDemandOption(NegotiationDemandDecision.Accept, $"Give {itemDemand.DisplayName}"),
                new NegotiationDemandOption(NegotiationDemandDecision.Refuse, "Refuse")
            ]);
        NegotiationDemandSelection choice = await commands.ReadDemandAsync(prompt, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (choice.Cancelled)
        {
            await emit(CreateCancellationEvent()).ConfigureAwait(false);
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Cancelled,
                NegotiationOutcomeReason.Cancelled,
                moodScore,
                events: emitted);
        }

        if (choice.Decision != NegotiationDemandDecision.Accept)
        {
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Failure,
                NegotiationOutcomeReason.ItemRefused,
                moodScore,
                events: emitted);
        }

        return new NegotiationSessionResult(
            NegotiationOutcomeKind.Success,
            NegotiationOutcomeReason.None,
            moodScore,
            itemSpentId: itemDemand.ItemId,
            events: emitted);
    }

    private static NegotiationEvent CreateCancellationEvent() => new(
        NegotiationEventKind.Information,
        NegotiationEventCode.Cancelled,
        "Negotiation was cancelled.");

    private NegotiationRuntimeDemand SelectAuthoredDemand(IReadOnlyList<NegotiationRuntimeDemand> demands)
    {
        if (!NegotiationNumericDomain.TrySumDemandWeights(
                demands.Select(demand => demand.Weight),
                out int totalWeight) || totalWeight == 0)
        {
            throw new InvalidOperationException(
                "Authored negotiation demand weights must have a positive aggregate within the supported numeric domain.");
        }

        int roll = _random.NextInt32(0, totalWeight);
        long cumulative = 0;
        foreach (NegotiationRuntimeDemand demand in demands)
        {
            cumulative += demand.Weight;
            if (roll < cumulative)
            {
                return demand;
            }
        }

        return demands[^1];
    }
}

public enum RecruitmentTransactionStatus
{
    Applied,
    Rejected
}

public enum RecruitmentTransactionErrorCode
{
    None,
    AlreadyRecruitedThisBattle,
    AlreadyOwned,
    RosterFull,
    InvalidTarget
}

public sealed record RecruitmentTransactionRequest(
    ContentId TargetId,
    bool AlreadyRecruitedThisBattle,
    bool AlreadyOwned,
    bool HasOpenRosterSlot,
    bool IsValidTarget = true);

public sealed record RecruitmentTransactionResult(
    RecruitmentTransactionStatus Status,
    RecruitmentTransactionErrorCode ErrorCode,
    ContentId TargetId)
{
    public bool Applied => Status == RecruitmentTransactionStatus.Applied;
}

public interface IRecruitmentTransactionService
{
    RecruitmentTransactionResult Validate(RecruitmentTransactionRequest request);
}

public sealed class RecruitmentTransactionService : IRecruitmentTransactionService
{
    public RecruitmentTransactionResult Validate(RecruitmentTransactionRequest request)
    {
        if (!request.IsValidTarget)
        {
            return Rejected(request.TargetId, RecruitmentTransactionErrorCode.InvalidTarget);
        }
        if (request.AlreadyRecruitedThisBattle)
        {
            return Rejected(request.TargetId, RecruitmentTransactionErrorCode.AlreadyRecruitedThisBattle);
        }
        if (request.AlreadyOwned)
        {
            return Rejected(request.TargetId, RecruitmentTransactionErrorCode.AlreadyOwned);
        }
        if (!request.HasOpenRosterSlot)
        {
            return Rejected(request.TargetId, RecruitmentTransactionErrorCode.RosterFull);
        }

        return new RecruitmentTransactionResult(
            RecruitmentTransactionStatus.Applied,
            RecruitmentTransactionErrorCode.None,
            request.TargetId);
    }

    private static RecruitmentTransactionResult Rejected(ContentId targetId, RecruitmentTransactionErrorCode code) =>
        new(RecruitmentTransactionStatus.Rejected, code, targetId);
}

public sealed record BattleRewardEnemySnapshot(
    ContentId EnemyId,
    int Level,
    decimal Strength,
    decimal Magic,
    decimal Vitality,
    decimal Agility,
    decimal Luck,
    decimal Defense = 0m);

public sealed record BattleRewardRecipientSnapshot(ContentId ActorId, bool IsAlive, bool HasActiveHostedEntity);

public enum BattleRewardRecipientKind
{
    Actor,
    ActiveHostedEntity
}

public sealed record BattleRewardApplication(
    ContentId RecipientId,
    BattleRewardRecipientKind Kind,
    int Experience);

public sealed record BattleRewardRequest
{
    public BattleRewardRequest(
        IEnumerable<BattleRewardEnemySnapshot> enemies,
        IEnumerable<BattleRewardRecipientSnapshot> recipients,
        bool grantRewards = true)
    {
        Enemies = Array.AsReadOnly((enemies ?? throw new ArgumentNullException(nameof(enemies))).ToArray());
        Recipients = Array.AsReadOnly((recipients ?? throw new ArgumentNullException(nameof(recipients))).ToArray());
        GrantRewards = grantRewards;
    }

    public IReadOnlyList<BattleRewardEnemySnapshot> Enemies { get; }
    public IReadOnlyList<BattleRewardRecipientSnapshot> Recipients { get; }
    public bool GrantRewards { get; }
}

public sealed record BattleRewardResult
{
    public BattleRewardResult(
        int totalExperience,
        int totalCurrency,
        IEnumerable<BattleRewardApplication>? applications = null)
    {
        TotalExperience = totalExperience;
        TotalCurrency = totalCurrency;
        Applications = Array.AsReadOnly((applications ?? []).ToArray());
    }

    public int TotalExperience { get; }
    public int TotalCurrency { get; }
    public IReadOnlyList<BattleRewardApplication> Applications { get; }
}

public interface IBattleRewardService
{
    BattleRewardResult Calculate(BattleRewardRequest request);
}

public sealed class BattleRewardService : IBattleRewardService
{
    private readonly ProductionCombatRuleset _ruleset;

    public BattleRewardService(ProductionCombatRuleset ruleset)
    {
        _ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
    }

    public BattleRewardResult Calculate(BattleRewardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.GrantRewards)
        {
            return new BattleRewardResult(0, 0);
        }

        int totalExperience = AggregateRewards(request.Enemies, CalculateExperienceYield);
        int totalCurrency = AggregateRewards(request.Enemies, CalculateCurrencyYield);
        var applications = new List<BattleRewardApplication>();
        foreach (BattleRewardRecipientSnapshot recipient in request.Recipients.Where(recipient => recipient.IsAlive))
        {
            applications.Add(new BattleRewardApplication(
                recipient.ActorId,
                BattleRewardRecipientKind.Actor,
                totalExperience));
            if (recipient.HasActiveHostedEntity)
            {
                applications.Add(new BattleRewardApplication(
                    recipient.ActorId,
                    BattleRewardRecipientKind.ActiveHostedEntity,
                    totalExperience));
            }
        }

        return new BattleRewardResult(totalExperience, totalCurrency, applications);
    }

    private int CalculateExperienceYield(BattleRewardEnemySnapshot enemy) =>
        _ruleset.CalculateExperienceYield(new(
            enemy.Level,
            Stats(enemy)));

    private int CalculateCurrencyYield(BattleRewardEnemySnapshot enemy) =>
        _ruleset.CalculateCurrencyYield(new(
            enemy.Level,
            Stats(enemy)));

    private static int AggregateRewards(
        IEnumerable<BattleRewardEnemySnapshot> enemies,
        Func<BattleRewardEnemySnapshot, int> calculateYield)
    {
        int total = 0;
        foreach (BattleRewardEnemySnapshot enemy in enemies)
        {
            total = CombatArithmetic.SaturatingAdd(total, calculateYield(enemy));
        }

        return total;
    }

    private static ProductionCombatStats Stats(BattleRewardEnemySnapshot enemy) =>
        new(
            enemy.Strength,
            enemy.Magic,
            enemy.Vitality,
            enemy.Agility,
            enemy.Luck,
            enemy.Defense);
}
