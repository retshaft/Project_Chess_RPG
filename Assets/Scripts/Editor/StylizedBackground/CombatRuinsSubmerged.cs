using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Checkmate.Background.Editor
{
    // Editor-only assembly of linked modules. Module origins are walking-surface centers.
    public static class CombatRuinsSubmerged
    {
        const string Folder = "Assets/Prefabs/RuinsBackgrounds/Modules";
        const string RootName = "Combat Ruins - Submerged District";

        static GameObject Module(string name, Action<Transform> build)
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var root = new GameObject(name);
            try { build(root.transform); return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Put(Transform parent, GameObject module, float x, float y, float z, float yaw = 0)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(module, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        [MenuItem("Tools/Checkmate/Background/Add Submerged Ruins Modules")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Test/BattleScene.unity")
                throw new InvalidOperationException("Open BattleScene in Edit Mode.");
            if (scene.GetRootGameObjects().Any(g => g.name == RootName))
                throw new InvalidOperationException("Submerged district already exists; edit its module instances.");
            Directory.CreateDirectory("Captures/CombatRuins");
            string backup = "Captures/CombatRuins/submerged-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Scene backup failed.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/RuinsBackgrounds", "Modules");
            var terrace = Module("Ruins_Terrace_8m", t => CombatRuinsConnections.Plaza(t, 0, 0, 0));
            var gallery = Module("Ruins_Gallery_12m", t => CombatRuinsConnections.Walk(t, new Vector3(0,0,-6), new Vector3(0,0,6)));
            // High end z=-3,y=0; low end z=3,y=-3. Walking-surface anchors.
            var stairs = Module("Ruins_DescendingStairs_6m", t => CombatRuinsConnections.Steps(t, new Vector3(0,0,-3), new Vector3(0,-3,3)));
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Add submerged ruin district");
            try
            {
                // Extend the existing southwest landing (surface y=-7.2, south edge z=-37.22).
                Put(root.transform, stairs, -32,-7.2f,-40.22f,180);
                Put(root.transform, terrace,-32,-10.2f,-47.62f);
                Put(root.transform, gallery,-21.6f,-10.2f,-47.62f,90);
                Put(root.transform, terrace,-11.2f,-10.2f,-47.62f);
                Put(root.transform, stairs,-3.8f,-10.2f,-47.62f,90);
                Put(root.transform, terrace,3.6f,-13.2f,-47.62f);
                Put(root.transform, gallery,14,-13.2f,-47.62f,90);
                Put(root.transform, terrace,24.4f,-13.2f,-47.62f);
                Put(root.transform, gallery,24.4f,-13.2f,-37.22f);
                Put(root.transform, terrace,24.4f,-13.2f,-26.82f);
                Put(root.transform, stairs,31.8f,-13.2f,-26.82f,90);
                Put(root.transform, terrace,39.2f,-16.2f,-26.82f);
                // Lower western cloister, partly drowned parapets and deep paving.
                Put(root.transform, gallery,-32,-10.2f,-58.02f);
                Put(root.transform, terrace,-32,-10.2f,-68.42f);
                Put(root.transform, stairs,-39.4f,-10.2f,-47.62f,270);
                Put(root.transform, terrace,-46.8f,-13.2f,-47.62f);
                Put(root.transform, gallery,-46.8f,-13.2f,-37.22f);
                Put(root.transform, terrace,-46.8f,-13.2f,-26.82f);
                Put(root.transform, gallery,-46.8f,-13.2f,-16.42f);
                Put(root.transform, terrace,-46.8f,-13.2f,-6.02f);
                Put(root.transform, stairs,-46.8f,-13.2f,1.38f);
                Put(root.transform, terrace,-46.8f,-16.2f,8.78f);
                // Sunken eastern quarter beneath and outside the existing elevated gallery.
                Put(root.transform, gallery,49.6f,-16.2f,-26.82f,90);
                Put(root.transform, terrace,60,-16.2f,-26.82f);
                Put(root.transform, gallery,60,-16.2f,-16.42f);
                Put(root.transform, terrace,60,-16.2f,-6.02f);
                Put(root.transform, stairs,60,-13.2f,1.38f,180);
                Put(root.transform, terrace,60,-13.2f,8.78f);
                Put(root.transform, gallery,60,-13.2f,19.18f);
                Put(root.transform, terrace,60,-13.2f,29.58f);
                Put(root.transform, gallery,60,-13.2f,39.98f);
                Put(root.transform, terrace,60,-13.2f,50.38f);
                Put(root.transform, stairs,60,-10.2f,57.78f,180);
                Put(root.transform, terrace,60,-10.2f,65.18f);
                Put(root.transform, gallery,49.6f,-10.2f,65.18f,90);
                Put(root.transform, terrace,39.2f,-10.2f,65.18f);
                Put(root.transform, stairs,31.8f,-10.2f,65.18f,270);
                Put(root.transform, terrace,24.4f,-13.2f,65.18f);
                Put(root.transform, gallery,14,-13.2f,65.18f,90);
                Put(root.transform, terrace,3.6f,-13.2f,65.18f);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
                File.WriteAllText("Captures/CombatRuins/submerged-layout.txt", "Backup="+backup+"\nModules="+root.transform.childCount+"\nColliders="+root.GetComponentsInChildren<Collider>().Length+"\nRuntime scripts="+root.GetComponentsInChildren<MonoBehaviour>().Length);
            }
            catch { UnityEngine.Object.DestroyImmediate(root); throw; }
        }
    }
}
