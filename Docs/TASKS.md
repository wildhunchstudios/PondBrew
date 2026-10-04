# Prototype tasks

Updated: 2026-10-03. **Milestone 01 two-room refinement implemented and verified; ready for first human playtest.** `[x]` means the stated work or execution completed, not creative approval. No later milestones started.

## 01R — agreed front store and back lab refinement

- [x] Implement user-confirmed front store (two display tanks/counter), back lab (Bloom/Flow growers, incubator, upgrade), and connecting doorway.
- [x] Update partition/colliders, station placement, spawn, signs, HUD guidance, and smoke viewpoints/routes.
- [x] Verify walking through the doorway in both directions, blocking by each side of the partition, and blocked E interaction through the wall.
- [x] Verify aiming at all seven stations and full production, transfer, sale, upgrade, save/load/reset loop in the revised spaces.
- [x] Pass two-room Editor run: 78 checks, no captured runtime errors, exit 0; hidden-editor cursor limitation retained.
- [x] Fix labels rendering through walls with a depth-tested world-text shader; position tool readout and move machine HUD to upper right.
- [x] Build exact final Windows player after readout/HUD fixes: Succeeded, zero errors/warnings, exit 0.
- [x] Launch final revised player: 79 checks, no captured runtime errors/limitations, exit 0, 49.577 scripted seconds.
- [x] Capture and review actual 1280x720 doorway, front store, back lab, counter, welcome, and tool/HUD views; creature/upgrade evidence retained.
- [x] Record current implementation, evidence paths, controls, and remaining human assessment limits in project records.
- [x] Save local checkpoint/tag `milestone-01-two-rooms` and deliver the verified ZIP, guide, and evidence.
- [ ] User first playtest: evaluate two-room navigation, production flow, collection/audio feel, creature appeal, slider behavior, and fresh-player timing.

Delivery: `EvolutionShop-TwoRooms-Windows.zip`, guide, evidence, and local checkpoint tag `milestone-01-two-rooms`. See `RESUME.md` for continuation. Completed checks in Checkpoint 00 and 01A-D below remain historical evidence for the prior single-room build/tag `milestone-01-playtest`. Historical 50 EditMode cases were not rerun for this refinement; final 79-check player evidence is new.

## Checkpoint 00 — workspace and runnable baseline

- [x] Inspect `C:\dev\DumbVideoGameStudio`; preserve handoff; confirm separate-project boundary.
- [x] Detect Unity installations/build modules; verify initial CLI has zero instances and no Unity MCP connection.
- [x] Create URP project at exact `EvolutionShop` child path; pin 6000.3.15f1 (c1aa84e375f6) and resolved packages.
- [x] Verify targeted editor identity, compile, enter minimal Play mode, capture/read floor/sphere screenshot.
- [x] Initialize local Git and save Checkpoint 00 commit `48ca887`; no remote/global safe-directory change.

## 01A — movement and culture harvesting (prior build)

- [x] Implement compact room, seven stations, signs/prompts, first-person controls, pause, sensitivity, and audio settings.
- [x] Implement distinct free growers, refill/capacity behavior, siphon reservoir cultures, harvest motion/sound, and dosing.
- [x] Verify synthetic WASD through real Input System/CharacterController; fix high-frame-rate minimum-move threshold.
- [x] Verify all seven aim targets, E interaction, timed collection, shared capacity, pause cursor release, and standalone cursor recapture.
- [x] Capture actual standalone HUD and correct footer wrapping, toast/title overlap, and discovery/menu overlap.
- [ ] User evaluates movement/collection feel and listens to audio; manually drag settings sliders.

## 01B — incubation and discovery (prior build)

- [x] Implement stable IDs, centralized tuning, exact recipes, recoverable invalid mixes, and once-only incubation.
- [x] Implement three smooth creature shapes/animations, discovery journal, and known-recipe shortcut.
- [x] Pass model recipe/consumption/invalid-input/duplicate/persistence coverage in 50 EditMode cases.
- [x] Discover all three in timed Play mode and standalone; visually inspect Pondlet/Bubblebelly and final Ribbonfin silhouette.
- [x] Lower incubator ring to expose creature and inspect final journal UI.
- [ ] User evaluates creature appeal and animation/interaction feel.

## 01C — display, sale, and upgrade (prior build)

- [x] Implement forgiving transfer, one resident per tank, bounded swimming, counter, selected sale, and currency.
- [x] Implement 24-credit upgrade: four-to-eight capacity, visibly larger chamber, 0.6-to-0.3-second collection, developer comparison.
- [x] Verify full-tank output retention, sale/upgrade once, insufficient funds, discovery persistence, and physical upgrade.
- [x] Sample Pondlet/Bubblebelly bounds and Ribbonfin tail bounds, each across 120 animation samples in standalone.

## 01D — persistence and first playtest (prior build)

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


## Historical update — R05.4 fourth culture (2026-10-04)

- [x] Verify the exact EvolutionShop workspace and Unity 6000.3.21f1 connection by guarded Editor launch; preserve 11 protected hashes.
- [x] Implement placed-vat Mist research/collection and one Airfin species, Driftwing, without a new machine.
- [x] Extend strict envelope16/game6 persistence and test frozen v15 migration/immutable archive.
- [x] Pass 1,195 EditMode tests, Editor Airfin 81, final packaged Airfin 81/restart12/conditioning200, build with 0 errors/warnings.
- [x] Save source and final delivery checkpoints; see the current roadmap and next R05.5 assignment in `../AGENTS.md`.
- [ ] Human assessment: Mist research value, Driftwing appeal, four-culture readability, shop/lab pacing and audio.

## Historical update — R05.5 lab validation (2026-10-04)

Current contract: `../AGENTS.md`. Source `4ceeb1d`, tag `r05-lab-validated`. R05.5 technical gate is complete: full EditMode 1,209/1,209; Editor and exact packaged lab flow 174 each; fresh-process restarts staged 25, working 23, canceled 24, partial 24, ready 25, recovered 21; packaged Airfin 81; all zero runtime errors and exits 0. Windows build succeeded with zero errors/warnings. All 290 shipped files match; all 11 protected baseline files match. Added 14 tests and a development-only harness; production rules/schema are unchanged. Initial diagnostic/test-pose and driver failures are retained and explained in AGENTS.md. Final playable: `EvolutionShop-LabValidated-Windows.zip` in Codex task outputs. Human lab tuning, audio and natural affordability remain pending. Next bounded assignment: R06.1 presentation sample; no bulk assets, staffing or automation yet.


## Historical update — R06.1 presentation sample (2026-10-04)

Canonical contract: `../AGENTS.md`. Final source `ee532eb`, tag **r06-1-sample**. Five in-game preview views and the original store/lab/Pondlet/casual/employee sample are delivered. Employee is preview-only. Full Unity suite1209 passed; exact final package presentation59, types108, slap58, lab144 passed with zero runtime errors/exits0; Windows build zero errors/warnings. Actual 1280×720 screens reviewed and the preview price-label overlap corrected. All290 package files and11 protected baseline files match. User-facing final archive: EvolutionShop-PresentationSample-v3-Windows.zip in Codex outputs. Escape → View presentation sample; normal controls unchanged. Human visual/audio tone acceptance, audio listening and low-spec performance remain open. Next bounded assignment R06.2; see AGENTS.md for evidence/limitations, not the historical task list above.
