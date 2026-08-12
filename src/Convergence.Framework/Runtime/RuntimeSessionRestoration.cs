using Convergence.Catalog;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Fusion;
using Convergence.Internal;

namespace Convergence.Runtime;

public enum RuntimeSaveMigrationDiagnosticCode
{
    MigrationPathMissing,
    MigrationStepFailed,
    MigrationStepInvalid
}

public sealed record RuntimeSaveMigrationDiagnostic(
    RuntimeSaveMigrationDiagnosticCode Code,
    string Message,
    int SourceVersion,
    int TargetVersion);

public sealed record RuntimeSaveMigrationResult
{
    public RuntimeSaveMigrationResult(
        RuntimeSaveGameSnapshot? snapshot,
        IEnumerable<RuntimeSaveMigrationDiagnostic>? diagnostics = null)
    {
        Snapshot = snapshot;
        Diagnostics = RuntimePersistenceCollections.List(diagnostics);
        if ((snapshot is null) == (Diagnostics.Count == 0))
        {
            throw new ArgumentException(
                "A migration result must contain either a snapshot or diagnostics, but not both.",
                nameof(diagnostics));
        }
    }

    public RuntimeSaveGameSnapshot? Snapshot { get; }
    public IReadOnlyList<RuntimeSaveMigrationDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Snapshot is not null;
}

public interface IRuntimeSaveMigrationStep
{
    int SourceContractVersion { get; }
    int TargetContractVersion { get; }
    RuntimeSaveGameSnapshot Migrate(RuntimeSaveGameSnapshot snapshot, GameDataCatalog catalog);
}

public interface IRuntimeSaveMigrationService
{
    RuntimeSaveMigrationResult MigrateToCurrent(RuntimeSaveGameSnapshot snapshot, GameDataCatalog catalog);
}

public sealed class RuntimeSaveMigrationService : IRuntimeSaveMigrationService
{
    private readonly IReadOnlyDictionary<int, IRuntimeSaveMigrationStep> _steps;

    public RuntimeSaveMigrationService(IEnumerable<IRuntimeSaveMigrationStep>? steps = null)
    {
        IRuntimeSaveMigrationStep[] supplied = (steps ?? []).ToArray();
        IRuntimeSaveMigrationStep? invalid = supplied.FirstOrDefault(step =>
            step.SourceContractVersion <= 0 || step.TargetContractVersion <= step.SourceContractVersion);
        if (invalid is not null)
        {
            throw new ArgumentException(
                "Migration steps must advance from a positive source version to a higher target version.",
                nameof(steps));
        }

        IGrouping<int, IRuntimeSaveMigrationStep>? duplicate = supplied
            .GroupBy(step => step.SourceContractVersion)
            .FirstOrDefault(group => group.Skip(1).Any());
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"More than one migration step starts at contract version {duplicate.Key}.",
                nameof(steps));
        }

        _steps = supplied.ToDictionary(step => step.SourceContractVersion);
    }

    public RuntimeSaveMigrationResult MigrateToCurrent(
        RuntimeSaveGameSnapshot snapshot,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        if (snapshot.ContractVersion == RuntimeSaveGameSnapshot.CurrentContractVersion)
        {
            return new RuntimeSaveMigrationResult(snapshot);
        }

        RuntimeSaveGameSnapshot current = snapshot;
        var visited = new HashSet<int>();
        while (current.ContractVersion != RuntimeSaveGameSnapshot.CurrentContractVersion)
        {
            int sourceVersion = current.ContractVersion;
            if (sourceVersion > RuntimeSaveGameSnapshot.CurrentContractVersion ||
                !visited.Add(sourceVersion) ||
                !_steps.TryGetValue(sourceVersion, out IRuntimeSaveMigrationStep? step))
            {
                return Failure(
                    RuntimeSaveMigrationDiagnosticCode.MigrationPathMissing,
                    $"No migration path exists from save contract version {sourceVersion} to " +
                    $"{RuntimeSaveGameSnapshot.CurrentContractVersion}.",
                    sourceVersion,
                    RuntimeSaveGameSnapshot.CurrentContractVersion);
            }

            try
            {
                RuntimeSaveGameSnapshot migrated = step.Migrate(current, catalog) ??
                    throw new InvalidOperationException("The migration step returned no snapshot.");
                if (migrated.ContractVersion != step.TargetContractVersion ||
                    migrated.ContractVersion <= sourceVersion ||
                    migrated.ContractVersion > RuntimeSaveGameSnapshot.CurrentContractVersion)
                {
                    return Failure(
                        RuntimeSaveMigrationDiagnosticCode.MigrationStepInvalid,
                        $"Migration step {sourceVersion}->{step.TargetContractVersion} produced contract " +
                        $"version {migrated.ContractVersion}.",
                        sourceVersion,
                        step.TargetContractVersion);
                }

                current = migrated;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Failure(
                    RuntimeSaveMigrationDiagnosticCode.MigrationStepFailed,
                    $"Migration step {sourceVersion}->{step.TargetContractVersion} failed: {exception.Message}",
                    sourceVersion,
                    step.TargetContractVersion);
            }
        }

        return new RuntimeSaveMigrationResult(current);
    }

    private static RuntimeSaveMigrationResult Failure(
        RuntimeSaveMigrationDiagnosticCode code,
        string message,
        int sourceVersion,
        int targetVersion) =>
        new(
            null,
            [new RuntimeSaveMigrationDiagnostic(code, message, sourceVersion, targetVersion)]);
}

