>2026-10-12 stronger-effects brief: FX-06R-00 specification complete; next FX-06R-01 particle vocabulary, then hatch/dose/growth. Current playable is unchanged; no new Unity tests/build or effects implementation. Revised review/native/listening and GPU acceptance remain open. Canonical plan: [AGENTS.md](../AGENTS.md), FX.10. Older records below are historical.

# Resume — Evolution Shop current checkpoint

> Latest FX-03 checkpoint: source 800cdc4, tag fx-lab-hero-validated. Canonical status/nextFX-04: ../AGENTS.md. Evidence: Evidence/fx-03-VALIDATION.md; prior records below are historical.

> Current assignment: R08.4 automation recovery proof is active. R08.3 desired-stock technical validation is complete and its human playtest remains open; R08.2 human acceptance was not recorded before the user directed work to proceed. See AGENTS.md, Docs/TASKS.md and Docs/Evidence/r08-3-VALIDATION.md.

Older dated checkpoints are historical. The R08.4 active assignment at the end of this file is the continuation point.

Workspace: `C:\dev\DumbVideoGameStudio`. Unity project: `C:\dev\DumbVideoGameStudio\EvolutionShop`. Verified again2026-10-10. Independent game; Dungeon Project and PersonalAssistant were not modified or imported.

## Current design and implementation

The user explicitly confirmed two physical rooms: **front store** with the two display tanks and sales counter; **back lab** with Bloom/Flow growers, incubator, and siphon upgrade bench. A collidable partition has an open3.2m×2.8m doorway. Player starts in the store. Signs, distinct floor accents, and HUD directions explain the trip between rooms. Depth-tested world labels prevent text showing through walls. The siphon has a visible physical numeric display; incubation status is above it in the upper-right HUD.

This paragraph records the Milestone01 gameplay baseline. R08.1 implementation and current candidate are documented below. R08.2's matching collector handoff passed technical validation on 2026-10-10. R08.3 adds persistent desired-stock configuration and status feedback; see the current continuation note at the end for its candidate and remaining playtest.

## Historical Milestone01 runtime and evidence

These results are from the 2026-10-03 two-room checkpoint and its Unity6000.3.15f1 pin. The current R08.1 candidate and pinned6000.3.21f1 results are appended below.

- Editor pin/installed executable: Unity6000.3.15f1(c1aa84e375f6); URP17.3.0, InputSystem1.19.0, TestFramework1.6.0.
- No editor bridge was assumed. Initial recheck showed no running Unity process and zero Pipeline instances. Targeted batch invocation and the exact-project guard verified EvolutionShop in editor logs. No MCP bridge installed.
- Scene: `EvolutionShop/Assets/EvolutionShop/Scenes/Shop.unity`. Regenerate via **Evolution Shop > Rebuild playable scene**, or `EvolutionShop.Editor.ProjectSetup.CreateScene`.
- Player: `C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe`, Windowsx64 development/Mono. Run without validation flags for normal play.
- Two-room Editor Play mode passed78checks, no runtime errors, exit0; the batch cursor limitation was explicit. Later physical tool-readout and HUD-placement changes were checked in the final player.
- Final Windows build succeeded with0errors/0warnings. Exact final player passed79checks, no captured runtime errors or limitations, exit0. Evidence: `Docs/Evidence/two-rooms-final-build-result.json`, `two-rooms-final-build-editor.log`, `two-rooms-final-player/smoke-result.json`, and `two-rooms-final-player.log`.
- New actual-input checks cover doorway traversal in both directions, partition collision on both sides, and blocked E interaction through the wall. All seven station targets and the full production/save/sale/upgrade scenario passed.
- Original50EditMode model cases are retained prior execution evidence; unchanged model code was not retested for this layout correction. Do not describe them as newly run.
- Actual final Windows captures document both rooms, welcome/HUD, counter, creatures, tool, and panels. Earlier visual failures and their successful gameplay reports remain preserved, not relabeled.

## Checkpoints and delivery

- `48ca887`: Checkpoint00 baseline.
- `54ea873`: Milestone01A-C loop.
- `milestone-01-playtest`: original single-room delivery.
- `milestone-01-two-rooms`: confirmed front-store/back-lab refinement. See git log/tag for commit.
- Revised deliverable: `EvolutionShop-TwoRooms-Windows.zip` in this Codex chat's outputs, with playtest guide and evidence. Previous ZIP preserved. No remote publishing.

## Next action and limits

User plays this revised build and evaluates the route between rooms, station clarity, movement/collection feel, creatures, sound, and upgrade impact. Human timing hypotheses (first creature2minutes, upgrade5minutes) are unverified. Audio has not been listened to; settings sliders have not been manually dragged. Procedural placeholder art/audio and provisional balance remain. No known technical blocker to first playtest. No FPS benchmark claim.

Read `AGENTS.md`, `TASKS.md`, `VALIDATION.md`, `UnityProjectContext.md` and original handoff before continuing. Reverify project/editor identity. Keep work inside this separate project; do not begin Milestone02/03 before first playtest and user direction.

Default save: `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\evolution-shop.json`. Do not delete it for testing. Use the confirmed New game UI for user resets; smoke flags select an evidence-directory save. Save format and gameplay IDs did not change. Simulation pauses while closed.


## Current continuation note — 2026-10-04 / R05.4

