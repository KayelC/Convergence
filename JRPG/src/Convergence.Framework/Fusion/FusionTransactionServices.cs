using Convergence.Content;
using Convergence.Encounters;
using Convergence.Inheritance;
using Convergence.Runtime;

namespace Convergence.Fusion;

public sealed record FusionTransactionPreparationRequest
{
    public FusionTransactionPreparationRequest(
        FusionParticipantStockKind ownerKind,
        FusionPlanningResult plan,
        ValidatedFusionInheritanceSelection inheritanceSelection,
        RuntimePartyStockSnapshot partyStock,
        RuntimeInstanceId proposedResultInstanceId,
        ContentId resultTeamId,
        ContentId? resultControllerId = null,
        RuntimeActorSnapshot? existingResultActor = null)
    {
        if (!Enum.IsDefined(ownerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(ownerKind));
        }

        OwnerKind = ownerKind;
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        InheritanceSelection = inheritanceSelection ?? throw new ArgumentNullException(nameof(inheritanceSelection));
        PartyStock = partyStock ?? throw new ArgumentNullException(nameof(partyStock));
        ProposedResultInstanceId = proposedResultInstanceId;
        ResultTeamId = resultTeamId;
        ResultControllerId = resultControllerId;
        ExistingResultActor = existingResultActor;
    }

    public FusionParticipantStockKind OwnerKind { get; }
    public FusionPlanningResult Plan { get; }
    public ValidatedFusionInheritanceSelection InheritanceSelection { get; }
    public RuntimePartyStockSnapshot PartyStock { get; }
    public RuntimeInstanceId ProposedResultInstanceId { get; }
    public ContentId ResultTeamId { get; }
    public ContentId? ResultControllerId { get; }
    public RuntimeActorSnapshot? ExistingResultActor { get; }
}

public sealed record PreparedFusionTransaction
{
    internal PreparedFusionTransaction(
        FusionParticipantStockKind ownerKind,
        FusionPlanningResult plan,
        ValidatedFusionInheritanceSelection inheritanceSelection,
        FusionPreviewSnapshot preview,
        RuntimeInstanceId resultInstanceId,
        ContentId resultTeamId,
        ContentId? resultControllerId,
        RuntimeActorSnapshot? existingResultActor,
        RuntimePartyStockSnapshot beforePartyStock,
        RuntimePartyStockSnapshot afterPartyStock,
        IEnumerable<RuntimeInstanceId> consumedParticipantIds,
        IEnumerable<ContentId> resultLearnedSkillIds,
        IEnumerable<ContentId> resultEquippedSkillIds,
        IEnumerable<PartyStockTransitionResult> stockTransitions)
    {
        OwnerKind = ownerKind;
        Plan = plan;
        InheritanceSelection = inheritanceSelection;
        Preview = preview;
        ResultInstanceId = resultInstanceId;
        ResultTeamId = resultTeamId;
        ResultControllerId = resultControllerId;
        ExistingResultActor = existingResultActor;
        BeforePartyStock = beforePartyStock;
        AfterPartyStock = afterPartyStock;
        ConsumedParticipantIds = Array.AsReadOnly(consumedParticipantIds.ToArray());
        ResultLearnedSkillIds = Array.AsReadOnly(resultLearnedSkillIds.ToArray());
        ResultEquippedSkillIds = Array.AsReadOnly(resultEquippedSkillIds.ToArray());
        StockTransitions = Array.AsReadOnly(stockTransitions.ToArray());
    }

    public FusionParticipantStockKind OwnerKind { get; }
    public FusionPlanningResult Plan { get; }
    public ValidatedFusionInheritanceSelection InheritanceSelection { get; }
    public FusionPreviewSnapshot Preview { get; }
    public RuntimeInstanceId ResultInstanceId { get; }
    public ContentId ResultTeamId { get; }
    public ContentId? ResultControllerId { get; }
    public RuntimeActorSnapshot? ExistingResultActor { get; }
    public RuntimePartyStockSnapshot BeforePartyStock { get; }
    public RuntimePartyStockSnapshot AfterPartyStock { get; }
    public IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds { get; }
    public IReadOnlyList<ContentId> ResultLearnedSkillIds { get; }
    public IReadOnlyList<ContentId> ResultEquippedSkillIds { get; }
    public IReadOnlyList<PartyStockTransitionResult> StockTransitions { get; }
}

