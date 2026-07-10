using System.Collections.ObjectModel;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Fusion.Inheritance;
using JRPGPrototype.Logic.Runtime;
using static JRPGPrototype.Logic.Fusion.FusionRuntimeCollections;

namespace JRPGPrototype.Logic.Fusion;

public enum FusionRuntimeOperation
{
    CreateNewEntity,
    RankUpParent,
    RankDownParent,
    StatBoost,
    NoFusionPossible
}

public enum FusionParticipantStockKind
{
    Demon,
    Persona
}

public enum FusionRuntimeDiagnosticCode
{
    None,
    MissingParentForm,
    NoRecipe,
    NoFusionPossible,
    MissingEntity,
    MissingRaceMembers,
    MitamaPairUnsupported,
    ElementCannotReceiveStatBoost,
    DuplicateResult,
    StockFull,
    InsufficientCurrency,
    InvalidSelection
}

public sealed record FusionRuntimeDiagnostic(
    FusionRuntimeDiagnosticCode Code,
    string Message,
    ContentId? ContentId = null,
    RuntimeInstanceId? InstanceId = null);

public sealed record FusionRecipeResultSnapshot(
    FusionResultOperationKind Operation,
    ContentId? ResultEntityId = null,
    ContentId? ResultRaceId = null,
    int? RankOffset = null,
    ContentId? PolicyId = null);

public sealed record FusionRecipeSnapshot(
    ContentId ParentAId,
    ContentId ParentBId,
    string ResultToken,
    FusionRecipeResultSnapshot? Result = null);

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
    int MoonPhase);

public sealed record FusionResolvedResult
{
    internal FusionResolvedResult(
        FusionRuntimeOperation operation,
        ContentId? resultEntityId,
        bool isAccident,
        FusionParticipantSnapshot? transformedParent,
        FusionParticipantSnapshot? catalystParent,
        IReadOnlyList<FusionRuntimeDiagnostic> diagnostics)
    {
        Operation = operation;
        ResultEntityId = resultEntityId;
        IsAccident = isAccident;
        TransformedParent = transformedParent;
        CatalystParent = catalystParent;
        Diagnostics = diagnostics;
    }

    public FusionRuntimeOperation Operation { get; }
    public ContentId? ResultEntityId { get; }
    public bool IsAccident { get; }
    public FusionParticipantSnapshot? TransformedParent { get; }
    public FusionParticipantSnapshot? CatalystParent { get; }
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
    private static readonly ContentId MitamaRaceId = ContentId.Parse("mitama");
    private static readonly ContentId ElementRaceId = ContentId.Parse("element");

    private readonly IFusionContentRepository _content;
    private readonly IRandomSource _random;

