using Convergence.Content;
using Convergence.Catalog;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Encounters;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed record TrainingAnnexNegotiationEvidence(
    ContentId TargetEntityId,
    RuntimeInstanceId TargetInstanceId,
    NegotiationOutcomeKind Outcome,
    NegotiationOutcomeReason Reason,
    int MoodScore,
    int MaccaSpent,
    string? ItemSpentId,
    RecruitmentTransactionStatus? RecruitmentStatus,
    RecruitmentTransactionErrorCode? RecruitmentErrorCode,
    PartyStockTransitionCode? StockTransitionCode,
    int WalletBefore,
    int WalletAfter,
    int DemonStockCountBefore,
    int DemonStockCountAfter,
    bool Recruited,
    int EventCount);

internal sealed record TrainingAnnexNegotiationInteractionResult(
    RuntimePartyStockSnapshot PartyStock,
    RuntimeWalletSnapshot Wallet,
    IReadOnlyList<TrainingAnnexNegotiationEvidence> Evidence);

internal sealed class TrainingAnnexNegotiationController
{
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;
    private readonly INegotiationSessionService _negotiations;
    private readonly IRecruitmentTransactionService _recruitment;
    private readonly IPartyStockTransitionService _partyStock;
    private readonly IStockCapacityPolicy _stockCapacity;

    public TrainingAnnexNegotiationController(
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        IRandomSource randomSource,
        IRecruitmentTransactionService? recruitment = null,
        IPartyStockTransitionService? partyStock = null,
        IStockCapacityPolicy? stockCapacity = null)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
        ArgumentNullException.ThrowIfNull(randomSource);

