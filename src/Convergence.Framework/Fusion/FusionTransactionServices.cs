using Convergence.Content;
using Convergence.Encounters;
using Convergence.Inheritance;
using Convergence.Runtime;

namespace Convergence.Fusion;

public sealed record FusionTransactionPreparationRequest
{
    public FusionTransactionPreparationRequest(
        FusionParticipantRosterKind ownerKind,
        FusionPlanningResult plan,
        ValidatedFusionInheritanceSelection inheritanceSelection,
        RuntimePartyRosterSnapshot partyRoster,
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
        PartyRoster = partyRoster ?? throw new ArgumentNullException(nameof(partyRoster));
        ProposedResultInstanceId = proposedResultInstanceId;
        ResultTeamId = resultTeamId;
        ResultControllerId = resultControllerId;
        ExistingResultActor = existingResultActor;
    }

    public FusionParticipantRosterKind OwnerKind { get; }
    public FusionPlanningResult Plan { get; }
    public ValidatedFusionInheritanceSelection InheritanceSelection { get; }
    public RuntimePartyRosterSnapshot PartyRoster { get; }
    public RuntimeInstanceId ProposedResultInstanceId { get; }
    public ContentId ResultTeamId { get; }
    public ContentId? ResultControllerId { get; }
    public RuntimeActorSnapshot? ExistingResultActor { get; }
}

public sealed record PreparedFusionTransaction
{
    internal PreparedFusionTransaction(
        FusionParticipantRosterKind ownerKind,
        FusionPlanningResult plan,
        ValidatedFusionInheritanceSelection inheritanceSelection,
        FusionPreviewSnapshot preview,
        RuntimeInstanceId resultInstanceId,
        ContentId resultTeamId,
        ContentId? resultControllerId,
        RuntimeActorSnapshot? existingResultActor,
        RuntimePartyRosterSnapshot beforePartyRoster,
        RuntimePartyRosterSnapshot afterPartyRoster,
        IEnumerable<RuntimeInstanceId> consumedParticipantIds,
        IEnumerable<ContentId> resultLearnedSkillIds,
        IEnumerable<ContentId> resultEquippedSkillIds,
        IEnumerable<PartyRosterTransitionResult> rosterTransitions)
    {
        OwnerKind = ownerKind;
        Plan = plan;
        InheritanceSelection = inheritanceSelection;
        Preview = preview;
        ResultInstanceId = resultInstanceId;
        ResultTeamId = resultTeamId;
        ResultControllerId = resultControllerId;
        ExistingResultActor = existingResultActor;
        BeforePartyRoster = beforePartyRoster;
        AfterPartyRoster = afterPartyRoster;
        ConsumedParticipantIds = Array.AsReadOnly(consumedParticipantIds.ToArray());
        ResultLearnedSkillIds = Array.AsReadOnly(resultLearnedSkillIds.ToArray());
        ResultEquippedSkillIds = Array.AsReadOnly(resultEquippedSkillIds.ToArray());
        RosterTransitions = Array.AsReadOnly(rosterTransitions.ToArray());
    }

    public FusionParticipantRosterKind OwnerKind { get; }
    public FusionPlanningResult Plan { get; }
    public ValidatedFusionInheritanceSelection InheritanceSelection { get; }
    public FusionPreviewSnapshot Preview { get; }
    public RuntimeInstanceId ResultInstanceId { get; }
    public ContentId ResultTeamId { get; }
    public ContentId? ResultControllerId { get; }
    public RuntimeActorSnapshot? ExistingResultActor { get; }
    public RuntimePartyRosterSnapshot BeforePartyRoster { get; }
    public RuntimePartyRosterSnapshot AfterPartyRoster { get; }
    public IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds { get; }
    public IReadOnlyList<ContentId> ResultLearnedSkillIds { get; }
    public IReadOnlyList<ContentId> ResultEquippedSkillIds { get; }
    public IReadOnlyList<PartyRosterTransitionResult> RosterTransitions { get; }
}

