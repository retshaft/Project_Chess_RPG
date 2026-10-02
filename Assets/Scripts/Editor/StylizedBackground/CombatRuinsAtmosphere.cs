using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Checkmate.Background.Editor
{
    public static class CombatRuinsAtmosphere
    {
        const string Folder = "Assets/Environment/CombatRuins/Sky/";
        const string RootName = "Combat Ruins - Distant City";
        static readonly Color Haze = new Color(.64f, .76f, .82f);

        static Scene RequireScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Test/BattleScene.unity")
                throw new InvalidOperationException("Open BattleScene in Edit Mode.");
            return scene;
        }

        [MenuItem("Tools/Checkmate/Background/Apply Ruins Sky and Distance Fog")]
        public static void Apply()
        {
            var scene = RequireScene();
            var shader = Shader.Find("Checkmate/Environment/Ruins Atmosphere Sky");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Sky shader must compile before application.");
            Directory.CreateDirectory("Captures/CombatRuins");
            const string backup = "Captures/CombatRuins/sky-before.unity";
            if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true))
                throw new IOException("Could not preserve scene before atmosphere changes.");
            Capture("sky-before.png", false, false);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "RuinsSky.mat");
            if (!sky) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, Folder + "RuinsSky.mat"); }
            Undo.RecordObject(sky, "Configure ruins sky");
            sky.SetColor("_Zenith", new Color(.15f,.43f,.70f));
            sky.SetColor("_Horizon", Haze);
            sky.SetColor("_CloudLight", new Color(.97f,.96f,.90f));
            sky.SetColor("_CloudShade", new Color(.56f,.67f,.77f));
            sky.SetFloat("_Coverage", .46f);
            var sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(l => l.type == LightType.Directional && l.isActiveAndEnabled);
            if (sun) sky.SetVector("_SunDirection", -sun.transform.forward);
            Undo.RecordObjects(new UnityEngine.Object[] { Unsupported.GetRenderSettings() }, "Apply ruins atmosphere");
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0035f;
            RenderSettings.fogColor = Haze;
            if (sun) RenderSettings.sun = sun;
            BuildCity(scene);
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save atmosphere scene.");
            Debug.Log("Ruins atmosphere applied: seamless procedural HDR sky, Exp2 fog, baked distant city mesh.");
        }

        [MenuItem("Tools/Checkmate/Background/Rebuild Distant Ruins Mesh")]
        public static void RebuildCity()
        {
            var scene = RequireScene();
            BuildCity(scene, true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildCity(Scene scene, bool rebuild = false)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root && !rebuild) return; // Preserve artist edits on subsequent atmosphere adjustment.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "DistantCity.asset");
            if (!mesh || rebuild)
            {
                var builder = new CityMesh();
                var random = new System.Random(90127);
                for (int ring=0; ring<2; ring++)
                {
                    int count = ring == 0 ? 23 : 31;
                    float radius = ring == 0 ? 220f : 365f;
                    for (int j=0;j<count;j++)
                    {
                        float angle = 2f*Mathf.PI*(j+.16f*(float)random.NextDouble())/count + ring*.13f;
                        float r=radius+((float)random.NextDouble()-.5f)*35;
                        var center = new Vector3(8+Mathf.Cos(angle)*r,-32,8+Mathf.Sin(angle)*r);
                        float h=75+(float)random.NextDouble()*85;
                        float w=3.8f+(float)random.NextDouble()*3.5f;
                        builder.Tower(center,h,w,j%3);
                    }
                    // Broken elevated ring causeways echo the reference without closing off the sky.
                    for (int j=0;j<10;j++)
                    {
                        float start=j*36+5;
                        float span=18+(float)random.NextDouble()*12;
                        builder.Arc(new Vector3(8,ring==0?31:59,8),radius,2.1f,3.2f,start,span);
                    }
                }
                var generated=builder.Create();
                if (mesh)
                {
                    Undo.RecordObject(mesh,"Rebuild distant ruins mesh");
                    // Explicitly update native vertex/index buffers as well as serialized data.
                    mesh.Clear();
                    mesh.indexFormat=generated.indexFormat;
                    mesh.vertices=generated.vertices;
                    mesh.triangles=generated.triangles;
                    mesh.normals=generated.normals;
                    mesh.bounds=generated.bounds;
                    mesh.UploadMeshData(false);
                    UnityEngine.Object.DestroyImmediate(generated);
                    EditorUtility.SetDirty(mesh);
                }
                else { mesh=generated; AssetDatabase.CreateAsset(mesh,Folder+"DistantCity.asset"); }
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"DistantStone.mat");
            if (!material)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor",new Color(.37f,.43f,.44f));
                material.SetFloat("_Metallic",.12f);
                material.SetFloat("_Smoothness",.22f);
                AssetDatabase.CreateAsset(material,Folder+"DistantStone.mat");
            }
            if (!root)
            {
                root=new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root,scene);
                Undo.RegisterCreatedObjectUndo(root,"Create distant ruins");
                root.AddComponent<MeshFilter>();
                root.AddComponent<MeshRenderer>();
            }
            root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }

        public static void Capture(string filename, bool highResolution, bool skyOnly)
        {
            RequireScene();
            var camera=Camera.main;
            if (!camera) throw new InvalidOperationException("Main Camera missing.");
            var position=camera.transform.position; var rotation=camera.transform.rotation;
            float fov=camera.fieldOfView, far=camera.farClipPlane;
            int mask=camera.cullingMask;
            bool ortho=camera.orthographic;
            try
            {
                camera.transform.position=skyOnly?new Vector3(8,12,8):new Vector3(38,17,-35);
                camera.transform.LookAt(skyOnly?new Vector3(8,80,220):new Vector3(8,12,18));
                camera.fieldOfView=60; camera.orthographic=false; camera.farClipPlane=1500;
                if (skyOnly) camera.cullingMask=0;
                CombatRuinsJob.Capture(filename,highResolution?3840:1440,highResolution?2160:810);
                var shader=Shader.Find("Checkmate/Environment/Ruins Atmosphere Sky");
                string report="project="+Application.dataPath+"\nscene="+SceneManager.GetActiveScene().path+
                    "\nplay="+EditorApplication.isPlaying+"\nshaderErrors="+ShaderUtil.ShaderHasError(shader)+
                    "\nfog="+RenderSettings.fogMode+" density="+RenderSettings.fogDensity+"\nsky="+AssetDatabase.GetAssetPath(RenderSettings.skybox)+"\n";
                foreach(var message in ShaderUtil.GetShaderMessages(shader)) report+=message.severity+": "+message.message+"\n";
                File.WriteAllText("Captures/CombatRuins/sky-report.txt",report);
            }
            finally { camera.transform.SetPositionAndRotation(position,rotation); camera.fieldOfView=fov; camera.farClipPlane=far; camera.orthographic=ortho; camera.cullingMask=mask; }
        }

        // Mesh is authored once in the editor: no runtime spawning, scripts, colliders or per-tower draws.
        sealed class CityMesh
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int> triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int i=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
            }
            void Cylinder(Vector3 center,float height,float bottom,float top,int segments=12)
            {
                for(int i=0;i<segments;i++)
                {
                    float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;
                    var da=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)); var db=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    Quad(center+db*bottom,center+da*bottom,center+da*top+Vector3.up*height,center+db*top+Vector3.up*height);
                    Quad(center+Vector3.up*height,center+db*top+Vector3.up*height,center+da*top+Vector3.up*height,center+Vector3.up*height);
                }
            }
            public void Tower(Vector3 p,float h,float w,int style)
            {
                Cylinder(p,h,w,w*.73f);
                Cylinder(p,5,w*1.7f,w*1.35f);
                for(int level=1;level<7;level++)
                {
                    float y=h*level/7;
                    Cylinder(p+Vector3.up*y,2.2f,w*1.22f,w*1.22f);
                }
                for(int rib=0;rib<6;rib++)
                {
                    float angle=rib*Mathf.PI/3;
                    Vector3 offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*w;
                    Cylinder(p+offset,h*.95f,w*.19f,w*.10f,5);
                }
                Cylinder(p+Vector3.up*h,3,w*1.5f,w*1.4f);
                if (style==0) Cylinder(p+Vector3.up*(h+3),h*.07f,w*.75f,w*.45f);
                else if (style==1) Cylinder(p+Vector3.up*(h+3),4,w*.95f,w*.95f);
                for(int spire=0;spire<5;spire++)
                {
                    float angle=spire*2*Mathf.PI/5;
                    Vector3 offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*w*1.3f;
                    Cylinder(p+offset+Vector3.up*(h+2),3+(spire%3)*2,w*.17f,w*.12f,5);
                }
            }
            public void Arc(Vector3 center,float radius,float halfWidth,float height,float start,float span)
            {
                int steps=Mathf.CeilToInt(span/1.5f);
                for(int j=0;j<steps;j++)
                {
                    float a=(start+span*j/steps)*Mathf.Deg2Rad,b=(start+span*(j+1)/steps)*Mathf.Deg2Rad;
                    var u=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var v=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    var ai=center+u*(radius-halfWidth);var ao=center+u*(radius+halfWidth);
                    var bi=center+v*(radius-halfWidth);var bo=center+v*(radius+halfWidth);var up=Vector3.up*height;
                    Quad(ao,bo,bi,ai);Quad(ai+up,bi+up,bo+up,ao+up);
                    Quad(bo,ao,ao+up,bo+up);Quad(ai,bi,bi+up,ai+up);
                    if(j%4==0) Cylinder(center+u*radius+up,4,.45f,.28f,5);
                }
            }
            public Mesh Create()
            {
                var mesh=new Mesh { name="Distant Ruins - 54 Towers and Broken Rings",indexFormat=IndexFormat.UInt32 };
                mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
