#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Data;

namespace CheckmateRPG.Testing.Editor
{
    public static class UpdateCosts
    {
        [InitializeOnLoadMethod]
        public static void UpdateUnitCosts()
        {
            UpdateCost("PawnUnitData.asset", 1);
            UpdateCost("KnightUnitData.asset", 3);
            UpdateCost("BishopUnitData.asset", 3);
            UpdateCost("RookUnitData.asset", 4);
            UpdateCost("QueenUnitData.asset", 5);
            UpdateCost("KingUnitData 1.asset", 3); // Handled differently if needed, but let's try this
            
            AssetDatabase.SaveAssets();
        }

        private static void UpdateCost(string fileName, int cost)
        {
            string path = $"Assets/Scenes/Test/{fileName}";
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (data != null && data.DeploymentCost != cost)
            {
                data.DeploymentCost = cost;
                EditorUtility.SetDirty(data);
                Debug.Log($"[UpdateCosts] Updated {fileName} DeploymentCost to {cost}");
            }
        }
    }
}
#endif
