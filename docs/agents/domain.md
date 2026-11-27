# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`GLOSSARY.md`** at the repo root: the ubiquitous language.
- **`docs/domain/business-rules.md`**: the business rules (`BR-*`) the glossary and the code refer to.
- **`docs/domain/context-map.md`**: the modules and how they relate.
- **`docs/adr/`**: read ADRs that touch the area you're about to work in.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

Single-context repo: one glossary for the whole modular monolith, one set of ADRs.

```
/
├── GLOSSARY.md
├── docs/
│   ├── adr/
│   │   ├── 0001-record-architecture-decisions.md
│   │   └── 0002-modular-monolith.md
│   └── domain/
│       ├── business-rules.md
│       └── context-map.md
└── src/
```

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `GLOSSARY.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal: either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0026 (steady state as a sequence of linear programmes), but worth reopening because…_
