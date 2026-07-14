using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Hosting;

namespace Convergence.Fusion;

public sealed record FusionPolicyContext
{
    public static FusionPolicyContext Empty { get; } = new();

    public FusionPolicyContext(
        IEnumerable<ContentId>? flags = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? numericValues = null)
    {
        Flags = Array.AsReadOnly((flags ?? []).Distinct().ToArray());
        NumericValues = new ReadOnlyDictionary<ContentId, decimal>(
            (numericValues ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    public IReadOnlyList<ContentId> Flags { get; }
    public IReadOnlyDictionary<ContentId, decimal> NumericValues { get; }

    public bool HasFlag(ContentId flagId) => Flags.Contains(flagId);

    public bool TryGetNumericValue(ContentId valueId, out decimal value) =>
        NumericValues.TryGetValue(valueId, out value);
}

public sealed record FusionInheritanceSlotTier
{
    public FusionInheritanceSlotTier(int minimumLegalSkillCount, int slots)
    {
        if (minimumLegalSkillCount < 0) throw new ArgumentOutOfRangeException(nameof(minimumLegalSkillCount));
        if (slots < 0) throw new ArgumentOutOfRangeException(nameof(slots));

        MinimumLegalSkillCount = minimumLegalSkillCount;
        Slots = slots;
    }

    public int MinimumLegalSkillCount { get; }
    public int Slots { get; }
}

public sealed record FusionInheritanceSlotPolicyRequest(
    IReadOnlyList<SkillDefinition> LegalSkills,
    int SacrificeBonusSlots,
    FusionPolicyContext Context);

public interface IFusionInheritanceSlotPolicy
{
    int GetMaximumSlots(FusionInheritanceSlotPolicyRequest request);
}

public sealed class TieredFusionInheritanceSlotPolicy : IFusionInheritanceSlotPolicy
{
    private readonly IReadOnlyList<FusionInheritanceSlotTier> _tiers;
    private readonly int _maximumSlots;

    public TieredFusionInheritanceSlotPolicy(
        IEnumerable<FusionInheritanceSlotTier> tiers,
        int maximumSlots)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        if (maximumSlots < 0) throw new ArgumentOutOfRangeException(nameof(maximumSlots));

        FusionInheritanceSlotTier[] snapshot = tiers
            .OrderBy(tier => tier.MinimumLegalSkillCount)
            .ToArray();
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("At least one inheritance slot tier is required.", nameof(tiers));
        }

        if (snapshot.Select(tier => tier.MinimumLegalSkillCount).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Inheritance slot tier minimums must be unique.", nameof(tiers));
        }

        _tiers = Array.AsReadOnly(snapshot);
        _maximumSlots = maximumSlots;
    }

    public int GetMaximumSlots(FusionInheritanceSlotPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.LegalSkills);
        if (request.SacrificeBonusSlots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Sacrifice bonus slots cannot be negative.");
        }

        int legalSkillCount = request.LegalSkills.Select(skill => skill.Id).Distinct().Count();
        int baseSlots = _tiers
            .Where(tier => legalSkillCount >= tier.MinimumLegalSkillCount)
            .Select(tier => tier.Slots)
            .DefaultIfEmpty(0)
            .Last();
        return Math.Min(_maximumSlots, checked(baseSlots + request.SacrificeBonusSlots));
    }
}

public sealed record FusionSacrificePolicyRequest(
    FusionParticipantSnapshot FirstParent,
    FusionParticipantSnapshot SecondParent,
    FusionParticipantSnapshot Sacrifice,
    FusionPolicyContext Context);

public sealed record FusionSacrificePolicyDecision(
    bool IsAllowed,
    int AdditionalInheritanceSlots,
    string? RejectionMessage = null);

public interface IFusionSacrificePolicy
{
    FusionSacrificePolicyDecision Assess(FusionSacrificePolicyRequest request);
}

public sealed class FixedFusionSacrificePolicy : IFusionSacrificePolicy
{
    private readonly bool _isAllowed;
    private readonly int _additionalInheritanceSlots;
    private readonly string? _rejectionMessage;

