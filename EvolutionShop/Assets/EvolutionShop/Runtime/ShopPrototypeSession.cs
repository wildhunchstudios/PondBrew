using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>One prototype day's measurements, logged at closing for the day-length decision (AD.6).</summary>
    [Serializable]
    public sealed class PrototypeDayStats
    {
        public int day, starsAtOpen, starsAtClose, visitors, sales, handsOnSales, wishes, exact, close, tips, haggledOff, rushArrivals, rushSales, walkedOut;
        public float length, rushStart, rushEnd, firstSale = -1, lastSale = -1, played;
        public long revenue;
    }

    /// <summary>A forecast visitor for tomorrow, on the night receipt.</summary>
    public struct PrototypeForecast
    {
        public string ArchetypeId, Archetype, Wish;
    }

    /// <summary>
    /// AD-E gameplay prototype (AD.6): wishes, tomorrow's forecast, the hands-on checkout, shop stars, tank themes and a
    /// shorter day with one rush, with placeholder art, for the user's playtest before any of it replaces current
    /// behaviour. Development builds only: on when a development build is played (F9 switches it), off in validation
    /// routes unless asked for (-evolutionPrototype), never in release builds; paused while the guided opening runs, whose
    /// lessons teach the classic Serve. Nothing is saved but ordinary money and reputation; switched off, every rule is
    /// the classic one.
    /// </summary>
    public sealed partial class ShopSession
    {
        public const float CheckoutSeconds = 1f;
        public const int ForecastVisitors = 5;
        bool prototypeOn;
        PrototypeDayStats prototypeDay;
        bool rushAnnounced, rushOverAnnounced;
        float prototypeDayStarted;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool PrototypeOn => prototypeOn;
#else
        public bool PrototypeOn => false;
#endif
        /// <summary>The prototype's rules apply: switched on and not during the guided opening.</summary>
        public bool PrototypeActive => PrototypeOn && Tutorial != null && Tutorial.Mode != TutorialMode.Active;
        /// <summary>The length the next opening gets.</summary>
        public float TradingDaySeconds => PrototypeActive ? ShopDayPlan.PrototypeSeconds : ShopDayCycle.DefaultTradingSeconds;
        public int Stars => ShopStars.Of(Reputation.Points);
        /// <summary>Today was opened as a prototype day and its rush is on.</summary>
        public bool InRush => PrototypeOn && prototypeDay != null && Cycle.Phase == ShopDayPhase.Open && ShopDayPlan.InRush(Cycle.DayLength, Cycle.SecondsRemaining);
        public PrototypeDayStats PrototypeDay => prototypeDay;
        public PrototypeDayStats LastPrototypeDay { get; private set; }
        /// <summary>The hands-on checkout's progress, 0 to 1, while the mouse is held on the counter.</summary>
        public float CheckoutProgress { get; private set; }
        public int HandsOnCheckouts { get; private set; }
        public string PrototypeLogPath { get; private set; }

        void InitializePrototype()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PrototypeLogPath = ValidationMode
                ? Path.Combine(Arg("-evolutionEvidence") ?? Application.temporaryCachePath, "prototype-days.jsonl")
                : Path.Combine(Application.persistentDataPath, "prototype-days.jsonl");
            bool asked = Arg("-evolutionPrototype") != null || Arg("-evolutionPrototypeSmoke") != null;
            if (Arg("-evolutionPrototypeOff") == null && (asked || !ValidationMode)) SetPrototype(true);
#endif
        }

        /// <summary>Switch the prototype. A day already open keeps its length; the next opening takes the new one.</summary>
        public void SetPrototype(bool on)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            prototypeOn = on;
            CheckoutProgress = 0;
            PresentPrototype();
