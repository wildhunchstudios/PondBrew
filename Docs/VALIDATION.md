# Validation evidence

Updated: 2026-10-03. Milestone 01 is ready for the first human playtest. Checkpoint 00, 50 EditMode cases, 70 Editor Play-mode checks, and 73 final Windows player checks passed. Final build: zero errors and zero warnings. No known critical progression blocker was found.

## Environment / Checkpoint 00

| Check | Observed result | Evidence / limitation |
|---|---|---|
| Workspace | `C:\dev\DumbVideoGameStudio` exists; original handoff preserved | Initial filesystem inspection; new project child is `EvolutionShop` |
| Installed editors | 6000.3.14f1 and 6000.3.15f1 executable installations found | 6000.0.27f1 folder lacks executable and is not a usable installation |
| Windows modules | Windows Mono and IL2CPP support detected | Installed files inspected; no build executed yet |
| Initial editor connection | Unity CLI status returned zero instances | No previous editor connection assumed |
| MCP | No Unity MCP tools exposed/connected in this session | No competing bridge installed; targeted Editor process used |
| Project creation | CLI creation succeeded for exact path `C:\dev\DumbVideoGameStudio\EvolutionShop` | Unity 6000.3.15f1, URP blank template |
| Editor pin | 6000.3.15f1, revision c1aa84e375f6 | `EvolutionShop/ProjectSettings/ProjectVersion.txt` and editor log agree |
| Packages | Resolved/registered URP 17.3.0, Input System 1.19.0, Test Framework 1.6.0 | Manifest, package lock, and editor package registration log inspected |
| Target verification | Editor log confirms the exact project path; result JSON reports the same path | This is process/log verification, not a persistent MCP connection |
| Compilation | No C# errors in the Checkpoint 00 editor log; checkpoint method executed | Does not establish milestone gameplay correctness |
| Minimal Play mode | `passed: true`, `playing: true`, scene `Assets/EvolutionShop/Scenes/Shop.unity` | `Docs/Evidence/checkpoint00-result.json` |
| Render capture | Actual Play-mode PNG captured and visually inspected: floor and sphere rendered | `Docs/Evidence/checkpoint00-playmode.png`; baseline scene only, not milestone art |
| Version control | Local Git initialized | Checkpoint 00 commit `48ca887`; no remote publication or global safe-directory change |
| Permissions | Narrow command-level writes under approved project workspace were approved | No unrestricted/global permission change |

Project creation command:
```powershell
unity.exe projects new EvolutionShop --path C:\dev\DumbVideoGameStudio --editor-version 6000.3.15f1 --template com.unity.template.urp-blank --non-interactive --json
```

Executed Checkpoint 00 command (log starts 2026-10-03 09:31:38 UTC):
```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -batchmode -projectPath 'C:\dev\DumbVideoGameStudio\EvolutionShop' -executeMethod EvolutionShop.Editor.Checkpoint00.Run -logFile 'C:\dev\DumbVideoGameStudio\Docs\Evidence\checkpoint00-editor.log'
```

Evidence paths are relative to workspace root. The result JSON reports `C:/dev/DumbVideoGameStudio/EvolutionShop`, editor `6000.3.15f1`, and the scene above.

### Baseline limitations
- Initial editor indexing emitted `UnityEditor.Search.SearchDatabase` `ArgumentOutOfRangeException`. The minimal scene still entered Play mode and rendered. This is a recorded editor limitation, not an error-free log claim.
- Licensing reported signature validation and unavailable access-token refresh messages, then successfully resolved entitlement details and updated the license. Editor execution proceeded; no licensing repair was performed.
- Package import logged terrain shader dependency warnings. The captured baseline floor/sphere rendered; milestone visual validation is still required.
- No human movement/feel test, milestone logic test, standalone build, or player launch is established by Checkpoint 00.

## Milestone source implementation
Implemented the runtime room, seven interactable stations, CharacterController movement, Input System controls, HUD/panels, procedural cultures/creatures/audio, exact recipes and guarded transactions, timed production, journal, tank transfer/sale, physical siphon upgrade, and local save/reset/settings. Source implementation is separate from the executed evidence below.

