# Convergence Technical Documentation

> **Status: Current implementation reference.** Files in this tree explain the source code as it exists. They do not override approved GDD or schema decisions and must be refreshed as consumers migrate.

This tree is the code-level documentation layer for Convergence. The existing
`docs/` folder explains project vision, architecture, and subsystem concepts.
`TechnicalDocs/` explains how the source files implement those concepts.

The documentation is intentionally organized like the codebase. A source file
such as `Logic/Fusion/FusionConductor.cs` should eventually have a matching
technical document such as `TechnicalDocs/Logic/Fusion/FusionConductor.md`.

## Reading Model

- Use `docs/README.md` first for project-wide orientation.
- Use `docs/subsystems/*.md` for subsystem-level concept summaries.
- Use this tree when reviewing or changing code.

## Current Coverage

- `Logic/Fusion`: first technical pass covering the root Fusion module files
  and scaffolding the mirrored folder layout.

## Documentation Standard

Each detailed file document should be code-heavy enough to support review
without constantly jumping back to the source file.

Detailed documents should include:

- relevant class and method signatures,
- focused C# snippets for important branches and mutation points,
- method-level walkthroughs that explain what the code is doing,
- state reads and writes tied to the exact members being touched,
- invariants and safety rules that future edits must preserve,
- data dependencies and where they enter the method,
- cancel, failure, and edge behavior,
- tests or smoke paths that protect the behavior,
- refactor notes for the framework direction.

The goal is not to paste entire source files into Markdown. The goal is to
quote the important parts of the implementation and explain why they matter.
