using System.Collections.Generic;
using System.Linq;
using EvolutionShop.Growth;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    /// <summary>GR-20: the wish bubble. The people kit has the bubble and what shows in it; each kind of wish shows its own
    /// content (the creature itself, a token in the colour with its sign, a token as big as the size with its pips, two tokens,
    /// three), every part inside the cloud and in front of it; the bubble floats above the visitor's head, pops in, pops into a
    /// heart when a sale grants the wish and shrinks away otherwise.</summary>
    public sealed class GrowthWishBubbleTests
    {
        readonly List<GameObject> made = new List<GameObject>();

        [TearDown] public void CleanUp() { foreach (var g in made) if (g) Object.DestroyImmediate(g); made.Clear(); }

        GrowthVisitorView Visitor(GrowthWish wish, int id)
        {
            var parent = new GameObject("Wish test"); made.Add(parent);
            var v = new GrowthVisitor { Id = id, Type = "casual", State = GrowthVisitorState.Browsing, Wish = wish };
            var view = GrowthVisitorView.Create(v, parent.transform);
            view.Follow(v, new GrowthShop(), -1);
            Assert.IsNotNull(view.Person, "a person");
            Assert.IsNotNull(view.Bubble, "a bubble for a wish");
            return view;
        }

        static Bounds Bounds(Transform t, System.Func<Renderer, bool> which = null)
        {
            var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && (which == null || which(r))).ToList();
            Assert.That(rs, Is.Not.Empty, t.name + " draws something");
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [Test]
        public void TheKitHasTheBubbleAndWhatShowsInIt()
        {
            var wish = GrowthPeopleKit.Data?.wish;
            Assert.IsNotNull(wish, "people.json records the wish bubble");
            Assert.That(wish.parts, Is.EquivalentTo(new[] { "wish__bubble", "wish__drop", "wish__fish-deep", "wish__fish-plain", "wish__fish-shimmer", "wish__fish-sunny", "wish__heart", "wish__pip", "wish__sparkle", "wish__sun" }));
            Assert.That(wish.front, Is.GreaterThan(0.04f).And.LessThan(0.1f));
            foreach (var part in wish.parts)
            {
                var holder = new GameObject("holder"); made.Add(holder);
                Assert.IsNotNull(GrowthPeopleKit.Prop(GrowthWishBubble.Asset, part, holder.transform), part);
                Assert.That(holder.GetComponentsInChildren<Collider>(), Is.Empty, part + " has no collider");
            }
        }

        [Test]
        public void EachKindOfWishShowsItsOwnContentInsideTheCloud()
        {
            var cases = new (GrowthWish wish, string content, int tokens, int pips)[]
            {
                (new GrowthWish { Kind = WishKind.Species, Species = "pondlet" }, "species:pondlet", 0, 0),
                (new GrowthWish { Kind = WishKind.Species, Species = "shellback" }, "species:shellback", 0, 0),
                (new GrowthWish { Kind = WishKind.Colour, Colour = Colour.Sunny }, "colour:Sunny", 1, 0),
                (new GrowthWish { Kind = WishKind.Colour, Colour = Colour.Deep }, "colour:Deep", 1, 0),
                (new GrowthWish { Kind = WishKind.Colour, Colour = Colour.Shimmer }, "colour:Shimmer", 1, 0),
                (new GrowthWish { Kind = WishKind.Size, Size = SizeClass.Small }, "size:Small", 1, 1),
                (new GrowthWish { Kind = WishKind.Size, Size = SizeClass.Medium }, "size:Medium", 1, 2),
                (new GrowthWish { Kind = WishKind.Size, Size = SizeClass.Large }, "size:Large", 1, 4),
                (new GrowthWish { Kind = WishKind.Pair }, "pair", 2, 0),
                (new GrowthWish { Kind = WishKind.School }, "school", 3, 0),
            };
            int id = 300;
            foreach (var (wish, content, tokens, pips) in cases)
            {
                var view = Visitor(wish, id++);
                var b = view.Bubble;
                for (int i = 0; i < 20; i++) view.Tick(0.05f, true);
                Assert.That((b.Content, b.Tokens, b.Pips), Is.EqualTo((content, tokens, pips)), wish.Text);
                Assert.That(b.Shown && Mathf.Approximately(b.Scale, 1), Is.True, wish.Text + " popped in");
                if (wish.Kind == WishKind.Species) Assert.That(b.Creature, Is.EqualTo(wish.Species));
                if (wish.Kind == WishKind.Colour) Assert.That(b.Sign, Is.EqualTo(wish.Colour == Colour.Sunny ? "wish__sun" : wish.Colour == Colour.Deep ? "wish__drop" : "wish__sparkle"));
                // Every icon inside the cloud and in front of it, as seen from in front.
                var bubble = b.BubbleTransform;
                bubble.rotation = Quaternion.identity;
                var cloud = Bounds(bubble.Find("Bubble") ?? bubble, r => r.transform.IsChildOf(bubble) && !r.transform.IsChildOf(bubble.Find("Content")));
                var icons = Bounds(bubble.Find("Content"));
                Assert.That(icons.min.x >= cloud.min.x + 0.02f && icons.max.x <= cloud.max.x - 0.02f, Is.True, wish.Text + ": across, inside the cloud (" + icons + " in " + cloud + ")");
                Assert.That(icons.min.y >= cloud.min.y + 0.02f && icons.max.y <= cloud.max.y - 0.02f, Is.True, wish.Text + ": up and down, inside the cloud (" + icons + " in " + cloud + ")");
                Assert.That(icons.max.z, Is.GreaterThan(cloud.max.z - 0.005f), wish.Text + ": in front of the cloud");
                // Above the head, beside it.
                var head = view.Person.Joint("head").position.y;
                Assert.That(cloud.min.y, Is.GreaterThan(head + view.Person.HeadHeight * 0.6f), wish.Text + ": above the head");
            }
        }

        [Test]
        public void ItPopsIntoAHeartWhenGrantedAndShrinksAwayOtherwise()
        {
            var wish = new GrowthWish { Kind = WishKind.Species, Species = "pondlet" };
            var granted = Visitor(wish, 401);
            var other = Visitor(wish, 402);
            Assert.That(granted.Bubble.Shown, Is.False, "not yet popped in");
            for (int i = 0; i < 4; i++) { granted.Tick(0.05f, true); other.Tick(0.05f, true); }
            Assert.That(granted.Bubble.Scale, Is.GreaterThan(0.75f).And.LessThanOrEqualTo(1f), "pops in within a quarter second");
            var g = new GrowthVisitor { Id = 401, Type = "casual", State = GrowthVisitorState.Leaving, Outcome = GrowthVisitOutcome.Bought, Wish = wish, WishGranted = true };
            var o = new GrowthVisitor { Id = 402, Type = "casual", State = GrowthVisitorState.Leaving, Outcome = GrowthVisitOutcome.Bought, Wish = wish, WishMissed = true };
            granted.Follow(g, new GrowthShop(), -1); other.Follow(o, new GrowthShop(), -1);
            granted.Tick(0.05f, true); other.Tick(0.05f, true);
            Assert.That(granted.Bubble.HeartShown && granted.Bubble.Content == "heart", Is.True, "a granted wish pops into a heart");
            Assert.That(other.Bubble.HeartShown, Is.False);
            for (int i = 0; i < 10; i++) { granted.Tick(0.05f, true); other.Tick(0.05f, true); }
            Assert.That(other.Bubble.Shown, Is.False, "shrunk away as they leave");
            Assert.That(granted.Bubble.Shown, Is.False, "the cloud gone behind the heart");
            Assert.That(granted.Bubble.HeartShown, Is.True, "the heart still rising");
            for (int i = 0; i < 20; i++) granted.Tick(0.05f, true);
            Assert.That(granted.Bubble.HeartShown, Is.False, "and gone after a second");
            // A visitor without a wish has no bubble.
            var parent = new GameObject("No wish"); made.Add(parent);
            var none = GrowthVisitorView.Create(new GrowthVisitor { Id = 403, Type = "bargain", State = GrowthVisitorState.Browsing }, parent.transform);
            none.Follow(new GrowthVisitor { Id = 403, Type = "bargain", State = GrowthVisitorState.Browsing }, new GrowthShop(), -1);
            Assert.That(none.Bubble, Is.Null);
        }
    }
}