    public FusionResultResolver(IFusionContentRepository content, IRandomSource random)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public FusionResolvedResult Resolve(FusionResultRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool isAccident = RollAccident(request.MoonPhase);
        FusionParticipantSnapshot a = request.FirstParent;
        FusionParticipantSnapshot b = request.SecondParent;

        bool aIsMitama = a.RaceId == MitamaRaceId;
        bool bIsMitama = b.RaceId == MitamaRaceId;
        if (aIsMitama || bIsMitama)
        {
            if (aIsMitama && bIsMitama)
            {
                return Failed(FusionRuntimeDiagnosticCode.MitamaPairUnsupported, "Mitama + Mitama fusion is not supported.");
            }

            FusionParticipantSnapshot target = aIsMitama ? b : a;
            FusionParticipantSnapshot catalyst = aIsMitama ? a : b;
            if (target.RaceId == ElementRaceId)
            {
                return Failed(FusionRuntimeDiagnosticCode.ElementCannotReceiveStatBoost, "Elements cannot receive Mitama stat boosts.");
            }

            return Successful(FusionRuntimeOperation.StatBoost, target.EntityId, isAccident, target, catalyst);
        }

        FusionRecipeSnapshot? recipe =
            FindRecipe(a.EntityId, b.EntityId) ??
            FindRecipe(a.RaceId, b.RaceId);

        if (recipe is null)
        {
            return Failed(FusionRuntimeDiagnosticCode.NoRecipe, "No fusion recipe matched the selected parents.");
        }

        if (recipe.Result is not null)
        {
            return ResolveAuthoredResult(recipe.Result, a, b, isAccident);
        }

        string token = recipe.ResultToken;
        if (TryToken(token, out ContentId tokenId) && _content.TryGetEntity(tokenId, out _))
        {
            return Successful(FusionRuntimeOperation.CreateNewEntity, tokenId, isAccident);
        }

        if (token == "1" || token == "-1")
        {
            FusionParticipantSnapshot? parentToRank = a.RaceId != ElementRaceId ? a : b.RaceId != ElementRaceId ? b : null;
            if (parentToRank is null)
            {
                return Failed(FusionRuntimeDiagnosticCode.NoFusionPossible, "No non-Element parent can receive the rank operation.");
            }

            int rankDirection = token == "1" ? 1 : -1;
            int targetRank = parentToRank.Rank + rankDirection;
            FusionEntitySnapshot? ranked = _content.GetEntitiesByRace(parentToRank.RaceId)
                .FirstOrDefault(entity => entity.Rank == targetRank);
            return ranked is null
                ? Failed(FusionRuntimeDiagnosticCode.NoFusionPossible, "No entity exists at the target rank.")
                : Successful(
                    rankDirection > 0 ? FusionRuntimeOperation.RankUpParent : FusionRuntimeOperation.RankDownParent,
                    ranked.Id,
                    isAccident,
                    parentToRank);
        }

        if (!TryToken(token, out ContentId raceId))
        {
            return Failed(FusionRuntimeDiagnosticCode.NoFusionPossible, $"Fusion result token '{token}' is not a valid entity or race ID.");
        }

        if (!_content.TryGetEntity(a.EntityId, out FusionEntitySnapshot? templateA) ||
            !_content.TryGetEntity(b.EntityId, out FusionEntitySnapshot? templateB))
        {
            return Failed(FusionRuntimeDiagnosticCode.MissingEntity, "A selected parent has no entity template.");
        }

        FusionEntitySnapshot[] racePool = _content.GetEntitiesByRace(raceId)
            .OrderBy(entity => entity.BaseLevel)
            .ThenBy(entity => entity.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (racePool.Length == 0)
        {
            return Failed(FusionRuntimeDiagnosticCode.MissingRaceMembers, $"Race '{raceId}' has no fusion results.");
        }

        FusionEntitySnapshot result;
        if (isAccident)
        {
            result = racePool[0];
        }
        else
        {
            FusionEntitySnapshot parentTemplateA = templateA ?? throw new InvalidOperationException("Parent A template disappeared during fusion resolution.");
            FusionEntitySnapshot parentTemplateB = templateB ?? throw new InvalidOperationException("Parent B template disappeared during fusion resolution.");
            int averageBaseLevel = (parentTemplateA.BaseLevel + parentTemplateB.BaseLevel) / 2;
            int targetLevel = averageBaseLevel + _random.NextInt32(1, 6);
            result = racePool.FirstOrDefault(entity => entity.BaseLevel >= targetLevel) ?? racePool[^1];

            if (result.Id == parentTemplateA.Id || result.Id == parentTemplateB.Id)
            {
                int index = Array.IndexOf(racePool, result);
                if (index + 1 < racePool.Length)
                {
                    result = racePool[index + 1];
                }
            }
        }

        return Successful(FusionRuntimeOperation.CreateNewEntity, result.Id, isAccident);
    }

    public ContentId? TryResolveDirectCreateResult(
        ContentId firstParentId,
        ContentId firstRaceId,
        ContentId secondParentId,
        ContentId secondRaceId)
    {
        FusionRecipeSnapshot? recipe =
            FindRecipe(firstParentId, secondParentId) ??
            FindRecipe(firstRaceId, secondRaceId);

        if (recipe is null || !TryToken(recipe.ResultToken, out ContentId resultId))
        {
            if (recipe?.Result is { Operation: FusionResultOperationKind.CreateEntity, ResultEntityId: ContentId authoredResultId } &&
                _content.TryGetEntity(authoredResultId, out _))
            {
                return authoredResultId;
            }

            return null;
        }

        return _content.TryGetEntity(resultId, out _) ? resultId : null;
    }

    private FusionResolvedResult ResolveAuthoredResult(
        FusionRecipeResultSnapshot result,
        FusionParticipantSnapshot a,
        FusionParticipantSnapshot b,
        bool isAccident)
    {
        return result.Operation switch
        {
            FusionResultOperationKind.CreateEntity => ResolveAuthoredCreateEntity(result, isAccident),
            FusionResultOperationKind.RankOffset => ResolveAuthoredRankOffset(result, a, b, isAccident),
            _ => Failed(
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                $"Fusion result operation '{result.Operation}' is not supported by the runtime resolver.")
        };
    }

    private FusionResolvedResult ResolveAuthoredCreateEntity(
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
            ? Successful(FusionRuntimeOperation.CreateNewEntity, entityId, isAccident)
            : Failed(FusionRuntimeDiagnosticCode.MissingEntity, $"Fusion result entity '{entityId}' was not found.");
    }

    private FusionResolvedResult ResolveAuthoredRankOffset(
        FusionRecipeResultSnapshot result,
        FusionParticipantSnapshot a,
        FusionParticipantSnapshot b,
        bool isAccident)
    {
        if (result.RankOffset is not int rankOffset || rankOffset == 0)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                "Rank-offset fusion result must specify a nonzero rank offset.");
        }

