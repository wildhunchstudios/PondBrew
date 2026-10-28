# FX-01 — Living Shop cue and asset contract

Frozen initial design, 2026-10-11. Accountable owner: root, acting as designer, animator, technical artist, audio designer and QA. Baseline: `dbf55c8` / `fx-baseline-audited`; runtime source is still the FX-00 diagnostic adapter over the approved R08.4 game. This document records decisions for FX-02–06; the current roadmap/status stays in **AGENTS.md**. No new animation, effects, hands, sounds or game build are implemented by this assignment.

## 1. Player purpose and hero sample

Make growing and collecting feel like operating a small living workshop, then make its Pondlet feel alive in a cozy aquarium. Preserve the honey/teal aquatic front shop, mint quirky back lab, opaque automatic door and accepted equipment. Rounded cream gloves and teal cuffs fit the toy-like horizontal collector; its coral suction bell remains the visual action centre. Routine beats are quiet and repeatable. A first hatch is the one bigger warm, bouncy reward in this sample.

The sample is **Bloom + Flow → existing Pondlet recipe → current incubation → fixed tank A → actual customer sale**, plus one installed collector module showing off/waiting/blocked/committed collection. Dosing, return-dose, ordinary readiness, refusal, cancellation and existing upgraded-tool geometry are required. Use recipe definitions, current collection interval and production countdown directly; no durations here alter them. Spark/Mist/conditioning, other four fish, staff/comedy/room/UI expansion keep their existing FX-07–20 assignments. Their policies must be compatible, but their assets are not bulk-produced before FX-06.

## 2. Source facts that determine the design

`ShopWorld.cs` currently builds and animates growers, chamber, tool and display residents. Its intake particles are created/destroyed every .12s and last .52s; the tool root and nozzle have a single current pose writer. `ShopWorldLabRefresh.cs` attaches the approved CollectorKit body/cap and fixed IncubatorFrame. `PondletSample.cs` owns the petal geometry; `CreatureVisual.cs` animates fins/tail; container display owns the swim root. `ShopWorldArtDirection.cs` owns the local warm/mint palette and cleanup. `ShopAudio.cs` owns two current mono 22050Hz voices, service and comedy, with service priority. `ShopSession.cs`, `ShopTutorialSession.cs`, `ShopAutomationSession.cs`, `ShopSpecimenStorage.cs` and the model own input/results/state. Consult `fx-00-BASELINE.md` for the full existing owner map.

Important corrections: the **actual CollectorKit muzzle is toolBody-local (0,.05,.489)**, from `ArtSource/CollectorKit/manifest.json`. The old nozzle light is toolBody-local (0,.05,.489) only after its parent offset .17 is included; never attach a new effect to a guessed child-local .319. Use the authored mouth socket directly. The original `incubationRing` is hidden by the accepted fixed frame; do not pretend rotating it delivers a new working animation. Add a small independent visual gauge/valve or animate the fluid and lamp. **Do not lift or rotate the accepted opaque frame/canopy.** CollectorBody is 6,692 triangles, bounds X±.179081/Y-.258735..+.1645/Z-.179..+.495; cap is 1,196 triangles, chamber-local bounds X/Z±.121/Y.117.. .178. These are historical source-manifest measurements, not new import/performance results.

FX-00 measured 120FPS-capped p95 frame intervals8.53–8.55ms and GPU2.93–4.07ms on i7-14700F/RTX4070TiSUPER/64GiB, D3D12/PC_RPAsset, with four pre-existing player processes. Current steady-view GC p95 ranged6,400–15,724 bytes; main-thread samples include waits and the render-thread counter was unavailable. Ordinary resources included395 renderers/195 loaded materials, busy lab746/238. These justify bounded incremental work, not a zero-allocation or minimum-PC claim.

## 3. Cue hierarchy, action keyframes and timing

Priority5 transaction/refusal;4 committed production/readiness;3 valid active interaction/blocked state;2 work motion/comedy;1 idle/personality;0 ambient. Priority is per channel: a persistent blocked lamp does not occupy a particle or sound voice. Service always protects feedback from comedy. Within one owner, a new manual command supersedes its old routine visual; same-key duplicate events are ignored. No long queue of old cosmetic actions. When two valid units arrive during a settle, update the true chamber count immediately and coalesce the tiny pulse instead of delaying or replaying it.