This file's earlier Milestone01 snapshot is historical; `../AGENTS.md` is the current working contract. Latest source checkpoint `af48f3b`, delivery tag `r05-4-airfin`. Unity 6000.3.21f1. Final Windows playable is `EvolutionShop-NewFamily-Windows-Final.zip` (extracted executable alongside it in the Codex task outputs). Mist research costs 40 at a placed vat after Spark/Lanternfin; hold left mouse there to collect. Driftwing Airfin is 1 Bloom + 1 Flow + 1 Mist, standard18. Envelope16/game6/layout7 preserves v15 on first replacement. Final EditMode 1,195/1,195; exact package Airfin81, fresh restart12, conditioning200, all zero runtime errors/exits0. Build succeeded with zero errors/warnings; all 290 package files match. All 11 protected files match pre-work hashes. Evidence prefixes and known limits are in `../AGENTS.md` R05.4 delivery section. Next bounded task: R05.5 validation gate, informed by the user's playtest; do not start staffing/automation or a bulk roster.

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


## 2026-10-10 — R08.1 Unity validation checkpoint


## 2026-10-10 — R08.1 license restored; Unity validation in progress

User reported license activated and authorized the missing checks. Fresh pinned Editor opened exact EvolutionShop project PID42424 after prior no-instance checks; no MCP bridge was used. Initial focused EditMode run retained actual failure evidence: 71/73, where the new save test fixture used an invalid33-character request ID (two round-trip cases). Corrected the fixture and asserted request creation; rerun **73/73 passed**.

First full-suite run exposed five reflection errors because production added a sixth optional parameter to `CaptureStaffSlot`, breaking existing reflection lookup/invocation. Replaced that change with the original five-parameter API forwarding to a distinctly named automation-aware capture method. Final full EditMode: **1,998/1,998 passed, zero failed/skipped**. Exact focused/full logs, XML and receipts under `Evidence/r08-1-editmode-resume-20261010-*`; both initial failing runs retained.

The actual Unity Editor `-evolutionAutomationSmoke` route passed **247 assertions**, Unity6000.3.21f1, zero captured runtime errors, elapsed39.54s. Report `Evidence/r08-1-editor-resume-20261010-automation01/smoke-result.json`; seven Editor camera exports are diagnostic only. Labelled fixture limitations include seeded300 credits/manual discovery, synthetic E/menu choices and one long-frame wage-priority boundary. No human/native-input, pricing-balance, or performance claim.

Still required: separate exact Windows development candidate, automation player smoke at1280x720 and1920x1080, inspect actual player captures, run Technician/Stocker/Staff/Tutorial regressions, check build warnings/artifact/package, audit normal save/protected files/four pre-existing players, then update R08.1 delivery status. Do not start R08.2.

## 2026-10-10 — R08.1 candidate validated

The Unity license was reported activated; the pinned Editor ran the exact EvolutionShop project in batch mode. There was no interactive Editor/MCP connection. Source checkpoint bf29ebe is built as a separate Windows development candidate at Builds/Windows-6000.3.21f1-r08-1-automation-candidate/EvolutionShop.exe. The Unity build summary says Succeeded, zero errors/warnings, exit0; executable SHA256 C8B0D73DC40E4F2CDDBF656CFB7257FCB8273DA22E44E12A8694CD8E275C6FB2.

Focused EditMode73/73 and full EditMode1,998/1,998 passed. Editor automation247 passed. Candidate automation passed247 checks at1280x720 and247 at1920x1080. Regressions passed: Technician136, Stocker133, Staff102 and Tutorial124. Each Windows route reported zero runtime errors and exited0. Exact logs, receipts, reports and player images are under Docs/Evidence/r08-1-*resume-20261010*; final audit JSON records hashes and process state. Eleven protected files, including the normal profile and backups, match baseline. Previous R07.5 executable/package and four original player processes remain.

Visual note: first locked-state 720p screen image is black; subsequent captures are visible. The culture panel is scrollable and its bench-return control is below the initial viewport. Human scroll/input usability remains untested. Unity player logs include MemoryLeaks cleanup telemetry without an exception or smoke runtime error; the build log contains a Licensing::Module access-token update message despite a successful build summary.

R08.1 passed its first human playtest on 2026-10-10; feedback was "playtested looks great for now." Its controls were WASD, mouse, E, Tab, F1 and Esc. R08.2 candidate: `Builds/Windows-6000.3.21f1-r08-2-automation-route/EvolutionShop.exe` plus `_Data`. At night, install and enable Collector36 and Doser24 at the lab bench, select discovered Pondlet, queue the same recipe first for Moss, ensure he is on duty, and request one batch. Check exact rack storage, Nori's configured display, then day service and Pip's agreed-price sale. The isolated Editor and Windows routes each passed145 checks; full EditMode passed1,999/1,999; build errors/warnings0. See `Docs/Evidence/r08-2-VALIDATION.md`. R08.2 human playtest is the next action; do not start R08.3 yet. The user profile was already written before these validation runs and was left untouched. TUT-11–14 remains explicitly deferred.

## Historical continuation — R08.3 desired-stock policy (2026-10-10)

