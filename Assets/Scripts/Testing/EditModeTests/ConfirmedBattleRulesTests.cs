using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    public class ConfirmedBattleRulesTests
    {
        [TestCase(ChessPieceType.Pawn, 1)]
        [TestCase(ChessPieceType.Knight, 3)]
        [TestCase(ChessPieceType.Bishop, 3)]
        [TestCase(ChessPieceType.Rook, 4)]
        [TestCase(ChessPieceType.Queen, 5)]
        public void OldPrototypeCostDoesNotOverridePieceDefault(ChessPieceType piece, int expected)
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            try
            {
                data.PieceType = piece; data.DeploymentCost = 2;
                Assert.AreEqual(expected, data.FormationCost);
                data.OverrideFormationCost = true;
                Assert.AreEqual(2, data.FormationCost);
            }
            finally { Object.DestroyImmediate(data); }
        }

        [Test] public void SuitCostAndMovingAnOperatorAreCountedOnce()
        {
            var king = ScriptableObject.CreateInstance<UnitData>();
            var queen = ScriptableObject.CreateInstance<UnitData>();
            var rook = ScriptableObject.CreateInstance<UnitData>();
            try
            {
                king.PieceType = ChessPieceType.King; king.DeploymentCost = 3;
                queen.PieceType = ChessPieceType.Queen; rook.PieceType = ChessPieceType.Rook;
                var deck = new[] { king, queen, rook, null };
                Assert.AreEqual(12, DeckManager.CalculateFormationCost(deck));
                Assert.AreEqual(12, DeckManager.CalculateFormationCost(deck, 3, queen));
                Assert.AreEqual(8, DeckManager.CalculateFormationCost(deck, 2, null));
                var fullBaseline = new[] { king, queen, rook,
                    CreatePiece(ChessPieceType.Bishop), CreatePiece(ChessPieceType.Knight),
                    CreatePiece(ChessPieceType.Pawn), CreatePiece(ChessPieceType.Pawn) };
                Assert.AreEqual(20, DeckManager.CalculateFormationCost(fullBaseline));
                fullBaseline[6].OverrideFormationCost = true;
                fullBaseline[6].DeploymentCost = 2;
                Assert.Greater(DeckManager.CalculateFormationCost(fullBaseline), DeckManager.MAX_FORMATION_COST);
                for (int i = 3; i < fullBaseline.Length; i++) Object.DestroyImmediate(fullBaseline[i]);
            }
            finally { Object.DestroyImmediate(king); Object.DestroyImmediate(queen); Object.DestroyImmediate(rook); }
        }

        private static UnitData CreatePiece(ChessPieceType piece)
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            data.PieceType = piece;
            return data;
        }

        [Test] public void FullSPIsRetainedAndReadinessOnlyFiresOncePerCharge()
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            var skill = ScriptableObject.CreateInstance<AbilityDefinition>();
            var go = new GameObject("SP fixture");
            try
            {
                skill.Levels.Add(new AbilityLevelData { SPCost = 3, InitSP = 0 });
                data.Abilities.Add(skill); data.InitSP = 99;
                var sp = go.AddComponent<SPComponent>(); sp.Initialise(data);
                Assert.AreEqual(0, sp.CurrentSP, "Explicit zero skill InitSP overrides unit fallback.");
                Assert.AreEqual(SkillActivationMode.Manual, skill.ActivationMode);
                int ready = 0; sp.OnSPFull += () => ready++;
                sp.AddSP(3); sp.AddSP(10);
                Assert.AreEqual(3, sp.CurrentSP); Assert.AreEqual(1, ready);
                sp.SetCurrentSP(0); sp.AddSP(3);
                Assert.AreEqual(2, ready);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(data); Object.DestroyImmediate(skill); }
        }

        [Test] public void OnlyFiveStarSkillsCanStartCloseup()
        {
            var output = new GameObject("Camera fixture").AddComponent<Camera>();
            var go = new GameObject("Director fixture");
            var actorGO = new GameObject("Actor fixture");
            var data = ScriptableObject.CreateInstance<UnitData>();
            try
            {
                var director = go.AddComponent<BattleCameraDirector>(); director.Initialise(output, null);
                var actor = actorGO.AddComponent<CheckmateRPG.Units.UnitBrain>(); actor.Prepare(data, Vector2Int.zero);
                data.Rarity = 4; director.PlaySkillCloseup(actor);
                Assert.IsFalse(director.IsIntroPlaying);
            }
            finally { Object.DestroyImmediate(actorGO); Object.DestroyImmediate(go); Object.DestroyImmediate(output.gameObject); Object.DestroyImmediate(data); }
        }
    }
}
