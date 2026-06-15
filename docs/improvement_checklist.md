# Overlay Improvement Checklist

Updated: 2026-06-14

Legend:
- `[x]` Implemented and previously validated
- `[ ]` Not completed
- **TEST** Implemented, but still needs controlled in-game validation

## Active Validation Queue

Run these objectives in order. These are the remaining validation tasks, not new feature work.

1. [ ] **RETEST: Abyss discovery and cleanup**
   Confirm `AbyssFinalNodeBase` appears after chunk streaming, remains cached after moving away,
   and disappears permanently after completion.
2. [ ] **RETEST: Ritual completion cleanup**
   Complete one Ritual, move far enough to unload its chunk, return, and confirm only unfinished
   Ritual markers remain.
3. [ ] **RETEST: Strongbox variants**
   Confirm sleeping preload, rendering, activation, and cleanup across at least three Strongbox
   variants. Spawned Strongbox monsters must not be promoted to encounter markers.
4. [ ] **TEST: Encounter clutter controls**
   With default settings, confirm encounter-spawned monsters are absent from both maps while the
   stable encounter anchor remains visible.
5. [ ] **TEST: Encounter label controls**
   Confirm labels default off, can be enabled independently, and do not appear beside every
   encounter-spawned monster.
6. [ ] **TEST: Dead mechanic cleanup**
   Complete a dense encounter and confirm dead monsters, temporary effects, and consumed anchors
   leave no stale markers.
7. [ ] **TEST: Essence identity and lifecycle**
   Identify the frozen Essence rare before activation, then capture authoritative activation and
   completion state changes. Do not use generic monster-mod helper entities as the anchor.
8. [ ] **TEST: Remaining mechanic lifecycles**
   Validate preload, activation, completion, and cleanup for Expedition, Delirium, Ultimatum, and
   other current PoE2 endgame mechanics encountered during testing.
9. [ ] **TEST: Long-session alignment**
   Run several maps with repeated arrow-key panning, inventory/stash transitions, and large-map
   close/reopen cycles without accumulating drift.
10. [ ] **TEST: Resolution coverage**
    Validate large-map projection at 1080p, 1440p, ultrawide, windowed mode, and after a live
    resolution change.
11. [ ] **RETEST: Atlas projection**
    Revalidate Atlas rings and labels at minimum, medium, and maximum Atlas zoom.

### Validation Already Confirmed

- [x] Ritual anchors preload and display the `Ritual` label.
- [x] Breach streams as `BrequelInitiator` and completed Breach markers remain removed.
- [x] Rogue Exiles preload sleeping and wake correctly without dormant Life being treated as dead.
- [x] Shrines preload sleeping and wake when approached.
- [x] Large-map arrow-key panning, reopen alignment, and inventory-panel compensation work.

## Immediate Validation

- [x] Large-map overlay follows arrow-key panning without accumulating calibration drift.
- [x] Closing and reopening the large map restores live automatic alignment.
- [x] Inventory and other UI panels do not displace the large-map overlay.
- [x] Ritual anchors appear immediately at map entry.
- [x] Ritual anchors use the mechanic label path and display `Ritual`.
- [ ] **RETEST** Abyss locations appear after chunk streaming through
      `Metadata/MiscellaneousObjects/Abyss/AbyssFinalNodeBase` and remain cached when the
      chunk unloads. Exact locations are not present in the known entity maps at area entry.
- [x] Breach locations appear after chunk streaming through `BrequelInitiator`; observed first
      as a sleeping entity at about 336 grid units. Exact locations are not present at area entry.
- [x] Rogue Exiles preload from `/Monsters/RogueExiles/`; confirmed
      `Metadata/Monsters/RogueExiles/Int/ExileSorceress2@80` sleeping at about 294 grid units.
      A second capture found `ExileRanger2@81` sleeping at 281.6 units, waking at 119.9,
      and retaining its full Life block until activation.
