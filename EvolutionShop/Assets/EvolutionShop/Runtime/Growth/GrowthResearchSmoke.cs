#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-30's Windows route (-evolutionGrowthResearchSmoke): tester round 1's research room, played in the real game. The hoarding
    /// in the power room's west wall, refused without the generator and opened with it; walked through with the real body; creatures
    /// clicked into jars and taken back; pairs and mixes queued at the desk and worked through by the tanks on the generator's power;
    /// new creatures on the tray; what is tried named, and what the microscope and planner add; the incubator's and journal's words;
    /// and the whole room through a save.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string ResearchScenarioText = "GR-30 tester round 1, the research room: the hoarding and what it needs; walking in; jars; the desk's pairs and mixes; the tanks on the generator's power; the tray; already tried; the microscope and planner; the incubator's tried mixes; the journal's how to make it; a save mid-way.";

        List<Button> CardButtons(System.Func<Button, bool> which) =>
            s.Ui.CardElement == null ? new List<Button>() : UQueryExtensions.Query<Button>(s.Ui.CardElement).ToList().Where(which).ToList();

        IEnumerator ResearchScenario()
        {
            limitations.Add("GR-30 route: the fixture is labelled: an era 3, three-star shop with 9,000 credits, the power room open, four founders known and every culture's grower full, the generator set by hand at the step that shows the room waiting for it, and the bench's level set by hand to show the microscope and the planner.");
            yield return Opening();
            TakeInput();
            s.AllowValidationInput = true;

            var fx = new GrowthShop { Era = 3, Reputation = GrowthContent.StarThresholds[2] + 10, Credits = 9000, Rp = 300, Annex = true };
            var founders = GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(5).Select(x => x.Id).ToList();
            fx.Discovered.AddRange(founders);
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Check(shop.Annex && !shop.HasGenerator && !shop.ResearchRoom && s.World.ResearchWallStanding, "Fixture: the power room open, no generator, the research room closed behind its hoarding");

            // ---- F1: the hoarding in the power room's west wall, and what it needs
            LookAt(new Vector3(-2.4f, 0, 15.35f), new Vector3(-4f, 1.4f, 15.35f));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.ResearchWall, "research", "The reticle focuses the hoarding");
            s.FocusName(out string prompt);
            Check(prompt.Contains("see what it needs"), "The hoarding says E sees what it needs (" + prompt + ")");
            yield return Shot("1301-hoarding");
            yield return Press(Key.E);
            yield return null; yield return null;
            var open = CardButton("Open the research room");
            Check(s.Card == GrowthCard.ResearchWall && open != null && !open.enabledSelf && s.Ui.CardText().Contains("Buy the generator first"), "Without the generator its button is off, and the card says why, in plain sight");
            yield return Shot("1302-needs-the-generator");
            shop.Levels["generator"] = 2;
            s.Ui.RefreshCard();
            yield return null; yield return null;
            open = CardButton("Open the research room");
            Check(open != null && open.enabledSelf && open.text.Contains(GrowthShop.ResearchDef.Credits + " cr"), "With the generator it opens, for its price");
            yield return Shot("1303-ready");
            yield return ClickOn(e => e is Button b && b.text.StartsWith("Open the research room"), "Press Open the research room");
            yield return Wait(() => shop.ResearchRoom, 3, "the room to open");
            Check(shop.Credits == 9000 - GrowthShop.ResearchDef.Credits && shop.Level("research") == 1 && shop.PowerOrder.Contains(GrowthShop.ResearchPower), "It cost its price, and its tanks are on the power board");
            yield return Wait(() => !s.World.ResearchWallStanding && !s.World.Revealing("research"), 8, "the hoarding to come down");
            Check(!s.World.PartitionShowing("research"), "The hoarding is gone");
            s.SetCard(GrowthCard.None);
            yield return null;
            LookAt(new Vector3(-0.6f, 0, 15.35f), new Vector3(-6f, 1.3f, 15.35f));
            yield return null; yield return null;
            yield return Shot("1303b-the-doorway");

            // ---- Walking in with the real body, through the doorway
            Stand(new Vector3(-2.6f, 0, 15.35f), -90, 0);
            yield return null;
            yield return WalkKeys(8, () => s.Player.transform.position.x < -6f, Key.W);
            Check(s.Player.transform.position.x < -6f, "Walked through the doorway into the room (x " + s.Player.transform.position.x.ToString("0.0") + ")");
            Stand(new Vector3(-4.7f, 0, 15.35f), -100, 8);
            yield return null; yield return null;
            yield return Shot("1304-the-room");
            LookAt(new Vector3(-7.4f, 0, 15.35f), new Vector3(-3f, 1.3f, 15.35f));
            yield return null; yield return null;
            yield return Shot("1304b-the-doorway-from-inside");

            // ---- F1: a click puts a creature in a jar; one of each; the take button takes it back
            shop.DevelopmentGive(founders[0]); shop.DevelopmentGive(founders[1]); shop.DevelopmentGive(founders[2]);
            LookAt(new Vector3(-8.2f, 0, 13.6f), GrowthWorld.JarPosition(7));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.ResearchJar, "7", "The reticle focuses a jar");
            s.FocusName(out prompt);
            Check(prompt.Contains("Click put yours in"), "A jar says a click puts yours in (" + prompt + ")");
            yield return Click();
            Check(shop.Specimens.Count == 1 && shop.Carried.Count == 2 && s.Card == GrowthCard.None, "A click puts a creature in a jar, and opens nothing");
            yield return Click();
            yield return Click();
            Check(shop.Specimens.Count == 3 && shop.Carried.Count == 0, "The others go in too");
            Check(shop.Stock.All(x => !shop.Specimens.Contains(x)), "They are not on sale");
            shop.DevelopmentGive(founders[0]);
            yield return Click();
            Check(shop.Specimens.Count == 3 && shop.Carried.Count == 1, "A second of one kind stays in the hand");
            shop.Carried.Clear();
            yield return null;
            Check(s.World.JarCreaturesShown == 3, "The jars show the three creatures");
            LookAt(new Vector3(-8.2f, 0, 12.55f), GrowthWorld.JarPosition(0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.ResearchJar, "0", "The reticle focuses the first jar");
            s.FocusName(out prompt);
            Check(prompt.Contains("right-click take it back"), "A full jar says the right button takes it back (" + prompt + ")");
            yield return Click(1);
            Check(shop.Specimens.Count == 2 && shop.Carried.Count == 1, "The take button takes it back");
            yield return Click();
            Check(shop.Specimens.Count == 3 && shop.Carried.Count == 0, "And a click puts it in again");
            shop.DevelopmentGive(founders[3]);
            yield return Click();
            Check(shop.Specimens.Count == 4, "A fourth kind");
            LookAt(new Vector3(-8.0f, 0, 13.6f), new Vector3(-9.8f, 1.4f, 13.6f));
            yield return null; yield return null;
            yield return Shot("1305-jars");

            // ---- F4: the desk's grid; a press queues a pair; the tank takes it
            LookAt(new Vector3(-6.9f, 0, 13.3f), GrowthWorld.ResearchDeskPosition + new Vector3(0, 1.4f, -0.1f));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.ResearchDesk, "desk", "The reticle focuses the desk");
            yield return Shot("1306-desk");
            yield return Press(Key.E);
            yield return null; yield return null;
            string card = s.Ui.CardText();
            Check(s.Card == GrowthCard.Research && card.Contains("Queue every pair left (6)"), "The desk opens on the grid: 6 pairs left");
            Check(CardButtons(b => b.text == "?").Count == 12, "Each pair has its ? on both sides of the grid");
            yield return Shot("1307-desk-grid");
            yield return ClickOn(e => e is Button b && b.text == "?", "Press ? on a pair");
            yield return Wait(() => shop.ResearchRunning.Any(), 2, "a tank to take the pair");
            var first = shop.ResearchRunning.First();
            Check(!first.IsMix && first.Tank == 0, "A tank takes the pair at once");
            yield return new WaitForSeconds(0.3f);
            Check(CardButtons(b => b.text == "…").Count == 2 && CardButtons(b => b.text == "?").Count == 10, "The grid says it is in a tank");
            yield return Press(Key.Escape);
            LookAt(new Vector3(-8.7f, 0, 15.2f), GrowthWorld.ResearchTankPosition(0) + new Vector3(0, 0.9f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.ResearchTank, "0", "The reticle focuses the first tank");
            s.FocusName(out prompt);
            Check(prompt.Contains("Tank 1:") && prompt.Contains(" s"), "It says what it holds and how long is left (" + prompt + ")");
            Check(s.World.TankCreaturesShown(0) == 2, "The pair swims in it");
            yield return Wait(() => s.World.TankTicksLit(0) >= 4, 12, "the timer to fill");
            Check(shop.PowerUse(GrowthShop.ResearchPower).Draw == GrowthShop.ResearchTankUnits, "It draws a unit of power");
            yield return Shot("1308-tank-at-work");
            // GR-33 (F59, F60): E on a tank opens the desk on its Tanks tab, with the job, a bar and the time left, live.
            yield return Press(Key.E);
            yield return null; yield return null;
            Check(s.Card == GrowthCard.Research && s.Ui.CurrentTab == GrowthUi.ResearchTanksTab, "F59: E on a tank opens the desk on its Tanks tab (" + s.Ui.CurrentTab + ")");
            string tanks = s.Ui.CardText();
            Check(tanks.Contains("Tank 1") && tanks.Contains(" s left") && tanks.Contains(GrowthContent.Get(first.A).Name + " × " + GrowthContent.Get(first.B).Name), "F60: it shows the pair in tank 1 and the time left");
            int builds = s.Ui.CardBuilds;
            string Left(string text) { int at = text.IndexOf(" s left"); return at < 3 ? "" : text.Substring(at - 3, 7); }
            string leftBefore = Left(tanks);
            yield return new WaitForSeconds(2.2f);
            string after = s.Ui.CardText();
            Check(s.Ui.CardBuilds == builds && Left(after) != leftBefore && leftBefore != "", "F61: the time left counts down in place, without rebuilding the card");
            yield return Shot("1308b-tanks-tab");
            yield return Press(Key.Escape);
            LookAt(new Vector3(-8.2f, 0, 12.55f), GrowthWorld.JarPosition(0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.ResearchJar, "0", "The reticle focuses the first jar again");
            yield return Press(Key.E);
            yield return null; yield return null;
            string jarsText = s.Ui.CardText();
            Check(s.Card == GrowthCard.Research && s.Ui.CurrentTab == GrowthUi.ResearchJarsTab && jarsText.Contains("IN YOUR HANDS") && jarsText.Contains("IN THE JARS · " + shop.Specimens.Count + " OF"),
                "F59, F63: E on a jar opens the Jars tab: what you carry, then the jars, each under its heading (" + s.Card + ", " + s.Ui.CurrentTab + ", " + shop.Specimens.Count + " jars)");
            Check(jarsText.Contains("PUT A CREATURE IN A JAR") || jarsText.Contains("Put a creature in a jar: one of each kind"), "F70: the Jars tab says what jars are for, in four steps");
            Check(jarsText.Contains("A tank breeds them by itself") && jarsText.Contains("Anything new waits on the tray"), "F70: the steps go on to the tank and the tray");
            int emptyShown = UQueryExtensions.Query<VisualElement>(s.Ui.CardElement, className: "gr-rs-jar--empty").ToList().Count;
            Check(emptyShown == Mathf.Min(3, GrowthShop.SpecimenJars - shop.Specimens.Count) && (jarsText.Contains("Click a jar with a creature in hand") || jarsText.Contains("Press Put in a jar above")), "F70: the empty jars say how to fill one (" + emptyShown + " shown)");
            yield return Shot("1308c-jars-tab");
            yield return Press(Key.Escape);
            // F70: the sign over the jar wall says what the jars are for.
            LookAt(new Vector3(-7.4f, 0, 13.6f), new Vector3(-9.9f, 2.0f, 13.6f));
            yield return null; yield return null;
            yield return Shot("1308d-jar-wall-sign");
            LookAt(new Vector3(-8.7f, 0, 15.2f), GrowthWorld.ResearchTankPosition(0) + new Vector3(0, 0.9f, 0));
            yield return null; yield return null;

            // ---- F4: every pair left, three at once
            Check(CardButton("Queue every pair left") == null, "Closed, the card is gone");
            LookAt(new Vector3(-6.9f, 0, 13.3f), GrowthWorld.ResearchDeskPosition + new Vector3(0, 1.4f, -0.1f));
            yield return null; yield return null;
            yield return Press(Key.E);
            yield return ClickOn(e => e is Button b && b.text.StartsWith("Queue every pair left"), "Press Queue every pair left");
            yield return Wait(() => shop.ResearchRunning.Count() == 3, 3, "three tanks at work");
            Check(shop.ResearchJobs.Count == 6 && shop.PowerUse(GrowthShop.ResearchPower).Draw == 3 * GrowthShop.ResearchTankUnits, "All six queued, three tanks drawing three units");
            yield return new WaitForSeconds(0.2f);
            Check(s.Ui.CardText().Contains("3 of 3 tanks at work · 3 waiting"), "The desk says how it goes (" + s.Ui.CardText().Substring(0, Mathf.Min(70, s.Ui.CardText().Length)) + ")");
            yield return Shot("1309-queue");
            s.SetCard(GrowthCard.None);
            LookAt(new Vector3(-7.3f, 0, 14.2f), GrowthWorld.ResearchTankPosition(1) + new Vector3(0, 0.9f, 0));
            yield return null; yield return null;
            yield return Shot("1310-three-tanks");
            yield return Wait(() => shop.ResearchJobs.Count == 0, 60, "the queue to finish");
            Check(shop.ResearchDone == 6 && shop.PairLog.Count == 6 && shop.ResearchTray.Count > 0, "All six done by themselves; " + shop.ResearchTray.Count + " new on the tray");
            Check(shop.PairLog.All(p => GrowthContent.Get(p.Species).Kind != SpeciesKind.Founder), "F8: none of them made a founder");
            Check(shop.ResearchTray.All(x => shop.Discovered.Contains(x.Species)), "What is on the tray is in the journal");
            yield return null; yield return null;
            Check(s.World.TrayCreaturesShown == shop.ResearchTray.Count, "The tray shows them");
            Check(s.World.ScreenCellsTried == 12, "The desk's screen lights the pairs tried");

            // ---- F72: what each job made, read on the Tanks tab, a repeat too
            s.SetCard(GrowthCard.Research, GrowthUi.ResearchTanksTab);
            yield return new WaitForSeconds(0.2f);
            string results = s.Ui.CardText();
            Check(shop.ResearchResults.Count == 6 && results.Contains("RESULTS · THE LAST 6") && shop.ResearchResults.All(r => results.Contains(GrowthContent.Get(r.Made).Name)),
                "F72: the Tanks tab lists what each of the six jobs made (" + string.Join(", ", shop.ResearchResults.Select(r => GrowthContent.Get(r.Made).Name + (r.WasNew ? " new" : " repeat"))) + ")");
            yield return Shot("1310-results");
            s.SetCard(GrowthCard.None);

            // ---- F74: the family tree: found creatures joined to the pair that treeChild them, the rest a "?"
            s.SetCard(GrowthCard.Research, GrowthUi.FamilyTreeTab);
            yield return new WaitForSeconds(0.2f);
            Check(s.Ui.CurrentTab == GrowthUi.FamilyTreeTab, "F74: the desk has a Family tree tab (" + s.Ui.CurrentTab + ")");
            var treeChild = shop.PairLog.Select(p => GrowthContent.Get(p.Species)).First();
            yield return ClickOn(e => e is Button b && b.name == "gr-tree-family-" + treeChild.Family, "Choose the family of the first pair's child");
            yield return new WaitForSeconds(0.2f);
            string tree = s.Ui.CardText();
            var family = GrowthContent.Species.Where(x => x.Family == treeChild.Family).ToList();
            var nodes = UQueryExtensions.Query<VisualElement>(s.Ui.CardElement, className: "gr-tree__node").ToList();
            Check(nodes.Count == family.Count, "F74: a node for each of the family's " + family.Count + " creatures (" + nodes.Count + ")");
            Check(tree.Contains(treeChild.Name) && family.Where(x => !shop.Discovered.Contains(x.Id)).All(x => !tree.Contains(x.Name)) && tree.Contains("Not found yet") == family.Any(x => !shop.Discovered.Contains(x.Id)),
                "F74: what is found is named, what is not is a ? and not named");
            var have = family.Where(x => shop.Discovered.Contains(x.Id)).Select(x => x.Id).ToList();
            int possible = have.Count * (have.Count - 1) / 2;
            int treeTried = shop.PairLog.Where(p => p.A != p.B && have.Contains(p.A) && have.Contains(p.B)).Select(p => string.CompareOrdinal(p.A, p.B) < 0 ? p.A + p.B : p.B + p.A).Distinct().Count();
            Check(tree.Contains("Pairs left to try") && tree.Contains(have.Count < 2 ? "Find two of the family" : "of " + possible + ", with the " + have.Count), "F74: the pairs left to try with the family's creatures found (" + (possible - treeTried) + " of " + possible + ")");
            yield return Shot("1310b-family-tree");
            yield return ClickOn(e => e is Button b && b.name == "gr-tree-to-pairs", "Show them on Pairs");
            yield return new WaitForSeconds(0.2f);
            Check(s.Ui.CurrentTab == "Pairs", "F74: its link opens the Pairs tab");
            s.SetCard(GrowthCard.None);

            // ---- F1: the tray
            int onTray = shop.ResearchTray.Count;
            LookAt(new Vector3(-8.8f, 0, 13.0f), GrowthWorld.ResearchTrayPosition + new Vector3(0, 0.9f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.ResearchTray, "tray", "The reticle focuses the tray");
            yield return Shot("1311-tray");
            yield return Click();
            Check(shop.Carried.Count == 1 && shop.ResearchTray.Count == onTray - 1, "A click takes a new creature off the tray");
            shop.Carried.Clear();

            // ---- F7: a tried pair names what it makes
            s.SetCard(GrowthCard.Research);
            yield return new WaitForSeconds(0.2f);
            var tried = UQueryExtensions.Query<VisualElement>(s.Ui.CardElement, className: "gr-rs-cell--tried").ToList();
            Check(tried.Count == 12 && tried.All(x => x.tooltip.Contains(" makes ")), "Each tried pair shows what it made, and names it");
            Check(s.Ui.PortraitsShown > 0, "With its picture");
            Check(s.Ui.CardText().Contains("A new creature on the tray") || s.Ui.CardText().Contains("new creatures on the tray") || onTray == 1, "The desk says what waits on the tray");
            yield return Shot("1312-grid-tried");
            var again = shop.QueuePair(founders[0], founders[1]);
            Check(again.Failure == GrowthFailure.AlreadyTried && again.Detail.StartsWith("Already tried: makes"), "A tried pair is refused, and says what it makes (" + again.Detail + ")");

            // ---- F2: a mix, on the second tab
            Check(s.Ui.ShowTabNamed("Mixes"), "The Mixes tab");
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("takes its doses from your buffers and growers"), "It says where its doses come from");
            string culture = shop.Cultures[0];
            float before = shop.GrowerUnits[culture];
            yield return ClickOn(e => e is Button b && b.text == "+" && b.enabledSelf, "Press + on the first culture");
            yield return null;
            var queueMix = CardButton("Queue this mix");
            Check(queueMix != null && queueMix.enabledSelf, "A dose in, the mix can be queued");
            yield return Shot("1313-mixes");
            yield return ClickOn(e => e is Button b && b.text == "Queue this mix", "Press Queue this mix");
            var doses = new int[GrowthContent.Cultures.Length]; doses[GrowthContent.CultureIndex(culture)] = 1;
            yield return Wait(() => shop.QueuedMix(doses) != null && shop.QueuedMix(doses).Running || shop.MixEntry(doses) != null, 3, "a tank to take the mix");
            Check(shop.GrowerUnits[culture] <= before - 0.5f, "It took its dose from the grower (" + before.ToString("0.0") + " to " + shop.GrowerUnits[culture].ToString("0.0") + ")");
            yield return null; yield return null;
            Check(s.World.TankDosesShown(shop.QueuedMix(doses).Tank) == 1, "A drop of the culture swims in the tank");
            s.SetCard(GrowthCard.None);
            LookAt(new Vector3(-8.7f, 0, 15.2f), GrowthWorld.ResearchTankPosition(shop.QueuedMix(doses).Tank) + new Vector3(0, 0.9f, 0));
            yield return null; yield return null;
            yield return Shot("1314-mix-in-a-tank");
            yield return Wait(() => shop.MixEntry(doses) != null, 30, "the mix to finish");
            s.SetCard(GrowthCard.Research);
            s.Ui.ShowTabNamed("Mixes");
            yield return null; yield return null;
            yield return ClickOn(e => e is Button b && b.text == "+" && b.enabledSelf, "Press + on the first culture again");
            yield return null; yield return null;
            card = s.Ui.CardText();
            string made = GrowthContent.Get(shop.MixEntry(doses).Species).Name;
            queueMix = CardButton("Queue this mix");
            Check(card.Contains("Already tried: this makes " + made) && queueMix != null && !queueMix.enabledSelf, "F7: a repeat says so and names what it makes (" + made + ")");
            Check(card.Contains("MIXES TRIED · 1") && card.Contains(" makes " + made), "F6: the mixes tried, listed");
            yield return Shot("1315-mix-already-tried");

            // ---- F3: what the microscope and the planner add
            Check(s.Ui.ShowTabNamed("Jars"), "The Jars tab");
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("IN THE JARS · 4 OF " + GrowthShop.SpecimenJars) && CardButtons(b => b.text == "Take back").Count == 4, "It lists the jars, each with Take back");
            yield return Shot("1316-jars-tab");
            shop.DevelopmentGive(founders[4]);
            yield return new WaitForSeconds(0.25f); // the card shows what is in hand when it next rebuilds
            yield return ClickOn(e => e is Button b && b.text == "Put in a jar" && b.enabledSelf, "Press Put in a jar");
            Check(shop.Specimens.Count == 5, "A fifth kind goes in from the card");
            shop.Levels["bench"] = 1;
            s.Ui.ShowTabNamed("Pairs");
            yield return new WaitForSeconds(0.2f);
            Check(CardButtons(b => b.text == "★").Count == 0, "Without the microscope, no pair is marked");
            shop.Levels["bench"] = 2;
            yield return new WaitForSeconds(0.25f);
            int looksNew = shop.PairsLeft().Count(p => shop.PairLooksNew(p.a, p.b));
            Check(CardButtons(b => b.text == "★").Count == looksNew * 2, "With the microscope, a star on each pair that would be new (" + looksNew + ")");
            yield return Shot("1317-microscope");
            shop.Levels["bench"] = 4;
            yield return new WaitForSeconds(0.25f);
            Check(CardButtons(b => b.tooltip != null && b.tooltip.Contains("the planner says")).Count == shop.PairsLeft().Count * 2, "With the planner, each cell names what it would make");
            yield return Shot("1318-planner");

            // ---- The upgrade tab
            Check(s.Ui.ShowTabNamed("Upgrade"), "The Upgrade tab");
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("Research room · level 1 of 3") && s.Ui.CardText().Contains("Quicker sampling"), "The room's upgrade shows what it does");
            yield return Shot("1319-upgrade");
            s.SetCard(GrowthCard.None);

            // ---- F6, F7: the incubator
            shop.Chamber.Add(culture);
            s.SetCard(GrowthCard.Incubator);
            yield return null; yield return null;
            card = s.Ui.CardText();
            Check(card.Contains("Already tried: this makes " + made), "F7: the incubator says a repeat is one, and names it");
            Check(card.Contains("TRIED LATELY") && card.Contains(" makes " + made), "F6: and lists the mixes tried");
            yield return Shot("1320-incubator-tried");
            shop.Chamber.Clear();
            s.SetCard(GrowthCard.None);

            // ---- F5: the journal says how to make what is known
            s.SetCard(GrowthCard.Journal);
            s.Ui.ShowTabNamed("Creatures");
            yield return null; yield return null;
            var tips = UQueryExtensions.Query<VisualElement>(s.Ui.CardElement).ToList().Where(e => !string.IsNullOrEmpty(e.tooltip)).Select(e => e.tooltip).ToList();
            Check(tips.Any(t => t.Contains("bred from")), "F5: a hybrid's tile says the pair that made it");
            Check(tips.Any(t => t.Contains(GrowthContent.Get(founders[0]).Name) && t.Contains(GrowthReport.Recipe(GrowthContent.Get(founders[0]).Recipe))), "F5: a founder's says its recipe");
            // GR-33 (F77): a founder's tip carries its recipe as coloured dots.
            var founderTile = UQueryExtensions.Query<VisualElement>(s.Ui.CardElement).ToList().FirstOrDefault(e => e.userData is int[] && e.tooltip != null && e.tooltip.StartsWith(GrowthContent.Get(founders[0]).Name));
            Check(founderTile != null && ((int[])founderTile.userData).Sum() == GrowthContent.Get(founders[0]).Doses, "F77: a founder's tile carries its recipe's doses, drawn as dots in its tip");
            // GR-33 (F79): pressing a creature found shows what makes it.
            s.Ui.PickCreature(founders[0]);
            yield return null; yield return null;
            string picked = s.Ui.CardText();
            Check(s.Ui.CreaturePicked == founders[0] && picked.Contains("Best way:") && picked.Contains("MIXES YOU TRIED THAT MADE IT"), "F79: a creature pressed shows its best way and the mixes tried that made it");
            yield return Shot("1320b-journal-picked");
            s.Ui.PickCreature(null);
            // GR-33 (F78): the mix log, filtered by a culture, as dots and pictures, a page at a time.
            Check(s.Ui.ShowTabNamed("Mix log"), "The Mix log tab");
            yield return null; yield return null;
            string firstCulture = GrowthContent.Cultures.First(c => shop.MixLog.Any(m => m.Doses[GrowthContent.CultureIndex(c.Id)] > 0)).Name;
            yield return ClickOn(e => e is Button b && b.text == firstCulture, "Press the " + firstCulture + " filter");
            yield return null;
            string log = s.Ui.CardText();
            int withIt = shop.MixLog.Count(m => m.Doses[GrowthContent.CultureIndex(GrowthContent.Cultures.First(c => c.Name == firstCulture).Id)] > 0);
            Check(log.Contains(withIt + (withIt == 1 ? " mix" : " mixes")), "F78: the filter keeps the " + withIt + " mixes with " + firstCulture);
            yield return Shot("1320c-mix-log-filtered");
            yield return ClickOn(e => e is Button b && b.text == firstCulture, "Press it again to take it off");
            s.SetCard(GrowthCard.None);

            // ---- The power board
            s.SetCard(GrowthCard.PowerBoard);
            yield return null; yield return null;
            Check(s.Ui.CardText().Contains("Research room"), "The power board lists the room");
            yield return Shot("1321-power-board");
            s.SetCard(GrowthCard.None);

            // ---- A save mid-way
            Check(shop.QueueAllPairs().Success, "More pairs to save mid-way");
            yield return Wait(() => shop.ResearchRunning.Count() == 3, 3, "three tanks at work again");
            yield return new WaitForSeconds(0.5f);
            float left = shop.ResearchInTank(0).Left;
            int jobs = shop.ResearchJobs.Count, jars = shop.Specimens.Count, onTrayNow = shop.ResearchTray.Count;
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(shop.Snapshot())), out string problem);
            Check(string.IsNullOrEmpty(problem) && back != null, "The save loads (" + problem + ")");
            s.LoadFixture(back);
            shop = s.Shop;
            yield return null; yield return null;
            Check(shop.ResearchRoom && !s.World.ResearchWallStanding && shop.Specimens.Count == jars && shop.ResearchTray.Count == onTrayNow && shop.ResearchJobs.Count == jobs, "The room comes back as it was");
            Check(Mathf.Abs(shop.ResearchInTank(0).Left - left) < 2 && s.World.TankCreaturesShown(0) + s.World.TankDosesShown(0) > 0, "A tank carries on where it was");
            Check(s.World.JarCreaturesShown == jars && s.World.TrayCreaturesShown == onTrayNow, "The jars and tray show theirs");
            LookAt(new Vector3(-4.8f, 0, 12.0f), new Vector3(-8.0f, 1.2f, 15.2f));
            yield return null; yield return null;
            yield return Shot("1322-after-the-save");

            // ---- The room by day: the tanks carry on with the shop open
            shop.DevelopmentStock(shop.Containers[0].Id, "pondlet");
            Check(shop.OpenShop().Success, "The shop opens");
            yield return new WaitForSeconds(3);
            Check(shop.Phase == GrowthPhase.Day && shop.ResearchRunning.Any(), "By day the tanks carry on");
            LookAt(new Vector3(-4.8f, 0, 12.0f), new Vector3(-8.0f, 1.2f, 15.2f));
            yield return null; yield return null;
            yield return Shot("1323-by-day");
        }
    }
}
#endif
