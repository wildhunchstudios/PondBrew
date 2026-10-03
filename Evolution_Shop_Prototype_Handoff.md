# Evolution Shop — First Playable Prototype

**Prepared:** October 3, 2026  
**Status:** Proposed implementation brief, not a Unity project or a tested build.  
**Working label:** Evolution Shop. This is not an approved commercial title.

## 1. Purpose and decision boundaries

Build a small first-person 3D prototype that tests this experience:

> I grew living cultures, combined them into a creature, sold it, bought an improvement, and immediately wanted to make something else.

The user's stated concept is growing small organisms/cells, combining different types and quantities to create creatures, selling them in a store, improving equipment and the business, hiring workers, and eventually automating production. Funny worker/customer interactions matter. Aquatic creatures are the leading theme, not an irrevocable decision. The ultimate objective remains undecided.

The exact recipes, counts, equipment, controls, balancing values, and milestones below are reversible prototype recommendations. Do not describe them as final user-approved game design. The previous discussion's full-release creature count, campaign length, pricing, and aquarium-opening ending are not commitments.

This is a NEW project. Do not modify, reset, migrate, or import code/assets from the Dungeon Roguelite / Dungeon Project or the PersonalAssistant project. Reuse general workflow knowledge, not another project's files or history.

## 2. Development workflow

Use the user's established Windows / local Codex / Unity workflow. Default to Unity 6 with Universal Render Pipeline (URP) and C#. Inspect the actual installation and project before selecting or changing an editor or package version. Prefer a compatible, supported, installed editor over an unnecessary migration; pin the selected editor and package versions once verified.

The user supervises creative decisions and playtests. The coding agent performs implementation, scene setup, procedural placeholder creation, tests, and build attempts through the tools actually available to it. Never assume that a remembered Unity or MCP setup is currently running.

Work only in the explicitly selected new workspace. Use ordinary workspace-scoped permissions. Request narrow approval for genuinely necessary privileged/system changes; do not request unrestricted access, modify global settings silently, or purchase assets/services.

Proceed through routine implementation decisions within this milestone without repeated approval requests. Stop for a genuine tool/permission blocker, ambiguous workspace ownership, or a product decision that materially changes the agreed concept. Save a useful checkpoint before stopping.

## 3. Checkpoint 00 — Verify the workspace

Before editing game content:

1. Report the absolute workspace path and inspect its existing contents. Preserve user files. If it contains an unrelated project, stop rather than replacing it.
2. Detect the installed Unity editor(s), available build modules, existing project version if any, and usable local tools. Do not guess installation paths or package versions.
3. Verify the Unity editor connection, including the exact project path returned by the connection. Multiple open editors must not cause a write to the wrong project. Prefer the existing working integration; do not add a competing editor bridge without a reason.
4. Create or initialize the new URP project only inside the approved workspace. Use a documented, available project-creation route. If installation, licensing, or a GUI action is required, report the exact blocker and minimum user action.
5. Establish project-local version control/checkpoints. Do not rewrite history, delete unrelated files, or publish to a remote service.
6. Create a minimal running scene, verify compilation, enter Play mode if possible, and capture evidence before expanding the scope.

If the editor cannot be run, bounded source scaffolding may still be useful, but label it **unverified**. Do not call generated scripts a playable or tested game.

## 4. Milestone 01 — Manual loop with one meaningful upgrade

### Play space

One compact room combining a little lab and a sales counter. Include two culture growers, one incubator, two display tanks, a simple sales interaction, and an upgrade station. Fixed placement is sufficient. No construction editor is needed.

Use readable signs, clear interaction prompts, comfortable first-person movement, and a basic pause menu with mouse sensitivity and sound controls. Do not add combat, jumping puzzles, a large exterior, or an elaborate inventory grid.

### Cultures and handling

Use two cultures with temporary identities: **Bloom** and **Flow**. They must differ by label and silhouette as well as color.

Both growers replenish culture without charging money. Growers have visible capacity limits and pause at capacity. This guarantees recovery after spending currency or making mistakes.

Create a simple culture siphon. Aim at a grower and hold an action to gather visibly moving organisms into a transparent reservoir. Dispense measured integer doses into the incubator. Use generous targeting and snapping; avoid precision physics chores.

Prefer a lightweight visual approximation over realistic fluid simulation. Collection sound, motion, and immediate response are part of the prototype, not optional final polish.

### Three discoverable creatures

| Temporary species | Exact recipe per specimen | Distinct presentation |
|---|---|---|
| Pondlet | 1 Bloom + 1 Flow | Small teardrop body with a simple tail |
| Bubblebelly | 2 Bloom + 1 Flow | Rounded body and short fins |
| Ribbonfin | 1 Bloom + 2 Flow | Long, narrow body and flowing tail |

Use exact integer quantities, not ratios. Batch size is a separate concept: two copies of a recipe do not silently become a new species. Milestone 01 may limit the incubator to one specimen per cycle and omit a batch UI.

Give each creature a short, different swim/idle animation. Smooth procedural placeholders are acceptable. Do not present voxel/blocky characters as the intended art direction. Different colors alone do not count as three distinct creatures.

A valid recipe consumes resources once, matures, then produces exactly one creature. Prevent repeated inputs from duplicating creatures, money, or ingredients.

For this first milestone, an unsupported recipe should be clearly identified and remain recoverable without resource loss. A richer system of experimental failures and clues belongs in a later milestone.

Record each discovery automatically in a tiny journal. Reproducing known recipes should be convenient, not require re-solving the combination. Keep the first-discovery reveal brief and make repeated production faster to interact with.

### Display and sale

