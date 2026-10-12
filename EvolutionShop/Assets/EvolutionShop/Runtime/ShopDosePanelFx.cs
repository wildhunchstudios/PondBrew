using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        void DrawLabDoseStrip()
        {
            Rect rect=GUILayoutUtility.GetRect(1,86,GUILayout.ExpandWidth(true));hudTheme.Draw(rect,hudTheme.Inset);
            bool active=World && World.LabDoseRemaining>0;
            FxDoseMode mode=active ? World.LabDoseMode : FxDoseMode.Pour;
            bool refused=active && World.LabDoseId==FxCueId.HandRefused;
            string label=refused ? "No change — check the reason above." : !active ? "MEASURED CULTURES" : mode==FxDoseMode.Clear ? "All doses returned safely" : mode==FxDoseMode.Recipe ? "Known recipe measured" : mode==FxDoseMode.Return ? World.LabDoseCulture+" returned to the siphon" : World.LabDoseCulture+" measured into the hatchery";
            float diagramWidth=Mathf.Min(166,rect.width*.34f),textWidth=rect.width-diagramWidth-24;
            GUI.Label(new Rect(rect.x+14,rect.y+8,textWidth,34),label,smallStyle);
            var mix=FxDoseMix.Read(Model);
            string amount=mix.Bloom+" Bloom / "+mix.Flow+" Flow";
            if(Model.SparkResearchUnlocked)amount+=" / "+mix.Spark+" Spark";
            if(Model.MistResearchUnlocked)amount+=" / "+mix.Mist+" Mist";
            GUI.Label(new Rect(rect.x+14,rect.y+43,textWidth,35),amount+" staged",smallStyle);
            Rect area=new Rect(rect.xMax-diagramWidth-8,rect.y+4,diagramWidth,78);
            Rect vessel=new Rect(area.x+20,area.y+27,area.width-40,44);
            hudTheme.Draw(vessel,hudTheme.Accent,new Color(.48f,.71f,.71f));
            Rect fluid=new Rect(vessel.x+3,vessel.y+3,vessel.width-6,vessel.height-6);
            hudTheme.Draw(fluid,hudTheme.Accent,new Color(.84f,.9f,.85f));
            float fill=fluid.height*Mathf.Clamp01(mix.Total/(float)Mathf.Max(1,Model.Capacity));
            if(fill>0)hudTheme.Draw(new Rect(fluid.x,fluid.yMax-fill,fluid.width,fill),hudTheme.Accent,Color.Lerp(ShopUiTheme.Paper,mix.Tint,.6f));
            if(refused)hudTheme.Draw(new Rect(rect.x,rect.y,4,rect.height),hudTheme.Accent,coral);
            if(!active || refused || Event.current.type!=EventType.Repaint)return;
            int count=FxDosePour.Count(mode,World.LabDoseLow,World.LabDoseReduced);
            GUI.BeginGroup(area);
            for(int i=0;i<count;i++)
            {
                FxDosePour.Sample(i,World.LabDoseSeed,mode,World.LabDoseMixture,World.LabDoseCulture,World.LabDoseLow,World.LabDoseReduced,out float delay,out var origin,out var p);
                float age=World.LabDoseAge-delay;if(age<0 || age>=p.Lifetime)continue;
                float f=age/p.Lifetime;var point=FxDosePour.Point(origin,p,f);
                float size=Mathf.Lerp(p.Size,p.EndSize,Mathf.SmoothStep(0,1,f))*area.width;
                var r=new Rect((point.x+.4f)*area.width-size*.5f,(1.96f-point.y)*98-size*.5f,size,size);
                Color color=p.Color;color.a*=Mathf.Min(1,f/.06f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,f)));
                var old=GUI.color;bool ring=p.Shape==FxParticleShape.Ring;
                Color edge=Color.Lerp(color,ShopUiTheme.Teal,.6f);edge.a=color.a;GUI.color=edge;
                GUI.DrawTexture(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),ring ? hudTheme.FxRing : hudTheme.FxDisc);
                GUI.color=color;GUI.DrawTexture(r,ring ? hudTheme.FxRing : hudTheme.FxDisc);
                if(!ring){GUI.color=new Color(1,1,1,color.a*.85f);GUI.DrawTexture(new Rect(r.x+r.width*.18f,r.y+r.height*.13f,r.width*.25f,r.height*.25f),hudTheme.FxDisc);}
                GUI.color=old;
            }
            GUI.EndGroup();
        }
    }
}
