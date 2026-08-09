#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CheckmateRPG.Testing;
using CheckmateRPG.Progression;
using CheckmateRPG.Core;

namespace CheckmateRPG.Editor
{
    public static class CampaignCombatSandboxEditor
    {
        [MenuItem("CheckmateRPG/Launch Campaign AI Combat Sandbox (M11 캠페인 AI전 샌드박스 실행)", false, 15)]
        [MenuItem("Tools/Launch Campaign AI Combat Sandbox (M11 캠페인 AI전 샌드박스 실행)", false, 15)]
        public static void SetupCampaignSandboxScene()
        {
            string referenceScenePath = "Assets/Scenes/Test/BattleScene.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(referenceScenePath))
            {
                Debug.LogError($"[CampaignCombatSandboxEditor] Reference battle scene '{referenceScenePath}' not found!");
                return;
            }

            // 1. Bake canonical campaign stages first
            StageDesignerStudio.BakeDefaultCampaignStages();

            // 2. Open BattleScene as the architectural base
            var scene = EditorSceneManager.OpenScene(referenceScenePath, OpenSceneMode.Single);

            // 3. Ensure StageManager exists in scene
            if (Object.FindFirstObjectByType<StageManager>() == null)
            {
                GameObject smGo = new GameObject("StageManager");
                smGo.AddComponent<StageManager>();
            }

            // 4. Clean up any previous tester instance
            var oldTester = Object.FindFirstObjectByType<CampaignCombatTester>();
            if (oldTester != null)
            {
                Object.DestroyImmediate(oldTester.gameObject);
            }

            // 5. Create dedicated CampaignCombatTester GameObject
            GameObject testerGo = new GameObject("M11_CampaignCombatTester");
            testerGo.AddComponent<CampaignCombatTester>();

            // 6. Save new sandboxed scene to prevent polluting main BattleScene
            string targetScenePath = "Assets/Scenes/Test/CampaignCombatSandbox.unity";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Test")) AssetDatabase.CreateFolder("Assets/Scenes", "Test");

            EditorSceneManager.SaveScene(scene, targetScenePath);
            Debug.Log($"🚀 [M11] Campaign AI Combat Sandbox Scene generated and ready at: {targetScenePath}. Press Play in Editor to deploy!");
        }
    }
}
#endif
