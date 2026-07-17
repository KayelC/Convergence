# Convergence Engineering Guide

## Product Authority

- `src/Convergence.Framework` is the only reusable product assembly.
- `samples/Convergence.DemoHost` and `samples/Convergence.GodotHost` are optional consumers, not rule authorities.
- `tools/Convergence.ContentValidator` owns filesystem-based authoring validation.
- `ArchiveDocs` is unsupported history. Active code, tests, content, or documentation must never depend on it.
- Current source and executable tests define implemented behavior.
- Confirmed mechanics and decision records define intended design.
- Schemas define authored structural contracts.
- Review reports are evidence, not specifications.
- If implemented behavior and intended design disagree, stop and surface the discrepancy. Do not silently choose one.

## Design Authority

- Follow [`docs/documentation-design-pattern.md`](docs/documentation-design-pattern.md) for documentation structure, review states, diagrams, and evidence.
- Follow [`docs/policy-family-design-pattern.md`](docs/policy-family-design-pattern.md) whenever multiple coherent mechanics answer the same gameplay question. Audit state, mutation, lifecycle, persistence, events, and host composition before defining the shared policy contract.
- Read the relevant mechanics, developer-guide, technical, and decision documents before changing a rule.
- Do not infer an unclear rule from current code, display text, examples, or archived behavior.
- Explain uncertain behavior in plain language and obtain the project owner's decision before treating it as intended design.
- Record confirmed decisions under `docs/decisions` and update every affected audience document.
- A document becomes `reviewed` only after source verification and explicit project-owner confirmation.
- `AGENTS.md` governs working discipline. It must point to design authority rather than duplicating the complete mechanics manual.

## Framework Boundary

- Target .NET 8 and C# 12.
- Keep Framework engine-neutral, serializer-neutral at its public boundary, non-packable, and free of runtime package dependencies.
- Framework must not read files, write files, access a console, reference Godot, or own a host save-file format.
- Hosts supply content text, commands, events, randomness, presentation, scene objects, and persistence encoding through typed boundaries.
- Do not infer behavior from display names, descriptions, or presentation text.

## Change Discipline

- Prefer existing typed contracts and module ownership over new abstractions.
- Give each policy family one explicitly selected authority per runtime scope. Do not add configuration flags, silent defaults, or direct mutation paths that let one implementation impersonate several incompatible mechanics.
- Keep mutations atomic. Assessment, execution, reservations, lifecycle operations, and restoration must reject without leaving partial live state.
- Preserve immutable public results by defensive copying at construction boundaries.
- Treat content schemas, save versions, and public API signatures as separate versioned contracts.
- A public API change requires a reviewed update to `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` and an accompanying migration note when applicable.
- Update `tests/Convergence.Framework.Tests/Fixtures/framework-source-inventory.json` whenever a Framework source file is added, removed, moved, or changes public-surface ownership.
- Update mechanics documentation when player-visible rules change and integration documentation when host responsibilities change.

## Documentation Discipline

- Maintain three coordinated audiences:
  - `docs/mechanics` for player-visible and designer-facing rules;
  - `docs/developer-guide` for host integration and extension recipes;
  - `docs/technical` for state machines, invariants, ordering, and ownership.
- Use concept-oriented documents. Do not create hand-maintained prose copies of every C# file.
- Use Mermaid diagrams when state, sequence, ownership, or data flow is easier to understand visually.
- Mark assumptions, configured defaults, optional modules, unresolved decisions, and host responsibilities explicitly.
- Update the documentation coverage matrix whenever a capability's documentation state or document ownership changes.
- Preserve review reports under `docs/reviews` and active priorities under `docs/roadmap`; neither replaces current design authority.

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