The playable candidate is Builds/Windows-6000.3.21f1-r08-3-target-policy-final3/EvolutionShop.exe with its adjacent _Data folder. Controls: WASD move, mouse look, E interact, Tab journal, F1 help, Esc pause. At the research bench, use Culture preparation to select a known recipe and destination, set desired stock from 1 to 9, and enable Maintain. Pause/Resume retains helper work; Stop new automatic refills preserves work already queued or started. Counts include matching specimens in displays, racks, incubator and Moss's queued starts, so this is an in-pipeline target rather than a sale-ready count. Moss and Nori duties continue while the culture helper is paused.

Technical status: focused EditMode37/37; full EditMode2,018/2,018; Editor integration280 checks; final Windows player270 checks each at1280×720 and1920×1080; runtime errors/exits0. Build succeeded on Unity6000.3.21f1. Exact validation details and UI screenshots are in Docs/Evidence/r08-3-VALIDATION.md and Docs/Evidence/r08-3/. The normal user save was not used by smoke routes; each has a labelled evidence save.

Remaining: human playtest the R08.3 target controls. R08.4 is active under the user's continue instruction; the current implementation and Unity IPC blocker are recorded below.
## Historical continuation — R08.4 automation recovery proof (2026-10-10)

R08.4 is active. The isolated AutomationSmoke route now covers missing/off-duty Moss, higher-priority two-recipe queue reorder, canceling/re-adding both the unrelated and target-matching starts, exact pending-request identity, and unchanged cash/owned stock/cultures while the helper is held. Paused target/order/request state is also asserted across save/reload. A temporary labelled Bubblebelly production/sale creates a second known recipe for the queue-order probe. These assertions are source changes only; they have not passed a Unity runtime yet.

Fallback compile: dotnet build EvolutionShop.Runtime.csproj -nologo -t:Rebuild -p:RestoreIgnoreFailedSources=true -p:RunAnalyzers=false, exit 0, 0 C# errors, 102 analyzer/source-generator load warnings caused by unavailable Microsoft.CodeAnalysis assemblies in the standalone dotnet host. It does not substitute for Unity compilation or test execution.

The prior pinned Unity 6000.3.21f1 focused AutomationTests attempt failed to initialize LicenseClient-retai IPC, timed out, and ended before the TestRunner produced any XML. No full EditMode suite, updated Editor automation, fresh Windows candidate, or updated 1280x720 / 1920x1080 player route is available. The older R08.3 final3 candidate and its validation are preserved and are not evidence for these latest smoke assertions. Detailed status and exact evidence paths are in Docs/Evidence/r08-4-VALIDATION.md.

Post-compile preservation: the ordinary save SHA256 still matches the captured baseline; the four pre-existing player processes 18420, 29528, 39396 and 47552 remain. Unity stays pinned at 6000.3.21f1. No active Editor or callable Unity MCP bridge is available. Next: restore the existing License Client IPC, run focused/full EditMode, execute the updated Editor route, build a separate validation candidate and run automation plus technician/stocker recoveries at both player sizes. Do not advance R08.5 or deferred TUT-11–14 until this technical gate has evidence. R08.2 and R08.3 human playtest acceptance remain unrecorded.

## 2026-10-10 — R08.4 recheck after license activation

The user confirmed the Unity license is on. Verified Unity 6000.3.21f1 remains pinned, the local Unity entitlement file is present, and the Licensing Client responds and parsed an entitlement. A new focused AutomationTests launch still failed at Unity Editor IPC (LicenseClient-retai refused); the Editor briefly initialized licensing, lost the client, then remained stuck through the 180-second limit. It never started TestRunner and produced no result XML. We stopped only the Editor PID launched for this attempt. No build was attempted. This is an Editor/client-channel problem, not a claim that the Unity license is absent.

The ordinary save hash is unchanged at 08D2230570E091E9E97891AB5BB236509A19A646E8A153B95ACE5B4EA8DDF85E. Existing player PIDs 18420, 29528, 39396 and 47552 remain. The preserved R08.3 candidate hash is unchanged. No live Editor or callable Unity MCP connection is available. Attempt log/receipt: Docs/Evidence/r08-4/focused-license-on/; preservation/connection receipt: Docs/Evidence/r08-4/post-license-recheck-preservation.json. R08.4 remains the current task: finish focused/full tests, current-source Editor smoke, fresh Windows candidate and both resolution routes only after a working Editor IPC channel is available. Do not start R08.5 or tutorial tasks.

## 2026-10-11 — R08.4 validation resumed

Approved normal desktop execution connected immediately to the same Licensing Client. Focused37/37 and final full2,018/2,018 passed. Editor automation passed335 with zero runtime errors after correcting the new probe's assumption about saved queue planning while Moss is unavailable: one order is retained, and execution/resources wait. The original failed route is preserved. Worker Editor routes and the separate r08-4-recovery build are in progress; normal launch remains pinned at6000.3.21f1. Consult retry-20261011-* receipts under Docs/Evidence/r08-4 before continuing. Earlier IPC-blocked notes are historical; do not repeat activation or change license files.

## Current continuation — R08.4 complete (2026-10-11)

Source checkpoint8406a86; tag **r08-recovery-validated**. The new playable is C:\dev\DumbVideoGameStudio\Builds\Windows-6000.3.21f1-r08-4-recovery\EvolutionShop.exe with its adjacent _Data/runtime files. Controls: WASD move, mouse look, E interact, Tab journal, F1 help, Esc pause. At the lab bench, Culture preparation configures a known recipe/destination and target1–9; Maintain saves the target, Pause/Resume retains helper work, and Stop prevents new refills while existing queued/started work remains. Staff duties continue independently. The stock count includes pipeline work.

