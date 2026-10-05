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

## Historical update — R06.2 asset conventions (2026-10-04)

Canonical contract: `../AGENTS.md`. Source `1f6adaa`, tag **r06-2-conventions**. Asset/audio rules plus development-only geometry/PCM audit delivered. Relevant Unity tests66; Editor97; exact package presentation97/slap58 passed, errors0 and exits0. Windows build errors0/warnings0. All290 package files and11 protected files match. Exported five original PCM references; no listening approval or performance benchmark. Normal playable content unchanged from R06.1. Deliverable EvolutionShop-AssetConventions-Windows.zip in Codex outputs. Next bounded assignment R06.3; R06.5 tone gate remains open. See AGENTS.md for the live roadmap and full limitations.

## Historical update — R06.3 comedy kit (2026-10-05)

Canonical contract: `../AGENTS.md`. Source `874fe5f`, tag **r06-3-comedy**. Six bounded incidents, captions, Backspace skip, shared/per-kind cooldowns, original cues, service-priority voice limits and a paused preview are delivered; worker celebration remains preview-only. Full Unity EditMode1246/1246; exact package comedy155, slap59, presentation137 (351 assertions across three scenarios), zero runtime errors/exit0. Windows build errors0/warnings0. All290 package files and11 protected hashes match. Failures and corrections retained under Docs/Evidence/r06-3-* and summarized in AGENTS.md. Actual1280×720 preview/HUD screens reviewed; live fish/incubator framing, broader interruption sweep, human listening/tone and performance acceptance remain open. Playable EvolutionShop-ComedyKit-Windows.zip and guide in Codex outputs. Next bounded assignment R06.4; no R07 or bulk content yet.

## Historical update — R06.4 interruption validation (2026-10-05)

Canonical contract: `../AGENTS.md`. Source `3c61f09`, tag **r06-4-comedy-validated**. Reproduced/fixed stale preview ownership on disable, disabled-customer gag writes and belch visuals after collection. Editor gate251 and full Unity EditMode1246 passed; exact Windows package interruption251/comedy155/slap59/presentation137 (602 assertions), zero runtime errors and clean exits. Build errors0/warnings0. All290 package files and11 protected files match. Actual1280×720 and1920×1080 Windows captures reviewed; retained failures and detailed limits are in AGENTS.md and Docs/Evidence/r06-4-*. Deliverable EvolutionShop-ComedyValidated-Windows.zip in Codex outputs. Next R06.5 requires human listening/tone feedback; employee remains preview-only and no bulk content/staffing begins yet.

## Historical update — R06.5 review preparation (2026-10-05)

Current contract: `../AGENTS.md`. R06.5 is active, with human listening/visual-tone feedback pending. Prepared a self-contained review page with 15 exact original WAVs, four existing R06.4 native captures, an in-game route and feedback prompts. All19 embedded payloads match source hashes; WAV headers and section anchors checked. Browser review was blocked by the tool's local-file URL policy, so no browser playback/render or human listening approval is claimed. No Unity test/build rerun and no runtime changes; current playable remains source3c61f09 / delivery6432459 / r06-4-comedy-validated. Page and validation receipt are under Docs/Evidence/r06-5-* and the Codex outputs. Next: obtain current sample feedback, address concrete issues, record acceptance; R07 remains planned. Preserve prior playtest approvals without inventing newer audio/tone acceptance.

## Historical update — R06.5v1 cozy shop / quirky lab (2026-10-05)

Canonical contract: `../AGENTS.md`. User selected a cozy/colorful cartoon shop and quirky cartoon science lab; the old sample is not final art approval. Delivered room architecture, lighting, matching cabinet/machine finishes and compact cream/teal HUD. Source1a21062, tagr06-5-cozy-lab (initial roomsf37749e; planc445a6c). Integrated full EditMode1246 passed before final button-color correction; final build errors0/warnings0, exact package presentation139/comedy155/lab144/edited228/airfin81 (747 assertions), zero captured runtime errors and clean exits. All290 shipped files and11 protected hashes match; normal save and four existing players preserved. Actual1280x720/1920x1080 native images inspected, pale button text corrected. Playable EvolutionShop-CozyLab-v2-Windows.zip and8-image screenshot bundle are in Codex outputs. Raw logs/reports/candidates retained under Docs/Evidence/r06-5-art-*. Revised-look/audio acceptance remains open; first style pass, existing characters/tool/expansion-wing polish and some label/UI limits remain. No R07. See AGENTS.md and r06-5-art-validation.md for exact evidence limits and next action.


## 2026-10-05 — TK01 project tooling setup (historical record)

Canonical status remains AGENTS.md. Six custom project skills and the official Unity CLI skill were installed; all seven format validators passed. Blender5.0.1 with pinned mcp-for-blender2.1.6 completed actual MCP initialization, tool discovery, scratch-scene read and safe-mode rejection on loopback; task-owned session stopped. Context7 server4.1.1 returned real Input System docs, but exact1.19.0 coverage was not established; local package docs remain the versioned fallback. Figma15.0.0 installed/enabled, account/design probe pending. Project-scoped Codex MCP activation/trust and the explicit Unity Pipeline0.8.0-exp.1 package proposal await user action. Existing Unity6000.3.21f1 guarded scene inspection exited0; a nonfatal licensing refresh message was retained. No new full test suite, build, production asset import, paid request or external upload occurred. Game source, packages, scene, normal save and the current R06.5v1 playable are preserved. Detailed report/lock: Docs/tooling/. Evidence: Docs/Evidence/toolkit-2026-10-05/. Next game task remains R06.5 feedback, not R07.

## Historical record — R06.5v2 redesign, 2026-10-05

Core original Blender assets and native IMGUI redesign delivered at source `056ac01`, tag `r06-5-redesign`. Final EditMode: 1,246 passed; Windows build errors: 0/warnings: 0; exact package presentation: 248 / lab: 151 / edited: 228 / airfin: 81 / slap: 59 / comedy: 155 = 922 assertions, runtime errors: 0/exits: 0. All 11 protected files unchanged. Canonical scope, corrected failures, limitations and next playtest are in AGENTS.md; full raw evidence under Docs/Evidence/r06-5-redesign-*. Prior records remain historical. Human revised-look/audio feedback is next; no R07 work.
