# Prototype tasks

Updated: 2026-10-03. **Milestone 01 is ready for the first human playtest.** `[x]` means the stated implementation or execution completed, not creative approval. No known critical progression blocker was found. No later milestones started.

## Checkpoint 00 — workspace and runnable baseline
- [x] Inspect `C:\dev\DumbVideoGameStudio`; preserve handoff; confirm separate-project boundary.
- [x] Detect Unity installations/build modules; verify initial CLI has zero instances and no Unity MCP connection.
- [x] Create URP project at exact `EvolutionShop` child path; pin 6000.3.15f1 (c1aa84e375f6) and resolved packages.
- [x] Verify targeted editor identity, compile, enter minimal Play mode, capture/read floor/sphere screenshot.
- [x] Initialize local Git and save Checkpoint 00 commit `48ca887`; no remote/global safe-directory change.

## 01A — movement and culture harvesting
- [x] Implement compact room, seven stations, signs/prompts, first-person controls, pause, sensitivity, and audio settings.
- [x] Implement distinct free growers, refill/capacity behavior, siphon reservoir cultures, harvest motion/sound, and dosing.
- [x] Verify synthetic WASD through real Input System/CharacterController; fix high-frame-rate minimum-move threshold.
- [x] Verify all seven aim targets, E interaction, timed collection, shared capacity, pause cursor release, and standalone cursor recapture.
- [x] Capture actual standalone HUD and correct footer wrapping, toast/title overlap, and discovery/menu overlap.
- [ ] User evaluates movement/collection feel and listens to audio; manually drag settings sliders.

## 01B — incubation and discovery
- [x] Implement stable IDs, centralized tuning, exact recipes, recoverable invalid mixes, and once-only incubation.
- [x] Implement three smooth creature shapes/animations, discovery journal, and known-recipe shortcut.
- [x] Pass model recipe/consumption/invalid-input/duplicate/persistence coverage in 50 EditMode cases.
- [x] Discover all three in timed Play mode and standalone; visually inspect Pondlet/Bubblebelly and final Ribbonfin silhouette.
- [x] Lower incubator ring to expose creature and inspect final journal UI.
- [ ] User evaluates creature appeal and animation/interaction feel.

## 01C — display, sale, and upgrade
- [x] Implement forgiving transfer, one resident per tank, bounded swimming, counter, selected sale, and currency.
- [x] Implement 24-credit upgrade: four-to-eight capacity, visibly larger chamber, 0.6-to-0.3-second collection, developer comparison.
- [x] Verify full-tank output retention, sale/upgrade once, insufficient funds, discovery persistence, and physical upgrade.
- [x] Sample Pondlet/Bubblebelly bounds and Ribbonfin tail bounds, each across 120 animation samples in standalone.

## 01D — persistence and first playtest
- [x] Implement one local slot, stable IDs, active incubation, sensitivity/volume, malformed-save preservation, and confirmed reset.
- [x] Pass model JSON and real file save/load checks during incubation and after sale; verify upgrade/repeat production survive reload.
- [x] Pass 70 timed Editor Play-mode checks with no captured runtime errors, exit 0.
- [x] Build Windows x64 development/Mono player: succeeded, zero errors, zero warnings, exit 0.
- [x] Launch exact final player after UI fixes: 73 checks, no captured runtime errors/limitations, exit 0.
- [x] Capture actual final HUD/screens and inspect welcome, room, incubator, Pondlet/Ribbonfin, journal, pause/settings, and reset confirmation.
- [x] Preserve baseline/failed attempts and final evidence; document commands, controls, scene/build paths, and limits.
- [x] Save final local milestone checkpoint/tag `milestone-01-playtest` and deliver Windows ZIP with evidence and playtest guide.
- [ ] User first playtest: assess feel/audio/readability and measure fresh-player timing hypotheses before any later milestone.

## Deferred until user playtest
Milestone 02 automation/workers/humor and Milestone 03 presentation sample. No implementation work has started; user playtest determines next changes.