public sealed record RuntimeActorRestoreProfile
{
    public RuntimeActorRestoreProfile(
        RuntimeStatSourceKind statSourceKind,
        MissingHostedEntityBehavior missingHostedEntityBehavior,
        IEnumerable<KeyValuePair<ContentId, decimal>>? equipmentStatModifiers = null,
        IEnumerable<ContentId>? equipmentGrantedSkillIds = null)
    {
        if (!Enum.IsDefined(statSourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(statSourceKind));
        }
        if (!Enum.IsDefined(missingHostedEntityBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(missingHostedEntityBehavior));
        }
        StatSourceKind = statSourceKind;
        MissingHostedEntityBehavior = missingHostedEntityBehavior;
        EquipmentStatModifiers = RuntimePersistenceCollections.Dictionary(equipmentStatModifiers);
        EquipmentGrantedSkillIds = RuntimePersistenceCollections.List(equipmentGrantedSkillIds);
    }

    public RuntimeStatSourceKind StatSourceKind { get; }
    public MissingHostedEntityBehavior MissingHostedEntityBehavior { get; }
    public IReadOnlyDictionary<ContentId, decimal> EquipmentStatModifiers { get; }
    public IReadOnlyList<ContentId> EquipmentGrantedSkillIds { get; }
}

public sealed record RuntimeActorRestoreProfileRequest(
    RuntimeActorSnapshot Actor,
    RuntimeSaveGameSnapshot Session,
    GameDataCatalog Catalog);

public interface IRuntimeActorRestoreProfileResolver
{
    RuntimeActorRestoreProfile Resolve(RuntimeActorRestoreProfileRequest request);
}

public enum RuntimeSessionRestoreDiagnosticCode
{
    MigrationRejected,
    SaveValidationRejected,
    ActorProfileResolutionFailed,
    HostedEntityDependencyMissing,
    HostedEntityDependencyCycle,
    ActorRestoreFailed,
    StatModifierPolicyResolutionFailed,
    ChargePolicyResolutionFailed
}

public sealed record RuntimeSessionRestoreDiagnostic(
    RuntimeSessionRestoreDiagnosticCode Code,
    string Message,
    RuntimeInstanceId? ActorId = null,
    string? Path = null,
    RuntimeSaveValidationCode? SaveValidationCode = null,
    CatalogBattleActorDiagnosticCode? ActorDiagnosticCode = null);

