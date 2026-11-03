# Tester round 2: the compact list

From the tester build of 2026-10-31 (GR-28's second package), with the user's own two notes. The tester's own words are in `RAW.md`; the tester called the build not playable until these are fixed. Numbers go on from round 1's F1 to F57. "Now" lines are what the code does today, checked on 2026-11-03.

## Bugs (first, at the user's word)

- **F58 An emptied buffer keeps its culture.** Siphoned dry by hand, it still "holds" what it held, so another grower can't be piped into it, and selling it is the only way out. Selling also refuses until it is empty. Now: confirmed; collecting from a buffer never forgets its culture, and only Pour out does, when no line feeds it.
- **F59 Every device in the research room opens the desk's Pairs tab.** A jar should open the Jars tab, a tank its own view, the tray what waits on it. Now: confirmed; the desk, tanks, jars and tray all open the same card.
- **F60 A research tank's card shows no progress.** Only the timer face on the tank itself does.
- **F61 Cards rebuild whenever anything in the shop changes,** and long live lines break their title: Nori's card rebuilds each time Nori carries a creature, and "Nori, stocker" wraps under its status chip. The tester asks that no card do this. Now: confirmed; one signature covers every card, so a change anywhere (a creature moved, a credit earned) rebuilds the open card.
- **F62 Sale notes flicker at the top of the screen.** With Pip selling fast they jump from one to the next. Wanted: a short log at the side, the last five, each fading. Now: confirmed; one note at a time, each replacing the last.
- **F63 "Hazewing in your hands" on the Jars tab** looks out of place: it seems to come from nowhere, its icon far to the left and its button far to the right.
- **F64 "No way through: other pipes are in the way"** when piping buffers to the incubator, with no word of a limit. Wanted: every buffer can feed the incubator (through upgrades if need be). Now: confirmed; the incubator's port is one cell of the lab grid and pipes may only cross at right angles, never run side by side, so only a few lines can reach it. There are six buffer places.
- **F65 RP seems stuck** at 109, though each day's receipt seems to promise 50 more. Now: research points come only from discoveries; the receipt's "Reputation +50" is reputation, which makes stars. The HUD shows RP right under the stars, so the two read as one.
- **F66 Thin creatures' icons are unreadable** (Hazewing, Puffglider, Driftwing): drawn side-on, they show as a line. Wanted: a view that shows their shape, from above if need be.
- **F67 A decoration sits on the edge of its tank** (the duck on its stand, on the teal ledge) rather than in the water. Now: by design since GR-19, which the user approved in game: a tank's decor stands on its lid, at a back corner. Not a bug, so it moves to GR-36 with F85 (decorations that move), for the user's call.

## Space and production (the user's two notes)

- **F68 The rooms feel crowded and too small,** and once every upgrade and tank is bought some places can't be walked through. The tester's photo: no way between the machines in the power room. Wanted: rooms a little larger.
- **F69 Production is too slow late in the game.** With the incubator fully upgraded and always working, the tester could not keep even one room of tanks full. Wanted: buy more incubators and breeding tanks, perhaps in a new lab room bought and filled step by step.

## The research room

- **F70 The jars are not intuitive:** how to use them isn't clear.
- **F71 Buy jars and research tanks rather than get them all,** and allow many more than 15 jars and 3 tanks.
- **F72 Where a queued job's result goes.** A repeat leaves no creature, which is fine, but the tester wants to read what each job made, in the research room.
- **F73 Send a found recipe to the incubator,** as a quick recipe with a queue on the incubator, so it keeps making it.
- **F74 Everything tried, at a glance:** every mix and pair tried, anywhere, shown as a tree or map in the research room, so what is left to try is clear and encouraging.
- **F75 The research room opens only at night.**

## The incubator and the journal

- **F76 The incubator's card scrolls.** Wanted: a layout that fits without scrolling.
- **F77 Recipes in words** ("Bloom 1 + Flow 2") in the journal's tooltips. Wanted: colour dots, as on the quick recipes.
- **F78 The mix log and the pair log** are hard to read and can't be filtered.
- **F79 What makes a creature:** pressing a creature in the journal should show every mix and pair that makes it, which of them you have tried, and the ideal recipe in its tooltip.
- **F80 Emptying the siphon** belongs somewhere of its own, a bin, not the incubator's card.

## The shop, helpers and night

- **F81 Night-only work while the shop opens by itself:** laying pipes, opening rooms and the like should work during the day too. Now: upgrades work any time since round 1 (F33); walls, rooms, moving tanks, buffers and pipes stay night only.
- **F82 At most three visitors at once,** with no word of why. Now: two for the first unit, one more for each shop unit and the exhibition hall (up to five). Wanted: the game says so.
- **F83 The counter's "Open by itself" is unclear,** in its layout and its words ("it opens 15 s or more into the night").
- **F84 Nori's stock targets in a window of their own,** opened by a Manage stock button, with each creature's picture.
- **F85 Decorations should move:** the volcano's and the duck's bubbles are still.

## Interface and wording

- **F86 The lesson card's yellow note** is the same on every lesson. Wanted: drop it, with Skip lesson beside Got it.
- **F87 The main menu's Quit** should say Save and quit: testers fear their game isn't saved.

## What the tester liked

- Understanding that the research room's devices match the desk's tabs ("no problem with that").
- Not getting a creature from a repeat, as long as the result can be read.

## Status (2026-11-03)

Technically complete (GR.75): F58 to F66 (GR-33); F75 to F79, F81 to F84, F86, F87. Technically complete (GR.76): F72, F73, F80, F85 with F67. Technically complete (GR.78): F70, F74. Approved and in progress (GR.77): F68 and F69 (GR-34), F71 (GR-35).
