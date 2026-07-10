using System.Globalization;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;

namespace JRPGPrototype.Logic.Fusion;

public sealed class CatalogFusionContentRepository : IFusionContentRepository
{
    private readonly GameDataCatalog _catalog;
    private readonly IReadOnlyList<FusionRecipeSnapshot> _recipes;

    public CatalogFusionContentRepository(GameDataCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _recipes = Array.AsReadOnly(_catalog.FusionRecipes.Values
            .Select(ToRecipeSnapshot)
            .Where(recipe => recipe is not null)
            .Cast<FusionRecipeSnapshot>()
            .ToArray());
    }

    public IEnumerable<FusionRecipeSnapshot> GetRecipes() => _recipes;

    public bool TryGetEntity(ContentId entityId, out FusionEntitySnapshot? entity)
    {
        if (_catalog.TryGetEntity(entityId, out EntityDefinition? definition) && definition is not null)
        {
            entity = new FusionEntitySnapshot(definition);
            return true;
        }

        entity = null;
        return false;
    }

    public IReadOnlyList<FusionEntitySnapshot> GetEntitiesByRace(ContentId raceId) =>
        Array.AsReadOnly(_catalog.Entities.Values
            .Where(entity => entity.RaceId == raceId)
            .OrderBy(entity => entity.Rank)
            .ThenBy(entity => entity.BaseLevel)
            .ThenBy(entity => entity.Id.ToString(), StringComparer.Ordinal)
            .Select(entity => new FusionEntitySnapshot(entity))
            .ToArray());

    public bool TryGetSkill(ContentId skillId, out SkillDefinition? skill) =>
        _catalog.TryGetSkill(skillId, out skill);

    public IReadOnlyList<SkillDefinition> GetSkills() =>
        Array.AsReadOnly(_catalog.Skills.Values.ToArray());

    private static FusionRecipeSnapshot? ToRecipeSnapshot(FusionRecipeDefinition recipe)
    {
        if (recipe.Parents.Count != 2)
        {
            return null;
        }

        FusionParentSelectorDefinition first = recipe.Parents[0];
        FusionParentSelectorDefinition second = recipe.Parents[1];
        FusionRecipeResultSnapshot result = ToResultSnapshot(recipe.Result);
        return new FusionRecipeSnapshot(
            first.Id,
            second.Id,
            ToLegacyResultToken(recipe.Result),
            result,
            recipe.AccidentPolicyId,
            recipe.MutationPolicyId);
    }

    private static FusionRecipeResultSnapshot ToResultSnapshot(FusionResultDefinition result) =>
        new(
            result.Operation,
            result.ResultEntityId,
            result.ResultRaceId,
            result.RankOffset,
            result.PolicyId,
            result.Parameters);

    private static string ToLegacyResultToken(FusionResultDefinition result) =>
        result.Operation switch
        {
            FusionResultOperationKind.CreateEntity when result.ResultEntityId is ContentId entityId =>
                entityId.ToString(),
            FusionResultOperationKind.RankOffset when result.ResultRaceId is ContentId raceId =>
                raceId.ToString(),
            FusionResultOperationKind.RankOffset when result.RankOffset is int offset =>
                offset.ToString(CultureInfo.InvariantCulture),
            _ => result.Operation.ToString()
        };
}
