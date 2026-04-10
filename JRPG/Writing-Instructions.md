# Writing-Instructions.md

## Purpose

This document defines how documentation must be created and maintained across the entire codebase. It is designed for iterative generation (e.g., via LLM tools like Gemini CLI) and must evolve continuously into a **comprehensive Product Requirements Document (PRD)**.

The key principle is **progressive enhancement without loss of information**.

---

## Core Rules

### 1. Never Remove Content

* **Do NOT delete, overwrite, or summarize away existing sections.**
* All previously written information must remain intact.
* If something becomes outdated or incorrect:

  * Add a new subsection labeled `Update` or `Revision`
  * Clearly explain the change
  * Preserve the original content for historical traceability

---

### 2. Always Append, Never Replace

* All new information must be **added**, not rewritten.
* Prefer:

  * Adding new sections
  * Expanding existing sections
  * Appending deeper details
* Avoid:

  * Rewriting entire sections
  * Collapsing detailed content into summaries

---

### 3. Maintain Iterative Structure

Each iteration should:

* Build on previous knowledge
* Increase specificity and clarity
* Add missing technical, product, or architectural details

Every pass should answer:

* What is missing?
* What is unclear?
* What needs deeper technical breakdown?

---

### 4. Use Structured Expansion

When adding content, follow consistent structure:

#### Example Section Pattern

```
## Feature: [Feature Name]

### Overview
High-level explanation

### Requirements
Detailed functional and non-functional requirements

### Implementation Details
Technical explanation (APIs, data models, logic)

### Edge Cases
List of edge conditions and handling

### Open Questions
Unresolved decisions or ambiguities

### Future Enhancements
Potential improvements or extensions
```

---

### 5. Prefer Depth Over Brevity

* Be **highly detailed**
* Document:

  * Data flows
  * Internal logic
  * Assumptions
  * Constraints
* Avoid vague descriptions

---

### 6. Preserve Historical Context

* Do not “clean up” older ideas
* Keep:

  * Deprecated approaches
  * Rejected ideas (mark them clearly)
* This helps track product evolution

---

### 7. Clearly Mark Updates

When modifying an existing concept:

```
### Update (YYYY-MM-DD)
Explanation of what changed and why
```

---

### 8. Expand Across the Entire Codebase

Documentation should progressively cover:

* System Architecture
* Features
* APIs
* Data Models
* Business Logic
* UI/UX Behavior
* Integrations
* Deployment & Infrastructure
* Error Handling
* Security Considerations
* Performance Constraints

---

### 9. No Assumptions Left Undocumented

If something is implied:

* Make it explicit
* Add explanation

---

### 10. Consistency is Critical

* Use consistent naming conventions
* Reuse terminology already defined
* If a term changes:

  * Document the transition
  * Do NOT silently rename

---

### 11. Encourage Redundancy for Clarity

* It is acceptable to repeat information across sections if it improves understanding
* Optimize for **readability and completeness**, not conciseness

---

### 12. Treat This as a Living PRD

This document should eventually answer:

* What are we building?
* Why are we building it?
* How does it work internally?
* How should it evolve?

---

## Iteration Workflow

Each time the document is updated:

1. **Read existing content first**
2. Identify:

   * Missing areas
   * Weak explanations
   * Undocumented features
3. Append new sections or expand existing ones
4. Add updates instead of modifying old text
5. Increase technical and product clarity

---

## Strict Prohibitions

* ❌ Do NOT delete sections
* ❌ Do NOT rewrite large portions of text
* ❌ Do NOT simplify detailed content into summaries
* ❌ Do NOT remove “outdated” ideas — annotate them instead

---

## Guiding Principle

> This document should only ever grow in clarity, depth, and completeness — never shrink.

---

## Final Note

This is not just documentation.
It is the **single source of truth** for the system’s design, behavior, and evolution.

Every iteration should move it closer to a **fully exhaustive, production-grade PRD**.
