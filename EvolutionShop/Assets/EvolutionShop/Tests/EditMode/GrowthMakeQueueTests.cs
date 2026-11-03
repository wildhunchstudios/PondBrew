using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// GR-35 (tester round 2, F73; the user's choice GR.74): the incubator's queue. Make on a found recipe adds batches; each is
    /// dosed from the buffers and growers, never the siphon, and the next starts by itself once the tray is clear. It comes
    /// with the incubator's level 3; the auto-doser and Moss wait while it has batches; a save keeps it.
    /// </summary>
    public sealed class GrowthMakeQueueTests
    {
        const string Species = "pondlet";

        /// <summary>A shop with the incubator at level 3, the Pondlet found and its growers full.</summary>
        static GrowthShop Ready()
        {
            var shop = new GrowthShop();
            shop.Levels["incubator"] = GrowthShop.MakeQueueLevel;
            var def = GrowthContent.Get(Species);
            shop.MixLog.Add(new GrowthMixEntry { Doses = (int[])def.Recipe.Clone(), Species = Species, Exact = true });
            if (!shop.Discovered.Contains(Species)) shop.Discovered.Add(Species);
            foreach (var c in Cultures(def))
            {
                if (!shop.Cultures.Contains(c)) shop.Cultures.Add(c);
                shop.GrowerUnits[c] = shop.GrowerCapacityOf(c);
            }
            return shop;
        }

        static string[] Cultures(SpeciesDef def) => Enumerable.Range(0, def.Recipe.Length).Where(i => def.Recipe[i] > 0).Select(i => GrowthContent.Cultures[i].Id).ToArray();

        static void Hatch(GrowthShop shop) { for (int i = 0; i < 60 && shop.Incubating; i++) shop.Tick(0.5f); }

        static void ClearTray(GrowthShop shop) { while (shop.Hatchlings.Count > 0) { shop.TakeHatchling(); shop.Carried.Clear(); } }

        [Test]
        public void F73_AQueuedRecipeIsDosedFromTheGrowersNeverTheSiphon_AndTheNextStartsByItself()
        {
            var shop = Ready();
            var def = GrowthContent.Get(Species);
            string first = Cultures(def)[0];
            shop.Held.Add(first);
            var before = Cultures(def).ToDictionary(c => c, c => shop.GrowerUnits[c]);
            var r = shop.QueueMake(Species, 3);
            Assert.That(r.Success, Is.True, r.ToString());
            Assert.That(r.Detail, Is.EqualTo("3"), "three batches added");
            Assert.That(shop.Incubating && shop.Pending == Species, Is.True, "the first starts at once");
            Assert.That(shop.MakeQueue, Has.Count.EqualTo(2), "two more wait");
            Assert.That(shop.MadeFromQueue, Is.EqualTo(1));
            Assert.That(shop.Held, Is.EqualTo(new[] { first }), "the siphon is never touched");
            foreach (var c in Cultures(def))
                Assert.That(shop.GrowerUnits[c], Is.EqualTo(before[c] - def.Recipe[GrowthContent.CultureIndex(c)]).Within(1e-4), c + " drawn from its grower");
            Hatch(shop);
            Assert.That(shop.Hatchlings, Is.Not.Empty, "it hatched");
            shop.Tick(0.1f);
            Assert.That(shop.Incubating, Is.False, "the next waits for the tray");
            Assert.That(shop.MakeWaiting(), Does.Contain("hatchlings"));
            ClearTray(shop);
            shop.Tick(0.1f);
            Assert.That(shop.Incubating && shop.MakeQueue.Count == 1 && shop.MadeFromQueue == 2, Is.True, "the next started by itself once the tray was clear");
        }

        [Test]
        public void F73_TheBuffersAreDrawnBeforeTheGrowers()
        {
            var shop = Ready();
            var def = GrowthContent.Get(Species);
            string c = Cultures(def)[0];
            shop.Buffers.Add(new GrowthBuffer { Place = 0, Culture = c, Units = 10 });
            float grower = shop.GrowerUnits[c];
            Assert.That(shop.QueueMake(Species).Success, Is.True);
            int need = def.Recipe[GrowthContent.CultureIndex(c)];
            Assert.That(shop.BufferAt(0).Units, Is.EqualTo(10 - need).Within(1e-4), "from the buffer");
            Assert.That(shop.GrowerUnits[c], Is.EqualTo(grower).Within(1e-4), "the grower untouched while a buffer holds it");
        }

        [Test]
        public void F73_ItWaitsForDosesAndSaysWhy_AndForTheChamber()
        {
            var shop = Ready();
            var def = GrowthContent.Get(Species);
            foreach (var c in Cultures(def)) shop.GrowerUnits[c] = 0;
            Assert.That(shop.QueueMake(Species).Success, Is.True, "queued even when the doses are not there yet");
            Assert.That(shop.Incubating, Is.False);
            Assert.That(shop.MakeWaiting(), Does.StartWith("Waiting for"), "says what it waits for");
            Assert.That(shop.MakeStatus, Is.EqualTo(shop.MakeWaiting()));
            foreach (var c in Cultures(def)) shop.GrowerUnits[c] = shop.GrowerCapacityOf(c);
            shop.Chamber.Add(Cultures(def)[0]);
            shop.Tick(0.1f);
            Assert.That(shop.Incubating, Is.False, "the player's doses in the chamber come first");
            Assert.That(shop.MakeWaiting(), Does.Contain("chamber"));
            shop.Chamber.Clear();
            shop.Tick(0.1f);
            Assert.That(shop.Incubating, Is.True, "then it starts");
        }

        [Test]
        public void F73_OnlyAFoundRecipe_FromTheIncubatorsLevel3_AndUpTo12()
        {
            var shop = Ready();
            shop.Levels["incubator"] = GrowthShop.MakeQueueLevel - 1;
            Assert.That(shop.QueueMake(Species).Failure, Is.EqualTo(GrowthFailure.Locked), "comes with level 3");
            shop.Levels["incubator"] = GrowthShop.MakeQueueLevel;
            var unknown = GrowthContent.Species.First(x => x.Kind == SpeciesKind.Founder && !shop.Discovered.Contains(x.Id)).Id;
            Assert.That(shop.QueueMake(unknown).Failure, Is.EqualTo(GrowthFailure.NotDiscovered), "never something not found");
            Assert.That(shop.QueueMake("puddlepup").Failure, Is.EqualTo(GrowthFailure.NotDiscovered), "a hybrid is never a recipe");
            foreach (var c in Cultures(GrowthContent.Get(Species))) shop.GrowerUnits[c] = 0;
            Assert.That(shop.QueueMake(Species, 20).Detail, Is.EqualTo(GrowthShop.MakeQueueLimit.ToString()), "as many as it has room for");
            Assert.That(shop.QueueMake(Species).Failure, Is.EqualTo(GrowthFailure.Busy), "the queue holds 12");
            Assert.That(shop.UnqueueMake(Species).Success && shop.MakeQueue.Count == GrowthShop.MakeQueueLimit - 1, Is.True, "one off");
            Assert.That(shop.UnqueueMake(Species, true).Success && shop.MakeQueue.Count == 0, Is.True, "all of it off");
            Assert.That(shop.UnqueueMake(Species).Success, Is.False);
            Assert.That(shop.ClearMakeQueue().Success, Is.False, "nothing to clear");
        }

        [Test]
        public void F73_TheAutoDoserAndMossWaitForTheQueue()
        {
            var shop = Ready();
            shop.Levels["incubator"] = 4;
            shop.AutoRecipe = Species;
            shop.AutoOn = true;
            foreach (var c in Cultures(GrowthContent.Get(Species))) shop.GrowerUnits[c] = 0;
            Assert.That(shop.QueueMake(Species).Success, Is.True);
            Assert.That(shop.AutoStatus, Is.EqualTo("Waiting for the incubator's queue"));
            shop.Tick(0.1f);
            Assert.That(shop.AutoBatch, Is.False, "no auto-doser batch while the queue has one");
            shop.ClearMakeQueue();
            Assert.That(shop.AutoStatus, Is.Not.EqualTo("Waiting for the incubator's queue"));
        }

        [Test]
        public void F73_ASaveKeepsTheQueue_AndOneFromBeforeHasNone()
        {
            var shop = Ready();
            foreach (var c in Cultures(GrowthContent.Get(Species))) shop.GrowerUnits[c] = 0;
            shop.QueueMake(Species, 4);
            shop.MadeFromQueue = 7;
            var save = UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot()));
            var back = GrowthShop.Restore(save, out string problem);
            Assert.That(problem, Is.Null.Or.Empty);
            Assert.That(back.MakeQueue, Is.EqualTo(shop.MakeQueue), "the batches in turn");
            Assert.That(back.MadeFromQueue, Is.EqualTo(7));
            save.makeQueue = null; save.madeFromQueue = 0; save.version = 15;
            Assert.That(GrowthShop.Restore(save, out problem).MakeQueue, Is.Empty, "a save from before has none");
            save.makeQueue = new[] { "nothing" };
            Assert.That(GrowthShop.Restore(save, out problem), Is.Null, "a queue of something unknown is not a save");
        }
    }
}