## EditMode — passed
Executed 2026-10-03 09:45:51 UTC; XML run at 09:46:00 UTC. `Docs/Evidence/editmode-results.xml`: **50 passed, 0 failed, 0 skipped**. `Docs/Evidence/editmode-editor.log` reports `Test run completed. Exiting with code 0 (Ok)`.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -batchmode -projectPath 'C:\dev\DumbVideoGameStudio\EvolutionShop' -runTests -testPlatform EditMode -testResults 'C:\dev\DumbVideoGameStudio\Docs\Evidence\editmode-results.xml' -logFile 'C:\dev\DumbVideoGameStudio\Docs\Evidence\editmode-editor.log'
```

Coverage includes exact recipes, invalid mixtures and IDs, ingredient reservation/consumption, grower/reservoir/tank limits, incubation duplication, transfer and sale once, insufficient funds and upgrade once, discovery retention, malformed save rejection, and JSON round trips during incubation, after sale, with ready output/tank placement, staged doses/grower time, and upgrade. These are model tests, not visual or human-input acceptance.

## Timed Editor Play mode — passed with stated limits
Executed command below; log starts 2026-10-03 09:58:02 UTC. `Docs/Evidence/playmode/smoke-result.json`: **passed, 70 checks, no captured runtime errors**. `playmode-editor.log` records `EVOLUTION_SMOKE_PASSED checks=70 errors=0`; process exited 0.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -batchmode -projectPath 'C:\dev\DumbVideoGameStudio\EvolutionShop' -executeMethod EvolutionShop.Editor.ProjectSetup.RunSmoke -evolutionSmoke -evolutionEvidence 'C:\dev\DumbVideoGameStudio\Docs\Evidence\playmode' -logFile 'C:\dev\DumbVideoGameStudio\Docs\Evidence\playmode-editor.log'
```

Executed checks: fresh zero-currency state, seven stations and all aim targets, synthetic keyboard through real Input System/CharacterController, timed harvest/capacity, unsupported 2+2 recipe recovery, all three discoveries, once-only start/transfer/sale, full-tank output retention, real file save/load during incubation and after sale, insufficient funds, 26 credits from three species, 24-credit upgrade to eight units, larger chamber/faster collection, base comparison, repeat known recipe, malformed-save preservation, cancel/confirm reset, and separate validation slot.

The bounds assertion samples the stocked Pondlet and Bubblebelly across 120 animation positions. Ribbonfin is subsequently transferred successfully. Its post-transfer bounds sampling was then added and passed in standalone; see below.

Context: Windows 11, Unity 6000.3.15f1, NVIDIA GeForce RTX 4070 Ti SUPER, Intel Core i7-14700F. Scripted scenario elapsed **39.79 seconds**; this is not a human completion-time measurement or validation of the two/five-minute playtest hypotheses. No measured frame-rate claim is made.

### Captures and limits
- Actual 1600x900 URP camera PNGs are in `Docs/Evidence/playmode/`: startup, room/base tool, Pondlet ready, Bubblebelly ready, full tanks/Ribbonfin, upgraded tool, and journal viewpoint. Root visually inspected room/Pondlet/Bubblebelly renders.
- Captures use URP `RenderPipeline.SubmitRenderRequest`; they show rendered camera content. Batch Editor produced no usable full-screen HUD captures; a camera image with a journal filename is not proof that journal UI rendered.
- Pause releases/shows the cursor and resume unpauses simulation. Hidden batch Editor has no focused Game View: requested resume capture was not achieved (`actual lock=None`). Real cursor recapture remains a standalone check.
- Timed collection invokes the production runtime path and aiming is separately checked. The run is scripted; it does not establish human mouse feel, HUD readability, audible feedback, or enjoyment.

### Failed attempts and resolved findings
Four prior attempt results/logs are retained under `Docs/Evidence/attempts`; do not remove or relabel them as passes.
- A real high-frame-rate movement defect was found: CharacterController minimum move distance 0.001 discarded small per-frame steps. It is now zero; runtime requests a 120 FPS cap. Movement verification subsequently observed about 2.36 metres over 0.7 seconds in a prior run and about 2.38 metres in the successful run.
- Initial camera capture path was corrected to URP SubmitRenderRequest so shader rendering is captured through the active pipeline.
- Initial SearchDatabase indexing exception stopped recurring after scene startup waits for an idle editor and a five-second delay. The original baseline error remains in its log; this does not assert a universal Unity fix.
- Attempt 4 stopped on the hidden-editor cursor-capture assertion. The final editor result preserves this as a limitation requiring standalone verification rather than claiming cursor capture passed.

