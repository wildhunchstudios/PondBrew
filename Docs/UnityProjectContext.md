# Unity project context

Last inspection: 2026-10-03.

| Item | Current state |
|---|---|
| Approved workspace | `C:\dev\DumbVideoGameStudio` |
| Unity project | `C:\dev\DumbVideoGameStudio\EvolutionShop` |
| Original brief | `Evolution_Shop_Prototype_Handoff.md` |
| Scope | Checkpoint 00 and Milestone 01 only |
| Installed executable editors | Unity 6000.3.14f1 and 6000.3.15f1 |
| Unusable installation folder | 6000.0.27f1 folder has no editor executable |
| Editor selected for creation | 6000.3.15f1; generated `ProjectVersion.txt` confirmation pending |
| Creation route | Installed Unity CLI, `projects new`, template `com.unity.template.urp-blank` |
| Render pipeline | URP; generated manifest specifies 17.3.0 |
| Input package | Generated manifest specifies Input System 1.19.0 |
| Test package | Generated manifest specifies Test Framework 1.6.0 |
| Windows build support | Mono and IL2CPP modules detected; no build run yet |
| Editor connection | Initial CLI status: zero instances; exact project connection not verified yet |
| Unity MCP | No tools exposed in this session |
| Scene/content | Implementation and initial running-scene verification pending |
| Source control | Initialization/checkpoint pending |

The CLI started creating the project. Generated manifest files do not prove creation finished, packages imported, or compilation succeeded. Confirm the editor-reported target path before any editor mutation. No project content or integration from Dungeon Project or PersonalAssistant may be imported.

Architecture, concrete controls, scene paths, save location/schema, setup commands, asset provenance, and verified validation commands should be added once implementation establishes them. Current continuation state lives in `RESUME.md`.
