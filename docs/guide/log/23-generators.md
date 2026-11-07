# Step 23 · Generators burn fuel and leave waste

**Goal:** you noticed colliders "starved" with buffers full of plutonium waste. The cause: generators were consumers that took everything and made nothing, so a nuclear plant's waste never entered the analysis.

**Result:**
- A generator burning a known fuel now takes only its burn rate and its water, and leaves its waste.
- Generators get their own findings, a *Generators* tab, and status colours on the map.
- **Tests:** 653 .NET and 34 web tests, all green.

**Decisions you made (ADR-0021, BR-A8, BR-A15):**
- burn rates from the Docs file, at full power;
- generators get their own findings and stay consumers;
- the fuel is the one in the generator at save time;
- you added generators to the test world.

**Decisions I made, for you to overrule:** see "Choices worth a second look".

## What was built

```
Catalog     Generator (new entry): power, water per MJ, per fuel its energy, water and waste · imports count generators
            · GetGeneratorsAsync · migration AddGenerators · a fluid's energy per litre in the file, per m³ here
Save reader a generator's fuel (mCurrentFuelClass) and clock speed
Snapshot    version 7: each generator's fuel and clock, and the Catalog's generators it has
World       Generation (fuel, burns, water, waste per minute) on a consumer; in the flow a machine node
            · generator findings (BR-A15), generators out of the machine counts · analysis version 4
Web         a Generators tab in the findings · generators in status colours on the map
```

## How it works

- **The burn rate** is power ÷ energy, × 60, exact.
  - **Nuclear on uranium:** 2,500 MW ÷ 750,000 MJ gives 0.2 rods a minute. 50 waste per rod gives 10 a minute, and 1.6 L per MJ gives 240 m³ of water.
  - **Coal generator on compacted coal:** 75 ÷ 630 × 60 = 50/7 a minute, kept as a fraction (ADR-0004).
- **In the flow** a generator burning a known fuel is a machine node, so it obeys BR-A1: it runs at the lowest of fuel supplied, water supplied and waste taken.
- **In the findings** the analysis tells generators from machines by their role in the snapshot: a consumer with a fuel.

## What your test world shows

| Generator | Result |
|---|---|
| Nuclear plant | 100%: rods from a container, water by pipe, waste out on a belt |
| Coal generator | 100% on compacted coal |
| Fuel generator | **starved of turbofuel** |

The fuel generator's turbofuel comes from a packager that unpacks packaged turbofuel. Its only output is the turbofuel pipe: its empty canisters have nowhere to go (12 in it at save time). Once its output is full it stops, and the generator runs dry. That's a finding about the save, not the model.

## What it took (the surprises)

1. **A new save would have broken 27 tests.** The world kept running between the saves (6 ore on a belt instead of 7, fewer screws), and you built more. So the new save is a second fixture, `Generators.sav`, the same world later. The other tests keep `Development.sav`.
2. **The test catalog had to learn turbofuel** (a fluid) and the unpackaging recipe. Without them the packager knew no recipe, and the fuel generator starved for the wrong reason.
3. **Flow test fluids:** a test pipe only carries what the test declares a fluid (`.Fluid(Fuel)`).
4. **Material tabs in a test:** setting `selectedIndex` from outside didn't switch the tab; clicking the tab label did. The rows of other tabs can stay in the page, so the test counts rows in the active tab only.

## Tests (one behaviour each)