public sealed record FusionTransactionAssessment
{
    internal FusionTransactionAssessment(
        PreparedFusionTransaction? preparedTransaction,
        RuntimePartyRosterSnapshot beforePartyRoster,
        RuntimePartyRosterSnapshot afterPartyRoster,
        ContentId? resultEntityId,
        IEnumerable<RuntimeInstanceId>? consumedParticipantIds = null,
        IEnumerable<PartyRosterTransitionResult>? rosterTransitions = null,
        IEnumerable<FusionRuntimeDiagnostic>? diagnostics = null)
    {
        PreparedTransaction = preparedTransaction;
        BeforePartyRoster = beforePartyRoster;
        AfterPartyRoster = afterPartyRoster;
        ResultEntityId = resultEntityId;
        ConsumedParticipantIds = Array.AsReadOnly((consumedParticipantIds ?? []).ToArray());
        RosterTransitions = Array.AsReadOnly((rosterTransitions ?? []).ToArray());
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public bool CanCommit => PreparedTransaction is not null && Diagnostics.Count == 0;
    public PreparedFusionTransaction? PreparedTransaction { get; }
    public RuntimePartyRosterSnapshot BeforePartyRoster { get; }
    public RuntimePartyRosterSnapshot AfterPartyRoster { get; }
    public ContentId? ResultEntityId { get; }
    public IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds { get; }
    public IReadOnlyList<PartyRosterTransitionResult> RosterTransitions { get; }
    public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; }

    public PreparedFusionTransaction RequirePreparedTransaction() =>
        PreparedTransaction ?? throw new InvalidOperationException(
            $"Fusion transaction preparation failed with {Diagnostics.Count} diagnostic(s).");
}

public sealed record FusionTransactionCommitRequest
{
    public FusionTransactionCommitRequest(
        PreparedFusionTransaction preparedTransaction,
        RuntimePartyRosterSnapshot currentPartyRoster,
        RuntimeActorSnapshot? currentResultActor = null)
    {
        PreparedTransaction = preparedTransaction ?? throw new ArgumentNullException(nameof(preparedTransaction));
        CurrentPartyRoster = currentPartyRoster ?? throw new ArgumentNullException(nameof(currentPartyRoster));
        CurrentResultActor = currentResultActor;
    }

