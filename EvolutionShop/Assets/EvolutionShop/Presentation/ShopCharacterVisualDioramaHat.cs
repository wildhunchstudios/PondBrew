using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D (AD.5): a slapped kit visitor's hat spins. The kit merges the hat into the head mesh, as its own colour slots
    /// (a beret and its nub, a flat cap, a hat), so the first spin lifts those slots' triangles into a hat of their own that
    /// turns on the head while the head's own hat slots are hidden; at rest the head shows its hat again.
    /// </summary>
    public sealed partial class ShopCharacterVisual
    {
        public const float DioramaHatSpinSeconds = .6f;
        Transform dioramaHatPivot;
        Renderer dioramaHeadRenderer;
        Mesh dioramaHatMesh;
        Material dioramaHatHidden;
        Material[] dioramaHeadSlots;
        readonly List<int> dioramaHatSlots = new List<int>();
        bool dioramaHatPrepared, dioramaHatSpinning;

        /// <summary>Whether the hat is spinning now, and how far round it has turned (degrees).</summary>
        public bool DioramaHatSpinning => dioramaHatSpinning;
        public float DioramaHatTurn { get; private set; }
        /// <summary>Whether this figure has a hat that can spin.</summary>
        public bool HasDioramaHat { get { PrepareDioramaHat(); return dioramaHatPivot; } }

        static bool HatSlot(string material)
        {
            string name = material.Replace("Diorama / ", "");
            return name == "DK_Beret" || name == "DK_Nub" || name.EndsWith("Cap") || name.EndsWith("Hat");
        }

        void PrepareDioramaHat()
        {
            if (dioramaHatPrepared || !IsDiorama || !head) return;
            dioramaHatPrepared = true;
            var headPart = head.Find("Head");
            if (!headPart) return;
            dioramaHeadRenderer = headPart.GetComponent<Renderer>();
            var mesh = headPart.GetComponent<MeshFilter>() ? headPart.GetComponent<MeshFilter>().sharedMesh :
                dioramaHeadRenderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
            if (!dioramaHeadRenderer || !mesh || !mesh.isReadable) return;
            dioramaHeadSlots = dioramaHeadRenderer.sharedMaterials;
            dioramaHatSlots.Clear();
            for (int i = 0; i < dioramaHeadSlots.Length && i < mesh.subMeshCount; i++)
                if (dioramaHeadSlots[i] && HatSlot(dioramaHeadSlots[i].name)) dioramaHatSlots.Add(i);
            if (dioramaHatSlots.Count == 0) return;
            // The hat's own triangles, with the head's vertices, about the hat's centre.
            var vertices = mesh.vertices; var normals = mesh.normals;
            var used = new HashSet<int>();
            foreach (int slot in dioramaHatSlots) foreach (int index in mesh.GetTriangles(slot)) used.Add(index);
            var centre = Vector3.zero; foreach (int index in used) centre += vertices[index]; centre /= used.Count;
            dioramaHatMesh = new Mesh { name = "Diorama spinning hat / " + DioramaRole?.Id, indexFormat = mesh.indexFormat };
            var moved = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) moved[i] = vertices[i] - new Vector3(centre.x, 0, centre.z);
            dioramaHatMesh.vertices = moved; if (normals.Length == vertices.Length) dioramaHatMesh.normals = normals;
            dioramaHatMesh.subMeshCount = dioramaHatSlots.Count;
            var hatMaterials = new Material[dioramaHatSlots.Count];
            for (int i = 0; i < dioramaHatSlots.Count; i++)
            { dioramaHatMesh.SetTriangles(mesh.GetTriangles(dioramaHatSlots[i]), i); hatMaterials[i] = dioramaHeadSlots[dioramaHatSlots[i]]; }
            dioramaHatMesh.RecalculateBounds();
            dioramaHatPivot = new GameObject("Diorama spinning hat").transform;
            dioramaHatPivot.SetParent(headPart, false);
            dioramaHatPivot.localPosition = new Vector3(centre.x, 0, centre.z);
            dioramaHatPivot.gameObject.AddComponent<MeshFilter>().sharedMesh = dioramaHatMesh;
            var renderer = dioramaHatPivot.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = hatMaterials; renderer.shadowCastingMode = ShadowCastingMode.Off;
            dioramaHatPivot.gameObject.SetActive(false);
            // An invisible stand-in for the head's own hat slots while the hat is off spinning.
            dioramaHatHidden = DioramaLook.Unlit("hidden hat slot", new Color(1, 1, 1, 0), false);
        }

        /// <summary>The spin after a slap: the hat pops up, turns twice and settles back (age since the contact).</summary>
        public void PoseDioramaHat(float age)
        {
            if (age < 0 || age >= DioramaHatSpinSeconds) { RestDioramaHat(); return; }
            PrepareDioramaHat();
            if (!dioramaHatPivot) return;
            if (!dioramaHatSpinning)
            {
                dioramaHatSpinning = true;
                var slots = (Material[])dioramaHeadSlots.Clone();
                foreach (int slot in dioramaHatSlots) slots[slot] = dioramaHatHidden;
                dioramaHeadRenderer.sharedMaterials = slots;
                dioramaHatPivot.gameObject.SetActive(true);
            }
            float t = age / DioramaHatSpinSeconds;
            DioramaHatTurn = 720 * (1 - (1 - t) * (1 - t));
            var rest = dioramaHatPivot.localPosition; rest.y = Mathf.Sin(Mathf.PI * t) * .09f;
            dioramaHatPivot.localPosition = rest;
            dioramaHatPivot.localRotation = Quaternion.Euler(0, DioramaHatTurn, 0);
        }

        public void RestDioramaHat()
        {
            if (!dioramaHatSpinning) return;
            dioramaHatSpinning = false; DioramaHatTurn = 0;
            if (dioramaHeadRenderer && dioramaHeadSlots != null) dioramaHeadRenderer.sharedMaterials = dioramaHeadSlots;
            if (dioramaHatPivot) { dioramaHatPivot.gameObject.SetActive(false); var p = dioramaHatPivot.localPosition; p.y = 0; dioramaHatPivot.localPosition = p; dioramaHatPivot.localRotation = Quaternion.identity; }
        }

        void ReleaseDioramaHat()
        {
            RestDioramaHat();
            if (dioramaHatMesh) { if (Application.isPlaying) Destroy(dioramaHatMesh); else DestroyImmediate(dioramaHatMesh); }
            if (dioramaHatHidden) { if (Application.isPlaying) Destroy(dioramaHatHidden); else DestroyImmediate(dioramaHatHidden); }
            dioramaHatMesh = null; dioramaHatHidden = null; dioramaHatPivot = null; dioramaHeadRenderer = null; dioramaHeadSlots = null;
            dioramaHatSlots.Clear(); dioramaHatPrepared = false;
        }
    }
}