All event timings are milliseconds and **follow** their accepted command. Anticipation is a visual beat, never an input lock or a precondition to commit. Interpolation uses monotonic smooth ease for position and gentle cubic recovery. No spring overshoot across a numeric bound, camera shake, head bob or hidden animation callback with gameplay authority. State cycles use actual work progress or seeded phase; they never restart on every Refresh.

### Operating hands: numeric key poses

Capture base rest transforms once. Values below are deltas in toolBody-local axes before the existing camera/tool transform and positive .62 tool scale. Right palm socket (.035,-.19,-.08), left support (-.06,-.13,.10) are initial fit targets, not proven rig measurements. Grip follows the existing angled mint handle; support touches the solid cradle. Fitting may shift individual palm sockets by at most20mm to avoid mesh penetration, recorded in the eventual manifest. Movement never moves the player/camera/collider.

| Beat | Pose at anticipation / action / settle | Envelope and exit |
|---|---|---|
| Idle / movement | Both wrists at rest; index curls at grip rest. Actual planar travel can add ±6mm lateral/vertical sway and ±1.5° tool-body rotation | Reduced motion: fixed rest. Stop sway when input disabled or an action owns the pose |
| Collect start80/120/100 | Lift4mm + roll2° at80ms; right index curls12° and thumb supports at200ms; settle to hold at300ms | No held input means immediate emit stop; cancel without completing a fake unit |
| Collect hold | Stable support, index stays12°; optional nozzle local response±2°, tool recoil at successful unit Z=-8mm peak120ms, rest280ms | No old39Hz nozzle jitter layered underneath. Full/empty/lost target stop now |
| Collect end0/80/140 | Uncurl index over80ms; return palms/tool to captured rest by220ms | No bounce on cancellation; hidden tool resets immediately |
| Dose70/160/170 | Support wrist rolls6° inward with4mm lift; index relaxes; tilt peaks at230ms and returns by400ms | Only gameplay-visible reachable action can move hands. Modal menu dosing shows station feedback, no reaching through its panel |
| Place60/160/180 | Left support opens index10° and advances≤10mm; palm lowers6mm; rest by400ms | Destination-only settle. There is no persistent held-fish mechanic to depict |
| Refusal0/80/100 | Tiny≤1° wrist stop; rest180ms, existing reason visible immediately | No pour/flow/success sparkle; one explicit rejection cue |
| Upgrade80/180/220 | Existing chamber size becomes true immediately; hands spread support≤15mm, gentle2° acknowledgment, rest480ms | Cap inherits chamber scale; no independent cap motion or scale double-application |
| Slap100/90/240 | Existing action/contact/recovery envelope; temporary empty-hand gesture uses the existing intentional slap owner | Existing cooldown/target rules; interrupt collection; no second independent SlapHand writer |

The final **combined** hand/tool action envelope is toolBody-local X[-.23,.23], Y[-.38,.38], Z[-.24,.52], including accepted upgraded chamber and cap (top .067+.178×1.48=.33044m before pose). This is a new admission limit, not measured art. The accepted tool geometry remains fixed; new palms must fit around it. Existing ToolRoot base is camera-local(.33,-.16,.58), rotation(-5,-7,-5), scale .62. Preserve that base and add resolved deltas once. Readout rectangle .135×.030m centred(0,-.129,-.182) stays unobstructed.

At current actual FOV inspect720p/1080p with both upgrades and pitch extremes. Additive hands/effects must avoid the central crosshair square x/y=.46.. .54 and existing IMGUI header, cash, crew, tutorial and lower interaction controls. Do not impose a new tool-only camera, clipping shader or render layer unless actual admission proves it necessary. Near-plane or HUD conflict means refit the new hands; it does not authorize moving gameplay roots.

### Lab key poses

