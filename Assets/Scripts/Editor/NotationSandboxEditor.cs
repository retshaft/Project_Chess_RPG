#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CheckmateRPG.Testing;
using CheckmateRPG.Progression;

namespace CheckmateRPG.Editor
{
    public static class NotationSandboxEditor
    {
        [MenuItem("CheckmateRPG/Launch Notation Puzzle Sandbox (M12 기보 스킬트리 셋업)", false, 16)]
        [MenuItem("Tools/Launch Notation Puzzle Sandbox (M12 기보 스킬트리 셋업)", false, 16)]
        public static void SetupNotationSandboxScene()
        {
            // 1. Ensure canonical notation trees are baked
            NotationDesignerStudio.BakeDefaultNotationTrees();

            // 2. Create a clean empty scene for the 3D chessboard and notation puzzle UI
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Ensure SaveManager exists in scene
            if (Object.FindFirstObjectByType<SaveManager>() == null)
            {
                GameObject smGo = new GameObject("SaveManager");
                smGo.AddComponent<SaveManager>();
            }

            // 4. Create dedicated NotationTester and screen GameObject
            GameObject testerGo = new GameObject("M12_NotationPuzzleSandbox");
            testerGo.AddComponent<NotationPuzzleScreen>();
            testerGo.AddComponent<NotationTester>();

            // 5. Save scene
            string targetScenePath = "Assets/Scenes/Test/NotationPuzzleSandbox.unity";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Test")) AssetDatabase.CreateFolder("Assets/Scenes", "Test");

            EditorSceneManager.SaveScene(scene, targetScenePath);
            Debug.Log($"🚀 [M12] Notation Puzzle Skill Tree Sandbox Scene generated and ready at: {targetScenePath}. Press Play in Editor to deploy!");
        }
    }
}
#endif
