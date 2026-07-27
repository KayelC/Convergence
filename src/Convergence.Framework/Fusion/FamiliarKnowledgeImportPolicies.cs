using Convergence.Content;
using Convergence.Internal;
using Convergence.Knowledge;

namespace Convergence.Fusion;

/// <summary>Describes why a host requested familiar-entity knowledge.</summary>
public enum FamiliarKnowledgeImportSource
{
    ExplicitRequest,
    Acquisition,
    CompendiumRegistration,
    RegisteredCompendiumSync
}

/// <summary>Supplies typed context to a game's familiar-knowledge policy.</summary>
public sealed class FamiliarKnowledgeImportPolicyRequest
{
    public FamiliarKnowledgeImportPolicyRequest(
        EntityDefinition entity,
        FamiliarKnowledgeImportSource source)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        Source = EnumDomain.RequireDefined(source, nameof(source));
    }

    public EntityDefinition Entity { get; }
    public FamiliarKnowledgeImportSource Source { get; }
}

/// <summary>
/// Selects which authored defense domains become persistent player knowledge
/// when a host explicitly reports familiarity with an entity.
/// </summary>
public interface IFamiliarKnowledgeImportPolicy
{
    IReadOnlyList<BattleAnalysisField> SelectDefenseFields(
        FamiliarKnowledgeImportPolicyRequest request);
}

/// <summary>Imports every authored defense domain for an explicitly familiar entity.</summary>
public sealed class StandardFamiliarKnowledgeImportPolicy : IFamiliarKnowledgeImportPolicy
{
    private static readonly IReadOnlyList<BattleAnalysisField> Fields = Array.AsReadOnly(
    [
        BattleAnalysisField.ElementalAffinities,
        BattleAnalysisField.AilmentResistances,
        BattleAnalysisField.InstantDeathResistances
    ]);

    public IReadOnlyList<BattleAnalysisField> SelectDefenseFields(
        FamiliarKnowledgeImportPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Fields;
    }
}

/// <summary>Disables familiarity-based knowledge without changing acquisition rules.</summary>
public sealed class DisabledFamiliarKnowledgeImportPolicy : IFamiliarKnowledgeImportPolicy
{
    public IReadOnlyList<BattleAnalysisField> SelectDefenseFields(
        FamiliarKnowledgeImportPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return [];
    }
}
