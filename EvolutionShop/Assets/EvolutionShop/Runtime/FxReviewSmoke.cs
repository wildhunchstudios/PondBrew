#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        IEnumerator ReviewClip(string name,float seconds,Action<float> action=null)
        {
            Check(!Application.isEditor,"Motion is captured from the actual standalone HUD / "+name);
            string path=Path.Combine(evidence,name+".mp4");
            using(var recorder=new FxReviewRecorder(ShopSession.Arg("-evolutionFxReviewEncoder"),path))
            {
                while(recorder.Age<seconds)
                {
                    action?.Invoke(recorder.Age);
                    yield return new WaitForEndOfFrame();recorder.Sample();
                    if(recorder.Failure!=null)throw new InvalidOperationException(recorder.Failure);
                }
                float deadline=Time.realtimeSinceStartup+3;
                while(recorder.Pending>0){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Drain review GPU callbacks.");yield return null;}
                var receipt=recorder.Finish();
                File.WriteAllText(Path.Combine(evidence,name+"-frames.json"),JsonUtility.ToJson(receipt,true));
                Check(receipt.frames.Length>=seconds*25 && receipt.achievedFPS>=25 && receipt.missedSamples<=seconds*5,"Measured live recording rate and bounded dropped samples / "+name+" fps="+receipt.achievedFPS+" missed="+receipt.missedSamples);
                Check(File.Exists(path) && new FileInfo(path).Length>1000,"Actual encoded MP4 exists / "+name);
            }
            yield return GateCapture(name+"-still");
        }
        void ReviewAudio()
        {
            var clips=shop.GetComponent<ShopAudio>().LabValidationClips();
            for(int c=0;c<clips.Length;c++)
            {
                var clip=clips[c];float[] samples=new float[clip.samples*clip.channels];Check(clip.GetData(samples,0),"Export original decoded sound / "+clip.name);
                using(var writer=new BinaryWriter(File.Create(Path.Combine(evidence,"sound-"+c+".wav"))))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);writer.Write((short)1);writer.Write((short)clip.channels);writer.Write(clip.frequency);writer.Write(clip.frequency*clip.channels*2);writer.Write((short)(clip.channels*2));writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);foreach(float sample in samples)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
                }
            }
            File.WriteAllText(Path.Combine(evidence,"sound-provenance.txt"),"Six existing original ShopAudioLabFx clips, decoded16-bit mono PCM. These previews do not bake game service gain/ducking or room mix. Videos are silent. Native game listening remains the human FX-06 gate.\n");
        }
        IEnumerator ReviewHarvest(CultureKind kind)
        {
            shop.SetPanel(ShopPanel.None);var station=StationOf(kind==CultureKind.Bloom?StationKind.Bloom:StationKind.Flow).transform;
            GateAim(station.TransformPoint(new Vector3(0,.2f,-1.7f)),station.TransformPoint(new Vector3(0,1.2f,0)));Physics.SyncTransforms();yield return null;yield return null;
            int before=shop.Model.Available(kind);bool held=false,released=false;
            yield return ReviewClip("collect-"+kind.ToString().ToLowerInvariant(),2.4f,age=>
            {
                if(age>=.35f && !held){held=true;cycleMouse.MakeCurrent();InputSystem.QueueStateEvent(cycleMouse,new MouseState{buttons=1});}
                if(age>=1.08f && !released){released=true;InputSystem.QueueStateEvent(cycleMouse,new MouseState());}
            });
            Check(shop.Model.Available(kind)==before+1 && shop.HarvestProgress==0,"One ordinary held unit and release in recorded collection / "+kind);
        }
        IEnumerator FxReviewScenario()
        {
            limitations.Add("FX-06 review: actual live standalone HUD recorded through bounded asynchronous readback at target30FPS with per-frame source timestamps; scripted camera/synthetic held mouse/E and application dose/menu commands. Recording is not a performance benchmark, native input or human creative approval. Videos are silent; six existing decoded clips are exported separately without game gains/ducking. Arrivals/comedy are explicitly quiet for inspecting the sample. Low/reduced are diagnostic policies, not new settings.");
            Check(shop.ValidationMode && Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Review uses its exact isolated save");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=smokeInputSettings;
            syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionFxReviewKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionFxReviewMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();
            int width=int.Parse(ShopSession.Arg("-screen-width")),height=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(width==1280?1920:1280,height==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==width && Screen.height==height,"Actual requested review resolution");
            ReviewAudio();yield return ReviewHarvest(CultureKind.Bloom);yield return ReviewHarvest(CultureKind.Flow);
            var inc=StationOf(StationKind.Incubator).transform;GateAim(inc.TransformPoint(new Vector3(0,.2f,-1.5f)),inc.TransformPoint(new Vector3(0,1.48f,0)));Physics.SyncTransforms();yield return null;yield return null;
            shop.SetPanel(ShopPanel.Incubator);bool bloom=false,flow=false;
            yield return ReviewClip("dose-menu",3,age=>{if(age>=.45f && !bloom){bloom=true;Check(shop.DoseCulture(CultureKind.Bloom),"Recorded guarded Bloom dose");}if(age>=1.4f && !flow){flow=true;Check(shop.DoseCulture(CultureKind.Flow),"Recorded guarded Flow dose");}});
            Check(shop.Model.Staged(CultureKind.Bloom)==1 && shop.Model.Staged(CultureKind.Flow)==1,"Exact recorded Pondlet recipe");bool started=false;
            yield return ReviewClip("incubation-hatch",12,age=>{if(age>=.4f && !started){started=true;Check(shop.BeginManualIncubation(),"Recorded actual ten-second incubation start");shop.SetPanel(ShopPanel.None);}});
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready && shop.Model.IsDiscovered("pondlet"),"Actual ready specimen/discovery after recorded countdown");
            Check(shop.SaveNow(),"Save ready review checkpoint");File.Copy(shop.SavePath,Path.Combine(evidence,"manual-ready-save.json"));string specimen=shop.Model.Incubator.SpecimenId;
            var tank=StationOf(StationKind.TankA).transform;GateAim(tank.TransformPoint(new Vector3(0,.2f,-2)),tank.TransformPoint(new Vector3(0,1.56f,0)));Physics.SyncTransforms();yield return null;yield return null;bool use=false,up=false;
            yield return ReviewClip("hands-place",3,age=>{if(age>=.4f && !use){use=true;shop.AllowValidationInput=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState(Key.E));}if(age>=.48f && !up){up=true;InputSystem.QueueStateEvent(syntheticKeyboard,new KeyboardState());shop.AllowValidationInput=false;}});
            Check(shop.Model.GetTank("tank-a").HasValue && shop.Model.GetTank("tank-a").Value.Id==specimen && shop.World.PondletPlacementCount==1,"Recorded real E placement commits the exact specimen once");
            yield return ReviewClip("pondlet-night",8.4f);Check(shop.SaveNow(),"Save stocked night for native review");File.Copy(shop.SavePath,Path.Combine(evidence,"manual-stocked-save.json"));
            string authority=FxAuthority();shop.World.SetFxPreviewModes(true,true);yield return ReviewClip("pondlet-reduced",3);shop.World.SetFxPreviewModes(false,false);Check(FxAuthority()==authority,"Reduced sample changes no stock/payment/production authority");
            shop.SetPanel(ShopPanel.None);Check(shop.OpenForDay(),"Open ordinary day for daylight sample");yield return ReviewClip("pondlet-day",4);
            GateAim(inc.TransformPoint(new Vector3(0,.2f,-1.5f)),inc.TransformPoint(new Vector3(0,1.48f,0)));Physics.SyncTransforms();yield return null;yield return null;yield return ReviewClip("lab-day-idle",3);
            Check(shop.SaveNow(),"Final isolated review save valid");
        }
    }
}
#endif