public sealed record RuntimeRestoredSession
{
    public RuntimeRestoredSession(
        RuntimeSaveGameSnapshot snapshot,
        IEnumerable<CatalogBattleActor> actors)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        CatalogBattleActor[] actorArray =
            (actors ?? throw new ArgumentNullException(nameof(actors))).ToArray();
        Actors = Array.AsReadOnly(actorArray);
        ActorsByInstanceId = RuntimePersistenceCollections.Dictionary(actorArray.Select(actor =>
            new KeyValuePair<RuntimeInstanceId, CatalogBattleActor>(actor.State.InstanceId, actor)));
    }

    public RuntimeSaveGameSnapshot Snapshot { get; }
    public IReadOnlyList<CatalogBattleActor> Actors { get; }
    public IReadOnlyDictionary<RuntimeInstanceId, CatalogBattleActor> ActorsByInstanceId { get; }
    public RuntimePartyRosterSnapshot PartyRoster => Snapshot.PartyRoster;
    public RuntimeInventorySnapshot Inventory => Snapshot.Inventory;
    public RuntimeCurrencyLedgerSnapshot CurrencyLedger => Snapshot.CurrencyLedger;
    public RuntimeShopStockSnapshot ShopStock => Snapshot.ShopStock;
    public RuntimeFieldSnapshot? Field => Snapshot.Field;
    public CompendiumStateSnapshot Compendium => Snapshot.Compendium;
    public RuntimeKnowledgeSnapshot Knowledge => Snapshot.Knowledge;
    public RuntimeSessionProgressSnapshot Progress => Snapshot.Session;
    public RuntimeCheckpointLogSnapshot Checkpoints => Snapshot.Checkpoints;
}

public sealed record RuntimeSessionRestoreResult
{
    public RuntimeSessionRestoreResult(
        RuntimeRestoredSession? session,
        IEnumerable<RuntimeSessionRestoreDiagnostic>? diagnostics = null)
    {
        Session = session;
        Diagnostics = RuntimePersistenceCollections.List(diagnostics);
        if ((session is null) == (Diagnostics.Count == 0))
        {
            throw new ArgumentException(
                "A session restore result must contain either a restored session or diagnostics, but not both.",
                nameof(diagnostics));
        }
    }

    public RuntimeRestoredSession? Session { get; }
    public IReadOnlyList<RuntimeSessionRestoreDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Session is not null;

    public RuntimeRestoredSession RequireSession() => Session ??
        throw new InvalidOperationException("Runtime session restoration was rejected.");
}

public interface IRuntimeSessionRestoreService
{
    RuntimeSessionRestoreResult Restore(RuntimeSaveGameSnapshot snapshot, GameDataCatalog catalog);
}

/// <summary>Validates and restores an aggregate runtime session without exposing partial live state.</summary>
public sealed class RuntimeSessionRestoreService : IRuntimeSessionRestoreService
{
    private readonly IRuntimeSaveValidator _validator;
    private readonly ICatalogBattleActorFactory _actorFactory;
    private readonly IRuntimeActorRestoreProfileResolver _profileResolver;
    private readonly IRuntimeSaveMigrationService _migration;
    private readonly IRuntimeRulesetBindingResolver? _rulesetBindings;
    private readonly IChargePolicyResolver? _chargePolicies;

    public RuntimeSessionRestoreService(
        IRuntimeSaveValidator validator,
        ICatalogBattleActorFactory actorFactory,
        IRuntimeActorRestoreProfileResolver profileResolver,
        IRuntimeSaveMigrationService? migration = null,
        IRuntimeRulesetBindingResolver? rulesetBindings = null,
        IChargePolicyResolver? chargePolicies = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _actorFactory = actorFactory ?? throw new ArgumentNullException(nameof(actorFactory));
        _profileResolver = profileResolver ?? throw new ArgumentNullException(nameof(profileResolver));
        _migration = migration ?? new RuntimeSaveMigrationService();
        _rulesetBindings = rulesetBindings;
        _chargePolicies = chargePolicies;
    }

