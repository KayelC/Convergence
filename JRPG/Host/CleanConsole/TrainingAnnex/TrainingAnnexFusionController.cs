using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Inheritance;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexFusionResultEvidence(
    string ScenarioId,
    RuntimeInstanceId FirstParentInstanceId,
    ContentId FirstParentEntityId,
    RuntimeInstanceId SecondParentInstanceId,
    ContentId SecondParentEntityId,
    FusionRuntimeOperation Operation,
    ContentId? ResultEntityId,
    bool IsAccident,
    IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics);

internal sealed record TrainingAnnexFusionPlanningEvidence(
    string ScenarioId,
    ContentId? ResultEntityId,
    int MaximumInheritanceSlots,
    int SacrificialMaximumInheritanceSlots,
    IReadOnlyList<ContentId> NaturalSkillIds,
    IReadOnlyList<ContentId> PickableSkillIds,
    IReadOnlyList<FusionInheritanceEntry> DisplaySkills,
    IReadOnlyList<ContentId> AccidentInheritedSkillIds,
    ContentId MutationSourceSkillId,
    ContentId MutationResultSkillId);

internal sealed record TrainingAnnexFusionCalculationResult(
    IReadOnlyList<TrainingAnnexFusionResultEvidence> Results,
    IReadOnlyList<TrainingAnnexFusionPlanningEvidence> Planning);

internal sealed record TrainingAnnexFusionPreviewEvidence(
    string ScenarioId,
    ContentId? ResultEntityId,
    IReadOnlyList<ContentId> SelectedSkillIds,
    IReadOnlyList<FusionInheritanceSelectionDiagnostic> SelectionDiagnostics,
    FusionPreviewSnapshot? Preview,
    bool Confirmed,
    bool MutatedRuntimeState);

internal sealed record TrainingAnnexFusionTransactionEvidence(
    string ScenarioId,
    ContentId? ResultEntityId,
    RuntimeInstanceId? ResultInstanceId,
    IReadOnlyList<ContentId> SelectedSkillIds,
    IReadOnlyList<ContentId> ResultSkillIds,
    FusionTransactionAssessment? Assessment,
    IReadOnlyList<PartyStockTransitionResult> StockTransitions,
    bool Confirmed,
    bool Committed,
    bool MutatedRuntimeState,
    int DemonStockCountBefore,
    int DemonStockCountAfter);

internal sealed record TrainingAnnexFusionTransactionResult(
    RuntimePartyStockSnapshot PartyStock,
    TrainingAnnexRuntimeActor? ResultActor,
    TrainingAnnexFusionTransactionEvidence Evidence);

internal sealed class TrainingAnnexFusionController
{
    private readonly IHostEventSink<string> _eventSink;