Actual evidence: focused37/37; final full EditMode2,018/2,018; Editor automation335/technician145/stocker133; exact Windows1,226 checks across six runs at1280x720 and1920x1080, runtime errors/exits0. Build Succeeded, errors0/warnings0. Two readable actual target-panel screenshots were inspected; black hidden-worker frames are excluded. Final audit10/10 protected hashes and four original players preserved; no validation process remained. Ordinary save unchanged. Details: Docs/Evidence/r08-4-VALIDATION.md and r08-4/retry-20261011-final-audit.json.

Earlier IPC-blocked entries are historical. The same Licensing Client connected immediately when the exact pinned Editor used scoped approved normal desktop access. Filesystem permission alone did not establish licensing IPC access. Preserve previous logs; use the saved Invoke-EvolutionValidation.ps1 with fresh evidence/revision names if new changes need validation. Do not reset the Hub account/license, update Unity or install another bridge. Pin stays6000.3.21f1; no active MCP connection is claimed.

Next bounded assignment: R08.5 consecutive automated shifts and one mixed manual/automatic shift. Establish recorded shift-to-shift state, correct wages and stock ownership, then measure waiting/work time and useful player decisions. Human target controls/automation usability and R08.2/R08.3 acceptance remain open. Tutorial remainder stays explicitly deferred. R08.5 and R09 have not started. The checkpoint includes reports/logs and selected reviewed screenshots; additional generated images remain local, with hashes in the evidence manifest. Keep them for later investigation.
## Current continuation — FX effects and animation plan (2026-10-11)

The user wants all presentation areas improved; priority is lab machines, siphon/player actions and creatures/tanks. Direction: cozy/bouncy with stronger comedy moments, cartoon hands/tool animation and matching original local sound. Their specific concern is that the lab and store tanks feel weak and uninteresting. Canonical specification and25 planned assignments: AGENTS.md FX.1–FX.9. FX-PLAN is complete; FX-00–24 are unimplemented/unvalidated.

Next start/continue request selects FX-00 baseline/state audit. Reverify exact project/Unity and any Blender session before runtime/asset work, capture source/reference states and measured baseline, then follow bounded tasks/dependencies. The first playable sample covers the lab/hands/Pondlet aquarium; obtain real motion/audio feedback before multiplying it. Follow project-local art, Blender, IMGUI and handoff contracts. Paid generation/uploads stay disabled; preserve rooms/opaque door, engine/packages, IDs/colliders, economy/production/jobs and normal save.

R08.4 source8406a86, docs98de291, tag r08-recovery-validated and playable remain intact. Its historical tests were not rerun for planning. R08.5, R08.2/R08.3 human acceptance and deferred TUT-11–14 remain open. No FX art/code, Unity tests/builds, paid jobs or new connections were performed. Planning evidence: Docs/Evidence/fx-plan-20261011.json. Local planning checkpoint/tag: fx-animation-spec.

## 2026-10-11 — FX-00 complete

FX-00 delivered on 2026-10-11; diagnostic source checkpoint b918fd4, tag fx-baseline-audited. Pinned Unity6000.3.21f1 verified the exact project through a guarded Editor probe, exit0. Separate diagnostic Windows candidate Builds/Windows-6000.3.21f1-fx00-baseline-5/EvolutionShop.exe built with0 errors/0 warnings. Fixture-only43 checks; final baseline720p 61 and1080p 61 checks, R08.4 automation regression 335, successful-route runtime errors/exits0. The extra legacy reset route failed at its stale paused-Welcome assumption and is recorded separately against the preserved build. Sixteen actual HUD captures and sixteen8s ordinary/busy night/day timing windows inspected/recorded. Frame interval p95 8.53–8.55 ms and GPU p95 2.93–4.07 ms across this recorded PC/setup;120FPS cap retained, Main Thread includes waits, Render Thread counter unavailable, no minimum-PC or campaign-performance claim. All11 protected files, existing435-file source/asset baseline except the smoke dispatcher,3 accepted source kits and four pre-existing players preserved. Four failed fixture attempts and black legacy captures are retained and excluded from acceptance. Native controls, listening and human FX appeal remain future gates. No production art/effect/gameplay changes; FX-01 cue/asset contracts is next. Canonical evidence: Docs/Evidence/fx-00-BASELINE.md and fx-00/audit.json.

Next: FX-01 only; reverify any editor/Blender connection before using it. Reproduce the baseline with the checked runner and fresh isolated evidence directories. Keep accepted R08.4 as the ordinary playable; the FX diagnostic candidate adds only optional measurement code. Do not reopen skipped tutorial or R09 work as part of FX continuation.


## FX-01 contract checkpoint — 2026-10-11

FX-01 delivered 41 typed cue contracts and 10 explicit attachment records for the lab/operating hands/Pondlet aquarium, numeric anticipation/action/settle poses, cancellation/identity policies, original local hand/export plan and initial emitter/particle/voice/material ceilings. These are frozen design contracts, not implemented effects or performance passes. Source/accepted CollectorKit mouth and upgraded cap bounds informed the plan; fixed hatchery frame retained. Full contract: Docs/Design/FX01_CUE_ASSET_CONTRACT.md; structured specification and original schematic: Docs/Evidence/fx-01/contracts.json and key-poses.png. Static validation and preservation evidence: fx-01/validation.json. No Unity/player build or game tests ran; runtime/asset/settings/save and preserved players unchanged. FX-02 minimal presentation integration is next; FX-06 human motion/audio sample approval remains open.