public sealed record FusionTransactionAssessment
{
    internal FusionTransactionAssessment(
        PreparedFusionTransaction? preparedTransaction,
        RuntimePartyStockSnapshot beforePartyStock,
        RuntimePartyStockSnapshot afterPartyStock,
        ContentId? resultEntityId,
        IEnumerable<RuntimeInstanceId>? consumedParticipantIds = null,
        IEnumerable<PartyStockTransitionResult>? stockTransitions = null,
        IEnumerable<FusionRuntimeDiagnostic>? diagnostics = null)
    {
        PreparedTransaction = preparedTransaction;
        BeforePartyStock = beforePartyStock;
        AfterPartyStock = afterPartyStock;
        ResultEntityId = resultEntityId;
        ConsumedParticipantIds = Array.AsReadOnly((consumedParticipantIds ?? []).ToArray());
        StockTransitions = Array.AsReadOnly((stockTransitions ?? []).ToArray());
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public bool CanCommit => PreparedTransaction is not null && Diagnostics.Count == 0;
    public PreparedFusionTransaction? PreparedTransaction { get; }
    public RuntimePartyStockSnapshot BeforePartyStock { get; }
    public RuntimePartyStockSnapshot AfterPartyStock { get; }
    public ContentId? ResultEntityId { get; }
    public IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds { get; }
    public IReadOnlyList<PartyStockTransitionResult> StockTransitions { get; }
    public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; }

    public PreparedFusionTransaction RequirePreparedTransaction() =>
        PreparedTransaction ?? throw new InvalidOperationException(
            $"Fusion transaction preparation failed with {Diagnostics.Count} diagnostic(s).");
}

public sealed record FusionTransactionCommitRequest
{
    public FusionTransactionCommitRequest(
        PreparedFusionTransaction preparedTransaction,
        RuntimePartyStockSnapshot currentPartyStock,
        RuntimeActorSnapshot? currentResultActor = null)
    {
        PreparedTransaction = preparedTransaction ?? throw new ArgumentNullException(nameof(preparedTransaction));
        CurrentPartyStock = currentPartyStock ?? throw new ArgumentNullException(nameof(currentPartyStock));
        CurrentResultActor = currentResultActor;
    }

    public PreparedFusionTransaction PreparedTransaction { get; }
    public RuntimePartyStockSnapshot CurrentPartyStock { get; }
    public RuntimeActorSnapshot? CurrentResultActor { get; }
}

public enum FusionTransactionCommitCode
{
    Applied,
    PreparationStale,
    ActorCreationRejected
}

public sealed record FusionTransactionCommitResult
{
    internal FusionTransactionCommitResult(
        FusionTransactionCommitCode code,
        PreparedFusionTransaction preparedTransaction,
        RuntimePartyStockSnapshot beforePartyStock,
        RuntimePartyStockSnapshot afterPartyStock,
        CatalogBattleActor? resultActor,
        RuntimeActorSnapshot? resultActorSnapshot,
        IEnumerable<FusionRuntimeDiagnostic>? diagnostics = null)
    {
        Code = code;
        PreparedTransaction = preparedTransaction;
        BeforePartyStock = beforePartyStock;
        AfterPartyStock = afterPartyStock;
        ResultActor = resultActor;
        ResultActorSnapshot = resultActorSnapshot;
        ConsumedParticipantIds = code == FusionTransactionCommitCode.Applied
            ? preparedTransaction.ConsumedParticipantIds
            : Array.AsReadOnly(Array.Empty<RuntimeInstanceId>());
        StockTransitions = code == FusionTransactionCommitCode.Applied
            ? preparedTransaction.StockTransitions
            : Array.AsReadOnly(Array.Empty<PartyStockTransitionResult>());
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public FusionTransactionCommitCode Code { get; }
    public bool Applied => Code == FusionTransactionCommitCode.Applied;
    public PreparedFusionTransaction PreparedTransaction { get; }
    public RuntimePartyStockSnapshot BeforePartyStock { get; }
    public RuntimePartyStockSnapshot AfterPartyStock { get; }
    public CatalogBattleActor? ResultActor { get; }
    public RuntimeActorSnapshot? ResultActorSnapshot { get; }
    public IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds { get; }
    public IReadOnlyList<PartyStockTransitionResult> StockTransitions { get; }
    public IReadOnlyList<RuntimeInstanceId> PlannedConsumedParticipantIds =>
        PreparedTransaction.ConsumedParticipantIds;
    public IReadOnlyList<PartyStockTransitionResult> PlannedStockTransitions =>
        PreparedTransaction.StockTransitions;
    public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; }
}

