# Evolution Shop tooling installation report

Checked 2026-10-05. User request: preserve the current game and Unity integration, prioritize Unity/Blender/Figma/Context7 and scoped skills, verify connections, and leave paid generation disabled. The supplied toolkit was reviewed as proposed setup guidance. Its command examples were not executed wholesale.

## Preserved baseline

- Repository `C:\dev\DumbVideoGameStudio`, clean at `927576db718b511db21ea804c6f94d6087548e9f` / `r06-5-cozy-lab` before setup. The task chat itself is projectless under Documents/Codex; that does not make it the game project.
- Unity `6000.3.21f1` (`c02631ffc030`), URP `17.3.0`, Input System `1.19.0`, Test Framework `1.6.0`, AI Navigation `2.0.12`. Current HUD is IMGUI `OnGUI`; installed uGUI is not evidence of a uGUI HUD. Runtime/Core/Editor and three test assembly boundaries retained.
- Approved direction remains cozy, colorful cartoon front shop and quirky cartoon science lab. R06.5v1 is delivered; human revised-look/audio acceptance remains open. No later milestone was started.
- No gameplay, scene, prefab, GUID, Unity version, package manifest/lock, existing build or normal save change was needed. Dungeon and PersonalAssistant were not modified or imported.

## Status by component

| Component | Installation/configuration | Actual operation / remaining state |
|---|---|---|
| Unity CLI 1.0.0-beta.6 | Already installed; preserved | Version/help work. Exact-project live status reports zero instances. No existing Unity bridge/package found. |
| Existing Unity batch workflow | Preserved, pinned Editor | Existing guarded `EnsurePlayableScene` inspected the existing scene and exited 0, logging exact project/version. No new tests or build were run. |
| Unity Pipeline 0.8.0-exp.1 | **Pending approval; not installed** | Exact package/dependencies reviewed. Proposal is `UNITY_CONTROL_PROPOSAL.md`. No new Unity MCP server or competing bridge registered. |
| Blender 5.0.1 + mcp-for-blender 2.1.6 | Pinned project environment; isolated session addon | **Verified:** MCP initialized/discovered tools and read actual scratch scene (Cube/Camera/Light). Safe-mode test rejected `import os`. Loopback listener `127.0.0.1:19876`; telemetry and providers off. Session PID 7692 clean-stop requested and process disappearance observed. |
| Context7 remote MCP | One project configuration; no npm/CLI duplicate or key | **Verified:** anonymous initialize, tools/list, library resolve and documentation query, HTTP 200; server identified 4.1.1. Version coverage limitation below. |
| Project MCP activation | `.codex/config.toml` installed | **Pending project trust/activation and fresh session.** Explicit `codex -C C:\dev\DumbVideoGameStudio mcp list` did not load these project servers; no explicit project trust entry exists. Trust was not silently added. Direct protocol probes do not establish native tools in this current chat. |
| Figma official plugin 15.0.0 | **Installed/enabled**, CLI receipt and catalog confirmed; 14 bundled skills | **Pending authentication/refresh and permitted design read.** No native Figma tools available in this turn; anonymous remote endpoint returned 401. This does not prove the user's account lacks an existing Figma login. No design content was read/written/uploaded. |
| Unity workflow bundle 0.1.3 | Already installed; preserved | Onboarding, health, feature, bug, validation and MCP guidance present. Bridge setup text is older than current Unity CLI docs; current sources govern provider setup. |
| Project-local skills | **6 authored + 1 official Unity CLI skill** | All 7 passed the bundled format validator. Behavioral/full-game quality is not established by this validator. Available when working in this repository; refresh next turn if necessary. |

## Selected setup and evidence

The six new skills are `game-art-direction`, `blender-to-unity-assets`, `unity-game-ui-ux`, `game-research-evidence`, `game-economy-simulation` and `game-session-handoff`. No prior project `.agents` or `.codex` configuration existed to merge. They reference the canonical AGENTS.md instead of creating another roadmap. They encode the real IMGUI framework, local art/axis/footprint rules, economy ownership, measured validation, and pending human gates.