- [ ] **TEST** Frozen Essence encounters appear before entering activation range.
- [x] Breach markers disappear when the encounter entities are consumed and do not return.
- [ ] **RETEST** Completed Ritual markers disappear and do not return after moving away.
- [ ] **TEST** Dead mechanic monsters and temporary effects leave no stale radar markers.
- [ ] **TEST** Encounter-spawned monsters are hidden by default on both the large map and
      minimap while the stable encounter anchor remains visible.
- [ ] **TEST** Encounter labels are off by default and can be enabled independently of
      encounter monster icons.
- [x] Shrines have a confirmed sleeping representation before activation range
      (`Metadata/Shrines/Shrine` with MinimapIcon), observed sleeping at 293.4 grid units
      and waking at 183.7.
- [ ] **RETEST** Strongboxes have a confirmed sleeping representation
      (`Metadata/Chests/StrongBoxes/MartialStrongboxHigh`) and rendered successfully in one map;
      confirm behavior across additional Strongbox variants.

## Map And Minimap Alignment

- [x] Read the game's live large-map shift and zoom.
- [x] Read shift, default shift, and zoom as one coherent memory snapshot.
- [x] Make plain arrow keys pan the game map without modifying persistent overlay calibration.
- [x] Preserve manual calibration through `Ctrl+Arrow`, Page Up/Down, and Home.
- [x] Reset renderer tracking when the large map is reopened.
- [x] Add resolution-aware projection scaling.
- [ ] Confirm long-session alignment stability across several maps and UI transitions.
- [ ] Validate 1080p, 1440p, ultrawide, windowed, and resolution-change behavior.
- [ ] Add alignment diagnostics showing live shift, zoom, center, and manual correction.
- [ ] Investigate whether player-screen viewport correction is still necessary.

## Mechanic Discovery And Cleanup

- [x] Add awake/sleeping mechanic lifecycle research probe.
- [x] Recover and validate the primary StateMachine pointer/value layout at `+0x158/+0x160`.
- [x] Add StateMachine value signatures to mechanic lifecycle `NEW` and `STATE` events.
- [x] Validate Expedition lifecycle states `0 -> 1 -> 5 -> 6 -> 7`; state `7` is completed.
- [x] Validate Ritual lifecycle states `0 -> 1 -> 2 -> 3`; state `3` is completed.
- [x] Exclude Essence bone walls and Expedition crack/controller effects from mechanic clutter.
- [x] Capture authoritative Breach transitions: BrequelInitiator `0 -> 1 -> 2 -> 4`,
      where `2` is active and `4` is completed.
- [ ] **TEST** Capture authoritative Essence activation/completion StateMachine transitions.
- [x] Add retryable Life reads and explicit alive, dead, unknown, and not-applicable states.
- [x] Remove or fade dead mechanic monsters.
- [x] Cache stable sleeping mechanic anchors and retain discovered anchors across chunk unloads.
- [x] Deduplicate overlapping anchor/interactable markers.
- [x] Track completed mechanic positions and minimap completion state.
- [x] Add a dashboard toggle for preloaded mechanic locations.
- [x] Add a dashboard toggle and generic fallback renderer for mechanic labels.
- [x] Add independent dashboard toggles for encounter monsters and encounter labels, with
      quiet defaults and a one-time migration for existing settings.
- [x] Expose awake/sleeping source state in Live Entities and Inspector, including a
      sleeping-only validation filter.
- [x] Add preload identities for Abyss entrances and Rogue Exile monster families.
- [x] Exempt preloaded anchors from normal range and untargetable clutter filters.
- [ ] Replace the removed Essence heuristic with verified component/modifier detection. Generic
      sleeping untargetable monster POIs are not sufficient: they also match Breach, Abyss,
      Beyond, and Rogue Exile monsters.
- [x] Use BrequelInitiator state `4` as authoritative Breach completion, retaining targetability
      only as a compatibility fallback for older Breach object variants.