Canonical next assignment is AGENTS.md FX-02; this record is historical evidence, not a competing roadmap. No new game build/tests or current live Unity/Blender connection is claimed.

FX-01 final evidence: **26/26 static checks passed**, process exit0; all540 tracked source/art/settings files and13 protected files unchanged. Conservative Pondlet reference box covered26,568 sampled corners plus a continuous-angle margin; actual Unity mesh sweep remains pending. The original local key-pose diagram was visually inspected. Delivery checkpoint tag: **fx-cue-contracts**. No game tests/build were run for this documentation assignment.


## FX-02 continuation — 2026-10-11

FX-02 delivered: bounded cue/state/lifetime/priority ownership and a fixed96 borrowed-mesh pool, with24 action-reserved slots and a32 Low cap. Accepted collection/dose and explicit dose refusal bind three small proof pulses after the real result; new success FX is suppressed after a failed write. Focus/pause/modal/arrange/disable/move/remove/reset cancel transient presentation; reload reconstructs steady state without replay. Final source ee005f2, tag fx-foundation-validated; separate player Builds\Windows-6000.3.21f1-fx02-foundation-2/EvolutionShop.exe. Final EditMode2,040/2,040, Editor63, exact Windows585 checks across five runs (64+64 FX,335 automation,61+61 baseline), runtime errors/exits0, build errors/warnings0. Eight actual changed-surface HUD captures reviewed and16 measured8s baseline/candidate windows recorded. All13 protected files and531/540 existing source/art/settings files unchanged;9 intentional C# integrations and14 new C#/meta files; Core assembly byte-identical. Four pre-existing players preserved. Full evidence: Docs/Evidence/fx-02-VALIDATION.md and fx-02/audit.json. FX-A foundation complete; next bounded assignment FX-03 working lab hero. Human motion/audio approval remains FX-06; R08.5, wider human gates and deferred tutorial are unchanged.

Read AGENTS.md first. Reverify current project/Editor, saves and processes. Unity CLI normal desktop execution is working with the pinned Editor; no new bridge or licensing reinstall. Start the six FX-03 subtasks only after the next continuation request. Incubator modal occlusion is an explicit FX-03 follow-up; native input/listening and human motion appeal are still open.

## FX-03 historical handoff — 2026-10-11

FX-03 is technically complete: Bloom/Flow refill motion and pooled intake into the real tool mouth; incubator circulation, true progress needle, growing Pondlet and first/repeat hatch accents; one collector valve/state path after saved operations; visible dose/return/refusal feedback; six original short local sounds. Source `800cdc4`, tag **fx-lab-hero-validated**. Separate playable `Builds/Windows-6000.3.21f1-fx03-lab-2/EvolutionShop.exe`. Final EditMode **2,049/2,049**, Editor **134**, candidate Windows **852 checks** (791 primary checks plus 61 repeated timing checks); runtime errors/exits 0, build errors/warnings 0. Reviewed 28 actual HUD captures; recorded 24 candidate and 24 preserved-reference timing windows, excluding focus mismatches from matched comparisons. All 16 protected files and 543/554 baseline files unchanged; 11 intentional C# integrations and 10 new C#/meta files. Core DLL is byte-identical; four existing players preserved. Evidence: Docs/Evidence/fx-03-VALIDATION.md. Native controls, listening and human motion approval remain FX-06. Next is FX-04; it is unstarted.

Primary candidate frame interval p95 8.51–8.57 ms; GPU p95 2.68–4.01 ms; whole-game GC p95 6400–15724 bytes. Fresh-reference matched frame change -0.10% to +0.46%. Four indicator renderers, eight transforms and one short module voice added over FX-02; total attributable FX topology is 100 renderers/105 transforms, with three audio sources total and no loaded mesh/material/Animator delta. The initial allowance has 12 renderers/75 transforms remaining. Repeated GPU values and their limitations are detailed below. This capped development workload does not establish minimum-spec or whole-game zero-GC performance.

Performance threshold review completed with 16 fresh matched-context pairs and eight repeated candidate windows against the earlier focused 1080p reference. The extra reversed-order reference lost focus and is excluded from matched comparison. Candidate frame interval p95 is 8.51–8.57 ms, change -0.12% to +0.46%; whole-game allocation p95 matches the reference in every pair. GPU p95 is 2.68–4.17 ms with mixed changes -21.6% to +24.2%. The 1080p ordinary-night lab increase repeats at +14.7%/+24.2% (+0.49/+0.81 ms); it is a measured cost/uncertainty, not a zero-regression pass. Archived recordings and the extra reversed-order reference were unfocused while the accepted fresh comparisons are focused, so their GPU percentages are confounded. Source/resource review finds four borrowed-mesh indicators, eight transforms, one short audio source, no added mesh/material/Animator/shader and no recurring collection allocations in the new steady-state loops. The recorded capped workload meets the initial 60 FPS aim; the GPU contribution is not isolated from the four preserved players. Recheck the lab with controlled single-instance profiling at FX-06/22 before multiplying the hero content.


