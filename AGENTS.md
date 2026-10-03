# Evolution Shop — working instructions and complete-game roadmap

Updated: 2026-10-03. Working title only. Current source checkpoint: tag `r01-baseline-safety`. Latest delivered player remains `milestone-01-two-rooms` until the next build is validated.

## 1. Read this first

This is the single current guide for project rules, product direction, milestones, task status, validation summaries, and continuation. On this Windows workspace, `Agents.md` resolves to this existing `AGENTS.md`; do not create a competing file with different capitalization. Keep the canonical uppercase filename for agent discovery.

The user approved the front-store/back-lab direction and requested this full-game plan. The user has now requested implementation of the next ready assignment. Work through the bounded assignments below; this is not authorization to implement the entire roadmap in one turn. Future implementation requests select a bounded milestone. A request to continue can start the next ready milestone without repeated permission questions about routine decisions.

This guide supersedes the original brief's single-room layout, its old M02/M03 roadmap, and the old requirement to maintain several parallel planning files. Preserve `Evolution_Shop_Prototype_Handoff.md` and existing `Docs/TASKS.md`, `Docs/RESUME.md`, `Docs/UnityProjectContext.md`, and `Docs/VALIDATION.md` as historical records and evidence. Read them when investigating prior implementation or results; their old scope/status statements do not override this newer plan. Store raw logs, reports, and captures under `Docs/Evidence/`, and summarize new work here. Do not create another live roadmap or task dashboard.

User instructions take precedence over this guide. Separate **user requirements**, **recommended design defaults**, and **verified implementation**. A plan, generated source, or attractive screenshot is not proof of a working feature.

## 2. Project identity and protected boundaries

| Item | Current verified baseline |
|---|---|
| Approved workspace / Git root | `C:\dev\DumbVideoGameStudio` |
| Unity project | `C:\dev\DumbVideoGameStudio\EvolutionShop` |
| Editor pin | Unity `6000.3.15f1` (`c1aa84e375f6`) |
| Packages | URP `17.3.0`; Input System `1.19.0`; Test Framework `1.6.0` |
| Editor executable | `C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe` |
| Playable scene | `EvolutionShop/Assets/EvolutionShop/Scenes/Shop.unity` |
| Latest built player | `Builds/Windows/EvolutionShop.exe`, Windows x64 development/Mono |
| Local save | `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\evolution-shop.json` |
| Scene generation | `EvolutionShop.Editor.ProjectSetup.CreateScene`; menu **Evolution Shop > Rebuild playable scene** |
| Runtime smoke / build | `EvolutionShop.Editor.ProjectSetup.RunSmoke` / `BuildWindows` |

- This is an independent game. Never modify or import files, code, assets, history, or settings from Dungeon Project, Dungeon Roguelite, or PersonalAssistant.
- Work within the approved workspace. Preserve user work and saves. Do not reset history, delete unrelated files, change global settings, or publish remotely.
- Use ordinary workspace permissions. If the tool sandbox requires escalation, request only the specific authorized path/action. Never seek unrestricted access or work around a denied operation.
- No asset purchase, paid generation, account/backend, engine upgrade, plugin installation, or external publication is implied by this roadmap.
- Before Unity work, inspect the installed executable, project version, modules, processes, and exact project path reported by the editor/connection. An old MCP status file is not connection evidence. Verify identity before editor mutations; retain the existing exact-project guard.
- Documentation-only work does not require launching Unity or claiming an active editor connection. Verify runtime identity again when implementation resumes.

## 3. Product contract

### User requirements

1. Separate physical rooms: a customer-facing front store and a production/research lab behind it.
2. A day/night rhythm: develop and grow creatures at night; personally work in the store during the day, especially at the beginning.
3. Store progression: expand, buy display tables/tanks, decorate, hire workers, attract more customers, and earn higher prices through a better business.
4. Lab progression: expand, improve equipment, unlock more culture/creature types and production processes, and ultimately automate routine work.
5. Deliberately silly interactions: cartoon customer slaps, funny noises, absurd spoken reactions, farts, and other short comic incidents.
6. A satisfying main ending. Collecting everything is a possibility, not an approved mandatory ending.

### Intended experience

Start as a hands-on creature grower and shopkeeper. Learn what customers want, discover strange aquatic creatures, improve the two rooms, and gradually become the designer of a functioning business. Automation should remove mastered repetition while leaving discovery, planning, presentation, pricing, and optional personal service worth doing.

The daily loop is **plan and grow at night → display and sell by day → review results → invest in store or lab → try something new**. Each cycle should produce a meaningful decision or a visible improvement, not just another round of compulsory refilling.

Moonlighter is the user's reference for the two-part shopkeeper rhythm. Its publisher describes a shopkeeper/adventurer duality; this game substitutes laboratory discovery for adventure. Do not import its characters, art, combat, or dungeon scope. Reference: [11 bit studios — Moonlighter](https://11bitstudios.com/games/moonlighter/), checked 2026-10-03. The concrete systems below are original recommendations for this project.

## 4. Recommended ending: the Grand Exhibition

**Design default, open to user revision:** earn the opportunity to host a Grand Exhibition in the expanded store. Prepare a curated living collection, show that the lab can reliably support the shop, and complete a special trading day. Finish with a short celebration, a visible shop award, and a results page; then continue the same save in free play.

