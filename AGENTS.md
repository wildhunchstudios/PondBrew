# Evolution Shop — working instructions and complete-game roadmap

**The Unity 6000.3.21f1 player is technically validated and ready for the human two-cycle playtest.** Continuous, native-UI, regression and ordinary Save and quit processes all exited cleanly. The previous 6000.3.15f1 native shutdown failure is resolved in this tested configuration; its evidence is retained. R02.5c is complete. Human R02.5d and full R02 remain open.

Updated: 2026-10-03. Working title only. Current patched source checkpoint: `fb4e4d2`. Preserved failed-shutdown candidate checkpoint: **9197e1c**, tag **r02-cycle-validation-candidate**; its gameplay source was **2ab4929**. Continuous-cycle rule checkpoint: **4b28bfe**. Previous player tag: `r02-daily-report`; report implementation: `de2de99`. Customer rule checkpoint: `b711bc7`, tag `r02-customer-rules`. Baseline safety checkpoint: `78e99c4`, tag `r01-baseline-safety`.

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
| Editor pin | Unity `6000.3.21f1` (`c02631ffc030`), explicitly authorized patch |
| Packages | URP `17.3.0`; Input System `1.19.0`; Test Framework `1.6.0` |
| Editor executable | `C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe` |
| Playable scene | `EvolutionShop/Assets/EvolutionShop/Scenes/Shop.unity` |
| Current patch validation player | `Builds/Windows-6000.3.21f1/EvolutionShop.exe`, Windows x64 development/Mono; qualification passed in the tested Windows configuration, including clean native process exits |
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

- [x] R02.1 Implement phase/day/clock UI, pause behavior, open/close commands, bounded closing and phase persistence. Owner: root. Verified: 128 EditMode cases; final Windows player 108 checks; old-save migration and exact unsold stock retained through closing/reload. Human pacing and button-click acceptance remain pending. Customer-dependent closing behavior belongs to R02.2–R02.5.
- [x] R02.2 Add one navigable customer with browse/reserve/queue/pay/leave states and a low concurrency cap; prevent customers entering the lab. Delivered with R02.3; one active visitor and one queue slot, actual runtime paths and lab exclusion verified.
  - [x] R02.2a Authoritative specimen reservations and atomic fixed-price checkout (scope_review); 28 new cases passed for duplicate/wrong-owner/overflow/cancellation transactions. Runtime checkout and recovery verified in Editor and Windows player.
  - [x] R02.2b Front-store-only runtime navigation and a readable original placeholder customer (world_presentation); complete entry/display/counter/exit paths, raw baked lab exclusion, movement/pause and visible reactions verified.
  - [x] R02.2c Pure visit state machine and bounded recovery (project_records); 19 new cases passed for empty stock, patience, failed travel, closing and restore policy. Real navigation and save/reload behavior verified; six additional file round-trip cases passed.
- [x] R02.3 Replace normal instant remote selling with personal counter service tied to the reserved specimen; keep simple default prices initially. Owner: root. HUD, physical counter proximity, pause, transient-visitor recovery and paid-sale persistence verified; tested Windows package delivered.
- [x] R02.4 Connect night preparation, day stock depletion, restocking, and a useful daily report. Preserve the free recovery route. Delivered at r02-daily-report; evidence below.
  - [x] R02.4a Core daily ledger and exactly-once outcomes (scope_review); 61 tests passed for duplicate outcome, cancellation, two days, corruption and actual Unity JSON absence handling.
  - [x] R02.4b Save format 3 and v1/v2 migration (project_records); 28 new actual-file cases plus existing save regressions passed; original bytes, pending-visit recovery and completed results preserved.
  - [x] R02.4c Runtime event wiring and readable night report/review button (root/world_presentation); actual multi-day report totals, reload and screen rendering verified. No new reward, stock authority, wages system or pricing.
  - [x] R02.4d Validate exact report totals through customer service, closing, reload, restocking and zero-sales days; Windows build/run/package and actual UI inspection completed, this guide updated (root).
- [ ] R02.5 Complete the whole-cycle playtest/validation gate: two preparation/trading/report cycles with queued closing, empty/full stock, pause/reload and rapid commands. Current technical evidence is recorded below; human clarity, waiting-versus-action and pacing acceptance remain pending. The user has not played the latest loop. Fix observed friction before closing R02; do not start R03 from automated evidence alone.

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

Current limitations: procedural placeholder art/audio, provisional balance, one visitor, fixed pricing, customer/player pass-through and no held-creature/bag animation. Adjustable pricing, construction, staff, full automation and the ending remain planned. Current automated rules/save/build/runtime checks pass and no critical progression/save/shutdown blocker remains in the executed configuration. Installer modules.json metadata is absent despite verified usable Editor/Mono components. The current native probe verifies eleven actual mouse-button controls; stock fixtures, scripted positioning and one arrival bound that coverage. Two attempted Computer Use Escape presses during the separate quit probe produced no observed change; cause is unconfirmed, so this does not establish physical-keyboard Escape acceptance. Synthetic Escape behavior is covered by runtime tests. Human clarity, workload, full eight-minute-day pacing, listened-to audio and untested settings sliders remain unaccepted; the user has not played the latest loop.

