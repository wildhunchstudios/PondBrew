# Evolution Shop prototype scope

Source of truth: `../Evolution_Shop_Prototype_Handoff.md` (2026-10-03). This summary preserves its Checkpoint 00 and Milestone 01 boundaries.

## Experience to test
Grow living cultures, combine them into one creature, display and sell it, then buy an improvement that makes the next creature easier to prepare. First-person 3D, one compact lab/shop room, Unity 6 URP/C#. Aquatic theme and all names/balance below are provisional.

## Required first playable
- Two free, capacity-limited growers: Bloom and Flow. Distinguish both by label, silhouette, and color.
- Hold-to-collect siphon, organisms moving into a visible transparent reservoir, responsive sound/motion, measured integer dosing.
- One incubator; one specimen per cycle; unsupported mixtures identified and recoverable without resource loss. Exactly-once resource consumption and output creation.
- Three species, each with a distinct smooth shape and short procedural swim/idle movement:

| Stable concept | Bloom | Flow | Presentation | Provisional sale |
|---|---:|---:|---|---:|
| Pondlet | 1 | 1 | Teardrop body and simple tail | 6 |
| Bubblebelly | 2 | 1 | Rounded body and short fins | 10 |
| Ribbonfin | 1 | 2 | Long narrow body and flowing tail | 10 |

- Exact quantities, not ratios. No implicit batching. Journal records discoveries permanently and enables convenient known-recipe reproduction.
- Two visible display tanks, forgiving transfer, bounded swimming. Reserve capacity before consuming an output; clearly block full destinations.
- Stationary placeholder buyer/sales counter: sell one actual chosen creature once for currency.
- One upgrade station: siphon capacity four to eight, visibly larger chamber, faster collection, development-only before/after comparison.
- Clear currency, carried cultures, machine readiness, objective, and interaction prompts. Comfortable movement; pause menu, sensitivity, and sound settings.
- One local save slot; explicitly confirmed reset. Save currency, cultures, discoveries, owned specimen IDs/tank placement, upgrade, and active incubation. Simulation pauses while closed.

## Provisional tuning
Zero starting currency; free replenishment every two seconds; ten-second incubation; upgrade cost 24. Centralize tuning. First creature within about two minutes and first upgrade within about five minutes are hypotheses for playtesting, not measured claims.

## Acceptance and excluded work
Acceptance requires a repeatable full loop, visually distinct discoveries, perceptible upgrade, no known critical progression blocker, and honest validation notes. User playtest evaluates feel.

No combat, jumping puzzles, large exterior, inventory grid, construction editor, economy simulation, workers, automatic production, navigation/queues, multiplayer, complex genetics, chemistry, campaign ending, offline rewards, login, cloud, or paid acceleration. Later milestones remain deferred until first playtest.

## Assets
Prefer original procedural placeholders and built-in capabilities. Record any external asset source/license before use. No assets from the user's other projects.
