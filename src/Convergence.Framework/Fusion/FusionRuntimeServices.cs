using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Hosting;
using Convergence.Inheritance;
using Convergence.Runtime;
using static Convergence.Fusion.FusionRuntimeCollections;

namespace Convergence.Fusion;

public enum FusionRuntimeOperation
{
    CreateNewEntity,
    RankUpParent,
    RankDownParent,
    StatBoost,
    NoFusionPossible
}

public enum FusionParticipantRosterKind
{
    Companion,
    HostedEntity
}

public enum FusionRuntimeDiagnosticCode
{
    None,
    MissingParentActorState,
    NoRecipe,
    NoFusionPossible,
    MissingEntity,
    MissingRaceMembers,
    CatalystPairUnsupported,
    TargetNotEligible,
    PolicyNotRegistered,
    InvalidPolicyResult,
    SacrificeNotAllowed,
    InvalidSacrifice,
    UnsupportedRecipeFormat,
    DuplicateResult,
    RosterFull,
    InsufficientCurrency,
    RecallUnavailable,
    InvalidSelection,
    InvalidParticipant,
    DuplicateParticipant,
    InvalidPreview,
    ResultIdentityInUse,
    ResultActorSnapshotInvalid,
    RosterTransitionRejected,
    ActorCreationFailed,
    TransactionStateChanged,
    AmbiguousRecipe,
    ParticipantDefinitionMismatch,
    InvalidCatalystRankShift,
    RankShiftResultMissing,
    RankShiftResultAmbiguous
}

public sealed record FusionRuntimeDiagnostic(
    FusionRuntimeDiagnosticCode Code,
    string Message,
    ContentId? ContentId = null,
    RuntimeInstanceId? InstanceId = null);

public sealed record FusionRecipeResultSnapshot
{
    public FusionRecipeResultSnapshot(
        FusionResultOperationKind Operation,
        ContentId? ResultEntityId = null,
        int? RankShift = null,
        ContentId? PolicyId = null,
        IEnumerable<KeyValuePair<string, object?>>? Parameters = null)
    {
        this.Operation = Operation;
        this.ResultEntityId = ResultEntityId;
        this.RankShift = RankShift;
        this.PolicyId = PolicyId;
        this.Parameters = DefinitionCollections.SnapshotParameters(Parameters);
    }

    public FusionResultOperationKind Operation { get; }
    public ContentId? ResultEntityId { get; }
    public int? RankShift { get; }
    public ContentId? PolicyId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public sealed record FusionRecipeParentSelectorSnapshot(
    FusionParentSelectorKind Kind,
    ContentId Id,
    FusionParentRole Role = FusionParentRole.Participant);

public sealed record FusionRecipeSnapshot
{
    public FusionRecipeSnapshot(
        FusionRecipeParentSelectorSnapshot FirstParent,
        FusionRecipeParentSelectorSnapshot SecondParent,
        FusionRecipeResultSnapshot? Result = null,
        ContentId? AccidentPolicyId = null,
        ContentId? MutationPolicyId = null,
        string? CompatibilityResultToken = null)
    {
        this.FirstParent = FirstParent ?? throw new ArgumentNullException(nameof(FirstParent));
        this.SecondParent = SecondParent ?? throw new ArgumentNullException(nameof(SecondParent));
        this.Result = Result;
        this.AccidentPolicyId = AccidentPolicyId;
        this.MutationPolicyId = MutationPolicyId;
        this.CompatibilityResultToken = CompatibilityResultToken;
    }

    public FusionRecipeParentSelectorSnapshot FirstParent { get; }
    public FusionRecipeParentSelectorSnapshot SecondParent { get; }
    public FusionRecipeResultSnapshot? Result { get; }
    public ContentId? AccidentPolicyId { get; }
    public ContentId? MutationPolicyId { get; }
    public string? CompatibilityResultToken { get; }
}

public sealed record FusionEntitySnapshot
{
    public FusionEntitySnapshot(EntityDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public EntityDefinition Definition { get; }
    public ContentId Id => Definition.Id;
    public string DisplayName => Definition.DisplayName;
    public ContentId RaceId => Definition.RaceId;
    public int Rank => Definition.Rank;
    public int BaseLevel => Definition.BaseLevel;
    public IReadOnlyList<ContentId> BaseSkillIds => Definition.BaseSkillIds;
    public IReadOnlyDictionary<ContentId, int> Stats => Definition.Stats;
}

public sealed record FusionParticipantSnapshot
{
    public FusionParticipantSnapshot(
        RuntimeInstanceId instanceId,
        ContentId entityId,
        string displayName,
        ContentId raceId,
        int rank,
        int level,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<KeyValuePair<ContentId, int>>? stats = null,
        long experience = 0,
        long lifetimeExperience = 0)
    {
        if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (rank < 0) throw new ArgumentOutOfRangeException(nameof(rank));
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        if (lifetimeExperience < 0) throw new ArgumentOutOfRangeException(nameof(lifetimeExperience));

        InstanceId = instanceId;
        EntityId = entityId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? entityId.ToString() : displayName;
        RaceId = raceId;
        Rank = rank;
        Level = level;
        SkillIds = Snapshot(skillIds);
        Stats = SnapshotDictionary(stats);
        Experience = experience;
        LifetimeExperience = lifetimeExperience;
    }

    public RuntimeInstanceId InstanceId { get; }
    public ContentId EntityId { get; }
    public string DisplayName { get; }
    public ContentId RaceId { get; }
    public int Rank { get; }
    public int Level { get; }
    public IReadOnlyList<ContentId> SkillIds { get; }
    public IReadOnlyDictionary<ContentId, int> Stats { get; }
    public long Experience { get; }
    public long LifetimeExperience { get; }
}

public interface IFusionContentRepository
{
    IEnumerable<FusionRecipeSnapshot> GetRecipes();
    bool TryGetEntity(ContentId entityId, out FusionEntitySnapshot? entity);
    IReadOnlyList<FusionEntitySnapshot> GetEntitiesByRace(ContentId raceId);
    bool TryGetSkill(ContentId skillId, out SkillDefinition? skill);
    IReadOnlyList<SkillDefinition> GetSkills();
}

public sealed record FusionResultRequest(
    FusionParticipantSnapshot FirstParent,
    FusionParticipantSnapshot SecondParent,
    FusionPolicyContext? PolicyContext = null);

public sealed record FusionResolvedResult
{
    internal FusionResolvedResult(
        FusionRuntimeOperation operation,
        ContentId? resultEntityId,
        bool isAccident,
        FusionParticipantSnapshot? transformedParent,
        FusionParticipantSnapshot? catalystParent,
        FusionRecipeSnapshot? matchedRecipe,
        ContentId? resultPolicyId,
        IReadOnlyDictionary<ContentId, int> resultStats,
        IReadOnlyList<FusionRuntimeDiagnostic> diagnostics)
    {
        Operation = operation;
        ResultEntityId = resultEntityId;
        IsAccident = isAccident;
        TransformedParent = transformedParent;
        CatalystParent = catalystParent;
        MatchedRecipe = matchedRecipe;
        ResultPolicyId = resultPolicyId;
        ResultStats = resultStats;
        Diagnostics = diagnostics;
    }

