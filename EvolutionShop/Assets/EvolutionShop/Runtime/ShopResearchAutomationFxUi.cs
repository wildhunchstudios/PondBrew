using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        void DrawResearchUpgradeStrip()
        {
            Rect rect = GUILayoutUtility.GetRect(1, 54, GUILayout.ExpandWidth(true));
            hudTheme.Draw(rect, hudTheme.Inset);
            bool active = World && World.ResearchFxRemaining > 0;
            string title = active ? ResearchUpgradeCaption(World.LastResearchUpgrade) : "RESEARCH / SAVED INSTALLATIONS";
            GUI.Label(new Rect(rect.x + 12, rect.y + 8, rect.width - 110, 40), title, smallStyle);
            if (!active || Event.current.type != EventType.Repaint) return;
            var area = new Rect(rect.xMax - 88, rect.y + 2, 80, 50);
            GUI.BeginGroup(area);
            int count = FxResearchAutomationMotion.Count(World.LabDoseLow, World.LabDoseReduced);
            for (int i = 0; i < count; i++) {
                FxResearchAutomationMotion.SampleResearch(i, World.ResearchFxSeed, World.LabDoseReduced, out var local, out var p);
                float age = World.ResearchFxAge - FxResearchAutomationMotion.Delay(i, World.ResearchFxSeed);
                if (age < 0 || age >= p.Lifetime) continue;
                float f = age / p.Lifetime;
                Vector3 point = FxDosePour.Point(local, p, f);
                float size = Mathf.Lerp(p.Size, p.EndSize, Mathf.SmoothStep(0, 1, f)) * 135;
                var previous = GUI.color;
                var tint = p.Color; tint.a *= Mathf.Min(1, f / .06f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, f)));
                GUI.color = tint;
                GUI.DrawTexture(new Rect(40 + point.x * 130 - size * .5f, 25 - point.y * 130 - size * .5f, size, size),
                    p.Shape == FxParticleShape.Ring ? hudTheme.FxRing : hudTheme.FxDisc);
                GUI.color = previous;
            }
            GUI.EndGroup();
        }
        static string ResearchUpgradeCaption(FxResearchUpgrade kind) => kind == FxResearchUpgrade.Siphon ? "INSTALLED / SIPHON MK II" :
            kind == FxResearchUpgrade.Spark ? "RESEARCH SAVED / SPARK UNLOCKED" : kind == FxResearchUpgrade.Mist ? "RESEARCH SAVED / MIST UNLOCKED" :
            kind == FxResearchUpgrade.Collector ? "INSTALLED / COLLECTOR OFF" : "INSTALLED / DOSER OFF";
    }
}
