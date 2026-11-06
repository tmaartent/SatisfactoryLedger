# Glossary: ubiquitous language

These terms are used **identically** in conversation, docs, code (class and method names), API routes and the UI. If the code needs a word that isn't here, add it here first.

## Game Catalog

| Term | Definition | Code |
|---|---|---|
| **Item** | Anything that can be on a belt or in a pipe: ore, ingots, parts, fluids. Has a form: *solid* or *fluid*. | `Item`, `ItemForm` |
| **Raw Resource** | An item extracted from the world (ores, limestone, coal, SAM, water, crude oil, nitrogen, …). Known from the catalog data. Everything else is *non-raw*. | `Item.IsRawResource` |
| **Recipe** | Game data: input amounts, output amounts and cycle time for one building type. May be an *alternate* recipe. | `Recipe` |
| **Building** | A production machine: Smelter, Constructor, Assembler, Refinery, … | `Building` |
| **Transport Tier** | A belt (Mk.1–Mk.6), lift (Mk.1–Mk.6) or pipe (Mk.1–Mk.2, and the *clean* variants without a flow indicator) with a maximum throughput. A lift carries as much as the belt of the same mark, so it has the *belt* form. A clean pipeline carries as much as the pipeline of the same mark. | `TransportTier` |
| **Resource Node** | A spot on the map where a resource can be extracted: its resource, its **purity** (impure, normal or pure) and its position. Game data, read from the map by a tool and imported into the Catalog (ADR-0015). Identified by the name the game and the save use (`BP_ResourceNode35`). A resource well's satellite nodes are resource nodes of their own kind. | `ResourceNode`, `ResourceNodeId`, `ResourceNodeKind` |
| **Purity** | How rich a resource node is. It multiplies an extractor's rate: impure ×0.5, normal ×1, pure ×2 (BR-W9). | `Purity` |
| **Extractor** | A building that takes a raw resource out of the world: Miner Mk.1–Mk.3, oil extractor, water extractor, resource well extractor. Its **extraction rate** is what it extracts per minute on a normal-purity node at 100% clock speed. In the World, a placed extractor is a provider. | `Extractor`, `Extractor.ExtractionRate` |
| **Catalog Entry** | An item, building, transport tier, recipe or extractor taken from the game's Docs file. Identified by the game's class name (`Desc_IronPlate_C`). | `ICatalogEntry<T>` |
| **Catalog Import** | One import of the Docs file: the game version, when it happened, and per kind of entry how many were added, updated or marked no longer in the game (ADR-0009). | `CatalogImport` |
| **No Longer In Game** | A catalog entry that a later import didn't contain. It stays in the catalog, so references to it stay valid, and it's shown as such. | `IsInGame = false`, `inGame` in the API |

## Shared kernel

| Term | Definition | Code |
|---|---|---|
| **Rate** | An amount of an item per minute, kept as an **exact fraction**. Fluids are in m³/min. | `Rate` |

## World (from the save file)

