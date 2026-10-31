using System;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-14: the guided first night (GR.43, the user's choice). A new game starts with the guide on: the next-step line says
    // what to do and a marker points at the thing to use, through the first loop: fill the siphon at the growers, mix and
    // start the incubator, take the hatchling, put it in a tank in the shop, open the shop at the counter and bag the first
    // sale; the guide ends with the first day's receipt. Each step is read from the shop as it is, so a step done early, or
    // in another order, is never asked for again, and a step undone (a creature taken back out of its tank) is asked for
    // again. The player can skip it any time (the pause screen). Old saves, start points and validation fixtures have no
    // guide (Off): an established shop is never put through it.

    /// <summary>GR-14: whether the guided first night runs: Off (an old save, a start point, a fixture), On, Done or Skipped.</summary>
    public enum GrowthGuideMode { Off, On, Done, Skipped }

    /// <summary>GR-14: the guided first night's steps, in their order.</summary>
    public enum GrowthGuideStep { Collect, Mix, Take, Stock, Open, Sell, FirstDay }

    /// <summary>GR-14: the guide's step now: what the next-step line says and what the marker points at (a target id, or null).</summary>
    public sealed class GrowthGuideView
    {
        public GrowthGuideStep Step;
        public string Text, Target, TargetName;
    }

    public sealed partial class GrowthShop
    {
        /// <summary>GR-14: the guided first night; Off unless a new game starts it.</summary>
        public GrowthGuideMode Guide;

        /// <summary>GR-14: the guide's step now; after the first night, R09.4's lesson step now; null when neither runs. Targets:
        /// "grower:bloom", "incubator", "container:id", "counter", and the lessons' "Kind" or "Kind:id" (a GrowthTargetKind).</summary>
        public GrowthGuideView GuideNow()
        {
            if (Guide != GrowthGuideMode.On) return LessonStep();
            var step = GuideStep();
            if (step == null) return null;
            var view = new GrowthGuideView { Step = step.Value };
            switch (step.Value)
            {
                case GrowthGuideStep.Collect:
                    string next = Held.Contains("bloom") ? "flow" : "bloom";
                    view.Text = Held.Contains("bloom") ? "Now the Flow grower: hold the left mouse on it to add a Flow dose" : "Fill your siphon: hold the left mouse on the Bloom grower, then the Flow grower";
                    view.Target = "grower:" + next; view.TargetName = Name(next) + " grower";
                    break;
                case GrowthGuideStep.Mix:
                    view.Text = Held.Count > 0 ? "Click the incubator to put your doses in, then open it (E) and press Start" : "Open the incubator (E) and press Start";
                    view.Target = "incubator"; view.TargetName = "Incubator";
                    break;
                case GrowthGuideStep.Take:
                    view.Text = Hatchlings.Count > 0 ? "It hatched: click the incubator to take your creature" : "Your creature is growing: click the incubator to take it when it hatches";
                    view.Target = "incubator"; view.TargetName = Hatchlings.Count > 0 ? "Take it here" : "Incubator";
                    break;
                case GrowthGuideStep.Stock:
                    var tank = ShopContainers.FirstOrDefault(c => c.Free > 0) ?? ShopContainers.FirstOrDefault();
                    view.Text = "Carry it to the tank in the shop and click to put it in";
                    view.Target = tank == null ? null : "container:" + tank.Id; view.TargetName = tank == null ? null : tank.Def.Name;
                    break;
                case GrowthGuideStep.Open:
                    view.Text = "Open the shop at the counter (E): a day lasts 5:30";
                    view.Target = "counter"; view.TargetName = "Counter";
                    break;
                case GrowthGuideStep.Sell:
                    view.Text = "Visitors queue at the counter: hold the left mouse on it to bag each sale";
                    view.Target = "counter"; view.TargetName = "Counter";
                    break;
                default:
                    view.Text = "Your first sale! Keep serving until the day ends: the night's receipt sums it up";
                    view.Target = "counter"; view.TargetName = "Counter";
                    break;
            }
            return view;
        }

        /// <summary>GR-14: the step the shop is at, read from its state; null once the first day is over with a sale.</summary>
        GrowthGuideStep? GuideStep()
        {
            if (SalesTotal >= 1) return Day >= 2 ? (GrowthGuideStep?)null : GrowthGuideStep.FirstDay;
            if (Phase == GrowthPhase.Day) return GrowthGuideStep.Sell;
            if (ShopContainers.Any(c => c.Occupants.Count > 0)) return GrowthGuideStep.Open;
            if (Carried.Count > 0) return GrowthGuideStep.Stock;
            if (Incubating || Hatchlings.Count > 0) return GrowthGuideStep.Take;
            bool collected = Chamber.Count > 0 || Held.Count >= 2 || Held.Contains("bloom") && Held.Contains("flow");
            return collected ? GrowthGuideStep.Mix : GrowthGuideStep.Collect;
        }

        /// <summary>GR-14: the guide ends once the first day with a sale is over; the next-step line shows the goals again.</summary>
        void TickGuide()
        {
            if (Guide != GrowthGuideMode.On || GuideStep() != null) return;
            Guide = GrowthGuideMode.Done;
            Raise(GrowthEventKind.Guide, "That was your first day! From here the line at the bottom shows your next goal, and the pinboard has them all.");
        }

        /// <summary>GR-14: skips the guided first night (the pause screen); nothing else changes.</summary>
        public void SkipGuide()
        {
            if (Guide == GrowthGuideMode.On) Guide = GrowthGuideMode.Skipped;
        }

        void SaveGuide(GrowthSave s) => s.guide = Guide == GrowthGuideMode.Off ? "" : Guide.ToString().ToLowerInvariant();

        /// <summary>Version 10 and older had no guide: the shop is Off (an established shop is never put through it).</summary>
        static string RestoreGuide(GrowthShop shop, GrowthSave s)
        {
            if (string.IsNullOrEmpty(s.guide)) { shop.Guide = GrowthGuideMode.Off; return null; }
            if (!Enum.TryParse(s.guide, true, out GrowthGuideMode mode) || !Enum.IsDefined(typeof(GrowthGuideMode), mode)) return "Unknown guide " + s.guide;
            shop.Guide = mode;
            return null;
        }
    }
}
