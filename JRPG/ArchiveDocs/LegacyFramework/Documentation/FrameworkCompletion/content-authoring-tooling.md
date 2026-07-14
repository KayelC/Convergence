# Problem: Content Authoring Tooling

## Current State

Clean content is authored as JSON and validated through the framework loader/validator.

The framework can report structural and semantic diagnostics, but authoring is still manual and easy to get wrong.

## Problem

As soon as content grows beyond tiny examples, raw JSON becomes hard to maintain.

The framework needs enough authoring support to help users create their own game data without reading source code.

## Needed Data

Authoring support should cover:

- template manifests;
- sample skill documents;
- sample entity/race documents;
- sample item/equipment/shop documents;
- sample encounter/dungeon documents;
- sample fusion recipe documents;
- sample ruleset documents;
- registration snapshot examples;
- validation error examples.

## Decisions Still Needed

- Should schemas be generated, handwritten, or both?
- Should authoring templates live in `docs`, `Data/Jsons`, or a separate sample package?
- Should content validation have a command-line tool?
- Should placeholders be intentionally bland or lightly flavored?

## Recommended Next Step

Create human-readable templates before creating large content.

Templates should be concept examples, not production lore.
