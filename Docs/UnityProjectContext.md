# Unity project context

Last inspection: 2026-10-03. Checkpoint 00 baseline runs; Milestone 01 implementation/validation remains next.

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
| Windows build support | Mono and IL2CPP modules detected; no build run yet |
| Editor connection | Initial CLI status: zero instances. Explicitly targeted batch editor process/logs and result JSON subsequently verified exact EvolutionShop path |
| Unity MCP | No tools exposed/connected in this session |
| Scene | `Assets/EvolutionShop/Scenes/Shop.unity`; minimal floor/sphere baseline entered Play mode and rendered |
| Baseline route | Editor execute method `EvolutionShop.Editor.Checkpoint00.Run` |
| Source control | Local Git initialized; Checkpoint 00 commit pending; no remote/global safe-directory modification |
| Permissions | Narrow command-level workspace write approvals granted; no unrestricted permission change |

## Verified baseline and limits
`Docs/Evidence/checkpoint00-result.json` reports passed and playing, correct project/editor/scene. `checkpoint00-playmode.png` was captured and visually inspected. No C# errors were found. The editor log contains initial search-index `ArgumentOutOfRangeException`, licensing messages followed by successful entitlement resolution, and package-import warnings; see `VALIDATION.md`. This baseline does not prove milestone gameplay, automated logic tests, or standalone launch.

Always verify the editor-reported target path before future editor mutation. No project content or integration from Dungeon Project or PersonalAssistant may be imported.

## Asset provenance
Current baseline uses Unity primitives and the bundled Unity URP template. No third-party game assets were imported. Milestone visuals and audio are planned as original procedural placeholders; record actual implementation/provenance when added. No paid assets/services are authorized.

Architecture, concrete controls, save location/schema, and milestone setup/validation commands should be added once implementation establishes them. Current continuation state lives in `RESUME.md`.
