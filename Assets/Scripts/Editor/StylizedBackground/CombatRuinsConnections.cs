using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Checkmate.Background.Editor
{
    public static class CombatRuinsConnections
    {
        const string Prefabs = "Assets/Prefabs/RuinsBackgrounds/";
        const string Captures = "Captures/CombatRuins/";
        public static readonly string[] Names = { "Path - Textured", "Path - Textured 1", "Platform", "Stairs - Textured", "LowWall - Textured", "LowWallOneMinus", "LowWallTwoMinus", "SmallPillar - Textured", "Pillar" };
        static Scene RequireScene()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/Scenes/Test/BattleScene.unity")
                throw new InvalidOperationException("Open BattleScene in Edit Mode.");
            return scene;
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) throw new InvalidOperationException("No renderer: "+go.name);
            var bounds=renderers[0].bounds;
            foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }
        public static void Inspect()
        {
            var scene=RequireScene();
            var report=new StringBuilder("Project="+Application.dataPath+"\nScene="+scene.path+"\nDirty="+scene.isDirty+"\n");
            foreach(var name in Names)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+name+".prefab");
                if(!prefab) throw new InvalidOperationException("Missing prefab "+name);
                var go=UnityEngine.Object.Instantiate(prefab);
                go.hideFlags=HideFlags.HideAndDontSave;
                try
                {
                    go.transform.position=Vector3.zero;
                    var b=BoundsOf(go);
                    report.AppendLine("PREFAB "+name+" rotation="+go.transform.eulerAngles+" scale="+go.transform.localScale+" bounds="+b.ToString("F3")+" min="+b.min.ToString("F3")+" max="+b.max.ToString("F3"));
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.bounds.center.magnitude>150 || r.name.StartsWith("Tile")) continue;
                report.AppendLine("SCENE "+r.name+" pos="+r.transform.position.ToString("F3")+" rot="+r.transform.eulerAngles+" scale="+r.transform.lossyScale+" min="+r.bounds.min.ToString("F3")+" max="+r.bounds.max.ToString("F3"));
            }
            File.WriteAllText(Captures+"connections-inspect.txt",report.ToString());
            Capture("connections-before.png",false);
            Capture("connections-top-before.png",true);
        }
        public static void Capture(string name,bool top)
        {
            RequireScene();
            var c=Camera.main; var p=c.transform.position;var q=c.transform.rotation;
            float fov=c.fieldOfView,size=c.orthographicSize;bool ortho=c.orthographic;
            try
            {
                c.transform.position=top?new Vector3(8,18,20):new Vector3(58,42,-69);
                c.transform.rotation=top?Quaternion.Euler(90,0,0):Quaternion.LookRotation(new Vector3(8,-1,16)-c.transform.position);
                c.orthographic=top;c.orthographicSize=57;c.fieldOfView=60;
                CombatRuinsJob.Capture(name,1920,1080);
            }
            finally{c.transform.SetPositionAndRotation(p,q);c.fieldOfView=fov;c.orthographic=ortho;c.orthographicSize=size;}
        }

        const string RootName="Combat Ruins - Connected Terraces";
        public static void FinishJoints()
        {
            var scene=RequireScene();
            var root=scene.GetRootGameObjects().Single(g=>g.name==RootName);
            bool previouslyFinished=File.Exists(Captures+"connections-joints.txt");
            int count=0;
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Connected paving ")))
            {
                var b=BoundsOf(t.gameObject);var s=t.localScale;
                bool alongX=Mathf.Abs(t.GetChild(0).TransformDirection(Vector3.up).x)>.5f;
                float width=alongX?b.size.z:b.size.x;
                bool wrongAxis=previouslyFinished && Mathf.Abs(width-4.155f)<.015f;
                if(previouslyFinished && !wrongAxis)continue;
                Undo.RecordObject(t,"Close paving joints");
                if(wrongAxis){if(alongX)s.z*=3.98f/b.size.z;else s.x*=3.98f/b.size.x;}
                if(alongX)s.x*=(b.size.x+.175f)/b.size.x;
                else s.z*=(b.size.z+.175f)/b.size.z;
                t.localScale=s;count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.AppendAllText(Captures+"connections-joints.txt","Paving joints tightened="+count+"\nColliders="+root.GetComponentsInChildren<Collider>().Length+"\nRuntime scripts="+root.GetComponentsInChildren<MonoBehaviour>().Length+"\n");
        }
        static Transform Group(Transform parent,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        // An axis-aligned wrapper fits world bounds while retaining the source prefab connection.
        static GameObject Place(Transform parent,string prefabName,string label,Vector3 center,float yaw,Vector3 dimensions,bool top)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+prefabName+".prefab");
            if(!prefab)throw new InvalidOperationException("Missing prefab: "+prefabName);
            var wrapper=Group(parent,label);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.gameObject.scene);
            go.transform.SetParent(wrapper,false);
            go.transform.localPosition=Vector3.zero;
            go.transform.localRotation=Quaternion.Euler(0,yaw,0)*prefab.transform.localRotation;
            var b=BoundsOf(go);
            go.transform.position-=b.center;
            wrapper.localScale=new Vector3(dimensions.x>0?dimensions.x/b.size.x:1,dimensions.y>0?dimensions.y/b.size.y:1,dimensions.z>0?dimensions.z/b.size.z:1);
            b=BoundsOf(go);
            wrapper.position=center-Vector3.up*(top?b.extents.y:-b.extents.y);
            return go;
        }
        static void Support(Transform p,float x,float z,float floor,float diameter=3.3f)
        {
            Place(p,"Pillar","Submerged structural pillar",new Vector3(x,floor-.68f,z),0,new Vector3(diameter,22,diameter),true);
        }
        static void Wall(Transform p,Vector3 a,Vector3 b,int variant)
        {
            bool alongX=Mathf.Abs(b.x-a.x)>Mathf.Abs(b.z-a.z);
            float length=Vector3.Distance(a,b);
            string name=variant==0?"LowWall - Textured":variant==1?"LowWallOneMinus":"LowWallTwoMinus";
            float yaw=alongX?(variant==0?90:0):(variant==0?0:90);
            Place(p,name,"Parapet - "+(2-variant)+" posts",(a+b)/2,yaw,alongX?new Vector3(length,0,0):new Vector3(0,0,length),false);
        }
        internal static void Walk(Transform p,Vector3 a,Vector3 b,float width=3.98f,bool rails=true)
        {
            bool alongX=Mathf.Abs(b.x-a.x)>Mathf.Abs(b.z-a.z);
            float length=Vector3.Distance(a,b);int count=Mathf.CeilToInt(length/5.8f);
            Vector3 side=alongX?Vector3.forward:Vector3.right;
            for(int i=0;i<count;i++)
            {
                var s=Vector3.Lerp(a,b,(float)i/count);var e=Vector3.Lerp(a,b,(float)(i+1)/count);
                var c=(s+e)/2;
                Place(p,"Path - Textured 1","Connected paving "+(i+1),c,alongX?90:0,alongX?new Vector3(length/count+.20f,0,width):new Vector3(width,0,length/count+.20f),true);
                if(i%2==0)Support(p,c.x,c.z,c.y-1.05f,2.9f);
                if(rails)
                    for(int edge=-1;edge<=1;edge+=2)
                    {
                        // Missing parapet sections expose water and break the repeated rhythm.
                        if((i+edge+count)%5==0)continue;
                        Vector3 offset=side*(width/2-.2f)*edge;
                        var inset=(e-s).normalized*.24f;
                        Wall(p,s+offset+inset,e+offset-inset,(i+(edge==1?1:0))%3);
                    }
            }
        }
        internal static void Plaza(Transform p,float x,float z,float height,float size=8.8f)
        {
            for(int ix=-1;ix<=1;ix+=2)for(int iz=-1;iz<=1;iz+=2)
            {
                float px=x+ix*size/4,pz=z+iz*size/4;
                Place(p,"Platform","Terrace slab",new Vector3(px,height,pz),0,new Vector3(size/2+.02f,0,size/2+.02f),true);
                Support(p,px,pz,height);
                Place(p,"SmallPillar - Textured","Terrace corner finial",new Vector3(x+ix*(size/2-.42f),height,z+iz*(size/2-.42f)),0,Vector3.zero,false);
            }
        }
        internal static void Steps(Transform p,Vector3 high,Vector3 low)
        {
            bool x=Mathf.Abs(high.x-low.x)>Mathf.Abs(high.z-low.z);
            float yaw=x?(low.x>high.x?90:270):(low.z>high.z?0:180);
            float length=x?Mathf.Abs(high.x-low.x):Mathf.Abs(high.z-low.z);
            Place(p,"Stairs - Textured","Descending flight",new Vector3((high.x+low.x)/2,low.y,(high.z+low.z)/2),yaw,x?new Vector3(length,high.y-low.y,3.98f):new Vector3(3.98f,high.y-low.y,length),false);
        }
        [MenuItem("Tools/Checkmate/Background/Add Connected Ruins")]
        public static void Apply()
        {
            var scene=RequireScene();
            if(scene.GetRootGameObjects().Any(g=>g.name==RootName))throw new InvalidOperationException("Connected terraces already exist. Edit the group rather than duplicating it.");
            Directory.CreateDirectory(Captures);
            string backup=Captures+"connections-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Cannot preserve live scene.");
            var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,scene);
            Undo.RegisterCreatedObjectUndo(root,"Add connected ruin terraces");
            try
            {
                var north=Group(root.transform,"01 - Northern cloister");
                // Existing open ends: west arm z=14.696 and north arm x=1.267.
                Walk(north,new Vector3(-12.572f,-1.125f,14.67f),new Vector3(-12.572f,-1.125f,24.14f));
                Plaza(north,-12.572f,28.535f,-1.125f);
                Walk(north,new Vector3(-8.172f,-1.125f,28.535f),new Vector3(1.29f,-1.125f,28.535f));
                Steps(north,new Vector3(-12.572f,-1.125f,32.93f),new Vector3(-12.572f,-2.828f,37.48f));
                Walk(north,new Vector3(-12.572f,-2.828f,37.46f),new Vector3(-12.572f,-2.828f,43.60f));
                Plaza(north,-12.572f,48,-2.828f);
                Walk(north,new Vector3(-8.172f,-2.828f,48),new Vector3(24.20f,-2.828f,48));
                Plaza(north,28.6f,48,-2.828f);
                Walk(north,new Vector3(28.6f,-2.828f,34.40f),new Vector3(28.6f,-2.828f,43.60f));
                var east=Group(root.transform,"02 - Eastern water gallery");
                Walk(east,new Vector3(34.51f,-2.86f,-12.6f),new Vector3(42.2f,-2.86f,-12.6f),2.89f);
                Plaza(east,46.6f,-12.6f,-2.86f);
                Walk(east,new Vector3(46.6f,-2.86f,-8.2f),new Vector3(46.6f,-2.86f,16.1f));
                Plaza(east,46.6f,20.5f,-2.86f);
                Walk(east,new Vector3(46.6f,-2.86f,24.9f),new Vector3(46.6f,-2.86f,48),3.98f,false);
                Walk(east,new Vector3(46.6f,-2.86f,48),new Vector3(33,-2.828f,48),3.98f,false);
                var south=Group(root.transform,"03 - Sunken southern terraces");
                Walk(south,new Vector3(-19.11f,-5.4f,-19.48f),new Vector3(-27.6f,-5.4f,-19.48f));
                Plaza(south,-32,-19.48f,-5.4f);
                Walk(south,new Vector3(-6.05f,-5.4f,-19.48f),new Vector3(2.6f,-5.4f,-19.48f));
                Plaza(south,7,-19.48f,-5.4f);
                Steps(south,new Vector3(-32,-5.4f,-23.88f),new Vector3(-32,-7.2f,-28.42f));
                Plaza(south,-32,-32.82f,-7.2f);
                // A few emergent columns frame the outer gallery without screening the chessboard.
                var towers=Group(root.transform,"04 - Outer colonnade");
                foreach(var v in new[]{new Vector3(-20,9,48),new Vector3(-4,5,54),new Vector3(15,12,54),new Vector3(36,7,54),new Vector3(54,6,28),new Vector3(54,3,-18)})
                    Place(towers,"Pillar","Weathered outer column",v,0,new Vector3(4.6f,26,4.6f),true);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.WriteAllText(Captures+"connections-layout.txt","Backup="+backup+"\nRoot="+RootName+"\nPrefab instances="+root.GetComponentsInChildren<Transform>().Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))+"\n");
            }
            catch { UnityEngine.Object.DestroyImmediate(root);throw; }
        }
    }
}
