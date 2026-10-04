#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class MilestoneSmoke
    {
        [Serializable] sealed class AssetContractReport
        {
            public string unity = Application.unityVersion;
            public string note = "R06.2 project conventions, not industry standards. Conservative mesh AABBs in root space; sampled poses, not a proof over continuous time. PCM is before source/listener gain, not LUFS, true peak, a mixed-output measurement or listening approval.";
            public List<VisualContractRow> visuals = new List<VisualContractRow>();
            public List<AudioContractRow> audio = new List<AudioContractRow>();
            public float oneOfEachPeakSum;
            public float boundedVoicePeakSum;
        }
        [Serializable] sealed class VisualContractRow
        {
            public string asset;
            public Vector3 min, max, limitMin, limitMax;
            public int poseSamples, meshInstances, triangleInstances, materialInstances;
        }
        [Serializable] sealed class AudioContractRow
        {
            public string clip, wav;
            public int channels, rate, frames;
            public float seconds, samplePeak, rms, dc, first, last, peakDbfs, rmsDbfs;
        }

        void AuditPresentationContracts(Transform stage)
        {
            var report = new AssetContractReport();
            void SaveReport() => File.WriteAllText(Path.Combine(evidence,"asset-contracts.json"),JsonUtility.ToJson(report,true));
            var turntable = stage.Find("Turntable");
            Check(turntable, "Contract audit finds the existing sample roots");
            for(int view=2;view<=4;view++)
            {
                shop.SelectPresentationView(view);
                Transform root = null;
                foreach(Transform child in turntable)
                    if(child.gameObject.activeSelf && (child.GetComponent<CreatureVisual>() || child.GetComponent<ShopCharacterVisual>())) root=child;
                Check(root, "Contract asset exists / view "+view);
                var character=root.GetComponent<ShopCharacterVisual>();
                var creature=root.GetComponent<CreatureVisual>();
                Vector3 position=root.localPosition, scale=root.localScale;
                Quaternion rotation=root.localRotation;
                Bounds swept=default;
                bool initialized=false;
                int poses=character?4:1;
                // Two minutes at quarter-second intervals exercises slow idle and fast fin cycles.
                for(int pose=0;pose<poses;pose++)
                    for(int frame=0;frame<=480;frame++)
                    {
                        if(character) character.Pose((CharacterSamplePose)pose,frame*.25f);
                        else creature.Animate(frame*.25f);
                        Bounds bounds=ContractMeshBounds(root);
                        if(!initialized){swept=bounds;initialized=true;}else swept.Encapsulate(bounds);
                    }
                int gagSamples=0;
                if(character)
                    for(int kind=0;kind<6;kind++)
                        for(int frame=0;frame<=52;frame++)
                        {character.PoseComedy((ComedyKind)kind,frame*.05f,false);swept.Encapsulate(ContractMeshBounds(root));gagSamples++;}
                var limits=character
                    ? new Bounds(new Vector3(0,.905f,.125f),new Vector3(1.7f,1.89f,1.55f))
                    : new Bounds(new Vector3(0,0,.175f),new Vector3(1.2f,.8f,1.35f));
                report.visuals.Add(ContractVisualRow(root,swept,limits,poses*481+gagSamples));SaveReport();
                Check(limits.Contains(swept.min) && limits.Contains(swept.max),"Posed meshes stay within the documented local envelope / "+root.name);
                Check(root.localPosition==position && root.localScale==scale && Quaternion.Angle(root.localRotation,rotation)<.001f,"Presentation poses have no root motion / "+root.name);
                Check(root.GetComponentsInChildren<Collider>(true).Length==0 && root.GetComponentsInChildren<Station>(true).Length==0,"Visual rig owns no colliders or interaction stations / "+root.name);
                if(character)character.Pose(CharacterSamplePose.Idle,0);else creature.Animate(0);
            }
            Check(shop.World.TryGetLabFacility(ShopLayout.BloomFacilityId,out var bloom),"Contract audit locates the existing Bloom facility");
            Bounds bloomBounds=ContractMeshBounds(bloom);
            var bloomLimits=new Bounds(new Vector3(0,1.25f,0),new Vector3(ShopLayout.FacilityWidth(ShopLayout.BloomFacilityId),2.5f,ShopLayout.FacilityDepth(ShopLayout.BloomFacilityId)));
            report.visuals.Add(ContractVisualRow(bloom,bloomBounds,bloomLimits,1));SaveReport();
            Check(bloomLimits.Contains(bloomBounds.min) && bloomLimits.Contains(bloomBounds.max),"Bloom sample stays inside the authoritative facility footprint and height limit");
            foreach(var collider in bloom.GetComponentsInChildren<Collider>(true))
                Check(collider.transform==bloom,"Bloom decoration adds no child collision");

            var clips=shop.GetComponent<ShopAudio>().ValidationClips();
            Check(clips.Length==15,"Audit covers five service/slap cues and ten comedy variations");
            float servicePeak=0,comedyPeak=0;
            for(int index=0;index<clips.Length;index++)
            {
                var clip=clips[index];
                Check(clip && clip.samples>0 && clip.channels==1 && clip.frequency==22050 && clip.length<=1f,"Current cue format and duration / "+index);
                var samples=new float[clip.samples*clip.channels];
                Check(clip.GetData(samples,0),"Read actual generated PCM / "+clip.name);
                double square=0,sum=0;float peak=0;
                foreach(float sample in samples)
                {
                    if(float.IsNaN(sample)||float.IsInfinity(sample))throw new InvalidOperationException("Non-finite PCM: "+clip.name);
                    square+=(double)sample*sample;sum+=sample;peak=Mathf.Max(peak,Mathf.Abs(sample));
                }
                var row=new AudioContractRow{clip=clip.name,channels=clip.channels,rate=clip.frequency,frames=clip.samples,seconds=(float)clip.samples/clip.frequency,
                    samplePeak=peak,rms=(float)Math.Sqrt(square/samples.Length),dc=(float)(sum/samples.Length),first=samples[0],last=samples[samples.Length-1],wav="audio-cue-"+index+".wav"};
                row.peakDbfs=20*Mathf.Log10(Mathf.Max(row.samplePeak,1e-9f));row.rmsDbfs=20*Mathf.Log10(Mathf.Max(row.rms,1e-9f));
                report.audio.Add(row);report.oneOfEachPeakSum+=peak;SaveReport();
                if(index<4)servicePeak=Mathf.Max(servicePeak,peak);else comedyPeak=Mathf.Max(comedyPeak,peak);
                Check(peak>0 && peak<=.25f && row.rms<=.12f && Mathf.Abs(row.dc)<=.005f,"Cue meets project peak, RMS and DC ceilings / "+clip.name);
                Check(Mathf.Abs(row.first)<=.005f && Mathf.Abs(row.last)<=.005f,"Cue starts/ends near zero / "+clip.name);
                WriteContractWav(Path.Combine(evidence,row.wav),samples,clip.frequency);
            }
            report.boundedVoicePeakSum=servicePeak+comedyPeak;SaveReport();
            Check(report.boundedVoicePeakSum<1f,"Worst service plus comedy voice peak sum stays below full scale before gains");
            shop.SelectPresentationView(4);
        }

        static Bounds ContractMeshBounds(Transform root)
        {
            bool initialized=false;Bounds bounds=default;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!ContractVisible(filter.transform,root))continue;
                var renderer=filter.GetComponent<MeshRenderer>();
                if(!renderer || !renderer.enabled || !filter.sharedMesh)continue;
                var local=filter.sharedMesh.bounds;
                var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var corner=matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(float.IsNaN(corner.x)||float.IsInfinity(corner.x)||float.IsNaN(corner.y)||float.IsInfinity(corner.y)||float.IsNaN(corner.z)||float.IsInfinity(corner.z))throw new InvalidOperationException("Invalid mesh bounds: "+filter.name);
                    if(!initialized){bounds=new Bounds(corner,Vector3.zero);initialized=true;}else bounds.Encapsulate(corner);
                }
            }
            if(!initialized)throw new InvalidOperationException("No visible meshes: "+root.name);
            return bounds;
        }
        static bool ContractVisible(Transform node,Transform root)
        {
            for(var current=node;current!=root;current=current.parent)if(!current.gameObject.activeSelf)return false;
            return true;
        }
        static VisualContractRow ContractVisualRow(Transform root,Bounds bounds,Bounds limits,int samples)
        {
            var materials=new HashSet<Material>();int meshes=0,triangles=0;
            // Counts include hidden parcel meshes; no claim about draw calls or batching.
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)throw new InvalidOperationException("Missing sample mesh: "+filter.name);
                meshes++;triangles+=filter.sharedMesh.triangles.Length/3;
            }
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.GetComponent<TextMesh>())continue;
                foreach(var material in renderer.sharedMaterials)
                {
                    if(!material || !material.shader || material.shader.name!="Universal Render Pipeline/Lit" || !material.shader.isSupported)throw new InvalidOperationException("Sample requires supported URP Lit: "+renderer.name);
                    materials.Add(material);
                }
            }
            return new VisualContractRow{asset=root.name,min=bounds.min,max=bounds.max,limitMin=limits.min,limitMax=limits.max,poseSamples=samples,meshInstances=meshes,triangleInstances=triangles,materialInstances=materials.Count};
        }
        static void WriteContractWav(string path,float[] samples,int rate)
        {
            // Original mono PCM for human comparison; no master/humor gain baked into evidence.
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);
                writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                foreach(float sample in samples)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
            }
        }
    }
}
#endif