    public FixedFusionSacrificePolicy(
        bool isAllowed,
        int additionalInheritanceSlots = 0,
        string? rejectionMessage = null)
    {
        if (additionalInheritanceSlots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalInheritanceSlots));
        }

        _isAllowed = isAllowed;
        _additionalInheritanceSlots = additionalInheritanceSlots;
        _rejectionMessage = rejectionMessage;
    }

    public FusionSacrificePolicyDecision Assess(FusionSacrificePolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new FusionSacrificePolicyDecision(
            _isAllowed,
            _isAllowed ? _additionalInheritanceSlots : 0,
            _isAllowed ? null : _rejectionMessage ?? "Sacrificial fusion is not enabled by the active policy.");
    }
}

public sealed record FusionAccidentPolicyRequest(
    FusionRecipeSnapshot? Recipe,
    FusionParticipantSnapshot FirstParent,
    FusionParticipantSnapshot SecondParent,
    FusionPolicyContext Context);

public interface IFusionAccidentPolicy
{
    ContentId Id { get; }
    bool IsAccident(FusionAccidentPolicyRequest request, IRandomSource random);
}

public sealed class PercentageFusionAccidentPolicy : IFusionAccidentPolicy
{
    private readonly int _chancePercent;

    public PercentageFusionAccidentPolicy(ContentId id, int chancePercent)
    {
        if (chancePercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(chancePercent));
        Id = id;
        _chancePercent = chancePercent;
    }

    public ContentId Id { get; }

    public bool IsAccident(FusionAccidentPolicyRequest request, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        return _chancePercent == 100 ||
            _chancePercent > 0 && random.NextInt32(0, 100) < _chancePercent;
    }
}

public sealed class ContextualPercentageFusionAccidentPolicy : IFusionAccidentPolicy
{
    private readonly int _defaultChancePercent;
    private readonly ContentId _contextValueId;
    private readonly decimal _matchingValue;
    private readonly int _matchingChancePercent;

    public ContextualPercentageFusionAccidentPolicy(
        ContentId id,
        int defaultChancePercent,
        ContentId contextValueId,
        decimal matchingValue,
        int matchingChancePercent)
    {
        if (defaultChancePercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(defaultChancePercent));
        if (matchingChancePercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(matchingChancePercent));

        Id = id;
        _defaultChancePercent = defaultChancePercent;
        _contextValueId = contextValueId;
        _matchingValue = matchingValue;
        _matchingChancePercent = matchingChancePercent;
    }

    public ContentId Id { get; }

    public bool IsAccident(FusionAccidentPolicyRequest request, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);

        int chance = request.Context.TryGetNumericValue(_contextValueId, out decimal value) && value == _matchingValue
            ? _matchingChancePercent
            : _defaultChancePercent;
        return chance == 100 || chance > 0 && random.NextInt32(0, 100) < chance;
    }
}

public sealed record FusionMutationPolicyRequest(
    ContentId SkillId,
    IFusionContentRepository Content,
    FusionPolicyContext Context);

public interface IFusionMutationPolicy
{
    ContentId Id { get; }
    ContentId Mutate(FusionMutationPolicyRequest request, IRandomSource random);
}

public sealed class AdjacentTierFusionMutationPolicy : IFusionMutationPolicy
{
    private readonly int _chancePercent;

    public AdjacentTierFusionMutationPolicy(ContentId id, int chancePercent)
    {
        if (chancePercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(chancePercent));
        Id = id;
        _chancePercent = chancePercent;
    }

    public ContentId Id { get; }

    public ContentId Mutate(FusionMutationPolicyRequest request, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);

        if (_chancePercent == 0 ||
            _chancePercent < 100 && random.NextInt32(0, 100) >= _chancePercent ||
            !request.Content.TryGetSkill(request.SkillId, out SkillDefinition? current) ||
            current?.Mutation is null)
        {
            return request.SkillId;
        }

        int direction = random.NextInt32(0, 2) == 0 ? 1 : -1;
        if (current.Mutation.Tier == 1 && direction == -1)
        {
            direction = 1;
        }

