using UnityEngine;

namespace EvolutionShop
{
    public readonly struct NativeUiHostEventSnapshot
    {
        public readonly EventType Type, RawType;
        public readonly Vector2 LocalPoint, ScreenPoint;
        public readonly bool HasCommand, TokenMatches;
        public readonly int FrameCount, QueuedEventCount;
        public readonly float Realtime;
        internal NativeUiHostEventSnapshot(string token)
        {
            var current = Event.current;
            Type = current.type; RawType = current.rawType;
            LocalPoint = current.mousePosition; ScreenPoint = GUIUtility.GUIToScreenPoint(LocalPoint);
            string command = current.commandName;
            HasCommand = !string.IsNullOrEmpty(command); TokenMatches = command == token;
            FrameCount = Time.frameCount; QueuedEventCount = Event.GetEventCount(); Realtime = Time.realtimeSinceStartup;
        }
    }

    public readonly struct NativeUiControlSnapshot
    {
        public readonly string Label;
        public readonly Rect LocalRect;
        public readonly Vector2 LocalPoint, ScreenPoint;
        public readonly EventType Type, RawType;
        public readonly int HotControl, FrameCount;
        public readonly bool Enabled, Accepted, RectFromRepaint;
        internal NativeUiControlSnapshot(string label, Rect rect, bool cached, bool accepted)
        {
            Label = label; LocalRect = rect; LocalPoint = Event.current.mousePosition;
            ScreenPoint = GUIUtility.GUIToScreenPoint(LocalPoint);
            Type = Event.current.type; RawType = Event.current.rawType;
            HotControl = GUIUtility.hotControl; FrameCount = Time.frameCount;
            Enabled = GUI.enabled; Accepted = accepted; RectFromRepaint = cached;
        }
    }

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
            public Rect LocalRect;
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
            if (nativeUiQueued) nativeUiQueuedCancelled = true;
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
            ObserveNativeUiDispatchedEvent();
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
                if (!nativeUiDiagnosticPass) nativeUiDiagnosticPointer = false;
#endif
                nativeUiPointer = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
        }
        void EndNativeUiGui()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (nativeUiDiagnosticPass)
            {
                if (Event.current.type == EventType.Used) nativeUiConsumedFrame = Time.frameCount;
                nativeUiDiagnosticPass = false;
            }
#endif
        }
        void ObserveNativeUiButton(string label, bool accepted)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ObserveNativeUiDiagnosticControl(label, accepted);