- [ ] Verify preload and completion behavior for Expedition, Delirium, Abyss, Ultimatum,
      Shrines, Strongboxes, and other confirmed PoE2 endgame mechanics.
- [x] Confirm Ritual anchors are generated at area load, while Breach and Abyss locations are
      streamed by proximity. The standard awake/sleeping entity maps contain no hidden global
      Breach or Abyss location collection at untouched map entry.
- [ ] Add per-mechanic preload, active, completed, and hidden display controls.
- [x] Remove Legion, Blight, Harvest, and Incursion from the PoE2 validation matrix; their
      legacy metadata may exist in packaged data, but they are not current PoE2 map mechanics.

## Atlas Assist

- [x] Use live Atlas UI node projection with resolution-aware scaling.
- [x] Resolve internal map codes to display names and package static lookup data.
- [x] Add searchable labels, pins, off-screen waypoint arrows, and rule aliases.
- [x] Add configurable semantic rings and Atlas loading status.
- [x] Move Atlas controls into a dedicated dashboard tab.
- [ ] Revalidate ring and label alignment at minimum, medium, and maximum Atlas zoom.
- [ ] Add map weight/value coloring equivalent to ExileMaps.
- [ ] Add meaningful content rings for every resolved Atlas mechanic.
- [ ] Add locked, unlocked, and completed connection-line styling.
- [ ] Add favorite stars, Atlas passive-point markers, and quest markers.
- [ ] Add shortest-route lines from explored nodes to pinned maps.
- [ ] Improve tooltip/text-source resolution for special and boss nodes.

## Dashboard And Inspector

- [x] Restore the reconstructed dashboard and full entity inspector.
- [x] Package `config/OtIdaOffsets.json` and inspector support in release builds.
- [x] Add floating save controls for long settings pages.
- [x] Split Radar Settings into inner Entities, Appearance, Map, Automation, and Performance tabs.
- [x] Add editable numeric Atlas settings and rule renaming.
- [ ] Add a compact diagnostics page for map alignment and mechanic-state decisions.
- [x] Add import/export and reset controls for the full active settings schema.
- [ ] Decide whether to retain the web dashboard or add an ExileCore-style in-game GUI.

## Rendering And Performance

- [x] Cache terrain and throttle expensive world/entity refresh work.
- [x] Gate expensive Atlas discovery and show visible loading progress.
- [x] Evict stale entity/component caches when addresses are recycled or disappear.
- [x] Remove avoidable per-frame/per-scan scratch allocations and add opt-in timing diagnostics.
- [x] Cap the software layered-window compositor at 60 Hz and add fast terrain sampling.
- [x] Move terrain and landmark discovery off the render thread to avoid zone-entry stalls.
- [ ] Profile frame time, memory reads, entity scanning, and rendering during dense encounters.
- [ ] Move sleeping-mechanic scanning to an incremental or indexed path if profiling requires it.
- [ ] Add optional marker clustering and label-overlap avoidance.
- [ ] Add a configurable performance mode for lower-end systems.

## Future Features

- [ ] Ritual reward price estimates using a maintained trade-price source.
- [ ] Runeshape/item price estimates inspired by RuneshapePriceChecker.
- [ ] Investigate poe.ninja availability, rate limits, caching, and PoE2 coverage before integration.
- [ ] Add richer waypoint/path guidance for mechanics and selected entities.
- [ ] Add an optional native Direct2D settings UI over the game.

## Compatibility And Release Quality

- [x] Package embedded map names, entity names, zone notes, landmarks, and inspector data.
- [x] Maintain a GitHub Actions release workflow and verified release ZIP contents.
- [ ] Add automated smoke tests for settings serialization, embedded resources, and release contents.
- [ ] Add post-patch offset/signature health reporting in one dashboard view.
- [ ] Revalidate stale byte-patch signatures after each game patch, especially enemy HP bars.
- [ ] Make `publish.ps1` wait for transient executable handles before creating the ZIP.
- [ ] Commit and release the current mechanic-cleanup, preload, and alignment work after validation.