    public FusionRuntimeOperation Operation { get; }
    public ContentId? ResultEntityId { get; }
    public bool IsAccident { get; }
    public FusionParticipantSnapshot? TransformedParent { get; }
    public FusionParticipantSnapshot? CatalystParent { get; }
    public FusionRecipeSnapshot? MatchedRecipe { get; }
    public ContentId? ResultPolicyId { get; }
    public IReadOnlyDictionary<ContentId, int> ResultStats { get; }
    public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; }
    public bool IsSuccessful => Operation != FusionRuntimeOperation.NoFusionPossible && ResultEntityId is not null;
}

public interface IFusionResultResolver
{
    FusionResolvedResult Resolve(FusionResultRequest request);
    ContentId? TryResolveDirectCreateResult(ContentId firstParentId, ContentId firstRaceId, ContentId secondParentId, ContentId secondRaceId);
}

public sealed class FusionResultResolver : IFusionResultResolver
{
    private readonly IFusionContentRepository _content;
    private readonly IRandomSource _random;
    private readonly FusionPolicyRegistry _policies;

    public FusionResultResolver(
        IFusionContentRepository content,
        IRandomSource random,
        FusionPolicyRegistry policies)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
    }

    public FusionResolvedResult Resolve(FusionResultRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        FusionParticipantSnapshot a = request.FirstParent;
        FusionParticipantSnapshot b = request.SecondParent;
        FusionPolicyContext context = request.PolicyContext ?? FusionPolicyContext.Empty;

        foreach (IFusionCombinationPolicy policy in _policies.CombinationPolicies)
        {
            FusionPolicyResolution? policyResolution = policy.TryResolve(new FusionCombinationPolicyRequest(
                a,
                b,
                _content,
                context,
                _random));
            if (policyResolution is null)
            {
                continue;
            }

            if (!policyResolution.IsSuccessful)
            {
                return Failed(policyResolution.Diagnostics);
            }

            if (!TryRollAccident(
                    _policies.DefaultAccidentPolicyId,
                    null,
                    a,
                    b,
                    context,
                    out bool isAccident,
                    out FusionResolvedResult? accidentFailure))
            {
                return accidentFailure!;
            }

            return FromPolicy(policyResolution, isAccident, null, policy.Id);
        }

        FusionRecipeMatch recipeMatch = FindRecipe(a, b);
        if (recipeMatch.IsAmbiguous)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.AmbiguousRecipe,
                "Multiple equal-specificity fusion recipes matched the selected parents.");
        }

        FusionRecipeSnapshot? recipe = recipeMatch.Recipe;

        if (recipe is null)
        {
            return Failed(FusionRuntimeDiagnosticCode.NoRecipe, "No fusion recipe matched the selected parents.");
        }

        FusionResolvedResult? policyFailure = ValidateRecipePoliciesBeforeRandomness(recipe);
        if (policyFailure is not null)
        {
            return policyFailure;
        }

        ContentId? accidentPolicyId = recipe.AccidentPolicyId ?? _policies.DefaultAccidentPolicyId;
        if (!TryRollAccident(
                accidentPolicyId,
                recipe,
                a,
                b,
                context,
                out bool isRecipeAccident,
                out FusionResolvedResult? recipeAccidentFailure))
        {
            return recipeAccidentFailure!;
        }

        if (recipe.Result is not null)
        {
            return ResolveAuthoredResult(recipeMatch, a, b, isRecipeAccident, context);
        }

        string? token = recipe.CompatibilityResultToken;
        if (TryToken(token, out ContentId tokenId) && _content.TryGetEntity(tokenId, out _))
        {
            return Successful(
                FusionRuntimeOperation.CreateNewEntity,
                tokenId,
                isRecipeAccident,
                matchedRecipe: recipe);
        }

        if (_policies.UnstructuredRecipePolicy is null)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.UnsupportedRecipeFormat,
                token is null
                    ? "Fusion recipe has neither a structured result nor a compatibility result token."
                    : $"Fusion recipe result token '{token}' requires an explicitly registered compatibility policy.");
        }

        FusionPolicyResolution unstructured = _policies.UnstructuredRecipePolicy.Resolve(
            new FusionUnstructuredRecipePolicyRequest(
                recipe,
                a,
                b,
                isRecipeAccident,
                _content,
                context),
            _random);
        return unstructured.IsSuccessful
            ? FromPolicy(unstructured, isRecipeAccident, recipe, null)
            : Failed(unstructured.Diagnostics);
    }

    private FusionResolvedResult? ValidateRecipePoliciesBeforeRandomness(FusionRecipeSnapshot recipe)
    {
        ContentId? accidentPolicyId = recipe.AccidentPolicyId ?? _policies.DefaultAccidentPolicyId;
        if (accidentPolicyId is ContentId accidentId &&
            !_policies.TryGetAccidentPolicy(accidentId, out _))
        {
            return Failed(
                FusionRuntimeDiagnosticCode.PolicyNotRegistered,
                $"Fusion accident policy '{accidentId}' is not registered.");
        }

        if (recipe.Result is { Operation: FusionResultOperationKind.StatBoost or FusionResultOperationKind.Special } result)
        {
            if (result.PolicyId is not ContentId resultPolicyId ||
                !_policies.TryGetResultPolicy(resultPolicyId, out _))
            {
                return Failed(
                    FusionRuntimeDiagnosticCode.PolicyNotRegistered,
                    result.PolicyId is ContentId missingId
                        ? $"Fusion result policy '{missingId}' is not registered."
                        : $"Fusion result operation '{result.Operation}' requires a policy ID.");
            }
        }

        string? compatibilityToken = recipe.CompatibilityResultToken;
        if (recipe.Result is null &&
            !(TryToken(compatibilityToken, out ContentId tokenId) && _content.TryGetEntity(tokenId, out _)) &&
            _policies.UnstructuredRecipePolicy is null)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.UnsupportedRecipeFormat,
                compatibilityToken is null
                    ? "Fusion recipe has neither a structured result nor a compatibility result token."
                    : $"Fusion recipe result token '{compatibilityToken}' requires an explicitly registered compatibility policy.");
        }

        return null;
    }

    public ContentId? TryResolveDirectCreateResult(
        ContentId firstParentId,
        ContentId firstRaceId,
        ContentId secondParentId,
        ContentId secondRaceId)
    {
        FusionRecipeMatch recipeMatch = FindRecipe(
            firstParentId,
            firstRaceId,
            secondParentId,
            secondRaceId);
        if (recipeMatch.IsAmbiguous)
        {
            return null;
        }

        FusionRecipeSnapshot? recipe = recipeMatch.Recipe;

        if (recipe?.Result is FusionRecipeResultSnapshot authoredResult)
        {
            if (authoredResult is { Operation: FusionResultOperationKind.CreateEntity, ResultEntityId: ContentId authoredResultId } &&
                _content.TryGetEntity(authoredResultId, out _))
            {
                return authoredResultId;
            }

            return null;
        }

        if (recipe is null || !TryToken(recipe.CompatibilityResultToken, out ContentId resultId))
        {
            return null;
        }

        return _content.TryGetEntity(resultId, out _) ? resultId : null;
    }

    private FusionResolvedResult ResolveAuthoredResult(
        FusionRecipeMatch recipeMatch,
        FusionParticipantSnapshot a,
        FusionParticipantSnapshot b,
        bool isAccident,
        FusionPolicyContext context)
    {
        FusionRecipeSnapshot recipe = recipeMatch.Recipe!;
        FusionRecipeResultSnapshot result = recipe.Result!;
        return result.Operation switch
        {
            FusionResultOperationKind.CreateEntity => ResolveAuthoredCreateEntity(recipe, result, isAccident),
            FusionResultOperationKind.CatalystRankShift =>
                ResolveAuthoredCatalystRankShift(recipeMatch, result, isAccident),
            FusionResultOperationKind.StatBoost or FusionResultOperationKind.Special =>
                ResolvePolicyResult(recipe, result, a, b, isAccident, context),
            _ => Failed(FusionRuntimeDiagnosticCode.NoFusionPossible,
                $"Fusion result operation '{result.Operation}' is not supported.")
        };
    }

    private FusionResolvedResult ResolveAuthoredCreateEntity(
        FusionRecipeSnapshot recipe,
        FusionRecipeResultSnapshot result,
        bool isAccident)
    {
        if (result.ResultEntityId is not ContentId entityId)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                "Create-entity fusion result is missing a result entity ID.");
        }

        return _content.TryGetEntity(entityId, out _)
            ? Successful(FusionRuntimeOperation.CreateNewEntity, entityId, isAccident, matchedRecipe: recipe)
            : Failed(FusionRuntimeDiagnosticCode.MissingEntity, $"Fusion result entity '{entityId}' was not found.");
    }

    private FusionResolvedResult ResolveAuthoredCatalystRankShift(
        FusionRecipeMatch recipeMatch,
        FusionRecipeResultSnapshot result,
        bool isAccident)
    {
        FusionRecipeSnapshot recipe = recipeMatch.Recipe!;
        if (result.RankShift is not int rankShift || rankShift == 0)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.InvalidCatalystRankShift,
                "Catalyst rank-shift fusion requires a nonzero rank shift.");
        }

        FusionParticipantSnapshot? target = ParticipantForRole(
            recipeMatch,
            FusionParentRole.RankShiftTarget);
        FusionParticipantSnapshot? catalyst = ParticipantForRole(
            recipeMatch,
            FusionParentRole.Catalyst);
        if (target is null || catalyst is null)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.InvalidCatalystRankShift,
                "Catalyst rank-shift fusion requires one catalyst selector and one rank-shift target selector.");
        }

        if (!TryValidateCatalogParticipant(target, out FusionEntitySnapshot? targetDefinition, out FusionResolvedResult? failure))
        {
            return failure!;
        }

        if (!TryValidateCatalogParticipant(catalyst, out _, out failure))
        {
            return failure!;
        }

        long shiftedRank = (long)targetDefinition!.Rank + rankShift;
        if (shiftedRank < 0 || shiftedRank > int.MaxValue)
        {
            return Failed([
                new FusionRuntimeDiagnostic(
                    FusionRuntimeDiagnosticCode.RankShiftResultMissing,
                    $"Race '{targetDefinition.RaceId}' has no entity at shifted rank {shiftedRank}.",
                    targetDefinition.Id,
                    target.InstanceId)
            ]);
        }

        FusionEntitySnapshot[] exactMatches = _content.GetEntitiesByRace(targetDefinition.RaceId)
            .Where(entity => entity.Rank == (int)shiftedRank)
            .ToArray();
        if (exactMatches.Length == 0)
        {
            return Failed([
                new FusionRuntimeDiagnostic(
                    FusionRuntimeDiagnosticCode.RankShiftResultMissing,
                    $"Race '{targetDefinition.RaceId}' has no entity at shifted rank {shiftedRank}.",
                    targetDefinition.Id,
                    target.InstanceId)
            ]);
        }

        if (exactMatches.Length > 1)
        {
            return Failed([
                new FusionRuntimeDiagnostic(
                    FusionRuntimeDiagnosticCode.RankShiftResultAmbiguous,
                    $"Race '{targetDefinition.RaceId}' has multiple entities at shifted rank {shiftedRank}.",
                    targetDefinition.Id,
                    target.InstanceId)
            ]);
        }

        return Successful(
            rankShift > 0 ? FusionRuntimeOperation.RankUpParent : FusionRuntimeOperation.RankDownParent,
            exactMatches[0].Id,
            isAccident,
            transformedParent: target,
            catalystParent: catalyst,
            matchedRecipe: recipe);
    }

    private static FusionParticipantSnapshot? ParticipantForRole(
        FusionRecipeMatch match,
        FusionParentRole role)
    {
        if (match.Recipe!.FirstParent.Role == role)
        {
            return match.FirstSelectorParticipant;
        }

        return match.Recipe.SecondParent.Role == role
            ? match.SecondSelectorParticipant
            : null;
    }

    private bool TryValidateCatalogParticipant(
        FusionParticipantSnapshot participant,
        out FusionEntitySnapshot? definition,
        out FusionResolvedResult? failure)
    {
        if (!_content.TryGetEntity(participant.EntityId, out definition) || definition is null)
        {
            failure = Failed([
                new FusionRuntimeDiagnostic(
                    FusionRuntimeDiagnosticCode.MissingEntity,
                    $"Fusion participant entity '{participant.EntityId}' was not found.",
                    participant.EntityId,
                    participant.InstanceId)
            ]);
            return false;
        }

        if (definition.RaceId != participant.RaceId || definition.Rank != participant.Rank)
        {
            failure = Failed([
                new FusionRuntimeDiagnostic(
                    FusionRuntimeDiagnosticCode.ParticipantDefinitionMismatch,
                    $"Fusion participant '{participant.EntityId}' does not match catalog race and rank.",
                    participant.EntityId,
                    participant.InstanceId)
            ]);
            return false;
        }

        failure = null;
        return true;
    }

    private FusionResolvedResult ResolvePolicyResult(
        FusionRecipeSnapshot recipe,
        FusionRecipeResultSnapshot result,
        FusionParticipantSnapshot a,
        FusionParticipantSnapshot b,
        bool isAccident,
        FusionPolicyContext context)
    {
        if (result.PolicyId is not ContentId policyId ||
            !_policies.TryGetResultPolicy(policyId, out IFusionResultPolicy? policy) ||
            policy is null)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.PolicyNotRegistered,
                result.PolicyId is ContentId missingId
                    ? $"Fusion result policy '{missingId}' is not registered."
                    : $"Fusion result operation '{result.Operation}' requires a policy ID.");
        }

        FusionPolicyResolution resolution = policy.Resolve(new FusionResultPolicyRequest(
            result,
            a,
            b,
            _content,
            context,
            _random));
        if (!resolution.IsSuccessful)
        {
            return Failed(resolution.Diagnostics);
        }

        return FromPolicy(resolution, isAccident, recipe, policyId);
    }

    private FusionRecipeMatch FindRecipe(
        FusionParticipantSnapshot first,
        FusionParticipantSnapshot second)
    {
        FusionRecipeMatch[] matches = _content.GetRecipes()
            .Select(recipe => MatchRecipe(recipe, first, second))
            .Where(match => match.Recipe is not null)
            .ToArray();
        return SelectAuthoritativeMatch(matches);
    }

    private FusionRecipeMatch FindRecipe(
        ContentId firstEntityId,
        ContentId firstRaceId,
        ContentId secondEntityId,
        ContentId secondRaceId)
    {
        FusionRecipeMatch[] matches = _content.GetRecipes()
            .Where(recipe =>
                (Matches(recipe.FirstParent, firstEntityId, firstRaceId) &&
                 Matches(recipe.SecondParent, secondEntityId, secondRaceId)) ||
                (Matches(recipe.FirstParent, secondEntityId, secondRaceId) &&
                 Matches(recipe.SecondParent, firstEntityId, firstRaceId)))
            .Select(recipe => new FusionRecipeMatch(recipe, null, null, IsAmbiguous: false))
            .ToArray();
        return SelectAuthoritativeMatch(matches);
    }

    private static FusionRecipeMatch MatchRecipe(
        FusionRecipeSnapshot recipe,
        FusionParticipantSnapshot first,
        FusionParticipantSnapshot second)
    {
        bool forward = Matches(recipe.FirstParent, first.EntityId, first.RaceId) &&
                       Matches(recipe.SecondParent, second.EntityId, second.RaceId);
        bool reverse = Matches(recipe.FirstParent, second.EntityId, second.RaceId) &&
                       Matches(recipe.SecondParent, first.EntityId, first.RaceId);
        if (!forward && !reverse)
        {
            return default;
        }

        if (forward && reverse && recipe.Result?.Operation == FusionResultOperationKind.CatalystRankShift)
        {
            return new FusionRecipeMatch(recipe, null, null, IsAmbiguous: true);
        }

        return forward
            ? new FusionRecipeMatch(recipe, first, second, IsAmbiguous: false)
            : new FusionRecipeMatch(recipe, second, first, IsAmbiguous: false);
    }

    private static FusionRecipeMatch SelectAuthoritativeMatch(FusionRecipeMatch[] matches)
    {
        if (matches.Length == 0)
        {
            return default;
        }

        int highestSpecificity = matches.Max(match => SelectorSpecificity(match.Recipe!));
        FusionRecipeMatch[] authoritativeMatches = matches
            .Where(match => SelectorSpecificity(match.Recipe!) == highestSpecificity)
            .Take(2)
            .ToArray();
        return authoritativeMatches.Length == 1 && !authoritativeMatches[0].IsAmbiguous
            ? authoritativeMatches[0]
            : new FusionRecipeMatch(null, null, null, IsAmbiguous: true);
    }

    private static bool Matches(
        FusionRecipeParentSelectorSnapshot selector,
        ContentId entityId,
        ContentId raceId) =>
        selector.Kind switch
        {
            FusionParentSelectorKind.Entity => selector.Id == entityId,
            FusionParentSelectorKind.Race => selector.Id == raceId,
            _ => false
        };

    private static int SelectorSpecificity(FusionRecipeSnapshot recipe) =>
        (recipe.FirstParent.Kind == FusionParentSelectorKind.Entity ? 1 : 0) +
        (recipe.SecondParent.Kind == FusionParentSelectorKind.Entity ? 1 : 0);

    private readonly record struct FusionRecipeMatch(
        FusionRecipeSnapshot? Recipe,
        FusionParticipantSnapshot? FirstSelectorParticipant,
        FusionParticipantSnapshot? SecondSelectorParticipant,
        bool IsAmbiguous);

    private static bool TryToken(string? token, out ContentId id)
    {
        if (token is not null)
        {
            return ContentId.TryParse(token, out id);
        }

        id = default;
        return false;
    }

    private bool TryRollAccident(
        ContentId? policyId,
        FusionRecipeSnapshot? recipe,
        FusionParticipantSnapshot a,
        FusionParticipantSnapshot b,
        FusionPolicyContext context,
        out bool isAccident,
        out FusionResolvedResult? failure)
    {
        isAccident = false;
        failure = null;
        if (policyId is null)
        {
            return true;
        }

        if (!_policies.TryGetAccidentPolicy(policyId.Value, out IFusionAccidentPolicy? policy) || policy is null)
        {
            failure = Failed(
                FusionRuntimeDiagnosticCode.PolicyNotRegistered,
                $"Fusion accident policy '{policyId}' is not registered.");
            return false;
        }

        isAccident = policy.IsAccident(new FusionAccidentPolicyRequest(recipe, a, b, context), _random);
        return true;
    }

    private FusionResolvedResult FromPolicy(
        FusionPolicyResolution resolution,
        bool isAccident,
        FusionRecipeSnapshot? matchedRecipe,
        ContentId? resultPolicyId)
    {
        ContentId resultEntityId = resolution.ResultEntityId!.Value;
        if (!_content.TryGetEntity(resultEntityId, out _))
        {
            return Failed(
                FusionRuntimeDiagnosticCode.MissingEntity,
                $"Fusion policy '{resultPolicyId?.ToString() ?? "unidentified"}' returned unknown entity '{resultEntityId}'.");
        }

        if (resolution.Operation == FusionRuntimeOperation.StatBoost &&
            (resolution.TransformedParent is null || resolution.CatalystParent is null))
        {
            return Failed(
                FusionRuntimeDiagnosticCode.InvalidPolicyResult,
                $"Fusion policy '{resultPolicyId?.ToString() ?? "unidentified"}' returned an incomplete stat-boost result.");
        }

        return Successful(
            resolution.Operation,
            resultEntityId,
            isAccident,
            resolution.TransformedParent,
            resolution.CatalystParent,
            matchedRecipe,
            resultPolicyId,
            resolution.ResultStats);
    }

    private static FusionResolvedResult Successful(
        FusionRuntimeOperation operation,
        ContentId resultEntityId,
        bool isAccident,
        FusionParticipantSnapshot? transformedParent = null,
        FusionParticipantSnapshot? catalystParent = null,
        FusionRecipeSnapshot? matchedRecipe = null,
        ContentId? resultPolicyId = null,
        IReadOnlyDictionary<ContentId, int>? resultStats = null) =>
        new(
            operation,
            resultEntityId,
            isAccident,
            transformedParent,
            catalystParent,
            matchedRecipe,
            resultPolicyId,
            resultStats ?? new ReadOnlyDictionary<ContentId, int>(new Dictionary<ContentId, int>()),
            Array.AsReadOnly(Array.Empty<FusionRuntimeDiagnostic>()));

    private static FusionResolvedResult Failed(FusionRuntimeDiagnosticCode code, string message) =>
        Failed([new FusionRuntimeDiagnostic(code, message)]);

    private static FusionResolvedResult Failed(IEnumerable<FusionRuntimeDiagnostic> diagnostics) =>
        new(
            FusionRuntimeOperation.NoFusionPossible,
            null,
            false,
            null,
            null,
            null,
            null,
            new ReadOnlyDictionary<ContentId, int>(new Dictionary<ContentId, int>()),
            Array.AsReadOnly(diagnostics.ToArray()));
}

