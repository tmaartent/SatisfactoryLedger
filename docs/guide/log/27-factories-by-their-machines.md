# Step 27 · A factory is its machines

**Goal:** part 2a of the 3D factory workflow. A factory is now the machines you select, not an output belt and input items. Its logistics, outputs and inputs are derived per snapshot by the symmetric rule, and existing factories are converted. Part 2b adds selecting machines in 3D and the factory page.

## What you decided

- **The definition is its machines** (BR-W2). A rebuilt machine is matched by **class and position within a metre**; one with no match is **missing** (BR-W11).
- **Logistics belongs to a factory only when everything it connects to does** (BR-W3). Ends (providers, sinks, uploaders, generators, stations, the containers in the line) don't count as another factory. A shared bus or byproduct network, or a belt straight from one factory into another, belongs to none.
- **Several outputs and inputs** (BR-W4).
- **Convert existing factories:** traced once in their world's latest snapshot; one that can't be traced is kept and reported.
- **Two parts:** this is 2a (domain, storage, API); 2b is the 3D selection and the factory page.

## Two refinements to look at

Both came up while implementing the rule. They're in the rules now; overrule them if they're not what you meant.

1. **"Upstream and downstream through logistics" is decided per line.** Logistics connected to each other, in any direction, form one line. In the bus test (smelter → bus → splitter → your constructor, and → another factory's rod maker), the branch to your constructor is part of the bus, so it isn't yours either. The constructor's input is that branch. Read strictly in the flow direction, the branch would have been yours: its only upstream is your own smelter.
2. **An input's items are what its machines ask for and don't get *from inside the factory*.** It used to say "and the factory doesn't make itself". In that same bus case your smelter makes ingots and your constructor takes them back off the bus. The old wording gave that input no items; now it carries ingots. An assembler's screws from your own screw maker are still left out.

## How it works

```
Domain        FactoryDefinition(machines).TraceMembers(world)
                1. lines: each group of connected logistics around its machines; ours when every machine on it is ours
                2. outputs: member → non-member, or an own line that ends (a container at its end)
                   inputs:  non-member → member, or an own line that starts (a container nothing feeds)
                3. items, suppliers (upstream of inputs through logistics), producers (machines reaching an output)
              OutputBeltTrace(belt, inputItems).Machines(world)   the old walk, kept for converting
Application   StoredFactory.Machines (id, class, x, y, z) → jsonb
              MachineMatch.In(snapshot, machines): same id, else nearest same-class machine within 100 cm
              StoredFactory.ConvertIn(latest)       old definition → machines
              FactoryConversion (startup)           converts what's left; logs what it can't
API           POST /factories { machines } or { outputBelt, inputItems }, not both
              GET /snapshots/{id}/factories: outputs, inputs, missing
```

**A factory's output now** (BR-A14) is summed over its outputs. The stored analysis keeps only what flows *into* each belt, pipe and container in a line, so `SnapshotAnalysis.Over` reads each output's flow as follows:
- an own container at the end of a line: the flow into the belts that feed it, since it's a sink in the flow network and has no entry of its own;
- a belt with one way out: the flow into that belt;
- otherwise: the flow into the logistics across.

## Files

| Where | What |
|---|---|
| `World.Domain/FactoryDefinition.cs` | The new definition: its machines, and the rule (lines, handovers, items, suppliers, producers). Its name stays in `StoredFactory`: nothing in tracing reads it, so the domain no longer has it |
| `World.Domain/FactoryConnection.cs`, `FactoryMembers.cs` | An output or input; members with `Outputs` and `Inputs` |
| `World.Domain/OutputBeltTrace.cs` | The old trace, machines only |
| `World/Factories/StoredMachine.cs`, `MachineMatch.cs`, `StoredFactory.cs` | Machines as stored, matching, converting |
| `World/Factories/FactoryConversion.cs`, `WorldModule.cs` | Converting at startup |
| `World/Factories/DefineFactory*.cs`, `FactoryEndpoints.cs`, `FactoryResponse.cs` | Define by machines or the old way; `World.NotMachines` |
| `World/Factories/FactoryTrace.cs`, `FactoryTraces.cs`, `ConnectionSummary.cs` | Trace with outputs, inputs and missing machines |
| `World/Analysis/SnapshotAnalysis.cs` | Output over all outputs |
| `World/Persistence/Migrations/…_StoreFactoryMachines.cs` | `machines` jsonb (default `[]`); the old columns nullable |
| `web/src/app/world/define-factory-dialog.ts` | `machines: null` in the old dialog's request |
| `docs/adr/0025-factories-defined-by-their-machines.md` | The decision |

## Points worth noticing

| .NET / C# | Java / Spring | Here |
|---|---|---|
| A `params` overload swallowed old calls: `(world, name, belt, item)` compiled as two machine ids | Java varargs: the same trap | `WorldClient`. In the primer. |
| EF Core binds a positional record's constructor in a complex collection | Hibernate wants a no-arg constructor | `StoredMachine`. In the primer, with `"{}"` again |
| A private `sealed class Trace` nested in `FactoryDefinition`, with a primary constructor | A private static nested class | Keeps the per-world state out of the definition |
| `BackgroundService` for a one-off job at startup, catching its own failures | `ApplicationRunner` | `FactoryConversion`, after `AnalysisRequeue`'s pattern |
| CA1859 asked for `NotMachines?` instead of `DefineFactoryOutcome?` on a private method | — | `DefineFactoryHandler.WithMachines` |

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `FactoryDefinitionTests` (new) | Is the machines selected · belts between them are its · its line to its own container is its, the container an output · a supply line from a miner feeding only it is its, the miner its supplier · a bus feeding two factories connects them, belongs to neither · a byproduct network fed by several belongs to none, each output ends where it joins · taking from a shared network is an input · a belt straight into another factory is neither's · product and byproduct outputs · an input carries what its machines don't get inside · an input bringing back what it also makes still carries it · an unfed container is an input and supplier · producers · traceable with any machine · not without · at least one machine |
| `OutputBeltTraceTests` (was `FactoryMembersTests`) | The old trace's machine tests, kept for converting |
| `TestWorldFactoryTests` | The test world: owns its supply line from the miner · hands plates to the sink and to the container · the sink isn't its · the old definition converts to its 3 machines |
| `FactoryTracesTests` | Machines with class and recipe · outputs and inputs · rebuilt within a metre still its · further away missing · as another class missing · none present: not traceable · converting, with positions · not convertible stays · not converted: not traceable |
| `FactoryAnalysisTests` | Output now over all outputs, 15 of 15 plates · no analysis without its machines |
| `WorldFactoryApiTests` | Define by machines · by output belt, stored as machines · converting old factories, one kept · outputs and inputs · not machines: 422 · machines or belt, not both: 400 · and the earlier cases |

Red first: the domain tests failed to compile until `FactoryDefinition(machines)`, `FactoryConnection` and `TestWorld.PipeNetwork` existed, then passed on the first run. Two application tests failed after that: the handover to the sink was the belt into it, not the smart splitter, and "now" read 0 because an end container has no stored flow. **Totals:** 751 .NET tests, 57 web tests.

## Choices worth a second look

1. **The two refinements above** (per line; items not got from inside).
2. **Output "now" on a shared line** counts the other factories' part too: exact only with flows stored per connection (ADR-0025, alternatives).
3. **An output's items** are everything its producers make, so a refinery's product and byproduct outputs each list both items. Which one goes where needs the flow, which the findings have.
4. **Unconverted factories** are only logged, and show as not traceable. The web app doesn't say why yet (2b).
5. **Missing machines** are reported, but the trace runs with the ones present. A factory with half its machines missing still gets outputs and findings.

## Next

Part 2b: select machines in the 3D view (click, or a rectangle on screen), define the factory from them, and a factory page with its outputs, inputs and missing machines. The old dialog and the output belt suggestions (BR-W12) then go.
