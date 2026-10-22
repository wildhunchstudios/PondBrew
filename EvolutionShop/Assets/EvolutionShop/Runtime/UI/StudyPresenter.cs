using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-09: the development builds' look-only studies (ShopPanel.Presentation), which Pause offers there: the presentation
    /// sample (store corner, grower, Pondlet, customer and employee on a turntable) and the comedy preview. A title strip at
    /// the top and a control strip at the bottom; the world between them stays undimmed, since it is what the study shows.
    /// It reads the session's study state and calls its commands only (SelectPresentationView, SetPresentationPose,
    /// SetPresentationMotion, SetPresentationAngle, PlayComedyPreview, StopComedyPreview); Esc goes back to Pause as before.
    /// </summary>
    public sealed class StudyPresenter : ShopUiPresenter
    {
        public const string RootName = "es-study", TopName = "es-study-top", EyebrowName = "es-study-eyebrow", TitleName = "es-study-title",
            BottomName = "es-study-bottom", NoteName = "es-study-note", HintName = "es-study-hint", ControlsName = "es-study-controls",
            RotateName = "es-study-rotate", MotionName = "es-study-motion", BeatsName = "es-study-beats", SkipName = "es-study-skip",
            PreviousName = "es-study-previous", NextName = "es-study-next", BackName = "es-study-back",
            CreaturesName = "es-study-creatures", SpeciesName = "es-study-species", VariantName = "es-study-variant", TrickName = "es-study-trick";
        public static string PoseName(CharacterSamplePose pose) => "es-study-pose-" + pose.ToString().ToLowerInvariant();
        public static string BeatName(ComedyKind kind) => "es-study-beat-" + kind.ToString().ToLowerInvariant();

        Label eyebrow, title, note, hint;
        VisualElement controls, rotateRow, poses, beats, creatures;
        Slider rotate;
        Button motion, skip, previous, next, species, variant, trick;

        /// <summary>True while the comedy preview, rather than the presentation sample, is shown.</summary>
        public bool ShowsComedy { get; private set; }

        public static bool Handles(ShopPanel panel) => panel == ShopPanel.Presentation;

        protected override VisualElement Build(VisualTreeAsset template)
        {
            var root = new VisualElement { name = RootName, pickingMode = PickingMode.Ignore };
            root.AddToClassList("es-screen-study");
            var top = Box(root, "es-screen-study__strip es-screen-study__top", TopName);
            eyebrow = Text(top, EyebrowName, "es-screen-study__eyebrow");
            title = Text(top, TitleName, "es-screen-study__title");
            var bottom = Box(root, "es-screen-study__strip es-screen-study__bottom", BottomName);
            note = Text(bottom, NoteName, "es-screen-study__note");
            hint = Text(bottom, HintName, "es-screen-study__hint");
            controls = Box(bottom, "es-screen-study__row", ControlsName);
            rotateRow = Box(controls, "es-screen-study__rotate", "es-study-rotate-row");
            Text(rotateRow, "es-study-rotate-label", "es-screen-study__label").text = Strings.Get("study.rotate");
            rotate = new Slider(-180, 180) { name = RotateName };
            rotate.AddToClassList("es-screen-slider");
            rotate.AddToClassList("es-screen-study__slider");
            rotate.RegisterValueChangedCallback(e => Shop.SetPresentationAngle(e.newValue));
            rotateRow.Add(rotate);
            motion = NewButton(controls, MotionName, "es-button--secondary es-screen-study__button", () => Shop.SetPresentationMotion(!Shop.PresentationMotion));
            poses = Box(controls, "es-screen-study__row", "es-study-poses");
            foreach (CharacterSamplePose pose in Enum.GetValues(typeof(CharacterSamplePose)))
            {
                var which = pose;
                NewButton(poses, PoseName(pose), "es-button--secondary es-screen-study__button", () => Shop.SetPresentationPose(which)).text =
                    Strings.Get("study.pose." + pose);
            }
            // AD-B: with the diorama look on, the creature study shows every species, its colour variants and its trick.
            creatures = Box(bottom, "es-screen-study__row", CreaturesName);
            // AD-C: the same row steps the character studies through the customer types or the crew, and their reactions.
            species = NewButton(creatures, SpeciesName, "es-button--secondary es-screen-study__button", () =>
            {
                if (Shop.PresentationCharacters) Shop.SelectPresentationCharacter((Shop.PresentationCrewView ? Shop.PresentationCrew : Shop.PresentationCustomer) + 1);
                else Shop.SelectPresentationSpecies(Shop.PresentationSpecies + 1);
            });
            species.text = Strings.Get("study.species");
            variant = NewButton(creatures, VariantName, "es-button--secondary es-screen-study__button", () => Shop.SelectPresentationVariant((int)Shop.PresentationVariant + 1));
            trick = NewButton(creatures, TrickName, "es-button--secondary es-screen-study__button", () =>
            {
                if (Shop.PresentationCharacters) Shop.CyclePresentationReaction();
                else Shop.PlayPresentationTrick();
            });
            trick.text = Strings.Get("study.trick");
            beats = Box(bottom, "es-screen-study__beats", BeatsName);
            foreach (ComedyKind kind in Enum.GetValues(typeof(ComedyKind)))
            {
                var which = kind;
                NewButton(beats, BeatName(kind), "es-button--secondary es-screen-study__beat", () => Shop.PlayComedyPreview(which)).text =
                    Strings.Get("study.comedy." + kind);
            }
            var nav = Box(bottom, "es-screen-study__row es-screen-study__nav", "es-study-nav");
            previous = NewButton(nav, PreviousName, "es-button--secondary es-screen-study__button", () =>
                Shop.SelectPresentationView((Shop.PresentationView + ShopSession.PresentationViews - 1) % ShopSession.PresentationViews));
            previous.text = Strings.Get("study.previous");
            next = NewButton(nav, NextName, "es-button--secondary es-screen-study__button", () =>
                Shop.SelectPresentationView((Shop.PresentationView + 1) % ShopSession.PresentationViews));
            next.text = Strings.Get("study.next");
            skip = NewButton(nav, SkipName, "es-button--secondary es-screen-study__button", () => Shop.StopComedyPreview());
            skip.text = Strings.Get("study.comedy.skip");
            Box(nav, "es-screen-study__spacer", "es-study-spacer");
            var back = NewButton(nav, BackName, "es-button--primary es-screen-study__button", () => Shop.SetPanel(ShopPanel.Pause));
            back.text = Strings.Get("study.back");
            return root;
        }

        public override void Refresh()
        {
            if (Element == null || Shop == null) return;
            var root = Shop.ToolkitUi;
            if (root == null || !root.Active || !Handles(Shop.Panel))
            {
                if (IsShown) Hide();
                return;
            }
            if (!IsShown) { Show(); FadeIn(); }
            ShowsComedy = Shop.ComedyPreview;
            bool comedy = ShowsComedy;
            int view = Mathf.Clamp(Shop.PresentationView, 0, ShopSession.PresentationViews - 1);
            SetText(eyebrow, comedy ? Strings.Get("study.comedy.eyebrow") : Strings.Format("study.eyebrow", view + 1, ShopSession.PresentationViews));
            bool kit = Shop.PresentationCreatures, characters = Shop.PresentationCharacters;
            string variantName = Strings.Get("study.variant." + Shop.PresentationVariant);
            string moodName = characters ? MoodName(Shop.PresentationMood, Shop.PresentationCrewView) : "";
            SetText(title, comedy ? Strings.Get("study.comedy." + Shop.ComedyPreviewKind) :
                kit ? Strings.Format("study.creature.title", Strings.Get("study.species." + Shop.PresentationSpeciesId), variantName) :
                characters ? (Shop.PresentationMood == DioramaVisitorMood.Content ? Strings.Get("study.role." + Shop.PresentationRoleId) :
                    Strings.Format("study.character.title", Strings.Get("study.role." + Shop.PresentationRoleId), moodName)) :
                Strings.Get("study.title." + view));
            var preview = Shop.PreviewComedy;
            SetText(note, comedy ? (preview != null && preview.Active ? preview.Caption : Strings.Get("study.comedy.pick")) :
                kit ? Strings.Get("study.creature.note." + Shop.PresentationSpeciesId) :
                characters ? Strings.Get("study.role.note." + Shop.PresentationRoleId) : Strings.Get("study.note." + view));
            SetText(hint, comedy ? Strings.Get("study.comedy.note") : view >= 2 ? "" : Strings.Get("study.lookOnly"));
            SetShown(hint, !string.IsNullOrEmpty(hint.text));
            // The turntable, motion and poses only exist for the living studies (Pondlet on; poses from the customer on).
            SetShown(controls, !comedy && view >= 2);
            SetShown(poses, !comedy && view >= 3);
            if (!comedy && view >= 2)
            {
                if (!Mathf.Approximately(rotate.value, Shop.PresentationAngle)) rotate.SetValueWithoutNotify(Shop.PresentationAngle);
                SetText(motion, Strings.Get(Shop.PresentationMotion ? "study.freeze" : "study.play"));
                foreach (CharacterSamplePose pose in Enum.GetValues(typeof(CharacterSamplePose)))
                    poses.Q<Button>(PoseName(pose))?.EnableInClassList("es-screen-study__button--on", Shop.PresentationPose == pose);
            }
            SetShown(creatures, kit || characters);
            SetShown(variant, kit);
            if (kit) SetText(variant, Strings.Format("study.variant", variantName));
            SetText(species, Strings.Get(characters ? (Shop.PresentationCrewView ? "study.next.crew" : "study.next.customer") : "study.species"));
            SetText(trick, characters ? Strings.Format("study.reaction", moodName) : Strings.Get("study.trick"));
            SetShown(beats, comedy);
            SetShown(previous, !comedy);
            SetShown(next, !comedy);
            SetShown(skip, comedy);
            if (comedy) skip.SetEnabled(preview != null && preview.Active);
        }

        static VisualElement Box(VisualElement parent, string classes, string name)
        {
            var box = new VisualElement { name = name };
            foreach (var c in classes.Split(' ')) box.AddToClassList(c);
            parent.Add(box);
            return box;
        }

        static Label Text(VisualElement parent, string name, string classes)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            foreach (var c in classes.Split(' ')) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }

        static Button NewButton(VisualElement parent, string name, string classes, Action onClick)
        {
            var button = new Button(onClick) { name = name };
            button.AddToClassList("es-button");
            foreach (var c in classes.Split(' ')) button.AddToClassList(c);
            parent.Add(button);
            return button;
        }

        static void SetText(TextElement element, string text) { if (element != null && element.text != text) element.text = text; }
        static void SetShown(VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element != null && element.style.display != display) element.style.display = display;
        }
        /// <summary>The name of a character study's reaction; a crew member's Shocked is being caught out.</summary>
        string MoodName(DioramaVisitorMood mood, bool crew) =>
            Strings.Get("study.mood." + mood + (crew && mood == DioramaVisitorMood.Shocked ? ".crew" : ""));
    }
}