public sealed record FusionInheritanceEntry(
    ContentId SkillId,
    string DisplayName,
    bool IsSelectable,
    string ReasonCode);

public sealed record FusionPlanningRequest(
    FusionParticipantSnapshot FirstParent,
    FusionParticipantSnapshot SecondParent,
    FusionParticipantSnapshot? Sacrifice,
    bool IsSacrificial,
    FusionPolicyContext? PolicyContext = null);

public sealed record FusionPlanningResult
{
    internal FusionPlanningResult(
        FusionResolvedResult result,
        FusionEntitySnapshot? resultEntity,
        FusionParticipantSnapshot? previewBaseline,
        FusionParticipantSnapshot? firstParent,
        FusionParticipantSnapshot? secondParent,
        FusionParticipantSnapshot? sacrifice,
        IReadOnlyList<ContentId> naturalSkillIds,
        IReadOnlyList<ContentId> pickableSkillIds,
        IReadOnlyList<ContentId> exclusiveSkillIds,
        IReadOnlyList<FusionInheritanceEntry> displaySkills,
        int maximumInheritanceSlots,
        FusionSacrificePolicyDecision? sacrificeDecision,
        FusionPolicyContext policyContext,
        FusionInheritancePlan? inheritancePlan = null)
    {
        Result = result;
        ResultEntity = resultEntity;
        PreviewBaseline = previewBaseline;
        FirstParent = firstParent;
        SecondParent = secondParent;
        Sacrifice = sacrifice;
        NaturalSkillIds = naturalSkillIds;
        PickableSkillIds = pickableSkillIds;
        ExclusiveSkillIds = exclusiveSkillIds;
        DisplaySkills = displaySkills;
        MaximumInheritanceSlots = maximumInheritanceSlots;
        SacrificeDecision = sacrificeDecision;
        PolicyContext = policyContext ?? throw new ArgumentNullException(nameof(policyContext));
        InheritancePlan = inheritancePlan;
    }

