---
name: blender-to-unity-assets
description: Prepare and validate a local Blender asset for Evolution Shop, including scale, axes, pivots, materials, optional rigs and the actual URP import. Use for asset production handoffs, not paid generation setup.
---

# Blender To Unity Assets

Inputs: an approved bounded asset target, source/license, .blend/export files, intended station/creature ID and current AGENTS.md section 7 asset contract. Check Docs/tooling/INSTALL_REPORT.md for actual bridge status and Tools/Tooling/Start-Blender.ps1 for the isolated launch path. Do not assume an open Blender scene belongs to this project; identify it before mutation.

Work on a project-local copy or scratch scene. Keep paid providers, telemetry and external asset uploads disabled. The bridge's safe mode is only an MCP code filter, not an authenticated socket or filesystem sandbox. Use loopback, the configured project port and only task-owned processes. Never close or replace another open scene.

Use 1 Unity unit = 1 metre, Unity Y-up. Furniture root: floor centre, unit scale, operator front -Z. Characters: feet root, +Z front. Creatures: body centre, -Z front. Apply source transforms and adapt export axes on a named visual child once; never change stable station roots to compensate for imported art. Preserve .meta GUIDs of accepted assets. Do not add an unreviewed model importer to make an unsupported format work; the existing project has no assumed glTF importer.

Inspect geometry and face count, bounds at normal scale, normals/negative scale, UVs when textured, material slots and texture colour-space settings. Start with a shared opaque palette and a functional glass/emissive material only where needed; budgets remain measured decisions. Verify URP materials in the actual Editor and player, not Blender alone. Keep simple colliders on existing gameplay roots and decorative geometry free of colliders. Validate footprint, neighbour/aisle clearance, operator reach and placement orientations.

For rigs: inspect bone axes/root motion policy, deformation extrema, attachment points, loop seams and tank/actor bounds throughout the loop. For LODs: inspect transitions at actual screen size; do not claim optimized from triangle count alone.

Output: source/export hashes, import settings, origin/license, actual prefab/material references, scale/pivot/bounds evidence, render/runtime captures, relevant save/placement regressions and remaining creative acceptance. A scratch export proves connectivity only. Stop before production replacement if source rights, target asset, import compatibility or required human art approval is missing.
