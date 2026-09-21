using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Checkmate.Water.Editor
{
    /// <summary>Creates only dedicated Water assets. Existing open scenes stay loaded and unsaved.</summary>
    public static class ShallowWaterSetup
    {
        const string MatRoot = "Assets/Resources/Materials/Water/";
        const string TexRoot = "Assets/Resources/Textures/Water/";
        const string SceneRoot = "Assets/Scenes/Test/Water/";
        public const string ScenePath = SceneRoot + "ShallowWaterShowcase.unity";
        [Serializable] class SavedRoots { public string scene; public List<string> ids = new List<string>(); public List<bool> enabled = new List<bool>(); }

        [MenuItem("Tools/Checkmate/Water/Create Shallow Water Showcase")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Showcase already exists. Open it rather than overwriting artist edits.");
            Directory.CreateDirectory(MatRoot); Directory.CreateDirectory(TexRoot); Directory.CreateDirectory(SceneRoot);
            AssetDatabase.Refresh();
            var shader = Shader.Find("Checkmate/Water/Shallow Water");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Compile the water shader first.");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("URP Lit shader missing.");
            var normalA = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/WaterTex/WaterNorm.png");
            var normalB = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/WaterTex/WaterDualNorm.png");
            var caustics = MakeCaustics();
            var sandTex = MakeSand();
            var water = new Material(shader) { name = "ShallowWater_Clear_High" };
            water.SetTexture("_NormalA",normalA); water.SetTexture("_NormalB",normalB); water.SetTexture("_NormalC",normalA);
            water.SetTexture("_CausticsTex",caustics);
            AssetDatabase.CreateAsset(water,MatRoot+water.name+".mat");
            Preset(water,"ShallowWater_Pool_Medium",1,0.38f,0.08f,0);
            Preset(water,"ShallowWater_Stream_High",2,0.75f,0.2f,0.12f);
            Preset(water,"ShallowWater_Mobile_Low",0,0.55f,0.1f,0);
            Preset(water,"ShallowWater_Clear_Ultra",3,0.65f,0.16f,0);
            var sand = Material(lit,"Sand",new Color(0.83f,0.77f,0.60f),0.16f);
            sand.SetTexture("_BaseMap",sandTex); sand.SetTextureScale("_BaseMap",new Vector2(5,5));
            var stone = Material(lit,"Warm Limestone",new Color(0.48f,0.47f,0.41f),0.23f);
            var stoneLight = Material(lit,"Light Limestone",new Color(0.69f,0.65f,0.53f),0.2f);
            var tile = Material(lit,"Pool Tile",new Color(0.46f,0.73f,0.73f),0.4f);
            var grout = Material(lit,"Grout",new Color(0.72f,0.74f,0.67f),0.2f);
            var plant = Material(lit,"Aquatic Green",new Color(0.16f,0.29f,0.11f),0.16f);
            var suit = Material(lit,"Character Navy",new Color(0.07f,0.12f,0.21f),0.22f);
            var skin = Material(lit,"Character Skin",new Color(0.89f,0.69f,0.52f),0.25f);
            ApplyToon(suit,new Color(0.07f,0.12f,0.21f)); ApplyToon(skin,new Color(0.89f,0.69f,0.52f));
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Water Daylight Sky" };
            sky.SetColor("_SkyTint",new Color(0.45f,0.55f,0.65f));
            sky.SetColor("_GroundColor",new Color(0.36f,0.38f,0.33f)); sky.SetFloat("_Exposure",1.2f);
            AssetDatabase.CreateAsset(sky,MatRoot+"WaterDaylightSky.mat");
            var previous = SceneManager.GetActiveScene();
            var saved = new SavedRoots { scene = previous.path };
            foreach (var root in previous.GetRootGameObjects())
            {
                saved.ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(root).ToString()); saved.enabled.Add(root.activeSelf); root.SetActive(false);
            }
            SessionState.SetString("Checkmate.Water.PreviousRoots",JsonUtility.ToJson(saved));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.51f,0.64f,0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f,0.45f,0.39f);
            RenderSettings.ambientGroundColor = new Color(0.20f,0.22f,0.17f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.65f,0.74f,0.77f); RenderSettings.fogDensity = 0.009f;
            var cameraGO = new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); cameraGO.tag="MainCamera";
            var camera = cameraGO.GetComponent<Camera>(); camera.allowHDR=true; camera.nearClipPlane=0.08f; camera.farClipPlane=150;
            camera.fieldOfView=48; camera.clearFlags=CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(6,5,-7); camera.transform.LookAt(new Vector3(0,-0.15f,0));
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresDepthOption=CameraOverrideOption.On; data.requiresColorOption=CameraOverrideOption.On;
            data.renderPostProcessing=true; data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var light = new GameObject("Sun",typeof(Light)).GetComponent<Light>();
            light.type=LightType.Directional; light.intensity=2.1f; light.color=new Color(1,0.94f,0.82f);
            light.transform.rotation=Quaternion.Euler(48,-35,0); light.shadows=LightShadows.Soft; RenderSettings.sun=light;
            var floor = Grid("Sandy Bed",90,18,16,false); floor.GetComponent<MeshRenderer>().sharedMaterial=sand;
            var surface = Grid("Shallow Water",128,16,14,true); surface.layer=4;
            surface.AddComponent<Checkmate.Water.ShallowWaterSceneSupport>();
            surface.GetComponent<MeshRenderer>().sharedMaterial=water;
            surface.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var probe = new GameObject("Water Environment Probe",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
            probe.transform.position=new Vector3(0,2,0); probe.size=new Vector3(24,12,24); probe.boxProjection=true;
            probe.mode=ReflectionProbeMode.Realtime; probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing; probe.resolution=128;
            probe.cullingMask=~(1<<4); probe.hdr=true;
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(713);
            for(int n=0;n<45;n++)
            {
                float a=n*Mathf.PI*2/45; float x=Mathf.Cos(a)*7.1f, z=Mathf.Sin(a)*5.9f;
                var rock=Primitive("Shore Stone "+n,PrimitiveType.Sphere,new Vector3(x,FloorHeight(x,z)+0.12f,z),new Vector3(UnityEngine.Random.Range(0.6f,1.3f),0.5f,UnityEngine.Random.Range(0.5f,1.0f)),n%3==0?stoneLight:stone);
                rock.transform.rotation=UnityEngine.Random.rotation;
            }
            for(int n=0;n<24;n++)
            {
                float x=UnityEngine.Random.Range(-5.5f,5.5f),z=UnityEngine.Random.Range(-4.5f,4.5f);
                float r=UnityEngine.Random.Range(0.07f,0.26f);
                Primitive("Submerged Pebble "+n,PrimitiveType.Sphere,new Vector3(x,FloorHeight(x,z)+r*0.3f,z),new Vector3(r*2,r,r*1.5f),n%2==0?stoneLight:stone);
            }
            Primitive("Tile Grout Bed",PrimitiveType.Cube,new Vector3(-2,-0.48f,-1),new Vector3(3.65f,0.10f,2.45f),grout);
            for(int x=0;x<9;x++) for(int z=0;z<6;z++)
                Primitive("Submerged Tile",PrimitiveType.Cube,new Vector3(-3.6f+x*0.4f,-0.416f,-2+z*0.4f),new Vector3(0.385f,0.025f,0.385f),tile);
            for(int n=0;n<8;n++)
            {
                float x=3.0f+n*0.18f,z=2.0f+Mathf.Sin(n)*0.3f;
                Primitive("Water Plant",PrimitiveType.Capsule,new Vector3(x,FloorHeight(x,z)+0.19f,z),new Vector3(0.035f,0.22f,0.05f),plant).transform.rotation=Quaternion.Euler(15,n*37,18);
            }
            UnityEngine.Random.state = randomState;
            var character = new GameObject("Knee Contact Mannequin");
            float fy=FloorHeight(1.6f,-1.8f);
            Part(character,"Left Boot",PrimitiveType.Capsule,new Vector3(1.42f,fy+0.33f,-1.8f),new Vector3(0.21f,0.33f,0.25f),suit);
            Part(character,"Right Boot",PrimitiveType.Capsule,new Vector3(1.78f,fy+0.33f,-1.8f),new Vector3(0.21f,0.33f,0.25f),suit);
            Part(character,"Body",PrimitiveType.Capsule,new Vector3(1.6f,fy+1.05f,-1.8f),new Vector3(0.52f,0.4f,0.3f),suit);
            Part(character,"Head",PrimitiveType.Sphere,new Vector3(1.6f,fy+1.63f,-1.8f),new Vector3(0.38f,0.43f,0.38f),skin);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,ScenePath);
            probe.RenderProbe();
            Selection.activeGameObject=surface;
            Debug.Log("[Water] Showcase created. Existing scene stays loaded with roots temporarily disabled; use Restore Previous Scene after verification.");
        }
        [MenuItem("Tools/Checkmate/Water/Create Portable Water Prefab")]
        public static void CreatePortableWaterPrefab()
        {
            var water = GameObject.Find("Shallow Water");
            if (!water) throw new InvalidOperationException("Open the water showcase scene first.");
            if (!water.GetComponent<Checkmate.Water.ShallowWaterSceneSupport>())
                water.AddComponent<Checkmate.Water.ShallowWaterSceneSupport>();
            const string path = "Assets/Resources/Prefabs/ShallowWaterPortable.prefab";
            Directory.CreateDirectory("Assets/Resources/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(water,path);
            AssetDatabase.SaveAssets();
            Debug.Log("[Water] Portable prefab created. It enables Depth/Opaque Texture on cameras when moved to another scene.");
        }

        static void Part(GameObject parent,string name,PrimitiveType type,Vector3 p,Vector3 scale,Material mat) { Primitive(name,type,p,scale,mat).transform.SetParent(parent.transform,true); }
        public static void ApplyToon(Material material,Color color)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Toon/WuWa.shadergraph");
            if(shader == null) return;
            var texture = new Texture2D(2,2,TextureFormat.RGB24,false);
            texture.SetPixels(new[]{color,color,color,color}); texture.Apply();
            material.shader=shader; material.SetTexture("_BaseMap",ImportTexture(texture,material.name.Replace(" ",""),true));
            material.SetFloat("_SpecIntensity",0.15f); material.SetFloat("_RimIntensity",0.15f);
            EditorUtility.SetDirty(material);
        }
        static Material Material(Shader shader,string name,Color color,float smoothness)
        {
            var m = new Material(shader) { name=name }; m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",smoothness);
            AssetDatabase.CreateAsset(m,MatRoot+name.Replace(" ","")+".mat"); return m;
        }
        static void Preset(Material source,string name,int quality,float density,float normals,float foam)
        {
            var m=new Material(source){name=name}; m.SetFloat("_Quality",quality); m.SetFloat("_Density",density); m.SetFloat("_StrengthA",normals);
            m.SetFloat("_FoamIntensity",foam); m.SetFloat("_ContactMode",foam>0?1:0);
            if(quality==0) { m.SetFloat("_CausticsIntensity",0.9f); m.SetFloat("_CausticsShadows",0); }
            AssetDatabase.CreateAsset(m,MatRoot+name+".mat");
        }
        static GameObject Primitive(string name,PrimitiveType type,Vector3 p,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.position=p; go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static float FloorHeight(float x,float z)
        {
            float edge=Mathf.Max(Mathf.Abs(x)/8,Mathf.Abs(z)/7);
            return -0.72f + Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.48f,1,edge))*1.1f + 0.045f*Mathf.PerlinNoise(x*0.65f+4,z*0.65f+5);
        }
        static GameObject Grid(string name,int segments,float width,float length,bool water)
        {
            var vertices=new Vector3[(segments+1)*(segments+1)]; var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*segments*6]; int t=0;
            for(int z=0;z<=segments;z++) for(int x=0;x<=segments;x++)
            {
                int i=z*(segments+1)+x; float px=(x/(float)segments-0.5f)*width,pz=(z/(float)segments-0.5f)*length;
                vertices[i]=new Vector3(px,water?0:FloorHeight(px,pz),pz); uv[i]=new Vector2(x/(float)segments,z/(float)segments);
                if(x==segments||z==segments)continue;
                triangles[t++]=i; triangles[t++]=i+segments+1; triangles[t++]=i+1;
                triangles[t++]=i+1; triangles[t++]=i+segments+1; triangles[t++]=i+segments+2;
            }
            var mesh=new Mesh{name=name}; mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=triangles; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var bounds=mesh.bounds; bounds.Expand(0.2f);
            if(water) bounds.size=new Vector3(bounds.size.x,0.6f,bounds.size.z);
            mesh.bounds=bounds;
            AssetDatabase.CreateAsset(mesh,SceneRoot+name.Replace(" ","")+".asset");
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.GetComponent<MeshFilter>().sharedMesh=mesh; return go;
        }
        static Texture2D ImportTexture(Texture2D texture,string name,bool srgb)
        {
            string path=TexRoot+name+".png"; File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path); var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=srgb; importer.mipmapEnabled=true; importer.wrapMode=TextureWrapMode.Repeat;
            importer.filterMode=FilterMode.Trilinear; importer.anisoLevel=4; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Texture2D MakeCaustics()
        {
            const int size=512,cells=7; var tex=new Texture2D(size,size,TextureFormat.RGB24,false,true); var pixels=new Color[size*size];
            var rng=new System.Random(817); var points=new Vector2[cells*cells];
            for(int y=0;y<cells;y++) for(int x=0;x<cells;x++) points[y*cells+x]=new Vector2(x+0.15f+(float)rng.NextDouble()*0.7f,y+0.15f+(float)rng.NextDouble()*0.7f);
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                Vector2 p=new Vector2(x/(float)size*cells,y/(float)size*cells); float d1=100,d2=100;
                int cx=Mathf.FloorToInt(p.x),cy=Mathf.FloorToInt(p.y);
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                {
                    int gx=cx+dx,gy=cy+dy,wx=(gx+cells)%cells,wy=(gy+cells)%cells;
                    Vector2 point=points[wy*cells+wx]+new Vector2(gx-wx,gy-wy); float d=Vector2.Distance(p,point);
                    if(d<d1){d2=d1;d1=d;} else if(d<d2)d2=d;
                }
                float ridge=Mathf.Exp(-Mathf.Pow((d2-d1)/0.12f,2)); pixels[y*size+x]=new Color(ridge,ridge,ridge);
            }
            tex.SetPixels(pixels); tex.Apply(); return ImportTexture(tex,"SoftCaustics",false);
        }
        static Texture2D MakeSand()
        {
            const int size=512; var tex=new Texture2D(size,size,TextureFormat.RGB24,false); var colors=new Color[size*size]; var rng=new System.Random(23);
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                float ripple=Mathf.Sin(v*Mathf.PI*32+Mathf.Sin(u*Mathf.PI*4)*1.4f)*0.035f;
                float grain=(float)rng.NextDouble()*0.12f;
                float value=0.82f+ripple+grain; colors[y*size+x]=new Color(value,value*0.94f,value*0.80f);
            }
            tex.SetPixels(colors); tex.Apply(); return ImportTexture(tex,"SandRipples",true);
        }
        [MenuItem("Tools/Checkmate/Water/Restore Previous Scene")]
        public static void Restore()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            string json=SessionState.GetString("Checkmate.Water.PreviousRoots",""); if(string.IsNullOrEmpty(json))return;
            var saved=JsonUtility.FromJson<SavedRoots>(json);
            for(int n=0;n<saved.ids.Count;n++)
                if(GlobalObjectId.TryParse(saved.ids[n],out var id) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is GameObject go)go.SetActive(saved.enabled[n]);
            var previous=SceneManager.GetSceneByPath(saved.scene);
            if(previous.IsValid()&&previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
                // SetActive from editor code does not reliably dirty a scene. Keep restored flags saveable,
                // including when Test Runner has automatically saved the temporarily disabled roots.
                EditorSceneManager.MarkSceneDirty(previous);
            }
            var demo=SceneManager.GetSceneByPath(ScenePath); if(demo.IsValid()&&demo.isLoaded)EditorSceneManager.CloseScene(demo,true);
            SessionState.EraseString("Checkmate.Water.PreviousRoots");
        }
    }
}
