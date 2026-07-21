# Content Authoring Validator

`Convergence.ContentValidator` is a host-side .NET 8 command-line tool for
checking authored content before a game loads it. It is deliberately separate
from `Convergence.Framework`: the tool owns filesystem access, JSON Schema
evaluation, and terminal diagnostics; Framework remains engine-neutral and has
no schema package dependency.

## Run It

From the repository root:

```powershell
dotnet run --project tools/Convergence.ContentValidator -- \
  --content-root content \
  --schema-root schemas/content/v6 \
  --registrations config/content-validator/active-samples.registrations.json
```

All three arguments are required. Paths may point at another project's content,
schema checkout, and registration profile.

Exit codes are stable:

- `0`: every layer passed, or `--help` was requested;
- `1`: authored content failed validation;
- `2`: command arguments, schema files, or the registration profile are invalid
  or unreadable.

Diagnostics use a stable `[code] source location: message` shape and are ordered
by source, location, and code.

## Validation Layers

One invocation performs the complete authoring path:

1. Parse every JSON document under the content root.
2. Evaluate the document against its declared Draft 2020-12 schema.
3. Require every document to be a manifest or owned by exactly one manifest.
4. Resolve document paths relative to their manifest without permitting the
   content root to be escaped.
5. Strictly deserialize manifests and content through Framework.
6. Apply Framework semantic and host-registration validation.
7. Resolve pack dependencies, external references, qualification, and catalog
   uniqueness by constructing a `GameDataCatalog`.

The success summary reports pack, document, and qualified-definition counts.

## Registration Profile

The registration profile is host configuration, not game content. It lists the
contexts, resources, stats, events, actor kinds, actions, policy IDs, and other
vocabulary the target game intends to support. The checked-in
`active-samples.registrations.json` profile covers only Convergence's six active
example packs.

The generic authoring tool recognizes every built-in definition type supported
by the matching Framework version. Custom formula, effect, condition, and
ailment-behavior IDs must still be listed explicitly. Their parameters are
accepted structurally by this generic tool because actual handler semantics
belong to the integrating game host. A production host should additionally use
its real `IContentParameterValidator` implementations when composing Framework
validation.

The profile currently has schema version `1`. Unknown profile properties and
unsupported profile versions are rejected rather than ignored.
