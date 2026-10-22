#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    // AD-D rooms and effects (-evolutionDioramaRoomsSmoke): the look on every room, wing, station and placed item; each
    // tier's set piece as the wings open (a bay window bench and hanging bowls, a feature tank, an overhead culture pipe
    // gallery, a Spark and Mist glass dome); the culture pipes carrying each culture to the incubator, pulsing with a dose
    // and following a moved station; the slimmer siphon; switching the look off and on; the comfort modes; and timings.
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class RoomsReceipt
        {
            public float classicToolArea, dioramaToolArea, toolAreaRatio;
            public int convertedMaterials, convertedAfterToggle;
            public List<string> setPieces = new List<string>(), pipes = new List<string>(), notes = new List<string>();
        }
        readonly RoomsReceipt roomsReceipt = new RoomsReceipt();

        IEnumerator DioramaRoomsScenario()
        {
            limitations.Add("AD-D rooms and effects: isolated slot started from the frozen v15 save (night, lab wing 1, Spark researched, a conditioning vat in the wing); Mist is researched there and both wings of each room bought with labelled credit fixtures through the game's own purchases; lighting keys forced by the presentation-only validation hook; a lab machine moved with the real placement controls; scripted camera views. Screens are not hands-on review, listening, creative approval or a minimum-spec GPU result. Frame timings are this PC's own, with the look on and off in the same session.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "AD-D isolated save only");
            yield return new WaitForSeconds(1);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("AD-D keyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("AD-D mouse"); cardMouse = cycleMouse;
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            shop.Customers.ArrivalsEnabled = false;
            if (!Application.isEditor)
            {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Check(Screen.width == width && Screen.height == height, "Actual requested Windows resolution " + width + "x" + height);
            }
            string fixture = Application.isEditor ?
                Path.Combine(Application.dataPath, "EvolutionShop", "Tests", "RuntimeEditMode", "Fixtures", "V15Conditioning.json") :
                Path.Combine(evidence, "seed-v15.json");
            File.WriteAllBytes(shop.SavePath, File.ReadAllBytes(fixture));
            Check(shop.ReloadSaved() && shop.Cycle.Phase == ShopDayPhase.Night && shop.Model.SparkResearchUnlocked && shop.Layout.LabTier == 1 && shop.Layout.StoreTier == 0,
                "The frozen v15 save loads: night, Spark researched, lab wing 1");
            if (!shop.DioramaLookOn) shop.SetDioramaLook(true);
            var w = shop.World;
            yield return RoomsFoundation(w);
            yield return RoomsMist(w);
            yield return RoomsTiers(w);
            yield return RoomsPipes(w);
            yield return RoomsToggle(w);
            yield return RoomsMoments(w);
            yield return RoomsComfort(w);
            yield return RoomsSiphon(w);
            yield return RoomsTimings(w);
            File.WriteAllText(Path.Combine(evidence, "rooms-trace.json"), JsonUtility.ToJson(roomsReceipt, true));
            shop.SetPanel(ShopPanel.Pause);
        }

        void RoomsKey(ShopWorld w, ShopDayPhase phase, float progress)
        {
            shop.DioramaTimeForValidation = (phase, progress);
            w.SnapDioramaLightForValidation();
        }

        IEnumerator RoomsShot(ShopWorld w, string name, Vector3 eye, Vector3 target)
        {
            DioramaView(eye, target);
            yield return null;
            w.SnapDioramaLightForValidation();
            yield return DioramaShot(name);
        }

        void RoomsConsistent(ShopWorld w, string context)
        {
            Check(w.DioramaWorldConsistent(out var detail), "The world is consistent with the look / " + context + " / " + detail);
            Check(w.DioramaMaterialsConsistent(out var swaps), "The AD-A corner is consistent with the look / " + context + " / " + swaps);
        }

        // ---------------------------------------------------------------- foundation

        IEnumerator RoomsFoundation(ShopWorld w)
        {
            Check(w.DioramaActive && DioramaLook.Active, "The rooms route switches the diorama look on");
            yield return null; yield return null;
            RoomsConsistent(w, "on");
            roomsReceipt.convertedMaterials = w.DioramaConvertedCount;
            Check(w.DioramaConvertedCount >= 20, "The look converts the world's own materials / " + w.DioramaConvertedCount);
            Check(w.DioramaSignsSimplified == 2, "The shop's and the lab's signs show their names alone, larger / " + w.DioramaSignsSimplified);
            RoomsKey(w, ShopDayPhase.Night, 0);
            yield return RoomsShot(w, "rooms-store-night", new Vector3(3.2f, .2f, -.6f), new Vector3(-3f, 1.4f, -3.8f));
            yield return RoomsShot(w, "rooms-lab-night", new Vector3(0f, .2f, 1.0f), new Vector3(-.5f, 2.2f, 4.6f));
            RoomsKey(w, ShopDayPhase.Open, .55f);
            yield return RoomsShot(w, "rooms-store-midday", new Vector3(3.2f, .2f, -.6f), new Vector3(-3f, 1.4f, -3.8f));
            yield return RoomsShot(w, "rooms-lab-midday", new Vector3(0f, .2f, 1.0f), new Vector3(-.5f, 2.2f, 4.6f));
            shop.SetDioramaLook(false); yield return null;
            RoomsConsistent(w, "off");
            yield return RoomsShot(w, "rooms-store-classic", new Vector3(3.2f, .2f, -.6f), new Vector3(-3f, 1.4f, -3.8f));
            yield return RoomsShot(w, "rooms-lab-classic", new Vector3(0f, .2f, 1.0f), new Vector3(-.5f, 2.2f, 4.6f));
            shop.SetDioramaLook(true); yield return null;
            RoomsKey(w, ShopDayPhase.Night, 0);
        }

        // ---------------------------------------------------------------- siphon

        IEnumerator RoomsSiphon(ShopWorld w)
        {
            DioramaView(new Vector3(0, .2f, -2.6f), new Vector3(0, 1.1f, -5.2f));
            yield return null; yield return null;
            var camera = shop.Player.View;
            var on = w.ToolScreenRectForValidation(camera);
            yield return DioramaShot("rooms-siphon-look");
            shop.SetDioramaLook(false); yield return null; yield return null;
            var off = w.ToolScreenRectForValidation(camera);
            yield return DioramaShot("rooms-siphon-classic");
            shop.SetDioramaLook(true); yield return null; yield return null;
            roomsReceipt.dioramaToolArea = on.width * on.height; roomsReceipt.classicToolArea = off.width * off.height;
            roomsReceipt.toolAreaRatio = roomsReceipt.classicToolArea > 0 ? roomsReceipt.dioramaToolArea / roomsReceipt.classicToolArea : 0;
            Check(roomsReceipt.toolAreaRatio > .5f && roomsReceipt.toolAreaRatio < .72f,
                "The siphon takes roughly a third less of the screen with the look / " + roomsReceipt.toolAreaRatio.ToString("0.00"));
            var back = w.ToolScreenRectForValidation(camera);
            Check(Mathf.Abs(back.width * back.height - roomsReceipt.dioramaToolArea) < 1, "The siphon slims again when the look comes back");
        }

        // ---------------------------------------------------------------- Mist

        IEnumerator RoomsMist(ShopWorld w)
        {
            StaffCreditFixture(1000, "AD-D labelled Mist research and expansion fixture");
            Check(shop.Model.MistResearchReady && !shop.Model.MistResearchUnlocked, "The v15 vat and Lanternfin satisfy the Mist gate");
            string vat = shop.Model.ConditionerIds[0];
            yield return ConditioningPanel(vat);
            Check(shop.ResearchMist() && shop.Model.MistResearchUnlocked, "Mist researched at the real vat");
            shop.SetPanel(ShopPanel.None);
            if (!shop.DioramaLookOn) shop.SetDioramaLook(true);
            yield return null;
        }

        // ---------------------------------------------------------------- tiers and set pieces

        IEnumerator RoomsTiers(ShopWorld w)
        {
            // Lab wing 1 is open in the save: its gallery shows with the look.
            yield return null;
            Check(w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.PipeGallery), "Lab wing 1 shows its overhead culture pipe gallery");
            Check(!w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.WindowBench) && !w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.FeatureTank) &&
                !w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.GlassDome), "No set piece shows before its wing opens");
            yield return RoomsShot(w, "rooms-lab-gallery-night", new Vector3(0f, .2f, 4.2f), new Vector3(0, 3.0f, 8.6f));
            // The store wings, bought through the game's own purchases.
            Check(shop.OpenPlacementMenu() && shop.BuyStoreExpansion(), "Buy the first store wing");
            shop.SetPanel(ShopPanel.None); yield return null; yield return null;
            Check(w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.WindowBench) && w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.HangingBowls),
                "Store wing 1 shows its bay window bench and hanging bowls");
            RoomsSetPieceRules(w, "store wing 1");
            yield return RoomsShot(w, "rooms-bench-night", new Vector3(-6.2f, .2f, -1.6f), new Vector3(-9.2f, 1.2f, -5.6f));
            yield return RoomsShot(w, "rooms-bowls-night", new Vector3(-5.4f, .2f, -.6f), new Vector3(-9.2f, 2.8f, -3.2f));
            Check(shop.OpenPlacementMenu() && shop.BuyStoreExpansion(), "Buy the second store wing");
            shop.SetPanel(ShopPanel.None); yield return null; yield return null;
            Check(w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.FeatureTank), "Store wing 2 shows its feature tank");
            RoomsSetPieceRules(w, "store wing 2");
            yield return RoomsShot(w, "rooms-tank-night", new Vector3(-11.6f, .2f, -2.2f), new Vector3(-13f, 1.5f, -5.8f));
            GateAim(new Vector3(0, .2f, 2), new Vector3(0, 1.2f, 5)); yield return null;
            Check(shop.OpenLabMenu() && shop.BuyLabExpansion(), "Buy the second lab wing");
            shop.SetPanel(ShopPanel.None); yield return null; yield return null;
            Check(w.DioramaSetPieceShown(ShopWorld.DioramaSetPiece.GlassDome), "Lab wing 2 shows its glass dome");
            Check(w.DioramaDomeGlows(CultureKind.Spark) && w.DioramaDomeGlows(CultureKind.Mist), "The dome glows with researched Spark and Mist");
            RoomsSetPieceRules(w, "lab wing 2");
            yield return RoomsShot(w, "rooms-dome-night", new Vector3(0f, .2f, 8.4f), new Vector3(0, 3.6f, 11.6f));
            foreach (var (phase, progress, key) in new[] { (ShopDayPhase.Open, .55f, "midday"), (ShopDayPhase.Closing, 0f, "closing") })
            {
                RoomsKey(w, phase, progress);
                yield return RoomsShot(w, "rooms-bench-" + key, new Vector3(-6.2f, .2f, -1.6f), new Vector3(-9.2f, 1.2f, -5.6f));
                yield return RoomsShot(w, "rooms-tank-" + key, new Vector3(-11.6f, .2f, -2.2f), new Vector3(-13f, 1.5f, -5.8f));
                yield return RoomsShot(w, "rooms-dome-" + key, new Vector3(0f, .2f, 8.4f), new Vector3(0, 3.6f, 11.6f));
                yield return RoomsShot(w, "rooms-gallery-" + key, new Vector3(0f, .2f, 4.2f), new Vector3(0, 3.0f, 8.6f));
            }
            RoomsKey(w, ShopDayPhase.Night, 0);
            Check(w.DioramaSignsSimplified == 4, "Each wing's sign shows its name alone too / " + w.DioramaSignsSimplified);
            yield return RoomsShot(w, "rooms-store-wide-night", new Vector3(3.2f, .2f, -.6f), new Vector3(-8f, 1.8f, -3.4f));
            foreach (var piece in (ShopWorld.DioramaSetPiece[])Enum.GetValues(typeof(ShopWorld.DioramaSetPiece)))
                roomsReceipt.setPieces.Add(piece + ": " + (w.DioramaSetPieceShown(piece) ? "shown" : "hidden"));
        }

        /// <summary>A set piece is decoration: no collider, nothing on the floor the player arranges, the wing's walls keep
        /// their colliders, and the world stays consistent with the look.</summary>
        void RoomsSetPieceRules(ShopWorld w, string context)
        {
            RoomsConsistent(w, context);
            foreach (var piece in (ShopWorld.DioramaSetPiece[])Enum.GetValues(typeof(ShopWorld.DioramaSetPiece)))
            {
                var root = w.DioramaSetPieceRoot(piece);
                if (!root || !root.gameObject.activeInHierarchy) continue;
                Check(root.GetComponentsInChildren<Collider>(true).Length == 0, "A set piece adds no collider / " + piece);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var b = renderer.bounds;
                    // Floors lie under the arranged floor by design, and wall art stays inside the classic panelling's face.
                    if (b.max.y <= .05f) continue;
                    // The arranged floor: the store from x = StoreMinX + 0.2 to the lab wall, z -5.4 to 0.19; the lab from z 0.81.
                    bool overStore = b.max.z > -5.25f && b.min.z < .19f && b.min.x < 6.8f, overLab = b.max.z > .81f && b.min.x > -6.8f && b.max.x < 6.8f;
                    bool low = b.min.y < 2.4f;
                    Check(!(low && (overStore || overLab)), "A set piece keeps off the arranged floor / " + piece + " / " + renderer.name + " / " + b);
                }
            }
        }

        // ---------------------------------------------------------------- pipes

        IEnumerator RoomsPipes(ShopWorld w)
        {
            yield return null; yield return null;
            var piped = w.DioramaPipedCultures.ToList();
            foreach (var kind in new[] { CultureKind.Bloom, CultureKind.Flow, CultureKind.Spark, CultureKind.Mist })
            {
                Check(piped.Contains(kind), "A culture pipe runs to the incubator / " + kind);
                var path = w.DioramaPipePath(kind);
                if (path.Length < 2) continue;
                roomsReceipt.pipes.Add(kind + ": " + string.Join(" > ", path.Select(p => "(" + p.x.ToString("0.00") + ", " + p.y.ToString("0.00") + ", " + p.z.ToString("0.00") + ")")));
                Check(path.All(p => p.y < 3.9f), "A pipe runs under the ceiling lights / " + kind);
            }
            Check(shop.World.TryGetLabFacility(ShopLayout.IncubatorFacilityId, out var incubator), "Find the incubator");
            yield return RoomsShot(w, "rooms-pipes-night", new Vector3(-1.5f, .2f, .9f), new Vector3(-1f, 3f, 3.9f));
            // A dose pulses down its pipe.
            int pulses = w.DioramaPipePulses;
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            yield return ResearchHarvest(CultureKind.Bloom);
            yield return ResearchFacilityPanel(ShopLayout.IncubatorFacilityId, ShopPanel.Incubator);
            Check(shop.DoseCulture(CultureKind.Bloom), "Dose Bloom at the incubator with the game's own dose");
            shop.SetPanel(ShopPanel.None);
            Check(w.DioramaPipePulses == pulses + 1 && w.DioramaPipePulse(CultureKind.Bloom) > .5f, "A dose pulses down its culture's pipe");
            Ok(shop.Perform(shop.Model.ReturnDose(CultureKind.Bloom)), "Return the Bloom dose to the siphon");
            yield return new WaitForSeconds(.35f); // the panel fades out
            yield return RoomsShot(w, "rooms-pipes-pulse", new Vector3(-1.5f, .2f, .9f), new Vector3(-1f, 3f, 3.9f));
            // Moving the incubator moves the pipes' ends with it.
            var before = w.DioramaPipePath(CultureKind.Bloom);
            Check(AdvancedFindPosition(ShopLayout.IncubatorFacilityId, true, 0, 4, out int x, out int z), "Find a legal place to move the incubator");
            var f = shop.Layout.GetFacility(ShopLayout.IncubatorFacilityId).Value;
            var center = new Vector3(f.WorldX + (x - f.X) * ShopLayout.GridSize, .2f, f.WorldZ + (z - f.Z) * ShopLayout.GridSize);
            yield return LabMove(ShopLayout.IncubatorFacilityId, x, z, 0, center + Vector3.back * 1.9f);
            shop.SetPanel(ShopPanel.None);
            for (int i = 0; i < 20; i++) yield return null;
            var after = w.DioramaPipePath(CultureKind.Bloom);
            Check(after.Length >= 2 && before.Length >= 2 && Vector3.Distance(after[after.Length - 1], before[before.Length - 1]) > .5f,
                "The pipes follow the moved incubator");
            yield return RoomsShot(w, "rooms-pipes-moved", new Vector3(-1.5f, .2f, .9f), new Vector3(-1f, 3f, 5f));
        }

        // ---------------------------------------------------------------- toggle

        IEnumerator RoomsToggle(ShopWorld w)
        {
            shop.SetDioramaLook(false); yield return null;
            RoomsConsistent(w, "off with every wing");
            Check(w.DioramaSignsSimplified == 0, "Every sign shows its tagline again with the look off");
            foreach (var piece in (ShopWorld.DioramaSetPiece[])Enum.GetValues(typeof(ShopWorld.DioramaSetPiece)))
                Check(!w.DioramaSetPieceShown(piece), "No set piece shows with the look off / " + piece);
            Check(!w.DioramaPipedCultures.Any() || !GameObject.Find("Diorama culture pipes (AD-D)"), "No culture pipe shows with the look off");
            yield return RoomsShot(w, "rooms-store-wide-classic", new Vector3(3.2f, .2f, -.6f), new Vector3(-8f, 1.8f, -3.4f));
            shop.SetDioramaLook(true); yield return null; yield return null;
            RoomsConsistent(w, "on again");
            roomsReceipt.convertedAfterToggle = w.DioramaConvertedCount;
        }
    }
}
#endif
