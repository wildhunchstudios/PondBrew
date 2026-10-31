using System;
using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// R09.4's lessons, GR-26's collection and R10.3's forecast in the interface: the lesson's card (what the system is, then
    /// Show me, its steps on the next-step line with the marker; Skip this lesson), the journal's Guide (every lesson, the ones
    /// met to read again, the rest locked by era) and its Collection (all 72 in their families, found and left), tomorrow's
    /// visitors on the night's receipt and the pinboard, and the marker finding any lesson's target in the rooms.
    /// </summary>
    public sealed partial class GrowthUi
    {
        // ---------------------------------------------------------------- the lesson's card

        /// <summary>R09.4: a lesson's card: its title, what the system is, and Got it (its steps then guide, if it has any); GR-32
        /// (F48): Skip lesson ends it, never asked again.</summary>
        void FillLesson(string id)
        {
            var lesson = GrowthLessons.Get(id);
            if (lesson == null) { s.SetCard(GrowthCard.None); return; }
            var body = Notebook(lesson.Title, "A new part of your shop · Era " + lesson.Era);
            foreach (var paragraph in lesson.Card)
            {
                var p = L(GrowthControls.Keyed(paragraph), "gr-lesson__text");
                body.Add(p);
            }
            if (!lesson.CardOnly)
            {
                var call = Box("gr-callout"); call.style.marginTop = 14;
                call.Add(L("The marker points at what to use, and the line at the bottom says what to do, until you have done it once.", "gr-strong"));
                var skip = Link("Skip lesson", () => { s.Shop.SkipLesson(); s.SaveNow(); s.SetCard(GrowthCard.None); });
                skip.name = "gr-lesson-skip";
                skip.style.alignSelf = Align.FlexStart; skip.style.marginLeft = 0; skip.style.marginTop = 8;
                call.Add(skip);
                body.Add(call);
            }
            var note = L("Every lesson stays in your journal's Guide (" + GrowthControls.NameOf(GrowthAction.Journal) + "). Lessons can be turned off in Settings › Accessibility.", "gr-hint");
            note.style.marginTop = 12;
            body.Add(note);
            NotebookFooter("Got it", () => s.SetCard(GrowthCard.None), "Got it");
        }

        /// <summary>R09.4: the pause screen's line for the lesson whose steps run, with its Skip.</summary>
        void PauseLessonRow(VisualElement body)
        {
            var now = s.Shop.LessonNow == null ? null : GrowthLessons.Get(s.Shop.LessonNow);
            if (now == null || s.Shop.Guide == GrowthGuideMode.On) return;
            var row = Box("gr-row"); row.style.marginTop = 8; row.style.alignItems = Align.Center;
            row.Add(L("Lesson: " + now.Title + " · the marker and the line at the bottom show what to do", "gr-hint"));
            var skip = ChipButton("Skip it", () => { s.Shop.SkipLesson(); s.SaveNow(); }, true); skip.style.marginLeft = 8; skip.name = "gr-pause-skip-lesson";
            row.Add(skip);
            body.Add(row);
        }

        // ---------------------------------------------------------------- the journal's Guide

        /// <summary>The Guide's own page, kept while the journal stays open (the families' pager has its own).</summary>
        int guidePage;

        /// <summary>
        /// R09.4: the journal's Guide: the first night's basics, then every lesson in its era: one met, or learned by using its
        /// system, reads in full; one not reached yet shows its title, locked, and when it comes. A few lessons a page.
        /// </summary>
        void GuideTab(VisualElement tab)
        {
            var shop = s.Shop;
            int known = GrowthLessons.All.Count(l => shop.LessonsMet.Contains(l.Id) || shop.LessonsLearned.Contains(l.Id));
            tab.Add(L("Guide · " + known + " of " + GrowthLessons.All.Length + " lessons" + (shop.LessonsOff ? " · lessons are off (Settings › Accessibility)" : ""), "gr-label"));
            var entries = new List<VisualElement>();
            var basics = Box("gr-lesson");
            basics.Add(L("The first night", "gr-lesson__title"));
            basics.Add(L(GrowthControls.Keyed("Click puts in and takes; E opens a menu. Hold the left mouse on the Bloom and Flow growers to fill your siphon; click the incubator to put your doses in, open it (E) and start; click it again for the hatchling and click a tank in the shop to put it in; open the shop at the counter (E) and hold the mouse on it to bag each sale."), "gr-lesson__text"));
            entries.Add(basics);
            foreach (var l in GrowthLessons.All)
            {
                bool open = shop.LessonsMet.Contains(l.Id) || shop.LessonsLearned.Contains(l.Id);
                var e = Box("gr-lesson");
                e.name = "gr-guide-" + l.Id;
                if (!open) e.AddToClassList("gr-lesson--locked");
                e.Add(L(open ? l.Title : l.Title + " · comes in Era " + l.Era, "gr-lesson__title"));
                if (open) e.Add(L(GrowthControls.Keyed(string.Join(" ", l.Card)), "gr-lesson__text"));
                else e.Add(L(shop.Era >= l.Era ? "Its lesson comes when it first appears in your shop." : "Era " + l.Era + ": " + GrowthContent.Eras[l.Era - 1].Name + ".", "gr-hint"));
                entries.Add(e);
            }
            var pages = new List<VisualElement>();
            for (int i = 0; i < entries.Count; i += GuidePerPage)
            {
                var page = Box("gr-body");
                page.AddToClassList("gr-page--guide");
                foreach (var e in entries.Skip(i).Take(GuidePerPage)) page.Add(e);
                pages.Add(page);
            }
            Pager(tab, pages, "Lessons", () => guidePage, v => guidePage = v);
        }

        /// <summary>The Guide's lessons a page, so a page fits the journal at 1280×720 without scrolling.</summary>
        const int GuidePerPage = 5;

        // ---------------------------------------------------------------- the journal's collection (GR-26)

        /// <summary>
        /// GR-26 (the user's request, "a journal to show what creatures you discovered and how many left"): the whole collection
        /// on one page, all 72 in their nine families: a row per family with its found and left, its eight as small tiles (found
        /// ones in colour with their name, the rest as silhouettes, a family not reached yet locked with the era it comes in), and
        /// the totals at the top: found, left, and colours found.
        /// </summary>
        VisualElement CollectionPage(Func<SpeciesDef, bool> reachable)
        {
            var shop = s.Shop;
            var page = Box("gr-body");
            page.name = "gr-collection";
            int found = shop.Discovered.Count(d => GrowthContent.Get(d) != null);
            var sum = Box("gr-collection__sum");
            sum.Add(Stat(found + " of " + GrowthContent.SpeciesCount, "creatures found"));
            sum.Add(Stat((GrowthContent.SpeciesCount - found).ToString(), found == GrowthContent.SpeciesCount ? "left: you have them all!" : "left to find"));
            sum.Add(Stat(shop.Journal.Count + " of " + GrowthContent.JournalEntries, "colours found"));
            sum.Add(Stat(shop.CrownsDiscovered + " of " + GrowthContent.Families.Length, "crowns"));
            page.Add(sum);
            foreach (var family in GrowthContent.Families)
            {
                var members = GrowthContent.Species.Where(x => x.Family == family.Id).ToList();
                int got = members.Count(m => shop.Discovered.Contains(m.Id));
                int era = family.Era;
                bool locked = got == 0 && shop.Era < era && !members.Any(m => m.Kind == SpeciesKind.Founder && reachable(m));
                var row = Box("gr-crow");
                row.name = "gr-collection-" + family.Id;
                if (locked) row.AddToClassList("gr-crow--locked");
                var head = Box("gr-crow__head");
                head.Add(L(family.Name, "gr-crow__name"));
                head.Add(L(locked ? "comes in Era " + era : got + " of " + members.Count + " · " + (members.Count - got) + " left", "gr-crow__count"));
                row.Add(head);
                foreach (var sp in members)
                {
                    bool known = shop.Discovered.Contains(sp.Id);
                    var tile = Box("gr-ct");
                    if (!known) tile.AddToClassList("gr-ct--unknown");
                    if (sp.Kind == SpeciesKind.Crown) tile.AddToClassList("gr-ct--crown");
                    if (known) tile.Add(CreatureIcon(sp, ColourFound(shop, sp), 40, "gr-ct__glyph"));
                    else { var g = new GrowthGlyph(new Color(0.72f, 0.67f, 0.57f, locked ? 0.3f : 0.55f)); g.AddToClassList("gr-ct__glyph"); tile.Add(g); }
                    var words = Box("gr-ct__words");
                    words.Add(L(known ? sp.Name : "?", "gr-ct__name"));
                    int colours = shop.Journal.Count(j => j.StartsWith(sp.Id + "/", StringComparison.Ordinal));
                    words.Add(L(known ? colours + "/" + GrowthContent.ColourCount + " colours" : sp.Kind == SpeciesKind.Crown ? "crown" : sp.Kind == SpeciesKind.Founder ? "founder" : "hybrid", "gr-ct__hint"));
                    tile.Add(words);
                    tile.tooltip = known ? sp.Name + " · " + sp.Kind.ToString().ToLowerInvariant() + " · " + sp.Price + " cr" :
                        sp.Kind == SpeciesKind.Founder ? "A founder: mixed in the incubator from its cultures" : sp.Kind == SpeciesKind.Crown ? "The family's crown: one special pair of its own" : "A hybrid: bred in the breeding tank";
                    row.Add(tile);
                }
                page.Add(row);
            }
            return page;
        }

        // ---------------------------------------------------------------- tomorrow's forecast (R10.3)

        /// <summary>
        /// R10.3 (AD-E's forecast): tomorrow's first visitors through the door and what they will wish for, as the shop stands
        /// now (what is made tonight can change a wish), most wished first; the night's goal. Nothing in the day.
        /// </summary>
        void ForecastSection(VisualElement body)
        {
            var shop = s.Shop;
            var forecast = shop.Forecast();
            if (forecast.Count == 0) return;
            var box = Box("gr-forecast");
            box.name = "gr-forecast";
            box.Add(L("Tomorrow · the first " + forecast.Count + " visitors through the door", "gr-strong"));
            var types = forecast.GroupBy(f => f.type.Name).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Count() + " " + g.Key + (g.Count() > 1 ? "s" : ""));
            box.Add(L(string.Join(", ", types) + ".", "gr-hint"));
            var wishes = forecast.Where(f => f.wish != null).GroupBy(f => f.wish.Text).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            var line = L(wishes.Count == 0 ? "No wishes yet: they wish for creatures you have found." :
                "They will wish for: " + string.Join(", ", wishes.Select(g => g.Key + (g.Count() > 1 ? " (" + g.Count() + ")" : ""))) + ". Stock them tonight for tips and reputation.", "gr-hint");
            line.name = "gr-forecast-wishes";
            box.Add(line);
            body.Add(box);
        }

        // ---------------------------------------------------------------- the marker's targets

        readonly Dictionary<string, (GrowthTarget target, float until)> lessonTargets = new Dictionary<string, (GrowthTarget, float)>();

        /// <summary>
        /// R09.4: where a lesson's target stands: a GrowthTargetKind's name and, when given, an id ("Place:a2", "Board"); the
        /// nearest such target to the player, a little above its collider. Looked up again every half second (things are built).
        /// </summary>
        Vector3? LessonTargetWorld(string target)
        {
            int colon = target.IndexOf(':');
            string kindName = colon < 0 ? target : target.Substring(0, colon), id = colon < 0 ? null : target.Substring(colon + 1);
            if (!Enum.TryParse(kindName, false, out GrowthTargetKind kind)) return null;
            if (!lessonTargets.TryGetValue(target, out var cached) || cached.target == null || !cached.target.isActiveAndEnabled || Time.unscaledTime > cached.until)
            {
                var from = s.Player != null ? s.Player.transform.position : Vector3.zero;
                var found = UnityEngine.Object.FindObjectsByType<GrowthTarget>(FindObjectsSortMode.None)
                    .Where(t => t.isActiveAndEnabled && t.Kind == kind && (id == null || t.Id == id))
                    .OrderBy(t => (t.transform.position - from).sqrMagnitude).FirstOrDefault();
                cached = (found, Time.unscaledTime + 0.5f);
                lessonTargets[target] = cached;
            }
            if (cached.target == null) return null;
            var col = cached.target.GetComponentInChildren<Collider>();
            if (col == null) return cached.target.transform.position + Vector3.up * 1.2f;
            var b = col.bounds;
            return new Vector3(b.center.x, Mathf.Min(b.max.y, 2.4f) + 0.2f, b.center.z);
        }
    }
}
