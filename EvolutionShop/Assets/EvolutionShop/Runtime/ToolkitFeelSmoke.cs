#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        /// <summary>One frame of a watched element: its inline feel pose (what the animator set), plus what the step reads.</summary>
        [Serializable]
        sealed class FeelFrame
        {
            public int frame;
            public float t;
            public bool scaled, moved, rotated, faded;
            public float sx = 1, sy = 1, tx, ty, rotate, opacity = 1;
            public string text;
            public int coins;
            public float coinX, coinY;
        }

        [Serializable]
        sealed class FeelTrace
        {
            public string label;
            public bool reduced, low;
            public List<FeelFrame> frames = new List<FeelFrame>();

            public float MinScale() { float v = 1; foreach (var f in frames) if (f.scaled) v = Mathf.Min(v, Mathf.Min(f.sx, f.sy)); return v; }
            public float MaxScale() { float v = 1; foreach (var f in frames) if (f.scaled) v = Mathf.Max(v, Mathf.Max(f.sx, f.sy)); return v; }
            public float MinScaleY() { float v = 1; foreach (var f in frames) if (f.scaled) v = Mathf.Min(v, f.sy); return v; }
            public float MaxScaleX() { float v = 1; foreach (var f in frames) if (f.scaled) v = Mathf.Max(v, f.sx); return v; }
            public float MaxMove() { float v = 0; foreach (var f in frames) if (f.moved) v = Mathf.Max(v, Mathf.Max(Mathf.Abs(f.tx), Mathf.Abs(f.ty))); return v; }
            public float MaxAbsRotate() { float v = 0; foreach (var f in frames) if (f.rotated) v = Mathf.Max(v, Mathf.Abs(f.rotate)); return v; }
            public float MinRotate() { float v = 0; foreach (var f in frames) if (f.rotated) v = Mathf.Min(v, f.rotate); return v; }
            public float MinOpacity() { float v = 1; foreach (var f in frames) if (f.faded) v = Mathf.Min(v, f.opacity); return v; }
            public float FirstOpacity() { foreach (var f in frames) if (f.faded) return f.opacity; return 1; }
            public bool AnyScaled() { foreach (var f in frames) if (f.scaled) return true; return false; }
            public bool AnyMoved() { foreach (var f in frames) if (f.moved) return true; return false; }
            public bool AnyRotated() { foreach (var f in frames) if (f.rotated) return true; return false; }
            public bool AnyFaded() { foreach (var f in frames) if (f.faded) return true; return false; }
            public int MaxCoins() { int v = 0; foreach (var f in frames) v = Mathf.Max(v, f.coins); return v; }
            /// <summary>The longest wait between two samples: a measured end can be this late (a capture or a first view stalls a frame).</summary>
            public float MaxGap() { float v = 0; for (int i = 1; i < frames.Count; i++) v = Mathf.Max(v, frames[i].t - frames[i - 1].t); return v; }
            /// <summary>Ended no earlier than <paramref name="seconds"/> less a frame, and no later than the sample after it.</summary>
            public bool Took(float measured, float seconds) => measured >= seconds - .025f && measured <= seconds + MaxGap() + .01f;
            public string Gap() => " (frames up to " + (MaxGap() * 1000).ToString("0") + " ms apart)";

            /// <summary>From the first frame with an inline pose to the first frame after it with none; -1 when it never settles.</summary>
            public float SettledAfter()
            {
                float start = -1;
                foreach (var f in frames)
                {
                    bool posed = f.scaled || f.moved || f.rotated || f.faded;
                    if (posed && start < 0) start = f.t;
                    else if (!posed && start >= 0) return f.t - start;
                }
                return -1;
            }

            /// <summary>From the first frame with a coin in the air to the first frame after it with none.</summary>
            public float CoinsTime()
            {
                float start = -1;
                foreach (var f in frames)
                {
                    if (f.coins > 0 && start < 0) start = f.t;
                    else if (f.coins == 0 && start >= 0) return f.t - start;
                }
                return -1;
            }
        }

        [Serializable]
        sealed class FeelReceipt
        {
            public string provenance = "Per-frame inline poses read in the route's coroutine (the pose the animator applied in the previous LateUpdate), " +
                "feel-clock times relative to each watch, and the generated UI clips' measured sample counts and peaks. Requests counted, not audible output.";
            public string screen;
            public List<string> sounds = new List<string>();
            public List<FeelTrace> traces = new List<FeelTrace>();
        }

        FeelReceipt feelReceipt;
        UiFeel feel;

        // UX-08: the feel pass through real flows on a fresh unguided game. Night 1: a real first hatch drops the discovery stamp;
        // the incubator and display cards open and close; a press squashes and ticks; the price tag wiggles and a face bounces; a
        // disabled step and the disabled Start say why. Day 1: E on the counter mid-close reopens at once and Open shop is clicked
        // during the open animation; a sale rings and sends five coins from Serve into the money chip, which counts up; then the
        // same sale with Low effects detail (no coins). Receipt 1 counts up and slides its tip in. Night/day 2 run reduced motion:
        // fades only, no coins, the tallies still count. Receipt 3 is skipped with a key; on receipt 4 Next night acts mid-count.
        IEnumerator ToolkitFeelScenario()
        {
            limitations.Add("UX-08 feel route: presses come from synthetic Input System devices added by the route (CardClick, ToolkitCardsSmoke.cs), not physical hardware. Animation is read from the inline styles the animator set each frame (a frame late), not from the rendered pixels; mid-animation captures are screen-only and land on whichever frame the requested age falls in. Sounds are counted requests and measured generated clips, not a listening check.");
            limitations.Add("Two captures (a card mid-open, standard and reduced) are taken on an extra reopen at a tenth of the feel clock's speed (UiAnimator.TimeScale, a validation control) so the frame is legible; every timing check runs at full speed.");
            limitations.Add("Stock beyond the first hatch comes from the labelled accelerated SpecimenProduce fixture with TransferToTank and an explicit save; customers are scripted casual visitors with arrivals off. The reduced-motion stamp is shown with a validation hook (RevealDiscoveryForValidation) for the species already found, since only a real first hatch raises it.");
            var ui = shop.ToolkitUi;
            Check(ui != null && ui.Ready && ui.Feel != null && ui.StationCard != null && ui.Hud != null && ui.Screens != null,
                "UI Toolkit root and its feel load" + (ui != null && ui.LoadProblem != null ? " / " + ui.LoadProblem : ""));
            if (ui == null || !ui.Ready || ui.Feel == null) yield break;
            feel = ui.Feel;
            feelReceipt = new FeelReceipt { screen = Screen.width + "x" + Screen.height + ", panel " + ui.Root.layout.size };
            var card = ui.StationCard;
            var hud = ui.Hud;
            var root = ui.Root;
            var s = ui.Strings;
            var sounds = feel.Sounds;
            GateFresh();
            CardInput();
            Check(shop.SetComfortSettings(false, FxEffectDetail.Standard) && !feel.Reduced && !feel.Low, "Comfort starts at standard motion and detail");
            Check(shop.ToolkitUiActive && ui.Active, "The UI Toolkit UI is on from start-up; since UX-09 it is the only UI");
            yield return HudFrames(4);
            feelReceipt.screen = Screen.width + "x" + Screen.height + ", panel " + root.layout.size;

            // ---------- the UI's own voice ----------
            var voice = sounds.Voice;
            Check(voice != null && voice.gameObject.name == ShopUiSounds.VoiceName && voice.transform.parent == ui.transform && voice.GetComponents<AudioSource>().Length == 1 &&
                !voice.playOnAwake && !voice.loop && voice.spatialBlend == 0 && voice.ignoreListenerPause && !voice.ignoreListenerVolume,
                "The UI has one voice of its own: a 2D one-shot voice under the UI root, reached by master volume, still heard while paused");
            Check(shop.GetComponents<AudioSource>().Length == 5, "The shop's own five voices are unchanged (FX-19 topology)");
            foreach (UiSound kind in Enum.GetValues(typeof(UiSound)))
            {
                var clip = sounds.Clip(kind);
                var data = new float[clip ? clip.samples : 0];
                if (clip) clip.GetData(data, 0);
                float peak = 0;
                foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                int expected = Mathf.RoundToInt(UiSoundSynth.Seconds(kind) * UiSoundSynth.Rate);
                Check(clip && clip.frequency == UiSoundSynth.Rate && clip.channels == 1 && data.Length == expected && peak > .05f && peak < .35f &&
                    Mathf.Abs(data[0]) < .01f && Mathf.Abs(data[data.Length - 1]) < .01f,
                    "Generated UI sound " + kind + ": " + data.Length + " samples at " + UiSoundSynth.Rate + " Hz, peak " + peak.ToString("0.000") + ", soft at both ends");
                feelReceipt.sounds.Add(kind + ": " + UiSoundSynth.Label(kind) + ", " + data.Length + " samples, peak " + peak.ToString("0.000"));
            }

            // ---------- discovery stamp: a real first hatch ----------
            var stamp = root.Q(HudPresenter.DiscoveryName);
            GateAim(new Vector3(0, .2f, -3.8f), new Vector3(0, 1.4f, -9f));
            int drops = hud.StampDrops;
            GateMature();
            var stampTrace = NewTrace("stamp-down");
            yield return Watch(stampTrace, stamp, .5f, "ux08-stamp-landing", .07f);
            Check(hud.StampDrops == drops + 1 && shop.DiscoveryRevealActiveForValidation && HudShown(root, HudPresenter.DiscoveryName),
                "A real first hatch drops the discovery stamp once (the lab plays its first-hatch sound with it)");
            Check(stampTrace.MaxScale() >= 1.35f && stampTrace.MaxScale() <= UiMotion.StampFrom + .01f && stampTrace.MinRotate() <= -6f && stampTrace.FirstOpacity() < .7f,
                "The stamp comes down from above: largest " + stampTrace.MaxScale().ToString("0.00") + "x, tilt from " + stampTrace.MinRotate().ToString("0.0") + "°, first opacity " + stampTrace.FirstOpacity().ToString("0.00"));
            float stampTime = stampTrace.SettledAfter();
            Check(stampTrace.Took(stampTime, UiMotion.StampSeconds), "It lands in about a quarter second (" + stampTime.ToString("0.000") + " s" + stampTrace.Gap() + ")");
            float rest = stamp.resolvedStyle.rotate.angle.ToDegrees();
            Check(!Posed(stamp) && Mathf.Abs(rest - UiMotion.StampRestDegrees) < .05f, "Landed, it rests at the stylesheet's tilt with no inline pose left (" + rest.ToString("0.0") + "°)");
            CheckHudLayout(hud, "Discovery stamp landed");
            yield return HudShot("ux08-stamp-landed");

            // ---------- incubator card: open and close (lab, enamel) ----------
            var incubator = StationOf(StationKind.Incubator);
            var counter = StationOf(StationKind.Counter);
            var tankA = StationOf(StationKind.TankA);
            int enamel = sounds.Count(UiSound.CardEnamel), paper = sounds.Count(UiSound.CardPaper), opens = card.Opens;
            var openTrace = NewTrace("card-open/incubator");
            yield return FeelOpenCard(incubator, openTrace, null, 0);
            Check(card.IsShown && card.CurrentView == card.IncubatorView && card.Opens == opens + 1 &&
                sounds.Count(UiSound.CardEnamel) == enamel + 1 && sounds.Count(UiSound.CardPaper) == paper, "The incubator card opens with one soft enamel click (lab strip)");
            float openMin = openTrace.MinScale(), openMax = openTrace.MaxScale(), openTime = openTrace.SettledAfter();
            Check(openMin <= .96f && openMin >= UiMotion.CardOpenFrom - .005f && openMax > 1.002f && openMax < 1.02f && openTrace.FirstOpacity() < .5f,
                "It grows from " + openMin.ToString("0.000") + " with a slight overshoot to " + openMax.ToString("0.0000") + ", fading in from " + openTrace.FirstOpacity().ToString("0.00"));
            Check(openTrace.Took(openTime, UiMotion.CardOpenSeconds) && !Posed(card.Element), "It settles in about 120 ms (" + openTime.ToString("0.000") + " s" + openTrace.Gap() + ") with no inline pose left");
            CheckCard(card, "Incubator card, opened", card.CurrentView != null && !string.IsNullOrEmpty(card.CurrentView.PrimaryText) ? 1 : 0);
            yield return HudShot("ux08-card-open");

            var closeTrace = NewTrace("card-close/incubator");
            Rect cardRect = card.Element.worldBound;
            var cardPicking = card.Element.pickingMode;
            bool sawClosing = false, pickedOldCard = false, shownWhileClosing = false;
            syntheticKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.Escape));
            yield return Watch(closeTrace, card.Element, .3f, "ux08-card-closing", .03f, f =>
            {
                if (!card.Closing) return;
                sawClosing = true;
                shownWhileClosing |= card.IsShown;
                var picked = root.panel.Pick(cardRect.center);
                pickedOldCard |= picked != null && (picked == card.Element || card.Element.Contains(picked));
            });
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return HudFrames(2);
            float closeTime = closeTrace.SettledAfter();
            Check(shop.Panel == ShopPanel.None && !card.IsShown && sawClosing && !shownWhileClosing, "Esc closes the card at once: the session moves on while the card fades");
            Check(!pickedOldCard, "Nothing in the fading card takes a click (the pointer reaches what is under it)");
            Check(closeTrace.MinScale() < .995f && closeTrace.MinScale() >= UiMotion.CardCloseTo - .005f && closeTrace.MinOpacity() < .95f && closeTrace.Took(closeTime, UiMotion.CardCloseSeconds),
                "It fades (to " + closeTrace.MinOpacity().ToString("0.00") + ") and shrinks (to " + closeTrace.MinScale().ToString("0.000") + ") in about 80 ms (" + closeTime.ToString("0.000") + " s" + closeTrace.Gap() + ")");
            Check(card.Element.resolvedStyle.display == DisplayStyle.None && !card.Closing && !Posed(card.Element) && card.Element.pickingMode == cardPicking,
                "Then it is gone, with its own picking back");
            Check(!shop.DiscoveryRevealActiveForValidation || (hud.StampDrops == drops + 1 && HudShown(root, HudPresenter.DiscoveryName) && !Posed(stamp)),
                "The stamp, hidden while the card was open, comes back as it was, without dropping again");

            // ---------- display: E places the hatch, the card opens on paper, a press squashes and ticks ----------
            yield return FeelOpenCard(tankA, NewTrace("place/tank"), null, 0);
            var placed = shop.Model.GetTank("tank-a");
            Check(placed.HasValue && placed.Value.SpeciesId == "pondlet" && shop.Panel == ShopPanel.None, "E on the empty display puts the ready Pondlet on show (the game's own rule, no card)");
            var openTank = NewTrace("card-open/tank");
            paper = sounds.Count(UiSound.CardPaper); enamel = sounds.Count(UiSound.CardEnamel);
            yield return FeelOpenCard(tankA, openTank, null, 0);
            var tank = card.TankView;
            Check(card.IsShown && card.CurrentView == tank && card.ShopStyled && !tank.State.Empty && sounds.Count(UiSound.CardPaper) == paper + 1 && sounds.Count(UiSound.CardEnamel) == enamel,
                "The display card opens on its price tag with one soft paper click (shop strip)");
            Check(openTank.AnyScaled() && openTank.MinScale() < .97f, "It grows in the same way (" + openTank.MinScale().ToString("0.000") + ")");
            // For the record only: the same open at a tenth of the feel clock's speed, captured 12 ms in (timings above are full speed).
            yield return FeelOpenCard(tankA, NewTrace("card-open/tank-tenth-speed"), "ux08-card-opening", .012f, .1f);
            Check(card.IsShown && card.CurrentView == tank && feel.Motion.TimeScale == 1f, "Reopened at a tenth of the speed for the capture, then back to full speed");

            // ---------- a press, the price wiggle and a face's bounce ----------
            var raise = root.Q<Button>(TankCardView.RaiseName);
            var lower = root.Q<Button>(TankCardView.LowerName);
            var priceLabel = root.Q<Label>(TankCardView.PriceName);
            int wiggles = tank.Wiggles, price0 = shop.Model.GetPrice("pondlet").Value.Price;
            int ticks = sounds.Count(UiSound.Tick), presses = feel.Presses;
            var wiggleTrace = NewTrace("price-wiggle");
            var raisePress = NewTrace("press/raise");
            yield return FeelClick(raise, raisePress, raise, .1f, wiggleTrace, tank.PriceAmount, .4f, "ux08-price-wiggle", .035f);
            Check(feel.Presses == presses + 1 && sounds.Count(UiSound.Tick) == ticks + 1 && feel.LastPressed == raise.name, "Pressing + ticks once");
            Check(raisePress.MinScaleY() < .95f && raisePress.MaxScaleX() > 1.02f && raisePress.MinScaleY() >= .91f,
                "and squashes it (" + raisePress.MaxScaleX().ToString("0.000") + " wide, " + raisePress.MinScaleY().ToString("0.000") + " tall)");
            float pressTime = raisePress.SettledAfter();
            Check(raisePress.Took(pressTime, UiMotion.PressSeconds), "for about 60 ms (" + pressTime.ToString("0.000") + " s" + raisePress.Gap() + ")");
            Check(shop.Model.GetPrice("pondlet").Value.Price == price0 + 1 && priceLabel.text == (price0 + 1).ToString() && tank.Wiggles == wiggles + 1, "+ raises the price by one and the tag wiggles once");
            Check(wiggleTrace.MaxAbsRotate() > 2f && wiggleTrace.MaxAbsRotate() <= UiMotion.WiggleDegrees + .01f && wiggleTrace.MaxScale() > 1.03f,
                "The tag rocks up to " + wiggleTrace.MaxAbsRotate().ToString("0.0") + "° and swells to " + wiggleTrace.MaxScale().ToString("0.000"));
            float wiggleTime = wiggleTrace.SettledAfter();
            Check(wiggleTrace.Took(wiggleTime, UiMotion.WiggleSeconds) && !Posed(tank.PriceAmount), "and settles in about 280 ms (" + wiggleTime.ToString("0.000") + " s" + wiggleTrace.Gap() + ")");
            int bounces = tank.Bounces;
            var bounceTrace = NewTrace("face-bounce");
            for (int i = 0; i < 12 && tank.Bounces == bounces && raise.enabledSelf; i++) yield return CardShiftClick(raise, "Shift +");
            MoodFace bouncing = null;
            foreach (var face in root.Query<MoodFace>().ToList()) if (CardShown(face) && Posed(face)) { bouncing = face; break; }
            Check(tank.Bounces > bounces && bouncing != null, "Raising the price swaps a customer's face, and that face bounces (" + (tank.Bounces - bounces) + ")");
            if (bouncing != null) yield return Watch(bounceTrace, bouncing, .3f, "ux08-face-bounce", .07f);
            Check(bounceTrace.MaxScale() > 1.06f && bounceTrace.MaxScale() <= 1.15f && bounceTrace.AnyMoved(),
                "The face hops up and swells to " + bounceTrace.MaxScale().ToString("0.000") + " before settling");

            // ---------- a disabled step says why ----------
            for (int i = 0; i < 40 && lower.enabledSelf; i++) yield return CardShiftClick(lower, "Shift −");
            int minimum = shop.Model.GetPrice("pondlet").Value.Price;
            Check(!lower.enabledSelf && lower.tooltip == s.Get("card.tank.why.min"), "At the lowest price − is disabled and carries why: " + lower.tooltip);
            int thunks = sounds.Count(UiSound.Thunk), disabledPresses = feel.DisabledPresses, tips = feel.TipsShown;
            ticks = sounds.Count(UiSound.Tick);
            var shakeTrace = NewTrace("disabled-shake/lower");
            yield return FeelClick(lower, shakeTrace, lower, .3f, null, null, 0);
            Check(feel.DisabledPresses == disabledPresses + 1 && feel.LastDisabled == lower.name && sounds.Count(UiSound.Thunk) == thunks + 1 && sounds.Count(UiSound.Tick) == ticks,
                "Pressing the disabled − thunks once (no tick)");
            Check(shakeTrace.MaxMove() >= 1.6f && shakeTrace.MaxMove() <= UiMotion.ShakePixels + .05f && !shakeTrace.AnyScaled(),
                "and shakes it " + shakeTrace.MaxMove().ToString("0.00") + " px side to side");
            float shakeTime = shakeTrace.SettledAfter();
            Check(shakeTrace.Took(shakeTime, UiMotion.ShakeSeconds) && !Posed(lower), "for about 200 ms (" + shakeTime.ToString("0.000") + " s" + shakeTrace.Gap() + ")");
            var tip = feel.Tip;
            Check(feel.TipsShown == tips + 1 && feel.TipShown && feel.TipText == s.Get("card.tank.why.min") && shop.Model.GetPrice("pondlet").Value.Price == minimum,
                "A tip says why (" + feel.TipText + ") and the price stays " + minimum);
            yield return HudFrames(2);
            Rect tipRect = tip.worldBound, lowerRect = lower.worldBound;
            Check(ShopUiTestDriver.FullyInside(tip, root) && !tipRect.Overlaps(lowerRect) && tipRect.yMax <= lowerRect.yMin + .5f,
                "The tip sits above the button, on screen (" + tipRect + " over " + lowerRect + ")");
            yield return HudShot("ux08-disabled-tip");
            yield return CardClick(root.Q<Button>(TankCardView.StandardName), "Use standard");
            Check(shop.Model.GetPrice("pondlet").Value.Price == shop.Model.GetPrice("pondlet").Value.Standard, "Use standard puts the price back");

            // ---------- the disabled Start flashes its reason ----------
            yield return FeelOpenCard(incubator, NewTrace("card-open/incubator-2"), null, 0);
            var primary = root.Q<Button>(StationCardPresenter.PrimaryName);
            var reason = root.Q<Label>(StationCardPresenter.ReasonName);
            Check(card.CurrentView == card.IncubatorView && CardShown(primary) && !primary.enabledSelf && CardShown(reason) && primary.tooltip == reason.text,
                "Start is disabled with nothing dosed and its tooltip carries the reason: " + reason.text);
            int flashes = card.ReasonFlashes;
            tips = feel.TipsShown; thunks = sounds.Count(UiSound.Thunk);
            var phase = shop.Model.Incubator.Phase;
            bool flashed = false;
            var startShake = NewTrace("disabled-shake/start");
            yield return FeelClick(primary, startShake, primary, .25f, null, null, 0, "ux08-reason-flash", .08f, f => flashed |= reason.ClassListContains(StationCardPresenter.ReasonFlashClass));
            Check(card.ReasonFlashes == flashes + 1 && flashed && feel.TipsShown == tips && sounds.Count(UiSound.Thunk) == thunks + 1 && shop.Model.Incubator.Phase == phase,
                "Pressing the disabled Start thunks and highlights the reason above it (no extra tip); nothing starts");
            Check(startShake.MaxMove() >= 1.6f, "and the button shakes (" + startShake.MaxMove().ToString("0.00") + " px)");
            yield return UiSettled();
            Check(!reason.ClassListContains(StationCardPresenter.ReasonFlashClass) && !Posed(reason), "The highlight goes after about 600 ms");

            // ---------- a dose through the card: the world's own pour stays in view beside it ----------
            // The classic panel drew a dose strip because it covered the incubator (FX-03); the card sits beside the station instead.
            shop.Model.Tick(30);
            Ok(shop.Model.Collect(CultureKind.Bloom), "A Bloom in the siphon (labelled fixture)");
            yield return HudFrames(2);
            ticks = sounds.Count(UiSound.Tick);
            yield return CardClick(root.Q<Button>(IncubatorCardView.PlusName(CultureKind.Bloom)), "+ Bloom");
            Check(shop.Model.Staged(CultureKind.Bloom) == 1 && sounds.Count(UiSound.Tick) == ticks + 1, "+ Bloom doses one Bloom, with its tick");
            Check(shop.World.LabDoseRemaining > 0 && !card.Placement.CoversAnchor && ShopUiTestDriver.FullyInside(card.Element, root),
                "The incubator's own dose pour plays in the world beside the card, which never covers the incubator (" + shop.World.LabDoseRemaining.ToString("0.00") + " s left)");
            yield return HudShot("ux08-dose-pour");
            yield return CardClick(root.Q<Button>(IncubatorCardView.ReturnName), "Return doses");
            Check(shop.Model.Staged(CultureKind.Bloom) == 0, "Return doses puts the Bloom back");

            // ---------- day 1: reopened mid-close, and a click during the open animation ----------
            SpecimenProduce("pondlet");
            Ok(shop.Model.TransferToTank("tank-b"), "Second display stocked (labelled fixture)");
            shop.World.Refresh(shop.Model);
            Check(shop.SaveNow(), "The stocked displays are saved before trading");
            yield return FeelOpenCard(incubator, NewTrace("card-open/incubator-3"), null, 0);
            HudFaceCounter(counter);
            Physics.SyncTransforms();
            shop.SetPanel(ShopPanel.None);
            yield return null;
            Check(card.Closing && !card.IsShown, "Leaving the incubator starts its card's fade");
            paper = sounds.Count(UiSound.CardPaper);
            shop.Interact(counter);
            yield return null;
            Check(card.IsShown && !card.Closing && shop.Panel == ShopPanel.Counter && card.CurrentView == card.CounterView && sounds.Count(UiSound.CardPaper) == paper + 1 &&
                card.Element.pickingMode == cardPicking, "E on the counter mid-fade opens its card at once (the fade never holds the next command)");
            primary = root.Q<Button>(StationCardPresenter.PrimaryName);
            reason = root.Q<Label>(StationCardPresenter.ReasonName);
            // The card is placed from its laid-out size, so it settles where the counter's content puts it a frame later;
            // the press is aimed at where the button is then, still well inside the 120 ms open.
            yield return null;
            cardMouse.MakeCurrent();
            Vector2 at = CardScreenPoint(primary);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return null;
            at = CardScreenPoint(primary);
            float openAge = feel.Motion.Age(card.Element);
            bool animatingAtPress = feel.Motion.IsPlaying(card.Element, UiMotionKind.CardOpen);
            presses = feel.Presses;
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(3);
            Check(animatingAtPress && feel.Presses == presses + 1 && shop.Cycle.Phase == ShopDayPhase.Open,
                "Open shop clicked " + (openAge * 1000).ToString("0") + " ms into the card's open animation opens the day at once (" + primary.text + ", " +
                (primary.enabledSelf ? "enabled" : "disabled") + ", phase " + shop.Cycle.Phase + ", presses " + (feel.Presses - presses) + ", at " + at + ", button " + primary.worldBound + ")");

            // ---------- a sale: ka-ching, coins from Serve into the money chip, the chip counts up ----------
            var moneyChip = root.Q(HudPresenter.MoneyName);
            var moneyLabel = root.Q<Label>("es-hud-money-amount");
            var moneyIcon = root.Q("es-hud-money-icon");
            yield return FeelSale("standard", counter, moneyChip, moneyLabel, moneyIcon, "ux08-coins", .2f, coins: UiMotion.Coins, bump: true);
            yield return HudShot("ux08-sale-settled");

            // ---------- the same sale with Low effects detail: no coins, everything else stays ----------
            Check(shop.SetComfortSettings(false, FxEffectDetail.Low) && feel.Low && !feel.Reduced, "Effects detail set to Low");
            yield return FeelSale("low", counter, moneyChip, moneyLabel, moneyIcon, "ux08-low-sale", .2f, coins: 0, bump: true);

            // ---------- receipt 1: the tallies count up, then the tip slides in ----------
            int receiptFades = feel.Motion.Played(UiMotionKind.ViewOpen), receiptPaper = sounds.Count(UiSound.CardPaper);
            yield return FeelCloseDay(counter);
            yield return FeelReceiptCount(1, "ux08-receipt-counting", reducedTip: false);
            Check(feel.Motion.Played(UiMotionKind.ViewOpen) == receiptFades + 1 && sounds.Count(UiSound.CardPaper) == receiptPaper + 1, "The receipt came up with one fade and one paper click");
            yield return HudShot("ux08-receipt-tip");
            yield return CardClick(root.Q<Button>(ReportView.NextName), "Next night");
            Check(shop.Panel == ShopPanel.None, "Next night goes back to play");

            // ---------- night and day 2 with reduced motion: fades only ----------
            Check(shop.SetComfortSettings(true, FxEffectDetail.Standard) && feel.Reduced && !feel.Low, "Reduced motion on (standard detail)");
            shop.SetPanel(ShopPanel.None);
            yield return UiSettled();
            GateAim(new Vector3(0, .2f, -3.8f), new Vector3(0, 1.4f, -9f));
            drops = hud.StampDrops;
            shop.RevealDiscoveryForValidation("pondlet");
            var reducedStamp = NewTrace("stamp-down/reduced");
            yield return Watch(reducedStamp, stamp, .45f, "ux08-reduced-stamp", .07f);
            float reducedStampTime = reducedStamp.SettledAfter();
            Check(hud.StampDrops == drops + 1 && !reducedStamp.AnyScaled() && !reducedStamp.AnyRotated() && reducedStamp.AnyFaded() && reducedStamp.FirstOpacity() < .5f &&
                reducedStamp.Took(reducedStampTime, UiMotion.StampSeconds), "With reduced motion the stamp fades in where it rests, no fall or tilt (" + reducedStampTime.ToString("0.000") + " s" + reducedStamp.Gap() + ")");
            SpecimenProduce("pondlet");
            Ok(shop.Model.TransferToTank("tank-a"), "Display restocked for day 2 (labelled fixture)");
            shop.World.Refresh(shop.Model);
            Check(shop.SaveNow(), "Restock saved");
            var reducedOpen = NewTrace("card-open/reduced");
            paper = sounds.Count(UiSound.CardPaper);
            yield return FeelOpenCard(counter, reducedOpen, null, 0);
            float reducedOpenTime = reducedOpen.SettledAfter();
            Check(!reducedOpen.AnyScaled() && reducedOpen.AnyFaded() && reducedOpen.FirstOpacity() < .5f && reducedOpen.FirstOpacity() >= .19f && sounds.Count(UiSound.CardPaper) == paper + 1 &&
                reducedOpen.Took(reducedOpenTime, UiMotion.CardOpenSeconds), "With reduced motion the card fades in without growing, and still clicks (" + reducedOpen.FirstOpacity().ToString("0.00") + " → 1 in " + reducedOpenTime.ToString("0.000") + " s" + reducedOpen.Gap() + ")");
            yield return FeelOpenCard(counter, NewTrace("card-open/reduced-tenth-speed"), "ux08-reduced-card-opening", .02f, .1f);
            Check(card.IsShown && feel.Motion.TimeScale == 1f, "Reopened at a tenth of the speed for the capture, then back to full speed");
            primary = root.Q<Button>(StationCardPresenter.PrimaryName);
            reason = root.Q<Label>(StationCardPresenter.ReasonName);
            ticks = sounds.Count(UiSound.Tick);
            var reducedPress = NewTrace("press/reduced");
            yield return FeelClick(primary, reducedPress, primary, .1f, null, null, 0);
            Check(shop.Cycle.Phase == ShopDayPhase.Open && sounds.Count(UiSound.Tick) == ticks + 1 && !reducedPress.AnyScaled() && reducedPress.MinOpacity() < .9f,
                "Open shop ticks and dims briefly instead of squashing (" + reducedPress.MinOpacity().ToString("0.00") + ")");
            yield return HudFrames(3);
            Check(!primary.enabledSelf && CardShown(reason) && primary.tooltip == reason.text, "With nobody waiting Serve is disabled and says why: " + reason.text);
            flashes = card.ReasonFlashes; thunks = sounds.Count(UiSound.Thunk);
            flashed = false;
            var reducedShake = NewTrace("disabled-shake/reduced");
            yield return FeelClick(primary, reducedShake, primary, .25f, null, null, 0, null, 0, f => flashed |= reason.ClassListContains(StationCardPresenter.ReasonFlashClass));
            Check(card.ReasonFlashes == flashes + 1 && flashed && sounds.Count(UiSound.Thunk) == thunks + 1 && !reducedShake.AnyMoved() && !reducedShake.AnyScaled(),
                "Pressing the disabled Serve thunks and highlights the reason, without the shake");
            yield return FeelSale("reduced", counter, moneyChip, moneyLabel, moneyIcon, "ux08-reduced-sale", .2f, coins: 0, bump: false);
            yield return FeelCloseDay(counter);
            yield return FeelReceiptCount(2, "ux08-reduced-receipt", reducedTip: true);
            yield return CardClick(root.Q<Button>(ReportView.NextName), "Next night");

            // ---------- receipt 3: any key skips the count ----------
            Check(shop.SetComfortSettings(false, FxEffectDetail.Standard) && !feel.Reduced && !feel.Low, "Back to standard motion and detail");
            SpecimenProduce("pondlet");
            Ok(shop.Model.TransferToTank("tank-a"), "Display restocked for day 3 (labelled fixture)");
            shop.World.Refresh(shop.Model);
            Check(shop.SaveNow(), "Restock saved");
            yield return FeelOpenCard(counter, NewTrace("card-open/counter-3"), null, 0);
            // ---------- the notebook comes up with a fade and a paper click ----------
            var plan = ui.Planning;
            int fades = feel.Motion.Played(UiMotionKind.ViewOpen);
            paper = sounds.Count(UiSound.CardPaper);
            var bookTrace = NewTrace("notebook-open");
            yield return FeelClick(root.Q<Button>(CounterCardView.CrewName), null, null, 0, bookTrace, plan.Element, .3f, "ux08-notebook-opening", .02f);
            float bookTime = bookTrace.SettledAfter();
            Check(shop.Panel == ShopPanel.Staff && plan.IsShown && feel.Motion.Played(UiMotionKind.ViewOpen) == fades + 1 && sounds.Count(UiSound.CardPaper) == paper + 1,
                "Crew opens the notebook with one fade and one paper click");
            Check(bookTrace.AnyFaded() && !bookTrace.AnyScaled() && !bookTrace.AnyMoved() && bookTrace.FirstOpacity() >= UiMotion.ViewOpenFrom - .01f && bookTrace.Took(bookTime, UiMotion.ViewOpenSeconds),
                "It fades in from " + bookTrace.FirstOpacity().ToString("0.00") + " without moving, in about 120 ms (" + bookTime.ToString("0.000") + " s" + bookTrace.Gap() + ")");
            CheckPlan(plan, "Notebook, faded in", 0, false);
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Counter && card.IsShown, "Esc goes back to the counter card");
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Open shop");
            yield return FeelSale("standard, day 3", counter, moneyChip, moneyLabel, moneyIcon, null, 0, coins: UiMotion.Coins, bump: true);
            yield return FeelCloseDay(counter);
            var report = ui.Screens.Report;
            float until = Time.realtimeSinceStartup + 5f;
            while (!(shop.Panel == ShopPanel.DayReport && report.Counting) && Time.realtimeSinceStartup < until) yield return null;
            Check(report.Counting && report.State.Day == 3, "Night 3's receipt is counting up");
            yield return null;
            int skipped = report.CountsSkipped;
            string income = "+" + report.State.Revenue.ToString("N0");
            syntheticKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.LeftShift));
            yield return null;
            yield return null;
            Check(report.CountsSkipped == skipped + 1 && !report.Counting && root.Q<Label>(ReportView.IncomeName).text == income &&
                root.Q<Label>(ReportView.SoldName).text == report.State.Sales.ToString(), "A key press skips the count to the totals at once: " + income +
                " (skips " + (report.CountsSkipped - skipped) + ", counting " + report.Counting + ", shows " + root.Q<Label>(ReportView.IncomeName).text + " and " + root.Q<Label>(ReportView.SoldName).text + " sold)");
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return HudShot("ux08-receipt-skipped");
            yield return CardClick(root.Q<Button>(ReportView.NextName), "Next night");

            // ---------- receipt 4: Next night acts mid-count ----------
            yield return FeelOpenCard(counter, NewTrace("card-open/counter-4"), null, 0);
            yield return CardClick(root.Q<Button>(StationCardPresenter.PrimaryName), "Open shop");
            yield return FeelCloseDay(counter);
            until = Time.realtimeSinceStartup + 5f;
            while (!(shop.Panel == ShopPanel.DayReport && report.Counting) && Time.realtimeSinceStartup < until) yield return null;
            Check(report.Counting && report.State.Day == 4, "Night 4's receipt is counting up");
            var next = root.Q<Button>(ReportView.NextName);
            at = CardScreenPoint(next);
            cardMouse.MakeCurrent();
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return null;
            bool countingAtPress = report.Counting;
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(2);
            Check(countingAtPress && shop.Panel == ShopPanel.None && !report.Counting, "Next night pressed mid-count goes to the night at once");

            // ---------- at rest (UX-09: the toolkit is the only UI, so the route ends with it on) ----------
            Check(NoImmediateModeGui(out string imgui), "No IMGUI path remains: nothing in the game assembly draws with OnGUI" + imgui);
            yield return UiSettled();
            Check(feel.Motion.Idle && !feel.TipShown && hud.CoinsShown == 0 && !Posed(card.Element) && !Posed(stamp) && !Posed(moneyChip) && !Posed(tank.PriceAmount),
                "Every feel animation ends at rest, with nothing left posed");
            yield return HudShot("ux08-at-rest");
            SaveFeelSamples();
        }

        FeelTrace NewTrace(string label)
        {
            var trace = new FeelTrace { label = label, reduced = feel.Reduced, low = feel.Low };
            feelReceipt.traces.Add(trace);
            return trace;
        }

        /// <summary>True while the element carries an inline feel pose (scale, translate, rotate or opacity).</summary>
        static bool Posed(VisualElement e) => e != null && (e.style.scale.keyword == StyleKeyword.Undefined || e.style.translate.keyword == StyleKeyword.Undefined ||
            e.style.rotate.keyword == StyleKeyword.Undefined || e.style.opacity.keyword == StyleKeyword.Undefined);

        static FeelFrame FeelSample(VisualElement e)
        {
            var f = new FeelFrame { frame = Time.frameCount };
            if (e == null) return f;
            var scale = e.style.scale;
            if (scale.keyword == StyleKeyword.Undefined) { f.scaled = true; f.sx = scale.value.value.x; f.sy = scale.value.value.y; }
            var translate = e.style.translate;
            if (translate.keyword == StyleKeyword.Undefined) { f.moved = true; f.tx = translate.value.x.value; f.ty = translate.value.y.value; }
            var rotate = e.style.rotate;
            if (rotate.keyword == StyleKeyword.Undefined) { f.rotated = true; f.rotate = rotate.value.angle.ToDegrees(); }
            var opacity = e.style.opacity;
            if (opacity.keyword == StyleKeyword.Undefined) { f.faded = true; f.opacity = opacity.value; }
            return f;
        }

        /// <summary>Samples the element every frame for <paramref name="seconds"/> of feel time, capturing the screen once at <paramref name="shotAge"/>.</summary>
        IEnumerator Watch(FeelTrace trace, VisualElement element, float seconds, string shot = null, float shotAge = 0, Action<FeelFrame> each = null)
        {
            float start = feel.Now, until = Time.realtimeSinceStartup + seconds / Mathf.Max(.05f, feel.Motion.TimeScale) + 3f;
            bool shotTaken = shot == null;
            while (feel.Now - start < seconds && Time.realtimeSinceStartup < until)
            {
                var f = FeelSample(element);
                f.t = feel.Now - start;
                each?.Invoke(f);
                trace.frames.Add(f);
                if (!shotTaken && f.t >= shotAge)
                {
                    shotTaken = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(evidence, shot + "-screen.png"));
                }
                yield return null;
            }
            // Saved after every watch, so a run that stops at a failed check still leaves its samples.
            SaveFeelSamples();
        }

        void SaveFeelSamples() => File.WriteAllText(Path.Combine(evidence, "feel-samples.json"), JsonUtility.ToJson(feelReceipt, true));

        /// <summary>A real click: pointer to the control, down, watch while held, up, watch after; disabled controls are pressed too.</summary>
        IEnumerator FeelClick(VisualElement button, FeelTrace holdTrace, VisualElement holdWatch, float holdSeconds, FeelTrace afterTrace, VisualElement afterWatch, float afterSeconds,
            string shot = null, float shotAge = 0, Action<FeelFrame> each = null)
        {
            Check(button != null && CardShown(button), (button != null ? button.name : "button") + " is shown");
            cardMouse.MakeCurrent();
            Vector2 at = CardScreenPoint(button);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            yield return HudFrames(3);
            at = CardScreenPoint(button);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at, buttons = 1 });
            bool after = afterTrace != null;
            if (holdTrace != null) yield return Watch(holdTrace, holdWatch, holdSeconds, after ? null : shot, shotAge, after ? null : each);
            else yield return HudFrames(2);
            InputSystem.QueueStateEvent(cardMouse, new MouseState { position = at });
            if (after) yield return Watch(afterTrace, afterWatch, afterSeconds, shot, shotAge, each);
            yield return HudFrames(3);
        }

        /// <summary>Closes the card, waits for it to settle, aims at the station and opens its card with E's own path, watching it open.</summary>
        IEnumerator FeelOpenCard(Station station, FeelTrace trace, string shot, float shotAge, float timeScale = 1f)
        {
            shop.SetPanel(ShopPanel.None);
            yield return UiSettled();
            var t = station.transform;
            switch (station.Kind)
            {
                case StationKind.Incubator:
                    GateAim(t.TransformPoint(new Vector3(0, .05f, -2.15f)), t.TransformPoint(new Vector3(0, 1.5f, -.1f)));
                    break;
                case StationKind.Counter:
                    HudFaceCounter(station);
                    break;
                default:
                {
                    string tankId = station.TankId ?? (station.Kind == StationKind.TankA ? "tank-a" : "tank-b");
                    Check(shop.World.TryGetHabitatForValidation(tankId, out var space, out _, out _, out _), "Display " + tankId + " has a habitat to face");
                    Vector3 position = shop.Customers.Navigation.BrowseSpot(tankId, 0);
                    position.y = .2f;
                    GateAim(position, space.TransformPoint(new Vector3(0, 1.15f, 0)));
                    break;
                }
            }
            Physics.SyncTransforms();
            yield return null;
            feel.Motion.TimeScale = timeScale;
            shop.Interact(station);
            yield return Watch(trace, shop.ToolkitUi.StationCard.Element, .3f, shot, shotAge);
            feel.Motion.TimeScale = 1f;
        }

        /// <summary>A scripted casual visitor buys from the display through the counter card's real Serve click; checks the sale's feel.</summary>
        IEnumerator FeelSale(string label, Station counter, VisualElement moneyChip, Label moneyLabel, VisualElement moneyIcon, string shot, float shotAge, int coins, bool bump)
        {
            var ui = shop.ToolkitUi;
            var card = ui.StationCard;
            var hud = ui.Hud;
            var root = ui.Root;
            var sounds = feel.Sounds;
            if (shop.Panel != ShopPanel.Counter) yield return FeelOpenCard(counter, NewTrace("card-open/counter-" + label), null, 0);
            float until = Time.realtimeSinceStartup + 30f;
            while (shop.Customers.ActiveCount > 0 && Time.realtimeSinceStartup < until) yield return null;
            var visit = Spawn(CustomerArchetypes.Casual);
            until = Time.realtimeSinceStartup + 45f;
            while (visit.Phase != CustomerPhase.Queued && Time.realtimeSinceStartup < until) yield return null;
            Check(visit.Phase == CustomerPhase.Queued, "A customer queues to buy (" + label + ")");
            yield return HudFrames(3);
            var serve = root.Q<Button>(StationCardPresenter.PrimaryName);
            Check(card.IsShown && card.CurrentView == card.CounterView && serve.enabledSelf, "Serve is ready (" + label + ")");
            yield return UiSettled();
            long before = shop.Model.Currency;
            int price = visit.Price;
            Rect serveRect = serve.worldBound;
            Vector2 iconCentre = moneyIcon.worldBound.center;
            int cues = hud.SaleCues, kachings = sounds.Count(UiSound.KaChing), flights = hud.CoinFlights, launched = hud.CoinsLaunched;
            int bumps = feel.Motion.Played(UiMotionKind.ChipBump);
            var trace = NewTrace("sale/" + label);
            yield return FeelClick(serve, null, null, 0, trace, moneyChip, .95f, shot, shotAge, f =>
            {
                f.text = moneyLabel.text;
                f.coins = hud.CoinsShown;
                // The coins are made with the first sale, so the first one is looked up as it flies.
                var coin = f.coins > 0 ? root.Q(HudPresenter.CoinName) : null;
                if (coin != null) { f.coinX = coin.style.left.value.value; f.coinY = coin.style.top.value.value; }
            });
            long after = shop.Model.Currency;
            Check(after == before + price, "A real click on Serve sells for " + price + " (" + label + ")");
            Check(hud.SaleCues == cues + 1 && sounds.Count(UiSound.KaChing) == kachings + 1, "The saved sale rings once: ka-ching (" + label + ")");
            if (coins > 0)
            {
                Check(hud.CoinFlights == flights + 1 && hud.CoinsLaunched == launched + coins && trace.MaxCoins() == coins,
                    coins + " coins fly (" + label + "; at most " + trace.MaxCoins() + " in the air at once)");
                Check((serveRect.Contains(hud.LastCoinOrigin) || serve.worldBound.Contains(hud.LastCoinOrigin)) && Vector2.Distance(hud.LastCoinTarget, iconCentre) < 2f,
                    "from the Serve button " + serveRect.center + " to the money chip's coin " + iconCentre);
                float flight = trace.CoinsTime();
                Check(trace.Took(flight, UiMotion.CoinTotalSeconds), "in about 450 ms (" + flight.ToString("0.000") + " s" + trace.Gap() + ")");
                float firstDistance = -1, lastDistance = -1;
                foreach (var f in trace.frames)
                {
                    if (f.coins == 0) continue;
                    float d = Vector2.Distance(new Vector2(f.coinX + 17f, f.coinY + 17f), iconCentre);
                    if (firstDistance < 0) firstDistance = d;
                    lastDistance = d;
                }
                Check(firstDistance > 0 && lastDistance >= 0 && lastDistance < firstDistance * .35f, "The first coin travels to the chip (" + firstDistance.ToString("0") + " px away, then " + lastDistance.ToString("0") + ")");
            }
            else Check(hud.CoinFlights == flights && hud.CoinsLaunched == launched && trace.MaxCoins() == 0, "No coins fly (" + label + ")");
            var seen = new List<long>();
            float firstChange = -1;
            foreach (var f in trace.frames)
            {
                if (string.IsNullOrEmpty(f.text) || !long.TryParse(f.text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out long v)) continue;
                if (seen.Count == 0 || seen[seen.Count - 1] != v) { seen.Add(v); if (v != before && firstChange < 0) firstChange = f.t; }
            }
            bool rising = true, between = false;
            for (int i = 1; i < seen.Count; i++) rising &= seen[i] >= seen[i - 1];
            foreach (var v in seen) between |= v > before && v < after;
            Check(seen.Count >= 3 && rising && between && seen[0] == before && seen[seen.Count - 1] == after,
                "The money chip counts up " + string.Join(" → ", seen) + " (" + label + ")");
            if (coins > 0) Check(firstChange >= UiMotion.CoinFlightSeconds - .02f, "It starts counting as the first coin lands (" + firstChange.ToString("0.000") + " s)");
            int newBumps = feel.Motion.Played(UiMotionKind.ChipBump) - bumps;
            Check(bump ? newBumps == 1 && trace.MaxScale() > 1.06f : newBumps == 0 && !trace.AnyScaled(),
                bump ? "The chip bumps as the money lands (" + trace.MaxScale().ToString("0.000") + ")" : "The chip doesn't bump with reduced motion");
            yield return UiSettled();
            Check(moneyLabel.text == shop.Model.Currency.ToString("N0") && hud.FeelSettled && hud.CoinsShown == 0, "Then the chip shows the saved credits, " + moneyLabel.text);
        }

        IEnumerator FeelCloseDay(Station counter)
        {
            var root = shop.ToolkitUi.Root;
            if (shop.Panel != ShopPanel.Counter) yield return FeelOpenCard(counter, NewTrace("card-open/counter-close"), null, 0);
            yield return CardClick(root.Q<Button>(CounterCardView.CloseEarlyName), "Close early");
            yield return CardClick(root.Q<Button>(CounterCardView.CloseNowName), "Close now");
            Check(shop.Cycle.Phase == ShopDayPhase.Closing, "Close now closes the shop");
            float until = Time.realtimeSinceStartup + 20f;
            while (shop.Cycle.Phase != ShopDayPhase.Night && Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>Watches a receipt count up from nothing to its totals, then its tip come in (a fade with reduced motion).</summary>
        IEnumerator FeelReceiptCount(int day, string shot, bool reducedTip)
        {
            var ui = shop.ToolkitUi;
            var report = ui.Screens.Report;
            var root = ui.Root;
            float until = Time.realtimeSinceStartup + 5f;
            int started = report.CountsStarted, slides = report.TipSlides;
            while (!(shop.Panel == ShopPanel.DayReport && report.Counting) && Time.realtimeSinceStartup < until) yield return null;
            Check(report.Counting && report.CountsStarted == started + 1 && report.State.Day == day && report.State.Revenue > 0,
                "Night " + day + "'s receipt opens counting up (income " + report.State.Revenue + ")");
            var incomeLabel = root.Q<Label>(ReportView.IncomeName);
            var trace = NewTrace("receipt/day-" + day);
            float countEnded = -1, countTook = -1;
            yield return Watch(trace, report.TipBox, 1.1f, shot, .25f, f =>
            {
                f.text = incomeLabel.text;
                if (countEnded < 0 && !report.Counting) { countEnded = f.t; countTook = feel.Now - report.CountStart; }
            });
            var seen = new List<long>();
            foreach (var f in trace.frames)
            {
                string t = f.text != null ? f.text.TrimStart('+') : "";
                if (!long.TryParse(t, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out long v)) continue;
                if (seen.Count == 0 || seen[seen.Count - 1] != v) seen.Add(v);
            }
            long total = report.State.Revenue;
            bool between = false, rising = true;
            for (int i = 1; i < seen.Count; i++) rising &= seen[i] >= seen[i - 1];
            foreach (var v in seen) between |= v > 0 && v < total;
            Check(seen.Count >= 3 && rising && between && seen[seen.Count - 1] == total, "The income counts up " + string.Join(" → ", seen));
            Check(trace.Took(countTook, UiMotion.CountUpSeconds), "over about 600 ms (" + countTook.ToString("0.000") + " s from its start" + trace.Gap() + ")");
            float tipStart = -1;
            foreach (var f in trace.frames) if ((f.moved || (f.faded && f.opacity > .01f)) && tipStart < 0) tipStart = f.t;
            Check(report.TipSlides == slides + 1 && tipStart >= countEnded - .02f, "Then the tip comes in (" + tipStart.ToString("0.000") + " s)");
            if (reducedTip) Check(!trace.AnyMoved() && trace.AnyFaded(), "With reduced motion the tip fades in where it rests");
            else Check(trace.MaxMove() >= 6f && trace.MaxMove() <= UiMotion.TipSlidePixels + .01f, "sliding up " + trace.MaxMove().ToString("0.0") + " px");
            Check(!report.Counting && root.Q<Label>(ReportView.SoldName).text == report.State.Sales.ToString() && !Posed(report.TipBox), "The receipt rests at its totals");
            CheckScreen("Receipt " + day + ", counted", 1);
        }
    }
}
#endif
