using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;

namespace JRPGPrototype.Logic.Battle.Runtime;

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
    MoonBlocked,
    FamiliarDemon,
    StockFull,
    GuardRefusal,
    MissingQuestions,
    Cancelled,
    MoodFailure,
    MoodFlee,
    TargetLevelTooHigh,
    InsufficientMacca,
    MaccaRefused,
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

public sealed record NegotiationEvent(NegotiationEventKind Kind, string Message);

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
    Macca,
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
        NegotiationDemandKind kind,
        string prompt,
        IEnumerable<NegotiationDemandOption> options)
    {
        Kind = kind;
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        Options = Array.AsReadOnly((options ?? throw new ArgumentNullException(nameof(options))).ToArray());
    }

    public NegotiationDemandKind Kind { get; }
    public string Prompt { get; }
    public IReadOnlyList<NegotiationDemandOption> Options { get; }
}

public sealed record NegotiationRuntimeDemand
{
    public NegotiationRuntimeDemand(
        ContentId demandId,
        NegotiationDemandKind kind,
        int weight,
        int? maccaAmount = null,
        NegotiationAvailableItem? item = null)
    {
        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Negotiation demand weight must be positive.");
        }
        if (kind == NegotiationDemandKind.Macca && maccaAmount is not > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maccaAmount), "Macca demands require a positive amount.");
        }
        if (kind == NegotiationDemandKind.Item && item is null)
        {
            throw new ArgumentNullException(nameof(item), "Item demands require an item.");
        }

        DemandId = demandId;
        Kind = kind;
        Weight = weight;
        MaccaAmount = maccaAmount;
        Item = item;
    }

    public ContentId DemandId { get; }
    public NegotiationDemandKind Kind { get; }
    public int Weight { get; }
    public int? MaccaAmount { get; }
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
    Macca,
    HealParty
}

public sealed record NegotiationFamiliarGift(
    NegotiationFamiliarGiftKind Kind,
    string? ItemId = null,
    int Quantity = 0,
    int Macca = 0,
    decimal HealPercent = 0m)
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
        int livingEnemyCount,
        bool isMoonBlocked,
        bool isTargetAlreadyOwned,
        bool hasOpenDemonStockSlot,
        int currentMacca,
        IEnumerable<NegotiationQuestionPrompt>? questions = null,
        IEnumerable<string>? familiarDialogueLines = null,
        string? specificFamiliarDialogue = null,
        IEnumerable<NegotiationAvailableItem>? availableHealingItems = null,
        IEnumerable<NegotiationRuntimeDemand>? demands = null)
    {
        if (actorLevel <= 0) throw new ArgumentOutOfRangeException(nameof(actorLevel));
        if (targetLevel <= 0) throw new ArgumentOutOfRangeException(nameof(targetLevel));
        if (livingEnemyCount <= 0) throw new ArgumentOutOfRangeException(nameof(livingEnemyCount));
        if (currentMacca < 0) throw new ArgumentOutOfRangeException(nameof(currentMacca));

        TargetName = string.IsNullOrWhiteSpace(targetName) ? "Demon" : targetName;
        ActorLevel = actorLevel;
        TargetLevel = targetLevel;
        ActorLuck = actorLuck;
        LivingEnemyCount = livingEnemyCount;
        IsMoonBlocked = isMoonBlocked;
        IsTargetAlreadyOwned = isTargetAlreadyOwned;
        HasOpenDemonStockSlot = hasOpenDemonStockSlot;
        CurrentMacca = currentMacca;
        Questions = Array.AsReadOnly((questions ?? []).ToArray());
        FamiliarDialogueLines = Array.AsReadOnly((familiarDialogueLines ?? []).ToArray());
        SpecificFamiliarDialogue = specificFamiliarDialogue;
        AvailableHealingItems = Array.AsReadOnly((availableHealingItems ?? []).ToArray());
        Demands = Array.AsReadOnly((demands ?? []).ToArray());
    }

    public string TargetName { get; }
    public int ActorLevel { get; }
    public int TargetLevel { get; }
    public int ActorLuck { get; }
    public int LivingEnemyCount { get; }
    public bool IsMoonBlocked { get; }
    public bool IsTargetAlreadyOwned { get; }
    public bool HasOpenDemonStockSlot { get; }
    public int CurrentMacca { get; }
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
        int maccaSpent = 0,
        string? itemSpentId = null,
        NegotiationFamiliarGift? familiarGift = null,
        IEnumerable<NegotiationEvent>? events = null)
    {
        Outcome = outcome;
        Reason = reason;
        MoodScore = moodScore;
        MaccaSpent = maccaSpent;
        ItemSpentId = itemSpentId;
        FamiliarGift = familiarGift ?? NegotiationFamiliarGift.None;
        Events = Array.AsReadOnly((events ?? []).ToArray());
    }

    public NegotiationOutcomeKind Outcome { get; }
    public NegotiationOutcomeReason Reason { get; }
    public int MoodScore { get; }
    public int MaccaSpent { get; }
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

