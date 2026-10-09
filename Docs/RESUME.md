# Resume — Milestone 01 two-room playable

Workspace: `C:\dev\DumbVideoGameStudio`. Unity project: `C:\dev\DumbVideoGameStudio\EvolutionShop`. Verified2026-10-03. Independent game; Dungeon Project and PersonalAssistant were not modified or imported.

## Current design and implementation

The user explicitly confirmed two physical rooms: **front store** with the two display tanks and sales counter; **back lab** with Bloom/Flow growers, incubator, and siphon upgrade bench. A collidable partition has an open3.2m×2.8m doorway. Player starts in the store. Signs, distinct floor accents, and HUD directions explain the trip between rooms. Depth-tested world labels prevent text showing through walls. The siphon has a visible physical numeric display; incubation status is above it in the upper-right HUD.

All Milestone01 gameplay remains: free growers, timed siphon, exact integer recipes, incubation/discovery, two tanks, chosen sales, currency, physical capacity/speed upgrade, one compatible local save, settings, and confirmed reset. No later milestone work started.

## Verified runtime and evidence

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
