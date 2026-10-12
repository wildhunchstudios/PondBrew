# Validation evidence

> Latest FX-03 checkpoint: source 800cdc4, tag fx-lab-hero-validated. Canonical status/nextFX-04: ../AGENTS.md. Evidence: Evidence/fx-03-VALIDATION.md; prior records below are historical.

Historical Milestone01 evidence from 2026-10-03 follows. Current R08.1 candidate results are appended at the end of this file.

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

## 2026-10-03 — user-confirmed two-room refinement (Milestone 01)

The user confirmed the front room is the actual store (two display tanks and sales counter); the back room is the lab (Bloom/Flow growers, incubator, upgrade bench). Implemented within the existing room footprint using a collidable partition and a 3.2 m wide / 2.8 m high open doorway. Updated signs, floor accents, station positions, onboarding/objectives, and validation viewpoints. The original brief remains untouched. No later milestone features were added.

Reverified workspace `C:\dev\DumbVideoGameStudio`, project `EvolutionShop`, pinned/installed editor `6000.3.15f1_c1aa84e375f6`. No Unity/player process was running before validation. `unity status --json` reported zero Pipeline instances; no active bridge was assumed. Both targeted editor logs identify `C:/dev/DumbVideoGameStudio/EvolutionShop` and Unity6000.3.15f1 through the project guard.

### Executed Editor checks

The first run (`two-rooms-editor.log`, `two-rooms-editor/smoke-result.json`) exited0 and passed78 checks. Screenshots nevertheless revealed TextMesh labels drawing through the opaque partition. This was a visual failure despite automated gameplay checks passing; its evidence is retained. A new URP world-text shader uses ordinary depth testing and tracks font atlas rebuilds. World labels now respect the wall and plaque backing.

The second run (`two-rooms-final-editor.log`, `two-rooms-final-editor/smoke-result.json`) exited0 and passed78 checks with no captured runtime errors, elapsed49.495seconds. Root inspected actual camera renders of the doorway, store display tanks, counter, and lab: through-wall text and reversed plaque overlap were resolved. Its remaining hidden-batch cursor limitation is explicit in JSON. A subsequently noticed tool-readout placement issue was corrected before the Windows build; final player captures are the evidence for that adjustment.

New executed checks use queued keyboard input through the actual Input System and CharacterController: walking through the doorway both ways, stopping against solid partition on either side, and E failing to interact with an in-range station across the wall from either room. All seven station aiming checks and the full production/save/sale/upgrade scenario also passed. Scripted collection now approaches its grower before harvesting. This is not a human route/feel test.

Exact Editor invocation (each run used the corresponding evidence/log name):

```powershell
$run = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -ArgumentList @('-batchmode','-projectPath','C:\dev\DumbVideoGameStudio\EvolutionShop','-executeMethod','EvolutionShop.Editor.ProjectSetup.RunSmoke','-evolutionSmoke','-evolutionEvidence','C:\dev\DumbVideoGameStudio\Docs\Evidence\two-rooms-final-editor','-logFile','C:\dev\DumbVideoGameStudio\Docs\Evidence\two-rooms-final-editor.log') -WindowStyle Hidden -Wait -PassThru
```

The unchanged core/model files were not edited, and their previously executed50 EditMode cases were not rerun for this presentation/layout correction. Their historical XML is preserved; no new EditMode execution is claimed.

### Windows build and exact final player

The first revised build (`two-rooms-build-result.json`) succeeded with0errors/0warnings, and its player (`two-rooms-player/smoke-result.json`) passed79checks with no runtime errors/limitations, exit0,49.571seconds. Actual screen captures revealed the incubation HUD status obscuring the new physical siphon readout. The status box was moved below currency at the upper right, and the player was rebuilt.

The final build (`two-rooms-final-build-result.json`, `two-rooms-final-build-editor.log`) **Succeeded,0errors,0warnings,166548369bytes**, editor process exit0. The exact final Windows player (`two-rooms-final-player/smoke-result.json`, `two-rooms-final-player.log`) **passed79checks, runtimeErrors[], limitations[], process exit0**. These include captured cursor on resume and all six new two-room movement/occlusion checks, along with the full gameplay/persistence scenario and three creature bounds checks.

