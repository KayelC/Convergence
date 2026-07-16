# Design Decisions

## Purpose

Decision records preserve confirmed design intent that cannot be inferred safely
from implementation alone. They prevent old prototypes, examples, or accidental
behavior from becoming authority through repetition.

## Statuses

- `proposed`: discussion is active and implementation must not assume an answer.
- `confirmed`: the project owner approved the decision.
- `superseded`: a newer record replaces the decision.
- `rejected`: the option was considered and deliberately not selected.

## Record Template

```markdown
# Decision: Short Title

Status: proposed
Date: YYYY-MM-DD

## Context

What problem or ambiguity requires a decision?

## Decision

What behavior is approved?

## Alternatives

What other approaches were considered?

## Consequences

What mechanics, APIs, content, saves, or hosts are affected?

## Evidence

Which source, tests, diagrams, and documentation must agree?
```

Decision records use lowercase kebab-case filenames. Confirmed or superseded
records must link every affected mechanics, developer, and technical page.
