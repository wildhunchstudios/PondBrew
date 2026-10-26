using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-11's world (GR.12): a sign over the shop door while the Grand Exhibition is prepared (pale gold) or under way
    /// (deep gold, with a lamp); a gold tag over each tank on a spotlit place that holds a crown on display; confetti when it
    /// is held; and the award, a plaque on unit A's front wall east of the door, from then on (E on it shows the results).
    /// Placeholder shapes, as the rest of the greybox.
    /// </summary>
    public sealed partial class GrowthWorld
    {
        public static readonly Vector3 AwardPosition = new Vector3(0.75f, 2.05f, -3.87f), ExhibitionSignPosition = new Vector3(-0.5f, 2.78f, -3.86f);
        const float ConfettiSeconds = 4f;

        Transform award, exhibitionSign;
        Renderer signFace;
        Light signLamp;
        Material goldPale, goldDeep;
        readonly Dictionary<string, Transform> displayTags = new Dictionary<string, Transform>();
        readonly List<(Transform piece, Vector3 velocity, float spin)> confetti = new List<(Transform, Vector3, float)>();
        float confettiLeft;

        /// <summary>Validation: what shows of the exhibition.</summary>
        public bool AwardShowing => award != null && award.gameObject.activeSelf;
        public bool SignShowing => exhibitionSign != null && exhibitionSign.gameObject.activeSelf;
        public bool SignToday => SignShowing && signLamp.enabled;
        public int DisplayTagsShown => displayTags.Values.Count(t => t.gameObject.activeSelf);
        public int ConfettiCount => confetti.Count;

        void ExhibitionPieces()
        {
            // GR-16: in the look, the sign's face is a pale gold while it is prepared and a glowing gold on the day.
            goldPale = LookOn ? Plastic("exhibition gold (pale)", C("#FFE7A3"), 0.45f) : ShopWorld.MakeMaterial("GR exhibition gold (pale)", new Color(0.93f, 0.84f, 0.55f), 0, 0.3f);
            goldDeep = LookOn ? Glowing("exhibition gold", C("#FFC93C"), 0.6f, 0.7f) : ShopWorld.MakeMaterial("GR exhibition gold", new Color(0.86f, 0.64f, 0.16f), 0.3f, 0.5f);
            var root = new GameObject("Grand Exhibition").transform; root.SetParent(transform, false);

            // The sign over the door, inside, its face to the shop.
            exhibitionSign = new GameObject("Exhibition sign").transform; exhibitionSign.SetParent(root, false);
            exhibitionSign.localPosition = ExhibitionSignPosition;
            Box(exhibitionSign, "Board", Vector3.zero, new Vector3(1.5f, 0.42f, 0.04f), trim);
            signFace = Box(exhibitionSign, "Face", new Vector3(0, 0, 0.025f), new Vector3(1.36f, 0.3f, 0.01f), goldPale).GetComponent<Renderer>();
            Box(exhibitionSign, "Rosette", new Vector3(-0.52f, 0, 0.032f), new Vector3(0.18f, 0.18f, 0.01f), paper);
            signLamp = new GameObject("Sign lamp").AddComponent<Light>();
            signLamp.transform.SetParent(exhibitionSign, false);
            signLamp.transform.localPosition = new Vector3(0, -0.4f, 0.6f);
            signLamp.type = LightType.Point; signLamp.range = 3f; signLamp.intensity = 1.4f; signLamp.color = new Color(1f, 0.85f, 0.55f); signLamp.shadows = LightShadows.None;
            exhibitionSign.gameObject.SetActive(false);

            // A tag over each spotlit place's tank, shown while a crown in it is on display.
            foreach (var place in GrowthShop.SpotlitPlaces)
            {
                var spot = GrowthSpaces.SpotOf(place);
                float yaw = spot.Yaw * Mathf.Deg2Rad;
                var front = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
                var tag = new GameObject("Display tag " + place).transform; tag.SetParent(root, false);
                tag.localPosition = new Vector3(spot.Centre.x, 2.2f, spot.Centre.z) + front * 0.2f;
                tag.localRotation = Quaternion.Euler(0, spot.Yaw, 0);
                Box(tag, "Ribbon", Vector3.zero, new Vector3(0.7f, 0.22f, 0.03f), goldDeep);
                Box(tag, "Star", new Vector3(0, 0, 0.02f), new Vector3(0.12f, 0.12f, 0.01f), paper);
                tag.gameObject.SetActive(false);
                displayTags[place] = tag;
            }

            // The award: a plaque with a gold medal, on unit A's front wall east of the door.
            award = new GameObject("Grand Exhibition award").transform; award.SetParent(root, false);
            award.localPosition = AwardPosition;
            Box(award, "Plaque", Vector3.zero, new Vector3(0.6f, 0.75f, 0.05f), counterMat, true);
            Box(award, "Card", new Vector3(0, -0.2f, 0.03f), new Vector3(0.44f, 0.18f, 0.01f), paper);
            var medal = ShopWorld.Shape(award, "Medal", PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0.04f), new Vector3(0.28f, 0.015f, 0.28f), goldDeep);
            medal.localRotation = Quaternion.Euler(90, 0, 0);
            Box(award, "Ribbon", new Vector3(0, 0.33f, 0.035f), new Vector3(0.12f, 0.14f, 0.01f), bannerMats.Count > 0 ? bannerMats[0] : goldPale);
            Target(award.gameObject, GrowthTargetKind.Award, "award");
            award.gameObject.SetActive(false);
        }

        void SyncExhibition(GrowthShop shop)
        {
            if (award == null) return;
            if (award.gameObject.activeSelf != shop.ExhibitionDone) award.gameObject.SetActive(shop.ExhibitionDone);
            bool sign = shop.ExhibitionPrepared || shop.ExhibitionToday;
            if (exhibitionSign.gameObject.activeSelf != sign) exhibitionSign.gameObject.SetActive(sign);
            if (sign)
            {
                signLamp.enabled = shop.ExhibitionToday;
                var face = shop.ExhibitionToday ? goldDeep : goldPale;
                if (signFace.sharedMaterial != face) signFace.sharedMaterial = face;
            }
            foreach (var kv in displayTags)
            {
                var c = shop.ContainerAt(kv.Key);
                bool show = c != null && c.Occupants.Any(GrowthShop.IsOnDisplay);
                if (kv.Value.gameObject.activeSelf != show) kv.Value.gameObject.SetActive(show);
            }
        }

        /// <summary>The Grand Exhibition is held: confetti falls in front of the player for a few seconds (less with reduced motion).</summary>
        public void Celebrate(Vector3 around, Vector3 facing)
        {
            ClearConfetti();
            var ahead = new Vector3(facing.x, 0, facing.z);
            ahead = ahead.sqrMagnitude < 0.01f ? Vector3.forward : ahead.normalized;
            var centre = around + ahead * 2.2f;
            int count = session != null && session.ReducedMotion ? 40 : 120;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f, radius = 0.2f + 2.4f * ((i * 0.618034f) % 1f);
                var at = centre + new Vector3(Mathf.Cos(angle) * radius, 1.9f + (i % 9) * 0.14f, Mathf.Sin(angle) * radius);
                var mat = bannerMats.Count > 0 ? bannerMats[i % bannerMats.Count] : goldDeep;
                var piece = Box(transform, "Confetti", transform.InverseTransformPoint(at), new Vector3(0.13f, 0.015f, 0.09f), i % 4 == 0 ? goldDeep : mat);
                piece.localRotation = Quaternion.Euler(i * 37f, i * 53f, 0);
                confetti.Add((piece, new Vector3(Mathf.Sin(i) * 0.2f, -0.45f - (i % 7) * 0.05f, Mathf.Cos(i * 1.3f) * 0.2f), 90f + (i % 9) * 40f));
            }
            confettiLeft = ConfettiSeconds;
        }

        void AnimateConfetti(float dt)
        {
            if (confetti.Count == 0) return;
            confettiLeft -= dt;
            if (confettiLeft <= 0) { ClearConfetti(); return; }
            foreach (var (piece, velocity, spin) in confetti)
            {
                var p = piece.localPosition + velocity * dt;
                if (p.y < 0.02f) p.y = 0.02f;
                piece.localPosition = p;
                if (p.y > 0.02f) piece.localRotation *= Quaternion.Euler(spin * dt, spin * 0.6f * dt, 0);
            }
        }

        void ClearConfetti()
        {
            foreach (var (piece, _, _) in confetti) if (piece) Destroy(piece.gameObject);
            confetti.Clear();
            confettiLeft = 0;
        }

        void ResetExhibition() => ClearConfetti();
    }
}