### R01 baseline evidence and constraints

- Exact workspace/editor reverified: `C:/dev/DumbVideoGameStudio/EvolutionShop`, Unity `6000.3.15f1_c1aa84e375f6`. Unity CLI reported zero connected editors before launch; validation used explicit `-projectPath` batch processes and logged runtime identity. No MCP connection was assumed or installed.
- `r01-editmode-results.xml`: first run 97/106 passed; all nine failures were scene test setup (an unsaved runner scene prevented additive scene creation). The repaired fixture uses isolated preview scenes. `r01-editmode-fixed-results.xml`: **106/106 passed**, exit 0. Of these, 79 cover R01/model/full-slot/scene safety and 27 are the independently staged R02.1 clock tests; the latter are excluded from the R01 source checkpoint.
- `r01-playmode/smoke-result.json`: **86 checks passed**, zero captured runtime errors, exit 0, 53.05 seconds. Recovery, malformed saves, blocked writes, failed reset preservation and later save recovery executed. Hidden batch Editor cannot verify focused cursor capture; the final player must cover it. No new Windows build is claimed at this checkpoint.
- Build/smoke now validate existing authored scene content and never regenerate a present scene. Explicit regeneration warns interactively and batch mode rejects extra authored structure; dirty scenes and Play mode block it. Nine tests cover session ownership and preserving extra objects. `r01-scene-before.sha256` and `r01-scene-after.sha256` match after actual smoke: `2F81309DDB7CAA56E0E56B4D6B705EB274E234AE9CF0D7BCBB968373AB9AE16F`.
- `Shop.unity` remains a single-session bootstrap. `ShopSession` creates the world/player/camera; authored room content requires a deliberate ownership migration to avoid duplicates. Scene preservation alone does not perform that migration.
- Known-good historical full-slot fixture: `EvolutionShop/Assets/EvolutionShop/Tests/RuntimeEditMode/Fixtures/V1Progress.json`, copied from original M01 evidence. It contains the upgrade, all discoveries, 2 credits and a live Pondlet batch. File round trips cover staging, incubation, ready output with full tanks, a sale, and upgraded capacity. Invalid saves stay intact; rotating `.bak` files are preserved but are not automatically loaded.
- At the R01 checkpoint, envelope/game schema was version 1. New envelope fields require migration; creature/specimen IDs and progress must survive. Model validation constrains grower/incubator remaining time using configuration, so later balance-duration changes need an explicit migration/clamping policy. Keep a pre-migration fixture and backup before writing upgraded saves.

### R02.1 historical delivered behavior and evidence (superseded checkout below)

- New games begin on **untimed preparation night 1**. The front counter opens an **8-minute trading day**; manual prototype sales are accepted only while Open. A confirmed early close or clock expiry enters **3 active seconds of Closing**, then the next preparation night. Empty stock cannot trap a day. The lab runs during active nights/days and remains accessible throughout. There are no customers yet.
- HUD shows phase/day/time and pauses; night uses cooler retained room lights and day uses warmer lighting. No world is rebuilt for a transition. Open/close commands cannot reset an active clock or duplicate a day. Pause, Welcome, reset confirmation and focus loss stop simulation; ordinary station panels and journal continue it. No offline progress.
- Envelope version **2** stores game state, day/phase/countdown and settings. Inner model schema remains **1**. Valid v1 saves migrate in memory to preparation night 1 with progression/settings unchanged; reading never modifies the file. The first write archives original bytes as `.v1.bak`, independently of rotating `.bak`. Existing archives are not overwritten; a failed archive write preserves the primary. Backups do not auto-load. Failed reset keeps both current model and clock; failed in-game quit stays open.
- `r02-1-editmode-results.xml`: **128/128 passed**, zero skipped, exit 0 (59 model, 27 clock, 33 full-slot, 9 scene-safety cases). Rules, file migration, malformed data, failed IO, exact boundaries, overshoot, repeat commands and phase restores executed.
- `r02-1-playmode/smoke-result.json`: **104 checks passed**, no captured errors, exit 0; hidden Editor cursor limitation. Initial standalone `r02-1-player/smoke-result.json`: **105 checks passed**, no errors/limitations, exit 0. Review then strengthened the closing scenario to retain a real stocked specimen; only smoke validation code changed afterward.
- Final `r02-1-final-build-result.json`: **Succeeded**, 0 errors, 0 warnings, 166,569,008 bytes. Final exact Windows player `r02-1-final-player/smoke-result.json`: **108 checks passed**, no captured runtime errors/limitations, exit 0, 68.46 seconds. E input opens the actual front counter; pause/reload covers day and Closing; early closing retains the exact specimen ID and currency into the next saved night. Empty fresh open/close recovery also executed.
- Day 1 preparation/production and early closing ran with real frames. Second-cycle timeout/completion use explicitly accelerated deltas; this proves transition behavior, not eight-minute pacing. GUI commands execute through the same session methods used by buttons; screenshot review does not establish actual mouse-click usability or enjoyment.
- Actual standalone captures reviewed at 1280×720: Welcome, back lab/night readability, night/open counter, Closing and stocked night 2. Test hardware: Intel i7-14700F / NVIDIA RTX 4070 Ti SUPER. No minimum-hardware or performance target claim. Scene hash still matches the R01 preserved value after builds.
- Delivered archive: `EvolutionShop-DayNight-Windows.zip`, **69,093,719 bytes**, SHA256 `F32A88BD41F0FA7EECA5FCF4D4241AC8281F3A919E7C3D76F5F9AB32498B9D27`. All **293 files** in its 295 entries match the tested build by hash; debug-only build folders excluded. Receipt: `Docs/Evidence/r02-1-package-verification.json`. Prior two-room archive retained for rollback.
- Checkpoint: tag `r02-1-day-night`. User-facing delivery folder: `C:/Users/retai/Documents/Codex/2026-10-03/read-evolution-shop-prototype-handoff-md-3/outputs`. Includes the ZIP, `PLAYTEST-DayNight.md`, `VALIDATION-DayNight.md`, actual captures and a snapshot of this guide. The root guide remains authoritative.

