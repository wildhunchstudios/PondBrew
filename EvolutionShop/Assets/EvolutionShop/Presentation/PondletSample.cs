using UnityEngine;

namespace EvolutionShop
{
    public sealed partial class CreatureVisual
    {
        Material[] pondletMaterials;
        Mesh pondletFinMesh;

        void BuildPondlet()
        {
            var coral=ShopWorld.MakeMaterial("Pondlet / coral clay",new Color(.92f,.32f,.23f),0,.32f);
            var cream=ShopWorld.MakeMaterial("Pondlet / custard belly",new Color(1,.87f,.58f),0,.35f);
            var fin=ShopWorld.MakeMaterial("Pondlet / petal tips",new Color(1,.62f,.37f),0,.28f);
            var ink=ShopWorld.MakeMaterial("Pondlet / ink",new Color(.045f,.14f,.17f),0,.5f);
            var white=ShopWorld.MakeMaterial("Pondlet / eye ivory",new Color(1,.97f,.85f),0,.5f);
            pondletMaterials=new[]{coral,cream,fin,ink,white};
            ShopWorld.Shape(transform,"Pondlet round teardrop",PrimitiveType.Sphere,Vector3.zero,new Vector3(.50f,.43f,.72f),coral);
            ShopWorld.Shape(transform,"Custard chin",PrimitiveType.Sphere,new Vector3(0,-.084f,-.055f),new Vector3(.43f,.30f,.60f),cream);
            tail=new GameObject("Petal tail pivot").transform; tail.SetParent(transform,false); tail.localPosition=new Vector3(0,0,.25f);
            for(int side=-1;side<=1;side+=2)
            {
                ShopWorld.Shape(tail,"Heart tail lobe",PrimitiveType.Sphere,new Vector3(0,side*.094f,.20f),new Vector3(.075f,.245f,.32f),fin).localRotation=Quaternion.Euler(-side*32,0,0);
                ShopWorld.Shape(transform,"Raised eye",PrimitiveType.Sphere,new Vector3(side*.167f,.055f,-.255f),new Vector3(.151f,.164f,.115f),white);
                ShopWorld.Shape(transform,"Curious pupil",PrimitiveType.Sphere,new Vector3(side*.167f,.05f,-.309f),new Vector3(.073f,.090f,.047f),ink);
                ShopWorld.Shape(transform,"Eye glint",PrimitiveType.Sphere,new Vector3(side*.167f-.018f,.074f,-.332f),Vector3.one*.025f,white);
                for(int i=0;i<3;i++) ShopWorld.Shape(transform,"Golden cheek freckle",PrimitiveType.Sphere,new Vector3(side*(.211f-i*.012f),-.015f-i*.035f,-.181f),Vector3.one*.031f,fin);
            }
            ShopWorld.Shape(transform,"Pursed little mouth",PrimitiveType.Sphere,new Vector3(0,-.054f,-.350f),new Vector3(.068f,.040f,.026f),ink);
            // A small solid leaf mesh gives the sample fins a deliberate tapered silhouette.
            var outline=new[]{new Vector2(0,0),new Vector2(.18f,.05f),new Vector2(.25f,.18f),new Vector2(.15f,.28f),new Vector2(.015f,.17f)};
            var v=new Vector3[10]; var triangles=new int[48]; int n=0;
            for(int i=0;i<5;i++){v[i]=new Vector3(outline[i].x,-.015f,outline[i].y);v[i+5]=new Vector3(outline[i].x,.015f,outline[i].y);}
            for(int i=1;i<4;i++){triangles[n++]=0;triangles[n++]=i;triangles[n++]=i+1;triangles[n++]=5;triangles[n++]=i+6;triangles[n++]=i+5;}
            for(int i=0;i<5;i++){int j=(i+1)%5;triangles[n++]=i;triangles[n++]=i+5;triangles[n++]=j;triangles[n++]=j;triangles[n++]=i+5;triangles[n++]=j+5;}
            pondletFinMesh=new Mesh{name="Original Pondlet tapered petal",vertices=v,triangles=triangles};pondletFinMesh.RecalculateNormals();pondletFinMesh.RecalculateBounds();
            finRight=Petal("Right petal",new Vector3(.18f,-.06f,-.04f),fin);
            finLeft=Petal("Left petal",new Vector3(-.18f,-.06f,.15f),fin);finLeft.localRotation=Quaternion.Euler(0,180,0);
            crest=ShopWorld.Shape(transform,"Crown petal",PrimitiveType.Sphere,new Vector3(0,.202f,.07f),new Vector3(.065f,.16f,.29f),fin);
            CapturePondletRest();
            Animate(0);
        }
        Transform Petal(string label,Vector3 position,Material material)
        {
            var t=new GameObject(label).transform;t.SetParent(transform,false);t.localPosition=position;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=pondletFinMesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;
        }
    }
}
