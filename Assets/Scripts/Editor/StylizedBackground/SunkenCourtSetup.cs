using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Checkmate.Background.Editor
{
    public static class SunkenCourtSetup
    {
        public const string Root = "Assets/Environment/SunkenCourt/";
        public const string ScenePath = Root + "SunkenCourt_Blockout.unity";
        [MenuItem("Tools/Checkmate/Background/Create Sunken Court Blockout")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists; preserve artist edits by opening it directly.");
            Directory.CreateDirectory(Root+"Materials");
            AssetDatabase.Refresh();
            var stone = Mat("AgedLimestone",new Color(.57f,.56f,.46f));
            var dark = Mat("WetStone",new Color(.26f,.33f,.32f));
            var bronze = Mat("OldBronze",new Color(.28f,.25f,.15f));
            var floor = Mat("SubmergedFloor",new Color(.32f,.43f,.40f));
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var board=new GameObject("01 Board - 8x8 - cell size 2");
                for(int x=0;x<8;x++) for(int z=0;z<8;z++)
                {
                    string name=(x+z)%2==0?"TileMetal":"TileRock";
                    var tile=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/"+name+".prefab"),scene);
                    tile.name="Tile_"+x+"_"+z+"_"+name;
                    tile.transform.SetParent(board.transform,true);
                    var r=tile.GetComponentInChildren<Renderer>();
                    tile.transform.position+=new Vector3(1+x*2,.35f,1+z*2)-r.bounds.center;
                }
                var platform=new GameObject("02 Raised Platform and Stairs");
                Box(platform,"Foundation",new Vector3(8,-.8f,8),new Vector3(18,1.6f,18),dark);
                for(int side=0;side<2;side++) for(int i=0;i<9;i++)
                {
                    Box(platform,"Side Coping",new Vector3(side==0?-1:17,.25f,i*2),new Vector3(.7f,.5f,1.96f),stone);
                    if(side==0&&(i<3||i>5)) Box(platform,"Front Coping",new Vector3(i*2,.25f,-1),new Vector3(1.96f,.5f,.7f),stone);
                    if(side==0) Box(platform,"Rear Coping",new Vector3(i*2,.25f,17),new Vector3(1.96f,.5f,.7f),stone);
                }
                for(int n=0;n<8;n++)
                    Box(platform,"Entry Step "+n,new Vector3(8,-.08f-n*.23f,-1.4f-n*.7f),new Vector3(5,.35f,.74f),stone);
                var props=new GameObject("03 Corner Pylons - outside play area");
                foreach(float x in new[]{-1.6f,17.6f}) foreach(float z in new[]{-1.6f,17.6f})
                {
                    Column(props,new Vector3(x,-1.4f,z),3.7f,1.15f,stone,bronze);
                    Ring(props,"Pylon Ring",new Vector3(x,3.25f,z),.75f,.11f,bronze,24);
                }
                var skyline=new GameObject("04 Background Silhouettes - replace with final art");
                for(int n=0;n<7;n++)
                {
                    float x=-15+n*7.5f, z=26+(n%2)*8, h=15+(n%3)*3;
                    Column(skyline,new Vector3(x,-2.2f,z),h,1.6f,stone,bronze);
                }
                // Broken elevated arch: individually replaceable blockout voussoirs.
                for(int n=0;n<30;n++)
                {
                    if(n==7||n==8||n==22) continue;
                    float a=n*180f/29*Mathf.Deg2Rad;
                    var go=Box(skyline,"Broken Arch Segment "+n,new Vector3(8+11*Mathf.Cos(a),4+11*Mathf.Sin(a),28),new Vector3(1.12f,1.5f,1.1f),stone);
                    go.transform.rotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
                }
                var ruins=new GameObject("05 Submerged Rubble and Causeway");
                for(int i=0;i<14;i++)
                {
                    float a=i*2.39996f;
                    var p=new Vector3(8+Mathf.Cos(a)*(13+i%3),-2.1f,8+Mathf.Sin(a)*(14+i%4));
                    Column(ruins,p,1.4f+i%3*.7f,.6f,dark,bronze);
                }
                Box(ruins,"Submerged Approach",new Vector3(8,-2.65f,-12),new Vector3(5,.5f,14),dark);
                Box(ruins,"Lake Bed",new Vector3(8,-4,8),new Vector3(300,.5f,300),floor);
                var waterSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Water/ShallowWater_Lagoon_Reference.mat");
                string waterPath=Root+"Materials/CourtWater.mat";
                var waterMat=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
                if(!waterMat)
                {
                    waterMat=new Material(waterSource){name="CourtWater"};
                    waterMat.SetFloat("_WaveAmplitude",.065f);
                    waterMat.SetFloat("_CausticsIntensity",.25f);
                    waterMat.SetFloat("_Density",.75f);
                    waterMat.SetFloat("_FadeStart",45);
                    waterMat.SetFloat("_FadeEnd",120);
                    AssetDatabase.CreateAsset(waterMat,waterPath);
                }
                var water=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/ShallowWaterPortable.prefab"),scene);
                water.name="06 Turquoise Water";
                water.transform.position=new Vector3(8,-1.65f,8);
                water.transform.localScale=new Vector3(12,1,12);
                water.GetComponent<Renderer>().sharedMaterial=waterMat;
                var camera=new GameObject("Court Camera").AddComponent<Camera>();
                camera.tag="MainCamera";
                camera.transform.position=new Vector3(31,19,-29);
                camera.transform.LookAt(new Vector3(8,3,9));
                camera.fieldOfView=48;
                camera.farClipPlane=200;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.44f,.63f,.72f);
                var cameraData=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing=true;
                cameraData.requiresDepthTexture=true;
                cameraData.requiresColorTexture=true;
                var sun=new GameObject("Sun").AddComponent<Light>();
                sun.type=LightType.Directional;
                sun.transform.rotation=Quaternion.Euler(48,-35,0);
                sun.color=new Color(1,.97f,.88f);
                sun.intensity=1.4f;
                sun.shadows=LightShadows.Soft;
                RenderSettings.sun=sun;
                RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.48f,.64f,.73f);
                RenderSettings.ambientEquatorColor=new Color(.32f,.42f,.45f);
                RenderSettings.ambientGroundColor=new Color(.2f,.25f,.26f);
                RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear;
                RenderSettings.fogColor=camera.backgroundColor;
                RenderSettings.fogStartDistance=38;RenderSettings.fogEndDistance=115;
                var volume=new GameObject("ACES Preview").AddComponent<Volume>();
                volume.isGlobal=true;
                volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Environment/StylizedBackground/StudyToneMapping.asset");
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
        }
        static Material Mat(string name,Color color)
        {
            string path=Root+"Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m)return m;
            m=new Material(Shader.Find("Checkmate/Environment/Stylized Background")){name=name};
            m.SetColor("_BaseColor",color);m.SetFloat("_SpecularStrength",.08f);
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Box(GameObject parent,string name,Vector3 p,Vector3 s,Material m)
            => Shape(parent,name,PrimitiveType.Cube,p,s,m);
        static GameObject Shape(GameObject parent,string name,PrimitiveType type,Vector3 p,Vector3 s,Material m)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent.transform);
            go.transform.position=p;go.transform.localScale=s;go.GetComponent<Renderer>().sharedMaterial=m;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Column(GameObject parent,Vector3 bottom,float height,float radius,Material stone,Material trim)
        {
            Shape(parent,"Column Shaft",PrimitiveType.Cylinder,bottom+Vector3.up*height*.5f,new Vector3(radius*1.6f,height*.5f,radius*1.6f),stone);
            foreach(float y in new[]{0f,height*.15f,height*.8f,height})
                Shape(parent,"Column Collar",PrimitiveType.Cylinder,bottom+Vector3.up*y,new Vector3(radius*2,.16f,radius*2),trim);
            for(int n=0;n<8;n++)
            {
                float a=n*Mathf.PI/4;
                Box(parent,"Column Rib",bottom+new Vector3(Mathf.Cos(a)*radius*.77f,height*.5f,Mathf.Sin(a)*radius*.77f),new Vector3(.15f,height,.15f),trim);
            }
        }
        static void Ring(GameObject parent,string name,Vector3 center,float radius,float width,Material mat,int count)
        {
            for(int n=0;n<count;n++)
            {
                float a=n*2*Mathf.PI/count;
                var go=Box(parent,name,center+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),new Vector3(width,radius*2*Mathf.PI/count*1.05f,width),mat);
                go.transform.rotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
            }
        }
    }
}