    public FusionResolvedResult Result { get; }
    public bool IsSuccessful => Result.IsSuccessful && ResultEntity is not null;
    public FusionEntitySnapshot? ResultEntity { get; }
    public FusionParticipantSnapshot? PreviewBaseline { get; }
    public FusionParticipantSnapshot? FirstParent { get; }
    public FusionParticipantSnapshot? SecondParent { get; }
    public FusionParticipantSnapshot? Sacrifice { get; }
    public IReadOnlyList<ContentId> NaturalSkillIds { get; }
    public IReadOnlyList<ContentId> PickableSkillIds { get; }
    public IReadOnlyList<ContentId> ExclusiveSkillIds { get; }
    public IReadOnlyList<FusionInheritanceEntry> DisplaySkills { get; }
    public int MaximumInheritanceSlots { get; }
    public FusionSacrificePolicyDecision? SacrificeDecision { get; }
    public FusionPolicyContext PolicyContext { get; }
    internal FusionInheritancePlan? InheritancePlan { get; }
}

public sealed record FusionAccidentInheritanceMutation(
    ContentId SourceSkillId,
    ContentId ResultSkillId);

public sealed record FusionAccidentInheritanceResult
{
    internal FusionAccidentInheritanceResult(
        IEnumerable<FusionInheritanceSelectionDiagnostic> diagnostics,
        IEnumerable<FusionAccidentInheritanceMutation> mutations,
        ValidatedFusionInheritanceSelection? validatedSelection)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Mutations = Array.AsReadOnly(mutations.ToArray());
        ValidatedSelection = validatedSelection;
    }

    public IReadOnlyList<FusionInheritanceSelectionDiagnostic> Diagnostics { get; }
    public IReadOnlyList<FusionAccidentInheritanceMutation> Mutations { get; }
    public ValidatedFusionInheritanceSelection? ValidatedSelection { get; }
    public bool IsValid => Diagnostics.Count == 0 && ValidatedSelection is not null;

    public ValidatedFusionInheritanceSelection RequireValidSelection() =>
        ValidatedSelection ?? throw new FusionInheritanceSelectionException(Diagnostics);
}

