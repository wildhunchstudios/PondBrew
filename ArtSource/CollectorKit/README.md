# Original culture collector kit

Original local Blender 5.0.1 work for Evolution Shop. No external models, textures, paid generation, uploads, or third-party assets. Existing project palette and authoring helper conventions are reused.

Two source-generated, consolidated, triangulated UV meshes replace the old syringe-like tool: a compact horizontal cream/teal motor with broad coral suction mouth, swept fins, angled mint grip and low readout; and a low saucer cap with a twist handle. Existing reservoir glass, culture cells, seals, upgrade band, nozzle effect, input and gameplay remain runtime-owned.

## Integration

- `CollectorBody.fbx`: ToolBody parent, identity position/rotation/scale. 6,692 triangles. Unity bounds X -0.179082..0.179082 m, Y -0.258736..0.164501 m, Z -0.179000..0.495001 m.
- `CollectorCap.fbx`: ToolChamber parent, identity position/rotation/scale. 1,196 triangles. Local X/Z +/-0.121001 m, Y 0.116999..0.178001 m. It inherits the existing chamber scale and offset. Its gasket intentionally overlaps the retained top seal; do not add the previous pump-crown translation.
- Existing live capacity text: ToolBody-local `(0,-0.129,-0.182)`, maximum width 0.135 m, full height 0.030 m. The new mesh contains a cream bezel and teal inset. Disable the old cube panel.
- Existing nozzle effect: ToolBody-local `(0,0.05,0.489)`, centered within the hollow mouth. Keep it.
- Disable old Siphon/SiphonCage/SiphonCrown, old procedural grip/base/barrel/collar and old readout panel. Do not remove the live vessel, cells or stable gameplay roots.

Root remaps RW_* opaque palette slots to the existing shared owned URP/Lit materials. This flat-color kit needs no textures or glass imports. Add no colliders or animation. Preserve generated Unity GUIDs once imported.

## Reproduction and evidence

Run `Tools/Art/create_collector_kit.py` in Blender background/factory-startup with `--python-exit-code 1`, followed by `-- --repo C:/dev/DumbVideoGameStudio`. Then run this folder's `validate_fbx.py` with the same explicit Python exit policy. No plugin/bridge is needed by these isolated local jobs.

`manifest.json` records source/export hashes, positive unit transforms, bounds, triangle counts, material slots, origins, integration and limitations. `fbx_roundtrip_validation.json` proves actual export/reimport equivalence: one mesh/UV layer per export, positive determinants, and bounds error below 0.00000002 m. Each chamber size had 30,164 body vertex/edge-midpoint/face-center probes with zero probes in open glass. This is a sampled exclusion check, not an exhaustive collision proof. `validation_attempt_01.json` preserves the earlier measured brace intrusion and correction.

`CollectorKit_ContactSheet.png` and `CollectorKit_MuzzleReview.png` are reviewed Blender source renders of base/upgraded assemblies, not screenshots from the game. Their non-exported reservoir/rim/cell proxies are labeled REVIEW_ONLY. The blend retains review positions; the generator exports at the documented original roots.

Actual Unity shader mapping, first-person visibility, capacity readout, collection effect, upgrade behavior and human visual acceptance remain integration checks. No LOD, performance or gameplay test claim follows from the asset-only checks.