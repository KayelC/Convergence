# Public API Namespaces

Convergence `0.1.0` exposes the following framework-owned namespaces. The
checked-in API baseline guards their public members; no compatibility shims or
type forwarding preserve former prototype namespaces.

| Namespace | Responsibility |
|---|---|
| `Convergence.Content` | IDs, immutable definitions, semantic versions, and serializer-neutral deserialization contracts. |
| `Convergence.Catalog` | pack loading, qualification, repositories, and `GameDataCatalog`. |
| `Convergence.Validation` | registrations, semantic validation, diagnostics, and validated packs. |
| `Convergence.Hosting` | engine-neutral content, command, event, and randomness ports. |
| `Convergence.Battle` | combat vocabulary, defense profiles, affinities, resistance, and combat rulesets. |
| `Convergence.Execution` | typed action, skill, item, effect, passive, targeting, and status execution. |
| `Convergence.Encounters` | encounter participants, orchestration, automation, lifecycle ports, negotiation, and rewards. |
| `Convergence.Knowledge` | elemental, ailment, and instant-death knowledge stores. |
| `Convergence.TurnEconomy` | Action Token and action turn-consumption rules. |
| `Convergence.Runtime` | actor snapshots, progression, party and rosters, resources, navigation, traversal, and persistence. |
| `Convergence.Fusion` | fusion planning, strategies, transactions, and Compendium services. |
| `Convergence.Inheritance` | typed fusion inheritance decisions, plans, and validated selections. |

`Convergence.DemoHost` and `Convergence.DemoHost.TrainingAnnex` are optional sample-host namespaces, not framework API namespaces.

`Convergence.Serialization` and `Convergence.Internal` are implementation
namespaces and intentionally export no public types. Their DTOs, converters,
mappers, generated metadata, and helper algorithms may change without becoming
part of the supported API contract.