public interface IFusionTransactionService
{
    FusionTransactionAssessment Prepare(FusionTransactionPreparationRequest request);
    FusionTransactionCommitResult Commit(FusionTransactionCommitRequest request);
}

public sealed class FusionTransactionService : IFusionTransactionService
{
    private readonly ICatalogBattleActorFactory _actorFactory;
    private readonly IPartyStockTransitionService _partyStock;
    private readonly IFusionPreviewService _previews;

    public FusionTransactionService(
        ICatalogBattleActorFactory actorFactory,
        IPartyStockTransitionService partyStock,
        IFusionPreviewService? previews = null)
    {
        _actorFactory = actorFactory ?? throw new ArgumentNullException(nameof(actorFactory));
        _partyStock = partyStock ?? throw new ArgumentNullException(nameof(partyStock));
        _previews = previews ?? new FusionPreviewService();
    }

    public FusionTransactionAssessment Prepare(FusionTransactionPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        FusionPlanningResult plan = request.Plan;
        if (!plan.IsSuccessful || plan.ResultEntity is null)
        {
            return Rejected(
                request.PartyStock,
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                "The fusion plan has no result.");
        }

        FusionTransactionAssessment? participantFailure = ValidateParticipants(plan, request.PartyStock);
        if (participantFailure is not null)
        {
            return participantFailure;
        }

        FusionTransactionAssessment? selectionFailure = ValidateSelection(
            plan,
            request.InheritanceSelection,
            request.PartyStock);
        if (selectionFailure is not null)
        {
            return selectionFailure;
        }

        FusionPreviewSnapshot? preview = _previews.CreatePreview(new FusionPreviewRequest(
            plan,
            request.InheritanceSelection));
        if (preview is null)
        {
            return Rejected(
                request.PartyStock,
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                "The fusion preview could not be constructed.");
        }
        if (preview.EntityId != plan.ResultEntity.Id ||
            !preview.InheritedSkillIds.SequenceEqual(request.InheritanceSelection.SelectedSkillIds))
        {
            return Rejected(
                request.PartyStock,
                FusionRuntimeDiagnosticCode.InvalidPreview,
                "The fusion preview does not match the planned result and validated inheritance selection.",
                plan.ResultEntity.Id);
        }

        if (plan.Result.Operation == FusionRuntimeOperation.CreateNewEntity &&
            OwnsEntity(request.PartyStock, request.OwnerKind, preview.EntityId))
        {
            return Rejected(
                request.PartyStock,
                FusionRuntimeDiagnosticCode.DuplicateResult,
                "The fusion result is already owned.",
                preview.EntityId);
        }

        RuntimeInstanceId resultInstanceId = request.ProposedResultInstanceId;
        if (plan.Result.Operation == FusionRuntimeOperation.StatBoost)
        {
            if (plan.Result.TransformedParent is not FusionParticipantSnapshot transformedParent ||
                transformedParent.InstanceId != resultInstanceId)
            {
                return Rejected(
                    request.PartyStock,
                    FusionRuntimeDiagnosticCode.ResultIdentityInUse,
                    "A stat-boost fusion must retain the owned transformed parent's runtime identity.",
                    preview.EntityId,
                    resultInstanceId);
            }

            if (request.ExistingResultActor is not RuntimeActorSnapshot existingResultActor ||
                existingResultActor.Identity.InstanceId != transformedParent.InstanceId ||
                existingResultActor.Identity.EntityDefinitionId != preview.EntityId ||
                existingResultActor.Ownership.TeamId != request.ResultTeamId ||
                (request.ResultControllerId is ContentId resultControllerId &&
                 existingResultActor.Ownership.ControllerId != resultControllerId))
            {
                return Rejected(
                    request.PartyStock,
                    FusionRuntimeDiagnosticCode.ResultActorSnapshotInvalid,
                    "A stat-boost fusion requires the matching existing actor snapshot during preparation.",
                    preview.EntityId,
                    resultInstanceId);
            }
        }
        else
        {
            if (request.ExistingResultActor is not null)
            {
                return Rejected(
                    request.PartyStock,
                    FusionRuntimeDiagnosticCode.ResultActorSnapshotInvalid,
                    "Only a stat-boost fusion may supply an existing result actor snapshot.",
                    preview.EntityId,
                    resultInstanceId);
            }

            if (RuntimePartyStockIdentityRules.ContainsInstanceId(request.PartyStock, resultInstanceId))
            {
                return Rejected(
                    request.PartyStock,
                    FusionRuntimeDiagnosticCode.ResultIdentityInUse,
                    $"Fusion result runtime instance ID '{resultInstanceId}' is already in use.",
                    preview.EntityId,
                    resultInstanceId);
            }
        }

        IReadOnlyList<FusionParticipantSnapshot> consumedParticipants = ConsumedParticipants(plan);
        IReadOnlyList<RuntimeInstanceId> consumedParticipantIds = Array.AsReadOnly(
            consumedParticipants.Select(participant => participant.InstanceId).ToArray());
        if (consumedParticipantIds.Contains(request.PartyStock.Owner.InstanceId))
        {
            return Rejected(
                request.PartyStock,
                FusionRuntimeDiagnosticCode.StockTransitionRejected,
                "The party/stock owner cannot be consumed by a fusion transaction.",
                instanceId: request.PartyStock.Owner.InstanceId);
        }

        foreach (FusionParticipantSnapshot participant in PlanParticipants(plan))
        {
            RuntimeActorReferenceSnapshot[] owned = OwnedReferences(request.PartyStock, request.OwnerKind)
                .Where(reference => reference.InstanceId == participant.InstanceId)
                .ToArray();
            if (owned.Length == 0)
            {
                return Rejected(
                    request.PartyStock,
                    FusionRuntimeDiagnosticCode.StockTransitionRejected,
                    $"Fusion participant '{participant.InstanceId}' is not owned in the selected stock.",
                    participant.EntityId,
                    participant.InstanceId);
            }

            RuntimeActorReferenceSnapshot? mismatch = owned
                .FirstOrDefault(reference => reference.EntityDefinitionId != participant.EntityId);
            if (mismatch is not null)
            {
                return Rejected(
                    request.PartyStock,
                    FusionRuntimeDiagnosticCode.StockTransitionRejected,
                    $"Fusion participant '{participant.InstanceId}' identifies entity '{participant.EntityId}', " +
                    $"but an owned reference identifies '{mismatch.EntityDefinitionId}'.",
                    participant.EntityId,
                    participant.InstanceId);
            }
        }

        RuntimePartyStockSnapshot current = request.PartyStock;
        var transitions = new List<PartyStockTransitionResult>();
        foreach (RuntimeInstanceId participantId in consumedParticipantIds)
        {
            PartyStockTransitionResult consumed = Consume(request.OwnerKind, current, participantId);
            transitions.Add(consumed);
            if (!consumed.Applied)
            {
                return Rejected(
                    request.PartyStock,
                    plan.Result.ResultEntityId,
                    consumedParticipantIds,
                    transitions,
                    TransitionDiagnostic(consumed, participantId));
            }

            current = consumed.After;
        }

        if (plan.Result.Operation != FusionRuntimeOperation.StatBoost)
        {
            var resultReference = new RuntimeActorReferenceSnapshot(
                resultInstanceId,
                preview.EntityId,
                preview.DisplayName);
            PartyStockTransitionResult added = Add(request.OwnerKind, current, resultReference);
            transitions.Add(added);
            if (!added.Applied)
            {
                return Rejected(
                    request.PartyStock,
                    plan.Result.ResultEntityId,
                    consumedParticipantIds,
                    transitions,
                    TransitionDiagnostic(added, resultInstanceId));
            }

            current = added.After;
        }

        (IReadOnlyList<ContentId> learnedSkills, IReadOnlyList<ContentId> equippedSkills) =
            ResultSkills(preview, request.ExistingResultActor);
        var prepared = new PreparedFusionTransaction(
            request.OwnerKind,
            plan,
            request.InheritanceSelection,
            preview,
            resultInstanceId,
            request.ResultTeamId,
            request.ResultControllerId,
            request.ExistingResultActor,
            request.PartyStock,
            current,
            consumedParticipantIds,
            learnedSkills,
            equippedSkills,
            transitions);
        return new FusionTransactionAssessment(
            prepared,
            prepared.BeforePartyStock,
            prepared.AfterPartyStock,
            prepared.Preview.EntityId,
            prepared.ConsumedParticipantIds,
            prepared.StockTransitions);
    }

