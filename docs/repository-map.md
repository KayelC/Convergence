# Repository Map

## Active Product

The Git repository root is the product root. Cloning Convergence opens directly
on the active solution, README, source, samples, tests, content, and
documentation; no nested workspace directory is required.

```text
Convergence.sln
src/
  Convergence.Framework/
samples/
  Convergence.DemoHost/
  Convergence.GodotHost/
tools/
  Convergence.ContentValidator/
eng/
  Assert-CoberturaCoverage.ps1
tests/
  Convergence.Framework.Tests/
  Convergence.DemoHost.Tests/
  Convergence.ContentValidator.Tests/
config/
  content-validator/
content/
  reference/
  demos/
  original/training-annex/
docs/
  mechanics/
  developer-guide/
  technical/
  decisions/
  reference/
  reviews/
  roadmap/
ArchiveDocs/
  LegacyFramework/
  LegacyRepository/
```

| Path | Ownership |
|---|---|
| `src/Convergence.Framework` | The only reusable product assembly. It owns definitions, catalogs, rules, runtime state, transitions, diagnostics, and host-neutral ports. |
| `samples/Convergence.DemoHost` | Optional console example. It owns filesystem reads, terminal input/output, host JSON, and Training Annex orchestration. |
| `samples/Convergence.GodotHost` | Real Godot 4.7.1 .NET source-reference consumer and headless integration proof. |
| `tools/Convergence.ContentValidator` | Host-side authoring CLI. It owns filesystem discovery and independent JSON Schema evaluation, then delegates semantic and catalog validation to Framework. |
| `tests/Convergence.Framework.Tests` | Framework-only tests. This project references only Framework. |
| `tests/Convergence.DemoHost.Tests` | Example-host tests. This project references Framework and DemoHost only. |
| `tests/Convergence.ContentValidator.Tests` | Validator CLI tests. This project references the tool and its transitive Framework dependency. |
| `eng` | Repository-owned release-gate helpers, currently the Framework Cobertura threshold verifier. |
| `config/content-validator` | Explicit host-registration profiles used by authoring validation. |
| `content/reference` | Small schema and catalog reference packs. |
| `content/demos` | Focused battle and shared-effect demonstrations. |
| `content/original/training-annex` | Original end-to-end example content. |
| `docs/mechanics` | Player-visible and designer-facing rules. |
| `docs/developer-guide` | Host integration and extension recipes. |
| `docs/technical` | State machines, ordering, invariants, and implementation ownership. |
| `docs/decisions` | Proposed, confirmed, superseded, and rejected design decisions. |
| `docs/reference` | Tested inventories, API ownership, and documentation coverage. |
| `docs/reviews` | Source-review evidence that does not define current mechanics. |
| `docs/roadmap` | Product priorities, capability maturity, and completed release records. |
| `ArchiveDocs/LegacyFramework` | Non-built, unsupported prototype history and migration evidence. |
| `ArchiveDocs/LegacyRepository` | Retired pre-Convergence root solution, README, documentation, and older file archive. |

Generated `bin` and `obj` directories are not source and are ignored by Git.

## Dependency Direction

```text
Convergence.Framework.Tests ---> Convergence.Framework

Convergence.DemoHost.Tests ----> Convergence.DemoHost ----> Convergence.Framework
             `--------------------------------------------> Convergence.Framework

Convergence.ContentValidator.Tests ---> Convergence.ContentValidator ---> Convergence.Framework

Convergence.GodotHost -----------------------------------------------> Convergence.Framework
```

Framework has no project reference and no external runtime package dependency. Its pinned analyzer and compiler packages are private build dependencies. No active project references the archive.

## Framework Source Areas

- `Content`, `Catalog`, `Serialization`, `Validation`
- `Hosting`
- `Battle`, `Execution`, `Encounters`, `Knowledge`, `TurnEconomy`
- `Runtime`
- `Fusion`, `Inheritance`

See [Public API Namespaces](public-api-namespaces.md) for detailed responsibility.
The tested [Framework Source Ownership](reference/framework-source-ownership.md)
inventory accounts for each active C# file and its exported or internal-only
surface.

## Historical Material

The archived console executable, adapters, DTOs, prototype content,
characterization tests, ledgers, reviews, and migration plans are preserved
under `ArchiveDocs/LegacyFramework`. The superseded repository-root solution,
README, documentation, and older archive are preserved under
`ArchiveDocs/LegacyRepository`. Both trees are excluded from `Convergence.sln`,
are not copied by active projects, and are not release inputs.
