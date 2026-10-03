# Unity project context

Last inspection: 2026-10-03. **Milestone 01 ready for first human playtest.** 50 EditMode cases, 70 Editor Play-mode checks, and 73 final Windows player checks passed; final build succeeded with zero errors/warnings.

| Item | Current state |
|---|---|
| Approved workspace | `C:\dev\DumbVideoGameStudio` |
| Unity project | `C:\dev\DumbVideoGameStudio\EvolutionShop` |
| Original brief | `Evolution_Shop_Prototype_Handoff.md` |
| Scope | Checkpoint 00 and Milestone 01 only |
| Installed executable editors | Unity 6000.3.14f1 and 6000.3.15f1 |
| Unusable installation folder | 6000.0.27f1 folder has no editor executable |
| Verified editor pin | 6000.3.15f1 (c1aa84e375f6), confirmed in ProjectVersion and executed editor log |
| Editor executable | `C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe` |
| Creation route | Installed Unity CLI, `projects new`, template `com.unity.template.urp-blank`; succeeded |
| Render pipeline | URP 17.3.0, resolved and registered |
| Input package | Input System 1.19.0, resolved and registered |
| Test package | Test Framework 1.6.0, resolved and registered |
| Windows build support | Mono and IL2CPP modules detected; Windows x64 development/Mono build and final player launch passed |
| Editor connection | Initial CLI status: zero instances. Explicitly targeted batch editor process/logs and result JSON subsequently verified exact EvolutionShop path |
| Unity MCP | No tools exposed/connected in this session |
| Scene | `Assets/EvolutionShop/Scenes/Shop.unity`; runtime bootstrap creates the playable lab/shop; baseline and milestone entered Play mode |
| Baseline route | Editor execute method `EvolutionShop.Editor.Checkpoint00.Run` |
| Source control | Checkpoint 00 commit `48ca887`; loop checkpoint `54ea873`; final tag `milestone-01-playtest`; no remote/global safe-directory modification |
| Permissions | Narrow command-level workspace write approvals granted; no unrestricted permission change |

## Verified baseline and limits
`Docs/Evidence/checkpoint00-result.json` reports passed and playing, correct project/editor/scene. `checkpoint00-playmode.png` was captured and visually inspected. No C# errors were found. The editor log contains initial search-index `ArgumentOutOfRangeException`, licensing messages followed by successful entitlement resolution, and package-import warnings; see `VALIDATION.md`. This baseline does not prove milestone gameplay, automated logic tests, or standalone launch.

Always verify the editor-reported target path before future editor mutation. No project content or integration from Dungeon Project or PersonalAssistant may be imported.

## Architecture and tuning
- `Core/GameTypes.cs`: serializable definitions/config/save DTOs, stable IDs, views/results. `GameConfig.CreateDefault()` centralizes provisional balance: growers capacity 6/initial 4/refill 2 sec, incubation 10 sec, siphon 4/8 capacity and 0.6/0.3 sec collection, upgrade 24, species values 6/10/10.
- `Core/GameModel.cs`: engine-independent authoritative state and guarded transactions. Public API: `Collect`, `Dose`, `ReturnDose`, `ClearDoses`, `UseKnownRecipe`, `BeginIncubation`, `Tick`, `TransferToTank`, `SellFromTank`, `BuyUpgrade`, `CaptureSave`, `TryRestore`. Staged doses reserve return space. Views/config are copied to protect authority.
- `Runtime/ShopSession.cs`: runtime orchestration, timing, interaction/focus, settings, autosave, panel/cursor control, and validation slot isolation. `ShopHud.cs` is its IMGUI panel partial; `ShopPlayer.cs` uses Input System/CharacterController; `ShopAudio.cs` synthesizes feedback tones.
- `Runtime/SaveStore.cs`: one JSON slot with formatVersion 1, game save version 1, sensitivity and volume. Writes temp then replace/backup. Validates before restoring; preserves invalid original and blocks autosave pending confirmed reset.
- `Presentation/ShopWorld.cs`: procedural room, seven stations, tool/reservoir, culture silhouettes and harvest motion. `CreatureVisual.cs`: distinct smooth bodies/fins/tails and swim animation.
- `Editor/ProjectSetup.cs`: guarded exact-project scene generation, delayed editor smoke start, and Windows development/Mono build route. `Runtime/MilestoneSmoke.cs` is Editor/development-only scripted validation. `Tests/EditMode/GameModelTests.cs` covers model invariants and persistence.

Default save: `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\evolution-shop.json`; settings are saved too. No elapsed offline simulation. Autosave runs during play and on successful actions/quit. Validation uses `-evolutionSmoke -evolutionEvidence <directory>` and a separate `smoke-save.json`.

## Controls and setup
WASD move, mouse look, hold left mouse at grower to harvest, E station/transfer, Tab journal, Escape panel/pause, mouse for panels. Pause includes sensitivity, sound, save/reload, confirmed reset, and quit. F8 compares base/upgraded siphon in Editor/development builds after purchase and with at most four reserved cultures.

Open `Shop.unity`, then Play. Menu **Evolution Shop > Rebuild playable scene** calls `ProjectSetup.CreateScene`. Batch smoke uses `ProjectSetup.RunSmoke`; Windows development/Mono build uses `ProjectSetup.BuildWindows` and targets `../Builds/Windows/EvolutionShop.exe`. The final build completed with zero errors/warnings and its exact player passed 73 checks, exit 0. Exact executed commands are in `VALIDATION.md`.

## Current verification limits
Final Windows player: `C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe`. Final report: `Docs/Evidence/final-player/smoke-result.json` (73 passed checks, no captured errors/limitations, 44.665 seconds); actual HUD/screens also captured and reviewed. Standalone verifies cursor recapture, E interaction, and all three species bounds. No known critical progression blocker was found. Human feel/timing/creative approval remains pending; audio was not listened to and settings sliders were not manually dragged. Scripted completion time is not a human timing measurement. Failed attempts remain in `Docs/Evidence/attempts`. No later milestones started.

## Unity-written build/import changes
Reviewed and preserved generated normalization: PC_RPAsset prefilter keyword fields, DefaultVolumeProfile schema fields, UniversalRenderPipelineGlobalSettings runtime list, and PlayerSettings standalone batching. These were written by Unity build/import; they are not manually introduced shader configuration changes.

## Asset provenance
Room, stations, tool, cultures, and creatures are original procedural code using Unity primitives and URP materials. Short collection/transaction/discovery sounds are synthesized in `ShopAudio`. Unity URP/template/package dependencies are recorded in manifest/lock. No third-party game assets, paid services, or other-project assets were imported.

See `README.md` for play instructions and `RESUME.md` for exact continuation state. Do not begin later milestones before the user's first playtest.
