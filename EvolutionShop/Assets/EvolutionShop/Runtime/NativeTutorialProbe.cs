#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    // Explicit manual QA profile. Does not inject input, mutate stock/lessons,
    // change focus policy, accelerate time, or call gameplay/menu commands.
    public sealed class NativeTutorialProbe : MonoBehaviour
    {
        public const string Marker = "Evolution Shop isolated native QA";
        public static string ResolveSavePath(string directory, bool smoke)
        {
            if (directory == null) return null;
            if (smoke) throw new ArgumentException("Native QA cannot use smoke input overrides.");
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
                throw new ArgumentException("Native QA requires an absolute marked folder.");
            string root = Path.GetFullPath(directory);
            if (root == Path.GetPathRoot(root)) throw new ArgumentException("Native QA cannot use a filesystem root.");
            root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string profile = Path.GetFullPath(Path.GetDirectoryName(SaveStore.DefaultPath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(root, profile, StringComparison.OrdinalIgnoreCase) || root.StartsWith(profile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Native QA cannot use the normal player profile.");
            if (!Directory.Exists(root)) throw new ArgumentException("Create the isolated marked folder first.");
            for (var info = new DirectoryInfo(root); info != null; info = info.Parent)
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Native QA refuses linked directories.");
            string marker = Path.Combine(root, "native-qa-profile.txt");
            if (!File.Exists(marker) || File.ReadAllText(marker).Trim() != Marker)
                throw new ArgumentException("The isolated native QA marker is missing.");
            string path = Path.Combine(root, "native-save.json");
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(path + suffix) && (File.GetAttributes(path + suffix) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Native QA refuses linked save files.");
            return path;
        }

        [Serializable] sealed class State
        {
            public string time, panel, lesson, mode, phase, focused, cursor, savePath, pose;
            public bool validationMode, focusedWindow, paused, cursorVisible, inputEnabled, leftHeld, autosaveBlocked;
            public int width, height, currency, bloom, flow, clicks, ePresses, f1Presses, escapePresses, tabPresses, moves, looks;
            public int sales, interrupted;
            public float harvestProgress, maxHarvestProgress, maxHeldSeconds, incubationSeconds;
            public string incubatorPhase, tutorialFacts;
            public Vector3 position;
        }
        ShopSession shop;
        string directory, lastPose, lastSize, lastSignature;
        float next, held, maxHeld, maxProgress;
        int clicks, ePresses, f1Presses, escapePresses, tabPresses, moves, looks;
        void Start()
        {
            shop = GetComponent<ShopSession>();
            directory = Path.GetDirectoryName(shop.SavePath);
            if (shop.ValidationMode || ResolveSavePath(directory, false) != shop.SavePath) throw new InvalidOperationException("Native observer requires ordinary isolated input.");
            shop.Player.ControlledMotion += Motion;
            Application.logMessageReceived += Log;
            Record("started: ordinary input/focus/arrivals; explicit poses are fixtures");
        }
        void Motion(bool moved, bool looked, bool lab) { if (moved) moves++; if (looked) looks++; }
        void LateUpdate()
        {
            if (shop == null) return;
            var k = Keyboard.current; var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) clicks++;
            if (k != null)
            {
                if (k.eKey.wasPressedThisFrame) ePresses++;
                if (k.f1Key.wasPressedThisFrame) f1Presses++;
                if (k.escapeKey.wasPressedThisFrame) escapePresses++;
                if (k.tabKey.wasPressedThisFrame) tabPresses++;
            }
            held = m != null && m.leftButton.isPressed && Application.isFocused ? held + Time.unscaledDeltaTime : 0;
            maxHeld = Mathf.Max(maxHeld, held); maxProgress = Mathf.Max(maxProgress, shop.HarvestProgress);
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .25f;
            string path = Path.Combine(directory, "native-pose.txt");
            if (File.Exists(path))
            {
                string pose = File.ReadAllText(path).Trim();
                if (pose != lastPose) { ApplyPose(pose); lastPose = pose; Record("fixture pose: " + pose); }
            }
            path = Path.Combine(directory, "native-size.txt");
            if (File.Exists(path))
            {
                string size = File.ReadAllText(path).Trim();
                if (size != lastSize)
                {
                    if (size == "720") Screen.SetResolution(1280, 720, false);
                    else if (size == "1080") Screen.SetResolution(1920, 1080, false);
                    else throw new ArgumentException("Native QA size must be720 or1080.");
                    lastSize = size; Record("fixture resolution: " + size);
                }
            }
            var state = Snapshot();
            string json = JsonUtility.ToJson(state, true);
            File.WriteAllText(Path.Combine(directory, "native-state.json"), json);
            string signature = state.panel + "/" + state.lesson + "/" + state.phase + "/" + state.currency + "/" + state.focusedWindow + "/" + state.bloom + "/" + state.flow;
            if (signature != lastSignature) { lastSignature = signature; Record("state", state); }
        }
        void ApplyPose(string pose)
        {
            if (pose == "spawn") { shop.Player.Teleport(new Vector3(0, .2f, -3.8f), Quaternion.identity); return; }
            if (!Enum.TryParse(pose, out StationKind kind) || !Enum.IsDefined(typeof(StationKind), kind)) throw new ArgumentException("Unknown native fixture station.");
            Station station = null;
            foreach (var candidate in shop.World.Stations) if (candidate.Kind == kind) { station = candidate; break; }
            if (station == null) throw new InvalidOperationException("Native fixture station is absent.");
            bool lab = kind == StationKind.Bloom || kind == StationKind.Flow || kind == StationKind.Incubator;
            Vector3 position = lab ? new Vector3(station.transform.position.x, .2f, 2.4f) : station.transform.TransformPoint(new Vector3(0, .2f, -2.5f));
            Vector3 direction = station.transform.TransformPoint(new Vector3(0, 1.1f, 0)) - (position + Vector3.up * 1.6f);
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            shop.Player.Teleport(position, Quaternion.Euler(0, yaw, 0));
            typeof(ShopPlayer).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(shop.Player, pitch);
            shop.Player.View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
        State Snapshot() => new State {
            time = DateTime.UtcNow.ToString("o"), panel = shop.Panel.ToString(), lesson = shop.ActiveTutorialLesson.ToString(), mode = shop.Tutorial.Mode.ToString(), phase = shop.Cycle.Phase.ToString(),
            focused = shop.Focused == null ? "none" : shop.Focused.Kind.ToString(), cursor = Cursor.lockState.ToString(), savePath = shop.SavePath, pose = lastPose,
            validationMode = shop.ValidationMode, focusedWindow = Application.isFocused, paused = shop.Paused, cursorVisible = Cursor.visible, inputEnabled = shop.Player.InputEnabled,
            leftHeld = Mouse.current != null && Mouse.current.leftButton.isPressed, autosaveBlocked = shop.AutosaveBlocked,
            width = Screen.width, height = Screen.height, currency = shop.Model.Currency, bloom = shop.Model.Available(CultureKind.Bloom), flow = shop.Model.Available(CultureKind.Flow),
            clicks = clicks, ePresses = ePresses, f1Presses = f1Presses, escapePresses = escapePresses, tabPresses = tabPresses, moves = moves, looks = looks,
            sales = shop.Reports.Current?.sales ?? 0, interrupted = shop.Reports.Current?.interrupted ?? 0,
            harvestProgress = shop.HarvestProgress, maxHarvestProgress = maxProgress, maxHeldSeconds = maxHeld, incubationSeconds = shop.Model.Incubator.SecondsRemaining,
            incubatorPhase = shop.Model.Incubator.Phase.ToString(), tutorialFacts = shop.Tutorial.Facts.ToString(), position = shop.Player.transform.position };
        void Record(string reason, State state = null) => File.AppendAllText(Path.Combine(directory, "native-events.jsonl"), JsonUtility.ToJson(new Entry { reason = reason, state = state ?? Snapshot() }) + "\n");
        [Serializable] sealed class Entry { public string reason; public State state; }
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                File.AppendAllText(Path.Combine(directory, "native-errors.txt"), type + ": " + message + "\n" + stack + "\n");
        }
        void OnApplicationQuit() { if (shop != null) Record("application quit"); }
        void OnDestroy() { if (shop != null && shop.Player != null) shop.Player.ControlledMotion -= Motion; Application.logMessageReceived -= Log; }
    }
}
#endif