public sealed class NegotiationSessionService : INegotiationSessionService
{
    private readonly IRandomSource _random;

    public NegotiationSessionService(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public async ValueTask<NegotiationSessionResult> RunAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        IHostEventSink<NegotiationEvent>? events = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(commands);

        var emitted = new List<NegotiationEvent>();
        async ValueTask EmitAsync(NegotiationEventKind kind, string message)
        {
            var negotiationEvent = new NegotiationEvent(kind, message);
            emitted.Add(negotiationEvent);
            if (events is not null)
            {
                await events.PublishAsync(negotiationEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        if (request.IsMoonBlocked)
        {
            await EmitAsync(
                NegotiationEventKind.Failure,
                $"The {request.TargetName} is agitated due to the Full Moon and cannot be reasoned with!");
            return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.MoonBlocked);
        }

        if (request.IsTargetAlreadyOwned)
        {
            return await ResolveFamiliarAsync(request, emitted, EmitAsync).ConfigureAwait(false);
        }

        if (!request.HasOpenDemonStockSlot)
        {
            await EmitAsync(NegotiationEventKind.Failure, "Your Demon Stock is full!");
            return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.StockFull);
        }

        if (!CheckNegotiationChance(request.LivingEnemyCount))
        {
            await EmitAsync(
                NegotiationEventKind.Failure,
                $"{request.TargetName} is on guard and refuses to talk!");
            return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.GuardRefusal);
        }

        if (request.Questions.Count == 0)
        {
            await EmitAsync(
                NegotiationEventKind.Failure,
                $"{request.TargetName} seems unresponsive...");
            return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.MissingQuestions);
        }

        int moodScore = 0;
        var questions = request.Questions.ToList();
        for (int i = 0; i < 3 && questions.Count > 0; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int questionIndex = _random.NextInt32(0, questions.Count);
            NegotiationQuestionPrompt question = questions[questionIndex];
            questions.RemoveAt(questionIndex);

            NegotiationAnswerSelection answer = await commands.ReadAnswerAsync(question, cancellationToken)
                .ConfigureAwait(false);
            if (answer.Cancelled)
            {
                await EmitAsync(
                    NegotiationEventKind.Failure,
                    $"{request.TargetName} seems disappointed...");
                return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.Cancelled, moodScore);
            }

            if (answer.SelectedIndex < 0 || answer.SelectedIndex >= question.Answers.Count)
            {
                throw new InvalidOperationException("Negotiation answer selection was outside the prompt options.");
            }