| Term | Definition | Code |
|---|---|---|
| **World** | Every snapshot with the same session name. Factory definitions belong to a world (BR-W11). | `StoredSnapshot.SessionName`, `StoredFactory.World` |
| **Candidate Output Belt** | A belt where items leave a group of machines: fed by machines through logistics, ending in a container, a consumer or nothing. Suggested to the player for BR-W2 (BR-W12). | `CandidateOutputBelts`, `OutputBeltCandidate` |
| **Steady State** | What flows where once everything has run long enough to settle (BR-A1 to A10). An approximation to 0.0001/min. | `SteadyState`, `FlowResult` |
| **Utilisation** | The share of 100% a machine runs at: the lowest of what its inputs supply and its outputs take (BR-A1). | `MachineState.Utilisation` |
| **Starved / Output Blocked** | Why a machine runs below 100%: too little of an input is offered, or an output can't leave (BR-A11). | `MachineLimit` |
| **Bottleneck** | A belt, lift or pipe at its capacity while more is offered upstream (BR-A12). The first saturated segment of a line. | `Bottleneck` |
| **Unused Supply** | What a provider could deliver on top of what it does (BR-A13). | `UnusedSupply` |
| **Supplier** | A provider, station or container nothing feeds that a factory's input lines reach. Never part of the factory (BR-W1); can supply several (BR-A14). | `FactoryMembers.Suppliers` |
| **Producer** | A machine of a factory whose output reaches the output belt through logistics only: it makes what the factory makes (BR-A14). | `FactoryMembers.Producers` |
| **Analysis Status** | Whether a snapshot's analysis is ready: *pending* until the background worker has computed it, *ready*, or *failed* with its reason (ADR-0017). | `AnalysisStatus`, `StoredAnalysis` |
| **Finding** | Something the analysis reports about a snapshot: a machine below 100% and why, a bottleneck, unused supply, or a source of unknown rate and how long its stock lasts (BR-A9, BR-A11 to A13). With where it stands in the game. | `SnapshotAnalysis`, `AnalysisResponse`, `GET /snapshots/{id}/analysis` |
| **Flow Part / Approximate** | A connected part of the world, simulated on its own. A part that hasn't settled within 2,000 sweeps is *approximate*: its numbers came close but some still moved (BR-A10). | `FlowPart`, `FlowResult.PartOf` |
| **Sort Rule** | What one output of a smart or programmable splitter sends: an item, *any*, *any undefined*, *overflow* or *none* (BR-A4). Outputs are counted from 0, as the splitter's settings number them. | `SortRule`, `SortRuleKind` |
| **Port** | Which output of a splitter a belt leaves from, or which input of a merger it enters by, counted from 0. | `Connection.FromPort`, `Connection.ToPort` |
| **Spare** | What a connection takes only when the line's other consumers have what they want: the fuel of a drone port or truck station (BR-A8). A sender serves spare last. | `SteadyState`: `_spare`, `TakeEverything` |
| **Station** | A train platform, fluid platform, drone port or truck station. Its ports decide its role: belts leaving its outputs make it unload (a provider without a known rate), belts into its inputs make it load (a consumer). A drone port can do both (BR-W1, BR-W8). | `Provider` without an offer |
| **Stock** | What a station or storage container holds right now, per item, as the save stores it. Decides whether a source of unknown rate is *confirmed* supplied (BR-A9). | `Provider.Stock`, `Logistics.Stock` |
| **Item Offer** | What a provider can deliver: an item and a rate per minute. For an extractor: its extraction rate × its node's purity × its clock speed (BR-W9). Unknown for a station, or a node the Catalog doesn't know. | `ItemOffer`, `Provider.Offer` |
| **Demand / Production** | What a machine asks and makes per minute when it runs at 100%: recipe rate × clock speed, and × production boost for what it makes (BR-A1). | `Machine.Demand`, `Machine.Production` |
| **Solids / Fluids** | What a line can carry: belts, lifts, splitters, mergers and containers carry solids; pipes, junctions and fluid buffers carry fluids (BR-A3). | `Carries` |
| **Assumed / Confirmed Supplied** | A source with an unknown rate supplies what's asked. Confirmed when its stock lasts 30 minutes at that rate (BR-A9). | `UnknownSupply` |
| **World Snapshot** | One imported save: when it was saved and imported, its world graph, and what the import couldn't place (unknown recipes, unsupported buildables). Every snapshot is kept (BR-W10). | (planned) |
| **World Graph** | The world as read from one save file: its buildables and how they're connected. Never changed after import. Later stored per import as a snapshot. | `WorldGraph` |
| **Buildable** | Anything built in the world that items move through: a machine, provider, consumer or piece of logistics. Identified by its id in the save, which stays the same from save to save. The game's own term. | `Buildable`, `BuildableId` |
| **Connection** | Items moving from one buildable's output to another buildable's input. | `Connection` |
| **Upstream / Downstream** | Upstream of a buildable is what feeds it; downstream is what it feeds. | `WorldGraph.Upstream`, `Downstream` |
| **Machine** | A building that runs a recipe: smelter, constructor, assembler, refinery, … Has a recipe, a clock speed and somersloops. | `Machine` |
| **Provider** | A building that brings items into the network from outside: miner, oil, water or resource well extractor, train, truck or drone station. Never part of a factory. Can supply several factories (BR-W1). | `Provider` |
| **Factory** | A group of machines **the player defines** by its input items and its output belt. Its machines are found by tracing upstream from the output belt (BR-W2, BR-W3). A machine can be shared by several factories. Replaces the earlier "blueprint placed in the world" meaning. | `FactoryDefinition` |
| **Output Belt** | The belt (or pipe) where a factory's merged output leaves it. Marked by the player. | `FactoryDefinition.OutputBelt` |
| **Factory Members** | The machines and logistics that belong to a factory, traced from its output belt. | `FactoryMembers` |
| **Factory Input** | An item the player declares as entering a factory. The input line starts at a splitter (BR-W4). | `FactoryDefinition.InputItems` |
| **Consumer** | A building that takes items out of the network: AWESOME Sink, dimensional depot uploader, power generator, a station that loads. Never part of a factory (BR-W8). | `Consumer` |
| **Undeclared Output** | An item other than the output item that leaves a factory, to a sink or elsewhere. A finding (BR-W7). | (planned) |
| **Logistics** | Everything that moves items between buildings without changing them: belts, lifts, pipes, splitters, mergers, storage containers, fluid buffers. A belt, lift or pipe has a **capacity** from its tier. | `Logistics`, `LogisticsKind`, `Logistics.Capacity` |

## Blueprint Design

> On hold since [ADR-0012](../adr/0012-diagnose-the-world-from-the-save-file.md). The per-machine terms (Clock Speed, Power Shard, Somersloop, Slot Count) still apply.

