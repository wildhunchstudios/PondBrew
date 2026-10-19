#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class GrowerFrame
        {
            public float seconds, refillRemaining, fill, sourceError, mouthError, statusWidth, plateWidth;
            public int units, cells, carried, staged, capacity, circulation, refillEdges, accepted, blockedEntries, particles, ambient, total;
            public string stock, interaction, statusText;
            public Vector3 root, outlet, mouth;
            public Quaternion rotation;
            public int currentMouseDevice, currentKeyboardDevice, fixtureMouseDevice;
            public bool currentMouseHeld, fixtureMouseHeld;
        }
        [Serializable] sealed class GrowerInputDeviceReceipt
        {
            public float seconds;
            public int deviceId;
            public string change, name, layout, interfaceName;
            public bool native, enabled, fixture;
        }
        [Serializable] sealed class GrowerInputStateReceipt
        {
            public float seconds;
            public int frame, mouseDevice, keyboardDevice;
            public bool mouseHeld, fixtureMouseHeld;
        }
        [Serializable] sealed class GrowerInputSuppressionReceipt
        {
            public int deviceId, count;
            public string eventType;
        }
        [Serializable] sealed class GrowerInputReceipt
        {
            public string scope = "Diagnostic player's Unity InputSystem frontend only; no OS device disabling; no raw keyboard contents recorded";
            public int processId, fixtureMouseDevice, fixtureKeyboardDevice, updateSamples, foreignCurrentSamples, otherEnabledSamples, droppedTransitions;
            public bool restored;
            public int suppressedStateEvents, suppressedDeltaEvents, suppressedPreviouslyHandledEvents;
            public GrowerInputSuppressionReceipt[] suppressedEvents;
            public GrowerInputDeviceReceipt[] devices;
            public GrowerInputStateReceipt[] transitions;
        }
        readonly List<InputDevice> growerDisabledInputs = new List<InputDevice>();
        readonly List<GrowerInputDeviceReceipt> growerInputDevices = new List<GrowerInputDeviceReceipt>();
        readonly List<GrowerInputStateReceipt> growerInputTransitions = new List<GrowerInputStateReceipt>();
        readonly List<GrowerInputSuppressionReceipt> growerSuppressedInputs = new List<GrowerInputSuppressionReceipt>();
        GrowerInputReceipt growerInputReceipt;
        string growerInputReceiptFile;
        bool growerIsolatingInputs, growerChangingInput;
        void GrowerInputDeviceRecord(InputDevice device, string change)
        {
            if (growerInputDevices.Count >= 4096) { growerInputReceipt.droppedTransitions++; return; }
            growerInputDevices.Add(new GrowerInputDeviceReceipt { seconds = Time.realtimeSinceStartup - start,
                deviceId = device.deviceId, change = change, name = device.name, layout = device.layout,
                interfaceName = device.description.interfaceName, native = device.native, enabled = device.enabled,
                fixture = device == cycleMouse || device == syntheticKeyboard });
        }
        void GrowerDisableForeignInput(InputDevice device)
        {
            if (!(device is Mouse) && !(device is Keyboard) || device == cycleMouse || device == syntheticKeyboard || !device.added || !device.enabled) return;
            if (!growerDisabledInputs.Contains(device)) growerDisabledInputs.Add(device);
            growerChangingInput = true;
            try {
                // Frontend-only: do not send a runtime/OS DisableDeviceCommand.
                InputSystem.DisableDevice(device, keepSendingEvents: true);
                GrowerInputDeviceRecord(device, "disabled-in-player-frontend");
            }
            finally { growerChangingInput = false; }
        }
        void GrowerInputDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (!growerIsolatingInputs || growerChangingInput || !(device is Mouse) && !(device is Keyboard)) return;
            GrowerInputDeviceRecord(device, change.ToString());
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled)
                GrowerDisableForeignInput(device);
        }
        void GrowerInputEvent(UnityEngine.InputSystem.LowLevel.InputEventPtr inputEvent, InputDevice device)
        {
            if (!growerIsolatingInputs || device == null || !(device is Mouse) && !(device is Keyboard) ||
                device == cycleMouse || device == syntheticKeyboard) return;
            bool state = inputEvent.type == StateEvent.Type, delta = inputEvent.type == DeltaStateEvent.Type;
            if (!state && !delta) return; // Device configuration/removal and other event kinds retain their normal lifecycle.
            // In the pinned package, frontend-only disabling still permits state updates and MakeCurrent.
            // onEvent precedes both; its default SuppressStateUpdates policy discards handled state events.
            // Keep strict after-update provenance checks: unexpected policy/current changes must still fail.
            if (inputEvent.handled) growerInputReceipt.suppressedPreviouslyHandledEvents++;
            inputEvent.handled = true;
            if (state) growerInputReceipt.suppressedStateEvents++; else growerInputReceipt.suppressedDeltaEvents++;
            string type = state ? "StateEvent" : "DeltaStateEvent";
            for (int i = 0; i < growerSuppressedInputs.Count; i++) {
                var record = growerSuppressedInputs[i];
                if (record.deviceId == device.deviceId && record.eventType == type) { record.count++; return; }
            }
            if (growerSuppressedInputs.Count >= 256) { growerInputReceipt.droppedTransitions++; return; }
            // Metadata only: no keyboard payload, key values, pointer coordinates or event-state bytes.
            growerSuppressedInputs.Add(new GrowerInputSuppressionReceipt { deviceId = device.deviceId, eventType = type, count = 1 });
        }
        void GrowerInputAfterUpdate()
        {
            if (!growerIsolatingInputs) return;
            growerInputReceipt.updateSamples++;
            if (Mouse.current != cycleMouse || Keyboard.current != syntheticKeyboard) growerInputReceipt.foreignCurrentSamples++;
            foreach (var device in InputSystem.devices)
                if ((device is Mouse || device is Keyboard) && device != cycleMouse && device != syntheticKeyboard && device.enabled)
                    growerInputReceipt.otherEnabledSamples++;
            int mouseId = Mouse.current != null ? Mouse.current.deviceId : -1, keyboardId = Keyboard.current != null ? Keyboard.current.deviceId : -1;
            bool held = Mouse.current != null && Mouse.current.leftButton.isPressed, fixtureHeld = cycleMouse != null && cycleMouse.leftButton.isPressed;
            var previous = growerInputTransitions.Count > 0 ? growerInputTransitions[growerInputTransitions.Count - 1] : null;
            if (previous != null && previous.mouseDevice == mouseId && previous.keyboardDevice == keyboardId && previous.mouseHeld == held && previous.fixtureMouseHeld == fixtureHeld) return;
            if (growerInputTransitions.Count >= 4096) { growerInputReceipt.droppedTransitions++; return; }
            growerInputTransitions.Add(new GrowerInputStateReceipt { seconds = Time.realtimeSinceStartup - start, frame = Time.frameCount,
                mouseDevice = mouseId, keyboardDevice = keyboardId, mouseHeld = held, fixtureMouseHeld = fixtureHeld });
        }
        void GrowerIsolateInputs(string receiptFile = "grower-input-provenance.json")
        {
            growerInputReceiptFile = receiptFile;
            growerInputReceipt = new GrowerInputReceipt { processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                fixtureMouseDevice = cycleMouse.deviceId, fixtureKeyboardDevice = syntheticKeyboard.deviceId };
            growerIsolatingInputs = true;
            InputSystem.onEvent += GrowerInputEvent;
            InputSystem.onDeviceChange += GrowerInputDeviceChanged;
            foreach (var device in InputSystem.devices) {
                if (!(device is Mouse) && !(device is Keyboard)) continue;
                GrowerInputDeviceRecord(device, "initial"); GrowerDisableForeignInput(device);
            }
            syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            InputSystem.onAfterUpdate += GrowerInputAfterUpdate;
            GrowerInputAfterUpdate();
        }
        void RestoreGrowerInputIsolation()
        {
            if (!growerIsolatingInputs) return;
            growerIsolatingInputs = false;
            InputSystem.onEvent -= GrowerInputEvent;
            InputSystem.onAfterUpdate -= GrowerInputAfterUpdate;
            InputSystem.onDeviceChange -= GrowerInputDeviceChanged;
            bool restored = true;
            foreach (var device in growerDisabledInputs) {
                try {
                    if (device.added) { InputSystem.EnableDevice(device); GrowerInputDeviceRecord(device, "restored-enabled"); }
                    else GrowerInputDeviceRecord(device, "removed-before-restore");
                }
                catch (Exception error) { restored = false; errors.Add("Grower input restore: " + error); }
            }
            growerDisabledInputs.Clear(); growerInputReceipt.restored = restored;
            if (growerInputReceipt.foreignCurrentSamples != 0 || growerInputReceipt.otherEnabledSamples != 0 || growerInputReceipt.droppedTransitions != 0)
                errors.Add("Synthetic input isolation provenance failed; inspect " + growerInputReceiptFile);
            growerInputReceipt.suppressedEvents = growerSuppressedInputs.ToArray();
            growerInputReceipt.devices = growerInputDevices.ToArray(); growerInputReceipt.transitions = growerInputTransitions.ToArray();
            File.WriteAllText(Path.Combine(evidence, growerInputReceiptFile), JsonUtility.ToJson(growerInputReceipt, true));
        }
        [Serializable] sealed class GrowerTrace
        {
            public string name, culture, context;
            public int samples, peak, ambientPeak, particleSamples;
            public float maxSourceError, maxMouthError;
            public GrowerFrame[] frames;
        }
        readonly string[] growerInventories = new string[2];
        static string GrowerId(CultureKind culture) => culture == CultureKind.Bloom ? ShopLayout.BloomFacilityId : ShopLayout.FlowFacilityId;
        Transform GrowerRoot(CultureKind culture) => StationOf(culture == CultureKind.Bloom ? StationKind.Bloom : StationKind.Flow).transform;
        int GrowerAccepted(CultureKind culture) => shop.World.GrowerAcceptedCountForValidation(culture);
        string GrowerInventory(CultureKind culture)
        {
            var result = new StringBuilder();
            foreach (var child in GrowerRoot(culture).GetComponentsInChildren<Transform>(true)) result.Append(child.GetInstanceID()).Append(',');
            foreach (var filter in GrowerRoot(culture).GetComponentsInChildren<MeshFilter>(true)) result.Append('m').Append(filter.sharedMesh ? filter.sharedMesh.GetInstanceID() : 0);
            foreach (var renderer in GrowerRoot(culture).GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials) result.Append('r').Append(material ? material.GetInstanceID() : 0);
            foreach (var collider in GrowerRoot(culture).GetComponentsInChildren<Collider>(true)) result.Append('c').Append(collider.GetInstanceID());
            return result.ToString();
        }
        void GrowerResourceCheck(string context)
        {
            var pool = shop.World.FxPool;
            Check(pool.OwnedCount == 96 && pool.OwnedMeshCount == 5 && pool.OwnedMaterialCount == 1 &&
                pool.Root.GetComponentsInChildren<Collider>(true).Length == 0 && shop.GetComponents<AudioSource>().Length == 5,
                "Fixed pool, shared assets, no particle colliders and five bounded voices (3 short +2 loops) / " + context);
            foreach (var culture in new[] { CultureKind.Bloom, CultureKind.Flow })
                Check(GrowerInventory(culture) == growerInventories[(int)culture], "Original grower transforms/meshes/materials/colliders remain identical / " + context + " / " + culture);
        }
        GrowerFrame GrowerSample(CultureKind culture, float seconds)
        {
            if (growerIsolatingInputs && (growerInputReceipt.foreignCurrentSamples != 0 || growerInputReceipt.otherEnabledSamples != 0))
                throw new InvalidOperationException("FX07 isolated input provenance changed; inspect grower-input-provenance.json");
            var world = shop.World; var pool = world.FxPool; var root = GrowerRoot(culture);
            var view = shop.Model.GetGrower(culture); var stock = world.GrowthGrowerForValidation(culture);
            var outlet = root.Find("Outlet opening"); var mouth = world.ToolRoot.Find("Siphon chassis/Collection nozzle/Nozzle light");
            int cells = 0; string prefix = culture.ToString().ToUpperInvariant() + " unit ";
            foreach (Transform child in root) if (child.name.StartsWith(prefix, StringComparison.Ordinal) && child.gameObject.activeSelf) cells++;
            if (cells != view.Units || world.GrowerActiveCellsForValidation(culture) != cells)
                throw new InvalidOperationException("Actual visible culture count differs from stock / " + culture + " / " + cells + "/" + view.Units);
            if (world.GrowerRootForValidation(culture) != root || outlet == null || mouth == null)
                throw new InvalidOperationException("Grower uses a replacement root or lacks its actual outlet/nozzle");
            var glass = root.Find("Clear growing chamber"); var glassBounds = glass.GetComponent<MeshFilter>().sharedMesh.bounds;
            var medium = world.GrowerMediumForValidation(culture);
            var mediumBounds = medium.GetComponent<MeshFilter>().sharedMesh.bounds;
            float top = root.InverseTransformPoint(medium.TransformPoint(new Vector3(0, mediumBounds.max.y, 0))).y;
            var owner = world.GrowerOwnerForValidation(culture);
            int particles = pool.ParticleCountForValidation(owner);
            for (int i = 0; i < particles; i++)
            {
                var visual = pool.ParticleVisualForValidation(owner, i);
                if (!visual) throw new InvalidOperationException("Grower particle has no inspectable rich mesh");
                foreach (var vertex in visual.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    Vector3 point = visual.TransformPoint(vertex), p = glass.InverseTransformPoint(point) - glassBounds.center;
                    float radial = p.x * p.x / (glassBounds.extents.x * glassBounds.extents.x) + p.z * p.z / (glassBounds.extents.z * glassBounds.extents.z);
                    if (float.IsNaN(radial) || radial > 1.005f || Mathf.Abs(p.y) > glassBounds.extents.y + .003f || root.InverseTransformPoint(point).y > top + .006f)
                        throw new InvalidOperationException("Actual particle mesh leaves its current grower glass/medium / " + culture + " radial=" + radial + " y=" + p.y);
                }
            }
            float sourceError = 0, mouthError = 0;
            if (world.CollectionActive && shop.Focused && shop.Focused.transform == root)
            {
                for (int i = 0; i < world.CollectionParticleCountForValidation; i++)
                {
                    if (!pool.TryGetParticleForValidation(world.CollectionOwnerForValidation, i, out var startPoint, out var endPoint, out _, out _, out _))
                        throw new InvalidOperationException("Missing live collection particle");
                    sourceError = Mathf.Max(sourceError, Vector3.Distance(startPoint, outlet.position));
                    mouthError = Mathf.Max(mouthError, Vector3.Distance(endPoint, mouth.position));
                }
            }
            int ambient = 0;
            for (int i = FxEffectPool.ActionReserved; i < FxEffectPool.Capacity; i++) if (pool.Root.GetChild(i).gameObject.activeSelf) ambient++;
            string statusText = null; float statusWidth = 0;
            foreach (var label in root.GetComponentsInChildren<TextMesh>())
                if (label.text.StartsWith(view.Units + " / " + view.Capacity + " | ", StringComparison.Ordinal)) {
                    statusText = label.text;
                    statusWidth = label.GetComponent<Renderer>().localBounds.size.x * Mathf.Abs(label.transform.localScale.x);
                    break;
                }
            var plate = root.Find("Machine faceplate");
            float plateWidth = plate ? plate.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * Mathf.Abs(plate.localScale.x) : 0;
            return new GrowerFrame { seconds = seconds, units = view.Units, cells = cells, refillRemaining = view.SecondsToNextUnit,
                fill = stock.Fill, stock = stock.State.ToString(), interaction = world.GrowerInteractionForValidation(culture).ToString(),
                statusText = statusText, statusWidth = statusWidth, plateWidth = plateWidth,
                carried = shop.Model.Available(culture), staged = shop.Model.Staged(culture), capacity = shop.Model.Capacity,
                circulation = world.GrowerCirculationCountForValidation(culture), refillEdges = world.GrowerRefillCountForValidation(culture),
                accepted = GrowerAccepted(culture), blockedEntries = world.GrowerBlockedTransitionCountForValidation(culture),
                particles = particles, ambient = ambient, total = pool.ActiveCount, root = root.position, rotation = root.rotation,
                outlet = outlet.position, mouth = mouth.position, sourceError = sourceError, mouthError = mouthError,
                currentMouseDevice = Mouse.current != null ? Mouse.current.deviceId : -1,
                currentKeyboardDevice = Keyboard.current != null ? Keyboard.current.deviceId : -1,
                fixtureMouseDevice = cycleMouse.deviceId, currentMouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed,
                fixtureMouseHeld = cycleMouse.leftButton.isPressed };
        }
        IEnumerator GrowerClip(string name, CultureKind culture, float duration, Action<float> action = null, bool low = false, bool reduced = false)
        {
            var frames = new List<GrowerFrame>(); var report = new GrowerTrace { name = name, culture = culture.ToString(), context = "Actual normal-time session; camera/input scripted" };
            Action<float> sample = age => {
                var frame = GrowerSample(culture, age); frames.Add(frame);
                report.peak = Mathf.Max(report.peak, frame.total); report.ambientPeak = Mathf.Max(report.ambientPeak, frame.ambient);
                report.maxSourceError = Mathf.Max(report.maxSourceError, frame.sourceError); report.maxMouthError = Mathf.Max(report.maxMouthError, frame.mouthError);
                if (frame.particles > 0) report.particleSamples++;
                action?.Invoke(age);
            };
            if (Application.isEditor) {
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < duration) { sample(Time.realtimeSinceStartup - start); yield return null; }
            }
            else yield return ReviewClip(name, duration, sample);
            report.samples = frames.Count; report.frames = frames.ToArray();
            File.WriteAllText(Path.Combine(evidence, name + "-motion.json"), JsonUtility.ToJson(report, true));
            Check(report.samples > 2 && report.peak <= (low ? 32 : 96) && report.ambientPeak <= (low ? 8 : 32), "Actual grower frames obey separate total/ambient limits / " + name);
            Check(report.maxSourceError <= .066f && (reduced || report.maxMouthError < .015f), "Intake attaches to current real outlet and nozzle / " + name);
            GrowerResourceCheck(name);
        }
        IEnumerator GrowerWaitFull(CultureKind culture)
        {
            float deadline = Time.realtimeSinceStartup + shop.Config.GrowerRefillSeconds * 7 + 2;
            while (shop.Model.GetGrower(culture).Units < shop.Model.GetGrower(culture).Capacity) {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Natural grower refill / " + culture);
                yield return null;
            }
            yield return null;
        }
        IEnumerator GrowerHoldUntil(CultureKind culture, Func<bool> reached, string context)
        {
            shop.AllowValidationInput = false; shop.SetPanel(ShopPanel.None); CollectionAim(culture); yield return null; yield return null;
            Check(shop.Focused && shop.Focused.transform == GrowerRoot(culture) && shop.ScriptedHarvest == null, "Real focused held collection / " + context);
            float deadline = Time.realtimeSinceStartup + 12; CollectionMouse(true);
            while (!reached()) { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(context); yield return null; }
            yield return CollectionRelease();
        }
        IEnumerator GrowerCycle(CultureKind culture)
        {
            yield return CollectionClearBuffer(); yield return GrowerWaitFull(culture); CollectionAim(culture); yield return null; yield return null;
            var world = shop.World; int accepted = GrowerAccepted(culture), toolAccepted = world.CollectionAcceptedCount;
            int sound = shop.GetComponent<ShopAudio>().LabPlaybackCount, refill = world.GrowerRefillCountForValidation(culture);
            int circulation = world.GrowerCirculationCountForValidation(culture), stage = 0, minimum = int.MaxValue;
            bool depleted = false, partial = false, completed = false;
            yield return GrowerClip("grower-" + culture.ToString().ToLowerInvariant() + "-cycle-live", culture, 17, age => {
                int units = shop.Model.GetGrower(culture).Units; minimum = Mathf.Min(minimum, units);
                if (stage == 0 && age >= .35f) { CollectionMouse(true); stage = 1; }
                if (stage == 1 && units == 0) { depleted = true; CollectionMouse(false); stage = 2; }
                if (stage == 2 && units > 0 && units < shop.Model.GetGrower(culture).Capacity) partial = true;
                if (stage == 2 && units == shop.Model.GetGrower(culture).Capacity) completed = true;
            });
            yield return CollectionRelease(); int actual = shop.Model.Available(culture);
            Check(depleted && partial && completed && minimum == 0 && actual > 0 && actual < shop.Model.Capacity,
                "Actual held depletion and unaccelerated refill return to full without filling buffer / " + culture);
            Check(GrowerAccepted(culture) == accepted + actual && world.CollectionAcceptedCount == toolAccepted + actual && shop.GetComponent<ShopAudio>().LabPlaybackCount == sound + actual,
                "Every saved unit has exactly one source response, tool response and existing sound / " + culture);
            Check(world.GrowerRefillCountForValidation(culture) == refill + actual && world.GrowerCirculationCountForValidation(culture) > circulation,
                "Observed refill edges match actual naturally regenerated units / " + culture);
            yield return new WaitForSeconds(1.6f);
            int fullEmission = world.GrowerCirculationCountForValidation(culture), fullRefill = world.GrowerRefillCountForValidation(culture);
            yield return new WaitForSeconds(.6f);
            Check(world.GrowthGrowerForValidation(culture).State == FxGrowerVisualState.Full && world.GrowthGrowerAmbientCount(culture) == 0 &&
                world.GrowerCirculationCountForValidation(culture) == fullEmission && world.GrowerRefillCountForValidation(culture) == fullRefill,
                "Full actual stock keeps quiet organisms without refill emission or repeating accents / " + culture);
        }
        IEnumerator GrowerFullBuffer()
        {
            yield return CollectionClearBuffer(); yield return GrowerWaitFull(CultureKind.Bloom);
            yield return GrowerHoldUntil(CultureKind.Bloom, () => shop.Model.ReservedCultureCount == shop.Model.Capacity, "Fill base reservoir through real collection");
            yield return GrowerWaitFull(CultureKind.Bloom); CollectionAim(CultureKind.Bloom); yield return null; yield return null;
            var world = shop.World; int accepted = GrowerAccepted(CultureKind.Bloom), sound = shop.GetComponent<ShopAudio>().LabPlaybackCount;
            int blocked = world.GrowerBlockedTransitionCountForValidation(CultureKind.Bloom); string authority = FxAuthority();
            CollectionMouse(true);
            yield return GrowerClip("grower-full-buffer-blocked-live", CultureKind.Bloom, 3);
            Check(world.GrowerInteractionForValidation(CultureKind.Bloom) == FxGrowerInteraction.ReservoirFull &&
                world.GrowerBlockedTransitionCountForValidation(CultureKind.Bloom) == blocked + 1 && !world.CollectionActive && world.CollectionParticleCountForValidation == 0,
                "One real full-buffer entry stays blocked without an intake or per-frame transition");
            Check(GrowerAccepted(CultureKind.Bloom) == accepted && shop.GetComponent<ShopAudio>().LabPlaybackCount == sound && FxAuthority() == authority,
                "Repeated blocked hold produces no success response, sound, stock or authority change");
            yield return CollectionRelease(); yield return CollectionClearBuffer();
        }
        bool GrowerMoveTarget(CultureKind culture, out int x, out int z)
        {
            string id = GrowerId(culture); var current = shop.Layout.GetFacility(id).Value;
            int preferredX = culture == CultureKind.Bloom ? 0 : 8, preferredZ = 6;
            var receipt = new StringBuilder("Bounded preview-only search; each selected turn is later committed through real placement.\n");
            var former = shop.Layout.TryPreviewFacility(id, culture == CultureKind.Bloom ? 0 : 6, 6, 0, out _);
            receipt.Append("Former assumed target turn 0: ").Append(former.Failure).Append('\n');
            for (int radius = 0; radius <= 4; radius++)
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++) {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius) continue;
                        int cx = preferredX + dx, cz = preferredZ + dz;
                        if (cx == current.X && cz == current.Z) continue;
                        bool valid = true; string reason = "";
                        for (int turn = 0; turn < 4 && valid; turn++) {
                            var result = shop.Layout.TryPreviewFacility(id, cx, cz, turn, out var candidate);
                            if (!result.Success) { valid = false; reason = "turn " + turn + " / " + result.Failure; break; }
                            var placed = candidate.GetFacility(id).Value;
                            var center = new Vector3(placed.WorldX, .2f, placed.WorldZ);
                            var rotation = Quaternion.Euler(0, turn * 90, 0);
                            // Keep the placement operator reachable before and after the real commit.
                            var operatorPoint = center + rotation * new Vector3(0, 0, -1.9f);
                            if (!shop.Layout.CanReachMainAisle(operatorPoint.x, operatorPoint.z) ||
                                !candidate.CanReachMainAisle(operatorPoint.x, operatorPoint.z)) {
                                valid = false; reason = "turn " + turn + " / unreachable placement operator"; break;
                            }
                            // CollectionLive uses this actual local front and a bounded .13m camera arc.
                            foreach (float side in new[] { -.13f, 0f, .13f }) {
                                var point = center + rotation * new Vector3(side, 0, -1.7f);
                                if (!candidate.CanReachMainAisle(point.x, point.z)) {
                                    valid = false; reason = "turn " + turn + " / unreachable collection operator"; break;
                                }
                            }
                        }
                        receipt.Append(cx).Append(',').Append(cz).Append(" : ").Append(valid ? "all four turns and operators legal" : reason).Append('\n');
                        if (!valid) continue;
                        x = cx; z = cz;
                        File.WriteAllText(Path.Combine(evidence, "grower-" + culture.ToString().ToLowerInvariant() + "-placement-selection.txt"), receipt.ToString());
                        return true;
                    }
            x = z = 0;
            File.WriteAllText(Path.Combine(evidence, "grower-" + culture.ToString().ToLowerInvariant() + "-placement-selection.txt"), receipt.ToString());
            return false;
        }
        IEnumerator GrowerMoves(CultureKind culture)
        {
            yield return CollectionClearBuffer(); yield return GrowerWaitFull(CultureKind.Bloom); yield return GrowerWaitFull(CultureKind.Flow);
            var original = GrowerRoot(culture); string id = GrowerId(culture);
            var moveFrames = new List<GrowerFrame>();
            Check(GrowerMoveTarget(culture, out int x, out int z), "Find relocated position legal at every quarter-turn with reachable operators / " + culture);
            for (int turns = 0; turns < 4; turns++) {
                var oldOwner = shop.World.GrowerOwnerForValidation(culture);
                int accepted = GrowerAccepted(culture), refill = shop.World.GrowerRefillCountForValidation(culture);
                string authority = FxAuthority(); var current = shop.Layout.GetFacility(id).Value;
                Vector3 target = new Vector3(current.WorldX + (x - current.X) * ShopLayout.GridSize, .2f, current.WorldZ + (z - current.Z) * ShopLayout.GridSize);
                Vector3 position = target + Quaternion.Euler(0, turns * 90, 0) * Vector3.back * 1.9f;
                var preview = shop.Layout.TryPreviewFacility(id, x, z, turns, out _);
                Check(preview.Success, "Planned grower quarter-turn is legal / " + culture + " / " + turns + " / " + preview.Failure);
                yield return LabMove(id, x, z, turns, position);
                Check(shop.SaveError == null && GrowerRoot(culture) == original && !shop.World.FxCues.IsValid(oldOwner) &&
                    shop.World.FxPool.ParticleCountForValidation(oldOwner) == 0 && FxAuthority() == authority,
                    "Actual saved relocation retires prior owner and preserves production/root / " + culture + " / " + turns);
                shop.SetPanel(ShopPanel.None); shop.AllowValidationInput = false; CollectionAim(culture); yield return null; yield return null;
                Check(shop.Focused && shop.Focused.transform == original && GrowerAccepted(culture) == accepted && shop.World.GrowerRefillCountForValidation(culture) == refill,
                    "Each legal quarter-turn remains focusable without false collection/refill accents / " + culture + " / " + turns);
                CollectionMouse(true); yield return new WaitForSeconds(.10f);
                var sample = GrowerSample(culture, turns); moveFrames.Add(sample);
                Check(shop.World.CollectionActive && shop.World.CollectionParticleCountForValidation > 0 && sample.sourceError <= .066f && sample.mouthError < .015f && GrowerAccepted(culture) == accepted,
                    "Actual short intake remains at the moved outlet/current nozzle before any unit / " + culture + " / " + turns);
                yield return CollectionRelease(); GrowerResourceCheck("saved turn " + culture + "/" + turns);
            }
            File.WriteAllText(Path.Combine(evidence, "grower-" + culture.ToString().ToLowerInvariant() + "-saved-rotations.json"),
                JsonUtility.ToJson(new GrowerTrace { name = "saved quarter-turns", culture = culture.ToString(), context = "Legal placement commits plus real pre-acceptance held intake", samples = moveFrames.Count, frames = moveFrames.ToArray() }, true));
            string layout = JsonUtility.ToJson(shop.Layout.CaptureSave());
            Check(shop.SaveNow() && shop.ReloadSaved() && JsonUtility.ToJson(shop.Layout.CaptureSave()) == layout && GrowerRoot(culture) == original,
                "Reload retains exact relocated orientation and original root / " + culture);
            shop.SetPanel(ShopPanel.None);
            int sourceAccepted = GrowerAccepted(culture);
            yield return CollectionLive(culture, "grower-" + culture.ToString().ToLowerInvariant() + "-relocated-collection-live");
            Check(GrowerAccepted(culture) == sourceAccepted + 3, "Relocated saved intake also responds at its own source / " + culture);
            GrowerSample(culture, 0); GrowerResourceCheck("relocated collection " + culture);
        }
        IEnumerator GrowerComfort(bool low, bool reduced)
        {
            CultureKind culture = low ? CultureKind.Bloom : CultureKind.Flow;
            yield return CollectionClearBuffer(); yield return GrowerWaitFull(culture);
            yield return GrowerHoldUntil(culture, () => shop.Model.Available(culture) >= 3, "Real partial supply before comfort recording");
            shop.World.SetFxPreviewModes(reduced, low); CollectionAim(culture); yield return null; yield return null;
            int accepted = GrowerAccepted(culture), sound = shop.GetComponent<ShopAudio>().LabPlaybackCount;
            yield return GrowerClip(low ? "grower-low-refill-live" : "grower-reduced-refill-live", culture, 4, null, low, reduced);
            Check(GrowerAccepted(culture) == accepted && shop.GetComponent<ShopAudio>().LabPlaybackCount == sound && shop.Model.Available(culture) == 3,
                "Comfort refill does not invent collection or repeat collection sound");
            shop.World.SetFxPreviewModes(false, false); shop.World.ResetFxPresentation();
        }
        IEnumerator GrowerBeginEdge(CultureKind culture)
        {
            yield return CollectionClearBuffer(); yield return GrowerWaitFull(culture);
            shop.SetPanel(ShopPanel.None); shop.SetFxFocusForValidation(true); CollectionAim(culture); yield return null; yield return null;
            CollectionMouse(true); yield return new WaitForSeconds(.10f);
            Check(shop.World.CollectionActive && shop.Model.Available(culture) == 0, "Real pre-acceptance held source for interruption / " + culture);
        }
        IEnumerator GrowerLifecycle()
        {
            var world = shop.World; CultureKind culture = CultureKind.Bloom;
            foreach (var panel in new[] { ShopPanel.Pause, ShopPanel.Incubator, ShopPanel.Journal, ShopPanel.Arrange }) {
                yield return GrowerBeginEdge(culture); int accepted = GrowerAccepted(culture);
                shop.SetPanel(panel); CollectionMouse(false); yield return null; yield return null;
                Check(!world.CollectionActive && world.CollectionParticleCountForValidation == 0 && GrowerAccepted(culture) == accepted &&
                    world.GrowerInteractionForValidation(culture) == FxGrowerInteraction.Idle && shop.Model.Available(culture) == 0,
                    "Panel boundary clears attempted source without a unit or response / " + panel);
                shop.SetPanel(ShopPanel.None);
            }
            yield return GrowerBeginEdge(culture); shop.SetFxFocusForValidation(false); CollectionMouse(false); yield return null; yield return null;
            Check(!world.CollectionActive && world.CollectionParticleCountForValidation == 0 && world.GrowerInteractionForValidation(culture) == FxGrowerInteraction.Idle,
                "Focus loss cancels source/intake without a stale attempted state"); shop.SetFxFocusForValidation(true);
            yield return GrowerBeginEdge(culture); GameObject blocker = null;
            try {
                blocker = new GameObject("FX07 diagnostic-only side-path obstruction"); blocker.AddComponent<BoxCollider>().size = Vector3.one * .04f;
                blocker.transform.position = (GrowerRoot(culture).Find("Outlet opening").position + world.ToolRoot.Find("Siphon chassis/Collection nozzle/Nozzle light").position) * .5f;
                Physics.SyncTransforms(); yield return null; yield return null;
                Check(shop.Focused && shop.Focused.transform == GrowerRoot(culture) && world.GrowerInteractionForValidation(culture) == FxGrowerInteraction.PathObstructed && world.CollectionParticleCountForValidation == 0 && shop.Model.Available(culture) == 0,
                    "Labeled side-path collider keeps aim but truthfully suppresses obstructed intake before a unit");
            }
            finally { if (blocker) { blocker.SetActive(false); Destroy(blocker); } CollectionMouse(false); Physics.SyncTransforms(); }
            yield return CollectionRelease();
            yield return GrowerBeginEdge(culture); CollectionMouse(false); yield return null; yield return null;
            Check(shop.SaveNow(), "Durable full source before deliberate save obstruction"); byte[] durable = File.ReadAllBytes(shop.SavePath);
            int savedAvailable = shop.Model.Available(culture), savedUnits = shop.Model.GetGrower(culture).Units;
            int successes = GrowerAccepted(culture), sound = shop.GetComponent<ShopAudio>().LabPlaybackCount;
            TutorialBlockSave();
            try {
                CollectionMouse(true); float deadline = Time.realtimeSinceStartup + 2;
                while (shop.SaveError == null) { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Expected isolated save failure"); yield return null; }
                yield return null; yield return null;
                Check(world.GrowerInteractionForValidation(culture) == FxGrowerInteraction.SaveBlocked && world.CollectionParticleCountForValidation == 0 && GrowerAccepted(culture) == successes && shop.GetComponent<ShopAudio>().LabPlaybackCount == sound,
                    "Failed save has no accepted-source/tool/audio success and cannot keep its intake");
                Check(Convert.ToBase64String(durable) == Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)), "Failed collection write preserves durable slot bytes (no live rollback claim)");
                shop.SetPanel(ShopPanel.Pause); yield return null;
                shop.SetPanel(ShopPanel.None); yield return null; yield return null;
                Check(world.GrowerInteractionForValidation(culture) == FxGrowerInteraction.SaveBlocked && world.CollectionParticleCountForValidation == 0,
                    "Policy interruption cannot imitate physical release and clear the held save-failure latch");
            }
            finally { CollectionMouse(false); TutorialUnblockSave(); }
            yield return null; yield return null;
            CollectionMouse(true); yield return new WaitForSeconds(.10f);
            Check(world.CollectionActive && world.GrowerInteractionForValidation(culture) == FxGrowerInteraction.Collecting && GrowerAccepted(culture) == successes,
                "Actual physical release clears the latch despite an old save-error message; a fresh short hold can resume before acceptance");
            yield return CollectionRelease();
            int refillBeforeRecovery = world.GrowerRefillCountForValidation(culture);
            Check(shop.ReloadSaved(), "Recover exact durable grower state after blocked write"); shop.SetPanel(ShopPanel.None);
            Check(shop.Model.Available(culture) == savedAvailable && shop.Model.GetGrower(culture).Units == savedUnits && GrowerAccepted(culture) == successes &&
                world.GrowerRefillCountForValidation(culture) == refillBeforeRecovery,
                "Reload recovers higher durable stock without replaying failed success or inventing a refill accent");
            yield return GrowerBeginEdge(culture); successes = GrowerAccepted(culture); CollectionMouse(false); shop.enabled = false;
            Check(!world.CollectionActive && world.CollectionParticleCountForValidation == 0, "Disable cancels grower intake");
            shop.enabled = true; shop.SetFxFocusForValidation(true); yield return null; yield return null;
            Check(GrowerAccepted(culture) == successes, "Enable does not replay an unaccepted collection");
            shop.FreezeFxVocabularyForValidation = true;
            try {
                Check(shop.SaveNow(), "Freeze only for exact lifecycle authority comparison"); string authority = FxAuthority();
                int refill = world.GrowerRefillCountForValidation(culture); successes = GrowerAccepted(culture);
                for (int i = 0; i < 4; i++) {
                    Check(shop.ReloadSaved(), "Repeated isolated reload / " + i); world.ResetFxPresentation(); world.Animate(Time.time, .02f);
                    Check(world.GrowerRefillCountForValidation(culture) == refill && GrowerAccepted(culture) == successes && FxAuthority() == authority,
                        "Reload/reset does not replay refill/acceptance or alter frozen authority / " + i);
                    GrowerResourceCheck("reload " + i);
                }
            }
            finally { shop.FreezeFxVocabularyForValidation = false; }
        }
        IEnumerator FxGrowerScenario()
        {
            limitations.Add("FX07: actual isolated saved commands, synthetic held InputSystem collection and real placement/rotation commits; fixture camera positions. One explicitly seeded purchase-credit fixture buys the existing upgrade and first lab expansion. No accelerated production or model-only depletion. A diagnostic-only side collider and save-path obstruction exercise failures. Repeated reload exact-authority checks freeze time explicitly. Low/reduced are diagnostic presentation settings. Silent Windows captures are not native input, heard audio, human creative acceptance or GPU acceptance; deferred FX06R08 remains open.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath) == Path.GetFullPath(Path.Combine(evidence, "smoke-save.json")), "FX07 uses only its exact isolated slot");
            yield return new WaitForSeconds(1); Check(shop.SkipTutorial(), "Explicit isolated tutorial skip");
            shop.Customers.ArrivalsEnabled = false; shop.AmbientComedyEnabled = false; shop.AllowValidationInput = false; shop.SetFxFocusForValidation(true);
            previousInputSettings = InputSystem.settings; smokeInputSettings = Instantiate(previousInputSettings); smokeInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = smokeInputSettings; syntheticKeyboard = InputSystem.AddDevice<Keyboard>("EvolutionGrowerKeyboard"); cycleMouse = InputSystem.AddDevice<Mouse>("EvolutionGrowerMouse"); syntheticKeyboard.MakeCurrent(); cycleMouse.MakeCurrent();
            GrowerIsolateInputs();
            limitations.Add("Only the diagnostic player's Unity InputSystem frontend disables other Mouse/Keyboard devices, restoring previously enabled devices during cleanup. Device/current-input provenance is retained; this does not establish what caused any earlier run's unexplained input.");
            if (!Application.isEditor) {
                int width = int.Parse(ShopSession.Arg("-screen-width")), height = int.Parse(ShopSession.Arg("-screen-height"));
                Screen.SetResolution(width == 1280 ? 1920 : 1280, height == 720 ? 1080 : 720, FullScreenMode.Windowed); yield return new WaitForSeconds(.3f);
                Screen.SetResolution(width, height, FullScreenMode.Windowed); yield return new WaitForSeconds(.5f);
                Check(Screen.width == width && Screen.height == height, "Exact actual grower HUD recording resolution");
            }
            foreach (var culture in new[] { CultureKind.Bloom, CultureKind.Flow }) growerInventories[(int)culture] = GrowerInventory(culture);
            yield return GrowerFullBuffer();
            StaffCreditFixture(shop.Config.UpgradeCost + shop.Layout.NextLabExpansionPrice, "FX07 existing upgrade and first lab expansion purchases only");
            shop.SetPanel(ShopPanel.Upgrade); Ok(shop.BuySiphonUpgrade(), "Guarded existing siphon upgrade"); shop.SetPanel(ShopPanel.None);
            Check(shop.Model.Capacity == 8 && shop.Model.CollectionSecondsPerUnit == shop.Config.UpgradedCollectionSeconds, "Real upgraded capacity/cadence supports ordinary depletion");
            yield return GrowerCycle(CultureKind.Bloom); yield return GrowerCycle(CultureKind.Flow);
            GateAim(new Vector3(0, .2f, 2), new Vector3(0, 1.2f, 5)); yield return null;
            Check(shop.OpenLabMenu() && shop.BuyLabExpansion() && shop.Layout.LabTier == 1, "Buy real first lab expansion for legal grower rotation space");
            shop.SetPanel(ShopPanel.None);
            yield return GrowerMoves(CultureKind.Bloom); yield return GrowerMoves(CultureKind.Flow);
            yield return GrowerComfort(true, false); yield return GrowerComfort(false, true);
            yield return GrowerLifecycle(); GrowerResourceCheck("all completed grower states");
            Check(shop.SaveNow(), "Save final isolated relocated grower state");
            File.Copy(shop.SavePath, Path.Combine(evidence, "grower-final-save.json"), true);
            shop.FreezeFxVocabularyForValidation = true;
            try {
                int bloom = GrowerAccepted(CultureKind.Bloom), flow = GrowerAccepted(CultureKind.Flow);
                int bloomRefill = shop.World.GrowerRefillCountForValidation(CultureKind.Bloom), flowRefill = shop.World.GrowerRefillCountForValidation(CultureKind.Flow);
                shop.SetPanel(ShopPanel.Reset); shop.ConfirmNewGame();
                Check(shop.Layout.LabTier == 0 && shop.Model.ReservedCultureCount == 0 && !shop.Model.UpgradeOwned && GrowerAccepted(CultureKind.Bloom) == bloom && GrowerAccepted(CultureKind.Flow) == flow &&
                    shop.World.GrowerRefillCountForValidation(CultureKind.Bloom) == bloomRefill && shop.World.GrowerRefillCountForValidation(CultureKind.Flow) == flowRefill,
                    "Actual isolated new-game reset restores original layout/stock without synthetic accepted/refill events");
                GrowerResourceCheck("new-game reset");
            }
            finally { shop.FreezeFxVocabularyForValidation = false; shop.World.SetFxPreviewModes(false, false); CollectionMouse(false); }
            Check(shop.SkipTutorial(), "Explicitly leave reset tutorial Welcome in diagnostic slot"); shop.SetPanel(ShopPanel.Pause);
            Check(growerInputReceipt.updateSamples > 0 && growerInputReceipt.foreignCurrentSamples == 0 && growerInputReceipt.otherEnabledSamples == 0 && growerInputReceipt.droppedTransitions == 0,
                "All observed input updates retain only fixture Mouse/Keyboard provenance; device restore receipt follows cleanup");
        }
    }
}
#endif
