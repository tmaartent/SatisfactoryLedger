# Step 59 · A station only for what it delivers

**Goal:** on your world, the Crystal Computer explanation listed *Unknown supply · AI Limiter · Sent by 0/min · Freight Platform ×4*, but no AI Limiters come by train (asked 2026-11-22). Branch `feat/mcp-server`, uncommitted.

## Why it happened

The upstream trace (BR-A20) walked every line into the Insulated Crystal Oscillator manufacturers and took **any** station it met as a source of **any** item: a station could unload anything, and the save doesn't say what. The platforms unload something else onto a line that also carries AI Limiters, so they became an AI Limiter start. The *0/min* came from `FlowReadings.OutOf`, which knew no rate for a station.

The analysis knew better all along. For every source whose supply is unknown, the flow solver records what it **delivered** per item (`UnknownSupply.Delivered`, BR-A9). The trace only got the ids.

## What you decided

A station counts as a start only for the items the analysis has it deliver. Its row shows that rate, not 0/min.

## How it's built

- **`FlowReadings`:** `unknownSupply` is now per source *what it delivered per item and minute*, not a set of ids. `Delivered(id, item)` reads it, and `OutOf` gives it for a provider without an offer.
- **`ShortageTrace.UpstreamLine.Walk`:** a provider is a source of the item when it offers it, when a non-station holds it, or when the analysis has it deliver some. `provider.IsStation` alone no longer counts.
- **`SnapshotTraces.Readings`:** passes `supply.Delivered` through.
- **BR-A20:** says so.

## Tests

- `A_station_whose_supply_the_save_does_not_say_is_unknown_supply` now also checks the rate (15/min).
- New: `A_station_that_delivered_only_other_items_onto_a_shared_line_is_not_where_it_starts`. A platform delivers ore onto the merged belt that brings the smelter's ingots, and the one start is *too few producers* at the smelter.

## C# / .NET notes

- **`Assert.Equal` on a tuple holding arrays** compares the arrays by reference (`ValueTuple.Equals` uses each element's `Equals`), so two equal arrays fail. Assert them separately and xUnit compares the collections item by item. Java's `assertEquals(List.of(a, arr), …)` has the same trap with arrays.
