# Evolution Shop: proposed live Editor connection

The existing Unity CLI 1.0.0-beta.6 is installed. Evolution Shop has no Pipeline/MCP package and the exact-project status query returns zero connected Editors. The existing batch test/build workflow remains available.

Proposed project-only change: add `com.unity.pipeline` pinned to `0.8.0-exp.1` in `EvolutionShop/Packages/manifest.json`, then let Unity resolve `packages-lock.json`. Keep Unity `6000.3.21f1`, URP `17.3.0`, Input System `1.19.0`, and Test Framework `1.6.0` pinned. No competing Unity bridge or MCP client registration is needed; use the already installed CLI.

The official package supports Unity 6000.0 or newer. Declared dependencies: Test Framework 1.1.33 minimum, Mono.Cecil 1.11.6, UIElements/JSON serialization/screen capture modules 1.0.0, Newtonsoft JSON 3.0.2 minimum. Existing compatible higher versions must remain. Resolution will be reviewed before acceptance.

This is experimental Editor control software. Version 0.8.0-exp.1 binds its server to 127.0.0.1 and authenticates requests; it also contains development-player automation/code-reload functionality. Production scenes, game scripts, player builds, and saves will not be changed as a setup test.

After approval: use the pinned installer for this exact project, open the pinned Editor, confirm project identity, read the current hierarchy and console, inspect the package/configuration diff, and run relevant compile/test validation. Do not regenerate or save the gameplay scene. Revert only the newly added integration on request, retaining evidence and any user changes.

This package change is pending because the supplied toolkit explicitly requires authorization for Pipeline's project-package changes. The rest of the toolkit setup can proceed independently.

Sources checked 2026-10-05: [Unity CLI](https://docs.unity.com/en-us/unity-cli), [replacement guidance](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli), [package registry](https://packages.unity.com/com.unity.pipeline). Reviewed official tarball SHA-1: `62c08c808737255821f3449466689a527a2eea50`.
