#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Data;

namespace CheckmateRPG.Testing.Editor
{
    [InitializeOnLoad]
    public static class AutoGenerateTestAssets
    {
        static AutoGenerateTestAssets()
        {
            EditorApplication.delayCall += GenerateAssets;
        }

        private static void GenerateAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Test"))
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "Test");
            }

            // 1. Create ConscriptUnitData
            string conscriptPath = "Assets/Scenes/Test/ConscriptUnitData.asset";
            if (AssetDatabase.LoadAssetAtPath<UnitData>(conscriptPath) == null)
            {
                UnitData conscript = ScriptableObject.CreateInstance<UnitData>();
                conscript.UnitName = "Conscript";
                conscript.BaseRole = "징집병";
                conscript.PieceType = ChessPieceType.Pawn;
                conscript.MaxHealth = 80f; // 80% of pawn
                conscript.AttackDamage = 8f; // 80% of pawn
                conscript.DeploymentCost = 1;
                AssetDatabase.CreateAsset(conscript, conscriptPath);
                Debug.Log($"[AutoGenerateTestAssets] Created {conscriptPath}");
            }

            // 2. Create DefaultTestDeck
            string deckPath = "Assets/Scenes/Test/DefaultTestDeck.asset";
            if (AssetDatabase.LoadAssetAtPath<PlayerDeckData>(deckPath) == null)
            {
                PlayerDeckData deck = ScriptableObject.CreateInstance<PlayerDeckData>();
                deck.MaxCostCap = 15;
                
                // Try to load existing units to populate
                UnitData king = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Scenes/Test/KingUnitData.asset");
                UnitData queen = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Scenes/Test/QueenUnitData.asset");
                UnitData rook = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Scenes/Test/RookUnitData.asset");
                UnitData bishop = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Scenes/Test/BishopUnitData.asset");
                UnitData knight = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Scenes/Test/KnightUnitData.asset");
                UnitData pawn = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Scenes/Test/PawnUnitData.asset");

                if (king) deck.Roster.Add(king);
                if (queen) deck.Roster.Add(queen);
                if (rook) deck.Roster.Add(rook);
                if (bishop) deck.Roster.Add(bishop);
                if (knight) deck.Roster.Add(knight);
                if (pawn) { deck.Roster.Add(pawn); deck.Roster.Add(pawn); deck.Roster.Add(pawn); }

                AssetDatabase.CreateAsset(deck, deckPath);
                Debug.Log($"[AutoGenerateTestAssets] Created {deckPath}");
            }
        }
    }
}
#endif
