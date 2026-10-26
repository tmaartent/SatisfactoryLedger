# ADR-0001: Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-10-26

## Context
This is a portfolio project. A reviewer should be able to see *why* the system looks the way it does, not only *what* it is. Decisions made alone are also easily forgotten.

## Decision
We will record every significant decision (anything that's costly to reverse, or that a newcomer would ask "why?" about) as an ADR in `docs/adr/`.

## Consequences
- Decisions get reviewed as part of the PR that introduces them.
- A small writing cost per decision. Keep ADRs to one page.
