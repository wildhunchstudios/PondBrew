# Evolution Shop

A separate first-person Unity game about growing aquatic creatures in a back lab and personally serving customers in the front store. The current R02 loop is **prepare at night → stock displays → trade during the day → review results → prepare again**. The store has two display tanks and a checkout counter; the lab has free Bloom/Flow growers, an incubator, and a siphon upgrade bench.

**R03.2 customer types (2026-10-03):** the build in `Builds\Windows-6000.3.21f1` now has price tags (R03.1), four customer types, up to two visitors at once and a two-place counter queue. It built with zero errors or warnings (167,183,341 bytes); EditMode 424/424; the exact player passed the customer-types scenario (107 checks), the pricing scenario (47), the integrated regression (180) and the continuous two-day route (96), each with zero runtime errors and a clean exit. Neither pricing nor customer types has been playtested by a person yet. Details and limits are in `AGENTS.md`.

**R02 validation status:** Unity **6000.3.21f1** built successfully with zero errors or warnings (167,088,274 bytes). Its native UI probe passed 40 checks, and the continuous two-day route passed 96 checks in 104.3 seconds; both had zero runtime errors and clean process exits. The integrated regression passed 180 checks with zero captured runtime errors and exit 0. Ordinary Save and quit also exited 0, and the final EditMode suite passed 273/273. The preceding 6000.3.15f1 candidate's native Windows shutdown failure remains historical evidence. The installer could not write Hub module metadata, but the verified editor/Windows Mono payload built and ran successfully; see `AGENTS.md` for exact evidence and limits. The user has now playtested the current build and reported **"i playtested currently works great"**. R02 is accepted for progression; R03.1 pricing is ready and has not started. No detailed timing, audio/settings or individual physical-key checklist was reported.

## Run and play

Run `C:\dev\DumbVideoGameStudio\Builds\Windows-6000.3.21f1\EvolutionShop.exe`, then click **Enter your shop**. If using an archive, extract it completely and keep the executable beside its data/runtime folders. Consult the validation status above before treating a candidate as the tested delivery.

1. During untimed preparation night, collect one Bloom and one Flow in the back lab. At the incubator, click **+ Bloom**, **+ Flow**, then **Begin incubation**. A Pondlet grows in ten seconds.
2. Press **E** at an empty front-store tank to transfer the ready specimen. Each tank holds one creature; a full tank leaves the finished output safe in the incubator.
3. At the counter, choose **Open for day 1**. Trading lasts up to eight minutes. Up to two visitors at a time walk among the front displays, choose available stock, and line up at the counter (two places) with visible patience timers. There are four kinds of customer, each with its own look: the **Casual visitor** (beret), the **Bargain hunter** (yellow cap; spends at most 8 credits, very patient), the **Collector** (bowler and glasses; pays up to 2.5× for Bubblebellies and Ribbonfins and heads for them first, but only a fair price for Pondlets) and the **Big spender** (top hat; pays up to 2.5× but waits only 30 seconds). The journal (Tab) lists them.
4. Set prices with **E** at a stocked tank: **-5 / -1 / +1 / +5** change that species' price tag (1 credit up to 3× its standard price) and **Use standard price** resets it. The panel shows how each kind of customer will take it: **Bargain**, **Fair price**, **Pricey, still buys**, **Too expensive** or **Over budget**, and how many of the four kinds will buy. A visitor who finds a tag too expensive says so, tries a display they have not refused, or leaves. The price is agreed when they reserve, so changing a tag never changes an order already on its way.
5. Return to the physical counter, press **E**, and click **Serve customer** to complete the first waiting purchase; the next customer then steps up. Refill empty displays as you work. Customers can discover stock added during browsing and walk to that display before reserving it. The lab remains accessible and continues producing during the day.
6. Choose **Close for the night...**, then **Close today's shop**, or let the day expire. Closing provides three seconds for accepted checkout; unpaid orders release safely. Unsold stock remains yours.
7. Review income, visitors, missed sales (including **Too expensive**), and stock movement. Click **Continue preparation**, or press Escape. The report stays available through **Review day N results** at the night counter. Report income is separate from current cash; there are no employees or wages yet.

Both cultures replenish for free. Unsupported doses can be returned safely; surplus carried culture can be released at the incubator. Discover three creature types and save 24 credits for the larger, faster **siphon MK II**.

| Control | Action |
|---|---|
| WASD / mouse | Move / look |
| Hold left mouse while aiming at a grower | Collect culture into the siphon |
| E while aiming at a station | Open its panel; transfer ready output at an empty tank; set the price tag at a stocked tank |
| Tab | Open or close the discovery journal |
| Escape | Close a panel; open or leave pause |
| Mouse in panels | Dose, incubate, open/close trading, serve, upgrade, and change settings |
| F8 in Editor/development builds | Compare the purchased siphon with its original form; requires at most four held/staged cultures |

## Saves and source

The pause menu contains sensitivity, volume, **Save and resume**, **Reload local save**, confirmed **New game...**, and **Save and quit**. Progress saves automatically to `%USERPROFILE%\AppData\LocalLow\DumbVideoGameStudio\EvolutionShopPrototype\evolution-shop.json`. Reload cancels unpaid visits, preserves their creatures, and retains completed sales and price tags. Saves are now format 5. An R03.1 (format 4) save keeps its price tags; an R02 (format 3) save loads with standard prices. The first time the game saves over an older file, its exact bytes are kept as `evolution-shop.json.v4.bak` or `.v3.bak`. Invalid saves are preserved with an on-screen saving error. Automated validation uses separate evidence-directory slots and must not replace the normal player save.

Pause stops production and the day clock. Ordinary station panels and the daily report keep simulation running. There is no offline progress.

Open `C:\dev\DumbVideoGameStudio\EvolutionShop` with Unity **6000.3.21f1**, then open `Assets/EvolutionShop/Scenes/Shop.unity` and enter Play mode. The existing scene is validated and preserved by normal build/smoke commands; **Rebuild playable scene** is an explicit replacement operation.

Read **`AGENTS.md`** before continuing development: it is the current roadmap and evidence summary. Raw validation records are under `Docs/Evidence/`; older handoff/task/resume documents are historical. No code or assets are imported from Dungeon Project or PersonalAssistant. Procedural art/audio and balance remain provisional.

The R02 build is accepted from the user's successful playtest; it remains available in `EvolutionShop-CyclePlaytest-Windows.zip`. R03.1 pricing and R03.2 customer types are technically validated and await a playtest; R03.3 reputation is the next ready assignment. Detailed pacing/audio/settings coverage remains unmeasured. See `AGENTS.md` for the bounded scope.