    public RuntimeSessionRestoreResult Restore(RuntimeSaveGameSnapshot snapshot, GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);

        RuntimeSaveMigrationResult migration = _migration.MigrateToCurrent(snapshot, catalog);
        if (!migration.IsSuccess)
        {
            return Rejected(migration.Diagnostics.Select(diagnostic => new RuntimeSessionRestoreDiagnostic(
                RuntimeSessionRestoreDiagnosticCode.MigrationRejected,
                diagnostic.Message,
                Path: "$.contractVersion")));
        }

        RuntimeSaveGameSnapshot current = migration.Snapshot!;
        RuntimeSaveValidationResult validation = _validator.Validate(current, catalog);
        if (!validation.IsValid)
        {
            return Rejected(validation.Diagnostics.Select(diagnostic => new RuntimeSessionRestoreDiagnostic(
                RuntimeSessionRestoreDiagnosticCode.SaveValidationRejected,
                diagnostic.Message,
                diagnostic.InstanceId,
                diagnostic.Path,
                diagnostic.Code)));
        }

        var profiles = new Dictionary<RuntimeInstanceId, RuntimeActorRestoreProfile>();
        var diagnostics = new List<RuntimeSessionRestoreDiagnostic>();
        foreach (RuntimeActorSnapshot actor in current.Actors)
        {
            try
            {
                RuntimeActorRestoreProfile profile = _profileResolver.Resolve(
                    new RuntimeActorRestoreProfileRequest(actor, current, catalog)) ??
                    throw new InvalidOperationException("The actor restore-profile resolver returned no profile.");
                profiles.Add(actor.Identity.InstanceId, profile);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                    RuntimeSessionRestoreDiagnosticCode.ActorProfileResolutionFailed,
                    $"Restore profile for actor '{actor.Identity.InstanceId}' failed: {exception.Message}",
                    actor.Identity.InstanceId));
            }
        }

        if (diagnostics.Count > 0)
        {
            return Rejected(diagnostics);
        }

        var chargePolicies = new Dictionary<RuntimeInstanceId, IChargePolicyService>();
        foreach (RuntimeActorSnapshot actor in current.Actors)
        {
            RuntimeChargeStateSnapshot? state = actor.BattleStatus.ChargeState;
            if (state is null)
            {
                continue;
            }

            if (_chargePolicies is null ||
                !_chargePolicies.TryResolve(state.PolicyId, out IChargePolicyService? policy) ||
                policy is null)
            {
                diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                    RuntimeSessionRestoreDiagnosticCode.ChargePolicyResolutionFailed,
                    $"Actor '{actor.Identity.InstanceId}' retains charge state for policy " +
                    $"'{state.PolicyId}', but no matching charge policy was supplied.",
                    actor.Identity.InstanceId,
                    "$.actors.battleStatus.chargeState.policyId"));
                continue;
            }

            ChargePolicyValidationResult retainedState = policy.ValidateState(state);
            if (!retainedState.IsValid)
            {
                diagnostics.AddRange(retainedState.Diagnostics.Select(issue =>
                    new RuntimeSessionRestoreDiagnostic(
                        RuntimeSessionRestoreDiagnosticCode.ChargePolicyResolutionFailed,
                        issue.Message,
                        actor.Identity.InstanceId,
                        "$.actors.battleStatus.chargeState")));
                continue;
            }

            chargePolicies.Add(actor.Identity.InstanceId, policy);
        }

        if (diagnostics.Count > 0)
        {
            return Rejected(diagnostics);
        }

        var statModifierPolicies = new Dictionary<RuntimeInstanceId, IStatModifierPolicyService>();
        foreach (RuntimeActorSnapshot actor in current.Actors)
        {
            RuntimeStatModifierStateSnapshot? state = actor.BattleStatus.StatModifiers;
            if (state is null)
            {
                continue;
            }

            if (_rulesetBindings is null)
            {
                diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                    RuntimeSessionRestoreDiagnosticCode.StatModifierPolicyResolutionFailed,
                    $"Actor '{actor.Identity.InstanceId}' retains stat modifiers, but no ruleset " +
                    "binding resolver was supplied for restoration.",
                    actor.Identity.InstanceId,
                    "$.actors.battleStatus.statModifiers.policyId"));
                continue;
            }

            RulesetBindingResult<IStatModifierPolicyService> binding =
                _rulesetBindings.BindStatModifierPolicy(catalog, state.PolicyId);
            if (!binding.IsSuccess || binding.Service is null)
            {
                diagnostics.AddRange(binding.Diagnostics.Select(issue =>
                    new RuntimeSessionRestoreDiagnostic(
                        RuntimeSessionRestoreDiagnosticCode.StatModifierPolicyResolutionFailed,
                        issue.Message,
                        actor.Identity.InstanceId,
                        "$.actors.battleStatus.statModifiers.policyId")));
                continue;
            }

            StatModifierValidationResult retainedState = binding.Service.ValidateState(state);
            if (!retainedState.IsValid)
            {
                diagnostics.AddRange(retainedState.Diagnostics.Select(issue =>
                    new RuntimeSessionRestoreDiagnostic(
                        RuntimeSessionRestoreDiagnosticCode.StatModifierPolicyResolutionFailed,
                        issue.Message,
                        actor.Identity.InstanceId,
                        "$.actors.battleStatus.statModifiers")));
                continue;
            }

            statModifierPolicies.Add(actor.Identity.InstanceId, binding.Service);
        }

        if (diagnostics.Count > 0)
        {
            return Rejected(diagnostics);
        }

        Dictionary<RuntimeInstanceId, RuntimeActorSnapshot> snapshots = current.Actors
            .ToDictionary(actor => actor.Identity.InstanceId);
        var restored = new Dictionary<RuntimeInstanceId, CatalogBattleActor>();
        var visiting = new HashSet<RuntimeInstanceId>();

        foreach (RuntimeActorSnapshot actor in current.Actors)
        {
            RestoreActor(actor.Identity.InstanceId);
        }

        if (diagnostics.Count > 0)
        {
            return Rejected(diagnostics);
        }

        CatalogBattleActor[] ordered = current.Actors
            .Select(actor => restored[actor.Identity.InstanceId])
            .ToArray();
        RuntimeSaveGameSnapshot normalized = NormalizeSnapshot(current, ordered);
        return new RuntimeSessionRestoreResult(new RuntimeRestoredSession(normalized, ordered));

        CatalogBattleActor? RestoreActor(RuntimeInstanceId actorId)
        {
            if (restored.TryGetValue(actorId, out CatalogBattleActor? existing))
            {
                return existing;
            }
            if (!visiting.Add(actorId))
            {
                diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                    RuntimeSessionRestoreDiagnosticCode.HostedEntityDependencyCycle,
                    $"Actor restore dependency cycle includes '{actorId}'.",
                    actorId));
                return null;
            }

            RuntimeActorSnapshot actor = snapshots[actorId];
            RuntimeActorRestoreProfile profile = profiles[actorId];
            RuntimeActorState? activeHostedEntity = null;
            if (profile.StatSourceKind == RuntimeStatSourceKind.ActiveHostedEntity)
            {
                RuntimeActorReferenceSnapshot? activeReference =
                    current.PartyRoster.ActiveHostedEntity;
                if (activeReference is null)
                {
                    if (profile.MissingHostedEntityBehavior ==
                        MissingHostedEntityBehavior.RejectStatResolution)
                    {
                        diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                            RuntimeSessionRestoreDiagnosticCode.HostedEntityDependencyMissing,
                            $"Actor '{actorId}' requires the canonical party roster to select an " +
                            "active Hosted Entity.",
                            actorId,
                            "$.partyRoster.activeHostedEntity"));
                        visiting.Remove(actorId);
                        return null;
                    }
                }
                else
                {
                    if (current.PartyRoster.Owner.InstanceId != actorId)
                    {
                        diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                            RuntimeSessionRestoreDiagnosticCode.ActorProfileResolutionFailed,
                            $"Actor '{actorId}' cannot use the canonical active Hosted Entity " +
                            $"because party owner is '{current.PartyRoster.Owner.InstanceId}'.",
                            actorId,
                            "$.partyRoster.owner"));
                        visiting.Remove(actorId);
                        return null;
                    }

                    RuntimeInstanceId dependencyId = activeReference.InstanceId;
                    if (!snapshots.ContainsKey(dependencyId))
                    {
                        diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                            RuntimeSessionRestoreDiagnosticCode.HostedEntityDependencyMissing,
                            $"Actor '{actorId}' depends on missing Hosted Entity " +
                            $"'{dependencyId}'.",
                            actorId,
                            "$.partyRoster.activeHostedEntity"));
                        visiting.Remove(actorId);
                        return null;
                    }

                    CatalogBattleActor? dependency = RestoreActor(dependencyId);
                    if (dependency is null)
                    {
                        visiting.Remove(actorId);
                        return null;
                    }
                    activeHostedEntity = dependency.State;
                }
            }

            CatalogBattleActorCreationResult result;
            try
            {
                result = _actorFactory.Restore(new CatalogBattleActorRestoreRequest(
                    actor,
                    profile.StatSourceKind,
                    profile.MissingHostedEntityBehavior,
                    current.PartyRoster,
                    activeHostedEntity is null ? [] : [activeHostedEntity],
                    profile.EquipmentStatModifiers,
                    statModifierPolicies.GetValueOrDefault(actorId),
                    chargePolicies.GetValueOrDefault(actorId),
                    profile.EquipmentGrantedSkillIds));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(new RuntimeSessionRestoreDiagnostic(
                    RuntimeSessionRestoreDiagnosticCode.ActorRestoreFailed,
                    $"Actor '{actorId}' restore port failed: {exception.Message}",
                    actorId));
                visiting.Remove(actorId);
                return null;
            }

            if (!result.IsSuccess)
            {
                diagnostics.AddRange(result.Diagnostics.Select(diagnostic =>
                    new RuntimeSessionRestoreDiagnostic(
                        RuntimeSessionRestoreDiagnosticCode.ActorRestoreFailed,
                        diagnostic.Message,
                        actorId,
                        ActorDiagnosticCode: diagnostic.Code)));
                visiting.Remove(actorId);
                return null;
            }

            CatalogBattleActor restoredActor = result.RequireActor();
            restored.Add(actorId, restoredActor);
            visiting.Remove(actorId);
            return restoredActor;
        }
    }

    private static RuntimeSessionRestoreResult Rejected(
        IEnumerable<RuntimeSessionRestoreDiagnostic> diagnostics) =>
        new(null, diagnostics);

    private static RuntimeSaveGameSnapshot NormalizeSnapshot(
        RuntimeSaveGameSnapshot source,
        IEnumerable<CatalogBattleActor> actors) =>
        new(
            source.FrameworkVersion,
            source.ContentPacks,
            actors.Select(actor => actor.State.ToSnapshot()),
            source.PartyRoster,
            source.Inventory,
            source.CurrencyLedger,
            source.ShopStock,
            source.Field,
            source.Compendium,
            source.Knowledge,
            source.Session,
            source.Checkpoints,
            source.HostContext,
            source.ContractVersion);
}
