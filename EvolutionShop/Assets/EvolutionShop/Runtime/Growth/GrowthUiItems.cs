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
            BuildCues();
        }

        // ---------------------------------------------------------------- GR-31: what the shop floor shows

        VisualElement boopBox, counterCue, cueProgress;
        Label boopKey, cueText;
        readonly List<VisualElement> pickedIcons = new List<VisualElement>();
        bool cueWasOn;

        void BuildCues()
        {
            // F29: over the counter, as soon as a visitor stands at it.
            counterCue = Box("gr-cue");
            cueText = L("", "gr-cue__text");
            var track = Box("gr-cue__track");
            cueProgress = Box("gr-cue__progress");
            track.Add(cueProgress);
            counterCue.Add(cueText); counterCue.Add(track);
            counterCue.style.display = DisplayStyle.None;
            hud.Add(counterCue);
        }

        /// <summary>Validation: whether the counter's cue shows, and its words.</summary>
        public (bool on, string words) CounterCue => (counterCue.style.display == DisplayStyle.Flex, cueText.text);
        /// <summary>Validation: how many picked or bought icons show, and their words.</summary>
        public List<string> PickedIcons => pickedIcons.Where(i => i.style.display == DisplayStyle.Flex).Select(i => i.Q<Label>().text).ToList();

        /// <summary>Where a point in the world is on the HUD, when it is in front of the camera and nothing stands in the way; null otherwise.</summary>
        Vector2? OnScreen(Vector3 world, Vector3 lookAt, System.Func<GrowthTarget, bool> itself = null)
        {
            var cam = s.Player != null ? s.Player.View : null;
            var panel = hud.panel;
            if (cam == null || panel == null || cam.WorldToViewportPoint(world).z <= 0) return null;
            var from = cam.transform.position;
            if (Physics.Linecast(from, lookAt, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<GrowthTarget>();
                if (itself == null || target == null || !itself(target)) return null;
            }
            return RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam);
        }

        void TickCues(bool free)
        {
            var shop = s.Shop;
            // F29: a visitor has reached the counter: the bell, and a cue over it with how far the bag is.
            var front = free && shop.Phase == GrowthPhase.Day ? shop.PlayerVisitor : null;
            bool arrived = false;
            if (front != null && s.World.VisitorViews.TryGetValue(front.Id, out var at) && at != null)
            {
                var feet = at.transform.position; var spot = GrowthWorld.QueueSpots[0];
                arrived = new Vector2(feet.x - spot.x, feet.z - spot.z).magnitude < 0.7f;
            }
            if (arrived && !cueWasOn && s.Audio != null) s.Audio.BellDue = true;
            cueWasOn = arrived;
            var cueAt = arrived ? OnScreen(GrowthWorld.CounterPosition + new Vector3(0, 1.45f, 0), GrowthWorld.CounterPosition + new Vector3(0, 0.9f, 0), t => t.Kind == GrowthTargetKind.Counter) : null;
            counterCue.style.display = cueAt.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (cueAt.HasValue)
            {
                cueText.text = GrowthControls.Keyed(s.ServingNow ? "Bagging..." : "Ready: hold the mouse to bag");
                counterCue.style.left = cueAt.Value.x; counterCue.style.top = cueAt.Value.y;
                cueProgress.style.width = new Length(s.ServingNow ? Mathf.Clamp01(shop.ServeProgress / Mathf.Max(0.01f, shop.ServeSeconds)) * 100 : 0, LengthUnit.Percent);
            }
            // F30: what each visitor picked, then bought, by their hand: their wish stays in the bubble over their head.
            int used = 0;
            if (free)
                foreach (var v in shop.Visitors)
                {
                    if (used >= 6) break;
                    string species; Colour colour; string word;
                    if (v.State == GrowthVisitorState.Leaving) { if (v.Outcome != GrowthVisitOutcome.Bought || v.BoughtSpecies == null) continue; species = v.BoughtSpecies; colour = v.BoughtColour; word = "bought"; }
                    else
                    {
                        var picked = v.State == GrowthVisitorState.Queued ? shop.PickedBy(v) : null;
                        if (picked == null) continue;
                        species = picked.Species; colour = picked.Colour; word = "picked";
                    }
                    if (!s.World.VisitorViews.TryGetValue(v.Id, out var view) || view == null) continue;
                    var chest = view.transform.position + new Vector3(0, 1.0f, 0);
                    if ((chest - s.Player.View.transform.position).sqrMagnitude > 64) continue;
                    var p = OnScreen(chest, chest);
                    if (!p.HasValue) continue;
                    if (used == pickedIcons.Count)
                    {
                        var icon = Box("gr-picked");
                        icon.Add(L("", "gr-picked__word"));
                        pickedIcons.Add(icon);
                        hud.Add(icon);
                    }
                    var el = pickedIcons[used++];
                    el.style.display = DisplayStyle.Flex;
                    el.style.left = p.Value.x; el.style.top = p.Value.y;
                    el.Q<Label>().text = word;
                    var portrait = GrowthPortraits.Get(species, colour);
                    if (portrait != null) el.style.backgroundImage = new StyleBackground(portrait);
                }
            for (int i = used; i < pickedIcons.Count; i++) pickedIcons[i].style.display = DisplayStyle.None;
        }

        void TickItemBar(bool free)
        {
            TickCues(free);
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
