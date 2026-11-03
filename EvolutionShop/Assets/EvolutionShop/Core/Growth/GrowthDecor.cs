using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-19: decor (GR.60 and GR.61, the user's choices; GR.6, GR.10). Nine pieces in three themes, each bought once from the
    // supplier catalog: ordered at night, delivered into the decor box when the shop next opens. At night a piece goes on a
    // shop tank (one a tank; the user chose a tank's top over floor spots, which the packed places left no room for, and in tester round 2, F67 and F85, its water, at the back);
    // it moves with its tank and goes back to the box when the tank is sold. A tank with a piece is more appealing (visitors
    // look into it first, and pay a little more there, as for a Show tank or harmony); its creatures whose water matches the
    // piece's theme are themed: visitors pay up to a quarter more for them (GR.10.1's +25, capped with schools at +40).

    /// <summary>A piece of decor: its theme (the water it suits), its catalog page (stars, era) and price, and its kit asset.</summary>
    public sealed class DecorDef
    {
        public string Id, Name, Asset, Text;
        public Water Water;
        public int Stars, Era, Credits;
        public string Theme => GrowthContent.ThemeName(Water);
    }

    public static partial class GrowthContent
    {
        /// <summary>GR.10.1: a themed creature's visitors stretch their budget by this much (capped with schools at ToleranceCap).</summary>
        public const float ThemeTolerance = 0.25f;

        public static string ThemeName(Water water) => water == Water.Fresh ? "Riverbank" : water == Water.Warm ? "Hot springs" : "Seaside";
        public static string ThemeSuits(Water water) => water == Water.Fresh ? "fresh-water creatures" : water == Water.Warm ? "warm-water creatures" : "brine creatures";

        static DecorDef[] decor;
        /// <summary>The fifteen pieces, five in each theme (Riverbank and Hot springs on the catalog's 2-star page, Seaside on its 4-star page, with Brine); GR-32 (F57): two plain ones each, the cheapest.</summary>
        public static DecorDef[] Decor => decor ?? (decor = BuildDecor());

        static DecorDef[] BuildDecor()
        {
            DecorDef D(string id, string name, string asset, Water water, int stars, int era, int credits, string text) =>
                new DecorDef { Id = id, Name = name, Asset = asset, Water = water, Stars = stars, Era = era, Credits = credits, Text = text };
            return new[]
            {
                D("stones", "River stones", "DecorStones", Water.Fresh, 2, 2, 60, "A little cairn of smooth stones, moss at its foot"),
                D("fern", "Potted fern", "DecorFern", Water.Fresh, 2, 2, 90, "A green fern in a terracotta pot"),
                D("reeds", "Reeds and a dragonfly", "DecorReeds", Water.Fresh, 2, 2, 120, "A clump of reeds with cattails, a dragonfly resting on one"),
                D("lily-lamp", "Lily-pad lamp", "DecorLilyLamp", Water.Fresh, 2, 2, 160, "A water lily glowing on its stem, a frog on the pad"),
                D("watermill", "Little watermill", "DecorWatermill", Water.Fresh, 2, 2, 200, "A wooden mill with a lit window, its wheel in a stream"),
                D("rocks", "Warm rocks", "DecorRocks", Water.Warm, 2, 2, 60, "A heap of dark rocks, a sprig of green between them"),
                D("bamboo", "Bamboo in a pot", "DecorBamboo", Water.Warm, 2, 2, 90, "Three canes of bamboo in a stone pot"),
                D("volcano", "Little volcano", "DecorVolcano", Water.Warm, 2, 2, 120, "Lava glowing in its crater, a puff of steam above"),
                D("lantern", "Stone lantern", "DecorLantern", Water.Warm, 2, 2, 160, "Warm light behind its paper windows"),
                D("duck-spa", "Rubber-duck hot pool", "DecorDuckSpa", Water.Warm, 2, 2, 200, "A steaming rock pool with a rubber duck afloat"),
                D("shells", "Shells on the sand", "DecorShells", Water.Brine, 4, 4, 300, "A few shells and pebbles the tide left"),
                D("driftwood", "Driftwood", "DecorDriftwood", Water.Brine, 4, 4, 450, "A bleached branch washed ashore"),
                D("lighthouse", "Lighthouse", "DecorLighthouse", Water.Brine, 4, 4, 800, "Coral and cream stripes, its lamp glowing"),
                D("chest", "Treasure chest", "DecorChest", Water.Brine, 4, 4, 1000, "Thrown open, gold and pearls spilling out"),
                D("coral-arch", "Coral arch", "DecorCoralArch", Water.Brine, 4, 4, 1200, "Pink coral, a starfish and shells on the sand"),
            };
        }

        public static DecorDef DecorPiece(string id) => Decor.FirstOrDefault(d => d.Id == id);
    }

    public sealed partial class GrowthShop
    {
        /// <summary>The decor delivered: in the box or on a tank (each piece once).</summary>
        public readonly List<string> DecorOwned = new List<string>();

        /// <summary>The pieces in the box: owned and on no tank.</summary>
        public IEnumerable<string> DecorBox => DecorOwned.Where(d => !Containers.Any(c => c.Decor == d));

        public GrowthContainer DecorHolder(string decor) => Containers.FirstOrDefault(c => c.Decor == decor);
        public static DecorDef DecorOn(GrowthContainer c) => c?.Decor == null ? null : GrowthContent.DecorPiece(c.Decor);

        /// <summary>A creature is themed in its tank when the tank's decor suits its water.</summary>
        public static bool Themed(GrowthContainer c, string species)
        {
            var d = DecorOn(c);
            return d != null && GrowthContent.Get(species)?.Water == d.Water;
        }

        /// <summary>Why a piece cannot go on a container now (None when it can).</summary>
        public GrowthFailure DecorCheck(string decor, string containerId, out string why)
        {
            why = null;
            var def = GrowthContent.DecorPiece(decor);
            if (def == null) { why = "Not a piece of decor"; return GrowthFailure.NotInCatalog; }
            if (!DecorOwned.Contains(decor)) { why = "Not yours yet: order it from the supplier catalog"; return GrowthFailure.NotOwned; }
            var c = Container(containerId);
            if (c == null) { why = "No such tank"; return GrowthFailure.NoSuchContainer; }
            if (c.Def.Lab) { why = "Decor goes on the shop's tanks"; return GrowthFailure.DoesNotFit; }
            if (c.Decor == decor) { why = "It is on this tank"; return GrowthFailure.Owned; }
            return GrowthFailure.None;
        }

        /// <summary>Puts a piece on a shop tank at night: from the box or from another tank; a piece already on this tank goes
        /// back to the box.</summary>
        public GrowthResult PutDecor(string decor, string containerId)
        {
            var failure = DecorCheck(decor, containerId, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var from = DecorHolder(decor);
            if (from != null) from.Decor = null;
            var c = Container(containerId);
            var def = GrowthContent.DecorPiece(decor);
            c.Decor = decor;
            int suited = c.Occupants.Count(s => Themed(c, s.Species));
            Raise(GrowthEventKind.Decorated, def.Name + " on the " + c.Def.Name + " at " + c.Place + ": " + def.Theme + ", " + GrowthContent.ThemeSuits(def.Water) +
                " sell for more there" + (suited > 0 ? " (" + suited + " of its creatures)" : ""));
            return GrowthResult.Ok();
        }

        /// <summary>Why a tank's piece cannot come off now (None when it can).</summary>
        public GrowthFailure TakeDecorCheck(string containerId, out string why)
        {
            why = null;
            var c = Container(containerId);
            if (c == null) { why = "No such tank"; return GrowthFailure.NoSuchContainer; }
            if (c.Decor == null) { why = "No decor on it"; return GrowthFailure.NotOwned; }
            return GrowthFailure.None;
        }

        /// <summary>Takes a tank's piece back to the box at night.</summary>
        public GrowthResult TakeDecor(string containerId)
        {
            var failure = TakeDecorCheck(containerId, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var c = Container(containerId);
            var def = GrowthContent.DecorPiece(c.Decor);
            c.Decor = null;
            Raise(GrowthEventKind.Decorated, def.Name + " back in the decor box");
            return GrowthResult.Ok();
        }

        void SaveDecor(GrowthSave s)
        {
            s.decorOwned = DecorOwned.ToArray();
            foreach (var b in s.containers ?? new GrowthSave.Box[0]) { b.decor = Container(b.id)?.Decor ?? ""; b.rule = Container(b.id)?.Rule ?? ""; }
        }

        /// <summary>Version 11 and older had no decor.</summary>
        static string RestoreDecor(GrowthShop shop, GrowthSave s)
        {
            foreach (var d in s.decorOwned ?? new string[0])
            {
                if (GrowthContent.DecorPiece(d) == null) return "Unknown decor " + d;
                if (shop.DecorOwned.Contains(d)) return "A piece of decor is saved twice";
                shop.DecorOwned.Add(d);
            }
            foreach (var b in s.containers ?? new GrowthSave.Box[0])
            {
                if (string.IsNullOrEmpty(b.decor)) continue;
                var c = shop.Container(b.id);
                if (c == null || c.Def.Lab) return "Decor on a container that takes none";
                if (!shop.DecorOwned.Contains(b.decor)) return "Decor on a tank that the shop does not own: " + b.decor;
                if (shop.Containers.Any(x => x.Decor == b.decor)) return "A piece of decor on two tanks";
                c.Decor = b.decor;
            }
            return null;
        }
    }
}
