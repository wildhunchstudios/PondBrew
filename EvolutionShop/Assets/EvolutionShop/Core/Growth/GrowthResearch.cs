using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionShop.Growth
{
    // GR-30 (tester round 1, F1 to F4, the user's choices in GR.72): the research room, west of the power room, opened at night once
    // the generator stands. The player puts one creature of each kind in a jar, where it stays as a specimen: not for sale and not
    // used up. At the desk they queue pairs and mixes, and three small tanks work through the queue by themselves, each drawing a
    // unit of the generator's power, about 20 s a job against the breeding tank's 40 to 60. A pair costs only power and time; a mix
    // takes its doses as it starts, from the buffers and then the growers, never the siphon in the player's hands. Something new
    // goes in the journal and leaves one real creature on the tray to take; a repeat only says what it makes. The breeding tank
    // stays the way to make stock. Every job, new or not, joins the same pair log and mix log the breeding tank and incubator use,
    // so nothing is tried twice by accident.

    /// <summary>One job in the research room: a pair of species, or a mix of doses, waiting in the queue or under way in a tank.</summary>
    public sealed class GrowthResearchJob
    {
        public string A, B;
        public int[] Doses;
        /// <summary>The tank working on it (0 to 2), or -1 while it waits.</summary>
        public int Tank = -1;
        /// <summary>Seconds of work left; at 0 with its tank still taken, a new creature waits for room on the tray.</summary>
        public float Left;
        public bool IsMix => Doses != null;
        public bool Running => Tank >= 0;
    }

    public sealed partial class GrowthShop
    {
        public const string ResearchPower = "research";
        public const int ResearchTankCount = 3, SpecimenJars = 15, ResearchTrayPlaces = 6, ResearchQueueLimit = 120;
        /// <summary>What each working tank draws from the generator.</summary>
        public const float ResearchTankUnits = 1;

        public static ExpansionDef ResearchDef => GrowthContent.Expansions.First(e => e.Id == "research");

        public bool ResearchRoom;
        /// <summary>The creatures in the jars: one of each kind.</summary>
        public readonly List<GrowthSpecimen> Specimens = new List<GrowthSpecimen>();
        /// <summary>New creatures the room made, waiting to be taken.</summary>
        public readonly List<GrowthSpecimen> ResearchTray = new List<GrowthSpecimen>();
        /// <summary>The queue, oldest first; those with a tank are under way.</summary>
        public readonly List<GrowthResearchJob> ResearchJobs = new List<GrowthResearchJob>();
        /// <summary>How many jobs the room has finished (a lesson is learned by it).</summary>
        public int ResearchDone;

        /// <summary>How long a job takes at the room's level.</summary>
        public float ResearchSeconds => Stat("research", "seconds");
        public IEnumerable<GrowthResearchJob> ResearchRunning => ResearchJobs.Where(j => j.Running);
        public IEnumerable<GrowthResearchJob> ResearchWaiting => ResearchJobs.Where(j => !j.Running);
        public GrowthResearchJob ResearchInTank(int tank) => ResearchJobs.FirstOrDefault(j => j.Tank == tank);
        public GrowthSpecimen SpecimenOf(string species) => Specimens.FirstOrDefault(x => x.Species == species);

        /// <summary>F5: how to make a creature, in words: a founder's recipe, or a pair that has made it.</summary>
        public string HowToMake(SpeciesDef def)
        {
            if (def.Recipe != null) return GrowthReport.Recipe(def.Recipe);
            var bred = PairLog.FirstOrDefault(p => p.Species == def.Id);
            if (bred != null) return "bred from " + GrowthContent.Get(bred.A).Name + " × " + GrowthContent.Get(bred.B).Name;
            return "bred";
        }

        // ---------------------------------------------------------------- the room

        /// <summary>Why the research room cannot open now (None when it can), with a sentence.</summary>
        public GrowthFailure ResearchCheck(out string why)
        {
            var room = ResearchDef;
            why = null;
            if (ResearchRoom) { why = "Already open"; return GrowthFailure.MaxLevel; }
            if (!Annex) { why = "Open the power room first"; return GrowthFailure.Locked; }
            if (!HasGenerator) { why = "Buy the generator first"; return GrowthFailure.Locked; }
            if (Era < room.Era) { why = "Needs Era " + room.Era; return GrowthFailure.Locked; }
            if (Stars < room.Stars) { why = "Needs " + room.Stars + " stars"; return GrowthFailure.Locked; }
            if (Credits < room.Credits) { why = "Needs " + room.Credits + " credits"; return GrowthFailure.NotEnoughCredits; }
            return GrowthFailure.None;
        }

        /// <summary>Opens the power room's west wall into the research room, its tanks on the power board.</summary>
        public GrowthResult BuyResearchRoom()
        {
            var failure = ResearchCheck(out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            Credits -= ResearchDef.Credits;
            ResearchRoom = true;
            Levels["research"] = 1;
            if (!PowerOrder.Contains(ResearchPower)) PowerOrder.Add(ResearchPower);
            Raise(GrowthEventKind.Opened, ResearchDef.Name + " opened", amount: ResearchDef.Credits);
            return GrowthResult.Ok();
        }

        // ---------------------------------------------------------------- the jars

        /// <summary>Why a creature cannot go in a jar now (None when it can), with a sentence.</summary>
        public GrowthFailure SpecimenCheck(GrowthSpecimen specimen, out string why)
        {
            why = null;
            if (!ResearchRoom) { why = "Open the research room first"; return GrowthFailure.Locked; }
            if (SpecimenOf(specimen.Species) != null) { why = "One of each kind is enough: a " + specimen.Def.Name + " is already in a jar"; return GrowthFailure.Owned; }
            if (Specimens.Count >= SpecimenJars) { why = "Every jar is taken: take a creature back first"; return GrowthFailure.Busy; }
            return GrowthFailure.None;
        }

        /// <summary>Puts a carried creature in a jar (the one asked for, or the first that may go). It stays there: not for sale, not used up.</summary>
        public GrowthResult PutSpecimen(int specimenId = 0)
        {
            if (Carried.Count == 0) return GrowthResult.Fail(GrowthFailure.HandsEmpty);
            var candidates = specimenId == 0 ? Carried.ToList() : Carried.Where(x => x.Id == specimenId).ToList();
            if (candidates.Count == 0) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            GrowthSpecimen specimen = null; string why = null; var failure = GrowthFailure.None;
            foreach (var x in candidates) { failure = SpecimenCheck(x, out why); if (failure == GrowthFailure.None) { specimen = x; break; } }
            if (specimen == null) return GrowthResult.Fail(failure, why);
            Carried.Remove(specimen);
            Specimens.Add(specimen);
            return GrowthResult.Ok(specimen.Species);
        }

        /// <summary>
        /// Takes a creature back out of its jar. Pairs waiting for it leave the queue, as there is no creature to pair; a pair already
        /// in a tank carries on, as the tank has what it needs.
        /// </summary>
        public GrowthResult TakeSpecimen(int specimenId)
        {
            var specimen = Specimens.FirstOrDefault(x => x.Id == specimenId);
            if (specimen == null) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            Specimens.Remove(specimen);
            Carried.Add(specimen);
            ResearchJobs.RemoveAll(j => !j.IsMix && !j.Running && (j.A == specimen.Species || j.B == specimen.Species));
            return GrowthResult.Ok(specimen.Species);
        }

        // ---------------------------------------------------------------- the tray

        /// <summary>Takes a new creature off the tray (the oldest, or the one asked for).</summary>
        public GrowthResult TakeResearchResult(int specimenId = 0)
        {
            var result = specimenId == 0 ? ResearchTray.FirstOrDefault() : ResearchTray.FirstOrDefault(x => x.Id == specimenId);
            if (result == null) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen, "Nothing on the tray");
            if (Carried.Count >= CarryLimit) return GrowthResult.Fail(GrowthFailure.HandsFull, "carry up to " + CarryLimit);
            ResearchTray.Remove(result);
            Carried.Add(result);
            return GrowthResult.Ok(result.Species);
        }

        // ---------------------------------------------------------------- the queue

        /// <summary>The job queued or under way for a pair (either order), or null.</summary>
        public GrowthResearchJob QueuedPair(string a, string b)
        {
            var (x, y) = Ordered(a, b);
            return ResearchJobs.FirstOrDefault(j => !j.IsMix && j.A == x && j.B == y);
        }

        public GrowthResearchJob QueuedMix(int[] doses) => ResearchJobs.FirstOrDefault(j => j.IsMix && j.Doses.SequenceEqual(doses));

        /// <summary>The mix log's entry for these doses, or null when the mix was never tried.</summary>
        public GrowthMixEntry MixEntry(int[] doses) => MixLog.FirstOrDefault(e => e.Doses.SequenceEqual(doses));

        /// <summary>F3: with the microscope, whether a pair would make something new.</summary>
        public bool PairLooksNew(string a, string b) => Microscope && !Discovered.Contains(GrowthRules.Breed(a, b).Id);

        /// <summary>Why a pair cannot join the queue now (None when it can), with a sentence.</summary>
        public GrowthFailure PairCheck(string a, string b, out string why)
        {
            why = null;
            if (!ResearchRoom) { why = "Open the research room first"; return GrowthFailure.Locked; }
            if (a == b) { why = "Two of one kind only make more of it"; return GrowthFailure.SameWater; }
            if (SpecimenOf(a) == null || SpecimenOf(b) == null) { why = "Both need to be in a jar"; return GrowthFailure.NoSuchSpecimen; }
            var tried = PairEntry(a, b);
            if (tried != null) { why = "Already tried: makes " + GrowthContent.Get(tried.Species).Name; return GrowthFailure.AlreadyTried; }
            if (QueuedPair(a, b) != null) { why = "Already in the queue"; return GrowthFailure.Busy; }
            if (ResearchJobs.Count >= ResearchQueueLimit) { why = "The queue is full"; return GrowthFailure.Busy; }
            return GrowthFailure.None;
        }

        public GrowthResult QueuePair(string a, string b)
        {
            var failure = PairCheck(a, b, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            var (x, y) = Ordered(a, b);
            ResearchJobs.Add(new GrowthResearchJob { A = x, B = y, Left = ResearchSeconds });
            return GrowthResult.Ok();
        }

        /// <summary>The pairs in the jars not tried or queued yet, with the microscope those that look new first.</summary>
        public List<(string a, string b)> PairsLeft()
        {
            var pairs = new List<(string a, string b)>();
            for (int i = 0; i < Specimens.Count; i++)
                for (int j = i + 1; j < Specimens.Count; j++)
                    if (PairCheck(Specimens[i].Species, Specimens[j].Species, out _) == GrowthFailure.None) pairs.Add((Specimens[i].Species, Specimens[j].Species));
            return pairs.OrderByDescending(p => PairLooksNew(p.a, p.b)).ToList();
        }

        /// <summary>Queues every pair left; how many, in the result's detail.</summary>
        public GrowthResult QueueAllPairs()
        {
            if (!ResearchRoom) return GrowthResult.Fail(GrowthFailure.Locked, "Open the research room first");
            var pairs = PairsLeft();
            if (pairs.Count == 0) return GrowthResult.Fail(GrowthFailure.AlreadyTried, Specimens.Count < 2 ? "Put two creatures in the jars first" : "Every pair in the jars is tried or queued");
            int queued = 0;
            foreach (var (a, b) in pairs) if (QueuePair(a, b).Success) queued++;
            return GrowthResult.Ok(queued.ToString());
        }

        /// <summary>Why a mix cannot join the queue now (None when it can), with a sentence.</summary>
        public GrowthFailure MixCheck(int[] doses, out string why)
        {
            why = null;
            if (!ResearchRoom) { why = "Open the research room first"; return GrowthFailure.Locked; }
            if (doses == null || doses.Length != GrowthContent.Cultures.Length || doses.Any(d => d < 0) || doses.Sum() == 0) { why = "Add a dose first"; return GrowthFailure.ChamberEmpty; }
            if (doses.Sum() > MaxDoses) { why = "up to " + MaxDoses + " doses"; return GrowthFailure.ChamberFull; }
            for (int i = 0; i < doses.Length; i++)
                if (doses[i] > 0 && !Owns(GrowthContent.Cultures[i].Id)) { why = "You have no " + GrowthContent.Cultures[i].Name + " of your own"; return GrowthFailure.NotOwned; }
            var tried = MixEntry(doses);
            if (tried != null) { why = "Already tried: makes " + GrowthContent.Get(tried.Species).Name; return GrowthFailure.AlreadyTried; }
            if (QueuedMix(doses) != null) { why = "Already in the queue"; return GrowthFailure.Busy; }
            if (ResearchJobs.Count >= ResearchQueueLimit) { why = "The queue is full"; return GrowthFailure.Busy; }
            return GrowthFailure.None;
        }

        public GrowthResult QueueMix(int[] doses)
        {
            var failure = MixCheck(doses, out string why);
            if (failure != GrowthFailure.None) return GrowthResult.Fail(failure, why);
            ResearchJobs.Add(new GrowthResearchJob { Doses = (int[])doses.Clone(), Left = ResearchSeconds });
            return GrowthResult.Ok();
        }

        /// <summary>Takes a waiting job out of the queue. One in a tank carries on: it finishes in seconds, and a mix's doses have gone in.</summary>
        public GrowthResult Unqueue(GrowthResearchJob job)
        {
            if (job == null || !ResearchJobs.Contains(job)) return GrowthResult.Fail(GrowthFailure.NoSuchSpecimen);
            if (job.Running) return GrowthResult.Fail(GrowthFailure.Busy, "It is in a tank: it finishes by itself");
            ResearchJobs.Remove(job);
            return GrowthResult.Ok();
        }

        // ---------------------------------------------------------------- the tanks

        /// <summary>How many doses of a culture the room can draw now: the buffers' and the grower's, never the siphon in the player's hands.</summary>
        public int DosesForResearch(string culture) =>
            Buffers.Where(b => b.Culture == culture).Sum(b => (int)Math.Floor(b.Units + 0.001f)) + (GrowerUnits.TryGetValue(culture, out float u) ? (int)Math.Floor(u + 0.001f) : 0);

        /// <summary>Whether the doses for a mix are there now.</summary>
        public bool MixHasDoses(int[] doses)
        {
            for (int i = 0; i < doses.Length; i++)
                if (doses[i] > 0 && DosesForResearch(GrowthContent.Cultures[i].Id) < doses[i]) return false;
            return true;
        }

        void TakeResearchDoses(int[] doses)
        {
            for (int i = 0; i < doses.Length; i++)
            {
                string culture = GrowthContent.Cultures[i].Id;
                int need = doses[i];
                foreach (var b in Buffers.Where(x => x.Culture == culture).OrderByDescending(x => x.Units).ThenBy(x => x.Place))
                {
                    int take = Math.Min(need, (int)Math.Floor(b.Units + 0.001f));
                    b.Units = Math.Max(0, b.Units - take);
                    ForgetIfDry(b.Place);
                    need -= take;
                }
                if (need > 0) GrowerUnits[culture] = Math.Max(0, GrowerUnits[culture] - need);
            }
        }

        /// <summary>
        /// Before the power is shared out: what is finished comes out, and each free tank takes the oldest job it can. A mix without
        /// its doses lets the next job go first, so one empty grower never holds the whole queue up.
        /// </summary>
        void TickResearchTanks()
        {
            if (!ResearchRoom) return;
            FinishResearch();
            for (int tank = 0; tank < ResearchTankCount; tank++)
            {
                if (ResearchInTank(tank) != null) continue;
                var job = ResearchWaiting.FirstOrDefault(j => !j.IsMix || MixHasDoses(j.Doses));
                if (job == null) break;
                if (job.IsMix) TakeResearchDoses(job.Doses);
                job.Tank = tank;
                job.Left = ResearchSeconds;
            }
        }

        /// <summary>What the room draws now: a unit for each tank at work.</summary>
        float ResearchDraw => ResearchRoom ? ResearchRunning.Count(j => j.Left > 0) * ResearchTankUnits : 0;

        /// <summary>The tanks' work for this tick, at the share of power the room got.</summary>
        void WorkResearch(float seconds)
        {
            foreach (var job in ResearchRunning) job.Left = Math.Max(0, job.Left - seconds);
            FinishResearch();
        }

        void FinishResearch()
        {
            foreach (var job in ResearchRunning.Where(j => j.Left <= 0).ToList()) if (job.IsMix ? FinishMix(job) : FinishPair(job)) { ResearchJobs.Remove(job); ResearchDone++; }
        }

        /// <summary>Something new goes in the journal and onto the tray; while the tray is full it stays in its tank. False while it waits.</summary>
        bool ResearchFound(string species)
        {
            if (ResearchTray.Count >= ResearchTrayPlaces) return false;
            var creature = new GrowthSpecimen { Id = nextId++, Species = species, Colour = Colour.Classic };
            ResearchTray.Add(creature);
            Record(creature);
            return true;
        }

        bool FinishPair(GrowthResearchJob job)
        {
            var child = GrowthRules.Breed(job.A, job.B);
            bool isNew = !Discovered.Contains(child.Id);
            if (isNew && !ResearchFound(child.Id)) return false;
            var entry = PairEntry(job.A, job.B);
            if (entry != null) entry.Count++;
            else PairLog.Add(new GrowthPairEntry { A = job.A, B = job.B, Species = child.Id, WasNew = isNew });
            if (!isNew) Raise(GrowthEventKind.Researched, GrowthContent.Get(job.A).Name + " and " + GrowthContent.Get(job.B).Name + " make " + child.Name, child.Id);
            return true;
        }

        bool FinishMix(GrowthResearchJob job)
        {
            var mix = GrowthRules.Mix(job.Doses, Cultures, Discovered);
            if (mix.Species == null) return true;
            bool isNew = !Discovered.Contains(mix.Species.Id);
            if (isNew && !ResearchFound(mix.Species.Id)) return false;
            var entry = MixEntry(job.Doses);
            if (entry != null) entry.Count++;
            else MixLog.Add(new GrowthMixEntry { Doses = job.Doses, Species = mix.Species.Id, Exact = mix.Exact, WasNew = isNew, DistanceToNew = mix.DistanceToNew });
            if (!isNew) Raise(GrowthEventKind.Researched, GrowthReport.Recipe(job.Doses) + " makes " + mix.Species.Name, mix.Species.Id);
            return true;
        }

        /// <summary>What the room is doing, in a sentence (the power board).</summary>
        public string ResearchStatus
        {
            get
            {
                int working = ResearchRunning.Count(j => j.Left > 0), waiting = ResearchWaiting.Count();
                if (working == 0)
                {
                    if (ResearchRunning.Any()) return "Waiting for room on the tray";
                    return waiting > 0 ? "Waiting for doses" : "Nothing queued";
                }
                return PowerStatus(PowerUse(ResearchPower), working + (working == 1 ? " tank" : " tanks") + " at work" + (waiting > 0 ? ", " + waiting + " waiting" : ""));
            }
        }

        /// <summary>A tank's line for the desk: what it holds and how long it has left.</summary>
        public string ResearchTankLine(int tank)
        {
            var job = ResearchInTank(tank);
            if (job == null) return "Tank " + (tank + 1) + ": free";
            string what = job.IsMix ? GrowthReport.Recipe(job.Doses) : GrowthContent.Get(job.A).Name + " × " + GrowthContent.Get(job.B).Name;
            return "Tank " + (tank + 1) + ": " + what + (job.Left > 0 ? " · " + Math.Max(1, (int)Math.Ceiling(job.Left)) + " s" : " · waiting for room on the tray");
        }

        // ---------------------------------------------------------------- saving (version 15)

        void SaveResearch(GrowthSave s)
        {
            s.researchRoom = ResearchRoom;
            s.researchDone = ResearchDone;
            s.specimens = Specimens.Select(GrowthSave.Specimen.Of).ToArray();
            s.researchTray = ResearchTray.Select(GrowthSave.Specimen.Of).ToArray();
            s.researchJobs = ResearchJobs.Select(j => new GrowthSave.ResearchJob { a = j.A ?? "", b = j.B ?? "", mix = j.IsMix, doses = j.Doses, tank = j.Tank, left = j.Left }).ToArray();
        }

        /// <summary>Restores the room, its jars, tray and queue; a reason when they are not consistent. The room itself comes back with the power (its power use is on the board).</summary>
        static string RestoreResearch(GrowthShop shop, GrowthSave s)
        {
            shop.ResearchDone = Math.Max(0, s.researchDone);
            bool any = shop.Level("research") > 0 || (s.specimens?.Length ?? 0) > 0 || (s.researchTray?.Length ?? 0) > 0 || (s.researchJobs?.Length ?? 0) > 0;
            if (any && !shop.ResearchRoom) return "Research without the research room";
            if (shop.ResearchRoom && (!shop.Annex || shop.Level("research") < 1)) return "A research room without its place";
            foreach (var x in s.specimens ?? new GrowthSave.Specimen[0])
            {
                if (GrowthContent.Get(x.species) == null) return "An unknown creature in a jar";
                if (shop.SpecimenOf(x.species) != null) return "Two jars of one kind";
                shop.Specimens.Add(x.ToSpecimen());
            }
            if (shop.Specimens.Count > SpecimenJars) return "More creatures than jars";
            foreach (var x in s.researchTray ?? new GrowthSave.Specimen[0])
            {
                if (GrowthContent.Get(x.species) == null) return "An unknown creature on the tray";
                shop.ResearchTray.Add(x.ToSpecimen());
            }
            if (shop.ResearchTray.Count > ResearchTrayPlaces) return "More creatures than the tray holds";
            foreach (var j in s.researchJobs ?? new GrowthSave.ResearchJob[0])
            {
                if (j.tank < -1 || j.tank >= ResearchTankCount || j.tank >= 0 && shop.ResearchInTank(j.tank) != null) return "Two jobs in one research tank";
                if (j.left < 0 || j.left > 3600) return "A research job's time that cannot be";
                if (j.mix)
                {
                    if (j.doses == null || j.doses.Length != GrowthContent.Cultures.Length || j.doses.Any(d => d < 0) || j.doses.Sum() == 0) return "A research mix that cannot be";
                    shop.ResearchJobs.Add(new GrowthResearchJob { Doses = j.doses, Tank = j.tank, Left = j.left });
                }
                else
                {
                    if (GrowthContent.Get(j.a) == null || GrowthContent.Get(j.b) == null || j.a == j.b) return "A research pair that cannot be";
                    if (j.tank < 0 && (shop.SpecimenOf(j.a) == null || shop.SpecimenOf(j.b) == null)) return "A queued pair without its jars";
                    shop.ResearchJobs.Add(new GrowthResearchJob { A = j.a, B = j.b, Tank = j.tank, Left = j.left });
                }
            }
            if (shop.ResearchJobs.Count > ResearchQueueLimit) return "More jobs than the queue holds";
            return null;
        }
    }
}
