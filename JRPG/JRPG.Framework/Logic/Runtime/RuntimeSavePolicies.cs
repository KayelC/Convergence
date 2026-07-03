using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Runtime;

public enum RuntimeSaveKind
{
    Manual,
    Suspend
}

public enum RuntimeSuspendLoadBehavior
{
    KeepRecord,
    ConsumeAfterSuccessfulRestore
}

public enum RuntimeSavePolicyDiagnosticCode
{
    MissingSaveRecord,
    SaveKindMismatch,
    ContextNotAllowed,
    PendingHostAction
}

public sealed record RuntimeSaveContextSnapshot
{
    public RuntimeSaveContextSnapshot(ContentId contextId, bool hasPendingHostAction = false)
    {
        ContextId = contextId;
        HasPendingHostAction = hasPendingHostAction;
    }

    public ContentId ContextId { get; }
    public bool HasPendingHostAction { get; }
}

public sealed record RuntimeSavePolicyOptions
{
    public RuntimeSavePolicyOptions(
        IEnumerable<ContentId>? manualAllowedContextIds = null,
        IEnumerable<ContentId>? suspendAllowedContextIds = null,
        RuntimeSuspendLoadBehavior suspendLoadBehavior = RuntimeSuspendLoadBehavior.ConsumeAfterSuccessfulRestore)
    {
        ManualAllowedContextIds = RuntimeSnapshotCollections.List(
            (manualAllowedContextIds ?? []).Distinct());
        SuspendAllowedContextIds = RuntimeSnapshotCollections.List(
            (suspendAllowedContextIds ?? []).Distinct());
        SuspendLoadBehavior = suspendLoadBehavior;
    }

    public IReadOnlyList<ContentId> ManualAllowedContextIds { get; }
    public IReadOnlyList<ContentId> SuspendAllowedContextIds { get; }
    public RuntimeSuspendLoadBehavior SuspendLoadBehavior { get; }

    internal IReadOnlyList<ContentId> AllowedContexts(RuntimeSaveKind kind) =>
        kind == RuntimeSaveKind.Manual ? ManualAllowedContextIds : SuspendAllowedContextIds;
}

public sealed record RuntimeSavePolicyDiagnostic(
    RuntimeSavePolicyDiagnosticCode Code,
    string Message,
    RuntimeSaveKind? Kind = null,
    ContentId? ContextId = null);

public sealed record RuntimeSavePolicyAssessment
{
    public RuntimeSavePolicyAssessment(
        RuntimeSaveKind kind,
        RuntimeSaveContextSnapshot context,
        IEnumerable<RuntimeSavePolicyDiagnostic>? diagnostics = null,
        bool consumeAfterSuccessfulRestore = false)
    {
        Kind = kind;
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
        ConsumeAfterSuccessfulRestore = consumeAfterSuccessfulRestore;
    }

    public RuntimeSaveKind Kind { get; }
    public RuntimeSaveContextSnapshot Context { get; }
    public IReadOnlyList<RuntimeSavePolicyDiagnostic> Diagnostics { get; }
    public bool IsAllowed => Diagnostics.Count == 0;
    public bool ConsumeAfterSuccessfulRestore { get; }
}

public sealed record RuntimeSaveRecord
{
    public RuntimeSaveRecord(
        RuntimeSaveKind kind,
        RuntimeSaveGameSnapshot snapshot,
        RuntimeSaveContextSnapshot context,
        long sequence = 0)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Save sequence cannot be negative.");
        }

        Kind = kind;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Sequence = sequence;
    }

    public RuntimeSaveKind Kind { get; }
    public RuntimeSaveGameSnapshot Snapshot { get; }
    public RuntimeSaveContextSnapshot Context { get; }
    public long Sequence { get; }
}

public interface IRuntimeSavePolicyService
{
    RuntimeSavePolicyAssessment AssessSave(RuntimeSaveKind kind, RuntimeSaveContextSnapshot context);
    RuntimeSavePolicyAssessment AssessLoad(
        RuntimeSaveRecord? record,
        RuntimeSaveKind expectedKind,
        RuntimeSaveContextSnapshot context);
}

public sealed class RuntimeSavePolicyService : IRuntimeSavePolicyService
{
    private readonly RuntimeSavePolicyOptions _options;

    public RuntimeSavePolicyService(RuntimeSavePolicyOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public RuntimeSavePolicyAssessment AssessSave(RuntimeSaveKind kind, RuntimeSaveContextSnapshot context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<RuntimeSavePolicyDiagnostic> diagnostics = ValidateContext(kind, context);
        return new RuntimeSavePolicyAssessment(kind, context, diagnostics);
    }

    public RuntimeSavePolicyAssessment AssessLoad(
        RuntimeSaveRecord? record,
        RuntimeSaveKind expectedKind,
        RuntimeSaveContextSnapshot context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<RuntimeSavePolicyDiagnostic> diagnostics = ValidateContext(expectedKind, context);
        if (record is null)
        {
            diagnostics.Add(new RuntimeSavePolicyDiagnostic(
                RuntimeSavePolicyDiagnosticCode.MissingSaveRecord,
                $"No {expectedKind.ToString().ToLowerInvariant()} save record is available.",
                expectedKind,
                context.ContextId));
        }
        else if (record.Kind != expectedKind)
        {
            diagnostics.Add(new RuntimeSavePolicyDiagnostic(
                RuntimeSavePolicyDiagnosticCode.SaveKindMismatch,
                $"Save record kind '{record.Kind}' cannot be loaded as '{expectedKind}'.",
                expectedKind,
                context.ContextId));
        }

        bool consume = diagnostics.Count == 0 &&
            expectedKind == RuntimeSaveKind.Suspend &&
            _options.SuspendLoadBehavior == RuntimeSuspendLoadBehavior.ConsumeAfterSuccessfulRestore;
        return new RuntimeSavePolicyAssessment(expectedKind, context, diagnostics, consume);
    }

    private List<RuntimeSavePolicyDiagnostic> ValidateContext(
        RuntimeSaveKind kind,
        RuntimeSaveContextSnapshot context)
    {
        var diagnostics = new List<RuntimeSavePolicyDiagnostic>();
        if (context.HasPendingHostAction)
        {
            diagnostics.Add(new RuntimeSavePolicyDiagnostic(
                RuntimeSavePolicyDiagnosticCode.PendingHostAction,
                "Save operations are not allowed while a host action is pending.",
                kind,
                context.ContextId));
        }

        if (!_options.AllowedContexts(kind).Contains(context.ContextId))
        {
            diagnostics.Add(new RuntimeSavePolicyDiagnostic(
                RuntimeSavePolicyDiagnosticCode.ContextNotAllowed,
                $"Save kind '{kind}' is not allowed in context '{context.ContextId}'.",
                kind,
                context.ContextId));
        }

        return diagnostics;
    }
}
