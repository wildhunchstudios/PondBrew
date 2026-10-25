using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-16 wave 1: the growth kit (Tools/Art/create_growth_kit.py) against the game. Every piece loads as one mesh
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
        public void EachStationsPieceStaysWithinItsFootprint(string name, float halfX, float halfZ, float leeway)
        {
            var b = MeshBounds(Piece(name));
            Assert.That(b.max.x, Is.LessThanOrEqualTo(halfX + leeway), name + " east");
            Assert.That(-b.min.x, Is.LessThanOrEqualTo(halfX + leeway), name + " west");
            Assert.That(b.max.z, Is.LessThanOrEqualTo(halfZ + leeway), name + " front");
            Assert.That(-b.min.z, Is.LessThanOrEqualTo(halfZ + leeway), name + " back");
            Assert.That(b.min.y, Is.GreaterThanOrEqualTo(-0.01f), name + " stands on the floor");
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
