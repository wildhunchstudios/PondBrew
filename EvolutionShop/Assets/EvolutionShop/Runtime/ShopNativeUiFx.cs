using UnityEngine;

namespace EvolutionShop
{
    public enum NativeUiObservationKind { None, RepaintPointer, NativeEvent }

    public readonly struct NativeUiWindowSnapshot
    {
        public readonly uint ProcessId, Message;
        public readonly long Handle;
        public readonly int ClientWidth, ClientHeight, RenderWidth, RenderHeight, WheelDelta, PostedFrame;
        public readonly Vector2 RequestedPoint, ClientPoint, ScreenPoint, MessagePoint;
        public readonly bool LeftDown;
        internal NativeUiWindowSnapshot(uint process, System.IntPtr handle, uint message, int width, int height,
            int wheel, Vector2 requested, Vector2 client, Vector2 represented, Vector2 messagePoint, bool leftDown)
        {
            ProcessId = process; Handle = handle.ToInt64(); Message = message;
            ClientWidth = width; ClientHeight = height; RenderWidth = Screen.width; RenderHeight = Screen.height;
            WheelDelta = wheel; PostedFrame = Time.frameCount;
            RequestedPoint = requested; ClientPoint = client; ScreenPoint = represented;
            MessagePoint = messagePoint; LeftDown = leftDown;
        }
    }

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
            HasCommand = !string.IsNullOrEmpty(command); TokenMatches = !string.IsNullOrEmpty(token) && command == token;
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
        public readonly uint PointerObservationCount;
        public readonly NativeUiObservationKind ObservationKind;
        public readonly int ButtonCount, FrameCount, DiagnosticAppliedFrame, DiagnosticConsumedFrame;
        public readonly bool Reduced, Low;
        public readonly Vector2 PanelScroll, DiagnosticScreenPoint;
        public readonly EventType DiagnosticOriginalType, DiagnosticAppliedType;
        internal NativeUiSnapshot(FxNativeUiCue money, FxNativeUiCue state, long delta, float moneyAge, float stateAge,
            float panelAge, float hover, float press, uint moneySequence, uint stateSequence, uint moves, uint downs,
            uint ups, uint accepts, uint diagnostics, int buttons, bool reduced, bool low, Vector2 scroll,
            Vector2 point, EventType original, EventType applied, int appliedFrame, int consumedFrame,
            uint pointerObservations, NativeUiObservationKind observationKind)
        {
            MoneyKind = money; StateKind = state; MoneyDelta = delta; MoneyAge = moneyAge; StateAge = stateAge;
            PanelAge = panelAge; HoverAmount = hover; PressAmount = press; MoneySequence = moneySequence;
            StateSequence = stateSequence; MouseMoveCount = moves; MouseDownCount = downs; MouseUpCount = ups;
            AcceptedButtonCount = accepts; DiagnosticEventCount = diagnostics; ButtonCount = buttons;
            Reduced = reduced; Low = low; PanelScroll = scroll; DiagnosticScreenPoint = point;
            DiagnosticOriginalType = original; DiagnosticAppliedType = applied; DiagnosticAppliedFrame = appliedFrame;
            DiagnosticConsumedFrame = consumedFrame; FrameCount = Time.frameCount; Realtime = Time.realtimeSinceStartup;
            PointerObservationCount = pointerObservations; ObservationKind = observationKind;
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
        uint nativeUiPointerObservations;
        NativeUiObservationKind nativeUiObservation;
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
            nativeUiDiagnosticPoint, nativeUiDiagnosticOriginal, nativeUiDiagnosticApplied, nativeUiDiagnosticFrame, nativeUiConsumedFrame,
            nativeUiPointerObservations, nativeUiObservation);

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
            ReleaseNativeUiPostedPress();
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
                nativeUiPointer = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
            if (type == EventType.MouseMove) nativeUiMoves++;
            if (type == EventType.MouseDown) nativeUiDowns++;
            if (type == EventType.MouseUp) nativeUiUps++;
            if (type == EventType.MouseMove || type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.ScrollWheel)
            {
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
        bool nativeUiQueued, nativeUiDiagnosticPass, nativeUiPostedLeftDown;
        Vector2 nativeUiQueuedPoint, nativeUiQueuedDelta;
        EventType nativeUiQueuedType;
        int nativeUiQueuedButton, nativeUiPostedFrame;
        NativeUiWindowSnapshot nativeUiWindowSnapshot;
        public NativeUiWindowSnapshot NativeUiWindowForValidation => nativeUiWindowSnapshot;
        public Vector2 NativeUiDiagnosticDeltaForValidation { get; private set; }
        public string NativeUiDiagnosticErrorForValidation { get; private set; }

        public bool QueueNativeUiEventForValidation(EventType type, Vector2 screenPoint, int button = 0, Vector2 delta = default)
        {
            if (!ValidationMode || Arg("-evolutionFxNativeUiSmoke") == null || nativeUiQueued || !NativeUiVisible ||
                (type != EventType.MouseMove && type != EventType.MouseDown && type != EventType.MouseUp && type != EventType.ScrollWheel) ||
                !FiniteNativeUiPoint(screenPoint) || !FiniteNativeUiPoint(delta) || button != 0 ||
                screenPoint.x < 0 || screenPoint.y < 0 || screenPoint.x >= Screen.width || screenPoint.y >= Screen.height ||
                (type == EventType.ScrollWheel && (delta.x != 0 || delta.y == 0 || Mathf.Abs(delta.y) > 24))) return false;
            nativeUiHostEventCount = 0;
            if (!PostNativeUiWindowEvent(type, screenPoint, delta)) return false;
            nativeUiQueuedType = type; nativeUiQueuedPoint = nativeUiWindowSnapshot.ScreenPoint;
            nativeUiQueuedButton = button; nativeUiQueuedDelta = delta;
            nativeUiPostedFrame = Time.frameCount; nativeUiQueued = true;
            return true;
        }
        static bool FiniteNativeUiPoint(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
        bool NativeUiWindowError(string message)
        {
            NativeUiDiagnosticErrorForValidation = "FX18 own-window message diagnostic: " + message;
            Debug.LogError(NativeUiDiagnosticErrorForValidation); return false;
        }
        void ObserveNativeUiDispatchedEvent()
        {
            if (nativeUiQueued && nativeUiHostEventCount < nativeUiHostEvents.Length)
                nativeUiHostEvents[nativeUiHostEventCount++] = new NativeUiHostEventSnapshot(null);
            if (!nativeUiQueued || Time.frameCount < nativeUiPostedFrame) return;
            EventType raw = Event.current.rawType;
            bool requestedMove = nativeUiQueuedType == EventType.MouseMove;
            // MonoBehaviour does not necessarily receive MouseMove callbacks. A later repaint
            // can prove pointer position only; it is never counted as a delivered mouse event.
            bool repaintPointer = requestedMove && raw == EventType.Repaint && Time.frameCount > nativeUiPostedFrame;
            bool nativeEvent = raw == nativeUiQueuedType || (requestedMove && raw == EventType.MouseDrag);
            if ((!repaintPointer && !nativeEvent) || (nativeEvent && Event.current.button != nativeUiQueuedButton)) return;
            Vector2 point = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            if ((point - nativeUiQueuedPoint).sqrMagnitude > 1) return;
            // Windows messages cannot carry a unique Unity commandName. This is correlation,
            // not physical-input provenance. Never consume or rewrite a foreign event.
            if (raw == EventType.ScrollWheel && (Event.current.delta.y == 0 ||
                Mathf.Sign(Event.current.delta.y) != Mathf.Sign(nativeUiQueuedDelta.y))) return;
            nativeUiQueued = false; nativeUiDiagnosticPass = true;
            nativeUiDiagnosticControlCount = 0;
            nativeUiDiagnosticOriginal = Event.current.type; nativeUiDiagnosticApplied = raw;
            nativeUiDiagnosticPoint = point; nativeUiPointer = point;
            NativeUiDiagnosticDeltaForValidation = Event.current.delta;
            nativeUiDiagnosticFrame = Time.frameCount;
            nativeUiObservation = repaintPointer ? NativeUiObservationKind.RepaintPointer : NativeUiObservationKind.NativeEvent;
            if (requestedMove) nativeUiPointerObservations++;
            if (nativeEvent) nativeUiDiagnosticCount++;
        }

#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativeUiWinRect { public int Left, Top, Right, Bottom; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativeUiWinPoint { public int X, Y; }
        delegate bool NativeUiEnumWindowsCallback(System.IntPtr window, System.IntPtr data);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern bool EnumWindows(NativeUiEnumWindowsCallback callback, System.IntPtr data);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(System.IntPtr window, out uint process);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessId();
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true)]
        static extern int GetClassNameW(System.IntPtr window, System.Text.StringBuilder name, int count);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern bool GetClientRect(System.IntPtr window, out NativeUiWinRect rect);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern bool ClientToScreen(System.IntPtr window, ref NativeUiWinPoint point);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool PostMessageW(System.IntPtr window, uint message, System.UIntPtr wParam, System.IntPtr lParam);
        readonly System.Text.StringBuilder nativeUiWindowClass = new System.Text.StringBuilder(96);
        System.IntPtr nativeUiWindow;
        NativeUiEnumWindowsCallback nativeUiWindowEnumerator;
        bool ValidateNativeUiWindow(System.IntPtr window, out NativeUiWinRect rect)
        {
            rect = default;
            if (window == System.IntPtr.Zero || GetWindowThreadProcessId(window, out uint process) == 0 || process != GetCurrentProcessId()) return false;
            nativeUiWindowClass.Length = 0;
            if (GetClassNameW(window, nativeUiWindowClass, nativeUiWindowClass.Capacity) <= 0 || nativeUiWindowClass.ToString() != "UnityWndClass") return false;
            return GetClientRect(window, out rect) && rect.Left == 0 && rect.Top == 0 &&
                rect.Right > 1 && rect.Bottom > 1 && rect.Right <= short.MaxValue && rect.Bottom <= short.MaxValue;
        }
        bool FindNativeUiWindow(System.IntPtr window, System.IntPtr unused)
        {
            if (!ValidateNativeUiWindow(window, out _)) return true;
            nativeUiWindow = window; return false;
        }
        bool ResolveNativeUiWindow(out NativeUiWinRect rect)
        {
            if (ValidateNativeUiWindow(nativeUiWindow, out rect)) return true;
            nativeUiWindow = System.IntPtr.Zero;
            if (nativeUiWindowEnumerator == null) nativeUiWindowEnumerator = FindNativeUiWindow;
            EnumWindows(nativeUiWindowEnumerator, System.IntPtr.Zero);
            return ValidateNativeUiWindow(nativeUiWindow, out rect);
        }
        static System.IntPtr NativeUiPackedPoint(int x, int y) => new System.IntPtr(unchecked((int)((uint)(ushort)(short)x | ((uint)(ushort)(short)y << 16))));
#endif

