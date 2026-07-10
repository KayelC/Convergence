using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

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

        _stockCapacity = stockCapacity ?? new LegacyStockCapacityPolicy();
        _negotiations = new NegotiationSessionService(randomSource);
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

        TrainingAnnexRuntimeActor target = FindRecruitmentCandidate(roster);
        NegotiationDefinition negotiation = catalog.GetRequiredNegotiation(TrainingAnnexHostSupport.SteadySampleNegotiation);

        await _eventSink.PublishAsync(
            $"Negotiation opened: {negotiation.DisplayName}; target {target.Actor.Entity.DisplayName}; wallet {wallet.Macca} M.",
            cancellationToken).ConfigureAwait(false);

        HostCommandReadResult<CleanTrainingAnnexPlayCommand> targetSelection =
            await _commandSource.ReadAsync(
                CreateTargetMenu(target, party),
                cancellationToken).ConfigureAwait(false);
        if (!targetSelection.IsSelected || targetSelection.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync(
                "Negotiation canceled before contact; wallet and Demon stock are unchanged.",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexNegotiationInteractionResult(party, wallet, []);
        }

        commands.Add(targetSelection.Command);
        var negotiationCommands = new TrainingAnnexNegotiationCommandSource(_commandSource, commands);
        var negotiationEvents = new TrainingAnnexNegotiationEventSink(_eventSink);
        NegotiationSessionResult session = await _negotiations.RunAsync(
            BuildRequest(negotiation, target, roster.Player, party, wallet),
            negotiationCommands,
            negotiationEvents,
            cancellationToken).ConfigureAwait(false);

        if (session.Outcome != NegotiationOutcomeKind.Success)
        {
            await _eventSink.PublishAsync(
                $"Negotiation ended: {session.Outcome} ({session.Reason}); wallet and Demon stock are unchanged.",
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
        if (session.MaccaSpent > 0)
        {
            WalletTransactionResult spend = economy.SpendMacca(wallet, session.MaccaSpent);
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
            $"Recruitment applied: {target.Actor.Entity.DisplayName} joined Demon stock; wallet {wallet.Macca}->{nextWallet.Macca} M; Demon stock {party.DemonStock.Count}->{stock.After.DemonStock.Count}.",
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
            livingEnemyCount: 1,
            isMoonBlocked: false,
            isTargetAlreadyOwned: AlreadyOwnedByEntity(party, target.Actor.Entity.Id),
            hasOpenDemonStockSlot: HasOpenDemonStockSlot(party),
            currentMacca: wallet.Macca,
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

    private static TrainingAnnexRuntimeActor FindRecruitmentCandidate(TrainingAnnexActorRoster roster) =>
        roster.StockMembers.FirstOrDefault(actor =>
            actor.Actor.State.InstanceId == TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance) ??
        throw new InvalidOperationException("Training Annex recruitment candidate was not hydrated.");

    private static NegotiationRuntimeDemand MapDemand(NegotiationDemandDefinition demand)
    {
        if (demand.DemandId == TrainingAnnexHostSupport.SampleMaccaDemand)
        {
            return new NegotiationRuntimeDemand(
                demand.DemandId,
                NegotiationDemandKind.Macca,
                demand.Weight,
                maccaAmount: RequiredPositiveIntParameter(demand, "amount"));
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
        TrainingAnnexRuntimeActor target,
        RuntimePartyStockSnapshot party)
    {
        bool alreadyOwned = AlreadyOwnedByEntity(party, target.Actor.Entity.Id);
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Clean Negotiation",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.SelectNegotiationTarget,
                    alreadyOwned
                        ? $"{target.Actor.Entity.DisplayName} [Familiar]"
                        : target.Actor.Entity.DisplayName,
                    Description: alreadyOwned
                        ? "Runs the familiar-demon negotiation path without adding stock."
                        : "Starts a clean negotiation session for this recruitable sample.",
                    SelectionIdentity: HostCommandSelectionIdentity.ForRuntimeInstance(
                        target.Actor.State.InstanceId)),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);
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
            session.MaccaSpent,
            session.ItemSpentId,
            recruitment?.Status,
            recruitment?.ErrorCode,
            stock?.Code,
            beforeWallet.Macca,
            afterWallet.Macca,
            beforeParty.DemonStock.Count,
            afterParty.DemonStock.Count,
            recruited,
            session.Events.Count);

    private static int StatAsInt(RuntimeActorSnapshot actor, ContentId statId) =>
        (int)Math.Round(actor.Stats.EffectiveStats.GetValueOrDefault(statId), MidpointRounding.AwayFromZero);

    private sealed class TrainingAnnexNegotiationCommandSource(
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commands,
        ICollection<CleanTrainingAnnexPlayCommand> commandLog) : INegotiationCommandSource
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
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection = await commands.ReadAsync(
                new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
                    prompt.Prompt,
                    prompt.Options.Select(option => new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                            CleanTrainingAnnexPlayCommand.SelectNegotiationDemand,
                            option.Label,
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

    private sealed class TrainingAnnexNegotiationEventSink(IHostEventSink<string> events)
        : IHostEventSink<NegotiationEvent>
    {
        public ValueTask PublishAsync(
            NegotiationEvent value,
            CancellationToken cancellationToken = default) =>
            events.PublishAsync(
                $"Negotiation event: {value.Kind}; {value.Message}",
                cancellationToken);
    }
}