    public FusionTransactionCommitResult Commit(FusionTransactionCommitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PreparedFusionTransaction prepared = request.PreparedTransaction;
        if (!ReferenceEquals(request.CurrentPartyStock, prepared.BeforePartyStock) ||
            !ReferenceEquals(request.CurrentResultActor, prepared.ExistingResultActor))
        {
            return new FusionTransactionCommitResult(
                FusionTransactionCommitCode.PreparationStale,
                prepared,
                request.CurrentPartyStock,
                request.CurrentPartyStock,
                null,
                null,
                [
                    new FusionRuntimeDiagnostic(
                        FusionRuntimeDiagnosticCode.TransactionStateChanged,
                        "The party/stock or retained actor state changed after fusion preparation; prepare the transaction again.",
                        prepared.Preview.EntityId,
                        prepared.ResultInstanceId)
                ]);
        }

        CatalogBattleActorCreationResult actorResult = HydrateResultActor(request);
        if (!actorResult.IsSuccess)
        {
            FusionRuntimeDiagnostic[] diagnostics = actorResult.Diagnostics
                .Select(diagnostic => new FusionRuntimeDiagnostic(
                    FusionRuntimeDiagnosticCode.ActorCreationFailed,
                    diagnostic.Message,
                    diagnostic.EntityId,
                    prepared.ResultInstanceId))
                .ToArray();
            if (diagnostics.Length == 0)
            {
                diagnostics =
                [
                    new FusionRuntimeDiagnostic(
                        FusionRuntimeDiagnosticCode.ActorCreationFailed,
                        "Fusion result actor creation failed without a diagnostic.",
                        prepared.Preview.EntityId,
                        prepared.ResultInstanceId)
                ];
            }

            return new FusionTransactionCommitResult(
                FusionTransactionCommitCode.ActorCreationRejected,
                prepared,
                request.CurrentPartyStock,
                prepared.BeforePartyStock,
                null,
                null,
                diagnostics);
        }

        CatalogBattleActor actor = actorResult.RequireActor();
        RuntimeActorSnapshot snapshot = actor.State.ToSnapshot();
        if (!ActorMatchesPreparedDecision(actor, snapshot, prepared))
        {
            return new FusionTransactionCommitResult(
                FusionTransactionCommitCode.ActorCreationRejected,
                prepared,
                request.CurrentPartyStock,
                request.CurrentPartyStock,
                null,
                null,
                [
                    new FusionRuntimeDiagnostic(
                        FusionRuntimeDiagnosticCode.ActorCreationFailed,
                        "The result actor factory returned an actor that does not match the prepared fusion decision.",
                        prepared.Preview.EntityId,
                        prepared.ResultInstanceId)
                ]);
        }

        return new FusionTransactionCommitResult(
            FusionTransactionCommitCode.Applied,
            prepared,
            request.CurrentPartyStock,
            prepared.AfterPartyStock,
            actor,
            snapshot);
    }

