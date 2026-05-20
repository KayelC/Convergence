using System.Collections.Generic;

namespace JRPGPrototype.Data.Definitions.Schemas
{
    public sealed record SchemaValidationResult(
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }
}
