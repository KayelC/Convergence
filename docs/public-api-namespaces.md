# Public API Namespaces

Phase 8 establishes the breaking pre-release namespace identity for Convergence. No compatibility shims or type forwarding preserve the former prototype namespaces.

| Namespace | Responsibility |
|---|---|
| `Convergence.Content` | IDs, immutable definitions, semantic versions, and serializer-neutral deserialization contracts. |
| `Convergence.Catalog` | pack loading, qualification, repositories, and `GameDataCatalog`. |
| `Convergence.Serialization` | internal System.Text.Json DTOs, converters, mapper, and source-generated metadata. |
| `Convergence.Validation` | registrations, semantic validation, diagnostics, and validated packs. |
| `Convergence.Hosting` | engine-neutral content, command, event, and randomness ports. |
| `Convergence.Battle` | combat vocabulary, defense profiles, affinities, resistance, and combat rulesets. |
| `Convergence.Execution` | typed action, skill, item, effect, passive, targeting, and status execution. |
| `Convergence.Encounters` | encounter participants, orchestration, automation, lifecycle ports, negotiation, and rewards. |
| `Convergence.Knowledge` | elemental, ailment, and instant-death knowledge stores. |
| `Convergence.TurnEconomy` | Press Turn and action turn-consumption rules. |
| `Convergence.Runtime` | actor snapshots, progression, party/stock, resources, navigation, traversal, and persistence. |
| `Convergence.Fusion` | fusion planning, strategies, transactions, and Compendium services. |
| `Convergence.Inheritance` | typed fusion inheritance decisions, plans, and validated selections. |

`Convergence.DemoHost` and `Convergence.DemoHost.TrainingAnnex` are optional sample-host namespaces, not framework API namespaces.