    private CatalogBattleActorCreationResult HydrateResultActor(FusionTransactionCommitRequest request)
    {
        PreparedFusionTransaction prepared = request.PreparedTransaction;
        FusionPreviewSnapshot preview = prepared.Preview;
        RuntimeActorSnapshot baseline;
        int unspentStatPoints;

        if (prepared.Plan.Result.Operation == FusionRuntimeOperation.StatBoost)
        {
            RuntimeActorSnapshot? existing = prepared.ExistingResultActor;
            if (existing is null ||
                existing.Identity.InstanceId != prepared.ResultInstanceId ||
                existing.Identity.EntityDefinitionId != preview.EntityId)
            {
                return ActorCreationRejected(
                    preview.EntityId,
                    "Stat-boost fusion requires the matching existing result actor snapshot.");
            }

            baseline = existing;
            unspentStatPoints = existing.Progression.UnspentStatPoints;
        }
        else
        {
            CatalogBattleActorCreationResult created = _actorFactory.Create(new CatalogBattleActorCreationRequest(
                preview.EntityId,
                prepared.ResultInstanceId,
                prepared.ResultTeamId,
                preview.Level,
                new RuntimeProgressionSnapshot(
                    preview.Level,
                    preview.Experience,
                    preview.LifetimeExperience,
                    0),
                prepared.ResultControllerId,
                RuntimeActorDeployment.Reserve,
                IsActive: false));
            if (!created.IsSuccess)
            {
                return created;
            }

            baseline = created.RequireActor().State.ToSnapshot();
            unspentStatPoints = 0;
        }

        KeyValuePair<ContentId, decimal>[] stats = preview.Stats
            .Select(entry => new KeyValuePair<ContentId, decimal>(entry.Key, entry.Value))
            .ToArray();
        var resultSnapshot = new RuntimeActorSnapshot(
            baseline.Identity,
            baseline.Ownership,
            baseline.Deployment,
            new RuntimeProgressionSnapshot(
                preview.Level,
                preview.Experience,
                preview.LifetimeExperience,
                unspentStatPoints),
            baseline.Resources,
            new RuntimeStatBlockSnapshot(stats, stats),
            new RuntimeSkillStateSnapshot(
                prepared.ResultLearnedSkillIds,
                prepared.ResultEquippedSkillIds),
            baseline.Forms,
            baseline.Equipment,
            baseline.BattleStatus,
            baseline.BattleActivations,
            baseline.BaseResourceValues,
            baseline.VitalResourceId,
            baseline.CapabilityIds);
        return _actorFactory.Restore(resultSnapshot);
    }