This ties together discovery, store quality, service, staffing, and automation. A collection counter alone would reward only one part of the game and could make the final hours depend on repetitive or lucky discoveries.

Provisional implementation contract:

- The objective board shows progress from the first few days; the ending must not appear as a surprise late requirement.
- Unlock the event after reaching the final planned store and lab tiers, discovering 9 of the proposed 12 species across all three families (including the three signature species), and completing one ordinary shift with a working automated production-to-sale route. Identify those signature targets early on the objective board.
- Prepare three designated signature species, one from each family, in protected exhibition displays. Their acquisition is deterministic through research clues and known processes; they cannot be accidentally sold by a worker.
- Prepare for six clearly listed exhibition purchases using species already unlocked. These are preannounced ordinary customer purchases through the existing queue and checkout, not a new quest-delivery system. Targets and counts are tunable; the event introduces no new service mechanic that day.
- Complete all six purchases while keeping the three exhibition specimens on display. Failures affect only this attempt: retain upgrades, discoveries, remaining unsold stock, and legitimately earned money; release unpaid reservations. Completed sales stay committed: never restore sold specimens while retaining their payment. Show why the attempt failed and allow another preparation night. No campaign restart or irreversible failure.
- Completion is awarded once and survives save/load. Free play keeps the upgraded shop and lab. Discovering all 12 species, optional customer requests, alternate layouts, and efficiency challenges are extra goals, not prerequisites for the main ending.

Validate this goal with a rough, shortened playable version before building all final content. If business-oriented players find it too collection-heavy, reduce the discovery requirement; if collectors find business steps excessive, shorten those gates. Preserve a clear finish rather than expanding requirements indefinitely.

## 5. Bounded launch scope

The following numbers are **planning ceilings and tuning defaults**, not promises or measured balance. Change them with evidence and record the decision here before multiplying content.

| Area | Proposed first finished release |
|---|---|
| Platform | Offline, single-player Windows PC, keyboard/mouse; one shop and adjoining lab |
| Room growth | Three store tiers and three lab tiers, including the starting tier; modular extensions of the same location |
| Discoveries | 12 visibly distinct aquatic species in three families, including the existing three; four culture types total |
| Production | Culture growing, incubation, and one later conditioning process; a small set of upgrades and automation modules |
| Shop furniture | Small/large display tables or tank stands, counter, storage; approximately eight reusable décor items |
| Customers | Four behavior archetypes with shared navigation/transaction systems and varied presentation |
| Employees | Three roles: cashier, stocker, lab technician; at most one active employee per role initially |
| Humor | Six short reusable incident types with cooldowns and reaction variants |
| Campaign | One progression path and one Grand Exhibition ending; optional continuation afterward |
| Content standard | One approved visual/audio sample before bulk production; readable, coherent assets rather than many rough variants |

Out of scope unless the user expands the contract: multiplayer, combat/dungeons, another explorable town, several shops, land-animal systems, realistic water chemistry, creature death/feeding chores, complex genetics, breeding pedigrees, random shiny hunting, conveyor physics, loans/debt simulation, offline income, real-time waiting, subscriptions, cloud accounts, and paid acceleration. Decorations and lab machines must use the same small placement system rather than separate editors.

## 6. System specifications and design rules

### 6.1 Day and night

- Begin a new game with a guided preparation night. Use the existing simple recipe to prepare the first shop stock, then let the player open the store.
- Model explicit phases: **Night preparation → Open day → Closing → Night preparation**. Store day index, phase, clock, transition state, and any once-per-day payments/rewards. Transition commands must be safe under rapid input and reload.
- Start testing with an 8-minute trading day and an untimed preparation night. Allow the player to finish preparation when ready. These are hypotheses; measure workload and waiting before choosing final timings.
- The simulation advances during active gameplay and ordinary station panels. Pause/settings stop it. Closed application time does not advance it. Night is a production/planning space, not mandatory idle waiting.
- Growers and incubation run during both active phases. Daytime lab access is allowed: the cost is attention to customers. Staff later make that tradeoff easier. Do not arbitrarily lock the lab door.
- Closing stops new arrivals. Complete existing accepted transactions or cancel them safely after a clearly signaled grace period; release reservations and never pay for an unfinished sale. No customer pathfinding error may prevent the next night forever.
- Provide open/close confirmation when customers or accepted orders would be affected. Show a daily report with sales, stockouts, lost customers, wages, and the next improvement opportunity.
- Construction, hiring, and major rearrangement happen at night. Repricing future unreserved stock and ordinary restocking can happen during the day.

### 6.2 Hands-on shopkeeping and customers

- The early day must require the player: open the shop, choose stock/prices, serve the counter, and replenish displays. Merely pressing a remote sell button is not the finished store loop.
- Customer states: arrive, browse a reachable display, decide, reserve a specimen, queue, transact, react, leave. Start with one customer and a bounded queue, then increase the configured concurrency.
- One specimen has one owner and one reservation. A sale atomically removes that exact specimen and pays once. Customer cancellation, impatience, closing, or a gag releases the reservation without deleting stock.
- Show readable price reactions and reasons for a lost sale. Later archetypes differ in preferences, budgets, patience, and reactions: bargain shopper, casual visitor, collector, and enthusiastic premium buyer.
- The player's price is bounded and validated. Lock the agreed price when a customer commits; repricing a display must not change a queued sale. Queue length, walking space, and service time limit practical demand.
- A developed shop attracts a larger bounded arrival rate and unlocks customers with larger budgets. Presentation and reputation raise willingness to pay within caps; customers still value particular creatures and fair prices.
- Keep the stationary prototype buyer until the first real customer loop works, then retire that shortcut from normal play. Keep development shortcuts separate and clearly labeled.

