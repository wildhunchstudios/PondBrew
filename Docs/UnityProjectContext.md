# Unity project context

Updated: 2026-10-03. **Milestone 01 two-room refinement ready for first human playtest.** Final revised Windows build succeeded with zero errors/warnings and its exact player passed 79 checks, exit 0. Two-room Editor run passed 78 checks before minor readout/HUD placement changes; final standalone covers those changes. Historical 50 EditMode cases were not rerun for this refinement.

| Item | Current state |
|---|---|
| Approved workspace | `C:\dev\DumbVideoGameStudio` |
| Unity project | `C:\dev\DumbVideoGameStudio\EvolutionShop` |
| Original brief | `Evolution_Shop_Prototype_Handoff.md` |
| Scope | Checkpoint 00 and Milestone 01 only, including the user-confirmed front-store/back-lab correction |
| Installed executable editors | Unity 6000.3.14f1 and 6000.3.15f1 |
| Unusable installation folder | 6000.0.27f1 folder has no editor executable |
| Verified editor pin | 6000.3.15f1 (c1aa84e375f6), confirmed in ProjectVersion and executed editor log |
| Editor executable | `C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe` |
| Creation route | Installed Unity CLI, `projects new`, template `com.unity.template.urp-blank`; succeeded |
| Render pipeline | URP 17.3.0, resolved and registered |
| Input package | Input System 1.19.0, resolved and registered |
| Test package | Test Framework 1.6.0, resolved and registered |
| Windows build support | Mono and IL2CPP modules detected; final revised Windows x64 development/Mono build and player launch passed |
| Editor connection | Initial CLI status: zero instances. Explicitly targeted batch editor process/logs and result JSON subsequently verified exact EvolutionShop path |
| Unity MCP | No tools exposed/connected in this session |
| Scene | `Assets/EvolutionShop/Scenes/Shop.unity`; runtime bootstrap creates the verified front store and separate back lab with connecting doorway |
| Baseline route | Editor execute method `EvolutionShop.Editor.Checkpoint00.Run` |
| Source control | Checkpoint 00 commit `48ca887`; loop checkpoint `54ea873`; prior delivery tag `milestone-01-playtest`; root preparing refinement tag `milestone-01-two-rooms`; no remote/global safe-directory modification |
| Permissions | Narrow command-level workspace write approvals granted; no unrestricted permission change |

## Verified baseline and limits

`Docs/Evidence/checkpoint00-result.json` reports passed and playing, correct project/editor/scene. `checkpoint00-playmode.png` was captured and visually inspected. No C# errors were found. The editor log contains initial search-index `ArgumentOutOfRangeException`, licensing messages followed by successful entitlement resolution, and package-import warnings; see `VALIDATION.md`. This baseline does not prove milestone gameplay, automated logic tests, or standalone launch.

Always verify the editor-reported target path before future editor mutation. No project content or integration from Dungeon Project or PersonalAssistant may be imported.

## Agreed layout refinement

The user confirmed a separate **front store** containing display tanks A/B and the sales counter, and a **back lab** containing Bloom and Flow growers, the incubator, and the upgrade bench. A doorway connects the rooms. Preserve all seven stations and the existing Milestone 01 manual loop; this changes the spatial arrangement within Milestone 01.

The procedural world, stations, spawn, HUD guidance, and smoke viewpoints/routes now implement this layout. Final standalone checks verify doorway traversal both directions, partition collision on both sides, blocked through-wall interaction, all seven aim targets, and the complete manual loop. Actual 1280x720 front-store/back-lab/doorway/counter/welcome captures were reviewed. Labels use `Resources/EvolutionWorldText.shader` for depth testing; tool readout is visible with the machine HUD moved to the upper right. The original handoff is preserved; this user-confirmed correction supersedes its single compact-room assumption.

## Architecture and tuning