## Windows build and final player — passed
The final Windows x64 **development/Mono** build completed with editor process exit 0. `Docs/Evidence/windows-build-result.json`: `Succeeded`, **0 errors, 0 warnings**, 166,534,419 bytes. `windows-build-editor.log` begins 2026-10-03 10:06:11 UTC and is the final build evidence.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -batchmode -projectPath 'C:\dev\DumbVideoGameStudio\EvolutionShop' -executeMethod EvolutionShop.Editor.ProjectSetup.BuildWindows -logFile 'C:\dev\DumbVideoGameStudio\Docs\Evidence\windows-build-editor.log'
```

Produced player: `C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe`. Scene: `EvolutionShop/Assets/EvolutionShop/Scenes/Shop.unity`. Keep the player with its accompanying data/runtime folders.

Earlier hidden and visible standalone runs each passed 73 checks (`standalone/smoke-result.json` and `standalone-visible/smoke-result.json`; matching player logs retained). After final HUD/scene fixes, the **exact final build was launched again** and exited 0. `Docs/Evidence/final-player/smoke-result.json` reports **passed, 73 checks, runtimeErrors [], limitations []**, elapsed **44.665 seconds**; `final-player.log` records `EVOLUTION_SMOKE_PASSED checks=73 errors=0` and the runtime build/save identity.

Exact final player launch:
```powershell
Start-Process -FilePath 'C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe' -ArgumentList @('-evolutionSmoke', '-evolutionEvidence', 'C:\dev\DumbVideoGameStudio\Docs\Evidence\final-player', '-logFile', 'C:\dev\DumbVideoGameStudio\Docs\Evidence\final-player.log', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0') -WindowStyle Normal -Wait -PassThru
```
This selects a separate validation save, leaving the user's normal slot untouched.

Beyond the 70 editor checks, standalone verifies real cursor recapture on resume, synthetic E input opening the aimed incubator panel, and Ribbonfin's tail staying inside tank bounds over 120 animation samples. Pondlet/Bubblebelly bounds are also sampled. This resolves the hidden-editor cursor limitation for the produced player.

### Final visual evidence
Actual screen captures (including IMGUI HUD/panels) and separate URP camera captures are in `Docs/Evidence/final-player/`. Root visually inspected:
- `01-room-base-screen.png`: room, tool, HUD, unwrapped controls footer, and toast positioning.
- `04-ribbonfin-ready-screen.png`: clear long Ribbonfin silhouette with the lowered incubator ring.
- `06-journal-screen.png`: visible journal; discovery card does not overlap open menus.
- `07-pause-settings-screen.png`: readable pause/settings panel.
- `08-reset-confirmation-screen.png`: explicit reset confirmation.

Final welcome, incubator, and Pondlet screen captures were also visually inspected. Bubblebelly was visually inspected in earlier captures. Corrections in this final build: footer unwrapped, toast moved away from title, discovery card shifted left/hidden while panels are open, and incubator ring lowered to expose the creature.

### Unity-generated project normalization
Build/import rewrote URP prefilter keyword fields in `Assets/Settings/PC_RPAsset`, schema fields in `DefaultVolumeProfile`, the `UniversalRenderPipelineGlobalSettings` runtime list, and standalone batching in PlayerSettings. These Unity-written changes were reviewed and preserved; they are build/import normalization, not manual shader-setting edits.

### Known limits at handoff
- These are automated timed scenarios, not a human playtest. The final 44.665-second scripted run does not validate first-creature/two-minute or upgrade/five-minute hypotheses.
- No known critical progression blocker was observed in executed checks. Human enjoyment, movement/harvest feel, creature appeal, and balancing need user evaluation.
- Audio feedback is generated and wired, but no listening test was performed. Sensitivity/volume settings are implemented and saved; sliders were not manually dragged. A rendered settings panel is not proof of hands-on slider behavior.
- Procedural placeholders and provisional names/recipes/balance remain. No performance benchmark or FPS guarantee is claimed.
- Initial editor indexing/licensing messages and failed attempts remain documented above; final player has no captured runtime errors. A zero-warning BuildReport describes that build report, not a claim that every historical editor log is warning-free.
- No later milestones have started. Stop for the user's first playtest before expanding scope.
## Evidence recording rule
Append exact command/tool, execution context, observed result, and saved evidence after execution. Label source inspection, automated execution, and human observation separately. Do not infer a pass from generated code or expected output.
