using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;

namespace EvolutionShop.Tests
{
    /// <summary>
    /// GR-29 (tester round 1): nothing leaves the player stuck. Doses can always be poured away (the chamber, the siphon, a
    /// buffer), creatures can change places between a full tank and full hands, a click fills the chamber as far as it goes,
    /// and the mix log waits for the hatch.
    /// </summary>
    public sealed class GrowthDeadEndsTests
    {
        static GrowthShop WithSiphon(params string[] doses)
        {
            var shop = new GrowthShop();
            shop.Held.AddRange(doses);
            return shop;
        }

        static void Hatch(GrowthShop shop) { for (int i = 0; i < 40 && shop.Incubating; i++) shop.Tick(1); }

        [Test]
        public void TheMixLogWaitsForTheHatch()
        {
            var shop = WithSiphon("bloom", "bloom", "flow");
            Assert.That(shop.FillChamber().Success, Is.True);
            Assert.That(shop.StartIncubation().Success, Is.True);
            Assert.That(shop.MixLog, Is.Empty, "nothing logged while it incubates");
            Assert.That(shop.LastMix, Is.Null);
            Hatch(shop);
            Assert.That(shop.MixLog, Has.Count.EqualTo(1), "logged once it hatches");
            Assert.That(shop.MixLog[0].Species, Is.EqualTo(shop.Hatchling.Species));
            Assert.That(shop.LastMix, Is.SameAs(shop.MixLog[0]));
            Assert.That(shop.MixLog[0].WasNew, Is.True, "still counted as new: it was not found until it hatched");
        }

        [Test]
        public void ARepeatedMixCountsOnlyOnceItHatches()
        {
            var shop = WithSiphon("bloom", "bloom", "flow");
            shop.FillChamber(); shop.StartIncubation(); Hatch(shop); shop.TakeHatchling();
            shop.Held.AddRange(new[] { "bloom", "bloom", "flow" });
            shop.FillChamber(); shop.StartIncubation();
            Assert.That(shop.MixLog[0].Count, Is.EqualTo(1), "not yet");
            Hatch(shop);
            Assert.That(shop.MixLog, Has.Count.EqualTo(1));
            Assert.That(shop.MixLog[0].Count, Is.EqualTo(2));
            Assert.That(shop.MixLog[0].WasNew, Is.False);
        }

        [Test]
        public void AMixStillIncubatingIsLoggedAfterASaveAndReload()
        {
            var shop = WithSiphon("bloom", "bloom", "flow");
            shop.FillChamber(); shop.StartIncubation();
            var loaded = GrowthShop.Restore(UnityEngine.JsonUtility.FromJson<GrowthSave>(UnityEngine.JsonUtility.ToJson(shop.Snapshot())), out string problem);
            Assert.That(problem, Is.Null);
            Assert.That(loaded.MixLog, Is.Empty);
            Hatch(loaded);
            Assert.That(loaded.MixLog, Has.Count.EqualTo(1));
            Assert.That(loaded.MixLog[0].Doses, Is.EqualTo(shop.PendingMix.Doses));
        }

