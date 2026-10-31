#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;

namespace EvolutionShop
{
    /// <summary>
    /// GR-32's Windows route (-evolutionGrowthWordsSmoke): tester round 1's batch 3, played in the real game. The rooms' new
    /// names and the words explained on the cards; the catalog's kinds; the journal letting the shop run, a day ending while it
    /// is open; each creature's own picture; the incubator's and the siphon's sounds, and its burp a rare treat; a tank in the
    /// middle of the shop with nothing behind its glass; and the plain decorations.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string WordsScenarioText = "GR-32 tester round 1, batch 3: the rooms' new names and the words on the cards; the catalog's kinds; the journal letting the shop run; each creature's own picture; the incubator's and the siphon's sounds; a see-through tank in the middle of the shop; the plain decorations.";

        static readonly string[] PlainDecor = { "stones", "fern", "rocks", "bamboo", "shells", "driftwood" };

        IEnumerator WordsScenario()
        {
            limitations.Add("GR-32 route: the fixture is labelled: an era 4, four-star shop with 30,000 credits, unit B open, 12 founders known and five of their recipes found, Large tanks at a2 and a3, and the six plain decorations in the box.");
            yield return Opening();
            TakeInput();
            s.AllowValidationInput = true;

            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 30000, Rp = 300, UnitB = true, SalesTotal = 160 };
            var founders = GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).ToList();
            fx.Discovered.AddRange(founders.Select(x => x.Id));
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            foreach (var f in founders.Take(5)) fx.MixLog.Add(new GrowthMixEntry { Doses = (int[])f.Recipe.Clone(), Species = f.Id, Exact = true });
            fx.Levels["grower"] = 2; fx.Levels["incubator"] = 1; fx.Levels["counter"] = 1; fx.Levels["breeding"] = 1;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            fx.Containers.Add(new GrowthContainer { Id = "words-a3", Kind = "large", Place = "a3" });
            fx.Containers.Add(new GrowthContainer { Id = "words-a2", Kind = "large", Place = "a2" });
            fx.DecorOwned.AddRange(PlainDecor);
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;

            // ---- F46: the rooms' new names; F49: Era with its capital
            Check(GrowthContent.Eras[0].Name == "Laboratory" && GrowthContent.Eras[0].Opens.StartsWith("The laboratory") && GrowthShop.AnnexDef.Name == "Power room", "The rooms are the laboratory and the power room");
            var board = shop.Board();
            Check(board.NextTitle == null || board.NextTitle.StartsWith("Next: Era") || board.NextTitle.StartsWith("Now"), "Era has its capital on the pinboard (" + board.NextTitle + ")");
            yield return AimAt(GrowthWorld.GrowerPositions["bloom"] + new Vector3(1.4f, 0, 0), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0), GrowthTargetKind.Grower, "bloom", "The reticle focuses the Bloom grower");

            // ---- F39: what a culture is; F55: the siphon draws out loud
            yield return Press(UnityEngine.InputSystem.Key.E);
            yield return null; yield return null;
            Check(s.Card == GrowthCard.Grower && s.Ui.CardText().Contains("Bloom is a culture: a gene your creatures are made from."), "The grower's card says what a culture is");
            yield return Shot("1300-culture-explained");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Hold(true);
            yield return Wait(() => s.CollectingNow, 3, "the siphon to start");
            yield return new WaitForSeconds(0.2f);
            Check(s.Audio.DrawPlaying, "Holding the mouse on a grower, the siphon's draw plays");
            yield return Wait(() => shop.Held.Count >= 2, 8, "two doses");
            Hold(false);
            yield return new WaitForSeconds(0.7f);
            Check(!s.Audio.DrawPlaying, "Letting go, it stops");

            // ---- F42: the bench says the siphon is upgraded there
            yield return AimAt(GrowthWorld.WorkbenchPosition + new Vector3(-1.3f, 0, 0), GrowthWorld.WorkbenchPosition + new Vector3(0, 1.0f, 0), GrowthTargetKind.Workbench, null, "The reticle focuses the research bench");
            s.FocusName(out string status);
            Check(status.Contains("upgrade your siphon"), "The bench says the siphon is upgraded there (" + status + ")");
            yield return Press(UnityEngine.InputSystem.Key.E);
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("Your siphon is upgraded here"), "And its card says so first");
            yield return Shot("1301-bench");
            s.SetCard(GrowthCard.None);
            yield return null;

            // ---- F55: the incubator bubbles while it works, and says it is done with two soft notes, not a burp; F45: no clutch
            yield return AimAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0), GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            yield return Click();
            Ok(shop.StartIncubation(), "Start the mix (the card's button)");
            yield return new WaitForSeconds(0.5f);
            Check(s.Audio.BrewPlaying, "While it works, the incubator bubbles");
            int readies = s.Audio.Readies;
            s.Gags.HatchesSinceBurp = 0;
            s.Advance(shop.IncubationSeconds + 0.05f);
            yield return Wait(() => s.Audio.Readies == readies + 1, 3, "the incubator's two notes");
            yield return new WaitForSeconds(GrowthGags.BelchDelay + 0.3f);
            Check(s.Gags.Policy.Current != GrowthGag.Belch && !s.Audio.BrewPlaying, "It finishes with its two notes and no burp, and stops bubbling");
            Ok(shop.TakeHatchling(), "Take the hatchling (a click)");

            // ---- F44 and F47: the counter's words, and each catalog item says what kind of thing it is
            var counterAim = GrowthWorld.CounterPosition + new Vector3(0, 0.9f, 0);
            yield return AimAt(GrowthWorld.CounterPosition + new Vector3(0, 0, -1.25f), counterAim, GrowthTargetKind.Counter, null, "The reticle focuses the counter");
            yield return Press(UnityEngine.InputSystem.Key.E);
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("a day lasts 5 minutes 30 seconds"), "A day's length has its units");
            s.SetCard(GrowthCard.None);
            yield return null;
            s.SetCard(GrowthCard.Catalog);
            yield return null; yield return null;
            string card = s.Ui.CardText();
            Check(card.Contains("DECORATION") && card.Contains("STARTER VIAL") && card.Contains("River stones") && card.Contains("Order tonight: it arrives when you open the shop"), "The catalog names each kind of thing, and when it arrives");
            yield return Shot("1302-catalog-page-2");
            s.SetCard(GrowthCard.None);
            yield return null;

            // ---- F56: nothing behind the glass of a tank in the middle of the shop; one by a wall keeps its backdrop
            var middle = shop.Containers.First(c => c.Place == "a3");
            var byWall = shop.Containers.First(c => c.Place == "a2");
            yield return null; yield return null;
            Check(GrowthSpaces.FreeStanding("a3") && !GrowthSpaces.FreeStanding("a2"), "a3 stands in the middle of the shop, a2 by a wall");
            Check(!s.World.Tanks[middle.Id].Root.Cast<Transform>().Any(c => c.name == "Look / TankBackdropLarge") && s.World.Tanks[byWall.Id].Root.Cast<Transform>().Any(c => c.name == "Look / TankBackdropLarge"), "The tank in the middle has no backdrop, the one by the wall does");
            for (int i = 0; i < 3; i++) Ok(shop.DevelopmentStock(middle.Id, "pondlet"), "Stock a Pondlet in the middle tank");
            yield return null; yield return null;
            LookAt(GrowthWorld.Places["a3"].Position + new Vector3(0.4f, 0, 2.2f), GrowthWorld.Places["a3"].Position + new Vector3(0, 1.15f, 0));
            yield return Shot("1303-middle-tank-from-behind");
            LookAt(GrowthWorld.Places["a3"].Position + new Vector3(0.4f, 0, -2.2f), GrowthWorld.Places["a3"].Position + new Vector3(0, 1.15f, 0));
            yield return Shot("1304-middle-tank-from-the-front");

            // ---- F57: the plain decorations, each on a tank's top
            var tanks = new[] { middle, byWall, shop.Containers.First(c => c.Place == "a1") };
            foreach (var decor in PlainDecor)
            {
                var def = GrowthContent.DecorPiece(decor);
                var on = tanks[0];
                if (on.Decor != null) Ok(shop.TakeDecor(on.Id), "Take the last piece off");
                Ok(shop.PutDecor(decor, on.Id), "Put " + def.Name + " on the tank");
                yield return null; yield return null;
                var holder = s.World.DecorOf(on.Id);
                Check(holder != null && HasKit(holder, def.Asset), def.Name + " stands on the tank as the kit's piece");
                var at = holder.position;
                LookAt(at + new Vector3(0.75f, -1.4f, 0.75f), at + new Vector3(0, 0.15f, 0));
                yield return Shot("131" + System.Array.IndexOf(PlainDecor, decor) + "-" + decor);
            }

            // ---- F54: each creature's own picture, in the journal
            int shown = s.Ui.PortraitsShown;
            s.SetCard(GrowthCard.Journal);
            yield return null; yield return null;
            s.Ui.ShowTabNamed("Creatures");
            yield return null; yield return null;
            Check(s.Ui.PortraitsShown > shown, "The journal shows the creatures' own pictures (" + (s.Ui.PortraitsShown - shown) + ")");
            yield return Shot("1305-journal-pictures");
            s.SetCard(GrowthCard.None);

            // ---- F54: the same in the breeding tank
            shop.Carried.Clear();
            Ok(shop.DevelopmentGive("pondlet"), "A Pondlet in hand");
            Ok(shop.DevelopmentGive("ribbonfin"), "A Ribbonfin in hand");
            foreach (var c in shop.Carried.ToList()) Ok(shop.PutParent(c.Id), "Put a parent in the breeding tank");
            s.SetCard(GrowthCard.Breeding);
            yield return null; yield return null;
            yield return Shot("1306-breeding-pictures");
            s.SetCard(GrowthCard.None);

            // ---- F50: the journal lets the shop run; pause, Settings and lessons still stop it; a day can end while it is open
            foreach (var c in shop.ShopContainers.ToList()) if (c.Occupants.Count == 0) Ok(shop.DevelopmentStock(c.Id, "pondlet"), "Stock a tank");
            Ok(shop.OpenShop(), "Open the shop");
            yield return null;
            s.SetCard(GrowthCard.Journal);
            yield return null;
            Check(!s.Paused, "The journal does not pause the shop");
            float before = shop.DaySecondsLeft;
            yield return new WaitForSeconds(1.2f);
            Check(shop.DaySecondsLeft < before - 0.8f, "With the journal open, the day's clock runs (" + before.ToString("0.0") + " to " + shop.DaySecondsLeft.ToString("0.0") + ")");
            s.SetCard(GrowthCard.Pause);
            yield return null;
            Check(s.Paused, "The pause menu still stops time");
            s.SetCard(GrowthCard.Journal);
            yield return null;
            s.Advance(shop.DaySecondsLeft + 1);
            yield return Wait(() => s.Card == GrowthCard.Receipt, 4, "the day's receipt");
            Check(s.Card == GrowthCard.Receipt, "A day ending while the journal is open opens the receipt");
            yield return Shot("1307-receipt-from-the-journal");
            s.SetCard(GrowthCard.None);
        }
    }
}
#endif
