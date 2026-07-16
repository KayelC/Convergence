namespace Convergence.ContentValidator;

internal sealed record ValidatorDiagnostic(
    string Code,
    string SourceName,
    string Location,
    string Message)
{
    public override string ToString() => $"[{Code}] {SourceName} {Location}: {Message}";
}

internal sealed record ContentValidatorOptions(
    string ContentRoot,
    string SchemaRoot,
    string RegistrationsPath);