        [Test]
        public void AnOlderSaveMidIncubationHasAlreadyLoggedItsMix()
        {
            var shop = WithSiphon("bloom", "bloom", "flow");
            shop.FillChamber(); shop.StartIncubation();
            var save = shop.Snapshot();
            save.version = 13; save.pendingMix = null;
            save.mixLog = new[] { new GrowthSave.Mix { doses = shop.PendingMix.Doses, species = shop.Pending, exact = shop.PendingMix.Exact, count = 1 } };
            var loaded = GrowthShop.Restore(save, out string problem);
            Assert.That(problem, Is.Null);
            Hatch(loaded);
            Assert.That(loaded.MixLog, Has.Count.EqualTo(1), "not logged twice");
            Assert.That(loaded.MixLog[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void AClickFillsTheChamberAsFarAsItGoes()
        {
            var shop = WithSiphon("bloom", "flow");
            Assert.That(shop.FillChamber().Detail, Is.EqualTo("2"));
            Assert.That(shop.Chamber, Is.EqualTo(new[] { "bloom", "flow" }));
            Assert.That(shop.Held, Is.Empty);
            Assert.That(shop.FillChamber().Failure, Is.EqualTo(GrowthFailure.NoDose), "the siphon is empty");
            shop.Held.AddRange(new[] { "flow", "flow", "bloom" });
            Assert.That(shop.FillChamber().Detail, Is.EqualTo("1"), "one place left at level 1");
            Assert.That(shop.Chamber, Has.Count.EqualTo(shop.MaxDoses));
            Assert.That(shop.Held, Is.EqualTo(new[] { "flow", "bloom" }), "the rest stays in the siphon, in its order");
            Assert.That(shop.FillChamber().Failure, Is.EqualTo(GrowthFailure.ChamberFull));
            shop.StartIncubation();
            Assert.That(shop.FillChamber().Failure, Is.EqualTo(GrowthFailure.IncubatorBusy));
        }

        [Test]
        public void AFullChamberAndAFullSiphonAreNoDeadEnd()
        {
            var shop = WithSiphon("bloom", "bloom", "bloom");
            shop.FillChamber();
            shop.Held.AddRange(Enumerable.Repeat("flow", shop.SiphonCapacity));
            Assert.That(shop.ReturnDose("bloom").Failure, Is.EqualTo(GrowthFailure.SiphonFull), "they cannot go back");
            Assert.That(shop.PourOutChamber().Detail, Is.EqualTo(shop.MaxDoses.ToString()));
            Assert.That(shop.Chamber, Is.Empty);
            Assert.That(shop.Held, Has.Count.EqualTo(shop.SiphonCapacity), "the siphon is untouched");
            Assert.That(shop.PourOutChamber().Failure, Is.EqualTo(GrowthFailure.ChamberEmpty));
        }

        [Test]
        public void TheChamberIsNotPouredOutWhileBusy()
        {
            var shop = WithSiphon("bloom", "flow");
            shop.FillChamber(); shop.StartIncubation();
            Assert.That(shop.PourOutChamber().Failure, Is.EqualTo(GrowthFailure.IncubatorBusy));
        }

        [Test]
        public void TheSiphonPoursOutOneCultureOrEveryDose()
        {
            var shop = WithSiphon("bloom", "flow", "bloom");
            Assert.That(shop.PourOutSiphon("bloom").Detail, Is.EqualTo("2"));
            Assert.That(shop.Held, Is.EqualTo(new[] { "flow" }));
            Assert.That(shop.PourOutSiphon("bloom").Failure, Is.EqualTo(GrowthFailure.NoDose));
            Assert.That(shop.PourOutSiphon().Detail, Is.EqualTo("1"));
            Assert.That(shop.Held, Is.Empty);
            Assert.That(shop.PourOutSiphon().Failure, Is.EqualTo(GrowthFailure.NoDose));
        }

        [Test]
        public void ABufferPoursOutAndAPipedOneStaysForItsCulture()
        {
            var shop = GrowthStarts.Make(GrowthStarts.Era4);
            var piped = shop.Buffers.First(b => shop.InletOf(b.Place) != null);
            piped.Units = 5;
            string culture = piped.Culture;
            Assert.That(shop.PourOutBuffer(piped.Place).Detail, Is.EqualTo("5"));
            Assert.That(piped.Units, Is.EqualTo(0));
            Assert.That(piped.Culture, Is.EqualTo(culture), "its line still brings that culture");
            Assert.That(shop.PourOutBuffer(piped.Place).Failure, Is.EqualTo(GrowthFailure.GrowerEmpty));
            var loose = new GrowthBuffer { Place = 5, Culture = "bloom", Units = 3 };
            shop.Buffers.Add(loose);
            Assert.That(shop.PourOutBuffer(5).Success, Is.True);
            Assert.That(loose.Culture, Is.Null, "with no line, it is free for any culture");
        }

        static GrowthShop FullTankFullHands(out GrowthContainer tank)
        {
            var shop = new GrowthShop();
            tank = shop.Containers[0];
            while (tank.Free > 0) Assert.That(shop.DevelopmentStock(tank.Id, "pondlet").Success, Is.True);
            for (int i = 0; i < GrowthShop.CarryLimit; i++) Assert.That(shop.DevelopmentGive("bubblebelly").Success, Is.True);
            return shop;
        }

        [Test]
        public void AFullTankAndFullHandsCanSwap()
        {
            var shop = FullTankFullHands(out var tank);
            Assert.That(shop.PutIn(tank.Id).Failure, Is.EqualTo(GrowthFailure.DoesNotFit), "no room to put one in");
            var take = tank.Occupants[0]; var put = shop.Carried[1];
            int inTank = tank.Occupants.Count;
            Assert.That(shop.Swap(tank.Id, take.Id, put.Id).Success, Is.True);
            Assert.That(tank.Occupants.Contains(put) && !tank.Occupants.Contains(take), Is.True);
            Assert.That(shop.Carried.Contains(take) && !shop.Carried.Contains(put), Is.True);
            Assert.That(tank.Occupants.Count, Is.EqualTo(inTank));
            Assert.That(shop.Carried.Count, Is.EqualTo(GrowthShop.CarryLimit));
        }

        [Test]
        public void ASwapNeedsTheCreatureToFitInThePlaceLeft()
        {
            var shop = FullTankFullHands(out var tank);
            shop.Carried.Clear();
            Assert.That(shop.DevelopmentGive("shellback").Success, Is.True, "a large one");
            var big = shop.Carried[0];
            Assert.That(shop.SwapCheck(tank.Id, tank.Occupants[0].Id, big.Id, out string why), Is.EqualTo(GrowthFailure.DoesNotFit));
            Assert.That(why, Does.StartWith("Too big"));
            var before = tank.Occupants.ToList();
            Assert.That(shop.Swap(tank.Id, tank.Occupants[0].Id, big.Id).Success, Is.False);
            Assert.That(tank.Occupants, Is.EqualTo(before), "nothing moved");
        }

        [Test]
        public void ASwapLeavesAReservedCreatureInItsTank()
        {
            var shop = FullTankFullHands(out var tank);
            tank.Occupants[0].ReservedBy = 7;
            Assert.That(shop.SwapCheck(tank.Id, tank.Occupants[0].Id, shop.Carried[0].Id, out _), Is.EqualTo(GrowthFailure.Reserved));
            Assert.That(shop.Swap(tank.Id, tank.Occupants[0].Id, shop.Carried[0].Id).Failure, Is.EqualTo(GrowthFailure.Reserved));
            tank.Occupants[0].ReservedBy = 0;
            Assert.That(shop.Swap(tank.Id, tank.Occupants[0].Id, 987654).Failure, Is.EqualTo(GrowthFailure.NoSuchSpecimen), "not in hand");
        }

        [Test]
        public void ThePutInCreatureIsTheOneChosen()
        {
            var shop = new GrowthShop();
            shop.DevelopmentGive("pondlet"); shop.DevelopmentGive("bubblebelly"); shop.DevelopmentGive("ribbonfin");
            var tank = shop.Containers[0];
            var chosen = shop.Carried[2];
            Assert.That(shop.PutIn(tank.Id, chosen.Id).Success, Is.True);
            Assert.That(tank.Occupants, Is.EqualTo(new[] { chosen }));
            Assert.That(shop.Carried.Select(c => c.Species), Is.EqualTo(new[] { "pondlet", "bubblebelly" }));
        }

        [Test]
        public void TheTakeButtonHasItsOwnUsualInput()
        {
            var settings = new GrowthSettings();
            Assert.That(settings.InputOf(GrowthAction.Take), Is.EqualTo("Mouse/Right"));
            Assert.That(settings.InputOf(GrowthAction.Use), Is.EqualTo("Mouse/Left"));
            Assert.That(settings.InputOf(GrowthAction.Interact), Is.EqualTo("Key/E"));
            var older = new GrowthSettings(); older.bindings.RemoveAll(b => b.action == "Take"); older.Sanitize();
            Assert.That(older.InputOf(GrowthAction.Take), Is.EqualTo("Mouse/Right"), "a settings file from before it gets it");
            var slapOnRight = new GrowthSettings(); slapOnRight.bindings.RemoveAll(b => b.action == "Take");
            slapOnRight.bindings.First(b => b.action == "Slap").input = "Mouse/Right"; slapOnRight.Sanitize();
            Assert.That(slapOnRight.InputOf(GrowthAction.Slap), Is.EqualTo("Mouse/Right"), "the player's own choice is kept");
            Assert.That(slapOnRight.InputOf(GrowthAction.Take), Is.Not.EqualTo("Mouse/Right"), "and take finds a free one");
        }
    }
}
