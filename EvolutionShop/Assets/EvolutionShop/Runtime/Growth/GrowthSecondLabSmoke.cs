#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-34 wave 2's Windows route (-evolutionGrowthSecondLabSmoke): tester round 2's second lab (F69), played in the real game. The
    /// hoarding in the power room's west wall and what it needs, the room opened and walked into with the real body; its machines'
    /// dashed places; an incubator placed, a recipe queued and made onto its own tray, a hatchling clicked off it; a breeding tank
    /// placed and a pair clicked in; Moss set to the second lab's incubator, walking there and starting his recipe; the pipe's drops
    /// with the first incubator's level 4 and its auto-doser; and the whole room through a save.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string SecondLabScenarioText = "GR-34 tester round 2, the second lab (F69): the hoarding and what it needs; walking in; the machines' places; an incubator placed, its queue and tray; a breeding tank placed and a pair in; Moss working there; the pipe and the auto-doser; a save.";

        IEnumerator SecondLabScenario()
        {
            limitations.Add("GR-34 second lab route: the fixture is labelled: an era 4, four-star shop with 40,000 credits, the power room open, the generator and electric pumps, the incubator at level 3 and the breeding tank at level 1, the Pondlet found and its growers full, a Large tank in the shop; the incubation and breeding times passed with the shop's own clock; the incubator's level 4 set by hand to show the pipe's drops.");
            yield return Opening();
            TakeInput();
            s.AllowValidationInput = true;

            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 40000, Rp = 500, Annex = true, SalesTotal = GrowthContent.PipSalesGate };
            fx.Levels["generator"] = 2; fx.Levels["pump"] = 2; fx.Levels["incubator"] = 3; fx.Levels["breeding"] = 1;
            var pondlet = GrowthContent.Get("pondlet");
            fx.MixLog.Add(new GrowthMixEntry { Doses = (int[])pondlet.Recipe.Clone(), Species = "pondlet", Exact = true });
            if (!fx.Discovered.Contains("pondlet")) fx.Discovered.Add("pondlet");
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)).Where(k => !fx.Journal.Contains(k)).ToList());
            for (int i = 0; i < pondlet.Recipe.Length; i++)
                if (pondlet.Recipe[i] > 0 && !fx.Cultures.Contains(GrowthContent.Cultures[i].Id)) fx.Cultures.Add(GrowthContent.Cultures[i].Id);
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = fx.GrowerCapacityOf(c);
            fx.Containers.Add(new GrowthContainer { Id = "large-a3", Kind = "large", Place = "a3" });
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Annex && !shop.SecondLab && s.World.SecondLabWallStanding, "Fixture: the power room open, the second lab closed behind its hoarding");

            // ---- The hoarding in the power room's west wall, and what it needs
            LookAt(new Vector3(-2.9f, 0, 12.5f), new Vector3(-5f, 1.4f, 12.5f));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.SecondLabWall, "lab2", "The reticle focuses the hoarding");
            s.FocusName(out string prompt);
            Check(prompt.Contains("see what it needs"), "The hoarding says E sees what it needs (" + prompt + ")");
            yield return Shot("1501-hoarding");
            yield return Press(Key.E);
            yield return null; yield return null;
            var open = CardButton("Open the second lab");
            Check(s.Card == GrowthCard.SecondLab && open != null && open.enabledSelf && open.text.Contains(GrowthShop.SecondLabDef.Credits + " cr"), "Its card offers the second lab for its price");
            yield return Shot("1502-the-card");
            yield return ClickOn(e => e is Button b && b.text.StartsWith("Open the second lab"), "Press Open the second lab");
            yield return Wait(() => shop.SecondLab, 3, "the room to open");
            Check(shop.Credits == 40000 - GrowthShop.SecondLabDef.Credits, "It cost its price");
            yield return Wait(() => !s.World.SecondLabWallStanding && !s.World.Revealing("lab2"), 8, "the hoarding to come down");
            Check(!s.World.PartitionShowing("lab2"), "The hoarding is gone");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(s.World.LabIncubatorSlotsShown == 3 && s.World.LabBreederSlotsShown == 3 && s.World.LabIncubatorsShown == 0, "Its three incubator places and three breeding tank places are dashed");

            // ---- Walking in with the real body, through the doorway
            Stand(new Vector3(-3.4f, 0, 12.5f), -90, 0);
            yield return null;
            yield return WalkKeys(8, () => s.Player.transform.position.x < -6.4f, Key.W);
            Check(s.Player.transform.position.x < -6.4f, "Walked through the doorway into the second lab (x " + s.Player.transform.position.x.ToString("0.0") + ")");
            Stand(new Vector3(-5.8f, 0, 9.5f), -125, 10);
            yield return null; yield return null;
            yield return Shot("1503-the-room");

            // ---- An incubator placed at its place, a recipe queued, made onto its own tray
            LookAt(new Vector3(-10.625f, 0, 11.0f), new Vector3(-10.625f, 0.5f, 13.12f));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.LabIncubator, "0", "The reticle focuses the first incubator's place");
            s.FocusName(out prompt);
            Check(prompt.Contains("E place it"), "Its place says E places it (" + prompt + ")");
            yield return Press(Key.E);
            yield return null; yield return null;
            Check(s.Card == GrowthCard.LabIncubator && s.Ui.CardText().Contains("Place incubator 2 · 3000 cr"), "Its card offers incubator 2 for 3,000");
            yield return ClickOn(e => e is Button b && b.name == "gr-buy-lab-incubator", "Place incubator 2");
            yield return null; yield return null;
            Check(shop.LabIncubators.Count == 1 && s.World.LabIncubatorsShown == 1 && s.World.LabIncubatorSlotsShown == 2, "Incubator 2 stands in its place");
            yield return ClickOn(e => e is Button b && b.name == "gr-lab-make-pondlet", "Make a Pondlet");
            var inc = shop.LabIncubators[0];
            Check(inc.Incubating && inc.Pending == "pondlet" && !shop.Incubating, "It starts at once, from the growers; the laboratory's incubator is left alone");
            yield return Shot("1504-incubating");
            s.SetCard(GrowthCard.None);
            shop.Tick(shop.IncubationSeconds + 0.2f);
            yield return null; yield return null;
            Check(inc.Tray.Count == shop.Clutch && s.World.LabTrayCreaturesShown(0) == shop.Clutch, "The clutch is on its own tray, and shows there (" + s.World.LabTrayCreaturesShown(0) + ")");
            LookAt(new Vector3(-10.625f, 0, 11.6f), new Vector3(-10.625f, 0.9f, 12.9f));
            yield return null; yield return null;
            yield return Shot("1505-its-tray");
            s.FocusName(out prompt);
            Check(prompt.Contains("click take"), "Its focus says a click takes it (" + prompt + ")");
            yield return Click();
            Check(shop.Carried.Count == 1 && inc.Tray.Count == shop.Clutch - 1, "A click takes a hatchling off its tray");
            while (inc.Tray.Count > 0) shop.TakeLabHatchling(0);
            shop.Carried.Clear();

            // ---- A breeding tank placed, and a pair clicked in
            LookAt(new Vector3(-10.2f, 0, 6.6f), new Vector3(-12.09f, 0.5f, 6.6f));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.LabBreeder, "0", "The reticle focuses the first breeding tank's place");
            yield return Press(Key.E);
            yield return null; yield return null;
            yield return ClickOn(e => e is Button b && b.name == "gr-buy-lab-breeder", "Place breeding tank 2");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(shop.LabBreeders.Count == 1 && s.World.LabBreedersShown == 1, "Breeding tank 2 stands in its place");
            shop.DevelopmentGive("pondlet"); shop.DevelopmentGive("pondlet");
            yield return null;
            yield return Click();
            yield return Click();
            Check(shop.LabBreeders[0].Pairs[0].Parents.Count == 2 && shop.Carried.Count == 0 && shop.LabBreeders[0].Pairs[0].Breeding, "Two clicks put the pair in, and it breeds");
            yield return null; yield return null;
            Check(s.World.LabBreederCreaturesShown(0) == 2, "The pair shows in the tank");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("1506-a-pair");

            // ---- Moss set to the second lab's incubator: he walks there and starts his recipe
            Ok(shop.Hire("moss"), "Hire Moss");
            s.SetCard(GrowthCard.Helper, "moss");
            yield return null; yield return null;
            yield return ClickOn(e => e is Button b && b.name == "gr-moss-incubator-1", "Moss: his recipe in incubator 2");
            Check(shop.MossIncubator == 1, "His card sets him to incubator 2");
            Ok(shop.SetMossRecipe("pondlet"), "Moss's recipe: a Pondlet");
            s.SetCard(GrowthCard.None);
            Fill(shop);
            yield return Wait(() => inc.Incubating && shop.MossBatches == 1, 20, "Moss to walk to incubator 2 and start his recipe");
            Check(inc.Incubating && shop.MossBatches == 1 && !shop.Incubating, "Moss walked to incubator 2 and started a Pondlet there");
            LookAt(new Vector3(-9.6f, 0, 9.8f), new Vector3(-10.625f, 1.0f, 12.6f));
            yield return null; yield return null;
            yield return Shot("1507-moss-there");

            // ---- The pipe's drops with the first incubator's level 4, and its auto-doser
            shop.Levels["incubator"] = 4;
            yield return null; yield return null;
            Check(s.World.LabPipeDropsShown == 1, "The pipe drops to its auto-doser port (" + s.World.LabPipeDropsShown + ")");
            s.SetCard(GrowthCard.LabIncubator, "0");
            yield return null; yield return null;
            Check(CardButtons(b => b.name == "gr-lab-auto-pondlet").Count == 1, "Its card offers the auto-doser's recipe");
            s.SetCard(GrowthCard.None);
            LookAt(new Vector3(-8.75f, 0, 10.2f), new Vector3(-9.2f, 2.4f, 13.4f));
            yield return null; yield return null;
            yield return Shot("1508-the-pipe");

            // ---- Saved and loaded
            Check(s.SaveNow() && s.ReloadSaved(), "Saved and reloaded");
            shop = s.Shop;
            yield return null; yield return null;
            Check(shop.SecondLab && shop.LabIncubators.Count == 1 && shop.LabBreeders.Count == 1 && shop.LabBreeders[0].Pairs[0].Parents.Count == 2 && shop.MossIncubator == 1, "The second lab, its machines and Moss's place come back");
            Check(s.World.LabIncubatorsShown == 1 && s.World.LabBreedersShown == 1 && !s.World.PartitionShowing("lab2"), "And the world shows them");
        }

        static void Fill(GrowthShop shop) { foreach (var c in shop.Cultures) shop.GrowerUnits[c] = shop.GrowerCapacityOf(c); }
    }
}
#endif
