using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-16 waves 1 and 2: the growth kit (Tools/Art/create_growth_kit.py) against the game. Every piece loads as one mesh
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
            "TankStarter", "TankBed", "TankFilter", "TankLid", "Counter", "CounterBags", "CounterReader", "CounterTill",
            "UnitAFloor", "UnitADressing", "Street", "BackRoomFloor", "BackRoomDressing",
            "Vat", "VatBatch", "VatStirrer", "BreedingTank", "BreedingPort", "BreedingPair", "Bench", "BenchMicroscope", "BenchAnalyzer",
            "BenchPlanner", "SiphonBulb", "SiphonBig", "SiphonTwin", "SiphonVacuum", "VialShelf", "VialBottle", "Catalog", "Pinboard",
            "TankLarge", "TankBedLarge", "TankLidLarge", "TankShow", "TankBedShow", "TankSpotlight", "TankRack", "TankLidRack",
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
            Assert.That(shop.Guide, Is.EqualTo(GrowthGuideMode.Off), "a start point is never guided");
            Assert.That(shop.Snapshot(), Is.Not.Null, "it saves");
        }

        [Test] public void EachRoomsDressingStaysInsideItsRoom()
        {
            // Unit A: x -4..4, z -4..4, ceiling 3.2; the back room: x -4..2, z 4..9, ceiling 3.6 (GrowthWorld.Rooms).
            foreach (var (name, min, max) in new[] { ("UnitAFloor", new Vector3(-4, -0.05f, -4), new Vector3(4, 0.01f, 4)), ("UnitADressing", new Vector3(-4, 0, -4), new Vector3(4, 3.2f, 4)),
                                                    ("BackRoomFloor", new Vector3(-4, -0.05f, 4), new Vector3(2, 0.01f, 9)), ("BackRoomDressing", new Vector3(-4, 0, 4), new Vector3(2, 3.6f, 9)) })
            {
                var b = MeshBounds(Piece(name));
                for (int a = 0; a < 3; a++)
                {
                    Assert.That(b.min[a], Is.GreaterThanOrEqualTo(min[a] - 0.01f), name + " min on axis " + a);
                    Assert.That(b.max[a], Is.LessThanOrEqualTo(max[a] + 0.01f), name + " max on axis " + a);
                }
            }
            Assert.That(MeshBounds(Piece("Street")).max.z, Is.LessThanOrEqualTo(-4.05f), "the street stays outside unit A's front wall");
        }
    }
}