#endif
        }

        void TickPrototype(Keyboard keyboard)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ValidationMode && keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                SetPrototype(!prototypeOn);
                Notify(prototypeOn ? "Gameplay prototype on: wishes, stars, themes, hands-on checkout and " + ShopHudRules.Clock(ShopDayPlan.PrototypeSeconds) + " days (from the next opening)."
                    : "Gameplay prototype off: the classic rules from the next arrival and opening.");
            }
            if (prototypeDay != null && Cycle.Phase == ShopDayPhase.Open)
            {
                prototypeDay.played = Cycle.DayLength - Cycle.SecondsRemaining;
                if (InRush && !rushAnnounced) { rushAnnounced = true; Notify("Rush hour! Visitors pour in for the next " + Mathf.RoundToInt(Cycle.DayLength * (ShopDayPlan.RushEnd - ShopDayPlan.RushStart)) + " seconds."); }
                else if (rushAnnounced && !rushOverAnnounced && !InRush) { rushOverAnnounced = true; Notify("The rush is over. Thanks for keeping up!"); }
            }
            PresentPrototype(Time.frameCount % 15 == 0);
#endif
        }

        bool themeTagsShown;
        void PresentPrototype(bool themes = true)
        {
            if (!World) return;
            bool shown = PrototypeActive;
            int points = Reputation != null ? Reputation.Points : 0;
            int stars = ShopStars.Of(points);
            World.SetPrototypeSign(shown, stars, ShopStars.NextThreshold(points), ShopStars.Unlock(Math.Min(ShopStars.Max, stars + 1)));
            if (themes && shown && Layout != null && Model != null)
            {
                themeTagsShown = true;
                var tags = new List<PrototypeThemeTag>();
                foreach (string tank in Model.TankIds)
                {
                    var theme = Layout.ThemeOf(tank);
                    if (theme == ShopTheme.None || !Layout.TryGetDisplayCentre(tank, out float x, out float z)) continue;
                    var stock = Model.GetTank(tank);
                    var species = stock.HasValue ? Model.GetSpecies(stock.Value.SpeciesId) : null;
                    tags.Add(new PrototypeThemeTag { TankId = tank, Theme = theme, X = x, Z = z, Matching = species != null && ShopThemes.Matches(theme, ShopWishes.Family(species)) });
                }
                World.SetThemeTags(tags, Player ? Player.View : null);
            }
            else if (!shown && themeTagsShown) { themeTagsShown = false; World.SetThemeTags(null, null); }
            Vector3 counter = Vector3.zero;
            foreach (var station in World.Stations) if (station.Kind == StationKind.Counter) { counter = station.transform.position; break; }
            World.PresentCheckoutHold(shown ? CheckoutProgress : 0, counter, ReducedVisualMotion);
        }

        /// <summary>The counter can be worked by hand: prototype on, at the counter, someone waiting to pay.</summary>
        public bool CheckoutHoldAvailable => PrototypeActive && !Paused && Panel == ShopPanel.None && Focused != null &&
            Focused.Kind == StationKind.Counter && IsAtCounter && Customers && Customers.WaitingCount > 0 &&
            (Cycle.Phase == ShopDayPhase.Open || Cycle.Phase == ShopDayPhase.Closing);

        /// <summary>Holding the mouse on the counter for a second scoops and bags the order (AD.6 hands-on checkout).</summary>
        void TickCheckout(bool holding, float seconds)
        {
            if (!holding || !CheckoutHoldAvailable) { CheckoutProgress = 0; return; }
            CheckoutProgress = Mathf.Min(1, CheckoutProgress + seconds / CheckoutSeconds);
            if (CheckoutProgress < 1) return;
            CheckoutProgress = 0;
            collectionNeedsRelease = true;
            ServeHandsOn();
        }

        /// <summary>The hands-on checkout's sale: the same rules, ledger and save as the counter's Serve.</summary>
        public bool ServeHandsOn()
        {
            if (!PrototypeActive || Paused || Panel != ShopPanel.None || !IsAtCounter)
            { Notify("Stand at the front counter and hold the mouse on it to bag the waiting order."); return false; }
            var customer = Customers.NextToServe;
            int agreed = customer == null ? 0 : customer.Price;
            var result = Customers.Serve();
            if (!result.HasValue) { Notify("No customer is waiting to check out yet."); return false; }
            Tutorial.ObserveSale(Model, result.Value, TutorialActionOrigin.Player, Cycle.DayNumber);
            bool success = Perform(result.Value, customer).Success;
            if (success)
            {
                HandsOnCheckouts++;
                if (prototypeDay != null) prototypeDay.handsOnSales++;
                int tip = customer != null && customer.WishMatch == WishMatch.Exact ? ShopWishes.Tip(agreed) : 0;
                Notify("Bagged! Sold for " + agreed + " credits" + (tip > 0 ? " and a " + tip + " credit tip: their wish came true!" : customer != null && customer.Haggled ? " after some haggling." : "."));
            }
            return success;
        }

        /// <summary>A sale resolved in the ledger (by hand or by Pip): a wish's tip and point, and the day's measurements.</summary>
        internal void PrototypeSold(CustomerVisit visit, bool rush)
        {
            if (visit == null) return;
            if (visit.WishMatch == WishMatch.Exact)
            {
                int tip = ShopWishes.Tip(visit.Price);
                if (tip > 0 && Model.ReceiveTip(tip).Success && prototypeDay != null) prototypeDay.tips += tip;
                Reputation.AwardWish();
            }
            if (prototypeDay == null) return;
            prototypeDay.sales++;
            prototypeDay.revenue += visit.Price;
            if (visit.WishMatch == WishMatch.Exact) prototypeDay.exact++;
            else if (visit.WishMatch == WishMatch.Close) prototypeDay.close++;
            if (visit.Haggled) prototypeDay.haggledOff += Math.Max(0, visit.TagPrice - visit.Price);
            if (rush) prototypeDay.rushSales++;
            float at = Cycle.DayLength - Cycle.SecondsRemaining;
            if (prototypeDay.firstSale < 0) prototypeDay.firstSale = at;
            prototypeDay.lastSale = at;
        }

        internal void PrototypeArrived(CustomerVisit visit, bool rush)
        {
            if (prototypeDay == null) return;
            prototypeDay.visitors++;
            if (visit.Wish != null) prototypeDay.wishes++;
            if (rush) prototypeDay.rushArrivals++;
        }

        internal void PrototypeLeft(CustomerVisit visit)
        {
            if (prototypeDay != null && visit != null && visit.Outcome != CustomerOutcome.Sold) prototypeDay.walkedOut++;
        }

        void BeginPrototypeDay()
        {
            rushAnnounced = rushOverAnnounced = false;
            prototypeDay = null;
            if (!PrototypeActive) return;
            prototypeDayStarted = Time.realtimeSinceStartup;
            prototypeDay = new PrototypeDayStats
            {
                day = Cycle.DayNumber, length = Cycle.DayLength, starsAtOpen = Stars,
                rushStart = ShopDayPlan.RushStartsAt(Cycle.DayLength), rushEnd = ShopDayPlan.RushEndsAt(Cycle.DayLength)
            };
        }

        void FinishPrototypeDay()
        {
            if (prototypeDay == null) return;
            prototypeDay.starsAtClose = Stars;
            prototypeDay.played = prototypeDay.length;
            LastPrototypeDay = prototypeDay;
            prototypeDay = null;
            string line = JsonUtility.ToJson(LastPrototypeDay);
            Debug.Log("EVOLUTION_PROTOTYPE_DAY " + line);
            try { File.AppendAllText(PrototypeLogPath, line + "\n"); }
            catch (Exception e) { Debug.LogWarning("EVOLUTION_PROTOTYPE_DAY could not append the day log: " + e.Message); }
        }

        /// <summary>Tomorrow's first visitors and their wishes, as arrivals pick them (the night receipt's forecast).</summary>
        public List<PrototypeForecast> Forecast(int count = ForecastVisitors)
        {
            var result = new List<PrototypeForecast>();
            if (!PrototypeActive || !Customers || Cycle.Phase != ShopDayPhase.Night) return result;
            for (int i = 0; i < count; i++)
            {
                var type = Customers.ForecastArchetype(Cycle.DayNumber, i);
                if (type == null) break;
                var wish = Customers.WishFor(Cycle.DayNumber, i, type);
                result.Add(new PrototypeForecast { ArchetypeId = type.Id, Archetype = type.DisplayName, Wish = wish != null ? wish.Label : null });
            }
            return result;
        }
    }

    public struct PrototypeThemeTag
    {
        public string TankId;
        public ShopTheme Theme;
        public float X, Z;
        public bool Matching;
    }
}