| Term | Definition | Code |
|---|---|---|
| **Blueprint** | A design made in the 6×6 (Mk.3) Blueprint Designer. Turns its inputs into one main product and optionally a byproduct. | `Blueprint` |
| **Production Step** | One recipe running on a number of identical machines (same clock speed, same somersloops). E.g. "4× Constructor making Iron Plate at 100%". | `ProductionStep` |
| **Clock Speed** | A machine's speed, 1%–250%. Scales inputs and outputs linearly. Under 100% = underclocked, over 100% = overclocked. | `ClockSpeed` |
| **Power Shard** | Needed to overclock: one per started 50% above 100%. | — (derived) |
| **Somersloop** | Production amplifier placed in a machine's slots. Raises output, not input. Full slots double the output. | `SomersloopCount` |
| **Slot Count** | How many somersloops a building can hold (catalog data). | `Building.SomersloopSlots` |
| **Main Product** | The single item the blueprint is built to produce. | `MainProduct` |
| **Byproduct** | A secondary output, **declared** by the designer, that leaves the blueprint. | `Byproduct` |
| **Undeclared Surplus** | An item with balance > 0 that's neither the main product nor a declared byproduct. A design error. | — |
| **Precision Surplus** | A tiny undeclared surplus that the game's clock precision (4 decimals) can't remove. Allowed (BR-B15). | — |
| **Balanced / Unbalanced** | Blueprint status: unbalanced while it has a deficit or an undeclared surplus. Only balanced blueprints can be placed. | `BlueprintStatus` |
| **Blueprint Input / Output** | An item rate crossing the blueprint boundary, per copy. *Derived* from the production steps, not typed in. | `BlueprintIO` |
| **Item Balance** | Net rate of one item inside a blueprint: produced + input − consumed − output. | `ItemBalance` |
| **Surplus / Exact / Deficit** | Balance > 0, = 0 or < 0. The UI shows surplus and exact as green, deficit as red. | `BalanceStatus` |

## Factory Network

> On hold since ADR-0012. **Factory** now has the meaning in *World* above. The supply line terms will be redefined on the real graph.

| Term | Definition | Code |
|---|---|---|
| **Factory** | A blueprint **placed** in the world, stacked vertically a number of times. | `Factory` |
| **Stack Height** | How many copies of the blueprint are chained vertically. Inputs and outputs scale linearly. | `StackHeight` |
| **Flagged Factory** | A factory whose blueprint became unbalanced or whose stack no longer fits after a blueprint edit. It keeps working with the last balanced inputs and outputs (BR-F6, BR-F7). | `Factory.Flags` |
| **Max Stack Height** | The highest stack whose total input and output rates still fit the max belt or pipe capacity (Mk.6 belt, Mk.2 pipe). | — (derived) |
| **Resource Supply** | A raw resource source with a rate (miners, train, drone). Where it physically comes from is out of scope for v1. | `ResourceSupply` |
| **Supply Line** | One belt (or pipe) carrying one item: a *source* at the start, then an ordered sequence of *taps* and *injections*. | `SupplyLine` |
| **Source** | What feeds a supply line: a resource supply, a factory output, or the overflow of another line. | `LineSource` |
| **Tap** | A consumer taking its demand of the line's item at a point along the line. The consumer is a factory or a dimensional depot uploader. | `Tap` |
| **Dimensional Depot Uploader** | Uploads items to the dimensional depot so they can be used as building material. Modelled as a tap with the upload rate as its demand. | `DepotTap` / consumer kind |
| **AWESOME Sink** | Destroys items for coupons. Required at the end of every belt line carrying a non-raw item (BR-S16). | `Sink` / line end |
| **Injection** | A second source merged into a line with a **priority merger**. Belts only. Fluid lines have no injections. | `Injection` |
| **Unused Supply** | Flow left on a fluid line after the last tap. It's wasted, not reserved for the future. | — |
| **Underclock Advice** | The clock speed the source of a fluid line should run at so supply matches demand (rounded up). | `UnderclockAdvice` |
| **Priority Merger** | Takes the leftover of the line as *primary* first, then tops up from the *secondary* (injected) source until the belt is full. | — (rule, see business rules) |
| **Section** | A run of consecutive taps between two injections (or the line start/end). Taps in a section have no priority: a shortage is shared proportionally. | `Section` |
| **Segment** | The stretch of belt between two consecutive points (taps or injections), with one flow value. | `Segment` |
| **Remaining** | What's still on the belt after a point. The remaining after the last point is available to **future factories**. | — |
| **Unallocated Leftover** | Raw resource flow left at the end of a belt line, waiting to be routed as primary input to the next factory (BR-S15). | — |
| **Overflow** | The part of an injected source that doesn't fit through the merger. It can be split off as the source of a new supply line. | `Overflow` |
| **Stranded Capacity** | Overflow that isn't routed anywhere: supply you have but can't use. | — |
| **Line Analysis** | The segment-by-segment result for a supply line: flow, remaining, deficits, overflow, stranded capacity. | `LineAnalysis` |
| **Consumer Link** | A record that factory B consumes (part of) factory A's main product or byproduct. | — (modelled as a tap on A's output line) |

## Words we deliberately do NOT use

| Avoid | Because | Use instead |
|---|---|---|
| "ratio" | Ratios were the spreadsheet's workaround. Rates follow from recipes. | Rate |
| "factory" for the design | Design and placed instance are different things. | Blueprint (design) / Factory (placed) |
| "manager", "helper", "util" | Not domain words. | A domain service named after what it does, e.g. `SupplyLineAnalyzer` |