public interface IFusionPlanningService
{
    FusionPlanningResult CreatePlan(FusionPlanningRequest request);
    int GetInheritanceSlotCount(IEnumerable<SkillDefinition> legalSkills);
    int GetInheritanceSlotCount(
        IEnumerable<SkillDefinition> legalSkills,
        FusionPolicyContext context);
    ContentId MutateSkill(ContentId skillId, ContentId policyId, FusionPolicyContext? context = null);
    FusionAccidentInheritanceResult CreateAccidentInheritance(FusionPlanningResult plan);
    FusionInheritanceSelectionResult ValidateInheritanceSelection(
        FusionPlanningResult plan,
        IEnumerable<ContentId> selectedSkillIds);
}

public sealed class FusionPlanningService : IFusionPlanningService
{
    private readonly IFusionContentRepository _content;
    private readonly IFusionResultResolver _resolver;
    private readonly IFusionInheritancePlanner _inheritancePlanner;
    private readonly IFusionInheritanceSelectionValidator _selectionValidator;
    private readonly IRandomSource _random;
    private readonly FusionPolicyRegistry _policies;

    public FusionPlanningService(
        IFusionContentRepository content,
        IFusionResultResolver resolver,
        IRandomSource random,
        FusionPolicyRegistry policies,
        IFusionInheritancePlanner? inheritancePlanner = null,
        IFusionInheritanceSelectionValidator? selectionValidator = null)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _inheritancePlanner = inheritancePlanner ?? new FusionInheritancePlanner();
        _selectionValidator = selectionValidator ?? new FusionInheritanceSelectionValidator();
    }

    public FusionPlanningResult CreatePlan(FusionPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        FusionPolicyContext context = request.PolicyContext ?? FusionPolicyContext.Empty;
        FusionSacrificePolicyDecision? sacrificeDecision = null;
        if (request.IsSacrificial)
        {
            if (request.Sacrifice is null)
            {
                return Empty(FailedPlanningResult(
                    FusionRuntimeDiagnosticCode.InvalidSacrifice,
                    "Sacrificial fusion requires a sacrifice participant."), context);
            }

            sacrificeDecision = _policies.SacrificePolicy.Assess(new FusionSacrificePolicyRequest(
                request.FirstParent,
                request.SecondParent,
                request.Sacrifice,
                context));
            if (!sacrificeDecision.IsAllowed)
            {
                return Empty(FailedPlanningResult(
                    FusionRuntimeDiagnosticCode.SacrificeNotAllowed,
                    sacrificeDecision.RejectionMessage ?? "Sacrificial fusion is not enabled."), context);
            }
        }
        else if (request.Sacrifice is not null)
        {
            return Empty(FailedPlanningResult(
                FusionRuntimeDiagnosticCode.InvalidSacrifice,
                "A sacrifice participant was supplied for a non-sacrificial fusion request."), context);
        }

        FusionResolvedResult result = _resolver.Resolve(new FusionResultRequest(
            request.FirstParent,
            request.SecondParent,
            context));
        if (!result.IsSuccessful ||
            result.ResultEntityId is not ContentId resultEntityId ||
            !_content.TryGetEntity(resultEntityId, out FusionEntitySnapshot? resultEntity))
        {
            return Empty(result, context);
        }

        if (result.MatchedRecipe?.MutationPolicyId is ContentId mutationPolicyId &&
            !_policies.TryGetMutationPolicy(mutationPolicyId, out _))
        {
            return Empty(FailedPlanningResult(
                FusionRuntimeDiagnosticCode.PolicyNotRegistered,
                $"Fusion mutation policy '{mutationPolicyId}' is not registered."), context);
        }

        // Authored catalyst rank shifts create the exact catalog entity at the shifted rank. A
        // registered custom result policy may still explicitly preserve an identified parent.
        FusionParticipantSnapshot? previewBaseline =
            result.Operation == FusionRuntimeOperation.StatBoost || result.ResultPolicyId is not null
            ? result.TransformedParent
            : null;

        IReadOnlyList<ContentId> naturalSkills = result.Operation == FusionRuntimeOperation.StatBoost
            ? Snapshot(previewBaseline?.SkillIds)
            : Snapshot(resultEntity!.BaseSkillIds);

        List<SkillDefinition> candidates = CandidateSkillDefinitions(
            request.FirstParent,
            request.SecondParent,
            request.Sacrifice);

        FusionInheritancePlan inheritancePlan = _inheritancePlanner.CreatePlan(new FusionInheritancePlanRequest(
            resultEntity!.Definition,
            candidates,
            naturalSkills,
            maximumSelections: int.MaxValue));

        var display = new List<FusionInheritanceEntry>();
        var pickable = new List<ContentId>();
        var exclusive = new List<ContentId>();
        foreach (FusionInheritanceCandidate candidate in inheritancePlan.Candidates)
        {
            if (candidate.IsSelectable)
            {
                pickable.Add(candidate.Skill.Id);
            }
            else if (!candidate.PolicyDecision.IsAllowed)
            {
                exclusive.Add(candidate.Skill.Id);
            }

            display.Add(new FusionInheritanceEntry(
                candidate.Skill.Id,
                candidate.Skill.DisplayName,
                candidate.IsSelectable,
                candidate.AvailabilityReasonCode));
        }

        SkillDefinition[] legalSkills = inheritancePlan.Candidates
            .Where(candidate => candidate.PolicyDecision.IsAllowed)
            .Select(candidate => candidate.Skill)
            .ToArray();
        int maxSlots = _policies.InheritanceSlotPolicy.GetMaximumSlots(
            new FusionInheritanceSlotPolicyRequest(
                Array.AsReadOnly(legalSkills),
                sacrificeDecision?.AdditionalInheritanceSlots ?? 0,
                context));
        FusionInheritancePlan selectionPlan = inheritancePlan.WithMaximumSelections(maxSlots);

        return new FusionPlanningResult(
            result,
            resultEntity,
            previewBaseline,
            request.FirstParent,
            request.SecondParent,
            request.Sacrifice,
            naturalSkills,
            Snapshot(pickable),
            Snapshot(exclusive),
            Snapshot(display),
            maxSlots,
            sacrificeDecision,
            context,
            selectionPlan);
    }

    public int GetInheritanceSlotCount(IEnumerable<SkillDefinition> legalSkills) =>
        GetInheritanceSlotCount(legalSkills, FusionPolicyContext.Empty);

    public int GetInheritanceSlotCount(
        IEnumerable<SkillDefinition> legalSkills,
        FusionPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(legalSkills);
        ArgumentNullException.ThrowIfNull(context);
        return _policies.InheritanceSlotPolicy.GetMaximumSlots(
            new FusionInheritanceSlotPolicyRequest(
                Array.AsReadOnly(legalSkills.ToArray()),
                0,
                context));
    }

    public ContentId MutateSkill(
        ContentId skillId,
        ContentId policyId,
        FusionPolicyContext? context = null)
    {
        if (!_policies.TryGetMutationPolicy(policyId, out IFusionMutationPolicy? policy) || policy is null)
        {
            throw new InvalidOperationException($"Fusion mutation policy '{policyId}' is not registered.");
        }

        return policy.Mutate(
            new FusionMutationPolicyRequest(skillId, _content, context ?? FusionPolicyContext.Empty),
            _random);
    }

    public FusionAccidentInheritanceResult CreateAccidentInheritance(FusionPlanningResult plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsSuccessful || plan.InheritancePlan is null)
        {
            return new FusionAccidentInheritanceResult(
                [new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.PlanUnavailable,
                    "The fusion plan does not contain an authoritative inheritance plan.")],
                [],
                validatedSelection: null);
        }

        SkillDefinition[] selectedSources = plan.InheritancePlan.Candidates
            .Where(candidate => candidate.IsSelectable)
            .Select(candidate => candidate.Skill)
            .DistinctBy(skill => skill.Id)
            .Select(skill => new KeyValuePair<int, SkillDefinition>(
                _random.NextInt32(0, int.MaxValue),
                skill))
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .Take(plan.MaximumInheritanceSlots)
            .ToArray();

        ContentId? mutationPolicyId = plan.Result.MatchedRecipe?.MutationPolicyId ??
            _policies.DefaultMutationPolicyId;
        var mutations = new List<FusionAccidentInheritanceMutation>(selectedSources.Length);
        var selectedSkills = new List<SkillDefinition>(selectedSources.Length);
        var diagnostics = new List<FusionInheritanceSelectionDiagnostic>();
        var seenResultIds = new HashSet<ContentId>();
        foreach (SkillDefinition sourceSkill in selectedSources)
        {
            ContentId resultId = mutationPolicyId is ContentId policyId
                ? MutateSkill(sourceSkill.Id, policyId, plan.PolicyContext)
                : sourceSkill.Id;
            mutations.Add(new FusionAccidentInheritanceMutation(sourceSkill.Id, resultId));

            if (!seenResultIds.Add(resultId))
            {
                diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.SkillDuplicate,
                    $"Accident mutation produced skill '{resultId}' more than once.",
                    resultId));
                continue;
            }

            if (!_content.TryGetSkill(resultId, out SkillDefinition? resultSkill) || resultSkill is null)
            {
                diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.SkillUnknown,
                    $"Accident mutation produced unknown skill '{resultId}'.",
                    resultId));
                continue;
            }

            FusionInheritanceDecision decision = plan.InheritancePlan.Evaluator.Evaluate(
                plan.InheritancePlan.ReceivingEntity,
                resultSkill);
            if (!decision.IsAllowed)
            {
                diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.SkillIneligible,
                    $"Accident mutation produced ineligible skill '{resultId}': {decision.ReasonCode}.",
                    resultId,
                    decision.Code));
                continue;
            }

            selectedSkills.Add(resultSkill);
        }

        ValidatedFusionInheritanceSelection? validatedSelection = diagnostics.Count == 0
            ? new ValidatedFusionInheritanceSelection(
                plan.InheritancePlan.Authority,
                plan.InheritancePlan.ReceivingEntityId,
                plan.MaximumInheritanceSlots,
                selectedSkills)
            : null;
        return new FusionAccidentInheritanceResult(diagnostics, mutations, validatedSelection);
    }

    public FusionInheritanceSelectionResult ValidateInheritanceSelection(
        FusionPlanningResult plan,
        IEnumerable<ContentId> selectedSkillIds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selectedSkillIds);

        if (!plan.IsSuccessful || plan.InheritancePlan is null)
        {
            return new FusionInheritanceSelectionResult(
                [new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.PlanUnavailable,
                    "The fusion plan does not contain an authoritative inheritance plan.")],
                validatedSelection: null);
        }

        return _selectionValidator.Validate(plan.InheritancePlan, selectedSkillIds);
    }

    private List<SkillDefinition> CandidateSkillDefinitions(params FusionParticipantSnapshot?[] participants)
    {
        var result = new List<SkillDefinition>();
        var seen = new HashSet<ContentId>();
        foreach (FusionParticipantSnapshot? participant in participants)
        {
            foreach (ContentId skillId in participant?.SkillIds ?? [])
            {
                if (!seen.Add(skillId))
                {
                    continue;
                }

                if (_content.TryGetSkill(skillId, out SkillDefinition? skill) && skill is not null)
                {
                    result.Add(skill);
                }
            }
        }

        return result;
    }

    private static FusionPlanningResult Empty(
        FusionResolvedResult result,
        FusionPolicyContext context) =>
        new(
            result,
            null,
            null,
            null,
            null,
            null,
            Array.AsReadOnly(Array.Empty<ContentId>()),
            Array.AsReadOnly(Array.Empty<ContentId>()),
            Array.AsReadOnly(Array.Empty<ContentId>()),
            Array.AsReadOnly(Array.Empty<FusionInheritanceEntry>()),
            0,
            null,
            context);

    private static FusionResolvedResult FailedPlanningResult(
        FusionRuntimeDiagnosticCode code,
        string message) =>
        new(
            FusionRuntimeOperation.NoFusionPossible,
            null,
            false,
            null,
            null,
            null,
            null,
            new ReadOnlyDictionary<ContentId, int>(new Dictionary<ContentId, int>()),
            Array.AsReadOnly([new FusionRuntimeDiagnostic(code, message)]));
}