    private static bool ActorMatchesPreparedDecision(
        CatalogBattleActor actor,
        RuntimeActorSnapshot snapshot,
        PreparedFusionTransaction prepared)
    {
        FusionPreviewSnapshot preview = prepared.Preview;
        if (actor.Entity.Id != preview.EntityId ||
            snapshot.Identity.InstanceId != prepared.ResultInstanceId ||
            snapshot.Identity.EntityDefinitionId != preview.EntityId ||
            snapshot.Ownership.TeamId != prepared.ResultTeamId ||
            (prepared.ResultControllerId is ContentId controllerId &&
             snapshot.Ownership.ControllerId != controllerId) ||
            snapshot.Progression.Level != preview.Level ||
            snapshot.Progression.Experience != preview.Experience ||
            snapshot.Progression.LifetimeExperience != preview.LifetimeExperience ||
            !snapshot.Skills.LearnedSkillIds.SequenceEqual(prepared.ResultLearnedSkillIds) ||
            !snapshot.Skills.EquippedSkillIds.SequenceEqual(prepared.ResultEquippedSkillIds) ||
            snapshot.Stats.BaseStats.Count != preview.Stats.Count)
        {
            return false;
        }

        foreach ((ContentId statId, int value) in preview.Stats)
        {
            if (!snapshot.Stats.BaseStats.TryGetValue(statId, out decimal actual) || actual != value)
            {
                return false;
            }
        }

        return prepared.ExistingResultActor is null ||
               snapshot.Resources.SequenceEqual(prepared.ExistingResultActor.Resources);
    }