## FX-06 prepared review — 2026-10-11

**FX-06 prepared for review — 2026-10-11.** Technical preparation is saved at source `2fa5efc`, tag **fx-review-prepared**; separate playable `Builds/Windows-6000.3.21f1-fx06-review-2/EvolutionShop.exe`. Actual final EditMode **2,060/2,060**, Editor **266**, exact Windows **365** assertions across three routes; successful errors/exits0, build0 errors/0 warnings. Eighteen real720p/1080p videos contain2,490 decoded frames at29.92–30.18FPS/missed0; six original raw sound previews and local native review launchers are provided in `Docs/Evidence/fx-06/human-review/index.html`. All36 protected files and586/587 baseline source/art/settings files unchanged; one intentional diagnostic dispatcher,5 code/meta payload, Core DLL byte-identical and four original players preserved. Capture inversion was fixed and rerun; initial media is retained/excluded. Ordinary-input isolated candidate/reference startup verified, but hidden windows required task-owned cleanup; no native controls/listening/user approval or controlled GPU pass is claimed. Evidence: `Docs/Evidence/fx-06-VALIDATION.md`. **FX-06 remains active; FX-B/FX-07 gated pending review and isolated night-lab GPU attribution.**

- [x] Exact Unity/project guard, protected baseline and diagnostic source checkpoint.
- [x] Exact final candidate, regression tests,18 actual motion clips,6 raw sound previews, ordinary-input startup and isolated human launchers.
- [x] Saved local review package, immutable evidence receipts and updated guide.
- [ ] Actual native keyboard/held collection/stations, heard game mix and user lab/hands/tank assessment.
- [ ] GPU attribution acceptance:2026-10-12 authorized comparisons are recorded; the night-lab increase did not repeat, while tank GPU deltas and measurement limits remain. Old-PID closure was authorized and denied by Windows; apparent inactivity is evidenced. No repeated permission request is needed.

Continue within FX-06; do not multiply content or mark FX-B approved. Normal controls: WASD/mouse, hold LMB, E, Tab, F1, Escape.


2026-10-12: The authorized FX-06 comparison is checkpointed separately from the unchanged playable/review package. Eight successful routes,64 end-focused windows and40 matched pairs; user has not played the sample. Windows denied old-PID closure, but execution/GPU evidence shows apparent inactivity. Preserve the completed evidence and finish only the pending FX-06 review/attribution gates before FX-07. See the canonical guide and dated report above.


2026-10-12: User requests stronger combined liquid/bubble, glowing-science and cartoon-puff effects. Priority hatching, dosing, growth; all living ambient details. Canonical FX.10 includes eight bounded implementation/review assignments and updates every remaining FX-06–24 row. Planning only: FX-06R-00 complete; next R-01. No revised runtime/creative pass.


2026-10-12 FX-06R-01 delivery: reusable five-shape motion/fade vocabulary, fixed96 pool with64/32 Standard and24/8 Low split, separate candidate54126ec / fx06r01-vocabulary-2. Actual2,080 EditMode /29 Editor /1,270 Windows checks; final build errors/warnings0.32 actual HUD views,32 conservative raster audits and16 continuous-focus cost windows; GPU attribution remains provisional. Protected save/assets/three prior builds preserved. AGENTS.md remains the live roadmap; next R-02 hatch/discovery. See Evidence/fx-06r-01-VALIDATION.md, fx-06r-01/audit.json and the local gallery. No FX-B acceptance or later FX expansion.


## 2026-10-12 — FX-06R-01 organic correction

User rejected rows/icon-like effects. Source d75a46d, tag fx-organic-hatch-correction, separate fx06r01-organic-1 player: original shaded bubbles/round vapor and staggered real first/repeat hatch. Actual2,086 EditMode,51 Editor,271 Windows checks; build0 errors/0 warnings. Ten actual videos/838 decoded frames; all36 protected files and prior candidate unchanged. One initial reduced-recipe test failure retained then corrected. Visual acceptance/native controls/listening/GPU attribution remain open. Canonical status and next action in AGENTS.md; evidence Docs/Evidence/fx-06r-01-organic-VALIDATION.md.


## 2026-10-12 — FX-06R-02 hatch/discovery complete

Source7db5cc7, tagfx-hatch-hero-validated; separate fx06r02-hatch-1 player. Approved organic bubbles now coordinate with1.4s first/.85s repeat Pondlet wake/settle and two original sounds; immediate collection stays available. Actual2,095 EditMode,116 Editor,668 Windows checks passed; build errors/warnings0. Ten videos/838 decoded frames, raw sound previews, actual864-pose20-mesh sweeps. All36 protected files/prior candidate preserved. Evidence Docs/Evidence/fx-06r-02-VALIDATION.md; gallery8789. Native controls/listening/new visual acceptance/integrated GPU remain open. AGENTS.md is canonical; **next FX-06R-03 dose/mixing**, no later work started.


## 2026-10-12 — FX-06R-03 delivered