### R02.2/R02.3 historical delivered customer service and evidence

- **One active visitor / one queue slot**: enter, walk to a stocked display, browse, reserve an exact specimen at its default fixed price, walk to checkout, wait, react and leave. Original procedural character with walking animation and readable status captions; stationary prototype buyer and normal instant-sale buttons retired. Player must use E at the physical front counter and choose Serve customer. Remote checkout from the lab fails.
- Core rules own reservations and atomic payment; one specimen/owner/price per order, no repeat payout. Visitors remain transient: saving keeps the live visit; reloading cancels unpaid visitors and releases claims while retaining saved stock IDs, money and clock. Paid sales stay committed. Save envelope remains 2 / model 1 / cycle 1, with previous v1 migration/backups retained.
- Runtime uses the already installed AI Navigation 2.0.12 package and one NavMesh bake from owned room colliders. Full paths connect entrance, both displays, counter and exit; raw navigation probes and runtime positions confirm the lab is excluded. No package, authored scene, external asset or protected project was imported/changed. Navigation failure blocks opening with restart guidance; normal reload resets visitors without rebaking.
- Tunable first-slice defaults: first arrival after 2 seconds, next visit 6 seconds after departure, browsing 2 seconds, queued patience 45 seconds, travel timeout 18 seconds, paid reaction 1.5 seconds. Closing stops arrivals and new reservations; accepted queued orders can finish during the existing 3-second grace. Night cancels unpaid visits, retaining creatures and granting no payment. Pause/settings/focus loss stop customer movement, patience and simulation.
- Exact workspace/editor/installed Windows module and connection were reverified before work. Unity CLI reported zero connected editors; no Unity MCP tools were exposed. Explicit project-targeted Unity processes logged the guarded identity. No connection was assumed or installed.
- Small rule checkpoint: commit **b711bc7**, tag **r02-customer-rules**; 175/175 EditMode cases passed. Integrated r02-customer-integrated-results.xml: **181/181 passed**, 0 failed/skipped, exit 0 (128 previous + 28 reservation + 19 visit + 6 actual customer save-file cases). These tests ran before the navigation lifecycle repair; the subsequent runtime and build runs compiled/exercised that repair.
- Initial r02-customer-playmode/ failed after 64 checks: native NavMeshPath allocation in a MonoBehaviour field initializer prevented navigation startup. Fixed both navigation and actor allocations to run in Build/Initialize; added early readiness diagnostics. Failed report/log retained. Corrected r02-customer-playmode-fixed/smoke-result.json: **153 checks passed**, 0 captured runtime errors, exit 0, 181.06 seconds; hidden Editor cursor limitation recorded.
- r02-customer-build-result.json: **Succeeded**, 0 errors, 0 warnings, **166,625,481 bytes**, exit 0. Exact Windows development/Mono player r02-customer-player/smoke-result.json: **154 checks passed**, no captured errors or limitations, exit 0, **181.07 seconds**. Includes actual E input, walking, navigation/pause, lab checkout rejection, repeat payment, queue reload, paid/unpaid closing, impatience, no-stock departure, and three customer purchases funding the 24-credit upgrade. Observed 9,898 navigation frames within the store and one bake across reloads.
- Actual standalone screenshots 20–23 reviewed at **1280×720**: browsing caption/HUD, checkout order/reserved display stock/service button, paid reaction and retained unpaid stock in the next night. No text overflow found. Intel i7-14700F / RTX 4070 Ti SUPER; this is not a minimum-spec or performance benchmark. Authored scene SHA256 still **2F81309DDB7CAA56E0E56B4D6B705EB274E234AE9CF0D7BCBB968373AB9AE16F**. Final log scans found no gameplay exception; Editor's licensing refresh message did not prevent successful validation/build.
- Delivered EvolutionShop-Customers-Windows.zip: **69,116,036 bytes**, SHA256 **8DA65BD0CD624672CA157A67F508EECC2D9D3909ED2C21DEA9B8736A006C9C89**. All **293 files / 295 entries** match the tested build by hash; debug-only folders excluded. Receipt Docs/Evidence/r02-customer-package-verification.json. Previous day/night and two-room packages retained. Final source tag **r02-customer-service**.
- Evidence lives under Docs/Evidence/; delivery folder remains C:/Users/retai/Documents/Codex/2026-10-03/read-evolution-shop-prototype-handoff-md-3/outputs, with PLAYTEST-Customers.md, VALIDATION-Customers.md, raw result copies, captures, archive and guide snapshot.
- Limits: one procedural visitor who does not physically block the player; no held-creature/bag animation; fixed prices and provisional timings. Scenarios seed customer stock through core recipe operations; selected timeouts/day transitions use explicitly accelerated or injected deltas. Actual navigation and E input ran; GUI commands use application methods, not verified mouse clicks. No new human pacing/audio/settings acceptance. No known critical save/progression blocker. At this customer-service checkpoint, R02.4 and R02.5 remained unfinished; see the newer R02.4 results below.

