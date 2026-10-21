using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    // UX-09: the IMGUI path is gone. Nothing in the game draws with IMGUI, every panel the session can open has a UI Toolkit
    // view, the code-drawn icons the toolkit shows have an owner of their own, and the strings no longer mention the
    // classic UI or its F9 toggle.
    public sealed class ImguiRemovalTests
    {
        static readonly string Assets = Path.Combine(Application.dataPath, "EvolutionShop");

        [Test] public void NoMonoBehaviourDrawsWithImmediateModeGui()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var drawing = typeof(ShopSession).Assembly.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && t.GetMethod("OnGUI", flags) != null).Select(t => t.Name).ToArray();
            Assert.That(drawing, Is.Empty, "OnGUI in " + string.Join(", ", drawing));
        }

        [Test] public void GameSourcesUseNoImmediateModeGui()
        {
            var imgui = new Regex(@"\bGUI\.|\bGUILayout\b|\bGUILayoutUtility\b|\bGUIStyle\b|\bGUIContent\b|\bGUISkin\b|\bGUIUtility\b|\bEvent\.current\b|\bOnGUI\b");
            // Code only: comments and string literals (the routes' own "no OnGUI" checks name it) are left out.
            var literal = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
            var found = new[] { "Runtime", "Presentation", "Core" }
                .SelectMany(folder => Directory.GetFiles(Path.Combine(Assets, folder), "*.cs", SearchOption.AllDirectories))
                .SelectMany(file => File.ReadAllLines(file).Select((line, i) => (file, line, i)))
                .Where(x => !x.line.TrimStart().StartsWith("//") && imgui.IsMatch(literal.Replace(x.line, "\"\"")))
                .Select(x => Path.GetFileName(x.file) + ":" + (x.i + 1) + " " + x.line.Trim()).ToArray();
            Assert.That(found, Is.Empty, string.Join("\n", found));
        }

        [Test] public void EveryPanelHasExactlyOneToolkitView()
        {
            foreach (ShopPanel panel in Enum.GetValues(typeof(ShopPanel)))
            {
                if (panel == ShopPanel.None) continue;
                int owners = (StationCardPresenter.Handles(panel) ? 1 : 0) + (PlanningViewPresenter.Handles(panel) ? 1 : 0) +
                    (ScreenPresenter.Handles(panel) ? 1 : 0) + (StudyPresenter.Handles(panel) ? 1 : 0);
                Assert.That(owners, Is.EqualTo(1), panel + " is drawn by " + owners + " toolkit presenters");
            }
        }

        [Test] public void ClosingEarlyIsTheCounterCardsConfirmStep()
        {
            Assert.That(StationCardPresenter.Handles(ShopPanel.CloseShop), Is.True);
            Assert.That(StationCardPresenter.CardPanel(ShopPanel.CloseShop), Is.EqualTo(ShopPanel.Counter));
            foreach (ShopPanel panel in Enum.GetValues(typeof(ShopPanel)))
                if (panel != ShopPanel.CloseShop) Assert.That(StationCardPresenter.CardPanel(panel), Is.EqualTo(panel), panel.ToString());
        }

        [Test] public void TheIconsHaveAnOwnerOfTheirOwn()
        {
            // The toolkit draws the code-made icons; since UX-09 they no longer come from an IMGUI skin (ShopUiTheme is internal).
            var theme = typeof(ShopSession).Assembly.GetType("EvolutionShop.ShopUiTheme", true);
            Assert.That(theme.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null), Is.Not.Null,
                "the theme is made without a GUISkin");
            Assert.That(theme.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Any(f => f.FieldType.Name.StartsWith("GUI")), Is.False,
                "no GUISkin or GUIStyle fields");
            var instance = Activator.CreateInstance(theme, true);
            var symbol = theme.GetNestedType("Symbol", BindingFlags.NonPublic | BindingFlags.Public);
            var icon = theme.GetMethod("IconTexture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            try
            {
                foreach (var value in Enum.GetValues(symbol))
                {
                    var texture = icon.Invoke(instance, new[] { value }) as Texture2D;
                    Assert.That(texture, Is.Not.Null, value.ToString());
                    Assert.That(texture.width == 64 && texture.height == 64, Is.True, value.ToString());
                    // Uploaded and released on the CPU side (Apply(false, true)), as the IMGUI theme did: the GPU keeps the only copy.
                    Assert.That(texture.isReadable, Is.False, value + " keeps no CPU copy");
                    Assert.That(texture.name, Does.EndWith(value.ToString()), value.ToString());
                }
            }
            finally { ((IDisposable)instance).Dispose(); }
        }

        [Test] public void TheUiRootOwnsTheIconsAndHasNoPreviewToggle()
        {
            Assert.That(typeof(ShopUiRoot).GetProperty("Icons", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), Is.Not.Null);
            Assert.That(typeof(ShopUiRoot).GetMethod("SetActive", BindingFlags.Instance | BindingFlags.Public), Is.Null, "no preview switch on the root");
            Assert.That(typeof(ShopSession).GetMethod("SetToolkitUiActive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null,
                "no preview switch on the session");
            string root = File.ReadAllText(Path.Combine(Assets, "Runtime/UI/ShopUiRoot.cs"));
            Assert.That(root.Contains("f9Key"), Is.False, "F9 no longer toggles a classic UI");
        }

        static ShopUiStrings Table() => ShopUiStrings.LoadDefault();

        [Test] public void TheStudiesHaveTheirWordsAndFollowTheCopyRules()
        {
            var s = Table();
            for (int i = 0; i < ShopSession.PresentationViews; i++)
            {
                Assert.That(s.Has("study.title." + i) && s.Has("study.note." + i), Is.True, "study " + i);
                Assert.That(s.Get("study.title." + i).Contains(" / "), Is.False, "study title " + i);
            }
            foreach (CharacterSamplePose pose in Enum.GetValues(typeof(CharacterSamplePose))) Assert.That(s.Has("study.pose." + pose), Is.True, pose.ToString());
            foreach (ComedyKind kind in Enum.GetValues(typeof(ComedyKind))) Assert.That(s.Has("study.comedy." + kind), Is.True, kind.ToString());
            foreach (var key in new[] { "study.freeze", "study.play", "study.previous", "study.next", "study.back", "study.comedy.skip", "study.rotate" })
            {
                string text = s.Get(key);
                int words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.That(words, Is.InRange(1, 4), key + ": " + text);
                Assert.That(text.Contains(" / "), Is.False, key);
            }
        }

        [Test] public void NoStringMentionsTheClassicUi()
        {
            string json = File.ReadAllText(Path.Combine(Assets, "Resources/EvolutionShopUI/strings.en.json"));
            Assert.That(json.Contains("F9"), Is.False, "F9 toggled the classic UI, which is gone");
            Assert.That(Regex.IsMatch(json, @"\bclassic\b", RegexOptions.IgnoreCase), Is.False, "no string names the classic UI");
        }
    }
}