#endif
            if (accepted)
            {
                nativeUiAccepts++; nativeUiPressLabel = label; nativeUiPressAge = 0;
            }
            if (Event.current.type != EventType.Repaint) return;
            Rect local = GUILayoutUtility.GetLastRect();
            Vector2 first = GUIUtility.GUIToScreenPoint(local.min), last = GUIUtility.GUIToScreenPoint(local.max);
            Rect screen = Rect.MinMaxRect(first.x, first.y, last.x, last.y);
            if (nativeUiButtonCount < nativeUiButtons.Length)
                nativeUiButtons[nativeUiButtonCount++] = new NativeUiButton { Label = label, ScreenRect = screen, LocalRect = local, Enabled = GUI.enabled };
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
        readonly NativeUiHostEventSnapshot[] nativeUiHostEvents = new NativeUiHostEventSnapshot[64];
        int nativeUiHostEventCount;
        public int NativeUiHostEventCountForValidation => nativeUiHostEventCount;
        public int NativeUiQueueCountBeforeForValidation { get; private set; }
        public int NativeUiQueueCountAfterForValidation { get; private set; }
        public bool TryGetNativeUiHostEventForValidation(int index, out NativeUiHostEventSnapshot snapshot)
        {
            if (index >= 0 && index < nativeUiHostEventCount) { snapshot = nativeUiHostEvents[index]; return true; }
            snapshot = default; return false;
        }
        readonly NativeUiControlSnapshot[] nativeUiDiagnosticControls = new NativeUiControlSnapshot[64];
        int nativeUiDiagnosticControlCount;
        public bool TryGetNativeUiControlForValidation(string label, out NativeUiControlSnapshot snapshot)
        {
            for (int i = 0; i < nativeUiDiagnosticControlCount; i++)
                if (nativeUiDiagnosticControls[i].Label == label)
                { snapshot = nativeUiDiagnosticControls[i]; return true; }
            snapshot = default; return false;
        }
        void ObserveNativeUiDiagnosticControl(string label, bool accepted)
        {
            if (!nativeUiDiagnosticPass || nativeUiDiagnosticControlCount >= nativeUiDiagnosticControls.Length) return;
            bool cached = Event.current.type == EventType.Used || Event.current.type == EventType.Layout;
            Rect rect = default;
            if (!cached) rect = GUILayoutUtility.GetLastRect();
            else
                for (int i = 0; i < nativeUiButtonCount; i++)
                    if (nativeUiButtons[i].Label == label) { rect = nativeUiButtons[i].LocalRect; break; }
            // Read-only evidence: never allocate a control ID or change native mouse ownership.
            // Ordinary repaints leave these samples intact until the next diagnostic event.
            nativeUiDiagnosticControls[nativeUiDiagnosticControlCount++] = new NativeUiControlSnapshot(label, rect, cached, accepted);
        }
        readonly Event nativeUiDiagnosticEvent = new Event();
        System.Action<Event> nativeUiEngineQueue;
        string nativeUiQueuedToken;
        bool nativeUiQueueResolved, nativeUiQueued, nativeUiQueuedCancelled, nativeUiDiagnosticPointer, nativeUiDiagnosticPass;
        public string NativeUiDiagnosticErrorForValidation { get; private set; }
        Vector2 nativeUiQueuedPoint, nativeUiQueuedDelta;
        EventType nativeUiQueuedType;
        int nativeUiQueuedButton;
        public bool QueueNativeUiEventForValidation(EventType type, Vector2 screenPoint, int button = 0, Vector2 delta = default)
        {
            if (!ValidationMode || Arg("-evolutionFxNativeUiSmoke") == null || nativeUiQueued || !NativeUiVisible ||
                (type != EventType.MouseMove && type != EventType.MouseDown && type != EventType.MouseUp && type != EventType.ScrollWheel) ||
                !FiniteNativeUiPoint(screenPoint) || !FiniteNativeUiPoint(delta) || button < 0 || button > 2) return false;
            if (!ResolveNativeUiEngineQueue()) return false;
            nativeUiQueued = true; nativeUiQueuedCancelled = false;
            nativeUiQueuedType = type; nativeUiQueuedPoint = screenPoint;
            nativeUiQueuedButton = button; nativeUiQueuedDelta = delta;
            nativeUiHostEventCount = 0;
            nativeUiQueuedToken = "EvolutionShop.FX18." + System.Guid.NewGuid().ToString("N");
            nativeUiDiagnosticEvent.type = type;
            nativeUiDiagnosticEvent.commandName = nativeUiQueuedToken;
            nativeUiDiagnosticEvent.mousePosition = screenPoint;
            nativeUiDiagnosticEvent.button = button;
            nativeUiDiagnosticEvent.delta = delta;
            nativeUiDiagnosticEvent.clickCount = 1;
            nativeUiDiagnosticEvent.modifiers = EventModifiers.None;
            try
            {
                NativeUiQueueCountBeforeForValidation = Event.GetEventCount();
                nativeUiEngineQueue(nativeUiDiagnosticEvent);
                NativeUiQueueCountAfterForValidation = Event.GetEventCount();
                return true;
            }
            catch (System.Exception error)
            {
                nativeUiQueued = false;
                NativeUiDiagnosticErrorForValidation = "FX18 diagnostic engine queue failed: " + error.Message;
                Debug.LogError(NativeUiDiagnosticErrorForValidation); return false;
            }
        }
        static bool FiniteNativeUiPoint(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
        bool ResolveNativeUiEngineQueue()
        {
            if (nativeUiQueueResolved) return nativeUiEngineQueue != null;
            nativeUiQueueResolved = true;
            try
            {
                // Pinned diagnostic adapter only. Normal gameplay never resolves this internal API.
                if (!Application.unityVersion.StartsWith("6000.3.", System.StringComparison.Ordinal))
                    throw new System.NotSupportedException("Requires the pinned Unity 6000.3 runtime.");
                var method = typeof(Event).GetMethod("QueueEvent", System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(Event) }, null);
                if (method == null) throw new System.MissingMethodException("UnityEngine.Event.QueueEvent(Event)");
                nativeUiEngineQueue = (System.Action<Event>)System.Delegate.CreateDelegate(typeof(System.Action<Event>), method);
                return true;
            }
            catch (System.Exception error)
            {
                NativeUiDiagnosticErrorForValidation = "FX18 diagnostic engine queue unavailable: " + error.Message;
                Debug.LogError(NativeUiDiagnosticErrorForValidation); return false;
            }
        }
        void ObserveNativeUiDispatchedEvent()
        {
            // Bounded host-pass evidence distinguishes non-dispatch from lost tags or coordinates.
            if (nativeUiQueued && nativeUiHostEventCount < nativeUiHostEvents.Length)
                nativeUiHostEvents[nativeUiHostEventCount++] = new NativeUiHostEventSnapshot(nativeUiQueuedToken);
            // Unity dispatch establishes the absolute GUIClip pointer before OnGUI.
            // Observe only our one pending event; never replace a pass or reset Ignore/ownership.
            if (!nativeUiQueued || Event.current.commandName != nativeUiQueuedToken ||
                Event.current.rawType != nativeUiQueuedType || Event.current.button != nativeUiQueuedButton) return;
            Vector2 point = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            if ((point - nativeUiQueuedPoint).sqrMagnitude > 1) return;
            if (nativeUiQueuedType == EventType.ScrollWheel && (Event.current.delta - nativeUiQueuedDelta).sqrMagnitude > .01f) return;
            nativeUiQueued = false;
            if (nativeUiQueuedCancelled)
            {
                // Drain only the matching canceled diagnostic, never flush unrelated engine input.
                nativeUiQueuedCancelled = false; Event.current.Use(); return;
            }
            nativeUiDiagnosticPass = true;
            nativeUiDiagnosticControlCount = 0;
            nativeUiDiagnosticPointer = true;
            nativeUiDiagnosticOriginal = Event.current.type;
            nativeUiDiagnosticApplied = Event.current.rawType; nativeUiDiagnosticPoint = point;
            nativeUiDiagnosticFrame = Time.frameCount; nativeUiDiagnosticCount++;
        }
#endif
    }
}
