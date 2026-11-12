# Step 46 · Edit and delete a factory

**Goal:** CRUD for factories (asked 2026-11-12): every machine of your world now belongs to a factory, so new factories need old ones changed. Delete a factory from the overview; edit and delete one in 3D. Branch: `main`, uncommitted, after step 45.

## What you decided (2026-11-12)

- **In 3D, from the factory legend:** each factory in the area gets *Edit* and *Delete*.
- **Other factories' machines stay blocked while editing**, as when defining. To move a machine, take it out of its factory first.

BR-W2 now says a factory can be edited (name and machines, with the same checks) and deleted.

## API

| | |
|---|---|
| `PUT /factories/{id}` `{ name, machines }` | 200 with the factory; 404 unknown; 400 without name or machines; 422 ids that aren't machines; 409 a name or a machine another factory of the world has |
| `DELETE /factories/{id}` | 204; 404 unknown |

Editing runs the define checks (`DefineFactoryHandler.EditAsync`): the machines must be machines or generators in the world's latest snapshot (BR-W2), and the name and the machines mustn't be another factory's (BR-W11, BR-W5). The clash check (`ClashAsync`) leaves the factory itself out, so it may keep its name and machines. Both endpoints map their outcomes to the same problems (`ProblemOf`). `StoredFactory.Name` can now change.

## Web

- **Factories tab** (the overview in a snapshot's findings): a delete button per factory. The findings view only emits `deleteFactory`; the snapshot page asks in a dialog (`FactoryDeletion`, as `SnapshotDeletion` does for snapshots), deletes, and reloads the factories.
- **3D legend:** *Edit* opens the choosing mode with the factory's machines blue and its name filled in (*Edit factory*); *Save* sends `PUT` and opens its page. While editing, only the other factories' machines are blocked (`blockedMachines`): its own can be clicked or boxed out and back in. *Delete* asks, deletes, and reloads the outlines.
- The live preview (step 45) works while editing too.

## Files

| Where | What |
|---|---|
| `World/Factories/FactoryEndpoints.cs` | `PUT`, `DELETE`, `ProblemOf` |
| `World/Factories/DefineFactoryHandler.cs` | `EditAsync`, `ClashAsync` |
| `World/Factories/EditFactoryRequest.cs`, `EditFactoryCommand.cs` (new) | |
| `World/Factories/DefineFactoryOutcome.cs` | `UnknownFactory` |
| `World/Factories/StoredFactory.cs` | `Name` settable |
| `web/src/app/world/factory-deletion.ts` (new) | Ask, delete, report |
| `web/src/app/world/findings.*`, `snapshot-page.*` | Delete in the Factories tab |
| `web/src/app/world/area/*` | Edit and delete in the legend; `blockedMachines` |

## Tests

API (`WorldFactoryApiTests`, written first, 7): editing stores the new name and machines · a factory keeps its name and machines when edited with them · it can't be edited to take another factory's machine · nor renamed to another factory's name · an unknown one can't be edited · a deleted factory is gone and its machines are free again · an unknown one can't be deleted. Web: the machines that can't be chosen are every factory's when defining, only the other factories' when editing · the overview offers to delete a factory. The 3D legend has no test (three.js), as before. **Not tried in a browser yet.**

**Totals:** 895 .NET tests, 91 web tests.

## Points worth noticing

| | Java / Spring | Here |
|---|---|---|
| `PUT` and `DELETE` | `@PutMapping`, `@DeleteMapping`, `ResponseEntity.noContent()` | `MapPut`, `MapDelete`; `Results<NoContent, NotFound>` lists every response in the signature, so OpenAPI knows them |
| An entity changed, then not saved | Hibernate flushes it at commit | EF Core saves only on `SaveChangesAsync`; an edit refused after `factory.Name` was set is never saved, and the context is scoped to the request |
| A component event | `@Output() deleteFactory = new EventEmitter()` | `output<FactoryTrace>()`: the signal-era counterpart, bound with `(deleteFactory)="…"` |

## Choices worth a second look

1. **Deleting asks once, in a dialog.** There's no undo; the factory can be defined again in 3D.
2. **The factory page has no Edit or Delete** (you chose the legend). Its *3D* button opens the area with the factory in the legend.

## Next

Commit steps 45 and 46 (they share the 3D page's files), then you choose.