        bool PostNativeUiWindowEvent(EventType type, Vector2 requested, Vector2 delta)
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (Screen.width <= 0 || Screen.height <= 0 || !ResolveNativeUiWindow(out var rect))
                return NativeUiWindowError("No validated current-process UnityWndClass client window.");
            int x = Mathf.RoundToInt(requested.x * rect.Right / Screen.width);
            // Pinned Windows player evidence: client row y is reported by IMGUI as row y+1.
            // Keep that pixel-index conversion explicit and independently checked at each size.
            int y = Mathf.RoundToInt(requested.y * rect.Bottom / Screen.height - 1);
            if (x < 0 || x >= rect.Right || y < 0 || y >= rect.Bottom) return NativeUiWindowError("Quantized point is outside the client area.");
            Vector2 represented = new Vector2(x * (float)Screen.width / rect.Right, (y + 1) * (float)Screen.height / rect.Bottom);
            uint message = type == EventType.MouseDown ? 0x0201u : type == EventType.MouseUp ? 0x0202u : type == EventType.ScrollWheel ? 0x020Au : 0x0200u;
            bool nextLeft = type == EventType.MouseDown || (type != EventType.MouseUp && nativeUiPostedLeftDown);
            uint keys = nextLeft ? 1u : 0u;
            int wheel = 0;
            var messagePoint = new NativeUiWinPoint { X = x, Y = y };
            if (type == EventType.ScrollWheel)
            {
                // WM wheel units are 120 per notch; Unity convention is three delta units.
                wheel = -Mathf.RoundToInt(delta.y * 40);
                if (wheel == 0 || wheel < short.MinValue || wheel > short.MaxValue ||
                    !ClientToScreen(nativeUiWindow, ref messagePoint) || messagePoint.X < short.MinValue ||
                    messagePoint.X > short.MaxValue || messagePoint.Y < short.MinValue || messagePoint.Y > short.MaxValue)
                    return NativeUiWindowError("Wheel intent or desktop point cannot be represented safely.");
                keys |= (uint)(ushort)(short)wheel << 16;
            }
            // Revalidate immediately before every post; never address a stale or foreign HWND.
            if (!ValidateNativeUiWindow(nativeUiWindow, out var checkedRect) || checkedRect.Right != rect.Right || checkedRect.Bottom != rect.Bottom)
                return NativeUiWindowError("Window ownership or client size changed before posting.");
            NativeUiQueueCountBeforeForValidation = Event.GetEventCount();
            if (!PostMessageW(nativeUiWindow, message, new System.UIntPtr(keys), NativeUiPackedPoint(messagePoint.X, messagePoint.Y)))
                return NativeUiWindowError("PostMessage failed / Win32 " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            NativeUiQueueCountAfterForValidation = Event.GetEventCount();
            nativeUiPostedLeftDown = nextLeft;
            nativeUiWindowSnapshot = new NativeUiWindowSnapshot(GetCurrentProcessId(), nativeUiWindow, message, rect.Right, rect.Bottom,
                wheel, requested, new Vector2(x, y), represented, new Vector2(messagePoint.X, messagePoint.Y), nextLeft);
            return true;
#else
            return NativeUiWindowError("Requires an explicit Windows development player; editor and other platforms are unsupported.");
#endif
        }
        void ReleaseNativeUiPostedPress()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (!nativeUiPostedLeftDown) return;
            // Only our posted hold is released. Client(1,1) is inside the root clip and outside
            // all existing UI controls, so native capture can release without activating a button.
            if (ValidateNativeUiWindow(nativeUiWindow, out _) && PostMessageW(nativeUiWindow, 0x0202u,
                System.UIntPtr.Zero, NativeUiPackedPoint(1, 1))) nativeUiPostedLeftDown = false;
            else NativeUiWindowError("Could not release this diagnostic's posted left-button hold.");
#endif
        }

#endif
    }
}
