#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        // Measure peak drift, not only the final position: a stopped agent must not
        // coast while braking. The retained path is exercised by the rest of this route.
        IEnumerator CheckPausedCustomerMotion(string label, float seconds)
        {
            var actors = new System.Collections.Generic.List<CustomerActor>();
            var positions = new System.Collections.Generic.List<Vector3>();
            foreach (var visit in shop.Customers.Visits)
            {
                var actor = shop.Customers.ActorOf(visit);
                if (!actor) continue;
                actors.Add(actor); positions.Add(actor.transform.position);
            }
            Check(shop.Paused && actors.Count > 0, label + " has paused navigation to inspect");
            float peakDrift = 0, until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                for (int i = 0; i < actors.Count; i++)
                    peakDrift = Mathf.Max(peakDrift, Vector3.Distance(positions[i], actors[i].transform.position));
                ObserveCustomers(); yield return null;
            }
            for (int i = 0; i < actors.Count; i++)
                peakDrift = Mathf.Max(peakDrift, Vector3.Distance(positions[i], actors[i].transform.position));
            File.AppendAllText(Path.Combine(evidence, "pause-motion-evidence.txt"),
                label + ": actors=" + actors.Count + " seconds=" + seconds + " peakPositionDrift=" + peakDrift.ToString("F6") + Environment.NewLine);
            Check(peakDrift < .001f, label + " freezes every customer immediately (peak drift <1mm)");
        }

        IEnumerator SlapScenario()
        {
            limitations.Add("Slap probe uses isolated saves, accelerated recipe stock, scripted customer types, camera/player positioning and one temporary LOS blocker. Actual Input System key edges, active-time cooldown, navigation, state recovery and renderer execute. Sound request/gain/clip checks do not prove audible quality. No R03.5 three-shift balance claim.");
            Check(shop.ValidationMode && ShopSession.Arg("-evolutionSlapSmoke") != null &&
                Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "Slap scenario uses only its isolated evidence save");
            yield return new WaitForSeconds(1);
            var customers = shop.Customers;
            var sound = shop.GetComponent<ShopAudio>();
            Check(customers.Ready && customers.MaxVisitors == 2 && shop.SlapKey == "F", "Normal two-customer setup and separate default F binding");
            previousInputSettings = InputSystem.settings;
            smokeInputSettings = Instantiate(previousInputSettings);
            smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings;
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionSlapKeyboard");
            syntheticKeyboard.MakeCurrent(); shop.AllowValidationInput = true;
            SetReputationFixture(5);
            SeedStock("pondlet", "tank-a"); SeedStock("bubblebelly", "tank-b");
            yield return EnterCounter(); Check(shop.OpenForDay(), "Open slap validation day");
            var first = Spawn(CustomerArchetypes.Casual);
            yield return Hold(1);
            var second = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => customers.ActorOf(first).GetComponent<UnityEngine.AI.NavMeshAgent>().velocity.sqrMagnitude > .64f,
                10, "a moving customer before the pause regression");
            shop.SetPanel(ShopPanel.Pause);
            yield return CheckPausedCustomerMotion("Two arriving customers", .4f);
            shop.SetPanel(ShopPanel.None);
            yield return WaitFor(() => first.Phase == CustomerPhase.Queued && second.Phase == CustomerPhase.Queued, 35, "two orders before slap");
            string retainedId = first.SpecimenId, firstTank = first.TargetTankId;
            int originalCash = shop.Model.Currency;
            var actor = customers.ActorOf(first);
            yield return AimForSlap(first, 3);
            Check(customers.FindSlapTarget(shop.Player.View) == null && !shop.TrySlapFocusedCustomer(), "Out-of-range customer cannot be slapped");
            yield return AimForSlap(first);
            shop.Player.Teleport(shop.Player.transform.position, Quaternion.LookRotation(Vector3.forward));
            Check(customers.FindSlapTarget(shop.Player.View) == null && !shop.TrySlapFocusedCustomer(), "Looking away prevents a slap");
            yield return AimForSlap(first);
            Vector3 target = actor.transform.position + Vector3.up * 1.45f;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Slap smoke temporary line-of-sight blocker";
            blocker.transform.position = Vector3.Lerp(shop.Player.View.transform.position, target, .5f);
            blocker.transform.localScale = new Vector3(.3f, 2, 1);
            Physics.SyncTransforms();
            Check(customers.FindSlapTarget(shop.Player.View) == null && !shop.TrySlapFocusedCustomer(), "A physical obstruction blocks the targeted slap ray");
            Destroy(blocker); yield return null; Physics.SyncTransforms();
            Check(customers.FindSlapTarget(shop.Player.View) == first, "A clear close aim selects only the intended customer");
            yield return MoveFor(Key.E, .05f);
            Check(first.Outcome == CustomerOutcome.None && shop.Reports.Current.slapped == 0, "Normal E interaction never becomes a slap");
            // Both opening and closing a menu must consume simultaneous action-key edges.
            yield return SlapKeys(Key.Tab, Key.F);
            Check(shop.Panel == ShopPanel.Journal && first.Outcome == CustomerOutcome.None, "Opening the journal with a simultaneous slap key never strikes");
            yield return SlapKeys(Key.Tab, Key.F);
            Check(shop.Panel == ShopPanel.None && first.Outcome == CustomerOutcome.None, "Closing the journal with a simultaneous slap key never strikes");
            shop.SetPanel(ShopPanel.Journal);
            yield return MoveFor(Key.F, .05f);
            Check(!shop.TrySlapFocusedCustomer() && shop.Reports.Current.slapped == 0, "Journal and other panels block the slap action");
            shop.SetPanel(ShopPanel.Pause); shop.BeginSlapRebind();
            yield return MoveFor(Key.E, .05f);
            Check(shop.WaitingForSlapKey && shop.SlapKey == "F" && shop.Paused, "Reserved E cannot replace service and keeps remap safely paused");
            yield return MoveFor(Key.R, .05f);
            Check(shop.SlapKey == "R" && !shop.WaitingForSlapKey && shop.Reports.Current.slapped == 0, "Real R key rebind is saved without also striking");
            shop.HumorVolume = .4f; shop.SaveNow();
            yield return Capture("70-slap-settings");
            yield return AimForSlap(first);
            yield return MoveFor(Key.F, .05f);
            Check(first.Outcome == CustomerOutcome.None, "Old F binding no longer triggers the gag");
            yield return Capture("71-slap-target-prompt");

            // Hold the actual new key through cooldown: only the original press may activate it.
            syntheticKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.R));
            yield return null; yield return null;
            Check(first.Outcome == CustomerOutcome.Slapped && first.Phase == CustomerPhase.Leaving &&
                shop.Model.Currency == originalCash && shop.Model.GetTank(firstTank).Value.Id == retainedId && !shop.Model.GetReservation(firstTank).HasValue,
                "Actual remapped key cancels only the unpaid order, preserves exact creature and grants no money");
            Check(shop.Reputation.Points == 4 && shop.Reports.Current.slapped == 1 && shop.Reports.IsPending(second.Id) &&
                shop.Model.GetReservation(second.TargetTankId).Value.CustomerId == second.Id,
                "One reputation point and one slap report; the other order keeps its reservation");
            yield return WaitFor(() => sound.SlapPlaybackCount == 1, .4f, "accepted slap reaches actual contact sound");
            Check(actor.IsSlapReacting && actor.StatusLabel == "HEY! MY DIGNITY!" && shop.SlapGesture.IsPlaying &&
                sound.SlapPlaybackCount == 1 && Mathf.Abs(sound.LastSlapGain - .4f) < .001f, "One glove gesture, customer reaction, caption and humor-gain sound request");
            if (!Application.isBatchMode) { ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "72-slap-impact-screen.png")); yield return null; }
            shop.SetPanel(ShopPanel.Pause);
            float cooldown = shop.SlapCooldownRemaining, reaction = actor.SlapReactionRemaining, gesture = shop.SlapGesture.Remaining;
            Vector3 pausedPosition = actor.transform.position;
            yield return CheckPausedCustomerMotion("Slap departure", .4f);
            Check(Mathf.Abs(shop.SlapCooldownRemaining - cooldown) < .001f && Mathf.Abs(actor.SlapReactionRemaining - reaction) < .001f &&
                Mathf.Abs(shop.SlapGesture.Remaining - gesture) < .001f && Vector3.Distance(pausedPosition, actor.transform.position) < .01f,
                "Pause freezes cooldown, glove, reaction and customer navigation together");
            Check(!shop.TrySlapFocusedCustomer(), "Pause rejects the command without another penalty");
            yield return AimForSlap(second);
            Check(!shop.TrySlapFocusedCustomer(), "Global cooldown blocks an immediate slap on the other customer");
            yield return MoveFor(Key.R, 3.2f); // R was never released before this helper; a held key has no new edge.
            Check(shop.Reports.Current.slapped == 1 && second.Outcome == CustomerOutcome.None && shop.SlapCooldownRemaining == 0 &&
                sound.SlapPlaybackCount == 1, "Holding the slap key past cooldown does not repeat or strike the next customer");
            yield return ServeNext("Remaining customer after the slap");
            int paidCash = shop.Model.Currency, paidRep = shop.Reputation.Points;
            yield return AimForSlap(second);
            yield return MoveFor(Key.R, .05f);
            Check(second.Outcome == CustomerOutcome.Sold && shop.Model.Currency == paidCash && shop.Reputation.Points == paidRep &&
                shop.Reports.Current.slapped == 1 && !shop.TrySlapFocusedCustomer(), "Paid visitor cannot be slapped, refunded or penalized");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "both original visitors leaving");
            var rescue = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => rescue.Phase == CustomerPhase.Queued, 30, "released creature sold to a later visitor");
            Check(rescue.SpecimenId == retainedId, "The released exact creature can be reserved again");
            yield return ServeNext("Resale after safe cancellation");
            yield return WaitFor(() => customers.ActiveCount == 0, 15, "resale customer leaving");
            Check(shop.Reputation.Points == 8 && shop.Reports.Current.sales == 2 && shop.Model.Currency == 16,
                "Recovery sales pay once each, with no money or reputation farm from the slap");

            SeedStock("pondlet", "tank-a");
            shop.SetPanel(ShopPanel.Pause); shop.HumorVolume = 0; shop.ReducedGags = true; shop.SaveNow(); shop.SetPanel(ShopPanel.None);
            var reduced = Spawn(CustomerArchetypes.Premium);
            yield return AimForSlap(reduced);
            yield return MoveFor(Key.R, .04f);
            Check(reduced.Outcome == CustomerOutcome.Slapped && shop.Reputation.Points == 7, "Reduced slap outcome commits immediately before timed sound");
            yield return WaitFor(() => sound.SlapPlaybackCount == 2, .25f, "reduced slap reaches actual contact sound");
            Check(reduced.Outcome == CustomerOutcome.Slapped && sound.SlapPlaybackCount == 2 && sound.LastSlapGain == 0 &&
                customers.ActorOf(reduced) != null && customers.ActorOf(reduced).IsSlapReacting &&
                customers.ActorOf(reduced).SlapReactionRemaining < .33f && shop.Reputation.Points == 7,
                "An entrance slap survives long enough for its shorter muted reaction, with unchanged transaction rules");
            float beforeReload = shop.SlapCooldownRemaining;
            Check(shop.SaveNow() && shop.ReloadSaved() && customers.ActiveCount == 0 && shop.Reputation.Points == 7 && shop.Reports.Current.slapped == 2 &&
                shop.SlapCooldownRemaining <= beforeReload && shop.SlapCooldownRemaining > 0 && !shop.SlapGesture.IsPlaying &&
                shop.SlapKey == "R" && shop.HumorVolume == 0 && shop.ReducedGags,
                "Reload cancels transient visuals, retains the active cooldown and saved result/settings without replay");
            var closing = Spawn(CustomerArchetypes.Casual);
            yield return WaitFor(() => closing.Phase == CustomerPhase.Queued, 30, "closing-time reserved customer");
            yield return EnterCounter(); Check(shop.CloseForNight(), "Begin closing with one unpaid order");
            yield return AimForSlap(closing);
            yield return MoveFor(Key.R, .05f);
            Check(closing.Outcome == CustomerOutcome.Slapped && shop.Reports.Current.slapped == 3 && shop.Reputation.Points == 6,
                "A queued closing-time slap cancels safely before night, exactly once");
            yield return null;
            Check(shop.Comedy.Active && shop.Comedy.Kind==ComedyKind.Slap,"Closing cleanup preserves newly accepted slap feedback across frames");
            yield return WaitFor(() => shop.Cycle.Phase == ShopDayPhase.Night && customers.ActiveCount == 0, 10, "night interrupting any remaining reaction");
            var report = shop.Reports.Last;
            Check(report.visitors == 5 && report.sales == 2 && report.slapped == 3 && report.revenue == 16 && report.closed == 0 &&
                report.stockedAtClose == 1 && shop.Model.Currency == 16 && shop.Reputation.Points == 6,
                "Daily results reconcile two paid sales and three slapped departures without double-counting closing");
            yield return Capture("73-slap-day-report");
            Check(shop.SaveNow() && shop.ReloadSaved() && shop.Reports.Last.slapped == 3 && shop.Reputation.Points == 6 && !shop.TrySlapFocusedCustomer(),
                "Night/reopened report/load cannot repeat the penalty or slap anybody");

            shop.ConfirmNewGame();
            Check(shop.Reports.Last == null && shop.Reputation.Points == 0 && shop.SlapKey == "R" && shop.ReducedGags && shop.HumorVolume == 0,
                "New game clears progression/reports and preserves the player's key/accessibility settings");
            SetReputationFixture(30); // Explicit isolated legacy boundary fixture, with empty report history.
            var legacyTrades = shop.Reports.CaptureSave(); legacyTrades.version = 3;
            string legacy = "{\"formatVersion\":6,\"game\":" + LegacySmokeGameV2Json() + ",\"cycle\":" +
                JsonUtility.ToJson(shop.Cycle.CaptureSave()) + ",\"trades\":" + JsonUtility.ToJson(legacyTrades) +
                ",\"reputation\":" + JsonUtility.ToJson(shop.Reputation.CaptureSave()) + ",\"sensitivity\":1,\"volume\":0.65}";
            File.WriteAllText(shop.SavePath, legacy);
            Check(shop.ReloadSaved() && shop.Reputation.Points == 30 && shop.SlapKey == "F" && shop.HumorVolume == 1 && !shop.ReducedGags &&
                File.ReadAllText(shop.SavePath) == legacy, "Format-six migration preserves earned reputation and supplies safe new controls without writing");
            Check(shop.SaveNow() && File.ReadAllText(shop.SavePath + ".v6.bak") == legacy &&
                JsonUtility.FromJson<SlotData>(File.ReadAllText(shop.SavePath)).formatVersion == SaveStore.CurrentFormatVersion,
                "First new save preserves exact format-six bytes and writes the current envelope");
        }

        IEnumerator SlapKeys(params Key[] keys)
        {
            syntheticKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(keys));
            yield return Hold(.05f);
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null; yield return null;
        }

        IEnumerator AimForSlap(CustomerVisit visit, float distance = 1.35f)
        {
            shop.SetPanel(ShopPanel.None);
            var actor = shop.Customers.ActorOf(visit);
            if (!actor) throw new InvalidOperationException("Slap target actor missing");
            Vector3 position = actor.transform.position + Vector3.right * distance; position.y = .2f;
            Vector3 direction = actor.transform.position - position; direction.y = 0;
            shop.Player.Teleport(position, Quaternion.LookRotation(direction));
            shop.Player.View.transform.LookAt(actor.transform.position + Vector3.up * 1.45f);
            yield return null; yield return null;
        }
    }
}
#endif