            moodScore += question.Answers[answer.SelectedIndex].Score;
        }

        if (moodScore >= 4)
        {
            await EmitAsync(
                NegotiationEventKind.MoodPositive,
                $"{request.TargetName} seems pleased with your answers.");
            return await ResolveDemandsAsync(request, commands, emitted, EmitAsync, moodScore, cancellationToken)
                .ConfigureAwait(false);
        }

        if (moodScore > 0)
        {
            await EmitAsync(
                NegotiationEventKind.MoodNeutral,
                $"{request.TargetName} is considering your words...");
            return Result(NegotiationOutcomeKind.Flee, NegotiationOutcomeReason.MoodFlee, moodScore);
        }

        await EmitAsync(NegotiationEventKind.MoodNegative, $"{request.TargetName} grows angry!");
        return Result(NegotiationOutcomeKind.Failure, NegotiationOutcomeReason.MoodFailure, moodScore);

        NegotiationSessionResult Result(
            NegotiationOutcomeKind outcome,
            NegotiationOutcomeReason reason,
            int score = 0,
            int maccaSpent = 0,
            string? itemSpent = null,
            NegotiationFamiliarGift? gift = null) =>
            new(outcome, reason, score, maccaSpent, itemSpent, gift, emitted);
    }

    private async ValueTask<NegotiationSessionResult> ResolveFamiliarAsync(
        NegotiationSessionRequest request,
        List<NegotiationEvent> emitted,
        Func<NegotiationEventKind, string, ValueTask> emit)
    {
        string dialogue = $"{request.TargetName} looks at you with a sense of familiarity...";
        if (!string.IsNullOrWhiteSpace(request.SpecificFamiliarDialogue))
        {
            dialogue = $"{request.TargetName}: \"{request.SpecificFamiliarDialogue}\"";
        }
        else if (request.FamiliarDialogueLines.Count > 0)
        {
            dialogue = $"{request.TargetName}: \"{request.FamiliarDialogueLines[_random.NextInt32(0, request.FamiliarDialogueLines.Count)]}\"";
        }

        await emit(NegotiationEventKind.FamiliarDialogue, dialogue).ConfigureAwait(false);

        int roll = _random.NextInt32(0, 100);
        NegotiationFamiliarGift gift;
        if (roll < 50)
        {
            await emit(
                NegotiationEventKind.Information,
                $"{request.TargetName} gives you a Medicine and departs.").ConfigureAwait(false);
            gift = new NegotiationFamiliarGift(NegotiationFamiliarGiftKind.Item, ItemId: "101", Quantity: 1);
        }
        else if (roll < 80)
        {
            int macca = request.TargetLevel * 20;
            await emit(
                NegotiationEventKind.Information,
                $"{request.TargetName} gives you {macca} Macca and departs.").ConfigureAwait(false);
            gift = new NegotiationFamiliarGift(NegotiationFamiliarGiftKind.Macca, Macca: macca);
        }
        else
        {
            await emit(
                NegotiationEventKind.Information,
                $"{request.TargetName} casts a gentle light upon your party before departing.").ConfigureAwait(false);
            gift = new NegotiationFamiliarGift(NegotiationFamiliarGiftKind.HealParty, HealPercent: 0.15m);
        }

        return new NegotiationSessionResult(
            NegotiationOutcomeKind.FamiliarFlee,
            NegotiationOutcomeReason.FamiliarDemon,
            familiarGift: gift,
            events: emitted);
    }

    private bool CheckNegotiationChance(int livingEnemyCount)
    {
        if (livingEnemyCount <= 1) return true;
        if (livingEnemyCount == 2) return _random.NextInt32(0, 100) < 75;
        if (livingEnemyCount == 3) return _random.NextInt32(0, 100) < 50;
        return _random.NextInt32(0, 100) < 25;
    }

    private async ValueTask<NegotiationSessionResult> ResolveDemandsAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        List<NegotiationEvent> emitted,
        Func<NegotiationEventKind, string, ValueTask> emit,
        int moodScore,
        CancellationToken cancellationToken)
    {
        if (request.TargetLevel > request.ActorLevel)
        {
            await emit(
                NegotiationEventKind.Warning,
                $"{request.TargetName}: \"You have courage, but you are not yet worthy to command me. Perhaps we shall meet again.\"")
                .ConfigureAwait(false);
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Flee,
                NegotiationOutcomeReason.TargetLevelTooHigh,
                moodScore,
                events: emitted);
        }

        if (request.Demands.Count > 0)
        {
            return await ResolveAuthoredDemandAsync(
                request,
                commands,
                emitted,
                emit,
                moodScore,
                cancellationToken).ConfigureAwait(false);
        }

        double baseCost = Math.Pow(request.TargetLevel, 2) * 10;
        double luckDiscount = baseCost * (request.ActorLuck / 100.0);
        int maccaDemand = (int)Math.Max(request.TargetLevel * 5, baseCost - luckDiscount);
        NegotiationAvailableItem? itemDemand = request.AvailableHealingItems.FirstOrDefault();
        bool demandsItem = itemDemand is not null && _random.NextInt32(0, 100) < 50;

        await emit(
            NegotiationEventKind.DemandIntro,
            $"{request.TargetName}: \"Your words are intriguing. But talk is cheap.\"")
            .ConfigureAwait(false);

        int maccaSpent = 0;
        if (maccaDemand > 0)
        {
            if (request.CurrentMacca < maccaDemand)
            {
                await emit(
                    NegotiationEventKind.Failure,
                    $"The required donation of {maccaDemand} Macca is missing.").ConfigureAwait(false);
                return new NegotiationSessionResult(
                    NegotiationOutcomeKind.Failure,
                    NegotiationOutcomeReason.InsufficientMacca,
                    moodScore,
                    events: emitted);
            }

            var prompt = new NegotiationDemandPrompt(
                NegotiationDemandKind.Macca,
                $"{request.TargetName}: \"A gift of {maccaDemand} Macca should suffice.\"",
                [
                    new NegotiationDemandOption(NegotiationDemandDecision.Accept, $"Give {maccaDemand} Macca"),
                    new NegotiationDemandOption(NegotiationDemandDecision.Refuse, "Refuse")
                ]);
            NegotiationDemandSelection choice = await commands.ReadDemandAsync(prompt, cancellationToken)
                .ConfigureAwait(false);
            if (choice.Cancelled || choice.Decision != NegotiationDemandDecision.Accept)
            {
                return new NegotiationSessionResult(
                    NegotiationOutcomeKind.Failure,
                    NegotiationOutcomeReason.MaccaRefused,
                    moodScore,
                    events: emitted);
            }

            maccaSpent = maccaDemand;
            if (!demandsItem || itemDemand is null)
            {
                return new NegotiationSessionResult(
                    NegotiationOutcomeKind.Success,
                    NegotiationOutcomeReason.None,
                    moodScore,
                    maccaSpent,
                    events: emitted);
            }
        }

        if (demandsItem && itemDemand is not null)
        {
            var prompt = new NegotiationDemandPrompt(
                NegotiationDemandKind.Item,
                $"{request.TargetName}: \"A {itemDemand.DisplayName} would be lovely.\"",
                [
                    new NegotiationDemandOption(NegotiationDemandDecision.Accept, $"Give {itemDemand.DisplayName}"),
                    new NegotiationDemandOption(NegotiationDemandDecision.Refuse, "Refuse")
                ]);
            NegotiationDemandSelection choice = await commands.ReadDemandAsync(prompt, cancellationToken)
                .ConfigureAwait(false);
            if (choice.Cancelled || choice.Decision != NegotiationDemandDecision.Accept)
            {
                return new NegotiationSessionResult(
                    NegotiationOutcomeKind.Failure,
                    NegotiationOutcomeReason.ItemRefused,
                    moodScore,
                    maccaSpent,
                    events: emitted);
            }

            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Success,
                NegotiationOutcomeReason.None,
                moodScore,
                maccaSpent,
                itemDemand.ItemId,
                events: emitted);
        }

        if (_random.NextInt32(0, 100) < 50)
        {
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Success,
                NegotiationOutcomeReason.None,
                moodScore,
                maccaSpent,
                events: emitted);
        }

        await emit(NegotiationEventKind.Warning, $"{request.TargetName}: \"Hmph. You waste my time.\"")
            .ConfigureAwait(false);
        return new NegotiationSessionResult(
            NegotiationOutcomeKind.Trick,
            NegotiationOutcomeReason.Trick,
            moodScore,
            maccaSpent,
            events: emitted);
    }

    private async ValueTask<NegotiationSessionResult> ResolveAuthoredDemandAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        List<NegotiationEvent> emitted,
        Func<NegotiationEventKind, string, ValueTask> emit,
        int moodScore,
        CancellationToken cancellationToken)
    {
        NegotiationRuntimeDemand demand = SelectAuthoredDemand(request.Demands);
        await emit(
            NegotiationEventKind.DemandIntro,
            $"{request.TargetName}: \"Your words are intriguing. But talk is cheap.\"")
            .ConfigureAwait(false);

        return demand.Kind switch
        {
            NegotiationDemandKind.Macca => await ResolveAuthoredMaccaDemandAsync(
                request,
                commands,
                emit,
                emitted,
                demand,
                moodScore,
                cancellationToken).ConfigureAwait(false),
            NegotiationDemandKind.Item => await ResolveAuthoredItemDemandAsync(
                request,
                commands,
                emitted,
                demand,
                moodScore,
                cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported negotiation demand kind '{demand.Kind}'.")
        };
    }

    private async ValueTask<NegotiationSessionResult> ResolveAuthoredMaccaDemandAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        Func<NegotiationEventKind, string, ValueTask> emit,
        IReadOnlyList<NegotiationEvent> emitted,
        NegotiationRuntimeDemand demand,
        int moodScore,
        CancellationToken cancellationToken)
    {
        int maccaDemand = demand.MaccaAmount!.Value;
        if (request.CurrentMacca < maccaDemand)
        {
            await emit(
                NegotiationEventKind.Failure,
                $"The required donation of {maccaDemand} Macca is missing.").ConfigureAwait(false);
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Failure,
                NegotiationOutcomeReason.InsufficientMacca,
                moodScore,
                events: emitted);
        }

        var prompt = new NegotiationDemandPrompt(
            NegotiationDemandKind.Macca,
            $"{request.TargetName}: \"A gift of {maccaDemand} Macca should suffice.\"",
            [
                new NegotiationDemandOption(NegotiationDemandDecision.Accept, $"Give {maccaDemand} Macca"),
                new NegotiationDemandOption(NegotiationDemandDecision.Refuse, "Refuse")
            ]);
        NegotiationDemandSelection choice = await commands.ReadDemandAsync(prompt, cancellationToken)
            .ConfigureAwait(false);
        if (choice.Cancelled || choice.Decision != NegotiationDemandDecision.Accept)
        {
            return new NegotiationSessionResult(
                NegotiationOutcomeKind.Failure,
                NegotiationOutcomeReason.MaccaRefused,
                moodScore,
                events: emitted);
        }

        return new NegotiationSessionResult(
            NegotiationOutcomeKind.Success,
            NegotiationOutcomeReason.None,
            moodScore,
            maccaDemand,
            events: emitted);
    }

    private async ValueTask<NegotiationSessionResult> ResolveAuthoredItemDemandAsync(
        NegotiationSessionRequest request,
        INegotiationCommandSource commands,
        IReadOnlyList<NegotiationEvent> emitted,
        NegotiationRuntimeDemand demand,
        int moodScore,
        CancellationToken cancellationToken)
    {
        NegotiationAvailableItem itemDemand = demand.Item!;
        var prompt = new NegotiationDemandPrompt(
            NegotiationDemandKind.Item,
            $"{request.TargetName}: \"A {itemDemand.DisplayName} would be lovely.\"",
            [
                new NegotiationDemandOption(NegotiationDemandDecision.Accept, $"Give {itemDemand.DisplayName}"),
                new NegotiationDemandOption(NegotiationDemandDecision.Refuse, "Refuse")
            ]);
        NegotiationDemandSelection choice = await commands.ReadDemandAsync(prompt, cancellationToken)
            .ConfigureAwait(false);
        if (choice.Cancelled || choice.Decision != NegotiationDemandDecision.Accept)
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

    private NegotiationRuntimeDemand SelectAuthoredDemand(IReadOnlyList<NegotiationRuntimeDemand> demands)
    {
        int totalWeight = checked(demands.Sum(demand => demand.Weight));
        int roll = _random.NextInt32(0, totalWeight);
        int cumulative = 0;
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
    StockFull,
    InvalidTarget
}

public sealed record RecruitmentTransactionRequest(
    ContentId TargetId,
    bool AlreadyRecruitedThisBattle,
    bool AlreadyOwned,
    bool HasOpenStockSlot,
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
        if (!request.HasOpenStockSlot)
        {
            return Rejected(request.TargetId, RecruitmentTransactionErrorCode.StockFull);
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

public sealed record BattleRewardRecipientSnapshot(ContentId ActorId, bool IsAlive, bool HasActiveForm);

public enum BattleRewardRecipientKind
{
    Actor,
    ActiveForm
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
        int totalMacca,
        IEnumerable<BattleRewardApplication>? applications = null)
    {
        TotalExperience = totalExperience;
        TotalMacca = totalMacca;
        Applications = Array.AsReadOnly((applications ?? []).ToArray());
    }

    public int TotalExperience { get; }
    public int TotalMacca { get; }
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

        int totalExperience = request.Enemies.Sum(CalculateExperienceYield);
        int totalMacca = request.Enemies.Sum(CalculateMaccaYield);
        var applications = new List<BattleRewardApplication>();
        foreach (BattleRewardRecipientSnapshot recipient in request.Recipients.Where(recipient => recipient.IsAlive))
        {
            applications.Add(new BattleRewardApplication(
                recipient.ActorId,
                BattleRewardRecipientKind.Actor,
                totalExperience));
            if (recipient.HasActiveForm)
            {
                applications.Add(new BattleRewardApplication(
                    recipient.ActorId,
                    BattleRewardRecipientKind.ActiveForm,
                    totalExperience));
            }
        }

        return new BattleRewardResult(totalExperience, totalMacca, applications);
    }

    private int CalculateExperienceYield(BattleRewardEnemySnapshot enemy) =>
        _ruleset.CalculateExperienceYield(new(
            enemy.Level,
            Stats(enemy)));

    private int CalculateMaccaYield(BattleRewardEnemySnapshot enemy) =>
        _ruleset.CalculateMaccaYield(new(
            enemy.Level,
            Stats(enemy)));

    private static ProductionCombatStats Stats(BattleRewardEnemySnapshot enemy) =>
        new(
            enemy.Strength,
            enemy.Magic,
            enemy.Vitality,
            enemy.Agility,
            enemy.Luck,
            enemy.Defense);
}
