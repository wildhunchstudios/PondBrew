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
        sealed class SpeciesGeometry
        {
            public string Key, Species, Specimen;
            public Transform Space, Water, Sand;
            public CreatureVisual Creature;
            public Bounds Interior, WaterMesh;
            public PondletMesh[] Meshes;
            public int Vertices;
        }
        [Serializable] sealed class SpeciesFit
        {
            public string key, species, specimen, context;
            public int meshes, vertices, samples;
            public Vector3 minimum, maximum, scale;
            public float period, phase;
        }
        [Serializable] sealed class SpeciesFrame
        {
            public float seconds, clock, phase;
            public Vector3 position;
            public Quaternion rotation;
            public int particles, total, emissions;
            public bool trail;
        }
        [Serializable] sealed class SpeciesTrace
        {
            public string species, specimen, key, context;
            public SpeciesFrame[] frames;
        }
        readonly Dictionary<Mesh,Vector3[]> speciesParticleVertices = new Dictionary<Mesh,Vector3[]>();
        string speciesRack, speciesSlot;

        CreatureVisual SpeciesVisual(string key)
        {
            shop.World.TryGetCreatureForValidation(key,out _,out _,out var creature,out _,out _);return creature;
        }

        SpeciesGeometry SpeciesCache(string key)
        {
            var world=shop.World;
            Check(world.TryGetCreatureForValidation(key,out var space,out var interior,out var creature,out var species,out var specimen),"Actual rendered species resident / "+key);
            var water=space.Find("Water volume");if(!water)water=space.Find("Storage water");if(!water)water=space.Find("Conditioning water");
            var sand=space.Find("Tank sand");if(!sand)sand=space.Find("Storage sand");if(!sand)sand=space.Find("Conditioning sand");
            Check(water&&sand&&creature&&specimen!=null,"Actual physical water/sand and exact identity / "+key);
            var filters=creature.GetComponentsInChildren<MeshFilter>();
            var result=new SpeciesGeometry{Key=key,Species=species,Specimen=specimen,Space=space,Interior=interior,Creature=creature,Water=water,Sand=sand,
                WaterMesh=water.GetComponent<MeshFilter>().sharedMesh.bounds,Meshes=new PondletMesh[filters.Length]};
            for(int i=0;i<filters.Length;i++) {
                Check(filters[i].sharedMesh&&filters[i].sharedMesh.isReadable,"Original readable species geometry / "+species+" / "+filters[i].name);
                result.Meshes[i]=new PondletMesh{Root=filters[i].transform,Vertices=filters[i].sharedMesh.vertices};result.Vertices+=result.Meshes[i].Vertices.Length;
            }
            Check(result.Vertices>0&&creature.GetComponentsInChildren<Collider>(true).Length==0,"Complete species mesh has no gameplay collider / "+key);
            return result;
        }
        void SpeciesPointFits(SpeciesGeometry geometry,Vector3 point,string mesh)
        {
            var local=geometry.Space.InverseTransformPoint(point);var water=geometry.Water.InverseTransformPoint(point);
            var bounds=geometry.WaterMesh;var min=bounds.min-Vector3.one*.0002f;var max=bounds.max+Vector3.one*.0002f;
            float floor=geometry.Sand.localPosition.y+geometry.Sand.localScale.y*.5f;
            if(water.x<min.x||water.x>max.x||water.y<min.y||water.y>max.y||water.z<min.z||water.z>max.z||local.y<floor-.0002f)
                throw new InvalidOperationException("Actual species mesh escapes physical water/sand / "+geometry.Key+" / "+geometry.Species+" / "+mesh+" / "+local+" water "+water);
        }
        void SpeciesMeshFits(SpeciesGeometry geometry,SpeciesFit receipt=null)
        {
            foreach(var mesh in geometry.Meshes)foreach(var vertex in mesh.Vertices) {
                var point=mesh.Root.TransformPoint(vertex);SpeciesPointFits(geometry,point,mesh.Root.name);
                if(receipt!=null){var p=geometry.Space.InverseTransformPoint(point);receipt.minimum=Vector3.Min(receipt.minimum,p);receipt.maximum=Vector3.Max(receipt.maximum,p);}
            }
        }
        void SpeciesSweep(string key,string name)
        {
            var geometry=SpeciesCache(key);var world=shop.World;string authority=FxAuthority();
            var position=geometry.Space.position;var rotation=geometry.Space.rotation;
            var receipt=new SpeciesFit{key=key,species=geometry.Species,specimen=geometry.Specimen,meshes=geometry.Meshes.Length,vertices=geometry.Vertices,
                minimum=Vector3.one*100,maximum=Vector3.one*-100,scale=geometry.Creature.transform.lossyScale,period=CreatureMotion.Period(geometry.Species),phase=world.CreaturePhaseForValidation(key),
                context="Frozen-authority actual complete mesh vertices; three temporal offsets through the travel loop and reduced pose; real water/sand transforms"};
            for(int offset=0;offset<3;offset++)for(int i=0;i<=96;i++) {
                world.PreviewCreatureForValidation(key,receipt.period*i/96+offset*1.37f,false);SpeciesMeshFits(geometry,receipt);receipt.samples++;
            }
            world.PreviewCreatureForValidation(key,31,true);SpeciesMeshFits(geometry,receipt);receipt.samples++;
            world.Animate(Time.time,0);
            Check(FxAuthority()==authority&&geometry.Space.position==position&&geometry.Space.rotation==rotation,"Species sweep preserves authority and container root / "+name);
            File.WriteAllText(Path.Combine(evidence,name+"-mesh.json"),JsonUtility.ToJson(receipt,true));
        }
        void SpeciesWakeFits(SpeciesGeometry geometry)
        {
            var world=shop.World;var owner=world.AquariumOwnerForValidation("tank-a");var pool=world.FxPool;
            int count=pool.ParticleCountForValidation(owner);
            for(int i=0;i<count;i++) {
                var visual=pool.ParticleVisualForValidation(owner,i);var filter=visual.GetComponent<MeshFilter>();
                if(!speciesParticleVertices.TryGetValue(filter.sharedMesh,out var vertices)){vertices=filter.sharedMesh.vertices;speciesParticleVertices.Add(filter.sharedMesh,vertices);}
                foreach(var vertex in vertices)SpeciesPointFits(geometry,visual.TransformPoint(vertex),"owned wake "+i);
            }
            if(pool.ActiveCount>96||pool.OwnedCount!=96||pool.OwnedMeshCount!=5||pool.OwnedMaterialCount!=1)
                throw new InvalidOperationException("Five-species motion changes fixed FX ownership");
        }
        IEnumerator SpeciesClip(string key,string name,float duration,bool requireWake=false)
        {
            var geometry=SpeciesCache(key);var world=shop.World;var frames=new List<SpeciesFrame>();int index=0,emissions=world.AquariumTrailEmissionCount;
            int transforms=world.GetComponentsInChildren<Transform>(true).Length,renderers=world.GetComponentsInChildren<Renderer>(true).Length;
            Action<float> sample=age=>{
                if(index++%5==0){SpeciesMeshFits(geometry);if(key=="tank-a")SpeciesWakeFits(geometry);}
                if(!geometry.Creature||!world.TryGetCreatureForValidation(key,out _,out _,out _,out var species,out var id)||species!=geometry.Species||id!=geometry.Specimen)
                    throw new InvalidOperationException("Live species capture lost exact visual identity");
                frames.Add(new SpeciesFrame{seconds=age,clock=world.CreatureAgeForValidation(key),phase=world.CreaturePhaseForValidation(key),
                    position=geometry.Creature.transform.localPosition,rotation=geometry.Creature.transform.localRotation,
                    particles=key=="tank-a"?AquariumParticles():0,total=world.FxPool.ActiveCount,emissions=world.AquariumTrailEmissionCount,trail=key=="tank-a"&&world.AquariumTrailActiveForValidation(key)});
            };
            if(Application.isEditor){float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<duration){sample(Time.realtimeSinceStartup-began);yield return null;}}
            else yield return ReviewClip(name,duration,sample);
            Check(frames.Count>2&&world.GetComponentsInChildren<Transform>(true).Length==transforms&&world.GetComponentsInChildren<Renderer>(true).Length==renderers,"Live species uses stable actual topology / "+name);
            if(requireWake)Check(world.AquariumTrailEmissionCount>emissions,"Moving species emits contained existing-owner wake / "+name);
            File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(new SpeciesTrace{species=geometry.Species,specimen=geometry.Specimen,key=key,
                context="Actual normal-HUD live simulation, scripted operator camera, synthetic isolated inputs. No synchronous PNG within recording; silent video is not human/listening/GPU acceptance.",frames=frames.ToArray()},true));
        }
        IEnumerator SpeciesRackAim()
        {
            yield return SpecimenRackPanel(speciesRack);shop.SetPanel(ShopPanel.None);
            Check(shop.World.TryGetStorageRack(speciesRack,out var root),"Actual existing rack root");
            shop.Layout.TryGetStorageAccessPoint(speciesRack,out float x,out float z);
            GateAim(new Vector3(x,.2f,z),root.TransformPoint(new Vector3(0,1.15f,0)));yield return null;yield return null;
        }
        IEnumerator SpeciesIdentityFixture(string key,string specimen)
        {
            var world=shop.World;shop.SetPanel(ShopPanel.Pause);Check(shop.SaveNow(),"Save actual resident before labeled identity fixture");
            byte[] durable=File.ReadAllBytes(shop.SavePath);var old=SpeciesVisual(key);float phase=world.CreaturePhaseForValidation(key);
            string replacement;
            do {replacement=Guid.NewGuid().ToString("N");} while(CreatureMotion.IdentityPhase(replacement)==phase);
            string text=File.ReadAllText(shop.SavePath);Check(text.Contains(specimen),"Identity fixture replaces existing real saved ID only");
            try {
                File.WriteAllText(shop.SavePath,text.Replace(specimen,replacement));Check(shop.ReloadSaved(),"Load labeled valid same-species ID replacement");
                Check(world.TryGetCreatureForValidation(key,out _,out _,out var current,out _,out var rendered)&&rendered==replacement&&current==old,
                    "Same-species identity replacement reuses original geometry and updates actual identity / "+key);
                Check(world.CreatureAgeForValidation(key)==0&&world.CreaturePhaseForValidation(key)!=phase,
                    "Same-species identity starts deterministic new phase without inherited age / "+key);
            }
            finally {File.WriteAllBytes(shop.SavePath,durable);Check(shop.ReloadSaved(),"Restore exact real saved identity after labeled fixture");shop.SetPanel(ShopPanel.None);}
            PlayerExactOwner(specimen,key);
            yield return null;
        }
        IEnumerator SpeciesLifecycle(string species,string specimen)
        {
            var world=shop.World;var root=SpeciesVisual("tank-a");
            foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Journal,ShopPanel.Arrange}) {
                shop.SetPanel(panel);yield return null;float age=world.CreatureAgeForValidation("tank-a");var position=root.transform.localPosition;var rotation=root.transform.localRotation;
                yield return new WaitForSeconds(.2f);
                Check(world.CreatureAgeForValidation("tank-a")==age&&root.transform.localPosition==position&&root.transform.localRotation==rotation&&AquariumParticles()==0,
                    "Actual policy freezes fish and cancels wake / "+panel);shop.SetPanel(ShopPanel.None);yield return null;
            }
            shop.SetFxFocusForValidation(false);yield return null;float stopped=world.CreatureAgeForValidation("tank-a");yield return new WaitForSeconds(.2f);
            Check(world.CreatureAgeForValidation("tank-a")==stopped&&AquariumParticles()==0,"Lost focus freezes species clock and retires wake");
            shop.SetFxFocusForValidation(true);shop.SetPanel(ShopPanel.None);yield return null;
            int placed=world.PondletPlacementCount;float phase=world.CreaturePhaseForValidation("tank-a");
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload actual occupied species display");shop.SetPanel(ShopPanel.None);yield return null;
            Check(world.TryGetCreatureForValidation("tank-a",out _,out _,out _,out _,out var id)&&id==specimen&&world.CreaturePhaseForValidation("tank-a")==phase&&world.PondletPlacementCount==placed,
                "Reload retains exact deterministic personality with no placement replay");
            world.SetFxPreviewModes(true,true);yield return AquariumAim(2.6f);yield return SpeciesClip("tank-a","species-"+species+"-reduced-live",2);
            Check(AquariumParticles()==0,"Reduced species has no motion-linked trail");world.SetFxPreviewModes(false,true);
            yield return SpeciesClip("tank-a","species-"+species+"-low-live",2);Check(world.FxPool.ActiveCount<=32,"Low policy retains total shared budget");world.SetFxPreviewModes(false,false);
        }
        IEnumerator SpeciesRackMove(string specimen)
        {
            Check(shop.CloseForNight(),"Actual close before night-only occupied rack relocation");
            yield return AutomationWait(()=>shop.Cycle.Phase==ShopDayPhase.Night&&shop.Customers.ActiveCount==0,8,"Night and empty shop admit arrangement");shop.SetPanel(ShopPanel.None);
            var current=shop.Layout.GetItem(speciesRack).Value;int turn=(current.QuarterTurns+1)%4;int chosenX=0,chosenZ=0;bool found=false;
            for(int radius=1;radius<=5&&!found;radius++)for(int dz=-radius;dz<=radius&&!found;dz++)for(int dx=-radius;dx<=radius&&!found;dx++) {
                int x=current.X+dx,z=current.Z+dz;if(x==current.X&&z==current.Z)continue;
                var result=shop.Layout.TryPreview(ShopLayout.RackDefinitionId,speciesRack,x,z,turn,out var candidate);if(!result.Success)continue;
                var point=new Vector3(x*ShopLayout.GridSize,.2f,z*ShopLayout.GridSize)+Quaternion.Euler(0,turn*90,0)*Vector3.back*1.9f;
                if(!candidate.CanReachMainAisle(point.x,point.z)||!shop.Layout.CanReachMainAisle(point.x,point.z))continue;
                chosenX=x;chosenZ=z;found=true;
            }
            Check(found,"One legal occupied rack relocation and rotation exists");Check(shop.World.TryGetStorageRack(speciesRack,out var root),"Original occupied rack root");
            var collider=root.GetComponent<BoxCollider>();Vector3 size=collider.size,center=collider.center;
            yield return EditedPlace(speciesRack,ShopLayout.RackDefinitionId,chosenX,chosenZ,turn,
                new Vector3(chosenX*ShopLayout.GridSize,.2f,chosenZ*ShopLayout.GridSize)+Quaternion.Euler(0,turn*90,0)*Vector3.back*1.9f);
            shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;yield return null;
            Check(shop.World.TryGetStorageRack(speciesRack,out var moved)&&moved==root&&collider.size==size&&collider.center==center,"Occupied move preserves actual root/collider");
            PlayerExactOwner(specimen,speciesSlot);SpeciesSweep(speciesSlot,"species-pondlet-relocated-rack");
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload occupied moved rack");shop.SetPanel(ShopPanel.None);PlayerExactOwner(specimen,speciesSlot);
            Check(shop.OpenForDay(),"Reopen actual trading after legal night-only rack move");
        }
        IEnumerator SpeciesPaidSale(string specimen)
        {
            var world=shop.World;shop.Customers.ScriptedArchetypes.Enqueue(CustomerArchetypes.Casual);Check(shop.Customers.SpawnNow(),"Scripted arrival uses normal customer navigation and selection");
            var visit=shop.Customers.Visits[shop.Customers.Visits.Count-1];yield return AutomationWait(()=>visit.Phase==CustomerPhase.Queued,40,"Actual customer reserves real animated Pondlet");
            Check(visit.SpecimenId==specimen&&visit.TargetTankId=="tank-a"&&shop.Model.GetReservation("tank-a").HasValue,"Actual reservation owns exact animated specimen");
            yield return AquariumAim(2.6f);yield return SpeciesClip("tank-a","species-pondlet-reserved-day-live",2);
            yield return EnterCounter();int cash=shop.Model.Currency,price=visit.Price;long revenue=shop.Reports.Current.revenue;
            Check(shop.ServeCustomer()&&visit.Outcome==CustomerOutcome.Sold&&shop.Model.Currency==cash+price&&shop.Reports.Current.revenue==revenue+price,"Actual checkout pays exact agreed price once");
            Check(!visit.Serve(shop.Cycle.Phase).Success&&shop.Model.Currency==cash+price,"Sold animated specimen cannot pay twice");
            yield return null;yield return null;Check(!SpeciesVisual("tank-a")&&AquariumParticles()==0,"Paid sale retires actual fish and its wake");
            Check(shop.SaveNow()&&shop.ReloadSaved()&&!shop.Model.GetTank("tank-a").HasValue&&!SpeciesVisual("tank-a")&&shop.Model.Currency==cash+price,"Paid reload preserves cash and empty visual ownership");shop.SetPanel(ShopPanel.None);
            File.WriteAllText(Path.Combine(evidence,"species-paid-sale.json"),JsonUtility.ToJson(new PondletSaleReceipt{specimen=specimen,visit=visit.Id,price=price,cashBefore=cash,cashAfter=shop.Model.Currency,revenueBefore=revenue,revenueAfter=shop.Reports.Current.revenue},true));
        }
        IEnumerator SpeciesBatch(string species)
        {
            yield return IncubationStart(species);yield return AutomationWait(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,20,"Actual original-duration species recipe / "+species);
            yield return new WaitForSeconds(1.8f);string specimen=shop.Model.Incubator.SpecimenId;
            Check(shop.Model.Incubator.SpeciesId==species&&shop.World.IncubationSpecimenForValidation==specimen,"Exact actual ready species / "+species);
            IncubationSweep("species-"+species+"-ready",false);
            yield return AquariumAim(2.6f);Ok(shop.TransferToDisplay("tank-a"),"Actual saved ready specimen to display / "+species);yield return null;yield return null;
            PlayerExactOwner(specimen,"tank-a");SpeciesSweep("tank-a","species-"+species+"-display");
            yield return SpeciesClip("tank-a","species-"+species+"-night-live",CreatureMotion.Period(species)+.5f,true);
            Check(shop.OpenForDay(),"Open real ordinary day for species daylight view / "+species);yield return SpeciesClip("tank-a","species-"+species+"-day-live",CreatureMotion.Period(species)+.5f,true);
            if(species=="ribbonfin")yield return SpeciesLifecycle(species,specimen);
            yield return SpecimenDisplayPanel("tank-a");Check(shop.ReturnDisplayToStorage(speciesSlot,specimen),"Saved exact animated specimen to rack / "+species);
            shop.SetPanel(ShopPanel.None);yield return null;yield return null;PlayerExactOwner(specimen,speciesSlot);
            Check(!SpeciesVisual("tank-a")&&AquariumParticles()==0,"Transfer retires display fish/wake / "+species);
            SpeciesSweep(speciesSlot,"species-"+species+"-rack");yield return SpeciesRackAim();yield return SpeciesClip(speciesSlot,"species-"+species+"-rack-live",2);
            if(species=="pondlet"){yield return SpeciesIdentityFixture(speciesSlot,specimen);yield return SpeciesRackMove(specimen);}
            yield return SpecimenDisplayPanel("tank-a");Check(shop.StockDisplayFromStorage(speciesSlot,specimen),"Saved exact rack specimen returns to display / "+species);
            shop.SetPanel(ShopPanel.None);yield return null;PlayerExactOwner(specimen,"tank-a");
            if(species=="pondlet"){yield return SpeciesIdentityFixture("tank-a",specimen);yield return SpeciesPaidSale(specimen);}
            else {Ok(shop.Perform(shop.Model.SellFromTank("tank-a")),"Labeled direct-domain cleanup sale, not customer checkout / "+species);yield return null;yield return null;Check(!SpeciesVisual("tank-a")&&AquariumParticles()==0,"Cleanup has no stale species or wake / "+species);}
            Check(shop.CloseForNight(),"Close real day after species viewing / "+species);yield return AutomationWait(()=>shop.Cycle.Phase==ShopDayPhase.Night,6,"Actual closing to night");shop.SetPanel(ShopPanel.None);
        }
        IEnumerator SpeciesConditioner()
        {
            yield return AdvancedProduce("pondlet");yield return SpecimenRackPanel(speciesRack);Check(shop.StoreReadyInRack(speciesSlot),"Actual Pondlet input to existing rack");
            string specimen=shop.Model.GetStorage(speciesSlot).Value.Id;yield return AdvancedBeginBatch(speciesSlot);
            string key=advancedVat+"-conditioning-visual-0";yield return null;SpeciesSweep(key,"species-conditioner-pondlet");
            AdvancedAim(CultureKind.Mist);yield return null;yield return SpeciesClip(key,"species-conditioner-pondlet-live",2);
            yield return AutomationWait(()=>shop.Model.GetConditioner(advancedVat).Value.Phase==ConditionerPhase.Ready,16,"Actual supported Pondlet to Lanternfin conditioning");yield return null;yield return null;
            Check(shop.Model.GetConditioner(advancedVat).Value.First.Value.Id==specimen&&shop.Model.GetConditioner(advancedVat).Value.First.Value.SpeciesId=="lanternfin","Actual conditioner preserves identity through supported species change");
            SpeciesSweep(key,"species-conditioner-lanternfin");yield return SpeciesClip(key,"species-conditioner-lanternfin-live",2);
            yield return SpecimenRackPanel(speciesRack);Check(shop.StoreConditionerInRack(advancedVat,0,speciesSlot,specimen),"Saved exact conditioned output to rack");
            yield return null;yield return null;Check(!SpeciesVisual(key),"Collected conditioner retires visual resident");PlayerExactOwner(specimen,speciesSlot);
        }
        IEnumerator FxSpeciesScenario()
        {
            limitations.Add("FX12 sequential five existing species: real original recipes, saved display/rack transfers, actual customer Pondlet sale and one supported conditioning batch. Scripted operator cameras, isolated synthetic InputSystem, guarded menu commands;1500 purchase credits only. Other direct-domain sales are labeled housekeeping. Mesh/identity fixtures explicitly freeze or rewind isolated authority. Fixed tank-a wake only; no FX13 habitat expansion. Silent normal-HUD media is not native-input/listening/creative/minimum-spec/causal-GPU acceptance.");
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Species exact isolated save");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit isolated tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionSpeciesKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionSpeciesMouse");syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("species-input-provenance.json");
            if(!Application.isEditor){int width=int.Parse(ShopSession.Arg("-screen-width")),height=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(width==1280?1920:1280,height==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==width&&Screen.height==height,"Exact actual species HUD resolution");}
            StaffCreditFixture(1500,"FX12 existing siphon/research/rack/lab/vat construction only; no earned-economy claim");Check(shop.BuySiphonUpgrade().Success,"Existing siphon upgrade for real recipes");
            GateAim(new Vector3(0,.2f,2),new Vector3(0,1.2f,5));yield return null;Check(shop.OpenLabMenu()&&shop.BuyLabExpansion()&&shop.BuyLabExpansion(),"Actual legal lab extensions");shop.SetPanel(ShopPanel.None);
            yield return EditedPlace(null,ShopLayout.RackDefinitionId,9,7,0,new Vector3(4.5f,.2f,1.3f));speciesRack=shop.Layout.Items[shop.Layout.Items.Count-1].Id;speciesSlot=shop.Layout.GetStorageSlotIds(speciesRack)[0];
            foreach(string species in new[]{"pondlet","bubblebelly","ribbonfin"})yield return SpeciesBatch(species);
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);Check(shop.ResearchSpark(),"Actual Spark research after three real discoveries");shop.SetPanel(ShopPanel.None);
            yield return SpeciesBatch("lanternfin");
            Check(AdvancedFindPosition(null,false,-6,14,out int vatX,out int vatZ),"Existing legal conditioning prerequisite position");
            yield return EditedPlace(null,ShopLayout.ConditionerDefinitionId,vatX,vatZ,0,new Vector3(vatX*ShopLayout.GridSize,.2f,vatZ*ShopLayout.GridSize-1.9f));advancedVat=shop.Model.ConditionerIds[0];
            yield return ConditioningPanel(advancedVat);Check(shop.ResearchMist(),"Actual Mist research after Lanternfin discovery");shop.SetPanel(ShopPanel.None);
            yield return SpeciesBatch("driftwing");yield return SpeciesConditioner();
            var pool=shop.World.FxPool;Check(pool.OwnedCount==96&&pool.OwnedMeshCount==5&&pool.OwnedMaterialCount==1&&pool.Root.GetComponentsInChildren<Collider>(true).Length==0&&shop.GetComponents<AudioSource>().Length==3,"Five species retain exact pool and audio ownership");
            Check(growerInputReceipt.foreignCurrentSamples==0&&growerInputReceipt.otherEnabledSamples==0&&growerInputReceipt.droppedTransitions==0,"Species route actual synthetic input provenance stays isolated");
            Check(shop.SaveNow(),"Save final real species state");File.Copy(shop.SavePath,Path.Combine(evidence,"species-final-save.json"),true);shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
