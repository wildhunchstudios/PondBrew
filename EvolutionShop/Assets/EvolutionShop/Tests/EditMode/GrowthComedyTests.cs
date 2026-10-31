using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>GR-18 wave 4 (§6.7): the slap's rules (GrowthShop.Slap: by day, never at an exhibition guest, once every few
    /// seconds; a casual visitor laughs off the first; anyone else leaves in a huff, their reservation released, at a small
    /// reputation cost capped each day; never a payment, never a sale) and the silly moments' shared timing (GrowthComedy: one
    /// at a time, quiet in between, each kind's cooldown, fewer and shorter at Fewer, only the slap at Off, Skip, the room's
    /// moments in turn, an attempt that finds no one dropped) and their lines.</summary>
    public sealed class GrowthComedyTests
    {
        static GrowthShop OpenShop(params string[] species)
        {
            var shop = new GrowthShop();
            foreach (var s in species) shop.Containers[0].Occupants.Add(new GrowthSpecimen { Id = 1000 + shop.Containers[0].Occupants.Count, Species = s });
            Assert.That(shop.OpenShop().Success, Is.True, "the shop opens");
            shop.Visitors.Clear();
            shop.Reputation = 20;
            return shop;
        }

        static GrowthVisitor Visitor(GrowthShop shop, int id, string type, GrowthVisitorState state = GrowthVisitorState.Browsing)
        {
            var v = new GrowthVisitor { Id = id, Type = type, Budget = 500, State = state, ContainerId = shop.Containers[0].Id };
            if (state == GrowthVisitorState.Queued) v.Patience = GrowthShop.QueuePatience;
            shop.Visitors.Add(v);
            return v;
        }

        [Test]
        public void ASlapIsTheDaysOnlyNeverAtAGuestAndWaitsForTheHand()
        {
            var night = new GrowthShop();
            night.Visitors.Add(new GrowthVisitor { Id = 9, Type = "bargain" });
            Assert.That(night.Slap(9).Failure, Is.EqualTo(GrowthFailure.NotDuringDay), "no slaps at night");
            var shop = OpenShop("pondlet");
            Visitor(shop, 1, GrowthShop.GuestType);
            Visitor(shop, 2, "bargain");
            Visitor(shop, 3, "collector");
            int rep = shop.Reputation;
            Assert.That(shop.Slap(1).Success, Is.False, "an exhibition guest is here on business");
            Assert.That(shop.Visitors.First(v => v.Id == 1).State, Is.EqualTo(GrowthVisitorState.Browsing), "the guest carries on");
            Assert.That(shop.Slap(404).Failure, Is.EqualTo(GrowthFailure.NoSuchVisitor), "nobody there");
            Assert.That(shop.Slap(2).Success, Is.True);
            Assert.That(shop.Slap(2).Failure, Is.EqualTo(GrowthFailure.NoSuchVisitor), "someone already on their way out");
            Assert.That(shop.Slap(3).Failure, Is.EqualTo(GrowthFailure.Busy), "the hand is still stinging");
            shop.Tick(GrowthShop.SlapCooldown - 0.1f);
            Assert.That(shop.Slap(3).Failure, Is.EqualTo(GrowthFailure.Busy));
            shop.Tick(0.2f);
            Assert.That(shop.Slap(3).Success, Is.True, "a few seconds later, again");
            Assert.That(shop.Reputation, Is.EqualTo(rep - 2));
        }

        [Test]
        public void ACasualVisitorLaughsOffTheFirstSlapAndLeavesAtTheSecond()
        {
            var shop = OpenShop("pondlet");
            var v = Visitor(shop, 1, "casual");
            int rep = shop.Reputation, credits = shop.Credits;
            var r = shop.Slap(1);
            Assert.That(r.Success, Is.True);
            Assert.That(r.Detail, Is.EqualTo("laughed"));
            Assert.That(v.State, Is.EqualTo(GrowthVisitorState.Browsing), "they laugh it off and carry on browsing");
            Assert.That(shop.Reputation, Is.EqualTo(rep), "no harm done");
            Assert.That(shop.Today.LaughedOff, Is.EqualTo(1));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Slapped && e.Text.Contains("laughed")), Is.True, "the note says so");
            shop.Tick(GrowthShop.SlapCooldown);
            r = shop.Slap(1);
            Assert.That(r.Detail, Is.EqualTo("left"), "twice is not funny");
            Assert.That(v.State, Is.EqualTo(GrowthVisitorState.Leaving));
            Assert.That(v.Outcome, Is.EqualTo(GrowthVisitOutcome.Slapped));
            Assert.That(shop.Reputation, Is.EqualTo(rep + GrowthShop.SlapReputation));
            Assert.That(shop.Credits, Is.EqualTo(credits), "a slap never pays");
            Assert.That(shop.Today.Slapped, Is.EqualTo(1));
            Assert.That(shop.TakeEvents().Any(e => e.Kind == GrowthEventKind.Slapped && e.Text.Contains("huff")), Is.True);
        }

        [Test]
        public void ASlappedVisitorReleasesWhatTheyReservedAndNeverBuysIt()
        {
            var shop = OpenShop("pondlet", "pondlet");
            var first = Visitor(shop, 1, "collector", GrowthVisitorState.Queued);
            var specimen = shop.Containers[0].Occupants[0];
            Assert.That(shop.ReserveAll(first, new[] { specimen }), Is.True);
            int sales = shop.SalesTotal, credits = shop.Credits;
            Assert.That(shop.Slap(1).Detail, Is.EqualTo("left"));
            Assert.That(specimen.ReservedBy, Is.EqualTo(0), "the reservation is released");
            Assert.That(shop.Containers[0].Occupants, Does.Contain(specimen), "the creature stays in its tank");
            Assert.That(first.SpecimenIds, Is.Empty, "nothing goes home with them");
            Assert.That(shop.Queue, Is.Empty, "they leave the queue");
            Assert.That(shop.SalesTotal, Is.EqualTo(sales)); Assert.That(shop.Credits, Is.EqualTo(credits));
            // The next visitor can have it, and buys it at the counter as usual.
            var second = Visitor(shop, 2, "bargain", GrowthVisitorState.Queued);
            Assert.That(shop.ReserveAll(second, new[] { specimen }), Is.True, "free for someone else");
            for (int i = 0; i < 200 && shop.Queue.Any(); i++) shop.Serve(0.1f);
            Assert.That(shop.SalesTotal, Is.EqualTo(sales + 1), "one sale, the next visitor's");
            Assert.That(second.Outcome, Is.EqualTo(GrowthVisitOutcome.Bought));
            Assert.That(first.Outcome, Is.EqualTo(GrowthVisitOutcome.Slapped), "the slapped one still left in a huff");
        }

        [Test]
        public void SlappingTheVisitorBeingServedStartsTheBaggingAgainForTheNext()
        {
            var shop = OpenShop("pondlet", "ribbonfin");
            var a = Visitor(shop, 1, "bargain", GrowthVisitorState.Queued); a.Clock = 5;
            var b = Visitor(shop, 2, "collector", GrowthVisitorState.Queued); b.Clock = 1;
            Assert.That(shop.ReserveAll(a, new[] { shop.Containers[0].Occupants[0] }), Is.True);
            Assert.That(shop.ReserveAll(b, new[] { shop.Containers[0].Occupants[1] }), Is.True);
            shop.Serve(shop.ServeSeconds * 0.8f);
            Assert.That(shop.ServeProgress, Is.GreaterThan(0));
            Assert.That(shop.Slap(1).Detail, Is.EqualTo("left"));
            Assert.That(shop.ServeProgress, Is.EqualTo(0), "the half-bagged order is not carried over to the next visitor");
            int sales = shop.SalesTotal;
            shop.Serve(shop.ServeSeconds * 0.5f);
            Assert.That(shop.SalesTotal, Is.EqualTo(sales), "the next visitor's order is bagged from the start");
            shop.Serve(shop.ServeSeconds * 0.6f);
            Assert.That(shop.SalesTotal, Is.EqualTo(sales + 1));
            Assert.That(b.Outcome, Is.EqualTo(GrowthVisitOutcome.Bought));
        }

        [Test]
        public void SlappingTheVisitorPipIsServingLetsPipsOrderGoWithNoSale()
        {
            var shop = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 50000, Rp = 500, SalesTotal = GrowthContent.PipSalesGate };
            Assert.That(shop.Hire("pip").Success, Is.True);
            var tank = shop.Containers[0];
            tank.Occupants.Add(new GrowthSpecimen { Id = 801, Species = "pondlet" }); tank.Occupants.Add(new GrowthSpecimen { Id = 802, Species = "pondlet" });
            shop.OpenShop();
            shop.Visitors.Clear();
            var a = Visitor(shop, 1, "bargain", GrowthVisitorState.Queued); a.Clock = 5;
            var b = Visitor(shop, 2, "collector", GrowthVisitorState.Queued); b.Clock = 1;
            Assert.That(shop.ReserveAll(a, new[] { tank.Occupants[0] }) && shop.ReserveAll(b, new[] { tank.Occupants[1] }), Is.True);
            shop.Tick(1.5f);
            Assert.That(shop.Helper("pip").Step, Is.EqualTo("serve")); Assert.That(shop.PipVisitor, Is.EqualTo(1), "Pip bags the front visitor's order");
            int sales = shop.SalesTotal;
            Assert.That(shop.Slap(1).Detail, Is.EqualTo("left"));
            shop.Tick(1.6f);
            Assert.That(shop.SalesTotal, Is.EqualTo(sales), "no sale for the slapped visitor (Pip's half-bagged order is let go of)");
            Assert.That(shop.PipVisitor, Is.EqualTo(2), "Pip starts on the next visitor's order from the beginning");
            shop.Tick(3.1f);
            Assert.That(shop.SalesTotal, Is.EqualTo(sales + 1));
            Assert.That(b.Outcome, Is.EqualTo(GrowthVisitOutcome.Bought)); Assert.That(a.Outcome, Is.EqualTo(GrowthVisitOutcome.Slapped));
            Assert.That(tank.Occupants.Select(o => o.Id), Is.EqualTo(new[] { 801 }), "the slapped visitor's creature stays, free again");
            Assert.That(tank.Occupants[0].ReservedBy, Is.EqualTo(0));
        }

        [Test]
        public void SlapsCostAtMostTwoReputationADay()
        {
            var shop = OpenShop("pondlet");
            shop.Reputation = 20;
            for (int i = 0; i < 5; i++)
            {
                Visitor(shop, 10 + i, "bargain");
                Assert.That(shop.Slap(10 + i).Detail, Is.EqualTo("left"));
                shop.Tick(GrowthShop.SlapCooldown);
            }
            Assert.That(shop.Reputation, Is.EqualTo(20 + GrowthShop.SlapPenaltiesADay * GrowthShop.SlapReputation), "the day's first two huffs cost a point each; later ones are free");
            Assert.That(shop.Today.Slapped, Is.EqualTo(5));
            // A new day: the next huff costs again.
            while (shop.Phase == GrowthPhase.Day) shop.Tick(5f);
            Assert.That(shop.OpenShop().Success, Is.True);
            shop.Visitors.Clear();
            Visitor(shop, 99, "premium");
            int before = shop.Reputation;
            Assert.That(shop.Slap(99).Detail, Is.EqualTo("left"));
            Assert.That(shop.Reputation, Is.EqualTo(before + GrowthShop.SlapReputation));
        }

        [Test]
        public void TheMomentsPlayOneAtATimeWithQuietBetweenAndEachKindsCooldown()
        {
            var c = new GrowthComedy();
            Assert.That(c.Start(GrowthGag.Fart, 1), Is.True);
            Assert.That(c.WhyNot(GrowthGag.Complaint), Is.EqualTo("busy"), "one at a time");
            Run(c, GrowthComedy.Length - 0.05f);
            Assert.That(c.Current, Is.EqualTo(GrowthGag.Fart));
            Assert.That(c.Age, Is.EqualTo(GrowthComedy.Length - 0.05f).Within(0.01f));
            Run(c, 0.1f);
            Assert.That(c.Current, Is.Null, "it ends with its length");
            Assert.That(c.WhyNot(GrowthGag.Complaint), Is.EqualTo("quiet"), "then quiet");
            Run(c, GrowthComedy.Quiet);
            Assert.That(c.CanStart(GrowthGag.Complaint), Is.True);
            Assert.That(c.WhyNot(GrowthGag.Fart), Is.EqualTo("cooldown"), "the fart's own cooldown is longer than the quiet");
            Run(c, GrowthComedy.Cooldown(GrowthGag.Fart));
            Assert.That(c.CanStart(GrowthGag.Fart), Is.True);
            foreach (GrowthGag g in System.Enum.GetValues(typeof(GrowthGag)))
                if (g != GrowthGag.Slap && g != GrowthGag.Boop) Assert.That(GrowthComedy.Cooldown(g), Is.GreaterThanOrEqualTo(GrowthComedy.Quiet), g + " never comes back sooner than the quiet");
            // Paused, nothing moves on.
            c.Start(GrowthGag.Belch, 0);
            float age = c.Age;
            c.Tick(0); c.Tick(-1); c.Tick(float.NaN);
            Assert.That(c.Age, Is.EqualTo(age), "no time, no change");
        }

        [Test]
        public void FewerMomentsAreShorterAndRarerAndOffLeavesOnlyTheSlap()
        {
            var fewer = new GrowthComedy { Level = GrowthGagLevel.Fewer };
            Assert.That(fewer.LengthOf(GrowthGag.Complaint), Is.LessThan(GrowthComedy.Length));
            fewer.Start(GrowthGag.Complaint, 1);
            Run(fewer, fewer.LengthOf(GrowthGag.Complaint) + GrowthComedy.Quiet + 0.1f);
            Assert.That(fewer.WhyNot(GrowthGag.Fart), Is.EqualTo("quiet"), "the quiet is longer");
            Run(fewer, GrowthComedy.FewerQuiet);
            Assert.That(fewer.CanStart(GrowthGag.Fart), Is.True);
            Run(fewer, GrowthComedy.Cooldown(GrowthGag.Complaint));
            Assert.That(fewer.WhyNot(GrowthGag.Complaint), Is.EqualTo("cooldown"), "each kind's cooldown doubles");
            var off = new GrowthComedy { Level = GrowthGagLevel.Off };
            foreach (var g in new[] { GrowthGag.Fart, GrowthGag.Complaint, GrowthGag.Belch, GrowthGag.Dance, GrowthGag.Mimic })
                Assert.That(off.WhyNot(g), Is.EqualTo("off"));
            Run(off, 120);
            Assert.That(off.AmbientDue(), Is.False, "the room tries nothing");
            Assert.That(off.Start(GrowthGag.Slap, 7), Is.True, "the player's own slap still lands");
        }

        [Test]
        public void ASlapCutsAnotherMomentShortAndSkipKeepsTheQuiet()
        {
            var c = new GrowthComedy();
            c.Start(GrowthGag.Mimic, 3);
            Run(c, 0.5f);
            Assert.That(c.Start(GrowthGag.Slap, 4), Is.True);
            Assert.That(c.Current, Is.EqualTo(GrowthGag.Slap)); Assert.That(c.Subject, Is.EqualTo(4));
            Assert.That(c.CutShort, Is.EqualTo(1));
            Assert.That(c.Duration, Is.EqualTo(GrowthComedy.SlapLength).Within(1e-4f));
            c.Cancel(true);
            Assert.That(c.Current, Is.Null);
            Assert.That(c.Skipped, Is.EqualTo(1));
            Assert.That(c.WhyNot(GrowthGag.Complaint), Is.EqualTo("quiet"), "a skipped moment still leaves the quiet after it");
            c.Clear();
            Assert.That(c.CanStart(GrowthGag.Mimic), Is.True, "a new shop forgets it all");
        }

        [Test]
        public void TheRoomTriesItsMomentsInTurnAndDropsAnAttemptThatFindsNoOne()
        {
            var c = new GrowthComedy();
            Run(c, GrowthComedy.AmbientEvery - 0.1f);
            Assert.That(c.AmbientDue(), Is.False);
            Run(c, 0.2f);
            Assert.That(c.AmbientDue(), Is.True);
            Assert.That(c.AmbientDue(), Is.False, "and then waits again");
            Assert.That(new[] { c.NextAmbient(), c.NextAmbient(), c.NextAmbient(), c.NextAmbient() },
                Is.EqualTo(new[] { GrowthGag.Fart, GrowthGag.Complaint, GrowthGag.Mimic, GrowthGag.Fart }), "in turn");
            c.Drop();
            Assert.That(c.Dropped, Is.EqualTo(1));
            Run(c, GrowthComedy.AmbientEvery - 1);
            Assert.That(c.AmbientDue(), Is.False, "a dropped attempt is not saved up for later");
            c.Start(GrowthGag.Fart, 1);
            Run(c, 30);
            Assert.That(c.Current, Is.Null);
        }

        [Test]
        public void TheLinesComeInTurnAndAreMildGibberish()
        {
            var c = new GrowthComedy();
            Assert.That(new[] { c.Variant(GrowthGag.Complaint, 3), c.Variant(GrowthGag.Complaint, 3), c.Variant(GrowthGag.Complaint, 3), c.Variant(GrowthGag.Complaint, 3) }, Is.EqualTo(new[] { 0, 1, 2, 0 }));
            Assert.That(c.Variant(GrowthGag.Fart, 3), Is.EqualTo(0), "each kind its own turn");
            var all = GrowthComedy.Grumbles.Concat(GrowthComedy.Laughs).Concat(GrowthComedy.Blames).Concat(GrowthComedy.Complaints).Concat(GrowthComedy.Belches).Concat(GrowthComedy.Mimics)
                .Concat(new[] { "pip", "nori", "moss" }.SelectMany(h => new[] { GrowthComedy.Cheer(h, 0), GrowthComedy.Cheer(h, 1) })).ToList();
            foreach (var line in all)
            {
                Assert.That(line.Length, Is.InRange(4, 60), line + " is short enough for a caption");
                foreach (var word in new[] { "damn", "hell", "crap", "stupid", "idiot", "shut up" }) StringAssert.DoesNotContain(word, line.ToLowerInvariant());
            }
            Assert.That(GrowthComedy.Line(GrowthComedy.Complaints, 0, "Pondlet"), Is.EqualTo("That Pondlet looks just like my accountant."));
            Assert.That(GrowthComedy.Line(GrowthComedy.Blames, -1, "Pondlet"), Does.Contain("Pondlet"), "any turn is a line");
            Assert.That(GrowthShop.TypeName("casual"), Is.EqualTo("Casual visitor"));
            Assert.That(GrowthShop.TypeName(GrowthShop.GuestType), Is.EqualTo("Exhibition guest"));
        }

        static void Run(GrowthComedy c, float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.05f) c.Tick(System.Math.Min(0.05f, seconds - t)); }
    }
}
