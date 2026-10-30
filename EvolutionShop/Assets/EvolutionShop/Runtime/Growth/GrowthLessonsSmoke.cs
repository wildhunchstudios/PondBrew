#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// R09.4's, R10.3's and GR-26's Windows route (-evolutionGrowthLessonsSmoke): the tutorial for every part of the game, the
    /// forecast of tomorrow's visitors and the journal's collection. From a shop past its guided first night, every lesson in
    /// its order: staged where its system appears (GrowthLessonWalk, labelled), its card opening by itself once the shop is
    /// free, its words; Show me, the next-step line saying its step and the marker over the thing to use, then the system used
    /// once and the note that the lesson is learned; Got it for a card only; one lesson skipped from its card and one from the
    /// pause screen; the setting switching lessons off and on. Between, the night's receipt and the pinboard forecast tomorrow's
    /// first 8 visitors and their wishes, and the next day's first 8 are those. The journal: found and left at the top, the
    /// Collection page of all 72 in their families (locked ones in era 1, every family at the end), the Guide of every lesson
    /// met. Save and reload keep it all.
    /// </summary>
    public sealed partial class GrowthSmoke
    {
        const string LessonsScenarioText = "R09.4 lessons for every part of the game, R10.3 tomorrow's forecast and GR-26 the journal's collection: from a shop past its first night, each of the 36 lessons in its order staged where its system appears, its card opening by itself, Show me with the next-step line and the marker over the thing to use, the system used and the lesson learned; Got it for a card only; skipped from a card and from the pause screen; the setting off and on; the night's receipt and the pinboard forecasting tomorrow's first 8 visitors and their wishes, and the next day's first 8 those; the journal's found and left, its Collection of all 72 and its Guide; saved and reloaded";

        int lessonShot = 900;

        /// <summary>Stands where the eye sees a marker's point unblocked, 1.8 to 3.2 m from it, looking at the thing under it.
        /// False (the view as it was) when no such place is found.</summary>
        bool ViewOfTarget(Vector3 point)
        {
            foreach (float d in new[] { 2.4f, 1.8f, 3.2f })
                for (int a = 0; a < 16; a++)
                {
                    float angle = a * 22.5f * Mathf.Deg2Rad;
                    var feet = new Vector3(point.x + Mathf.Sin(angle) * d, 0, point.z + Mathf.Cos(angle) * d);
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.7f, 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 eye = feet + Vector3.up * 1.65f, to = point - eye;
                    // Clear both ways (a wall's far side has no face for a ray from behind it), and the thing under the point too.
                    if (Physics.Raycast(eye, to.normalized, to.magnitude - 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Raycast(point, -to.normalized, to.magnitude - 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    // Inside the rooms: walls within reach on at least three sides (the rooms are closed boxes).
                    int walls = 0;
                    foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                        if (Physics.Raycast(eye, dir, 14f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) walls++;
                    if (walls < 3) continue;
                    LookAt(feet, point + Vector3.down * 0.45f);
                    return true;
                }
            return false;
        }

        /// <summary>Clicks a tab of the open notebook card by its name.</summary>
        IEnumerator Tab(string name) => ClickOn(e => e is Button b && b.text == name && (b.ClassListContains("gr-nb-tab") || b.ClassListContains("gr-tab")), "Click the " + name + " tab");

        /// <summary>Whether an element is inside one named so (a tab's body, gr-tab-guide).</summary>
        static bool Within(VisualElement e, string name)
        {
            for (var p = e.parent; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        VisualElement CardPart(string name) => s.Ui.CardElement == null ? null : s.Ui.CardElement.Q<VisualElement>(name);

        /// <summary>Scrolls the open card to one of its parts (for its capture).</summary>
        IEnumerator ShowPart(string name)
        {
            var part = CardPart(name);
            var scroll = part?.GetFirstAncestorOfType<ScrollView>();
            if (scroll != null) { scroll.ScrollTo(part); yield return null; yield return null; }
        }

        IEnumerator LessonsScenario()
        {
            limitations.Add(GrowthLessonWalk.Labelled);
            limitations.Add("R09.4 lessons route: lessons switched on for this route (GrowthSession.LessonsOnInValidation; every other route keeps them off, so their cards and captures stay as they were). Each lesson's card opens by itself, as in play, once the shop has been free of cards for 1.5 s; its Show me, Got it and Skip this lesson, the pause screen's Skip it, the journal's and the pinboard's tabs and the Settings switch are clicked with the route's mouse. The route stands where the marker's thing is in view (a point found by the route's own line-of-sight search) for each marker's capture. Each system is then used once with the shop's own action, as the card's button or E on the thing would (GrowthLessonWalk.Act); the pinboard and the journal are opened as their keys do. Days run on the shop's clock, accelerated where the route advances it, the queue served by the shop's Serve as a held mouse would.");
            yield return Opening();
            TakeInput();
            var shop = s.Shop;
            yield return new WaitForSeconds(GrowthSession.LessonPause + 0.5f);
            Check(shop.LessonsMet.Count == 0 && s.Card == GrowthCard.None, "Another route's shop meets no lessons: they stay off in validation unless a route asks");

            GrowthLessonWalk.Prepare(shop);
            s.LessonsOnInValidation = true;
            Stand(GrowthWorld.Spawn, -90, 8);
            Check(shop.Guide == GrowthGuideMode.Done && shop.Day == 2 && shop.Phase == GrowthPhase.Night, "Fixture: a shop past its guided first night, day 2, at night");

            var all = GrowthLessons.All;
            for (int i = 0; i < all.Length; i++)
            {
                var l = all[i];
                string staged = GrowthLessonWalk.Stage(shop, l.Id, s.Advance);
                Check(staged == null, "Staged where it appears: " + l.Title + (staged == null ? "" : " / " + staged));
                if (s.Card == GrowthCard.Receipt) s.SetCard(GrowthCard.None);

                // The setting switches lessons off and on (with the crowns' lesson due).
                if (l.Id == "crowns") yield return LessonsSwitch();

                yield return Wait(() => s.Card == GrowthCard.Lesson, 15, "the " + l.Title + " lesson's card to open by itself");
                yield return null; yield return null;
                string text = s.Ui.CardText();
                Check(s.CardTarget == l.Id && shop.LessonsMet.Contains(l.Id) && text.Contains(l.Title) && l.Card.All(p => text.Contains(GrowthControls.Keyed(p))) &&
                      text.Contains(l.CardOnly ? "Got it" : "Show me") && (l.CardOnly || text.Contains("Skip this lesson")) && text.Contains("journal's Guide"),
                    (i + 1) + "/" + all.Length + " " + l.Title + ": its card opens by itself " + (l.AnyTime && shop.Phase == GrowthPhase.Day ? "in the day, no one waiting at the counter" : "at night") + ", saying what it is" + (l.CardOnly ? "" : ", with Show me and Skip this lesson"));
                yield return Shot(lessonShot++ + "-lesson-" + l.Id);

                if (l.CardOnly)
                {
                    yield return ClickOn(e => ButtonText(e, "Got it"), "Click Got it on " + l.Title);
                    yield return null;
                    Check(s.Card == GrowthCard.None && shop.LessonsLearned.Contains(l.Id) && shop.LessonNow == null, l.Title + ": a card only, learned by reading it");
                    // R10.3: after the day's lessons, the night's forecast and the day it forecast.
                    if (l.Id == "days") yield return ForecastDays();
                    continue;
                }

                // Skipped from its card: never asked again, nothing changes.
                if (l.Id == "tank-levels")
                {
                    var levels = shop.Containers.Select(c => c.Level).ToList();
                    yield return ClickOn(e => e.name == "gr-lesson-skip", "Click Skip this lesson on " + l.Title);
                    yield return null;
                    Check(s.Card == GrowthCard.None && shop.LessonNow == null && shop.LessonsLearned.Contains(l.Id) && shop.Containers.Select(c => c.Level).SequenceEqual(levels) && shop.GuideNow() == null,
                        l.Title + ": skipped from its card, it ends (never asked again) and nothing else changes");
                    continue;
                }

                yield return ClickOn(e => ButtonText(e, "Show me"), "Click Show me on " + l.Title);
                yield return Wait(() => s.Card == GrowthCard.None, 3, "the card to close");
                var step = shop.GuideNow();
                Check(step != null && shop.LessonNow == l.Id && step.Text == l.Step(shop).Text, l.Title + ": its step runs (" + step?.Text + ")");
                yield return Wait(() => s.Ui.HudNext == GrowthControls.Keyed(step.Text), 3, "the next-step line to say " + l.Title + "'s step");
                Check(true, l.Title + ": the next-step line says \"" + s.Ui.HudNext + "\"");
                if (step.Target != null)
                {
                    var point = s.Ui.GuidePointOf(step.Target);
                    Check(point != null, l.Title + ": the marker finds the " + step.TargetName + " in the rooms (" + step.Target + ")");
                    bool viewed = ViewOfTarget(point.Value);
                    yield return null; yield return null; yield return null;
                    Check(s.Ui.GuideTarget == step.Target && (!viewed || !s.Ui.GuideAtEdge),
                        l.Title + ": the marker " + (viewed ? "stands over" : "points the way to") + " the " + step.TargetName + " (" + s.Ui.GuideTarget + ", \"" + s.Ui.GuideLabel + "\")");
                    yield return Shot(lessonShot++ + "-marker-" + l.Id);
                }
                else yield return Shot(lessonShot++ + "-step-" + l.Id);

                // Skipped from the pause screen: the line for the lesson whose steps run, and its Skip it.
                if (l.Id == "decor")
                {
                    s.SetCard(GrowthCard.Pause);
                    yield return null; yield return null;
                    Check(s.Ui.CardText().Contains("Lesson: " + l.Title), "The pause screen names the lesson whose steps run, with Skip it");
                    yield return Shot(lessonShot++ + "-pause-lesson");
                    yield return ClickOn(e => e.name == "gr-pause-skip-lesson", "Click Skip it on the pause screen");
                    Check(shop.LessonNow == null && shop.LessonsLearned.Contains(l.Id) && shop.GuideNow() == null, l.Title + ": skipped from the pause screen, its step ends");
                    s.SetCard(GrowthCard.None);
                    continue;
                }

                int notes = s.Ui.Log.Count;
                if (l.Id == "goals") yield return Pinboard();
                else if (l.Id == "journal") yield return Journal(true);
                else
                {
                    string acted = GrowthLessonWalk.Act(shop, l.Id, s.Advance);
                    Check(acted == null, l.Title + ": the system used once" + (acted == null ? "" : " / " + acted));
                }
                s.Advance(0.02f);
                Check(shop.LessonsLearned.Contains(l.Id) && shop.LessonNow == null && s.Ui.Log.Skip(notes).Any(n => n == "Lesson learned: " + l.Title + ". It's in your journal's Guide."),
                    l.Title + ": used once, the lesson is learned and a note says so");
                if (i == 0) yield return Shot(lessonShot++ + "-lesson-learned-note");

            }

            Check(shop.LessonsMet.Count == all.Length && shop.LessonsLearned.Count == all.Length && shop.LessonDue() == null, "Every one of the " + all.Length + " lessons met and learned");
            yield return Journal(false);

            // Saved and reloaded: the lessons, the notes and the collection kept.
            var met = shop.LessonsMet.ToList(); var noted = shop.Noted.ToList(); int found = shop.Discovered.Count;
            s.SaveNow();
            Check(s.ReloadSaved(), "Reload the saved game");
            shop = s.Shop;
            yield return null; yield return null;
            Check(shop.LessonsMet.SetEquals(met) && shop.LessonsLearned.Count == all.Length && shop.Noted.SetEquals(noted) && shop.LessonNow == null && shop.Discovered.Count == found,
                "Saved and reloaded: every lesson met and learned, what the lessons read (" + string.Join(", ", noted.OrderBy(n => n)) + ") and the collection kept");
            yield return new WaitForSeconds(GrowthSession.LessonPause + 0.5f);
            Check(s.Card == GrowthCard.None, "No lesson opens again after the reload");
        }

        /// <summary>R09.4: the Settings switch for lessons (Accessibility): off, the due lesson's card waits; on, it opens.</summary>
        IEnumerator LessonsSwitch()
        {
            var shop = s.Shop;
            var due = shop.LessonDue();
            s.SetCard(GrowthCard.Settings);
            yield return Tab("Accessibility");
            yield return ClickOn(e => e.name == "gr-switch-lessons" && Visible(e), "Click the Lessons switch off in Settings › Accessibility");
            Check(s.Settings.lessonsOff && s.Ui.CardText().Contains("Lessons"), "Settings › Accessibility: Lessons off");
            yield return Shot(lessonShot++ + "-settings-lessons-off");
            s.SetCard(GrowthCard.None);
            yield return new WaitForSeconds(GrowthSession.LessonPause + 2f);
            Check(due != null && s.Card == GrowthCard.None && shop.LessonDue() == null && shop.LessonsOff, "Lessons off: the " + due?.Title + " lesson's card does not open");
            s.SetCard(GrowthCard.Settings);
            yield return Tab("Accessibility");
            yield return ClickOn(e => e.name == "gr-switch-lessons" && Visible(e), "Click the Lessons switch on again");
            Check(!s.Settings.lessonsOff, "Lessons on again");
            s.SetCard(GrowthCard.None);
        }

        /// <summary>The goals lesson: the pinboard opened (as E on it), its Tomorrow tab at night forecasting the first visitors.</summary>
        IEnumerator Pinboard()
        {
            var shop = s.Shop;
            s.SetCard(GrowthCard.Board);
            yield return null; yield return null;
            Check(shop.Noted.Contains("board"), "The pinboard opened: noted");
            yield return Tab("Tomorrow");
            Check(CardPart("gr-forecast") != null && s.Ui.CardText().Contains("Tomorrow · the first " + GrowthShop.ForecastVisitors + " visitors through the door"),
                "At night the pinboard's Tomorrow tab forecasts the first " + GrowthShop.ForecastVisitors + " visitors and their wishes");
            yield return Shot(lessonShot++ + "-board-tomorrow");
            s.SetCard(GrowthCard.None);
        }

        /// <summary>The journal: found and left, the Collection page and the Guide (in era 1, families not reached locked; at the
        /// end, every lesson met in the Guide).</summary>
        IEnumerator Journal(bool early)
        {
            var shop = s.Shop;
            s.SetCard(GrowthCard.Journal);
            yield return null; yield return null;
            int found = shop.Discovered.Count(d => GrowthContent.Get(d) != null);
            string text = s.Ui.CardText();
            Check(text.Contains("Found " + found + " of " + GrowthContent.SpeciesCount + " creatures · " + (GrowthContent.SpeciesCount - found) + " left"),
                "The journal says how many creatures are found and how many are left (" + found + " of " + GrowthContent.SpeciesCount + ")");
            if (early) Check(shop.Noted.Contains("journal"), "The journal opened: noted");
            yield return Tab("Creatures");
            var collection = CardPart("gr-collection");
            text = s.Ui.CardText();
            var rows = collection == null ? new List<VisualElement>() : collection.Query<VisualElement>().Where(v => v.name != null && v.name.StartsWith("gr-collection-")).ToList();
            int tiles = collection == null ? 0 : collection.Query<VisualElement>(className: "gr-ct").ToList().Count;
            int locked = rows.Count(r => r.ClassListContains("gr-crow--locked"));
            Check(collection != null && rows.Count == GrowthContent.Families.Length && tiles == GrowthContent.SpeciesCount && text.Contains(found + " of " + GrowthContent.SpeciesCount + " | creatures found") &&
                  text.Contains((GrowthContent.SpeciesCount - found) + " | left to find"),
                "The Collection page: all " + GrowthContent.SpeciesCount + " creatures in their " + GrowthContent.Families.Length + " families, " + found + " found and " + (GrowthContent.SpeciesCount - found) + " left");
            if (early) Check(locked > 0 && text.Contains("comes in era"), "Families not reached yet are locked, each saying the era it comes in (" + locked + ")");
            else Check(locked == 0, "In era 5 no family is locked");
            yield return Shot(lessonShot++ + "-journal-collection" + (early ? "" : "-end"));
            yield return Tab("Guide");
            text = s.Ui.CardText();
            Check(text.Contains("Guide · " + shop.LessonsMet.Count + " of " + GrowthLessons.All.Length + " lessons") && text.Contains("The first night") &&
                  shop.LessonsMet.Where(id => GrowthLessons.Get(id).Era == 1).All(id => text.Contains(GrowthLessons.Get(id).Title)) && (early ? text.Contains("comes in era") : true),
                "The journal's Guide: " + shop.LessonsMet.Count + " of " + GrowthLessons.All.Length + " lessons met, each to read again" + (early ? ", the rest locked with their era" : ""));
            yield return Shot(lessonShot++ + "-journal-guide" + (early ? "" : "-end"));
            if (!early)
            {
                yield return ClickOn(e => e is Button b && b.text == "▶" && Visible(e) && Within(e, "gr-tab-guide"), "Click the Guide's next page");
                yield return null;
                text = s.Ui.CardText();
                var second = GrowthLessons.All.Skip(5).First();
                Check(text.Contains(second.Title), "The Guide's second page: " + second.Title + " and the lessons after it");
                yield return Shot(lessonShot++ + "-journal-guide-page-2");
            }
            s.SetCard(GrowthCard.None);
        }

        /// <summary>R10.3: the night's receipt and the pinboard forecast tomorrow's first visitors and their wishes; the next day's
        /// first 8 visitors are those.</summary>
        IEnumerator ForecastDays()
        {
            var shop = s.Shop;
            string closed = GrowthLessonWalk.ToNight(shop, s.Advance);
            Check(closed == null, "The day closed (the shop's Close for the day) and run to its night" + (closed == null ? "" : " / " + closed));
            yield return Wait(() => s.Card == GrowthCard.Receipt, 5, "the receipt");
            yield return null; yield return null;
            var forecast = shop.Forecast();
            string text = s.Ui.CardText();
            var types = forecast.GroupBy(f => f.type.Name).Select(g => g.Count() + " " + g.Key + (g.Count() > 1 ? "s" : "")).ToList();
            var wished = forecast.Where(f => f.wish != null).Select(f => f.wish.Text).Distinct().ToList();
            Check(forecast.Count == GrowthShop.ForecastVisitors && CardPart("gr-forecast") != null && text.Contains("Tomorrow · the first " + GrowthShop.ForecastVisitors + " visitors through the door") &&
                  types.All(t => text.Contains(t)) && wished.All(w => text.Contains(w)),
                "The night's receipt forecasts tomorrow: " + string.Join(", ", types) + "; wishing for " + string.Join(", ", wished));
            yield return ShowPart("gr-forecast");
            yield return Shot(lessonShot++ + "-receipt-forecast");
            s.SetCard(GrowthCard.None);

            // The next day's first 8 visitors (the shop's own) are those forecast, each with the wish forecast. An empty shop draws
            // nobody in, so the tanks are kept stocked through the day (development stock, labelled), as Nori would.
            limitations.Add("R10.3 forecast day: the shop's tanks kept full of Pondlets through the day (GrowthShop.DevelopmentStock, labelled), as Nori would, so its visitors keep coming: an empty shop draws nobody in.");
            void KeepStocked() { foreach (var c in shop.ShopContainers.ToList()) while (c.Free > 0 && shop.DevelopmentStock(c.Id, "pondlet").Success) { } }
            KeepStocked();
            // The visitors still walking out from the day before are not tomorrow's.
            var already = shop.Visitors.Select(v => v.Id).ToList();
            s.SetCard(GrowthCard.Counter);
            yield return ClickOn(e => ButtonText(e, "Open the shop"), "Click Open the shop on the counter card");
            Check(shop.Phase == GrowthPhase.Day && shop.Forecast().Count == 0, "The shop opens; no forecast in the day");
            var came = new List<GrowthVisitor>(); var seen = new HashSet<int>(already);
            for (int n = 0; n < 6000 && came.Count < forecast.Count && shop.Phase == GrowthPhase.Day; n++)
            {
                s.Advance(0.1f);
                foreach (var v in shop.Visitors.Where(v => v.Type != GrowthShop.GuestType).OrderBy(v => v.Id)) if (seen.Add(v.Id)) came.Add(v);
                if (shop.FrontOfQueue != null) shop.Serve(shop.ServeSeconds);
                if (n % 10 == 0) { KeepStocked(); yield return null; }
            }
            bool same = came.Count >= forecast.Count && Enumerable.Range(0, forecast.Count).All(n => came[n].Type == forecast[n].type.Id && came[n].Wish?.Text == forecast[n].wish?.Text);
            Check(same, "The next day's first " + forecast.Count + " visitors are those forecast, each with the wish forecast (" +
                string.Join("; ", came.Take(forecast.Count).Select(v => GrowthShop.TypeName(v.Type) + ": " + (v.Wish?.Text ?? "no wish"))) + ")");
            closed = GrowthLessonWalk.ToNight(shop, s.Advance);
            Check(closed == null, "The forecast day closed and run to its night");
            yield return Wait(() => s.Card == GrowthCard.Receipt, 5, "the forecast day's receipt");
            s.SetCard(GrowthCard.None);
        }
    }
}
#endif
