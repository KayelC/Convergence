# Problem: Legacy Retirement Gate

## Current State

The legacy console prototype remains active and protected.

The Track T5 archive review found:

- 36 protected capabilities reviewed;
- 0 `clean_parity` capabilities;
- 0 archive candidates;
- 0 removal authorizations.

`ArchiveDocs/LegacyFramework` is policy-only.

## Problem

Deleting or archiving active code too early would repeat the regression risk that triggered the recovery branch.

Old code should be retired only when it is genuinely unreachable through migrated consumers.

## Retirement Requirements

A capability can be retired only when all are true:

1. the framework owns the rule;
2. clean content exists if content is required;
3. the real consumer has migrated;
4. parity or approved-replacement tests pass;
5. documentation records the behavior decision;
6. the recovery parity ledger marks `clean_parity`;
7. the ledger marks `consumerMigrated: true`;
8. the ledger marks `removalAuthorized: true`;
9. retired files are moved to `ArchiveDocs/LegacyFramework/<gate>/<original-path>`;
10. the active build/runtime no longer references the retired files.

## Decisions Still Needed

- Which capability should be the first retirement candidate?
- Should retirement happen only after a clean interactive loop exists?
- How much legacy characterization must remain after retirement?

## Recommended Next Step

Do not retire anything yet.

First build more clean original content and one clean interactive consumer. Then review one narrow capability at a time.
