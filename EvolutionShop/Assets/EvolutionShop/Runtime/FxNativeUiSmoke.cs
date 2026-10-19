#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class NativeUiFrame
        {
            public string clip, panel, moneyKind, stateKind, originalEvent, appliedEvent, phase, observationKind;
            public int renderFrame, appliedFrame, consumedFrame, buttons, currency, reservedCultures;
            public float realtime, moneyAge, stateAge, panelAge, hover, press;
            public long delta;
            public uint moneySequence, stateSequence, diagnosticEvents, pointerObservations, accepted;
            public Vector2 scroll, point;
            public bool paused, reduced, low;
        }
        [Serializable] sealed class NativeUiAction
        {
            public string label, type, observedType, observationKind, controlType, controlRawType;
            public Rect bounds, controlLocalRect;
            public Vector2 point, representedPoint, requestedDelta, observedDelta, controlLocalPoint, controlScreenPoint;
            public NativeUiWindow window;
            public int frame, controlFrame, hotControl;
            public float realtime;
            public uint diagnosticBefore, diagnosticAfter, pointerBefore, pointerAfter, acceptedBefore, acceptedAfter;
            public bool enabled, controlFound, controlEnabled, controlAccepted, controlRectFromRepaint;
        }
        [Serializable] sealed class NativeUiWindow
        {
            public uint processId, message;
            public long handle;
            public int clientWidth, clientHeight, renderWidth, renderHeight, wheelDelta, postedFrame;
            public Vector2 requestedPoint, clientPoint, representedPoint, messagePoint;
            public bool leftDown;
        }
        static NativeUiWindow NativeUiWindowRow(NativeUiWindowSnapshot w) => new NativeUiWindow{processId=w.ProcessId,message=w.Message,handle=w.Handle,clientWidth=w.ClientWidth,clientHeight=w.ClientHeight,renderWidth=w.RenderWidth,renderHeight=w.RenderHeight,wheelDelta=w.WheelDelta,postedFrame=w.PostedFrame,requestedPoint=w.RequestedPoint,clientPoint=w.ClientPoint,representedPoint=w.ScreenPoint,messagePoint=w.MessagePoint,leftDown=w.LeftDown};
        [Serializable] sealed class NativeUiHost
        {
            public string type, rawType;
            public Vector2 localPoint, screenPoint;
            public bool hasCommand, tokenMatches;
            public int frame, queuedEventCount;
            public float realtime;
        }
        [Serializable] sealed class NativeUiReceipt
        {
            public string input = "Explicit Windows development-only own-process HWND posted mouse messages, observed through actual native OnGUI/GUILayout controls. No global cursor/focus/SendInput; not physical-human input. Repaint pointer observations are distinct from native button/wheel events. InputSystem keyboard used only for Escape.";
            public string clock = "EndOfFrame immediately before recorder.Sample; event application frame is independently recorded, not assumed equal.";
            public NativeUiFrame[] frames;
            public NativeUiAction[] actions;
            public NativeUiHost[] latestQueuedHostEvents;
            public int latestQueueCountBefore, latestQueueCountAfter;
            public string diagnosticError;
            public NativeUiWindow latestWindowMessage;
        }
        readonly List<NativeUiFrame> nativeUiFrames = new List<NativeUiFrame>();
        readonly List<NativeUiAction> nativeUiActions = new List<NativeUiAction>();
        bool nativeUiActionDone;
        Exception nativeUiActionFailure;
        Rect nativeUiRect;
        bool nativeUiEnabled;
        void NativeUiObserve(string clip)
        {
            var s = shop.NativeUiFxSnapshot;
            if (s.ButtonCount < 0 || s.ButtonCount > 64 || !CustomerMotionFinite(s.MoneyAge) || !CustomerMotionFinite(s.PanelAge))
                throw new InvalidOperationException("Native UI state escaped fixed capacity/finite clock");
            nativeUiFrames.Add(new NativeUiFrame { clip=clip, panel=shop.Panel.ToString(), renderFrame=Time.frameCount, realtime=Time.realtimeSinceStartup,
                currency=shop.Model.Currency, reservedCultures=shop.Model.ReservedCultureCount, phase=shop.Model.Incubator.Phase.ToString(),
                moneyKind=s.MoneyKind.ToString(), stateKind=s.StateKind.ToString(), delta=s.MoneyDelta, moneyAge=s.MoneyAge, stateAge=s.StateAge,
                panelAge=s.PanelAge, hover=s.HoverAmount, press=s.PressAmount, moneySequence=s.MoneySequence, stateSequence=s.StateSequence,
                diagnosticEvents=s.DiagnosticEventCount, pointerObservations=s.PointerObservationCount, observationKind=s.ObservationKind.ToString(), accepted=s.AcceptedButtonCount, buttons=s.ButtonCount, scroll=s.PanelScroll,
                point=s.DiagnosticScreenPoint, originalEvent=s.DiagnosticOriginalType.ToString(), appliedEvent=s.DiagnosticAppliedType.ToString(),
                appliedFrame=s.DiagnosticAppliedFrame, consumedFrame=s.DiagnosticConsumedFrame, paused=shop.Paused, reduced=s.Reduced, low=s.Low });
        }
        void NativeUiFlush()
        {
            var host=new List<NativeUiHost>();
            for(int i=0;i<shop.NativeUiHostEventCountForValidation;i++)
                if(shop.TryGetNativeUiHostEventForValidation(i,out var sample))host.Add(new NativeUiHost{type=sample.Type.ToString(),rawType=sample.RawType.ToString(),localPoint=sample.LocalPoint,screenPoint=sample.ScreenPoint,hasCommand=sample.HasCommand,tokenMatches=sample.TokenMatches,frame=sample.FrameCount,queuedEventCount=sample.QueuedEventCount,realtime=sample.Realtime});
            File.WriteAllText(Path.Combine(evidence,"native-ui.json"),JsonUtility.ToJson(new NativeUiReceipt{frames=nativeUiFrames.ToArray(),actions=nativeUiActions.ToArray(),latestQueuedHostEvents=host.ToArray(),latestQueueCountBefore=shop.NativeUiQueueCountBeforeForValidation,latestQueueCountAfter=shop.NativeUiQueueCountAfterForValidation,diagnosticError=shop.NativeUiDiagnosticErrorForValidation,latestWindowMessage=NativeUiWindowRow(shop.NativeUiWindowForValidation)},true));
        }
        IEnumerator NativeUiRunAction(IEnumerator action)
        {
            // Manually flatten nested enumerators so an assertion remains observable by the recording owner.
            var stack = new Stack<IEnumerator>(); stack.Push(action);
            while (stack.Count > 0)
            {
                object current = null; bool moved = false;
                try { moved=stack.Peek().MoveNext(); if(moved) current=stack.Peek().Current; }
                catch(Exception e) { nativeUiActionFailure=e; break; }
                if(!moved){stack.Pop();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}
                yield return current;
            }
            nativeUiActionDone=true;
        }
        IEnumerator NativeUiClip(string name,float seconds,IEnumerator action=null)
        {
            try
            {
            nativeUiActionDone=action==null; nativeUiActionFailure=null;
            if(action!=null) StartCoroutine(NativeUiRunAction(action));
            if(Application.isEditor)
            {
                float started=Time.realtimeSinceStartup;
                while(Time.realtimeSinceStartup-started<seconds || !nativeUiActionDone)
                {
                    if(nativeUiActionFailure!=null)throw nativeUiActionFailure;
                    if(Time.realtimeSinceStartup-started>seconds+30)throw new TimeoutException(name);
                    yield return null;NativeUiObserve(name);
                }
            }
            else using(var recorder=new FxReviewRecorder(ShopSession.Arg("-evolutionFxReviewEncoder"),Path.Combine(evidence,name+".mp4")))
            {
                while(recorder.Age<seconds || !nativeUiActionDone)
                {
                    if(nativeUiActionFailure!=null)throw nativeUiActionFailure;
                    if(recorder.Age>seconds+30)throw new TimeoutException(name);
                    yield return new WaitForEndOfFrame();NativeUiObserve(name);recorder.Sample();
                    if(recorder.Failure!=null)throw new InvalidOperationException(recorder.Failure);
                }
                float deadline=Time.realtimeSinceStartup+3;
                while(recorder.Pending>0){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Native UI capture drain");yield return null;}
                var receipt=recorder.Finish();File.WriteAllText(Path.Combine(evidence,name+"-frames.json"),JsonUtility.ToJson(receipt,true));
                Check(receipt.frames.Length>=seconds*25 && receipt.achievedFPS>=25 && receipt.missedSamples<=receipt.wallSeconds*5,"Actual ordinary HUD native UI video / "+name);
            }
            NativeUiFlush(); if(nativeUiActionFailure!=null)throw nativeUiActionFailure;
            }
            finally { NativeUiFlush(); }
        }
        IEnumerator NativeUiFind(string label)
        {
            yield return WaitFor(()=>shop.TryGetNativeUiButtonForValidation(label,out nativeUiRect,out nativeUiEnabled),3,"Actual repaint inventory / "+label);
            Check(nativeUiRect.width>0&&nativeUiRect.height>0,"Observed nonempty native control / "+label);
            for(int attempt=0;attempt<12;attempt++)
            {
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f), top=(Screen.height-720*scale)/2;
                bool footerLabel=label=="Close  /  ESC"||label=="Help  /  F1"||label=="Continue preparation  /  ESC"||label=="Back to my next goal  /  ESC"||label=="Back to my shop  /  ESC or F1";
                bool footer=footerLabel&&nativeUiRect.yMin>=top+568*scale&&nativeUiRect.yMax<=top+620*scale;
                if(footer||(nativeUiRect.yMin>=top+130*scale&&nativeUiRect.yMax<=top+553*scale))yield break;
                yield return NativeUiScrollbar(nativeUiRect.yMin>=top+130*scale);
                yield return null;
                Check(shop.TryGetNativeUiButtonForValidation(label,out nativeUiRect,out nativeUiEnabled),"Control retained after actual scroll / "+label);
            }
            throw new InvalidOperationException("Native button could not be brought into visible content / "+label);
        }
        IEnumerator NativeUiEvent(EventType type,Vector2 point,Vector2 delta=default,string target=null)
        {
            var before=shop.NativeUiFxSnapshot;
            Check(shop.QueueNativeUiEventForValidation(type,point,0,delta),"Single own-window message admitted / "+type+" / "+shop.NativeUiDiagnosticErrorForValidation);
            var posted=shop.NativeUiWindowForValidation;Vector2 represented=posted.ScreenPoint;
            yield return WaitFor(()=>type==EventType.MouseMove?shop.NativeUiFxSnapshot.PointerObservationCount>before.PointerObservationCount:shop.NativeUiFxSnapshot.DiagnosticEventCount>before.DiagnosticEventCount,2,"Actual native input observation / "+type);
            var after=shop.NativeUiFxSnapshot;
            bool pointerOnly=after.ObservationKind==NativeUiObservationKind.RepaintPointer;
            bool actualType=after.DiagnosticAppliedType==type||(type==EventType.MouseMove&&posted.LeftDown&&after.DiagnosticAppliedType==EventType.MouseDrag)||(type==EventType.MouseMove&&pointerOnly&&after.DiagnosticAppliedType==EventType.Repaint);
            Check(actualType&&Vector2.Distance(after.DiagnosticScreenPoint,represented)<.25f&&after.DiagnosticEventCount==before.DiagnosticEventCount+(pointerOnly?0u:1u)&&after.PointerObservationCount==before.PointerObservationCount+(type==EventType.MouseMove?1u:0u),"Exact classified native event or repaint pointer observation / "+type);
            Check(pointerOnly?type==EventType.MouseMove:after.ObservationKind==NativeUiObservationKind.NativeEvent,"Repaint pointer evidence never substitutes for native button or wheel events");
            var row=new NativeUiAction{label=target??"event without target",type=type.ToString(),observedType=after.DiagnosticAppliedType.ToString(),observationKind=after.ObservationKind.ToString(),pointerBefore=before.PointerObservationCount,pointerAfter=after.PointerObservationCount,point=point,representedPoint=represented,requestedDelta=delta,observedDelta=shop.NativeUiDiagnosticDeltaForValidation,window=NativeUiWindowRow(posted),frame=Time.frameCount,realtime=Time.realtimeSinceStartup,diagnosticBefore=before.DiagnosticEventCount,diagnosticAfter=after.DiagnosticEventCount,acceptedBefore=before.AcceptedButtonCount,acceptedAfter=after.AcceptedButtonCount};
            if(target!=null&&shop.TryGetNativeUiControlForValidation(target,out var control))
            {
                row.controlFound=true;row.controlLocalRect=control.LocalRect;row.controlLocalPoint=control.LocalPoint;row.controlScreenPoint=control.ScreenPoint;
                row.controlType=control.Type.ToString();row.controlRawType=control.RawType.ToString();row.hotControl=control.HotControl;row.controlEnabled=control.Enabled;
                row.controlAccepted=control.Accepted;row.controlFrame=control.FrameCount;row.controlRectFromRepaint=control.RectFromRepaint;
            }
            nativeUiActions.Add(row);
            NativeUiFlush();
            if(row.controlFound)Check(Vector2.Distance(row.controlScreenPoint,represented)<.25f,"Actual native control pointer roundtrip matches represented window point / "+target);
            yield return null;
        }
        IEnumerator NativeUiClick(string label)
        {
            yield return NativeUiFind(label);Rect rect=nativeUiRect;bool enabled=nativeUiEnabled;uint accepted=shop.NativeUiFxSnapshot.AcceptedButtonCount;
            Check(enabled,"Enabled native action / "+label);
            yield return NativeUiEvent(EventType.MouseMove,rect.center,target:label);
            yield return NativeUiEvent(EventType.MouseDown,rect.center,target:label);
            Check(shop.TryGetNativeUiControlForValidation(label,out var pressedControl)&&pressedControl.LocalRect.Contains(pressedControl.LocalPoint),"Inside click reaches actual nested native control rectangle / "+label);
            yield return NativeUiEvent(EventType.MouseUp,rect.center,target:label);
            Check(shop.NativeUiFxSnapshot.AcceptedButtonCount==accepted+1,"Exactly one GUILayout button accepted / "+label);
            nativeUiActions.Add(new NativeUiAction{label=label,type="completed native GUILayout click",bounds=rect,enabled=enabled,frame=Time.frameCount,realtime=Time.realtimeSinceStartup,acceptedBefore=accepted,acceptedAfter=shop.NativeUiFxSnapshot.AcceptedButtonCount});
        }
        IEnumerator FxNativeUiScenario()
        {
            limitations.Add("FX18 isolated save. Windows development-only own-process HWND posted mouse messages are observed through real OnGUI/GUILayout controls. Integer client coordinates and represented render coordinates are traced. Wheel automation is blocked: candidate 8 delivered ScrollWheel with an unrelated global-cursor position; this route instead operates the actual native scrollbar with window-posted Down/Up, without moving the global cursor. Mouse positioning may be observed on a later Repaint and is counted separately from actual native Down/Up/Wheel events; held native movement may arrive as MouseDrag. Not physical-human input, and no global cursor/focus/SendInput. InputSystem Escape is separately labelled. Setup seeds carried cultures, currency and stock explicitly; captured incubation uses ordinary frames. Saved failure live-model behavior is not rolled back by this fixture. Static sampled images and silent video do not establish native-human, listening, creative or GPU acceptance.");
            yield return new WaitForSeconds(1);
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX18 isolated save only");
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("FX18 Escape keyboard");cycleMouse=InputSystem.AddDevice<Mouse>("FX18 isolated mouse; IMGUI injection separately traced");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("native-ui-input-provenance.json");
            GateFresh();shop.AllowValidationInput=false;yield return null;
            yield return NativeUiProduction();
            yield return NativeUiStagedPurchase();
            yield return NativeUiSale(false);
            yield return NativeUiSale(true);
            yield return NativeUiSale(false,true);
            yield return NativeUiPanels();
            yield return NativeUiDisabled();
            Check(nativeUiFrames.Count>300&&nativeUiActions.Count>20,"Actual native UI frame/event receipts populated");NativeUiFlush();
        }
        IEnumerator NativeUiProduction()
        {
            shop.Model.Tick(60);Ok(shop.Model.Collect(CultureKind.Bloom),"Explicit carried Bloom preparation");Ok(shop.Model.Collect(CultureKind.Flow),"Explicit carried Flow preparation");shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Prepared cultures saved");
            PlaceUiClickAt(StationKind.Incubator);shop.SetPanel(ShopPanel.Incubator);
            yield return NativeUiClip("ui-hover-press-cancel",2,NativeUiBoundsAndCancel("+ Bloom"));
            Check(shop.Model.Staged(CultureKind.Bloom)==0,"Canceled native press cannot dose");
            yield return NativeUiClip("ui-dose-start",2,NativeUiDoseStart());
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Incubating&&shop.Model.Incubator.SpeciesId=="pondlet"&&shop.Panel==ShopPanel.None,"Actual GUI starts authoritative exact recipe and closes panel");
            uint state=shop.NativeUiFxSnapshot.StateSequence;
            yield return NativeUiClip("ui-progress-ready-discovery",shop.Config.IncubationSeconds+1);
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready&&shop.Model.IsDiscovered("pondlet")&&shop.NativeUiFxSnapshot.StateSequence>state,"Ordinary frame readiness/discovery produces state cue");
            Check(shop.Model.Currency==0&&shop.DisplayedStockCount()==0,"Readiness does not fake stock or money");
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Ready specimen reload");yield return NativeUiClip("ui-ready-reload-quiet",1.3f);
            Check(shop.NativeUiFxSnapshot.StateKind==FxNativeUiCue.None&&shop.NativeUiFxSnapshot.MoneyKind==FxNativeUiCue.None,"Ready reload cannot replay success cues");
        }
        IEnumerator NativeUiDoseStart()
        {
            yield return NativeUiClick("+ Bloom");Check(shop.Model.Staged(CultureKind.Bloom)==1,"GUI Bloom updates actual staging immediately");
            yield return NativeUiClick("+ Flow");Check(shop.Model.Staged(CultureKind.Flow)==1&&shop.Model.ReservedCultureCount==2,"GUI Flow updates actual staging immediately");
            yield return NativeUiClick("Begin incubation  /  "+shop.Config.IncubationSeconds+" sec");
        }
        IEnumerator NativeUiBoundsAndCancel(string label)
        {
            yield return NativeUiFind(label);Rect original=nativeUiRect;uint accepted=shop.NativeUiFxSnapshot.AcceptedButtonCount;
            yield return NativeUiEvent(EventType.MouseMove,original.center,target:label);yield return new WaitForSeconds(.15f);
            Check(shop.NativeUiFxSnapshot.HoverAmount>0,"Actual IMGUI hover accent observed");
            yield return NativeUiFind(label);Check(nativeUiRect==original,"Hover does not move native hit bounds");
            yield return NativeUiEvent(EventType.MouseDown,original.center,target:label);yield return new WaitForSeconds(.12f);
            yield return NativeUiFind(label);Check(nativeUiRect==original,"Held press does not move native hit bounds");
            var outside=new Vector2(10,Screen.height-10);
            yield return NativeUiEvent(EventType.MouseMove,outside,target:label);yield return NativeUiEvent(EventType.MouseUp,outside,target:label);
            Check(shop.NativeUiFxSnapshot.AcceptedButtonCount==accepted,"Drag-out release cancels native action");
            yield return NativeUiEvent(EventType.MouseDown,outside,target:label);yield return NativeUiEvent(EventType.MouseUp,original.center,target:label);
            Check(shop.NativeUiFxSnapshot.AcceptedButtonCount==accepted,"Outside-down inside-up cannot activate native button");
        }
        IEnumerator NativeUiStagedPurchase()
        {
            GateFresh();StaffCreditFixture(100,"FX18 explicitly seeded staged wage purchase");yield return StaffEnter();yield return NativeUiClick("Hire Pip / initially off schedule");
            int cash=shop.Model.Currency;uint sequence=shop.NativeUiFxSnapshot.MoneySequence;string disk=File.ReadAllText(shop.SavePath);string label="Schedule Pip / pay "+ShopStaff.CashierWage+" credits";
            TutorialBlockSave();
            try{yield return NativeUiClip("ui-staged-write-failure",1.5f,NativeUiClick(label));Check(shop.SaveError!=null&&shop.Model.Currency==cash&&!shop.Staff.Scheduled&&File.ReadAllText(shop.SavePath)==disk&&shop.NativeUiFxSnapshot.MoneySequence==sequence,"Failed staged GUI purchase preserves live/disk cash and suppresses spend");}
            finally{TutorialUnblockSave();}
            Check(shop.SaveNow()&&shop.NativeUiFxSnapshot.MoneySequence==sequence,"Unchanged save after failed staged purchase cannot invent spend");
            yield return NativeUiClip("ui-staged-purchase-retry",1.4f,NativeUiClick(label));
            Check(shop.Staff.Scheduled&&shop.Model.Currency==cash-ShopStaff.CashierWage&&shop.NativeUiFxSnapshot.MoneySequence==sequence+1,"Explicit successful GUI retry commits exactly one wage and spend event");
            uint saved=shop.NativeUiFxSnapshot.MoneySequence;Check(shop.SaveNow()&&shop.SaveNow()&&shop.NativeUiFxSnapshot.MoneySequence==saved,"Repeated saved publication cannot replay spend");
        }
        IEnumerator NativeUiSale(bool failure,bool overflow=false)
        {
            GateFresh();if(overflow)StaffCreditFixture(int.MaxValue,"FX18 actual rejected sale overflow boundary");SetReputationFixture(5);SeedStock("pondlet","tank-a");yield return EnterCounter();Check(shop.OpenForDay(),"Real trading day for GUI checkout");shop.SetPanel(ShopPanel.None);
            var visit=Spawn(CustomerArchetypes.Casual);yield return WaitFor(()=>visit.Phase==CustomerPhase.Queued,35,"Actual physical queued order");yield return EnterCounter();
            string label="Serve customer  /  +"+visit.Price+" credits";int cash=shop.Model.Currency;uint sequence=shop.NativeUiFxSnapshot.MoneySequence;string disk=File.ReadAllText(shop.SavePath);
            if(overflow)
            {
                yield return NativeUiClip("ui-overflow-sale-rejected",1.4f,NativeUiClick(label));
                Check(shop.Model.Currency==int.MaxValue&&shop.Reports.Current.sales==0&&shop.Model.GetTank("tank-a").HasValue&&shop.NativeUiFxSnapshot.MoneySequence==sequence,"Actual GUI overflow rejection preserves exact money/specimen and emits no wrapped success");
                yield break;
            }
            if(failure)TutorialBlockSave();
            try{yield return NativeUiClip(failure?"ui-live-sale-write-failure":"ui-saved-sale",1.4f,NativeUiClick(label));
                Check(visit.Outcome==CustomerOutcome.Sold&&shop.Model.Currency==cash+visit.Price&&shop.Reports.Current.sales==1,"GUI checkout authoritative live sale occurs exactly once");
                if(failure)Check(shop.SaveError!=null&&File.ReadAllText(shop.SavePath)==disk&&shop.NativeUiFxSnapshot.MoneySequence==sequence,"Failed live sale leaves old durable bytes and no success cue");
                else Check(shop.NativeUiFxSnapshot.MoneySequence==sequence+1&&shop.NativeUiFxSnapshot.MoneyDelta==visit.Price,"Saved GUI sale emits exact positive delta once");}
            finally{if(failure)TutorialUnblockSave();}
            if(failure){Check(shop.SaveNow()&&shop.NativeUiFxSnapshot.MoneySequence==sequence,"Later successful save cannot replay discarded failed-live-sale delta");}
            int paid=shop.Model.Currency;uint finalSequence=shop.NativeUiFxSnapshot.MoneySequence;
            Check(!shop.ServeCustomer()&&shop.Model.Currency==paid&&shop.NativeUiFxSnapshot.MoneySequence==finalSequence,"Duplicate service rejection cannot repay or replay");
            Check(shop.SaveNow()&&shop.ReloadSaved()&&shop.Model.Currency==paid,"Reload restores latest actually saved cash");yield return NativeUiClip(failure?"ui-live-sale-retry-quiet":"ui-sale-reload-quiet",1);
            Check(shop.NativeUiFxSnapshot.MoneyKind==FxNativeUiCue.None,"Reload clears money presentation");
        }
        IEnumerator NativeUiPanels()
        {
            GateFresh();StaffCreditFixture(int.MaxValue,"FX18 explicit maximum supported currency display");shop.SetPanel(ShopPanel.Pause);
            yield return NativeUiClip("ui-long-values-pause",1.2f);Check(shop.Model.Currency==int.MaxValue&&shop.Paused,"Maximum supported currency remains authoritative under pause");
            shop.SetPanel(ShopPanel.Journal);yield return NativeUiClip("ui-panel-scroll-footer",2.5f,NativeUiScrollFooter());
            shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.Escape));yield return null;yield return null;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());yield return null;
            Check(shop.Panel==ShopPanel.Pause&&shop.Paused,"Separately labelled InputSystem Escape opens pause");
            yield return NativeUiClip("ui-pause-save-resume",1.2f,NativeUiClick("Save and resume"));Check(shop.Panel==ShopPanel.None&&!shop.Paused,"Actual GUI Save and resume exits pause");shop.AllowValidationInput=false;
            foreach(string mode in new[]{"low","reduced"})
            {
                GateFresh();StaffCreditFixture(100,"FX18 comfort wage fixture "+mode);shop.World.SetFxPreviewModes(mode=="reduced",mode=="low");yield return StaffEnter();Check(shop.HireCashier(),"Prepared real off-schedule employee");
                yield return NativeUiClip("ui-money-"+mode,1.4f,NativeUiClick("Schedule Pip / pay "+ShopStaff.CashierWage+" credits"));
                Check(shop.Model.Currency==100-ShopStaff.CashierWage,"Comfort policy preserves exact committed price / "+mode);
            }
            shop.World.SetFxPreviewModes(false,false);shop.SetPanel(ShopPanel.Pause);yield return NativeUiFind("Save and resume");yield return NativeUiEvent(EventType.MouseDown,nativeUiRect.center,target:"Save and resume");shop.SetPanel(ShopPanel.Journal);yield return NativeUiClip("ui-panel-interruption",.8f);
            uint before=shop.NativeUiFxSnapshot.AcceptedButtonCount;yield return NativeUiEvent(EventType.MouseUp,new Vector2(10,10),target:"Save and resume");Check(shop.NativeUiFxSnapshot.AcceptedButtonCount==before,"Panel interruption cannot replay held old button");
            shop.SetFxFocusForValidation(false);yield return null;shop.SetFxFocusForValidation(true);shop.enabled=false;yield return null;shop.enabled=true;yield return null;
            Check(shop.SaveNow()&&shop.ReloadSaved(),"UI focus/disable/reload lifecycle saves normally");yield return NativeUiClip("ui-lifecycle-rest",.8f);Check(shop.NativeUiFxSnapshot.MoneyKind==FxNativeUiCue.None&&shop.NativeUiFxSnapshot.StateKind==FxNativeUiCue.None,"Lifecycle cannot replay monetary/state success");
        }
        IEnumerator NativeUiDisabled()
        {
            GateFresh();SeedStock("pondlet","tank-a");PlaceUiClickAt(StationKind.TankA);shop.SetPanel(ShopPanel.Tank);
            yield return NativeUiClip("ui-disabled-price-control",1.5f,NativeUiDisabledAction());
        }
        IEnumerator NativeUiDisabledAction()
        {
            int price=shop.Model.GetPrice("pondlet").Value.Standard;string label="Use standard price  /  "+price+" credits";
            yield return NativeUiFind(label);Check(!nativeUiEnabled,"Actual standard-price native button is disabled");
            Rect rect=nativeUiRect;uint accepted=shop.NativeUiFxSnapshot.AcceptedButtonCount,sequence=shop.NativeUiFxSnapshot.MoneySequence;
            yield return NativeUiEvent(EventType.MouseDown,rect.center,target:label);yield return NativeUiEvent(EventType.MouseUp,rect.center,target:label);
            Check(shop.NativeUiFxSnapshot.AcceptedButtonCount==accepted&&shop.NativeUiFxSnapshot.MoneySequence==sequence&&shop.Model.GetPrice("pondlet").Value.Price==price,"Disabled actual native control cannot accept or mutate price/money");
        }
        IEnumerator NativeUiScrollbar(bool down)
        {
            // Existing 720p native image actual-hover.png shows track x889..901 (center895).
            // DrawPanel retains content origin377,130, width526, and height423/477.
            bool fixedFooter=shop.Panel!=ShopPanel.Welcome&&shop.Panel!=ShopPanel.Pause&&shop.Panel!=ShopPanel.Reset;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            Vector2 origin=new Vector2((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2);
            Vector2 point=origin+new Vector2(895,down?(fixedFooter?530:584):153)*scale;
            uint accepted=shop.NativeUiFxSnapshot.AcceptedButtonCount;
            yield return NativeUiEvent(EventType.MouseMove,point,target:"native vertical scrollbar track");
            yield return NativeUiEvent(EventType.MouseDown,point,target:"native vertical scrollbar track");
            yield return NativeUiEvent(EventType.MouseUp,point,target:"native vertical scrollbar track");
            Check(shop.NativeUiFxSnapshot.AcceptedButtonCount==accepted,"Native scrollbar cannot activate a content or footer button");
        }
        IEnumerator NativeUiScrollFooter()
        {
            yield return NativeUiFind("Close  /  ESC");Rect footer=nativeUiRect;Vector2 before=shop.NativeUiFxSnapshot.PanelScroll;
            yield return NativeUiScrollbar(true);
            yield return NativeUiFind("Close  /  ESC");Check(nativeUiRect==footer&&shop.NativeUiFxSnapshot.PanelScroll.y>before.y,"Native scrollbar scrolls content while fixed footer hit bounds stay fixed");
            yield return NativeUiClick("Help  /  F1");Check(shop.Panel==ShopPanel.TutorialHelp,"Direct GUILayout footer Help responds through actual GUI");
            yield return NativeUiClick("Back to my shop  /  ESC or F1");
        }
    }
}
#endif
