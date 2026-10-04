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