| Moment | Numeric pose / effect | Authority / limits |
|---|---|---|
| Full grower | Small organisms remain, optional drift≤15mm; pump/valve at rest; true filled amount and FULL shown | Actual units/capacity. Refill animation stops at full |
| Refill | Optional valve0→8→0° over each real refill fraction; small working shape; no exported fluid until a real unit exists | No always-on pump pretending work; units appear only when state contains them |
| Valid collecting | Outlet accent80ms, local valve8°; ≤6 droplets/s, lifetime≤.52s, curved arc rise≤.12m, diameter≤.04m | Begin at station outlet, end at actual mouth; remove at mouth. Accepted unit gives separate chamber pulse; no stream through walls |
| Staged dose | Small downward plip at incubator-fluid anchor; swirl peaks160ms then settles170ms | Real dose delta/culture. Panel commands can show vessel response while hands stay tucked |
| Incubating | Fluid/cell circulation at true incubation fraction, gauge motion≤12°; no frame movement; lamp remains actual working state | Current10s/configured batch time; pulse1200ms max subtle cycle, not extra production |
| First hatch |120ms fluid gathers; fish fins uncurl420ms; four soft bubbles≤.035m; settle660ms | One1.2s interruptible presentation after actual first Ready. No mandatory cutscene, discovery replay or specimen duplication |
| Repeat ready |60ms gather,180ms wake,260ms settle; two bubbles | Actual repeat batch; no loud first-discovery flourish |
| Ready/blocked | Steady lamp and existing READY/destination reason; circulation settles180ms | A blocked destination is an overlay on Ready, not a new IncubatorPhase. No flashing or retry chime |
| Module operation | Installed collector valve6°→0 over300ms after accepted automatic unit; small local tick | Only actual module result/request; off/waiting/paused/blocked has still mechanism and true text; never animates player hands |

All new lab motion/effects remain inside the approved fixed footprint and existing solid height. New valve/gauge parts use visual-only pivots on a named child, at most±12° and≤10mm movement. Their full sweep must remain within their existing solid housing plus20mm visual clearance, never in operator/door/aisle clearance. Source-output arc is the exception only during valid reachable collection: ray/line-of-sight and focus must remain true, and translucent coverage is checked in real views. No decorative collider. Incubator reveal stays within the accepted vessel; local fish/fluid clearance requires actual swept admission, especially after fin uncurl.

### Pondlet and aquarium key poses

Tank A resident root is station-local(0,1.56,0), current fish scale .69. Conservative whole-fish root-space reference is X±.60,Y±.40,Z-.50.. .85 including petals/tail. Initial new cruise uses root X±.22/Y±.05/Z0m, total yaw -100..-80°, pitch/roll±2°. This retains broadside orientation and gives fin silhouette clearance; **no180° body flips** in this first tank. A slow nose-led turn within this envelope changes direction, paired with tail amplitude/speed. Stronger reversal needs a tighter actual mesh-bound admission, not guessed room.

| Pose | Timed design | Details |
|---|---|---|
| Cruise |2.4–3.6s smooth travel segment; ease velocity400ms | Tail amplitude≤14°, petal fins±12° about their captured rest orientation, crest≤4°. Left petal keeps its authored mirrored/rest heading; no replacement by a generic zero Euler pose |
| Curious pause |120ms decelerate,600–1000ms gentle hold,180ms return | Tail slows≤5°; fins keep life; no periodic whole-body squash. Stable specimen ID seeds different initial phases |
| Focus glance | After1.5s continuous legal focus;120/280/200ms; cooldown8s | Total heading still inside -100..-80°; tiny petal flick. No repeated sound; no humanoid eye pop |
| Just placed |80/180/260ms | One destination settle. Current stock root owns it immediately; no source fish or travelling inventory proxy |
| Hatch | First/repeat cue above; fins folded toward rest then unfold | Fish already exists once; no separate egg/hatched specimen or new item |

Initial tank admission prism X[-.93,.93],Y[1.08,1.97],Z[-.57,.57] lies inside the actual fixed water cube X±.95,Y.965..1.995,Z±.60 and above sand (top1.05). The existing validator's glass interior is wider: centre(0,1.57,0), size(1.94,1.06,1.24). Use the water/sand prism for this sample. A mathematical conservative prism sweep accompanies this contract; it does **not** replace actual mesh/fin/parent-transform sweeps in Unity. Purchased displays/racks/conditioners/incubator have separate owner-derived interiors; never reuse this tank's envelope for them.

