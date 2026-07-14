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
tests/
  Convergence.Framework.Tests/
  Convergence.DemoHost.Tests/
content/
  reference/
  demos/
  original/training-annex/
docs/
ArchiveDocs/
  LegacyFramework/
  LegacyRepository/
```

| Path | Ownership |
|---|---|
| `src/Convergence.Framework` | The only reusable product assembly. It owns definitions, catalogs, rules, runtime state, transitions, diagnostics, and host-neutral ports. |
| `samples/Convergence.DemoHost` | Optional console example. It owns filesystem reads, terminal input/output, host JSON, and Training Annex orchestration. |
| `tests/Convergence.Framework.Tests` | Framework-only tests. This project references only Framework. |
| `tests/Convergence.DemoHost.Tests` | Example-host tests. This project references Framework and DemoHost only. |
| `content/reference` | Small schema and catalog reference packs. |
| `content/demos` | Focused battle and shared-effect demonstrations. |
| `content/original/training-annex` | Original end-to-end example content. |
| `docs` | Current product documentation. |
| `ArchiveDocs/LegacyFramework` | Non-built, unsupported prototype history and migration evidence. |
| `ArchiveDocs/LegacyRepository` | Retired pre-Convergence root solution, README, documentation, and older file archive. |

Generated `bin` and `obj` directories are not source and are ignored by Git.

## Dependency Direction

```text
Convergence.Framework.Tests ---> Convergence.Framework

Convergence.DemoHost.Tests ----> Convergence.DemoHost ----> Convergence.Framework
             `--------------------------------------------> Convergence.Framework
```

Framework has no project reference and no external package dependency. No active project references the archive.

## Framework Source Areas

- `Content`, `Catalog`, `Serialization`, `Validation`
- `Hosting`
- `Battle`, `Execution`, `Encounters`, `Knowledge`, `TurnEconomy`
- `Runtime`
- `Fusion`, `Inheritance`

See [Public API Namespaces](public-api-namespaces.md) for detailed responsibility.

## Historical Material

The archived console executable, adapters, DTOs, prototype content,
characterization tests, ledgers, reviews, and migration plans are preserved
under `ArchiveDocs/LegacyFramework`. The superseded repository-root solution,
README, documentation, and older archive are preserved under
`ArchiveDocs/LegacyRepository`. Both trees are excluded from `Convergence.sln`,
are not copied by active projects, and are not release inputs.
