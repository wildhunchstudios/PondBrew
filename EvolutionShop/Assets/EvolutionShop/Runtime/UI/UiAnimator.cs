using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// Runs the UX-08 feel animations (<see cref="UiMotion"/>) on UI Toolkit elements through inline opacity, scale,
    /// translate and rotate, one animation per element. When one ends its inline values are cleared, so the stylesheet's
    /// own values (a disabled button's half opacity, the stamp's tilt) come back exactly. Presentation only: nothing here
    /// waits for an animation before a command runs (UX.9).
    /// </summary>
    public sealed class UiAnimator
    {
        sealed class Tween
        {
            public VisualElement Element;
            public UiMotionKind Kind;
            public float Started;
            public UiPoseChannels Touched;
            public Action Done;
        }

        readonly List<Tween> tweens = new List<Tween>();
        readonly List<Tween> ended = new List<Tween>();
        readonly int[] played = new int[Enum.GetValues(typeof(UiMotionKind)).Length];

        /// <summary>The feel clock: real time since the root started, times <see cref="TimeScale"/>.</summary>
        public float Now { get; private set; }
        /// <summary>1 in play. Validation slows the clock to capture a frame mid-animation; every feel timer follows it.</summary>
        public float TimeScale { get; set; } = 1f;
        public bool Idle => tweens.Count == 0;
        public int Active => tweens.Count;
        public int Played(UiMotionKind kind) => played[(int)kind];

        public void Advance(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            // A long hitch ends animations rather than replaying them.
            Now += Mathf.Min(seconds, .25f) * Mathf.Max(0f, TimeScale);
        }

        /// <summary>Starts <paramref name="kind"/> on the element, replacing whatever it was playing (without that one's callback).</summary>
        public void Play(VisualElement element, UiMotionKind kind, Action done = null)
        {
            if (element == null) { done?.Invoke(); return; }
            Stop(element);
            tweens.Add(new Tween { Element = element, Kind = kind, Started = Now, Done = done });
            played[(int)kind]++;
        }

        public bool IsPlaying(VisualElement element) => Find(element) != null;
        public bool IsPlaying(VisualElement element, UiMotionKind kind) { var t = Find(element); return t != null && t.Kind == kind; }
        public bool AnyPlaying(UiMotionKind kind) { foreach (var t in tweens) if (t.Kind == kind) return true; return false; }

        /// <summary>Seconds since the element's animation started; NaN when it has none.</summary>
        public float Age(VisualElement element) { var t = Find(element); return t != null ? Now - t.Started : float.NaN; }

        /// <summary>Ends the element's animation at rest, without its callback.</summary>
        public void Stop(VisualElement element)
        {
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                if (tweens[i].Element != element) continue;
                Clear(element, tweens[i].Touched);
                tweens.RemoveAt(i);
            }
        }

        /// <summary>Ends every animation at rest and runs their callbacks, as when the preview turns off.</summary>
        public void Finish()
        {
            ended.Clear();
            ended.AddRange(tweens);
            tweens.Clear();
            foreach (var t in ended) { Clear(t.Element, t.Touched); t.Done?.Invoke(); }
            ended.Clear();
        }

        /// <summary>Applies this frame's pose to every animated element; finished ones return to their stylesheet values.</summary>
        public void Tick(bool reduced)
        {
            ended.Clear();
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                var t = tweens[i];
                float age = Now - t.Started;
                if (t.Element.panel == null || age >= UiMotion.Duration(t.Kind)) { tweens.RemoveAt(i); ended.Add(t); continue; }
                var pose = UiMotion.Sample(t.Kind, age, reduced);
                // A channel this frame doesn't set (reduced motion switched on mid-animation) goes back to the stylesheet.
                Clear(t.Element, t.Touched & ~pose.Channels);
                Apply(t.Element, pose);
                t.Touched |= pose.Channels;
            }
            foreach (var t in ended) { Clear(t.Element, t.Touched); t.Done?.Invoke(); }
            ended.Clear();
        }

        Tween Find(VisualElement element)
        {
            foreach (var t in tweens) if (t.Element == element) return t;
            return null;
        }

        static void Apply(VisualElement e, UiPose pose)
        {
            if (pose.Has(UiPoseChannels.Opacity)) e.style.opacity = Mathf.Clamp01(pose.Opacity);
            if (pose.Has(UiPoseChannels.Scale)) e.style.scale = new Scale(new Vector3(pose.ScaleX, pose.ScaleY, 1f));
            if (pose.Has(UiPoseChannels.Translate)) e.style.translate = new Translate(pose.OffsetX, pose.OffsetY);
            if (pose.Has(UiPoseChannels.Rotate)) e.style.rotate = new Rotate(new Angle(pose.Rotate, AngleUnit.Degree));
        }

        static void Clear(VisualElement e, UiPoseChannels channels)
        {
            if (e == null || channels == UiPoseChannels.None) return;
            if ((channels & UiPoseChannels.Opacity) != 0) e.style.opacity = StyleKeyword.Null;
            if ((channels & UiPoseChannels.Scale) != 0) e.style.scale = StyleKeyword.Null;
            if ((channels & UiPoseChannels.Translate) != 0) e.style.translate = StyleKeyword.Null;
            if ((channels & UiPoseChannels.Rotate) != 0) e.style.rotate = StyleKeyword.Null;
        }
    }
}