Habitat: use existing side bubbles at(.72,1.16,.29), at most4 visible bubbles per hero tank (Low1), y1.10..1.94m; plant tip movement≤8mm/±3° about its existing root; optional single sheen on the existing water top below1.995m. Reuse glass/water material and existing plants. No new point light, screen-space reflection, fog, alpha layer over every fin, full-room caustic projector or extra decorative fish. Empty stays empty, reservation uses existing price/status text, and no brightness/color pulse is the only ready/reserved signal.

## 4. Ownership, cancellation and save/rebuild policy

The proposed `FxCueRuntime` is a small plain presentation utility owned by `ShopWorld`, not a manager/singleton/event bus. It consumes typed IDs plus owner ID, session epoch, monotonic per-owner action sequence and origin (manual/worker/module). The domain does not reference it. Bound active transient records to16 and remembered event keys to64 total; only active known owners are registered. A removed owner unregisters; epoch/reset clears all. If no slot survives, drop low priority effects and preserve immediate state/text/audio. Do not retain an ever-growing history keyed by sold fish.

Publish events only at the existing successful result/committed-step branches; generic World.Refresh, SaveNow, OnEnable, panel reopen and reconstruction are **state reconciliation only**. Distinguish live `Model.Tick` first/repeat Ready transition from loaded Ready. Begin/load/refresh sets the current state snapshot silently. A duplicate animation completion cannot dose, hatch, transfer, discover, pay, save, award a tutorial step or finish an employee job. Save failure follows existing owner recovery policy; presentation may still show true in-memory state, but suppress celebratory delivery until the existing durable route succeeds. Do not add a new save or delay the model to match a pose.

| Boundary | Immediate behavior | Resume |
|---|---|---|
| Release/full/empty/lost collection target | Stop intake and action audio immediately; remove stale transient world stream; return visible hands≤220ms | New valid hold starts from neutral progress; no old unit/cue replay |
| Any modal, arrange mode, lost app focus | Cancel manual hands/tool action and its intake; tuck or hide palms so controls are unobstructed; halt view-only cosmetics/audio | Reconcile current state. Do not freeze domain machines when the existing panel does not pause simulation |
| Actual paused state | Freeze steady visible pose; clear transient particles/hand actions; stop/fade sustained audio≤80ms | Read true state; no catch-up burst or replay of missed Ready |
| Non-pausing station menu | Keep true machine/amount state; suppress large transients/hand reach while modal covers view | Successful menu dose can show small local vessel accent if visible, never reach through UI |
| Day closes | Cancel customer/comedy actions whose owners end; preserve actual running lab state and model flow | No unconditional cancellation of real lab production |
| Station moved/removed | Cancel old owner emitters before reparent/disposal; new world attachments derive from legal current transform | Preserve stable station/specimen IDs; no effects stranded at old coordinate |
| Specimen transfer/sold | Source visual releases, destination registers once by **specimen ID**, not cached species | Same-species replacement still changes owner; no old personality/history transferred |
| Reset/load/rebuild/quit/disable | Increment epoch, clear keys/poses/pooled effects/voices; release instance-owned resources | Reconstruct steady Ready/occupied/blocked state silently |

One writer per transform: container writes swim root, `CreatureVisual` writes fins/tail/crest, character/navigation owners keep actor roots, `ShopWorld` resolves final tool pose, hands visual writes its own wrists/fingers. Comedy fish mimic requests an overlay with explicit priority in that same fish detail resolver; it must no longer call Animate a second time. Automatic jobs/modules never drive manual hand poses. A worker carry proxy, when later supported, has one specimen-aware presentation owner; FX-01's direct player transfer uses destination-only settle to avoid inventing transport.

## 5. Initial limits and degradation

These are **frozen initial implementation ceilings**, not measured success. Hardware target remains60FPS at1080p on the recorded PC, with >10% baseline regression investigated before acceptance in equivalent ordinary/busy routes. Zero recurring allocation is a goal for newly authored steady-state FX only; the existing game allocates. Record profiling uncertainty rather than subtracting an unavailable render-thread value.

