# Framework Source Ownership

## Purpose

The executable source inventory is
[`../../tests/Convergence.Framework.Tests/Fixtures/framework-source-inventory.json`](../../tests/Convergence.Framework.Tests/Fixtures/framework-source-inventory.json).
It currently accounts for 93 active Framework C# files. Architecture tests
compare that inventory with the filesystem and fail when a source is missing,
duplicated, assigned to the wrong directory owner, or misclassified as exported
or internal-only.

Every exported runtime type is also matched to a public declaration in its
namespace owner's inventoried source. This makes ownership auditable without
creating one Markdown copy of every C# file.

## Owners

| Owner | Path | Namespace | Responsibility |
|---|---|---|---|
| Combat rules | `Battle/` | `Convergence.Battle` | Arithmetic, defenses, affinities, resistance, and the supplied combat ruleset. |
| Catalog | `Catalog/` | `Convergence.Catalog` | Qualified loading, repositories, catalog construction, and definition qualification. |
| Content contracts | `Content/` | `Convergence.Content` | Authored definitions, IDs, vocabulary, conditions, and effects. |
| Encounter runtime | `Encounters/` | `Convergence.Encounters` | Actor creation, encounters, lifecycle ports, negotiation, rewards, and events. |
| Effect execution | `Execution/` | `Convergence.Execution` | Runtime actor state, targeting, effects, skills, items, actions, and lifecycle. |
| Fusion and Compendium | `Fusion/` | `Convergence.Fusion` | Planning, transactions, strategy policies, recall, integrity, and pricing. |
| Host boundaries | `Hosting/` | `Convergence.Hosting` | Content, command, event, and randomness ports. |
| Inheritance | `Inheritance/` | `Convergence.Inheritance` | Inheritance eligibility, planning, and selection validation. |
| Internal shared | `Internal/` | `Convergence.Internal` | Non-exported implementation guards. |
| Combat knowledge | `Knowledge/` | `Convergence.Knowledge` | Elemental, ailment, and instant-death knowledge stores. |
| Assembly metadata | `Properties/` | none | Assembly attributes and test visibility. |
| Runtime services | `Runtime/` | `Convergence.Runtime` | Progression, rosters, resources, travel, persistence, restoration, and rulesets. |
| Internal serialization | `Serialization/` | `Convergence.Serialization` | Non-exported JSON DTOs, converters, generated metadata, and mapping. |
| Turn economy | `TurnEconomy/` | `Convergence.Execution`, `Convergence.TurnEconomy` | Generic contracts and the optional Action Token implementation. |
| Content validation | `Validation/` | `Convergence.Validation` | Semantic validation, diagnostics, and registration snapshots. |

## Documentation Layers

Convergence uses three complementary forms of API evidence:

1. `PublicAPI.Shipped.txt` guards the exact compatibility surface.
2. `Convergence.Framework.xml` documents selected composition entry points.
3. The source inventory records ownership and public-surface placement.

XML documentation is curated and intentionally incomplete; `CS1591` remains
suppressed. Consumer guidance belongs in concept-oriented developer pages,
cross-file invariants belong in technical pages, and player-visible behavior
belongs under `docs/mechanics`. Generated placeholder comments are not a
substitute for those explanations.

## Maintenance

When a Framework file moves or changes public ownership, update the inventory
in the same commit. When an exported concept changes meaning, update its API
summary and the appropriate concept document rather than documenting only its
containing file.
