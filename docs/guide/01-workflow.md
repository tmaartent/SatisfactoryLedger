# 01 · Workflow

How you work is visible in the git history. Reviewers *do* look at it.

## Git

- **Trunk-based development:** `main` is always green. Work on short-lived branches (`feat/rate-value-object`) that live for hours to days, not weeks.
- **Pull requests even when you're the only developer.** Each PR is a reviewable story, and CI runs on it. Ask Claude to review the PR before merging. Squash-merge or rebase-merge to keep `main` linear.
- **Conventional Commits:** `feat(network): analyse injections on supply lines`, `test(shared): rate normalisation`, `refactor(blueprint): extract step rate calculation`, `docs(adr): accept ADR-0007`. Scopes are the module names.
- **Small commits that follow the TDD rhythm.** A good history reads `test: …` → `feat: …` → `refactor: …`. Don't commit red tests to `main`.
- **First commit:** the scaffold as-is (`chore: initial scaffold`), so all your own work is visible from the second commit onward.

## Issues and planning

- Create a GitHub issue per milestone slice (M1, M2, …) and per business rule group. Link PRs to issues (`Closes #12`).
- A GitHub Project board (Todo / Doing / Done) is enough. It shows you can plan.
- Write the acceptance criteria in the issue, in domain language. They become your first tests.

## Documentation as code

- The glossary, business rules and ADRs change **in the same PR** as the code that changes them.
- A PR template (`.github/pull_request_template.md`) with checkboxes: tests added · business rules updated · ADR needed? · OpenAPI changed?

## CI from day one

Set up GitHub Actions before you write domain code (see [chapter 07](07-quality-and-delivery.md)). A green badge in the README from the first week shows discipline.

## Done when
- [ ] Repo on GitHub (public, when you're comfortable showing it), with the scaffold as the first commit
- [ ] Branch protection or a ruleset on `main`: PR required, CI must pass (free on public repos; private repos need a paid plan)
- [ ] PR template and issue for M1 created
- [ ] CI workflow builds and tests backend and frontend
