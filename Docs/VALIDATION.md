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