### 6.3 Economy and progression

- Separate cash, business reputation, display appeal, and discovery progress. Give each a visible purpose; do not conceal all progress behind an unexplained score.
- Prefer capped additive modifiers for presentation, service, and species preference. Avoid multiplying every store upgrade into both unlimited traffic and unlimited price. Repeated copies of the same decoration have diminishing or no additional economic benefit.
- More stock capacity, faster handling, new customer demand, and new recipes should create distinct choices. Preview costs and concrete benefits before purchase; avoid upgrades that are only imperceptible percentages.
- Keep starter cultures renewable for free and at least one profitable recipe available at every stage. Zero money, a full incubator, an unstaffed shift, or a failed exhibition must remain recoverable.
- Introduce wages only once ordinary income is reliable. Default: schedule staff at night; a paid duty period covers that preparation night and the following trading day. Charge once before an employee first starts work in that period, so nighttime lab work is covered. Persist employee ID, duty-period ID, and payment; dismissal/reactivation cannot avoid or duplicate the charge. Preview that started periods are paid in full. Insufficient funds leave the employee off duty with a clear message; the player can always work manually. No hidden debt or permanent staff loss.
- Centralize balancing in definitions/configuration. Record income per shift, operating cost, stockouts, waiting time, and days to upgrades using local test data. Do not add external analytics without authorization.
- Keep a balance worksheet/table inside this file when needed. Do not commit to campaign hours, prices, hardware targets, or production dates without measurement.

### 6.4 Store/lab layout, construction, and decoration

- Preserve the front entrance and recognizable passage to the back lab. Expand the store sideways; extend the lab behind it. Prioritize a clear path from entry to displays to counter to exit, and a short stock route from lab to store.
- Use snapped footprints and explicit access points. Validate room bounds, non-overlap, doorway clearance, reachable service positions, queue space, and navigation before committing a placement or charging currency.
- Keep at least a provisional 1.2 m clear main aisle; widen after crowd tests. Exact requirements come from player/agent dimensions and measured congestion, not decorative grid appearance alone.
- Provide a ghost preview, rotate, cancel, move, and a recoverable storage option. Distinguish a visual display table from the stock capacity it provides; décor must not silently grant fish storage.
- Block movement/removal of a stocked or active machine until contents are moved safely, or perform a validated relocation transaction. Never destroy creatures, pending production, jobs, or paid furniture through rearrangement.
- Build navigation around actual doors, station access, queues, and worker destinations. After layout changes, rebuild/update only the necessary navigation and recheck existing jobs. Unreachable placements should be rejected rather than trapping actors.
- Keep storefront and laboratory visually distinct through materials, lighting, signs, sound, and props. Validate world-label occlusion, silhouettes, sightlines, and HUD coverage from normal player height.

### 6.5 Research and laboratory growth

- Preserve the proven exact-integer recipe model and convenient known-recipe selection. First discoveries should give a visible new silhouette/behavior, a journal entry, and a useful production or market choice.
- Add culture types and one new processing method gradually. Use purchasable research/facility unlocks and journal clues so experimentation is directed. No required discovery depends on an unbounded random roll.
- Higher recipes may require a processing stage as well as culture amounts. Keep the UI clear about ingredients, facility, time, destination, and expected result for known recipes. Do not turn this into a genetic simulation.
- Separate unique specimen identity from species definition. Model stock locations such as incubator output, lab storage, display, customer reservation, and sold. Movement changes ownership once; presentation objects are not authoritative inventory.
- Machines visibly expose inputs, work in progress, output, capacity, and blocked reason. Reserve output space before consuming ingredients, or safely retain the completed output in the machine. Full storage pauses the chain.
- Add comfortable shortcuts for mastered work: batch orders, known recipes, target stock levels, and reusable machine settings. Preserve manual operation and cancellation/recovery throughout progression.

### 6.6 Staff and automation

- Automate chores only after the manual version is understandable and useful. Give the first helper one clear job before building a general assignment system.
- Cashier: serve valid queued transactions. Stocker: move explicitly available stock to configured displays. Lab technician: perform assigned known production work. Machines provide predictable processing; staff handle interaction/transport and service.
- Every job has a stable ID, one owner, reserved resources/destination, a state, and an understandable blocked reason. Assign priorities and allow pause/reassignment. Avoid two actors claiming the same specimen, slot, or sale.
- On dismissal, path failure, room editing, cancellation, or load, finish or safely release ownership/reservations. Retry with a bounded policy and visible status; never silently discard a job or retry forever while consuming resources.
- Default control: desired stock count per known species, destination display/storage, enabled facilities, worker role/priority. Show shortages and why a route is idle. Avoid an abstract node-graph editor for the first release.
- **End goal of automation:** a known recipe can replenish from renewable growers through processing, storage, display, and customer sale across a complete shift without repeated collection/dosing/restocking/checkout clicks. Full buffers halt safely; supply or space restoration resumes work; save/load preserves the result.
- Full routine automation is optional to operate day-to-day and a demonstrated capability for the proposed ending. The player still chooses discoveries, product mix, prices, expansions, display design, exhibition stock, and optional special requests. Never sabotage working automation with compulsory breakdown chores just to force activity.