        FusionParticipantSnapshot? rankedParent = result.ResultRaceId is null
            ? SelectNonElementParent(a, b)
            : null;
        ContentId raceId = result.ResultRaceId ?? rankedParent?.RaceId ?? a.RaceId;
        int baseRank = rankedParent?.Rank ?? (a.Rank + b.Rank) / 2;
        int targetRank = baseRank + rankOffset;

        FusionEntitySnapshot[] racePool = _content.GetEntitiesByRace(raceId)
            .OrderBy(entity => entity.Rank)
            .ThenBy(entity => entity.BaseLevel)
            .ThenBy(entity => entity.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (racePool.Length == 0)
        {
            return Failed(FusionRuntimeDiagnosticCode.MissingRaceMembers, $"Race '{raceId}' has no fusion results.");
        }

        FusionEntitySnapshot ranked = rankOffset > 0
            ? racePool.FirstOrDefault(entity => entity.Rank >= targetRank) ?? racePool[^1]
            : racePool.LastOrDefault(entity => entity.Rank <= targetRank) ?? racePool[0];
        return Successful(
            rankOffset > 0 ? FusionRuntimeOperation.RankUpParent : FusionRuntimeOperation.RankDownParent,
            ranked.Id,
            isAccident,
            rankedParent);
    }

    private bool RollAccident(int moonPhase)
    {
        int accidentThreshold = moonPhase == 8 ? 12 : 1;
        return _random.NextInt32(0, 100) < accidentThreshold;
    }

    private FusionRecipeSnapshot? FindRecipe(ContentId a, ContentId b) =>
        _content.GetRecipes().FirstOrDefault(recipe =>
            recipe.ParentAId == a && recipe.ParentBId == b ||
            recipe.ParentAId == b && recipe.ParentBId == a);

    private static FusionParticipantSnapshot? SelectNonElementParent(
        FusionParticipantSnapshot a,
        FusionParticipantSnapshot b) =>
        a.RaceId != ElementRaceId ? a : b.RaceId != ElementRaceId ? b : null;

    private static bool TryToken(string token, out ContentId id) =>
        ContentId.TryParse(token, out id);

    private static FusionResolvedResult Successful(
        FusionRuntimeOperation operation,
        ContentId resultEntityId,
        bool isAccident,
        FusionParticipantSnapshot? transformedParent = null,
        FusionParticipantSnapshot? catalystParent = null) =>
        new(operation, resultEntityId, isAccident, transformedParent, catalystParent, Array.AsReadOnly(Array.Empty<FusionRuntimeDiagnostic>()));

    private static FusionResolvedResult Failed(FusionRuntimeDiagnosticCode code, string message) =>
        new(
            FusionRuntimeOperation.NoFusionPossible,
            null,
            false,
            null,
            null,
            Array.AsReadOnly(new[] { new FusionRuntimeDiagnostic(code, message) }));
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
    int MoonPhase);

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
        int maximumInheritanceSlots)
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
}

public interface IFusionPlanningService
{
    FusionPlanningResult CreatePlan(FusionPlanningRequest request);
    int GetInheritanceSlotCount(IEnumerable<SkillDefinition> legalSkills);
    ContentId MutateSkill(ContentId skillId);
    IReadOnlyList<ContentId> CreateAccidentInheritance(IReadOnlyList<ContentId> legalSkillIds, int maximumSlots);
}

public sealed class FusionPlanningService : IFusionPlanningService
{
    private readonly IFusionContentRepository _content;
    private readonly IFusionResultResolver _resolver;
    private readonly IFusionInheritancePlanner _inheritancePlanner;
    private readonly IRandomSource _random;

