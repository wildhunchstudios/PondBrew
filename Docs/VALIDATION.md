# Validation evidence

Updated: 2026-10-03. No compilation, automated test, Play-mode result, build, or standalone launch has been confirmed at this checkpoint.

## Environment / Checkpoint 00

| Check | Observed result | Evidence / limitation |
|---|---|---|
| Workspace | `C:\dev\DumbVideoGameStudio` exists; original handoff preserved | Initial filesystem inspection; new project child is `EvolutionShop` |
| Installed editors | 6000.3.14f1 and 6000.3.15f1 executable installations found | Session environment inspection; 6000.0.27f1 folder lacks executable and is not usable evidence |
| Windows modules | Windows Mono and IL2CPP support detected | Installed files inspected; no build executed yet |
| Editor connection | Initial Unity CLI status returned zero instances | No editor connection assumed; exact target still requires confirmation |
| MCP | No Unity MCP tools exposed in this session | Existing integration not available; no competing bridge installed |
| Project creation | Started via installed Unity CLI with URP blank template | In progress at this checkpoint; not a successful creation/compilation claim |
| Package manifest | Generated manifest includes URP 17.3.0, Input System 1.19.0, Test Framework 1.6.0 | Manifest file read; package resolution/import not yet confirmed |

Creation command:
```powershell
unity.exe projects new EvolutionShop --path C:\dev\DumbVideoGameStudio --editor-version 6000.3.15f1 --template com.unity.template.urp-blank --non-interactive --json
```

## Required logic validation — not run yet
Recipe matching and exact integer quantities; once-only ingredient consumption; unsupported input recovery; negative/invalid transaction rejection; grower/siphon/tank capacities; incubator occupancy; once-only sale and upgrade; insufficient funds; retained discoveries; stable specimen IDs; save/load round-trip during incubation and after sale.

## Required interactive validation — not run yet
Fresh-start full loop, all three discoveries and distinct animations, visible/functional siphon upgrade, full-tank handling, pause/cursor behavior, sensitivity/audio controls, collection feedback, and confirmed reset. Automated checks cannot establish that harvesting feels enjoyable.

## Build / player — not run yet
A Windows build attempt and a separate player launch remain required. Editor compilation or Play mode will not be treated as standalone build evidence.

## Evidence recording rule
Append the exact command/tool or interaction, execution time/context, observed result, and saved log/screenshot path after execution. Label source inspection separately from executed tests. Do not replace a blocked or unrun check with a guessed pass. No frame-rate claims without actual machine/context and measurement.