| Where | Tests |
|---|---|
| `GeneratorTests` (Catalog domain) | A nuclear plant burns a uranium rod every five minutes · leaves 10 waste a minute · a plutonium rod lasts twice as long and leaves 1 · 240 m³ water · a fuel generator burns 20 m³ · no supplemental, none needed · no waste, none left · can't burn what it has no entry for · produces power · a fuel has energy · a newer file replaces power and fuels |
| `SatisfactoryTranslatorTests` | The generators that burn fuel · a nuclear plant from the file · a fluid's energy is per litre · a coal generator burns 15 and needs 45 m³ |
| `CatalogImportTests`, `CatalogQueriesTests` | An import counts generators · a generator found by class name with power and fuels |
| `SaveFileReaderTests` | A generator burns the fuel in it (three) · 100% without a saved clock · a sink burns nothing · the nuclear plant's waste leaves on its belt · water arrives by pipe |
| `WorldFlowTests`, `SteadyStateTests` | A generator on a known fuel asks for fuel and water and makes waste · without waste makes nothing · with an unknown fuel takes everything · takes only what it burns · a nuclear plant whose waste can't leave stops |
| `GeneratorSnapshotTests` | Copies its generators · nuclear rates · compacted coal 50/7 exactly · turbofuel 7.5 m³ · water and turbofuel are fluids · a fuel the catalog doesn't know takes everything |
| `GeneratorFindingsTests` | Generators aren't machines · every generator with its fuel · worst first · the nuclear plant and coal generator at full power · the fuel generator starves behind the blocked unpacker. *The last three were written after seeing the results: they pin down what happens.* |
| Web: `findings.spec.ts` | Lists each generator with its fuel and why |

## Choices worth a second look

1. **Full power.** A partly loaded grid burns less, so its waste and fuel needs come out too high here. That's on the enhancements pile.
2. **A generator's fuel buffer** (50 rods in the nuclear plant) isn't shown as contents yet.
3. **The alien power augmenter and geothermal** stay plain consumers: they burn nothing the Docs file gives a rate for.
4. **Factory findings** never list generators (BR-W8).

## Then: uploaders, and a loop the solver can't start

The colliders were still starved after generators. Walking the chain back in your world:

1. **Uranium never reached the cell makers.** All 300 ore/min from the one uranium miner went through a smart splitter (*any* to an uploader, *overflow* to the factory) into a **dimensional depot uploader**, which took everything (BR-A8).
   - **You decided:** an uploader takes only what the line's other consumers leave. In steady state a depot is full.
2. **A solver bug behind it.** The smart splitter added an output's leftover-only room to its normal room, so an uploader (or a drone port's fuel) beat the overflow outputs. The solver's own rule says leftovers are served last; the smart splitter now does so too.
3. **A loop at zero.** Your nitric acid blenders get their water only from the non-fissile uranium blenders, which need the nitric acid. "Both at zero" is a valid steady state, and the solver, starting from zero, finds it.
   - **Tried:** solving from the top (every connection offering plenty before the first sweep). The loop then ran, and your world fully settled for the first time. But overall it did slightly worse (1,748 running against 1,781), and four inputs of the cell makers locked at the same 68%. That's a sign of the solver's start point, not of the world.
   - **You decided:** take it out, and look at the world first. You suspect the nuclear plants are starved on purpose, to keep nuclear waste from building up where it can't be sunk.
   - **Next for this:** the exact solver (linear programming) on the enhancements pile finds the largest steady state without a start point.

**Effect in your world** (simulated locally): 1,781 machines running instead of 1,572, 732 starved instead of 883, colliders running 8 instead of 4.

**More tests:**
- `SteadyStateTests`: an uploader behind a smart splitter gets only what the factory leaves · an uploader at the end of a line takes everything · a loop that gives back too little winds down.
- `WorldFlowTests`: an uploader takes only leftovers · a sink takes everything.
- `UploaderSnapshotTests`: an uploader marked by the save reader takes only leftovers · an AWESOME Sink takes everything.
- `GameClassesTests`: the depot uploader's class.

**How I found it:** a file-based C# app (`dotnet run flow.cs`) loaded your world's flow network from the internal endpoint, ran the solver, and followed one item from its miner through every belt to where it ended.

## Next

Your look in the UI, after a restart and a re-import of the Docs file (to fill the Catalog's generators) and of your big save (snapshot version 7). Then the Ficsonium colliders should no longer be starved, unless their plutonium waste really is short.