Final source d5af465, tag fx-dose-mixing-validated, separate fx06r03-dose-2 player. World/panel dose streams, actual composition/fill, return/clear/known recipes; journal cancellation and faint UI masks corrected. Actual 2,101 EditMode, 87 Editor, 767 final Windows checks; build 0 errors/0 warnings; 12 final videos/1176 decoded frames. All 36 protected files/previous hatch candidate preserved. Evidence Docs/Evidence/fx-06r-03-VALIDATION.md; gallery 8790. AGENTS.md is canonical; next FX-06R-04 only. Native/listening/creative/GPU gates stay open.


## 2026-10-12 — FX-06R-04 delivered

Source b8a60c6, tag fx-growth-lab-validated, separate fx06r04-growth-1 player. State-derived maturation, chamber circulation and true grower refill/full/depleted. Actual 2,108 EditMode, 81 Editor and 440 Windows checks; build 0 errors/0 warnings; 12 videos/1,781 decoded frames and actual 176-pose mesh fit. All 36 protected files and previous dosing player preserved. Simple support used GPT-6.1 Sol; advanced implementation/validation used GPT-6 Astra, persisted policy in AGENTS section 8. Evidence Docs/Evidence/fx-06r-04-VALIDATION.md; gallery 8791. Next R-05 only. Native/listening/creative/GPU gates stay open.


## 2026-10-12 — FX-06R-05 delivered

Source f0b7e4e, tag fx-collection-swirl-validated, separate fx06r05-collection-2 player. Irregular actual-outlet→moving-mouth collection, accepted-unit chamber response; original capacities/cadence. Actual 2,115 EditMode, 163 final Editor, 991 Windows checks; build 0 errors / 0 warnings; 12 videos / 1,380 decoded frames. All 36 protected files / prior growth player preserved; Core DLL identical. Sol simple support/Astra advanced policy retained. Evidence Docs/Evidence/fx-06r-05-VALIDATION.md, gallery 8792. Native/listening/creative/GPU gates open. Next R-06 only.


## 2026-10-14 — FX-06R-06 delivered

Source 11df5a8, tag fx-aquarium-validated, separate fx06r06-aquarium-2 player. The current fixed tank-a reuses water, plants and irregular existing bubbles, adds a contained motion-linked Pondlet trail and a saved-placement surface ripple, and clears the specimen trail after sale. The existing fish pose owner and fixed FX pool are reused. Actual 2,121 passed EditMode tests, 297 Editor checks and 1139 exact Windows checks; build 0 errors / 0 warnings. 16 actual silent 720p/1080p videos decode 1,408 frames. All 36 protected files and the prior 291-file collection-2 player are preserved; the retained initial aquarium candidate has 291 unchanged files. 873/878 baseline files are unchanged. Core DLL is identical. Evidence Docs/Evidence/fx-06r-06-VALIDATION.md; gallery http://127.0.0.1:8793/index.html. Native controls, in-game listening, user creative acceptance and integrated CPU/GPU attribution remain pending in R-07/08. FX-06/FX-B stay open; FX-07 expansion waits for R-08 acceptance. Next R-07 only.


## 2026-10-14 — FX-06R-07 technical audit complete with limits

**FX-06R-07 technical audit complete with limits — 2026-10-14.** Preserved the approved organic lab/tool/aquarium effects and produced separate integrated-3 candidate, source **2f0cb21**, delivery tag **fx-integrated-audited**. Actual **2121 passed EditMode tests**, **1692 final-candidate Windows hero checks**, and retained candidate2 **649 gameplay/helper checks + 674 profile checks**; successful build 0 errors / 0 warnings. 62 actual silent 720p/1080p videos decode 6,584 frames. Final candidate differs from measured candidate2/dfc0a67 only in two reviewed diagnostic fixtures; it was not separately profiled. 96 windows / 92,160 samples show stable authority/resources and zero allocation in the direct presentation scope, with p95 elapsed 0.0888–0.1933ms. All 48 GPU comparisons remain excluded by external foreground/overlap and clock evidence; no causal GPU or minimum-spec pass. All 36 protected files and 873 prior-build files preserved; both earlier integrated candidates retained; Core DLL identical. Evidence `Docs/Evidence/fx-06r-07-VALIDATION.md`; gallery `http://127.0.0.1:8794/index.html`; controls in `Docs/Evidence/fx-06r-07/PLAY.md`. **Next: FX-06R-08 only.** Native controls, actual listening, creative acceptance and the final GPU decision remain open; FX-07 has not started.

Retained candidate2/dfc0a67 measurements and loop regressions are explicitly distinguished from finalcandidate3 hero runs; two diagnostic-only corrections and evidence provenance are in candidate-input-equivalence.json. All attempts remain saved. AGENTS.md is canonical. Next FX-06R-08 only.


## 2026-10-14 — FX-07 growers delivered provisionally

**FX-07 technically complete — 2026-10-14; hands-on review deferred.** Bloom and Flow now have distinct contained circulation, natural refill fizz, saved-collection accents, restrained gauge responses and truthful empty/full/refusal labels. Existing vessels, roots, colliders, particle pool and accepted-unit sound are reused; no new production mechanic, art topology or audio loop. Source **70821d1**, separate playable `C:\dev\DumbVideoGameStudio\Builds\Windows-6000.3.21f1-fx07-growers-3\EvolutionShop.exe`, delivery tag **fx-growers-validated**. Actual **2131 passed EditMode tests**, **977 Windows route checks**, successful build 0 errors / 0 warnings. 14 actual silent 720p/1080p videos decode 3170 frames. All 36 protected files and 873 prior-build files preserved; Core DLL identical. Evidence `Docs/Evidence/fx-07-VALIDATION.md`; gallery `http://127.0.0.1:8795/index.html`; controls `Docs/Evidence/fx-07/PLAY.md`. The user explicitly authorized provisional continuation and will give hands-on feedback later. FX-06R-08 native controls, listening, creative acceptance and GPU decision remain open; FX-06/FX-B are not closed. **Next implementation assignment: FX-08 only when continued; not started here.**