public sealed record FusionPreviewRequest
{
    public FusionPreviewRequest(
        FusionPlanningResult plan,
        ValidatedFusionInheritanceSelection inheritanceSelection)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        InheritanceSelection = inheritanceSelection ??
            throw new ArgumentNullException(nameof(inheritanceSelection));
    }

    public FusionPlanningResult Plan { get; }
    public ValidatedFusionInheritanceSelection InheritanceSelection { get; }
}

internal static class FusionValidatedSelectionRules
{
    public static bool BelongsToPlan(
        FusionPlanningResult plan,
        ValidatedFusionInheritanceSelection selection)
    {
        if (!plan.IsSuccessful || plan.ResultEntity is null || plan.InheritancePlan is null ||
            !ReferenceEquals(selection.PlanAuthority, plan.InheritancePlan.Authority) ||
            selection.ReceivingEntityId != plan.ResultEntity.Id ||
            selection.MaximumSelections != plan.MaximumInheritanceSlots ||
            selection.SelectedSkillIds.Count > plan.MaximumInheritanceSlots ||
            selection.SelectedSkillIds.Distinct().Count() != selection.SelectedSkillIds.Count)
        {
            return false;
        }

        return selection.SelectedSkills.All(skill =>
            plan.InheritancePlan.Evaluator
                .Evaluate(plan.InheritancePlan.ReceivingEntity, skill)
                .IsAllowed);
    }
}