        int targetTier = current.Mutation.Tier + direction;
        SkillDefinition? mutation = request.Content.GetSkills().FirstOrDefault(skill =>
            skill.Mutation is not null &&
            skill.Mutation.FamilyId == current.Mutation.FamilyId &&
            skill.Mutation.Tier == targetTier);
        return mutation?.Id ?? request.SkillId;
    }
}

public sealed record FusionResultPolicyRequest(
    FusionRecipeResultSnapshot Result,
    FusionParticipantSnapshot FirstParent,
    FusionParticipantSnapshot SecondParent,
    IFusionContentRepository Content,
    FusionPolicyContext Context,
    IRandomSource Random);

public sealed record FusionCombinationPolicyRequest(
    FusionParticipantSnapshot FirstParent,
    FusionParticipantSnapshot SecondParent,
    IFusionContentRepository Content,
    FusionPolicyContext Context,
    IRandomSource Random);

public sealed record FusionUnstructuredRecipePolicyRequest(
    FusionRecipeSnapshot Recipe,
    FusionParticipantSnapshot FirstParent,
    FusionParticipantSnapshot SecondParent,
    bool IsAccident,
    IFusionContentRepository Content,
    FusionPolicyContext Context);

public sealed record FusionPolicyResolution
{
    public FusionPolicyResolution(
        FusionRuntimeOperation operation,
        ContentId? resultEntityId,
        FusionParticipantSnapshot? transformedParent = null,
        FusionParticipantSnapshot? catalystParent = null,
        IEnumerable<KeyValuePair<ContentId, int>>? resultStats = null,
        IEnumerable<FusionRuntimeDiagnostic>? diagnostics = null)
    {
        Operation = operation;
        ResultEntityId = resultEntityId;
        TransformedParent = transformedParent;
        CatalystParent = catalystParent;
        ResultStats = new ReadOnlyDictionary<ContentId, int>(
            (resultStats ?? []).ToDictionary(pair => pair.Key, pair => pair.Value));
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public FusionRuntimeOperation Operation { get; }
    public ContentId? ResultEntityId { get; }
    public FusionParticipantSnapshot? TransformedParent { get; }
    public FusionParticipantSnapshot? CatalystParent { get; }
    public IReadOnlyDictionary<ContentId, int> ResultStats { get; }
    public IReadOnlyList<FusionRuntimeDiagnostic> Diagnostics { get; }
    public bool IsSuccessful => Operation != FusionRuntimeOperation.NoFusionPossible && ResultEntityId is not null;
}

public interface IFusionResultPolicy
{
    ContentId Id { get; }
    FusionPolicyResolution Resolve(FusionResultPolicyRequest request);
}

public interface IFusionCombinationPolicy
{
    ContentId Id { get; }
    FusionPolicyResolution? TryResolve(FusionCombinationPolicyRequest request);
}

public interface IFusionUnstructuredRecipePolicy
{
    FusionPolicyResolution Resolve(FusionUnstructuredRecipePolicyRequest request, IRandomSource random);
}

public sealed record FusionCatalystStatBoostRule
{
    public FusionCatalystStatBoostRule(
        ContentId catalystEntityId,
        IEnumerable<KeyValuePair<ContentId, int>> statDeltas)
    {
        ArgumentNullException.ThrowIfNull(statDeltas);
        CatalystEntityId = catalystEntityId;
        StatDeltas = new ReadOnlyDictionary<ContentId, int>(
            statDeltas.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    public ContentId CatalystEntityId { get; }
    public IReadOnlyDictionary<ContentId, int> StatDeltas { get; }
}

public sealed class CatalystStatBoostFusionPolicy : IFusionResultPolicy, IFusionCombinationPolicy
{
    private readonly IReadOnlyDictionary<ContentId, FusionCatalystStatBoostRule> _rules;
    private readonly IReadOnlyList<ContentId> _blockedTargetRaceIds;
    private readonly int? _maximumStatValue;

    public CatalystStatBoostFusionPolicy(
        ContentId id,
        IEnumerable<FusionCatalystStatBoostRule> rules,
        IEnumerable<ContentId>? blockedTargetRaceIds = null,
        int? maximumStatValue = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (maximumStatValue is < 0) throw new ArgumentOutOfRangeException(nameof(maximumStatValue));

        FusionCatalystStatBoostRule[] ruleSnapshot = rules.ToArray();
        if (ruleSnapshot.Length == 0)
        {
            throw new ArgumentException("At least one catalyst stat-boost rule is required.", nameof(rules));
        }

        Id = id;
        _rules = new ReadOnlyDictionary<ContentId, FusionCatalystStatBoostRule>(
            ruleSnapshot.ToDictionary(rule => rule.CatalystEntityId));
        _blockedTargetRaceIds = Array.AsReadOnly((blockedTargetRaceIds ?? []).Distinct().ToArray());
        _maximumStatValue = maximumStatValue;
    }

    public ContentId Id { get; }

    public FusionPolicyResolution Resolve(FusionResultPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Resolve(request.FirstParent, request.SecondParent);
    }

    public FusionPolicyResolution? TryResolve(FusionCombinationPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool firstIsCatalyst = _rules.ContainsKey(request.FirstParent.EntityId);
        bool secondIsCatalyst = _rules.ContainsKey(request.SecondParent.EntityId);
        return firstIsCatalyst || secondIsCatalyst
            ? Resolve(request.FirstParent, request.SecondParent)
            : null;
    }

    private FusionPolicyResolution Resolve(
        FusionParticipantSnapshot first,
        FusionParticipantSnapshot second)
    {
        bool firstIsCatalyst = _rules.TryGetValue(first.EntityId, out FusionCatalystStatBoostRule? firstRule);
        bool secondIsCatalyst = _rules.TryGetValue(second.EntityId, out FusionCatalystStatBoostRule? secondRule);
        if (firstIsCatalyst == secondIsCatalyst)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.CatalystPairUnsupported,
                firstIsCatalyst
                    ? "A stat-boost fusion requires one catalyst, not two."
                    : "The selected parents do not contain a registered stat-boost catalyst.");
        }

        FusionParticipantSnapshot catalyst = firstIsCatalyst ? first : second;
        FusionParticipantSnapshot target = firstIsCatalyst ? second : first;
        FusionCatalystStatBoostRule rule = firstIsCatalyst ? firstRule! : secondRule!;
        if (_blockedTargetRaceIds.Contains(target.RaceId))
        {
            return Failed(
                FusionRuntimeDiagnosticCode.TargetNotEligible,
                $"Fusion target '{target.EntityId}' is not eligible for this stat-boost policy.",
                target.EntityId);
        }

        var stats = new Dictionary<ContentId, int>(target.Stats);
        try
        {
            foreach ((ContentId statId, int delta) in rule.StatDeltas)
            {
                stats.TryGetValue(statId, out int current);
                int updated = checked(current + delta);
                stats[statId] = _maximumStatValue is int maximum
                    ? Math.Min(maximum, updated)
                    : updated;
            }
        }
        catch (OverflowException)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.InvalidPolicyResult,
                $"Stat-boost policy '{Id}' overflowed an authored stat value.");
        }

