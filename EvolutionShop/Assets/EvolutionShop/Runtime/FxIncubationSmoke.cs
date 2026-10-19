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
        [Serializable] sealed class IncubationFrame
        {
            public float seconds, remaining, progress, hatchAge, hatchRemaining, fit;
            public int hatches, sounds, particles, total, ambient;
            public string phase, species, specimen, renderedSpecies, renderedSpecimen;
            public bool first, reduced, low;
            public Vector3 scale, station, fluidScale;
            public Quaternion rotation;
        }
        [Serializable] sealed class IncubationTrace { public string context; public IncubationFrame[] frames; }
        [Serializable] sealed class IncubationFit
        {
            public string species, specimen, context;
            public int meshes, vertices, samples, quarterTurns;
            public float maxFluidRadiusSquared;
            public Vector3 station;
            public Quaternion rotation;
            public string worstMesh;
            public Vector3 worstLocalVertex;
            public float worstTime, worstProgressOrAge;
            public bool worstReduced, worstFirst;
        }
        PondletMesh[] incubationMeshes;
        bool incubationLow, incubationReduced;
        int incubationMoveX, incubationMoveZ;
        string incubationFitMesh;
        Vector3 incubationFitVertex;
        Material[] IncubationPrivateMaterials()
        {
            var materials=new HashSet<Material>();
            foreach(var renderer in shop.World.HatchCreatureForValidation.GetComponentsInChildren<Renderer>())
                foreach(var material in renderer.sharedMaterials)if(material)materials.Add(material);
            var result=new Material[materials.Count];materials.CopyTo(result);return result;
        }
        void IncubationMaterialsRetired(Material[] materials,string context)
        {
            Check(materials.Length>0,"Actual old resident owned palette / "+context);
            foreach(var material in materials)Check(material==null,"Engine destruction releases old resident private material / "+context);
        }
        void IncubationCacheMeshes()
        {
            var root=shop.World.GrowthCreatureForValidation;
            Check(root,"Incubation uses an actual rendered resident");
            var filters=root.GetComponentsInChildren<MeshFilter>();
            incubationMeshes=new PondletMesh[filters.Length];
            for(int i=0;i<filters.Length;i++) incubationMeshes[i]=new PondletMesh {Root=filters[i].transform,Vertices=filters[i].sharedMesh.vertices};
            Check(filters.Length>0&&root.GetComponentsInChildren<Collider>(true).Length==0,"Actual resident has visible geometry and no gameplay collider");
        }
        float IncubationMeshFit()
        {
            var fluid=shop.World.IncubationFluidForValidation;
            var bounds=fluid.GetComponent<MeshFilter>().sharedMesh.bounds;
            float maximum=0;
            // Existing fluid is an actual sphere primitive, including its offset and animated scale.
            // Each visible fin/tail/lantern transform is sampled; no model-scale surrogate is used.
            foreach(var mesh in incubationMeshes) foreach(var vertex in mesh.Vertices) {
                var p=fluid.InverseTransformPoint(mesh.Root.TransformPoint(vertex))-bounds.center;
                var q=new Vector3(p.x/bounds.extents.x,p.y/bounds.extents.y,p.z/bounds.extents.z);
                if(q.sqrMagnitude>maximum){maximum=q.sqrMagnitude;incubationFitMesh=mesh.Root.name;incubationFitVertex=vertex;}
            }
            return maximum;
        }
        IncubationFrame IncubationSample(float seconds)
        {
            var world=shop.World;var inc=shop.Model.Incubator;var root=world.GrowthCreatureForValidation;var pool=world.FxPool;
            if(!root||world.IncubationSpecimenForValidation!=inc.SpecimenId||world.IncubationSpeciesForValidation!=inc.SpeciesId)
                throw new InvalidOperationException("Rendered incubation resident identity differs from current authority");
            int ambient=0;for(int i=FxEffectPool.ActionReserved;i<FxEffectPool.Capacity;i++)if(pool.Root.GetChild(i).gameObject.activeSelf)ambient++;
            float fit=IncubationMeshFit();
            if(fit>1.003f)throw new InvalidOperationException("Actual live specimen escapes animated physical fluid / "+inc.SpeciesId+" / "+fit+" / "+incubationFitMesh+" / "+incubationFitVertex);
            if(pool.ActiveCount>(incubationLow?32:96)||ambient>(incubationLow?8:32))throw new InvalidOperationException("Incubation exceeds shared total/ambient policy");
            return new IncubationFrame {seconds=seconds,remaining=inc.SecondsRemaining,progress=world.LabGrowthProgress,hatchAge=world.LabHatchAge,hatchRemaining=world.LabHatchRemaining,
                hatches=world.LabHatchCount,sounds=shop.GetComponent<ShopAudio>().LabPlaybackCount,particles=OrganicHatchParticles(),total=pool.ActiveCount,ambient=ambient,
                phase=inc.Phase.ToString(),species=inc.SpeciesId,specimen=inc.SpecimenId,renderedSpecies=world.IncubationSpeciesForValidation,renderedSpecimen=world.IncubationSpecimenForValidation,
                first=world.LabHatchFirst,reduced=incubationReduced,low=incubationLow,scale=root.localScale,fit=fit,
                station=world.IncubationStationForValidation.position,rotation=world.IncubationStationForValidation.rotation,fluidScale=world.IncubationFluidForValidation.localScale};
        }
        IEnumerator IncubationClip(string name,float seconds,string context,Action<float> action=null)
        {
            var frames=new List<IncubationFrame>();Action<float> sample=age=>{action?.Invoke(age);frames.Add(IncubationSample(age));};
            if(Application.isEditor){float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<seconds){sample(Time.realtimeSinceStartup-began);yield return null;}}
            else yield return ReviewClip(name,seconds,sample);
            File.WriteAllText(Path.Combine(evidence,name+"-motion.json"),JsonUtility.ToJson(new IncubationTrace{context=context,frames=frames.ToArray()},true));
            Check(frames.Count>2,"Actual incubation samples / "+name);
        }
        void IncubationSweep(string name,bool growth)
        {
            var world=shop.World;var station=world.IncubationStationForValidation;var inc=shop.Model.Incubator;
            IncubationCacheMeshes();string authority=FxAuthority();var position=station.position;var rotation=station.rotation;
            var report=new IncubationFit {species=inc.SpeciesId,specimen=inc.SpecimenId,meshes=incubationMeshes.Length,station=position,rotation=rotation,
                quarterTurns=shop.Layout.GetFacility(ShopLayout.IncubatorFacilityId).Value.QuarterTurns,
                context=growth?"Frozen-authority actual growth/comfort geometry sweep":"Frozen-authority actual first/repeat hatch, release landmarks and comfort geometry sweep"};
            foreach(var mesh in incubationMeshes)report.vertices+=mesh.Vertices.Length;
            for(int reduced=0;reduced<2;reduced++) {
                world.SetFxPreviewModes(reduced==1,false);
                for(int phase=0;phase<8;phase++) {
                    if(growth)for(int step=0;step<=10;step++) {
                        world.SampleGrowthPoseForValidation(step/10f,phase*.95f);report.samples++;
                        IncubationWorst(report,phase*.95f,step/10f,reduced==1,false);
                    }
                    else for(int first=0;first<2;first++)for(int step=0;step<=14;step++) {
                        float age=step==13?FxHatchChoreography.Release(first==1):step==14?FxHatchChoreography.Release(first==1)+.10f:FxHatchChoreography.Duration(first==1)*step/12f;
                        world.SampleHatchPoseForValidation(age,first==1,phase*.95f);report.samples++;
                        IncubationWorst(report,phase*.95f,age,reduced==1,first==1);
                    }
                }
            }
            world.SetFxPreviewModes(false,false);world.Animate(Time.time,0);
            File.WriteAllText(Path.Combine(evidence,name+"-mesh.json"),JsonUtility.ToJson(report,true));
            Check(report.vertices>0&&report.maxFluidRadiusSquared<=1.003f,"Actual complete animated mesh fits physical fluid / "+name+" / "+report.maxFluidRadiusSquared);
            Check(FxAuthority()==authority&&station.position==position&&station.rotation==rotation,"Geometry sweep cannot alter countdown, stock, identity or station / "+name);
        }
        void IncubationWorst(IncubationFit report,float time,float progressOrAge,bool reduced,bool first)
        {
            float fit=IncubationMeshFit();if(fit<=report.maxFluidRadiusSquared)return;
            report.maxFluidRadiusSquared=fit;report.worstMesh=incubationFitMesh;report.worstLocalVertex=incubationFitVertex;
            report.worstTime=time;report.worstProgressOrAge=progressOrAge;report.worstReduced=reduced;report.worstFirst=first;
        }
        IEnumerator IncubationStart(string species)
        {
            yield return AdvancedClear();var recipe=shop.Model.GetSpecies(species);
            foreach(var kind in new[]{CultureKind.Bloom,CultureKind.Flow,CultureKind.Spark,CultureKind.Mist}) {
                int needed=ShopAutomation.Required(recipe,kind);if(needed>0)yield return AdvancedHarvest(kind,needed);
            }
            DoseAim();foreach(var kind in new[]{CultureKind.Bloom,CultureKind.Flow,CultureKind.Spark,CultureKind.Mist})
                for(int i=0;i<ShopAutomation.Required(recipe,kind);i++)Check(shop.DoseCulture(kind),"Saved real recipe dose / "+species+" / "+kind);
            Check(shop.BeginManualIncubation()&&shop.Model.Incubator.SpeciesId==species,"Start actual recipe with original duration / "+species);
            var created=shop.World.GrowthCreatureForValidation;Check(created,"Accepted incubation has an actual rendered resident immediately");
            foreach(var collider in created.GetComponentsInChildren<Collider>(true))Check(!collider.enabled,"New primitive collider is disabled before deferred engine removal / "+species);
            // Shape disables primitive colliders synchronously and uses normal deferred Destroy in play.
            // Let that engine boundary complete, then retain the strict zero-component invariant below.
            yield return null;
            GrowthAim();IncubationCacheMeshes();
        }
        IEnumerator IncubationQuietReady(string species)
        {
            var world=shop.World;var audio=shop.GetComponent<ShopAudio>();string id=shop.Model.Incubator.SpecimenId;
            int hatches=world.LabHatchCount,sounds=audio.LabPlaybackCount;
            foreach(var panel in new[]{ShopPanel.Pause,ShopPanel.Arrange,ShopPanel.None}) {
                shop.SetPanel(panel);yield return new WaitForSeconds(.15f);
                Check(shop.Model.Incubator.SpecimenId==id&&world.LabHatchRemaining==0&&OrganicHatchParticles()==0&&!audio.HatchAudioActive,
                    "Ready policy transition preserves quiet same-ID output / "+species+" / "+panel);
            }
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Reload actual ready output / "+species);shop.SetPanel(ShopPanel.None);
            var old=world.HatchCreatureForValidation;var oldMaterials=IncubationPrivateMaterials();world.RebuildIncubationResidentForValidation();yield return null;yield return null;
            IncubationMaterialsRetired(oldMaterials,species+" rebuilt-ready");
            Check(world.HatchCreatureForValidation&&world.HatchCreatureForValidation!=old,"Rebuild creates actual replacement rendered resident / "+species);IncubationCacheMeshes();
            for(int i=0;i<12;i++) {world.Refresh(shop.Model);yield return null;
                Check(world.LabHatchCount==hatches&&audio.LabPlaybackCount==sounds&&world.LabHatchRemaining==0&&OrganicHatchParticles()==0&&!audio.HatchAudioActive&&world.IncubationSpecimenForValidation==id,
                    "Repeated ready refresh/reload/rebuild never replays saved success / "+species+" / "+i);}
        }
        IEnumerator IncubationCollect(string species,string destination,bool immediate=false)
        {
            string id=shop.Model.Incubator.SpecimenId;var world=shop.World;var audio=shop.GetComponent<ShopAudio>();
            if(destination!="tank-b"&&!immediate) {
                int hatches=world.LabHatchCount;Check(!shop.TransferToDisplay("tank-b").Success&&shop.Model.Incubator.SpecimenId==id&&world.LabHatchCount==hatches,
                    "Occupied destination refuses without consuming ready output or replaying hatch / "+species);
            }
            if(immediate)Check(world.LabHatchRemaining>0,"Immediate collection starts during actual active hatch");
            var oldMaterials=IncubationPrivateMaterials();
            Ok(shop.TransferToDisplay(destination),"Saved collection of exact real output / "+species);yield return null;yield return null;
            IncubationMaterialsRetired(oldMaterials,species+" actual transfer");
            Check(shop.Model.GetTank(destination).Value.Id==id&&shop.Model.GetTank(destination).Value.SpeciesId==species&&shop.Model.Incubator.Phase==IncubatorPhase.Dosing&&
                !world.HatchCreatureForValidation&&world.LabHatchRemaining==0&&!audio.HatchAudioActive,"Exact transfer has one model owner and retires incubator pose/audio / "+species);
            if(destination=="tank-a") {
                // Bounded housekeeping through the existing domain transaction, not a customer checkout claim.
                Ok(shop.Perform(shop.Model.SellFromTank(destination)),"Labeled domain cleanup sale frees display for next incubation case");
                Check(!shop.Model.GetTank(destination).HasValue&&shop.SaveError==null,"Cleanup sale is durably saved once");
            }
        }
        IEnumerator IncubationRotations(string species,bool growth)
        {
            var world=shop.World;var original=world.IncubationStationForValidation;string authority=FxAuthority();int hatches=world.LabHatchCount;
            shop.FreezeFxVocabularyForValidation=true;
            try {
                for(int turn=0;turn<4;turn++) {
                    var preview=shop.Layout.TryPreviewFacility(ShopLayout.IncubatorFacilityId,incubationMoveX,incubationMoveZ,turn,out var layout);
                    Check(preview.Success,"Existing incubator supports legal chosen orientation / "+turn);var placed=layout.GetFacility(ShopLayout.IncubatorFacilityId).Value;
                    var position=new Vector3(placed.WorldX,.2f,placed.WorldZ)+Quaternion.Euler(0,turn*90,0)*Vector3.back*1.9f;
                    yield return LabMove(ShopLayout.IncubatorFacilityId,incubationMoveX,incubationMoveZ,turn,position);
                    shop.SetPanel(ShopPanel.None);shop.AllowValidationInput=false;GrowthAim();
                    Check(original==world.IncubationStationForValidation&&FxAuthority()==authority&&world.LabHatchCount==hatches,"Legal saved move preserves original root, exact production and no hatch / "+species+" / "+turn);
                    IncubationSweep(species+(growth?"-growth-":"-ready-")+turn,growth);
                }
            }
            finally {shop.FreezeFxVocabularyForValidation=false;shop.SetPanel(ShopPanel.None);world.SetFxPreviewModes(false,false);world.ResetFxPresentation();}
        }
        IEnumerator IncubationBatch(string species,bool first)
        {
            Check(shop.Model.IsDiscovered(species)!=first,"Natural first/repeat prerequisite / "+species+" / "+first);
            yield return IncubationStart(species);var world=shop.World;var audio=shop.GetComponent<ShopAudio>();
            string id=shop.Model.Incubator.SpecimenId;int hatches=world.LabHatchCount,sounds=audio.LabPlaybackCount;
            if(first) {
                // Presentation sweeps freeze only this labeled fixture, before the ordinary recording begins.
                yield return IncubationRotations(species,true);GrowthAim();IncubationCacheMeshes();
            }
            float initial=shop.Model.Incubator.SecondsRemaining,began=Time.realtimeSinceStartup,minScale=float.MaxValue,maxScale=0,maxClockError=0;
            bool seenRelease=false,wrongFirst=false;int peak=0;
            yield return IncubationClip(species+(first?"-first-growth-hatch-live":"-repeat-growth-hatch-live"),initial+1.8f,
                "Normal real-time existing recipe countdown and committed discovery; scripted operator, silent capture",age=>{
                    var inc=shop.Model.Incubator;
                    if(inc.Phase==IncubatorPhase.Incubating) {
                        maxClockError=Mathf.Max(maxClockError,Mathf.Abs((initial-inc.SecondsRemaining)-(Time.realtimeSinceStartup-began)));
                        minScale=Mathf.Min(minScale,world.GrowthCreatureForValidation.localScale.x);maxScale=Mathf.Max(maxScale,world.GrowthCreatureForValidation.localScale.x);
                        if(world.LabHatchCount!=hatches)throw new InvalidOperationException("Hatch happened before actual readiness");
                    }
                    if(world.LabHatchRemaining>0){seenRelease=true;wrongFirst|=world.LabHatchFirst!=first;}
                    peak=Mathf.Max(peak,OrganicHatchParticles());
                });
            Check(shop.Model.Incubator.Phase==IncubatorPhase.Ready&&shop.Model.Incubator.SpecimenId==id&&shop.Model.IsDiscovered(species)&&world.LabHatchCount==hatches+1,
                "One naturally saved same-ID hatch / "+species+" / "+first);
            Check(seenRelease&&!wrongFirst&&peak>0&&audio.LabPlaybackCount==sounds+1&&maxClockError<.25f&&maxScale>minScale*1.5f,
                "Actual growth, original clock, correct first/repeat release and exactly one sound / "+species+" clockError="+maxClockError+" peak="+peak);
            Check(world.LabHatchRemaining==0&&OrganicHatchParticles()==0&&!world.LabGrowthActive,"Ready settles without growth emissions / "+species);
            if(first){yield return IncubationRotations(species,false);yield return IncubationQuietReady(species);}
            yield return IncubationCollect(species,species=="pondlet"&&first?"tank-b":"tank-a");
        }
        IEnumerator IncubationFailureAndImmediate()
        {
            yield return IncubationStart("driftwing");var world=shop.World;var audio=shop.GetComponent<ShopAudio>();string id=shop.Model.Incubator.SpecimenId;
            shop.SetPanel(ShopPanel.Pause);float remaining=shop.Model.Incubator.SecondsRemaining;
            Check(shop.SaveNow()&&shop.ReloadSaved(),"Save/reload paused working exact batch");yield return new WaitForSeconds(.2f);
            Check(shop.Model.Incubator.SpecimenId==id&&shop.Model.Incubator.SecondsRemaining==remaining&&world.LabHatchRemaining==0,"Paused working reload cannot advance or replay");
            shop.SetPanel(ShopPanel.None);GrowthAim();IncubationCacheMeshes();
            shop.FreezeFxVocabularyForValidation=true;
            try {
                string authority=FxAuthority();
                for(int mode=1;mode<=2;mode++) {
                    incubationLow=mode==1;incubationReduced=mode==2;world.SetFxPreviewModes(incubationReduced,incubationLow);world.ResetFxPresentation();
                    yield return IncubationClip(mode==1?"driftwing-growth-low-cosmetic":"driftwing-growth-reduced-cosmetic",3,
                        "Explicit frozen-authority diagnostic comfort sample, not normal countdown footage",age=>world.Animate(Time.time,Time.deltaTime));
                    Check(FxAuthority()==authority,"Comfort geometry cannot advance authoritative batch");
                }
                world.SetFxPreviewModes(false,false);incubationLow=incubationReduced=false;
                shop.SetFxFocusForValidation(false);world.Animate(Time.time,.3f);
                Check(!world.LabGrowthActive&&OrganicHatchParticles()==0&&world.LabHatchRemaining==0,"Focus loss suppresses working owner with no fake readiness");
                shop.SetFxFocusForValidation(true);shop.enabled=false;
                Check(OrganicHatchParticles()==0&&world.LabHatchRemaining==0,"Disable clears active incubation particles");shop.enabled=true;shop.SetFxFocusForValidation(true);
                Check(FxAuthority()==authority,"Frozen working lifecycle preserves all production authority");
            }
            finally {shop.enabled=true;shop.FreezeFxVocabularyForValidation=false;incubationLow=incubationReduced=false;world.SetFxPreviewModes(false,false);world.ResetFxPresentation();shop.SetFxFocusForValidation(true);}
            yield return WaitFor(()=>shop.Model.Incubator.SecondsRemaining<.25f,12,"Natural pre-hatch write-failure boundary");
            Check(shop.SaveNow(),"Preserve actual near-ready durable batch");byte[] durable=File.ReadAllBytes(shop.SavePath);int hatches=world.LabHatchCount,sounds=audio.LabPlaybackCount;
            TutorialBlockSave();
            try {
                yield return WaitFor(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,2,"Natural unsaved readiness");yield return null;
                Check(shop.SaveError!=null&&world.LabHatchCount==hatches&&audio.LabPlaybackCount==sounds&&!audio.HatchAudioActive&&world.LabHatchRemaining==0&&OrganicHatchParticles()==0,
                    "Failed natural non-Pondlet readiness has no success pose, burst or sound");
                Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Failed hatch preserves prior durable slot; live mutation is not claimed rollback");
            }
            finally {TutorialUnblockSave();}
            Check(shop.ReloadSaved()&&shop.Model.Incubator.Phase==IncubatorPhase.Incubating&&shop.Model.Incubator.SpecimenId==id,"Restore exact durable working batch after failed readiness");
            shop.SetPanel(ShopPanel.None);yield return WaitFor(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,2,"Recovered actual natural hatch");
            Check(world.LabHatchCount==hatches+1&&shop.Model.Incubator.SpecimenId==id,"Recovered saved readiness celebrates exactly once");
            yield return IncubationCollect("driftwing","tank-a",true);
        }
        IEnumerator IncubationFirstFailure()
        {
            Check(!shop.Model.IsDiscovered("pondlet")&&!shop.DiscoveryRevealActiveForValidation,"Fresh isolated first-failure case has no discovery or reveal");
            yield return IncubationStart("pondlet");string id=shop.Model.Incubator.SpecimenId;var world=shop.World;var audio=shop.GetComponent<ShopAudio>();
            yield return WaitFor(()=>shop.Model.Incubator.SecondsRemaining<.25f,12,"Natural first-discovery failure boundary");
            Check(shop.SaveNow(),"Save actual first working batch before failure");byte[] durable=File.ReadAllBytes(shop.SavePath);
            File.WriteAllBytes(Path.Combine(evidence,"first-failure-working-save.json"),durable);
            int hatches=world.LabHatchCount,sounds=audio.LabPlaybackCount;TutorialBlockSave();
            try {
                yield return WaitFor(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,2,"Natural first discovery with blocked persistence");
                yield return new WaitForSeconds(.25f);
                Check(shop.SaveError!=null&&world.LabHatchCount==hatches&&audio.LabPlaybackCount==sounds&&world.LabHatchRemaining==0&&OrganicHatchParticles()==0&&!audio.HatchAudioActive&&
                    !shop.DiscoveryRevealActiveForValidation&&string.IsNullOrEmpty(shop.RevealedSpeciesForValidation),"Unsaved first discovery creates no reveal, hatch, delayed sound or retry");
                Check(Convert.ToBase64String(durable)==Convert.ToBase64String(File.ReadAllBytes(shop.SavePath)),"Unsaved first discovery preserves durable still-undiscovered working slot");
            }
            finally {TutorialUnblockSave();}
            Check(shop.ReloadSaved()&&!shop.Model.IsDiscovered("pondlet")&&shop.Model.Incubator.SpecimenId==id&&shop.Model.Incubator.Phase==IncubatorPhase.Incubating,"Reload restores same-ID undiscovered working batch");
            shop.SetPanel(ShopPanel.None);yield return WaitFor(()=>shop.Model.Incubator.Phase==IncubatorPhase.Ready,2,"Natural saved first-discovery recovery");
            Check(shop.Model.IsDiscovered("pondlet")&&shop.Model.Incubator.SpecimenId==id&&world.LabHatchCount==hatches+1&&world.LabHatchFirst&&shop.DiscoveryRevealActiveForValidation&&shop.RevealedSpeciesForValidation=="pondlet",
                "Recovered first discovery has one exact-ID saved reveal and first hatch");
            shop.ConfirmNewGame();Check(shop.SkipTutorial()&&!shop.Model.IsDiscovered("pondlet"),"Reset only isolated failure fixture before independent natural five-species media");
            shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
        }
        IEnumerator FxIncubationScenario()
        {
            limitations.Add("FX09: five actual first discoveries and repeated original-duration recipes; synthetic held collection and guarded dosing with isolated input provenance.1000 purchase credits are the only credit setup fixture; between cases direct-domain saved sales are labeled cleanup, not customer checkout. Legal saved rotations and all-mesh sweeps freeze authority explicitly; videos use ordinary real-time progression. One physical supported incubator is covered, no new container mechanics. Low/reduced are diagnostic policies. Silent media is not native-input/listening/creative/GPU acceptance.");
            Check(shop.ValidationMode&&Path.GetFullPath(shop.SavePath)==Path.GetFullPath(Path.Combine(evidence,"smoke-save.json")),"Incubation uses exact isolated slot");
            yield return new WaitForSeconds(1);Check(shop.SkipTutorial(),"Explicit incubation tutorial skip");shop.Customers.ArrivalsEnabled=false;shop.AmbientComedyEnabled=false;shop.AllowValidationInput=false;shop.SetFxFocusForValidation(true);
            previousInputSettings=InputSystem.settings;smokeInputSettings=Instantiate(previousInputSettings);smokeInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            smokeInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=smokeInputSettings;syntheticKeyboard=InputSystem.AddDevice<Keyboard>("EvolutionIncubationKeyboard");cycleMouse=InputSystem.AddDevice<Mouse>("EvolutionIncubationMouse");
            syntheticKeyboard.MakeCurrent();cycleMouse.MakeCurrent();GrowerIsolateInputs("incubation-input-provenance.json");
            if(!Application.isEditor){int w=int.Parse(ShopSession.Arg("-screen-width")),h=int.Parse(ShopSession.Arg("-screen-height"));Screen.SetResolution(w==1280?1920:1280,h==720?1080:720,FullScreenMode.Windowed);yield return new WaitForSeconds(.3f);Screen.SetResolution(w,h,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);Check(Screen.width==w&&Screen.height==h,"Exact incubation media resolution");}
            yield return IncubationFirstFailure();
            StaffCreditFixture(1000,"FX09 existing research, siphon and construction only; no earned-economy claim");Check(shop.BuySiphonUpgrade().Success,"Buy existing siphon upgrade");
            GateAim(new Vector3(0,.2f,2),new Vector3(0,1.2f,5));yield return null;Check(shop.OpenLabMenu()&&shop.BuyLabExpansion()&&shop.BuyLabExpansion(),"Buy actual legal lab extensions");shop.SetPanel(ShopPanel.None);
            Check(AdvancedFindPosition(ShopLayout.IncubatorFacilityId,true,0,18,out incubationMoveX,out incubationMoveZ),"Preview one genuinely relocated position legal at all four orientations");
            foreach(string species in new[]{"pondlet","bubblebelly","ribbonfin"}){yield return IncubationBatch(species,true);yield return IncubationBatch(species,false);}
            yield return ResearchFacilityPanel(ShopLayout.UpgradeFacilityId,ShopPanel.Upgrade);int cash=shop.Model.Currency;
            Check(shop.ResearchSpark()&&shop.Model.Currency==cash-shop.Config.SparkResearchCost&&!shop.Model.IsDiscovered("lanternfin"),"Paid Spark research follows three real first discoveries");shop.SetPanel(ShopPanel.None);
            yield return IncubationBatch("lanternfin",true);yield return IncubationBatch("lanternfin",false);
            Check(AdvancedFindPosition(null,false,-6,14,out int vatX,out int vatZ),"Preview real legal conditioner prerequisite");
            yield return EditedPlace(null,ShopLayout.ConditionerDefinitionId,vatX,vatZ,0,new Vector3(vatX*ShopLayout.GridSize,.2f,vatZ*ShopLayout.GridSize-1.9f));advancedVat=shop.Model.ConditionerIds[0];
            yield return ConditioningPanel(advancedVat);cash=shop.Model.Currency;Check(shop.ResearchMist()&&shop.Model.Currency==cash-shop.Config.MistResearchCost,"Paid Mist research follows actual Lanternfin discovery and installed vat");shop.SetPanel(ShopPanel.None);
            yield return IncubationBatch("driftwing",true);yield return IncubationBatch("driftwing",false);
            yield return IncubationFailureAndImmediate();
            var pool=shop.World.FxPool;Check(pool.OwnedCount==96&&pool.OwnedMeshCount==5&&pool.OwnedMaterialCount==1&&pool.Root.GetComponentsInChildren<Collider>(true).Length==0&&shop.GetComponents<AudioSource>().Length==5,"Repeated five-species work retains fixed pool/resources/voices");
            Check(growerInputReceipt.foreignCurrentSamples==0&&growerInputReceipt.otherEnabledSamples==0&&growerInputReceipt.droppedTransitions==0,"Incubation synthetic input provenance stays exact");
            Check(shop.SaveNow(),"Save final isolated incubation state");File.Copy(shop.SavePath,Path.Combine(evidence,"incubation-final-save.json"),true);shop.SetPanel(ShopPanel.Pause);
        }
    }
}
#endif
