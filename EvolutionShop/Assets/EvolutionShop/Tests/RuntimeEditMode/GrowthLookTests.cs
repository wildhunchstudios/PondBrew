using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-16 waves 1 to 4: the growth kit (Tools/Art/create_growth_kit.py) against the game. Every piece loads as one mesh
    /// whose material slots the look maps; the manifest lists them all with the palette's colours; each station's piece stays
    /// within its station's footprint, so the dressing never reaches into an aisle or a neighbour; and each room's dressing
    /// stays inside its room.</summary>
    public sealed class GrowthLookTests
    {
        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        static string Manifest => File.ReadAllText(Path.Combine(Repo, "ArtSource", "GrowthKit", "manifest.json"));

        static readonly string[] Pieces =
        {
            "Grower", "GrowerRing", "GrowerTap", "GrowerLid", "Incubator", "IncubatorScreen", "IncubatorPort", "IncubatorDoser",
            "TankBackdrop", "TankBackdropLarge", "DecorStones", "DecorFern", "DecorRocks", "DecorBamboo", "DecorShells", "DecorDriftwood",
            "TankStarter", "TankBed", "TankFilter", "TankLid", "Counter", "CounterBags", "CounterReader", "CounterTill",
            "UnitAFloor", "UnitADressing", "Street", "BackRoomFloor", "BackRoomDressing",
            "Vat", "VatBatch", "VatStirrer", "BreedingTank", "BreedingPort", "BreedingPair", "Bench", "BenchMicroscope", "BenchAnalyzer",
            "BenchPlanner", "SiphonBulb", "SiphonBig", "SiphonTwin", "SiphonVacuum", "VialShelf", "VialBottle", "Catalog", "Pinboard",
            "TankLarge", "TankBedLarge", "TankLidLarge", "TankShow", "TankBedShow", "TankSpotlight", "TankRack", "TankLidRack",
            "GenCrank", "GenCrankWheel", "GenTurbine", "GenTwin", "GenTide", "PumpHand", "PumpHandWheel", "PumpMotor", "Buffer",
            "BufferTag1", "BufferTag2", "BufferTag3", "BufferTag4", "BufferTag5", "BufferTag6", "PowerBoard", "Sorter", "IncubatorOutlet",
            "PipeGallery", "WorkshopDome", "AnnexFloor", "AnnexDressing", "WorkshopFloor", "WorkshopDressing",
            "ResearchFloor", "ResearchDressing", "ResearchTank", "ResearchJars", "ResearchDesk", "ResearchTray", "PartitionResearch",
            "UnitBFloor", "UnitBDressing", "StreetEast", "BayWindow", "WindowDisplay", "UnitCFloor", "UnitCDressing", "FeatureWall", "FeatureTank",
            "PartitionB", "PartitionC", "PartitionAnnex", "PartitionWorkshop", "BannerCoral", "BannerYellow", "BannerBlue", "HallSpot",
            "ExhibitionSign", "Award", "DisplayTag", "StaffBoard", "HallBoard",
            "DecorReeds", "DecorLilyLamp", "DecorWatermill", "DecorVolcano", "DecorLantern", "DecorDuckSpa", "DecorLighthouse", "DecorChest", "DecorCoralArch",
            "SiphonBin", "DecorDuck", "JarShelf", "SpecimenJar", "SecondLabFloor", "SecondLabDressing", "PartitionLab2",
        };

        static GameObject Piece(string name) => Resources.Load<GameObject>(GrowthWorld.KitPath + name);

        static Bounds MeshBounds(GameObject piece)
        {
            var filters = piece.GetComponentsInChildren<MeshFilter>(true);
            var b = new Bounds();
            bool first = true;
            foreach (var f in filters)
            {
                var m = f.sharedMesh;
                var local = m.bounds;
                // The kit's meshes sit directly under the root at its origin (the export's convention).
                var t = f.transform;
                foreach (var corner in new[] { local.min, local.max, new Vector3(local.min.x, local.min.y, local.max.z), new Vector3(local.max.x, local.max.y, local.min.z) })
                {
                    var p = piece.transform.InverseTransformPoint(t.TransformPoint(corner));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>A piece's vertices in its own space (the kit's one mesh under its root).</summary>
        static Vector3[] Vertices(string name)
        {
            var piece = Piece(name);
            var filter = piece.GetComponentInChildren<MeshFilter>(true);
            var toPiece = piece.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            return filter.sharedMesh.vertices.Select(v => toPiece.MultiplyPoint3x4(v)).ToArray();
        }

        /// <summary>GR-19, in the water since tester round 2 (F67, F85): every piece of decor is the kit's, stands on its base (its
        /// origin at the middle of its bottom), within 0.36 m across and 0.56 m tall; in every shop tank it stands on the gravel
        /// at the back inside the water, scaled no smaller than 0.75 and with its top under the surface, and the creatures keep
        /// in front of it.</summary>
        [Test] public void EveryPieceOfDecorStandsInEveryShopTanksWater()
        {
            Assert.That(EvolutionShop.Growth.GrowthContent.Decor.Select(d => d.Asset), Is.SubsetOf(Pieces));
            foreach (var d in EvolutionShop.Growth.GrowthContent.Decor)
            {
                var b = MeshBounds(Piece(d.Asset));
                Assert.That(b.min.y, Is.GreaterThan(-0.002f), d.Asset + " stands on its base");
                Assert.That(b.max.y, Is.LessThan(0.56f), d.Asset + " is at most 0.56 m tall");
                Assert.That(Mathf.Max(-b.min.x, b.max.x, -b.min.z, b.max.z), Is.LessThan(0.18f), d.Asset + " is within 0.36 m across");
            }
            foreach (var (kind, stand, glass) in new[] { ("starter", 0.9f, 0.62f), ("large", 0.8f, 0.75f), ("show", 0.55f, 1.0f) })
            {
                var inner = GrowthWorld.WaterOf(kind);
                float waterH = glass - 0.12f;
                var at = GrowthWorld.DecorAt(kind, stand);
                Assert.That(at.z, Is.LessThan(0), kind + ": at the back");
                Assert.That(at.y, Is.EqualTo(stand + 0.055f).Within(1e-4f), kind + ": on the gravel");
                foreach (var d in EvolutionShop.Growth.GrowthContent.Decor)
                {
                    float h = MeshBounds(Piece(d.Asset)).max.y, s = GrowthWorld.DecorScale(h, waterH);
                    Assert.That(s, Is.GreaterThanOrEqualTo(0.75f), kind + ", " + d.Asset + ": no smaller than three quarters");
                    Assert.That(Mathf.Abs(at.x) + 0.18f * s, Is.LessThanOrEqualTo(inner.x * 0.5f + 1e-4f), kind + ", " + d.Asset + ": inside the water, side to side");
                    Assert.That(-at.z + 0.18f * s, Is.LessThanOrEqualTo(inner.z * 0.5f + 1e-4f), kind + ", " + d.Asset + ": inside the water at the back");
                    Assert.That(at.y + h * s, Is.LessThanOrEqualTo(stand + 0.05f + waterH - 0.04f + 1e-4f), kind + ", " + d.Asset + ": its top under the surface");
                    float back = GrowthWorld.DecorBack(at, s, inner.z);
                    Assert.That(-inner.z * 0.5f + back, Is.GreaterThanOrEqualTo(at.z + 0.17f * s - 1e-4f), kind + ", " + d.Asset + ": the creatures keep in front of it");
                    Assert.That(back, Is.LessThan(inner.z * 0.6f), kind + ", " + d.Asset + ": and still have most of the water");
                }
            }
        }

        [Test] public void EveryPieceLoadsAsOneMeshWhoseSlotsTheLookMaps()
        {
            foreach (var name in Pieces)
            {
                var piece = Piece(name);
                Assert.That(piece, Is.Not.Null, name + " loads from Resources/" + GrowthWorld.KitPath);
                var renderers = piece.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(1), name + " is one joined mesh");
                foreach (var m in renderers[0].sharedMaterials)
                    Assert.That(GrowthWorld.LookSlots.ContainsKey(m.name), Is.True, name + "'s slot " + m.name + " is mapped by the look");
                Assert.That(piece.GetComponentsInChildren<Collider>(true), Is.Empty, name + " brings no collider");
            }
        }

        [Test] public void TheManifestListsEveryPieceAndThePalettesColours()
        {
            string manifest = Manifest;
            foreach (var name in Pieces) StringAssert.Contains("\"id\": \"" + name + "\"", manifest);
            foreach (var kv in GrowthWorld.LookSlots)
            {
                if (kv.Value.kind == 'c' || kv.Value.kind == 'x') continue; // the culture's own colour, the look's glass
                var match = Regex.Match(manifest, "\"" + kv.Key + "\": \\{\\s*\"srgb_hex\": \"(#[0-9A-F]{6})\"");
                Assert.That(match.Success, Is.True, kv.Key + " is in the kit's palette");
                Assert.That(match.Groups[1].Value, Is.EqualTo(kv.Value.hex), kv.Key + " has the kit's colour");
            }
        }

        /// <summary>Each station's piece within its greybox footprint (GrowthWorld's colliders, half widths), a little leeway for
        /// a gauge, a frame or a tap; the incubator's front is -Z, the others' +Z.</summary>
        [TestCase("Grower", 0.425f, 0.375f, 0.04f)]
        [TestCase("GrowerTap", 0.425f, 0.46f, 0.0f)]
        [TestCase("Incubator", 0.475f, 0.4f, 0.0f)]
        [TestCase("Counter", 1.05f, 0.375f, 0.02f)]
        [TestCase("TankStarter", 0.75f, 0.5f, 0.07f)]
        [TestCase("TankBed", 0.75f, 0.5f, 0.0f)]
        [TestCase("Vat", 0.425f, 0.375f, 0.015f)]
        [TestCase("VatStirrer", 0.425f, 0.375f, 0.0f)]
        [TestCase("BreedingTank", 0.56f, 0.26f, 0.035f)]
        [TestCase("BreedingPort", 0.56f, 0.26f, 0.035f)]
        [TestCase("BreedingPair", 0.56f, 0.26f, 0.035f)]
        [TestCase("Bench", 0.675f, 0.325f, 0.01f)]
        [TestCase("BenchMicroscope", 0.675f, 0.325f, 0.0f)]
        [TestCase("BenchAnalyzer", 0.675f, 0.325f, 0.0f)]
        [TestCase("BenchPlanner", 0.675f, 0.325f, 0.0f)]
        [TestCase("SiphonBulb", 0.675f, 0.325f, 0.0f)]
        [TestCase("SiphonBig", 0.675f, 0.325f, 0.0f)]
        [TestCase("SiphonTwin", 0.675f, 0.325f, 0.0f)]
        [TestCase("SiphonVacuum", 0.675f, 0.325f, 0.0f)]
        [TestCase("Catalog", 0.25f, 0.2f, 0.0f)]
        [TestCase("TankLarge", 1.25f, 0.5f, 0.07f)]
        [TestCase("TankBedLarge", 1.25f, 0.5f, 0.0f)]
        [TestCase("TankLidLarge", 1.25f, 0.5f, 0.03f)]
        [TestCase("TankShow", 1.5f, 0.75f, 0.07f)]
        [TestCase("TankBedShow", 1.5f, 0.75f, 0.0f)]
        [TestCase("TankRack", 0.75f, 0.5f, 0.03f)]
        [TestCase("TankLidRack", 0.75f, 0.5f, 0.03f)]
        [TestCase("GenCrank", 0.55f, 0.45f, 0.0f)]
        [TestCase("GenTurbine", 0.55f, 0.45f, 0.0f)]
        [TestCase("Buffer", 0.21f, 0.21f, 0.0f)]
        [TestCase("IncubatorOutlet", 0.1f, 0.12f, 0.0f)]
        public void EachStationsPieceStaysWithinItsFootprint(string name, float halfX, float halfZ, float leeway)
        {
            var b = MeshBounds(Piece(name));
            Assert.That(b.max.x, Is.LessThanOrEqualTo(halfX + leeway), name + " east");
            Assert.That(-b.min.x, Is.LessThanOrEqualTo(halfX + leeway), name + " west");
            Assert.That(b.max.z, Is.LessThanOrEqualTo(halfZ + leeway), name + " front");
            Assert.That(-b.min.z, Is.LessThanOrEqualTo(halfZ + leeway), name + " back");
            Assert.That(b.min.y, Is.GreaterThanOrEqualTo(-0.01f), name + " stands on the floor");
        }

        /// <summary>The pieces that sit beside or above their station rather than inside its footprint.</summary>
        [Test] public void WaveTwosOtherPiecesStayWhereTheyBelong()
        {
            // The vat's second batch stands beside it (local +X), clear of the next source along the wall (1.2 m away).
            var batch = MeshBounds(Piece("VatBatch"));
            Assert.That(batch.max.x, Is.LessThanOrEqualTo(1.2f - 0.425f), "the second batch stays clear of the next source");
            Assert.That(batch.min.y, Is.GreaterThanOrEqualTo(-0.01f), "the second batch stands on the floor");
            // The Show tank's spotlight hangs above the aisle in front of it, over head height and under unit B's 3.2 m ceiling.
            var spot = MeshBounds(Piece("TankSpotlight"));
            Assert.That(spot.min.y, Is.GreaterThanOrEqualTo(2.4f), "the spotlight hangs over head height");
            Assert.That(spot.max.y, Is.LessThanOrEqualTo(3.2f + 0.01f), "the spotlight hangs from the ceiling");
            // A vial's bottle on its place on the shelf, inside the shelf's depth, a bottle's width from its neighbours.
            var bottle = MeshBounds(Piece("VialBottle"));
            Assert.That(bottle.max.x - bottle.min.x, Is.LessThanOrEqualTo(0.25f - 0.05f), "a bottle leaves room beside the next");
            Assert.That(bottle.min.y, Is.GreaterThanOrEqualTo(-0.01f), "a bottle stands on the shelf");
            var shelf = MeshBounds(Piece("VialShelf"));
            Assert.That(shelf.min.z, Is.GreaterThanOrEqualTo(-0.15f - 0.01f), "the shelf stays in front of the wall");
            // The pinboard lies on its wall: its back no deeper than the wall's face (0.04 m behind its centre), its top under 3.6 m.
            var board = MeshBounds(Piece("Pinboard"));
            Assert.That(board.max.z, Is.LessThanOrEqualTo(0.04f), "the pinboard stays in front of the wall");
            Assert.That(board.max.y + GrowthWorld.BoardPosition.y, Is.LessThanOrEqualTo(3.6f), "the pinboard stays under the ceiling");
        }

        /// <summary>GR-16's start point for the user's look (Play-GR16-look.cmd): every station the look dresses at its top level,
        /// the vat, five vials, a Large tank at 3, a Show tank at 2, the workshop with lab racks at 3 and 1, its own save, unguided.</summary>
        [Test] public void TheLookStartHasEveryDressedStationAndTankAtItsTopLevel()
        {
            var shop = GrowthStarts.Make(GrowthStarts.Look);
            Assert.That(shop, Is.Not.Null);
            foreach (var (station, level) in new[] { ("grower", 4), ("incubator", 4), ("counter", 4), ("breeding", 3), ("bench", 4), ("siphon", 4), ("vat", 3) })
                Assert.That(shop.Level(station), Is.EqualTo(level), station + " at its top level");
            Assert.That(shop.Owns("mist") && shop.Workshop && shop.UnitB, Is.True, "Mist owned, the workshop and unit B open");
            Assert.That(shop.Vials.Count(v => v.Value > 0), Is.EqualTo(5), "five vials on the shelf");
            foreach (var (place, kind, level) in new[] { ("a3", "large", 3), ("b4", "show", 2), ("r1", "rack", 3), ("r2", "rack", 1) })
            {
                var c = shop.ContainerAt(place);
                Assert.That(c != null && c.Kind == kind && c.Level == level, Is.True, kind + " on " + place + " at level " + level);
            }
            Assert.That(shop.ContainerAt("b4").Occupants.Count, Is.GreaterThan(0), "the Show tank has creatures");
            Assert.That(shop.Level("generator") == 4 && shop.Level("pump") == 3 && shop.HasSorter, Is.True, "wave 3: the tide engine, fast pumps and the sorter");
            Assert.That(shop.Buffers.Count >= 3 && shop.Lines.Count() >= 3, Is.True, "three buffers, each piped from a grower");
            Assert.That(shop.Chute != null && shop.Pipes.Any(p => p.To == GrowthLab.Port), Is.True, "the sorter's chute and the auto-doser's port pipe");
            Assert.That(shop.UnitC && shop.Hall, Is.True, "wave 4: unit C and the exhibition hall open");
            foreach (var (place, kind, level) in new[] { ("c1", "show", 2), ("c2", "show", 1), ("e1", "large", 3) })
            {
                var c = shop.ContainerAt(place);
                Assert.That(c != null && c.Kind == kind && c.Level == level && c.Occupants.Count > 0, Is.True, kind + " on " + place + " at level " + level + ", with creatures");
            }
            Assert.That(shop.Guide, Is.EqualTo(GrowthGuideMode.Off), "a start point is never guided");
            Assert.That(shop.Snapshot(), Is.Not.Null, "it saves");
        }

        /// <summary>Wave 3's machines where their greybox has them: the generator's turbines in the workshop's north-east corner, its
        /// back clear of the north wall; the sorter against that wall; the crank's and the hand pump's wheels round their axles
        /// (they turn); the pumps on a grower's front; the power board on its wall; buffers up to the inlet the pipe drops to.</summary>
        [Test] public void WaveThreesMachinesStayWhereTheyBelong()
        {
            foreach (var name in new[] { "GenTwin", "GenTide" })
            {
                var b = MeshBounds(Piece(name));
                Assert.That(-b.min.z, Is.LessThanOrEqualTo(14.9f - 14.35f), name + " clear of the workshop's north wall");
                Assert.That(b.max.x - b.min.x, Is.LessThanOrEqualTo(1.8f), name + " within the workshop's corner");
            }
            var sorter = MeshBounds(Piece("Sorter"));
            Assert.That(-sorter.min.z, Is.LessThanOrEqualTo(0.45f), "the sorter's back within its footprint, clear of the wall");
            Assert.That(sorter.max.x, Is.LessThanOrEqualTo(0.85f), "the sorter within its footprint's width");
            foreach (var name in new[] { "GenCrankWheel", "PumpHandWheel" })
            {
                var b = MeshBounds(Piece(name));
                Assert.That(Mathf.Abs(b.center.y) < 0.02f && Mathf.Abs(b.min.x + b.max.x) < 0.05f, Is.True, name + " centred on its axle, so it turns in place");
            }
            foreach (var name in new[] { "PumpHand", "PumpMotor" })
            {
                var b = MeshBounds(Piece(name));
                // GR-24: its bracket reaches back to the cabinet's front (the pump stands at 0.44, the cabinet's front at 0.35;
                // it stood 2 cm off it), never into it.
                Assert.That(b.max.x <= 0.15f && -b.min.z <= 0.09f + 0.001f, Is.True, name + " on the grower's front, not into it");
            }
            Assert.That(MeshBounds(Piece("PowerBoard")).max.z, Is.LessThanOrEqualTo(0.031f), "the power board on its wall (its back where the greybox board's is)");
            var buffer = MeshBounds(Piece("Buffer"));
            Assert.That(buffer.max.y, Is.LessThanOrEqualTo(GrowthLab.Height(GrowthLab.Buffer(0)) + 0.03f), "a buffer's inlet where its pipe drops to");
        }

        /// <summary>No room's dressing reaches into a doorway (GR-16 wave 3 found wave 1's back room panels across the doorway to the
        /// lab annex): no part of it between a doorway's posts, from the floor's skirting to under its head, either side of its wall.</summary>
        [Test] public void NoRoomsDressingCrossesADoorway()
        {
            // Each doorway: its centre on the floor plane, whether its wall runs along x, and its width (GrowthWorld.Rooms, AnnexRoom, WorkshopRoom).
            // GR-34: on the plan before, scaled with the rooms (a quarter larger each way).
            var doorways = new[]
            {
                ("unit A's entrance", new Vector2(-0.5f, -4f), true, 1.4f), ("the back room's door", new Vector2(0.8f, 4f), true, 1.2f),
                ("the arch to unit B", new Vector2(4f, 0f), false, 3.2f), ("the lab annex's doorway", new Vector2(1.15f, 9f), true, 1.4f),
                ("the workshop's doorway", new Vector2(4f, 10.3f), false, 1.4f), ("the arch to unit C", new Vector2(10.875f, 4f), true, 1.95f),
            }.Select(d => (d.Item1, d.Item2 * GrowthPlan.K, d.Item3, d.Item4 * GrowthPlan.K)).ToArray();
            foreach (var name in new[] { "UnitADressing", "BackRoomDressing", "AnnexDressing", "WorkshopDressing", "UnitBDressing", "UnitCDressing", "FeatureWall" })
            {
                var filter = Piece(name).GetComponentInChildren<MeshFilter>(true);
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                Assert.That(vertices.Length, Is.GreaterThan(1000), name + "'s vertices are read (the test would pass on none)");
                var toPiece = Piece(name).transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var (door, centre, alongX, width) in doorways)
                {
                    int inside = 0;
                    foreach (var v in vertices)
                    {
                        var p = toPiece.MultiplyPoint3x4(v);
                        float along = alongX ? p.x - centre.x : p.z - centre.y, across = alongX ? p.z - centre.y : p.x - centre.x;
                        if (Mathf.Abs(along) < width / 2 - 0.04f && Mathf.Abs(across) < 0.3f && p.y > 0.05f && p.y < 2.0f) inside++;
                    }
                    Assert.That(inside, Is.EqualTo(0), name + " reaches into " + door);
                }
            }
        }

        [Test] public void EachRoomsDressingStaysInsideItsRoom()
        {
            // Unit A: x -4..4, z -4..4, ceiling 3.2; the back room: x -4..2, z 4..9, ceiling 3.6 (GrowthWorld.Rooms).
            // GR-16 wave 3: the lab annex, x -4..4, z 9..17, and the workshop, x 4..12, z 9..15, both with a 3.6 m ceiling.
            foreach (var (name, min, max) in new[] { ("UnitAFloor", new Vector3(-4, -0.05f, -4), new Vector3(4, 0.01f, 4)), ("UnitADressing", new Vector3(-4, 0, -4), new Vector3(4, 3.2f, 4)),
                                                    ("BackRoomFloor", new Vector3(-4, -0.05f, 4), new Vector3(2, 0.01f, 9)), ("BackRoomDressing", new Vector3(-4, 0, 4), new Vector3(2, 3.6f, 9)),
                                                    ("AnnexFloor", new Vector3(-4, -0.05f, 9), new Vector3(4, 0.01f, 17)), ("AnnexDressing", new Vector3(-4, 0, 9), new Vector3(4, 3.6f, 17)),
                                                    ("WorkshopFloor", new Vector3(4, -0.05f, 9), new Vector3(12, 0.01f, 15)), ("WorkshopDressing", new Vector3(4, 0, 9), new Vector3(12, 3.6f, 15)) })
            {
                var b = MeshBounds(Piece(name));
                for (int a = 0; a < 3; a++)
                {
                    float k = a == 1 ? 1 : GrowthPlan.K; // GR-34: the rooms a quarter larger each way
                    Assert.That(b.min[a], Is.GreaterThanOrEqualTo(min[a] * k - 0.01f), name + " min on axis " + a);
                    Assert.That(b.max[a], Is.LessThanOrEqualTo(max[a] * k + 0.01f), name + " max on axis " + a);
                }
            }
            Assert.That(MeshBounds(Piece("Street")).max.z, Is.LessThanOrEqualTo(GrowthPlan.S(-4f) - 0.05f), "the street stays outside unit A's front wall");
            // GR-16 wave 4: unit B, x 4..12, z -4..4, and unit C, x 2..12, z 4..9, both with a 3.2 m ceiling; the street outside unit B.
            foreach (var (name, min, max) in new[] { ("UnitBFloor", new Vector3(4, -0.05f, -4), new Vector3(12, 0.01f, 4)), ("UnitBDressing", new Vector3(4, 0, -4), new Vector3(12, 3.2f, 4)),
                                                    ("UnitCFloor", new Vector3(2, -0.05f, 4), new Vector3(12, 0.01f, 9)), ("UnitCDressing", new Vector3(2, 0, 4), new Vector3(12, 3.2f, 9)) })
            {
                var b = MeshBounds(Piece(name));
                for (int a = 0; a < 3; a++)
                {
                    float k = a == 1 ? 1 : GrowthPlan.K; // GR-34: the rooms a quarter larger each way
                    Assert.That(b.min[a], Is.GreaterThanOrEqualTo(min[a] * k - 0.01f), name + " min on axis " + a);
                    Assert.That(b.max[a], Is.LessThanOrEqualTo(max[a] * k + 0.01f), name + " max on axis " + a);
                }
            }
            Assert.That(MeshBounds(Piece("StreetEast")).max.z, Is.LessThanOrEqualTo(GrowthPlan.S(-4f) - 0.05f), "the street stays outside unit B's front wall");
        }

        /// <summary>Wave 4's pieces where their greybox has them: the bay window bench in front of its wall, its bowls hanging over
        /// head height; the feature tank between unit C's two feature places and against the wall; the feature wall a paint layer
        /// on its wall (its cove of light only at the top); each board, the sign and the award on its wall; each partition inside
        /// its doorway, as thick as the greybox's; a banner inside the cloth it replaces, which unrolls; a spotlight's fixture under
        /// the ceiling; the window display on unit A's sill.</summary>
        [Test] public void WaveFoursPiecesStayWhereTheyBelong()
        {
            var bay = Vertices("BayWindow");
            Assert.That(bay.All(v => Mathf.Abs(v.x) <= 1.03f && v.z >= -0.3f), Is.True, "the bay window's frame within its opening, nothing past the glass");
            Assert.That(bay.Where(v => v.y < 2.0f).All(v => v.z <= 0.28f), Is.True, "below the bowls, nothing reaches past the bench's front (its knobs)");
            Assert.That(bay.Where(v => v.y < 0.8f).All(v => Mathf.Abs(v.x) <= 0.93f), Is.True, "the bench within its seat's width");
            Assert.That(bay.Where(v => v.z > 0.28f).All(v => v.y >= 2.05f), Is.True, "the hanging bowls over head height");
            var tank = MeshBounds(Piece("FeatureTank"));
            Assert.That(Mathf.Max(tank.max.x, -tank.min.x), Is.LessThanOrEqualTo(0.725f), "the feature tank between the feature places (c1 to x 5.25, c2 from 6.7)");
            Assert.That(tank.max.z, Is.LessThanOrEqualTo(0.15f + 0.001f), "its back against unit C's north wall");
            float K = GrowthPlan.K, wallFace = 9f * K - 0.1f; // GR-34: unit C's north wall on the new plan
            Assert.That(Vertices("FeatureWall").All(v => v.x >= 2.18f * K && v.x <= 9.82f * K && v.z <= wallFace + 0.001f && (v.z >= wallFace - 0.05f || v.y >= 3.05f)), Is.True,
                "the feature wall a paint layer on the wall's face, only its cove of light standing out at the top");
            Assert.That(MeshBounds(Piece("StaffBoard")).max.z, Is.LessThanOrEqualTo(0.04f), "the staff board on unit A's north wall");
            Assert.That(MeshBounds(Piece("HallBoard")).min.z, Is.GreaterThanOrEqualTo(-0.04f), "the hall's plans on unit C's north wall");
            Assert.That(MeshBounds(Piece("Award")).min.z, Is.GreaterThanOrEqualTo(-0.03f - 0.001f), "the award on unit A's front wall");
            Assert.That(MeshBounds(Piece("ExhibitionSign")).min.z, Is.GreaterThanOrEqualTo(-0.04f - 0.001f), "the sign on unit A's front wall");
            foreach (var (name, alongX, line, a0, a1, height) in new[] { ("PartitionB", false, 4f * K, -1.6f * K, 1.6f * K, 2.4f), ("PartitionC", true, 4f * K, 9.9f * K, 11.85f * K, 2.4f),
                                                                       ("PartitionAnnex", true, 9f * K, 0.45f * K, 1.85f * K, 2.8f), ("PartitionWorkshop", false, 4f * K, 9.6f * K, 11.0f * K, 2.8f) })
            {
                var b = MeshBounds(Piece(name));
                float along0 = alongX ? b.min.x : b.min.z, along1 = alongX ? b.max.x : b.max.z, across0 = alongX ? b.min.z : b.min.x, across1 = alongX ? b.max.z : b.max.x;
                Assert.That(along0 >= a0 - 0.01f && along1 <= a1 + 0.01f, Is.True, name + " within its doorway");
                Assert.That(across0 >= line - 0.08f && across1 <= line + 0.06f + 0.001f, Is.True, name + " as thick as the greybox's partition, its paint on the open side");
                Assert.That(b.min.y >= -0.01f && b.max.y <= height + 0.01f, Is.True, name + " from the floor to the doorway's head");
            }
            foreach (var name in new[] { "BannerCoral", "BannerYellow", "BannerBlue" })
            {
                var b = MeshBounds(Piece(name));
                Assert.That(b.max.y <= 0.05f && b.min.y >= -1.26f && Mathf.Max(b.max.x, -b.min.x) <= 0.56f && Mathf.Max(b.max.z, -b.min.z) <= 0.045f, Is.True,
                    name + " hangs from its rod as the greybox's cloth did, 1.2 m, both faces painted");
            }
            var spot = MeshBounds(Piece("HallSpot"));
            Assert.That(spot.max.y <= 0.15f + 0.001f && spot.min.y >= -0.01f && spot.extents.x <= 0.1f, Is.True, "a spotlight's fixture under the 3.2 m ceiling");
            var display = MeshBounds(Piece("WindowDisplay"));
            Assert.That(display.min.y >= -0.01f && Mathf.Max(display.max.z, -display.min.z) <= 0.1f, Is.True, "the window display on unit A's sill");
        }
    }
}
