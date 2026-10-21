using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EvolutionShop
{
    /// <summary>
    /// UX-08 feel pass (UX.9), owned by <see cref="ShopUiRoot"/>: the animator, the UI sounds, button presses and presses on
    /// disabled buttons. An enabled button squashes and ticks as it is pressed; a disabled one shakes two pixels, thunks and
    /// says why: the station card flashes its reason line, any other button shows its tooltip text in a small tip above it.
    /// Feedback follows the input or the saved result and never holds up the next command (the FX ownership rule).
    /// </summary>
    public sealed class UiFeel
    {
        public const string TipName = "es-press-tip";
        public const float TipSeconds = 2.2f;

        readonly ShopSession shop;
        readonly ShopUiRoot owner;
        readonly VisualElement root;
        readonly Label tip;
        VisualElement tipAnchor;
        float tipUntil = float.NegativeInfinity;

        public UiAnimator Motion { get; } = new UiAnimator();
        public ShopUiSounds Sounds { get; }
        /// <summary>Reduced motion and Low effects detail as the comfort settings have them now (FX-20).</summary>
        public bool Reduced => shop != null && shop.ToolkitReducedMotion;
        public bool Low => shop != null && shop.ToolkitLowDetail;
        public float Now => Motion.Now;
        public int Presses { get; private set; }
        public int DisabledPresses { get; private set; }
        public string LastPressed { get; private set; }
        public string LastDisabled { get; private set; }
        public int TipsShown { get; private set; }
        public bool TipShown => tip != null && tip.style.display == DisplayStyle.Flex;
        public string TipText => tip != null ? tip.text : "";
        public Label Tip => tip;

        public UiFeel(ShopSession shop, ShopUiRoot owner, VisualElement root, VisualElement tipLayer)
        {
            this.shop = shop; this.owner = owner; this.root = root;
            Sounds = ShopUiSounds.Create(owner.transform);
            tip = new Label { name = TipName, pickingMode = PickingMode.Ignore };
            tip.AddToClassList("es-press-tip");
            tip.style.display = DisplayStyle.None;
            tipLayer.Add(tip);
            // Trickle-down on the root sees every press before the button's own handler, whatever the button does with it.
            root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        }

        public void Unbind()
        {
            root?.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            Motion.Finish();
            Sounds.Destroy();
        }

        public void Advance(float seconds) => Motion.Advance(seconds);

        /// <summary>After the presenters: catches presses on disabled buttons, keeps the tip in place and applies this frame's poses.</summary>
        public void Tick()
        {
            PollDisabledPress();
            if (TipShown)
            {
                if (Now >= tipUntil || tipAnchor == null || tipAnchor.panel == null || !HudPresenter.Visible(tipAnchor)) HideTip();
                else PlaceTip();
            }
            Motion.Tick(Reduced);
        }

        /// <summary>Ends every animation and hides the tip, as when the preview turns off.</summary>
        public void Reset()
        {
            Motion.Finish();
            HideTip();
        }

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            var button = ButtonOf(e.target as VisualElement);
            // A disabled button may not get the event at all; PollDisabledPress handles those, so each press counts once.
            if (button == null || !button.enabledInHierarchy) return;
            Pressed(button);
        }

        public void Pressed(Button button)
        {
            Presses++;
            LastPressed = button.name;
            Motion.Play(button, UiMotionKind.Press);
            Sounds.Play(UiSound.Tick);
        }

        void PollDisabledPress()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || root?.panel == null) return;
            Vector2 panel = root.layout.size;
            if (Screen.width <= 0 || Screen.height <= 0 || float.IsNaN(panel.x) || panel.x <= 0) return;
            Vector2 at = mouse.position.ReadValue();
            var point = new Vector2(at.x * panel.x / Screen.width, (Screen.height - at.y) * panel.y / Screen.height);
            var button = ButtonOf(root.panel.Pick(point));
            if (button == null || button.enabledInHierarchy || !HudPresenter.Visible(button)) return;
            DisabledPress(button);
        }

        public void DisabledPress(Button button)
        {
            DisabledPresses++;
            LastDisabled = button.name;
            Motion.Play(button, UiMotionKind.DisabledShake);
            Sounds.Play(UiSound.Thunk);
            // The card's primary already has its reason line above it; flash that. Anything else shows its own reason, if it has one.
            if (owner.StationCard != null && owner.StationCard.FlashReason(button)) return;
            if (!string.IsNullOrEmpty(button.tooltip)) ShowTip(button, button.tooltip);
        }

        public void ShowTip(VisualElement anchor, string text)
        {
            if (tip == null || anchor == null || string.IsNullOrEmpty(text)) return;
            tip.text = text;
            tip.style.display = DisplayStyle.Flex;
            tipAnchor = anchor;
            tipUntil = Now + TipSeconds;
            TipsShown++;
            PlaceTip();
            Motion.Play(tip, UiMotionKind.TipSlide);
        }

        void HideTip()
        {
            if (tip == null) return;
            Motion.Stop(tip);
            tip.style.display = DisplayStyle.None;
            tipAnchor = null;
        }

        /// <summary>Above the button, centred on it, inside the screen; below it when there is no room above.</summary>
        void PlaceTip()
        {
            Vector2 panel = root.layout.size;
            Rect a = tipAnchor.worldBound;
            float w = tip.layout.width, h = tip.layout.height;
            if (float.IsNaN(w) || w <= 1) { w = 320f; h = 52f; }
            const float gap = 10f, margin = 12f;
            float x = Mathf.Clamp(a.center.x - w * .5f, margin, Mathf.Max(margin, panel.x - margin - w));
            float y = a.yMin - gap - h;
            if (y < margin) y = Mathf.Min(a.yMax + gap, Mathf.Max(margin, panel.y - margin - h));
            tip.style.left = x;
            tip.style.top = y;
        }

        static Button ButtonOf(VisualElement element)
        {
            for (var v = element; v != null; v = v.parent) if (v is Button b) return b;
            return null;
        }
    }
}
