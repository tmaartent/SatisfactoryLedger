# ADR-0025: A factory is defined by its machines

- **Status:** Accepted
- **Date:** 2026-11-09

## Context
Until now a factory was an **output belt and input items** (BR-W2, step 11). Its machines were found by walking upstream from that belt up to the machines making an input item. That has two problems:

- **One output only.** A refinery making plastic and leaving heavy oil residue, or encoders leaving dark matter residue, have more than one output. They couldn't be described.
- **Hard to pick.** The player chose a belt id from a list of suggestions (BR-W12). In the 3D view of an area (ADR-0024) the owner can see and click the machines themselves.

The owner also found that *which logistics belongs to a factory* can't be decided from one end. A byproduct pipe network fed by the encoders of several factories, or a bus feeding several factories, belongs to none of them.

## Decision
- **The definition is the factory's machines** (decided 2026-11-09): selected in 3D (part 2b), stored per world. Everything else is derived per snapshot.
- **Logistics belongs to a factory per line** (BR-W3): logistics connected to each other form a line. A line belongs to the factory when every machine on it is the factory's. Providers, consumers and the containers in the line are its ends and don't count as another factory. A line touching another factory's machine connects them and belongs to none.
- **Outputs and inputs are handovers** (BR-W4): from a member to what isn't one, or into nothing after an own container at the end of a line. There can be several of each.
- **Stored as machine references:** id, class and position, as a jsonb list on `factories.machines`. A machine rebuilt between saves gets a new id, so it's matched by **class and position within a metre**. One without a match is reported as **missing** (BR-W11).
- **Old factories are converted:** at startup, each factory still stored by its output belt is traced once in its world's latest snapshot, and stored as the machines that trace finds. One that can't be traced there is left as it was, logged, and tried again at the next start. Until then it isn't traceable.
- **The old define request still works:** an output belt and input items are traced right away and stored as machines. `POST /factories` takes `machines` or `outputBelt`, not both. Since step 37 the web app no longer sends it: factories are chosen in 3D.
- **A generator can be one of a factory's machines** (BR-W2, decided 2026-11-11). In the domain it's still a `Consumer`, now with `IsGenerator`; `FactoryDefinition` treats a chosen one as a machine that takes its fuel and water and makes its waste. The API, the stored `machines` and the term *machines* stay as they are: generators are among them. The factory's findings add its generators and their power.
- **A machine belongs to one factory at most** (BR-W5, decided 2026-11-11). The rule spans the world's factories, so `DefineFactoryHandler` checks it beside the unique name (`ClaimedMachines`), not in `FactoryDefinition`. Overlapping factories stored before it are kept and shown.
- **A factory's output now** (BR-A14) is read from the stored analysis's flows into logistics (ADR-0017). It doesn't store a flow per connection.

## Alternatives considered
- **Keep the output belt, allow several:** still one end to trace from, and the shared-network problem stays.
- **Store the machines as ids only:** simpler, but a rebuilt machine would silently drop out of the factory.
- **Convert in an EF migration:** a migration can't run the trace, which needs the snapshot and the domain.
- **Store the flow per connection in the analysis:** exact output figures for a shared pipe network. Larger stored analyses and a new analysis version; not needed for the cases so far.

## Consequences
- `FactoryDefinition` is now its machines only; the name stays with the stored definition, since tracing never reads it. The old trace survives as `OutputBeltTrace`, only for converting and for the old dialog.
- The `factories` table gains a `machines` jsonb column; `output_belt` and `input_items` become nullable and stay filled only for factories not converted yet.
- An output into a line shared with other factories reports the line's whole flow as "now", their part included (`SnapshotAnalysis.Over`).
- The web app chooses a factory's machines in the 3D view of an area (step 37): a click adds or takes out one, a box dragged with shift or alt adds or takes out every machine inside it. The factory page with outputs and inputs follows in part 2b.