| Resource | Standard / Low | Policy |
|---|---|---|
| Active emitters |12 /6 total:4 action reserved +8 /2 ambient | Drop far/occluded ambient first. Readiness/transactions keep text/shape/audio even when effects cannot spawn |
| Pooled active particles/mesh motes |96 /32 total:24 action reserved +72 /8 ambient | Warm fixed pool96 once; no per-mote materials/meshes/audio sources. Pool exhaustion coalesces action pulse or omits mote; cannot change game result |
| Mote lifetime |≤1.2s, intake≤.52s | Return/reset on interruption; no historical queue or indefinite trails |
| Extra world resources |≤3 materials,24 loaded meshes,112 renderers,180 transforms attributable to FX | Includes fixed96 pool renderers +≤12 hands +4 other hero visuals. Imported shared assets aren't destroyed; private resources are disposed once |
| Hands |≤6,000 triangles pair,14 articulated pivots,12 renderers,3 material slots | Consolidated rigid cartoon segments; reuse existing palette; no per-frame rebake or Animator dependency |
| Audio voices |6 hard max: service1, comedy1, action1, machine2, room1 | Existing2 voices preserved; at most4 added. Sparse one-shots first; no AudioSource on droplets/stations |

Slots for two machine loops and one room loop are reserved for later work; **hero has no sustained machine/room loops**. Use a short intake-start and existing committed-unit plips, silence on hold except actual units. One logical action gets one selected sound across its machine/hand/fish cues; state reconciliation never repeatedly plays a state sound. Simultaneous important cues use one composed hatch/discovery clip; avoid stopping a discovery with a separate hatch on the same service voice. A module/action short voice is never allowed to replace service, and service ducks non-service gains over30ms/release200ms. Keep existing service protection and humor counters/semantics. Concurrent summed signal must be measured, not assumed safe from each clip peak. Future loops are admitted only with measured seams/room-door attenuation/listening and the saved machine/ambience gain in FX-19/20; unavailable loops stay disabled.

New short clips are original locally synthesized/recorded mono22050Hz,≤1s, PCM peak≤.25/RMS≤.12/DC/end magnitude≤.005, documented source/license. Fade both ends, no hard seam or clip loading per trigger. Muted requests may be recorded for diagnostics but are not audible passes. Existing master/humor values retain meaning. No paid provider, voice library, music, external upload or AudioMixer package is introduced.

Low detail reduces environmental motion/motes without suppressing committed units, true progress, refusal, ready text, stock, price or reserved state. Reduced visual motion removes tool/wrist idle sway, screen/UI movement, fast jitter, big hatch motion and large squash; essential small local pose/audio/state text remain. Existing reduced-gag setting independently limits comedy. Hero must expose a **development-only isolated preview override** for these two modes before persistent settings are implemented in FX-20; it must not write the ordinary slot or pretend user settings shipped.

## 6. Asset/export and integration contract

Local production scene: `ArtSource/LivingShopFX/EvolutionShop_LivingShopFX.blend`, local generator `Tools/Art/create_living_shop_fx.py`, manifest beside the blend, exports under `EvolutionShop/Assets/EvolutionShop/Resources/Art/LivingShopFX/`. These paths are **proposed**, not created. Preserve existing14 admitted FBX and .meta GUIDs plus all three accepted source kits/generators. No wholesale CollectorKit or room replacement.

`HandsPair.fbx` contains a named `HandsPair` visual root at toolBody origin, unit scale, metre/Y-up conversion baked once. Plan a rigid transform rig: each side Wrist, ThumbBase, ThumbTip, IndexBase, IndexTip, Fingers and Cuff (14 pivots total); Wrist is a pivot-only parent, and the palm plus three other fingers are distinct rounded lobes within one rigid Fingers mesh, giving six rendered segments per hand. Rounded cream glove/teal cuff/coral small seam; no fingernail realism. Rest pose and curl axes must be recorded in the manifest from actual Blender/Unity import, not assumed from mirror scale. No negative-scale mirroring, root motion, cloth/IK/rigging dependency, Animator, .anim/controller or Humanoid avatar for this sample. Procedural in-place pose curves are the first delivery strategy; this is intentionally compatible with the existing Animator count0.