The existing Unity CLI installed its embedded `unity-cli` skill with the verified `codex --local` target. No CLI update was performed. The Blender environment uses Python 3.12.14 and exact package/dependency versions in `Tools/Tooling/requirements-lock.txt`; PyYAML 6.0.3 was added solely for the bundled skill validator. The initial validator attempts lacked PyYAML and did not run; all seven were rerun successfully after resolving it.

Blender's older global `addon.py` (1.2) was found through the bridge's startup diagnostic after the initial filename-based inventory missed its generic name. Its bytes and global preferences are preserved. It was not running. This project uses the same provider's reviewed 2.1.6 bundled addon in an isolated factory-startup session, not a competing provider or a second simultaneously running server. The server warns about the old global file, then successfully handshakes with the current session's protocol 13. Do not run the global and project sessions as interchangeable targets.

The Blender bootstrap cancels upstream auto-start/update callbacks, enables no paid/download providers, saves no preferences, and explicitly binds loopback. The Codex allowlist exposes only `get_scene_info`, `execute_blender_code`, and `look`, excluding generation/catalog/premium/telemetry opt-in tools. `BLENDER_MCP_SAFE_MODE=1` and `DISABLE_TELEMETRY=true` are explicit. Safe mode is not a filesystem sandbox or authentication for the underlying local socket. No asset export/import, production Blender file, Unity prefab round-trip or art acceptance is claimed.

Context7 matched `/unity-technologies/inputsystem` and returned primary-source `develop` branch pages for a request about 1.19.0. It did **not** establish exact 1.19.0 coverage. The locally installed `com.unity.inputsystem@21a28c3a6c83/package.json` confirms 1.19.0, and its `Documentation~/Actions.md` contains the enable-action guidance. Use installed/exact publisher docs for version-dependent implementation. Only generic public documentation queries were sent; no project source, images or assets were uploaded.

The Figma marketplace resolved 15.0.0, different from the public GitHub manifest (2.0.20) used in initial review. The actual installed manifest was subsequently inspected: official Figma connector, bundled skills, no declared lifecycle hooks. No duplicate direct Figma MCP registration was added. A private backup of existing Codex configuration is outside the repository at `.codex/config.toml.evolution-toolkit-20261005.bak`; never copy it into evidence or Git.

Unity's exact batch check logged a nonfatal licensing access-token refresh error, then completed scene inspection and exited 0. It is not an error-free Console claim, test pass, Windows rebuild or live hierarchy/console connection proof. The initial sandbox CLI doctor could not read the account store; an initial Git hash script hit ownership protection and was rerun with scoped authorized access, without altering global Git safety settings. A sandbox registry check hit SSL failure; scoped retry retrieved the official package list. Raw successful evidence and these limitations remain recorded.

## Reviewed and skipped

- **Superpowers:** upstream `8ca22d...` session-start hook injects its broad workflow; current official Codex manifest 6.3.0 declares empty hooks. No hook/full methodology was enabled. Installed Unity workflows and the small project handoff/review instructions already cover the current need. No repeated discovery, mandatory large planning ritual or automatic cleanup was introduced.
- **Creative Production:** native image generation and installed art skills cover current work; no missing integration justifies another bundle yet.
- **Impeccable / Playwright / Browser Game Studio:** no current browser dashboard, WebGL or web UI assignment. Existing browser tools remain available; no engine or frontend migration.
- **GitHub:** Git works; `gh` was absent on PATH. This assignment needs no remote issue/PR/CI access, so no connector, publication or credentials were added.
- **Exa:** native web research plus Context7 meets the concrete need; no redundant search provider.
- **Meshy / paid providers:** skipped and generation disabled pending API access, external-upload consent and a budget. No chargeable job, purchase or external asset upload occurred.
- **Game Development Studio / Build 3D Game Rooms:** existing skills retained. `game-dev` was not available on PATH, so CLI/adapter/provider workflows remain **blocked/unverified**. No guessed installer or paid Meshy dependency activated.

