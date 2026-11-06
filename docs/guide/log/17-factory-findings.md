# Step 17 · Findings per factory

**Goal:** the findings of one factory, not the whole world: does it make what it should, which of its machines run below 100% and why, where its belts are too slow, and is its supply stretched thin?
**Result:** `GET /api/v1/world/snapshots/{id}/factories/{factoryId}/analysis`. Tests: 537, all green.
**Decisions you made (BR-A14):**
- a factory's findings include its **suppliers**: the providers and stations that feed it, even though they're never part of it;
- it says its **output now and at 100%**;
- it has **its own endpoint**.

**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
World.Domain       FactoryMembers.Suppliers   providers, stations and unfed containers its input lines reach
                   FactoryMembers.Producers   its machines whose output reaches the output belt through logistics only
World.Domain.Flow  FlowResult.Into(id)        what flows into a buildable, per item
World/Analysis     SnapshotAnalysis.ForFactory: simulates the whole world, keeps the factory's findings,
                   adds its output; AnalyseFactory endpoint
```

## How it works

**The trace** (BR-W3) already walks upstream from the output belt and stops at providers. It now writes down where it stopped: those are the **suppliers**, together with any container it passed that nothing feeds. A second, short walk from the output belt, through logistics only, finds the **producers**.

**The whole world is simulated**, not just the factory. A factory depends on its supply, and its suppliers may feed other factories too. Then:
- **Machines:** only the factory's own; the summary counts only those.
- **Bottlenecks:** only on the factory's logistics.
- **Unused supply and sources of unknown rate:** only its suppliers.
- **Output:** per item, what flows onto the output belt now (`FlowResult.Into`), and what the producers make at 100% (`Machine.Production`, at their clock speeds and somersloops).
- **Settled:** read from the parts of the world the factory is in.

For the iron plate factory of the test world:

```json
{ "output": [ { "item": "Desc_IronPlate_C", "now": 15, "atFullSpeed": 15 } ],
  "summary": { "machines": 3, "running": 2, "starved": 0, "outputBlocked": 1, "noRecipe": 0 },
  "machines": [ smelter A, 41.67%, output blocked ],
  "unusedSupply": [ the miner, 22.5 of 60 ] }
```

Constructor C is clocked at 75%, so 15 plates a minute is all it makes at full speed, and it runs fully. These expectations were worked out before the code.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `FactoryMembersTests` | The provider feeding a factory is its supplier · a provider shared with another factory supplies both · an unfed container on an input line is a supplier · a machine of another factory isn't · the machines feeding the output belt are its producers |
| `SteadyStateTests` | What flows into a belt is what its inputs bring together |
| `FactoryAnalysisTests` (the test world) | Output now and at 100% · the summary counts only the factory's machines · only its own machines are findings · the miner feeding it, with its unused supply · a source feeding another factory isn't listed · an untraceable factory has no analysis |
| `WorldAnalysisApiTests` | The factory's output over the API · an unknown factory is 404 |

## .NET points worth noticing

| .NET | Java/Spring (TypeScript) | Note |
|---|---|---|
| `members.Machines.Contains` passed as a `Func<BuildableId, bool>` | `members.machines()::contains` | A method group converts to a delegate, like a Java method reference. |

## Choices worth a second look

1. **"At 100%" is per producer, at its own clock speed.** A factory that's underclocked on purpose shows what it makes at that clock, not at 250%.
2. **Not found (404)** covers four cases: no such snapshot, no such factory, a factory from another world, and an output belt not in this snapshot. The trace endpoint says `traceable: false` instead; a problem detail with a reason would tell them apart.
3. **Approximate** is read from the parts the factory's machines and logistics are in. A supplier in an unsettled part of its own isn't counted.
4. **Not tried on your big world:** none of its factories are defined yet. Define one with `POST /factories` (a candidate output belt comes from `GET /snapshots/{id}/output-belts`), and the endpoint will show it.

## Next

- Your call: storing the analysis with the snapshot (it takes seconds on a big world), comparing snapshots over time, or the web app.
