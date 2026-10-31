using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// GR-29 (F13, F16, F21, tester round 1): the item bar, always there while walking: the siphon as a bar of its doses'
    /// colours, and a slot for each creature the hands can hold, each its own portrait, the one a click puts in marked. It
    /// replaces two lines of text that vanished whenever the siphon or the hands were empty.
    /// </summary>
    public sealed partial class GrowthUi
    {
        VisualElement siphonBar;
        readonly List<VisualElement> itemSlots = new List<VisualElement>();
        string itemsShown;

        void BuildItemBar()
        {
            carryBox = Box("gr-items");
            var siphon = Box("gr-items__siphon");
            siphonLabel = L("", "gr-items__label");
            siphonBar = Box("gr-items__bar");
            siphon.Add(siphonLabel); siphon.Add(siphonBar);
            carryBox.Add(siphon);
            var hands = Box("gr-items__hands");
            carryLabel = L("", "gr-items__label");
            var slots = Box("gr-items__slots");
            for (int i = 0; i < GrowthShop.CarryLimit; i++)
            {
                var slot = Box("gr-slot");
                slot.Add(L((i + 1).ToString(), "gr-slot__key"));
                itemSlots.Add(slot);
                slots.Add(slot);
            }
            hands.Add(carryLabel); hands.Add(slots);
            carryBox.Add(hands);
            hud.Add(carryBox);
        }

        void TickItemBar(bool free)
        {
            carryBox.style.display = free ? DisplayStyle.Flex : DisplayStyle.None;
            if (!free) return;
            var shop = s.Shop;
            string now = string.Join(",", shop.Held) + "|" + shop.SiphonCapacity + "|" + string.Join(",", shop.Carried.Select(c => c.Id)) + "|" + s.CarriedChoice;
            if (now == itemsShown) return;
            itemsShown = now;
            siphonLabel.text = "Siphon " + shop.Held.Count + "/" + shop.SiphonCapacity;
            siphonBar.Clear();
            foreach (var culture in shop.Held.OrderBy(GrowthContent.CultureIndex))
            {
                var dose = Box("gr-dosebar");
                dose.style.backgroundColor = CultureColour(culture);
                dose.Add(L(GrowthShop.Name(culture).Substring(0, 1), "gr-dosebar__letter"));
                siphonBar.Add(dose);
            }
            for (int i = shop.Held.Count; i < shop.SiphonCapacity; i++) siphonBar.Add(Box("gr-dosebar", "gr-dosebar--empty"));
            var chosen = s.ChosenCarried;
            carryLabel.text = chosen == null ? "Hands empty" : GrowthSession.Named(chosen) + (shop.Carried.Count > 1 ? " · scroll or 1-" + shop.Carried.Count : "");
            for (int i = 0; i < itemSlots.Count; i++)
            {
                var creature = i < shop.Carried.Count ? shop.Carried[i] : null;
                var portrait = creature == null ? null : GrowthPortraits.Get(creature.Species, creature.Colour);
                itemSlots[i].style.backgroundImage = portrait == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(portrait);
                itemSlots[i].EnableInClassList("gr-slot--chosen", creature != null && creature == chosen);
                itemSlots[i].EnableInClassList("gr-slot--empty", creature == null);
            }
        }

        /// <summary>Validation (GR-29): what the item bar says and shows: the siphon's line, its doses' count, the hands' line, the chosen slot (-1: none).</summary>
        public (string siphon, int doses, string hands, int chosen, int portraits) ItemBar =>
            (siphonLabel.text, siphonBar.Children().Count(d => !d.ClassListContains("gr-dosebar--empty")), carryLabel.text,
                itemSlots.FindIndex(x => x.ClassListContains("gr-slot--chosen")), itemSlots.Count(x => !x.ClassListContains("gr-slot--empty")));
    }
}
