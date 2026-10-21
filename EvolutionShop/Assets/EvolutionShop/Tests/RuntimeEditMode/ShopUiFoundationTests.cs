using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop.Tests
{
    // UX-02 foundation: placement math, scaling formula, string table and shipped UI resources.
    public sealed class ShopUiFoundationTests
    {
        static readonly Vector2 Panel = new Vector2(1920, 1080);
        static readonly Vector2 Card = new Vector2(380, 300);

        [Test] public void CardPrefersTheRightSideOfItsAnchor()
        {
            var p = ShopUiLayout.PlaceBeside(new Vector2(800, 500), Card, Panel);
            Assert.That(p.Position.x, Is.EqualTo(800 + ShopUiLayout.DefaultGap).Within(0.01f));
            Assert.That(p.Flipped, Is.False);
            Assert.That(p.CoversAnchor, Is.False);
            Assert.That(p.Position.y, Is.EqualTo(500 - Card.y * 0.5f).Within(0.01f));
        }

        [Test] public void CardFlipsLeftNearTheRightEdge()
        {
            var p = ShopUiLayout.PlaceBeside(new Vector2(1700, 500), Card, Panel);
            Assert.That(p.Flipped, Is.True);
            Assert.That(p.Position.x + Card.x, Is.EqualTo(1700 - ShopUiLayout.DefaultGap).Within(0.01f));
            Assert.That(p.CoversAnchor, Is.False);
        }

        [Test] public void CardStaysInsideThePanelVertically()
        {
            var top = ShopUiLayout.PlaceBeside(new Vector2(600, 10), Card, Panel);
            var bottom = ShopUiLayout.PlaceBeside(new Vector2(600, 1075), Card, Panel);
            Assert.That(top.Position.y, Is.EqualTo(ShopUiLayout.DefaultMargin).Within(0.01f));
            Assert.That(bottom.Position.y + Card.y, Is.EqualTo(Panel.y - ShopUiLayout.DefaultMargin).Within(0.01f));
        }

        [Test] public void HiddenAnchorUsesTheLeftFallbackSlot()
        {
            var p = ShopUiLayout.PlaceBeside(new Vector2(float.NaN, float.NaN), Card, Panel);
            Assert.That(p.AnchorHidden, Is.True);
            Assert.That(p.Position.x, Is.EqualTo(ShopUiLayout.DefaultMargin).Within(0.01f));
            Assert.That(p.Position.y, Is.EqualTo((Panel.y - Card.y) * 0.5f).Within(0.01f));
        }

        [Test] public void TinyPanelClampsAndReportsCoveringTheAnchor()
        {
            var p = ShopUiLayout.PlaceBeside(new Vector2(300, 200), Card, new Vector2(600, 400));
            Assert.That(p.Position.x, Is.GreaterThanOrEqualTo(ShopUiLayout.DefaultMargin));
            Assert.That(p.CoversAnchor, Is.True);
        }

        [Test] public void UnmeasuredCardUsesTheFallbackSize()
        {
            var p = ShopUiLayout.PlaceBeside(new Vector2(1700, 500), new Vector2(float.NaN, 0), Panel);
            Assert.That(p.Flipped, Is.True);
            Assert.That(p.Position.x + ShopUiLayout.FallbackCardSize.x, Is.EqualTo(1700 - ShopUiLayout.DefaultGap).Within(0.01f));
        }

        [TestCase(1280, 720, 1920, 1080)]
        [TestCase(1920, 1080, 1920, 1080)]
        [TestCase(3840, 2160, 1920, 1080)]
        public void SixteenByNineScreensKeepTheReferencePanel(int w, int h, int ew, int eh)
        {
            var size = ShopUiLayout.ExpectedPanelSize(new Vector2(w, h), ShopUiRoot.ReferenceResolution);
            Assert.That(size.x, Is.EqualTo(ew).Within(0.5f));
            Assert.That(size.y, Is.EqualTo(eh).Within(0.5f));
        }

        [Test] public void UltrawideScreenGetsAWiderShorterPanel()
        {
            var size = ShopUiLayout.ExpectedPanelSize(new Vector2(2560, 1080), ShopUiRoot.ReferenceResolution);
            // Measured in the UX-02 run 2 player: 2560x1080 gave a 2194x926 panel (linear blend, not uGUI's log average).
            float scale = Mathf.Lerp(2560f / 1920f, 1f, 0.5f);
            Assert.That(size.x, Is.EqualTo(2560f / scale).Within(0.5f));
            Assert.That(size.y, Is.EqualTo(1080f / scale).Within(0.5f));
            Assert.That(size.x, Is.GreaterThan(1920f));
            Assert.That(size.y, Is.LessThan(1080f));
        }

        [Test] public void StringTableParsesAndShowsMissingKeys()
        {
            var table = ShopUiStrings.Parse("{\"entries\":[{\"key\":\"a.b\",\"text\":\"Hello\"},{\"key\":\"empty\"}]}");
            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.Get("a.b"), Is.EqualTo("Hello"));
            Assert.That(table.Get("empty"), Is.EqualTo(""));
            Assert.That(table.Get("nope"), Is.EqualTo("[nope]"));
            Assert.That(table.Has("nope"), Is.False);
        }

        [Test] public void StringTableRejectsBadInput()
        {
            Assert.Throws<ArgumentException>(() => ShopUiStrings.Parse(" "));
            Assert.Throws<FormatException>(() => ShopUiStrings.Parse("{\"entries\":[]}"));
            Assert.Throws<FormatException>(() => ShopUiStrings.Parse("{\"entries\":[{\"key\":\"\",\"text\":\"x\"}]}"));
            Assert.Throws<FormatException>(() => ShopUiStrings.Parse("{\"entries\":[{\"key\":\"k\",\"text\":\"1\"},{\"key\":\"k\",\"text\":\"2\"}]}"));
        }

        [Test] public void ShippedStringTableCoversTheStationCard()
        {
            var table = ShopUiStrings.LoadDefault();
            foreach (var key in new[] { "card.close", "card.keycap.back", "card.hint" })
                Assert.That(table.Has(key), Is.True, key);
            foreach (ShopPanel panel in Enum.GetValues(typeof(ShopPanel)))
                if (StationCardPresenter.Handles(panel)) Assert.That(table.Has("card.title." + StationCardPresenter.CardPanel(panel)), Is.True, "card.title." + panel);
        }

        [Test] public void UiResourcesImportAndTheCardTemplateHasItsNamedParts()
        {
            Assert.That(Resources.Load<ThemeStyleSheet>(ShopUiRoot.ThemeResource), Is.Not.Null, "theme");
            Assert.That(Resources.Load<StyleSheet>(ShopUiRoot.TokensResource), Is.Not.Null, "tokens");
            var template = Resources.Load<VisualTreeAsset>(ShopUiRoot.StationCardResource);
            Assert.That(template, Is.Not.Null, "station card");
            var tree = template.Instantiate();
            foreach (var name in new[] { StationCardPresenter.CardName, StationCardPresenter.StripName, StationCardPresenter.TitleName,
                StationCardPresenter.StatusName, StationCardPresenter.BodyName, StationCardPresenter.KeycapName, StationCardPresenter.HintName })
                Assert.That(tree.Q<VisualElement>(name), Is.Not.Null, name);
            Assert.That(tree.Q<Button>(StationCardPresenter.CloseName), Is.Not.Null, StationCardPresenter.CloseName);
        }

        [Test] public void StationPanelsAreCardsAndPlanningPanelsAreNot()
        {
            foreach (var panel in new[] { ShopPanel.Incubator, ShopPanel.Tank, ShopPanel.Counter, ShopPanel.Upgrade, ShopPanel.StorageRack, ShopPanel.Conditioning })
                Assert.That(StationCardPresenter.Handles(panel), Is.True, panel.ToString());
            foreach (var panel in new[] { ShopPanel.None, ShopPanel.Welcome, ShopPanel.Journal, ShopPanel.Pause, ShopPanel.DayReport, ShopPanel.Staff, ShopPanel.Automation, ShopPanel.Arrange })
                Assert.That(StationCardPresenter.Handles(panel), Is.False, panel.ToString());
            Assert.That(StationCardPresenter.IsShopPanel(ShopPanel.Counter) && StationCardPresenter.IsShopPanel(ShopPanel.Tank), Is.True);
            Assert.That(StationCardPresenter.IsShopPanel(ShopPanel.Incubator), Is.False);
        }

        [Test] public void FontsShipWithTheirLicences()
        {
            foreach (var file in new[] { "Baloo2-ExtraBold.ttf", "Nunito-SemiBold.ttf", "Nunito-ExtraBold.ttf", "OFL-Baloo2.txt", "OFL-Nunito.txt", "PROVENANCE.md" })
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "EvolutionShop/UI/Fonts", file)), Is.True, file);
        }
    }
}
