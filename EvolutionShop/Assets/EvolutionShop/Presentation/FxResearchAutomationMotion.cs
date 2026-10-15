using UnityEngine;

namespace EvolutionShop
{
    public enum FxResearchUpgrade { None, Siphon, Spark, Mist, Collector, Doser }

    /// <summary>Small local committed pulses; no imaginary connection between separate machines.</summary>
    public static class FxResearchAutomationMotion
    {
        public const float Duration = .72f;
        // Existing Collector indicator beside the rendered microscope focus dial; no remote pipe.
        public static readonly Vector3 CollectorValve = new Vector3(.704f, 1.44f, -.085f);
        public static readonly Vector3 DoserInlet = new Vector3(.34f, 1.78f, -.55f);
        public static readonly Vector3 ResearchSample = new Vector3(.565f, 1.474f, -.10f);
        static float Noise(int index, uint sequence, uint salt)
        {
            uint n = unchecked((uint)(index + 1) * 747796405u + sequence * 2891336453u + salt);
            n = unchecked((n ^ (n >> 16)) * 2246822519u);
            return (n & 65535u) / 65535f;
        }
        public static float Delay(int index, uint sequence) => index == 0 ? 0 : .025f + Noise(index, sequence, 71) * .19f;
        public static int Count(bool low, bool reduced) => reduced ? 1 : low ? 5 : 10;
        public static void SampleTransfer(CultureKind culture, int ordinal, uint sequence, bool doser, bool reduced,
            out Vector3 local, out FxParticleRequest request)
        {
            float a = Noise(ordinal, sequence, 17), b = Noise(ordinal, sequence, 137), c = Noise(ordinal, sequence, 997);
            bool puff = ordinal % 5 == 4 || culture == CultureKind.Mist && ordinal % 3 == 1;
            var shape = puff ? FxParticleShape.Puff : culture == CultureKind.Spark && ordinal % 3 == 1 ? FxParticleShape.Spark :
                culture == CultureKind.Flow ? FxParticleShape.Droplet : FxParticleShape.Bubble;
            request = FxParticleRequest.Create(shape, FxAdvancedLabMotion.IsAdvanced(culture) ? FxAdvancedLabMotion.CultureColor(culture) : FxDosePour.ColorOf(culture));
            local = new Vector3((a - .5f) * .075f, (b - .5f) * .055f, (c - .5f) * .045f);
            request.Motion = culture == CultureKind.Flow || culture == CultureKind.Mist ? FxParticleMotion.Spiral : FxParticleMotion.Curve;
            request.End = doser ? new Vector3(-.38f + (b - .5f) * .09f, -.39f + c * .05f, -.015f) : new Vector3((b - .5f) * .11f, .12f + c * .10f, -.07f);
            request.Bend = new Vector3((a - .5f) * .16f - (doser ? .05f : 0), .06f + c * .025f, -.04f);
            request.Radius = culture == CultureKind.Flow ? .045f : .025f;
            request.Turns = .6f + b * .8f; request.Spin = (a - .5f) * 90;
            request.Lifetime = .34f + b * .12f;
            request.Size = (puff ? .10f : .06f) + c * .035f;
            request.EndSize = puff ? .18f : .025f;
            if (puff) request.Color.a *= .55f;
            if (reduced) { local = Vector3.zero; request.Motion = FxParticleMotion.Drift; request.End = request.Bend = Vector3.zero;
                request.Radius = request.Turns = request.Spin = 0; request.Shape = FxParticleShape.Ring; request.Size = request.EndSize = .13f; }
        }
        public static void SampleResearch(int ordinal, uint sequence, bool reduced, out Vector3 local, out FxParticleRequest request)
        {
            float a = Noise(ordinal, sequence, 283), b = Noise(ordinal, sequence, 983), c = Noise(ordinal, sequence, 127);
            bool ring = ordinal == 0 || ordinal == 3;
            request = FxParticleRequest.Create(ring ? FxParticleShape.Ring : FxParticleShape.Spark,
                ring ? new Color(.45f, .94f, .83f, .86f) : new Color(1, .85f, .44f, .9f));
            local = ring ? new Vector3(0, -.065f, -.025f) : new Vector3((a - .5f) * .26f, (b - .5f) * .16f, -.03f - c * .035f);
            request.Motion = FxParticleMotion.Curve;
            request.End = ring ? new Vector3(0, .14f, 0) : new Vector3((a - .5f) * .14f, .05f + c * .09f, -.02f);
            request.Bend = new Vector3((b - .5f) * .06f, .02f, -.01f);
            request.Size = ring ? .13f : .035f + c * .025f; request.EndSize = ring ? .29f : .009f;
            request.Lifetime = .37f + a * .10f; request.Spin = ring ? 0 : (b - .5f) * 160;
            if (reduced) { local = Vector3.zero; request.Motion = FxParticleMotion.Drift; request.End = request.Bend = Vector3.zero;
                request.Spin = 0; request.Size = request.EndSize = .17f; request.Shape = FxParticleShape.Ring; }
        }
        public static string StateLabel(LabModuleVisualState state) => state == LabModuleVisualState.Absent ? "NOT INSTALLED" :
            state == LabModuleVisualState.Off ? "OFF" : state == LabModuleVisualState.Paused ? "PAUSED" :
            state == LabModuleVisualState.StaffPriority ? "MOSS PRIORITY" : state == LabModuleVisualState.Blocked ? "BLOCKED" : "WAITING";
    }
}
