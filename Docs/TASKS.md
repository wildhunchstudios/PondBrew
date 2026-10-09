# Prototype tasks

> Current checkpoint: R08.1 source implemented; Unity license blocks runtime/build validation. See AGENTS.md and `Evidence/r08-1-VALIDATION.md`. Earlier entries below are historical.

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

## Historical record — R06.5v3 lab equipment, 2026-10-05

Three requested props redesigned; source `26b111a`, delivery tag `r06-5-lab-equipment`. EditMode 1246 passed; Windows build errors/warnings 0. Exact package presentation: 271, research: 194, lab: 153, airfin: 83, comedy: 155: 856 assertions, no captured runtime errors, all exits 0. All 11 protected files unchanged. Current instructions and detailed continuation are in AGENTS.md; raw evidence uses Docs/Evidence/r06-5-labrefresh-*. Await specific feedback on these three designs; no R07 work.

## Historical record — R06.5v4 aquatic rooms and door, 2026-10-05

Both rooms redesigned and solid automatic lab door delivered. Source `f05a1dc`, tag `r06-5-aquatic-rooms`. EditMode 1246; package rooms: 65, presentation: 271, lab: 153, edited: 228, regression: 182, total 899 assertions; runtime errors/exits: 0; Windows build errors/warnings: 0. All 11 protected files unchanged. AGENTS.md is canonical; raw evidence Docs/Evidence/r06-5-rooms-*. Await room/door feedback, no R07.


## 2026-10-08 — R07.1/R07.2 historical handoff pointer

First cashier delivered from `44dfdc9`, tag `r07-first-cashier`. Full EditMode1342, Editor102, Windows307; build0 errors/0 warnings; all runtime routes passed/exit0. Night hire/schedule at physical counter;4 credits/night+day;2.2-second checkout; manual fallback. All11 protected hashes unchanged. Exact evidence/limitations/continuation are maintained in **../AGENTS.md**, the sole live guide. Play `../Builds/Windows-6000.3.21f1-r07-1-cashier/EvolutionShop.exe`. Human cashier feedback pending; R07.3 unstarted. Raw evidence: Evidence/r07-1-*.


## 2026-10-08 — R07.3 historical handoff pointer

Nori stocker delivered, source `a4c1184`, tag `r07-stocker`. Full EditMode1451; Editor131; final package stocker133/restart32/cashier102/rooms65 =332 assertions, runtime errors/exits0; build0 errors/0 warnings. All11 protected files unchanged. AGENTS.md is the sole current guide with scope, controls, evidence, limitations and next task. Play `../Builds/Windows-6000.3.21f1-r07-3-stocker-final/EvolutionShop.exe`; raw evidence `Evidence/r07-3-*`. Human staffing feedback pending; R07.4 technician remains unstarted.


## 2026-10-08 — R07.4 historical handoff pointer

Moss technician delivered, source `4a373b4`, tag `r07-lab-technician`. EditMode1594; Editor135; exact Windows459; build0 errors/0 warnings; runtime errors/exits0. All11 protected files unchanged. Finite known-recipe queue, persistent active batch, physical production/delivery and independent wages. AGENTS.md is the sole current guide; see Evidence/r07-4-PLAYTEST.md and Evidence/r07-4-VALIDATION.md for controls, actual evidence and limits. Human staffing playtest pending; next bounded task R07.5, R08 unstarted.

## 2026-10-08 — TUT-00–03 tutorial foundation checkpoint

Canonical plan/status: AGENTS.md TUT section; checkpoint `tut-state-save-foundation`. Ten-lesson state authority and envelope20 migration/runtime save carry implemented. Targeted168 and full1762 EditMode passed; existing technician Editor136 checks/errors0/exit0; protected11/11 hashes unchanged. No guided UI/live action wiring or new Windows build yet. Continue TUT-04 then TUT-05. Evidence: `Docs/Evidence/tut-03-foundation-VALIDATION.md`. This is a historical pointer, not a separate roadmap.


## 2026-10-08 — TUT-04–05 development checkpoint

TUT-04–05 complete: real action/lifecycle integration and native guided entry/objective/paused help. Source `28e06e4`, checkpoint tag **tut-guided-ui**. Final EditMode1783/1783; Editor tutorial124; exact final Windows tutorial124 + technician regression136; runtime errors0/exits0. Final Windows build0 errors/0 warnings. Protected11/11 files unchanged. **Next: TUT-06 live room/station guidance, then TUT-07 introductory customer pacing.** TUT-08–14 remain pending; this is an intermediate development playable, not the tester release. Native mouse/keyboard usability and fresh-player sessions remain unverified. R07.5 follows TUT; R08 unstarted.

AGENTS.md is the sole live roadmap. Evidence: `Docs/Evidence/tut-04-05-VALIDATION.md`; local intermediate playable: `Builds/Windows-6000.3.21f1-tut-04-05-final/EvolutionShop.exe`. Historical records above are preserved.


## 2026-10-08 — TUT-06 room guidance checkpoint