Exact final build and launch:

```powershell
$run = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -ArgumentList @('-batchmode','-projectPath','C:\dev\DumbVideoGameStudio\EvolutionShop','-executeMethod','EvolutionShop.Editor.ProjectSetup.BuildWindows','-logFile','C:\dev\DumbVideoGameStudio\Docs\Evidence\two-rooms-final-build-editor.log') -WindowStyle Hidden -Wait -PassThru
Copy-Item -LiteralPath 'C:\dev\DumbVideoGameStudio\Docs\Evidence\windows-build-result.json' -Destination 'C:\dev\DumbVideoGameStudio\Docs\Evidence\two-rooms-final-build-result.json'
$run = Start-Process -FilePath 'C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe' -ArgumentList @('-evolutionSmoke','-evolutionEvidence','C:\dev\DumbVideoGameStudio\Docs\Evidence\two-rooms-final-player','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile','C:\dev\DumbVideoGameStudio\Docs\Evidence\two-rooms-final-player.log') -WindowStyle Normal -Wait -PassThru
```

The original milestone BuildReport JSON was preserved as `milestone01-original-build-result.json` before the setup route wrote the current `windows-build-result.json`. Original logs/reports/screens and the prior delivered ZIP remain intact.

Root inspected actual1280×720 final-player captures, including doorway/labels/HUD, both rooms, the counter, updated welcome text, Pondlet, Ribbonfin, full tanks, and upgraded tool. The final tool readout is visible and no text appears through the solid partition. These rendered observations are separate from the79 scripted assertions. Text uses a Resources-loaded shader, so successful player rendering also verifies build inclusion.

Capture-specific limit: close-up creature/tank images temporarily reposition the camera inside the capture coroutine. Their interaction prompt can still show the previous frame's grower target. Use them as rendering evidence, not evidence of the shown prompt/aim pairing; the separate seven-station targeting checks wait for updates and passed. Normal player camera movement updates focus each frame. Final scripted elapsed time was49.577seconds, not a human playtest pace.

The revised ZIP includes the executable, data, Mono runtime, graphics DLLs and D3D12 folder. Its package-verification JSON records archive SHA256 and matching hashes for the tested executable, UnityPlayer, runtime/core assemblies, Resources data, and scene data. Burst debug-information output is excluded. Output playtest guide and evidence copies accompany it.

No known technical blocker to the first user playtest. Placeholder art/audio, provisional balance, human route/feel/readability/creature appeal, audio listening, and manual settings-slider use remain playtest work. No performance or human completion-time claim is made. Save format/IDs and the user's normal save remain unchanged. Stop within Milestone01; do not expand to later milestones before user playtest.


## R05.4 validation record — 2026-10-04

Pinned Unity 6000.3.21f1 and exact `C:\dev\DumbVideoGameStudio\EvolutionShop` project path were confirmed on a fresh guarded Editor run; no Unity MCP bridge was assumed. Full final-source EditMode result: 1,195 passed, 0 failed/skipped, exit0 (`Docs/Evidence/r05-4-editmode-hud-final-suite-results.xml`). Windows development/Mono build succeeded with 0 errors/warnings, 167,773,926 bytes (`r05-4-build-delivery-result.json`). Exact final extracted package passed visible Airfin flow 81 checks, separate-process active-incubation restart 12 and prior conditioning 200; all had zero runtime errors and process exit0. See the corresponding `r05-4-player-*-delivery-*` process/smoke receipts. Final package verification hashes all 290 files against both build and ZIP; ZIP SHA256 `6A1BE8211E5A17E865E404CBE414A8FD8EC66A5E7FEFC06279409EAB6AC82883`. The 11 protected scene/package/settings/normal-save files match `r05-4-preservation-baseline.json` in `r05-4-preservation-final.json`.

