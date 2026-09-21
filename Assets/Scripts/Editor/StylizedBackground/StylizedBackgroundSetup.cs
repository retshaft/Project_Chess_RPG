using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Checkmate.Background.Editor
{
    /// <summary>Creates new, dedicated assets. Does not replace scene or source materials.</summary>
    public static class StylizedBackgroundSetup
    {
        public const string Root = "Assets/Environment/StylizedBackground/";
        public const string PrefabPath = Root + "BackgroundMaterialStudy.prefab";
        public const string ScenePath = Root + "BackgroundMaterialStudy.unity";

        [MenuItem("Tools/Checkmate/Background/Create Preview Scene")]
        public static void CreatePreviewScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Preview scene already exists; open it to preserve edits.");
            if (!File.Exists(PrefabPath)) Create();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                var camera = new GameObject("Study Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(12,9,-17);
                camera.transform.LookAt(new Vector3(0,1.4f,0));
                camera.fieldOfView = 42;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.42f,.61f,.69f);
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 200;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                var sun = new GameObject("Study Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(48,-38,0);
                sun.color = new Color(1,.96f,.88f);
                sun.intensity = 2.2f;
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.48f,.62f,.7f);
                RenderSettings.ambientEquatorColor = new Color(.34f,.41f,.44f);
                RenderSettings.ambientGroundColor = new Color(.22f,.24f,.22f);
                RenderSettings.fog = false;
                string profilePath = Root + "StudyToneMapping.asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                if (!profile)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile,profilePath);
                    var tone = profile.Add<Tonemapping>(true);
                    tone.mode.Override(TonemappingMode.ACES);
                    AssetDatabase.AddObjectToAsset(tone,profile);
                    EditorUtility.SetDirty(profile);
                }
                var volume = new GameObject("Study Tone Mapping").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("Tools/Checkmate/Background/Create Material Study")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("Material study already exists; open its prefab to preserve edits.");
            var shader = Shader.Find("Checkmate/Environment/Stylized Background");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Compile background shader first.");
            Directory.CreateDirectory(Root + "Materials");
            AssetDatabase.Refresh();
            var concrete = Material("WarmConcrete", new Color(.75f,.72f,.61f), .06f);
            var teal = Material("TealPlaster", new Color(.32f,.64f,.59f), .08f);
            var red = Material("VermilionPaint", new Color(.72f,.24f,.19f), .18f);
            var metal = Material("BlueGreyMetal", new Color(.27f,.36f,.42f), .30f);
            var yellow = Material("OchreTrim", new Color(.91f,.69f,.22f), .15f);
            var green = Material("Foliage", new Color(.36f,.53f,.20f), .03f);
            var floor = Material("MechanicalFloor", Color.white, .12f);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/MechanicalRuins/Textures/MechanicalFloor.png");
            if (texture) floor.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(floor);
            var root = new GameObject("Background Material Study");
            try
            {
                Box(root,"Foundation",new Vector3(0,-.25f,0),new Vector3(14,.5f,11),metal);
                for (int x=0;x<7;x++) for(int z=0;z<5;z++)
                    Box(root,"Courtyard Tile",new Vector3(-6+x*2,.025f,-4+z*2),new Vector3(1.96f,.05f,1.96f),concrete);
                for(int x=0;x<3;x++)
                    Box(root,"Textured Mechanical Floor",new Vector3(-2+x*2,.065f,-1),new Vector3(1.95f,.04f,1.95f),floor);
                Box(root,"Teal Wall Left",new Vector3(-3,2.4f,3.8f),new Vector3(4,4.8f,.6f),teal);
                Box(root,"Teal Wall Right",new Vector3(3,2.4f,3.8f),new Vector3(4,4.8f,.6f),teal);
                Box(root,"Arch Lintel",new Vector3(0,4.15f,3.8f),new Vector3(2,1.3f,.6f),teal);
                Box(root,"Roof Coping",new Vector3(0,4.85f,3.8f),new Vector3(10.5f,.25f,1.1f),concrete);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(root,"Painted Column",new Vector3(side*1.25f,1.65f,2.7f),new Vector3(.45f,3.3f,.45f),red);
                    Box(root,"Column Base",new Vector3(side*1.25f,.3f,2.7f),new Vector3(.65f,.6f,.65f),concrete);
                    Box(root,"Window Frame",new Vector3(side*3.2f,2.6f,3.43f),new Vector3(1.8f,1.65f,.2f),concrete);
                    Box(root,"Window Recess",new Vector3(side*3.2f,2.6f,3.28f),new Vector3(1.55f,1.4f,.12f),metal);
                    Box(root,"Window Mullion",new Vector3(side*3.2f,2.6f,3.15f),new Vector3(.08f,1.4f,.08f),yellow);
                    Box(root,"Planter",new Vector3(side*5,.35f,1),new Vector3(1.8f,.7f,1.3f),concrete);
                    for(int n=0;n<5;n++)
                    {
                        var shrub=Shape(root,"Foliage Volume",PrimitiveType.Sphere,new Vector3(side*5+(n%3-1)*.43f,.95f,1+(n/3-.5f)*.5f),new Vector3(.95f,.9f,.9f),green);
                        shrub.transform.localRotation=Quaternion.Euler(0,n*31,0);
                    }
                }
                Box(root,"Gate Beam",new Vector3(0,3.35f,2.7f),new Vector3(3.25f,.35f,.65f),red);
                Box(root,"Gate Gold Inlay",new Vector3(0,3.36f,2.36f),new Vector3(2.85f,.065f,.03f),yellow);
                for(int n=0;n<3;n++)
                    Box(root,"Entry Step",new Vector3(0,.10f+n*.10f,1.5f+n*.35f),new Vector3(2.1f,.2f,.36f),concrete);
                var mats=new[]{concrete,teal,red,metal,yellow,green};
                for(int n=0;n<mats.Length;n++)
                {
                    float x=-5+n*2;
                    Box(root,"Swatch Plinth "+mats[n].name,new Vector3(x,.2f,-3.3f),new Vector3(1.45f,.4f,1.45f),metal);
                    Shape(root,"Swatch "+mats[n].name,PrimitiveType.Sphere,new Vector3(x,.95f,-3.3f),Vector3.one*1.2f,mats[n]);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Material Material(string name,Color color,float specular)
        {
            string path=Root+"Materials/"+name+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing) return existing;
            var material=new Material(Shader.Find("Checkmate/Environment/Stylized Background")) { name=name, enableInstancing=true };
            material.SetColor("_BaseColor",color);
            material.SetFloat("_SpecularStrength",specular);
            AssetDatabase.CreateAsset(material,path);
            return material;
        }
        static GameObject Box(GameObject parent,string name,Vector3 p,Vector3 scale,Material mat)
            => Shape(parent,name,PrimitiveType.Cube,p,scale,mat);
        static GameObject Shape(GameObject parent,string name,PrimitiveType type,Vector3 p,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(type);
            go.name=name;
            go.transform.SetParent(parent.transform,false);
            go.transform.localPosition=p;
            go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