        _stockCapacity = stockCapacity ?? NoLimitStockCapacityPolicy.Instance;
        _negotiations = new NegotiationSessionService(randomSource, new TrainingAnnexNegotiationPolicy());
        _recruitment = recruitment ?? new RecruitmentTransactionService();
        _partyStock = partyStock ?? new PartyStockTransitionService(_stockCapacity);
    }

    public async ValueTask<TrainingAnnexNegotiationInteractionResult> OpenAsync(
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        RuntimePartyStockSnapshot party,
        RuntimeWalletSnapshot wallet,
        IEconomyTransactionService economy,
        ISet<ContentId> recruitedThisSession,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(recruitedThisSession);
        ArgumentNullException.ThrowIfNull(commands);

        NegotiationDefinition negotiation = catalog.GetRequiredNegotiation(TrainingAnnexHostSupport.SteadySampleNegotiation);
        IReadOnlyList<TrainingAnnexRuntimeActor> candidates = FindRecruitmentCandidates(negotiation, roster);
        if (candidates.Count == 0)
        {
            await _eventSink.PublishAsync(
                $"Negotiation unavailable: {negotiation.DisplayName} has no prepared recruitable targets.",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexNegotiationInteractionResult(party, wallet, []);
        }

        await _eventSink.PublishAsync(
            $"Negotiation opened: {negotiation.DisplayName}; {TargetSummary(candidates)}; wallet {wallet.Balance} M.",
            cancellationToken).ConfigureAwait(false);

        HostCommandReadResult<CleanTrainingAnnexPlayCommand> targetSelection =
            await _commandSource.ReadAsync(
                CreateTargetMenu(candidates, party),
                cancellationToken).ConfigureAwait(false);
        TrainingAnnexRuntimeActor? target = ResolveSelectedTarget(candidates, targetSelection);
        if (!targetSelection.IsSelected ||
            targetSelection.Command == CleanTrainingAnnexPlayCommand.Back ||
            target is null)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync(
                "Negotiation canceled before contact; wallet and Demon stock are unchanged.",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexNegotiationInteractionResult(party, wallet, []);
        }

        commands.Add(targetSelection.Command);
        var negotiationCommands = new TrainingAnnexNegotiationCommandSource(
            _commandSource,
            commands,
            target.Actor.Entity.DisplayName);
        var negotiationEvents = new TrainingAnnexNegotiationEventSink(
            _eventSink,
            target.Actor.Entity.DisplayName);
        NegotiationSessionResult session = await _negotiations.RunAsync(
            BuildRequest(negotiation, target, roster.Player, party, wallet),
            negotiationCommands,
            negotiationEvents,
            cancellationToken).ConfigureAwait(false);

        if (session.Outcome != NegotiationOutcomeKind.Success)
        {
            await _eventSink.PublishAsync(
                $"Negotiation ended: {session.Outcome} ({PresentationReasonLabel(session.Reason)}); wallet and Demon stock are unchanged.",
                cancellationToken).ConfigureAwait(false);
            return Result(
                party,
                wallet,
                Evidence(target, session, party, party, wallet, wallet, null, null, false));
        }

        RecruitmentTransactionResult recruitment = _recruitment.Validate(new RecruitmentTransactionRequest(
            target.Actor.Entity.Id,
            recruitedThisSession.Contains(target.Actor.Entity.Id),
            AlreadyOwnedByEntity(party, target.Actor.Entity.Id),
            HasOpenDemonStockSlot(party),
            target.Actor.Entity.Capabilities.Recruitable));
        if (!recruitment.Applied)
        {
            await _eventSink.PublishAsync(
                $"Recruitment rejected: {recruitment.ErrorCode}; wallet and Demon stock are unchanged.",
                cancellationToken).ConfigureAwait(false);
            return Result(
                party,
                wallet,
                Evidence(target, session, party, party, wallet, wallet, recruitment, null, false));
        }

        PartyStockTransitionResult stock = _partyStock.AddDemonToStock(new AddDemonToStockRequest(
            party,
            TrainingAnnexHostSupport.Reference(target)));
        if (!stock.Applied)
        {
            await _eventSink.PublishAsync(
                $"Recruitment stock update rejected: {stock.Code}; wallet and Demon stock are unchanged.",
                cancellationToken).ConfigureAwait(false);
            return Result(
                party,
                wallet,
                Evidence(target, session, party, party, wallet, wallet, recruitment, stock, false));
        }

        RuntimeWalletSnapshot nextWallet = wallet;
        if (session.CurrencySpent > 0)
        {
            WalletTransactionResult spend = economy.Debit(wallet, session.CurrencySpent);
            if (!spend.Applied)
            {
                await _eventSink.PublishAsync(
                    $"Recruitment donation rejected: {spend.Code}; wallet and Demon stock are unchanged.",
                    cancellationToken).ConfigureAwait(false);
                return Result(
                    party,
                    wallet,
                    Evidence(target, session, party, party, wallet, wallet, recruitment, stock, false));
            }

            nextWallet = spend.After;
        }

        recruitedThisSession.Add(target.Actor.Entity.Id);
        await _eventSink.PublishAsync(
            $"Recruitment applied: {target.Actor.Entity.DisplayName} joined Demon stock; wallet {wallet.Balance}->{nextWallet.Balance} M; Demon stock {party.DemonStock.Count}->{stock.After.DemonStock.Count}.",
            cancellationToken).ConfigureAwait(false);
        return Result(
            stock.After,
            nextWallet,
            Evidence(target, session, party, stock.After, wallet, nextWallet, recruitment, stock, true));
    }

    private static TrainingAnnexNegotiationInteractionResult Result(
        RuntimePartyStockSnapshot party,
        RuntimeWalletSnapshot wallet,
        TrainingAnnexNegotiationEvidence evidence) =>
        new(party, wallet, [evidence]);

    private NegotiationSessionRequest BuildRequest(
        NegotiationDefinition negotiation,
        TrainingAnnexRuntimeActor target,
        TrainingAnnexRuntimeActor player,
        RuntimePartyStockSnapshot party,
        RuntimeWalletSnapshot wallet)
    {
        RuntimeActorSnapshot playerSnapshot = player.Actor.State.ToSnapshot();
        return new NegotiationSessionRequest(
            target.Actor.Entity.DisplayName,
            actorLevel: player.Level,
            targetLevel: target.Level,
            actorLuck: StatAsInt(playerSnapshot, StandardProgressionIds.Luck),
            activeOpponentCount: 1,
            contextIds: [],
            isTargetFamiliar: AlreadyOwnedByEntity(party, target.Actor.Entity.Id),
            hasRecruitmentCapacity: HasOpenDemonStockSlot(party),
            currentCurrency: wallet.Balance,
            questions: negotiation.Questions.Select(question => new NegotiationQuestionPrompt(
                question.Text,
                question.Answers.Select(answer => new NegotiationAnswerOption(answer.Text, answer.Score)))),
            familiarDialogueLines: negotiation.FamiliarDialogueLines,
            demands: negotiation.Demands.Select(MapDemand));
    }

    private bool HasOpenDemonStockSlot(RuntimePartyStockSnapshot party) =>
        party.DemonStock.Count < _stockCapacity.GetCapacity(party.OwnerLevel);

    private static bool AlreadyOwnedByEntity(RuntimePartyStockSnapshot party, ContentId entityId) =>
        party.DemonStock.Any(demon => demon.EntityDefinitionId == entityId);

    private static IReadOnlyList<TrainingAnnexRuntimeActor> FindRecruitmentCandidates(
        NegotiationDefinition negotiation,
        TrainingAnnexActorRoster roster) =>
        roster.StockMembers
            .Where(IsHostRecruitmentCandidate)
            .Where(actor => actor.Actor.Entity.Capabilities.Recruitable)
            .Where(actor => MatchesNegotiationDefaults(negotiation, actor))
            .ToArray();

    private static bool IsHostRecruitmentCandidate(TrainingAnnexRuntimeActor actor) =>
        actor.Role.Contains("candidate", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesNegotiationDefaults(
        NegotiationDefinition negotiation,
        TrainingAnnexRuntimeActor actor)
    {
        bool hasDefaultEntities = negotiation.DefaultEntityIds.Count > 0;
        bool hasDefaultRaces = negotiation.DefaultRaceIds.Count > 0;
        if (!hasDefaultEntities && !hasDefaultRaces)
        {
            return true;
        }

        EntityDefinition entity = actor.Actor.Entity;
        return negotiation.DefaultEntityIds.Contains(entity.Id) ||
            negotiation.DefaultRaceIds.Contains(entity.RaceId);
    }

    private static TrainingAnnexRuntimeActor? ResolveSelectedTarget(
        IReadOnlyList<TrainingAnnexRuntimeActor> candidates,
        HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection)
    {
        RuntimeInstanceId? selectedId = selection.SelectionIdentity?.RuntimeInstanceId;
        return selectedId is null
            ? null
            : candidates.FirstOrDefault(candidate => candidate.Actor.State.InstanceId == selectedId);
    }

    private static string TargetSummary(IReadOnlyList<TrainingAnnexRuntimeActor> candidates) =>
        candidates.Count == 1
            ? $"target {candidates[0].Actor.Entity.DisplayName}"
            : $"{candidates.Count} targets";

    private static NegotiationRuntimeDemand MapDemand(NegotiationDemandDefinition demand)
    {
        if (demand.DemandId == TrainingAnnexHostSupport.SampleMaccaDemand)
        {
            return new NegotiationRuntimeDemand(
                demand.DemandId,
                NegotiationDemandKind.Currency,
                demand.Weight,
                currencyAmount: RequiredPositiveIntParameter(demand, "amount"));
        }

        throw new InvalidOperationException(
            $"Training Annex negotiation demand '{demand.DemandId}' has no host mapping.");
    }

    private static int RequiredPositiveIntParameter(NegotiationDemandDefinition demand, string key)
    {
        if (!demand.Parameters.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException(
                $"Training Annex negotiation demand '{demand.DemandId}' is missing '{key}'.");
        }

        int amount = value switch
        {
            int integer => integer,
            long integer when integer >= int.MinValue && integer <= int.MaxValue => (int)integer,
            decimal number when decimal.Truncate(number) == number &&
                number >= int.MinValue &&
                number <= int.MaxValue => (int)number,
            _ => throw new InvalidOperationException(
                $"Training Annex negotiation demand '{demand.DemandId}' parameter '{key}' must be a whole number.")
        };
        if (amount <= 0)
        {
            throw new InvalidOperationException(
                $"Training Annex negotiation demand '{demand.DemandId}' parameter '{key}' must be positive.");
        }

        return amount;
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateTargetMenu(
        IReadOnlyList<TrainingAnnexRuntimeActor> targets,
        RuntimePartyStockSnapshot party)
    {
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Clean Negotiation",
            targets.Select(target =>
                {
                    bool alreadyOwned = AlreadyOwnedByEntity(party, target.Actor.Entity.Id);
                    return new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                        CleanTrainingAnnexPlayCommand.SelectNegotiationTarget,
                        alreadyOwned
                            ? $"{target.Actor.Entity.DisplayName} [Familiar]"
                            : target.Actor.Entity.DisplayName,
                        Description: alreadyOwned
                            ? "Runs the familiar-demon negotiation path without adding stock."
                            : "Starts a clean negotiation session for this recruitable sample.",
                        SelectionIdentity: HostCommandSelectionIdentity.ForRuntimeInstance(
                            target.Actor.State.InstanceId));
                })
                .Append(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back"))
                .ToArray());
    }

    private static TrainingAnnexNegotiationEvidence Evidence(
        TrainingAnnexRuntimeActor target,
        NegotiationSessionResult session,
        RuntimePartyStockSnapshot beforeParty,
        RuntimePartyStockSnapshot afterParty,
        RuntimeWalletSnapshot beforeWallet,
        RuntimeWalletSnapshot afterWallet,
        RecruitmentTransactionResult? recruitment,
        PartyStockTransitionResult? stock,
        bool recruited) =>
        new(
            target.Actor.Entity.Id,
            target.Actor.State.InstanceId,
            session.Outcome,
            session.Reason,
            session.MoodScore,
            session.CurrencySpent,
            session.ItemSpentId,
            recruitment?.Status,
            recruitment?.ErrorCode,
            stock?.Code,
            beforeWallet.Balance,
            afterWallet.Balance,
            beforeParty.DemonStock.Count,
            afterParty.DemonStock.Count,
            recruited,
            session.Events.Count);

    private static int StatAsInt(RuntimeActorSnapshot actor, ContentId statId) =>
        (int)Math.Round(actor.Stats.EffectiveStats.GetValueOrDefault(statId), MidpointRounding.AwayFromZero);

    private static string PresentationReasonLabel(NegotiationOutcomeReason reason) => reason switch
    {
        NegotiationOutcomeReason.PolicyBlocked => "MoonBlocked",
        NegotiationOutcomeReason.FamiliarTarget => "FamiliarDemon",
        NegotiationOutcomeReason.CapacityUnavailable => "StockFull",
        NegotiationOutcomeReason.InsufficientCurrency => "InsufficientMacca",
        NegotiationOutcomeReason.CurrencyRefused => "MaccaRefused",
        _ => reason.ToString()
    };

    private sealed class TrainingAnnexNegotiationCommandSource(
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commands,
        ICollection<CleanTrainingAnnexPlayCommand> commandLog,
        string targetName) : INegotiationCommandSource
    {
        public async ValueTask<NegotiationAnswerSelection> ReadAnswerAsync(
            NegotiationQuestionPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection = await commands.ReadAsync(
                new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
                    prompt.Text,
                    prompt.Answers.Select((answer, index) => new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                            CleanTrainingAnnexPlayCommand.SelectNegotiationAnswer,
                            answer.Text,
                            SelectionIdentity: HostCommandSelectionIdentity.ForContent(
                                ContentId.Parse($"answer_{index}"))))
                        .Append(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                            CleanTrainingAnnexPlayCommand.Back,
                            "Back"))),
                cancellationToken).ConfigureAwait(false);

            if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
            {
                commandLog.Add(CleanTrainingAnnexPlayCommand.Back);
                return NegotiationAnswerSelection.Cancel();
            }

            commandLog.Add(selection.Command);
            string? selected = selection.SelectionIdentity?.ContentId?.ToString();
            if (selected is null ||
                !selected.StartsWith("answer_", StringComparison.Ordinal) ||
                !int.TryParse(selected["answer_".Length..], out int index))
            {
                return NegotiationAnswerSelection.Cancel();
            }

            return NegotiationAnswerSelection.Selected(index);
        }

        public async ValueTask<NegotiationDemandSelection> ReadDemandAsync(
            NegotiationDemandPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            string header = prompt.Kind switch
            {
                NegotiationDemandKind.Currency =>
                    $"{targetName}: \"A gift of {prompt.Demand.CurrencyAmount} Macca should suffice.\"",
                NegotiationDemandKind.Item =>
                    $"{targetName}: \"A {prompt.Demand.Item!.DisplayName} would be useful.\"",
                _ => prompt.Prompt
            };
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection = await commands.ReadAsync(
                new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
                    header,
                    prompt.Options.Select(option => new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                            CleanTrainingAnnexPlayCommand.SelectNegotiationDemand,
                            option.Decision switch
                            {
                                NegotiationDemandDecision.Accept when prompt.Kind == NegotiationDemandKind.Currency =>
                                    $"Give {prompt.Demand.CurrencyAmount} Macca",
                                NegotiationDemandDecision.Accept when prompt.Kind == NegotiationDemandKind.Item =>
                                    $"Give {prompt.Demand.Item!.DisplayName}",
                                _ => "Refuse"
                            },
                            SelectionIdentity: HostCommandSelectionIdentity.ForContent(
                                ContentId.Parse(option.Decision == NegotiationDemandDecision.Accept
                                    ? "accept"
                                    : "refuse"))))
                        .Append(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                            CleanTrainingAnnexPlayCommand.Back,
                            "Back"))),
                cancellationToken).ConfigureAwait(false);

            if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
            {
                commandLog.Add(CleanTrainingAnnexPlayCommand.Back);
                return NegotiationDemandSelection.Cancel();
            }

            commandLog.Add(selection.Command);
            return selection.SelectionIdentity?.ContentId?.ToString() == "accept"
                ? NegotiationDemandSelection.Selected(NegotiationDemandDecision.Accept)
                : NegotiationDemandSelection.Selected(NegotiationDemandDecision.Refuse);
        }
    }

    private sealed class TrainingAnnexNegotiationEventSink(
        IHostEventSink<string> events,
        string targetName)
        : IHostEventSink<NegotiationEvent>
    {
        public ValueTask PublishAsync(
            NegotiationEvent value,
            CancellationToken cancellationToken = default) =>
            events.PublishAsync(
                $"Negotiation event: {value.Kind}; {Present(value)}",
                cancellationToken);

        private string Present(NegotiationEvent value) => value.Code switch
        {
            NegotiationEventCode.PolicyBlocked =>
                $"The {targetName} is agitated due to the Full Moon and cannot be reasoned with!",
            NegotiationEventCode.CapacityUnavailable => "Your Demon Stock is full!",
            NegotiationEventCode.OpeningRefused => $"{targetName} is on guard and refuses to talk!",
            NegotiationEventCode.MissingQuestions => $"{targetName} seems unresponsive...",
            NegotiationEventCode.Cancelled => $"{targetName} seems disappointed...",
            NegotiationEventCode.MoodPositive => $"{targetName} seems pleased with your answers.",
            NegotiationEventCode.MoodNeutral => $"{targetName} is considering your words...",
            NegotiationEventCode.MoodNegative => $"{targetName} grows angry!",
            NegotiationEventCode.TargetLevelTooHigh =>
                $"{targetName}: \"You have courage, but you are not yet worthy to command me. Perhaps we shall meet again.\"",
            NegotiationEventCode.DemandIntro =>
                $"{targetName}: \"Your words are intriguing. But talk is cheap.\"",
            NegotiationEventCode.InsufficientCurrency =>
                $"The required donation of {value.Amount} Macca is missing.",
            NegotiationEventCode.DemandlessRejected => $"{targetName}: \"Hmph. You waste my time.\"",
            _ => value.Message
        };
    }
}
