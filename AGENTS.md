# Convergence Engineering Guide

## Product Authority

- `src/Convergence.Framework` is the only reusable product assembly.
- `samples/Convergence.DemoHost` and `samples/Convergence.GodotHost` are optional consumers, not rule authorities.
- `tools/Convergence.ContentValidator` owns filesystem-based authoring validation.
- `ArchiveDocs` is unsupported history. Active code, tests, content, or documentation must never depend on it.
- Current source, executable tests, schemas, and active `docs` are authoritative. Review reports are evidence, not specifications.

## Framework Boundary

- Target .NET 8 and C# 12.
- Keep Framework engine-neutral, serializer-neutral at its public boundary, non-packable, and free of runtime package dependencies.
- Framework must not read files, write files, access a console, reference Godot, or own a host save-file format.
- Hosts supply content text, commands, events, randomness, presentation, scene objects, and persistence encoding through typed boundaries.
- Do not infer behavior from display names, descriptions, or presentation text.

## Change Discipline

- Prefer existing typed contracts and module ownership over new abstractions.
- Keep mutations atomic. Assessment, execution, reservations, lifecycle operations, and restoration must reject without leaving partial live state.
- Preserve immutable public results by defensive copying at construction boundaries.
- Treat content schemas, save versions, and public API signatures as separate versioned contracts.
- A public API change requires a reviewed update to `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` and an accompanying migration note when applicable.
- Update `tests/Convergence.Framework.Tests/Fixtures/framework-source-inventory.json` whenever a Framework source file is added, removed, moved, or changes public-surface ownership.
- Update mechanics documentation when player-visible rules change and integration documentation when host responsibilities change.

## Review Standard

An actionable defect should identify:

1. the intended invariant;
2. a reachable code path;
3. a concrete consequence; and
4. reproducible source or test evidence.

Label theoretical hardening, impossible domain values, and alternative product designs separately. Do not inflate them into runtime vulnerabilities.

## Verification

For Framework changes, run focused tests first, then:

```powershell
dotnet test Convergence.sln --no-restore --configuration Release
dotnet build Convergence.sln --configuration Release --no-restore --no-incremental -warnaserror
dotnet format Convergence.sln --verify-no-changes --no-restore
git diff --check
```

Run relevant DemoHost modes, content validation, Godot smoke coverage, documentation checks, and boundary searches when the changed area affects them. Keep commits narrow and independently green.
