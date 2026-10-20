using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    // UX-05 lab cards: the bench's one primary, research and module reasons, the rack's target, the vat's start, strings.
    public sealed class LabCardTests
    {
        static ShopUiStrings Table() => ShopUiStrings.LoadDefault();
        static BenchModule Module(bool owned, bool enabled = false) => new BenchModule { Owned = owned, Enabled = enabled };

        [Test] public void BenchPrimaryIsTheNextThingToGetThenToSwitchOn()
        {
            Assert.That(StationCardRules.BenchPrimary(false, false, Module(false), Module(false)), Is.EqualTo(BenchAction.ResearchSpark));
            Assert.That(StationCardRules.BenchPrimary(false, true, Module(true), Module(true)), Is.EqualTo(BenchAction.ResearchSpark), "Spark comes first");
            Assert.That(StationCardRules.BenchPrimary(true, false, Module(false), Module(false)), Is.EqualTo(BenchAction.BuySiphon));
            Assert.That(StationCardRules.BenchPrimary(true, true, Module(false), Module(false)), Is.EqualTo(BenchAction.InstallCollector));
            Assert.That(StationCardRules.BenchPrimary(true, true, Module(true), Module(false)), Is.EqualTo(BenchAction.InstallDoser));
            Assert.That(StationCardRules.BenchPrimary(true, true, Module(true), Module(true)), Is.EqualTo(BenchAction.TurnOnCollector));
            Assert.That(StationCardRules.BenchPrimary(true, true, Module(true, true), Module(true)), Is.EqualTo(BenchAction.TurnOnDoser));
            Assert.That(StationCardRules.BenchPrimary(true, true, Module(true, true), Module(true, true)), Is.EqualTo(BenchAction.None), "nothing left to do");
        }

        [Test] public void SparkResearchSaysWhyItCantStart()
        {
            Assert.That(StationCardRules.SparkBlockedKey(false, true, 100, 48), Is.EqualTo("card.bench.why.starters"));
            Assert.That(StationCardRules.SparkBlockedKey(true, false, 100, 48), Is.EqualTo("card.bench.why.night"));
            Assert.That(StationCardRules.SparkBlockedKey(true, true, 40, 48), Is.EqualTo("card.bench.why.coins"));
            Assert.That(StationCardRules.SparkBlockedKey(true, true, 48, 48), Is.Null);
            Assert.That(Table().Format("card.bench.why.coins", 8), Is.EqualTo("Need 8 more coins"));
        }

        [Test] public void ModulesNeedTheSiphonAPondletAClearNightAndCoins()
        {
            Assert.That(StationCardRules.ModuleBlockedKey(false, true, true, 0, 100, 36), Is.EqualTo("card.bench.why.siphon"));
            Assert.That(StationCardRules.ModuleBlockedKey(true, false, true, 0, 100, 36), Is.EqualTo("card.bench.why.pondlet"));
            Assert.That(StationCardRules.ModuleBlockedKey(true, true, false, 0, 100, 36), Is.EqualTo("card.bench.why.clearNight"));
            Assert.That(StationCardRules.ModuleBlockedKey(true, true, true, 1, 100, 36), Is.EqualTo("card.bench.why.clearNight"), "customers in the shop");
            Assert.That(StationCardRules.ModuleBlockedKey(true, true, true, 0, 20, 36), Is.EqualTo("card.bench.why.coins"));
            Assert.That(StationCardRules.ModuleBlockedKey(true, true, true, 0, 36, 36), Is.Null);
        }

        [Test] public void RackStoresInTheChosenEmptyCompartmentElseTheFirstEmpty()
        {
            var both = new List<RackCompartment> { new RackCompartment { StorageId = "a" }, new RackCompartment { StorageId = "b" } };
            Assert.That(StationCardRules.RackTarget(both, null), Is.EqualTo("a"));
            Assert.That(StationCardRules.RackTarget(both, "b"), Is.EqualTo("b"));
            var lowerFull = new List<RackCompartment> { new RackCompartment { StorageId = "a", SpeciesName = "Pondlet" }, new RackCompartment { StorageId = "b" } };
            Assert.That(StationCardRules.RackTarget(lowerFull, "a"), Is.EqualTo("b"), "a chosen compartment that filled up falls back to an empty one");
            var full = new List<RackCompartment> { new RackCompartment { StorageId = "a", SpeciesName = "Pondlet" }, new RackCompartment { StorageId = "b", SpeciesName = "Ribbonfin" } };
            Assert.That(StationCardRules.RackTarget(full, null), Is.Null);
        }

        [Test] public void VatStartsWithOneOrTwoPondletsAndASpark()
        {
            Assert.That(StationCardRules.VatBlockedKey(0, 0, 1), Is.EqualTo("card.vat.why.none"), "nothing stored to pick");
            Assert.That(StationCardRules.VatBlockedKey(2, 0, 1), Is.EqualTo("card.vat.why.pick"));
            Assert.That(StationCardRules.VatBlockedKey(2, 1, 0), Is.EqualTo("card.vat.why.spark"));
            Assert.That(StationCardRules.VatBlockedKey(2, 1, 1), Is.Null);
            Assert.That(StationCardRules.VatBlockedKey(2, 2, 3), Is.Null);
            Assert.That(StationCardRules.VatCapacity, Is.EqualTo(2));
        }

        [Test] public void ShippedStringsCoverEveryLabCardKey()
        {
            var s = Table();
            foreach (var key in new[]
            {
                "card.rack.compartments", "card.rack.ready", "card.rack.gone", "card.rack.status.full", "card.rack.status.free",
                "card.rack.line", "card.rack.reserved", "card.rack.empty", "card.rack.storeIdle", "card.rack.why.nothing", "card.rack.store",
                "card.rack.why.full", "card.rack.lower", "card.rack.upper",
                "card.vat.pondlets", "card.vat.why.none", "card.vat.sparkReserved", "card.vat.cancel", "card.vat.confirm", "card.vat.keep",
                "card.vat.cancelNow", "card.vat.mist", "card.vat.gone", "card.vat.status.idle", "card.vat.status.glowing", "card.vat.status.ready",
                "card.vat.line.locked", "card.vat.start", "card.vat.why.locked", "card.vat.line.idle", "card.vat.line.glowingOne", "card.vat.line.glowing",
                "card.vat.line.ready", "card.vat.noSpace", "card.vat.sparkOk", "card.vat.sparkMissing", "card.vat.mistUnlocked",
                "card.vat.researchMist", "card.vat.why.mistLocked", "card.vat.mistOffer", "card.vat.why.pick", "card.vat.why.spark",
                "card.bench.culturePrep", "card.bench.spark", "card.bench.siphon", "card.bench.collector", "card.bench.doser", "card.bench.status",
                "card.bench.line", "card.bench.line.spark", "card.bench.sparkDone", "card.bench.sparkDetail", "card.bench.stampDone", "card.bench.stampTodo",
                "card.bench.researched", "card.bench.researchShort", "card.bench.siphonDetail", "card.bench.installed", "card.bench.buyShort",
                "card.bench.collectorDetail", "card.bench.doserDetail", "card.bench.researchSpark", "card.bench.buySiphon", "card.bench.installCollector",
                "card.bench.installDoser", "card.bench.turnOnCollector", "card.bench.turnOnDoser", "card.bench.on", "card.bench.off", "card.bench.installShort",
                "card.bench.turnOff", "card.bench.turnOn", "card.bench.why.starters", "card.bench.why.night", "card.bench.why.coins", "card.bench.why.siphon",
                "card.bench.why.pondlet", "card.bench.why.clearNight", "card.title.Upgrade"
            }) Assert.That(s.Has(key), Is.True, key);
            Assert.That(s.Has("card.preview") || s.Has("card.status.preview"), Is.False, "the UX-02 placeholder strings are gone");
        }

        /// <summary>
        /// The HUD and card stylesheets are both on the panel root, so a class name in both styles both. UX-04's card .es-pip
        /// collapsed the HUD discovery stamp's recipe pips, and UX-05's first .es-stamp tilted the bench's requirement chips.
        /// ShopTokens.uss is the shared base and may be refined by either.
        /// </summary>
        [Test] public void HudAndCardStylesShareNoClassNames()
        {
            string folder = System.IO.Path.Combine(UnityEngine.Application.dataPath, "EvolutionShop/Resources/EvolutionShopUI");
            var hud = SelectorClasses(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "HudStyles.uss")));
            var cards = SelectorClasses(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "CardStyles.uss")));
            Assert.That(hud.Count, Is.GreaterThan(20), "HUD selectors were read");
            Assert.That(cards.Count, Is.GreaterThan(20), "card selectors were read");
            Assert.That(hud.Intersect(cards).OrderBy(name => name).ToArray(), Is.Empty, "class names styled by both the HUD and the cards");
        }

        static HashSet<string> SelectorClasses(string uss)
        {
            // Selectors only: drop comments and every { ... } block (url(...) paths live inside the blocks).
            uss = System.Text.RegularExpressions.Regex.Replace(uss, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline);
            uss = System.Text.RegularExpressions.Regex.Replace(uss, @"\{[^}]*\}", " ");
            var names = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(uss, @"\.([A-Za-z][\w-]*)"))
                names.Add(match.Groups[1].Value);
            return names;
        }

        [Test] public void LabCardButtonsFollowTheCopyRules()
        {
            var s = Table();
            Assert.That(s.Format("card.bench.researchSpark", 48), Is.EqualTo("Research Spark · 48"));
            Assert.That(s.Format("card.vat.start", 12), Is.EqualTo("Start vat · 12s"));
            Assert.That(s.Format("card.rack.store", "Pondlet"), Is.EqualTo("Store Pondlet"));
            foreach (var key in new[] { "card.rack.store", "card.rack.storeIdle", "card.vat.start", "card.vat.cancel", "card.vat.keep", "card.vat.cancelNow",
                "card.vat.researchMist", "card.bench.researchSpark", "card.bench.buySiphon", "card.bench.installCollector", "card.bench.installDoser",
                "card.bench.turnOnCollector", "card.bench.turnOnDoser", "card.bench.installShort", "card.bench.turnOn", "card.bench.turnOff",
                "card.bench.researchShort", "card.bench.buyShort", "card.bench.culturePrep" })
            {
                string text = s.Get(key);
                Assert.That(text.Contains(" / "), Is.False, key + " uses the retired ' / ' separator");
                int words = text.Split(' ').Count(word => word.Any(char.IsLetter));
                Assert.That(words, Is.InRange(1, 4), key + " is 1-4 words: " + text);
            }
        }
    }
}