### 6.7 Comedy and personality

Use brief authored setups, clear reactions, and fast recovery. Suggested six incident types:

| Incident | Player/character beat | Mechanical limit |
|---|---|---|
| Cartoon slap | Deliberate separate action; exaggerated face, wobble, silly sound, grumbled gibberish | Ordinary shopper may cancel and leave; clownish visitor can laugh. Small capped reputation consequence; release stock reservations. No payment reward for repeated slaps. |
| Suspicious fart | Brief squeak, delayed reactions, a customer blaming the display fish | Cooldown and audio variation; never removes valuable stock or blocks a doorway. |
| Ridiculous complaint | “This fish looks like my accountant” and an expressive reaction | Short optional dialogue; standard skip; no mandatory voice performance budget. |
| Incubator belch | A successful specimen reveal ends with a silly burp or bubble | Never signals failure or masks the completion cue. |
| Overenthusiastic worker | Tiny victory dance or squeaky shoe after completing a job | Only when idle/safe; cannot abandon a customer or freeze the task queue. |
| Fish copies a customer | Creature imitates a pose or makes an absurd noise | Visual/audio reaction only; fish stays inside bounds. |

- Do not bind a slap to the ordinary service/interact button. Add remappable intentional input and a clear target prompt. Never strike on opening a menu.
- Gags share cooldown/concurrency limits so the room is not constantly shouting. Use readable captions for important reactions and separate voice/humor volume plus a reduced-gags option.
- Default to cartoon slapstick, invented mild swearing/gibberish, and no gore. Profanity intensity, final dialogue style, and intended age rating remain user-facing content decisions before recording final assets.
- Gags must tolerate the target leaving, closing time, pause, save/load, and interrupted animations. They must not corrupt a sale, trap an NPC, punish a player repeatedly, or become a superior money/reputation farm.

## 7. Technical implementation rules

Current code under `EvolutionShop/Assets/EvolutionShop/`: `Core/GameTypes.cs` and `GameModel.cs` own rules/state; `Runtime/ShopSession.cs` coordinates play; `ShopHud.cs` presents UI; `ShopPlayer.cs` handles movement/input; `SaveStore.cs` handles saves; `Presentation/ShopWorld.cs` builds the procedural space; `CreatureVisual.cs` renders creatures; `Editor/ProjectSetup.cs` generates the scene and builds; `Tests/EditMode/GameModelTests.cs` and `Runtime/MilestoneSmoke.cs` hold existing validation.

- Extend these boundaries incrementally. Extract clock, transactions, placement, and jobs when a milestone needs them; do not keep growing one session/model into every system, and do not build a speculative engine/framework first.
- Keep economic/production rules testable without scene objects. UI, animation, audio, and AI request validated commands; they must not independently alter money or inventory.
- Use stable IDs and versioned save DTOs. Definitions contain tuning and presentation references, not mutable runtime ownership. Avoid saving scene hierarchy paths or localized display strings as identity.
- Prefer deterministic rules and seeded test scenarios. Validate negative/overflow quantities, missing IDs, capacities, duplicate commands, interrupted work, and transaction order.
- Navigation and presentation may fail without losing economic state. Recovery must resolve a blocked actor/job and preserve valid inventory rather than teleporting unpaid stock into a sale.
- Evolve the generated scene deliberately. Current CreateScene replaces the prototype scene with its bootstrap; inspect ownership/unsaved edits before using it after authored scene work begins. Move reusable room/modules to prefabs when needed, while keeping a repeatable setup route.
- Preserve URP shader inclusion and depth-tested world text in standalone builds. Keep audio feedback distinguishable from comedy. No new third-party dependencies just to solve a small local problem.
- Record provenance/license and local asset source for every admitted asset. Approve scale, pivot, colliders, material/shader, animation, LOD/readability, and build inclusion before replacing placeholders. Paid assets/generation require separate authorization.

### Save and migration contract

Persist day/phase/time, currency, cultures, discoveries, specimens and locations, machine progress/output, room tiers/layout, prices, staff/paid-duty-period state, job ownership, progression/exhibition flags, and settings. Transient customer visuals can be reconstructed, but accepted transactions and reservations need an explicit deterministic restore/cancel policy with no duplication.

Keep atomic writes and recovery backups; validate before replacing a known-good slot. Do not overwrite malformed or unsupported saves silently. Back up before schema migration and test representative prior saves, including the current Milestone 01 format. Never use the user's normal slot for automated tests. Retain local-only storage and no offline progression unless the user changes that direction.

## 8. How to execute work