Retained failed attempts: first source compile reported a local variable collision; first Airfin smoke compile found a missing Input System namespace/unassigned root; two native containment sweeps found Driftwing tail outside glass and led to smaller/shorter procedural geometry. The first built-player Airfin run could not locate an editor-only fixture path; the smoke now takes its seed from isolated evidence. First full suite failed 78 tests, mostly legacy synthetic fixture shape and one starter-loop assumption; fixtures were corrected without relaxing old save validation, then final full suite passed. Hidden player GUI screenshots were black despite passing assertions; the final visible-window run produced actual UI captures. The earlier candidate ZIP is retained but not the delivered build. These failures are not counted as passes.

Scenario limitations: seeded progressed v15 fixture, selected customer, camera/player teleports, application menu commands rather than human GUI clicks, sampled animation sweep rather than exhaustive poses. No natural affordability/time-to-unlock, human feel/audio, every layout, or hardware benchmark was measured. The exact final package is playable without validation flags. Current guide: `../AGENTS.md` R05.4 delivery.

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

## 2026-10-10 — R08.1 technical candidate validated

Unity 6000.3.21f1 built source bf29ebe to Builds/Windows-6000.3.21f1-r08-1-automation-candidate/EvolutionShop.exe. Build summary: Succeeded,0 build errors,0 warnings, process exit0. Focused EditMode73/73; full EditMode1,998/1,998; Editor automation247. Exact Windows automation passed247 checks at1280x720 and247 at1920x1080. Technician136, Stocker133, Staff102 and Tutorial124 regressions passed. All player reports show runtimeErrors0 and process exits0.

Capture review covers real player images at both sizes. The first locked-state 720p screen capture is black; later 720p captures render correctly. The lab panel is scrollable, with its lower bench-return action below the initial viewport. Native-input/human playability, sound, provisional module prices, and campaign balance remain unverified. Build log includes an access-token update diagnostic though the editor produced and ran the candidate; player shutdown emits Unity MemoryLeaks telemetry without a captured runtime exception. Protected11/11 hashes and normal save match baseline; prior R07.5 build/package and four original players remain.

Candidate and exact evidence: Docs/Evidence/r08-1-VALIDATION.md. Await first user playtest before R08.2; tutorial remainder stays explicitly deferred.


## 2026-10-11 — FX-00 complete

FX-00 delivered on 2026-10-11; diagnostic source checkpoint b918fd4, tag fx-baseline-audited. Pinned Unity6000.3.21f1 verified the exact project through a guarded Editor probe, exit0. Separate diagnostic Windows candidate Builds/Windows-6000.3.21f1-fx00-baseline-5/EvolutionShop.exe built with0 errors/0 warnings. Fixture-only43 checks; final baseline720p 61 and1080p 61 checks, R08.4 automation regression 335, successful-route runtime errors/exits0. The extra legacy reset route failed at its stale paused-Welcome assumption and is recorded separately against the preserved build. Sixteen actual HUD captures and sixteen8s ordinary/busy night/day timing windows inspected/recorded. Frame interval p95 8.53–8.55 ms and GPU p95 2.93–4.07 ms across this recorded PC/setup;120FPS cap retained, Main Thread includes waits, Render Thread counter unavailable, no minimum-PC or campaign-performance claim. All11 protected files, existing435-file source/asset baseline except the smoke dispatcher,3 accepted source kits and four pre-existing players preserved. Four failed fixture attempts and black legacy captures are retained and excluded from acceptance. Native controls, listening and human FX appeal remain future gates. No production art/effect/gameplay changes; FX-01 cue/asset contracts is next. Canonical evidence: Docs/Evidence/fx-00-BASELINE.md and fx-00/audit.json.

Next: FX-01 only; reverify any editor/Blender connection before using it. Reproduce the baseline with the checked runner and fresh isolated evidence directories. Keep accepted R08.4 as the ordinary playable; the FX diagnostic candidate adds only optional measurement code. Do not reopen skipped tutorial or R09 work as part of FX continuation.


## FX-01 contract checkpoint — 2026-10-11