### R02.4 delivered daily results and evidence

- One ledger observes committed customer outcomes and successful daytime display transfers; it never owns creatures, changes money, or grants a report reward. It persists the current day and the latest completed day. Opening, checkout, visitor cancellation and finalization are safe under repeated commands. Sold outcomes count at payment, before departure; unfinished visits at night count as closed. Reloaded unpaid visits count as interrupted without income or stock loss.
- At night, an automatic results panel shows sales income, visitors served/lost, five loss reasons, opening/restocked/closing display counts, current cash and preparation guidance. Wages explicitly remain zero because employees are not implemented. Reopen the latest report at the night counter; Escape or the always-visible Continue preparation button dismisses it. The untimed night and lab production continue while reading; normal pause/settings still freeze simulation.
- Save envelope **3**, game **1**, cycle **1**, report **1**. Versions 1 and 2 migrate in memory; the first replacement archives original bytes in .v1.bak/.v2.bak without overwriting an existing archive. Rotating .bak remains; backups do not automatically load. A legacy Open/Closing day starts an explicitly **partial** report with current displayed stock and zero invented historical earnings. Validating a v3 slot checks report version, presence flags, counts and day/phase consistency before replacing state.
- Reverified clean baseline **c97ea21**, exact project/pinned installed Unity 6000.3.15f1_c1aa84e375f6 and Windows support. No connected Editor/MCP was assumed: Unity CLI count0, no exposed Unity MCP tools; actual test/build processes used the guarded exact project. No dependencies, authored scene or assets from another project were changed.
- Initial r02-report-editmode-results.xml: **233/257 passed, 24 failed**, exit2; raw evidence retained. Unity JSON serializes absent inline classes as default objects, so nullable report references were insufficient. Fixed explicit hasCurrent/hasLast flags, canonical empty payload checks and a separate zero-default report header; retained original failure assertions and added focused regressions. Corrected r02-report-editmode-fixed-results.xml: **270/270 passed**, 0 failed/skipped, exit0 (181 existing + 61 ledger + 28 report file cases). Implementation checkpoint **de2de99**.
- r02-report-playmode/smoke-result.json: **179 checks passed**, no captured runtime errors, exit0, **191.50 seconds**. Hidden Editor cursor limitation retained. Four short consecutive days exercise exact income/outcome totals, interrupted reload, closing payment/cancellation, real customer navigation, lost stock/patience/path outcomes, daytime restocking, no visitors, next-day counter reset, report re-open, free recovery and reset. Night incubation actually advances during report display. A v2 runtime reload produces partial results, keeps progression and archives exact prior bytes.
- r02-report-build-result.json: **Succeeded**, 0 errors/warnings, **166,647,244 bytes**, exit0. Exact Windows player: **180 checks passed**, no captured runtime errors/limitations, exit0, **191.48 seconds**. Report screen captures 30–33 reviewed at 1280×720 on Intel i7-14700F / RTX4070TiSUPER. Actual report text and fixed Continue button are readable, including zero-sales and partial-save cases; this is not a minimum-spec/performance claim.
- Delivered EvolutionShop-DailyReport-Windows.zip: **69,124,055 bytes**, SHA256 **D5ECE7533AC3CB54EEAA643D08CCBBAF21A580A71CEE21FFD633C037EF315FFD**; all **293 files** match the tested player by hash. Receipt Docs/Evidence/r02-report-package-verification.json. Previous builds retained. Scene hash remains **2F81309DDB7CAA56E0E56B4D6B705EB274E234AE9CF0D7BCBB968373AB9AE16F**. Source tag **r02-daily-report**; output folder contains the ZIP, PLAYTEST-DailyReport.md, VALIDATION-DailyReport.md, result copies, actual report captures and a snapshot of this guide.
- No known critical progression/save blocker in executed checks. Limits remain one visitor, fixed prices, placeholder presentation, customer/player pass-through, no held-fish/bag animation and provisional timings. Runtime tests seed stock through recipe operations and accelerate selected timeout/day-transition checks; real walking/E/Escape input executed, GUI buttons use application commands. No new human pacing, mouse-button, audio-listening or settings acceptance is claimed. **R02.5 remains open; full R02 is not yet accepted.**