Export only selected task-owned objects with FBX axis conversion matching existing generators, apply object scale, preserve positive parent scale and named pivot hierarchy, disable baked animation/export-all-actions and include no camera/light/collider. Re-import and verify pivot/rest axes, face counts, bounds and material slots in actual Unity; Blender geometry alone does not admit an asset. A fixed-handed mesh fallback at safe rest must leave collection/dosing usable when a pivot/clip is missing. New decorative lab parts, if required, are separate local `LabMotionParts.fbx` children; do not split or rewrite the accepted combined frame/body simply to expose a pivot.

Shared URP palette remapping stays through existing `AttachAuthored`/`RedesignMaterial` ownership. Start with existing opaque/glass/emissive resources and optional one sheen that works with current URP transparent ordering. No new textures needed. Preserve project pin/packages, input bindings, .asmdef boundaries, station/collider roots, layout/operator paths, save IDs, money and recipe definitions. Production script/material/FBX admission requires actual Editor/Windows shader/build evidence at its later task.

Proposed implementation files and exact assignments are in `fx-01/contracts.json`. FX-02 creates only `FxCueRuntime.cs`, `FxEffectPool.cs`, `ShopWorldFx.cs` and `ShopFxSession.cs` plus focused validation where needed. Keep enum/value records in the existing presentation assembly, and the session adapter in the existing runtime assembly; no dependency cycle or Core reference back to presentation. Only wire enough successful/rejected/interrupted sample cues to prove lifetime/ownership. Hands/asset production belongs to03/04; Pondlet motion belongs to05. Extend existing ShopAudio via its partial in03; no second independent audio manager.

## 7. Acceptance evidence for subsequent assignments

FX-02: accepted result and one explicit rejected command; no gameplay callback; repeated same-key request does not stack; pool reuse/cancel/dispose; pause/modal/focus/arrange/reset epoch; same-species replacement drops old visual owner; remove/move owner. Compare exact culture/specimen/cash/job identity with presentation enabled/disabled. Exercise a retained/refused save path through existing recovery policy. Check missing attachment/audio fallback and priority exhaustion. These are behavioral checks, not tests of constants alone.

FX-03–05: actual held collection release/full/empty/target loss; menu/manual doses and return; real known Pondlet batch/first/repeat ready; blocked output; collector off/waiting/actual step; upgraded tool/hand fit; empty/occupied/reserved tank; hatch→display→customer paid sale once; pause/load Ready without hatch replay; repeated same-species identity replacement. Actual animated mesh sweep, material inclusion, instance cleanup, input and true state remain required. Direct diagnostic commands, simulated InputSystem devices and native mouse/keyboard are labelled separately.

FX-06: separate exact build, actual720p/1080p day/night camera footage/screens showing approved rooms, hands/tool and the normal HUD; reduced/Low preview; recorded audible mix and native held input. Compare FX-00 same seeded ordinary/busy layout/cap/quality/hardware after warmup, and repeated30s loop object/resource retention. Listen to intake/hatch alongside service/comedy; no manufactured pass from waveform output. Ask user about lab/tank interest, hand feel and repetition. **Creative approval is still pending; no asset multiplication before this gate.** Broader worst supported layout, all species, worker interruption, final persistent settings and release validation remain20–24.

Known inherited issue: default smoke's paused Welcome reset assumption failed108 checks in both R08.4 and FX-00. Retained for FX-21 fixture repair. Current busy world labels can obscure tanks/tool; subsequent character/UI FX14/15/18 must address their planned overlap. Neither issue is fixed here.

## 8. What ran for FX-01

Repository/source/manifest and two actual historical FX-00 gameplay captures were inspected. Exact project version and installed Editor executable were checked; no Unity Editor process or callable Unity/Blender MCP was active. Blender installed version command returned5.0.1. No Editor/player/Blender scene was opened or modified, no bridge/tool/package installed, no authentication attempted. FX-00's guarded Editor connection was a successful **historical CLI probe**, not a current persistent connection.