    public FusionPlanningService(
        IFusionContentRepository content,
        IFusionResultResolver resolver,
        IRandomSource random,
        IFusionInheritancePlanner? inheritancePlanner = null)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _inheritancePlanner = inheritancePlanner ?? new FusionInheritancePlanner();
    }

    public FusionPlanningResult CreatePlan(FusionPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        FusionResolvedResult result = _resolver.Resolve(new FusionResultRequest(
            request.FirstParent,
            request.SecondParent,
            request.MoonPhase));
        if (!result.IsSuccessful ||
            result.ResultEntityId is not ContentId resultEntityId ||
            !_content.TryGetEntity(resultEntityId, out FusionEntitySnapshot? resultEntity))
        {
            return Empty(result);
        }

        FusionParticipantSnapshot? previewBaseline = result.Operation == FusionRuntimeOperation.StatBoost
            ? result.TransformedParent
            : request.FirstParent.RaceId != ContentId.Parse("element")
                ? request.FirstParent
                : request.SecondParent;

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
        int legalCount = 0;
        foreach (FusionInheritanceCandidate candidate in inheritancePlan.Candidates)
        {
            if (candidate.PolicyDecision.IsAllowed)
            {
                legalCount++;
            }

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

        int maxSlots = GetInheritanceSlotCount(
            inheritancePlan.Candidates
                .Where(candidate => candidate.PolicyDecision.IsAllowed)
                .Select(candidate => candidate.Skill));
        if (request.IsSacrificial)
        {
            maxSlots += 2;
        }

        maxSlots = Math.Min(8, maxSlots);

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
            maxSlots);
    }

    public int GetInheritanceSlotCount(IEnumerable<SkillDefinition> legalSkills)
    {
        ArgumentNullException.ThrowIfNull(legalSkills);
        int uniqueSkillCount = legalSkills.Select(skill => skill.Id).Distinct().Count();
        if (uniqueSkillCount >= 24) return 6;
        if (uniqueSkillCount >= 19) return 5;
        if (uniqueSkillCount >= 14) return 4;
        if (uniqueSkillCount >= 10) return 3;
        if (uniqueSkillCount >= 7) return 2;
        return 1;
    }

    public ContentId MutateSkill(ContentId skillId)
    {
        if (!_content.TryGetSkill(skillId, out SkillDefinition? current) ||
            current is null ||
            current.Mutation is null)
        {
            return skillId;
        }

        int direction = _random.NextInt32(0, 2) == 0 ? 1 : -1;
        if (current.Mutation.Tier == 1 && direction == -1)
        {
            direction = 1;
        }

        int targetTier = current.Mutation.Tier + direction;
        SkillDefinition? mutation = _content.GetSkills().FirstOrDefault(skill =>
            skill.Mutation is not null &&
            skill.Mutation.FamilyId == current.Mutation.FamilyId &&
            skill.Mutation.Tier == targetTier);
        return mutation?.Id ?? skillId;
    }

    public IReadOnlyList<ContentId> CreateAccidentInheritance(IReadOnlyList<ContentId> legalSkillIds, int maximumSlots)
    {
        ArgumentNullException.ThrowIfNull(legalSkillIds);
        if (maximumSlots < 0) throw new ArgumentOutOfRangeException(nameof(maximumSlots));

        List<ContentId> shuffled = legalSkillIds
            .Distinct()
            .Select(id => new KeyValuePair<int, ContentId>(_random.NextInt32(0, int.MaxValue), id))
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .Take(maximumSlots)
            .ToList();

        for (int i = 0; i < shuffled.Count; i++)
        {
            if (_random.NextInt32(0, 100) < 20)
            {
                shuffled[i] = MutateSkill(shuffled[i]);
            }
        }

        return Snapshot(shuffled);
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

    private static FusionPlanningResult Empty(FusionResolvedResult result) =>
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
            0);
}