| Active record | Value |
|---|---|
| Current request | R02.5c authorized patch validation completed; ready for human R02.5d two-cycle playtest |
| Runtime changes in this request | Engine patch and versioned build output only; preserve earlier late-restock repair (4b28bfe / 2ab4929) and failed candidate 9197e1c. Package/settings normalization is reviewed below. No later-milestone implementation |
| User feedback | Original grow → sale → upgrade loop had no major issues; latest DailyReport/customer/report loop **not played yet** (2026-10-03) |
| Next action | User plays two preparation/trading/report cycles in the delivered 6000.3.21f1 build, then report and repair observed friction. R02.5d and full R02 remain open; R03 is not ready |
| Human acceptance still needed | Two preparation/trading/report cycles, restocking workload, full eight-minute-day pacing, patience/closing grace, separate audio and unverified settings controls. Ending/comedy tone later |
| Technical blocker / connection | passed in the tested Windows configuration, including clean native process exits. Editor 6000.3.21f1 is pinned and has executed actual EditMode tests. Installation metadata write failed; tested Editor/Mono payload is usable. Current connection/process snapshot: no MCP connection was assumed; actual validation processes identify this exact project/editor. Reverify exact project/editor identity before any new Unity operation |

When resuming implementation, select the first unfinished ready task, record its owner/status and intended acceptance check here, and keep the remaining roadmap planned. Do not mistake the unimplemented feature specifications above for existing behavior.

### Decisions and change log

| Date | Decision | Status / basis |
|---|---|---|
| 2026-10-03 | Separate front store and back lab | User confirmed; implemented and verified at `5606dbf` |
| 2026-10-03 | Night laboratory work, daytime personal shopkeeping, expansion, staff, automation, and comedy | User requirements; night/day and first personal customer checkout now implemented. Expansion, staff, automation and comedy remain planned |
| 2026-10-03 | Grand Exhibition main ending; collect-all optional | Recommended design default; user can revise after seeing the rationale/playable goal |
| 2026-10-03 | 12-species launch ceiling, four cultures, three employee roles, same-location expansions | Provisional scope control; validate before bulk production |
| 2026-10-03 | One canonical root guide; R01–R11 replace old draft future milestone labels | This document request; preserve earlier files as historical evidence |
| 2026-10-03 | R01.1–R01.4 baseline gate completed; update this guide after every assignment | User's no-major-issues playthrough plus executed safety checks; tag `r01-baseline-safety`. Next: R02.1 clock integration only |
| 2026-10-03 | R02.1 day/night foundation delivered; remaining R02 tasks stay planned | Tag `r02-1-day-night`; 128 EditMode cases, final player 108 checks, build/package verified. Next: R02.2 customer loop; human day/night playtest pending |
| 2026-10-03 | R02.2/R02.3 active scope and acceptance | One customer at a time. Build NavMesh from current room colliders with lab excluded. Preserve creatures until atomic checkout. On reload, cancel unpaid visitors/reservations without income or stock changes; paid sales remain committed. Keep save schema 2/inner 1 because visitors are deliberately transient. Bounded travel/patience/closing recovery; no report, pricing system, staff or new assets in this slice |
| 2026-10-03 | R02.2a/R02.2c rule checkpoint | `r02-customer-rules`: `Docs/Evidence/r02-customer-domain-results.xml` 175/175 EditMode cases passed, exit 0 (128 previous + 28 reservation + 19 visit). Actual live customer/navigation/UI integration is still in progress; no new build is claimed at this checkpoint |
| 2026-10-03 | R02.2/R02.3 completed | Tag r02-customer-service: 181 EditMode cases, 153 Editor / 154 exact Windows checks, successful build, verified package; initial navigation lifecycle failure repaired and retained. Next R02.4 daily report; customer pacing/manual UI feedback pending |
| 2026-10-03 | R02.4 implementation checkpoint; runtime pending | 270/270 EditMode cases passed, exit 0; r02-report-editmode-fixed-results.xml. Initial 233/257 run retained: Unity inline JSON null reports required explicit presence flags and a zero-default header. Report runtime/build/human validation still pending; do not label full R02.4 delivered yet |
| 2026-10-03 | R02.4 daily report delivered | r02-daily-report: 270 EditMode cases, 179 Editor / 180 Windows checks, build/package and report screens verified. Initial JSON failures repaired and retained. Next R02.5 full-cycle human playtest/validation gate; later milestones remain planned |
| 2026-10-03 | R02.5 technical cycle checkpoint; human gate stays open | Core 4b28bfe; reproduced 20/22 before fix, corrected 273/273 tests, continuous Editor 96 checks passed. Final Windows/native-GUI/package basis: Windows continuous 96, native UI 40, old regression 180 gameplay assertions completed with no managed errors, but all three crashed at native shutdown. Candidate packaged for investigation, not final validation. Latest user feedback: not played yet. R02.5d and full R02 remain open; no R03 implementation |

| 2026-10-03 | R02.5c Unity patch validation completed | Source fb4e4d2; handoff r02-cycle-playtest. Unity 6000.3.21f1, final273 EditMode, continuous96/native40/regression180 checks and ordinary Save and quit passed with clean exits. Archive verified; installer metadata caveat retained. Next: human R02.5d two-cycle playtest; no R03 |