    public PreparedFusionTransaction PreparedTransaction { get; }
    public RuntimePartyRosterSnapshot CurrentPartyRoster { get; }
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
        RuntimePartyRosterSnapshot beforePartyRoster,
        RuntimePartyRosterSnapshot afterPartyRoster,
        CatalogBattleActor? resultActor,
        RuntimeActorSnapshot? resultActorSnapshot,
        IEnumerable<FusionRuntimeDiagnostic>? diagnostics = null)
    {
        Code = code;
        PreparedTransaction = preparedTransaction;
        BeforePartyRoster = beforePartyRoster;
        AfterPartyRoster = afterPartyRoster;
        ResultActor = resultActor;
        ResultActorSnapshot = resultActorSnapshot;
        ConsumedParticipantIds = code == FusionTransactionCommitCode.Applied
            ? preparedTransaction.ConsumedParticipantIds
            : Array.AsReadOnly(Array.Empty<RuntimeInstanceId>());
        RosterTransitions = code == FusionTransactionCommitCode.Applied
            ? preparedTransaction.RosterTransitions
            : Array.AsReadOnly(Array.Empty<PartyRosterTransitionResult>());
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public FusionTransactionCommitCode Code { get; }
    public bool Applied => Code == FusionTransactionCommitCode.Applied;
    public PreparedFusionTransaction PreparedTransaction { get; }
    public RuntimePartyRosterSnapshot BeforePartyRoster { get; }
    public RuntimePartyRosterSnapshot AfterPartyRoster { get; }
    public CatalogBattleActor? ResultActor { get; }
    public RuntimeActorSnapshot? ResultActorSnapshot { get; }
    public IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds { get; }
    public IReadOnlyList<PartyRosterTransitionResult> RosterTransitions { get; }
    public IReadOnlyList<RuntimeInstanceId> PlannedConsumedParticipantIds =>
        PreparedTransaction.ConsumedParticipantIds;
    public IReadOnlyList<PartyRosterTransitionResult> PlannedRosterTransitions =>
        PreparedTransaction.RosterTransitions;
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
    private readonly IPartyRosterTransitionService _partyRoster;
    private readonly IFusionPreviewService _previews;

    public FusionTransactionService(
        ICatalogBattleActorFactory actorFactory,
        IPartyRosterTransitionService partyRoster,
        IFusionPreviewService? previews = null)
    {
        _actorFactory = actorFactory ?? throw new ArgumentNullException(nameof(actorFactory));
        _partyRoster = partyRoster ?? throw new ArgumentNullException(nameof(partyRoster));
        _previews = previews ?? new FusionPreviewService();
    }

    public FusionTransactionAssessment Prepare(FusionTransactionPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        FusionPlanningResult plan = request.Plan;
        if (!plan.IsSuccessful || plan.ResultEntity is null)
        {
            return Rejected(
                request.PartyRoster,
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                "The fusion plan has no result.");
        }

        FusionTransactionAssessment? participantFailure = ValidateParticipants(plan, request.PartyRoster);
        if (participantFailure is not null)
        {
            return participantFailure;
        }

        FusionTransactionAssessment? selectionFailure = ValidateSelection(
            plan,
            request.InheritanceSelection,
            request.PartyRoster);
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
                request.PartyRoster,
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                "The fusion preview could not be constructed.");
        }
        if (preview.EntityId != plan.ResultEntity.Id ||
            !preview.InheritedSkillIds.SequenceEqual(request.InheritanceSelection.SelectedSkillIds))
        {
            return Rejected(
                request.PartyRoster,
                FusionRuntimeDiagnosticCode.InvalidPreview,
                "The fusion preview does not match the planned result and validated inheritance selection.",
                plan.ResultEntity.Id);
        }

        if (plan.Result.Operation == FusionRuntimeOperation.CreateNewEntity &&
            OwnsEntity(request.PartyRoster, request.OwnerKind, preview.EntityId))
        {
            return Rejected(
                request.PartyRoster,
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
                    request.PartyRoster,
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
                    request.PartyRoster,
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
                    request.PartyRoster,
                    FusionRuntimeDiagnosticCode.ResultActorSnapshotInvalid,
                    "Only a stat-boost fusion may supply an existing result actor snapshot.",
                    preview.EntityId,
                    resultInstanceId);
            }

            if (RuntimePartyRosterIdentityRules.ContainsInstanceId(request.PartyRoster, resultInstanceId))
            {
                return Rejected(
                    request.PartyRoster,
                    FusionRuntimeDiagnosticCode.ResultIdentityInUse,
                    $"Fusion result runtime instance ID '{resultInstanceId}' is already in use.",
                    preview.EntityId,
                    resultInstanceId);
            }
        }

        IReadOnlyList<FusionParticipantSnapshot> consumedParticipants = ConsumedParticipants(plan);
        IReadOnlyList<RuntimeInstanceId> consumedParticipantIds = Array.AsReadOnly(
            consumedParticipants.Select(participant => participant.InstanceId).ToArray());
        if (consumedParticipantIds.Contains(request.PartyRoster.Owner.InstanceId))
        {
            return Rejected(
                request.PartyRoster,
                FusionRuntimeDiagnosticCode.RosterTransitionRejected,
                "The party/roster owner cannot be consumed by a fusion transaction.",
                instanceId: request.PartyRoster.Owner.InstanceId);
        }

        foreach (FusionParticipantSnapshot participant in PlanParticipants(plan))
        {
            RuntimeActorReferenceSnapshot[] owned = OwnedReferences(request.PartyRoster, request.OwnerKind)
                .Where(reference => reference.InstanceId == participant.InstanceId)
                .ToArray();
            if (owned.Length == 0)
            {
                return Rejected(
                    request.PartyRoster,
                    FusionRuntimeDiagnosticCode.RosterTransitionRejected,
                    $"Fusion participant '{participant.InstanceId}' is not owned in the selected roster.",
                    participant.EntityId,
                    participant.InstanceId);
            }

            RuntimeActorReferenceSnapshot? mismatch = owned
                .FirstOrDefault(reference => reference.EntityDefinitionId != participant.EntityId);
            if (mismatch is not null)
            {
                return Rejected(
                    request.PartyRoster,
                    FusionRuntimeDiagnosticCode.RosterTransitionRejected,
                    $"Fusion participant '{participant.InstanceId}' identifies entity '{participant.EntityId}', " +
                    $"but an owned reference identifies '{mismatch.EntityDefinitionId}'.",
                    participant.EntityId,
                    participant.InstanceId);
            }
        }

        RuntimePartyRosterSnapshot current = request.PartyRoster;
        var transitions = new List<PartyRosterTransitionResult>();
        foreach (RuntimeInstanceId participantId in consumedParticipantIds)
        {
            PartyRosterTransitionResult consumed = Consume(request.OwnerKind, current, participantId);
            transitions.Add(consumed);
            if (!consumed.Applied)
            {
                return Rejected(
                    request.PartyRoster,
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
            PartyRosterTransitionResult added = Add(request.OwnerKind, current, resultReference);
            transitions.Add(added);
            if (!added.Applied)
            {
                return Rejected(
                    request.PartyRoster,
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
            request.PartyRoster,
            current,
            consumedParticipantIds,
            learnedSkills,
            equippedSkills,
            transitions);
        return new FusionTransactionAssessment(
            prepared,
            prepared.BeforePartyRoster,
            prepared.AfterPartyRoster,
            prepared.Preview.EntityId,
            prepared.ConsumedParticipantIds,
            prepared.RosterTransitions);
    }

    public FusionTransactionCommitResult Commit(FusionTransactionCommitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PreparedFusionTransaction prepared = request.PreparedTransaction;
        if (!ReferenceEquals(request.CurrentPartyRoster, prepared.BeforePartyRoster) ||
            !ReferenceEquals(request.CurrentResultActor, prepared.ExistingResultActor))
        {
            return new FusionTransactionCommitResult(
                FusionTransactionCommitCode.PreparationStale,
                prepared,
                request.CurrentPartyRoster,
                request.CurrentPartyRoster,
                null,
                null,
                [
                    new FusionRuntimeDiagnostic(
                        FusionRuntimeDiagnosticCode.TransactionStateChanged,
                        "The party/roster or retained actor state changed after fusion preparation; prepare the transaction again.",
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
                request.CurrentPartyRoster,
                prepared.BeforePartyRoster,
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
                request.CurrentPartyRoster,
                request.CurrentPartyRoster,
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
            request.CurrentPartyRoster,
            prepared.AfterPartyRoster,
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
                IsDeployed: false,
                new RuntimeProgressionSnapshot(
                    preview.Level,
                    preview.Experience,
                    preview.LifetimeExperience,
                    0),
                prepared.ResultControllerId));
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
            baseline.EncounterPresence,
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
            baseline.Equipment,
            baseline.BattleStatus,
            baseline.BattleActivations,
            baseline.BaseResourceValues,
            baseline.VitalResourceId,
            baseline.CapabilityIds);
        return _actorFactory.Restore(
            CatalogBattleActorRestoreRequest.FromValidatedFrameworkSnapshot(resultSnapshot));
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
        RuntimePartyRosterSnapshot partyRoster)
    {
        if (!FusionValidatedSelectionRules.BelongsToPlan(plan, selection))
        {
            return Rejected(
                partyRoster,
                FusionRuntimeDiagnosticCode.InvalidSelection,
                "The validated inheritance selection does not belong to this fusion plan.");
        }

        return null;
    }

    private static FusionTransactionAssessment? ValidateParticipants(
        FusionPlanningResult plan,
        RuntimePartyRosterSnapshot partyRoster)
    {
        IReadOnlyList<FusionParticipantSnapshot> participants = PlanParticipants(plan);
        if (plan.FirstParent is null || plan.SecondParent is null || participants.Count < 2)
        {
            return Rejected(
                partyRoster,
                FusionRuntimeDiagnosticCode.InvalidParticipant,
                "A successful fusion transaction requires two parent participants.");
        }

        IGrouping<RuntimeInstanceId, FusionParticipantSnapshot>? duplicate = participants
            .GroupBy(participant => participant.InstanceId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            return Rejected(
                partyRoster,
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
                    partyRoster,
                    FusionRuntimeDiagnosticCode.InvalidParticipant,
                    "A stat-boost result must identify distinct transformed and catalyst actors from the fusion parents.");
            }
        }

        return null;
    }

    private PartyRosterTransitionResult Consume(
        FusionParticipantRosterKind ownerKind,
        RuntimePartyRosterSnapshot snapshot,
        RuntimeInstanceId participantId) =>
        ownerKind switch
        {
            FusionParticipantRosterKind.Companion => _partyRoster.ConsumeCompanion(new ConsumeCompanionRequest(snapshot, participantId)),
            FusionParticipantRosterKind.HostedEntity => _partyRoster.ConsumeHostedEntity(new ConsumeHostedEntityRequest(snapshot, participantId)),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind))
        };

    private PartyRosterTransitionResult Add(
        FusionParticipantRosterKind ownerKind,
        RuntimePartyRosterSnapshot snapshot,
        RuntimeActorReferenceSnapshot result) =>
        ownerKind switch
        {
            FusionParticipantRosterKind.Companion => _partyRoster.AddCompanionToRoster(new AddCompanionToRosterRequest(snapshot, result)),
            FusionParticipantRosterKind.HostedEntity => _partyRoster.AddHostedEntityToRoster(new AddHostedEntityToRosterRequest(snapshot, result)),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind))
        };

    private static bool OwnsEntity(
        RuntimePartyRosterSnapshot partyRoster,
        FusionParticipantRosterKind ownerKind,
        ContentId entityId) =>
        OwnedReferences(partyRoster, ownerKind)
            .Any(actor => actor.EntityDefinitionId == entityId);

    private static IEnumerable<RuntimeActorReferenceSnapshot> OwnedReferences(
        RuntimePartyRosterSnapshot partyRoster,
        FusionParticipantRosterKind ownerKind) =>
        ownerKind switch
        {
            FusionParticipantRosterKind.Companion => partyRoster.CompanionRoster,
            FusionParticipantRosterKind.HostedEntity => partyRoster.HostedEntityRoster,
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
        PartyRosterTransitionResult result,
        RuntimeInstanceId instanceId)
    {
        PartyRosterTransitionDiagnostic? source = result.Diagnostics.FirstOrDefault();
        FusionRuntimeDiagnosticCode code = result.Code switch
        {
            PartyRosterTransitionCode.RosterFull => FusionRuntimeDiagnosticCode.RosterFull,
            PartyRosterTransitionCode.RuntimeInstanceIdInUse => FusionRuntimeDiagnosticCode.ResultIdentityInUse,
            _ => FusionRuntimeDiagnosticCode.RosterTransitionRejected
        };
        return new FusionRuntimeDiagnostic(
            code,
            source?.Message ?? $"Party/roster transition '{result.Code}' rejected the fusion transaction.",
            InstanceId: source?.SubjectInstanceId ?? instanceId);
    }

    private static CatalogBattleActorCreationResult ActorCreationRejected(ContentId entityId, string message) =>
        new(
            null,
            [new CatalogBattleActorDiagnostic(CatalogBattleActorDiagnosticCode.SnapshotInvalid, message, entityId)]);

    private static FusionTransactionAssessment Rejected(
        RuntimePartyRosterSnapshot beforePartyRoster,
        ContentId? resultEntityId,
        IEnumerable<RuntimeInstanceId> consumedParticipantIds,
        IEnumerable<PartyRosterTransitionResult> rosterTransitions,
        params FusionRuntimeDiagnostic[] diagnostics) =>
        new(
            null,
            beforePartyRoster,
            beforePartyRoster,
            resultEntityId,
            consumedParticipantIds,
            rosterTransitions,
            diagnostics);

    private static FusionTransactionAssessment Rejected(
        RuntimePartyRosterSnapshot beforePartyRoster,
        FusionRuntimeDiagnosticCode code,
        string message,
        ContentId? contentId = null,
        RuntimeInstanceId? instanceId = null) =>
        Rejected(
            beforePartyRoster,
            contentId,
            [],
            [],
            new FusionRuntimeDiagnostic(code, message, contentId, instanceId));
}
