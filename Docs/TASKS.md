# Prototype tasks

Updated: 2026-10-03. Status legend: `[x]` executed/verified as described; `[ ]` outstanding. Source implementation and runtime validation are separate tasks.

## Checkpoint 00 — workspace and runnable baseline
- [x] Inspect `C:\dev\DumbVideoGameStudio` and preserve the original handoff.
- [x] Confirm this is separate from Dungeon Project and PersonalAssistant.
- [x] Detect installed editors and Windows build support; see `UnityProjectContext.md`.
- [x] Inspect integration availability: initial Unity CLI status reported zero instances; no Unity MCP tools exposed.
- [x] Create `EvolutionShop` using the installed Unity CLI and URP blank template.
- [x] Verify editor 6000.3.15f1 (c1aa84e375f6), resolved packages, and exact project targeted by batch editor process and Play-mode result.
- [x] Initialize project-local Git; no remote publication or global safe-directory change.
- [ ] Save Checkpoint 00 commit after evidence/records are updated.
- [x] Create minimal `Shop.unity`, verify compilation (no C# errors), enter Play mode, and capture/read screenshot. Baseline editor warnings/errors are documented in `VALIDATION.md`.

## 01A — movement and culture harvesting
- [ ] Build compact room and fixed stations with readable prompts/signs.
- [ ] Implement comfortable first-person movement and pause/cursor behavior.
- [ ] Implement two distinguishable free growers, refill/capacity behavior, and siphon.
- [ ] Verify hold collection, moving organisms, reservoir, sound, dosing, and capacity.
- [ ] Save checkpoint and record observed results.

## 01B — incubation and discovery
- [ ] Define stable IDs, centralized tuning, exact recipes, and inventory transactions.
- [ ] Implement recoverable unsupported recipes and exactly-once incubation output.
- [ ] Build three distinctive smooth creature placeholders and animations.
- [ ] Implement discovery journal and convenient known-recipe reproduction.
- [ ] Run recipe, consumption, invalid-input, and duplicate-protection checks.
- [ ] Save checkpoint and record observed results.

## 01C — display, sale, and upgrade
- [ ] Implement forgiving tank transfer with capacity reservation and bounded swimming.
- [ ] Implement selected specimen sale, currency, and stationary buyer.
- [ ] Implement purchasable four-to-eight capacity upgrade, visible chamber change, faster collection, and developer comparison.
- [ ] Verify full tank, repeated transactions, insufficient funds, actual specimen removal, and persistent discovery.
- [ ] Save checkpoint and record observed results.

## 01D — persistence and first playtest
- [ ] Implement one local save slot, stable specimen IDs, active incubation restore, and confirmed reset.
- [ ] Run save/load round-trip checks including during incubation and after a sale.
- [ ] Run fresh-start full loop, all three discoveries, physical upgrade, full tank, and pause/cursor checks in Play mode.
- [ ] Capture actual screenshot/recording evidence when tools allow.
- [ ] Attempt Windows build; separately launch and verify the produced player.
- [ ] Record actual controls, scene/build paths, test evidence, known issues, unverified checks, and exact resume action.
- [ ] Deliver Milestone 01 to user for first playtest and stop expanding scope.

## Deferred until user playtest
Milestone 02 automation/workers/humor and Milestone 03 presentation sample. No implementation work authorized yet.