    public TrainingAnnexFusionController(IHostEventSink<string> eventSink)
    {
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public async ValueTask<TrainingAnnexFusionCalculationResult> CalculateAsync(
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(roster);

        var repository = new CatalogFusionContentRepository(catalog);
        var resolver = new FusionResultResolver(repository, new TrainingAnnexFusionRandomSource());
        TrainingAnnexRuntimeActor ashling = FindActor(roster, TrainingAnnexHostSupport.DemonAshlingInstance);
        TrainingAnnexRuntimeActor bramble = FindActor(roster, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance);

        TrainingAnnexFusionResultEvidence direct = await ResolveAsync(
            catalog,
            resolver,
            "direct_entity_result",
            ashling,
            bramble,
            cancellationToken).ConfigureAwait(false);
        TrainingAnnexFusionResultEvidence rank = await ResolveAsync(
            catalog,
            resolver,
            "race_rank_offset_result",
            roster.Player,
            bramble,
            cancellationToken).ConfigureAwait(false);

        TrainingAnnexFusionPlanningEvidence planning = await PlanAsync(
            catalog,
            repository,
            roster.Player,
            bramble,
            ashling,
            cancellationToken).ConfigureAwait(false);

        return new TrainingAnnexFusionCalculationResult(
            Array.AsReadOnly([direct, rank]),
            Array.AsReadOnly([planning]));
    }

    public async ValueTask<TrainingAnnexFusionPreviewEvidence?> PreviewAsync(
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(commandSource);
        ArgumentNullException.ThrowIfNull(commands);

        var repository = new CatalogFusionContentRepository(catalog);
        var resolver = new FusionResultResolver(repository, new TrainingAnnexFusionRandomSource());
        var planner = new FusionPlanningService(
            repository,
            resolver,
            new TrainingAnnexFusionAccidentRandomSource());

        TrainingAnnexRuntimeActor first = roster.Player;
        TrainingAnnexRuntimeActor second = FindActor(roster, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance);
        TrainingAnnexRuntimeActor sacrifice = FindActor(roster, TrainingAnnexHostSupport.DemonAshlingInstance);
        FusionParticipantSnapshot firstParent = ToFusionParticipant(first);
        FusionParticipantSnapshot secondParent = ToFusionParticipant(second);
        FusionParticipantSnapshot sacrificeParent = ToFusionParticipant(sacrifice);

        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            firstParent,
            secondParent,
            sacrificeParent,
            IsSacrificial: true,
            MoonPhase: 0));
        if (!plan.IsSuccessful || plan.ResultEntity is null)
        {
            await _eventSink.PublishAsync(
                "Fusion preview rejected: no successful plan was available.",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexFusionPreviewEvidence(
                "sacrificial_preview_confirmation",
                null,
                [],
                [],
                null,
                Confirmed: false,
                MutatedRuntimeState: false);
        }

        IReadOnlyList<ContentId>? selectedSkillIds = await SelectInheritedSkillsAsync(
            catalog,
            commandSource,
            commands,
            plan,
            cancellationToken).ConfigureAwait(false);
        if (selectedSkillIds is null)
        {
            await _eventSink.PublishAsync(
                "Fusion preview canceled before validation.",
                cancellationToken).ConfigureAwait(false);
            return null;
        }

        FusionInheritancePlan selectionPlan = CreateSelectionPlan(
            repository,
            plan,
            firstParent,
            secondParent,
            sacrificeParent);
        FusionInheritanceSelectionResult selection =
            new FusionInheritanceSelectionValidator().Validate(selectionPlan, selectedSkillIds);
        if (!selection.IsValid)
        {
            foreach (FusionInheritanceSelectionDiagnostic diagnostic in selection.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"Fusion preview selection rejected [{diagnostic.Code}]: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return new TrainingAnnexFusionPreviewEvidence(
                "sacrificial_preview_confirmation",
                plan.ResultEntity.Id,
                selectedSkillIds,
                selection.Diagnostics,
                null,
                Confirmed: false,
                MutatedRuntimeState: false);
        }

        ValidatedFusionInheritanceSelection validSelection = selection.RequireValidSelection();
        FusionPreviewSnapshot? preview = new FusionPreviewService().CreatePreview(new FusionPreviewRequest(
            plan,
            validSelection.SelectedSkillIds));
        if (preview is null)
        {
            await _eventSink.PublishAsync(
                "Fusion preview rejected: preview service produced no result.",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexFusionPreviewEvidence(
                "sacrificial_preview_confirmation",
                plan.ResultEntity.Id,
                validSelection.SelectedSkillIds,
                selection.Diagnostics,
                null,
                Confirmed: false,
                MutatedRuntimeState: false);
        }

        await _eventSink.PublishAsync(
            FormatPreview(catalog, preview),
            cancellationToken).ConfigureAwait(false);

        HostCommandReadResult<CleanTrainingAnnexPlayCommand> confirmation =
            await commandSource.ReadAsync(
                CreateFusionPreviewConfirmationMenu(preview),
                cancellationToken).ConfigureAwait(false);
        if (!confirmation.IsSelected || confirmation.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync(
                "Fusion preview canceled at confirmation. No runtime state was mutated.",
                cancellationToken).ConfigureAwait(false);
            return new TrainingAnnexFusionPreviewEvidence(
                "sacrificial_preview_confirmation",
                plan.ResultEntity.Id,
                validSelection.SelectedSkillIds,
                selection.Diagnostics,
                preview,
                Confirmed: false,
                MutatedRuntimeState: false);
        }

        commands.Add(confirmation.Command);
        await _eventSink.PublishAsync(
            $"Fusion preview confirmed: {preview.DisplayName} with inherited {FormatSkillNames(catalog, preview.InheritedSkillIds)}. No runtime state was mutated.",
            cancellationToken).ConfigureAwait(false);
        return new TrainingAnnexFusionPreviewEvidence(
            "sacrificial_preview_confirmation",
            plan.ResultEntity.Id,
            validSelection.SelectedSkillIds,
            selection.Diagnostics,
            preview,
            Confirmed: true,
            MutatedRuntimeState: false);
    }

    public async ValueTask<TrainingAnnexFusionTransactionResult> CommitAsync(
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        RuntimePartyStockSnapshot partyStock,
        ICatalogBattleActorFactory actorFactory,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(partyStock);
        ArgumentNullException.ThrowIfNull(actorFactory);
        ArgumentNullException.ThrowIfNull(commandSource);
        ArgumentNullException.ThrowIfNull(commands);

        const string scenarioId = "direct_transaction_commit";
        var repository = new CatalogFusionContentRepository(catalog);
        var resolver = new FusionResultResolver(repository, new TrainingAnnexFusionRandomSource());
        var planner = new FusionPlanningService(
            repository,
            resolver,
            new TrainingAnnexFusionNoAccidentRandomSource());
        FusionParticipantSnapshot firstParent = ToFusionParticipant(
            FindActor(roster, TrainingAnnexHostSupport.DemonAshlingInstance));
        FusionParticipantSnapshot secondParent = ToFusionParticipant(
            FindActor(roster, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance));
        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            firstParent,
            secondParent,
            Sacrifice: null,
            IsSacrificial: false,
            MoonPhase: 0));