public sealed record FusionPreviewSnapshot
{
    public FusionPreviewSnapshot(
        ContentId entityId,
        string displayName,
        ContentId raceId,
        int rank,
        int level,
        IEnumerable<ContentId>? naturalSkillIds = null,
        IEnumerable<ContentId>? inheritedSkillIds = null,
        IEnumerable<KeyValuePair<ContentId, int>>? stats = null,
        long experience = 0,
        long lifetimeExperience = 0)
    {
        if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));

        EntityId = entityId;
        DisplayName = displayName;
        RaceId = raceId;
        Rank = rank;
        Level = level;
        NaturalSkillIds = Snapshot(naturalSkillIds);
        InheritedSkillIds = Snapshot(inheritedSkillIds);
        Stats = SnapshotDictionary(stats);
        Experience = experience;
        LifetimeExperience = lifetimeExperience;
    }

    public ContentId EntityId { get; }
    public string DisplayName { get; }
    public ContentId RaceId { get; }
    public int Rank { get; }
    public int Level { get; }
    public IReadOnlyList<ContentId> NaturalSkillIds { get; }
    public IReadOnlyList<ContentId> InheritedSkillIds { get; }
    public IReadOnlyDictionary<ContentId, int> Stats { get; }
    public long Experience { get; }
    public long LifetimeExperience { get; }
}

public interface IFusionPreviewService
{
    FusionPreviewSnapshot? CreatePreview(FusionPreviewRequest request);
}

public sealed class FusionPreviewService : IFusionPreviewService
{
    public FusionPreviewSnapshot? CreatePreview(FusionPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        FusionPlanningResult plan = request.Plan;
        if (!FusionValidatedSelectionRules.BelongsToPlan(plan, request.InheritanceSelection) ||
            plan.ResultEntity is null)
        {
            return null;
        }

        FusionEntitySnapshot entity = plan.ResultEntity;
        int level = entity.BaseLevel;
        long experience = 0;
        long lifetimeExperience = 0;
        Dictionary<ContentId, int> stats = new(entity.Stats);
        IReadOnlyList<ContentId> naturalSkills = plan.NaturalSkillIds;

        if (plan.Result.Operation == FusionRuntimeOperation.StatBoost && plan.PreviewBaseline is not null)
        {
            level = plan.PreviewBaseline.Level;
            experience = plan.PreviewBaseline.Experience;
            lifetimeExperience = plan.PreviewBaseline.LifetimeExperience;
            stats = plan.Result.ResultStats.Count > 0
                ? new Dictionary<ContentId, int>(plan.Result.ResultStats)
                : new Dictionary<ContentId, int>(plan.PreviewBaseline.Stats);
            naturalSkills = plan.PreviewBaseline.SkillIds;
        }
        else if (plan.Result.Operation is FusionRuntimeOperation.RankUpParent or FusionRuntimeOperation.RankDownParent &&
                 plan.PreviewBaseline is not null)
        {
            foreach ((ContentId statId, int value) in plan.PreviewBaseline.Stats)
            {
                stats[statId] = value;
            }
        }

        return new FusionPreviewSnapshot(
            entity.Id,
            entity.DisplayName,
            entity.RaceId,
            entity.Rank,
            level,
            naturalSkills,
            request.InheritanceSelection.SelectedSkillIds,
            stats,
            experience,
            lifetimeExperience);
    }
}

public enum CompendiumRegistrationCode
{
    Added,
    Updated,
    AlreadyRegistered,
    InvalidEntry
}

public enum CompendiumRecallCode
{
    Available,
    MissingEntry,
    DuplicateOwned,
    RosterFull,
    RecallUnavailable,
    InsufficientCurrency
}

public sealed record CompendiumEntrySnapshot
{
    public CompendiumEntrySnapshot(
        ContentId speciesId,
        string displayName,
        int level,
        IEnumerable<KeyValuePair<ContentId, int>>? stats = null,
        IEnumerable<ContentId>? skillIds = null,
        long experience = 0,
        long lifetimeExperience = 0,
        int unspentStatPoints = 0,
        IEnumerable<ContentId>? equippedSkillIds = null)
    {
        if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        if (lifetimeExperience < 0) throw new ArgumentOutOfRangeException(nameof(lifetimeExperience));
        if (unspentStatPoints < 0) throw new ArgumentOutOfRangeException(nameof(unspentStatPoints));

        SpeciesId = speciesId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? speciesId.ToString() : displayName;
        Level = level;
        Stats = SnapshotDictionary(stats);
        SkillIds = Snapshot(skillIds);
        EquippedSkillIds = Snapshot(equippedSkillIds ?? SkillIds);
        Experience = experience;
        LifetimeExperience = lifetimeExperience;
        UnspentStatPoints = unspentStatPoints;
    }