- `Core/GameTypes.cs`: serializable definitions/config/save DTOs, stable IDs, views/results. `GameConfig.CreateDefault()` centralizes provisional balance: growers capacity 6/initial 4/refill 2 sec, incubation 10 sec, siphon 4/8 capacity and 0.6/0.3 sec collection, upgrade 24, species values 6/10/10.
- `Core/GameModel.cs`: engine-independent authoritative state and guarded transactions. Public API: `Collect`, `Dose`, `ReturnDose`, `ClearDoses`, `UseKnownRecipe`, `BeginIncubation`, `Tick`, `TransferToTank`, `SellFromTank`, `BuyUpgrade`, `CaptureSave`, `TryRestore`. Staged doses reserve return space. Views/config are copied to protect authority.
- `Runtime/ShopSession.cs`: runtime orchestration, timing, interaction/focus, settings, autosave, panel/cursor control, and validation slot isolation. `ShopHud.cs` is its IMGUI panel partial; `ShopPlayer.cs` uses Input System/CharacterController; `ShopAudio.cs` synthesizes feedback tones.
- `Runtime/SaveStore.cs`: one JSON slot with formatVersion 1, game save version 1, sensitivity and volume. Writes temp then replace/backup. Validates before restoring; preserves invalid original and blocks autosave pending confirmed reset.
- `Presentation/ShopWorld.cs`: procedural front store/back lab, partition/doorway, seven stations, tool/reservoir, culture silhouettes and harvest motion. `Resources/EvolutionWorldText.shader` prevents world labels showing through walls. `CreatureVisual.cs`: distinct smooth bodies/fins/tails and swim animation.
- `Editor/ProjectSetup.cs`: guarded exact-project scene generation, delayed editor smoke start, and Windows development/Mono build route. `Runtime/MilestoneSmoke.cs` is Editor/development-only scripted validation. `Tests/EditMode/GameModelTests.cs` covers model invariants and persistence.

Default save: `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\evolution-shop.json`; settings are saved too. No elapsed offline simulation. Autosave runs during play and on successful actions/quit. Validation uses `-evolutionSmoke -evolutionEvidence <directory>` and a separate `smoke-save.json`.

## Controls and setup

WASD move, mouse look, hold left mouse at grower to harvest, E station/transfer, Tab journal, Escape panel/pause, mouse for panels. Pause includes sensitivity, sound, save/reload, confirmed reset, and quit. F8 compares base/upgraded siphon in Editor/development builds after purchase and with at most four reserved cultures.

Open `Shop.unity`, then Play. Menu **Evolution Shop > Rebuild playable scene** calls `ProjectSetup.CreateScene`. Batch smoke uses `ProjectSetup.RunSmoke`; Windows development/Mono build uses `ProjectSetup.BuildWindows` and targets `../Builds/Windows/EvolutionShop.exe`. The final revised build completed with zero errors/warnings and its exact player passed 79 checks, exit 0. Delivery archive name is `EvolutionShop-TwoRooms-Windows.zip`; root owns final packaging/checkpoint. Exact executed commands are in `VALIDATION.md`.

## Current verification and limits

Build output: `C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe`. Exact revised build report: `Docs/Evidence/two-rooms-final-build-result.json` (`Succeeded`, 0 errors, 0 warnings, 166,548,369 bytes; process exit 0). Exact player report: `Docs/Evidence/two-rooms-final-player/smoke-result.json` (79 checks passed, runtimeErrors [], limitations [], 49.5767059 scripted seconds; process exit 0). Actual HUD/screens are retained beside the report. The final player covers the late readout/HUD placement changes.

`two-rooms-final-editor` passed 78 checks with no captured runtime errors before those small final placements, with hidden batch cursor capture listed as a limitation. Standalone verifies cursor recapture. The historical 50 EditMode cases, initial 70 Editor/73 player checks, prior delivery tag, and failed attempts remain retained; they were not relabeled as fresh layout tests.

No critical progression blocker was found in executed final-player checks. Human enjoyment, two-room navigation/flow, timing, and creative approval still require the user's first playtest. Audio was not listened to and settings sliders were not manually dragged. Scripted time is not a human timing measurement. No later milestones started.

## Unity-written build/import changes

Reviewed and preserved generated normalization: PC_RPAsset prefilter keyword fields, DefaultVolumeProfile schema fields, UniversalRenderPipelineGlobalSettings runtime list, and PlayerSettings standalone batching. These were written by Unity build/import; they are not manually introduced shader configuration changes.

## Asset provenance

Room, stations, tool, cultures, and creatures are original procedural code using Unity primitives and URP materials. Short collection/transaction/discovery sounds are synthesized in `ShopAudio`. Unity URP/template/package dependencies are recorded in manifest/lock. No third-party game assets, paid services, or other-project assets were imported.

See `README.md` for play instructions and `RESUME.md` for exact continuation state. Do not begin later milestones before the user's first playtest.

## Historical record — R06.5v2 redesign, 2026-10-05

Core original Blender assets and native IMGUI redesign delivered at source `056ac01`, tag `r06-5-redesign`. Final EditMode: 1,246 passed; Windows build errors: 0/warnings: 0; exact package presentation: 248 / lab: 151 / edited: 228 / airfin: 81 / slap: 59 / comedy: 155 = 922 assertions, runtime errors: 0/exits: 0. All 11 protected files unchanged. Canonical scope, corrected failures, limitations and next playtest are in AGENTS.md; full raw evidence under Docs/Evidence/r06-5-redesign-*. Prior records remain historical. Human revised-look/audio feedback is next; no R07 work.
