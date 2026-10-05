using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopWorld
    {
        // Original room dressing. This file creates no collider, station, inventory or light.
        // Every opaque group is merged once; its meshes join the existing artMeshes owner.
        Mesh aquaticFishRelief, aquaticLeafRelief, aquaticCircleRelief;

        void BuildAquaticRooms()
        {
            var shop = Group("Aquatic shop room identity", transform, Vector3.zero);
            AquaticPlankFloor(shop, -6.895f, 7, 1.97f);
            ArtBox(shop, "Coral welcome mat binding", new Vector3(0, .026f, -4.35f), new Vector3(2.75f, .008f, 1.02f), artCoral);
            var mat = Group("Fish shop welcome mat", shop, new Vector3(0, .033f, -4.35f));
            mat.localRotation = Quaternion.Euler(90, 0, 0);
            ArtBox(mat, "Teal woven mat", Vector3.zero, new Vector3(2.55f, .85f, .004f), artTeal);
            AquaticFish(mat, new Vector3(0, 0, -.005f), .66f, artPaleOak, false);
            for (int side = -1; side <= 1; side += 2)
            {
                // The full separating wall and door are owned by Room/Partition.
                // All panels stay outside X +/-1.75 below the door header.
                var partition = Group("Aquatic shop partition panel", shop, new Vector3(side * 4.25f, 0, .355f));
                ShopPanelling(partition, 5.18f);
                AquaticScenePanel(partition, new Vector3(0, 2.14f, -.095f), 4.68f, 1.72f,
                    side < 0 ? "CURIOUS AQUATIC LIFE" : "LITTLE UNDERWATER WORLDS", side > 0);

                var front = Group("Aquatic shop front window", shop, new Vector3(side * 3.75f, 0, -5.38f));
                front.localRotation = Quaternion.Euler(0, 180, 0);
                ShopPanelling(front, 4.76f);
                ArtWindow(front);
                AquaticCarePanel(front, new Vector3(side * 1.83f, 2.13f, -.09f), .82f);
                ArtShelfPlant(front, new Vector3(-side * 1.65f, 1.21f, -.08f), .42f);
            }
            Pendant(shop, new Vector3(-3.4f, 3.36f, -2.18f), artCoral);
            Pendant(shop, new Vector3(3.5f, 3.36f, -2.18f), artMint);
            SealArt(shop);

            // Aquarium backdrops are above the fixed tanks, not extra pretend stock tanks.
            var east = Group("Aquarium display wall", transform, new Vector3(6.81f, 0, -2.5f));
            east.localRotation = Quaternion.Euler(0, 90, 0);
            ShopPanelling(east, 5.75f);
            AquaticScenePanel(east, new Vector3(0, 2.99f, -.035f), 5.40f, 1.22f, "FISH  /  PLANTS  /  HAPPY HOMES", false);
            ArtBox(east, "Display wall honey picture rail", new Vector3(0, 3.76f, -.035f), new Vector3(5.78f, .085f, .12f), artPaleOak);
            SealArt(east);

            // This whole retail display follows the actual exterior west wall on expansion.
            var west = Group("Moving aquatic supplies wall", transform, new Vector3(-6.81f, 0, -2.5f));
            west.localRotation = Quaternion.Euler(0, -90, 0);
            ShopPanelling(west, 5.75f);
            AquaticSupplyWall(west);
            SealArt(west);
            west.SetParent(storeWestWall, true);

            BuildWorkingLabRoomArt();
        }

        void AquaticPlankFloor(Transform root, float left, int columns, float width)
        {
            for (int row = 0; row < 12; row++)
                for (int column = 0; column < columns; column++)
                {
                    float x = left + (column + .5f) * width;
                    float z = -5.23f + row * .475f;
                    ArtBox(root, "Honey retail floorboard", new Vector3(x, .016f, z), new Vector3(width - .018f, .012f, .461f),
                        (column + row) % 3 == 0 ? artPaleOak : artHoney, false);
                    if ((column + row) % 4 == 0)
                        ArtBox(root, "Woodgrain mark", new Vector3(x + .16f, .023f, z + .09f), new Vector3(.50f, .002f, .013f), artHoney, false);
                }
        }

        void AquaticScenePanel(Transform parent, Vector3 position, float width, float height, string title, bool planted)
        {
            var root = Group("Framed aquatic habitat illustration", parent, position);
            ArtBox(root, "Honey aquascape frame", Vector3.zero, new Vector3(width, height, .065f), artHoney);
            ArtBox(root, "Blue aquascape background", new Vector3(0, 0, -.043f), new Vector3(width - .13f, height - .12f, .030f), artSky);
            ArtBox(root, "Deep water lower band", new Vector3(0, -.20f * height, -.061f), new Vector3(width - .17f, height * .39f, .009f), artTeal);
            ArtBox(root, "Cream sand illustration", new Vector3(0, -.385f * height, -.073f), new Vector3(width - .18f, height * .10f, .012f), artPaleOak);
            ArtBox(root, "Aquatic panel heading", new Vector3(0, height * .335f, -.081f), new Vector3(width - .20f, height * .20f, .028f), cream);
            Text(root, title, new Vector3(0, height * .335f, -.10f), .071f, artTeal.color, width - .36f);
            float floor = -.33f * height;
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * width * .23f;
                AquaticPlantRelief(root, new Vector3(x, floor, -.086f), height * (i % 2 == 0 ? .30f : .43f), i % 2 == 0 ? artLeaf : artMint);
            }
            AquaticFish(root, new Vector3(-width * .21f, -.018f * height, -.107f), height * .36f, planted ? artCoral : cream, true);
            AquaticFish(root, new Vector3(width * .15f, -.11f * height, -.112f), height * .26f, planted ? artButter : artCoral, false);
            AquaticFish(root, new Vector3(width * .35f, .10f * height, -.106f), height * .17f, cream, true);
            for (int i = 0; i < 3; i++)
                AquaticRing(root, "Illustrated oxygen bubble", new Vector3(-width * .37f + i * .07f, -.03f + i * .11f, -.112f), .056f - i * .01f, .056f - i * .01f, cream);
        }

        void AquaticCarePanel(Transform parent, Vector3 position, float width)
        {
            var root = Group("Aquarium care guide", parent, position);
            ArtBox(root, "Care card frame", Vector3.zero, new Vector3(width, 1.28f, .07f), artTeal);
            ArtBox(root, "Care card paper", new Vector3(0, 0, -.045f), new Vector3(width - .085f, 1.18f, .025f), cream);
            Text(root, "FISH CARE", new Vector3(0, .43f, -.065f), .058f, artTeal.color, width - .13f);
            AquaticFish(root, new Vector3(0, .12f, -.075f), .28f, artTeal, false);
            AquaticPlantRelief(root, new Vector3(-.17f, -.36f, -.075f), .23f, artLeaf);
            AquaticRing(root, "Fresh water care symbol", new Vector3(.16f, -.22f, -.08f), .10f, .13f, artSky);
            Text(root, "ROOM TO SWIM", new Vector3(0, -.47f, -.067f), .028f, artTeal.color, width - .12f);
        }

        void AquaticSupplyWall(Transform root)
        {
            ArtBox(root, "Retail supplies cream backing", new Vector3(0, 2.42f, -.04f), new Vector3(5.31f, 1.66f, .07f), cream);
            ArtBox(root, "Retail supplies teal header", new Vector3(0, 3.31f, -.055f), new Vector3(5.38f, .39f, .085f), artTeal);
            Text(root, "AQUARIUM SUPPLIES", new Vector3(0, 3.31f, -.104f), .11f, cream.color, 4.98f);
            for (int column = -1; column <= 1; column++)
            {
                float x = column * 1.72f;
                ArtBox(root, "Supplies shelf", new Vector3(x, 2.54f, -.14f), new Vector3(1.54f, .095f, .34f), artPaleOak);
                ArtBox(root, "Shelf price strip", new Vector3(x, 2.46f, -.281f), new Vector3(1.48f, .07f, .025f), artTeal);
                Text(root, column < 0 ? "FISH FOOD" : column == 0 ? "WATER CARE" : "PLANTS & NETS", new Vector3(x, 1.93f, -.094f), .055f, artTeal.color, 1.40f);
                if (column < 1)
                    for (int item = 0; item < 3; item++)
                        AquaticSupplyJar(root, new Vector3(x + (item - 1) * .43f, 2.59f, -.16f), column == 0, item % 2 == 0 ? artCoral : artSky);
                else
                {
                    AquaticNet(root, new Vector3(x - .40f, 2.77f, -.14f), .44f);
                    AquaticPlantRelief(root, new Vector3(x + .10f, 2.62f, -.20f), .36f, artLeaf);
                    ArtBox(root, "Aquatic plant starter pot", new Vector3(x + .12f, 2.70f, -.17f), new Vector3(.25f, .20f, .17f), artCoral);
                    AquaticNet(root, new Vector3(x + .55f, 2.77f, -.14f), .34f);
                }
            }
            // Care symbols are shallow; the counter below stays usable and unobstructed.
            for (int i = -2; i <= 2; i++)
                AquaticFish(root, new Vector3(i * .93f, 1.69f, -.090f), .15f, i % 2 == 0 ? artSky : artCoral, i % 2 == 0);
        }

        void AquaticSupplyJar(Transform parent, Vector3 position, bool bottle, Material color)
        {
            var root = Group(bottle ? "Water-care bottle display" : "Fish-food tin display", parent, position);
            float height = bottle ? .38f : .27f;
            ArtBox(root, "Rounded product container", new Vector3(0, height * .5f, 0), new Vector3(.26f, height, .20f), color);
            ArtBox(root, "Opaque cream lid", new Vector3(0, height + .022f, 0), new Vector3(bottle ? .13f : .27f, .055f, bottle ? .13f : .21f), cream);
            ArtBox(root, "Product label", new Vector3(0, height * .51f, -.108f), new Vector3(.20f, .13f, .012f), cream);
            AquaticFish(root, new Vector3(0, height * .51f, -.117f), .073f, artTeal, false);
        }

        void AquaticNet(Transform root, Vector3 position, float size)
        {
            AquaticRing(root, "Little aquarium net hoop", position, size * .43f, size * .34f, artTeal);
            ArtBox(root, "Aquarium net handle", position + new Vector3(0, -size * .41f, .005f), new Vector3(.032f, size * .47f, .025f), artHoney);
            for (int i = -1; i <= 1; i++)
                ArtBox(root, "Net fabric line", position + new Vector3(i * size * .10f, 0, .011f), new Vector3(.008f, size * .56f, .008f), artSky, false);
        }

        void BuildWorkingLabRoomArt()
        {
            var room = Group("Working laboratory interior", transform, Vector3.zero);
            WorkingLabFloor(room, .99f, 6);
            ArtBox(room, "Lab enamel door threshold", new Vector3(0, .029f, .5f), new Vector3(3.16f, .008f, .22f), artCoral, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var partition = Group("Scientific lab partition face", room, new Vector3(side * 4.27f, 0, .66f));
                partition.localRotation = Quaternion.Euler(0, 180, 0);
                WorkingLabTiles(partition, 5.16f);
                LabBlueprintPanel(partition, new Vector3(0, 2.66f, -.058f), 4.64f, 1.52f,
                    side < 0 ? "OBSERVE  /  RECORD  /  DISCOVER" : "MEASURE  /  CULTURE  /  HATCH", side > 0);

                var wall = Group("Laboratory side wall services", room, new Vector3(side * 6.81f, 0, 3.0f));
                wall.localRotation = Quaternion.Euler(0, side * 90, 0);
                WorkingLabTiles(wall, 4.73f);
                LabSpecimenShelf(wall, new Vector3(0, 2.88f, -.045f), 3.66f, side < 0 ? "CLEAN TOOLS" : "CULTURE ARCHIVE", side < 0);
                WorkingLabDuct(room, side * 6.55f, .86f, 5.22f);
            }
            SealArt(room);

            var rear = Group("Moving laboratory science wall", labRearAssembly, Vector3.zero);
            var tiles = Group("Rear laboratory tile field", rear, new Vector3(0, 0, 5.33f));
            WorkingLabTiles(tiles, 13.48f);
            for (int side = -1; side <= 1; side += 2)
            {
                LabSpecimenShelf(rear, new Vector3(side * 4.98f, 2.83f, 5.30f), 3.32f,
                    side < 0 ? "SPECIMEN NOTES" : "REAGENT BANK", side < 0);
                ArtPipe(rear, new Vector3(side * 6.58f, 2.41f, 5.23f), new Vector3(side * 6.58f, 3.87f, 5.23f), side < 0 ? artCoral : artButter);
                ArtPipe(rear, new Vector3(side * 6.58f, 3.87f, 5.23f), new Vector3(side * .40f, 3.87f, 5.23f), side < 0 ? artCoral : artButter);
                ArtGauge(rear, new Vector3(side * 6.53f, 3.34f, 5.12f), .60f);
            }
            // The existing central THE CURIOSITY LAB title remains fully exposed.
            SealArt(rear);
        }

        void WorkingLabFloor(Transform root, float firstZ, int rows)
        {
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < 17; column++)
                    ArtBox(root, "Laboratory checker tile", new Vector3(-6.40f + column * .80f, .020f, firstZ + row * .80f), new Vector3(.78f, .008f, .78f),
                        (column + row) % 2 == 0 ? artTile : cream, false);
        }

        void WorkingLabTiles(Transform root, float width)
        {
            ArtBox(root, "Clean pale laboratory splashback", new Vector3(0, 1.06f, .014f), new Vector3(width, 2.09f, .036f), artTile, false);
            int divisions = Mathf.FloorToInt(width / .72f);
            for (int i = 0; i <= divisions; i++)
                ArtBox(root, "Vertical ceramic joint", new Vector3(-width * .5f + i * width / divisions, 1.08f, -.009f), new Vector3(.015f, 2.05f, .008f), cream, false);
            for (int row = 1; row <= 4; row++)
                ArtBox(root, "Horizontal ceramic joint", new Vector3(0, row * .43f, -.012f), new Vector3(width, .015f, .008f), cream, false);
            ArtBox(root, "Mint laboratory chair rail", new Vector3(0, 2.16f, -.024f), new Vector3(width, .095f, .065f), artMint);
            ArtBox(root, "Teal sanitary skirting", new Vector3(0, .09f, -.012f), new Vector3(width, .15f, .057f), artTeal);
        }

        void LabBlueprintPanel(Transform parent, Vector3 position, float width, float height, string heading, bool hatchery)
        {
            var root = Group("Original laboratory process blueprint", parent, position);
            ArtBox(root, "Blueprint cream frame", Vector3.zero, new Vector3(width, height, .065f), cream);
            ArtBox(root, "Blueprint teal paper", new Vector3(0, 0, -.043f), new Vector3(width - .12f, height - .12f, .024f), artTeal);
            Text(root, heading, new Vector3(0, height * .335f, -.066f), .074f, cream.color, width - .30f);
            for (int item = -1; item <= 1; item++)
            {
                float x = item * width * .285f;
                ArtBox(root, "Blueprint ruled baseline", new Vector3(x, -height * .31f, -.062f), new Vector3(width * .21f, .018f, .008f), artSky, false);
                if (item < 0)
                {
                    AquaticRing(root, "Culture diagram outer", new Vector3(x, -.03f, -.073f), .22f, .28f, artSky);
                    AquaticRing(root, "Culture diagram inner", new Vector3(x + .02f, -.02f, -.084f), .083f, .083f, cream);
                    AquaticRelief(root, "Culture cell", AquaticLeafMesh(), new Vector3(x - .074f, -.085f, -.081f), new Vector3(.12f, .14f, .022f), artMint);
                }
                else if (item == 0)
                {
                    ArtBox(root, "Blueprint vessel", new Vector3(x, -.01f, -.067f), new Vector3(.30f, .49f, .012f), artSky);
                    ArtBox(root, "Blueprint vessel interior", new Vector3(x, -.01f, -.080f), new Vector3(.24f, .405f, .009f), artTeal);
                    for (int cell = -1; cell <= 1; cell++)
                        AquaticRing(root, "Blueprint suspension cell", new Vector3(x + cell * .054f, cell * .09f, -.090f), .052f, .052f, cream);
                }
                else
                {
                    AquaticFish(root, new Vector3(x, -.03f, -.083f), .34f, hatchery ? artCoral : artMint, false);
                    if (hatchery) AquaticRing(root, "Hatchery diagram window", new Vector3(x, -.03f, -.090f), .37f, .31f, artSky);
                }
                if (item < 1)
                {
                    float arrowX = x + width * .143f;
                    ArtBox(root, "Process arrow shaft", new Vector3(arrowX, -.03f, -.076f), new Vector3(.21f, .021f, .010f), artSky, false);
                    var upper = ArtBox(root, "Process arrow upper", new Vector3(arrowX + .09f, .005f, -.077f), new Vector3(.09f, .021f, .010f), artSky, false);
                    upper.localRotation = Quaternion.Euler(0, 0, -40);
                    var lower = ArtBox(root, "Process arrow lower", new Vector3(arrowX + .09f, -.065f, -.077f), new Vector3(.09f, .021f, .010f), artSky, false);
                    lower.localRotation = Quaternion.Euler(0, 0, 40);
                }
            }
        }

        void LabSpecimenShelf(Transform parent, Vector3 position, float width, string heading, bool instruments)
        {
            var root = Group("Ordered laboratory wall station", parent, position);
            ArtBox(root, "Lab storage enamel panel", Vector3.zero, new Vector3(width, 1.00f, .060f), cream);
            ArtBox(root, "Lab storage mint inset", new Vector3(0, -.07f, -.039f), new Vector3(width - .15f, .70f, .021f), artMint);
            ArtBox(root, "Laboratory shelf lip", new Vector3(0, -.35f, -.16f), new Vector3(width - .13f, .075f, .32f), artTeal);
            Text(root, heading, new Vector3(0, .345f, -.051f), .068f, artTeal.color, width - .25f);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * (width - .52f) / 5;
                if (instruments && i == 2)
                {
                    AquaticRing(root, "Safety goggles left lens", new Vector3(x - .115f, -.07f, -.090f), .091f, .076f, artTeal);
                    AquaticRing(root, "Safety goggles right lens", new Vector3(x + .115f, -.07f, -.090f), .091f, .076f, artTeal);
                    ArtBox(root, "Safety goggles bridge", new Vector3(x, -.055f, -.092f), new Vector3(.063f, .026f, .018f), artTeal);
                }
                else
                {
                    float height = i % 2 == 0 ? .41f : .31f;
                    ArtBox(root, "Rounded specimen vial", new Vector3(x, -.30f + height * .5f, -.142f), new Vector3(.23f, height, .16f), i % 2 == 0 ? artSky : artCoral);
                    ArtBox(root, "Specimen vial cap", new Vector3(x, -.30f + height + .024f, -.142f), new Vector3(.25f, .048f, .17f), artTeal);
                    ArtBox(root, "Numbered specimen paper", new Vector3(x, -.12f, -.228f), new Vector3(.17f, .115f, .012f), cream);
                    // Short bars read as catalogue marks without pretending to be live inventory.
                    for (int mark = 0; mark < 2; mark++)
                        ArtBox(root, "Vial catalogue mark", new Vector3(x - .040f + mark * .060f, -.12f, -.236f), new Vector3(.027f, .065f, .004f), artTeal, false);
                }
            }
        }

        void WorkingLabDuct(Transform root, float x, float startZ, float endZ)
        {
            float length = endZ - startZ;
            ArtBox(root, "Overhead lab cable duct", new Vector3(x, 3.84f, (startZ + endZ) * .5f), new Vector3(.24f, .16f, length), cream);
            ArtPipe(root, new Vector3(x - Mathf.Sign(x) * .29f, 3.72f, startZ), new Vector3(x - Mathf.Sign(x) * .29f, 3.72f, endZ), x < 0 ? artCoral : artButter);
            int straps = Mathf.Max(2, Mathf.CeilToInt(length / 1.25f));
            for (int i = 0; i < straps; i++)
                ArtBox(root, "Duct fastening strap", new Vector3(x, 3.835f, Mathf.Lerp(startZ + .15f, endZ - .15f, i / (float)(straps - 1))), new Vector3(.28f, .185f, .056f), artTeal);
        }

        // Root calls each method once at the end of its existing wing builder.
        // Roots remain controlled by the existing expansion activation/state code.
        void DecorateAquaticStoreWing(Transform root, int index)
        {
            float x = -9 - index * 4;
            var oldRunner = root.Find("Gallery floor accent");
            if (oldRunner) oldRunner.gameObject.SetActive(false);
            var art = Group("Aquatic gallery extension art", root, Vector3.zero);
            AquaticPlankFloor(art, x - 2, 2, 2);
            var rear = Group("Gallery aquatic rear wall", art, new Vector3(x, 0, .355f));
            ShopPanelling(rear, 3.90f);
            AquaticScenePanel(rear, new Vector3(0, 2.04f, -.074f), 3.66f, 1.38f, "AQUATIC DISCOVERIES", index == 1);
            var front = Group("Gallery aquatic front wall", art, new Vector3(x, 0, -5.38f));
            front.localRotation = Quaternion.Euler(0, 180, 0);
            ShopPanelling(front, 3.90f);
            AquaticScenePanel(front, new Vector3(0, 2.43f, -.070f), 3.66f, 1.83f, "FIND A LITTLE WONDER", index == 0);
            SealArt(art);
        }

        void DecorateWorkingLabWing(Transform root, int index)
        {
            float z = 7.5f + index * 4;
            var oldArea = root.Find("Expanded lab work area");
            if (oldArea) oldArea.gameObject.SetActive(false);
            var art = Group("Working laboratory extension art", root, Vector3.zero);
            WorkingLabFloor(art, z - 1.60f, 5);
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = Group("Expanded laboratory ceramic wall", art, new Vector3(side * 6.81f, 0, z));
                wall.localRotation = Quaternion.Euler(0, side * 90, 0);
                WorkingLabTiles(wall, 3.90f);
                LabSpecimenShelf(wall, new Vector3(0, 2.90f, -.045f), 3.35f, side < 0 ? "OBSERVE & RECORD" : "CULTURE SUPPLIES", side < 0);
                WorkingLabDuct(art, side * 6.55f, z - 1.93f, z + 1.93f);
            }
            SealArt(art);
        }

        void AquaticFish(Transform parent, Vector3 position, float size, Material material, bool reverse)
        {
            var root = Group("Original fish relief", parent, position);
            if (reverse) root.localRotation = Quaternion.Euler(0, 0, 180);
            AquaticRelief(root, "Rounded fish silhouette", AquaticFishMesh(), Vector3.zero, new Vector3(size, size, .021f), material);
            AquaticRing(root, "Fish illustration eye", new Vector3(size * .35f, size * (reverse ? -.065f : .065f), -.014f), size * .032f, size * .032f, artTeal);
        }

        void AquaticPlantRelief(Transform parent, Vector3 position, float height, Material material)
        {
            ArtBox(parent, "Aquatic plant stem", position + new Vector3(0, height * .40f, 0), new Vector3(.025f, height * .80f, .012f), material, false);
            for (int i = 0; i < 3; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var leaf = AquaticRelief(parent, "Aquatic leaf", AquaticLeafMesh(), position + new Vector3(side * height * .11f, height * (.28f + i * .20f), -.006f), new Vector3(height * .25f, height * .47f, .018f), material);
                leaf.localRotation = Quaternion.Euler(0, 0, side * -35);
            }
        }

        Transform AquaticRelief(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Material material)
        {
            var root = Group(name, parent, position); root.localScale = scale;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return root;
        }

        Transform AquaticRing(Transform parent, string name, Vector3 position, float radiusX, float radiusY, Material material)
        {
            if (!aquaticCircleRelief)
            {
                const int segments = 24;
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments;
                    vertices.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0));
                    vertices.Add(new Vector3(Mathf.Cos(a) * .79f, Mathf.Sin(a) * .79f, 0));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = i * 2, b = ((i + 1) % segments) * 2;
                    triangles.Add(a); triangles.Add(b + 1); triangles.Add(b);
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b + 1);
                }
                aquaticCircleRelief = new Mesh { name = "Owned aquatic diagram ring" };
                aquaticCircleRelief.SetVertices(vertices); aquaticCircleRelief.SetTriangles(triangles, 0); aquaticCircleRelief.RecalculateNormals(); aquaticCircleRelief.RecalculateBounds();
                artMeshes.Add(aquaticCircleRelief);
            }
            return AquaticRelief(parent, name, aquaticCircleRelief, position, new Vector3(radiusX, radiusY, 1), material);
        }

        Mesh AquaticFishMesh()
        {
            if (aquaticFishRelief) return aquaticFishRelief;
            var body = new Vector2[18];
            for (int i = 0; i < body.Length; i++)
            {
                float a = i * Mathf.PI * 2 / body.Length;
                body[i] = new Vector2(Mathf.Cos(a) * .52f, Mathf.Sin(a) * .30f);
            }
            aquaticFishRelief = AquaticConvexRelief("Owned rounded fish relief", body,
                new[] { new Vector2(-.35f, 0), new Vector2(-.79f, .28f), new Vector2(-.79f, -.28f) });
            return aquaticFishRelief;
        }

        Mesh AquaticLeafMesh()
        {
            if (aquaticLeafRelief) return aquaticLeafRelief;
            aquaticLeafRelief = AquaticConvexRelief("Owned aquatic leaf relief", new[]
            {
                new Vector2(0, -.50f), new Vector2(.43f, -.15f), new Vector2(.46f, .17f),
                new Vector2(0, .50f), new Vector2(-.46f, .17f), new Vector2(-.43f, -.15f)
            });
            return aquaticLeafRelief;
        }

        Mesh AquaticConvexRelief(string name, params Vector2[][] outlines)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var outline in outlines)
            {
                int start = vertices.Count, count = outline.Length;
                Vector2 center = Vector2.zero; foreach (var point in outline) center += point; center /= count;
                vertices.Add(new Vector3(center.x, center.y, -.5f));
                foreach (var point in outline) vertices.Add(new Vector3(point.x, point.y, -.5f));
                vertices.Add(new Vector3(center.x, center.y, .5f));
                foreach (var point in outline) vertices.Add(new Vector3(point.x, point.y, .5f));
                for (int i = 0; i < count; i++)
                {
                    int a = start + 1 + i, b = start + 1 + (i + 1) % count;
                    int backA = a + count + 1, backB = b + count + 1;
                    triangles.Add(start); triangles.Add(b); triangles.Add(a);
                    triangles.Add(start + count + 1); triangles.Add(backA); triangles.Add(backB);
                    triangles.Add(a); triangles.Add(b); triangles.Add(backA);
                    triangles.Add(backA); triangles.Add(b); triangles.Add(backB);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); artMeshes.Add(mesh);
            return mesh;
        }
    }
}