    public ContentId EntityId => SpeciesId;
    public ContentId SpeciesId { get; }
    public string DisplayName { get; }
    public int Level { get; }
    public IReadOnlyDictionary<ContentId, int> Stats { get; }
    public IReadOnlyList<ContentId> SkillIds { get; }
    public IReadOnlyList<ContentId> EquippedSkillIds { get; }
    public long Experience { get; }
    public long LifetimeExperience { get; }
    public int UnspentStatPoints { get; }
}

public sealed record CompendiumStateSnapshot
{
    public CompendiumStateSnapshot(IEnumerable<CompendiumEntrySnapshot>? entries = null)
    {
        Entries = Snapshot(entries?.OrderBy(entry => entry.Level).ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<CompendiumEntrySnapshot> Entries { get; }

    public bool TryGet(ContentId speciesId, out CompendiumEntrySnapshot? entry)
    {
        entry = Entries.FirstOrDefault(candidate => candidate.SpeciesId == speciesId);
        return entry is not null;
    }
}

public sealed record CompendiumRegistrationResult(
    CompendiumRegistrationCode Code,
    CompendiumStateSnapshot Before,
    CompendiumStateSnapshot After,
    CompendiumEntrySnapshot? Entry);

public sealed record CompendiumRecallAssessment(
    CompendiumRecallCode Code,
    CompendiumEntrySnapshot? Entry,
    int Cost,
    IEnumerable<FusionRuntimeDiagnostic>? diagnostics = null)
{
    public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; } = Snapshot(diagnostics);
    public bool CanRecall => Code == CompendiumRecallCode.Available;
}

public interface ICompendiumService
{
    CompendiumRegistrationResult Register(CompendiumStateSnapshot state, CompendiumEntrySnapshot entry);
    CompendiumRegistrationResult RecordAcquisition(
        CompendiumStateSnapshot state,
        CompendiumEntrySnapshot entry);
    CompendiumRecallPricingDecision GetRecallPricing(CompendiumEntrySnapshot entry, int? basePrice = null);
    CompendiumRecallAssessment AssessRecall(
        CompendiumStateSnapshot state,
        ContentId speciesId,
        int availableCurrency,
        bool alreadyOwned,
        bool hasOpenRosterSlot,
        int? basePrice = null);
}

public sealed class CompendiumService : ICompendiumService
{
    private readonly ICompendiumRecallPricingPolicy? _recallPricing;

    public CompendiumService(ICompendiumRecallPricingPolicy? recallPricing = null)
    {
        _recallPricing = recallPricing;
    }

    public CompendiumRegistrationResult Register(CompendiumStateSnapshot state, CompendiumEntrySnapshot entry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entry);

        bool exists = state.Entries.Any(existing => existing.SpeciesId == entry.SpeciesId);
        CompendiumEntrySnapshot[] entries = state.Entries
            .Where(existing => existing.SpeciesId != entry.SpeciesId)
            .Append(entry)
            .ToArray();
        CompendiumStateSnapshot after = new(entries);
        return new CompendiumRegistrationResult(
            exists ? CompendiumRegistrationCode.Updated : CompendiumRegistrationCode.Added,
            state,
            after,
            entry);
    }

    public CompendiumRegistrationResult RecordAcquisition(
        CompendiumStateSnapshot state,
        CompendiumEntrySnapshot entry)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entry);

        if (state.TryGet(entry.SpeciesId, out CompendiumEntrySnapshot? existing) && existing is not null)
        {
            return new CompendiumRegistrationResult(
                CompendiumRegistrationCode.AlreadyRegistered,
                state,
                state,
                existing);
        }

        return Register(state, entry);
    }

    public CompendiumRecallPricingDecision GetRecallPricing(
        CompendiumEntrySnapshot entry,
        int? basePrice = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var request = new CompendiumRecallPricingRequest(entry, basePrice);
        return _recallPricing?.GetPricing(request) ??
            CompendiumRecallPricingDecision.Unavailable(
                "Compendium recall is not enabled by the active host policy.");
    }

    public CompendiumRecallAssessment AssessRecall(
        CompendiumStateSnapshot state,
        ContentId speciesId,
        int availableCurrency,
        bool alreadyOwned,
        bool hasOpenRosterSlot,
        int? basePrice = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (availableCurrency < 0) throw new ArgumentOutOfRangeException(nameof(availableCurrency));

        if (!state.TryGet(speciesId, out CompendiumEntrySnapshot? entry) || entry is null)
        {
            return RecallRejected(CompendiumRecallCode.MissingEntry, "The Compendium entry does not exist.", speciesId);
        }

        CompendiumRecallPricingDecision pricing = GetRecallPricing(entry, basePrice);
        if (!pricing.IsAvailable)
        {
            return RecallRejected(
                CompendiumRecallCode.RecallUnavailable,
                pricing.RejectionMessage ?? "Compendium recall is not available.",
                speciesId,
                entry);
        }

        int cost = pricing.Cost;
        if (alreadyOwned)
        {
            return RecallRejected(CompendiumRecallCode.DuplicateOwned, "The Compendium entry is already owned.", speciesId, entry, cost);
        }
        if (!hasOpenRosterSlot)
        {
            return RecallRejected(CompendiumRecallCode.RosterFull, "There is no open roster slot for the recalled entry.", speciesId, entry, cost);
        }
        if (availableCurrency < cost)
        {
            return RecallRejected(
                CompendiumRecallCode.InsufficientCurrency,
                "There is not enough available currency to recall this entry.",
                speciesId,
                entry,
                cost);
        }

        return new CompendiumRecallAssessment(CompendiumRecallCode.Available, entry, cost);
    }

    private static CompendiumRecallAssessment RecallRejected(
        CompendiumRecallCode code,
        string message,
        ContentId speciesId,
        CompendiumEntrySnapshot? entry = null,
        int cost = 0) =>
        new(
            code,
            entry,
            cost,
            [new FusionRuntimeDiagnostic(code switch
            {
                CompendiumRecallCode.MissingEntry => FusionRuntimeDiagnosticCode.MissingEntity,
                CompendiumRecallCode.DuplicateOwned => FusionRuntimeDiagnosticCode.DuplicateResult,
                CompendiumRecallCode.RosterFull => FusionRuntimeDiagnosticCode.RosterFull,
                CompendiumRecallCode.RecallUnavailable => FusionRuntimeDiagnosticCode.RecallUnavailable,
                CompendiumRecallCode.InsufficientCurrency => FusionRuntimeDiagnosticCode.InsufficientCurrency,
                _ => FusionRuntimeDiagnosticCode.NoFusionPossible
            }, message, speciesId)]);
}

internal static class FusionRuntimeCollections
{
    public static IReadOnlyList<T> Snapshot<T>(IEnumerable<T>? values) =>
        Array.AsReadOnly((values ?? []).ToArray());

    public static IReadOnlyDictionary<TKey, TValue> SnapshotDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(values ?? []));
}
