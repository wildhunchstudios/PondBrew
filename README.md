# Evolution Shop prototype

A separate first-person Unity 6 URP game. **The Milestone 01 two-room layout is ready for the first human playtest.** The front store contains two display tanks and the sales counter. A doorway connects it to the back lab containing the Bloom/Flow growers, incubator, and upgrade bench. No later milestone has started.

The exact final revised Windows build succeeded with zero errors/warnings and passed **79 standalone checks**, with no captured runtime errors or limitations, exit 0. The two-room Editor run passed 78 checks before minor readout/HUD positioning changes; the final player covers those changes. The earlier 50 passing EditMode cases are historical and were not rerun for this layout-only refinement. See `Docs/VALIDATION.md` for evidence and limits.

## Play the Windows build

Run `C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe` and click **Open the shop**. For the `EvolutionShop-TwoRooms-Windows.zip` delivery, extract the complete archive first. Keep the executable beside its data/runtime folders; do not copy the executable alone. This is a Windows x64 development build with F8 comparison enabled.

## Open the Unity source

1. Open `C:\dev\DumbVideoGameStudio\EvolutionShop` in Unity **6000.3.15f1**.
2. Open `Assets/EvolutionShop/Scenes/Shop.unity`, enter Play mode, and click **Open the shop**.
3. If scene regeneration is needed, use **Evolution Shop > Rebuild playable scene**. This replaces the prototype scene with its repeatable runtime bootstrap.

| Control | Action |
|---|---|
| WASD | Move |
| Mouse | Look |
| Hold left mouse, aiming at grower | Collect culture into siphon |
| E, aiming at station | Open station; transfer ready creature at a tank |
| Tab | Open/close discovery journal |
| Escape | Close station/journal; open or leave pause |
| Mouse in panels | Dose, incubate, sell, upgrade, and change settings |
| F8, Editor/development build | Compare base/upgraded siphon after purchase; at most four cultures must be held/staged |

For the agreed layout, production happens in the back lab and display/sales happen in the front store, with the doorway connecting the loop. Start by collecting one Bloom and one Flow in the lab. At the incubator, add one dose of each and begin incubation. After ten seconds, press E at an empty display tank to transfer the specimen. Sell it at the counter. Discover the other two creatures and save 24 credits for the visibly larger, faster siphon. Unsupported mixtures can be returned safely. Both growers replenish for free.

The pause menu includes sensitivity, volume, save/resume, reload, confirmed new game, and save/quit. One local save preserves progression, active incubation, and settings; simulation pauses while closed. Default slot: `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\evolution-shop.json`. A malformed slot is preserved and automatic overwriting is blocked until a confirmed new game. Validation sessions use their own evidence-directory slot.

## Continue development

Read `AGENTS.md`, then `Docs/RESUME.md`, `Docs/TASKS.md`, `Docs/VALIDATION.md`, and `Docs/UnityProjectContext.md`. Preserve the original `Evolution_Shop_Prototype_Handoff.md`; the user-confirmed front-store/back-lab correction documented here updates its compact-room layout within Milestone 01. Source layout, tuning, setup routes, and validation commands are documented in those records.

No code or assets from Dungeon Project or PersonalAssistant are used. Visuals and short sounds are original procedural placeholders using Unity built-ins/URP. Names, recipes, and balance are provisional. Do not begin later milestones before the user's first playtest.

## First playtest feedback

Please judge movement/collection feel, readable interactions, creature differences, the upgrade's usefulness, and whether you want another production cycle. The two-minute first-creature and five-minute first-upgrade goals are unvalidated human timing hypotheses. Automated validation does not establish enjoyment. Audio is implemented but was not listened to; settings sliders were not manually dragged. Art and balance remain placeholders/provisional. No later milestones have started.