`Validate-FX01.py` validates schema, unique IDs, attachment/sound coverage, timing/cap arithmetic, roster, numeric conservative tank sweep, immutable protected/source/assets, exact proposed runtime paths still uncreated and documentation scope. Its raw report is `fx-01/validation.json`. This is **static contract validation**, not game tests, visible motion, heard audio or a new successful build. All later implementation/creative gates stay open. See AGENTS.md for checkpoint and next selected assignment.

## Appendix A — full cue sheet

Every row inherits the ownership/cancellation/sound-dedup policy above. Times are anticipation/action/settle ms; state timings describe motion response, not domain duration. Exact typed names are proposed for FX-02; none exists in production yet.

| Cue / channel / priority | Live trigger or steady state | A/A/S ms | Anchor / audio | Reduced presentation |
|---|---|---|---|---|
| `GrowerLocked` / grower / state / P1 | Culture locked: Spark/Mist extension only | 0/0/0 | grower-outlet / none | Still silhouette + existing locked text |
| `GrowerFull` / grower / state / P1 | Units == capacity | 0/180/220 | grower-outlet / none | Static filled level + FULL text |
| `GrowerRefilling` / grower / state / P1 | Units < capacity; supported grower refill clock | 0/240/180 | grower-outlet / none | Readable level + single slow gauge movement |
| `GrowerEmpty` / grower / state / P2 | Units == 0; refill may continue | 0/100/180 | grower-outlet / none | EMPTY text + settled visible mechanism |
| `GrowerCollecting` / grower / state / P3 | Manual held collection valid and focused with positive progress | 80/200/120 | grower-outlet / collect-start | One small nozzle accent; no continuous ribbon |
| `CollectedUnit` / grower / event / P4 | Model.Collect success at ShopSession.Harvest | 0/120/160 | collector-mouth / collect-unit | One plip + immediate true count |
| `IncubatorDosingEmpty` / incubator / state / P1 | Phase Dosing and all staged amounts zero | 0/0/180 | incubator-fluid / none | Static empty mixture + existing doses |
| `IncubatorDosingStaged` / incubator / state / P2 | Phase Dosing and any staged dose > 0 | 0/140/180 | incubator-fluid / none | Actual staged colours/amounts remain readable |
| `DoseCommitted` / incubator / event / P4 | Player DoseCulture success; exact accepted culture delta | 70/160/170 | incubator-fluid / dose | Local vessel plip; no reaching hand under panel |
| `DoseReturned` / incubator / event / P4 | ReturnCultureDose success; exact returned culture delta | 50/140/160 | incubator-fluid / return-dose | Count update + quiet click |
| `IncubationStarted` / incubator / event / P4 | Accepted start changes Dosing to Incubating | 100/220/180 | incubator-fluid / start | One state-lamp confirmation |
| `IncubatorWorking` / incubator / state / P2 | Phase Incubating; true remaining/total time | 0/600/0 | incubator-fluid / none | Steady working shape/text; no rotating ring |
| `HatchFirst` / incubator / event / P4 | Live Tick output FirstDiscovery; Dosing/Incubating to Ready | 120/420/660 | incubator-fluid / hatch-first | 300ms gentle plip + state/discovery text |
| `HatchRepeat` / incubator / event / P4 | Live successful batch completion with existing discovery | 60/180/260 | incubator-fluid / hatch-repeat | 180ms small readiness plip |
| `IncubatorReady` / incubator / state / P3 | Phase Ready including load/rebuild | 0/0/180 | hatchery-lamp / none | Steady READY lamp/text |
| `ReadyOutputBlocked` / incubator / state / P3 | Ready with real full/reserved/inaccessible destination reason | 0/0/180 | hatchery-lamp / none | Existing reason text + steady ready shape |
| `OutputRemoved` / incubator / event / P3 | Accepted transfer removes ready output | 0/120/180 | incubator-fluid / place | Immediate source empty / destination stock |
| `ModuleOff` / module / state / P1 | Installed but disabled; absent module hidden | 0/0/140 | grower-outlet / none | OFF text/shape |
| `ModuleWaiting` / module / state / P1 | Enabled with no committed step / insufficient supply | 0/0/140 | grower-outlet / none | Waiting reason + still valve |
| `ModuleBlocked` / module / state / P3 | Owner reports blocked, paused or save-recovery hold | 0/0/140 | grower-outlet / none | Persistent owner reason/shape |
| `ModuleCollected` / module / event / P3 | Automation Collect succeeds once for owner request | 0/140/160 | grower-outlet / module-step | One local gauge tick |
| `ModuleDosed` / module / event / P3 | Automation Dose succeeds once for owner request | 0/160/180 | incubator-fluid / module-step | One staged-state accent |
| `HandIdle` / hands / state / P1 | Gameplay tool visible, no higher action | 0/0/120 | right-grip / none | Fixed grip, idle sway disabled |
| `HandMove` / hands / state / P1 | Actual player planar travel; no action overrides | 0/200/160 | right-grip / none | No movement sway |
| `HandCollectStart` / hands / event / P3 | Valid collect input edge; not success of an uncollected unit | 80/120/100 | right-grip / collect-start | Small index squeeze only |
| `HandCollectHold` / hands / state / P3 | Held input valid and growing HarvestProgress | 0/200/0 | right-grip / none | Stable grip; no jitter or continuous trail |
| `HandCollectEnd` / hands / event / P3 | Release, full, empty, focus lost or another action | 0/80/140 | right-grip / none | Return to idle without bounce |
| `HandDose` / hands / event / P4 | Successful reachable manual DoseCulture; gameplay view only | 70/160/170 | left-support / dose | Small wrist settle; no reach |
| `HandPlace` / hands / event / P4 | Accepted direct transfer at reachable focused destination | 60/160/180 | left-support / place | Small supported wrist settle |
| `HandRefused` / hands / event / P5 | Explicit rejected manual command; never automatic retry | 0/80/100 | right-grip / reject | Existing rejection text + one small stop pose |
| `HandUpgrade` / hands / event / P4 | Upgrade buy succeeds; chamber capacity already true | 80/180/220 | collector-fluid / confirm | Immediate true chamber size + quiet confirm |
| `HandSlap` / hands / event / P2 | Existing accepted intentional slap action only | 100/90/240 | left-support / slap-existing | Existing reduced-gag/reduced-motion limits |
| `TankEmpty` / tank / state / P2 | Container has no authoritative specimen | 0/0/180 | tank-resident / none | Empty text + quiet water |
| `TankOccupied` / tank / state / P2 | Container has authoritative specimen and no reservation | 0/0/180 | tank-resident / none | Bounded slow fin motion + price |
| `TankReserved` / tank / state / P3 | Actual customer/worker reservation on this specimen | 0/0/140 | tank-resident / none | Existing reserved text + price |
| `TankHabitat` / tank / state / P0 | Existing tank visible in gameplay; empty or occupied | 0/800/0 | tank-bubbles / none | Low: <=1 bubble; reduced: no plant sway |
| `PondletCruise` / pondlet / state / P1 | Authoritative resident visible, ordinary idle phase | 0/2400/400 | tank-resident / none | Root stationary or slow <=20mm drift, gentle fins |
| `PondletPause` / pondlet / state / P1 | Deterministic cruise segment enters curious pause | 120/600/180 | tank-resident / none | Quiet fins; no squash |
| `PondletGlance` / pondlet / event / P1 | Focused occupied tank after >=1.5s; cooldown >=8s | 120/280/200 | tank-resident / none | Tiny fin response; no root lean |
| `PondletPlaced` / pondlet / event / P3 | Accepted placement into hero display; same specimen ID | 80/180/260 | tank-resident / place | 120ms fin settle; root at legal rest position |
| `SaleCommitted` / sale / event / P5 | ServeCustomer succeeds and ledger records actual payment | 0/120/240 | counter-result / confirm | Immediate cash/receipt + existing confirm |

Final FX-01 result: **26/26 static contract/preservation checks passed**, exit0. The numeric reference-box sweep tested26,568 corners with a conservative continuous-angle margin. All540 tracked source/art/settings files and13 protected files were unchanged. Original design schematic `../Evidence/fx-01/key-poses.png` inspected; it is not final art or a Unity capture. Delivery tag **fx-cue-contracts**; subsequent game execution remains open.
