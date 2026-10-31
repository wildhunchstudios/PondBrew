# Tester round 1: the compact list

From the tester build of 2026-10-30 (GR-28). The testers' own words are in `RAW.md`. "Now" lines are what the code does today, checked on 2026-10-31; "likely" marks a cause not yet reproduced.

## Research and experimenting (where the tester stopped playing)

- **F1 Research room (the main request).** A dedicated room to develop and research creatures.
- **F2 Testing mixes gets tedious.** There is no comfortable way to try culture combinations as the game goes on. The tester stopped playing at this point.
- **F3 No help with breeding.** Choosing pairs feels like guessing.
- **F4 Breeding means waiting.** Each breed takes 40 s (60 s at level 1) while the player stands and watches. Wanted: plan several pairs and let them run in the background.
- **F5 How to make a creature.** Hovering a discovered creature in the journal should show the best recipe or pair for it.
- **F6 Mixes already tried.** The incubator should show a small list of them. Testers keep repeating mixes by accident.
- **F7 "Already tried" hint.** When a mix is a repeat, say so and name what it makes. Keep "this mix is something new" as the Microscope upgrade: the tester called it a great upgrade and now prefers earning it.
- **F8 Founders from breeding.** Getting a founder out of breeding feels wrong. Now: a pair breeds the non-crown species nearest its average breeding value, and founders count (GR.7.2).
- **F9 Mix log spoils the result.** Now: the result is logged when Start is pressed, before the creature hatches.

## Dead ends and bugs

- **F10 Incubator dead end.** With the chamber and the siphon both full, doses can't go back and nothing can discard them. Now: confirmed in `ReturnDose`.
- **F11 Start blocked with no visible reason.** It later works without explanation. Likely: Start is only blocked while a hatchling waits in the tray, and the reason shows only on hover. Any mix can start, so "only red" wasn't the cause.
- **F12 Tank dead end.** With a full tank and full hands, the tank can't be opened to swap anything, so the player is stuck until a sale, and at night no sale can come. Now: confirmed; E on a tank always tries to put a creature in while carrying.
- **F13 No choice of what to place.** Carrying several creatures, E places one the player didn't choose.
- **F14 E takes when it should place.** Now: confirmed; within peek range (1.6 m), E takes the creature in view, even while carrying.
- **F15 Incubator card scrolling jumps.**
- **F16 Siphon vial disappears after using the incubator.** To reproduce.
- **F17 No way to empty the siphon.**
- **F18 No way to empty a buffer** except pushing it into the incubator and making creatures nobody wants.

## Controls and movement

- **F19 Separate click from E everywhere.** Tester's proposal: click for quick actions (take, put, fill), E to open a station's menu. Clicking the incubator tips in the whole siphon; E opens it to set the ratios and press Start; whatever wasn't used is discarded.
- **F20 Incubator store (tester's "maybe").** Click fills a store beside the incubator, doses are chosen from it, and upgrades make it bigger.
- **F21 Item bar.** Show what the player is carrying (tester's suggestion for F16; it would also solve F13).
- **F22 No run key.** Now: walking is a fixed 3.2 m/s.
- **F23 Movement feels faster near machines and tanks.** Likely the peek zoom: within 1.6 m of a tank the view narrows from 68° to 45° in 0.25 s, while walking speed stays the same.
- **F24 Can't walk behind the counter.** To check what blocks it.
- **F25 No highlight on the creature under the pointer** when taking creatures back from a tank.

## Incubator and upgrades

- **F26 Choose the three quick recipes** instead of having them picked for you.
- **F27 Quick recipes should fit the siphon** and show what can be made from what it holds now.
- **F28 Upgrades as before and after.** Show numbers with an arrow (12 s → 8 s, mixes 3 → 4), not just the new value in a sentence.

## Shop and customers

- **F29 Clear moment to bag a sale.** The counter animation and wrapping are unclear. Show that the customer has arrived and bagging can start, rather than waiting for a button to appear.
- **F30 Wish and purchase look the same.** The bubble over a customer reads as what they bought. Add an icon floating over the bag for the creature actually bought.
- **F31 More silly customer interactions.** Testers love the customers and want more to do with them.
- **F32 Auto-open.** Open the shop by itself once stock reaches a chosen level.
- **F33 Upgrades while the shop is open.** Now: upgrades, building and orders are night-only.

## Helpers and automation

- **F34 Nori looks idle.** Now: Nori only moves species below their stock target, and nothing says why Nori is waiting. Nori does reach both shop units (the tester confirmed it).
- **F35 Nori's job settings need a table** instead of a long scroll.
- **F36 Moss has no clear purpose.** The tester had the incubator's auto-doser and a generator first, and Moss's recipe job overlaps with the auto-doser.
- **F37 Sorter rules.** Sort by calm, school or water type, or add push and pull rules.
- **F38 "Other pipes are in the way"** doesn't say what to do about it.

## Explanations and naming

- **F39 Cultures aren't understood.** Bloom, Flow and the rest didn't read as genes.
- **F40 The colours explanation** wasn't understood.
- **F41 The supplier catalog explanation** is the most confusing one.
- **F42 The research bench** doesn't make clear that the siphon is upgraded there.
- **F43 "Pump crank"** is unclear and hard to find. Say that it's on the front of the grower.
- **F44 "Lasts 5:30"** has no unit.
- **F45 "Clutch"** reads as a chance. Now: a fixed number from the incubator's level (2 at level 3, 3 at level 4).
- **F46 Rename "Back room" to "Laboratory".** Note: the "Lab annex" is a separate room, so it may need a new name too.
- **F47 Decorations aren't labelled as decorations** among the other catalog items.
- **F48 "Show me" feels odd on a lesson.** Offer Skip lesson and nothing more.
- **F49 "Era" capitalisation varies** between screens, for example on the pinboard.

## Interface

- **F50 The journal stops the clock,** which surprised testers. Now: it pauses the shop on purpose.
- **F51 Simpler siphon display.** A bar of colours instead of text, bottom right.
- **F52 The scrollbar looks like Windows XP.** Restyle it.
- **F53 Resolution as a dropdown.**
- **F54 Each creature's own icon** in the journal and the breeding tank, instead of a generic fish.

## Sound and art

- **F55 More sound effects.** The incubator's finish sound is unpleasant, and it has no running sound. The siphon has no draw sound.
- **F56 The tank in the middle of the shop** should be see-through on all sides.
- **F57 More ordinary decorations.** The current pieces read as odd.

## What testers loved (keep)

- The customers, and being able to slap them.
- Colours.
- The Microscope's "this mix is something new".
