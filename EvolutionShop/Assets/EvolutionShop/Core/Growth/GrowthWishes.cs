using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-20: visitors' wishes (GR.61 and GR.62, the user's choices; GR.10, AD-E). Every ordinary visitor comes in wishing for
    // something the shop could have: a named species, any creature in a colour (Sunny, Deep or Shimmer), or any small, medium
    // or large one; from 3 stars also a matching pair or a school of three of one species. A wish asks only for what the shop
    // has discovered (never a crown), in a colour it has made, and at a price the visitor can pay without any bonus, so stocking
    // it is always enough. It is a preference, not a quest: a visitor looks first into the tanks that hold their wish and takes
    // it when they find it within budget, and otherwise buys as they would have. A granted wish earns a reputation point and a
    // tip of a quarter of the price (at least a credit), by whoever serves; exhibition guests keep their own orders (GR-11).
    // What each visitor wishes for is fixed by the day and their place in it, as their type is (no luck in what the player is owed).

    public enum WishKind { Species, Colour, Size, Pair, School }

    /// <summary>What an ordinary visitor wishes for (GR-20).</summary>
    public sealed class GrowthWish
    {
        public WishKind Kind;
        /// <summary>The species, for a Species wish.</summary>
        public string Species;
        /// <summary>The colour, for a Colour wish (Sunny, Deep or Shimmer).</summary>
        public Colour Colour;
        /// <summary>The size class, for a Size wish.</summary>
        public SizeClass Size;
        /// <summary>How many creatures grant it: two for a pair, three for a school, else one.</summary>
        public int Count => Kind == WishKind.Pair ? 2 : Kind == WishKind.School ? 3 : 1;

        /// <summary>Whether a creature fits the wish on its own (a pair or a school needs its count of one species).</summary>
        public bool Fits(GrowthSpecimen s)
        {
            switch (Kind)
            {
                case WishKind.Species: return s.Species == Species;
                case WishKind.Colour: return s.Colour == Colour;
                case WishKind.Size: return s.Def.Size == Size;
                default: return true;
            }
        }

        /// <summary>Whether what a visitor bought grants the wish.</summary>
        public bool Matches(IList<GrowthSpecimen> bought)
        {
            if (bought == null || bought.Count == 0) return false;
            if (Kind == WishKind.Pair || Kind == WishKind.School) return bought.Count == Count && bought.All(s => s.Species == bought[0].Species);
            return bought.All(Fits);
        }

        /// <summary>The wish in words, after "wishes for": a Pondlet, a Sunny creature, a large creature, a matching pair, a school of three.</summary>
        public string Text
        {
            get
            {
                switch (Kind)
                {
                    case WishKind.Species: return GrowthWishes.Article(GrowthContent.Get(Species).Name);
                    case WishKind.Colour: return "a " + Colour + " creature";
                    case WishKind.Size: return "a " + Size.ToString().ToLowerInvariant() + " creature";
                    case WishKind.Pair: return "a matching pair";
                    default: return "a school of three";
                }
            }
        }

        public override string ToString() => Text;
    }

    public static class GrowthWishes
    {
        /// <summary>AD-E: a granted wish's tip, a quarter of what the visitor paid, at least a credit.</summary>
        public const float TipShare = 0.25f;
        /// <summary>GR.10: pairs and schools are wished for from 3 stars.</summary>
        public const int GroupStars = 3;

        /// <summary>
        /// The kinds of wish each visitor type has, and how often (GR.10, AD-E's flavour): casual visitors like a creature, a
        /// colour or a size; bargain hunters a cheap species or any small one; collectors a species, a colour or a matching pair;
        /// big spenders something big, a whole school or one of the dearest species. A kind the shop cannot offer yet is skipped.
        /// </summary>
        public static readonly Dictionary<string, (WishKind kind, int weight)[]> Kinds = new Dictionary<string, (WishKind, int)[]>
        {
            ["casual"] = new[] { (WishKind.Species, 4), (WishKind.Colour, 3), (WishKind.Size, 3) },
            ["bargain"] = new[] { (WishKind.Species, 6), (WishKind.Size, 4) },
            ["collector"] = new[] { (WishKind.Species, 5), (WishKind.Colour, 3), (WishKind.Pair, 2) },
            ["premium"] = new[] { (WishKind.Size, 4), (WishKind.School, 3), (WishKind.Species, 3) },
        };

        /// <summary>The tip for a granted wish on a purchase of this price.</summary>
        public static int Tip(int price) => Math.Max(1, (int)Math.Floor(price * TipShare + 0.5));

        /// <summary>A name with its article: a Pondlet, an Inkling.</summary>
        public static string Article(string name) => ("AEIOU".IndexOf(char.ToUpperInvariant(name[0])) >= 0 ? "an " : "a ") + name;
    }

    public sealed partial class GrowthShop
    {
        /// <summary>
        /// What a visitor of a type with a budget would wish for as the day's nth visitor, or null when the shop has nothing to
        /// wish for (no creature discovered they could pay for). Fixed by the day and n: the same shop always gets the same wishes.
        /// </summary>
        public GrowthWish WishFor(string type, int budget, int n)
        {
            if (type == GuestType || !GrowthWishes.Kinds.TryGetValue(type, out var kinds)) return null;
            // What the shop could offer this visitor: its discovered species but the crowns, at a price they can pay as it is.
            var known = Discovered.Select(GrowthContent.Get).Where(d => d != null && d.Kind != SpeciesKind.Crown && d.Price <= budget)
                .OrderBy(d => d.Price).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (known.Count == 0) return null;
            var sizes = Discovered.Select(GrowthContent.Get).Where(d => d != null && d.Kind != SpeciesKind.Crown).Select(d => d.Size).Distinct().Count();
            var options = new List<(WishKind kind, int weight, Func<int, GrowthWish> make)>();
            foreach (var (kind, weight) in kinds)
            {
                switch (kind)
                {
                    case WishKind.Species:
                    {
                        // Bargain hunters wish for the cheaper half of what they can pay for, big spenders the dearer half.
                        var pool = type == "bargain" ? known.Take((known.Count + 1) / 2).ToList() : type == "premium" ? known.Skip(known.Count / 2).ToList() : known;
                        options.Add((kind, weight, r => new GrowthWish { Kind = kind, Species = pool[r % pool.Count].Id }));
                        break;
                    }
                    case WishKind.Colour:
                    {
                        // Only a colour the shop has made, in a species this visitor can pay for.
                        var colours = new[] { Colour.Sunny, Colour.Deep, Colour.Shimmer }.Where(c => known.Any(d => Journal.Contains(JournalKey(d.Id, c)))).ToList();
                        if (colours.Count > 0) options.Add((kind, weight, r => new GrowthWish { Kind = kind, Colour = colours[r % colours.Count] }));
                        break;
                    }
                    case WishKind.Size:
                    {
                        // A size is a choice once the shop knows two: bargain hunters any small one, big spenders a medium or large one.
                        if (sizes < 2) break;
                        var pool = known.Select(d => d.Size).Distinct().Where(z => type == "bargain" ? z == SizeClass.Small : type == "premium" ? z != SizeClass.Small : true).OrderBy(z => z).ToList();
                        if (pool.Count > 0) options.Add((kind, weight, r => new GrowthWish { Kind = kind, Size = pool[r % pool.Count] }));
                        break;
                    }
                    case WishKind.Pair:
                    case WishKind.School:
                    {
                        int count = kind == WishKind.Pair ? 2 : 3;
                        if (Stars >= GrowthWishes.GroupStars && known.Any(d => count * d.Price <= budget)) options.Add((kind, weight, r => new GrowthWish { Kind = kind }));
                        break;
                    }
                }
            }
            if (options.Count == 0) return null;
            int total = options.Sum(o => o.weight);
            int roll = (int)(Hash(Day * 6007 + n * 92821 + 31) % (uint)total);
            int pick = (int)(Hash(Day * 4099 + n * 15485 + 7) % 100000u);
            foreach (var o in options) { if (roll < o.weight) return o.make(pick); roll -= o.weight; }
            return options[0].make(pick);
        }

        /// <summary>
        /// What would grant a visitor's wish in one container, within their budget as that container stretches it (GR.10.1): the
        /// species wished for; the dearest creature in the colour or size; the dearest matching pair or school (reserved together,
        /// all or none). Null when it holds nothing that would.
        /// </summary>
        public List<GrowthSpecimen> WishIn(GrowthVisitor v, GrowthContainer c)
        {
            var w = v.Wish;
            if (w == null || c == null) return null;
            var free = c.Occupants.Where(s => s.ReservedBy == 0).ToList();
            if (w.Kind == WishKind.Pair || w.Kind == WishKind.School)
            {
                var group = free.GroupBy(s => s.Species).Where(g => g.Count() >= w.Count && w.Count * Price(g.Key) <= v.Budget * Tolerance(c, g.Key))
                    .OrderByDescending(g => Price(g.Key)).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault();
                return group?.OrderBy(s => s.Id).Take(w.Count).ToList();
            }
            var best = free.Where(s => w.Fits(s) && Price(s.Species) <= v.Budget * Tolerance(c, s.Species))
                .OrderByDescending(s => Price(s.Species)).ThenBy(s => s.Id).FirstOrDefault();
            return best == null ? null : new List<GrowthSpecimen> { best };
        }

        /// <summary>
        /// Development and validation only: a visitor of a type comes in now wishing for something chosen, to look into one
        /// container for a while (counted in the day as any visitor is); they decide when their look is over, as anyone does.
        /// </summary>
        public GrowthVisitor DevelopmentArrive(string type, GrowthWish wish, string containerId, float browseFor)
        {
            var def = GrowthContent.Visitors.First(t => t.Id == type);
            var v = new GrowthVisitor { Id = nextId++, Type = type, Budget = def.Budgets[Stars - 1], State = GrowthVisitorState.Browsing, Wish = wish, BrowseFor = browseFor };
            v.Plan.Add(containerId);
            v.ContainerId = containerId;
            visitorsToday++;
            if (Today != null) { Today.Visitors++; if (wish != null) Today.Wishes++; }
            Visitors.Add(v);
            return v;
        }

        /// <summary>Whether what a queued visitor reserved grants their wish (the counter and the HUD say so before the sale).</summary>
        public bool WouldGrant(GrowthVisitor v) => v.Type != GuestType && v.Wish != null && v.Wish.Matches(Reserved(v));

        /// <summary>What a visitor reserved (none until they decide).</summary>
        public List<GrowthSpecimen> Reserved(GrowthVisitor v) =>
            v.SpecimenIds.Select(id => HolderOf(id)?.Occupants.FirstOrDefault(o => o.Id == id)).Where(s => s != null).ToList();

        /// <summary>What a visitor's reservation costs, all of it.</summary>
        public int ReservedPrice(GrowthVisitor v) => Reserved(v).Sum(s => Price(s.Species));

        /// <summary>What a visitor reserved, in words: a Pondlet, a pair of Pondlets, a school of three Pondlets.</summary>
        public string ReservedText(GrowthVisitor v)
        {
            var r = Reserved(v);
            if (r.Count == 0) return "something gone";
            string name = r[0].Def.Name;
            return r.Count == 1 ? GrowthWishes.Article(name) : r.Count == 2 ? "a pair of " + Plural(name) : "a school of three " + Plural(name);
        }

        /// <summary>A sale grants the visitor's wish: the reputation point and the tip (by the player or Pip); the tip, or 0 when the
        /// sale does not grant it (then the wish is missed). The sale raises the events.</summary>
        int GrantWish(GrowthVisitor v, List<GrowthSpecimen> bought, int price)
        {
            if (v.Type == GuestType || v.Wish == null || !v.Wish.Matches(bought)) { MissWish(v); return 0; }
            int tip = GrowthWishes.Tip(price);
            v.WishGranted = true;
            Credits += tip;
            Reputation = Math.Min(GrowthContent.ReputationMax, Reputation + GrowthContent.RepPerWish);
            if (Today != null) { Today.WishesGranted++; Today.Tips += tip; Today.Revenue += tip; Today.ReputationGained += GrowthContent.RepPerWish; }
            return tip;
        }

        /// <summary>A visit that ends without the wish granted: the night's receipt lists what was wished for.</summary>
        void MissWish(GrowthVisitor v)
        {
            if (v.Type == GuestType || v.Wish == null || v.WishGranted || v.WishMissed) return;
            v.WishMissed = true;
            if (Today == null) return;
            Today.WishesMissed.TryGetValue(v.Wish.Text, out int n);
            Today.WishesMissed[v.Wish.Text] = n + 1;
        }
    }
}