        return new FusionPolicyResolution(
            FusionRuntimeOperation.StatBoost,
            target.EntityId,
            target,
            catalyst,
            stats);
    }

    private static FusionPolicyResolution Failed(
        FusionRuntimeDiagnosticCode code,
        string message,
        ContentId? contentId = null) =>
        new(
            FusionRuntimeOperation.NoFusionPossible,
            null,
            diagnostics: [new FusionRuntimeDiagnostic(code, message, contentId)]);
}

public sealed class FusionPolicyRegistry
{
    private readonly IReadOnlyDictionary<ContentId, IFusionAccidentPolicy> _accidentPolicies;
    private readonly IReadOnlyDictionary<ContentId, IFusionMutationPolicy> _mutationPolicies;
    private readonly IReadOnlyDictionary<ContentId, IFusionResultPolicy> _resultPolicies;

    public FusionPolicyRegistry(
        IFusionInheritanceSlotPolicy inheritanceSlotPolicy,
        IFusionSacrificePolicy sacrificePolicy,
        IEnumerable<IFusionAccidentPolicy>? accidentPolicies = null,
        IEnumerable<IFusionMutationPolicy>? mutationPolicies = null,
        IEnumerable<IFusionResultPolicy>? resultPolicies = null,
        IEnumerable<IFusionCombinationPolicy>? combinationPolicies = null,
        IFusionUnstructuredRecipePolicy? unstructuredRecipePolicy = null,
        ContentId? defaultAccidentPolicyId = null,
        ContentId? defaultMutationPolicyId = null)
    {
        InheritanceSlotPolicy = inheritanceSlotPolicy ?? throw new ArgumentNullException(nameof(inheritanceSlotPolicy));
        SacrificePolicy = sacrificePolicy ?? throw new ArgumentNullException(nameof(sacrificePolicy));
        _accidentPolicies = SnapshotById(accidentPolicies, policy => policy.Id, nameof(accidentPolicies));
        _mutationPolicies = SnapshotById(mutationPolicies, policy => policy.Id, nameof(mutationPolicies));
        _resultPolicies = SnapshotById(resultPolicies, policy => policy.Id, nameof(resultPolicies));
        IFusionCombinationPolicy[] combinationSnapshot = (combinationPolicies ?? []).ToArray();
        if (combinationSnapshot.Select(policy => policy.Id).Distinct().Count() != combinationSnapshot.Length)
        {
            throw new ArgumentException(
                "Combination policy IDs must be unique.",
                nameof(combinationPolicies));
        }

        CombinationPolicies = Array.AsReadOnly(combinationSnapshot);
        UnstructuredRecipePolicy = unstructuredRecipePolicy;
        DefaultAccidentPolicyId = defaultAccidentPolicyId;
        DefaultMutationPolicyId = defaultMutationPolicyId;

        RequireRegisteredDefault(defaultAccidentPolicyId, _accidentPolicies, nameof(defaultAccidentPolicyId));
        RequireRegisteredDefault(defaultMutationPolicyId, _mutationPolicies, nameof(defaultMutationPolicyId));
    }