    private static FusionTransactionAssessment? ValidateSelection(
        FusionPlanningResult plan,
        ValidatedFusionInheritanceSelection selection,
        RuntimePartyStockSnapshot partyStock)
    {
        if (!FusionValidatedSelectionRules.BelongsToPlan(plan, selection))
        {
            return Rejected(
                partyStock,
                FusionRuntimeDiagnosticCode.InvalidSelection,
                "The validated inheritance selection does not belong to this fusion plan.");
        }

        return null;
    }

    private static FusionTransactionAssessment? ValidateParticipants(
        FusionPlanningResult plan,
        RuntimePartyStockSnapshot partyStock)
    {
        IReadOnlyList<FusionParticipantSnapshot> participants = PlanParticipants(plan);
        if (plan.FirstParent is null || plan.SecondParent is null || participants.Count < 2)
        {
            return Rejected(
                partyStock,
                FusionRuntimeDiagnosticCode.InvalidParticipant,
                "A successful fusion transaction requires two parent participants.");
        }

        IGrouping<RuntimeInstanceId, FusionParticipantSnapshot>? duplicate = participants
            .GroupBy(participant => participant.InstanceId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            return Rejected(
                partyStock,
                FusionRuntimeDiagnosticCode.DuplicateParticipant,
                $"Runtime actor '{duplicate.Key}' cannot occupy more than one fusion participant slot.",
                instanceId: duplicate.Key);
        }

        if (plan.Result.Operation == FusionRuntimeOperation.StatBoost)
        {
            FusionParticipantSnapshot? transformed = plan.Result.TransformedParent;
            FusionParticipantSnapshot? catalyst = plan.Result.CatalystParent;
            bool transformedMatches = transformed is not null && participants.Any(parent =>
                parent.InstanceId == transformed.InstanceId && parent.EntityId == transformed.EntityId);
            bool catalystMatches = catalyst is not null && participants.Any(parent =>
                parent.InstanceId == catalyst.InstanceId && parent.EntityId == catalyst.EntityId);
            if (!transformedMatches || !catalystMatches ||
                transformed!.InstanceId == catalyst!.InstanceId ||
                transformed.EntityId != plan.ResultEntity!.Id)
            {
                return Rejected(
                    partyStock,
                    FusionRuntimeDiagnosticCode.InvalidParticipant,
                    "A stat-boost result must identify distinct transformed and catalyst actors from the fusion parents.");
            }
        }

        return null;
    }

