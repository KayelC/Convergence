using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;

namespace JRPGPrototype.Logic.Fusion;

internal static class LegacyFusionStrategyPolicies
{
    internal static readonly ContentId AccidentPolicyId = ContentId.Parse("legacy_fusion_accident");
    internal static readonly ContentId MutationPolicyId = ContentId.Parse("legacy_fusion_mutation");
    internal static readonly ContentId CatalystPolicyId = ContentId.Parse("legacy_catalyst_stat_boost");
    internal static readonly ContentId MoonPhaseValueId = ContentId.Parse("legacy_moon_phase");

    internal static FusionPolicyRegistry CreateRegistry()
    {
        var catalystPolicy = new CatalystStatBoostFusionPolicy(
            CatalystPolicyId,
            [
                Catalyst("ara_mitama", ("strength", 2), ("agility", 1)),
                Catalyst("nigi_mitama", ("magic", 2), ("luck", 1)),
                Catalyst("kusi_mitama", ("vitality", 2), ("agility", 1)),
                Catalyst("saki_mitama", ("vitality", 2), ("luck", 1))
            ],
            blockedTargetRaceIds: [ContentId.Parse("element")],
            maximumStatValue: 40);

        return new FusionPolicyRegistry(
            new TieredFusionInheritanceSlotPolicy(
                [
                    new FusionInheritanceSlotTier(0, 1),
                    new FusionInheritanceSlotTier(7, 2),
                    new FusionInheritanceSlotTier(10, 3),
                    new FusionInheritanceSlotTier(14, 4),
                    new FusionInheritanceSlotTier(19, 5),
                    new FusionInheritanceSlotTier(24, 6)
                ],
                maximumSlots: 8),
            new FixedFusionSacrificePolicy(isAllowed: true, additionalInheritanceSlots: 2),
            accidentPolicies:
            [
                new ContextualPercentageFusionAccidentPolicy(
                    AccidentPolicyId,
                    defaultChancePercent: 1,
                    MoonPhaseValueId,
                    matchingValue: 8,
                    matchingChancePercent: 12)
            ],
            mutationPolicies: [new AdjacentTierFusionMutationPolicy(MutationPolicyId, chancePercent: 100)],
            resultPolicies: [catalystPolicy],
            combinationPolicies: [catalystPolicy],
            unstructuredRecipePolicy: new LegacyUnstructuredFusionRecipePolicy(),
            defaultAccidentPolicyId: AccidentPolicyId,
            defaultMutationPolicyId: MutationPolicyId);
    }

    internal static FusionPolicyContext CreateContext(int moonPhase) =>
        new(numericValues: [new KeyValuePair<ContentId, decimal>(MoonPhaseValueId, moonPhase)]);

    private static FusionCatalystStatBoostRule Catalyst(
        string catalystId,
        params (string StatId, int Delta)[] deltas) =>
        new(
            ContentId.Parse(catalystId),
            deltas.Select(delta =>
                new KeyValuePair<ContentId, int>(ContentId.Parse(delta.StatId), delta.Delta)));
}

internal sealed class LegacyUnstructuredFusionRecipePolicy : IFusionUnstructuredRecipePolicy
{
    private static readonly ContentId ElementRaceId = ContentId.Parse("element");

    public FusionPolicyResolution Resolve(
        FusionUnstructuredRecipePolicyRequest request,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);

        string token = request.Recipe.ResultToken;
        if (token is "1" or "-1")
        {
            return ResolveRank(request, token == "1" ? 1 : -1);
        }

        if (!ContentId.TryParse(token, out ContentId raceId))
        {
            return Failed(
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                $"Legacy fusion result token '{token}' is not a valid entity or race ID.");
        }

        if (!request.Content.TryGetEntity(request.FirstParent.EntityId, out FusionEntitySnapshot? templateA) ||
            templateA is null ||
            !request.Content.TryGetEntity(request.SecondParent.EntityId, out FusionEntitySnapshot? templateB) ||
            templateB is null)
        {
            return Failed(FusionRuntimeDiagnosticCode.MissingEntity, "A selected parent has no legacy entity template.");
        }

        FusionEntitySnapshot[] racePool = request.Content.GetEntitiesByRace(raceId)
            .OrderBy(entity => entity.BaseLevel)
            .ThenBy(entity => entity.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (racePool.Length == 0)
        {
            return Failed(FusionRuntimeDiagnosticCode.MissingRaceMembers, $"Race '{raceId}' has no fusion results.");
        }

        FusionEntitySnapshot result;
        if (request.IsAccident)
        {
            result = racePool[0];
        }
        else
        {
            int averageBaseLevel = (templateA.BaseLevel + templateB.BaseLevel) / 2;
            int targetLevel = averageBaseLevel + random.NextInt32(1, 6);
            result = racePool.FirstOrDefault(entity => entity.BaseLevel >= targetLevel) ?? racePool[^1];
            if (result.Id == templateA.Id || result.Id == templateB.Id)
            {
                int index = Array.IndexOf(racePool, result);
                if (index + 1 < racePool.Length)
                {
                    result = racePool[index + 1];
                }
            }
        }

        return new FusionPolicyResolution(FusionRuntimeOperation.CreateNewEntity, result.Id);
    }

    private static FusionPolicyResolution ResolveRank(
        FusionUnstructuredRecipePolicyRequest request,
        int rankDirection)
    {
        FusionParticipantSnapshot? parentToRank = request.FirstParent.RaceId != ElementRaceId
            ? request.FirstParent
            : request.SecondParent.RaceId != ElementRaceId
                ? request.SecondParent
                : null;
        if (parentToRank is null)
        {
            return Failed(
                FusionRuntimeDiagnosticCode.NoFusionPossible,
                "No non-Element parent can receive the legacy rank operation.");
        }

        int targetRank = parentToRank.Rank + rankDirection;
        FusionEntitySnapshot? ranked = request.Content.GetEntitiesByRace(parentToRank.RaceId)
            .FirstOrDefault(entity => entity.Rank == targetRank);
        return ranked is null
            ? Failed(FusionRuntimeDiagnosticCode.NoFusionPossible, "No entity exists at the target rank.")
            : new FusionPolicyResolution(
                rankDirection > 0 ? FusionRuntimeOperation.RankUpParent : FusionRuntimeOperation.RankDownParent,
                ranked.Id,
                transformedParent: parentToRank);
    }

    private static FusionPolicyResolution Failed(FusionRuntimeDiagnosticCode code, string message) =>
        new(
            FusionRuntimeOperation.NoFusionPossible,
            null,
            diagnostics: [new FusionRuntimeDiagnostic(code, message)]);
}
