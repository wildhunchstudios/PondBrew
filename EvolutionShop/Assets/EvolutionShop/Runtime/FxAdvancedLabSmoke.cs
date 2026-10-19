#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        string advancedVat,advancedRack,advancedSpareRack; string[] advancedSlots,advancedSpareSlots;
        [Serializable] sealed class AdvancedFrame
        {
            public float seconds,sourceError,mouthError;
            public int sparkUnits,mistUnits,sparkCells,mistCells,sparkManual,mistManual,sparkModule,mistModule,sparkRefill,mistRefill;
            public int total,ambient,vatCount,vatWorking,vatCompleted,vatCancelled,vatOutput,mouseDevice;
            public string phase,firstId,secondId,firstSpecies,secondSpecies;
            public double remaining;
            public bool held,low,reduced;
        }
        [Serializable] sealed class AdvancedTrace { public string name,context;public AdvancedFrame[] frames; }
        bool advancedLow,advancedReduced;
        Transform AdvancedRoot(CultureKind kind)
        {
            if(kind==CultureKind.Spark){Check(shop.World.TryGetLabFacility(ShopLayout.UpgradeFacilityId,out var root),"Actual Spark research root");return root;}
            if(kind==CultureKind.Mist){Check(shop.World.TryGetConditioner(advancedVat,out var root),"Actual installed Mist vat");return root;}
            return StationOf(kind==CultureKind.Bloom?StationKind.Bloom:StationKind.Flow).transform;
        }
        void AdvancedAim(CultureKind kind)
        {
            var root=AdvancedRoot(kind);
            GateAim(root.TransformPoint(new Vector3(0,.2f,-1.7f)),root.TransformPoint(new Vector3(0,1.25f,0)));Physics.SyncTransforms();
        }
        AdvancedFrame AdvancedSample(float seconds)
        {
            var world=shop.World;var pool=world.FxPool;var spark=world.AdvancedSourceForValidation(CultureKind.Spark);var mist=world.AdvancedSourceForValidation(CultureKind.Mist);
            var vat=world.ConditionerForValidation(advancedVat);
            foreach(var kind in new[]{CultureKind.Spark,CultureKind.Mist}) {
                var source=world.AdvancedSourceForValidation(kind);
                int expected=source.Present?shop.Model.GetGrower(kind).Units:0;
                if(source.ActiveCells!=expected||source.Present&&(!world.FxCues.IsValid(source.Owner)||source.Root==null))throw new InvalidOperationException("Advanced source visual/owner differs from true installed stock / "+kind);
                if(!source.Present&&source.Particles!=0)throw new InvalidOperationException("Absent/locked advanced source emits particles / "+kind);
                if(source.Present&&kind==CultureKind.Spark)for(int i=0;i<source.Particles;i++) {
                    var glass=source.Root.Find("Upgrade reservoir sample");var bounds=glass.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var visual=pool.ParticleVisualForValidation(source.Owner,i);
                    foreach(var vertex in visual.GetComponent<MeshFilter>().sharedMesh.vertices) {
                        Vector3 p=glass.InverseTransformPoint(visual.TransformPoint(vertex))-bounds.center;
                        float radial=p.x*p.x/(bounds.extents.x*bounds.extents.x)+p.z*p.z/(bounds.extents.z*bounds.extents.z);
                        if(radial>1.005f||Mathf.Abs(p.y)>bounds.extents.y+.003f)throw new InvalidOperationException("Actual Spark source mesh escapes its existing glass");
                    }
                }
            }
            var actual=shop.Model.GetConditioner(advancedVat);
            if(vat.Present&&(!actual.HasValue||vat.Count!=actual.Value.Count||vat.FirstId!=actual.Value.First?.Id||vat.SecondId!=actual.Value.Second?.Id||vat.FirstSpecies!=actual.Value.First?.SpeciesId||vat.SecondSpecies!=actual.Value.Second?.SpeciesId))
                throw new InvalidOperationException("Conditioner diagnostic snapshot does not match current authority (render identity is not inferred)");
            if(vat.Present)for(int i=0;i<vat.Particles;i++) {
                var visual=pool.ParticleVisualForValidation(vat.Owner,i);
                if(!visual)throw new InvalidOperationException("Conditioner particle lacks actual pooled mesh");
                foreach(var vertex in visual.GetComponent<MeshFilter>().sharedMesh.vertices) {
                    Vector3 worldPoint=visual.TransformPoint(vertex);bool inside=false;
                    for(int slot=1;slot<=2;slot++) {
                        var water=vat.Root.Find("Conditioning chamber "+slot+"/Conditioning water");
                        var bounds=water.GetComponent<MeshFilter>().sharedMesh.bounds;bounds.Expand(.003f);
                        if(bounds.Contains(water.InverseTransformPoint(worldPoint)))inside=true;
                    }
                    if(!inside)throw new InvalidOperationException("Actual conditioner particle mesh leaves existing chamber water");
                }
            }
            float sourceError=0,mouthError=0;
            if(world.CollectionActive&&shop.Focused&&shop.TryStationCulture(shop.Focused,out var culture)&&FxAdvancedLabMotion.IsAdvanced(culture)) {
                var source=world.AdvancedSourceForValidation(culture);
                for(int i=0;i<world.CollectionParticleCountForValidation;i++) {
                    if(!pool.TryGetParticleForValidation(world.CollectionOwnerForValidation,i,out var a,out var b,out _,out _,out _))throw new InvalidOperationException("Missing advanced intake particle");
                    sourceError=Mathf.Max(sourceError,Vector3.Distance(a,source.Source));mouthError=Mathf.Max(mouthError,Vector3.Distance(b,world.CollectionMouthForValidation));
                }
                if(sourceError>.15f||!advancedReduced&&mouthError>.015f)throw new InvalidOperationException("Advanced intake source/nozzle attachment failure");
            }
            int ambient=0;for(int i=FxEffectPool.ActionReserved;i<FxEffectPool.Capacity;i++)if(pool.Root.GetChild(i).gameObject.activeSelf)ambient++;
            if(pool.ActiveCount>(advancedLow?32:96)||ambient>(advancedLow?8:32))throw new InvalidOperationException("Advanced lab exceeded actual total/ambient pool policy");
            return new AdvancedFrame{seconds=seconds,sparkUnits=spark.Units,mistUnits=mist.Units,sparkCells=spark.ActiveCells,mistCells=mist.ActiveCells,
                sparkManual=spark.ManualCount,mistManual=mist.ManualCount,sparkModule=spark.ModuleCount,mistModule=mist.ModuleCount,sparkRefill=spark.RefillCount,mistRefill=mist.RefillCount,
                total=pool.ActiveCount,ambient=ambient,phase=vat.Phase.ToString(),vatCount=vat.Count,vatWorking=vat.WorkingAdmitted,vatCompleted=vat.CompletedCount,vatCancelled=vat.CancelledCount,vatOutput=vat.OutputCount,
                firstId=vat.FirstId,secondId=vat.SecondId,firstSpecies=vat.FirstSpecies,secondSpecies=vat.SecondSpecies,remaining=vat.SecondsRemaining,
                sourceError=sourceError,mouthError=mouthError,mouseDevice=Mouse.current!=null?Mouse.current.deviceId:-1,held=cycleMouse.leftButton.isPressed,low=advancedLow,reduced=advancedReduced};
        }
        void AdvancedResources(string context)
        {
            var pool=shop.World.FxPool;
            Check(pool.OwnedCount==96&&pool.OwnedMeshCount==5&&pool.OwnedMaterialCount==1&&pool.Root.GetComponentsInChildren<Collider>(true).Length==0&&shop.GetComponents<AudioSource>().Length==5,
                "Advanced lab retains96 carriers/5 meshes/1 material/no particle colliders/five bounded voices (3 short +2 loops) / "+context);
        }
        IEnumerator AdvancedClip(string name,float duration,Action<float> action=null)
        {
            var frames=new List<AdvancedFrame>();Action<float> sample=age=>{frames.Add(AdvancedSample(age));action?.Invoke(age);};
            if(Application.isEditor){float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<duration){sample(Time.realtimeSinceStartup-began);yield return null;}}
            else yield return ReviewClip(name,duration,sample);
            File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(new AdvancedTrace{name=name,context="Actual normal-time commands; scripted operator; diagnostic Low/reduced values recorded per sample",frames=frames.ToArray()},true));
            Check(frames.Count>2,"Actual advanced lab motion samples / "+name);AdvancedResources(name);
        }
        IEnumerator AdvancedClear()
        {
            DoseAim();
            if(shop.Model.Incubator.Phase==IncubatorPhase.Dosing&&shop.Model.Staged(CultureKind.Bloom)+shop.Model.Staged(CultureKind.Flow)+shop.Model.Staged(CultureKind.Spark)+shop.Model.Staged(CultureKind.Mist)>0)
                Check(shop.ClearCultureDoses(),"Guarded recovery of staged advanced cultures");
            foreach(var kind in new[]{CultureKind.Bloom,CultureKind.Flow,CultureKind.Spark,CultureKind.Mist})while(shop.Model.Available(kind)>0)Check(shop.ReleaseCulture(kind),"Guarded surplus release / "+kind);
            shop.SetPanel(ShopPanel.None);yield return null;
        }
        IEnumerator AdvancedHarvest(CultureKind kind,int count)
        {
            shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;AdvancedAim(kind);yield return null;yield return null;
            Check(shop.Focused&&shop.TryStationCulture(shop.Focused,out var focused)&&focused==kind&&shop.ScriptedHarvest==null,"Real source focus / "+kind);
            int target=shop.Model.Available(kind)+count;float deadline=Time.realtimeSinceStartup+12;CollectionMouse(true);
            while(shop.Model.Available(kind)<target){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Advanced real harvest / "+kind);yield return null;}
            yield return CollectionRelease();Check(shop.Model.Available(kind)==target,"Exact real held culture count / "+kind);
        }
        IEnumerator AdvancedProduce(string species)
        {
            yield return AdvancedClear();var recipe=shop.Model.GetSpecies(species);
            foreach(var kind in new[]{CultureKind.Bloom,CultureKind.Flow,CultureKind.Spark,CultureKind.Mist}) {
                int needed=ShopAutomation.Required(recipe,kind);if(needed>0)yield return AdvancedHarvest(kind,needed);
            }
            DoseAim();foreach(var kind in new[]{CultureKind.Bloom,CultureKind.Flow,CultureKind.Spark,CultureKind.Mist})
                for(int i=0;i<ShopAutomation.Required(recipe,kind);i++)Check(shop.DoseCulture(kind),"Guarded manual recipe dose / "+species+" / "+kind);
            Check(shop.BeginManualIncubation(),"Real normal-time manual incubation / "+species);
            yield return WaitFor(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,16,"Advanced fixture normal incubation / "+species);
            Check(shop.Model.Incubator.SpeciesId==species,"Actual mature recipe / "+species);
        }
        IEnumerator AdvancedSourceCycle(CultureKind kind)
        {
            yield return AdvancedClear();yield return GrowerWaitFull(kind);AdvancedAim(kind);yield return null;yield return null;
            var before=shop.World.AdvancedSourceForValidation(kind);int stage=0;bool empty=false,partial=false,full=false;
            yield return AdvancedClip("advanced-"+kind.ToString().ToLowerInvariant()+"-cycle-live",17,age=>{
                int units=shop.Model.GetGrower(kind).Units;
                if(stage==0&&age>=.35f){CollectionMouse(true);stage=1;}
                if(stage==1&&units==0){empty=true;CollectionMouse(false);stage=2;}
                if(stage==2&&units>0&&units<shop.Model.GetGrower(kind).Capacity)partial=true;
                if(stage==2&&units==shop.Model.GetGrower(kind).Capacity)full=true;
            });
            yield return CollectionRelease();var after=shop.World.AdvancedSourceForValidation(kind);int accepted=shop.Model.Available(kind);
            Check(empty&&partial&&full&&accepted>0&&accepted<shop.Model.Capacity,"Real advanced depletion/refill/full with unaccelerated clocks / "+kind);
            Check(after.ManualCount==before.ManualCount+accepted&&after.ModuleCount==before.ModuleCount&&after.RefillCount==before.RefillCount+accepted,
                "Saved manual acceptance and real refill edges match actual culture changes / "+kind);
            Check(after.AmbientAdmitted>before.AmbientAdmitted&&after.AccentAdmitted>before.AccentAdmitted,"Advanced source emitted actual visible ambient and action particles / "+kind);
            yield return new WaitForSeconds(1.5f);int requests=shop.World.AdvancedSourceForValidation(kind).AmbientRequests;
            yield return new WaitForSeconds(.5f);Check(shop.World.AdvancedSourceForValidation(kind).AmbientRequests==requests,"Full advanced stock has no continuing refill particles / "+kind);
        }
        IEnumerator AdvancedStoreReady(string slot)
        {yield return SpecimenRackPanel(advancedRack);Check(shop.StoreReadyInRack(slot),"Real rack receives exact prepared specimen");}
        IEnumerator AdvancedBeginBatch(params string[] slots)
        {
            yield return AdvancedClear();yield return AdvancedHarvest(CultureKind.Spark,1);yield return ConditioningPanel(advancedVat);
            foreach(string slot in slots)Check(shop.ToggleConditioningInput(slot,shop.Model.GetStorage(slot).Value.Id),"Choose exact Pondlet at actual vat");
            Check(shop.BeginSelectedConditioning()&&shop.SaveError==null,"Real saved conditioning begins");shop.SetPanel(ShopPanel.None);
        }
        IEnumerator AdvancedConditioning()
        {
            yield return AdvancedProduce("pondlet");yield return AdvancedStoreReady(advancedSlots[0]);
            yield return AdvancedProduce("pondlet");yield return AdvancedStoreReady(advancedSlots[1]);
            string first=shop.Model.GetStorage(advancedSlots[0]).Value.Id,second=shop.Model.GetStorage(advancedSlots[1]).Value.Id;
            var before=shop.World.ConditionerForValidation(advancedVat);
            yield return AdvancedBeginBatch(advancedSlots);AdvancedAim(CultureKind.Mist);
            bool cancelled=false;
            yield return AdvancedClip("advanced-conditioning-cancel-live",4,age=>{
                if(!cancelled&&age>=.7f){shop.SetPanel(ShopPanel.Conditioning);Check(shop.CancelSelectedConditioning(),"Actual saved cancel");shop.SetPanel(ShopPanel.None);cancelled=true;}
            });
            var canceled=shop.World.ConditionerForValidation(advancedVat);var state=shop.Model.GetConditioner(advancedVat).Value;
            Check(cancelled&&state.Phase==ConditionerPhase.Ready&&state.First.Value.Id==first&&state.Second.Value.Id==second&&state.First.Value.SpeciesId=="pondlet"&&state.Second.Value.SpeciesId=="pondlet"&&shop.Model.Available(CultureKind.Spark)==1,
                "Canceled Ready retains original two Pondlets and refunds one Spark");
            Check(canceled.CancelledCount==before.CancelledCount+1&&canceled.CompletedCount==before.CompletedCount,"Canceled Ready is not a successful transformation cue");
            yield return ConditioningPanel(advancedVat);Check(!shop.CancelSelectedConditioning(),"Repeated cancel cannot replay a response/refund");
            yield return SpecimenRackPanel(advancedRack);Check(shop.StoreConditionerInRack(advancedVat,0,advancedSlots[0],first)&&shop.StoreConditionerInRack(advancedVat,1,advancedSlots[1],second),"Recover actual canceled residents");
            before=shop.World.ConditionerForValidation(advancedVat);yield return AdvancedBeginBatch(advancedSlots);AdvancedAim(CultureKind.Mist);
            yield return AdvancedClip("advanced-conditioning-batch-live",14);
            var ready=shop.World.ConditionerForValidation(advancedVat);state=shop.Model.GetConditioner(advancedVat).Value;
            Check(state.Phase==ConditionerPhase.Ready&&state.Count==2&&state.First.Value.Id==first&&state.Second.Value.Id==second&&state.First.Value.SpeciesId=="lanternfin"&&state.Second.Value.SpeciesId=="lanternfin",
                "Natural12s transformation preserves both exact IDs");
            Check(ready.StartedCount==before.StartedCount+1&&ready.CompletedCount==before.CompletedCount+1&&ready.WorkingAdmitted>before.WorkingAdmitted&&ready.CompletedFirstId==first&&ready.CompletedSecondId==second,
                "One saved batch start/complete receipt with actual working particles and matching IDs");
            shop.SetPanel(ShopPanel.Pause);int completed=ready.CompletedCount;Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload real Ready conditioning");
            Check(shop.World.ConditionerForValidation(advancedVat).CompletedCount==completed,"Ready reload never replays completion");shop.SetPanel(ShopPanel.None);
            yield return SpecimenDisplayPanel("tank-a");Check(shop.StockDisplayFromConditioner(advancedVat,0,first),"Collect exact first conditioned output to display");
            var partial=shop.World.ConditionerForValidation(advancedVat);Check(partial.Count==1&&partial.FirstId==null&&partial.SecondId==second,"Partial Ready preserves chamber hole and second identity");
            Check(!shop.StockDisplayFromConditioner(advancedVat,0,first),"Repeated output collection cannot succeed twice");
            yield return SpecimenDisplayPanel("tank-b");Check(shop.StockDisplayFromConditioner(advancedVat,1,second),"Collect exact second output to display");
            Check(shop.World.ConditionerForValidation(advancedVat).OutputCount==ready.OutputCount+2&&shop.Model.GetConditioner(advancedVat).Value.Count==0,"Two saved output cues drain to empty once");
        }
        bool AdvancedFindPosition(string id,bool facility,int preferredX,int preferredZ,out int x,out int z)
        {
            for(int radius=0;radius<=6;radius++)for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++) {
                if(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dz))!=radius)continue;
                int cx=preferredX+dx,cz=preferredZ+dz;bool legal=true;
                // A relocation fixture must actually move, including its first quarter-turn.
                if(id!=null) {
                    if(facility) {var current=shop.Layout.GetFacility(id);if(current.HasValue&&current.Value.X==cx&&current.Value.Z==cz)continue;}
                    else {var current=shop.Layout.GetItem(id);if(current.HasValue&&current.Value.X==cx&&current.Value.Z==cz)continue;}
                }
                for(int turn=0;turn<4&&legal;turn++) {
                    ShopLayout candidate;var result=facility?shop.Layout.TryPreviewFacility(id,cx,cz,turn,out candidate):shop.Layout.TryPreview(ShopLayout.ConditionerDefinitionId,id,cx,cz,turn,out candidate);
                    if(!result.Success){legal=false;break;}
                    Vector3 center;
                    if(facility){var item=candidate.GetFacility(id).Value;center=new Vector3(item.WorldX,.2f,item.WorldZ);}
                    else center=new Vector3(cx*ShopLayout.GridSize,.2f,cz*ShopLayout.GridSize);
                    var rotation=Quaternion.Euler(0,turn*90,0);
                    foreach(float distance in new[]{1.7f,1.9f}) {
                        var point=center+rotation*Vector3.back*distance;
                        if(!candidate.CanReachMainAisle(point.x,point.z)||!shop.Layout.CanReachMainAisle(point.x,point.z)){legal=false;break;}
                    }
                }
                if(legal){x=cx;z=cz;return true;}
            }
            x=z=0;return false;
        }
        IEnumerator AdvancedMoves(CultureKind culture)
        {
            yield return AdvancedClear();yield return GrowerWaitFull(culture);
            bool bench=culture==CultureKind.Spark;string id=bench?ShopLayout.UpgradeFacilityId:advancedVat;var original=AdvancedRoot(culture);
            Check(AdvancedFindPosition(id,bench,bench?3:-6,bench?18:14,out int x,out int z),"Find actual all-quarter-turn legal advanced source position / "+culture);
            var frames=new List<AdvancedFrame>();
            for(int turn=0;turn<4;turn++) {
                var old=shop.World.AdvancedSourceForValidation(culture);var oldVat=shop.World.ConditionerForValidation(advancedVat);
                Vector3 center;
                if(bench){var f=shop.Layout.GetFacility(id).Value;center=new Vector3(f.WorldX+(x-f.X)*ShopLayout.GridSize,.2f,f.WorldZ+(z-f.Z)*ShopLayout.GridSize);}
                else center=new Vector3(x*ShopLayout.GridSize,.2f,z*ShopLayout.GridSize);
                var position=center+Quaternion.Euler(0,turn*90,0)*Vector3.back*1.9f;
                if(bench)yield return LabMove(id,x,z,turn,position);else yield return EditedPlace(id,ShopLayout.ConditionerDefinitionId,x,z,turn,position);
                Check(AdvancedRoot(culture)==original&&!shop.World.FxCues.IsValid(old.Owner)&&shop.World.FxPool.ParticleCountForValidation(old.Owner)==0,"Moved source retires prior effects and preserves original root / "+culture+" / "+turn);
                shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;AdvancedAim(culture);yield return null;yield return null;
                var after=shop.World.AdvancedSourceForValidation(culture);
                Check(after.ManualCount==old.ManualCount&&after.ModuleCount==old.ModuleCount&&after.RefillCount==old.RefillCount,"Moving does not invent acceptance/refill / "+culture+" / "+turn);
                if(!bench)Check(!shop.World.FxCues.IsValid(oldVat.Owner)&&shop.World.FxPool.ParticleCountForValidation(oldVat.Owner)==0,"Moved vat retires its previous working owner");
                CollectionMouse(true);yield return new WaitForSeconds(.10f);frames.Add(AdvancedSample(turn));
                Check(shop.World.CollectionActive&&shop.World.CollectionParticleCountForValidation>0&&shop.Model.Available(culture)==0,"Real pre-acceptance intake at legal moved orientation / "+culture+" / "+turn);
                yield return CollectionRelease();
            }
            File.WriteAllText(Path.Combine(evidence,"advanced-"+culture.ToString().ToLowerInvariant()+"-rotations.json"),JsonUtility.ToJson(new AdvancedTrace{name="Actual legal saved quarter-turns",frames=frames.ToArray()},true));
            string layout=JsonUtility.ToJson(shop.Layout.CaptureSave());Check(shop.SaveNow()&&shop.ReloadSaved()&&layout==JsonUtility.ToJson(shop.Layout.CaptureSave()),"Reload exact moved advanced layout");
            shop.SetPanel(ShopPanel.None);AdvancedAim(culture);yield return null;yield return null;
            int before=shop.World.AdvancedSourceForValidation(culture).ManualCount;bool started=false,released=false;
            yield return AdvancedClip("advanced-"+culture.ToString().ToLowerInvariant()+"-relocated-live",3,age=>{
                if(!started&&age>=.3f){CollectionMouse(true);started=true;}
                if(started&&!released&&shop.Model.Available(culture)>=3){CollectionMouse(false);released=true;}
            });
            yield return CollectionRelease();Check(released&&shop.Model.Available(culture)==3&&shop.World.AdvancedSourceForValidation(culture).ManualCount==before+3,"Moved source admits exactly3 saved units");
        }
        IEnumerator AdvancedBoundaries()
        {
            foreach(var culture in new[]{CultureKind.Spark,CultureKind.Mist}) {
                yield return AdvancedClear();yield return GrowerWaitFull(culture);
                foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Arrange,ShopPanel.Journal}) {
                    shop.SetPanel(ShopPanel.None);AdvancedAim(culture);yield return null;yield return null;int accepted=shop.World.AdvancedSourceForValidation(culture).ManualCount;
                    CollectionMouse(true);yield return new WaitForSeconds(.10f);var owner=shop.World.CollectionOwnerForValidation;
                    Check(shop.World.CollectionActive,"Real advanced intake before panel interruption / "+culture);
                    shop.SetPanel(panel);CollectionMouse(false);yield return null;yield return null;
                    Check(shop.World.FxPool.ParticleCountForValidation(owner)==0&&!shop.World.CollectionActive&&shop.World.AdvancedSourceForValidation(culture).ManualCount==accepted,"Panel retires only relevant active intake without success / "+culture+" / "+panel);
                }
                shop.SetPanel(ShopPanel.None);AdvancedAim(culture);yield return null;yield return null;
                Check(shop.SaveNow(),"Durable advanced source before failing held save");byte[] durable=File.ReadAllBytes(shop.SavePath);var before=shop.World.AdvancedSourceForValidation(culture);
                TutorialBlockSave();
                try {
                    CollectionMouse(true);yield return WaitFor(()=>shop.SaveError!=null,3,"Advanced held collection save failure");yield return null;yield return null;
                    Check(shop.World.AdvancedSourceForValidation(culture).ManualCount==before.ManualCount&&!shop.World.CollectionActive&&shop.World.CollectionParticleCountForValidation==0,"Failed saved unit produces no advanced accepted cue/continuing intake / "+culture);
                    Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed advanced write preserves durable bytes / "+culture);
                }
                finally {CollectionMouse(false);TutorialUnblockSave();}
                yield return null;yield return null;int refill=shop.World.AdvancedSourceForValidation(culture).RefillCount;
                Check(shop.ReloadSaved(),"Restore durable advanced state after failed save");yield return null;
                Check(shop.World.AdvancedSourceForValidation(culture).RefillCount==refill&&shop.World.AdvancedSourceForValidation(culture).ManualCount==before.ManualCount,"Adopting restored advanced stock never invents refill/success");
            }
            shop.SetPanel(ShopPanel.None);yield return AdvancedClear();
            var prior=shop.World.AdvancedSourceForValidation(CultureKind.Mist);var priorVat=shop.World.ConditionerForValidation(advancedVat);var item=shop.Layout.GetItem(advancedVat).Value;
            yield return ConditioningPanel(advancedVat);Check(shop.OpenOwnedFurnitureMenu()&&shop.StoreFurniture(advancedVat),"Actually store empty conditioner");
            yield return null;yield return null;
            Check(!shop.World.TryGetConditioner(advancedVat,out _)&&!shop.World.AdvancedSourceForValidation(CultureKind.Mist).Present&&!shop.World.ConditionerForValidation(advancedVat).Present&&
                shop.World.FxPool.ParticleCountForValidation(prior.Owner)==0&&shop.World.FxPool.ParticleCountForValidation(priorVat.Owner)==0,"Absent stored vat has neither Mist nor conditioning effects");
            int mistUnits=shop.Model.GetGrower(CultureKind.Mist).Units;yield return new WaitForSeconds(.4f);
            Check(shop.Model.GetGrower(CultureKind.Mist).Units>=mistUnits,"Storing presentation does not reset global Mist authority");
            var position=new Vector3(item.X*ShopLayout.GridSize,.2f,item.Z*ShopLayout.GridSize)+Quaternion.Euler(0,item.QuarterTurns*90,0)*Vector3.back*1.9f;
            yield return EditedPlace(advancedVat,ShopLayout.ConditionerDefinitionId,item.X,item.Z,item.QuarterTurns,position);
            shop.SetPanel(ShopPanel.None);yield return null;Check(shop.World.AdvancedSourceForValidation(CultureKind.Mist).Present&&shop.Model.GetConditioner(advancedVat).Value.Count==0,"Actual redeployment restores one empty machine and existing Mist");
        }
        IEnumerator AdvancedSingleBatchRecovery()
        {
            yield return AdvancedProduce("pondlet");yield return AdvancedStoreReady(advancedSlots[0]);string id=shop.Model.GetStorage(advancedSlots[0]).Value.Id;
            yield return AdvancedBeginBatch(advancedSlots[0]);
            shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow(),"Save exact active single-input batch");
            byte[] durable=File.ReadAllBytes(shop.SavePath);float remaining=shop.Model.GetConditioner(advancedVat).Value.SecondsRemaining;
            var before=shop.World.ConditionerForValidation(advancedVat);
            Check(shop.ReloadSaved()&&shop.Model.GetConditioner(advancedVat).Value.SecondsRemaining==remaining,"Paused working reload preserves exact remaining time");
            yield return new WaitForSeconds(.25f);Check(shop.Model.GetConditioner(advancedVat).Value.SecondsRemaining==remaining,"Paused advanced conditioning cannot progress");
            shop.SetPanel(ShopPanel.None);AdvancedAim(CultureKind.Mist);
            yield return WaitFor(()=>shop.Model.GetConditioner(advancedVat).Value.SecondsRemaining<.3f,15,"Prepare actual completion save obstruction");
            // Preserve the most recent valid slot: previous normal autosaves may have advanced its clock.
            durable=File.ReadAllBytes(shop.SavePath);TutorialBlockSave();
            try {
                yield return WaitFor(()=>shop.Model.GetConditioner(advancedVat).Value.Phase==ConditionerPhase.Ready,3,"Natural completion while saving fails");
                yield return null;yield return null;
                Check(shop.SaveError!=null&&shop.World.ConditionerForValidation(advancedVat).CompletedCount==before.CompletedCount,"Unsaved natural completion has no saved-success celebration");
                Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed completion leaves durable source unchanged (no live rollback claim)");
            }
            finally {TutorialUnblockSave();}
            Check(shop.ReloadSaved(),"Restore real durable working batch after failed completion");shop.SetPanel(ShopPanel.None);
            Check(shop.Model.GetConditioner(advancedVat).Value.Phase==ConditionerPhase.Processing&&shop.Model.GetConditioner(advancedVat).Value.First.Value.Id==id,"Recovery restores exact still-working single resident");
            yield return WaitFor(()=>shop.Model.GetConditioner(advancedVat).Value.Phase==ConditionerPhase.Ready,16,"Recovered single batch natural completion");
            var after=shop.World.ConditionerForValidation(advancedVat);
            Check(after.Count==1&&after.FirstId==id&&after.FirstSpecies=="lanternfin"&&after.SecondId==null&&after.CompletedCount==before.CompletedCount+1,"Recovered one-input batch completes exactly once with preserved identity");
            yield return SpecimenRackPanel(advancedRack);Check(shop.StoreConditionerInRack(advancedVat,0,advancedSlots[0],id),"Collect recovered single output through real rack command");
        }
        IEnumerator AdvancedAutomatic()
        {
            yield return AdvancedProduce("driftwing");yield return SpecimenRackPanel(advancedSpareRack);Check(shop.StoreReadyInRack(advancedSpareSlots[1]),"Store manually discovered Driftwing before automatic known recipe");
            yield return AdvancedClear();yield return AutomationEnter();
            Check(shop.PurchaseAutomationModule(true)&&shop.PurchaseAutomationModule(false)&&shop.SetAutomationEnabled(true,true)&&shop.SetAutomationEnabled(false,true),"Actual paid Collector/Doser modules enabled");
            foreach(string recipe in new[]{"lanternfin","driftwing"}) {
                yield return AdvancedClear();yield return AutomationEnter();
                Check(shop.SelectAutomationRecipe(recipe)&&shop.RequestAutomationBatch(),"Real known advanced automatic preparation request / "+recipe);
                var culture=recipe=="lanternfin"?CultureKind.Spark:CultureKind.Mist;var before=shop.World.AdvancedSourceForValidation(culture);
                shop.SetPanel(ShopPanel.None);AdvancedAim(culture);
                if(recipe=="lanternfin")yield return AdvancedClip("advanced-automatic-sources-live",8);
                else {yield return AutomationWait(()=>!shop.Automation.IsPending,12,"Mist automatic preparation");AdvancedSample(0);}
                yield return AutomationWait(()=>!shop.Automation.IsPending,12,"Advanced module request completion");
                var after=shop.World.AdvancedSourceForValidation(culture);
                Check(after.ModuleCount==before.ModuleCount+1&&after.ManualCount==before.ManualCount&&!shop.World.CollectionActive&&shop.World.CollectionParticleCountForValidation==0,
                    "Actual automatic advanced unit has source-only saved response, no player stream / "+culture);
                Check(shop.Model.Staged(culture)==1&&shop.Model.Incubator.Phase==IncubatorPhase.Dosing,"Automatic preparation respects existing manual-start boundary / "+recipe);
            }
            yield return AdvancedClear();yield return AutomationEnter();Check(shop.SetAutomationEnabled(true,false)&&shop.SetAutomationEnabled(false,false),"Quiet actual automation after advanced source validation");shop.SetPanel(ShopPanel.None);
        }
        IEnumerator AdvancedComfort()
        {
            yield return AdvancedClear();yield return AdvancedHarvest(CultureKind.Spark,3);AdvancedAim(CultureKind.Spark);
            advancedLow=true;advancedReduced=false;shop.World.SetFxPreviewModes(false,true);bool switched=false;
            yield return AdvancedClip("advanced-low-reduced-live",8,age=>{
                if(!switched&&age>=4){advancedLow=false;advancedReduced=true;shop.World.SetFxPreviewModes(true,false);AdvancedAim(CultureKind.Spark);switched=true;}
            });
            Check(switched&&shop.Model.Available(CultureKind.Spark)==3,"Comfort policies do not invent stock or accepted collection");
            advancedLow=advancedReduced=false;shop.World.SetFxPreviewModes(false,false);
        }
        IEnumerator FxAdvancedLabScenario()
        {
            limitations.Add("FX08 isolated normal-time saved manual commands, synthetic held input and real legal placement/rotation commits. Starter discovery preparation and1000 purchase credits are explicitly labeled setup fixtures; no accelerated source refills or conditioning. Saved command and completion failures use a temporary isolated save-path obstruction; in-memory mutation is not called rollback. Automatic coverage is actual Collector/Doser source preparation plus natural vat completion; no nonexistent automatic vat loader. Low/reduced are diagnostic policies. Videos are silent and scripted; native/listening/creative/GPU acceptance remains deferred.");
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Advanced lab exact isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit advanced-lab tutorial skip");
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionAdvancedLabKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionAdvancedLabMouse");
            syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("advanced-lab-input-provenance.json");
            if(!Application.isEditor){int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w&&Screen.height==h,"Exact advanced lab capture resolution");}
            Check(!shop.World.AdvancedSourceForValidation(CultureKind.Spark).Present&&!shop.World.AdvancedSourceForValidation(CultureKind.Mist).Present&&shop.Model.ConditionerIds.Length==0,"Fresh locked/absent sources have no active FX owners");
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);Check(!shop.ResearchSpark(),"Actual locked Spark research refusal");
            shop.SetPanel(ShopPanel.None);CollectionMouse(true);yield return new WaitForSeconds(.4f);yield return CollectionRelease();Check(shop.Model.Available(CultureKind.Spark)==0&&!shop.World.AdvancedSourceForValidation(CultureKind.Spark).Present,"Locked held bench cannot invent Spark or effects");
            StaffCreditFixture(1000,"FX08 construction/research/modules only; no earned-economy claim");
            foreach(string species in new[]{"pondlet","bubblebelly","ribbonfin"}){SpecimenProduce(species);Ok(shop.Model.TransferToTank("tank-a"),"Labeled starter prerequisite fixture transfer");Ok(shop.Model.SellFromTank("tank-a"),"Labeled starter prerequisite fixture sale");}
            shop.World.Refresh(shop.Model);Check(shop.SaveNow(),"Save explicit starter prerequisite fixture");
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);int cash=shop.Model.Currency;
            Check(shop.ResearchSpark()&&shop.Model.Currency==cash-shop.Config.SparkResearchCost&&!shop.Model.IsDiscovered("lanternfin"),"Actual research charges once without invented advanced discovery");Check(!shop.ResearchSpark(),"Repeated Spark unlock has no second command");
            Check(shop.BuySiphonUpgrade().Success,"Buy real existing upgraded siphon for normal depletion");
            GateAim(new Vector3(0,.2f,2),new Vector3(0,1.2f,5));yield return null;Check(shop.OpenLabMenu()&&shop.BuyLabExpansion()&&shop.BuyLabExpansion(),"Real two lab expansions provide supported rotation space");shop.SetPanel(ShopPanel.None);
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));advancedRack=shop.Layout.Items[shop.Layout.Items.Count-1].Id;advancedSlots=shop.Layout.GetStorageSlotIds(advancedRack);
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,14,0,new Vector3(4.5f,.2f,5.2f));advancedSpareRack=shop.Layout.Items[shop.Layout.Items.Count-1].Id;advancedSpareSlots=shop.Layout.GetStorageSlotIds(advancedSpareRack);
            yield return AdvancedProduce("lanternfin");yield return SpecimenRackPanel(advancedSpareRack);Check(shop.StoreReadyInRack(advancedSpareSlots[0]),"Preserve naturally discovered Lanternfin as real rack resident");
            Check(AdvancedFindPosition(null,false,-6,14,out int vatX,out int vatZ),"Preview actually legal vat geometry");
            yield return EditedPlace(null,ShopLayout.ConditionerDefinitionId,vatX,vatZ,0,new Vector3(vatX*ShopLayout.GridSize,.2f,vatZ*ShopLayout.GridSize-1.9f));advancedVat=shop.Model.ConditionerIds[0];
            Check(!shop.World.AdvancedSourceForValidation(CultureKind.Mist).Present,"Installed but research-locked vat has no Mist effect");
            yield return ConditioningPanel(advancedVat);cash=shop.Model.Currency;Check(shop.ResearchMist()&&shop.Model.Currency==cash-shop.Config.MistResearchCost,"Real paid Mist unlock at installed vat");Check(!shop.ResearchMist(),"Repeated Mist research refuses");shop.SetPanel(ShopPanel.None);
            yield return AdvancedSourceCycle(CultureKind.Spark);yield return AdvancedSourceCycle(CultureKind.Mist);
            yield return AdvancedConditioning();yield return AdvancedMoves(CultureKind.Spark);yield return AdvancedMoves(CultureKind.Mist);
            yield return AdvancedBoundaries();yield return AdvancedSingleBatchRecovery();yield return AdvancedAutomatic();yield return AdvancedComfort();
            AdvancedResources("final repeated advanced batches");Check(shop.SaveNow(),"Save final advanced isolated state");File.Copy(shop.SavePath,Path.Combine(evidence,"advanced-lab-final-save.json"),true);
            Check(growerInputReceipt.foreignCurrentSamples==0&&growerInputReceipt.otherEnabledSamples==0&&growerInputReceipt.droppedTransitions==0,"Advanced input provenance remains strictly isolated");shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
