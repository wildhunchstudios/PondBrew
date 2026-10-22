#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    // AD-B creature families (-evolutionDioramaCreaturesSmoke): every kit species in the display tanks with its swim, idle
    // habit, signature trick and family water; the comfort modes; each species in the incubator with its family fluid; the
    // creature study's species, variants and tricks through its controls; and the look switched off and on.
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class CreatureReceipt
        {
            public string species, family;
            public int swimFrames, trickFrames, bursts;
            public float tankScale, happyPeak, puffedPeak, glowPeak, glowRest;
            public bool trick;
            // The water and the incubator fluid as the screen shows them, read behind the creature (median of patches).
            public Color waterOnScreen, fluidOnScreen;
        }
        [Serializable] sealed class CreatureTrace { public List<CreatureReceipt> species = new List<CreatureReceipt>(); }
        readonly CreatureTrace creatureTrace = new CreatureTrace();

        IEnumerator DioramaCreaturesScenario()
        {
            limitations.Add("AD-B creature families: isolated slot; each species is put in the display tanks and the incubator by a presentation-only validation hook (no saved specimen; the next refresh puts the saved residents back), so the new kit species' hatch, storage and sale are not repeated here with the look on; scripted camera views; the creature study's controls pressed with the synthetic Input System mouse. Screens are not hands-on review, creative approval or a minimum-spec GPU result.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "AD-B isolated save only");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("AD-B keyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("AD-B mouse"); cardMouse = cycleMouse;
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            if (!Application.isEditor)
            {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Check(Screen.width == width && Screen.height == height, "Actual requested Windows resolution " + width + "x" + height);
            }
            GateFresh();
            var w = shop.World;
            yield return CreaturesFoundation(w);
            int count = DioramaSpecies.All.Count;
            for (int i = 0; i < count; i++) yield return CreatureInTank(w, DioramaSpecies.All[i], DioramaSpecies.All[(i + 1) % count]);
            FamilyColoursOnScreen("tank water", r => r.waterOnScreen);
            yield return CreatureComfort(w);
            yield return CreatureIncubator(w);
            yield return CreatureTimings(w);
            yield return CreatureStudy(w);
            yield return CreatureToggle(w);
            File.WriteAllText(Path.Combine(evidence, "creature-trace.json"), JsonUtility.ToJson(creatureTrace, true));
            shop.SetPanel(ShopPanel.Pause);
        }

        IEnumerator CreaturesFoundation(ShopWorld w)
        {
            Check(w.DioramaActive && DioramaLook.Active && shop.DioramaLookOn, "The creatures route switches the diorama look on");
            Check(DioramaVariants.Ready, "The colour variant atlas loads, readable, " + DioramaVariants.Slots.Length + " x " + DioramaVariants.SpeciesCapacity * 4);
            foreach (var spec in DioramaSpecies.All)
            {
                var kit = Resources.Load<GameObject>(DioramaLook.KitPath + spec.Kit);
                Check(kit, "Kit loads / " + spec.Kit);
                if (!kit) continue;
                foreach (var part in new[] { "Body", "Eyes", "Pupils", "Glints", "Lids", "Blush", "Lips", "Tail pivot", "Left fin pivot", "Right fin pivot", "Crest pivot" })
                    Check(kit.transform.Find(part), "The family rig's part / " + spec.Id + " / " + part);
                Check((kit.transform.Find("Lantern pivot") != null) == (spec.Family == DioramaFamily.Sparkline), "Only the Sparkline carries a lantern / " + spec.Id);
            }
            yield return null;
        }

        IEnumerator CreatureInTank(ShopWorld w, DioramaSpecies spec, DioramaSpecies neighbour)
        {
            var receipt = new CreatureReceipt { species = spec.Id, family = spec.Family.ToString() };
            w.PreviewResidentForValidation("tank-a", spec.Id, "adb-" + spec.Id);
            w.PreviewResidentForValidation("tank-b", neighbour.Id, "adb-b-" + neighbour.Id);
            w.SnapDioramaWaterForValidation();
            yield return null; yield return null;
            var creature = w.DisplayCreatureForValidation("tank-a");
            Check(creature && creature.IsDiorama && creature.DioramaSpecies.Id == spec.Id, "The kit " + spec.Id + " lives in the display tank");
            if (!creature) yield break;
            float classic = spec.Id == "driftwing" ? .59f : .69f;
            receipt.tankScale = creature.transform.localScale.x;
            Check(Mathf.Abs(receipt.tankScale - classic * spec.TankScale) < .002f, "Display tank shows it larger (AD.4) / " + spec.Id + " / scale " + receipt.tankScale);
            var tint = DioramaWater.Tint(spec.Family);
            Check(Near(w.DioramaWaterForValidation("tank-a"), tint, .01f), "The tank water takes the " + spec.Family + " tint / " + spec.Id + " / " + w.DioramaWaterForValidation("tank-a"));
            var glow = creature.DioramaGlowMaterial;
            Check((glow != null) == (spec.Family == DioramaFamily.Sparkline), "Only the lantern glows / " + spec.Id);
            if (glow) receipt.glowRest = MaxRgb(glow.GetColor("_EmissionColor"));
            DioramaView(CornerEye, CornerTarget);
            int tricks = w.DioramaTricksPlayed, glances = w.PondletGlanceCount, bursts = w.DioramaCreatureBursts, bubbles = w.DioramaTrickBubbles;
            float start = Time.realtimeSinceStartup;
            bool captured = false;
            while (Time.realtimeSinceStartup - start < 9)
            {
                yield return null;
                Check(w.ValidateTankBounds(out var bounds), "Every kit creature mesh stays inside the glass / " + spec.Id + " / trick age " + w.DioramaTrickAgeForValidation("tank-a").ToString("0.00") + " / " + bounds);
                receipt.swimFrames++;
                receipt.happyPeak = Mathf.Max(receipt.happyPeak, creature.DioramaWeightForValidation("Happy"));
                receipt.puffedPeak = Mathf.Max(receipt.puffedPeak, creature.DioramaWeightForValidation("Puffed"));
                if (glow) receipt.glowPeak = Mathf.Max(receipt.glowPeak, MaxRgb(glow.GetColor("_EmissionColor")));
                float age = w.DioramaTrickAgeForValidation("tank-a");
                if (age >= 0) receipt.trickFrames++;
                if (!captured && age >= DioramaTricks.Duration(spec.Id) * .45f)
                {
                    captured = true;
                    yield return DioramaShot("creature-" + spec.Id + "-trick");
                }
                receipt.trick = spec.Id == "pondlet" ? w.PondletGlanceCount > glances : w.DioramaTricksPlayed > tricks;
                if (receipt.trick && age < 0 && Time.realtimeSinceStartup - start > 7.6f) break;
            }
            receipt.bursts = w.DioramaCreatureBursts - bursts + w.DioramaTrickBubbles - bubbles;
            Check(receipt.swimFrames > 60, "Swim sampled for bounds / " + spec.Id + " / " + receipt.swimFrames + " frames");
            receiptsBySpecies[spec.Id] = receipt;
            Check(receipt.trick, "Lingering at the tank: its signature trick plays / " + spec.Id + " (" + spec.Signature + ")");
            Check(receipt.happyPeak > 50, "Its face lights up for the trick / " + spec.Id + " / " + receipt.happyPeak);
            Check(receipt.bursts >= (spec.Id == "bubblebelly" ? 2 : 1), "The trick (and the Bubblebelly's hiccup) releases bubbles or sparkles / " + spec.Id + " / " + receipt.bursts);
            if (spec.Id == "bubblebelly") Check(receipt.puffedPeak > 80, "The Bubblebelly balloons up / Puffed " + receipt.puffedPeak);
            if (spec.Id == "lanternfin") Check(receipt.glowPeak > receipt.glowRest * 3, "The lantern flares far past its resting glow / " + receipt.glowRest + " -> " + receipt.glowPeak);
            if (spec.Id != "pondlet") Check(receipt.trickFrames > 20, "The trick plays out / " + spec.Id + " / " + receipt.trickFrames + " frames");
            // The close-up aims a little to the right of the tank, so the aim prompt beside the crosshair clears the creature.
            var eye = new Vector3(3.6f, .2f, -.2f); var across = CornerTarget - eye; across.y = 0;
            DioramaView(eye, CornerTarget + Vector3.Cross(Vector3.up, across.normalized) * .32f);
            yield return FacingCamera(creature, 7, () => w.DioramaTrickAgeForValidation("tank-a") < 0);
            yield return DioramaShot("creature-" + spec.Id + "-tank");
            var tank = creature.transform.parent.parent;
            // The back of the water, clear of the fronds and bubbles at the sides.
            var waterPoints = new List<Vector3>();
            for (float x = -.6f; x <= .61f; x += .15f) for (float y = 1.12f; y <= 2.0f; y += .16f) waterPoints.Add(tank.TransformPoint(new Vector3(x, y, .58f)));
            yield return ScreenMedian(waterPoints, creature, c => receipt.waterOnScreen = c);
            creatureTrace.species.Add(receipt);
        }

        /// <summary>Waits (up to the given seconds) for the swimming creature, between tricks, to turn its face towards the camera.</summary>
        IEnumerator FacingCamera(CreatureVisual creature, float seconds, Func<bool> ready)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds && creature && creature.DioramaLips && shop.Player.View)
            {
                Vector3 nose = creature.DioramaLips.position - creature.transform.position, view = shop.Player.View.transform.position - creature.transform.position;
                nose.y = view.y = 0;
                if (ready() && nose.sqrMagnitude > 1e-6f && Vector3.Dot(nose.normalized, view.normalized) > .4f) yield break;
                yield return null;
            }
        }

        readonly Dictionary<string, CreatureReceipt> receiptsBySpecies = new Dictionary<string, CreatureReceipt>();

        /// <summary>Reads the screen at the given points with the creature hidden for that one frame (presentation only, shown
        /// again at once), so it is the water itself that is read: the median (by brightness) of 9x9-pixel patches, skipping
        /// the aim prompt beside the crosshair.</summary>
        IEnumerator ScreenMedian(List<Vector3> points, CreatureVisual hide, Action<Color> result)
        {
            var hidden = new List<Renderer>();
            if (hide) foreach (var r in hide.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var r in hidden) if (r) r.enabled = true;
            try
            {
                var camera = shop.Player.View; var samples = new List<Color>();
                float sx = shot.width / (float)Screen.width, sy = shot.height / (float)Screen.height;
                var prompt = Rect.MinMaxRect(Screen.width * .5f, Screen.height * .42f, Screen.width * .72f, Screen.height * .58f);
                foreach (var point in points)
                {
                    var p = camera.WorldToScreenPoint(point);
                    if (p.z <= 0 || prompt.Contains(p)) continue;
                    int cx = Mathf.RoundToInt(p.x * sx), cy = Mathf.RoundToInt(p.y * sy);
                    Color sum = Color.clear; int n = 0;
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            int px = cx + dx, py = cy + dy;
                            if (px < 0 || py < 0 || px >= shot.width || py >= shot.height) continue;
                            sum += shot.GetPixel(px, py); n++;
                        }
                    if (n > 0) samples.Add(sum / n);
                }
                samples.Sort((x, y) => (x.r + x.g + x.b).CompareTo(y.r + y.g + y.b));
                Check(samples.Count >= points.Count / 2, "The water is on screen to read / " + samples.Count + " of " + points.Count + " patches");
                result(samples.Count > 0 ? samples[samples.Count / 2] : Color.clear);
            }
            finally { Destroy(shot); }
        }

        static float Warmth(Color c) => c.r - c.b;
        static float Greenness(Color c) => c.g - (c.r + c.b) * .5f;
        static string Rgb(Color c) => "(" + c.r.ToString("0.00") + ", " + c.g.ToString("0.00") + ", " + c.b.ToString("0.00") + ")";

        /// <summary>AD.4: no species blends into its water. Checked as the screen shows it (a tint set but drowned by the night
        /// glow once passed every value check, and measured only 0.06 apart): the Airfin's water reads clearly warmer, the
        /// Sparkline's clearly greener, than the Brookkin's.</summary>
        void FamilyColoursOnScreen(string what, Func<CreatureReceipt, Color> of)
        {
            var brook = of(receiptsBySpecies["pondlet"]); var spark = of(receiptsBySpecies["lanternfin"]); var air = of(receiptsBySpecies["driftwing"]);
            Check(Warmth(air) - Warmth(brook) > .2f, "The Airfin's " + what + " reads warmer than the Brookkin's on screen / " + Rgb(air) + " vs " + Rgb(brook));
            Check(Greenness(spark) - Greenness(brook) > .12f, "The Sparkline's " + what + " reads greener than the Brookkin's on screen / " + Rgb(spark) + " vs " + Rgb(brook));
            // Within a family the reading varies a little with the animated caustics and the water's own pulse (up to 0.06 seen),
            // well under the 0.2 and 0.12 that set the families apart.
            foreach (var other in new[] { "bubblebelly", "ribbonfin" })
            {
                var c = of(receiptsBySpecies[other]);
                Check(Mathf.Abs(Warmth(c) - Warmth(brook)) < .1f && Mathf.Abs(Greenness(c) - Greenness(brook)) < .1f,
                    "Every Brookkin shares its family's " + what + " on screen / " + other + " " + Rgb(c) + " vs pondlet " + Rgb(brook));
            }
        }

        static float MaxRgb(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

        IEnumerator CreatureComfort(ShopWorld w)
        {
            w.PreviewResidentForValidation("tank-a", "lanternfin", "adb-comfort");
            yield return null;
            var creature = w.DisplayCreatureForValidation("tank-a");
            Check(shop.SetComfortSettings(true, FxEffectDetail.Standard), "Reduced visual motion for the creatures");
            DioramaView(CornerEye, CornerTarget);
            yield return null;
            int tricks = w.DioramaTricksPlayed, bursts = w.DioramaCreatureBursts;
            var position = creature.transform.localPosition; var rotation = creature.transform.localRotation;
            bool still = true;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 3.5f)
            {
                yield return null;
                still &= creature.transform.localPosition == position && creature.transform.localRotation == rotation;
            }
            Check(w.DioramaTricksPlayed == tricks && w.DioramaCreatureBursts == bursts, "Reduced motion: no trick, no habit, no bubbles or sparkles");
            Check(still, "Reduced motion: the creature holds still");
            var glow = creature.DioramaGlowMaterial;
            Check(glow && Mathf.Abs(MaxRgb(glow.GetColor("_EmissionColor")) - MaxRgb(glow.GetColor("_BaseColor")) * .6f) < .01f, "Reduced motion: the lantern keeps its resting glow");
            yield return DioramaShot("creature-reduced");
            Check(shop.SetComfortSettings(false, FxEffectDetail.Low), "Low effects detail for the creatures");
            w.PreviewResidentForValidation("tank-a", "lanternfin", "adb-comfort-low");
            yield return null;
            tricks = w.DioramaTricksPlayed; bursts = w.DioramaCreatureBursts;
            start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 4f && w.DioramaTricksPlayed == tricks) yield return null;
            yield return new WaitForSeconds(DioramaTricks.Duration("lanternfin"));
            Check(w.DioramaTricksPlayed > tricks && w.DioramaCreatureBursts > bursts, "Low effects detail still plays the trick, with fewer sparkles");
            Check(shop.SetComfortSettings(false, FxEffectDetail.Standard), "Back to standard comfort");
            yield return null;
        }

        /// <summary>AD.7: frame cost with the busiest residents (the Lanternfin, which flares and sparkles at the tank the camera
        /// lingers at, the Driftwing beside it and a Ribbonfin in the incubator), the look on and off, as the AD-A route measures.</summary>
        IEnumerator CreatureTimings(ShopWorld w)
        {
            w.PreviewResidentForValidation("tank-a", "lanternfin", "adb-timing-a");
            w.PreviewResidentForValidation("tank-b", "driftwing", "adb-timing-b");
            w.PreviewResidentForValidation("incubator", "ribbonfin", "adb-timing-incubator");
            yield return null;
            int tricks = w.DioramaTricksPlayed;
            yield return DioramaTimings(w);
            Check(w.DioramaTricksPlayed > tricks, "The Lanternfin performs while the corner is timed");
            File.WriteAllText(Path.Combine(evidence, "diorama-timing.json"), JsonUtility.ToJson(dioramaTiming, true));
            w.PreviewResidentForValidation("incubator", null, null);
            Check(w.DioramaActive && shop.DioramaLookOn, "The look is on again after the timings");
            yield return null;
        }

        IEnumerator CreatureIncubator(ShopWorld w)
        {
            foreach (var spec in DioramaSpecies.All)
            {
                w.PreviewResidentForValidation("incubator", spec.Id, "adb-incubator-" + spec.Id);
                DioramaView(IncubatorEye, IncubatorTarget);
                yield return new WaitForSeconds(1.2f);
                var creature = w.HatchCreatureForValidation ? w.HatchCreatureForValidation.GetComponent<CreatureVisual>() : null;
                Check(creature && creature.IsDiorama && creature.DioramaSpecies.Id == spec.Id, "The kit " + spec.Id + " in the incubator");
                var fluid = w.DioramaIncubatorFluidForValidation;
                Check(Near(fluid, DioramaWater.Fluid(spec.Family), .02f), "The incubator fluid takes the " + spec.Family + " tint / " + fluid);
                yield return DioramaShot("creature-" + spec.Id + "-incubator");
                var incubator = creature ? creature.transform.parent : null;
                while (incubator && !incubator.Find("Incubator fluid")) incubator = incubator.parent;
                Check(incubator, "The incubator's fluid found");
                if (incubator && receiptsBySpecies.TryGetValue(spec.Id, out var receipt))
                {
                    // Inside the fluid's silhouette in the hatchery frame (an ellipse 0.69 by 0.48 around 1.48).
                    var fluidPoints = new List<Vector3>();
                    for (float x = -.6f; x <= .61f; x += .1f)
                        for (float y = -.45f; y <= .46f; y += .075f)
                            if ((x / .69f) * (x / .69f) + (y / .48f) * (y / .48f) < .8f) fluidPoints.Add(incubator.TransformPoint(new Vector3(x, 1.48f + y, .3f)));
                    yield return ScreenMedian(fluidPoints, creature, c => receipt.fluidOnScreen = c);
                }
            }
            // The incubator's fluid only hints at the family colour: the hatchery frame's back panel sits inside the fluid, so
            // only its near side (in front of the creature) can tint, and the lab's cool grade mutes warm tints. Its on-screen
            // readings are recorded in creature-trace.json, not held to the tanks' standard.
            w.PreviewResidentForValidation("incubator", null, null);
            yield return null;
        }

        IEnumerator CreatureStudy(ShopWorld w)
        {
            var root = shop.ToolkitUi.Root;
            shop.SetPanel(ShopPanel.Pause);
            yield return HudFrames(3);
            yield return CardClick(root.Q<Button>(PauseView.PresentationName), "Presentation sample");
            yield return HudFrames(3);
            shop.SelectPresentationView(2);
            yield return HudFrames(3);
            Check(shop.PresentationCreatures && CardShown(root, StudyPresenter.CreaturesName) && CardShown(root, StudyPresenter.SpeciesName) &&
                CardShown(root, StudyPresenter.VariantName) && CardShown(root, StudyPresenter.TrickName), "The creature study offers species, colour and trick with the look on");
            var strings = shop.ToolkitUi.Strings;
            for (int i = 0; i < DioramaSpecies.All.Count; i++)
            {
                var spec = DioramaSpecies.All[i];
                if (i > 0) yield return CardClick(root.Q<Button>(StudyPresenter.SpeciesName), "Next species");
                else shop.SelectPresentationSpecies(0);
                yield return HudFrames(2);
                var study = w.StudyCreatureForValidation;
                Check(shop.PresentationSpeciesId == spec.Id && w.StudySpeciesForValidation == spec.Id && study && study.IsDiorama && study.DioramaSpecies.Id == spec.Id,
                    "Next species shows the kit " + spec.Id + " on the turntable");
                for (int v = 0; v < 4; v++)
                {
                    var variant = (DioramaVariant)v;
                    if (v > 0) yield return CardClick(root.Q<Button>(StudyPresenter.VariantName), "Colour");
                    else shop.SelectPresentationVariant(0);
                    yield return HudFrames(2);
                    study = w.StudyCreatureForValidation;
                    var body = study ? study.DioramaSlotForValidation(0) : null;
                    var expected = DioramaVariants.Color(spec.Index, variant, 0); expected.a = 1;
                    var sheen = DioramaVariants.Color(spec.Index, variant, DioramaVariants.Sheen);
                    Check(shop.PresentationVariant == variant && body && Near(body.GetColor("_BaseColor"), expected, .003f) &&
                        Mathf.Abs(body.GetFloat("_SheenStrength") - sheen.a) < .003f, "The atlas colours the " + spec.Id + " " + variant);
                    string title = strings.Format("study.creature.title", strings.Get("study.species." + spec.Id), strings.Get("study.variant." + variant));
                    Check(StudyText(root, StudyPresenter.TitleName) == title, "The study names the species and colour / " + title);
                    yield return DioramaShot("study-" + spec.Id + "-" + variant.ToString().ToLowerInvariant());
                    CheckStudy("Creature study, " + spec.Id + " " + variant);
                }
                yield return CardClick(root.Q<Button>(StudyPresenter.TrickName), "Trick");
                yield return HudFrames(2);
                Check(w.StudyTrickAgeForValidation >= 0, "Trick plays the " + spec.Id + "'s signature trick on the turntable");
                float half = (spec.Id == "pondlet" ? .6f : DioramaTricks.Duration(spec.Id)) * .45f;
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < 3 && w.StudyTrickAgeForValidation >= 0 && w.StudyTrickAgeForValidation < half) yield return null;
                yield return DioramaShot("study-" + spec.Id + "-trick");
                while (Time.realtimeSinceStartup - start < 4 && w.StudyTrickAgeForValidation >= 0) yield return null;
                Check(w.StudyTrickAgeForValidation < 0, "The trick ends / " + spec.Id);
            }
            yield return CardClick(root.Q<Button>(StudyPresenter.BackName), "Back to pause");
            Check(shop.Panel == ShopPanel.Pause, "Back to pause from the creature study");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
        }

        IEnumerator CreatureToggle(ShopWorld w)
        {
            // Previewed residents are not saved, so a look switch (which rebuilds from the game) empties the tank; show it again.
            shop.SetDioramaLook(false);
            yield return null;
            w.PreviewResidentForValidation("tank-a", "driftwing", "adb-toggle");
            yield return null; yield return null;
            var creature = w.DisplayCreatureForValidation("tank-a");
            Check(!DioramaLook.Active && creature && !creature.IsDiorama, "Look off: the tank shows the classic Driftwing again");
            Check(w.DioramaMaterialsConsistent(out var off), "Every swapped renderer is back to its classic material / " + off);
            shop.SetDioramaLook(true);
            yield return null;
            w.PreviewResidentForValidation("tank-a", "driftwing", "adb-toggle-on");
            yield return null; yield return null;
            creature = w.DisplayCreatureForValidation("tank-a");
            Check(DioramaLook.Active && creature && creature.IsDiorama && creature.DioramaSpecies.Id == "driftwing", "Look on again: the kit Driftwing is back");
            Check(w.DioramaMaterialsConsistent(out var on), "Swapped renderers show the diorama materials again / " + on);
        }
    }
}
#endif
