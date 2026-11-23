# Step 75 · Linting and formatting the frontend

**Goal:** the backend has been linted from the start: analyzers and code style run inside `dotnet build`, warnings fail it. The frontend had only TypeScript's type checks: no linter, and a Prettier configuration nothing ran (62 files didn't match it). Now ESLint and Prettier run locally and in CI (asked 2026-11-23). Branch `chore/frontend-lint`, from `feat/node-pins` (PR #20): formatting every file would have clashed with it otherwise.

## How it's built

- **ESLint, with angular-eslint** (`ng add angular-eslint`, v22.8, matching Angular 22): `eslint.config.js`, the new flat config, a JavaScript file rather than JSON.
  - For `.ts`: ESLint's and typescript-eslint's recommended rules, typescript-eslint's stylistic ones, and angular-eslint's (`app-` prefixes for selectors among them). Inline templates go through the template rules too.
  - For `.html`: angular-eslint's template rules, accessibility included.
  - `npm run lint` runs it (`ng lint`, the `lint` target in `angular.json`). The generated API client (`src/app/api/`) is left out.
- **Three findings, all fixed:**
  - `({ machines: _, ...factory }) => factory` (`factory-outlines.ts`) leaves a field out by destructuring it into a name never used. That's an idiom, so the rule allows it: `no-unused-vars` with `ignoreRestSiblings`.
  - The map spec's `ResizeObserver` stub had empty `observe()` and `disconnect()`. They're empty on purpose, so each now says why in a comment, which `no-empty-function` accepts.
- **`strictTemplates`** is on in `tsconfig.json`; the templates passed as they were. TypeScript 6 is strict by default, checked with a probe (implicit `any` and `null` into `string` both fail), so `strict` needn't be set.
- **Prettier:** `npm run format` and `npm run format:check`, over the whole web folder (it skips what `.gitignore` lists: the API client, `dist`, `.angular`). One commit, `style(web): format with Prettier`, reformatted 65 files and changed nothing else: lint, the 294 tests and the build pass after it.
  - **`.git-blame-ignore-revs`** names that commit, so `git blame` and GitHub skip it and a line keeps the commit that really changed it. It works as long as the PR is merged with a merge commit, as this repo's are: a squash or rebase gives the commit another SHA.
  - No `eslint-config-prettier`: typescript-eslint v8 has no formatting rules left, so the two don't fight.
- **CI** (`ci.yml`, frontend job): *Check formatting (Prettier)* and *Lint (ESLint)* run after `npm ci`, before the tests: they're quick and need no generated client.

### Technical choices to overrule

- **The rule sets:** recommended plus typescript-eslint's stylistic, as `ng add` set them. typescript-eslint's `strict` set, or the type-aware rules (floating promises and the like, slower, need the generated client), would find more.
- **Prettier's settings are the scaffold's:** 100 characters wide, single quotes.
- **Only the web app:** the Python prototype and the docs aren't formatted.

## Commits

`chore(web)` ESLint and strict templates · `chore(web)` format scripts · `style(web)` the formatting alone · `ci` the two checks · `chore` the blame-ignore file · `docs` this log, primer, quality guide, CLAUDE.md commands.

## Tests

No new tests: this changes tooling. `npm run lint` passes (exit 0), `npm run format:check` passes, 294 web tests pass (exit 0), the build is clean.