At future checkpoints append a concise row with task IDs, commit/tag, actual evidence, remaining risk, and exact next action. Keep this section current and short; put large raw outputs in evidence files. Do not erase history to turn an unverified or failed attempt into a pass.

### Historical R02.5 candidate on Unity 6000.3.15f1 — failed native shutdown

This section records checkpoint **9197e1c**, tag **r02-cycle-validation-candidate**, before the user authorized the patch. Its pending-approval wording and failed-player status describe that historical checkpoint; the following patch assignment is current.

- Shutdown investigation: exact matching PDBs resolve `PlatformAccessibilityTestUtilities::GetWindow` → `PlatformAccessibilityManager::GetProviderForUIA` → `PlayerMainWndProc`. This strongly matches Unity **UUM-146676**, listed as fixed in [Unity 6000.3.21f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.21f1). At this historical checkpoint, a patched-engine comparison had not run and no supported workaround was confirmed. The completed 6000.3.21f1 comparison is recorded below. Evidence: `Docs/Evidence/r02-cycle-shutdown-investigation.md`; combined process results: `Docs/Evidence/r02-cycle-run-outcomes.json`. The 6000.3.15f1 pin was preserved until the user authorized the bounded compatibility experiment.
- Windows continuous metrics: 104.22 s total, 41.37 s first preparation, 36.08 / 26.17 s trading days, 27.47 s / 87.31 m walking, 3.71 s harvesting, 29.99 s incubation, 33.56 s explicit customer wait, 0.71 s pause, six doorway crossings and 5,929 navigation samples. The route completed before the native crash. These overlapping automation timings do not establish human workload or full-shift balance.

- User feedback on 2026-10-03: latest DailyReport build **not played yet**. Human clarity, pacing and desire for a second day remain unaccepted. R03 must not start from automated evidence alone.
- [x] R02.5a: fix late restocking of an alternate display being ignored by a browsing customer; verify physical retargeting and reservation safety. Runtime result: native UI probe observed physical redirection from empty A to newly stocked B, fresh local browsing and exact reservation; Docs/Evidence/r02-cycle-native-ui/smoke-result.json. Gameplay checks passed; native shutdown failed.
- [x] R02.5b: run two continuous fresh-save cycles using actual timed collection, incubation, movement and E transfers, retaining stock and money across days. Editor passed; exact-player result: 96 gameplay checks with zero captured errors in 104.2194 s, but overall FAILED on native shutdown exit -1073741819 / 0xC0000005; Docs/Evidence/r02-cycle-player/smoke-result.json.
- [ ] R02.5c: separately probe actual native GUI clicks, build/package the exact tested player, and retain raw successful/failed evidence. Result: 40 native-UI assertions completed and candidate hashes match, but clean player shutdown is BLOCKED; no final validation pass.
- [ ] R02.5d: human playtest of latest loop; log waiting versus action and fix observed friction before marking R02 complete.