1. Read the current-state/next-action section and relevant contract above. Inspect Git status and affected code/evidence; preserve unrelated edits. Verify Unity identity when runtime/editor work is involved.
2. Select one ready milestone and one small task or tightly related group. State the intended playable result and acceptance check. Resolve routine implementation choices autonomously inside this scope.
3. Break a large task into smaller IDs before working. Give each active task an owner and a status. Parallelize independent reviews/assets/code only when file ownership and interfaces are clear.
4. Build the smallest complete playable slice. Use placeholders until behavior is tested; keep the existing loop functional. Fix regressions before expanding.
5. Run the strongest relevant checks that are actually available. Repeat or broaden tests when new changes, failures, or unresolved risks justify them; do not rerun everything solely to generate a larger count.
6. Review actual renders/audio/input for changed presentation. Separate automated execution, source review, screenshot review, and human playtest conclusions. Record failed attempts and fixes honestly.
7. After every completed assignment, update this AGENTS.md with status, actual evidence, limitations, and the next action before starting another assignment. Save a small local Git checkpoint after a coherent verified slice. Do not rewrite history or publish. Update this file's active task, evidence, known issues, decisions, and exact next action in the same checkpoint.
8. At a milestone gate, deliver the runnable build/scene, controls, change summary, executed test evidence, known issues, and the next playtest question. Obtain missing creative feedback before bulk content or irreversible production commitments; do not ask again for choices the user already made.

Task marks: `[ ]` planned, `[~]` active, `[!]` blocked with reason/next action, `[x]` acceptance checks executed successfully. Mark human acceptance separately. A checked implementation task is not a claim of enjoyment or creative approval. Use stable task IDs; record material scope changes rather than silently replacing requirements.

Before writing code, briefly consider all six perspectives: player decision/feedback, room navigation/readability, explicit system states/edge cases, maintainable implementation, falsifiable validation, and milestone dependencies/scope. Keep decisions concise and tied to the actual task.

## 9. Milestones and small tasks to a finished game

Delivered history remains **Checkpoint 00**, **M01**, and **01R**. The full-game roadmap uses **R01–R11** to avoid confusing the original draft M02/M03 labels. Original automation/worker/humor ideas are distributed across R03/R06/R07/R08; the original presentation sample becomes R06. Everything below is planned unless checked.

### R01 — Baseline playtest and final-loop design contract

Depends on: delivered two-room prototype. Output: a short verified foundation for expansion.

- [x] R01.1 Baseline playthrough: user reports growing → sale → siphon upgrade with no major issues. Prior standalone movement/room/input evidence is retained. Audio was not separately listened to and manual settings-slider interaction was not reported; do not infer those passes.
- [x] R01.2 Repair and verify the single-culture full-reservoir dead end, failed-save reset/quit behavior, persistent save errors, and focus-loss pause from station panels. Executed evidence is below.
- [x] R01.3 Keep the accepted plan as working defaults: Grand Exhibition, 8-minute day/untimed night, bounded comedy and launch ceiling. User approved the plan and continuation; final tone, pacing and ending quality require later playable evaluation.
- [x] R01.4 Preserve a historical full save fixture, document migration/scene ownership, and checkpoint the verified baseline as `r01-baseline-safety`.

Gate: known baseline issues are explicit, the user can complete the manual loop, and the next vertical slice has a clear scope. Do not start bulk creature/decoration production.

### R02 — One complete night and staffed-by-player day

Depends on: R01. Output: a playable preparation/trading/report cycle with one real customer type.

- [ ] R02.1 Implement phase/day/clock state and UI, pause behavior, open/close commands, closing recovery, and phase persistence.
- [ ] R02.2 Add one navigable customer with browse/reserve/queue/pay/leave states and a low concurrency cap; prevent customers entering the lab.
- [ ] R02.3 Replace normal instant remote selling with personal counter service tied to the reserved specimen; keep simple default prices initially.
- [ ] R02.4 Connect night preparation, day stock depletion, restocking, and a useful daily report. Preserve the free recovery route.
- [ ] R02.5 Test two consecutive cycles, closing with a queued customer, empty stock, full outputs, pause/reload in each phase, and rapid open/close/pay input. Build and launch the slice.

Gate/playtest: the player understands what to do in both phases and wants a second day. Log time spent waiting versus deciding/acting. Do not expand the shop until this cycle works.

### R03 — Prices, customer demand, and satisfying service

Depends on: R02. Output: a small profitable business with understandable choices.

- [ ] R03.1 Add validated display pricing, price snapshots for reservations, and clear affordable/too-expensive reactions.
- [ ] R03.2 Add four data-driven customer archetypes incrementally; tune arrival caps, patience, queues, browse access, and leave recovery.
- [ ] R03.3 Introduce business reputation and capped demand/premium rules. Show why a better shop can support more customers and higher prices.
- [ ] R03.4 Prototype one intentional cartoon slap and one reaction with placeholder sound; verify cancelled transactions, cooldowns, and recovery before adding more jokes.
- [ ] R03.5 Test repeated transactions, repricing during queueing, stockouts, last-specimen races, zero funds, and at least three ordinary shifts. Adjust economy from observed results.

Gate/playtest: the player can explain a lost sale and make a different stocking/pricing choice. Slapstick is readable and cannot duplicate money or permanently block a customer.

### R04 — Expand and personalize the store

Depends on: R03. Output: a visibly larger, better store with safe placement.

- [ ] R04.1 Build the small shared placement tool: footprint/ghost, rotate, cancel, access validation, cost preview, commit, and persistence.
- [ ] R04.2 Add the two store expansion steps and small/large display options; keep entrance, queue, lab passage, and service access clear.
- [ ] R04.3 Add initial décor choices and a capped appeal score; connect progression to bounded traffic and premium customer access.
- [ ] R04.4 Implement moving/storage/removal policies for owned furniture and stock. Reject blocked access and unaffordable purchases atomically.
- [ ] R04.5 Test edited layouts with player/customers, dense queues, doorway obstructions, save/reload, moving stocked furniture, and repeated décor stacking.

