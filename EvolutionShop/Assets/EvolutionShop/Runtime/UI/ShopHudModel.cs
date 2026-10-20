using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public enum HudDayPhase { Night, Open, Closing }

    /// <summary>One customer in the queue strip: waiting at the counter, or still walking to it.</summary>
    public struct HudQueueEntry
    {
        public string Id;
        public string ArchetypeId;
        public bool AtCounter;
        public float SecondsLeft;
        public float PatienceSeconds;
    }

    /// <summary>Something ready or stuck that may need the player: the incubator, a vat, or a blocked worker.</summary>
    public struct HudPing
    {
        public string Key;
        public string Text;
        public string Short;
        public Vector3 World;
    }

    /// <summary>The tag beside the crosshair: key caps, what the player is looking at and one live status word.</summary>
    public struct HudPrompt
    {
        public bool Shown;
        public string Key;
        public string SecondKey;
        public string Name;
        public string Status;
        public bool Slap;
        public float Progress;
    }

    /// <summary>
    /// Everything the UI Toolkit HUD shows, read from the session once a frame. The HUD never reads gameplay
    /// state itself and never changes it (UX.10); the session fills this in <c>FillHudState</c>.
    /// </summary>
    public sealed class ShopHudState
    {
        /// <summary>The live game model; a different one means New game or a reload, so values are a new baseline.</summary>
        public object Generation;
        public bool Gameplay;
        public bool PanelOpen;
        public bool Placement;
        public bool Paused;
        public HudDayPhase Phase;
        public int Day;
        public float SecondsLeft;
        public long Currency;
        public int MoneySequence;
        public long MoneyDelta;
        public int Reputation;
        public int Appeal;
        public string Objective = "";
        public bool Tutorial;
        public string Notice;
        public string SaveError;
        public string DiscoveryName;
        public int RecipeBloom, RecipeFlow, RecipeSpark, RecipeMist;
        public string Caption;
        public bool HasSpeaker;
        public Vector3 SpeakerWorld;
        public bool HasMarker;
        public string MarkerTitle;
        public string MarkerAction;
        public Vector3 MarkerWorld;
        public HudPrompt Prompt;
        public HudPrompt SlapPrompt;
        public bool Reduced;
        public readonly List<HudQueueEntry> Queue = new List<HudQueueEntry>();
        public readonly List<HudPing> Pings = new List<HudPing>();

        public void Clear()
        {
            Queue.Clear(); Pings.Clear();
            Notice = SaveError = DiscoveryName = Caption = MarkerTitle = MarkerAction = null;
            RecipeBloom = RecipeFlow = RecipeSpark = RecipeMist = 0;
            HasSpeaker = HasMarker = false;
            Prompt = SlapPrompt = default;
        }
    }

    /// <summary>Pure text, timing and layout rules for the UX-03 HUD, kept apart so EditMode tests can reach them.</summary>
    public static class ShopHudRules
    {
        /// <summary>The next-step line fades after this long without a change (UX.4).</summary>
        public const float NextStepSeconds = 10f;
        public const float ToastSeconds = 4.5f;
        public const float FloaterSeconds = 1.6f;
        public const float FloaterRise = 46f;
        /// <summary>Below this share of patience a queue face turns coral.</summary>
        public const float LowPatience = 0.3f;
        /// <summary>Half the side of the square around the crosshair that no HUD element may enter (panel units).</summary>
        public const float CrosshairHalf = 60f;
        public const int MaxFaces = 6;
        public const int MaxToasts = 3;

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>The day chip's two lines: "Night 3" / "no rush", "Day 3" / "4:12 left", "Day 3" / "closing 0:42".</summary>
        public static void DayChip(ShopUiStrings strings, HudDayPhase phase, int day, float seconds, bool paused, out string title, out string detail)
        {
            title = strings.Format(phase == HudDayPhase.Night ? "hud.day.night" : "hud.day.day", day.ToString("N0"));
            detail = paused ? strings.Get("hud.day.paused") : phase == HudDayPhase.Night ? strings.Get("hud.day.noRush") :
                strings.Format(phase == HudDayPhase.Open ? "hud.day.left" : "hud.day.closing", Clock(seconds));
        }

        public static float Patience01(HudQueueEntry entry) =>
            !entry.AtCounter || entry.PatienceSeconds <= 0 ? 1f : Mathf.Clamp01(entry.SecondsLeft / entry.PatienceSeconds);

        public static bool PatienceLow(HudQueueEntry entry) => entry.AtCounter && Patience01(entry) < LowPatience;

        /// <summary>The next step shows while the tutorial runs, and otherwise for ten seconds after it or F1 changed.</summary>
        public static bool NextStepVisible(float now, float shownSince, bool tutorial) => tutorial || now - shownSince < NextStepSeconds;

        /// <summary>Opacity and rise of a floater at its age; reduced motion fades in place.</summary>
        public static void FloaterPose(float age, bool reduced, out float alpha, out float rise)
        {
            float t = Mathf.Clamp01(age / FloaterSeconds);
            alpha = t < 0.08f ? t / 0.08f : t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
            rise = reduced ? 0f : FloaterRise * (1f - (1f - t) * (1f - t));
        }

        /// <summary>
        /// How many toasts, taken in priority order with their heights, fit in the top-centre column above the
        /// crosshair area once the stamp's height is used. The first (a save error) always shows.
        /// </summary>
        public static int ToastsThatFit(float columnLimit, float used, IList<float> heights, float gap)
        {
            int count = 0;
            for (int i = 0; i < heights.Count; i++)
            {
                if (i > 0 && used + heights[i] > columnLimit) break;
                used += heights[i] + gap;
                count++;
            }
            return count;
        }

        /// <summary>Height the top-centre column may use: from its top to a gap above the crosshair area.</summary>
        public static float ColumnLimit(float panelHeight, float columnTop, float gap) =>
            panelHeight * 0.5f - CrosshairHalf - gap - columnTop;

        public static string Signed(long value) => (value > 0 ? "+" : value < 0 ? "−" : "") + System.Math.Abs(value).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The square around the crosshair, centred on the panel.</summary>
        public static Rect CrosshairArea(Vector2 panel) =>
            new Rect(panel.x * 0.5f - CrosshairHalf, panel.y * 0.5f - CrosshairHalf, CrosshairHalf * 2, CrosshairHalf * 2);

        /// <summary>Right, left, above and below a point, each a gap away from it.</summary>
        public static Rect Slot(int index, Vector2 point, Vector2 size, float gap)
        {
            switch (index)
            {
                case 0: return new Rect(point.x + gap, point.y - size.y * 0.5f, size.x, size.y);
                case 1: return new Rect(point.x - gap - size.x, point.y - size.y * 0.5f, size.x, size.y);
                case 2: return new Rect(point.x - size.x * 0.5f, point.y - gap - size.y, size.x, size.y);
                default: return new Rect(point.x - size.x * 0.5f, point.y + gap, size.x, size.y);
            }
        }

        /// <summary>True when the rectangle lies inside the bounds and touches none of the occupied rectangles.</summary>
        public static bool Free(Rect rect, Rect bounds, List<Rect> occupied)
        {
            if (rect.xMin < bounds.xMin || rect.yMin < bounds.yMin || rect.xMax > bounds.xMax || rect.yMax > bounds.yMax) return false;
            for (int i = 0; i < occupied.Count; i++) if (rect.Overlaps(occupied[i])) return false;
            return true;
        }

        /// <summary>The first of the four slots around the point that is free; false when none is.</summary>
        public static bool TryPlaceAround(Vector2 point, Vector2 size, float gap, Rect bounds, List<Rect> occupied, out Rect placed)
        {
            for (int i = 0; i < 4; i++)
            {
                placed = Slot(i, point, size, gap);
                if (Free(placed, bounds, occupied)) return true;
            }
            placed = default;
            return false;
        }

        /// <summary>
        /// Where an off-screen pointer goes: on the line from the panel centre towards the target, pulled in to the
        /// safe band. A target behind the camera points the opposite way, as its projection is mirrored.
        /// </summary>
        public static Vector2 EdgePoint(Vector2 panel, Vector2 target, bool behind, Rect safe, out float angleDegrees)
        {
            Vector2 centre = panel * 0.5f;
            Vector2 direction = target - centre;
            if (behind) direction = -direction;
            if (direction.sqrMagnitude < 1e-4f) direction = Vector2.down;
            direction.Normalize();
            angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float tx = direction.x > 0 ? (safe.xMax - centre.x) / direction.x : direction.x < 0 ? (safe.xMin - centre.x) / direction.x : float.PositiveInfinity;
            float ty = direction.y > 0 ? (safe.yMax - centre.y) / direction.y : direction.y < 0 ? (safe.yMin - centre.y) / direction.y : float.PositiveInfinity;
            return centre + direction * Mathf.Min(tx, ty);
        }

        /// <summary>True when a projected point is outside the panel or behind the camera.</summary>
        public static bool OffScreen(Vector2 point, Vector2 panel, bool behind) =>
            behind || float.IsNaN(point.x) || point.x < 0 || point.y < 0 || point.x > panel.x || point.y > panel.y;
    }

    /// <summary>Remembers which pings were active, so a toast shows only when one starts (UX.4: appears, then fades).</summary>
    public sealed class HudPingTracker
    {
        readonly HashSet<string> active = new HashSet<string>();
        readonly HashSet<string> seen = new HashSet<string>();

        /// <summary>Adds the keys that became active this frame to <paramref name="started"/>; ended keys are forgotten.</summary>
        public void Update(List<HudPing> current, List<HudPing> started)
        {
            started.Clear();
            seen.Clear();
            foreach (var ping in current)
            {
                if (string.IsNullOrEmpty(ping.Key) || !seen.Add(ping.Key)) continue;
                if (active.Add(ping.Key)) started.Add(ping);
            }
            active.IntersectWith(seen);
        }

        public void Reset() { active.Clear(); }
        public bool IsActive(string key) => active.Contains(key);
    }
}