- Rule checkpoint **4b28bfe**. Original core reproduction: **20/22 passed, 2 expected failures**, retained in `Docs/Evidence/r02-cycle-before-fix-results.xml` and `.log`. Corrected full suite: **273/273 passed**, zero failed/skipped, Unity exit 0; `r02-cycle-editmode-results.xml` / `r02-cycle-editmode.log`. Original failures remain retained.
- Continuous Editor run: `Docs/Evidence/r02-cycle-editor/smoke-result.json`, **96 checks passed**, zero captured runtime errors, exit 0, **103.99 seconds**. `r02-cycle-editor.log` retains the executed process. Three natural productions, three E transfers, six doorway crossings and **87.23 m** walking; first preparation **41.18 s**, trading days **36.03 / 26.20 s**, incubation **29.89 s**, explicit customer waiting **33.57 s**, walking **27.45 s**, harvesting **3.74 s**, pause **0.71 s**. Metrics categories overlap; do not add them into a player-workload percentage.
- The fresh route uses ordinary 0.6-second held-mouse collection and ten-second incubation, synthetic WASD through the doorway, and E transfers. No seeded inventory, player teleport, scripted-harvest override or accelerated model/day delta. Application commands still drive recipe/open/serve/close/save/reload and both trading days close early. These are technical timings, not human pacing/enjoyment or full-shift balance evidence.
- Exact Windows build: **build operation Succeeded, 0 errors/warnings, 166,697,389 bytes; final player qualification FAILED at shutdown**, evidence `Docs/Evidence/r02-cycle-build-result.json and r02-cycle-build.log`. Exact continuous player: **96 gameplay checks, zero captured managed errors, 104.2194 s; FAILED overall, process exit -1073741819 / 0xC0000005**, evidence `Docs/Evidence/r02-cycle-player/smoke-result.json, cycle-metrics.json and player.log`. Inspected actual screenshots and findings: `actual continuous and native UI reports inspected at 1280x720: sums readable and Continue fully visible, no UI blocker observed. Captures 40-44 and ui-01 through ui-11 retained; native controls clicked from observed window states. Screen evidence does not prove clean shutdown`.
- Native GUI probe: `11 real Computer Use clicks (Enter shop, +Bloom, +Flow, Begin incubation, Open shop, Serve customer, Request close, Confirm close, Continue preparation, Save and resume, Reload); 40 assertions, zero captured managed errors, 197.0229 s. Overall FAILED with -1073740771 / 0xC000041D; original native exception 0xC0000005. Stock fixtures, teleports and one arrival isolate these button checks`, evidence `Docs/Evidence/r02-cycle-native-ui/smoke-result.json, player.log, ui-step.json and actual screen captures`. Only actually clicked controls are covered. Additional failed attempts/fixes, if any: `the continuous player and existing full regression also crashed at native shutdown. Regression completed 180 assertions with zero captured errors in 191.4164 s, then exited -1073741819 / 0xC0000005. Keep every failed run and Docs/Evidence/r02-cycle-run-outcomes.json; inner passed:true JSON does not override process failure`.
- Continuous day one expects 2 visitors, 2 sales, 12 revenue, 2 opening stock, 1 restock and 1 carried stock. Day two expects 2 visitors, 0 sales/revenue, 1 interrupted reload, 1 unpaid closing cancellation and the same retained specimen. Final evidence: `both Editor and Windows assertion sets matched those totals; r02-cycle-editor/ and r02-cycle-player/ each retain cycle-metrics.json, cycle-night-two.json and cycle-night-three.json. Final gameplay state is night3,12credits,one exact unsold Pondlet`. Reloading reports grants no payment. Schema remains envelope 3 / game 1 / cycle 1 / report 1; legacy backups remain unchanged.
- Archive `EvolutionShop-CyclePlaytest-Candidate-Windows.zip`: **69,139,545 bytes**, SHA-256 **8443592145C66683BCEDFC1A5F3E2C60A22AF6C0981C872B67B880463446EFBF**; **293** tested-runtime files in **295** entries verified by `Docs/Evidence/r02-cycle-package-verification.json and outputs/Evidence/cycle-package-verification.json`. Scene hash verification: `unchanged SHA-256 2F81309DDB7CAA56E0E56B4D6B705EB274E234AE9CF0D7BCBB968373AB9AE16F`. Candidate gameplay source: `2ab4929`; evidence checkpoint `9197e1c`, tag `r02-cycle-validation-candidate`.
- At this historical checkpoint, no later milestone had been implemented. R02.5d and full R02 were open and the proposed engine patch had not yet been authorized. The user later approved the patch; the completed validation and current next action are recorded below.

### R02.5c completed patch validation: ready for human playtest

- The user explicitly authorized installing **Unity 6000.3.21f1**, updating only Evolution Shop, and rerunning validation. No approval is pending for that bounded work. Protected projects and R03+ remain outside this assignment.
- [x] R02.5c1 Install and verify the authorized editor payload alongside existing versions. Project pin and executed editor identify `6000.3.21f1 (c02631ffc030)`; Windows Mono payload is present and tested. The CLI exited 6 because writing `modules.json` was denied; metadata remains absent. Valid Authenticode and exact published MD5 verify the installer. This qualification does not claim successful Hub metadata registration. Evidence: `Docs/Evidence/r02-patch21-installation.md` and installer verification receipt.
- [x] R02.5c2 Complete the migration/package/settings/save review: Completed: reviewed engine/package/settings changes, retained unchanged scene/GUID and all eleven baseline fixture/user-save hashes, and passed the final exact-manifest 273-case test suite. Direct manifest requirements are restored to candidate 9197e1c; the editor requires resolved Burst 1.8.30, Collections 2.6.8 and Performance Testing 3.5.0. Keep the resulting lockfile, without temporary added direct dependencies or forced older exact pins. See `Docs/Evidence/r02-patch21-migration.md`.
- [x] R02.5c3 Require successful EditMode, continuous player, native click/retarget, prior integrated regression and ordinary save-and-quit checks, including clean process exits: Completed: final EditMode 273/273, continuous 96 checks, native UI 40 checks, prior integrated regression 180 checks, and ordinary Save and quit all completed with process exit 0. Keep all failed 6000.3.15f1 evidence and the failed initial 21 output-copy attempt.
- [x] R02.5c4 Package the exact qualified player, verify every runtime file hash and update this guide: Completed: EvolutionShop-CyclePlaytest-Windows.zip contains 290 runtime files matching the tested build by hash, plus two empty-directory entries. Human R02.5d remains open regardless of automated outcomes.

- [x] R02.5c Current technical gate completed: patch migration, real native GUI interaction, clean player shutdown and exact package verification. The unchecked R02.5c in the preceding historical section records the older failed candidate.

Verified patch work:

