using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Logic.Battle.Runtime;

namespace JRPGPrototype.Logic.Runtime;

public sealed record EncounterStartRequest(
    ContentId EncounterId,
    ContentId OpponentTeamId,
    RuntimeInstanceId InstanceIdPrefix,
    int FormationIndex = 0);

public enum EncounterStartDiagnosticCode
{
    EncounterIdNotQualified,
    EncounterMissing,
    InvalidFormationIndex,
    FormationHasNoMembers,
    InvalidMemberLevel,
    InvalidMemberCount,
    IdentifierInvalid
}

public sealed record EncounterStartDiagnostic(
    EncounterStartDiagnosticCode Code,
    string Message,
    ContentId? ContentId = null);

public sealed record EncounterStartPlan
{
    public EncounterStartPlan(
        EncounterDefinition encounter,
        EncounterFormationDefinition formation,
        IEnumerable<CatalogBattleActorCreationRequest> actorRequests)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        Formation = formation ?? throw new ArgumentNullException(nameof(formation));
        ActorRequests = Array.AsReadOnly(
            actorRequests?.ToArray() ?? throw new ArgumentNullException(nameof(actorRequests)));
    }

    public EncounterDefinition Encounter { get; }
    public EncounterFormationDefinition Formation { get; }
    public IReadOnlyList<CatalogBattleActorCreationRequest> ActorRequests { get; }
}

public sealed record EncounterStartPlanResult
{
    internal EncounterStartPlanResult(
        EncounterStartPlan? plan,
        IEnumerable<EncounterStartDiagnostic> diagnostics)
    {
        Plan = plan;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public EncounterStartPlan? Plan { get; }
    public IReadOnlyList<EncounterStartDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Plan is not null && Diagnostics.Count == 0;

    public EncounterStartPlan RequirePlan() =>
        Plan ?? throw new EncounterStartPlanningException(Diagnostics);
}

public sealed class EncounterStartPlanningException : Exception
{
    public EncounterStartPlanningException(IEnumerable<EncounterStartDiagnostic> diagnostics)
        : this(Array.AsReadOnly(diagnostics.ToArray()))
    {
    }

    private EncounterStartPlanningException(IReadOnlyList<EncounterStartDiagnostic> diagnostics)
        : base($"Encounter start planning failed with {diagnostics.Count} diagnostic(s).")
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<EncounterStartDiagnostic> Diagnostics { get; }
}

public interface IEncounterStartPlanner
{
    EncounterStartPlanResult Plan(EncounterStartRequest request);
}

public sealed class CatalogEncounterStartPlanner : IEncounterStartPlanner
{
    private readonly IEncounterDefinitionRepository _encounters;

    public CatalogEncounterStartPlanner(IEncounterDefinitionRepository encounters)
    {
        _encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
    }

    public EncounterStartPlanResult Plan(EncounterStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<EncounterStartDiagnostic>();

        if (!request.EncounterId.IsValid)
        {
            diagnostics.Add(new EncounterStartDiagnostic(
                EncounterStartDiagnosticCode.IdentifierInvalid,
                "Encounter ID cannot be empty.",
                request.EncounterId));
        }
        if (!request.OpponentTeamId.IsValid)
        {
            diagnostics.Add(new EncounterStartDiagnostic(
                EncounterStartDiagnosticCode.IdentifierInvalid,
                "Opponent team ID cannot be empty.",
                request.OpponentTeamId));
        }
        if (!request.InstanceIdPrefix.IsValid)
        {
            diagnostics.Add(new EncounterStartDiagnostic(
                EncounterStartDiagnosticCode.IdentifierInvalid,
                "Runtime instance ID prefix cannot be empty."));
        }
        if (diagnostics.Count > 0)
        {
            return new EncounterStartPlanResult(null, diagnostics);
        }

        if (!request.EncounterId.IsQualified)
        {
            diagnostics.Add(new EncounterStartDiagnostic(
                EncounterStartDiagnosticCode.EncounterIdNotQualified,
                "Encounter start requests require a pack-qualified encounter ID.",
                request.EncounterId));
            return new EncounterStartPlanResult(null, diagnostics);
        }

        if (!_encounters.TryGetEncounter(request.EncounterId, out EncounterDefinition? encounter) || encounter is null)
        {
            diagnostics.Add(new EncounterStartDiagnostic(
                EncounterStartDiagnosticCode.EncounterMissing,
                $"No encounter definition exists for '{request.EncounterId}'.",
                request.EncounterId));
            return new EncounterStartPlanResult(null, diagnostics);
        }

        if (request.FormationIndex < 0 || request.FormationIndex >= encounter.Formations.Count)
        {
            diagnostics.Add(new EncounterStartDiagnostic(
                EncounterStartDiagnosticCode.InvalidFormationIndex,
                $"Encounter '{encounter.Id}' has no formation at index {request.FormationIndex}.",
                encounter.Id));
            return new EncounterStartPlanResult(null, diagnostics);
        }

        EncounterFormationDefinition formation = encounter.Formations[request.FormationIndex];
        if (formation.Members.Count == 0)
        {
            diagnostics.Add(new EncounterStartDiagnostic(
                EncounterStartDiagnosticCode.FormationHasNoMembers,
                $"Encounter '{encounter.Id}' formation {request.FormationIndex} has no members.",
                encounter.Id));
        }

        var actorRequests = new List<CatalogBattleActorCreationRequest>();
        int actorIndex = 1;
        foreach (EncounterMemberDefinition member in formation.Members)
        {
            if (member.Level <= 0)
            {
                diagnostics.Add(new EncounterStartDiagnostic(
                    EncounterStartDiagnosticCode.InvalidMemberLevel,
                    $"Encounter '{encounter.Id}' contains member '{member.EntityId}' with invalid level {member.Level}.",
                    member.EntityId));
            }

            if (member.Count <= 0)
            {
                diagnostics.Add(new EncounterStartDiagnostic(
                    EncounterStartDiagnosticCode.InvalidMemberCount,
                    $"Encounter '{encounter.Id}' contains member '{member.EntityId}' with invalid count {member.Count}.",
                    member.EntityId));
                continue;
            }

            for (int copy = 0; copy < member.Count; copy++)
            {
                actorRequests.Add(new CatalogBattleActorCreationRequest(
                    member.EntityId,
                    CreateInstanceId(request.InstanceIdPrefix, member.EntityId, actorIndex++),
                    request.OpponentTeamId,
                    member.Level));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new EncounterStartPlanResult(null, diagnostics);
        }

        return new EncounterStartPlanResult(
            new EncounterStartPlan(encounter, formation, actorRequests),
            []);
    }

    private static RuntimeInstanceId CreateInstanceId(RuntimeInstanceId prefix, ContentId entityId, int index) =>
        RuntimeInstanceId.Parse($"{prefix}_{LocalId(entityId)}_{index}");

    private static string LocalId(ContentId id)
    {
        string value = id.ToString();
        int separator = value.LastIndexOf(':');
        return separator >= 0 ? value[(separator + 1)..] : value;
    }
}
