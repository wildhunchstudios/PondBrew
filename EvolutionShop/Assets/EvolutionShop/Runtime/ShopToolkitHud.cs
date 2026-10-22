using System.Collections.Generic;
using UnityEngine;

namespace EvolutionShop
{
    // UX-03: what the UI Toolkit HUD reads from the session. Reads only; every change still goes through the
    // session's existing commands.
    public sealed partial class ShopSession
    {
        Transform comedySpeaker;
        Transform comedySpeakerMeasured;
        float comedySpeakerTop;
        readonly Dictionary<string, float> hudPatience = new Dictionary<string, float>();
        readonly List<string> hudPatienceSeen = new List<string>();

        /// <summary>True once the UI Toolkit HUD has loaded (since UX-09 it is the only HUD).</summary>
        public bool ToolkitHudActive => ToolkitUiActive;

        /// <summary>The code-drawn icon the UI root owns (ShopUiRoot.Icons); null before it loads.</summary>
        internal Texture2D HudIcon(ShopUiTheme.Symbol symbol) => ToolkitUi != null ? ToolkitUi.Icon(symbol) : null;

        /// <summary>Fills the HUD snapshot for this frame. Allocation is limited to strings that changed state needs.</summary>
        public void FillHudState(ShopHudState s, ShopUiStrings strings)
        {
            s.Clear();
            s.Generation = hudGeneration;
            s.PanelOpen = Panel != ShopPanel.None;
            s.Placement = PlacementActive;
            s.Planning = ToolkitUiActive && !PlacementActive && (PlanningViewPresenter.Handles(Panel) || ScreenPresenter.Handles(Panel));
            s.Paused = Paused;
            s.Gameplay = !s.PanelOpen && !s.Placement;
            s.Phase = Cycle.Phase == ShopDayPhase.Night ? HudDayPhase.Night : Cycle.Phase == ShopDayPhase.Open ? HudDayPhase.Open : HudDayPhase.Closing;
            s.Day = Cycle.DayNumber;
            s.SecondsLeft = Cycle.SecondsRemaining;
            s.Currency = Model.Currency;
            s.MoneySequence = (int)uiMoneySequence;
            s.MoneyDelta = uiMoneyDelta;
            s.Reputation = Reputation.Points;
            s.Appeal = Appeal.Points;
            s.Tutorial = Tutorial.Mode == TutorialMode.Active;
            s.Objective = Objective();
            s.Reduced = UiFeedbackReduced;
            s.SalesToday = Reports != null ? Reports.CurrentSales : 0;
            s.SalesDay = Reports != null ? Reports.CurrentDayNumber : 0;
            var counter = ToolkitAnchorFor(ShopPanel.Counter);
            s.HasCounter = counter;
            s.CounterWorld = counter ? ShopUiLayout.AnchorPoint(counter) : Vector3.zero;
            if (!string.IsNullOrEmpty(SaveError)) s.SaveError = SaveError;
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil) s.Notice = message;

            if (!s.PanelOpen && Time.unscaledTime < revealUntil && !string.IsNullOrEmpty(revealedSpecies))
            {
                var species = Model.GetSpecies(revealedSpecies);
                s.DiscoveryName = species.DisplayName;
                s.RecipeBloom = species.Bloom; s.RecipeFlow = species.Flow; s.RecipeSpark = species.Spark; s.RecipeMist = species.Mist;
            }
            FillHudQueue(s);
            FillHudPings(s, strings);
            if (s.Gameplay) FillHudPrompt(s, strings);
            if (s.Gameplay && Comedy.Active)
            {
                s.Caption = Comedy.Caption;
                if (comedySpeaker && comedySpeaker.gameObject.activeInHierarchy)
                {
                    s.HasSpeaker = true;
                    s.SpeakerWorld = comedySpeaker.position + Vector3.up * SpeakerTop(comedySpeaker);
                }
            }
            if (s.Gameplay && s.Tutorial && TryProjectTutorialGuidance(out var target, out _))
            {
                s.HasMarker = true;
                s.MarkerTitle = target.Label;
                s.MarkerAction = target.Action;
                s.MarkerWorld = target.Position;
            }
        }