AGENTS.md is canonical. Current continuation authorization supersedes the earlier FX-07 sequencing hold only for this completed bounded assignment. Hands-on feedback remains deferred by the user.


Known presentation follow-up: close views can put the small grower status plate behind the interaction HUD; the generic hold-to-collect prompt remains visible alongside the accurate full-reservoir explanation. Preserve this observation for UI polish/hands-on feedback. FX-07 technical completion does not assert comfortable status-label readability.


## 2026-10-14 — FX-08 advanced lab delivered provisionally

**FX-08 technically complete — 2026-10-14; hands-on review deferred.** Existing Spark and Mist now have distinct bounded source/refill effects and pooled source-to-siphon intake. The actual two-chamber conditioner responds to working progress, saved load/output and real Pondlet-to-Lanternfin completion; cancellation and reload do not celebrate a transformation. Existing art, roots, colliders, production rules, pool and audio ownership are preserved. Source **4442d9b**, separate playable `C:\dev\DumbVideoGameStudio\Builds\Windows-6000.3.21f1-fx08-lab-2\EvolutionShop.exe`, delivery tag **fx-advanced-lab-validated**. Actual **2140 passed EditMode tests**, **1680 final Windows checks**, successful build 0 errors / 0 warnings. 16 silent Windows videos decode 4456 frames. All 36 protected files and 873 prior-build files are preserved; Core DLL is identical. Evidence `Docs/Evidence/fx-08-VALIDATION.md`; gallery `http://127.0.0.1:8796/index.html`; controls `Docs/Evidence/fx-08/PLAY.md`. **Next: FX-09 incubation/hatch completion on a subsequent continuation; not started here.** FX-06R-08 controls, listening, creative acceptance and GPU decision remain open, as do earlier human gates.

AGENTS.md is canonical. Hands-on controls, actual listening, creative acceptance and the prior FX-06R-08 GPU decision remain deferred/open. This review inspected selected decoded frames, not every video frame or live gameplay motion. Close-up interaction HUD/tool partly hide the lower vat status and paddle; transient notifications can cover the Spark stock label. Track those view/overlay combinations in FX-18/22; the essential counts are also present in HUD text. No new performance/minimum-spec claim. The automatic clip records Spark only; Mist automatic preparation is independently checked by runtime assertions. Conditioner diagnostic specimen IDs derive from authority, not an independent mesh-identity proof. The bounded tests do not cover every full-buffer/path-obstruction or individual command-save-failure permutation.


## 2026-10-14 — FX-09 incubation/hatch delivered provisionally

**FX-09 technically complete — 2026-10-14; hands-on review deferred.** The five existing fish—Pondlet, Bubblebelly, Ribbonfin, Lanternfin and Driftwing—now use actual countdown growth and species-compatible hatch/settle presentation in the existing incubator. First discovery uses the stronger accepted release and repeat ready uses the smaller release; the visual clock does not delay collection or complete a batch. Recipes, timing, specimen ownership, existing art/rooms, roots/colliders and fixed particle/audio ownership are preserved. Source **c819793**, separate playable `C:\dev\DumbVideoGameStudio\Builds\Windows-6000.3.21f1-fx09-hatch-1\EvolutionShop.exe`, delivery tag **fx-incubation-validated**. Actual **2160 passed EditMode tests**, **3173 final Windows checks**, successful build 0 errors / 0 warnings. 24 silent Windows videos decode 7439 frames. All 40 protected files and 873 prior-build files are preserved; Core DLL is identical. Evidence `Docs/Evidence/fx-09-VALIDATION.md`; gallery `http://127.0.0.1:8797/index.html`; controls `Docs/Evidence/fx-09/PLAY.md`. **Next: FX-10 research and automation completion only on continuation; not started here.** Hands-on controls, actual listening, creative acceptance and the prior FX-06R-08 GPU decision remain deferred/open, as do earlier human gates. Technical completion does not imply native-input, human, minimum-spec or causal GPU acceptance.

AGENTS.md is canonical. Known limits: - This is sampled visual inspection and full file decoding, not a frame-by-frame human motion playtest. The 40 geometry sweeps per run and live sampled bounds are finite coverage, not a mathematical proof of every possible phase.
- Existing close-view HUD/tool occlusion remains: the interaction prompt and siphon hide part of the lower hatchery control panel. Central fish and countdown remain readable; general HUD/held-tool composition is deferred to FX18/22.
- Input, setup and camera are scripted. Videos are silent; actual controls, in-game listening and creative acceptance remain deferred by the user. Low/reduced are diagnostic policies. No new GPU, minimum-spec, campaign or causal performance pass is claimed.
- One actual movable incubator and five existing species are covered; no new production container, recipe, species or later milestone was introduced. Earlier tutorial/campaign gates and FX-06R-08 stay open.
