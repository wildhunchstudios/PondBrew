# Plans: measuring the walkways (GR-34)

The development route `-evolutionGrowthPlansSmoke` (Runtime/Growth/GrowthPlansSmoke.cs) builds the shop with everything bought and writes, into its evidence folder, `1400-plan-view.png` (from above, ceilings off), `1401-plan-obstacles.png` (5 cm cells, white where a collider stands between 10 cm and 1.8 m) and `plan.json`.

- `node clearance.cjs <folder> "$(cat rooms-now.json)"` measures each room from just inside its door: `clearance.json`, `clearance.png`, and `ways.png`, the walkways' centre lines over the view coloured by width (green 1.2 m or more, amber 0.6 to 1.2 m, red under 0.6 m, the player's 0.6 m capsule).
- `K=1.25 node proposed.cjs <folder> <out>` makes the proposed plan from the measured one: every room scaled by K about the shop's centre, each piece moved with its room (keeping its distance to the wall it stands against), the doors at least 1.4 m, and the second lab; then measure `<out>` with `clearance.cjs <out> "$(cat <out>/rooms.json)"`.

- GR-34 wave 1 (GR.79): the shop as built now, on the new plan, is measured with `rooms-gr34.json` (each room a quarter larger, the entry points just inside each door).
