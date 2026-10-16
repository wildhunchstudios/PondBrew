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
        [Serializable] sealed class ContainerFrame
        {
            public float seconds;
            public int transfers,sourceEmissions,destinationEmissions,particles,ambient;
            public string source,destination,specimen,panel;
            public Vector3 waterScale,camera;
            public Quaternion cameraRotation;
            public int width,height;
        }
        [Serializable] sealed class ContainerTrace { public string context;public ContainerFrame[] frames; }
        [Serializable] sealed class ContainerFit
        {
            public string key,species,specimen,context;
            public int meshes,vertices,poses,quarterTurns;
            public Vector3 minimum,maximum;
        }
        string containerRack,containerSmall,containerLarge;
        string[] containerSlots,containerTanks;
        readonly Dictionary<Mesh,Vector3[]> containerMeshVertices=new Dictionary<Mesh,Vector3[]>();
        bool containerLow;

        SpeciesGeometry ContainerGeometry(string key)
        {
            Check(shop.World.TryGetCreatureForValidation(key,out var space,out var interior,out var creature,out var species,out var specimen)&&creature,
                "Actual resident geometry / "+key);
            Transform water=space.Find("Water volume")??space.Find("Aquatic tint")??space.Find("Storage water")??space.Find("Conditioning water");
            Transform sand=space.Find("Tank sand")??space.Find("Compartment sand")??space.Find("Storage sand")??space.Find("Conditioning sand");
            Check(water&&sand,"Actual water and sand mesh owners / "+key);
            var filters=creature.GetComponentsInChildren<MeshFilter>();
            var geometry=new SpeciesGeometry{Key=key,Space=space,Interior=interior,Creature=creature,Species=species,Specimen=specimen,Water=water,Sand=sand,
                WaterMesh=water.GetComponent<MeshFilter>().sharedMesh.bounds,Meshes=new PondletMesh[filters.Length]};
            for(int i=0;i<filters.Length;i++) {
                var mesh=filters[i].sharedMesh;Check(mesh&&mesh.isReadable,"Readable original complete species mesh / "+key);
                geometry.Meshes[i]=new PondletMesh{Root=filters[i].transform,Vertices=mesh.vertices};geometry.Vertices+=geometry.Meshes[i].Vertices.Length;
            }
            return geometry;
        }
        void ContainerSweep(string key,string name)
        {
            var g=ContainerGeometry(key);string authority=FxAuthority();Vector3 position=g.Space.position;Quaternion rotation=g.Space.rotation;
            var receipt=new ContainerFit{key=key,species=g.Species,specimen=g.Specimen,meshes=g.Meshes.Length,vertices=g.Vertices,
                minimum=Vector3.one*100,maximum=Vector3.one*-100,quarterTurns=Mathf.RoundToInt(g.Space.eulerAngles.y/90)%4,
                context="Frozen authority; actual complete mesh vertices against actual animated water and sand. Explicit PreviewCreatureForValidation advances poses; Animate(time,0) is used only to restore live pose."};
            float period=CreatureMotion.Period(g.Species);
            for(int offset=0;offset<3;offset++)for(int i=0;i<=96;i++) {
                shop.World.PreviewCreatureForValidation(key,period*i/96+offset*1.37f,false);
                SpeciesMeshFits(g);foreach(var mesh in g.Meshes)foreach(var vertex in mesh.Vertices) {
                    var point=g.Space.InverseTransformPoint(mesh.Root.TransformPoint(vertex));receipt.minimum=Vector3.Min(receipt.minimum,point);receipt.maximum=Vector3.Max(receipt.maximum,point);
                }
                receipt.poses++;
            }
            shop.World.PreviewCreatureForValidation(key,31,true);SpeciesMeshFits(g);receipt.poses++;
            shop.World.Animate(Time.time,0);
            Check(authority==FxAuthority()&&g.Space.position==position&&g.Space.rotation==rotation,"Actual pose sweep preserves authority and container root / "+name);
            File.WriteAllText(Path.Combine(evidence,name+"-mesh.json"),JsonUtility.ToJson(receipt,true));
        }
        int ContainerParticles(string key)=>shop.World.FxPool.ParticleCountForValidation(shop.World.AquariumOwnerForValidation(key));
        void ContainerParticleFits(string key)
        {
            var w=shop.World;if(!w.TryGetHabitatForValidation(key,out var space,out var water,out _,out _))return;
            var token=w.AquariumOwnerForValidation(key);int count=w.FxPool.ParticleCountForValidation(token);
            for(int i=0;i<count;i++) {
                var visual=w.FxPool.ParticleVisualForValidation(token,i);var mesh=visual.GetComponent<MeshFilter>().sharedMesh;
                if(!containerMeshVertices.TryGetValue(mesh,out var vertices)){vertices=mesh.vertices;containerMeshVertices.Add(mesh,vertices);}
                var expanded=water;expanded.Expand(.0004f);
                foreach(var vertex in vertices) {
                    Vector3 point=space.InverseTransformPoint(visual.TransformPoint(vertex));
                    if(!expanded.Contains(point))throw new InvalidOperationException("Actual container particle escapes admitted water / "+key+" / "+point+" / "+water);
                }
            }
        }
        void ContainerDecorationFits(string key)
        {
            if(!shop.World.TryGetHabitatForValidation(key,out var space,out _,out bool ambient,out _))return;
            if(!ambient)return;
            Transform water=space.Find("Water volume")??space.Find("Aquatic tint")??space.Find("Storage water");
            Check(water,"Ambient habitat has actual water / "+key);
            var bounds=water.GetComponent<MeshFilter>().sharedMesh.bounds;bounds.Expand(.0005f);
            foreach(var filter in space.GetComponentsInChildren<MeshFilter>()) {
                string name=filter.name.ToLowerInvariant();
                if(!filter.gameObject.activeInHierarchy||(!name.Contains("bubble")&&!name.Contains("frond")))continue;
                // Specimen appendages are checked separately; only cached habitat decorations here.
                if(filter.GetComponentInParent<CreatureVisual>())continue;
                if(!containerMeshVertices.TryGetValue(filter.sharedMesh,out var vertices)){vertices=filter.sharedMesh.vertices;containerMeshVertices.Add(filter.sharedMesh,vertices);}
                foreach(var vertex in vertices)if(!bounds.Contains(water.InverseTransformPoint(filter.transform.TransformPoint(vertex))))
                    throw new InvalidOperationException("Actual habitat decoration escapes water / "+key+" / "+filter.name);
            }
        }
        void ContainerExactOwner(string specimen,string expected)
        {
            int count=0;string actual=null;
            if(shop.Model.Incubator.SpecimenId==specimen){count++;actual="incubator";}
            foreach(string key in shop.Model.TankIds)if(shop.Model.GetTank(key).HasValue&&shop.Model.GetTank(key).Value.Id==specimen){count++;actual=key;}
            foreach(string key in shop.Model.StorageIds)if(shop.Model.GetStorage(key).HasValue&&shop.Model.GetStorage(key).Value.Id==specimen){count++;actual=key;}
            foreach(string key in shop.Model.ConditionerIds) {
                var vat=shop.Model.GetConditioner(key).Value;
                if(vat.First.HasValue&&vat.First.Value.Id==specimen){count++;actual=key+"-conditioning-visual-0";}
                if(vat.Second.HasValue&&vat.Second.Value.Id==specimen){count++;actual=key+"-conditioning-visual-1";}
            }
            Check(count==1&&actual==expected,"One authoritative specimen owner / "+specimen+" -> "+expected);
            int visuals=0;foreach(var visual in shop.World.GetComponentsInChildren<CreatureVisual>()) {
                // Exact IDs come from the display registry, not an assumption based on mesh names.
                if(!visual.gameObject.activeInHierarchy)continue;
                foreach(string key in ContainerKeys())if(shop.World.TryGetCreatureForValidation(key,out _,out _,out var registered,out _,out string id)&&registered==visual&&id==specimen)visuals++;
            }
            Check(expected=="incubator"?shop.World.IncubationSpecimenForValidation==specimen:visuals==1,"One active registered visual specimen / "+expected);
        }
        List<string> ContainerKeys()
        {
            var keys=new List<string>(shop.Model.TankIds);keys.AddRange(shop.Model.StorageIds);keys.Add("incubator");
            foreach(string vat in shop.Model.ConditionerIds){keys.Add(vat+"-conditioning-visual-0");keys.Add(vat+"-conditioning-visual-1");}return keys;
        }
        ContainerFrame ContainerSample(float age,string key)
        {
            var w=shop.World;var pool=w.FxPool;int ambient=0;
            for(int i=FxEffectPool.ActionReserved;i<FxEffectPool.Capacity;i++)if(pool.Root.GetChild(i).gameObject.activeSelf)ambient++;
            if(pool.ActiveCount>(containerLow?32:96)||ambient>(containerLow?8:32)||pool.OwnedCount!=96||pool.OwnedMeshCount!=5||pool.OwnedMaterialCount!=1)
                throw new InvalidOperationException("Container presentation exceeds fixed shared resources");
            foreach(string id in ContainerKeys()){ContainerParticleFits(id);ContainerDecorationFits(id);}
            Vector3 waterScale=Vector3.zero;
            if(w.TryGetHabitatForValidation(key,out var space,out _,out _,out _)) {
                Transform water=space.Find("Water volume")??space.Find("Aquatic tint")??space.Find("Storage water")??space.Find("Conditioning water")??space.Find("Incubator fluid");if(water)waterScale=water.localScale;
            }
            return new ContainerFrame{seconds=age,transfers=w.HabitatTransferCount,sourceEmissions=w.HabitatSourceEmissionCount,destinationEmissions=w.HabitatDestinationEmissionCount,
                source=w.LastHabitatSourceId,destination=w.LastHabitatDestinationId,specimen=w.LastHabitatSpecimenId,particles=pool.ActiveCount,ambient=ambient,panel=shop.Panel.ToString(),waterScale=waterScale,
                camera=shop.Player.View.transform.position,cameraRotation=shop.Player.View.transform.rotation,width=Screen.width,height=Screen.height};
        }
        IEnumerator ContainerClip(string key,string name,float duration,Action<float> action=null)
        {
            var frames=new List<ContainerFrame>();int sample=0;SpeciesGeometry resident=null;
            Action<float> tick=age=>{
                action?.Invoke(age);
                if(sample++%4==0) {
                    if(shop.World.TryGetCreatureForValidation(key,out _,out _,out var creature,out _,out string id)&&creature) {
                        if(resident==null||resident.Creature!=creature||resident.Specimen!=id)resident=ContainerGeometry(key);
                        SpeciesMeshFits(resident);
                    }
                    frames.Add(ContainerSample(age,key));
                }
            };
            if(Application.isEditor){float begin=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-begin<duration){tick(Time.realtimeSinceStartup-begin);yield return null;}}
            else yield return ReviewClip(name,duration,tick);
            Check(frames.Count>2,"Actual HUD container motion frames / "+name);
            File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(new ContainerTrace{context="Actual ordinary HUD at commanded resolution, scripted play-distance camera, synthetic isolated input; sampled full meshes, original authority, fixed shared pool. Silent media is not native/listening/creative/GPU acceptance.",frames=frames.ToArray()},true));
        }
        IEnumerator ContainerAim(string key)
        {
            shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;
            if(key=="incubator"){GrowthAim();yield return null;yield break;}
            Check(shop.World.TryGetHabitatForValidation(key,out var space,out _,out _,out _),"Existing habitat camera subject / "+key);
            Vector3 target=space.position;Vector3 position;
            if(Array.IndexOf(shop.Model.TankIds,key)>=0) {
                position=shop.Customers.Navigation.BrowseSpot(key,0);target=space.TransformPoint(new Vector3(0,1.15f,0));
            } else if(Array.IndexOf(shop.Model.StorageIds,key)>=0) {
                shop.Layout.TryGetStorageAccessPoint(containerRack,out float x,out float z);position=new Vector3(x,.2f,z);target=space.position;
            } else {shop.Layout.TryGetConditionerAccessPoint(advancedVat,out float x,out float z);position=new Vector3(x,.2f,z);target=space.position;}
            position.y=.2f;GateAim(position,target);yield return null;yield return null;
        }
        void ContainerTransferReceipt(string source,string destination,string specimen,int before)
        {
            var w=shop.World;
            Check(shop.SaveError==null&&w.HabitatTransferCount==before+1&&w.LastHabitatSourceId==source&&w.LastHabitatDestinationId==destination&&w.LastHabitatSpecimenId==specimen,
                "One post-save source/destination receipt / "+source+" -> "+destination+" actual "+w.LastHabitatSourceId+" -> "+w.LastHabitatDestinationId+" count "+before+" -> "+w.HabitatTransferCount);
            ContainerExactOwner(specimen,destination);
        }
        IEnumerator ContainerTransfer(Func<bool> command,string source,string destination,string specimen,string clip=null,bool sourceView=false)
        {
            var w=shop.World;int before=w.HabitatTransferCount;bool done=false;
            Action invoke=()=>{
                int emittedSource=w.HabitatSourceEmissionCount,emittedDestination=w.HabitatDestinationEmissionCount;
                Check(command(),"Guarded actual saved container transfer / "+source+" -> "+destination);ContainerTransferReceipt(source,destination,specimen,before);
                Check(w.HabitatSourceEmissionCount>emittedSource&&w.HabitatDestinationEmissionCount>emittedDestination&&ContainerParticles(source)>0&&ContainerParticles(destination)>0,
                    "Saved transfer visibly admits both local responses under current policy / "+shop.Panel+" / "+source+" -> "+destination);
                ContainerParticleFits(source);ContainerParticleFits(destination);
                if(sourceView) {
                    var station=w.IncubationStationForValidation;
                    GateAim(station.TransformPoint(new Vector3(0,-station.position.y+.2f,-1.9f)),station.TransformPoint(new Vector3(0,1.48f,0)));
                }
                done=true;
            };
            if(clip!=null)yield return ContainerClip(sourceView?source:destination,clip,1.4f,age=>{if(!done&&age>=.2f)invoke();});else invoke();
            yield return null;yield return null;
            Check(done,"Transfer executed / "+destination);ContainerParticleFits(source);ContainerParticleFits(destination);
            if(clip!=null)Check(w.HabitatResponseAgeForValidation(source)<0&&w.HabitatResponseAgeForValidation(destination)<0,
                "Source and destination responses expire naturally even while menu remains open / "+clip);
            bool closing=shop.Panel!=ShopPanel.None;shop.SetPanel(ShopPanel.None);yield return null;
            Check(w.HabitatTransferCount==before+1&&(!(closing||clip!=null)||(w.HabitatResponseAgeForValidation(source)<0&&w.HabitatResponseAgeForValidation(destination)<0)),
                "Closing transfer menu cannot queue or replay either response");
            Check(!SpeciesVisual(source),"Source has no ghost creature after accepted transfer / "+source);
        }
        IEnumerator ContainerReserved(string key,string specimen,string rackSlot)
        {
            // Domain reservation fixture isolates every display shape without inventing a sixth customer behavior.
            string owner="fx13-reservation-"+key;int count=shop.World.HabitatTransferCount;
            Ok(shop.Model.ReserveTank(key,owner),"Labeled exact occupied reservation / "+key);shop.World.Refresh(shop.Model);
            yield return SpecimenDisplayPanel(key);string inventory=PlayerInventory();
            Check(!shop.ReturnDisplayToStorage(rackSlot,specimen)&&PlayerInventory()==inventory&&shop.World.HabitatTransferCount==count,"Reserved display refuses transfer without false success / "+key);
            ContainerExactOwner(specimen,key);ContainerSweep(key,"container-reserved-"+key);
            Ok(shop.Model.ReleaseReservation(key,owner),"Release labeled fixture reservation / "+key);shop.World.Refresh(shop.Model);shop.SetPanel(ShopPanel.None);
        }
        IEnumerator ContainerSpecies(string species,int index)
        {
            yield return IncubationStart(species);yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,20,"Original-duration container specimen / "+species);
            yield return new WaitForSeconds(1.8f);string specimen=shop.Model.Incubator.SpecimenId;IncubationSweep("container-"+species+"-incubator",false);
            if(index==0) {
                Check(AdvancedFindPosition(ShopLayout.IncubatorFacilityId,true,0,0,out incubationMoveX,out incubationMoveZ),"Legal incubator four-orientation site");
                yield return IncubationRotations("container-pondlet",false);
            }
            yield return ContainerAim("tank-a");
            yield return ContainerTransfer(()=>shop.TransferToDisplay("tank-a").Success,"incubator","tank-a",specimen,
                index==0?"container-fixed-a-transfer-night":index==1?"container-incubator-source-night":null,index==1);
            for(int tank=0;tank<containerTanks.Length;tank++) {
                string key=containerTanks[tank];
                if(tank>0) {
                    yield return SpecimenDisplayPanel(key);
                    yield return ContainerTransfer(()=>shop.StockDisplayFromStorage(containerSlots[tank%2],specimen),containerSlots[tank%2],key,specimen,index==0?"container-"+key+"-transfer-night":null);
                }
                ContainerSweep(key,"container-"+species+"-"+key);
                if(index==0)yield return ContainerReserved(key,specimen,containerSlots[(tank+1)%2]);
                if(index==0){yield return ContainerLifecycle(key,specimen);}
                yield return SpecimenDisplayPanel(key);string destination=containerSlots[(tank+1)%2];
                yield return ContainerTransfer(()=>shop.ReturnDisplayToStorage(destination,specimen),key,destination,specimen,index==0&&tank<2?"container-rack-"+(tank+1)+"-transfer-night":null);
                ContainerSweep(destination,"container-"+species+"-"+destination);
                if(index==0&&tank<2)yield return ContainerLifecycle(destination,specimen);
            }
            // End in rack slot 1 for five displays. Direct-domain cleanup is separately labeled.
            string last=containerSlots[containerTanks.Length%2];yield return SpecimenDisplayPanel("tank-a");
            yield return ContainerTransfer(()=>shop.StockDisplayFromStorage(last,specimen),last,"tank-a",specimen);
            Ok(shop.Perform(shop.Model.SellFromTank("tank-a")),"Labeled domain cleanup sale, no customer-payment evidence / "+species);yield return null;yield return null;
            Check(!SpeciesVisual("tank-a"),"Cleanup removes actual specimen mesh / "+species);
        }
        IEnumerator ContainerLifecycle(string key,string specimen)
        {
            var w=shop.World;
            foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Journal,ShopPanel.Arrange}) {
                shop.SetPanel(panel);yield return null;float age=w.CreatureAgeForValidation(key);
                yield return new WaitForSeconds(.15f);
                Check(w.CreatureAgeForValidation(key)==age&&ContainerParticles(key)==0,"Policy freezes actual resident and retires container transient / "+panel);ContainerExactOwner(specimen,key);
                shop.SetPanel(ShopPanel.None);yield return null;
            }
            shop.SetFxFocusForValidation(false);yield return null;float stopped=w.CreatureAgeForValidation(key);yield return new WaitForSeconds(.15f);
            Check(stopped==w.CreatureAgeForValidation(key)&&ContainerParticles(key)==0,"Focus loss freezes resident and clears container FX");shop.SetFxFocusForValidation(true);
            int count=w.HabitatTransferCount;Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload occupied container actual saved state");shop.SetPanel(ShopPanel.None);yield return null;
            Check(w.HabitatTransferCount==count&&ContainerParticles(key)==0,"Reload has no transfer replay");ContainerExactOwner(specimen,key);
        }
        IEnumerator ContainerFailedSave()
        {
            yield return AdvancedProduce("pondlet");string id=shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(containerRack);yield return ContainerTransfer(()=>shop.StoreReadyInRack(containerSlots[0]),"incubator",containerSlots[0],id);
            yield return SpecimenDisplayPanel(containerTanks[2]);Check(shop.SaveNow(),"Preserve exact durable failed-save baseline");byte[] saved=File.ReadAllBytes(shop.SavePath);
            int before=shop.World.HabitatTransferCount,source=shop.World.HabitatSourceEmissionCount,destination=shop.World.HabitatDestinationEmissionCount;
            TutorialBlockSave();
            try {
                shop.StockDisplayFromStorage(containerSlots[0],id);
                Check(shop.SaveError!=null&&shop.World.HabitatTransferCount==before&&shop.World.HabitatSourceEmissionCount==source&&shop.World.HabitatDestinationEmissionCount==destination,
                    "Failed durable write emits neither source nor destination success");
                Check(Convert.ToBase64String(saved)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed transfer preserves exact durable bytes");
            } finally {TutorialUnblockSave();}
            Check(shop.ReloadSaved(),"Restore exact durable authority after failed fixture write");shop.SetPanel(ShopPanel.None);yield return null;
            ContainerExactOwner(id,containerSlots[0]);Check(shop.World.HabitatTransferCount==before,"No delayed false success after failed-write recovery");
        }
        IEnumerator ContainerStocker()
        {
            string specimen=shop.Model.GetStorage(containerSlots[0]).Value.Id;
            yield return StockerEnter();Check(shop.HireStocker()&&shop.SetStockerPaused(true)&&shop.SetStockerScheduled(true),"Actual Nori hire/schedule with source retained");
            foreach(string key in containerTanks) {
                yield return StockerEnter();Check(shop.SetStockingTarget(key,"pondlet")&&shop.SetStockerPaused(false),"Real exact container stocking target / "+key);StockerParkPlayer();
                yield return StockerWait(()=>shop.Stocker.CurrentJobId!=null,8,"FX13 source/destination claim / "+key);
                var claim=shop.Model.GetStockTransfer(shop.Stocker.CurrentJobId);Check(claim.HasValue&&claim.Value.TankId==key&&claim.Value.SpecimenId==specimen,"Reserved worker target has exact original identity / "+key);
                Check(!SpeciesVisual(key)&&SpeciesVisual(containerSlots[0]),"Reserved empty target has no premature ghost resident / "+key);
                int before=shop.World.HabitatTransferCount;StockerParkPlayer();
                yield return StockerWait(()=>shop.Model.GetTank(key).HasValue,30,"FX13 actual Nori travel/service/delivery / "+key);
                ContainerTransferReceipt(containerSlots[0],key,specimen,before);ContainerSweep(key,"container-stocker-"+key);
                yield return StockerEnter();Check(shop.SetStockerPaused(true)&&shop.SetStockingTarget(key,null),"Pause Nori after exact saved delivery / "+key);
                yield return SpecimenDisplayPanel(key);yield return ContainerTransfer(()=>shop.ReturnDisplayToStorage(containerSlots[0],specimen),key,containerSlots[0],specimen);
            }
            yield return StockerEnter();Check(shop.SetStockerScheduled(false),"Nori off schedule before isolated geometry rearrangement");shop.SetPanel(ShopPanel.None);
        }
        IEnumerator ContainerConditioning()
        {
            yield return AdvancedProduce("pondlet");string second=shop.Model.Incubator.SpecimenId;
            yield return SpecimenRackPanel(containerRack);yield return ContainerTransfer(()=>shop.StoreReadyInRack(containerSlots[1]),"incubator",containerSlots[1],second);
            string first=shop.Model.GetStorage(containerSlots[0]).Value.Id;int before=shop.World.HabitatTransferCount;
            yield return AdvancedBeginBatch(containerSlots);Check(shop.World.HabitatTransferCount==before+2,"Two saved conditioning inputs each produce one transfer receipt");
            for(int i=0;i<2;i++){string key=advancedVat+"-conditioning-visual-"+i;ContainerExactOwner(i==0?first:second,key);ContainerSweep(key,"container-conditioning-working-"+i);}
            yield return ContainerAim(advancedVat+"-conditioning-visual-0");yield return ContainerClip(advancedVat+"-conditioning-visual-0","container-conditioning-working-night",2);
            yield return AutomationWait(()=>shop.Model.GetConditioner(advancedVat).Value.Phase==ConditionerPhase.Ready,16,"Actual two-Pondlet conditioning duration");
            yield return ContainerRotate(advancedVat,ShopLayout.ConditionerDefinitionId,new[]{advancedVat+"-conditioning-visual-0",advancedVat+"-conditioning-visual-1"});
            yield return ContainerLifecycle(advancedVat+"-conditioning-visual-0",first);
            for(int i=0;i<2;i++) {
                string key=advancedVat+"-conditioning-visual-"+i,id=i==0?first:second;ContainerSweep(key,"container-conditioning-ready-"+i);
                yield return SpecimenRackPanel(containerRack);int output=i;
                if(output==1){containerLow=true;shop.World.SetFxPreviewModes(false,true);}
                try {
                    yield return ContainerTransfer(()=>{
                        bool accepted=shop.StoreConditionerInRack(advancedVat,output,containerSlots[output],id);
                        if(output==1&&accepted) {
                            Check(shop.World.FxPool.ActiveCount<=32,"Actual Low accepted conditioned output retains shared total cap");
                            File.WriteAllText(Path.Combine(evidence,"container-low-accepted-transfer.json"),JsonUtility.ToJson(ContainerSample(0,key),true));
                        }
                        return accepted;
                    },key,containerSlots[output],id);
                } finally {if(output==1){shop.World.SetFxPreviewModes(false,false);containerLow=false;}}
            }
        }
        bool ContainerFindPose(string item,string definition,int turn,out int x,out int z)
        {
            var current=shop.Layout.GetItem(item).Value;
            for(int radius=0;radius<=5;radius++)for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++) {
                int xx=current.X+dx,zz=current.Z+dz;if(radius==0&&current.QuarterTurns==turn)continue;
                if(!shop.Layout.TryPreview(definition,item,xx,zz,turn,out var candidate).Success)continue;
                if(!ContainerWorkerRoutes(candidate))continue;
                var point=new Vector3(xx*ShopLayout.GridSize,.2f,zz*ShopLayout.GridSize)+Quaternion.Euler(0,turn*90,0)*Vector3.back*1.9f;
                if(!candidate.CanReachMainAisle(point.x,point.z)||!shop.Layout.CanReachMainAisle(point.x,point.z))continue;
                x=xx;z=zz;return true;
            }
            x=z=0;return false;
        }
        bool ContainerWorkerRoutes(ShopLayout candidate)
        {
            // Match the real placement guards; never move/delete an employee to force a fixture pose.
            if(shop.Stocker&&shop.Stocker.Actor&&shop.Stocker.Actor.isActiveAndEnabled) {
                var worker=shop.World.transform.InverseTransformPoint(shop.Stocker.Actor.transform.position);
                if(!candidate.CanReachMainAisle(worker.x,worker.z))return false;
            }
            if(shop.Technician&&shop.Technician.Actor&&shop.Technician.Actor.isActiveAndEnabled) {
                var worker=shop.World.transform.InverseTransformPoint(shop.Technician.Actor.transform.position);
                if(!candidate.CanReachMainAisle(worker.x,worker.z))return false;
            }
            if(shop.Cashier&&shop.Cashier.Actor&&shop.Cashier.Actor.isActiveAndEnabled) {
                var worker=shop.World.transform.InverseTransformPoint(shop.Cashier.Actor.transform.position);
                if(!candidate.CanReachMainAisle(worker.x,worker.z))return false;
            }
            return true;
        }
        IEnumerator ContainerOverview(string key,string name)
        {
            yield return ContainerAim(key);shop.World.TryGetHabitatForValidation(key,out var space,out _,out _,out _);
            Vector3 target=space.position;
            if(Array.IndexOf(shop.Model.TankIds,key)>=0)target=space.TransformPoint(new Vector3(0,1.15f,0));
            if(key==containerTanks[3]||key==containerTanks[4]) {
                Check(shop.World.TryGetPlacedTable(containerLarge,out var root),"Large display overview uses retained actual root");target=root.TransformPoint(new Vector3(0,1.15f,0));
            }
            if(Array.IndexOf(shop.Model.StorageIds,key)>=0) {
                Check(shop.World.TryGetStorageRack(containerRack,out var root),"Rack overview uses retained actual root");target=root.TransformPoint(new Vector3(0,1.3f,0));
            }
            Vector3 away=shop.Player.transform.position-target;away.y=0;Vector3 position=shop.Player.transform.position+away.normalized*.85f;
            var local=shop.World.transform.InverseTransformPoint(position);
            if(shop.Layout.CanReachMainAisle(local.x,local.z))GateAim(position,target);
            yield return null;yield return ContainerClip(key,name,2);
        }
        [Serializable] sealed class ContainerTopologyReceipt
        {
            public string context;
            public int worldTransforms,worldRenderers,poolOwned,cachedFixed,cachedAdded,addedColliders,addedStations,uniqueAddedMeshes,uniqueAddedMaterials;
            public string[] meshes,materials;
        }
        void ContainerTopology(string name,bool expectAdded)
        {
            var w=shop.World;var meshes=new HashSet<Mesh>();var materials=new HashSet<Material>();
            var receipt=new ContainerTopologyReceipt{context="Actual current layout; construction-time cached habitat children counted separately from unchanged96particle pool. This is not a totalFX renderer/GPU-budget acceptance or whole-game zero-allocation claim.",
                worldTransforms=w.GetComponentsInChildren<Transform>(true).Length,worldRenderers=w.GetComponentsInChildren<Renderer>(true).Length,poolOwned=w.FxPool.OwnedCount};
            foreach(string key in ContainerKeys()) {
                if(!w.TryGetHabitatForValidation(key,out var space,out _,out bool ambient,out _)||!ambient)continue;
                foreach(Transform child in space)if(child.name=="Air bubble"||child.name=="Aquatic frond") {
                    if(key=="tank-a"||key=="tank-b"){receipt.cachedFixed++;continue;}
                    receipt.cachedAdded++;receipt.addedColliders+=child.GetComponentsInChildren<Collider>(true).Length;receipt.addedStations+=child.GetComponentsInChildren<Station>(true).Length;
                    var filter=child.GetComponent<MeshFilter>();if(filter&&filter.sharedMesh)meshes.Add(filter.sharedMesh);
                    var renderer=child.GetComponent<Renderer>();if(renderer)foreach(var material in renderer.sharedMaterials)if(material)materials.Add(material);
                }
            }
            receipt.uniqueAddedMeshes=meshes.Count;receipt.uniqueAddedMaterials=materials.Count;
            var meshNames=new List<string>();foreach(var mesh in meshes)meshNames.Add(mesh.name);receipt.meshes=meshNames.ToArray();
            var materialNames=new List<string>();foreach(var material in materials)materialNames.Add(material.name);receipt.materials=materialNames.ToArray();
            Check((expectAdded?receipt.cachedAdded>0:receipt.cachedAdded==0)&&receipt.addedColliders==0&&receipt.addedStations==0&&receipt.poolOwned==96,
                "Cached habitat additions share visual assets, own no gameplay component and retire with furniture / "+name);
            File.WriteAllText(Path.Combine(evidence,"container-topology-"+name+".json"),JsonUtility.ToJson(receipt,true));
        }
        IEnumerator ContainerRotate(string item,string definition,string[] keys)
        {
            Check(shop.World.TryGetPlacedTable(item,out var original),"Retained actual furniture root / "+item);
            var colliders=original.GetComponentsInChildren<BoxCollider>();var sizes=new Vector3[colliders.Length];for(int i=0;i<sizes.Length;i++)sizes[i]=colliders[i].size;
            for(int turn=0;turn<4;turn++) {
                Check(ContainerFindPose(item,definition,turn,out int x,out int z),"Legal actual occupied move/rotation exists / "+item+" / "+turn);
                int count=shop.World.HabitatTransferCount;
                yield return EditedPlace(item,definition,x,z,turn,new Vector3(x*ShopLayout.GridSize,.2f,z*ShopLayout.GridSize)+Quaternion.Euler(0,turn*90,0)*Vector3.back*1.9f);
                shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;yield return null;
                Check(shop.World.TryGetPlacedTable(item,out var moved)&&moved==original&&shop.World.HabitatTransferCount==count,"Move preserves root and never replays stock transfer / "+item);
                for(int i=0;i<sizes.Length;i++)Check(colliders[i]&&colliders[i].size==sizes[i],"Original gameplay collider size preserved / "+item);
                foreach(string key in keys){if(SpeciesVisual(key))ContainerSweep(key,"container-rotated-"+key+"-"+turn);ContainerDecorationFits(key);ContainerParticleFits(key);}
            }
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload legally rotated occupied furniture / "+item);shop.SetPanel(ShopPanel.None);yield return null;
        }
        IEnumerator ContainerBusyAndRemoval()
        {
            // Explicit accelerated production only for busy-layout pressure; normal-duration recipes were proved above.
            foreach(string key in containerTanks) {
                SpecimenProduce("pondlet");yield return ContainerAim(key);Check(shop.TransferToDisplay(key).Success&&shop.SaveError==null,"Labeled accelerated busy-layout specimen / "+key);
            }
            foreach(string key in containerTanks)ContainerSweep(key,"container-busy-"+key);
            ContainerTopology("busy",true);
            yield return ContainerRotate(containerSmall,ShopLayout.SmallDisplayId,new[]{containerTanks[2]});
            yield return ContainerRotate(containerLarge,ShopLayout.LargeDisplayId,new[]{containerTanks[3],containerTanks[4]});
            yield return ContainerRotate(containerRack,ShopLayout.RackDefinitionId,containerSlots);
            yield return ContainerRotate(advancedVat,ShopLayout.ConditionerDefinitionId,new[]{advancedVat+"-conditioning-visual-0",advancedVat+"-conditioning-visual-1"});
            Check(shop.OpenForDay(),"Actual day for busy mixed occupied layout");yield return ContainerAim(containerTanks[2]);
            yield return ContainerClip(containerTanks[2],"container-busy-day",3);
            yield return ContainerOverview("tank-b","container-fixed-b-occupied-day");
            yield return ContainerOverview(containerTanks[3],"container-large-occupied-day");
            yield return ContainerOverview(containerSlots[0],"container-rack-occupied-day");
            yield return ContainerWakeFairness(false);
            shop.World.SetFxPreviewModes(false,true);containerLow=true;yield return ContainerAim(containerTanks[2]);yield return ContainerClip(containerTanks[2],"container-busy-low-day",3);
            yield return ContainerWakeFairness(true);
            shop.World.SetFxPreviewModes(true,true);float age=shop.World.CreatureAgeForValidation(containerTanks[2]);yield return new WaitForSeconds(.2f);
            Check(shop.World.CreatureAgeForValidation(containerTanks[2])==age&&ContainerParticles(containerTanks[2])==0,"Reduced busy habitat rests with no swimming wake");
            shop.World.SetFxPreviewModes(false,false);containerLow=false;
            Check(shop.CloseForNight(),"Close actual day before legal storage and resale");yield return AutomationWait(()=>shop.Cycle.Phase==ShopDayPhase.Night,8,"Night admits storage/removal");shop.SetPanel(ShopPanel.None);
            foreach(string item in new[]{containerSmall,containerLarge,containerRack}) {
                Check(shop.OpenOwnedFurnitureMenu()&&!shop.StoreFurniture(item)&&!shop.RequestFurnitureSale(item),"Occupied furniture cannot discard resident or transfer response / "+item);shop.SetPanel(ShopPanel.None);
            }
            foreach(string key in containerTanks) {Ok(shop.Perform(shop.Model.SellFromTank(key)),"Labeled busy-layout cleanup sale / "+key);yield return null;}
            // Rack residents return through saved menu transfers before their cleanup sale.
            foreach(string slot in containerSlots) {
                string specimen=shop.Model.GetStorage(slot).Value.Id;yield return SpecimenDisplayPanel("tank-a");
                yield return ContainerTransfer(()=>shop.StockDisplayFromStorage(slot,specimen),slot,"tank-a",specimen);
                Ok(shop.Perform(shop.Model.SellFromTank("tank-a")),"Labeled conditioned-resident cleanup sale");yield return null;
            }
            foreach(string item in new[]{containerSmall,containerLarge,containerRack,advancedVat}) {
                var pose=shop.Layout.GetItem(item).Value;Check(shop.World.TryGetPlacedTable(item,out var root),"Original empty removable furniture / "+item);
                Check(shop.OpenOwnedFurnitureMenu()&&shop.StoreFurniture(item),"Actual empty furniture storage / "+item);yield return null;
                Check((!root||!root.gameObject.activeSelf)&&!shop.World.TryGetPlacedTable(item,out _),"Stored object has no live world root / "+item);
                Check(shop.SaveNow()&&shop.ReloadSaved(),"Stored furniture survives reload with no ghost / "+item);shop.SetPanel(ShopPanel.None);
                yield return EditedPlace(item,pose.DefinitionId,pose.X,pose.Z,pose.QuarterTurns,new Vector3(pose.X*ShopLayout.GridSize,.2f,pose.Z*ShopLayout.GridSize)+Quaternion.Euler(0,pose.QuarterTurns*90,0)*Vector3.back*1.9f);
                Check(shop.OpenOwnedFurnitureMenu()&&shop.RequestFurnitureSale(item)&&shop.ConfirmFurnitureSale(),"Actual empty redeployed furniture resale / "+item);yield return null;yield return null;
                Check(!shop.Layout.GetItem(item).HasValue&&!shop.World.TryGetPlacedTable(item,out _),"Resale removes exact furniture and habitat owner / "+item);shop.SetPanel(ShopPanel.None);
            }
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Final removal reload");shop.SetPanel(ShopPanel.None);yield return null;yield return null;
            Check(shop.Model.StorageIds.Length==0&&shop.Model.ConditionerIds.Length==0&&shop.Model.TankIds.Length==2&&shop.World.GetComponentsInChildren<CreatureVisual>().Length==0,
                "Final reload has only original empty display registry and no ghost creature");
            ContainerTopology("removed",false);
        }
        [Serializable] sealed class ContainerWakeReceipt
        { public string key,policy,focusedTank;public int before,after;public float seconds;public Vector3 camera;public Quaternion cameraRotation; }
        [Serializable] sealed class ContainerWakeAudit {public ContainerWakeReceipt[] observations;}
        IEnumerator ContainerWakeFairness(bool low)
        {
            var observations=new List<ContainerWakeReceipt>();
            foreach(string key in containerTanks) {
                yield return ContainerAim(key);int before=shop.World.AquariumTrailEmissionCountForValidation(key);float begin=Time.realtimeSinceStartup;
                while(shop.World.AquariumTrailEmissionCountForValidation(key)==before&&Time.realtimeSinceStartup-begin<8.5f) {
                    ContainerSample(Time.realtimeSinceStartup-begin,key);yield return null;
                }
                int after=shop.World.AquariumTrailEmissionCountForValidation(key);
                observations.Add(new ContainerWakeReceipt{key=key,policy=low?"Low":"Standard",focusedTank=shop.Focused?shop.Focused.TankId:null,
                    before=before,after=after,seconds=Time.realtimeSinceStartup-begin,camera=shop.Player.View.transform.position,cameraRotation=shop.Player.View.transform.rotation});
                // Preserve this exact observation even if its following assertion fails.
                File.WriteAllText(Path.Combine(evidence,"container-wake-fairness-"+(low?"low":"standard")+".json"),JsonUtility.ToJson(new ContainerWakeAudit{observations=observations.ToArray()},true));
                Check(after>before,"Proximity admission gives this aimed container an actual wake under mixed occupancy / "+key+" / "+(low?"Low":"Standard"));
            }
            File.WriteAllText(Path.Combine(evidence,"container-wake-fairness-"+(low?"low":"standard")+".json"),JsonUtility.ToJson(new ContainerWakeAudit{observations=observations.ToArray()},true));
        }
        IEnumerator FxContainerScenario()
        {
            limitations.Add("FX13 isolated container route: synthetic InputSystem and play-distance scripted cameras; 2000 purchase credits only. Five original normal-duration recipes and exact saved manual/worker/conditioning transfers. Per-display occupied reservations are labeled domain fixtures; busy-layout stock is explicitly accelerated and cleanup sales use the labeled domain API. No native/listening/creative/GPU acceptance.");
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"FX13 exact isolated save");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionContainerKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionContainerMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("container-input-provenance.json");
            if(!Application.isEditor){int width=int.Parse(ShopSession.Arg("-screen-width")),height=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(width==1280?1920:1280,height==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==width&&Screen.height==height,"Exact actual FX13 HUD resolution");}
            StaffCreditFixture(2000,"FX13 legal furniture/expansion/research/staff construction only; no economy claim");Check(shop.BuySiphonUpgrade().Success,"Original tool upgrade for real species recipes");
            GateAim(new Vector3(0,.2f,2),new Vector3(0,1.2f,5));yield return null;Check(shop.OpenLabMenu()&&shop.BuyLabExpansion()&&shop.BuyLabExpansion(),"Actual both lab expansion tiers");shop.SetPanel(ShopPanel.None);
            Check(shop.OpenPlacementMenu()&&shop.BuyStoreExpansion()&&shop.BuyStoreExpansion(),"Actual both store expansion tiers");shop.SetPanel(ShopPanel.None);
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));containerRack=shop.Layout.Items[shop.Layout.Items.Count-1].Id;containerSlots=shop.Layout.GetStorageSlotIds(containerRack);
            advancedRack=containerRack;advancedSlots=containerSlots;
            yield return EditedPlace(null,ShopLayout.SmallDisplayId,3,-5,0,new Vector3(1.5f,.2f,-4.7f));containerSmall=shop.Layout.Items[shop.Layout.Items.Count-1].Id;
            yield return EditedPlace(null,ShopLayout.LargeDisplayId,-18,-3,0,new Vector3(-9,.2f,-4.5f));containerLarge=shop.Layout.Items[shop.Layout.Items.Count-1].Id;
            containerTanks=new[]{"tank-a","tank-b",shop.Layout.GetSlotIds(containerSmall)[0],shop.Layout.GetSlotIds(containerLarge)[0],shop.Layout.GetSlotIds(containerLarge)[1]};
            shop.SetPanel(ShopPanel.None);yield return ContainerAim(containerTanks[2]);yield return ContainerClip(containerTanks[2],"container-empty-small-night",1.5f);
            string[] species={"pondlet","bubblebelly","ribbonfin","lanternfin","driftwing"};
            for(int i=0;i<species.Length;i++) {
                if(i==3){yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);Check(shop.ResearchSpark(),"Actual Spark after three real discoveries");shop.SetPanel(ShopPanel.None);}
                if(i==4) {
                    Check(AdvancedFindPosition(null,false,-6,14,out int x,out int z),"Legal conditioning station site");
                    yield return EditedPlace(null,ShopLayout.ConditionerDefinitionId,x,z,0,new Vector3(x*ShopLayout.GridSize,.2f,z*ShopLayout.GridSize-1.9f));advancedVat=shop.Model.ConditionerIds[0];
                    yield return ConditioningPanel(advancedVat);Check(shop.ResearchMist(),"Actual Mist after Lanternfin discovery");shop.SetPanel(ShopPanel.None);
                }
                yield return ContainerSpecies(species[i],i);
            }
            yield return ContainerFailedSave();yield return ContainerStocker();yield return ContainerConditioning();yield return ContainerBusyAndRemoval();
            Check(growerInputReceipt.foreignCurrentSamples==0&&growerInputReceipt.otherEnabledSamples==0&&growerInputReceipt.droppedTransitions==0,"FX13 synthetic input provenance stays isolated");
            Check(shop.World.FxPool.OwnedCount==96&&shop.World.FxPool.OwnedMeshCount==5&&shop.World.FxPool.OwnedMaterialCount==1&&shop.GetComponents<AudioSource>().Length==3,"FX13 retains exact shared pool and original audio topology");
            Check(shop.SaveNow(),"Final FX13 durable save");File.Copy(shop.SavePath,Path.Combine(evidence,"container-final-save.json"),true);shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
