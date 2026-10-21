#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    // UX-09: the two panels whose only view was IMGUI. The development studies (presentation sample and comedy preview) are
    // checked from the UX-07 screens route's pause; closing early (ShopPanel.CloseShop) from the UX-04 cards route's counter.
    // Controls are pressed with the routes' synthetic Input System mouse and keyboard (CardClick, CardKey).
    public sealed partial class MilestoneSmoke
    {
        IEnumerator ScreensStudies(VisualElement root, ShopUiStrings s)
        {
            var ui = shop.ToolkitUi;
            var study = ui.Study;
            shop.SetPanel(ShopPanel.Pause);
            yield return HudFrames(3);
            Check(CardShown(root, PauseView.DevName), "Development builds offer the studies on Pause");
            yield return CardClick(root.Q<Button>(PauseView.PresentationName), "Presentation sample");
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Presentation && study.IsShown && !study.ShowsComedy && !ui.Hud.IsShown && !ui.Screens.IsShown,
                "Presentation sample opens the study view over the undimmed world, with the HUD and Pause hidden");
            Check(StudyText(root, StudyPresenter.TitleName) == s.Get("study.title.0") &&
                StudyText(root, StudyPresenter.EyebrowName) == s.Format("study.eyebrow", 1, ShopSession.PresentationViews) &&
                StudyText(root, StudyPresenter.NoteName) == s.Get("study.note.0") && CardShown(root, StudyPresenter.HintName),
                "The first study names the store corner and says it is look-only: " + StudyText(root, StudyPresenter.TitleName));
            Check(!CardShown(root, StudyPresenter.ControlsName) && CardShown(root, StudyPresenter.NextName), "The store corner has no turntable, only Next and Back");
            yield return HudShot("ux09-study-store");
            CheckStudy("Study, store corner");
            for (int i = 0; i < 3; i++) yield return CardClick(root.Q<Button>(StudyPresenter.NextName), "Next sample");
            Check(shop.PresentationView == 3 && StudyText(root, StudyPresenter.TitleName) == s.Get("study.title.3") &&
                CardShown(root, StudyPresenter.ControlsName) && CardShown(root, StudyPresenter.RotateName) &&
                CardShown(root, StudyPresenter.PoseName(CharacterSamplePose.Walk)), "Three Nexts reach the customer study with its turntable and poses");
            yield return CardClick(root.Q<Button>(StudyPresenter.PoseName(CharacterSamplePose.Walk)), "Walk");
            Check(shop.PresentationPose == CharacterSamplePose.Walk &&
                root.Q<Button>(StudyPresenter.PoseName(CharacterSamplePose.Walk)).ClassListContains("es-screen-study__button--on"), "Walk plays and is marked");
            yield return CardClick(root.Q<Button>(StudyPresenter.MotionName), "Freeze motion");
            Check(!shop.PresentationMotion && root.Q<Button>(StudyPresenter.MotionName).text == s.Get("study.play"), "Freeze stops the motion and offers Play");
            var rotate = root.Q<Slider>(StudyPresenter.RotateName);
            if (rotate != null) rotate.value = 90;
            yield return HudFrames(2);
            Check(Mathf.Approximately(shop.PresentationAngle, 90), "Rotate turns the study (" + shop.PresentationAngle + "°)");
            yield return HudShot("ux09-study-customer");
            CheckStudy("Study, customer");
            yield return CardClick(root.Q<Button>(StudyPresenter.PreviousName), "Previous");
            Check(shop.PresentationView == 2 && !CardShown(root, StudyPresenter.PoseName(CharacterSamplePose.Walk)) && CardShown(root, StudyPresenter.RotateName),
                "Previous reaches the Pondlet: a turntable but no poses");
            yield return CardClick(root.Q<Button>(StudyPresenter.BackName), "Back to pause");
            Check(shop.Panel == ShopPanel.Pause && ui.Screens.IsShown && !study.IsShown, "Back to pause returns to Pause");

            yield return CardClick(root.Q<Button>(PauseView.ComedyName), "Comedy preview");
            yield return HudFrames(3);
            Check(shop.Panel == ShopPanel.Presentation && study.IsShown && study.ShowsComedy && !CardShown(root, StudyPresenter.NextName) &&
                CardShown(root, StudyPresenter.BeatName(ComedyKind.Belch)) && StudyText(root, StudyPresenter.NoteName) == s.Get("study.comedy.pick"),
                "Comedy preview opens the beats, asking to pick one");
            yield return CardClick(root.Q<Button>(StudyPresenter.BeatName(ComedyKind.Belch)), "Incubator belch");
            yield return HudFrames(2);
            Check(shop.PreviewComedy.Active && shop.ComedyPreviewKind == ComedyKind.Belch && StudyText(root, StudyPresenter.TitleName) == s.Get("study.comedy.Belch") &&
                StudyText(root, StudyPresenter.NoteName) == shop.PreviewComedy.Caption && root.Q<Button>(StudyPresenter.SkipName).enabledSelf,
                "A beat plays with its caption: " + StudyText(root, StudyPresenter.NoteName));
            yield return HudShot("ux09-study-comedy");
            CheckStudy("Study, comedy");
            yield return CardClick(root.Q<Button>(StudyPresenter.SkipName), "Skip");
            Check(!shop.PreviewComedy.Active && StudyText(root, StudyPresenter.NoteName) == s.Get("study.comedy.pick"), "Skip stops the beat");
            yield return CardKey(Key.Escape);
            Check(shop.Panel == ShopPanel.Pause && ui.Screens.IsShown, "Esc goes back to Pause from a study");
            shop.SetPanel(ShopPanel.None);
            yield return HudFrames(2);
        }

        static string StudyText(VisualElement root, string name) => root.Q<TextElement>(name)?.text ?? "";

        /// <summary>The study's strips and every shown button lie inside the screen, and nothing in them is clipped.</summary>
        void CheckStudy(string state)
        {
            var root = shop.ToolkitUi.Root;
            foreach (var name in new[] { StudyPresenter.TopName, StudyPresenter.BottomName })
                Check(ShopUiTestDriver.FullyInside(root.Q(name), root), state + ": " + name + " is fully on screen " + root.Q(name)?.worldBound + " in " + root.worldBound);
            int buttons = 0;
            foreach (var button in root.Q(StudyPresenter.RootName).Query<Button>().ToList())
            {
                if (!CardShown(button)) continue;
                buttons++;
                Check(ShopUiTestDriver.FullyInside(button, root), state + ": " + button.name + " is on screen");
                var text = button.MeasureTextSize(button.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                Check(text.x <= button.contentRect.width + 1, state + ": " + button.name + " fits its text (" + text.x.ToString("0") + " in " + button.contentRect.width.ToString("0") + ")");
            }
            Check(buttons > 0, state + ": the study shows its buttons");
        }

        /// <summary>
        /// ShopPanel.CloseShop, the classic "Close for the night?" panel, is the counter card at its confirm step (UX-09). Keep
        /// trading goes back to the counter; the route closes the day itself afterwards through the card as before.
        /// </summary>
        IEnumerator CardsCloseShopPanel(VisualElement root, StationCardPresenter card)
        {
            shop.SetPanel(ShopPanel.CloseShop);
            yield return HudFrames(3);
            var counter = card.CounterView;
            Check(card.IsShown && card.CurrentView == counter && counter.Confirming && CardShown(root, CounterCardView.ConfirmName) &&
                CardShown(root, CounterCardView.CloseNowName), "The close-early panel opens the counter card at its confirm step");
            yield return HudShot("ux09-close-confirm");
            yield return CardClick(root.Q<Button>(CounterCardView.KeepName), "Keep trading");
            Check(shop.Panel == ShopPanel.Counter && card.IsShown && !counter.Confirming && shop.Cycle.Phase == ShopDayPhase.Open,
                "Keep trading goes back to the counter card, still open");
        }
    }
}
#endif