        void FillHudQueue(ShopHudState s)
        {
            hudPatienceSeen.Clear();
            if (!Customers) return;
            foreach (var visit in Customers.Visits)
            {
                if (visit.Phase != CustomerPhase.Queued && visit.Phase != CustomerPhase.ToCounter) continue;
                hudPatienceSeen.Add(visit.Id);
                if (!hudPatience.TryGetValue(visit.Id, out float patience))
                {
                    patience = visit.Archetype.PatienceSeconds;
                    hudPatience.Add(visit.Id, patience);
                }
                s.Queue.Add(new HudQueueEntry
                {
                    Id = visit.Id, ArchetypeId = visit.ArchetypeId, AtCounter = visit.Phase == CustomerPhase.Queued,
                    SecondsLeft = visit.QueueSecondsRemaining, PatienceSeconds = patience
                });
            }
            if (hudPatience.Count > hudPatienceSeen.Count + 8)
            {
                var keep = new Dictionary<string, float>();
                foreach (var id in hudPatienceSeen) keep[id] = hudPatience[id];
                hudPatience.Clear();
                foreach (var pair in keep) hudPatience.Add(pair.Key, pair.Value);
            }
        }

        void FillHudPings(ShopHudState s, ShopUiStrings strings)
        {
            if (!World) return;
            foreach (var station in World.Stations)
            {
                if (!station) continue;
                if (station.Kind == StationKind.Incubator && Model.Incubator.Phase == IncubatorPhase.Ready)
                {
                    string name = !string.IsNullOrEmpty(Model.Incubator.SpeciesId) ? Model.GetSpecies(Model.Incubator.SpeciesId).DisplayName : strings.Get("hud.ping.creature");
                    s.Pings.Add(new HudPing { Key = "incubator", Text = strings.Format("hud.ping.ready", name), Short = name, World = ShopUiLayout.AnchorPoint(station) });
                }
                else if (station.Kind == StationKind.Conditioning && !string.IsNullOrEmpty(station.ConditionerItemId))
                {
                    var vat = Model.GetConditioner(station.ConditionerItemId);
                    if (vat.HasValue && vat.Value.Phase == ConditionerPhase.Ready)
                        s.Pings.Add(new HudPing { Key = "vat:" + station.ConditionerItemId, Text = strings.Get("hud.ping.vat"), Short = strings.Get("hud.ping.vatShort"), World = ShopUiLayout.AnchorPoint(station) });
                }
            }
            if (Stocker && Stocker.Blocked && Stocking != null && Stocking.Employee.Hired && Stocker.Actor)
                s.Pings.Add(new HudPing { Key = "worker:nori", Text = strings.Format("hud.ping.stuck", "Nori"), Short = "Nori", World = Stocker.Actor.transform.position + Vector3.up * 1.9f });
            if (Technician && Technician.Blocked && Laboratory != null && Laboratory.Employee.Hired && Technician.Actor)
                s.Pings.Add(new HudPing { Key = "worker:moss", Text = strings.Format("hud.ping.stuck", "Moss"), Short = "Moss", World = Technician.Actor.transform.position + Vector3.up * 1.9f });
        }

