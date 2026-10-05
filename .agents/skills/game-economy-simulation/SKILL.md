---
name: game-economy-simulation
description: Investigate Evolution Shop production, prices, upgrades and progression with reproducible simulations from the current domain model. Use for balance questions, not to silently approve new tuning or future staffing rules.
---

# Game Economy Simulation

Read AGENTS.md current milestone and Core domain definitions before assuming any rate, price or unlock is implemented. The actual store/lab economy makes this skill relevant now. Staffing, wages, automation and Grand Exhibition specifications include future plans; label hypothetical inputs explicitly and do not implement later milestones through a simulation task.

Inputs: one player/economy question, source checkpoint, rules/parameter source, initial state, strategy assumptions, time horizon, seed and acceptance metrics. Use pure Core simulation or a small isolated runner; never the normal user save. Model untimed preparation versus daytime trade, finite stock/culture/incubator/storage capacity, customer patience and agreed-price reservations, committed payments, upgrades and bottlenecks relevant to the question.

Report time to afford/unlock, income per active minute/day, idle/blocked production, stockouts and alternative investment choices. Include low-cash recovery and sensitivity to prices/rates; preserve the invariant that cancelled/unpaid visits create neither money nor duplicate specimens. For probabilistic inputs publish seed/distribution and enough runs to explain variability, not a single convenient outcome.

Keep analysis outputs and proposed tuning separate from approved balance. Validate the model against current domain tests and at least one known state transition, then explain where simulation omits navigation, player effort, readability or humour timing. A fast automated scenario is not evidence that an eight-minute trading day feels good.

Output: reproducible command/script, source/inputs, result tables, invariants checked and a bounded recommended tuning experiment. Update AGENTS.md only with supported results. Stop before changing production tuning, save schema or milestone scope without the user's task authorizing that change.