FX-01 delivered 41 typed cue contracts and 10 explicit attachment records for the lab/operating hands/Pondlet aquarium, numeric anticipation/action/settle poses, cancellation/identity policies, original local hand/export plan and initial emitter/particle/voice/material ceilings. These are frozen design contracts, not implemented effects or performance passes. Source/accepted CollectorKit mouth and upgraded cap bounds informed the plan; fixed hatchery frame retained. Full contract: Docs/Design/FX01_CUE_ASSET_CONTRACT.md; structured specification and original schematic: Docs/Evidence/fx-01/contracts.json and key-poses.png. Static validation and preservation evidence: fx-01/validation.json. No Unity/player build or game tests ran; runtime/asset/settings/save and preserved players unchanged. FX-02 minimal presentation integration is next; FX-06 human motion/audio sample approval remains open.

Canonical next assignment is AGENTS.md FX-02; this record is historical evidence, not a competing roadmap. No new game build/tests or current live Unity/Blender connection is claimed.

FX-01 final evidence: **26/26 static checks passed**, process exit0; all540 tracked source/art/settings files and13 protected files unchanged. Conservative Pondlet reference box covered26,568 sampled corners plus a continuous-angle margin; actual Unity mesh sweep remains pending. The original local key-pose diagram was visually inspected. Delivery checkpoint tag: **fx-cue-contracts**. No game tests/build were run for this documentation assignment.


## FX-02 final evidence — 2026-10-11

FX-02 delivered: bounded cue/state/lifetime/priority ownership and a fixed96 borrowed-mesh pool, with24 action-reserved slots and a32 Low cap. Accepted collection/dose and explicit dose refusal bind three small proof pulses after the real result; new success FX is suppressed after a failed write. Focus/pause/modal/arrange/disable/move/remove/reset cancel transient presentation; reload reconstructs steady state without replay. Final source ee005f2, tag fx-foundation-validated; separate player Builds\Windows-6000.3.21f1-fx02-foundation-2/EvolutionShop.exe. Final EditMode2,040/2,040, Editor63, exact Windows585 checks across five runs (64+64 FX,335 automation,61+61 baseline), runtime errors/exits0, build errors/warnings0. Eight actual changed-surface HUD captures reviewed and16 measured8s baseline/candidate windows recorded. All13 protected files and531/540 existing source/art/settings files unchanged;9 intentional C# integrations and14 new C#/meta files; Core assembly byte-identical. Four pre-existing players preserved. Full evidence: Docs/Evidence/fx-02-VALIDATION.md and fx-02/audit.json. FX-A foundation complete; next bounded assignment FX-03 working lab hero. Human motion/audio approval remains FX-06; R08.5, wider human gates and deferred tutorial are unchanged.

The current source compiled and full2,040 EditMode cases passed, skipped0. Final Windows FX64+64, automation335 and baseline61+61 passed from the rebuilt culture-corrected executable. Human FX approval, native input and audio listening were not run. See the separate report for preserved failed attempts, measured comparisons, modality limitations and byte hashes.

## FX-03 historical handoff — 2026-10-11

FX-03 is technically complete: Bloom/Flow refill motion and pooled intake into the real tool mouth; incubator circulation, true progress needle, growing Pondlet and first/repeat hatch accents; one collector valve/state path after saved operations; visible dose/return/refusal feedback; six original short local sounds. Source `800cdc4`, tag **fx-lab-hero-validated**. Separate playable `Builds/Windows-6000.3.21f1-fx03-lab-2/EvolutionShop.exe`. Final EditMode **2,049/2,049**, Editor **134**, candidate Windows **852 checks** (791 primary checks plus 61 repeated timing checks); runtime errors/exits 0, build errors/warnings 0. Reviewed 28 actual HUD captures; recorded 24 candidate and 24 preserved-reference timing windows, excluding focus mismatches from matched comparisons. All 16 protected files and 543/554 baseline files unchanged; 11 intentional C# integrations and 10 new C#/meta files. Core DLL is byte-identical; four existing players preserved. Evidence: Docs/Evidence/fx-03-VALIDATION.md. Native controls, listening and human motion approval remain FX-06. Next is FX-04; it is unstarted.

Primary candidate frame interval p95 8.51–8.57 ms; GPU p95 2.68–4.01 ms; whole-game GC p95 6400–15724 bytes. Fresh-reference matched frame change -0.10% to +0.46%. Four indicator renderers, eight transforms and one short module voice added over FX-02; total attributable FX topology is 100 renderers/105 transforms, with three audio sources total and no loaded mesh/material/Animator delta. The initial allowance has 12 renderers/75 transforms remaining. Repeated GPU values and their limitations are detailed below. This capped development workload does not establish minimum-spec or whole-game zero-GC performance.

