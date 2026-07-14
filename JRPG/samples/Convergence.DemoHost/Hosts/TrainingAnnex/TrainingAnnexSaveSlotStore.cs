using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed class TrainingAnnexSaveSlotStore
{
    private string? _manualRecordJson;
    private string? _suspendRecordJson;

    public bool Has(RuntimeSaveKind kind) => GetRaw(kind) is not null;

    public string? GetRaw(RuntimeSaveKind kind) =>
        kind == RuntimeSaveKind.Manual ? _manualRecordJson : _suspendRecordJson;

    public void Save(RuntimeSaveRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        SetRaw(record.Kind, CleanSaveJsonCodec.SerializeRecord(record));
    }

    public void SetRaw(RuntimeSaveKind kind, string? json)
    {
        if (kind == RuntimeSaveKind.Manual)
        {
            _manualRecordJson = json;
        }
        else
        {
            _suspendRecordJson = json;
        }
    }

    public void Consume(RuntimeSaveKind kind) => SetRaw(kind, null);
}
