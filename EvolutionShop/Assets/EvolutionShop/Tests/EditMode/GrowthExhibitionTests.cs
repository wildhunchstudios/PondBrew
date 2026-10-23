using System;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-11: GR.12's objective board (now, next and the long goal, from the first night), every era's gate with
    /// its requirement and progress, and the Grand Exhibition (R09.2): its requirements on the new roster, five different
    /// crowns on display on the spotlit places and protected, six announced purchases bought by six guests through the
    /// queue, held once with free play after, an attempt that falls short losing nothing, and the version 10 save.</summary>
    public sealed class GrowthExhibitionTests
    {
        /// <summary>The exhibition start point with its five crowns carried to the tanks on the five spotlit places.</summary>
        static GrowthShop Ready()
        {
            var shop = GrowthStarts.ExhibitionStart();
            var crowns = shop.Stock.Where(s => s.Def.Kind == SpeciesKind.Crown).ToList();
            Assert.That(crowns.Select(c => c.Species).Distinct().Count(), Is.EqualTo(5));
            int k = 0;
            foreach (var c in crowns)
            {
                Assert.That(shop.TakeOut(shop.HolderOf(c.Id).Id, c.Id).Success, Is.True);
                Assert.That(shop.PutIn(shop.ContainerAt(GrowthShop.SpotlitPlaces[k++ % GrowthShop.SpotlitPlaces.Length]).Id).Success, Is.True);
            }
            return shop;
        }

        /// <summary>Keeps at least each of every announced creature still to buy in stock (as a player or Nori would).</summary>
        static void TopUp(GrowthShop shop, int each = 2, string except = null)
        {
            foreach (var o in shop.ExhibitionOrders.Where(o => !o.Bought && o.Species != except))
                while (shop.Stock.Count(s => s.Species == o.Species && s.ReservedBy == 0) < each)
                {
                    if (shop.ShopContainers.Where(c => !GrowthShop.SpotlitPlaces.Contains(c.Place)).Any(c => shop.DevelopmentStock(c.Id, o.Species).Success)) continue;
                    var spare = shop.ShopContainers.Where(c => !GrowthShop.SpotlitPlaces.Contains(c.Place)).SelectMany(c => c.Occupants)
                        .FirstOrDefault(s => s.ReservedBy == 0 && shop.ExhibitionOrders.All(x => x.Species != s.Species));
                    Assert.That(spare, Is.Not.Null, "room for " + o.Species);
                    shop.HolderOf(spare.Id).Occupants.Remove(spare);
                }
        }

        static void RunDay(GrowthShop shop, Action eachTick = null)
        {
            Assert.That(shop.OpenShop().Success, Is.True);
            for (int i = 0; i < 6000 && shop.Phase == GrowthPhase.Day; i++)
            {
                shop.Tick(0.1f);
                eachTick?.Invoke();
                shop.Serve(shop.ServeSeconds);
            }
            Assert.That(shop.Phase, Is.EqualTo(GrowthPhase.Night));
        }

        [Test]
        public void TheBoardShowsNowNextAndTheLongGoalFromTheFirstNight()
        {
            var shop = new GrowthShop();
            var b = shop.Board();
            Assert.That((b.NowTitle, b.Goal), Is.EqualTo(("Era 1 · Back room", "Discover 3 creatures and buy your second tank")));
            Assert.That(b.Now.Select(l => l.text), Has.Member("Discover 3 creatures (0/3)"));
            Assert.That((b.NextTitle, b.NextGate, b.NextOpens), Is.EqualTo(("Next: era 2 · Real shop", "2 stars, 3 creatures discovered, 2 tanks", "Shop unit B can open")));
            Assert.That(b.LongGoal, Is.EqualTo("Discover creatures: 0 / 72 · Journal: 0 / 288 · Exhibition: 0 of 6 requirements"));
            Assert.That(b.Exhibition.Select(l => l.text), Is.EqualTo(new[]
            {
                "The final shop: shop unit C open and the exhibition hall finished", "The final lab: the workshop open", "Discover 48 of the 72 creatures (0/48)",
                "… from all 9 families (0/9)", "Discover 5 of the 9 crowns (0/5)", "Run a full day on the line",
            }), "the ending shows from the first night (§4: never a surprise late requirement)");
            Assert.That(b.Exhibition.All(l => !l.done), Is.True);
            // Era 5: next is the Grand Exhibition itself, and the era's lines end with hosting it.
            var five = GrowthStarts.SpacesStart();
            Assert.That(five.Board().NextTitle, Is.EqualTo("Next: the Grand Exhibition"));
            Assert.That(five.Objectives().Last(), Is.EqualTo(("Host it: six exhibition guests buy what they came for (the day after you prepare it)", false)));
        }

        [Test]
        public void EveryEraGateShowsItsRequirementAndItsProgress()
        {
            for (int era = 1; era <= 4; era++)
            {
                var shop = new GrowthShop { Era = era, UnitB = era > 1 };
                var b = shop.Board();
                var next = GrowthContent.Eras[era];
                Assert.That((b.NowTitle, b.NextTitle, b.NextGate, b.NextOpens), Is.EqualTo(("Era " + era + " · " + GrowthContent.Eras[era - 1].Name, "Next: era " + (era + 1) + " · " + next.Name, next.Gate, next.Opens)));
                Assert.That(b.Now, Is.Not.Empty, "era " + era + " lists its goal's steps");
                Assert.That(b.Now.Any(l => l.text.Contains("/") || l.text.Contains("credits")), Is.True, "with their progress");
                Assert.That(next.Opens, Is.Not.Empty.And.Not.EndsWith("."));
            }
            // The era-up banners say what each era opens, from the same table.
            var two = new GrowthShop { Reputation = GrowthContent.StarThresholds[1] + 1 };
            two.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            two.Containers.Add(new GrowthContainer { Id = "second", Kind = "starter", Place = "a2" });
            two.OpenShop(); two.Tick(1);
            two.DaySecondsLeft = 0.01f; two.Tick(0.1f);
            Assert.That(two.Era, Is.EqualTo(2));
            Assert.That(two.TakeEvents().Single(e => e.Kind == GrowthEventKind.EraUp).Text, Is.EqualTo("Era 2: Real shop. Shop unit B can open."));
        }

        [Test]
        public void TheExhibitionOpensOnlyWithEveryRequirement()
        {
            void Refused(Action<GrowthShop> change, string line)
            {
                var shop = Ready();
                change(shop);
                Assert.That(shop.PrepareCheck(out string why), Is.EqualTo(GrowthFailure.Locked), line);
                Assert.That(why, Does.StartWith("Not yet open: " + line));
                Assert.That(shop.PrepareExhibition().Success, Is.False);
            }
            Refused(s => s.Hall = false, "The final shop");
            Refused(s => s.Workshop = false, "The final lab");
            Refused(s => { while (s.Discovered.Count > 47) s.Discovered.Remove(s.Discovered.Last(d => GrowthContent.Get(d).Kind == SpeciesKind.Founder)); }, "Discover 48 of the 72 creatures (47/48)");
            Refused(s => s.Discovered.RemoveAll(d => GrowthContent.Get(d).Family == "cometail" && GrowthContent.Get(d).Kind != SpeciesKind.Crown), "… from all 9 families");
            Refused(s => s.Discovered.Remove(s.Discovered.First(d => GrowthContent.Get(d).Kind == SpeciesKind.Crown)), "Discover 5 of the 9 crowns");
            Refused(s => s.LineDays = 0, "Run a full day on the line");
            // Open: five different crowns must be on display on the spotlit places (crowns elsewhere don't count).
            var start = GrowthStarts.ExhibitionStart();
            Assert.That(start.ExhibitionOpen, Is.True);
            Assert.That(start.PrepareCheck(out string why2), Is.EqualTo(GrowthFailure.NotEnough));
            Assert.That(why2, Is.EqualTo("Put 5 different crowns on display in tanks on the spotlit places (0/5)"));
            var four = Ready();
            var fifth = four.CrownsOnSpotlitPlaces.Last();
            four.HolderOf(fifth.Id).Occupants.Remove(fifth);
            four.ContainerAt("e2").Occupants.Add(new GrowthSpecimen { Id = 99001, Species = four.CrownsOnSpotlitPlaces.First().Species });
            Assert.That((four.PrepareCheck(out why2), why2), Is.EqualTo((GrowthFailure.NotEnough, "Put 5 different crowns on display in tanks on the spotlit places (4/5)")), "two of one crown are one");
            var day = Ready(); day.OpenShop();
            Assert.That(day.PrepareCheck(out _), Is.EqualTo(GrowthFailure.NotAtNight));
            Assert.That(Ready().PrepareCheck(out _), Is.EqualTo(GrowthFailure.None));
        }

        [Test]
        public void CrownsOnDisplayAreProtectedUntilTheExhibitionCloses()
        {
            var shop = Ready();
            Assert.That(shop.PrepareExhibition().Success, Is.True);
            var shown = shop.OnDisplay.ToList();
            Assert.That(shown.Count, Is.EqualTo(5));
            Assert.That(shown.All(s => s.Def.Kind == SpeciesKind.Crown && GrowthShop.SpotlitPlaces.Contains(shop.HolderOf(s.Id).Place)), Is.True);
            // No hand takes one, no tank with one moves, no visitor picks one.
            var first = shown[0];
            var take = shop.TakeOut(shop.HolderOf(first.Id).Id, first.Id);
            Assert.That((take.Failure, take.Detail), Is.EqualTo((GrowthFailure.Reserved, "on display for the Grand Exhibition")));
            shop.Containers.Remove(shop.ContainerAt("a3"));
            Assert.That(shop.MoveCheck(shop.ContainerAt("e1").Id, "a3", out string why), Is.EqualTo(GrowthFailure.Reserved), "a long place is free for e1's Large tank");
            Assert.That(why, Is.EqualTo("Its crowns are on display for the Grand Exhibition"));
            var rich = new GrowthVisitor { Id = 7, Type = "premium", Budget = 100000 };
            Assert.That(shop.PickFrom(rich, shop.HolderOf(first.Id)), Is.Null, "a tank holding only crowns on display offers nothing");
            shop.HolderOf(first.Id).Occupants.Add(new GrowthSpecimen { Id = 99002, Species = "pondlet" });
            Assert.That(shop.PickFrom(rich, shop.HolderOf(first.Id)).Single().Species, Is.EqualTo("pondlet"));
            shop.HolderOf(first.Id).Occupants.RemoveAll(o => o.Id == 99002);
            Assert.That(shop.ReserveAll(rich, new[] { first }), Is.False);
            // The day: no visitor's plan looks into a tank holding only crowns on display, and the crowns are all still there at close.
            TopUp(shop);
            bool sawDisplayOnly = false;
            var arrived = new System.Collections.Generic.HashSet<int>();
            RunDay(shop, () =>
            {
                TopUp(shop);
                foreach (var v in shop.Visitors.Where(v => v.Type != GrowthShop.GuestType && arrived.Add(v.Id)))
                    sawDisplayOnly |= v.Plan.Any(id => shop.Container(id).Occupants.Count > 0 && shop.Container(id).Occupants.All(GrowthShop.IsOnDisplay));
            });
            Assert.That(sawDisplayOnly, Is.False);
            Assert.That(shown.All(s => shop.HolderOf(s.Id) != null), Is.True, "every crown on display is still there");
            Assert.That(shop.OnDisplay, Is.Empty, "released at close");
            // Calling it off frees them too.
            var off = Ready();
            off.PrepareExhibition();
            Assert.That(off.CallOffExhibition().Success && !off.ExhibitionPrepared && !off.OnDisplay.Any(), Is.True);
            var crown = off.CrownsOnSpotlitPlaces.First();
            Assert.That(off.TakeOut(off.HolderOf(crown.Id).Id, crown.Id).Success, Is.True);
        }

        [Test]
        public void PreparingAnnouncesSixKnownCreaturesFromAtLeastFourFamilies()
        {
            var shop = Ready();
            Assert.That(shop.ExhibitionOrders, Is.Empty);
            Assert.That(shop.PrepareExhibition().Success, Is.True);
            var six = shop.ExhibitionOrders.Select(o => o.Species).ToList();
            Assert.That(six.Count, Is.EqualTo(GrowthShop.ExhibitionPurchases));
            Assert.That(six.Distinct().Count(), Is.EqualTo(6));
            Assert.That(six.All(id => shop.Discovered.Contains(id) && GrowthContent.Get(id).Kind != SpeciesKind.Crown), Is.True, "known, never a crown");
            Assert.That(six.Select(id => GrowthContent.Get(id).Family).Distinct().Count(), Is.GreaterThanOrEqualTo(GrowthShop.ExhibitionFamilies));
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.Exhibition).Text, Is.EqualTo("The Grand Exhibition is tomorrow: six guests are coming for " + shop.PurchaseList()));
            Assert.That(Ready().AnnouncePurchases(), Is.EqualTo(six), "the same shop announces the same six");
            Assert.That(shop.CallOffExhibition().Success && shop.PrepareExhibition().Success, Is.True);
            Assert.That(shop.ExhibitionOrders.Select(o => o.Species), Is.EqualTo(six), "the same six on every attempt");
            // With only four families known beyond their crowns, six still come from all four.
            var narrow = Ready();
            var keep = GrowthContent.Families.Take(4).Select(f => f.Id).ToList();
            narrow.Discovered.RemoveAll(d => !keep.Contains(GrowthContent.Get(d).Family) && GrowthContent.Get(d).Kind != SpeciesKind.Crown);
            var fromFour = narrow.AnnouncePurchases();
            Assert.That((fromFour.Count, fromFour.Distinct().Count(), fromFour.Select(id => GrowthContent.Get(id).Family).Distinct().Count()), Is.EqualTo((6, 6, 4)));
        }

        [Test]
        public void SixGuestsBuyThroughTheQueueAndTheExhibitionIsHeldOnce()
        {
            var shop = Ready();
            Assert.That(shop.PrepareExhibition().Success, Is.True);
            int credits = shop.Credits, day = shop.Day;
            var crowned = shop.OnDisplay.Select(s => s.Species).Distinct().ToList();
            TopUp(shop);
            int guestsSeen = 0, queued = 0;
            var seen = new System.Collections.Generic.HashSet<int>();
            RunDay(shop, () =>
            {
                TopUp(shop);
                foreach (var v in shop.Visitors.Where(v => v.Type == GrowthShop.GuestType))
                {
                    if (seen.Add(v.Id))
                    {
                        guestsSeen++;
                        Assert.That(v.Wants, Is.EqualTo(shop.ExhibitionOrders[v.Order].Species));
                        Assert.That(v.Plan.All(id => shop.Container(id).Occupants.Any(o => o.Species == v.Wants)), Is.True, "they look where it is");
                    }
                    if (v.State == GrowthVisitorState.Queued) { queued++; Assert.That(v.Patience, Is.GreaterThan(GrowthShop.QueuePatience), "a guest waits longer"); Assert.That(v.SpecimenIds.Select(id => shop.Stock.First(x => x.Id == id).Species), Is.EqualTo(new[] { v.Wants })); }
                }
            });
            Assert.That(guestsSeen, Is.EqualTo(6));
            Assert.That(queued, Is.GreaterThan(0), "they wait in the queue like anyone");
            var d = shop.LastDay;
            Assert.That((d.Exhibition, d.ExhibitionWon, d.Guests, d.GuestsServed), Is.EqualTo((true, true, 6, 6)));
            Assert.That(d.Visitors, Is.GreaterThan(6), "ordinary visitors came too");
            Assert.That(shop.ExhibitionDone && !shop.ExhibitionToday && !shop.ExhibitionPrepared && !shop.OnDisplay.Any(), Is.True);
            Assert.That(shop.Credits, Is.GreaterThan(credits));
            var r = shop.ExhibitionResults;
            Assert.That((r.Day, r.Attempts), Is.EqualTo((day, 1)));
            Assert.That(r.Guests, Is.EqualTo(shop.ExhibitionOrders.Select(o => o.Species)));
            Assert.That(r.Crowned, Is.EquivalentTo(crowned));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.ExhibitionWon && e.Text == "The Grand Exhibition is a success!"), Is.True);
            // Once: free play after, nothing to prepare again.
            Assert.That(shop.PrepareCheck(out string why), Is.EqualTo(GrowthFailure.MaxLevel));
            Assert.That(why, Is.EqualTo("The Grand Exhibition has been held: the shop is in free play"));
            Assert.That((shop.EraGoal, shop.Board().NowTitle, shop.Board().NextTitle), Is.EqualTo(("Free play: fill the journal", "Free play", "Next: free play")));
            Assert.That(shop.Objectives().First().text, Is.EqualTo("The Grand Exhibition was held on day " + day + ": the shop is in free play"));
            Assert.That(shop.Board().LongGoal, Does.EndWith("Exhibition: held on day " + day));
            // Free play goes on as a shop: the next day is an ordinary one.
            RunDay(shop);
            Assert.That((shop.LastDay.Exhibition, shop.ExhibitionDone, shop.ExhibitionAttempts), Is.EqualTo((false, true, 1)));
        }

        [Test]
        public void AnAttemptThatFallsShortLosesNothingAndCanBeTriedAgain()
        {
            var shop = Ready();
            Assert.That(shop.PrepareExhibition().Success, Is.True);
            var missing = shop.ExhibitionOrders[3].Species;
            // None of one announced creature in stock all day (labelled): its guest goes without.
            foreach (var c in shop.ShopContainers) c.Occupants.RemoveAll(s => s.Species == missing);
            var discovered = shop.Discovered.ToList();
            int credits = shop.Credits;
            TopUp(shop, except: missing);
            RunDay(shop, () => TopUp(shop, except: missing));
            Assert.That(shop.ExhibitionDone, Is.False);
            string name = GrowthContent.Get(missing).Name;
            Assert.That(shop.ExhibitionFailure, Is.EqualTo("the guest for a " + name + " found no " + name + " in stock"));
            Assert.That(shop.LastDay.ExhibitionNotes, Is.EqualTo(new[] { "the guest for a " + name + " found no " + name + " in stock" }));
            Assert.That((shop.LastDay.GuestsServed, shop.ExhibitionAttempts), Is.EqualTo((5, 1)));
            // Nothing lost: the day's sales stay made, discoveries and the crowns stay, the crowns are free again.
            Assert.That(shop.Credits, Is.EqualTo(credits + shop.LastDay.Revenue));
            Assert.That(shop.Discovered, Is.EqualTo(discovered));
            Assert.That(shop.DisplayReady, Is.EqualTo(5));
            Assert.That(shop.OnDisplay, Is.Empty);
            Assert.That(shop.TakeEvents().Single(e => e.Kind == GrowthEventKind.ExhibitionFailed).Text, Does.EndWith("Prepare it again any night: nothing is lost."));
            Assert.That(shop.Objectives().Single(o => o.text.StartsWith("Prepare the Grand Exhibition")).text, Does.EndWith("(again: nothing was lost)"));
            // The next night: the same six, and this time all of them in stock.
            var six = shop.ExhibitionOrders.Select(o => o.Species).ToList();
            Assert.That(shop.PrepareExhibition().Success, Is.True);
            Assert.That(shop.ExhibitionOrders.Select(o => o.Species), Is.EqualTo(six));
            Assert.That(shop.ExhibitionOrders.All(o => !o.Bought && o.Missed == ""), Is.True);
            TopUp(shop);
            RunDay(shop, () => TopUp(shop));
            Assert.That((shop.ExhibitionDone, shop.ExhibitionResults.Attempts), Is.EqualTo((true, 2)));
        }

        [Test]
        public void GuestsWaitLongerAndTakeOnlyWhatTheyCameFor()
        {
            Assert.That(GrowthShop.GuestPatience, Is.EqualTo(GrowthShop.QueuePatience * 3));
            var shop = Ready();
            var tank = shop.ContainerAt("a1");
            var guest = new GrowthVisitor { Id = 5, Type = GrowthShop.GuestType, Wants = "bubblebelly", Budget = 1000 };
            Assert.That(shop.PickFrom(guest, tank).Single().Species, Is.EqualTo("bubblebelly"));
            guest.Wants = "zephyray";
            Assert.That(shop.PickFrom(guest, tank), Is.Null, "nothing else, however dear");
            // Guests come at their times through the day, and do not take ordinary visitors' places.
            Assert.That(Enumerable.Range(0, 6).Select(GrowthShop.GuestArrival).Last(), Is.LessThan(0.75f));
            Assert.That(shop.PrepareExhibition().Success && shop.OpenShop().Success, Is.True);
            for (int i = 0; i < 50; i++) shop.Tick(0.1f);
            Assert.That(shop.Visitors.Count(v => v.Type == GrowthShop.GuestType), Is.Zero, "the first guest comes after a few seconds");
            for (int i = 0; i < 300 && !shop.Visitors.Any(v => v.Type == GrowthShop.GuestType); i++) shop.Tick(0.1f);
            Assert.That(shop.Visitors.Single(v => v.Type == GrowthShop.GuestType).Order, Is.EqualTo(0));
        }

        [Test]
        public void TheExhibitionSavesAtVersionTen()
        {
            var shop = Ready();
            Assert.That(shop.PrepareExhibition().Success, Is.True);
            var save = shop.Snapshot();
            Assert.That(save.version, Is.EqualTo(10));
            string json = JsonUtility.ToJson(save);
            var back = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(json), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That((back.ExhibitionPrepared, back.ExhibitionOrders.Count, back.OnDisplay.Count()), Is.EqualTo((true, 6, 5)));
            Assert.That(JsonUtility.ToJson(back.Snapshot()), Is.EqualTo(json));
            // Saved mid-exhibition with two bought and the third guest in the shop: two stay bought, and the third guest
            // comes in again after the reload (visitors are not saved).
            TopUp(back);
            back.OpenShop();
            for (int i = 0; i < 3000 && back.ExhibitionOrders[2].Guest == 0; i++) { TopUp(back); back.Tick(0.1f); back.Serve(back.ServeSeconds); }
            Assert.That((back.GuestsServed, back.ExhibitionOrders[2].Guest != 0), Is.EqualTo((2, true)));
            var mid = GrowthShop.Restore(JsonUtility.FromJson<GrowthSave>(JsonUtility.ToJson(back.Snapshot())), out problem);
            Assert.That(problem, Is.Null);
            Assert.That((mid.ExhibitionToday, mid.GuestsServed, mid.OnDisplay.Count(), mid.Visitors.Count), Is.EqualTo((true, 2, 5, 0)));
            mid.Tick(0.1f);
            Assert.That(mid.Visitors.Single(v => v.Type == GrowthShop.GuestType).Order, Is.EqualTo(2), "the third guest comes in again");
            // A version 9 save loads with no exhibition.
            var nine = GrowthStarts.SpacesStart().Snapshot(); nine.version = 9;
            Assert.That(GrowthShop.Restore(nine, out problem), Is.Not.Null, problem);
            // Refused.
            string Why(Action<GrowthSave> change) { var s = JsonUtility.FromJson<GrowthSave>(json); change(s); GrowthShop.Restore(s, out string p); return p; }
            Assert.That(Why(s => s.exhibitionToday = true), Is.EqualTo("An exhibition both prepared and under way"));
            Assert.That(Why(s => s.phase = "Day"), Is.EqualTo("An exhibition at the wrong time of day"));
            Assert.That(Why(s => s.exhibitionSpecies = s.exhibitionSpecies.Take(5).ToArray()), Is.EqualTo("The exhibition's purchases are not six"));
            Assert.That(Why(s => s.exhibitionSpecies[0] = s.exhibitionDisplay.Select(id => s.containers.SelectMany(b => b.occupants).First(o => o.id == id).species).First()), Does.StartWith("Unknown exhibition purchase"));
            Assert.That(Why(s => s.exhibitionDisplay = s.exhibitionDisplay.Take(4).ToArray()), Is.EqualTo("Fewer than 5 crowns on display"));
            Assert.That(Why(s => s.exhibitionDisplay[0] = s.containers.First(b => b.place == "a1").occupants[0].id), Is.EqualTo("A creature on display that is not a crown on a spotlit place"));
            Assert.That(Why(s => s.exhibitionPrepared = false), Is.EqualTo("Crowns on display with no exhibition"));
            Assert.That(Why(s => { s.exhibitionPrepared = false; s.exhibitionDisplay = new int[0]; s.exhibitionDone = true; }), Is.EqualTo("A Grand Exhibition held without its results"));
            Assert.That(Why(s => s.lineDays = 0), Is.EqualTo("An exhibition prepared before it opened"));
        }

        [Test]
        public void NothingTheEndingNeedsCanBecomeUnreachable()
        {
            // Selling every creature never undoes a discovery: the requirements count discoveries, not stock.
            var shop = Ready();
            foreach (var c in shop.Containers) c.Occupants.Clear();
            Assert.That(shop.ExhibitionOpen, Is.True);
            Assert.That(shop.DisplayReady, Is.Zero);
            // Every crown comes again from its special pair, every hybrid from a pair, every founder from its recipe.
            foreach (var crown in GrowthContent.Species.Where(s => s.Kind == SpeciesKind.Crown))
                Assert.That(GrowthRules.Breed(crown.PairA, crown.PairB).Id, Is.EqualTo(crown.Id), crown.Id);
            Assert.That(GrowthRules.Validate(), Is.Empty);
            // The announced six are always creatures the player knows how to make again.
            Assert.That(Ready().AnnouncePurchases().All(id => GrowthContent.Get(id).Kind != SpeciesKind.Crown && shop.Discovered.Contains(id)), Is.True);
        }
    }
}
