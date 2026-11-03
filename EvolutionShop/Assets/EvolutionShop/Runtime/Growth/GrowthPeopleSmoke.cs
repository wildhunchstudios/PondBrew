#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    /// <summary>
    /// GR-18's Windows route (-evolutionGrowthPeopleSmoke): wave 1 of the people (GR.56). Visitors of every kind come into
    /// shop unit A as people from the people kit, each with a look of their own; they walk in their kind's way, browse a
    /// tank (leaning in, pointing), queue at the counter (the front one holding out the coins, the others waiting, a tapping
    /// foot and an "o" as patience runs low), and leave with the creature they bought in the shop's bag and a grin, with a
    /// shrug when nothing was affordable, with a huff when tired of waiting, and with a wave at closing. A lineup of every
    /// kind by night and by day, a visitor of the shop's own, and the frame budget with a crowd of people in view. Wave 2
    /// (GR.57): the helpers as the board's cast at work: Pip behind the counter, Nori carrying a clutch to the shop's tank in
    /// the jar and reaching in at the tank, Moss bringing doses to the incubator in the pipette in each culture's colour and
    /// squeezing them in, and Pip bagging an order at the till with a wave to the visitor. Wave 3: the player's hands in first
    /// person, in the shop's yellow gloves: the siphon squeezed at a grower and banded with each dose, both hands on the crank,
    /// a pipe across them, a reach into a tank in peek for a creature, three bags carried and bobbing as the player walks, a
    /// reach to put one in another tank, lowered while a card is open, a bag held out over the counter and handed over, and
    /// what they cost. Wave 4 (§6.7): the silly moments: the room's own suspicious fart (the culprit's innocent look and its
    /// cloud, the neighbour blaming the fish), the slap on its own key with its prompt (a huff: the hand mark, squeezed eyes,
    /// dizzy stars, a grumble and a stagger on the way out; a casual visitor laughing a first one off; the hand still
    /// stinging), Skip, the ridiculous complaint, the fish copying a visitor's fish face inside its tank, the incubator's burp
    /// after the hatchling shows, Pip's little dance after a sale and its end the moment a visitor queues, a slap at the
    /// visitor Pip is serving, the Gags and Humour settings, a pause, closing time and a reload in the middle of a moment,
    /// and a new slap key from the pause menu.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string PeopleScenarioText = "GR-18 wave 1, the visitors and guests as people: the people kit in the look's own materials; visitors of every kind walking in their kind's way, browsing (leaning in, pointing), queueing (paying at the front, waiting and growing impatient behind), and leaving happy with the creature they bought in the shop's bag, with a shrug, a huff or a wave at closing; a lineup of every kind and of casual visitors' variety by night and by day; a visitor of the shop's own; the frame budget with a crowd in view; wave 2, the helpers as people at work: Pip at the counter and the till, Nori carrying in the jar and reaching in at the tank, Moss with the pipette in each dose's colour; wave 3, the player's hands in each thing the player does: siphoning, cranking, laying a pipe, taking from a tank in peek, carrying three bags, walking, putting one in, a card, serving at the counter; wave 4, the silly moments: the fart, the slap and its laugh, Skip, the complaint, the fish copying a face, the burp, Pip's dance, and how they end and wait; captures of each";

        static readonly string[] PeopleKinds = { "casual", "bargain", "collector", "premium", "guest" };

        /// <summary>The lowest point any shown part of a person reaches (their soles, standing).</summary>
        static float FeetY(GrowthPerson p) => p.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).Min(r => r.bounds.min.y);

        /// <summary>A person's look as the kit says (every slot in the look's materials, none left magenta) and inside the budget.</summary>
        void PersonChecks(GrowthPerson p, string kind, string what)
        {
            Check(p != null && p.Look.Kind == kind, what + " is a person of the kit's " + kind + " kind");
            var mats = p.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToList();
            // The look's glass (lenses, a bag's water) is named "Diorama / GR people glass ...".
            var odd = mats.Where(m => m == null || !m.name.Contains("GR people ") || m.name.StartsWith("PK_") || m.name.Contains("#FF00FF")).Select(m => m == null ? "(none)" : m.name).Distinct().ToList();
            Check(mats.Count > 0 && odd.Count == 0, what + " wears the look's own materials (" + mats.Select(m => m.name).Distinct().Count() + " of them), none left unmapped" + (odd.Count > 0 ? " (unmapped: " + string.Join(", ", odd) + ")" : ""));
            Check(p.Triangles > 10000 && p.Triangles < 90000 && p.Renderers < 70, what + ": " + p.Triangles + " triangles in " + p.Renderers + " parts, inside the person budget (90,000 and 70)");
        }

        /// <summary>A visitor made for the route (labelled), browsing one container until the route lets them decide.</summary>
        GrowthVisitor Visitor(int id, string type, int budget, GrowthContainer at)
        {
            var v = new GrowthVisitor { Id = id, Type = type, Budget = budget, State = GrowthVisitorState.Browsing, BrowseFor = 999 };
            v.Plan.Add(at.Id);
            v.ContainerId = at.Id;
            s.Shop.Visitors.Add(v);
            return v;
        }

        GrowthVisitorView ViewOf(GrowthVisitor v) => s.World.VisitorViews.TryGetValue(v.Id, out var view) ? view : null;

        /// <summary>A view of a person from part-way round to their right (or left), at a distance: half-way round for someone walking,
        /// from the side for someone at a tank (in front of them is the tank, and its collider would push the view away).</summary>
        void FaceOf(Transform t, float distance = 1.25f, float side = 1, float round = 0.7f)
        {
            Vector3 p = t.position, f = t.forward, r = t.right * side; f.y = 0; r.y = 0;
            LookAt(p + f.normalized * distance * round + r.normalized * distance * Mathf.Sqrt(1 - round * round), p + Vector3.up * 1.25f);
        }

        /// <summary>A row of people built straight from the kit for the captures (view-only, labelled), facing the door.</summary>
        Transform Lineup(string name, IEnumerable<(string kind, int seed)> who, float z)
        {
            var root = new GameObject("GR-18 " + name).transform;
            root.SetParent(s.World.transform, false);
            var list = who.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var holder = new GameObject(list[i].kind + " " + list[i].seed).transform;
                holder.SetParent(root, false);
                holder.SetPositionAndRotation(P(-0.05f, 0, z) + new Vector3(0.65f * i, 0, 0), Quaternion.Euler(0, 180, 0));
                var p = GrowthPerson.Build(GrowthPersonLook.For(list[i].kind, list[i].seed), holder);
                var pose = new GrowthPersonPose();
                GrowthPeopleMotion.Stand(pose, i * 0.7f, 0.2f);
                GrowthPeopleMotion.HoldArms(pose, list[i].kind, p.Look.HeldLeft, p.Look.HeldRight);
                p.Apply(pose);
            }
            return root;
        }

        IEnumerator PeopleScenario()
        {
            limitations.Add("GR-18 people route: the fixture is labelled: an era 2, three-star shop with 2,000 credits, Starter tanks on a1 to a4 with Pondlets, Bubblebellies and Ribbonfins, and the counter at level 3 (a queue of three). Four visitors (a casual visitor, a bargain hunter, a collector and a big spender) are put into the shop's own list of visitors by the route, one every 1.2 s from the opening (labelled), each browsing one tank until the route sets their browse clock to decide; from there the rules decide, queue, serve (the shop's own Serve, as the held mouse calls it), refuse and close: the collector's patience and the big spender's budget are set to run out (labelled), the day is ended by setting its clock (labelled) and the receipt it opens is closed by the route, so the rules' clock runs on while the last visitor (the bargain hunter) walks out. The lineups are people built straight from the kit and posed standing (view-only, labelled). Frame times are measured uncapped (vSync off) for 6 s after 1 s settling with the lineup and the day's visitors in view. Joint movement is checked as angles between frames, not by eye; how the people look is judged from the captures.");
            yield return Opening();
            TakeInput();
            var w = s.World;
            Check(GrowthWorld.LookOn && GrowthPeopleKit.Available, "The new game wears its look and the people kit is there (" + GrowthPeopleKit.Data.kinds.Length + " kinds)");

            var fx = new GrowthShop { Era = 2, Reputation = GrowthContent.StarThresholds[2] + 50, Credits = 2000, Rp = 100 };
            fx.Discovered.AddRange(new[] { "pondlet", "bubblebelly", "ribbonfin" });
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Levels["counter"] = 3;
            var tanks = new List<GrowthContainer> { fx.Containers[0] };
            foreach (var place in new[] { "a2", "a3", "a4" })
            {
                var c = new GrowthContainer { Id = "tank-people-" + place, Kind = "starter", Place = place };
                fx.Containers.Add(c);
                tanks.Add(c);
            }
            foreach (var c in tanks) { Stock(fx, c, "pondlet"); Stock(fx, c, "bubblebelly"); Stock(fx, c, "ribbonfin"); }
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null; yield return null;
            Check(shop.Phase == GrowthPhase.Night && shop.Stars == 3 && shop.QueueLength == 3 && shop.ShopContainers.Count() == 4,
                "Fixture: a three-star shop at night, four stocked Starter tanks in unit A, a queue of three at the counter");

            // By night: a lineup of every kind, then the casual visitors' variety.
            var everyone = Lineup("everyone", PeopleKinds.Select(k => (k, 1)), 1.6f);
            var variety = Lineup("variety", Enumerable.Range(11, 5).Select(i => ("casual", i)), 2.35f);
            yield return null;
            var front = everyone.GetComponentsInChildren<GrowthPerson>();
            for (int i = 0; i < front.Length; i++)
            {
                PersonChecks(front[i], PeopleKinds[i], "The lineup's " + PeopleKinds[i]);
                Check(Mathf.Abs(FeetY(front[i])) < 0.06f, "The lineup's " + PeopleKinds[i] + " stands on the floor (lowest point " + FeetY(front[i]).ToString("0.000") + " m)");
            }
            Check(front[4].Look.Hat == "tophat" && front[1].Look.Hat == "bucket" && front[3].Look.Sunglasses && front[2].Look.Glasses == "square",
                "Each kind wears its mark: the guest's top hat, the bargain hunter's bucket hat, the big spender's sunglasses, the collector's square glasses");
            var casuals = variety.GetComponentsInChildren<GrowthPerson>();
            int looks = casuals.Select(p => p.Look.Hair + p.Look.Colours["TOP"] + p.Look.Colours["SKIN"] + p.Look.Colours["HAIR"] + p.Look.Bottom).Distinct().Count();
            Check(looks == casuals.Length, "Five casual visitors, five looks of their own (hair, top, skin, hair colour, trousers or shorts)");
            LookAt(P(1.25f, 0, -0.6f), P(1.25f, 0.95f, 1.9f)); yield return Shot("500-everyone-night");
            LookAt(P(1.25f, 0, 0.55f), P(1.25f, 1.15f, 1.6f)); yield return Shot("501-faces-night");
            Destroy(variety.gameObject); Destroy(everyone.gameObject);
            yield return null;

            // The day opens; four visitors come in, one after another, and walk to the tanks.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            var types = new[] { ("casual", 45), ("bargain", 20), ("collector", 90), ("premium", 120) };
            var visitors = new List<GrowthVisitor>();
            for (int i = 0; i < 4; i++)
            {
                visitors.Add(Visitor(9001 + i, types[i].Item1, types[i].Item2, tanks[i]));
                if (i == 1)
                {
                    yield return new WaitForSeconds(0.6f);
                    var first = ViewOf(visitors[0]);
                    Check(first != null && first.Person != null && first.Activity == "walk" && first.Walked > 0.5f, "The first visitor walks in as a person (" + (first == null ? "no view" : first.Activity + ", " + first.Walked.ToString("0.0") + " m") + ")");
                    // A stride's swing, sampled every frame for 0.6 s (about one stride at the casual visitor's pace).
                    var hip = first.Person.Joint("hip.L").localRotation; var arm = first.Person.Joint("shoulder.R").localRotation;
                    float hipSwing = 0, armSwing = 0;
                    for (float until = Time.time + 0.6f; Time.time < until;)
                    {
                        yield return null;
                        hipSwing = Mathf.Max(hipSwing, Quaternion.Angle(hip, first.Person.Joint("hip.L").localRotation));
                        armSwing = Mathf.Max(armSwing, Quaternion.Angle(arm, first.Person.Joint("shoulder.R").localRotation));
                    }
                    Check(hipSwing > 15f && armSwing > 10f, "Walking, their legs and arms swing (" + hipSwing.ToString("0") + " degrees at the hip and " + armSwing.ToString("0") + " at the shoulder over 0.6 s)");
                    LookAt(P(1.1f, 0, -0.9f), P(-0.6f, 1.0f, -3.4f)); yield return Shot("502-walking-in");
                }
                else if (i < 3) yield return new WaitForSeconds(1.2f);
            }
            yield return Wait(() => visitors.All(v => ViewOf(v) != null && ViewOf(v).Arrived && ViewOf(v).Activity == "browse"), 30, "the four visitors at their tanks");
            yield return new WaitForSeconds(0.6f); // the last to arrive turns to face their tank
            Check(shop.Visitors.Count == 4, "Only the route's four visitors are in the shop (the rules' arrivals wait while unit A holds its two)");
            for (int i = 0; i < 4; i++)
            {
                var view = ViewOf(visitors[i]); var p = view.Person;
                PersonChecks(p, types[i].Item1, "The " + types[i].Item1 + " visitor");
                string hand = p.Look.HeldRight == null ? "point" : "grip";
                Check(view.Activity == "browse" && p.HandShape("R") == hand && p.MouthShown == p.Look.Mouth,
                    "The " + types[i].Item1 + " visitor browses the tank on " + tanks[i].Place + ": leaning in, the right hand " + (hand == "point" ? "pointing" : "holding what they carry") + ", their own smile (" + p.MouthShown + ")");
                Check(Vector3.Distance(view.transform.position, GrowthWorld.Places[tanks[i].Place].Browse) < 0.05f && Vector3.Angle(view.transform.forward, GrowthWorld.Places[tanks[i].Place].Position - view.transform.position) < 40f,
                    "The " + types[i].Item1 + " visitor stands at the tank's browse spot, facing it");
            }
            {
                var p = ViewOf(visitors[0]).Person; var spine = p.Joint("spine").localRotation;
                yield return new WaitForSeconds(0.8f);
                Check(Quaternion.Angle(spine, p.Joint("spine").localRotation) > 1f, "Browsing, they keep moving (leaning in and out)");
            }
            LookAt(P(1.6f, 0, -2.9f), P(-1.6f, 0.9f, 0.9f)); yield return Shot("503-browsing-unit-a");
            for (int i = 0; i < 4; i++) { FaceOf(ViewOf(visitors[i]).transform, 1.4f, i == 1 ? -1 : 1, 0.2f); yield return Shot("50" + (4 + i) + "-browsing-" + types[i].Item1); }

            // Three of them decide and queue: the front one pays, the others wait.
            for (int i = 0; i < 3; i++) { visitors[i].BrowseFor = 0.2f; visitors[i].Clock = 0.2f; }
            yield return Wait(() => visitors.Take(3).All(v => v.State == GrowthVisitorState.Queued && ViewOf(v).Arrived), 30, "three visitors in the queue");
            yield return new WaitForSeconds(0.6f);
            var queue = shop.Queue.OrderByDescending(v => v.Clock).ThenBy(v => v.Id).ToList();
            var payer = ViewOf(queue[0]);
            Check(payer.Activity == "pay" && payer.Person.HeldShown.Contains("R.coin") && payer.Person.HandShape("R") == "grip",
                "The front of the queue holds out the coins (" + string.Join(", ", payer.Person.HeldShown) + ")");
            Check(ViewOf(queue[1]).Activity == "wait" && ViewOf(queue[2]).Activity == "wait", "The others in the queue wait");
            Check(queue.All(v => Vector3.Angle(ViewOf(v).transform.forward, Vector3.back) < 20f), "The queue faces the counter");
            LookAt(P(0.6f, 0, -2.5f), P(2.6f, 1.0f, -0.9f)); yield return Shot("510-the-queue");

            var tired = queue[2]; var tiredView = ViewOf(tired);
            tired.Patience = GrowthShop.QueuePatience * 0.12f;
            yield return new WaitForSeconds(0.5f);
            Check(tiredView.Activity == "wait" && tiredView.Person.MouthShown == "o", "Patience running low: the last in the queue taps a foot, checks the time, and the mouth falls open");
            LookAt(P(1.4f, 0, -1.3f), tiredView.transform.position + Vector3.up * 1.2f); yield return Shot("511-impatient");
            tired.Patience = 0.05f;
            yield return Wait(() => tiredView.Activity == "leave-huff", 5, "the tired visitor leaving in a huff");
            Check(tired.Outcome == GrowthVisitOutcome.TiredOfWaiting, "Out of patience, they leave in a huff, arms folded (the rules' TiredOfWaiting)");
            yield return new WaitForSeconds(0.5f);
            FaceOf(tiredView.transform, 1.6f); yield return Shot("512-huff");

            // The front one is served at the counter and leaves with the creature in the shop's bag.
            var buyer = queue[0]; var buyerView = ViewOf(buyer);
            string species = shop.Stock.First(x => x.Id == buyer.SpecimenId).Species;
            var sold = shop.Serve(shop.ServeSeconds + 0.01f);
            Check(sold.Success && sold.Detail == "sold", "Served at the counter (the shop's own Serve): " + sold);
            yield return Wait(() => buyerView.Activity == "leave-happy" && buyerView.Carrying != null, 5, "the buyer leaving with the bag");
            Check(buyerView.Carrying == species && buyerView.Person.HeldShown.Contains("R.creature_bag"), "They leave with what they bought, a " + species + ", in the shop's water bag");
            Check(buyerView.Person.MouthShown == "grin", "And a grin");
            yield return new WaitForSeconds(0.6f);
            FaceOf(buyerView.transform, 1.5f); yield return Shot("513-happy-with-the-bag");

            // The big spender finds nothing in budget and shrugs.
            var spender = visitors[3]; var spenderView = ViewOf(spender);
            spender.Budget = 0; spender.BrowseFor = 0.2f; spender.Clock = 0.2f;
            yield return Wait(() => spenderView.Activity == "leave-shrug", 5, "the big spender leaving with a shrug");
            Check(spender.Outcome == GrowthVisitOutcome.NothingAffordable, "Nothing in budget: a shrug and an open mouth on the way out (the rules' NothingAffordable)");
            yield return new WaitForSeconds(0.5f);
            FaceOf(spenderView.transform, 1.5f); yield return Shot("514-shrug");

            // Closing: the last one waves goodbye.
            var last = queue[1]; var lastView = ViewOf(last);
            shop.DaySecondsLeft = 0.05f;
            yield return Wait(() => shop.Phase == GrowthPhase.Night && s.Card == GrowthCard.Receipt, 5, "the day to end and its receipt");
            Check(s.Card == GrowthCard.Receipt, "The day's receipt opens at closing");
            s.SetCard(GrowthCard.None);
            yield return Wait(() => lastView.Activity == "leave-wave", 5, "the last visitor waving at closing");
            Check(last.Outcome == GrowthVisitOutcome.Closed && shop.Phase == GrowthPhase.Night, "At closing the last visitor leaves with a wave (the rules' Closed)");
            yield return new WaitForSeconds(0.4f);
            FaceOf(lastView.transform, 1.6f); yield return Shot("515-wave-at-closing");
            yield return Wait(() => shop.Visitors.Count == 0 && w.VisitorViews.Count == 0, 20, "everyone out of the shop");
            Check(true, "Everyone walks out and is gone");

            // The next day: everyone by day, a visitor of the shop's own, and the frame budget with a crowd in view.
            Ok(s.Act(shop.OpenShop), "Open the shop for the next day");
            everyone = Lineup("everyone", PeopleKinds.Select(k => (k, 2)), 1.6f);
            variety = Lineup("variety", Enumerable.Range(21, 5).Select(i => ("casual", i)), 2.35f);
            yield return new WaitForSeconds(2.5f);
            Check(w.LookKeyName == "morning", "By day, the morning key");
            LookAt(P(1.25f, 0, -0.6f), P(1.25f, 0.95f, 1.9f)); yield return Shot("520-everyone-day");
            LookAt(P(1.25f, 0, 0.55f), P(1.25f, 1.15f, 1.6f)); yield return Shot("521-faces-day");
            yield return Wait(() => shop.Visitors.Count > 0 && w.VisitorViews.Values.Any(v => v.Person != null && v.Activity == "walk"), 20, "a visitor of the shop's own");
            var own = shop.Visitors.First(); var ownView = ViewOf(own);
            PersonChecks(ownView.Person, GrowthPersonLook.KindOf(own.Type), "The day's first visitor (a " + own.Type + " the rules sent)");
            yield return new WaitForSeconds(1.2f);
            LookAt(P(1.1f, 0, -0.9f), ownView.transform.position + Vector3.up * 1.0f); yield return Shot("522-a-visitor-of-the-day");

            int previousRate = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            int people = FindObjectsByType<GrowthPerson>(FindObjectsSortMode.None).Length;
            yield return Measure("530-people-crowd", P(1.25f, 0, -1.9f), P(0.6f, 1.0f, 1.9f), 6);
            Application.targetFrameRate = previousRate; QualitySettings.vSyncCount = previousVSync;
            Check(people >= 10, "The crowd in view: " + people + " people (the lineups and the day's visitors)");
            File.WriteAllText(Path.Combine(evidence, "people-timings.json"), JsonUtility.ToJson(new DensityReport
            {
                note = "Uncapped frame times with " + people + " people in shop unit A by day (" + SystemInfo.graphicsDeviceName + ", " + SystemInfo.processorType + ", " + Screen.width + "x" + Screen.height + ").",
                shopCreatures = s.Shop.Stock.Count(), stressCreatures = 0, views = timings.ToArray(),
            }, true));
            Destroy(variety.gameObject); Destroy(everyone.gameObject);

            yield return PeopleHelpers();
            yield return PeopleHands();
            yield return PeopleComedy();
            Check(frames > 0, "Frame timings recorded (average " + (frameSum / Mathf.Max(1, frames) * 1000f).ToString("0.0") + " ms, worst " + (worstFrame * 1000f).ToString("0") + " ms)");
        }

        /// <summary>GR-18 wave 2: Pip, Nori and Moss as people at work (the GR-09 helpers route's fixture and steps).</summary>
        IEnumerator PeopleHelpers()
        {
            limitations.Add("GR-18 wave 2 (the helpers): the fixture is labelled, as GR-09's helpers route's: an era 4, four-star shop with 8,000 credits, unit B and the power room open, 12 founders known, growers at level 2; a Large tank is bought on a3 with the place card's action and six Ribbonfins put in it (the routes' fixture action); the three helpers are hired with the staff card's action. The rules run the helpers' steps: the route's clock runs them on (accelerated, labelled) up to the moment it looks at, then the shop's own clock runs in real time while it checks and captures.");
            var w = s.World;
            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 8000, Rp = 300, UnitB = true, SalesTotal = 160 };
            fx.Discovered.AddRange(GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).Select(x => x.Id));
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Levels["grower"] = 2; fx.Levels["generator"] = 2; fx.Levels["pump"] = 2; fx.Levels["incubator"] = 3; fx.Levels["breeding"] = 1; fx.Levels["counter"] = 3;
            fx.Annex = true;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Ok(s.Act(() => shop.BuyContainer("large", "a3")), "A Large tank on a3 (the place card's button): Nori can be hired");
            var large = shop.ContainerAt("a3");
            for (int i = 0; i < 6; i++) Ok(shop.DevelopmentStock(large.Id, "ribbonfin"), "Fixture Ribbonfin " + (i + 1));
            foreach (var id in new[] { "pip", "nori", "moss" }) Ok(s.Act(() => shop.Hire(id)), "Hire " + GrowthContent.Helper(id).Name + " (the staff card's button)");
            s.SetCard(GrowthCard.None);
            yield return null; yield return null; yield return null;

            // The three, at work as people of the board's cast.
            foreach (var id in new[] { "pip", "nori", "moss" })
            {
                var p = w.FigurePerson(id);
                PersonChecks(p, id, GrowthContent.Helper(id).Name);
                Check(w.FigureShowing(id) && w.FigureActivity(id) == "stand", GrowthContent.Helper(id).Name + " stands at work, a person of the board's cast (" + string.Join(", ", p.Look.HeadParts().Where(x => x.StartsWith("hair") || x.StartsWith("hat"))) + ")");
            }
            Check(w.FigurePerson("nori").HeldShown.Contains("R.jar") && w.FigurePerson("moss").HeldShown.Contains("R.pipette") && w.FigurePerson("moss").HeldShown.Contains("L.clipboard") && !w.FigurePerson("pip").HeldShown.Any(),
                "Nori holds the shop's jar; Moss the pipette and the clipboard; Pip's hands are free for the till");
            Check(w.FigurePerson("moss").Colour("DOSE") == GrowthPersonLook.EmptyDose, "Moss's pipette holds no dose yet (rinsed pale)");
            LookAt(P(2.0f, 0, -1.4f), w.FigurePosition("pip") + Vector3.up * 1.2f); yield return Shot("540-pip-behind-the-counter");
            LookAt(P(0.8f, 0, 2.2f), P(0.45f, 1.0f, 5.8f)); yield return Shot("541-nori-and-moss-in-the-back-room");

            // Nori carries the player's clutch to the shop's tank, the Pondlet swimming in the jar, and reaches in at the tank.
            Ok(s.Act(() => shop.SetTarget("pondlet", 2)), "Target 2 Pondlets (Nori's card's + button)");
            PlayerClutch();
            var nori = shop.Helper("nori");
            float Walk(GrowthHelper h) => Mathf.Max(0.01f, h.StepSeconds - GrowthWalk.Handle);
            for (int i = 0; i < 2000 && !(nori.Step == "deliver" && nori.Progress >= 0.6f * Walk(nori)); i++) { s.Advance(0.05f); if (i % 4 == 0) yield return null; }
            Check(nori.Step == "deliver" && nori.Hands.Count == 1, "Nori is on the way to the tank with a Pondlet (the route's clock, accelerated)");
            yield return null; yield return null;
            var noriPerson = w.FigurePerson("nori");
            var hip = noriPerson.Joint("hip.L").localRotation; float swing = 0;
            for (float until = Time.time + 0.4f; Time.time < until && nori.Step == "deliver" && nori.Progress < Walk(nori);)
            {
                yield return null;
                swing = Mathf.Max(swing, Quaternion.Angle(hip, noriPerson.Joint("hip.L").localRotation));
            }
            Check(w.FigureActivity("nori") == "walk" && swing > 8f && w.FigureCarrying("nori") == nori.Hands[0].Id && w.FigureInJar("nori") == 1,
                "Nori walks the Pondlet to the tank (the hips swinging " + swing.ToString("0") + "°), the jar held out in front, the Pondlet swimming in it");
            FaceOf(noriPerson.transform, 1.7f); yield return Shot("542-nori-carrying");
            yield return Wait(() => nori.Step == "deliver" && nori.Progress > Walk(nori), 20, "Nori at the tank");
            yield return null; // the frame the walk ends still carries its last steps
            Check(w.FigureActivity("nori") == "work", "At the tank Nori stops and reaches the jar over its rim (the board's reach)");
            FaceOf(noriPerson.transform, 1.6f, 1, 0.2f); yield return Shot("543-nori-at-the-tank");
            for (int i = 0; i < 2000 && shop.Shown("pondlet") < 2; i++) { s.Advance(0.05f); if (i % 4 == 0) yield return null; }
            yield return null; yield return null;
            Check(shop.Shown("pondlet") == 2 && w.FigureInJar("nori") == 0, "Both Pondlets are in the shop's tank, and Nori's jar is empty again");

            // Moss runs a recipe: each dose carried in the pipette, in its culture's colour, and dropped in at the incubator.
            Ok(s.Act(() => shop.SetMossRecipe("pondlet")), "Moss runs the Pondlet (Moss's card's Run it button)");
            var moss = shop.Helper("moss");
            for (int i = 0; i < 2000 && !(moss.Step == "dose-back" && moss.Progress >= 0.4f * Walk(moss)); i++) { s.Advance(0.05f); if (i % 4 == 0) yield return null; }
            yield return null; yield return null;
            var mossPerson = w.FigurePerson("moss");
            Check(moss.Dose != null && w.FigureHoldsDose("moss") && mossPerson.Colour("DOSE") == GrowthContent.Culture(moss.Dose).Hex && w.FigureActivity("moss") == "walk",
                "Moss brings a dose of " + moss.Dose + " to the incubator, the pipette filled in its colour (" + mossPerson.Colour("DOSE") + ")");
            LookAt(P(1.3f, 0, 5.8f), w.FigurePosition("moss") + Vector3.up * 1.0f); yield return Shot("544-moss-with-a-dose");
            yield return Wait(() => moss.Step == "dose-back" && moss.Progress > Walk(moss), 20, "Moss at the incubator");
            yield return null;
            Check(w.FigureActivity("moss") == "work", "At the incubator Moss squeezes the drops in (the board's drops)");
            FaceOf(mossPerson.transform, 1.5f, 1, 0.3f); yield return Shot("545-moss-dosing");
            yield return Wait(() => moss.Dose == null, 20, "the dose in");
            yield return null; yield return null;
            Check(mossPerson.Colour("DOSE") == GrowthPersonLook.EmptyDose && !w.FigureHoldsDose("moss"), "The dose in, the pipette is rinsed pale again");

            // Pip at the till through the day: the scanner's sweep and a wave to the visitor.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            var pip = shop.Helper("pip");
            for (int i = 0; i < 2000 && pip.Step != "serve"; i++) { s.Advance(0.1f); if (i % 4 == 0) yield return null; }
            yield return null; yield return null;
            var pipPerson = w.FigurePerson("pip");
            Check(pip.Step == "serve" && w.FigureActivity("pip") == "work" && pipPerson.HandShape("L") == "grip" && pipPerson.HandShape("R") == "open",
                "Pip bags an order at the till: the scanner's sweep in the left hand, the right open to wave");
            LookAt(P(1.6f, 0, -2.9f), w.FigurePosition("pip") + Vector3.up * 1.2f); yield return Shot("546-pip-at-the-till");
            var arm = pipPerson.Joint("shoulder.R").localRotation; float wave = 0;
            for (float until = Time.time + 1.2f; Time.time < until && pip.Step == "serve";)
            {
                yield return null;
                wave = Mathf.Max(wave, Quaternion.Angle(arm, pipPerson.Joint("shoulder.R").localRotation));
            }
            Check(wave > 20f, "Pip's right arm goes up to wave goodbye as the order is bagged (" + wave.ToString("0") + "°)");
            yield return Wait(() => s.Ui.Log.Any(l => l.StartsWith("Pip sold ")) && w.VisitorViews.Values.Any(v => v.Carrying != null), 20, "the visitor Pip served leaving with the bag");
            Check(true, "The visitor Pip served leaves with what they bought in the shop's bag");
            yield return new WaitForSeconds(0.5f);
            var leaving = w.VisitorViews.Values.First(v => v.Carrying != null);
            FaceOf(leaving.transform, 1.6f); yield return Shot("547-pip-served-them");
        }

        /// <summary>GR-18 wave 4 (§6.7): the silly moments, played by the people of waves 1 to 3.</summary>
        IEnumerator PeopleComedy()
        {
            limitations.Add("GR-18 wave 4 (the silly moments): the fixture is labelled, as wave 2's: an era 4, four-star shop with 8,000 credits, unit B and the power room open, growers holding 10 doses each, a Starter tank on a1 to a4 with a Pondlet, a Bubblebelly and a Ribbonfin each; Pip is hired with the staff card's action and the shop opened with the counter card's. Four visitors (a bargain hunter at a2, a casual visitor at a4, a collector at a1, a big spender at a3) are put into the shop's own list by the route browsing their tank until it lets them decide (labelled); two more later, to queue for Pip. In a validation run the room's own moments are off unless the route switches them on (labelled): it does here, once the visitors stand at their tanks. The first fart is the room's own turn; the complaint, the fish and the second fart are started by the route at a chosen spot (labelled), each after it clears the comedy's quiet time (labelled, so the route need not wait 8 to 40 s between them). The slaps are the route's keyboard pressing F (then G) at the visitor under the reticle, and Backspace to skip. The hatch is the shop's own incubation with its clock advanced (labelled accelerated). The day is ended by setting its clock (labelled); the reload is the session's own (ReloadSaved). How funny it is, and how loud, are for the user to judge.");
            var w = s.World;
            var gags = s.Gags;
            var hands = s.Hands;
            Check(gags != null && gags.Sounds.Count == 12 && GrowthPeopleKit.Data.gags.parts.Length == 7, "The silly moments are in the game: " + gags.Sounds.Count + " sounds made here, and the people kit's " + GrowthPeopleKit.Data.gags.parts.Length + " faces and props");
            Check(!gags.RoomMoments, "In a validation run the room's own moments wait for the route to ask for them");
            var fx = new GrowthShop { Era = 4, Reputation = GrowthContent.StarThresholds[3] + 30, Credits = 8000, Rp = 300, UnitB = true, SalesTotal = 160, Annex = true };
            fx.Discovered.AddRange(GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).Select(x => x.Id));
            foreach (var sp in new[] { "pondlet", "bubblebelly", "ribbonfin" }) if (!fx.Discovered.Contains(sp)) fx.Discovered.Add(sp);
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Levels["grower"] = 2; fx.Levels["counter"] = 3;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            var tanks = new List<GrowthContainer> { fx.Containers[0] };
            foreach (var place in new[] { "a2", "a3", "a4" }) { var c = new GrowthContainer { Id = "tank-comedy-" + place, Kind = "starter", Place = place }; fx.Containers.Add(c); tanks.Add(c); }
            foreach (var c in tanks) { Stock(fx, c, "pondlet"); Stock(fx, c, "bubblebelly"); Stock(fx, c, "ribbonfin"); }
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null;
            Ok(s.Act(() => shop.Hire("pip")), "Hire Pip (the staff card's button)");
            s.SetCard(GrowthCard.None);
            Ok(s.Act(shop.OpenShop), "Open the shop (the counter card's button)");
            shop.Visitors.Clear();
            var at = tanks.ToDictionary(c => c.Place);
            var bargain = Visitor(31, "bargain", 500, at["a2"]);
            var casual = Visitor(32, "casual", 500, at["a4"]);
            var collector = Visitor(33, "collector", 500, at["a1"]);
            var premium = Visitor(34, "premium", 500, at["a3"]);
            var four = new[] { bargain, casual, collector, premium };
            yield return Wait(() => four.All(v => ViewOf(v) != null && ViewOf(v).Arrived && ViewOf(v).Activity == "browse"), 30, "the four visitors at their tanks");
            Check(shop.Phase == GrowthPhase.Day && shop.Working("pip") && four.All(v => v.State == GrowthVisitorState.Browsing), "Fixture: the shop open, Pip at the till, four visitors browsing a tank each in unit A");

            // The room's own fart: the visitor nearest the player squeaks and looks innocent, a puff of a cloud behind them; a
            // neighbour sniffs and blames the fish.
            var a1 = GrowthWorld.Places["a1"];
            LookAt(a1.Browse + new Vector3(2.2f, 0, -0.9f), a1.Browse + new Vector3(0, 1.1f, -0.2f));
            yield return null;
            gags.RoomMoments = true;
            yield return Wait(() => gags.Policy.Current == GrowthGag.Fart, 25, "the room's own turn (a fart)");
            var culprit = gags.Subject; var blamer = gags.Other;
            Check(culprit != null && blamer != null && culprit != blamer, "The room's own moment comes in its turn: a fart by the visitor nearest the player (" + culprit.name + "), a neighbour within " + GrowthGags.Neighbour + " m (" + blamer.name + ")");
            var pair = (culprit.transform.position + blamer.transform.position) * 0.5f;
            LookAt(pair + new Vector3(2.4f, 0, 1.2f), pair + Vector3.up * 0.95f);
            yield return Wait(() => gags.Policy.Age > 1.15f, 3, "the fart's blame");
            Check(culprit.Reaction == "culprit" && culprit.Person.GagShown("gag__cloud") && blamer.Reaction == "blame", "The culprit looks up and around, all innocence, a puff of a cloud behind them; the neighbour sniffs, then points at the fish");
            Check(gags.Asked.TryGetValue("squeak", out int sq) || gags.Asked.TryGetValue("squeak2", out sq), "The squeak sounds (once)");
            Check(gags.CaptionText != null && (gags.CaptionText.Contains("fish") || gags.CaptionText.Contains("tank") || GrowthContent.Species.Any(x => gags.CaptionText.Contains(x.Name))) && s.Ui.CaptionShown != null,
                "Its caption: \"" + s.Ui.CaptionShown + "\"");
            yield return Shot("570-a-suspicious-fart");
            yield return Wait(() => gags.Policy.Current == null, 4, "the fart's end");
            yield return null;
            Check(!culprit.Person.GagShown("gag__cloud") && culprit.Reaction == null && blamer.Reaction == null && s.Ui.CaptionShown == null, "Over: the cloud, the reactions and the caption go");
            Check(gags.Policy.WhyNot(GrowthGag.Complaint) == "quiet", "Then the room is quiet a while (nothing else starts)");

            // The slap: its own key at the visitor under the reticle, with a clear prompt; a bargain hunter leaves in a huff.
            var bv = ViewOf(bargain);
            var a2 = GrowthWorld.Places["a2"];
            LookAt(a2.Browse + new Vector3(1.2f, 0, 0.3f), bv.transform.position + Vector3.up * 1.3f);
            yield return null; yield return null;
            Check(gags.SlapTarget == bv && s.Ui.SlapPromptShown && s.Ui.SlapPromptKey == "F", "Aiming at the bargain hunter within reach, the slap's prompt shows its key (F) and whom");
            int rep = shop.Reputation, credits = shop.Credits, slaps = hands.Slaps;
            yield return Press(Key.F);
            Check(bargain.State == GrowthVisitorState.Leaving && bargain.Outcome == GrowthVisitOutcome.Slapped && shop.Reputation == rep + GrowthShop.SlapReputation && shop.Credits == credits,
                "F slaps: the bargain hunter leaves in a huff, the shop a reputation point down (" + rep + " to " + shop.Reputation + "), not a credit more");
            Check(hands.Slaps == slaps + 1 && hands.RightDoing == "slap" && hands.ShapeOf("R") == "open", "The right hand swings the slap, open-handed");
            yield return new WaitForSeconds(0.06f);
            yield return Shot("571-the-slap");
            yield return Wait(() => gags.Policy.Age > 0.45f, 3, "the slap's beats");
            Check(Vector3.Angle(bv.transform.forward, s.Player.transform.position - bv.transform.position) < 50f, "The slapped visitor turns to face the player");
            Check(bv.Reaction == "slapped" && bv.Person.GagShown("head__mark-slap") && bv.Person.GagShown("gag__stars") && (bv.Person.Look.Sunglasses || bv.Person.GagShown("head__eyes-squeeze") || gags.Policy.Age > 0.9f),
                "The visitor's head is knocked aside: a hand mark on the cheek, eyes squeezed shut, dizzy stars circling");
            Check(gags.Asked.ContainsKey("slap") && gags.Asked.ContainsKey("boing") && gags.Asked.ContainsKey("grumble") && GrowthComedy.Grumbles.Contains(gags.CaptionText) && gags.CaptionWho == "Bargain hunter",
                "A thwack, a boing and a grumbled gibberish: \"" + s.Ui.CaptionShown + "\"");
            Check(bv.Staggered > 0 && bv.Staggered <= 0.55f, "A stagger before the walk out (" + bv.Staggered.ToString("0.00") + " s, as long as the walk still ends before the rules let them go)");
            yield return Shot("572-slapped");
            Check(s.Ui.Log.Any(l => l.Contains("left in a huff")), "The note: a slapped visitor left in a huff");

            // The hand still stings; then a casual visitor laughs off a first slap, and Skip ends the moment (the laugh stands).
            var cv = ViewOf(casual);
            var a4 = GrowthWorld.Places["a4"];
            LookAt(a4.Browse + new Vector3(1.2f, 0, -0.3f), cv.transform.position + Vector3.up * 1.3f);
            yield return null; yield return null;
            Check(gags.SlapTarget == cv, "Aiming at the casual visitor");
            if (shop.SlapWait > 0.4f)
            {
                yield return Press(Key.F);
                Check(casual.Slaps == 0 && cv.Reaction == null, "Within a few seconds of a slap the hand is still stinging: nothing happens (" + shop.SlapWait.ToString("0.0") + " s to go)");
            }
            yield return Wait(() => shop.SlapWait <= 0, 4, "the hand to stop stinging");
            rep = shop.Reputation;
            yield return Press(Key.F);
            yield return Wait(() => gags.Policy.Age > 0.9f, 3, "the laugh");
            Check(casual.State == GrowthVisitorState.Browsing && casual.Slaps == 1 && cv.Reaction == "laugh" && cv.Person.MouthShown == "grin" && shop.Reputation == rep,
                "A casual visitor laughs off a first slap: doubled over, grinning, still browsing, no reputation lost");
            Check(GrowthComedy.Laughs.Contains(gags.CaptionText) && gags.Asked.ContainsKey("laugh"), "Ha ha: \"" + s.Ui.CaptionShown + "\"");
            yield return Shot("573-laughed-off");
            int skipped = gags.Policy.Skipped;
            yield return Press(Key.Backspace);
            yield return null;
            Check(gags.Policy.Skipped == skipped + 1 && gags.Policy.Current == null && cv.Reaction == null && s.Ui.CaptionShown == null && !cv.Person.GagShown("gag__stars"),
                "Backspace skips it: the reaction, the stars and the caption go");
            Check(casual.Slaps == 1 && casual.State == GrowthVisitorState.Browsing, "What the slap did stands (the rules')");

            // The complaint: a visitor at a tank jabs a finger at a creature.
            gags.Policy.Clear();
            LookAt(a4.Browse + new Vector3(-0.2f, 0, 1.0f), a4.Position + new Vector3(0.3f, 0.9f, 0));
            yield return null;
            Check(gags.Try(GrowthGag.Complaint) && gags.Subject == cv, "The complaint (started by the route at the casual visitor's tank, labelled)");
            yield return Wait(() => gags.Policy.Age > 0.7f, 3, "the complaint");
            Check(cv.Reaction == "complain" && (cv.Person.Look.HeldRight != null || cv.Person.HandShape("R") == "point") && (gags.Asked.ContainsKey("mumble") || gags.Asked.ContainsKey("mumble2")), "The visitor jabs a finger at the tank, mumbling");
            Check(GrowthContent.Species.Any(x => gags.CaptionText.Contains(x.Name)) && gags.CaptionWho == "Casual visitor", "A ridiculous complaint about a creature: \"" + s.Ui.CaptionShown + "\"");
            LookAt(a4.Browse + new Vector3(0.9f, 0, -1.3f), a4.Browse + new Vector3(-0.5f, 1.05f, 0));
            yield return Shot("574-a-complaint");
            yield return Wait(() => gags.Policy.Current == null, 4, "the complaint's end");

            // The fish copies a visitor's fish face: puffing its cheeks and waggling inside its tank, a blub and its bubbles.
            gags.Policy.Clear();
            var a3 = GrowthWorld.Places["a3"];
            var pv = ViewOf(premium);
            LookAt(a3.Browse + new Vector3(1.3f, 0, -0.3f), a3.Position + new Vector3(0, 0.95f, 0));
            yield return null; yield return null;
            Check(gags.Try(GrowthGag.Mimic) && gags.Subject == pv && gags.Fish != null, "The fish copies a face (started by the route at the big spender's tank, labelled)");
            var fish = gags.Fish;
            var tankView = w.Tanks[premium.ContainerId];
            var water = new Bounds(tankView.Root.TransformPoint(new Vector3(0, tankView.WaterY, 0)), tankView.Inner);
            bool inside = true; float puffed = 0;
            for (float until = Time.realtimeSinceStartup + 1.6f; Time.realtimeSinceStartup < until;)
            {
                yield return null;
                var rs = fish.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToList();
                if (rs.Count == 0) continue;
                var fb = rs[0].bounds; foreach (var r in rs) fb.Encapsulate(r.bounds);
                inside &= water.Contains(fb.min + Vector3.one * 0.01f) && water.Contains(fb.max - Vector3.one * 0.01f);
                if (fish.Mimic >= 0) puffed = Mathf.Max(puffed, fish.Mimic);
            }
            Check(pv.Reaction == "fishface" || gags.Policy.Current == null, "The visitor makes a fish face at the glass, hands flapping by the cheeks");
            Check(puffed > 0.5f && fish.MimicBursts >= 1 && gags.Asked.ContainsKey("blub"), "The creature copies it: cheeks puffed, a waggle, a blub and a burst of its bubbles");
            Check(inside, "Copying, it stays inside its tank's water (" + water.size.ToString("F2") + ")");
            Check(s.Ui.Log.Any(l => l.Contains("copying me") || l.Contains("doing my face")), "Its caption: \"" + s.Ui.Log.Last(l => l.Contains("copying me") || l.Contains("doing my face")) + "\"");
            // From the tank's far side, over it: the visitor's fish face at the glass and the creature copying it in the water.
            LookAt(a3.Position + new Vector3(0.05f, 0, 1.5f), a3.Browse + new Vector3(0, 0.95f, 0.25f));
            yield return Shot("575-the-fish-copies-a-face");
            yield return Wait(() => gags.Policy.Current == null, 4, "the fish's end");
            yield return null;
            Check(fish == null || fish.Mimic < 0, "Over: the fish is itself again");

            // The incubator's burp, after the hatchling shows (never before, and never hiding it).
            gags.Policy.Clear();
            var inc = GrowthWorld.IncubatorPosition;
            LookAt(inc + new Vector3(1.6f, 0, -1.4f), inc + new Vector3(0, 1.1f, 0));
            Ok(shop.Collect("bloom", shop.HoldSeconds), "Collect fixture Bloom"); Ok(shop.Collect("flow", shop.HoldSeconds), "Collect fixture Flow");
            Ok(shop.AddDose("bloom"), "Dose Bloom"); Ok(shop.AddDose("flow"), "Dose Flow");
            Ok(shop.StartIncubation(), "Start the incubation");
            gags.HatchesSinceBurp = GrowthGags.BelchEvery - 1;
            s.Advance(shop.IncubationSeconds + 0.05f);
            float hatched = gags.Policy.Clock;
            Check(shop.Hatchling != null, "It hatches (the shop's own clock, advanced: labelled)");
            yield return Wait(() => gags.Policy.Current == GrowthGag.Belch, 4, "the burp");
            Check(gags.Policy.Clock - hatched >= GrowthGags.BelchDelay - 0.05f && w.HatchlingView != null, "The burp waits for the hatchling to show (" + (gags.Policy.Clock - hatched).ToString("0.0") + " s after the hatch)");
            float low = 2;
            for (float until = Time.realtimeSinceStartup + 0.4f; Time.realtimeSinceStartup < until;) { yield return null; low = Mathf.Min(low, w.IncubatorSquash); }
            yield return Wait(() => w.BurpShowing, 2, "the bubble");
            Check(low < 0.99f && w.BurpPosition.y > w.IncubatorTop, "A squash of the incubator, then a bubble rising from its top");
            Check(gags.Asked.ContainsKey("burp") && GrowthComedy.Belches.Contains(gags.CaptionText) && gags.CaptionWho == "The incubator", "*buuurp*: \"" + s.Ui.CaptionShown + "\"");
            yield return Wait(() => gags.Policy.Age > 0.6f, 2, "the bubble to rise");
            yield return Shot("576-the-incubator-burps");
            yield return Wait(() => gags.Policy.Current == null, 4, "the burp's end");
            yield return null;
            Check(!w.BurpShowing, "The bubble pops");

            // Pip's little dance after a sale, only with nobody waiting; it stops the moment a visitor queues.
            gags.Policy.Clear();
            var buyer = Visitor(35, "bargain", 500, at["a1"]); buyer.BrowseFor = 0.5f;
            LookAt(P(1.6f, 0, -2.9f), w.FigurePosition("pip") + Vector3.up * 1.2f);
            var pip = shop.Helper("pip");
            int sold = shop.SalesTotal;
            yield return Wait(() => shop.SalesTotal > sold, 30, "Pip to sell to the visitor");
            yield return Wait(() => gags.Policy.Current == GrowthGag.Dance, 3, "Pip's dance");
            Check(pip.Step == null && !shop.Queue.Any() && gags.Helper == "pip", "After the sale, with nobody waiting, Pip dances (" + GrowthGags.DanceAfter + " s after the job)");
            yield return Wait(() => gags.Policy.Age > 0.5f, 2, "the dance");
            Check(w.FigureActivity("pip") == "dance" && w.FigurePerson("pip").GagShown("note0") && gags.Asked.ContainsKey("shoe"), "A tiny victory dance, music notes over Pip's head, squeaky shoes");
            Check(gags.CaptionWho == "Pip" && (gags.CaptionText == GrowthComedy.Cheer("pip", 0) || gags.CaptionText == GrowthComedy.Cheer("pip", 1)), "Pip's cheer: \"" + s.Ui.CaptionShown + "\"");
            yield return Shot("577-pip-dances");
            var next = Visitor(36, "collector", 500, at["a1"]); next.BrowseFor = 0.1f;
            yield return Wait(() => pip.Step == "serve", 30, "the next visitor to queue");
            yield return null; yield return null;
            Check(gags.Policy.Current == null && w.FigureActivity("pip") != "dance" && !w.FigurePerson("pip").GagShown("note0"), "A visitor at the till: the dance stops at once and Pip serves (the work never waits for it)");

            // The Gags setting off: the room's moments stop, the player's own slap still lands; Humour off: silent.
            var ov = ViewOf(collector);
            LookAt(a1.Browse + new Vector3(0.35f, 0, -1.1f), ov.transform.position + Vector3.up * 1.35f);
            yield return null; yield return null;
            yield return Wait(() => shop.SlapWait <= 0 && gags.SlapTarget == ov, 5, "the collector under the reticle");
            s.HumourLevel = GrowthAudio.Level.Off; s.GagLevel = GrowthGagLevel.Off;
            gags.Policy.Clear();
            yield return null;
            Check(!gags.Try(GrowthGag.Complaint), "With the Gags setting off, the room's own moments do not start");
            yield return Press(Key.F);
            Check(collector.Outcome == GrowthVisitOutcome.Slapped, "With the Gags setting off, the player's own slap still lands");
            Check(gags.LastGain == 0, "With Humour off, the thwack is silent");
            s.HumourLevel = GrowthAudio.Level.Full; s.GagLevel = GrowthGagLevel.Full;

            // Paused, a moment waits; closing time ends it; a reload clears it.
            gags.Policy.Clear();
            LookAt(a4.Browse + new Vector3(-0.2f, 0, 1.0f), a4.Position + new Vector3(0.3f, 0.9f, 0));
            yield return null;
            Check(gags.Try(GrowthGag.Complaint), "Another complaint (labelled)");
            yield return new WaitForSeconds(0.4f);
            s.SetCard(GrowthCard.Pause);
            float age = gags.Policy.Age;
            yield return new WaitForSeconds(0.6f);
            Check(Mathf.Abs(gags.Policy.Age - age) < 1e-4f && gags.Policy.Current == GrowthGag.Complaint, "Paused, the moment waits (" + age.ToString("0.00") + " s in)");
            s.SetCard(GrowthCard.None);
            yield return new WaitForSeconds(0.3f);
            Check(gags.Policy.Age > age, "Resumed, it goes on");
            shop.DaySecondsLeft = 0.05f;
            yield return Wait(() => shop.Phase == GrowthPhase.Night, 3, "closing time (the day's clock set: labelled)");
            if (s.Card == GrowthCard.Receipt) s.SetCard(GrowthCard.None);
            yield return null; yield return null;
            Check(gags.Policy.Current == null && cv.Reaction == null && s.Ui.CaptionShown == null, "Closing time: the moment ends as the visitor leaves");
            Check(s.ReloadSaved() && gags.Policy.Current == null && s.Ui.CaptionShown == null, "A reload: nothing left playing");

            // The slap's key from the Settings screen's Controls tab (GR-23, where every action is remappable): E swaps with
            // Interact; back to the defaults; then G, kept with the whole game's settings.
            s.OpenSettings("Controls");
            yield return null; yield return null;
            gags.StartRebind();
            yield return null;
            yield return Press(Key.E);
            Check(!gags.Rebinding && s.SlapKey == Key.E && s.Settings.InputOf(GrowthAction.Interact) == "Key/F",
                "E already does something: the two swap, Interact moving to F (\"" + gags.RebindProblem + "\")");
            s.Settings.ResetBindings(); s.SaveSettings(); s.Ui.RefreshCard();
            gags.StartRebind();
            yield return null;
            yield return Press(Key.G);
            Check(!gags.Rebinding && s.SlapKey == Key.G && s.Settings.InputOf(GrowthAction.Interact) == "Key/E", "The slap's key is now G (Interact back on E)");
            Check(File.ReadAllText(s.SettingsPath).Contains("\"input\": \"Key/G\""), "Kept with the whole game's settings (" + Path.GetFileName(s.SettingsPath) + ")");
            yield return Shot("578-the-pause-menus-comedy-row");
            s.SetCard(GrowthCard.None);
            s.SlapKey = Key.F; s.SaveSettings();
            gags.RoomMoments = false;
        }

        /// <summary>GR-18 wave 3: the player's hands, in each thing the player does.</summary>
        IEnumerator PeopleHands()
        {
            limitations.Add("GR-18 wave 3 (the player's hands): the fixture is labelled: an era 3, four-star shop with 10,000 credits, the power room open, the generator at level 1 (the hand crank), growers at level 3 (a tap each) holding 10 doses, the siphon at level 3 (the twin chamber), a Starter tank on a1 with three Pondlets and a Bubblebelly and an empty one on a2. The siphon is filled at the Bloom and Flow growers, the crank turned and the counter served with the route's held mouse; two creatures are taken from the tank in peek with E and a third with the tank card's action, and one put in the other tank with E, as a player does; pipe mode starts as the cards' button starts it and is stopped by the route; the day is opened with the counter card's action and the rules' clock is run on (accelerated, labelled) until a visitor waits at the counter; a card is opened and closed by the route. The player walks by being moved 2.5 cm a frame, back and then forth. Frame times are measured uncapped (vSync off) for 4 s with three bags and the siphon up and 4 s with the hands hidden by the route, the same view.");
            var w = s.World;
            var hands = s.Hands;
            Check(hands != null, "The player's hands are on the camera (the look, with the people kit's hands: " + GrowthPeopleKit.Data.hands.parts.Length + " parts)");
            var fx = new GrowthShop { Era = 3, Reputation = GrowthContent.StarThresholds[3] + 10, Credits = 10000, Rp = 100, UnitB = true, Annex = true };
            fx.Discovered.AddRange(GrowthContent.Species.Where(x => x.Kind == SpeciesKind.Founder).Take(12).Select(x => x.Id));
            if (!fx.Discovered.Contains("pondlet")) fx.Discovered[0] = "pondlet";
            if (!fx.Discovered.Contains("bubblebelly")) fx.Discovered[1] = "bubblebelly";
            fx.Journal.AddRange(fx.Discovered.Select(d => GrowthShop.JournalKey(d, Colour.Classic)));
            fx.Levels["grower"] = 3; fx.Levels["siphon"] = 3; fx.Levels["generator"] = 1; fx.Levels["counter"] = 3;
            foreach (var c in fx.Cultures) fx.GrowerUnits[c] = 10;
            var tank = fx.Containers[0];
            Stock(fx, tank, "pondlet"); Stock(fx, tank, "pondlet"); Stock(fx, tank, "pondlet"); Stock(fx, tank, "bubblebelly");
            var other = new GrowthContainer { Id = "tank-hands-a2", Kind = "starter", Place = "a2" };
            fx.Containers.Add(other);
            s.LoadFixture(fx);
            var shop = s.Shop;
            yield return null; yield return null; yield return null;
            Check(shop.Annex && shop.HandCrank && shop.Level("siphon") == 3 && shop.SiphonCapacity == 10 && shop.Taps >= 1 && tank.Occupants.Count == 4 && shop.Phase == GrowthPhase.Night,
                "Fixture: the power room open, the generator's hand crank, growers with a tap, the twin-chamber siphon, four creatures in the a1 tank, at night");

            // Empty-handed, the hands are out of view.
            LookAt(P(1.6f, 0, -2.9f), P(-1.2f, 1.2f, 1.6f));
            yield return new WaitForSeconds(0.8f);
            Check(!hands.Shown("L") && !hands.Shown("R") && hands.LeftDoing == "rest" && hands.RightDoing == "rest", "Empty-handed, the hands are lowered out of view");

            // The siphon at the Bloom grower: the bulb squeezed once a dose, each dose a band of Bloom in the twin tubes.
            LookAt(GrowthWorld.GrowerPositions["bloom"] + new Vector3(1.4f, 0, 0), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Grower, "bloom", "The reticle focuses the Bloom grower");
            Hold(true);
            float lo = 9, hi = 0; var shapes = new HashSet<string>();
            for (float until = Time.realtimeSinceStartup + 1.4f; Time.realtimeSinceStartup < until;)
            {
                yield return null;
                if (hands.RightDoing == "collect") { lo = Mathf.Min(lo, hands.BulbSize); hi = Mathf.Max(hi, hands.BulbSize); shapes.Add(hands.ShapeOf("R")); }
            }
            Check(s.CollectingNow && hands.RightDoing == "collect" && hands.SiphonShown && hands.Tubes == 2, "Holding the mouse at the grower, the right hand holds the siphon to it (the twin chamber's two tubes)");
            Check(hi - lo > 0.2f && shapes.Contains("cup") && shapes.Contains("grip"), "The hand cups the bulb and squeezes it once a dose (its size " + lo.ToString("0.00") + " to " + hi.ToString("0.00") + ")");
            yield return Shot("550-siphoning-the-bloom-grower");
            yield return Wait(() => shop.Held.Count >= 4, 6, "four Bloom doses");
            Hold(false); yield return null;
            LookAt(GrowthWorld.GrowerPositions["flow"] + new Vector3(1.4f, 0, 0), GrowthWorld.GrowerPositions["flow"] + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Hold(true);
            yield return Wait(() => shop.Held.Count >= 6, 6, "two Flow doses");
            Hold(false);
            yield return new WaitForSeconds(0.6f);
            Check(hands.RightDoing == "siphon" && hands.BandsShown == shop.Held.Count && Enumerable.Range(0, shop.Held.Count).All(i => hands.BandColour(i) == GrowthContent.Culture(shop.Held[i]).Hex),
                "Let go, the siphon stays in hand, a band of each dose in its culture's colour (" + string.Join(", ", Enumerable.Range(0, shop.Held.Count).Select(hands.BandColour)) + ")");
            LookAt(GrowthWorld.GrowerPositions["flow"] + new Vector3(1.5f, 0, -0.6f), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.1f, 0));
            yield return Shot("551-the-siphon-holding-doses");

            // The generator's hand crank: both hands on the handle, going round with it.
            var crank = GrowthWorld.GeneratorPosition + new Vector3(0, 0.9f, -0.3f);
            yield return AimAt(GrowthWorld.GeneratorPosition + new Vector3(0, 0, -1.4f), crank, GrowthTargetKind.Generator, "generator", "The reticle focuses the generator's crank");
            Hold(true);
            yield return Wait(() => s.CrankingNow, 3, "the crank to turn");
            yield return new WaitForSeconds(0.6f);
            var r0 = hands.WristOnScreen("R"); var l0 = hands.WristOnScreen("L");
            yield return new WaitForSeconds(0.3f);
            var r1 = hands.WristOnScreen("R"); var l1 = hands.WristOnScreen("L");
            Check(hands.LeftDoing == "crank" && hands.RightDoing == "crank" && !hands.SiphonShown, "Cranking, both hands are on the crank (the siphon put by)");
            Check((r1 - r0).magnitude > 0.02f && Mathf.Abs((r1 - r0).y - (l1 - l0).y) < 0.03f, "They go round with the handle, together (moved " + ((Vector2)(r1 - r0)).magnitude.ToString("0.000") + " of the screen)");
            yield return Shot("552-cranking-the-generator");
            Hold(false); yield return null;

            // Pipe mode: a length of pipe across both hands.
            s.StartPipe(GrowthLab.Tap("bloom"));
            LookAt(GrowthWorld.GeneratorPosition + new Vector3(-1.2f, 0, -1.8f), GrowthWorld.GrowerPositions["bloom"] + new Vector3(0, 1.4f, 0));
            yield return new WaitForSeconds(0.8f);
            Check(hands.LeftDoing == "pipe" && hands.RightDoing == "pipe" && hands.PipeShown, "Laying a pipe, a length of it is held in both hands");
            yield return Shot("553-laying-a-pipe");
            s.StopPipe();

            // In peek, E takes a creature from the tank: the right hand reaches in for it, and its bag joins the left hand's.
            var a1 = GrowthWorld.Places["a1"];
            bool reached = false, shot = false; float farthest = 0;
            for (int n = 1; n <= 2; n++)
            {
                LookAt(a1.Browse + new Vector3(0, 0, 0.15f), a1.Position + new Vector3(0, 1.18f, -0.1f));
                yield return Wait(() => s.Peeking, 3, "peek to ease in");
                float hunt = Time.realtimeSinceStartup + 12;
                while (s.PeekCreature == null && Time.realtimeSinceStartup < hunt) yield return null;
                Check(s.PeekCreature != null, "A creature under the reticle in peek (" + n + ")");
                yield return Click(1);
                Check(shop.Carried.Count == n, "The take button takes it: " + n + " carried");
                for (float from = Time.realtimeSinceStartup; Time.realtimeSinceStartup < from + 0.7f;)
                {
                    reached |= hands.RightDoing == "take"; farthest = Mathf.Max(farthest, hands.FarthestInView);
                    if (n == 1 && !shot && Time.realtimeSinceStartup > from + 0.14f) { shot = true; yield return Shot("554-taking-one-in-peek"); continue; }
                    yield return null;
                }
            }
            Check(reached, "Taking one in peek, the right hand reaches in for it");
            // The third with the tank card's action (two creatures left swim where the reticle may not find one soon).
            Ok(s.Act(() => shop.TakeOut(tank.Id, tank.Occupants[0].Id)), "Take a third out (the tank card's action)");
            yield return new WaitForSeconds(0.9f);
            Check(hands.BagsShown == 3 && hands.BagCreatures == 3 && hands.BagSpecimens.OrderBy(x => x).SequenceEqual(shop.Carried.Select(c => c.Id).OrderBy(x => x)),
                "Three carried: three of the shop's water bags in the left hand, a creature swimming in each, the ones carried");
            // In peek the hands drop out of the way of the glass: the same view with peek switched off has them higher.
            float peekY = hands.WristOnScreen("L").y;
            Check(s.Peeking, "Still in peek at the a1 tank");
            s.PeekOn = false;
            yield return new WaitForSeconds(0.9f);
            float plainY = hands.WristOnScreen("L").y;
            Check(!s.Peeking && plainY > peekY + 0.03f, "In peek the hands were lower, out of the way of the glass (" + peekY.ToString("0.00") + " of the screen up, " + plainY.ToString("0.00") + " with peek off)");
            s.PeekOn = true;

            // Carrying three, the bags in the left hand and the siphon in the right; walking, they bob.
            var a2 = GrowthWorld.Places["a2"];
            LookAt(a1.Browse + new Vector3(1.0f, 0, -2.4f), a2.Position + new Vector3(0, 1.0f, 0));
            yield return new WaitForSeconds(0.9f);
            Check(!s.Peeking && hands.LeftDoing == "carry" && hands.RightDoing == "siphon", "Carrying, the bags in the left hand, the siphon (holding doses) in the right");
            yield return Shot("555-carrying-three");
            float Range(List<float> ys) => ys.Max() - ys.Min();
            var still = new List<float>(); var walking = new List<float>();
            for (int f = 0; f < 60; f++) { yield return null; still.Add(hands.WristOnScreen("L").y); }
            var feet = s.Player.transform.position; var dir = s.Player.transform.forward; dir.y = 0; dir.Normalize();
            float yaw = s.Player.transform.eulerAngles.y, pitch = s.Player.Pitch;
            for (int f = 0; f < 60; f++)
            {
                feet += dir * (f < 30 ? -0.025f : 0.025f);
                s.Player.Teleport(feet, Quaternion.Euler(0, yaw, 0)); s.Player.Pitch = pitch;
                yield return null;
                walking.Add(hands.WristOnScreen("L").y);
            }
            Check(Range(walking) > 0.01f && Range(walking) > 2 * Range(still), "Walking, the hands bob (" + Range(walking).ToString("0.000") + " of the screen up and down, " + Range(still).ToString("0.000") + " standing)");

            // E on the other tank puts one in: the left hand reaches with its bag and lets go.
            LookAt(a2.Browse + new Vector3(0, 0, -0.6f), a2.Position + new Vector3(0, 1.1f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Tank, other.Id, "The reticle focuses the a2 tank");
            yield return Click();
            Check(shop.Carried.Count == 2 && other.Occupants.Count == 1, "A click puts one in the a2 tank");
            bool put = false; shot = false;
            for (float from = Time.realtimeSinceStartup; Time.realtimeSinceStartup < from + 0.8f;)
            {
                put |= hands.LeftDoing == "put"; farthest = Mathf.Max(farthest, hands.FarthestInView);
                if (!shot && Time.realtimeSinceStartup > from + 0.16f) { shot = true; yield return Shot("556-putting-one-in"); continue; }
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            Check(put && hands.BagsShown == 2 && hands.LeftDoing == "carry", "Putting it in, the left hand reaches with its bag and lets go; two bags left");
            Check(farthest < GrowthHands.Capsule, "Reaching to the tanks, nothing of the hands in view goes further from the eye than the player's capsule (" + farthest.ToString("0.000") + " m), so never through the glass");

            // A card lowers the hands; closing it, they come back.
            s.SetCard(GrowthCard.Tank, other.Id);
            yield return new WaitForSeconds(1.2f);
            Check(!hands.Shown("L") && !hands.Shown("R"), "A card open lowers the hands");
            s.SetCard(GrowthCard.None);
            yield return new WaitForSeconds(1.0f);
            Check(hands.Shown("L") && hands.BagsShown == 2, "Closing it, they come back with the bags");

            // What the hands cost: the same view with three bags and the siphon up, then with the hands hidden (labelled).
            Ok(s.Act(() => shop.TakeOut(other.Id, other.Occupants[0].Id)), "Take it back out (three carried; the card's action)");
            var view = (feet: a1.Browse + new Vector3(1.0f, 0, -2.4f), target: a2.Position + new Vector3(0, 1.0f, 0));
            int timed = timings.Count;
            int previousRate = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            yield return Measure("560-hands-up", view.feet, view.target, 4);
            hands.gameObject.SetActive(false);
            yield return Measure("561-hands-hidden", view.feet, view.target, 4);
            hands.gameObject.SetActive(true);
            Application.targetFrameRate = previousRate; QualitySettings.vSyncCount = previousVSync;
            int tris = hands.Triangles;
            Check(tris < 60000 && hands.Renderers < 60, "The hands draw " + tris + " triangles in " + hands.Renderers + " parts with three bags and the siphon (inside 60,000 and 60)");
            File.WriteAllText(Path.Combine(evidence, "hands-timings.json"), JsonUtility.ToJson(new DensityReport
            {
                note = "Uncapped frame times in shop unit A at night with the player's hands up (three bags with their creatures, the twin-chamber siphon with six doses) and hidden by the route, the same view (" + SystemInfo.graphicsDeviceName + ", " + SystemInfo.processorType + ", " + Screen.width + "x" + Screen.height + ").",
                shopCreatures = s.Shop.Stock.Count(), stressCreatures = 0, views = timings.Skip(timed).ToArray(),
            }, true));

            // The counter by day: a bag held out over it while bagging, let go of when the sale is made.
            Ok(s.Act(shop.OpenShop), "Open the shop");
            for (int i = 0; i < 4000 && shop.FrontOfQueue == null; i++) { s.Advance(0.1f); if (i % 4 == 0) yield return null; }
            var front = shop.FrontOfQueue;
            Check(front != null, "A visitor waits at the counter");
            yield return Wait(() => w.VisitorViews.TryGetValue(front.Id, out var v) && v.Arrived, 25, "the visitor to reach the counter");
            LookAt(GrowthWorld.CounterPosition + new Vector3(0.2f, 0, -0.95f), GrowthWorld.CounterPosition + new Vector3(0, 1.0f, 0));
            yield return null; yield return null;
            Aimed(GrowthTargetKind.Counter, null, "The reticle focuses the counter");
            int sales = shop.SalesTotal, handed = hands.Handovers;
            Hold(true);
            yield return Wait(() => s.ServingNow && shop.ServeProgress > 0.6f * shop.ServeSeconds, 6, "the bagging to come along");
            Check(hands.RightDoing == "serve" && hands.ServeBagShown && hands.LeftDoing == "carry", "Serving, the right hand holds a bag out over the counter (the left still carrying)");
            yield return Shot("557-serving-at-the-counter");
            yield return Wait(() => shop.SalesTotal > sales, 6, "the sale");
            yield return null;
            Check(hands.Handovers == handed + 1, "The sale is made: the bag is handed over (" + (hands.Handovers - handed) + " handed over for " + (shop.SalesTotal - sales) + " sale)");
            Hold(false); yield return null;
        }
    }
}
#endif
