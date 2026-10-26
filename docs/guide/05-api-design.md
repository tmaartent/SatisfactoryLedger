# 05 · API design

The API is a **product** with a contract. Design it from the client's needs (the screens), not from the database tables.

## Resource model (first draft, refine per milestone)

| Method & route | Purpose | Success |
|---|---|---|
| `GET /api/v1/catalog/items?form=solid` | Browse items | 200 |
| `GET /api/v1/catalog/recipes?produces={itemId}` | Recipes for an item, incl. alternates | 200 |
| `GET /api/v1/catalog/transport-tiers` | Belt and pipe tiers with capacity | 200 |
| `POST /api/v1/catalog/imports` | Import a game Docs file | 202 or 201 + Location |
| `POST /api/v1/blueprints` | Design a new blueprint | 201 + Location |
| `GET /api/v1/blueprints/{id}` | Blueprint with steps, inputs, outputs | 200 + ETag |
| `PUT /api/v1/blueprints/{id}/steps/{recipeId}` | Add or change the step for a recipe (machine count, clock speed, somersloops). The recipe identifies the step (BR-B16). | 201 / 200 |
| `DELETE /api/v1/blueprints/{id}/steps/{recipeId}` | Remove a step | 204 |
| `GET /api/v1/blueprints/{id}/balance` | Item balances (green/red) | 200 |
| `POST /api/v1/factories` | Place a blueprint (`blueprintId`, `stackHeight`) | 201 |
| `PATCH /api/v1/factories/{id}` | Restack (`If-Match` required) | 200 / 412 / 422 |
| `POST /api/v1/supply-lines` | Create a line with its source | 201 |
| `POST /api/v1/supply-lines/{id}/points` | Add a tap or injection at a position | 201 |
| `GET /api/v1/supply-lines/{id}/analysis` | Segment-by-segment analysis | 200 |
| `POST /api/v1/layout-suggestions` | Compute a suggestion (nothing stored) | 200 |
| `GET /api/v1/world/network` | Graph of factories and lines for the world view | 200 |

Design notes:
- **Nouns, plural, kebab-case.** The hierarchy only goes as deep as ownership (steps belong to a blueprint, factories don't).
- **Computations as resources:** `/balance` and `/analysis` are read-only sub-resources (GET, cacheable). A suggestion takes complex input, so it's a POST that creates nothing (200, not 201). Document that explicitly.
- **Commands that aren't CRUD:** if "restack" grows rules of its own, `POST /factories/{id}/restack` is clearer than a PATCH. Both are defensible, so pick one style and stay consistent.

## Status codes and errors

| Situation | Code |
|---|---|
| Malformed request, missing field | 400 + validation ProblemDetails (`errors` per field) |
| Resource doesn't exist | 404 |
| Business rule violated (stack exceeds belt capacity) | **422** + ProblemDetails with `code: "Factory.StackExceedsBeltCapacity"` and a domain-language `detail` |
| Concurrency conflict (`If-Match` stale) | 412 |
| `If-Match` missing on a protected update | 428 |
| Unexpected failure | 500, generic ProblemDetails, no stack traces (`traceId` included) |

- Use **RFC 9457 ProblemDetails everywhere** (`AddProblemDetails()`, `UseExceptionHandler()`).
- Error codes are part of the contract: stable, documented, and the frontend switches on them, never on message text.

## Representation

- **Rates (ADR-0004):** suggested shape `{ "perMinute": 5.625, "exact": "45/8" }`. Decimal for display, exact string for clients that care. Decide and document it.
- IDs are opaque strings (GUID v7 sorts by time, which works well for indexes). Don't expose database keys with meaning.
- Timestamps in ISO 8601 UTC. Enums as strings (`"solid"`, not `0`).
- Analysis responses spell out the domain: `segments[]` with `flowIn`, `action`, `flowOut`, `overflow`, `deficit`, plus a summary of `availableForFuture`, `strandedCapacity` and `minimumOverflow`. Fluid lines report `unusedSupply` and `underclockAdvice` instead. Think about whether that's one response shape with optional fields, or two shapes with a discriminator (`"form": "solid" | "fluid"`). Both are valid, but the choice shows up in the OpenAPI document and the generated client.

## Versioning, documentation, contract

- URL segment versioning (`/api/v1`), e.g. with `Asp.Versioning.Http`. Additive changes don't need a new version. Breaking changes do.
- Built-in OpenAPI (`Microsoft.AspNetCore.OpenApi`) + **Scalar** as the interactive UI in development.
- Give every endpoint a `WithName`, `WithSummary`, typed results (`Results<Created<…>, ValidationProblem, ProblemHttpResult>`) so the OpenAPI document is exact. That document generates the frontend client (ADR-0006).
- Commit `openapi.v1.json`. CI fails when the generated document differs from the committed one.

## Minimal API structure

- Each module exposes `MapBlueprintEndpoints(this IEndpointRouteBuilder)` with a **route group** (`/api/v1/blueprints`), shared tags and filters.
- An endpoint maps HTTP ↔ command/query and result ↔ status code. Nothing else.
- Input validation: .NET 10 built-in minimal API validation (`AddValidation()` + data annotations) or FluentValidation. Shape only, never business rules.

## Done when (per endpoint)
- [ ] Appears correctly in Scalar with summary, request and response schemas and all error codes
- [ ] Integration test covers success and the main error paths
- [ ] Error codes documented and stable
- [ ] Committed OpenAPI document updated in the same PR