    private PartyStockTransitionResult Consume(
        FusionParticipantStockKind ownerKind,
        RuntimePartyStockSnapshot snapshot,
        RuntimeInstanceId participantId) =>
        ownerKind switch
        {
            FusionParticipantStockKind.Demon => _partyStock.ConsumeDemon(new ConsumeDemonRequest(snapshot, participantId)),
            FusionParticipantStockKind.Persona => _partyStock.ConsumePersona(new ConsumePersonaRequest(snapshot, participantId)),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind))
        };

    private PartyStockTransitionResult Add(
        FusionParticipantStockKind ownerKind,
        RuntimePartyStockSnapshot snapshot,
        RuntimeActorReferenceSnapshot result) =>
        ownerKind switch
        {
            FusionParticipantStockKind.Demon => _partyStock.AddDemonToStock(new AddDemonToStockRequest(snapshot, result)),
            FusionParticipantStockKind.Persona => _partyStock.AddPersonaToStock(new AddPersonaToStockRequest(snapshot, result)),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind))
        };

    private static bool OwnsEntity(
        RuntimePartyStockSnapshot partyStock,
        FusionParticipantStockKind ownerKind,
        ContentId entityId) =>
        OwnedReferences(partyStock, ownerKind)
            .Any(actor => actor.EntityDefinitionId == entityId);

    private static IEnumerable<RuntimeActorReferenceSnapshot> OwnedReferences(
        RuntimePartyStockSnapshot partyStock,
        FusionParticipantStockKind ownerKind) =>
        ownerKind switch
        {
            FusionParticipantStockKind.Demon => partyStock.ActiveParty.Concat(partyStock.DemonStock),
            FusionParticipantStockKind.Persona => partyStock.ActiveForm is RuntimeActorReferenceSnapshot activeForm
                ? partyStock.PersonaStock.Append(activeForm)
                : partyStock.PersonaStock,
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind))
        };

    private static IReadOnlyList<FusionParticipantSnapshot> ConsumedParticipants(FusionPlanningResult plan)
    {
        IEnumerable<FusionParticipantSnapshot?> consumed = plan.Result.Operation == FusionRuntimeOperation.StatBoost
            ? [plan.Result.CatalystParent]
            : [plan.FirstParent, plan.SecondParent, plan.Sacrifice];
        return Array.AsReadOnly(consumed
            .OfType<FusionParticipantSnapshot>()
            .ToArray());
    }

    private static IReadOnlyList<FusionParticipantSnapshot> PlanParticipants(FusionPlanningResult plan) =>
        Array.AsReadOnly(new[] { plan.FirstParent, plan.SecondParent, plan.Sacrifice }
            .OfType<FusionParticipantSnapshot>()
            .ToArray());

    private static (IReadOnlyList<ContentId> Learned, IReadOnlyList<ContentId> Equipped) ResultSkills(
        FusionPreviewSnapshot preview,
        RuntimeActorSnapshot? existingResultActor)
    {
        if (existingResultActor is null)
        {
            ContentId[] skills = preview.NaturalSkillIds
                .Concat(preview.InheritedSkillIds)
                .Distinct()
                .ToArray();
            IReadOnlyList<ContentId> snapshot = Array.AsReadOnly(skills);
            return (snapshot, snapshot);
        }

        IReadOnlyList<ContentId> learned = Array.AsReadOnly(existingResultActor.Skills.LearnedSkillIds
            .Concat(preview.NaturalSkillIds)
            .Concat(preview.InheritedSkillIds)
            .Distinct()
            .ToArray());
        IReadOnlyList<ContentId> equipped = Array.AsReadOnly(existingResultActor.Skills.EquippedSkillIds
            .Concat(preview.InheritedSkillIds)
            .Distinct()
            .ToArray());
        return (learned, equipped);
    }

    private static FusionRuntimeDiagnostic TransitionDiagnostic(
        PartyStockTransitionResult result,
        RuntimeInstanceId instanceId)
    {
        PartyStockTransitionDiagnostic? source = result.Diagnostics.FirstOrDefault();
        FusionRuntimeDiagnosticCode code = result.Code switch
        {
            PartyStockTransitionCode.StockFull => FusionRuntimeDiagnosticCode.StockFull,
            PartyStockTransitionCode.RuntimeInstanceIdInUse => FusionRuntimeDiagnosticCode.ResultIdentityInUse,
            _ => FusionRuntimeDiagnosticCode.StockTransitionRejected
        };
        return new FusionRuntimeDiagnostic(
            code,
            source?.Message ?? $"Party/stock transition '{result.Code}' rejected the fusion transaction.",
            InstanceId: source?.SubjectInstanceId ?? instanceId);
    }

    private static CatalogBattleActorCreationResult ActorCreationRejected(ContentId entityId, string message) =>
        new(
            null,
            [new CatalogBattleActorDiagnostic(CatalogBattleActorDiagnosticCode.SnapshotInvalid, message, entityId)]);

    private static FusionTransactionAssessment Rejected(
        RuntimePartyStockSnapshot beforePartyStock,
        ContentId? resultEntityId,
        IEnumerable<RuntimeInstanceId> consumedParticipantIds,
        IEnumerable<PartyStockTransitionResult> stockTransitions,
        params FusionRuntimeDiagnostic[] diagnostics) =>
        new(
            null,
            beforePartyStock,
            beforePartyStock,
            resultEntityId,
            consumedParticipantIds,
            stockTransitions,
            diagnostics);

    private static FusionTransactionAssessment Rejected(
        RuntimePartyStockSnapshot beforePartyStock,
        FusionRuntimeDiagnosticCode code,
        string message,
        ContentId? contentId = null,
        RuntimeInstanceId? instanceId = null) =>
        Rejected(
            beforePartyStock,
            contentId,
            [],
            [],
            new FusionRuntimeDiagnostic(code, message, contentId, instanceId));
}
