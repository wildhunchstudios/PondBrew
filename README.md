# Evolution Shop prototype

A separate first-person Unity 6 URP game. **Milestone 01 is ready for the first human playtest.** Checkpoint 00, 50 EditMode tests, a 70-check Editor Play-mode session, and the final 73-check Windows player session passed. The final Windows build succeeded with zero errors and zero warnings. No known critical progression blocker was found. See `Docs/VALIDATION.md` for evidence and limits.

## Play the Windows build
Run `C:\dev\DumbVideoGameStudio\Builds\Windows\EvolutionShop.exe` and click **Open the shop**. Keep the executable beside its data/runtime folders; do not copy the executable alone. This is a Windows x64 development build with F8 comparison enabled.

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

Start by collecting one Bloom and one Flow. At the incubator, add one dose of each and begin incubation. After ten seconds, press E at an empty display tank to transfer the specimen. Sell it at the counter. Discover the other two creatures and save 24 credits for the visibly larger, faster siphon. Unsupported mixtures can be returned safely. Both growers replenish for free.

The pause menu includes sensitivity, volume, save/resume, reload, confirmed new game, and save/quit. One local save preserves progression, active incubation, and settings; simulation pauses while closed. Default slot: `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\evolution-shop.json`. A malformed slot is preserved and automatic overwriting is blocked until a confirmed new game. Validation sessions use their own evidence-directory slot.

## Continue development
Read `AGENTS.md`, then `Docs/RESUME.md`, `Docs/TASKS.md`, `Docs/VALIDATION.md`, and `Docs/UnityProjectContext.md`. The original `Evolution_Shop_Prototype_Handoff.md` remains authoritative. Source layout, tuning, setup routes, and validation commands are documented in those records.

No code or assets from Dungeon Project or PersonalAssistant are used. Visuals and short sounds are original procedural placeholders using Unity built-ins/URP. Names, recipes, and balance are provisional. Do not begin later milestones before the user's first playtest.

## First playtest feedback
Please judge movement/collection feel, readable interactions, creature differences, the upgrade's usefulness, and whether you want another production cycle. The two-minute first-creature and five-minute first-upgrade goals are unvalidated human timing hypotheses. Automated validation does not establish enjoyment. Audio is implemented but was not listened to; settings sliders were not manually dragged. Art and balance remain placeholders/provisional. No later milestones have started.
