using UnityEngine;

namespace EvolutionShop
{
    public readonly struct NativeUiSnapshot
    {
        public readonly FxNativeUiCue MoneyKind, StateKind;
        public readonly long MoneyDelta;
        public readonly float MoneyAge, StateAge, PanelAge, HoverAmount, PressAmount, Realtime;
        public readonly uint MoneySequence, StateSequence, MouseMoveCount, MouseDownCount, MouseUpCount, AcceptedButtonCount, DiagnosticEventCount;
        public readonly int ButtonCount, FrameCount, DiagnosticAppliedFrame, DiagnosticConsumedFrame;
        public readonly bool Reduced, Low;
        public readonly Vector2 PanelScroll, DiagnosticScreenPoint;
        public readonly EventType DiagnosticOriginalType, DiagnosticAppliedType;
        internal NativeUiSnapshot(FxNativeUiCue money, FxNativeUiCue state, long delta, float moneyAge, float stateAge,
            float panelAge, float hover, float press, uint moneySequence, uint stateSequence, uint moves, uint downs,
            uint ups, uint accepts, uint diagnostics, int buttons, bool reduced, bool low, Vector2 scroll,
            Vector2 point, EventType original, EventType applied, int appliedFrame, int consumedFrame)
        {
            MoneyKind = money; StateKind = state; MoneyDelta = delta; MoneyAge = moneyAge; StateAge = stateAge;
            PanelAge = panelAge; HoverAmount = hover; PressAmount = press; MoneySequence = moneySequence;
            StateSequence = stateSequence; MouseMoveCount = moves; MouseDownCount = downs; MouseUpCount = ups;
            AcceptedButtonCount = accepts; DiagnosticEventCount = diagnostics; ButtonCount = buttons;
            Reduced = reduced; Low = low; PanelScroll = scroll; DiagnosticScreenPoint = point;
            DiagnosticOriginalType = original; DiagnosticAppliedType = applied; DiagnosticAppliedFrame = appliedFrame;
            DiagnosticConsumedFrame = consumedFrame; FrameCount = Time.frameCount; Realtime = Time.realtimeSinceStartup;
        }
    }

    public sealed partial class ShopSession
    {
        struct NativeUiButton
        {
            public string Label;
            public Rect ScreenRect;
            public bool Enabled;
        }
        readonly NativeUiButton[] nativeUiButtons = new NativeUiButton[64];
        int nativeUiButtonCount;
        long nativeUiCurrencyBaseline, nativeUiDelta;
        bool nativeUiBaselineReady;
        FxNativeUiCue nativeUiMoney, nativeUiState;
        float nativeUiMoneyAge = 10, nativeUiStateAge = 10, nativeUiPanelAge = 10;
        float nativeUiHoverAge, nativeUiPressAge = 10;
        string nativeUiHoverLabel, nativeUiPressLabel, nativeUiMoneyLabel;
        Vector2 nativeUiPointer;
        uint nativeUiMoneySequence, nativeUiStateSequence, nativeUiMoves, nativeUiDowns, nativeUiUps, nativeUiAccepts;
        uint nativeUiDiagnosticCount;
        EventType nativeUiDiagnosticOriginal, nativeUiDiagnosticApplied;
        int nativeUiDiagnosticFrame = -1, nativeUiConsumedFrame = -1;
        Vector2 nativeUiDiagnosticPoint;
        bool NativeUiReduced => ReducedGags || World && World.CustomerReducedMotion;
        bool NativeUiLow => World && World.LabDoseLow;
        bool NativeUiVisible => fxHasFocus && !PlacementActive && Panel != ShopPanel.Arrange && Panel != ShopPanel.Presentation;
        public NativeUiSnapshot NativeUiFxSnapshot => new NativeUiSnapshot(nativeUiMoney, nativeUiState,
            nativeUiDelta, nativeUiMoneyAge, nativeUiStateAge, nativeUiPanelAge,
            nativeUiHoverLabel == null ? 0 : FxNativeUiMotion.Hover(nativeUiHoverAge), FxNativeUiMotion.Press(nativeUiPressAge),
            nativeUiMoneySequence, nativeUiStateSequence, nativeUiMoves, nativeUiDowns, nativeUiUps, nativeUiAccepts,
            nativeUiDiagnosticCount, nativeUiButtonCount, NativeUiReduced, NativeUiLow, panelScroll,
            nativeUiDiagnosticPoint, nativeUiDiagnosticOriginal, nativeUiDiagnosticApplied, nativeUiDiagnosticFrame, nativeUiConsumedFrame);