        void FillHudPrompt(ShopHudState s, ShopUiStrings strings)
        {
            if (Focused != null)
            {
                var p = new HudPrompt { Shown = true, Key = strings.Get("hud.key.use"), Progress = HarvestProgress };
                bool culture = TryStationCulture(Focused, out var kind);
                int capacity = BaselinePreview ? Config.BaseCapacity : Model.Capacity;
                if (culture)
                {
                    p.Key = strings.Get("hud.key.hold");
                    p.Name = strings.Format("hud.name.grower", strings.Get("hud.culture." + kind));
                    p.Status = strings.Get(Model.ReservedCultureCount >= capacity ? "hud.status.full" : "hud.status.ready");
                    if (kind == CultureKind.Spark || kind == CultureKind.Mist) p.SecondKey = strings.Get("hud.key.use");
                }
                switch (Focused.Kind)
                {
                    case StationKind.Incubator:
                        p.Name = strings.Get("card.title.Incubator");
                        var incubator = Model.Incubator;
                        int dosed = incubator.Bloom + incubator.Flow + incubator.Spark + incubator.Mist;
                        p.Status = incubator.Phase == IncubatorPhase.Ready ? strings.Get("hud.status.ready") :
                            incubator.Phase == IncubatorPhase.Incubating ? strings.Format("hud.status.seconds", Mathf.CeilToInt(incubator.SecondsRemaining)) :
                            dosed > 0 ? strings.Format("hud.status.dosed", dosed) : strings.Get("hud.status.empty");
                        break;
                    case StationKind.TankA: case StationKind.TankB: case StationKind.Display:
                        p.Name = Focused.Label;
                        var tank = string.IsNullOrEmpty(Focused.TankId) ? null : Model.GetTank(Focused.TankId);
                        p.Status = tank.HasValue ? Model.GetSpecies(tank.Value.SpeciesId).DisplayName : strings.Get("hud.status.empty");
                        break;
                    case StationKind.Counter:
                        p.Name = Focused.Label;
                        int waiting = 0;
                        if (Customers) foreach (var visit in Customers.Visits) if (visit.Phase == CustomerPhase.Queued) waiting++;
                        p.Status = Cycle.Phase == ShopDayPhase.Night ? strings.Get("hud.status.openShop") :
                            waiting > 0 ? strings.Format("hud.status.waiting", waiting) :
                            strings.Get(Cycle.Phase == ShopDayPhase.Open ? "hud.status.open" : "hud.status.closing");
                        // AD-E prototype: the counter is worked by hand; hold to scoop and bag the order.
                        if (CheckoutHoldAvailable)
                        {
                            p.Key = strings.Get("hud.key.hold");
                            p.Status = strings.Format("proto.hud.bag", waiting);
                            p.Progress = CheckoutProgress;
                        }
                        break;
                    case StationKind.Upgrade:
                        if (!culture) p.Name = strings.Get("card.title.Upgrade");
                        if (!culture || kind != CultureKind.Spark)
                            p.Status = Model.SparkResearchReady && !Model.SparkResearchUnlocked ? strings.Get("hud.status.research") :
                                !Model.UpgradeOwned ? strings.Get("hud.status.upgrade") : p.Status;
                        break;
                    case StationKind.StorageRack:
                        p.Name = Focused.Label;
                        break;
                    case StationKind.Conditioning:
                        p.Name = Focused.Label;
                        if (culture) { p.SecondKey = p.Key; p.Key = strings.Get("hud.key.use"); }
                        var vat = string.IsNullOrEmpty(Focused.ConditionerItemId) ? null : Model.GetConditioner(Focused.ConditionerItemId);
                        if (vat.HasValue)
                            p.Status = vat.Value.Phase == ConditionerPhase.Processing ? strings.Format("hud.status.seconds", Mathf.CeilToInt(vat.Value.SecondsRemaining)) :
                                vat.Value.Phase == ConditionerPhase.Ready ? strings.Format("hud.status.readyCount", vat.Value.Count) : strings.Get("hud.status.empty");
                        break;
                }
                if (string.IsNullOrEmpty(p.Name)) p.Name = Focused.Label;
                s.Prompt = p;
            }
            if (SlapTarget != null)
                s.SlapPrompt = new HudPrompt
                {
                    Shown = true, Slap = true, Key = SlapKey, Name = strings.Get("hud.name.slap"),
                    Status = SlapCooldownRemaining > 0 ? strings.Format("hud.status.secondsTenths", SlapCooldownRemaining.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)) : strings.Get("hud.status.ready")
                };
        }

        float SpeakerTop(Transform speaker)
        {
            if (speaker == comedySpeakerMeasured) return comedySpeakerTop;
            comedySpeakerMeasured = speaker;
            float top = 1.9f;
            var renderers = speaker.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                float max = float.NegativeInfinity;
                foreach (var r in renderers) if (r.enabled && !(r is ParticleSystemRenderer)) max = Mathf.Max(max, r.bounds.max.y);
                if (!float.IsNegativeInfinity(max)) top = max - speaker.position.y + 0.25f;
            }
            comedySpeakerTop = Mathf.Clamp(top, 0.6f, 3.2f);
            return comedySpeakerTop;
        }
    }
}