Gate/playtest: expansion changes what the player can display/serve; players can understand the benefit and recover from a bad layout without losing stock or money unexpectedly.

### R05 — Research, lab expansion, and production choices

Depends on: R04 placement and R03 economy. Output: an expanded lab and a second meaningful production method.

- [ ] R05.1 Apply shared placement to lab facilities/storage and two lab expansions; validate operator access and output paths.
- [ ] R05.2 Add culture unlocks toward the four-type ceiling, research clues, and progression-aware recipe definitions/journal.
- [ ] R05.3 Add one conditioning facility and a small batch/known-recipe workflow. Make capacity, progress, output, and blocked reasons visible.
- [ ] R05.4 Implement only enough additional species to prove a new family/process; postpone the full roster until the art sample and progression work.
- [ ] R05.5 Test every new recipe/process, invalid inputs, full storage, cancellations, facility relocation, and save/load with work in each stage.

Gate/playtest: a new facility produces a new kind of decision or creature, and lab upgrades remove noticeable work. No new system should strand a zero-cash player.

### R06 — Presentation sample and comedy kit

Depends on: R03–R05 playable behavior. Output: one representative finished-quality corner and a reusable humor system.

- [ ] R06.1 Establish a coherent sample: one store corner, one lab station, one creature, one customer, and one employee model/animation set. Review in the actual game.
- [ ] R06.2 Define asset scale/pivots/materials, silhouettes, collision/access footprints, animation events, readability, and audio loudness conventions.
- [ ] R06.3 Implement the six bounded incident types with reusable triggers/reactions, skip/cancel behavior, cooldowns, captions, and separate humor volume/reduced-gags setting. Preview the worker incident with a fixture/placeholder; connect it to real completed jobs in R07.5.
- [ ] R06.4 Verify available gags during queues, closing, pauses, and interrupted actions; ensure comedy does not obscure money, stock, or readiness feedback. Real worker-job interruption tests belong to R07.5.
- [ ] R06.5 Review normal gameplay audio by listening, not source inspection. Capture actual store/lab presentation and get feedback before multiplying assets.

Gate: the user has evaluated the visual/audio tone; available incidents recover safely and the worker preview is ready for R07 integration; the sample runs acceptably on a named target machine. Do not use an approved screenshot as proof of interaction quality.

### R07 — Helpful employees

Depends on: R03 service, R04 paths, R05 facilities. R06 supplies visual conventions; placeholders may still test behavior.

- [ ] R07.1 Add night hiring/scheduling, visible roles/wages, once-per-duty-period payment covering night and the following day, and the no-funds/manual-work fallback.
- [ ] R07.2 Implement cashier jobs first; verify queue ownership and exactly-once payouts while the player can also serve.
- [ ] R07.3 Add stocker jobs with source/destination reservations, target displays, and visible blocked states.
- [ ] R07.4 Add lab technician jobs for known recipes, priorities, reassignment, and safe cancellation/dismissal.
- [ ] R07.5 Integrate the worker-completion gag and test safe interruptions. Test contention with the player, two jobs requesting one item, blocked routes, dismissal/reactivation, wage payment/reload across night/day, and at least one duty period per role.

Gate/playtest: each role saves a recognizable chore and gives the player useful time. Workers explain why they are idle; none require constant rescue to function.

### R08 — Reliable routine automation

Depends on: R05 and R07. Output: one complete automatic grow-to-sale route, then bounded configuration for other known recipes.

- [ ] R08.1 Add automatic collection/dosing modules using the same validated rules as manual work, with real input/output capacity.
- [ ] R08.2 Connect production jobs, lab storage, stocker replenishment, customer reservations, and cashier transactions into one complete route.
- [ ] R08.3 Add desired stock levels, recipe/destination configuration, pause controls, shortages, and a clear explanation of each blocked state.
- [ ] R08.4 Prove normal operation plus full/empty buffers, missing staff, path failure, reorder/cancel, room edits, and interruption/reload without lost/duplicated resources.
- [ ] R08.5 Run several consecutive automated shifts and a mixed manual/automatic shift; measure whether free time creates useful choices or just idleness.

Gate: one known product can travel from free cultures to a paid sale for a complete shift without repetitive player servicing. Production stops/resumes safely and remains optional to operate manually.

### R09 — Complete progression, collection, and ending

Depends on: R06 tone/sample and R08 reliable loop. Output: a complete rough campaign from new game to credits/free play.

- [ ] R09.1 Build a dependency graph for room tiers, research, facilities, customers, staff, and automation. Give every required unlock a reachable source of funds/resources and a clear objective.
- [ ] R09.2 Prototype the objective board and shortened exhibition using existing species/placeholders before completing the roster. Prove eligibility, protected displays, six ordinary purchases, retry, once-only completion, and post-ending continuation; evaluate the proposed goal.
- [ ] R09.3 After the rough ending works, complete the bounded 12-species/three-family roster and needed décor/customer variations. Validate recipes, visual distinction, animation bounds, market role, and provenance; finish the exhibition's final targets, celebration, and presentation.
- [ ] R09.4 Complete onboarding in the actual rooms and explain phase transitions, pricing, placements, staffing, automation, and the ending when each becomes relevant.
- [ ] R09.5 Play through the main path without development cheats; separately use seeded/test saves to exercise late edge cases. Test ending/retry/reload and optional collect-all completion.

