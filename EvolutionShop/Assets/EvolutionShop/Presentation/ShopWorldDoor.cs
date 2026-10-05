using UnityEngine;
using Unity.AI.Navigation;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        public ShopRoomDoor LabDoor { get; private set; }

        void BuildLabDoor()
        {
            var root = Group("Automatic laboratory door", transform, new Vector3(0, 0, .5f));
            // Customer navigation already excludes the lab. A moving leaf is never baked into that surface.
            root.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var left = DoorLeaf(root, -1);
            var right = DoorLeaf(root, 1);
            LabDoor = root.gameObject.AddComponent<ShopRoomDoor>();
            LabDoor.Initialize(left, right);
            // Static frame stays separate from the moving leaves and leaves the original 3.2m passage clear.
            var frame = Group("Laboratory door frame", transform, new Vector3(0, 0, .5f));
            for (int side = -1; side <= 1; side += 2)
                ArtBox(frame, "Porcelain door jamb", new Vector3(side * 1.69f, 1.4f, 0), new Vector3(.18f, 2.8f, .42f), cream);
            ArtBox(frame, "Door motor cover", new Vector3(0, 2.83f, 0), new Vector3(3.55f, .24f, .44f), artTeal);
            for (int side = -1; side <= 1; side += 2)
            {
                var face = Group("Door direction " + side, frame, Vector3.zero);
                if (side > 0) face.localRotation = Quaternion.Euler(0, 180, 0);
                ArtBox(face, "Sensor", new Vector3(0, 2.82f, -.25f), new Vector3(.25f, .075f, .05f), glow);
                ArtBox(face, "Door instruction strip", new Vector3(0, 2.64f, -.225f), new Vector3(2.8f, .105f, .025f), cream);
                Text(face, "AUTOMATIC / KEEP CLEAR", new Vector3(0, 2.64f, -.244f), .048f, dark.color, 2.65f);
            }
            SealArt(frame);
        }

        Transform DoorLeaf(Transform parent, int side)
        {
            var leaf = Group(side < 0 ? "Left opaque leaf" : "Right opaque leaf", parent, Vector3.zero);
            var body = leaf.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0, 1.39f, 0); body.size = new Vector3(1.62f, 2.78f, .14f);
            ArtBox(leaf, "Solid enamel door", new Vector3(0, 1.39f, 0), body.size, artMint, false);
            for (int face = -1; face <= 1; face += 2)
            {
                var front = Group("Door face " + face, leaf, Vector3.zero);
                if (face > 0) front.localRotation = Quaternion.Euler(0, 180, 0);
                ArtBox(front, "Inset porcelain panel", new Vector3(0, 1.38f, -.081f), new Vector3(1.40f, 2.35f, .012f), cream);
                ArtBox(front, "Kick plate", new Vector3(0, .28f, -.092f), new Vector3(1.39f, .28f, .012f), artTeal);
                ArtBox(front, "Science stripe", new Vector3(0, 1.21f, -.092f), new Vector3(1.39f, .19f, .012f), artTeal);
                // Opaque blue observation emblem: no transparent shortcut through the closed separation.
                Shape(front, "Round observation rim", PrimitiveType.Cylinder, new Vector3(0, 1.96f, -.09f), new Vector3(.69f, .016f, .69f), artTeal).localRotation = Quaternion.Euler(90, 0, 0);
                Shape(front, "Frosted blue inset", PrimitiveType.Cylinder, new Vector3(0, 1.96f, -.099f), new Vector3(.57f, .009f, .57f), artSky).localRotation = Quaternion.Euler(90, 0, 0);
                ArtBox(front, "Window shine", new Vector3(-.10f, 2.06f, -.108f), new Vector3(.065f, .18f, .003f), cream);
                Text(front, face < 0 ? "LAB" : "SHOP", new Vector3(0, 1.21f, -.108f), .065f, cream.color, 1.12f);
                for (int i = 0; i < 3; i++)
                    ArtBox(front, "Lower air vent", new Vector3(0, .62f + i * .085f, -.093f), new Vector3(.47f, .019f, .01f), artMint);
            }
            SealArt(leaf);
            return leaf;
        }
    }
}