        public bool TryGetNativeUiButtonForValidation(string label, out Rect screenRect, out bool enabled)
        {
            for (int i = 0; i < nativeUiButtonCount; i++)
                if (nativeUiButtons[i].Label == label)
                { screenRect = nativeUiButtons[i].ScreenRect; enabled = nativeUiButtons[i].Enabled; return true; }
            screenRect = default; enabled = false; return false;
        }

        void ResetNativeUiFeedback()
        {
            CancelNativeUiFeedback();
            nativeUiCurrencyBaseline = Model == null ? 0 : Model.Currency;
            nativeUiBaselineReady = Model != null;
        }
        void CancelNativeUiFeedback()
        {
            nativeUiMoney = nativeUiState = FxNativeUiCue.None;
            nativeUiMoneyAge = nativeUiStateAge = nativeUiPanelAge = nativeUiPressAge = 10;
            nativeUiDelta = 0; nativeUiHoverAge = 0;
            nativeUiHoverLabel = nativeUiPressLabel = nativeUiMoneyLabel = null;
            nativeUiButtonCount = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            nativeUiQueued = false;
            nativeUiDiagnosticPointer = false;
#endif
        }
        void NativeUiPanelChanged(ShopPanel target)
        {
            // Pause cancels action cues, while its own controls and border can still settle.
            if (target == ShopPanel.Pause || target == ShopPanel.Reset || target == ShopPanel.Welcome ||
                target == ShopPanel.TutorialHelp || target == ShopPanel.Presentation || target == ShopPanel.Arrange)
                CancelNativeUiFeedback();
            nativeUiPanelAge = 0; nativeUiPressAge = 10; nativeUiHoverAge = 0;
            nativeUiHoverLabel = nativeUiPressLabel = null; nativeUiButtonCount = 0;
        }
        void TickNativeUiFeedback(float seconds)
        {
            if (!NativeUiVisible) { CancelNativeUiFeedback(); return; }
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            seconds = Mathf.Min(seconds, 1);
            nativeUiMoneyAge = Mathf.Min(10, nativeUiMoneyAge + seconds);
            nativeUiStateAge = Mathf.Min(10, nativeUiStateAge + seconds);
            nativeUiPanelAge = Mathf.Min(10, nativeUiPanelAge + seconds);
            nativeUiPressAge = Mathf.Min(10, nativeUiPressAge + seconds);
            nativeUiHoverAge = Mathf.Min(1, nativeUiHoverAge + seconds);
            if (nativeUiMoneyAge >= .85f) nativeUiMoney = FxNativeUiCue.None;
            if (nativeUiStateAge >= (nativeUiState == FxNativeUiCue.Discovery ? 1.1f : .8f)) nativeUiState = FxNativeUiCue.None;
        }

        // Observe the durable result without changing staging, publication, or economics.
        bool WriteUiObservedSlot(SlotData slot, out string error)
        {
            bool saved = SaveStore.Write(SavePath, slot, out error);
            if (!saved) { DiscardUnsavedNativeUiDelta(); return false; }
            long currency = slot.game.currency;
            long delta = nativeUiBaselineReady ? currency - nativeUiCurrencyBaseline : 0;
            nativeUiCurrencyBaseline = currency; nativeUiBaselineReady = true;
            if (delta != 0 && NativeUiVisible)
            {
                nativeUiDelta = delta; nativeUiMoneyAge = 0;
                nativeUiMoney = delta > 0 ? FxNativeUiCue.CreditGain : FxNativeUiCue.CreditSpend;
                nativeUiMoneyLabel = "SAVED " + (delta > 0 ? "+" : "") + delta.ToString("N0");
                nativeUiMoneySequence++;
            }
            return true;
        }
        void DiscardUnsavedNativeUiDelta()
        {
            // A failed live sale must not reappear as a new success during a later retry.
            nativeUiCurrencyBaseline = Model == null ? 0 : Model.Currency;
            nativeUiBaselineReady = Model != null;
            nativeUiMoney = FxNativeUiCue.None; nativeUiDelta = 0; nativeUiMoneyAge = 10;
            nativeUiMoneyLabel = null;
        }
        void PresentNativeUiReady(bool discovery)
        {
            if (!NativeUiVisible) return;
            nativeUiState = discovery ? FxNativeUiCue.Discovery : FxNativeUiCue.Ready;
            nativeUiStateAge = 0; nativeUiStateSequence++;
        }