        if (!plan.IsSuccessful || plan.ResultEntity is null)
        {
            await _eventSink.PublishAsync(
                "Fusion transaction rejected: no successful plan was available.",
                cancellationToken).ConfigureAwait(false);
            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                null,
                null,
                [],
                [],
                null,
                [],
                confirmed: false,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        FusionTransactionAssessment availabilityAssessment = AssessTransaction(
            partyStock,
            plan,
            []);
        if (!availabilityAssessment.CanCommit)
        {
            await PublishTransactionDiagnosticsAsync(availabilityAssessment, cancellationToken)
                .ConfigureAwait(false);
            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                plan.ResultEntity.Id,
                null,
                [],
                [],
                availabilityAssessment,
                [],
                confirmed: false,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        IReadOnlyList<ContentId>? selectedSkillIds = await SelectInheritedSkillsAsync(
            catalog,
            commandSource,
            commands,
            plan,
            cancellationToken).ConfigureAwait(false);
        if (selectedSkillIds is null)
        {
            await _eventSink.PublishAsync(
                "Fusion transaction canceled before validation. No runtime state was mutated.",
                cancellationToken).ConfigureAwait(false);
            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                plan.ResultEntity.Id,
                null,
                [],
                [],
                availabilityAssessment,
                [],
                confirmed: false,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        FusionInheritancePlan selectionPlan = CreateSelectionPlan(
            repository,
            plan,
            firstParent,
            secondParent);
        FusionInheritanceSelectionResult selection =
            new FusionInheritanceSelectionValidator().Validate(selectionPlan, selectedSkillIds);
        if (!selection.IsValid)
        {
            foreach (FusionInheritanceSelectionDiagnostic diagnostic in selection.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"Fusion transaction selection rejected [{diagnostic.Code}]: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                plan.ResultEntity.Id,
                null,
                selectedSkillIds,
                [],
                availabilityAssessment,
                [],
                confirmed: false,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        ValidatedFusionInheritanceSelection validSelection = selection.RequireValidSelection();
        FusionTransactionAssessment finalAssessment = AssessTransaction(
            partyStock,
            plan,
            validSelection.SelectedSkillIds);
        if (!finalAssessment.CanCommit)
        {
            await PublishTransactionDiagnosticsAsync(finalAssessment, cancellationToken)
                .ConfigureAwait(false);
            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                plan.ResultEntity.Id,
                null,
                validSelection.SelectedSkillIds,
                [],
                finalAssessment,
                [],
                confirmed: false,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        FusionPreviewSnapshot? preview = new FusionPreviewService().CreatePreview(new FusionPreviewRequest(
            plan,
            validSelection.SelectedSkillIds));
        if (preview is null)
        {
            await _eventSink.PublishAsync(
                "Fusion transaction rejected: preview service produced no result.",
                cancellationToken).ConfigureAwait(false);
            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                plan.ResultEntity.Id,
                null,
                validSelection.SelectedSkillIds,
                [],
                finalAssessment,
                [],
                confirmed: false,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        await _eventSink.PublishAsync(
            FormatPreview(catalog, preview),
            cancellationToken).ConfigureAwait(false);

        HostCommandReadResult<CleanTrainingAnnexPlayCommand> confirmation =
            await commandSource.ReadAsync(
                CreateFusionTransactionConfirmationMenu(preview),
                cancellationToken).ConfigureAwait(false);
        if (!confirmation.IsSelected || confirmation.Command == CleanTrainingAnnexPlayCommand.Back)
        {
            commands.Add(CleanTrainingAnnexPlayCommand.Back);
            await _eventSink.PublishAsync(
                "Fusion transaction canceled at confirmation. No runtime state was mutated.",
                cancellationToken).ConfigureAwait(false);
            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                plan.ResultEntity.Id,
                null,
                validSelection.SelectedSkillIds,
                ResultSkillIds(preview),
                finalAssessment,
                [],
                confirmed: false,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        commands.Add(confirmation.Command);
        TrainingAnnexRuntimeActor resultActor = CreateResultActor(
            roster,
            partyStock,
            actorFactory,
            preview);
        RuntimeActorReferenceSnapshot resultReference = TrainingAnnexHostSupport.Reference(resultActor);
        var transitions = new List<PartyStockTransitionResult>();
        RuntimePartyStockSnapshot current = partyStock;
        var partyTransitions = new PartyStockTransitionService();
        foreach (RuntimeInstanceId participantId in finalAssessment.ConsumedParticipantIds)
        {
            PartyStockTransitionResult consumed = partyTransitions.ConsumeDemon(
                new ConsumeDemonRequest(current, participantId));
            transitions.Add(consumed);
            if (!consumed.Applied)
            {
                await PublishStockDiagnosticsAsync(consumed, cancellationToken).ConfigureAwait(false);
                return TransactionResult(
                    partyStock,
                    null,
                    scenarioId,
                    plan.ResultEntity.Id,
                    resultActor.Actor.State.InstanceId,
                    validSelection.SelectedSkillIds,
                    ResultSkillIds(preview),
                    finalAssessment,
                    transitions,
                    confirmed: true,
                    committed: false,
                    mutated: false,
                    partyStock.DemonStock.Count);
            }

            current = consumed.After;
        }

        PartyStockTransitionResult added = partyTransitions.AddDemonToStock(
            new AddDemonToStockRequest(current, resultReference));
        transitions.Add(added);
        if (!added.Applied)
        {
            await PublishStockDiagnosticsAsync(added, cancellationToken).ConfigureAwait(false);
            return TransactionResult(
                partyStock,
                null,
                scenarioId,
                plan.ResultEntity.Id,
                resultActor.Actor.State.InstanceId,
                validSelection.SelectedSkillIds,
                ResultSkillIds(preview),
                finalAssessment,
                transitions,
                confirmed: true,
                committed: false,
                mutated: false,
                partyStock.DemonStock.Count);
        }

        current = added.After;
        await _eventSink.PublishAsync(
            $"Fusion transaction committed: {FormatParent(firstParent)} + {FormatParent(secondParent)} -> {preview.DisplayName}; consumed {FormatInstances(finalAssessment.ConsumedParticipantIds)}; added {resultActor.Actor.State.InstanceId}; Demon stock {partyStock.DemonStock.Count}->{current.DemonStock.Count}.",
            cancellationToken).ConfigureAwait(false);
        return TransactionResult(
            current,
            resultActor,
            scenarioId,
            plan.ResultEntity.Id,
            resultActor.Actor.State.InstanceId,
            validSelection.SelectedSkillIds,
            ResultSkillIds(preview),
            finalAssessment,
            transitions,
            confirmed: true,
            committed: true,
            mutated: true,
            partyStock.DemonStock.Count);
    }

    private async ValueTask<TrainingAnnexFusionResultEvidence> ResolveAsync(
        GameDataCatalog catalog,
        IFusionResultResolver resolver,
        string scenarioId,
        TrainingAnnexRuntimeActor first,
        TrainingAnnexRuntimeActor second,
        CancellationToken cancellationToken)
    {
        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            ToFusionParticipant(first),
            ToFusionParticipant(second),
            MoonPhase: 0));
        var evidence = new TrainingAnnexFusionResultEvidence(
            scenarioId,
            first.Actor.State.InstanceId,
            first.Actor.Entity.Id,
            second.Actor.State.InstanceId,
            second.Actor.Entity.Id,
            result.Operation,
            result.ResultEntityId,
            result.IsAccident,
            result.Diagnostics);

        await _eventSink.PublishAsync(
            FormatResult(catalog, first, second, evidence),
            cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    private async ValueTask<TrainingAnnexFusionPlanningEvidence> PlanAsync(
        GameDataCatalog catalog,
        IFusionContentRepository repository,
        TrainingAnnexRuntimeActor first,
        TrainingAnnexRuntimeActor second,
        TrainingAnnexRuntimeActor sacrifice,
        CancellationToken cancellationToken)
    {
        var resolver = new FusionResultResolver(repository, new TrainingAnnexFusionRandomSource());
        var planner = new FusionPlanningService(
            repository,
            resolver,
            new TrainingAnnexFusionAccidentRandomSource());
        FusionParticipantSnapshot firstParent = ToFusionParticipant(first);
        FusionParticipantSnapshot secondParent = ToFusionParticipant(second);
        FusionParticipantSnapshot sacrificeParent = ToFusionParticipant(sacrifice);

        FusionPlanningResult basePlan = planner.CreatePlan(new FusionPlanningRequest(
            firstParent,
            secondParent,
            Sacrifice: null,
            IsSacrificial: false,
            MoonPhase: 0));
        FusionPlanningResult sacrificialPlan = planner.CreatePlan(new FusionPlanningRequest(
            firstParent,
            secondParent,
            sacrificeParent,
            IsSacrificial: true,
            MoonPhase: 0));

        IReadOnlyList<ContentId> accidentInheritedSkillIds =
            planner.CreateAccidentInheritance([TrainingAnnexHostSupport.EchoStrike], maximumSlots: 1);
        ContentId mutationResult = accidentInheritedSkillIds.Count == 0
            ? TrainingAnnexHostSupport.EchoStrike
            : accidentInheritedSkillIds[0];
        var evidence = new TrainingAnnexFusionPlanningEvidence(
            "inheritance_slots_mutation_accident",
            basePlan.ResultEntity?.Id,
            basePlan.MaximumInheritanceSlots,
            sacrificialPlan.MaximumInheritanceSlots,
            basePlan.NaturalSkillIds,
            basePlan.PickableSkillIds,
            basePlan.DisplaySkills,
            accidentInheritedSkillIds,
            TrainingAnnexHostSupport.EchoStrike,
            mutationResult);

        await _eventSink.PublishAsync(
            FormatPlanning(catalog, evidence),
            cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    private async ValueTask<IReadOnlyList<ContentId>?> SelectInheritedSkillsAsync(
        GameDataCatalog catalog,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource,
        ICollection<CleanTrainingAnnexPlayCommand> commands,
        FusionPlanningResult plan,
        CancellationToken cancellationToken)
    {
        var selected = new List<ContentId>();
        while (true)
        {
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> selection =
                await commandSource.ReadAsync(
                    CreateFusionInheritanceMenu(catalog, plan, selected),
                    cancellationToken).ConfigureAwait(false);
            if (!selection.IsSelected || selection.Command == CleanTrainingAnnexPlayCommand.Back)
            {
                commands.Add(CleanTrainingAnnexPlayCommand.Back);
                return null;
            }

            commands.Add(selection.Command);
            if (selection.Command == CleanTrainingAnnexPlayCommand.BuildFusionPreview)
            {
                return selected.ToArray();
            }

            if (selection.Command != CleanTrainingAnnexPlayCommand.SelectFusionInheritedSkill ||
                selection.SelectionIdentity?.ContentId is not ContentId skillId)
            {
                continue;
            }

            if (!selected.Contains(skillId))
            {
                selected.Add(skillId);
                await _eventSink.PublishAsync(
                    $"Fusion inheritance selected: {SkillName(catalog, skillId)}.",
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static FusionInheritancePlan CreateSelectionPlan(
        IFusionContentRepository repository,
        FusionPlanningResult plan,
        params FusionParticipantSnapshot?[] participants)
    {
        var candidates = new List<SkillDefinition>();
        var seen = new HashSet<ContentId>();
        foreach (FusionParticipantSnapshot? participant in participants)
        {
            if (participant is null)
            {
                continue;
            }

            foreach (ContentId skillId in participant.SkillIds)
            {
                if (seen.Add(skillId) &&
                    repository.TryGetSkill(skillId, out SkillDefinition? skill) &&
                    skill is not null)
                {
                    candidates.Add(skill);
                }
            }
        }

        return new FusionInheritancePlanner().CreatePlan(new FusionInheritancePlanRequest(
            plan.ResultEntity!.Definition,
            candidates,
            plan.NaturalSkillIds,
            plan.MaximumInheritanceSlots));
    }

    private static FusionParticipantSnapshot ToFusionParticipant(TrainingAnnexRuntimeActor actor) =>
        new(
            actor.Actor.State.InstanceId,
            actor.Actor.Entity.Id,
            actor.Actor.Entity.DisplayName,
            actor.Actor.Entity.RaceId,
            actor.Actor.Entity.Rank,
            actor.Level,
            actor.Actor.SkillLoadout.Select(skill => skill.Id),
            actor.Actor.Entity.Stats);

    private static string FormatResult(
        GameDataCatalog catalog,
        TrainingAnnexRuntimeActor first,
        TrainingAnnexRuntimeActor second,
        TrainingAnnexFusionResultEvidence evidence)
    {
        if (evidence.ResultEntityId is ContentId resultId &&
            catalog.TryGetEntity(resultId, out EntityDefinition? resultEntity) &&
            resultEntity is not null)
        {
            return "Fusion result: "
                + $"{first.Actor.Entity.DisplayName} + {second.Actor.Entity.DisplayName} -> "
                + $"{resultEntity.DisplayName} ({FormatOperation(evidence.Operation)}; {evidence.ScenarioId}).";
        }

        string diagnostics = evidence.Diagnostics.Count == 0
            ? "no result"
            : string.Join(", ", evidence.Diagnostics.Select(diagnostic => diagnostic.Code.ToString()));
        return "Fusion result: "
            + $"{first.Actor.Entity.DisplayName} + {second.Actor.Entity.DisplayName} failed "
            + $"({evidence.ScenarioId}; {diagnostics}).";
    }

    private static string FormatPlanning(
        GameDataCatalog catalog,
        TrainingAnnexFusionPlanningEvidence evidence)
    {
        string resultName = evidence.ResultEntityId is ContentId resultId &&
            catalog.TryGetEntity(resultId, out EntityDefinition? resultEntity) &&
            resultEntity is not null
                ? resultEntity.DisplayName
                : "no result";
        string pickable = FormatSkillNames(catalog, evidence.PickableSkillIds);
        string blocked = FormatBlockedSkills(catalog, evidence.DisplaySkills);
        string mutationSource = SkillName(catalog, evidence.MutationSourceSkillId);
        string mutationResult = SkillName(catalog, evidence.MutationResultSkillId);
        return "Fusion planning: "
            + $"{resultName}; slots {evidence.MaximumInheritanceSlots}, "
            + $"sacrificial slots {evidence.SacrificialMaximumInheritanceSlots}; "
            + $"pickable {pickable}; blocked {blocked}; "
            + $"accident sample {mutationSource} -> {mutationResult}.";
    }

    private static string FormatPreview(GameDataCatalog catalog, FusionPreviewSnapshot preview)
    {
        string natural = FormatSkillNames(catalog, preview.NaturalSkillIds);
        string inherited = FormatSkillNames(catalog, preview.InheritedSkillIds);
        string stats = string.Join(
            ", ",
            preview.Stats
                .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                .Select(pair => $"{pair.Key} {pair.Value}"));
        return "Fusion preview: "
            + $"{preview.DisplayName}; level {preview.Level}; "
            + $"natural {natural}; inherited {inherited}; stats {stats}.";
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateFusionInheritanceMenu(
        GameDataCatalog catalog,
        FusionPlanningResult plan,
        IReadOnlyList<ContentId> selectedSkillIds)
    {
        var options = new List<HostCommandOption<CleanTrainingAnnexPlayCommand>>();
        foreach (FusionInheritanceEntry entry in plan.DisplaySkills)
        {
            bool alreadySelected = selectedSkillIds.Contains(entry.SkillId);
            bool enabled = entry.IsSelectable && !alreadySelected;
            string suffix = alreadySelected
                ? " [Selected]"
                : entry.IsSelectable
                    ? string.Empty
                    : $" [{entry.ReasonCode}]";
            options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.SelectFusionInheritedSkill,
                SkillName(catalog, entry.SkillId) + suffix,
                enabled,
                entry.ReasonCode,
                HostCommandSelectionIdentity.ForContent(entry.SkillId)));
        }

        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.BuildFusionPreview,
            $"Build Preview ({selectedSkillIds.Count}/{plan.MaximumInheritanceSlots})"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.Back,
            "Back"));
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            "Select Inherited Skills",
            options);
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateFusionPreviewConfirmationMenu(
        FusionPreviewSnapshot preview) =>
        new(
            $"Confirm preview for {preview.DisplayName}?",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ConfirmFusionPreview,
                    "Confirm Preview"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateFusionTransactionConfirmationMenu(
        FusionPreviewSnapshot preview) =>
        new(
            $"Commit fusion for {preview.DisplayName}?",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ConfirmFusionTransaction,
                    "Commit Fusion"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Back,
                    "Back")
            ]);

    private FusionTransactionAssessment AssessTransaction(
        RuntimePartyStockSnapshot partyStock,
        FusionPlanningResult plan,
        IReadOnlyList<ContentId> selectedSkillIds) =>
        new FusionTransactionService().Assess(new FusionTransactionRequest(
            FusionParticipantStockKind.Demon,
            plan,
            selectedSkillIds,
            ResultAlreadyOwned: plan.ResultEntity is not null &&
                OwnsEntity(partyStock, plan.ResultEntity.Id),
            HasOpenStockSlot: HasOpenStockSlot(partyStock, plan)));

    private static bool OwnsEntity(RuntimePartyStockSnapshot partyStock, ContentId entityId) =>
        partyStock.DemonStock
            .Concat(partyStock.ActiveParty)
            .Any(actor => actor.EntityDefinitionId == entityId);

    private static bool HasOpenStockSlot(RuntimePartyStockSnapshot partyStock, FusionPlanningResult plan)
    {
        if (plan.Result.Operation != FusionRuntimeOperation.CreateNewEntity)
        {
            return true;
        }

        RuntimeInstanceId[] consumedIds = new[]
        {
            plan.FirstParent,
            plan.SecondParent,
            plan.Sacrifice
        }
            .OfType<FusionParticipantSnapshot>()
            .Select(parent => parent.InstanceId)
            .Distinct()
            .ToArray();
        int consumedStockEntries = consumedIds
            .Count(id => partyStock.DemonStock.Any(demon => demon.InstanceId == id));
        int projected = partyStock.DemonStock.Count - consumedStockEntries + 1;
        return projected <= new LegacyStockCapacityPolicy().GetCapacity(partyStock.OwnerLevel);
    }

    private TrainingAnnexRuntimeActor CreateResultActor(
        TrainingAnnexActorRoster roster,
        RuntimePartyStockSnapshot partyStock,
        ICatalogBattleActorFactory actorFactory,
        FusionPreviewSnapshot preview)
    {
        RuntimeInstanceId instanceId = GenerateFusionInstanceId(roster, partyStock, preview.EntityId);
        CatalogBattleActorCreationResult created = actorFactory.Create(new CatalogBattleActorCreationRequest(
            preview.EntityId,
            instanceId,
            TrainingAnnexHostSupport.PlayerTeam,
            preview.Level,
            new RuntimeProgressionSnapshot(
                preview.Level,
                preview.Experience,
                preview.LifetimeExperience,
                0),
            ControllerId: ContentId.Parse("clean_training_annex"),
            Deployment: RuntimeActorDeployment.Reserve,
            IsActive: false));
        if (!created.IsSuccess)
        {
            string diagnostics = string.Join("; ", created.Diagnostics.Select(diagnostic => diagnostic.Message));
            throw new InvalidOperationException($"Fusion result actor creation failed: {diagnostics}");
        }

        CatalogBattleActor actor = created.RequireActor();
        RuntimeActorSnapshot snapshot = actor.State.ToSnapshot();
        RuntimeActorSnapshot withPreviewSkills = new(
            snapshot.Identity,
            snapshot.Ownership,
            snapshot.Deployment,
            snapshot.Progression,
            snapshot.Resources,
            snapshot.Stats,
            new RuntimeSkillStateSnapshot(ResultSkillIds(preview), ResultSkillIds(preview)),
            snapshot.Forms,
            snapshot.Equipment,
            snapshot.BattleStatus,
            snapshot.BattleActivations,
            snapshot.BaseResourceValues,
            snapshot.VitalResourceId,
            snapshot.CapabilityIds);
        CatalogBattleActorCreationResult restored = actorFactory.Restore(withPreviewSkills);
        if (!restored.IsSuccess)
        {
            string diagnostics = string.Join("; ", restored.Diagnostics.Select(diagnostic => diagnostic.Message));
            throw new InvalidOperationException($"Fusion result actor restore failed: {diagnostics}");
        }

        return new TrainingAnnexRuntimeActor("Fused Result", restored.RequireActor());
    }

    private static RuntimeInstanceId GenerateFusionInstanceId(
        TrainingAnnexActorRoster roster,
        RuntimePartyStockSnapshot partyStock,
        ContentId entityId)
    {
        string local = entityId.ToString();
        int colon = local.LastIndexOf(':');
        if (colon >= 0)
        {
            local = local[(colon + 1)..];
        }

        var used = new HashSet<RuntimeInstanceId>(
            roster.AllActors.Select(actor => actor.Actor.State.InstanceId)
                .Concat(partyStock.ActiveParty.Select(actor => actor.InstanceId))
                .Concat(partyStock.ReserveMembers.Select(actor => actor.InstanceId))
                .Concat(partyStock.PersonaStock.Select(actor => actor.InstanceId))
                .Concat(partyStock.DemonStock.Select(actor => actor.InstanceId)));
        int index = 1;
        while (true)
        {
            RuntimeInstanceId candidate = RuntimeInstanceId.Parse($"fusion_{local}_{index}");
            if (!used.Contains(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static IReadOnlyList<ContentId> ResultSkillIds(FusionPreviewSnapshot preview) =>
        preview.NaturalSkillIds
            .Concat(preview.InheritedSkillIds)
            .Distinct()
            .ToArray();

    private async ValueTask PublishTransactionDiagnosticsAsync(
        FusionTransactionAssessment assessment,
        CancellationToken cancellationToken)
    {
        foreach (FusionRuntimeDiagnostic diagnostic in assessment.Diagnostics)
        {
            await _eventSink.PublishAsync(
                $"Fusion transaction rejected [{diagnostic.Code}]: {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask PublishStockDiagnosticsAsync(
        PartyStockTransitionResult result,
        CancellationToken cancellationToken)
    {
        foreach (PartyStockTransitionDiagnostic diagnostic in result.Diagnostics)
        {
            await _eventSink.PublishAsync(
                $"Fusion stock transition rejected [{diagnostic.Code}]: {diagnostic.Message}",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static TrainingAnnexFusionTransactionResult TransactionResult(
        RuntimePartyStockSnapshot partyStock,
        TrainingAnnexRuntimeActor? resultActor,
        string scenarioId,
        ContentId? resultEntityId,
        RuntimeInstanceId? resultInstanceId,
        IReadOnlyList<ContentId> selectedSkillIds,
        IReadOnlyList<ContentId> resultSkillIds,
        FusionTransactionAssessment? assessment,
        IReadOnlyList<PartyStockTransitionResult> stockTransitions,
        bool confirmed,
        bool committed,
        bool mutated,
        int demonStockCountBefore) =>
        new(
            partyStock,
            resultActor,
            new TrainingAnnexFusionTransactionEvidence(
                scenarioId,
                resultEntityId,
                resultInstanceId,
                selectedSkillIds.ToArray(),
                resultSkillIds.ToArray(),
                assessment,
                stockTransitions.ToArray(),
                confirmed,
                committed,
                mutated,
                demonStockCountBefore,
                partyStock.DemonStock.Count));

    private static string FormatSkillNames(GameDataCatalog catalog, IReadOnlyList<ContentId> skillIds) =>
        skillIds.Count == 0
            ? "none"
            : string.Join(", ", skillIds.Select(id => SkillName(catalog, id)));

    private static string FormatBlockedSkills(
        GameDataCatalog catalog,
        IReadOnlyList<FusionInheritanceEntry> displaySkills)
    {
        string[] blocked = displaySkills
            .Where(entry => !entry.IsSelectable)
            .Select(entry => $"{SkillName(catalog, entry.SkillId)}:{entry.ReasonCode}")
            .ToArray();
        return blocked.Length == 0 ? "none" : string.Join(", ", blocked);
    }

    private static string SkillName(GameDataCatalog catalog, ContentId skillId) =>
        catalog.TryGetSkill(skillId, out SkillDefinition? skill) && skill is not null
            ? skill.DisplayName
            : skillId.ToString();

    private static string FormatOperation(FusionRuntimeOperation operation) =>
        operation switch
        {
            FusionRuntimeOperation.CreateNewEntity => "create_entity",
            FusionRuntimeOperation.RankUpParent => "rank_up",
            FusionRuntimeOperation.RankDownParent => "rank_down",
            FusionRuntimeOperation.StatBoost => "stat_boost",
            _ => "no_fusion"
        };

    private static string FormatParent(FusionParticipantSnapshot parent) =>
        $"{parent.DisplayName} ({parent.InstanceId})";

    private static string FormatInstances(IReadOnlyList<RuntimeInstanceId> instanceIds) =>
        instanceIds.Count == 0
            ? "none"
            : string.Join(", ", instanceIds.Select(id => id.ToString()));

    private static TrainingAnnexRuntimeActor FindActor(
        TrainingAnnexActorRoster roster,
        RuntimeInstanceId instanceId) =>
        roster.AllActors.First(actor => actor.Actor.State.InstanceId == instanceId);

    private sealed class TrainingAnnexFusionRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => maximumExclusive - 1;
        public decimal NextUnitDecimal() => 0.99m;
    }

    private sealed class TrainingAnnexFusionAccidentRandomSource : IRandomSource
    {
        private readonly Queue<int> _values = new([0, 0, 0]);

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            int value = _values.Count == 0 ? minimumInclusive : _values.Dequeue();
            return Math.Clamp(value, minimumInclusive, maximumExclusive - 1);
        }

        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class TrainingAnnexFusionNoAccidentRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        public decimal NextUnitDecimal() => 0.99m;
    }
}
