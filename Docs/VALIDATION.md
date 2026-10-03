# Validation evidence

Updated: 2026-10-03. Checkpoint 00 minimal-scene compilation and Play mode are verified. Milestone gameplay tests, Windows build, and standalone launch have not run yet.

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
| Version control | Local Git initialized | No remote publication; no global safe-directory change; checkpoint commit pending |
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

## Required logic validation — not run yet
Recipe matching and exact integer quantities; once-only ingredient consumption; unsupported input recovery; negative/invalid transaction rejection; grower/siphon/tank capacities; incubator occupancy; once-only sale and upgrade; insufficient funds; retained discoveries; stable specimen IDs; save/load round-trip during incubation and after sale.

## Required milestone interactive validation — not run yet
Fresh-start full loop, all three discoveries and distinct animations, visible/functional siphon upgrade, full-tank handling, pause/cursor behavior, sensitivity/audio controls, collection feedback, and confirmed reset. Automated checks cannot establish that harvesting feels enjoyable.

## Build / player — not run yet
A Windows build attempt and a separate player launch remain required. Editor compilation or Play mode will not be treated as standalone build evidence.

## Evidence recording rule
Append the exact command/tool or interaction, execution time/context, observed result, and saved log/screenshot path after execution. Label source inspection separately from executed tests. Do not replace a blocked or unrun check with a guessed pass. No frame-rate claims without actual machine/context and measurement.