        void BeginNativeUiGui()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ApplyNativeUiDiagnosticEvent();
#endif
            var type = Event.current.type;
            if (type == EventType.Repaint)
            {
                nativeUiButtonCount = 0;
                // Native hover follows the actual repaint pointer even without MouseMove delivery.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!nativeUiDiagnosticPointer)
#endif
                    nativeUiPointer = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
            if (type == EventType.MouseMove) nativeUiMoves++;
            if (type == EventType.MouseDown) nativeUiDowns++;
            if (type == EventType.MouseUp) nativeUiUps++;
            if (type == EventType.MouseMove || type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.ScrollWheel)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (nativeUiOriginalEvent == null) nativeUiDiagnosticPointer = false;
#endif
                nativeUiPointer = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
        }
        void EndNativeUiGui()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (nativeUiOriginalEvent != null)
            {
                if (Event.current.type == EventType.Used) nativeUiConsumedFrame = Time.frameCount;
                Event.current = nativeUiOriginalEvent; nativeUiOriginalEvent = null;
            }
#endif
        }
        void ObserveNativeUiButton(string label, bool accepted)
        {
            if (accepted)
            {
                nativeUiAccepts++; nativeUiPressLabel = label; nativeUiPressAge = 0;
            }
            if (Event.current.type != EventType.Repaint) return;
            Rect local = GUILayoutUtility.GetLastRect();
            Vector2 first = GUIUtility.GUIToScreenPoint(local.min), last = GUIUtility.GUIToScreenPoint(local.max);
            Rect screen = Rect.MinMaxRect(first.x, first.y, last.x, last.y);
            if (nativeUiButtonCount < nativeUiButtons.Length)
                nativeUiButtons[nativeUiButtonCount++] = new NativeUiButton { Label = label, ScreenRect = screen, Enabled = GUI.enabled };
            if (!NativeUiVisible || !GUI.enabled) return;
            bool hover = screen.Contains(nativeUiPointer);
            if (hover && nativeUiHoverLabel != label) { nativeUiHoverLabel = label; nativeUiHoverAge = 0; }
            else if (!hover && nativeUiHoverLabel == label) { nativeUiHoverLabel = null; nativeUiHoverAge = 0; }
            float amount = hover ? FxNativeUiMotion.Hover(nativeUiHoverAge) * .45f : 0;
            if (nativeUiPressLabel == label) amount = Mathf.Max(amount, FxNativeUiMotion.Press(nativeUiPressAge));
            if (amount > 0) NativeUiEdge(new Rect(local.x + 2, local.y + 2, local.width - 4, local.height - 4), ShopUiTheme.Honey, amount);
        }
        static void NativeUiEdge(Rect rect, Color color, float alpha)
        {
            if (Event.current.type != EventType.Repaint || alpha <= 0) return;
            color.a *= Mathf.Clamp01(alpha);
            Box(new Rect(rect.x, rect.y, rect.width, 2), color);
            Box(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
            Box(new Rect(rect.x, rect.y + 2, 2, rect.height - 4), color);
            Box(new Rect(rect.xMax - 2, rect.y + 2, 2, rect.height - 4), color);
        }
        void DrawNativeUiMoney()
        {
            if (Event.current.type != EventType.Repaint || !NativeUiVisible) return;
            var pose = FxNativeUiMotion.Sample(nativeUiMoney, nativeUiMoneyAge, NativeUiReduced, NativeUiLow);
            if (pose.Alpha <= 0) return;
            NativeUiEdge(new Rect(1032, 20, 228, 50), ShopUiTheme.Honey, pose.Alpha * .8f);
            Color previous = GUI.color; GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * pose.Alpha);
            for (int i = 0; i < pose.CoinCount; i++)
            {
                float x = pose.CoinCount == 1 ? 1053 : 1040 + i * 13;
                float y = 48 + pose.Offset.y + (i == 1 ? -4 : 0);
                hudTheme.Icon(new Rect(x, y, 12 * pose.Scale, 12 * pose.Scale), ShopUiTheme.Symbol.Coin);
            }
            GUI.color = previous;
        }
        void DrawNativeUiMachine()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (Model.Incubator.Phase == IncubatorPhase.Incubating)
            {
                float progress = Mathf.Clamp01(1 - Model.Incubator.SecondsRemaining / Mathf.Max(.001f, Config.IncubationSeconds));
                hudTheme.Draw(new Rect(1043, 192, 206, 3), hudTheme.Inset);
                hudTheme.Draw(new Rect(1043, 192, 206 * progress, 3), hudTheme.Accent, mint);
                if (progress > 0) Box(new Rect(1043 + 206 * progress - 2, 191, 2, 5), ShopUiTheme.Paper);
            }
            var pose = FxNativeUiMotion.Sample(nativeUiState, nativeUiStateAge, NativeUiReduced, NativeUiLow);
            if (NativeUiVisible) NativeUiEdge(new Rect(1032, 135, 228, 62), coral, pose.Alpha * .8f);
        }
        void DrawNativeUiDiscovery(Rect rect)
        {
            if (nativeUiState != FxNativeUiCue.Discovery || !NativeUiVisible) return;
            var pose = FxNativeUiMotion.Sample(nativeUiState, nativeUiStateAge, NativeUiReduced, NativeUiLow);
            NativeUiEdge(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), ShopUiTheme.Honey, pose.Alpha);
        }
        void DrawNativeUiPanel()
        {
            if (!NativeUiVisible) return;
            float amount = FxNativeUiMotion.Panel(nativeUiPanelAge);
            NativeUiEdge(new Rect(352, 114, 576, 514), mint, amount * (NativeUiReduced ? .35f : .7f));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly Event nativeUiDiagnosticEvent = new Event();
        Event nativeUiOriginalEvent;
        bool nativeUiQueued, nativeUiDiagnosticPointer;
        Vector2 nativeUiQueuedPoint, nativeUiQueuedDelta;
        EventType nativeUiQueuedType;
        int nativeUiQueuedButton;
        public bool QueueNativeUiEventForValidation(EventType type, Vector2 screenPoint, int button = 0, Vector2 delta = default)
        {
            if (!ValidationMode || Arg("-evolutionFxNativeUiSmoke") == null || nativeUiQueued || !NativeUiVisible ||
                (type != EventType.MouseMove && type != EventType.MouseDown && type != EventType.MouseUp && type != EventType.ScrollWheel) ||
                !FiniteNativeUiPoint(screenPoint) || !FiniteNativeUiPoint(delta) || button < 0 || button > 2) return false;
            nativeUiQueued = true; nativeUiQueuedType = type; nativeUiQueuedPoint = screenPoint;
            nativeUiQueuedButton = button; nativeUiQueuedDelta = delta; return true;
        }
        static bool FiniteNativeUiPoint(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
        void ApplyNativeUiDiagnosticEvent()
        {
            // Explicit smoke-only synthetic IMGUI input. Native OS input is not claimed.
            // Substitute a repaint pass after its layout has been built; preserve the host event.
            if (!nativeUiQueued || Event.current.type != EventType.Repaint) return;
            nativeUiQueued = false; nativeUiOriginalEvent = Event.current;
            nativeUiDiagnosticPointer = true;
            nativeUiDiagnosticOriginal = nativeUiOriginalEvent.type;
            nativeUiDiagnosticEvent.type = nativeUiQueuedType;
            nativeUiDiagnosticEvent.mousePosition = GUIUtility.ScreenToGUIPoint(nativeUiQueuedPoint);
            nativeUiDiagnosticEvent.button = nativeUiQueuedButton;
            nativeUiDiagnosticEvent.delta = nativeUiQueuedDelta;
            nativeUiDiagnosticEvent.clickCount = 1;
            nativeUiDiagnosticEvent.modifiers = EventModifiers.None;
            Event.current = nativeUiDiagnosticEvent;
            nativeUiDiagnosticApplied = nativeUiQueuedType; nativeUiDiagnosticPoint = nativeUiQueuedPoint;
            nativeUiDiagnosticFrame = Time.frameCount; nativeUiDiagnosticCount++;
        }
#endif
    }
}