- Patched Editor XML results: `r02-patch21-editmode-results.xml`, `r02-patch21-pinned-editmode-results.xml`, and `r02-patch21-baseline-packages-results.xml` each record **273 passed, 0 failed, 0 skipped**. The last run still had three temporary direct package entries; later removing them restored the baseline manifest without changing their already-resolved newer versions. Final exact-manifest test/process evidence: Docs/Evidence/r02-patch21-final-editmode-results.xml and r02-patch21-final-editmode-process.json: 273 passed, 0 failed/skipped, process exit 0.
- First patch build to the old `Builds/Windows` folder failed with five file-copy errors. Preserve `Docs/Evidence/r02-patch21-build.log` and `r02-patch21-build-result.json`. Old crashed-player remnants may hold output files; this is an inference, not a proven owner diagnosis. BuildWindows now derives `Builds/Windows-<Application.unityVersion>` using Path.Combine and retains the exact-project guard. Current target: `Builds/Windows-6000.3.21f1/EvolutionShop.exe`. No unrelated processes were forcibly stopped.
- Unity import/build serialization review: Only three iOS thermal-default fields remain as Unity-written ProjectSettings normalization. A temporary InputSystem preload entry was reviewed and restored to the original empty list; it was not a URP preload or authored gameplay change.
- Scene and user-save preservation: pre-patch baseline is retained in `Docs/Evidence/r02-patch21-upgrade-baseline.json`. Final scene SHA-256: 2F81309DDB7CAA56E0E56B4D6B705EB274E234AE9CF0D7BCBB968373AB9AE16F (unchanged). Normal user save/archive hash comparison: all eleven immutable baseline files match SHA-256 and byte length, including scene/GUID, historical fixtures, normal user slot, rotating backup and immutable v1/v2 archives; Docs/Evidence/r02-patch21-baseline-comparison.json. Save contracts remain envelope 3 / game 1 / cycle 1 / report 1, with v1/v2 byte-preserving migration archives.

Final patched evidence:

| Check | Actual completed outcome / evidence |
|---|---|
| Windows build | Succeeded, 0 errors/warnings, 167,088,274 bytes; build process exited 0. Docs/Evidence/r02-patch21-versioned-build-result.json and .log |
| Continuous fresh-save route | 96 checks, 0 captured errors, 104.32 seconds, process exit 0; Docs/Evidence/r02-patch21-continuous/smoke-result.json, cycle-metrics.json and process-result.json |
| Real native GUI clicks / retarget | 11 actual Computer Use clicks, 40 assertions, 0 captured errors, 179.18 seconds, process exit 0; Docs/Evidence/r02-patch21-native-ui/ plus r02-patch21-native-click-observations.json. Includes actual late-stock redirection and local reservation |
| Existing full regression | 180 checks, 0 captured errors, 191.69 seconds, process exit 0; Docs/Evidence/r02-patch21-regression/smoke-result.json and process-result.json |
| Ordinary Save and Quit | Real Save and quit click at the fixture pause panel; process exit 0. Isolated format-3 slot retained night/day 2, six credits, one sale/revenue 6 and empty displays. This intentionally interrupted fixture has no completed smoke report and is not counted as another 40-check pass. Docs/Evidence/r02-patch21-normal-quit/process-result.json, quit-validation.json and r02-patch21-native-click-observations.json |
| Actual screen review | Actual 1280x720 patched native-UI report, queued order and restored result captures were reviewed: totals reconcile, all report/Continue and checkout controls are readable, with no blocking clipping; Docs/Evidence/r02-patch21-native-ui-visual-review.md |
| Archive | EvolutionShop-CyclePlaytest-Windows.zip; 69305750 bytes; SHA-256 2CE68448011D465600CDA41D4BD46D3A6EC70EF7610362F716922E5F4C4C39A6; 290 matched runtime files / 292 archive entries. Receipt outputs/Evidence/patch21-package-verification.json; repository copy Docs/Evidence/r02-patch21-package-verification.json |
| Source checkpoint / tag | fb4e4d2 / r02-cycle-playtest (final guide/evidence delivery checkpoint) |

Every required patched-player process exited cleanly, independently of its inner assertions; the native shutdown blocker is resolved in this verified configuration. Preserve the earlier failed native process receipts. For future validation, an inner `passed:true` never overrides a native crash or nonzero process exit.

No later milestone was implemented. R02.5d and full R02 remain open: the user's latest report is **not played yet**. The continuous route uses natural production and actual synthetic movement/held-mouse/E but script-controlled camera/menu commands and early closing. Native GUI coverage applies only to actually clicked controls; the probe uses stock fixtures/teleports and a single arrival. Neither route proves enjoyment, a full eight-minute shift's pacing, listened-to audio, or untested settings-slider interaction. Next human action is two preparation/trading/report cycles and feedback on clarity, waiting, restocking workload and desire for another day. R03 is not ready.

- Final environment receipt: `Docs/Evidence/r02-patch21-final-environment.json` confirms all four tested parent processes exited 0 and no matching Application Error events were found after the first patched launch. Four older EvolutionShop process entries with inaccessible executable paths remain; their ownership is unconfirmed and they were left untouched. This is not a claim that no game-process entries exist.

- Patch checkpoint **fb4e4d2** retains the authorized editor/project-version update, resolved package minimums, three platform-default settings fields and versioned build output. The exact gameplay binaries above are qualified. Handoff tag **r02-cycle-playtest** identifies the final guide/evidence delivery checkpoint.