**TUT-06 complete:** live ON01–06 door/station guidance, one outlined marker and one local callout, moved/missing-target handling and visible full-siphon recovery controls. Final source `c16a44f`, checkpoint tag **tut-room-guidance**. Final full EditMode1802/1802; Editor guidance58 (before final layout-only correction); exact final Windows guidance58 + complete tutorial regression124; runtime errors0/exits0. Final Windows build0 errors/0 warnings;24 actual720p/1080p guidance screens captured and dimension-audited; representative layouts visually inspected. Protected11/11 files unchanged. **Next: TUT-07 introductory customer pacing.** TUT-08–14 remain pending; native input, tester packaging and novice acceptance are separate gates. R07.5 follows TUT; R08 unstarted.

AGENTS.md remains the sole live roadmap. Evidence: `Docs/Evidence/tut-06-VALIDATION.md`; playable `Builds/Windows-6000.3.21f1-tut-06-reviewed/EvolutionShop.exe`. Previous records are historical.


## 2026-10-08 — TUT-07 first-customer checkpoint

**TUT-07 complete:** production first-customer pacing admits one normal Casual with90 active seconds at checkout when reachable unreserved stock exists. Price refusal/retry, skip, reload and permanent normal-policy return after any successful sale are verified. Source `c8465f5`, tag **tut-first-customer**. Full EditMode1823/1823; Editor57 checks; exact Windows customer-policy58 + full tutorial124 + technician regression136; runtime errors0/exits0. Build0 errors/0 warnings;10 actual720p/1080p customer/UI captures reviewed; protected11/11 files unchanged. **Next: TUT-08 practice, report and handoff.** Native-input, tester packaging and novice acceptance remain separate pending gates; R07.5/R08 have not started.

AGENTS.md remains the sole live roadmap. Evidence: `Docs/Evidence/tut-07-VALIDATION.md`; playable `Builds/Windows-6000.3.21f1-tut-07-customers/EvolutionShop.exe`. Previous records are historical.


## 2026-10-08 — TUT-08 practice and handoff checkpoint

**TUT-08 complete:** requested practice directions, normal cross-day report/closing, optional live upgrade/journal help, and atomic tutorial completion are implemented. Explicit saved-sale receipt review recovers a replaced qualifying report. Source `4489178`, tag **tut-practice-handoff**. Full EditMode **1,851/1,851**; Editor handoff **108**; exact Windows handoff **108**, unassisted tutorial **124**, room-guidance regression **58**; runtime errors/exits 0. Build: 0 errors, 0 warnings. 26 actual 720p/1080p captures audited; changed views visually inspected. Protected files: 11/11 unchanged. **Next: TUT-09 recovery matrix and independent review.** TUT-10–14 final QA/native-input/tester packaging/novice gates remain pending. R07.5/R08 have not started.

AGENTS.md remains the sole live roadmap. Evidence: `Docs/Evidence/tut-08-VALIDATION.md`; player `Builds/Windows-6000.3.21f1-tut-08-handoff/EvolutionShop.exe`. Previous records are historical.


## 2026-10-09 — TUT-09 recovery audit checkpoint

**TUT-09 complete:** all twelve recovery-matrix rows audited, independent authority/save review completed, and reproduced gaps repaired. Source `79aa367`, tag **tut-recovery-reviewed**. Full EditMode **1,897/1,897**; Editor recovery **56** + conditioning **74**; exact Windows recovery **56**, conditioning **74**, unassisted tutorial **124**, three-role staff regression **136**; final runtime errors/exits 0. Build: 0 errors, 0 warnings. Twelve actual 720p/1080p recovery captures inspected; protected files 11/11 unchanged. Original failures retained. **Next: TUT-10 QA candidate and true fresh-process routes.** Native input, tester package and novice gates remain pending. R07.5/R08 have not started.

AGENTS.md remains the sole live roadmap. Evidence: `Docs/Evidence/tut-09-VALIDATION.md`; player `Builds/Windows-6000.3.21f1-tut-09-recovery/EvolutionShop.exe`. Earlier records are historical.


## 2026-10-09 — TUT-10 startup QA candidate

**TUT-10 complete:** separate Windows QA candidate and real startup continuation through all ten tutorial lessons. Source `2b0c058`, tag **tut-qa-candidate**. EditMode **1,913/1,913**; Editor tutorial **124**; exact Windows **21 separate processes / 555 checks**, capture retry **6 processes / 158 checks**, rooms **65**, three-role staff **136**; errors/exits 0. Build errors/warnings 0. Twelve actual 720p/1080p startup captures inspected; initial black 720p capture evidence retained and corrected through launch sizing on the same executable. Protected files 11/11 unchanged. **Next: TUT-11 native Windows input and presentation.** Tester packaging and novice gates remain pending; R07.5/R08 have not started.

AGENTS.md remains the sole live roadmap. Details: `Docs/Evidence/tut-10-VALIDATION.md`; player `Builds/Windows-6000.3.21f1-tut-10-qa/EvolutionShop.exe`. Earlier entries are historical.