public sealed record FusionPreviewRequest(
    FusionPlanningResult Plan,
    IEnumerable<ContentId> SelectedSkillIds);

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
    private static readonly ContentId Strength = ContentId.Parse("strength");
    private static readonly ContentId Magic = ContentId.Parse("magic");
    private static readonly ContentId Vitality = ContentId.Parse("vitality");
    private static readonly ContentId Agility = ContentId.Parse("agility");
    private static readonly ContentId Luck = ContentId.Parse("luck");

    public FusionPreviewSnapshot? CreatePreview(FusionPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        FusionPlanningResult plan = request.Plan;
        if (!plan.IsSuccessful || plan.ResultEntity is null)
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
            stats = new Dictionary<ContentId, int>(plan.PreviewBaseline.Stats);
            naturalSkills = plan.PreviewBaseline.SkillIds;
            string catalystKey = $"{plan.Result.CatalystParent?.EntityId} {plan.Result.CatalystParent?.DisplayName}";
            ApplyMitamaBoost(stats, catalystKey);
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
            request.SelectedSkillIds,
            stats,
            experience,
            lifetimeExperience);
    }

    private static void ApplyMitamaBoost(Dictionary<ContentId, int> stats, string resultEntityId)
    {
        // The legacy adapter stores Mitama boosts through the result entity identity.
        // Names are host-owned, so the framework recognizes only the stable normalized IDs.
        if (resultEntityId.Contains("ara", StringComparison.Ordinal))
        {
            Add(stats, Strength, 2);
            Add(stats, Agility, 1);
        }
        else if (resultEntityId.Contains("nigi", StringComparison.Ordinal))
        {
            Add(stats, Magic, 2);
            Add(stats, Luck, 1);
        }
        else if (resultEntityId.Contains("kusi", StringComparison.Ordinal))
        {
            Add(stats, Vitality, 2);
            Add(stats, Agility, 1);
        }
        else if (resultEntityId.Contains("saki", StringComparison.Ordinal))
        {
            Add(stats, Vitality, 2);
            Add(stats, Luck, 1);
        }
    }

    private static void Add(Dictionary<ContentId, int> stats, ContentId id, int value)
    {
        stats.TryGetValue(id, out int current);
        stats[id] = Math.Min(40, current + value);
    }
}

public sealed record FusionTransactionRequest(
    FusionParticipantStockKind OwnerKind,
    FusionPlanningResult Plan,
    IEnumerable<ContentId> SelectedSkillIds,
    bool ResultAlreadyOwned,
    bool HasOpenStockSlot);

public sealed record FusionTransactionAssessment
{
    internal FusionTransactionAssessment(
        bool canCommit,
        IReadOnlyList<FusionRuntimeDiagnostic> diagnostics,
        IReadOnlyList<RuntimeInstanceId> consumedParticipantIds,
        ContentId? resultEntityId)
    {
        CanCommit = canCommit;
        Diagnostics = diagnostics;
        ConsumedParticipantIds = consumedParticipantIds;
        ResultEntityId = resultEntityId;
    }

    public bool CanCommit { get; }
    public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; }
    public IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds { get; }
    public ContentId? ResultEntityId { get; }
}

public interface IFusionTransactionService
{
    FusionTransactionAssessment Assess(FusionTransactionRequest request);
}

public sealed class FusionTransactionService : IFusionTransactionService
{
    public FusionTransactionAssessment Assess(FusionTransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Plan.IsSuccessful)
        {
            return Rejected(FusionRuntimeDiagnosticCode.NoFusionPossible, "The fusion plan has no result.");
        }

        ArgumentNullException.ThrowIfNull(request.SelectedSkillIds);
        ContentId[] authoredSelection = request.SelectedSkillIds.ToArray();
        ContentId[] selectedSkillIds = authoredSelection
            .Distinct()
            .ToArray();
        if (selectedSkillIds.Length != authoredSelection.Length)
        {
            return Rejected(FusionRuntimeDiagnosticCode.InvalidSelection, "The inherited skill selection contains duplicates.");
        }
        if (selectedSkillIds.Length > request.Plan.MaximumInheritanceSlots)
        {
            return Rejected(FusionRuntimeDiagnosticCode.InvalidSelection, "The inherited skill selection exceeds the available fusion slots.");
        }
        foreach (ContentId skillId in selectedSkillIds)
        {
            if (!request.Plan.PickableSkillIds.Contains(skillId))
            {
                return Rejected(FusionRuntimeDiagnosticCode.InvalidSelection, $"Skill '{skillId}' cannot be inherited by this fusion result.", skillId);
            }
        }
        if (request.ResultAlreadyOwned && request.Plan.Result.Operation == FusionRuntimeOperation.CreateNewEntity)
        {
            return Rejected(FusionRuntimeDiagnosticCode.DuplicateResult, "The fusion result is already owned.", request.Plan.Result.ResultEntityId);
        }
        if (!request.HasOpenStockSlot && request.Plan.Result.Operation == FusionRuntimeOperation.CreateNewEntity)
        {
            return Rejected(FusionRuntimeDiagnosticCode.StockFull, "There is no open stock slot for the fusion result.", request.Plan.Result.ResultEntityId);
        }

