# Legacy Framework Archive

## Status

This folder is the approved holding area for legacy framework or console-host source material after it is proven obsolete.

No active production source has been moved here yet. As of Track T5, `Convergence.Tests/Fixtures/Parity/archive-candidate-review.t5.json` records 0 archive candidates and 0 removal authorizations. Every protected legacy capability in `Convergence.Tests/Fixtures/Parity/recovery-baseline.json` still has `removalAuthorized: false`, so moving live compatibility code would break the recovery branch instead of completing the framework.

## Archive Rule

Track S uses archive-first retirement:

1. Promote a capability to `clean_parity` only after the framework replacement, migrated consumer, tests, and docs all prove parity.
2. Set `consumerMigrated: true` and `removalAuthorized: true` in the parity ledger for the specific retired capability.
3. Move the obsolete source or data into this folder, preserving the original relative path under a track-specific subfolder.
4. Remove it from active project files, build inputs, data-loading paths, and runtime references.
5. Run the full quality gate before committing.

Example future layout:

```text
ArchiveDocs/LegacyFramework/
  TrackS/
    Logic/Battle/Effects/DamageEffect.cs
    Data/SkillData.cs
```

## Important Boundary

The framework architecture is ready to build on, but the framework itself is not finished. Several gameplay systems still rely on legacy console adapters, prototype datasets, or named default policies. Those files are protected until their replacement is complete and verified.

Archiving is therefore a final gate for a specific retired file, not a broad cleanup pass.
