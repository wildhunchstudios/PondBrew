#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    /// <summary>
    /// GR-29's Windows route (-evolutionGrowthDeadEndsSmoke): tester round 1's batch 1. Click acts and E only opens: at the
    /// incubator (a click puts the siphon's doses in and takes the hatchling), at tanks (a click puts the chosen creature in,
    /// even within peek; the take button takes the one aimed at, outlined). The item bar is always there, and chooses. Nothing
    /// is a dead end: the chamber, the siphon and a buffer pour out, a full tank and full hands swap, Start says what holds
    /// it. The mix log waits for the hatch, and a press in a scrolled card does not move it.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string DeadEndsScenarioText = "GR-29 tester round 1, batch 1: click acts and E opens, at the incubator and the tanks; the item bar always there, its doses as colours and the chosen creature in hand; the creature aimed at outlined; the chamber, the siphon and a buffer poured out, a full tank and full hands swapped; Start saying what holds it; the mix log waiting for the hatch; a press in a scrolled card not moving it.";

        /// <summary>GR-29: a click of the use button (0), or of the take button (1, the right mouse button).</summary>
        IEnumerator Click(int button = 0)
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(1 << button) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
        }

        UnityEngine.UIElements.Button CardButton(string text) =>
            s.Ui.CardElement == null ? null : UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Button>(s.Ui.CardElement).ToList().FirstOrDefault(b => b.text.StartsWith(text));

        IEnumerator DeadEndsScenario()
        {
            yield return Opening();
            TakeInput();
            s.AllowValidationInput = true;
            var shop = s.Shop;

            // ---- the item bar is there with nothing to show (F16: it used to vanish)
            yield return null; yield return null;
            var bar = s.Ui.ItemBar;
            Check(bar.siphon == "Siphon 0/" + shop.SiphonCapacity && bar.doses == 0 && bar.hands == "Hands empty" && bar.chosen == -1 && bar.portraits == 0, "The item bar shows an empty siphon and empty hands (\"" + bar.siphon + "\", \"" + bar.hands + "\")");
            yield return Shot("1100-item-bar-empty");

            // ---- the incubator: a click puts the doses in and opens nothing; E opens and moves nothing
            LookAt(GrowthWorld.IncubatorPosition + new Vector3(0, 0, -1.5f), GrowthWorld.IncubatorPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Incubator, null, "The reticle focuses the incubator");
            yield return Click();
            Check(s.Card == GrowthCard.None && shop.Chamber.Count == 0, "A click with an empty siphon opens nothing and adds nothing");
            shop.Held.AddRange(new[] { "bloom", "bloom", "flow" });
            yield return null; yield return null;
            bar = s.Ui.ItemBar;
            Check(bar.siphon == "Siphon 3/" + shop.SiphonCapacity && bar.doses == 3, "The siphon's doses show as a bar, three of " + shop.SiphonCapacity);
            s.FocusName(out string prompt);
            Check(prompt.Contains("click put yours in") && prompt.Contains("E mix"), "The prompt says what a click and E do (" + prompt + ")");
            yield return Shot("1101-item-bar-doses");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Incubator && shop.Chamber.Count == 0 && shop.Held.Count == 3, "E opens the card, and moves nothing");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            yield return Click();
            Check(s.Card == GrowthCard.None && shop.Chamber.Count == 3 && shop.Held.Count == 0, "A click puts the siphon's doses in the chamber, and opens nothing");

            // ---- F10 and F17: a full chamber and a full siphon are no dead end
            shop.Held.AddRange(Enumerable.Repeat("flow", shop.SiphonCapacity));
            yield return Press(Key.E);
            yield return null; yield return null;
            var back = CardButton("Return doses");
            Check(s.Card == GrowthCard.Incubator && back != null && !back.enabledSelf && s.Ui.CardText().Contains("Your siphon is full: pour something out first"), "With the chamber and the siphon full, Return doses is off and says why");
            yield return Shot("1102-incubator-full");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "Pour out the chamber", "Press Pour out the chamber");
            yield return null;
            Check(shop.Chamber.Count == 0 && shop.Held.Count == shop.SiphonCapacity, "The chamber pours out, and the siphon keeps its doses");
            Check(s.Ui.CardText().Contains("Pour out the " + shop.SiphonCapacity + " Flow in your siphon"), "The × says what it pours out");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "×" && b.enabledSelf && b.tooltip != null && b.tooltip.StartsWith("Pour out"), "Press × on Flow");
            yield return null;
            Check(shop.Held.Count == 0, "The × pours the siphon's Flow away");

            // ---- F9 and F11: the mix log waits for the hatch; Start says what holds it
            shop.Held.AddRange(new[] { "bloom", "bloom", "flow" });
            s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            yield return Click();
            yield return Press(Key.E);
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith("Start"), "Press Start");
            Check(shop.Incubating && shop.MixLog.Count == 0 && s.Ui.CardText().Contains("Mix log · 0 tried"), "While it incubates the mix log stays empty");
            yield return Wait(() => shop.Hatchling != null, 20, "the hatch");
            Check(shop.MixLog.Count == 1 && shop.MixLog[0].Species == shop.Hatchling.Species, "It hatched, and the mix is in the log: " + shop.Hatchling.Species);
            shop.Held.Add("bloom");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "+" && b.enabledSelf, "Press + on Bloom");
            yield return null; yield return null;
            string card = s.Ui.CardText();
            var start = CardButton("Start");
            Check(start != null && !start.enabledSelf && card.Contains("Start waits until you take the hatchling."), "Start says what holds it, in words on the card");
            yield return Shot("1103-start-waits");
            while (shop.Carried.Count < GrowthShop.CarryLimit) shop.DevelopmentGive("pondlet");
            yield return new WaitForSeconds(0.25f);
            Check(s.Ui.CardText().Contains("your hands are full: put a creature in a tank first"), "With full hands it says that too");

            // ---- F13, F21: the chosen creature is the one a click puts in
            shop.Carried.Clear();
            shop.DevelopmentGive("pondlet"); shop.DevelopmentGive("bubblebelly"); shop.DevelopmentGive("ribbonfin");
            s.SetCard(GrowthCard.None);
            var a1 = GrowthWorld.Places["a1"];
            var tank = shop.Containers.First(c => c.Place == "a1");
            LookAt(a1.Browse + new Vector3(0, 0, -0.6f), a1.Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, tank.Id, "The reticle focuses the Starter tank");
            bar = s.Ui.ItemBar;
            Check(bar.portraits == 3 && bar.chosen == 0 && bar.hands.StartsWith("Pondlet"), "The item bar shows the three in hand, the first chosen (" + bar.hands + ")");
            yield return Press(Key.Digit3);
            yield return null;
            bar = s.Ui.ItemBar;
            Check(s.CarriedChoice == 2 && bar.chosen == 2 && bar.hands.StartsWith("Ribbonfin"), "3 chooses the third (" + bar.hands + ")");
            yield return Shot("1104-item-bar-chosen");
            int choice = s.CarriedChoice;
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            Check(s.CarriedChoice != choice, "The mouse wheel moves the choice (" + choice + " to " + s.CarriedChoice + ")");
            yield return Press(Key.Digit3);
            yield return null;
            s.FocusName(out prompt);
            Check(prompt.Contains("click put in"), "At a tank with creatures in hand, the prompt says a click puts one in (" + prompt + ")");
            yield return Click();
            Check(s.Card == GrowthCard.None && tank.Occupants.Count == 1 && tank.Occupants[0].Species == "ribbonfin" && shop.Carried.Count == 2 && shop.Carried.All(c => c.Species != "ribbonfin"), "A click puts in the one chosen: the Ribbonfin");

            // ---- F14, F25: within peek a click still puts in; the take button takes the one aimed at, which wears an outline
            LookAt(a1.Browse + new Vector3(0, 0, 0.15f), a1.Position + new Vector3(0, 1.18f, -0.1f));
            yield return Wait(() => s.Peeking, 3, "peek to ease in");
            float hunt = Time.realtimeSinceStartup + 14;
            while (s.AimedCreature == null && Time.realtimeSinceStartup < hunt) yield return null;
            var aimed = s.AimedCreature;
            Check(aimed != null && aimed.Aimed && s.World.Tanks[tank.Id].Creatures.Values.Count(c => c.Aimed) == 1, "The creature under the reticle wears the outline, and only it");
            yield return Shot("1105-aimed-outline");
            int inTank = tank.Occupants.Count, inHand = shop.Carried.Count;
            yield return Click();
            Check(shop.Carried.Count == inHand - 1 && tank.Occupants.Count == inTank + 1, "Within peek, with creatures in hand, a click puts one in and takes none");
            hunt = Time.realtimeSinceStartup + 14;
            while (s.AimedCreature == null && Time.realtimeSinceStartup < hunt) yield return null;
            inTank = tank.Occupants.Count; inHand = shop.Carried.Count;
            int aimedAt = s.AimedCreature != null ? s.AimedCreature.SpecimenId : 0;
            yield return Click(1);
            Check(shop.Carried.Count == inHand + 1 && tank.Occupants.Count == inTank - 1 && shop.Carried.Any(c => c.Id == aimedAt), "The take button takes the one aimed at, with another in hand");
            LookAt(a1.Browse + new Vector3(0.8f, 0, -1.0f), a1.Position + new Vector3(3, 1.1f, 3));
            yield return null; yield return null; yield return null;
            Check(aimed == null || !aimed.Aimed, "Looking away takes the outline off");

            // ---- F12, F19: a full tank and full hands: a click says so, E opens, and a press swaps
            while (tank.Free > 0) shop.DevelopmentStock(tank.Id, "pondlet");
            while (shop.Carried.Count < GrowthShop.CarryLimit) shop.DevelopmentGive("bubblebelly");
            LookAt(a1.Browse + new Vector3(0, 0, -0.6f), a1.Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null;
            int logged = s.Ui.Log.Count;
            var before = tank.Occupants.ToList();
            yield return Click();
            Check(tank.Occupants.SequenceEqual(before) && s.Ui.Log.Skip(logged).Any(l => l.Contains("It does not fit")), "A click on a full tank says it does not fit");
            yield return Press(Key.E);
            Check(s.Card == GrowthCard.Tank && s.CardTarget == tank.Id, "E opens the tank with creatures in hand (it used to try to put one in)");
            yield return null; yield return null;
            card = s.Ui.CardText();
            Check(card.Contains("press a creature to swap it") && card.Contains("Swap it for your"), "Full hands: the card offers to swap");
            yield return Shot("1106-swap");
            var chosenNow = s.ChosenCarried;
            int spaces = tank.Used;
            string first = before[0].Def.Name;
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text.StartsWith(first), "Press " + first + " in the tank");
            yield return null;
            Check(tank.Occupants.Count == before.Count && shop.Carried.Count == GrowthShop.CarryLimit && tank.Occupants.Contains(chosenNow) && !shop.Carried.Contains(chosenNow), "They change places: the one chosen goes in, and one comes out");
            s.SetCard(GrowthCard.None);

            // ---- F15: a press in a scrolled card does not move it
            s.LoadFixture(GrowthStarts.Make(GrowthStarts.Look));
            shop = s.Shop;
            yield return null; yield return null;
            shop.Held.Clear(); shop.Chamber.Clear();
            shop.Held.AddRange(new[] { "bloom", "bloom", "flow" });
            s.SetCard(GrowthCard.Incubator);
            yield return new WaitForSeconds(0.3f);
            float scrolled = s.Ui.ScrollCardBy(70);
            yield return null; yield return null;
            Check(scrolled > 1, "The card scrolls (to " + scrolled.ToString("0") + ")");
            s.Act(() => shop.AddDose("bloom"));
            float worst = 0;
            for (int i = 0; i < 15; i++) { worst = Mathf.Max(worst, Mathf.Abs(s.Ui.ShownCardScroll - scrolled)); yield return null; }
            Check(worst < 1 && Mathf.Abs(s.Ui.CardScroll - scrolled) < 1, "A press in the scrolled card does not move what shows (at most " + worst.ToString("0.0") + " px)");
            Check(shop.Chamber.Count == 1 && s.Ui.CardText().Contains("In the chamber · 1 of"), "And the rebuilt card shows the change");
            yield return Shot("1107-scrolled");

            // ---- F17, F18: the grower's card and a buffer's pour out
            s.SetCard(GrowthCard.Grower, shop.Cultures.First());
            yield return null; yield return null;
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "Pour out your siphon", "Press Pour out your siphon");
            yield return null;
            Check(shop.Held.Count == 0, "The grower's card pours the siphon out");
            var buffer = shop.Buffers.First();
            buffer.Units = Mathf.Max(buffer.Units, 5);
            if (buffer.Culture == null) buffer.Culture = shop.Cultures.First();
            s.SetCard(GrowthCard.Buffer, buffer.Place.ToString());
            yield return null; yield return null;
            yield return Shot("1108-buffer");
            yield return ClickOn(e => e is UnityEngine.UIElements.Button b && b.text == "Pour this buffer out", "Press Pour this buffer out");
            yield return null;
            Check(buffer.Units == 0, "The buffer's card pours it out, with no creature made");
            s.SetCard(GrowthCard.None);
            s.AllowValidationInput = false;
        }
    }
}
#endif