## 2026-10-09 — TUT-11a native validation checkpoint

**TUT-11 partially verified; manual input gate remains open.** Source `92724f8`, checkpoint **tut-native-input-checkpoint**. Full EditMode **1,925/1,925**; exact Windows tutorial regression **124**, errors/exit0; separate build errors/warnings0. Native mouse Start/help/back/skip/scroll and Save-and-quit verified; both main QA players exited0. Actual window focus loss/return pauses safely. Twenty native screenshots inspected across720p/1080p. W/F1/Escape automation probes did not register; the supported drag held only~17ms, with no culture collected. E/Tab, sustained movement/collection, station menu clicks and customer overlay combinations still need real input evidence. Protected files11/11 unchanged. **Next: finish TUT-11 via the isolated manual launcher/checklist; do not start TUT-12 or later stages yet.**

Canonical continuation: AGENTS.md TUT.11. Evidence: `Evidence/tut-11-VALIDATION.md`; manual launch: root `Play-Tutorial-Input-Check.cmd`; input sheet: `Evidence/tut-11-PLAY.md`. No later tutorial/business milestone started.

## 2026-10-09 — TUT-11b vat status checkpoint

**TUT-11b presentation fix delivered; TUT-11 manual input gate remains open.** The focused vat card now shows live remaining time, ready output/destination or empty state, retaining E and unlocked Mist controls. Final build source `cb52e6a`, checkpoint **tut-vat-status-readable**. Full EditMode **1,925/1,925** on UI source `4186802`; final commit changes only a diagnostic capture name. Exact final Windows conditioning **86** and tutorial **124** checks passed, errors/exits 0; build errors/warnings 0. Eight actual vat captures inspected at 720p/1080p. Protected files 11/11 unchanged. The user explicitly answered **Not tested yet** on 2026-10-09. **Next: complete TUT-11 using the isolated launcher/checklist; TUT-12 and later stages remain pending.**

AGENTS.md remains the sole live roadmap. Evidence: `Evidence/tut-11b-VALIDATION.md`; manual launch: root `Play-Tutorial-Input-Check.cmd`; checklist: `Evidence/tut-11-PLAY.md`. The earlier native subset remains attributed to source92724f8. No tester packaging or later milestone started.

## 2026-10-09 — R07.5 worker completion checkpoint

**R07.5 delivered — worker completion humor and interruption safety.** Source `f65f82d`, tag **r07-worker-safety**. Pip, Nori and Moss can celebrate committed work with a named caption, tiny stationary dance and existing shoe squeak. New work, service feedback and interruptions take priority. EditMode **1925/1925**, Editor worker **109**, exact Windows **877 checks across six routes**, runtime errors/exits 0; build errors/warnings 0. Eight actual 720p/1080p worker captures inspected. Protected files 11/11 unchanged. Remaining TUT-11–14 work is **deferred by explicit user request**, not passed; preserve its implementation and evidence. **Next bounded R assignment: R08.1 automatic culture collection/dosing modules.** Human humor/audio and staffing pacing remain unverified.

Canonical plan remains AGENTS.md. Details: `Evidence/r07-5-VALIDATION.md`; playtest: `Evidence/r07-5-PLAYTEST.md`. TUT remainder deferred explicitly; R08.1 is next.


## 2026-10-09 — R08.1 implemented source; Unity validation blocked

Collector/doser one-batch helper, physical bench panel, shared resource/worker guards and envelope21 migration are implemented. Standalone compilation of all six assemblies passed;30/30 pure domain NUnit tests passed under Mono. These are not Unity test/build passes.43 new Unity save cases and the isolated automation runtime/capture route are authored and compile, but have not run. Independent runtime review found new-night wage ordering and reload-latch bypass; both fixed with scenario assertions prepared.

Both fresh exact-project Unity probes exited198 for no valid Editor license. No new R08.1 player or screenshots exist. Restore the existing Unity Hub license, then finish focused/full tests, runtime, separate build/regressions and actual720p/1080p UI review. Preserve the R07.5 playable; do not advance R08.2. Protected files11/11 unchanged; four existing players preserved. AGENTS.md is canonical; exact evidence, controls, failures and resume commands: `Evidence/r08-1-VALIDATION.md`. Tutorial remainder remains explicitly deferred.

Source checkpoint `2501b94`, tag **r08-1-source-license-blocked**. Resume validation from `Evidence/r08-1-VALIDATION.md`; no new playable claimed.


## 2026-10-09 — R08.1 continuation recheck

Fresh exact-project probe18604 still exits198: Unity access token/license unavailable. Independent domain/save/session source review completed without additional concrete defects. No gameplay edits, new tests/builds/captures or later milestone. Source matches prior standalone03;11 protected files and four existing players preserved. Evidence `Evidence/r08-1-resume-01-*`; AGENTS.md updated. Next: user signs in to Unity Hub and refreshes existing license, then resume prepared R08.1 Unity validation.