Gate: a new player can reach the ending through the supported progression. No missing content, impossible unlock, unbounded rarity grind, or compulsory total collection. Freeze feature scope after acceptance.

### R10 — Beta: balance, usability, accessibility, performance

Depends on: R09 complete campaign. Output: a release candidate with measured limits and an empty critical-defect list.

- [ ] R10.1 Conduct fresh-player and experienced-player sessions; record onboarding failures, service load, income, upgrade pacing, night waiting, automation value, ending clarity, and gag repetition.
- [ ] R10.2 Tune progression/economy and trim chores. Verify finite caps, starter recovery, wage affordability, exhibition retry, and all required content paths.
- [ ] R10.3 Finish input rebinding, mouse sensitivity/invert, hold/toggle collection where appropriate, readable text/UI scaling, captions, non-color-only cues, audio channels, and reduced camera motion/gags.
- [ ] R10.4 Profile the densest supported layout, maximum customers/workers, occupied tanks, effects, and long-running simulation. Record frame-time percentiles, spikes, memory, and load times on named hardware/settings; optimize measured bottlenecks.
- [ ] R10.5 Exercise migration from prior supported saves, backup/corrupt-save behavior, alt-tab/focus, pause, resolutions/window modes, first launch, reset/cancel, quit/relaunch, and prolonged repeated shifts.
- [ ] R10.6 Close all release-blocking defects and document lesser limitations. Set supported hardware/settings from measurement; a fast development PC alone does not establish minimum requirements.

Gate: representative human playtests are recorded, no known progression/save-loss blocker remains, and the exact release candidate meets the agreed usability/performance targets. No feature expansion during stabilization.

### R11 — Final build and delivery

Depends on: R10 accepted release candidate. Output: a complete locally deliverable game, not an unfinished development scene.

- [ ] R11.1 Produce the Windows non-development release configuration; remove/disable smoke hooks, cheats, debug overlays, and F8 comparison in normal release play without removing diagnostics needed for support.
- [ ] R11.2 Run the exact packaged executable separately from editor tests: fresh start, normal save/load, a full representative shift, advanced automation, exhibition/ending, and settings/quit. Verify full campaign evidence still matches the frozen content.
- [ ] R11.3 Check credits, licenses/provenance, title/version, controls, settings persistence, clear save location, and known-issues/readme information. No unlicensed placeholder assets/audio may remain.
- [ ] R11.4 Package required runtime/data files, verify hashes against the tested build, exclude unwanted debug artifacts, and preserve the source tag and rollback build.
- [ ] R11.5 Record final test evidence, release-blocker count, accepted limitations, and human acceptance here; deliver the archive and concise play instructions.

Gate: the agreed game is playable from onboarding through ending and continuation, with tested saves and an exact verified release package. Uploading to a storefront, buying accounts/assets, marketing, and remote publication are separate user-authorized actions; a finished local build does not imply publication.

## 10. Validation standard and definition of done

| Layer | Required evidence when affected |
|---|---|
| Rules/state | Meaningful automated tests for transactions, timing/phases, recipes, capacity, pricing, reservations, wages, progression and job ownership |
| Persistence | Actual file round trips and migrations during production, phase transition, queue/worker handoff, room editing, and ending; isolated slots |
| Runtime integration | Real input, navigation, service/production routes, full/empty cases, pauses, interruptions, and recovery in a running scene |
| Presentation/feel | Actual captures plus hands-on input and listening; verify legibility, occlusion, crowding, audio overlap, and comedy timing |
| Standalone | Build result and a separate launch of that exact binary/package, with settings/hardware/context and observed results |
| Campaign | Supported fresh-start path to ending, optional continuation, zero-money recovery, advanced automation, and no inaccessible required unlock |

For each result record task ID, code checkpoint/build identity, command or manual steps, expected behavior, actual result, evidence path, and limits. Never say “tests/build passed” unless executed. Retain failed-run evidence; distinguish an automated pass from a screenshot defect or a human complaint. Test counts alone do not establish quality.

Classify defects by effect: **P0** save loss/security or destructive corruption; **P1** crash, impossible progression, duplicated money/stock, permanent actor/phase deadlock; **P2** significant usability/balance/presentation failure; **P3** minor polish. No known P0/P1 at a milestone handoff or release; explicitly report blockers instead of claiming completion. Release also requires unresolved P2 issues to be fixed or explicitly accepted with a practical limit. Avoid adding tests that merely mirror trivial code or documentation edits.

The game is finished when the launch-scope features, complete progression/ending, reliable automation, coherent art/audio, usable controls/settings, save migration/recovery, measured performance, and exact release package have passed their gates. A growing backlog does not postpone that definition indefinitely. Optional additions remain post-release ideas until authorized.

## 11. Current state, active work, and next action

### Delivered, with evidence

- [x] Checkpoint 00: independent Unity project and runnable baseline; commit `48ca887`.
- [x] M01 manual loop: two free cultures, timed siphon, three exact recipes, incubation/discovery journal, two tanks, selected sales, currency, physical upgrade, settings and local save. Loop checkpoint `54ea873`; original delivery tag `milestone-01-playtest` at `0fbf73e`.
- [x] 01R: confirmed front store/back lab, doorway, station relocation, guidance, depth-tested labels and visible siphon readout. Tag `milestone-01-two-rooms` at `5606dbf`.
- [x] Latest prior Windows build: `Docs/Evidence/two-rooms-final-build-result.json`, succeeded with zero errors/warnings. Exact player: `Docs/Evidence/two-rooms-final-player/smoke-result.json`, 79 checks passed, no captured runtime errors/limitations, process exit 0. These results cover that checkpoint, not future features.
- [x] Prior Editor run: 78 checks, with hidden-batch cursor limitation; later standalone verified capture. Historical 50 EditMode cases were not rerun for the room-layout change. Reports remain in `Docs/Evidence/`.

