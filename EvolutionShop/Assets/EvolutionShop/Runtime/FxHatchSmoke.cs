#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class HatchFitReport { public string specimen; public int samples, meshes, vertices; public float maxEllipsoid; public Vector3 min,max; }
        void HatchMeshSweep(string name)
        {
            var w=shop.World;var root=w.HatchCreatureForValidation;var station=StationOf(StationKind.Incubator).transform;
            Check(root,"Actual ready Pondlet exists for mesh clearance / "+name);
            var filters=root.GetComponentsInChildren<MeshFilter>();var meshes=new PondletMesh[filters.Length];int vertices=0;
            for(int i=0;i<filters.Length;i++){meshes[i]=new PondletMesh{Root=filters[i].transform,Vertices=filters[i].sharedMesh.vertices};vertices+=meshes[i].Vertices.Length;}
            var report=new HatchFitReport{specimen=shop.Model.Incubator.SpecimenId,meshes=meshes.Length,vertices=vertices,min=Vector3.one*100,max=Vector3.one*-100};
            string authority=FxAuthority();Vector3 stationPosition=station.position;Quaternion stationRotation=station.rotation;
            for(int variant=0;variant<4;variant++)
            {
                bool first=variant%2==0;w.SetFxPreviewModes(variant>=2,false);
                for(int phase=0;phase<8;phase++)for(int step=0;step<=26;step++)
                {
                    float age=step==25?FxHatchChoreography.Release(first):step==26?FxHatchChoreography.Release(first)+.18f*(first?1:.6f):FxHatchChoreography.Duration(first)*step/24;
                    w.SampleHatchPoseForValidation(age,first,phase*.95f);report.samples++;
                    foreach(var m in meshes)foreach(var v in m.Vertices)
                    {
                        Vector3 p=station.InverseTransformPoint(m.Root.TransformPoint(v));report.min=Vector3.Min(report.min,p);report.max=Vector3.Max(report.max,p);
                        Vector3 q=new Vector3(p.x/.69f,(p.y-1.48f)/.48f,p.z/.49f);report.maxEllipsoid=Mathf.Max(report.maxEllipsoid,q.sqrMagnitude);
                    }
                }
            }
            w.SetFxPreviewModes(false,false);w.Animate(0,0);
            File.WriteAllText(Path.Combine(evidence,name+"-swept-mesh.json"),JsonUtility.ToJson(report,true));
            Check(report.maxEllipsoid<1 && report.samples==864,"All actual wake vertices fit inner fluid ellipsoid across first/repeat/reduced and eight idle phases / "+report.maxEllipsoid);
            Check(FxAuthority()==authority && station.position==stationPosition && station.rotation==stationRotation,"Wake sweep leaves frame/root and production authority unchanged");
        }
        IEnumerator FxHatchScenario()
        {
            yield return FxOrganicScenario();
            limitations.Add("FX-06R-02: original six short clips exported and PCM inspected; no audible-listening claim. Actual first/repeat10s batches are in Organic route; additional fresh-slot natural hatch below proves immediate transfer. Save failure uses one explicitly accelerated final boundary step. Mesh clearance samples actual triangles at864 poses per ready batch. No native input or controlled GPU benchmark.");
            LabAudioEvidence();
            GateFresh();shop.SetFxFocusForValidation(true);yield return FxLabPrepare(false);
            yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,12,"natural immediate-collection hatch");
            var w=shop.World;var audio=shop.GetComponent<ShopAudio>();string specimen=shop.Model.Incubator.SpecimenId;
            Check(w.LabHatchRemaining>1.1f,"New Ready is usable during gather, before release finishes");
            int count=w.LabHatchCount;uint sequence=shop.FxActionSequenceForValidation;
            Check(!w.TryFxCue(FxCueId.HatchFirst,sequence) && w.LabHatchCount==count,"Duplicate same-sequence hatch cannot replay");
            Ok(shop.TransferToDisplay("tank-a"),"Immediate guarded transfer during hatch");yield return null;
            Check(shop.Model.GetTank("tank-a").Value.Id==specimen && shop.Model.Incubator.Phase==IncubatorPhase.Dosing && w.LabHatchRemaining==0 && !audio.HatchAudioActive,
                "Immediate exact-ID transfer retires hatch pose/tail/audio without waiting for animation");
            GateFresh();shop.SetFxFocusForValidation(true);yield return FxLabPrepare(false);
            yield return AutomationWait(()=>shop.Model.Incubator.SecondsRemaining<.6f,12,"near-ready save failure boundary");
            Check(shop.SaveNow(),"Preserve actual working state before failure fixture");byte[] durable=File.ReadAllBytes(shop.SavePath);count=w.LabHatchCount;int played=audio.LabPlaybackCount;
            TutorialBlockSave();
            try
            {
                shop.Advance(1);Check(shop.SaveError!=null && w.LabHatchCount==count && audio.LabPlaybackCount==played && !audio.HatchAudioActive && w.FxPool.ActiveCount==0,
                    "Failed saved ready transition has no hatch burst, wake or sound");
                Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed hatch write leaves previous durable slot identical");
            }
            finally { TutorialUnblockSave(); }
            Check(shop.ReloadSaved(),"Reload saved working batch after write failure");shop.SetPanel(ShopPanel.None);shop.FreezeFxVocabularyForValidation=true;
            try
            {
                foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Incubator,ShopPanel.Arrange})
                {
                    shop.SetPanel(ShopPanel.None);w.ResetFxPresentation();Check(w.TryFxCue(FxCueId.HatchFirst,1),"Labeled interruption fixture / "+panel);
                    audio.LabHatch(true);w.Animate(0,.24f);w.Animate(0,.01f);shop.SetPanel(panel);
                    Check(w.LabHatchRemaining==0 && w.FxPool.ActiveCount==0 && !audio.HatchAudioActive,"Panel clears pose, pending births and owned audio / "+panel);
                    Check(!w.TryFxCue(FxCueId.HatchFirst,2),"Hidden/interrupted hatch cannot restart / "+panel);
                }
                shop.SetPanel(ShopPanel.None);w.ResetFxPresentation();w.TryFxCue(FxCueId.HatchFirst,1);audio.LabHatch(true);w.Animate(0,.2f);
                shop.SetFxFocusForValidation(false);Check(w.LabHatchRemaining==0 && w.FxPool.ActiveCount==0 && !audio.HatchAudioActive,"Focus loss clears coordinated hatch");shop.SetFxFocusForValidation(true);
                w.ResetFxPresentation();w.TryFxCue(FxCueId.HatchFirst,1);audio.LabHatch(true);w.Animate(0,.2f);
                var station=StationOf(StationKind.Incubator).transform;Vector3 position=station.position;station.position+=Vector3.right*.05f;w.Animate(0,.01f);yield return null;
                Check(w.LabHatchRemaining==0 && w.FxPool.ActiveCount==0 && !audio.HatchAudioActive,"Moved owner retires wake/emission/sound");station.position=position;w.ResetFxPresentation();
                audio.Feedback(true);audio.StopHatchAudio();Check(!audio.HatchAudioActive,"Hatch-specific stop never claims another service clip");
            }
            finally {shop.FreezeFxVocabularyForValidation=false;shop.SetPanel(ShopPanel.None);w.ResetFxPresentation();audio.StopHatchAudio();}
            Check(shop.ReloadSaved(),"Final saved working state restores without replay");shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