    public IFusionInheritanceSlotPolicy InheritanceSlotPolicy { get; }
    public IFusionSacrificePolicy SacrificePolicy { get; }
    public IReadOnlyList<IFusionCombinationPolicy> CombinationPolicies { get; }
    public IFusionUnstructuredRecipePolicy? UnstructuredRecipePolicy { get; }
    public ContentId? DefaultAccidentPolicyId { get; }
    public ContentId? DefaultMutationPolicyId { get; }

    public bool TryGetAccidentPolicy(ContentId id, out IFusionAccidentPolicy? policy) =>
        _accidentPolicies.TryGetValue(id, out policy);

    public bool TryGetMutationPolicy(ContentId id, out IFusionMutationPolicy? policy) =>
        _mutationPolicies.TryGetValue(id, out policy);

    public bool TryGetResultPolicy(ContentId id, out IFusionResultPolicy? policy) =>
        _resultPolicies.TryGetValue(id, out policy);

    private static IReadOnlyDictionary<ContentId, TPolicy> SnapshotById<TPolicy>(
        IEnumerable<TPolicy>? policies,
        Func<TPolicy, ContentId> idSelector,
        string parameterName)
    {
        TPolicy[] snapshot = (policies ?? []).ToArray();
        try
        {
            return new ReadOnlyDictionary<ContentId, TPolicy>(
                snapshot.ToDictionary(idSelector));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Policy IDs must be unique within each policy family.", parameterName, exception);
        }
    }

    private static void RequireRegisteredDefault<TPolicy>(
        ContentId? id,
        IReadOnlyDictionary<ContentId, TPolicy> policies,
        string parameterName)
    {
        if (id is ContentId policyId && !policies.ContainsKey(policyId))
        {
            throw new ArgumentException($"Default policy '{policyId}' is not registered.", parameterName);
        }
    }
}
