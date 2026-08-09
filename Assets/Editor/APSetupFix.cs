using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CheckmateRPG.Data;
using CheckmateRPG.Core;
using System.IO;

public static class APSetupFix
{
    [MenuItem("Tools/Apply Proposal 3 AP Values")]
    public static void Run()
    {
        // 1. Update UnitData assets
        string[] guids = AssetDatabase.FindAssets("t:UnitData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (data != null)
            {
                switch (data.PieceType)
                {
                    case ChessPieceType.Pawn:
                        data.MoveCostAP = 8f;
                        data.AttackCostAP = 8f;
                        break;
                    case ChessPieceType.Knight:
                        data.MoveCostAP = 24f;
                        data.AttackCostAP = 24f;
                        break;
                    case ChessPieceType.Bishop:
                        data.MoveCostAP = 24f;
                        data.AttackCostAP = 24f;
                        break;
                    case ChessPieceType.Rook:
                        data.MoveCostAP = 36f;
                        data.AttackCostAP = 36f;
                        break;
                    case ChessPieceType.Queen:
                        data.MoveCostAP = 48f;
                        data.AttackCostAP = 48f;
                        break;
                    case ChessPieceType.King:
                        data.MoveCostAP = 24f;
                        data.AttackCostAP = 24f;
                        break;
                }
                EditorUtility.SetDirty(data);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[APSetupFix] Updated UnitData AP costs based on Proposal 3.");

        // 2. Add APManager to BattleTest scene
        string scenePath = "Assets/Scenes/Test/BattleTest.unity";
        if (File.Exists(scenePath))
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            APManager existing = Object.FindFirstObjectByType<APManager>();
            if (existing == null)
            {
                GameObject apGo = new GameObject("GlobalAPManager");
                existing = apGo.AddComponent<APManager>();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[APSetupFix] Added APManager to BattleTest scene.");
            }
            else
            {
                Debug.Log("[APSetupFix] APManager already exists in BattleTest scene.");
            }
        }
        else
        {
            Debug.LogWarning("[APSetupFix] BattleTest scene not found at " + scenePath);
        }
    }
}
