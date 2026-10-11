#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        string FxAuthority() => JsonUtility.ToJson(shop.Model.CaptureSave()) + JsonUtility.ToJson(shop.Cycle.CaptureSave()) +
            JsonUtility.ToJson(shop.Staff.CaptureSave()) + JsonUtility.ToJson(shop.Stocking.CaptureSave(shop.Model)) + JsonUtility.ToJson(shop.Laboratory.CaptureSave(shop.Model)) +
            JsonUtility.ToJson(shop.Automation.CaptureSave()) + JsonUtility.ToJson(shop.Reports.CaptureSave());
        IEnumerator FxCueScenario()
        {
            limitations.Add("FX-02 uses real guarded session harvest/dose commands with diagnostic camera placement, explicit policy/focus switches and a temporary isolated save obstruction. This is automated API/lifecycle evidence, not native held-input or heard audio. Accelerated specimen/core-sale and dummy visual source fixtures isolate identity/removal behavior, not progression or an ordinary paid customer. Preview Low/reduced and FX disabled switches never write user settings. Four pre-existing players remain untouched. Existing intake is retained until the lab hero assignment; FX-02 adds only three small borrowed-mesh pulse bindings.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "FX cue smoke uses isolated slot");
            yield return new WaitForSeconds(1);
            Check(shop.SkipTutorial(), "Explicit isolated skip exits tutorial Welcome");
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false; shop.AllowValidationInput = false;
            if (!Application.isEditor)
            {
                int width = int.TryParse(ShopSession.Arg("-screen-width"), out int w) ? w : 1280;
                int height = int.TryParse(ShopSession.Arg("-screen-height"), out int h) ? h : 720;
                Screen.SetResolution(width == 1280 ? 1920 : 1280, height == 720 ? 1080 : 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.3f);
                Screen.SetResolution(width, height, FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
                Check(Screen.width == width && Screen.height == height, "Actual Windows render surface matches requested size");
            }
            else limitations.Add("Editor captures are diagnostic; actual rendered/HUD evidence is collected in separate Windows runs.");
            var world = shop.World; var cues = world.FxCues; var pool = world.FxPool;
            Check(cues != null && pool != null && pool.OwnedCount == 96 && pool.ActiveCount == 0, "Prewarmed fixed pool has96 hidden reusable motes");
            Check(pool.Root.GetComponentsInChildren<Collider>(true).Length == 0 && pool.Root.GetComponentsInChildren<AudioSource>(true).Length == 0, "Pooled effects have no collider or audio source");
            Check(shop.SaveNow(), "Save original isolated comparison state");
            byte[] initial = File.ReadAllBytes(shop.SavePath);
            string enabled = FxManualTrial(true);
            File.WriteAllBytes(shop.SavePath, initial); Check(shop.ReloadSaved(), "Restore exact comparison fixture"); shop.SetPanel(ShopPanel.None);
            string disabled = FxManualTrial(false);
            Check(enabled == disabled, "Enabled/disabled presentation has identical model, cash, cycle, stock, jobs and ledger outcome");
            world.SetFxEnabledForValidation(true); shop.SetPanel(ShopPanel.None);

            var grower = StationOf(StationKind.Flow).transform;
            GateAim(grower.TransformPoint(new Vector3(0, .2f, -1.7f)), grower.TransformPoint(new Vector3(0, 1.45f, 0)));
            int accepted = cues.AcceptedCount, beforeUnits = shop.Model.Available(CultureKind.Flow);
            shop.Harvest(CultureKind.Flow, shop.Model.CollectionSecondsPerUnit + .01f);
            uint sequence = shop.FxActionSequenceForValidation;
            Check(shop.Model.Available(CultureKind.Flow) == beforeUnits + 1 && cues.AcceptedCount == accepted + 1 && pool.ActiveCount == 1, "Successful real session harvest commits one unit and one new pulse");
            string unchanged = FxAuthority(); int emitted = pool.SpawnedCount;
            Check(!world.TryFxCue(FxCueId.CollectedUnit, sequence) && pool.SpawnedCount == emitted && FxAuthority() == unchanged, "Duplicate callback cannot replay or change game authority");
            yield return GateCapture("fx02-accepted-unit");
            yield return new WaitForSeconds(.12f);
            Check(pool.ActiveCount == 0, "Accepted pulse returns to fixed pool after lifetime");

            var incubator = StationOf(StationKind.Incubator).transform;
            GateAim(incubator.TransformPoint(new Vector3(0, .2f, -1.5f)), incubator.TransformPoint(new Vector3(0, 1.48f, 0))); Physics.SyncTransforms();
            shop.SetPanel(ShopPanel.Incubator);
            accepted = cues.AcceptedCount;
            Check(shop.DoseCulture(CultureKind.Flow) && cues.AcceptedCount == accepted + 1 && pool.ActiveCount == 1, "Reach-guarded menu dose gives small vessel cue after accepted delta");
            var activeMotes = pool.Root.GetComponentsInChildren<MeshRenderer>();
            Check(activeMotes.Length == 1 && activeMotes[0].sharedMaterial.name == "Flow turquoise", "Flow dose retains its accepted culture palette");
            yield return GateCapture("fx02-accepted-dose");
            yield return new WaitForSeconds(.12f);
            unchanged = FxAuthority(); accepted = cues.AcceptedCount;
            bool rejectedDose = !shop.DoseCulture(CultureKind.Flow);
            Check(rejectedDose, "Explicit empty culture dose is rejected");
            Check(cues.AcceptedCount == accepted + 1 && pool.ActiveCount == 1,
                "Rejected dose emits refusal / accepted=" + accepted + "->" + cues.AcceptedCount + " active=" + pool.ActiveCount + " policy=" + world.FxPolicy);
            Check(FxAuthority() == unchanged, "Rejected dose preserves exact resources and jobs");
            yield return GateCapture("fx02-refused-dose");
            accepted = cues.AcceptedCount; unchanged = FxAuthority();
            Check(!shop.Perform(shop.Model.Dose(CultureKind.Flow)).Success && cues.AcceptedCount == accepted && FxAuthority() == unchanged,
                "Generic action feedback has no unsolicited player-hand cue");
            Check(!world.TryFxCue(FxCueId.DoseCommitted, ++sequence + 10, FxCueOrigin.Worker) &&
                !world.TryFxCue(FxCueId.DoseCommitted, ++sequence + 10, FxCueOrigin.Module) && cues.AcceptedCount == accepted && FxAuthority() == unchanged,
                "Worker/module origins cannot operate the manual tool binding");
            shop.SetPanel(ShopPanel.None);
            unchanged = FxAuthority();
            foreach (FxViewPolicy policy in new[] { FxViewPolicy.Paused, FxViewPolicy.Unfocused, FxViewPolicy.Arrange })
            {
                world.TryFxCue(FxCueId.CollectedUnit, ++sequence + 100);
                world.SetFxViewPolicy(policy);
                Check(pool.ActiveCount == 0 && cues.ActiveCount == 0 && !world.TryFxCue(FxCueId.CollectedUnit, ++sequence + 100), "Policy cancels and rejects late transient / " + policy);
                world.SetFxViewPolicy(FxViewPolicy.Visible);
            }
            // The diagnostic high sequence above is local to the probe; a new epoch restores normal session sequencing.
            world.ResetFxPresentation();
            shop.SetFxFocusForValidation(false); Check(world.FxPolicy == FxViewPolicy.Unfocused && pool.ActiveCount == 0, "Session focus-loss hook cancels immediately");
            shop.SetFxFocusForValidation(true);
            shop.SetPanel(ShopPanel.Pause); Check(pool.ActiveCount == 0 && cues.ActiveCount == 0, "Actual pause panel owns cancellation");
            shop.SetPanel(ShopPanel.None); shop.SetPanel(ShopPanel.Arrange); Check(world.FxPolicy == FxViewPolicy.Arrange, "Arrangement panel owns cancellation"); shop.SetPanel(ShopPanel.None);
            Check(FxAuthority() == unchanged, "Policy/focus/panel operations do not mutate game authority");

            var proof = new GameObject("FX-02 isolated moving visual owner"); proof.transform.SetParent(world.transform, false);
            var token = world.BindFxOwner("fx02-proof", "specimen-a", proof.transform);
            var accent = world.ToolChamber.GetComponentInChildren<Renderer>().sharedMaterial;
            unchanged = FxAuthority();
            Check(pool.Emit(cues, token, proof.transform, Vector3.zero, accent, .28f, .04f), "Owned source proof emits with borrowed material");
            proof.transform.localPosition += Vector3.right;
            world.Animate(1, .01f);
            Check(pool.ActiveCount == 0 && !cues.IsValid(token), "Moving source invalidates generation and removes old effect");
            token = world.BindFxOwner("fx02-proof", "specimen-b", proof.transform);
            pool.Emit(cues, token, proof.transform, Vector3.zero, accent, .28f, .04f); proof.SetActive(false); world.Animate(1, .01f);
            Check(pool.ActiveCount == 0 && !cues.IsValid(token), "Removed source clears effects and rejects old owner");
            Destroy(proof);
            Check(!pool.Emit(cues, world.FxCues.Find("tool"), null, Vector3.zero, accent, .28f, .04f) && FxAuthority() == unchanged,
                "Missing attachment fails safely without altering authority");
            world.ResetFxPresentation();
            int topology = pool.Root.childCount;
            for (uint i = 1; i <= 100; i++) { world.TryFxCue(FxCueId.CollectedUnit, i); world.Animate(1, .3f); }
            Check(pool.Root.childCount == topology && pool.OwnedCount == 96 && pool.ActiveCount == 0 && FxAuthority() == unchanged,
                "100 pulse reuse cycles retain bounded objects and exact authority");
            world.ResetFxPresentation(); world.SetFxPreviewModes(true, true);
            Check(world.TryFxCue(FxCueId.CollectedUnit, 1) && pool.ActiveCount == 1, "Low/reduced preview retains accepted-unit feedback");
            yield return GateCapture("fx02-low-reduced");
            world.SetFxPreviewModes(false, false); world.ResetFxPresentation();
            int units = shop.Model.Available(CultureKind.Bloom); accepted = cues.AcceptedCount;
            byte[] saved = File.ReadAllBytes(shop.SavePath);
            TutorialBlockSave();
            try
            {
                shop.Harvest(CultureKind.Bloom, shop.Model.CollectionSecondsPerUnit + .01f);
                Check(shop.SaveError != null && shop.Model.Available(CultureKind.Bloom) == units + 1 && cues.AcceptedCount == accepted && pool.ActiveCount == 0 &&
                    Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)) == Convert.ToBase64String(saved),
                    "Failed write preserves durable bytes and existing manual in-memory outcome, suppressing new success FX");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.ReloadSaved(), "Restore durable state after failed-save fixture"); shop.SetPanel(ShopPanel.None);

            // Labelled accelerated identity fixture; no ordinary playtest/paid-customer claim.
            Ok(shop.Model.ClearDoses(), "Clear fixture doses before specimen owner check");
            SpecimenProduce("pondlet"); Ok(shop.Model.TransferToTank("tank-a"), "Fixture places first Pondlet"); world.Refresh(shop.Model);
            string firstId = shop.Model.GetTank("tank-a").Value.Id; var firstOwner = cues.Find("tank-a");
            Ok(shop.Model.SellFromTank("tank-a"), "Core sale empties identity fixture only");
            SpecimenProduce("pondlet"); Ok(shop.Model.TransferToTank("tank-a"), "Fixture replaces with same species"); world.Refresh(shop.Model);
            Check(shop.Model.GetTank("tank-a").Value.Id != firstId && !cues.IsValid(firstOwner) && !cues.Find("tank-a").Equals(firstOwner),
                "Same-species replacement changes presentation identity and rejects previous specimen token");
            SpecimenProduce("pondlet"); Check(shop.SaveNow(), "Save loaded-Ready fixture");
            accepted = cues.AcceptedCount; var previous = cues.Find("incubator");
            Check(shop.ReloadSaved() && shop.Model.Incubator.Phase == IncubatorPhase.Ready && !cues.IsValid(previous) && cues.AcceptedCount == accepted && pool.ActiveCount == 0,
                "Reload Ready reconstructs steady state without hatch/discovery replay");
            unchanged = FxAuthority();
            world.ResetFxPresentation(); world.TryFxCue(FxCueId.HandRefused, 1); shop.enabled = false;
            Check(pool.ActiveCount == 0 && cues.ActiveCount == 0 && !world.TryFxCue(FxCueId.HandRefused, 2) && FxAuthority() == unchanged, "Session disable cancels and rejects replay without authority change");
            shop.enabled = true; yield return null;
            var epochOwner = cues.Find("tool"); shop.ConfirmNewGame();
            Check(!cues.IsValid(epochOwner) && pool.ActiveCount == 0 && shop.Model.Currency == 0 && shop.Model.Incubator.Phase == IncubatorPhase.Dosing, "Explicit New Game retires old epoch without saved transient state");
            Check(shop.SkipTutorial() && shop.SaveNow(), "Final isolated slot is usable; normal save untouched");
        }
        string FxManualTrial(bool enabled)
        {
            shop.World.SetFxEnabledForValidation(enabled); shop.SetPanel(ShopPanel.None);
            shop.Harvest(CultureKind.Bloom, shop.Model.CollectionSecondsPerUnit + .01f);
            var station = StationOf(StationKind.Incubator).transform;
            GateAim(station.TransformPoint(new Vector3(0, .2f, -1.5f)), station.TransformPoint(new Vector3(0, 1.48f, 0))); Physics.SyncTransforms();
            shop.SetPanel(ShopPanel.Incubator);
            Check(shop.DoseCulture(CultureKind.Bloom) && !shop.DoseCulture(CultureKind.Flow), "Same guarded accepted/rejected commands / FX enabled=" + enabled);
            shop.SetPanel(ShopPanel.None); return FxAuthority();
        }
    }
}
#endif