## Next executable steps

1. Open/trust this exact repository in Codex if desired, then start a fresh turn and confirm project MCP discovery. Do not broaden trust to unrelated folders. Figma may need Plugins â†’ Figma â†’ Connect/Authenticate; then verify by reading a user-designated file, with no test write.
2. The Pipeline approval question remains pending. If approved, apply only the reviewed version/package changes, preserve all current pins, open the exact Editor, verify identity/hierarchy/console, inspect resolved dependencies and run relevant validation. Do not install Coplay or another bridge alongside it. The supplied toolkit explicitly requires authorization for this package change; elapsed time is not approval.
3. For local Blender work, run `Tools/Tooling/Start-Blender.ps1`, reverify scene identity, and stop only the owned session with `Stop-Blender.ps1`. The read-only MCP probe is `probe_blender.py`, run using the pinned venv Python. The session is stopped at handoff; installed is not currently running.
4. Resume game work only from AGENTS.md: obtain/refine feedback on the revised R06.5 rooms and audio. No tooling installation closes that creative gate or starts R07.

Final preservation check: 353 existing tracked files were byte-identical; the only five changed baseline files are .gitignore and the four requested project records. The normal save, older global Blender addon and current CozyLab ZIP hash are unchanged. The authored-file whitespace check passed after removing an extra helper-script EOF line; raw Unity logs and upstream embedded Unity skill references retain their original whitespace warnings.

Evidence: `Docs/Evidence/toolkit-2026-10-05/`. Machine-readable selection, versions, paths and rollback notes: `toolchain-lock.json`. Verification receipts record actual status; no new full suite/build or Unity asset round-trip is claimed.

## Primary sources reviewed

- [Unity CLI](https://docs.unity.com/en-us/unity-cli), [replacement guidance](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli), [Pipeline](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package), [official package registry](https://packages.unity.com/com.unity.pipeline).
- [MCP for Blender source](https://github.com/ahujasid/mcp-for-blender/tree/46a9bf467a2470bad8f10391dd271af9872db551) and [PyPI 2.1.6](https://pypi.org/project/mcp-for-blender/2.1.6/).
- [Context7](https://github.com/upstash/context7), [Figma MCP](https://developers.figma.com/docs/figma-mcp-server/), [official Figma plugin source](https://github.com/openai/plugins/tree/5fd93af4cd0c623e020d0cc7e9ce178b4ac1f70f/plugins/figma).
- [Codex MCP configuration](https://developers.openai.com/codex/mcp/), [repository-local skills](https://developers.openai.com/codex/skills/), [Superpowers source](https://github.com/obra/superpowers/tree/8ca22dba9a94f28898bbce59f2537ff4d87c747d).


## FX-06 concrete media tooling — 2026-10-11

Installed only the pinned imageio-ffmpeg0.6.0 Windows binary under ignored `.tooling/fx06-media`; FFmpeg7.1 version and wheel/binary SHA256 verified. It encoded18 original local Unity motion videos; all18 fully decoded with2,490 frames, correct resolution and upright HUD, then loaded successfully in the local IAB review page. Receipts: `../Evidence/fx-06/media-tool.json`, `media-validation.json`, `browser-review.json`. No global pip/PATH, Unity package, engine upgrade, bridge, paid generation or external upload.

The exact pinned Unity project was freshly connected through the guarded batch Editor probe, then2,060 EditMode tests,266 Editor runtime assertions and a separate build succeeded. There is no active live Editor/MCP connection; installed Unity control was preserved. Blender/Context7 and optional providers were skipped for this diagnostic-only assignment; Figma authentication remains historical/pending, not freshly verified. Raw sound decoding/browser playback availability does not establish actual listening or heard in-game mix.