Current limitations: procedural placeholder art/audio and provisional balance; no actual customer day/night system, construction, staff, full automation, or campaign ending yet. User reports no major problems in the baseline loop; separate audio listening and manual settings-slider checks remain unrecorded. Capture targeting now waits for the runtime focus query after camera repositioning. No known unresolved critical progression blocker in executed baseline checks.

### R01 baseline evidence and constraints

- Exact workspace/editor reverified: `C:/dev/DumbVideoGameStudio/EvolutionShop`, Unity `6000.3.15f1_c1aa84e375f6`. Unity CLI reported zero connected editors before launch; validation used explicit `-projectPath` batch processes and logged runtime identity. No MCP connection was assumed or installed.
- `r01-editmode-results.xml`: first run 97/106 passed; all nine failures were scene test setup (an unsaved runner scene prevented additive scene creation). The repaired fixture uses isolated preview scenes. `r01-editmode-fixed-results.xml`: **106/106 passed**, exit 0. Of these, 79 cover R01/model/full-slot/scene safety and 27 are the independently staged R02.1 clock tests; the latter are excluded from the R01 source checkpoint.
- `r01-playmode/smoke-result.json`: **86 checks passed**, zero captured runtime errors, exit 0, 53.05 seconds. Recovery, malformed saves, blocked writes, failed reset preservation and later save recovery executed. Hidden batch Editor cannot verify focused cursor capture; the final player must cover it. No new Windows build is claimed at this checkpoint.
- Build/smoke now validate existing authored scene content and never regenerate a present scene. Explicit regeneration warns interactively and batch mode rejects extra authored structure; dirty scenes and Play mode block it. Nine tests cover session ownership and preserving extra objects. `r01-scene-before.sha256` and `r01-scene-after.sha256` match after actual smoke: `2F81309DDB7CAA56E0E56B4D6B705EB274E234AE9CF0D7BCBB968373AB9AE16F`.
- `Shop.unity` remains a single-session bootstrap. `ShopSession` creates the world/player/camera; authored room content requires a deliberate ownership migration to avoid duplicates. Scene preservation alone does not perform that migration.
- Known-good historical full-slot fixture: `EvolutionShop/Assets/EvolutionShop/Tests/RuntimeEditMode/Fixtures/V1Progress.json`, copied from original M01 evidence. It contains the upgrade, all discoveries, 2 credits and a live Pondlet batch. File round trips cover staging, incubation, ready output with full tanks, a sale, and upgraded capacity. Invalid saves stay intact; rotating `.bak` files are preserved but are not automatically loaded.
- Existing envelope/game schema is version 1. New envelope fields require migration; creature/specimen IDs and progress must survive. Model validation constrains grower/incubator remaining time using configuration, so later balance-duration changes need an explicit migration/clamping policy. Keep a pre-migration fixture and backup before writing upgraded saves.

| Active record | Value |
|---|---|
| Current request | R01 baseline safeguards, then R02.1 day/night foundation; update this guide after each assignment |
| Runtime changes in this request | R01 safeguards verified and checkpointed; R02.1 clock integration next |
| User feedback | User reports playing growing → sale → siphon upgrade with no major issues (2026-10-03) |
| Next implementation milestone | R02.1 only: phase/day/clock UI and persistence; customers remain R02.2 |
| Human acceptance still needed | Day/night pacing, ending and comedy tone as these defaults become playable; separate audio/settings checks |
| Technical blocker | None. Use verified exact-project CLI; no active MCP connection |

When resuming implementation, select the first unfinished ready task, record its owner/status and intended acceptance check here, and keep the remaining roadmap planned. Do not mistake the unimplemented feature specifications above for existing behavior.

### Decisions and change log

| Date | Decision | Status / basis |
|---|---|---|
| 2026-10-03 | Separate front store and back lab | User confirmed; implemented and verified at `5606dbf` |
| 2026-10-03 | Night laboratory work, daytime personal shopkeeping, expansion, staff, automation, and comedy | User requirements for the complete game; not implemented |
| 2026-10-03 | Grand Exhibition main ending; collect-all optional | Recommended design default; user can revise after seeing the rationale/playable goal |
| 2026-10-03 | 12-species launch ceiling, four cultures, three employee roles, same-location expansions | Provisional scope control; validate before bulk production |
| 2026-10-03 | One canonical root guide; R01–R11 replace old draft future milestone labels | This document request; preserve earlier files as historical evidence |
| 2026-10-03 | R01.1–R01.4 baseline gate completed; update this guide after every assignment | User's no-major-issues playthrough plus executed safety checks; tag `r01-baseline-safety`. Next: R02.1 clock integration only |

At future checkpoints append a concise row with task IDs, commit/tag, actual evidence, remaining risk, and exact next action. Keep this section current and short; put large raw outputs in evidence files. Do not erase history to turn an unverified or failed attempt into a pass.