        return new FusionTransactionAssessment(
            true,
            Array.AsReadOnly(Array.Empty<FusionRuntimeDiagnostic>()),
            ConsumedParticipantIds(request.Plan),
            request.Plan.Result.ResultEntityId);
    }

    private static IReadOnlyList<RuntimeInstanceId> ConsumedParticipantIds(FusionPlanningResult plan)
    {
        IEnumerable<FusionParticipantSnapshot?> consumed = plan.Result.Operation == FusionRuntimeOperation.StatBoost
            ? [plan.Result.CatalystParent]
            : [plan.FirstParent, plan.SecondParent, plan.Sacrifice];
        return Array.AsReadOnly(consumed
            .OfType<FusionParticipantSnapshot>()
            .Select(parent => parent.InstanceId)
            .Distinct()
            .ToArray());
    }

    private static FusionTransactionAssessment Rejected(FusionRuntimeDiagnosticCode code, string message, ContentId? contentId = null) =>
        new(
            false,
            Array.AsReadOnly(new[] { new FusionRuntimeDiagnostic(code, message, contentId) }),
            Array.AsReadOnly(Array.Empty<RuntimeInstanceId>()),
            null);
}

public enum CompendiumRegistrationCode
{
    Added,
    Updated,
    InvalidEntry
}

public enum CompendiumRecallCode
{
    Available,
    MissingEntry,
    DuplicateOwned,
    StockFull,
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
        long lifetimeExperience = 0)
    {
        if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        if (lifetimeExperience < 0) throw new ArgumentOutOfRangeException(nameof(lifetimeExperience));

        SpeciesId = speciesId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? speciesId.ToString() : displayName;
        Level = level;
        Stats = SnapshotDictionary(stats);
        SkillIds = Snapshot(skillIds);
        Experience = experience;
        LifetimeExperience = lifetimeExperience;
    }

    public ContentId SpeciesId { get; }
    public string DisplayName { get; }
    public int Level { get; }
    public IReadOnlyDictionary<ContentId, int> Stats { get; }
    public IReadOnlyList<ContentId> SkillIds { get; }
    public long Experience { get; }
    public long LifetimeExperience { get; }
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
    int CalculateRecallCost(CompendiumEntrySnapshot entry, int? basePrice = null);
    CompendiumRecallAssessment AssessRecall(
        CompendiumStateSnapshot state,
        ContentId speciesId,
        int currentMacca,
        bool alreadyOwned,
        bool hasOpenStockSlot,
        int? basePrice = null);
}

public sealed class CompendiumService : ICompendiumService
{
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

    public int CalculateRecallCost(CompendiumEntrySnapshot entry, int? basePrice = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        int price = basePrice ?? 2000;
        return price + entry.Level * 100 + entry.Stats.Values.Sum() * 50 + entry.SkillIds.Count * 200;
    }

    public CompendiumRecallAssessment AssessRecall(
        CompendiumStateSnapshot state,
        ContentId speciesId,
        int currentMacca,
        bool alreadyOwned,
        bool hasOpenStockSlot,
        int? basePrice = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (currentMacca < 0) throw new ArgumentOutOfRangeException(nameof(currentMacca));

        if (!state.TryGet(speciesId, out CompendiumEntrySnapshot? entry) || entry is null)
        {
            return RecallRejected(CompendiumRecallCode.MissingEntry, "The Compendium entry does not exist.", speciesId);
        }

        int cost = CalculateRecallCost(entry, basePrice);
        if (alreadyOwned)
        {
            return RecallRejected(CompendiumRecallCode.DuplicateOwned, "The Compendium entry is already owned.", speciesId, entry, cost);
        }
        if (!hasOpenStockSlot)
        {
            return RecallRejected(CompendiumRecallCode.StockFull, "There is no open stock slot for the recalled entry.", speciesId, entry, cost);
        }
        if (currentMacca < cost)
        {
            return RecallRejected(CompendiumRecallCode.InsufficientCurrency, "There is not enough Macca to recall this entry.", speciesId, entry, cost);
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
                CompendiumRecallCode.StockFull => FusionRuntimeDiagnosticCode.StockFull,
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