Performance threshold review completed with 16 fresh matched-context pairs and eight repeated candidate windows against the earlier focused 1080p reference. The extra reversed-order reference lost focus and is excluded from matched comparison. Candidate frame interval p95 is 8.51–8.57 ms, change -0.12% to +0.46%; whole-game allocation p95 matches the reference in every pair. GPU p95 is 2.68–4.17 ms with mixed changes -21.6% to +24.2%. The 1080p ordinary-night lab increase repeats at +14.7%/+24.2% (+0.49/+0.81 ms); it is a measured cost/uncertainty, not a zero-regression pass. Archived recordings and the extra reversed-order reference were unfocused while the accepted fresh comparisons are focused, so their GPU percentages are confounded. Source/resource review finds four borrowed-mesh indicators, eight transforms, one short audio source, no added mesh/material/Animator/shader and no recurring collection allocations in the new steady-state loops. The recorded capped workload meets the initial 60 FPS aim; the GPU contribution is not isolated from the four preserved players. Recheck the lab with controlled single-instance profiling at FX-06/22 before multiplying the hero content.


2026-10-12 FX-06R-01 delivery: reusable five-shape motion/fade vocabulary, fixed96 pool with64/32 Standard and24/8 Low split, separate candidate54126ec / fx06r01-vocabulary-2. Actual2,080 EditMode /29 Editor /1,270 Windows checks; final build errors/warnings0.32 actual HUD views,32 conservative raster audits and16 continuous-focus cost windows; GPU attribution remains provisional. Protected save/assets/three prior builds preserved. AGENTS.md remains the live roadmap; next R-02 hatch/discovery. See Evidence/fx-06r-01-VALIDATION.md, fx-06r-01/audit.json and the local gallery. No FX-B acceptance or later FX expansion.


## 2026-10-12 — FX-06R-01 organic correction

User rejected rows/icon-like effects. Source d75a46d, tag fx-organic-hatch-correction, separate fx06r01-organic-1 player: original shaded bubbles/round vapor and staggered real first/repeat hatch. Actual2,086 EditMode,51 Editor,271 Windows checks; build0 errors/0 warnings. Ten actual videos/838 decoded frames; all36 protected files and prior candidate unchanged. One initial reduced-recipe test failure retained then corrected. Visual acceptance/native controls/listening/GPU attribution remain open. Canonical status and next action in AGENTS.md; evidence Docs/Evidence/fx-06r-01-organic-VALIDATION.md.


## 2026-10-12 — FX-06R-02 hatch/discovery complete

Source7db5cc7, tagfx-hatch-hero-validated; separate fx06r02-hatch-1 player. Approved organic bubbles now coordinate with1.4s first/.85s repeat Pondlet wake/settle and two original sounds; immediate collection stays available. Actual2,095 EditMode,116 Editor,668 Windows checks passed; build errors/warnings0. Ten videos/838 decoded frames, raw sound previews, actual864-pose20-mesh sweeps. All36 protected files/prior candidate preserved. Evidence Docs/Evidence/fx-06r-02-VALIDATION.md; gallery8789. Native controls/listening/new visual acceptance/integrated GPU remain open. AGENTS.md is canonical; **next FX-06R-03 dose/mixing**, no later work started.


## 2026-10-12 — FX-06R-03 delivered

Final source d5af465, tag fx-dose-mixing-validated, separate fx06r03-dose-2 player. World/panel dose streams, actual composition/fill, return/clear/known recipes; journal cancellation and faint UI masks corrected. Actual 2,101 EditMode, 87 Editor, 767 final Windows checks; build 0 errors/0 warnings; 12 final videos/1176 decoded frames. All 36 protected files/previous hatch candidate preserved. Evidence Docs/Evidence/fx-06r-03-VALIDATION.md; gallery 8790. AGENTS.md is canonical; next FX-06R-04 only. Native/listening/creative/GPU gates stay open.
