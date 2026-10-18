#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvolutionShop
{
    // Explicit standalone diagnostic only. Does not exist in a non-development player.
    // Three owned readback/byte slots bound memory; a worker encodes, never Unity's main thread.
    sealed class FxReviewRecorder : IDisposable
    {
        [Serializable] public sealed class Frame { public int index, renderFrame; public float seconds, latency, realtime; }
        [Serializable] public sealed class Receipt
        {
            public string file, source = "Actual standalone HUD / scripted camera and input / silent video";
            public int width, height, targetFPS = 30, missedSamples;
            public float wallSeconds, achievedFPS, originRealtime;
            public Frame[] frames;
        }
        sealed class Slot { public RenderTexture texture; public byte[] bytes; public bool busy; }
        readonly Slot[] slots = new Slot[3];
        readonly BlockingCollection<int> queue = new BlockingCollection<int>(3);
        readonly List<Frame> frames = new List<Frame>(512);
        readonly object mutex = new object();
        readonly Process encoder;
        readonly Thread writer;
        readonly System.Threading.Tasks.Task<string> stderr;
        readonly string output;
        readonly float origin;
        readonly int width,height;
        volatile string failure;
        int pending, missed, nextTick;
        bool completed;
        public float Age => Time.realtimeSinceStartup-origin;
        public int Pending => pending;
        public string Failure => failure;
        public FxReviewRecorder(string executable,string file)
        {
            if(Application.isEditor || !SystemInfo.supportsAsyncGPUReadback)throw new InvalidOperationException("Review capture requires a standalone asynchronous-readback device.");
            if(!Path.IsPathFullyQualified(executable) || !File.Exists(executable) || executable.IndexOf('"')>=0 || file.IndexOf('"')>=0 || File.Exists(file))throw new ArgumentException("Explicit existing encoder and fresh capture required.");
            output=file;width=Screen.width;height=Screen.height;
            for(int i=0;i<slots.Length;i++)
            {
                slots[i]=new Slot {texture=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32),bytes=new byte[width*height*4]};slots[i].texture.Create();
            }
            encoder=new Process {StartInfo=new ProcessStartInfo {FileName=executable,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true,
                Arguments="-hide_banner -loglevel error -n -f rawvideo -pix_fmt rgba -video_size "+width+"x"+height+" -framerate 30 -i pipe:0 -an -c:v libx264 -preset ultrafast -crf 21 -pix_fmt yuv420p -movflags +faststart \""+file+"\""}};
            try {encoder.Start();}catch {foreach(var s in slots){s.texture.Release();UnityEngine.Object.Destroy(s.texture);}throw;}
            stderr=encoder.StandardError.ReadToEndAsync();
            writer=new Thread(WriteFrames){IsBackground=true,Name="Evolution Shop review encoder"};writer.Start();origin=Time.realtimeSinceStartup;
        }
        void WriteFrames()
        {
            try
            {
                foreach(int index in queue.GetConsumingEnumerable())
                {
                    encoder.StandardInput.BaseStream.Write(slots[index].bytes,0,slots[index].bytes.Length);
                    lock(mutex)slots[index].busy=false;
                }
            }
            catch(Exception ex){failure=ex.ToString();}
            finally{try{encoder.StandardInput.Close();}catch(Exception ex){failure=ex.ToString();}}
        }
        public void Sample()
        {
            if(failure!=null || completed)return;
            int tick=Mathf.FloorToInt(Age*30);if(tick<nextTick)return;
            missed+=Mathf.Max(0,tick-nextTick);nextTick=tick+1;
            int index=-1;
            lock(mutex)for(int i=0;i<slots.Length;i++)if(!slots[i].busy){slots[i].busy=true;index=i;break;}
            if(index<0){missed++;return;}
            int selected=index;float absolute=Time.realtimeSinceStartup;float issue=absolute-origin;int renderFrame=Time.frameCount;pending++;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(slots[index].texture);
            AsyncGPUReadback.Request(slots[index].texture,0,TextureFormat.RGBA32,request=>
            {
                pending--;
                if(request.hasError){failure="GPU review readback failed.";lock(mutex)slots[selected].busy=false;return;}
                try
                {
                    request.GetData<byte>().CopyTo(slots[selected].bytes);
                    frames.Add(new Frame {index=frames.Count,renderFrame=renderFrame,seconds=issue,latency=Age-issue,realtime=absolute});queue.Add(selected);
                }
                catch(Exception ex){failure=ex.ToString();lock(mutex)slots[selected].busy=false;}
            });
        }
        public Receipt Finish()
        {
            if(pending!=0)throw new InvalidOperationException("Drain GPU callbacks before finishing.");
            completed=true;queue.CompleteAdding();
            if(!writer.Join(15000))throw new TimeoutException("Review encoder writer did not finish.");
            if(!encoder.WaitForExit(15000))throw new TimeoutException("Review encoder did not finish.");
            string error=stderr.GetAwaiter().GetResult();
            if(failure!=null || encoder.ExitCode!=0)throw new InvalidOperationException(failure??error);
            float span=frames.Count>1?frames[frames.Count-1].seconds-frames[0].seconds:0;
            return new Receipt {file=Path.GetFileName(output),width=width,height=height,missedSamples=missed,wallSeconds=Age,originRealtime=origin,achievedFPS=span>0?(frames.Count-1)/span:0,frames=frames.ToArray()};
        }
        public void Dispose()
        {
            // All task paths dispose before the smoke process exits. Do not affect older players.
            AsyncGPUReadback.WaitAllRequests();
            if(!completed){queue.CompleteAdding();writer.Join(3000);if(!encoder.HasExited)encoder.Kill();}
            foreach(var s in slots){s.texture.Release();UnityEngine.Object.Destroy(s.texture);}queue.Dispose();encoder.Dispose();
        }
    }
}
#endif
