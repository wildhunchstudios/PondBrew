using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // R09.4, broadened at the user's word (2026-10-30, "a rework of the tutorial system to include every part of the game"): after
    // the guided first night (GR-14), every system the game has gets a lesson when it first appears, era by era. A lesson is a
    // short card explaining it, then the next-step line and the marker walking the player through using it once (the user's
    // choice), each skippable. A lesson appears when its system does (Appears, read from the shop as it is) and is learned when
    // the system has been used once (Done, also read from the shop): so a new game meets them one at a time as it grows, and an
    // existing save (or a start point) meets only those for systems it has not used yet. A few are cards only (Done null): they
    // are learned by reading them. Every lesson met is kept to read again in the journal's Guide tab; a setting turns them off.

    /// <summary>R09.4: one lesson: its card, when its system appears, when it has been used, and its guided steps.</summary>
    public sealed class GrowthLessonDef
    {
        public string Id, Title;
        /// <summary>The era it belongs to (the Guide tab's order, and what a lesson not reached yet says).</summary>
        public int Era;
        /// <summary>The card: a few short paragraphs.</summary>
        public string[] Card;
        /// <summary>When its system appears (and the lesson may be met).</summary>
        public Func<GrowthShop, bool> Appears;
        /// <summary>When its system has been used once (the lesson is learned); null for a card only, learned by reading it.</summary>
        public Func<GrowthShop, bool> Done;
        /// <summary>Its guided step now (what the next-step line says and what the marker points at); null for a card only.</summary>
        public Func<GrowthShop, GrowthGuideView> Step;
        /// <summary>Its card may open during the day (things that happen only then, such as a visitor's wish); others wait for the night.</summary>
        public bool AnyTime;
        public bool CardOnly => Done == null;
    }

    public static class GrowthLessons
    {
        /// <summary>The marker's targets for lessons: a GrowthTargetKind's name, and an id when it needs one ("Place:a2").</summary>
        static GrowthGuideView V(string text, string target = null, string name = null) => new GrowthGuideView { Text = text, Target = target, TargetName = name };

        static GrowthContainer AShopTank(GrowthShop s) => s.ShopContainers.OrderBy(c => c.Place, StringComparer.Ordinal).FirstOrDefault();
        static string FreePlace(GrowthShop s, Func<GrowthPlace, bool> fits) =>
            s.FreePlaces.Select(GrowthShop.Place).Where(p => p != null && p.Unit.StartsWith("unit-", StringComparison.Ordinal) && fits(p)).Select(p => p.Id).FirstOrDefault();
        static bool CanHire(GrowthShop s, string id) => Ready(s.HireCheck(id, out _));
        /// <summary>A purchase lesson appears once the shop's own check would let the purchase through but for the night (its
        /// stars, era and credits met), so its step never stands on the next-step line out of reach.</summary>
        static bool Ready(GrowthFailure f) => f == GrowthFailure.None || f == GrowthFailure.NotAtNight;
        static bool CanUpgrade(GrowthShop s, string station) => Ready(s.UpgradeCheck(station, out _, out _));
        static bool CanBuy(GrowthShop s, string kind, Func<GrowthPlace, bool> fits)
        {
            var place = FreePlace(s, fits);
            return place != null && Ready(s.BuyCheck(kind, place, out _));
        }

        static GrowthGuideView Hire(string name, string job) =>
            V("Hire " + name + " at the staff board in unit A (E): " + job, "StaffBoard", "Staff board");

        static GrowthLessonDef[] all;
        /// <summary>Every lesson, in the order they are met (each era's in its order).</summary>
        public static GrowthLessonDef[] All => all ?? (all = Build());

        public static GrowthLessonDef Get(string id) => All.FirstOrDefault(l => l.Id == id);

        static GrowthLessonDef[] Build() => new[]
        {
            // ---------------------------------------------------------------- era 1: the back room
            new GrowthLessonDef
            {
                Id = "upgrades", Title = "Upgrading stations", Era = 1,
                Card = new[]
                {
                    "Every station has levels: the counter, the growers, the incubator and the research bench. Open one (E) and press Upgrade. Your siphon is upgraded at the research bench.",
                    "A level makes it faster or bigger: a bag dispenser bags sales quicker, a bigger bulb holds more doses, a grower refills sooner. Upgrades can be bought at any time.",
                },
                Appears = s => s.Day >= 2 && CanUpgrade(s, "counter"),
                Done = s => GrowthShop.GreyboxStations.Any(st => s.Level(st) > 1),
                Step = s => V(s.Phase == GrowthPhase.Night ? "Open the counter (E) and press Upgrade: a bag dispenser (20 credits)" : "Tonight, open the counter (E) and press Upgrade (20 credits)", "Counter", "Upgrade here"),
            },
            new GrowthLessonDef
            {
                Id = "tanks", Title = "More tanks", Era = 1,
                Card = new[]
                {
                    "Visitors buy from the tanks in the shop. More tanks hold more creatures, and three of a kind together make a school, which sells better.",
                    "Each dashed outline on the floor is a place for a tank. Aim at one at night and press E to buy a tank for it.",
                },
                Appears = s => s.Day >= 2 && s.ShopContainers.Count() < 2 && CanBuy(s, "starter", p => true),
                Done = s => s.ShopContainers.Count() >= 2,
                Step = s =>
                {
                    var place = FreePlace(s, p => true);
                    return V(s.Phase == GrowthPhase.Night ? "Aim at an empty place (a dashed outline) in the shop and press E: buy a tank" : "Tonight, buy a second tank at an empty place in the shop (E)",
                        place == null ? null : "Place:" + place, "Buy a tank here");
                },
            },
            new GrowthLessonDef
            {
                Id = "goals", Title = "Goals, stars and research points", Era = 1,
                Card = new[]
                {
                    "The pinboard in the laboratory holds your goals: what to do now, what opens next and the long goal. The line at the bottom of the screen always shows the next one.",
                    "Happy visitors raise your reputation, and reputation earns stars: each star brings more visitors and opens the next Era with one more goal. New discoveries earn research points (RP), spent at the pinboard.",
                },
                Appears = s => s.Day >= 2,
                Done = s => s.Noted.Contains("board"),
                Step = s => V("Open the pinboard in the laboratory (E): your goals and what is coming", "Board", "Pinboard"),
            },
            new GrowthLessonDef
            {
                Id = "journal", Title = "Your journal", Era = 1,
                Card = new[]
                {
                    "The journal (Tab) keeps your whole collection: all 72 creatures in their nine families, the ones you have found and how many are left, and the colours of each.",
                    "It also keeps your goals, your cultures, every mix and pair you have tried, and this guide: every lesson you have met, to read again.",
                },
                Appears = s => s.Day >= 2,
                Done = s => s.Noted.Contains("journal"),
                Step = s => V("Open your journal (Tab): your collection and how many are left"),
            },
            new GrowthLessonDef
            {
                Id = "wishes", Title = "Visitors' wishes", Era = 1, AnyTime = true,
                Card = new[]
                {
                    "Most visitors come in wishing for something: a creature, a colour, a size, or from 3 stars a pair or a school. Their bubble shows it, and they look first in the tank that has it.",
                    "A wish granted earns a quarter of the price as a tip and a reputation point. The night's receipt lists what was wished for and not granted, so you know what to make.",
                },
                Appears = s => s.Phase == GrowthPhase.Day && s.Visitors.Any(v => v.Wish != null),
                Done = s => s.Noted.Contains("wish"),
                Step = s => V("Aim at a visitor to read their wish; stock what they wish for"),
            },
            new GrowthLessonDef
            {
                Id = "days", Title = "Days and nights", Era = 1, AnyTime = true,
                Card = new[]
                {
                    "The night is yours: make creatures, stock tanks, buy and upgrade at your own pace. The day starts when you open the shop at the counter and lasts " + GrowthShop.DayLengthWords + ".",
                    "If the day is going nowhere, the counter can close the shop early: the visitors still inside are served first. The night's receipt sums the day up.",
                },
                Appears = s => s.Day >= 2 && s.Phase == GrowthPhase.Day,
            },

            // ---------------------------------------------------------------- era 2: a real shop
            new GrowthLessonDef
            {
                Id = "unit-b", Title = "Opening shop unit B", Era = 2,
                Card = new[]
                {
                    "Your shop grows by opening walls. Shop unit B doubles the shop: more places for tanks and more visitors at once.",
                    "Aim at the shop's east wall at night (E) to see what it needs.",
                },
                Appears = s => s.Era >= 2 && Ready(s.UnitBCheck(out _)),
                Done = s => s.UnitB,
                Step = s => V("At night, open shop unit B: aim at the shop's east wall and press E", "UnitBWall", "Shop unit B"),
            },
            new GrowthLessonDef
            {
                Id = "catalog", Title = "The supplier catalog", Era = 2,
                Card = new[]
                {
                    "The supplier catalog, on the stand by the counter, is where you order things for your shop. Its pages open as you earn stars.",
                    "A grower gives you a new culture for good. A starter vial is a few doses of one, to try it first. A decoration goes on top of a tank, and the creatures that suit its theme can sell for more.",
                    "Order at night. What you order arrives when you next open the shop.",
                },
                Appears = s => s.Era >= 2,
                Done = s => s.Noted.Contains("order") || s.Owns("ember") || s.Level("vat") > 0 || s.DecorOwned.Count > 0,
                Step = s => V("Open the supplier catalog by the counter (E) and order something", "Catalog", "Supplier catalog"),
            },
            new GrowthLessonDef
            {
                Id = "vials", Title = "Starter vials", Era = 2,
                Card = new[]
                {
                    "A starter vial holds a few doses of a culture you have no grower for yet. Vials stand on the shelf in the laboratory.",
                    "Hold the mouse on a vial to fill your siphon from it, as at a grower.",
                },
                Appears = s => s.Vials.Values.Any(d => d > 0),
                Done = s => s.Noted.Contains("vial"),
                Step = s =>
                {
                    var culture = s.Vials.Where(kv => kv.Value > 0).Select(kv => kv.Key).OrderBy(c => c, StringComparer.Ordinal).FirstOrDefault();
                    return V("Hold the mouse on a starter vial on the shelf to fill your siphon from it", culture == null ? null : "Vial:" + culture, "Starter vial");
                },
            },
            new GrowthLessonDef
            {
                Id = "research", Title = "Researching a culture", Era = 2,
                Card = new[]
                {
                    "Some cultures are researched rather than bought: Spark, and later Dusk. Spend research points (RP) on them at the pinboard.",
                    "A researched culture gets its own grower in the laboratory, and new founders to find with it.",
                },
                Appears = s => s.Era >= 2 && s.Rp > 0,
                Done = s => GrowthContent.Cultures.Any(c => c.Source == CultureSource.Research && s.Owns(c.Id)),
                Step = s => V("At the pinboard (E), research Spark with your research points", "Board", "Pinboard"),
            },
            new GrowthLessonDef
            {
                Id = "bench", Title = "The research bench", Era = 2,
                Card = new[]
                {
                    "The bench in the laboratory helps you find new creatures. Its notes show, in the journal, which cultures an undiscovered founder uses.",
                    "Its microscope (level 2) tells you after every mix how many doses you are from something new; later the analyzer gives a hint a day and the planner predicts what any two creatures breed.",
                },
                Appears = s => s.Discovered.Count >= 2 && CanUpgrade(s, "bench"),
                Done = s => s.Level("bench") >= 2,
                Step = s => V(s.Phase == GrowthPhase.Night ? "Open the research bench (E) and buy the microscope (level 2)" : "Tonight, buy the microscope at the research bench (E)", "Workbench", "Research bench"),
            },
            new GrowthLessonDef
            {
                Id = "breeding", Title = "Breeding hybrids", Era = 2,
                Card = new[]
                {
                    "Founders are mixed in the incubator; hybrids are bred. Two creatures in the breeding tank make an offspring, and the same pair always makes the same one.",
                    "The pair log in the journal keeps every pair you try. Each family has one crown: a special pair of its own.",
                },
                Appears = s => s.Era >= 2 && (s.HasBreedingTank || CanUpgrade(s, "breeding")),
                Done = s => s.PairLog.Count > 0,
                Step = s =>
                {
                    if (!s.HasBreedingTank) return V("At night, buy the breeding tank in the laboratory (E on it)", "Breeding", "Breeding tank");
                    if (s.Offspring != null) return V("The offspring is ready: click the breeding tank to take it", "Breeding", "Breeding tank");
                    if (s.Breeding) return V("Your pair is breeding: wait for the offspring", "Breeding", "Breeding tank");
                    return V("Carry two creatures to the breeding tank and click to put each in", "Breeding", "Breeding tank");
                },
            },
            new GrowthLessonDef
            {
                Id = "large", Title = "Sizes and the Large tank", Era = 2,
                Card = new[]
                {
                    "Creatures come in three sizes: a small one takes 1 space, a medium 2 and a large 4. A Starter tank holds small and medium ones.",
                    "A Large tank stands on a long place and holds every size. Medium creatures sell for more, large ones for more again.",
                },
                Appears = s => s.Era >= 2 && CanBuy(s, "large", p => p.Size == "long"),
                Done = s => s.ShopContainers.Any(c => c.Kind == "large"),
                Step = s =>
                {
                    var place = FreePlace(s, p => p.Size == "long");
                    return V(place == null ? "Move or sell a tank on a long place, then buy a Large tank there at night" : "At night, aim at a long place and press E: buy a Large tank",
                        place == null ? null : "Place:" + place, "A long place");
                },
            },
            new GrowthLessonDef
            {
                Id = "tank-levels", Title = "Bigger tanks", Era = 2,
                Card = new[]
                {
                    "Tanks have levels too: a Starter tank grows from 4 spaces to 6 and 8, a Large one more again. Open a tank (E) at night and press Upgrade.",
                },
                Appears = s => s.Era >= 2 && s.ShopContainers.Count() >= 2,
                Done = s => s.ShopContainers.Any(c => c.Level > 1),
                Step = s =>
                {
                    var tank = AShopTank(s);
                    return V("At night, open a tank in the shop (E) and press Upgrade: more spaces", tank == null ? null : "Tank:" + tank.Id, "Upgrade a tank");
                },
            },
            new GrowthLessonDef
            {
                Id = "decor", Title = "Décor and themes", Era = 2,
                Card = new[]
                {
                    "Décor from the catalog stands beside a tank: a riverbank, a hot spring or, later, the seaside. Creatures of its water are themed there, and their visitors pay a little more.",
                    "Every piece makes its tank more appealing, so visitors look there first.",
                },
                Appears = s => s.Era >= 2 && s.Stars >= GrowthContent.Decor.Min(d => d.Stars),
                Done = s => s.Containers.Any(c => c.Decor != null),
                Step = s =>
                {
                    if (!s.DecorBox.Any())
                        return s.Orders.Any(o => o.StartsWith("decor-", StringComparison.Ordinal)) ? V("Your decoration arrives when the shop next opens: then put it by a tank")
                                                                                     : V("Order a decoration from the catalog's Décor page: it arrives next night", "Catalog", "Supplier catalog");
                    var tank = AShopTank(s);
                    return V("Open a tank (E) and put your decoration beside it", tank == null ? null : "Tank:" + tank.Id, "Décor here");
                },
            },
            new GrowthLessonDef
            {
                Id = "colours", Title = "Colours", Era = 2,
                Card = new[]
                {
                    "Every creature comes in four colours: Classic, its usual look, then Sunny, Deep and Shimmer. It is the same creature in a new coat.",
                    "To make one, put a dose in a tint port, on the incubator or the breeding tank. Bloom makes what comes next Sunny, Flow makes it Deep, and Mist makes it Shimmer. With no tint, a bred creature takes the rarer colour of its parents.",
                    "The journal shows which colours you have found of each creature, and visitors may wish for one.",
                },
                Appears = s => s.TintPort || s.IncubatorTintPort,
                Done = s => s.Journal.Any(j => !j.EndsWith("/" + Colour.Classic, StringComparison.Ordinal)),
                Step = s => s.TintPort ? V("Open the breeding tank (E): a culture in its tint port colours the offspring", "Breeding", "Tint port")
                                       : V("Open the incubator (E): a culture in its tint port colours what hatches", "Incubator", "Tint port"),
            },
            new GrowthLessonDef
            {
                Id = "pip", Title = "Your first helper: Pip", Era = 2,
                Card = new[]
                {
                    "Pip the cashier bags sales at the counter, so you are free to stock and make creatures during the day.",
                    "Helpers are hired at the staff board for a fee and a wage each night; training makes them quicker, and a helper can be sent home for a night.",
                },
                Appears = s => CanHire(s, "pip"),
                Done = s => s.Staff.ContainsKey("pip"),
                Step = s => Hire("Pip", "your cashier"),
            },

            // ---------------------------------------------------------------- era 3: plumbing
            new GrowthLessonDef
            {
                Id = "annex", Title = "The power room", Era = 3,
                Card = new[]
                {
                    "Behind the laboratory is the power room: room for the generator, the power board and the buffers that keep cultures ready.",
                    "Aim at the laboratory's wall at night (E) to see what it needs.",
                },
                Appears = s => s.Era >= 3 && Ready(s.AnnexCheck(out _)),
                Done = s => s.Annex,
                Step = s => V("At night, open the power room: aim at the laboratory's wall and press E", "AnnexWall", "Power room"),
            },
            new GrowthLessonDef
            {
                Id = "generator", Title = "Power", Era = 3,
                Card = new[]
                {
                    "Machines need power. The generator makes it: the hand crank gives power while you hold it, and the turbines give it by themselves.",
                    "The power board shows the supply against the draw, and the order machines get power when there is not enough.",
                },
                Appears = s => s.Annex && CanUpgrade(s, "generator"),
                Done = s => s.HasGenerator,
                Step = s => V("At night, buy the generator in the power room (E on it)", "Generator", "Generator"),
            },
            new GrowthLessonDef
            {
                Id = "vat", Title = "The conditioning vat", Era = 3,
                Card = new[]
                {
                    "Mist is not grown but conditioned: the vat, ordered from the supplier catalog, makes it in the laboratory.",
                    "Mist opens the Airfin and Dewling founders.",
                },
                Appears = s => s.Era >= 3,
                Done = s => s.Level("vat") > 0 || s.Owns("mist") || s.Orders.Contains("vat"),
                Step = s => V("Order the conditioning vat from the supplier catalog: it makes Mist", "Catalog", "Supplier catalog"),
            },
            new GrowthLessonDef
            {
                Id = "pipes", Title = "Pumps, pipes and buffers", Era = 3,
                Card = new[]
                {
                    "A pipe carries a grower's culture to a buffer in the power room, where it waits in bulk: fill your siphon there in one go.",
                    "Growers at level 3 have a tap. Buy pumps and pipes on the power board, a buffer in the power room, then aim at a tap (E) and at the buffer (E) to lay the pipe.",
                    "A hand pump is worked from the front of the grower: aim at its crank there and hold the mouse.",
                },
                Appears = s => s.HasGenerator,
                Done = s => s.Lines.Any(),
                Step = s =>
                {
                    if (!s.HasPumps) return V("At night, buy pumps and pipes on the power board in the power room (E)", "PowerBoard", "Power board");
                    if (s.Level("grower") < 3) return V("Upgrade the growers to level 3 (E on a grower): a tap for a pipe", "Grower:" + s.Cultures.First(), "Upgrade the growers");
                    if (!s.Buffers.Any()) return V("Buy a buffer in the power room: aim at an empty buffer place (E)", "Buffer:0", "A buffer place");
                    return V("Aim at a grower's tap (E), then at the buffer (E): that lays the pipe", "Grower:" + s.Cultures.First(), "Start the pipe here");
                },
            },
            new GrowthLessonDef
            {
                Id = "research-room", Title = "The research room", Era = 3,
                Card = new[]
                {
                    "West of the power room is a room for trying things without waiting: three small tanks that run by themselves on the generator's power.",
                    "Put one creature of each kind in a jar, where it stays. At the desk, press ? on a pair to queue it, or queue a mix. Something new comes out as one real creature, on the tray; a repeat only tells you what it makes.",
                    "For stock, keep using the breeding tank and the incubator.",
                },
                Appears = s => s.Era >= 3 && Ready(s.ResearchCheck(out _)),
                Done = s => s.ResearchRoom && (s.ResearchJobs.Count > 0 || s.ResearchDone > 0),
                Step = s => !s.ResearchRoom ? V("At night, open the research room: the power room's west wall (E)", "ResearchWall", "Research room")
                    : s.Specimens.Count < 2 ? V("Bring two different creatures to the research room and click the jars with each", "ResearchJar", "Specimen jars")
                    : V("At the research desk (E), press ? on a pair", "ResearchDesk", "Research desk"),
            },
            new GrowthLessonDef
            {
                Id = "power", Title = "Powered pumps", Era = 3,
                Card = new[]
                {
                    "With the bubble turbine and electric pumps, every grower piped to a buffer fills it by itself, day and night, while there is power.",
                    "Watch the power board: when the draw is more than the supply, the machines lowest on it wait.",
                },
                Appears = s => s.HasGenerator && s.Lines.Any(),
                Done = s => s.PoweredPumps,
                Step = s => s.Level("generator") < 2 ? V("Upgrade the generator to the bubble turbine (E on it)", "Generator", "Generator")
                                                      : V("Upgrade the pumps to electric on the power board (E)", "PowerBoard", "Power board"),
            },
            new GrowthLessonDef
            {
                Id = "nori", Title = "Nori the stocker", Era = 3,
                Card = new[]
                {
                    "Nori carries creatures from the laboratory to the shop's tanks for you. Until you set any stock target, Nori fills the tanks with whatever is waiting; set one, and Nori keeps that many of each species on show.",
                },
                Appears = s => CanHire(s, "nori"),
                Done = s => s.Staff.ContainsKey("nori"),
                Step = s => Hire("Nori", "your stocker"),
            },

            new GrowthLessonDef
            {
                Id = "water", Title = "Water", Era = 3,
                Card = new[]
                {
                    "Creatures come from three waters: fresh, warm and brine. One in the wrong water is still sold, but a tank of its own water keeps it happy and themed décor works only with it.",
                    "Open a tank at night to change its water.",
                },
                Appears = s => s.Era >= GrowthContent.ComfortEra && s.Discovered.Any(d => GrowthContent.Get(d)?.Water != Water.Fresh),
                Done = s => s.Containers.Any(c => c.Water != Water.Fresh),
                Step = s =>
                {
                    var tank = AShopTank(s);
                    return V("At night, open a tank (E) and set its water for your warm-water creatures", tank == null ? null : "Tank:" + tank.Id, "Change its water");
                },
            },

            // ---------------------------------------------------------------- era 4: machines
            new GrowthLessonDef
            {
                Id = "workshop", Title = "The workshop", Era = 4,
                Card = new[]
                {
                    "East of the power room is the workshop: room for the bigger turbines, the hatchling sorter and the lab racks the line fills.",
                },
                Appears = s => s.Era >= 4 && Ready(s.WorkshopCheck(out _)),
                Done = s => s.Workshop,
                Step = s => V("At night, open the workshop: aim at the power room's east wall and press E", "WorkshopWall", "Workshop"),
            },
            new GrowthLessonDef
            {
                Id = "unit-c", Title = "Shop unit C", Era = 4,
                Card = new[]
                {
                    "Behind unit B is shop unit C, with six more places and a feature wall for a Show tank.",
                },
                Appears = s => s.Era >= 4 && Ready(s.UnitCCheck(out _)),
                Done = s => s.UnitC,
                Step = s => V("At night, open shop unit C: aim at unit B's back wall and press E", "UnitCWall", "Shop unit C"),
            },
            new GrowthLessonDef
            {
                Id = "show", Title = "The Show tank", Era = 4,
                Card = new[]
                {
                    "A Show tank stands on a feature place: the biggest tank, and the most appealing one, so visitors go to it first.",
                },
                Appears = s => s.Era >= 4 && CanBuy(s, "show", p => p.Size == "feature"),
                Done = s => s.ShopContainers.Any(c => c.Kind == "show"),
                Step = s =>
                {
                    var place = FreePlace(s, p => p.Size == "feature");
                    return V("At night, aim at a feature place and press E: buy a Show tank", place == null ? null : "Place:" + place, "Feature place");
                },
            },
            new GrowthLessonDef
            {
                Id = "autodoser", Title = "The auto-doser", Era = 4,
                Card = new[]
                {
                    "At level 4 the incubator has a port for a pipe: with a buffer piped to it and power, it doses a recipe by itself, again and again.",
                    "Choose the recipe on the incubator's card and switch the auto-doser on.",
                },
                Appears = s => s.Era >= 4,
                Done = s => s.AutoBatches > 0,
                Step = s =>
                {
                    if (s.Level("incubator") < 4) return V("Upgrade the incubator to level 4 (E on it): the auto-doser's port", "Incubator", "Incubator");
                    if (!s.Pipes.Any(p => p.To == GrowthLab.Port)) return V("Pipe a buffer to the incubator: aim at the buffer (E), then the incubator (E)", "Buffer:0", "Start the pipe here");
                    return V("Open the incubator (E): choose a recipe and switch the auto-doser on", "Incubator", "Incubator");
                },
            },
            new GrowthLessonDef
            {
                Id = "line", Title = "The hatchery line", Era = 4,
                Card = new[]
                {
                    "The sorter in the workshop takes each hatchling down a chute from the incubator to the racks and tanks, by the stock targets you set.",
                    "With the auto-doser, the sorter and Nori, a whole day runs by itself: the line.",
                },
                Appears = s => s.Workshop,
                Done = s => s.HasSorter && s.Chute != null,
                Step = s => !s.HasSorter ? V("At night, buy the sorter in the workshop (E on it)", "Sorter", "Sorter")
                                         : V("Lay its chute: aim at the incubator (E), then the sorter (E)", "Incubator", "Start the chute here"),
            },
            new GrowthLessonDef
            {
                Id = "line-day", Title = "A day on the line", Era = 4,
                Card = new[]
                {
                    "Keep the auto-doser and the sorter running from opening to close, and sell what they stock: that is a day on the line, and part of Era 4's goal.",
                },
                Appears = s => s.HasSorter && s.Chute != null && s.AutoDoser,
                Done = s => s.LineDays > 0,
                Step = s => V("Run a whole day with the auto-doser and the sorter on, open to close", "Sorter", "Sorter"),
            },
            new GrowthLessonDef
            {
                Id = "moss", Title = "Moss the technician", Era = 4,
                Card = new[]
                {
                    "Moss works by hand where no machine reaches: a recipe with no pipes laid, and the job only Moss does, keeping a pair in the breeding tank. You set the tasks on Moss's card.",
                },
                Appears = s => CanHire(s, "moss"),
                Done = s => s.Staff.ContainsKey("moss"),
                Step = s => Hire("Moss", "your technician"),
            },
            new GrowthLessonDef
            {
                Id = "analyzer", Title = "Hints and the planner", Era = 4,
                Card = new[]
                {
                    "The analyzer on the research bench gives a hint a day: one mix or pair you have not tried that makes something new. The planner, its next level, tells what any two creatures you know will breed.",
                },
                Appears = s => s.Analyzer,
            },

            // ---------------------------------------------------------------- era 5: the exhibition
            new GrowthLessonDef
            {
                Id = "hall", Title = "The exhibition hall", Era = 5,
                Card = new[]
                {
                    "At 5 stars the shop can be finished as an exhibition hall: banners, spotlights and two exhibition places. The hall's plans hang in unit C.",
                },
                Appears = s => Ready(s.HallCheck(out _)),
                Done = s => s.Hall,
                Step = s => V("At night, finish the hall: open the hall's plans in unit C (E)", "HallBoard", "Hall plans"),
            },
            new GrowthLessonDef
            {
                Id = "exhibition", Title = "The Grand Exhibition", Era = 5,
                Card = new[]
                {
                    "The Grand Exhibition is the game's ending: crowns on the spotlit places, and guests who come to buy six chosen creatures in one day.",
                    "The hall's plans list what it needs. When everything is ready, prepare it at night: it opens with the shop.",
                },
                Appears = s => s.Hall,
                Done = s => s.ExhibitionPrepared || s.ExhibitionDone || s.ExhibitionAttempts > 0,
                Step = s => V("Open the hall's plans (E): get what the exhibition needs, then prepare it", "HallBoard", "Hall plans"),
            },
            new GrowthLessonDef
            {
                Id = "crowns", Title = "Crowns", Era = 5,
                Card = new[]
                {
                    "Each family has one crown: its rarest creature, bred from one special pair of its own rather than an average. Crowns are what the Grand Exhibition shows.",
                },
                Appears = s => s.CrownsDiscovered > 0 || s.Era >= 5,
            },
            new GrowthLessonDef
            {
                Id = "free-play", Title = "After the exhibition", Era = 5,
                Card = new[]
                {
                    "The shop is yours to keep growing: find all 72 creatures in all four colours (the journal counts them), and hold the exhibition again with other crowns.",
                },
                Appears = s => s.ExhibitionDone,
            },
        };
    }

    public sealed partial class GrowthShop
    {
        /// <summary>R09.4: the lessons met (their card shown: readable in the journal's Guide) and learned (used, or skipped).</summary>
        public readonly HashSet<string> LessonsMet = new HashSet<string>(StringComparer.Ordinal), LessonsLearned = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>R09.4: things done that the shop does not otherwise keep: "board", "journal", "order", "vial", "wish".</summary>
        public readonly HashSet<string> Noted = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>R09.4: the lesson whose steps run now (its card read), or null.</summary>
        public string LessonNow;
        /// <summary>R09.4: lessons off (the setting; the session keeps it from the settings).</summary>
        public bool LessonsOff;

        /// <summary>R09.4: marks something done that the lessons read (opening the pinboard or the journal, an order, a vial, a wish),
        /// and learns its lesson at once: the pinboard and the journal pause the shop, so its tick would come only after them
        /// (and a save made as one closes would miss it).</summary>
        public void Note(string what)
        {
            if (Noted.Add(what)) TickLessons();
        }

        /// <summary>R09.4: lessons run once the guided first night is over (or there was none), and while the setting allows.</summary>
        bool LessonsRun => Guide != GrowthGuideMode.On && !LessonsOff;

        /// <summary>R09.4: the next lesson whose card should open: its system has appeared, it has not been met or learned, in the
        /// lessons' order; null while one runs, while the guide runs, or with lessons off.</summary>
        public GrowthLessonDef LessonDue()
        {
            if (!LessonsRun || LessonNow != null) return null;
            return GrowthLessons.All.FirstOrDefault(l => !LessonsMet.Contains(l.Id) && !LessonsLearned.Contains(l.Id) && l.Appears(this) && (l.Done == null || !l.Done(this)));
        }

        /// <summary>R09.4: a lesson's card is open: it is met (and kept in the Guide); a card only is learned by reading it, the
        /// others' steps start.</summary>
        public void MeetLesson(string id)
        {
            var l = GrowthLessons.Get(id);
            if (l == null) return;
            LessonsMet.Add(id);
            if (l.CardOnly) LessonsLearned.Add(id);
            else LessonNow = id;
        }

        /// <summary>R09.4: stops the lesson now (the card's Skip, or the pause screen's); it is kept as learned, never asked again.</summary>
        public void SkipLesson()
        {
            if (LessonNow == null) return;
            LessonsLearned.Add(LessonNow);
            LessonNow = null;
        }

        /// <summary>R09.4: the step of the lesson now, for the next-step line and the marker; null when none runs.</summary>
        public GrowthGuideView LessonStep()
        {
            if (!LessonsRun || LessonNow == null) return null;
            var l = GrowthLessons.Get(LessonNow);
            return l?.Step?.Invoke(this);
        }

        /// <summary>R09.4: every lesson whose system has been used is learned (a new game as it plays, an existing save at once,
        /// so it meets only lessons for what it has not used); the lesson now ends when its system is used.</summary>
        void TickLessons()
        {
            foreach (var l in GrowthLessons.All)
                if (l.Done != null && !LessonsLearned.Contains(l.Id) && l.Done(this))
                {
                    LessonsLearned.Add(l.Id);
                    if (LessonNow == l.Id)
                    {
                        LessonNow = null;
                        Raise(GrowthEventKind.Lesson, "Lesson learned: " + l.Title + ". It's in your journal's Guide.");
                    }
                }
            if (LessonNow != null && GrowthLessons.Get(LessonNow) == null) LessonNow = null;
        }

        void SaveLessons(GrowthSave s)
        {
            s.lessonsKept = true;
            s.lessonsMet = LessonsMet.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            s.lessonsLearned = LessonsLearned.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            s.noted = Noted.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            s.lessonNow = LessonNow ?? "";
        }

        /// <summary>Version 12 and older had no lessons: what it has used is learned as it is first ticked, and the cards only for
        /// what it has already met (its era past theirs, or their system there) are learned too, so an established shop meets
        /// only the lessons for systems it has not used. A lesson unknown to this version is dropped.</summary>
        static string RestoreLessons(GrowthShop shop, GrowthSave s)
        {
            foreach (var id in s.lessonsMet ?? new string[0]) if (GrowthLessons.Get(id) != null) shop.LessonsMet.Add(id);
            foreach (var id in s.lessonsLearned ?? new string[0]) if (GrowthLessons.Get(id) != null) shop.LessonsLearned.Add(id);
            foreach (var n in s.noted ?? new string[0]) shop.Noted.Add(n);
            shop.LessonNow = string.IsNullOrEmpty(s.lessonNow) || GrowthLessons.Get(s.lessonNow) == null ? null : s.lessonNow;
            if (!s.lessonsKept) shop.LearnWhatIsKnown();
            return null;
        }

        /// <summary>R09.4: an established shop (an older save, a start point): the cards only for what it has reached are learned,
        /// and what the journal and the pinboard teach is learned past era 1; the rest follows from what it has used.</summary>
        public void LearnWhatIsKnown()
        {
            foreach (var l in GrowthLessons.All)
            {
                bool reached = l.CardOnly ? l.Appears(this) || Era > l.Era : l.Done(this);
                if (reached) LessonsLearned.Add(l.Id);
            }
            if (Era >= 2) { LessonsLearned.Add("goals"); LessonsLearned.Add("journal"); }
        }
    }
}
