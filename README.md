# Evolution Shop

A first-person Unity game about growing aquatic creatures: you start with one small tank, two cultures and ten credits in a
small shop with a back-room lab, and grow it through five eras into a big shop with a lab full of machines, discovering 72
species in nine families by mixing cultures and breeding, until the Grand Exhibition. Serve the customers yourself at first;
helpers, power and pipes take over the routine you already know.

## Run and play

Run the newest validated build in `Builds\` (its folder name ends in the task and run, for example
`Windows-6000.3.21f1-gr17-one-game-1\EvolutionShop.exe`). A new shop starts with a guided first night; skip it any time from
the pause menu.

| Control | Action |
|---|---|
| WASD / mouse | Move / look |
| Hold the left mouse on a grower | Collect a culture into the siphon |
| Hold the left mouse on the counter | Bag a sale |
| Hold the left mouse on the crank or a hand pump | Power it |
| E | Open the station under the reticle; in peek, take the creature under it |
| Look closely at a tank's glass | Peek at each creature (switch it off in the pause menu) |
| Tab | The journal |
| Esc | Close a card, or pause |

The pause menu has the comfort settings (Peek, Reduced motion, mouse sensitivity, Sounds and Hum), Start over and quit. The
game saves by itself to `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\growth-gr02.json`. A save
from the removed classic game (`evolution-shop.json` there) is left as it was and never read.

## Source

Open `C:\dev\DumbVideoGameStudio\EvolutionShop` with Unity **6000.3.21f1**, then **Evolution Shop > Play the game**. Read
**`AGENTS.md`** before continuing development: it holds the rules of work, the game's specification (GR) and the task board.
Validation evidence is under `Docs/Evidence/gr-*`. The old (classic) game was removed at GR-17 (2026-10-28); its records are
in `Docs/Archive/`, and its last commit is tagged `old-game-last`.