Move completed creatures into a visible aquarium with a simple forgiving transfer interaction. They should swim within its bounds rather than behave like loose rigid bodies.

Sell a chosen stocked creature at the counter. A stationary placeholder buyer is sufficient for this milestone; customer navigation and queues are not required. Selling removes one actual specimen and awards currency once. A recorded discovery remains recorded after the specimen is sold.

Show currency, carried cultures, machine readiness, and the current objective clearly. Do not build a sophisticated pricing simulation or market-demand system yet.

### Upgrade

Provide one purchasable siphon upgrade that visibly changes the tool and its behavior. A suitable initial change is reservoir capacity from four to eight culture units, with an obvious larger chamber and faster collection. These are test values, not final balance.

The upgrade should make preparing repeat creatures noticeably less fiddly. Do not rely on a nearly invisible percentage increase alone. Allow a development-only before/after comparison.

### Initial balancing assumptions

Start with zero currency and free, replenishing cultures. A useful starting tuning set is two seconds per new culture unit, ten seconds per incubation, sale values of 6 / 10 / 10, and an upgrade cost of 24. Keep all values centralized and editable. Adjust through playtesting rather than treating these numbers as validated.

Grower refill and incubation should continue while the player performs other activities. Do not add offline rewards, real-world waiting, login requirements, or paid acceleration.

Target the first creature within about two minutes and the first noticeable upgrade within about five minutes of a fresh player starting. These are playtest hypotheses, not guaranteed completion times.

### Basic persistence

Include one local save slot and a clearly confirmed new-game/reset action. Preserve currency, cultures, discoveries, owned creatures/tank placement, upgrade state, and any active incubation state. Avoid duplicating or losing an in-progress batch on load.

Elapsed simulation time may pause while the application is closed. Explain that behavior. No account, cloud save, or backend is needed.

## 5. Implementation boundaries

Keep the implementation small and data-driven: recipe/species definitions, culture inventory, production state, transactions, upgrades, and save data should not depend on hard-coded display text. Avoid a large generic framework.

Use stable IDs for saved entities and definitions. Clamp capacities and reject invalid/negative transactions. Reserve or validate destination capacity before consuming an output. A full tank or occupied incubator should pause/block an action clearly, never silently delete a creature.

Provide a repeatable scene-setup route appropriate to the available editor tools. Record third-party asset provenance and licenses. Prefer original simple placeholders and built-in capabilities; no purchased assets or external paid APIs without explicit approval.

Defer worker hiring, full customer AI, humor scenes, automatic production lines, decorating systems, multiple shops, multiplayer, land animals, random rare variants, complex genetics, realistic aquarium chemistry, and the main campaign ending.

## 6. Checkpoints and validation

Use small tasks with saved checkpoints:

- **00:** Correct workspace/editor verified; base scene runs.
- **01A:** Comfortable movement and responsive culture harvesting.
- **01B:** Three recipes, incubation, distinctive creatures, and discovery journal.
- **01C:** Tank transfer, sale, currency, and visible siphon upgrade.
- **01D:** Save/load, error handling, fresh-start playtest, and build attempt.

Test recipe matching, ingredient consumption, unsupported inputs, capacity limits, sales, insufficient funds, duplicate interaction protection, discovery persistence, and save/load round-trips. Test a save during incubation and after a sale.

In Play mode, verify one complete fresh-start loop, all three discoveries, the upgrade's physical effect, a full-tank case, and correct pause/cursor behavior. Verify the standalone Windows build separately when the toolchain permits; editor success is not proof that the build launches.

Automated tests can verify logic, not whether collecting feels enjoyable. Capture an actual Play-mode screenshot or recording when possible and ask the user to evaluate the feel after this milestone.

Milestone acceptance requires: a repeatable complete loop, visibly different discoveries, a perceptible upgrade, no known critical progression blockers, and documented validation. Performance measurements must name the actual machine/context; do not invent frame-rate claims.

End with the actual build/scene path, controls, completed tasks, commands and tests run, observed results, known issues, any unverified checks, and the next resume point. Never report a screenshot, successful test, or launched build that did not occur.

## 7. Minimal project records

Create or preserve project-local instructions and these lightweight records:

- `AGENTS.md`: project boundaries, workflow, validation, and continuation rules.
- `Docs/PROTOTYPE_BRIEF.md`: current prototype scope and explicitly provisional choices.
- `Docs/TASKS.md`: mini-tasks and status.
- `Docs/RESUME.md`: last verified state, blockers, and exact next action.
- `Docs/VALIDATION.md`: test/build evidence and limitations.

Do not spend the first milestone building a separate dashboard application. Save progress in these files after meaningful checkpoints so a new session can continue safely.

## 8. What follows after the user's first playtest

**Milestone 02 — Prove automation and humor:** add one automatic dosing/production module, one reliable hireable worker, a small customer behavior loop, and one authored funny interaction. The player should use the freed time to pursue a new discovery rather than lose all active tasks.

**Milestone 03 — Presentation sample:** polish one part of the shop, one tool, one creature, one customer, and a discovery effect to establish the intended visual/audio quality before expanding content.

Do not begin these automatically after Milestone 01. Record them in the backlog, deliver the playable result, and use the user's playtest to choose the next changes. A technical pass is not a creative approval.

## 9. External tool references

These primary references were checked while preparing the brief. They describe tool capabilities, not evidence that the user's local tools are installed or connected. Verify current compatibility locally before changing dependencies.

- Unity 6 release support: https://unity.com/releases/unity-6/support
- Codex project/workspace behavior: https://developers.openai.com/codex/projects
- Codex IDE extension: https://developers.openai.com/codex/ide/
- MCP for Unity maintainer repository: https://github.com/CoplayDev/unity-mcp
