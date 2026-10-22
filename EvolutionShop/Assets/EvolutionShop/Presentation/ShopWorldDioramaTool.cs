using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// AD-D: the slim siphon (AD.4). With the look on, the held siphon is smaller and rests a little further to the side,
    /// and its chamber is a narrower glass tube mounted at the side of the chassis, still showing the carried cultures;
    /// together about a third less of the screen. The hands' animation offsets the tool from whichever rest pose is shown.
    /// Switching the look off restores the classic tool exactly.
    /// </summary>
    public sealed partial class ShopWorld
    {
        static readonly Vector3 ClassicToolRest = new Vector3(.33f, -.16f, .58f), DioramaToolRest = new Vector3(.34f, -.165f, .58f);
        const float ClassicToolScale = DioramaRoomRules.ClassicToolScale, DioramaToolScale = DioramaRoomRules.DioramaToolScale;
        /// <summary>The slim chamber: narrower across, a touch taller, set out to the chassis' outer side.</summary>
        static readonly Vector3 DioramaChamberShape = DioramaRoomRules.DioramaChamberShape;
        const float DioramaChamberSide = .052f;

        /// <summary>The held siphon's rest place (camera space), scale and turn for the look shown.</summary>
        Vector3 ToolRest => dioramaActive ? DioramaToolRest : ClassicToolRest;
        float ToolScale => dioramaActive ? DioramaToolScale : ClassicToolScale;
        static Quaternion ToolTurn => Quaternion.Euler(-5, -7, -5);

        /// <summary>Applies the tool's pose and chamber for the look shown and the capacity upgrade.</summary>
        void ApplyToolLook()
        {
            if (!ToolRoot || !ToolChamber) return;
            ToolRoot.localScale = Vector3.one * ToolScale;
            ToolRoot.localPosition = ToolRest; ToolRoot.localRotation = ToolTurn;
            var size = upgraded ? new Vector3(1.35f, 1.48f, 1.35f) : Vector3.one;
            if (dioramaActive) size = Vector3.Scale(size, DioramaChamberShape);
            ToolChamber.localScale = size;
            ToolChamber.localPosition = new Vector3(dioramaActive ? DioramaChamberSide : 0, upgraded ? .067f : .02f, .025f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Validation: the screen rectangle (pixels) the held siphon and hands cover, from their renderers' bounds.</summary>
        public Rect ToolScreenRectForValidation(Camera camera)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            if (!ToolRoot || !camera) return Rect.zero;
            foreach (var renderer in ToolRoot.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.GetComponent<TextMesh>()) continue;
                var b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = camera.WorldToScreenPoint(corner);
                    if (p.z <= 0) continue;
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
            }
            if (minX > maxX) return Rect.zero;
            minX = Mathf.Clamp(minX, 0, camera.pixelWidth); maxX = Mathf.Clamp(maxX, 0, camera.pixelWidth);
            minY = Mathf.Clamp(minY, 0, camera.pixelHeight); maxY = Mathf.Clamp(maxY, 0, camera.pixelHeight);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
#endif
    }
}
