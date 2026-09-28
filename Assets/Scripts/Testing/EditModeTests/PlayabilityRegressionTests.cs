using CheckmateRPG.Data;
using CheckmateRPG.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    public class PlayabilityRegressionTests
    {
        private StageData stage;
        private UnitData unit;
        [SetUp] public void SetUp()
        {
            stage = ScriptableObject.CreateInstance<StageData>();
            stage.EconomyConfigured = true;
            unit = ScriptableObject.CreateInstance<UnitData>();
            unit.PieceType = ChessPieceType.Pawn;
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(unit);
        }
        [Test] public void EmptyOrMissingEnemyCannotStart()
        {
            Assert.IsFalse(stage.TryGetReadiness(out _));
            stage.EnemySpawns.Add(new EnemySpawnInfo());
            Assert.IsFalse(stage.TryGetReadiness(out _));
        }
        [Test] public void InitialHealthNotifiesAlreadySelectedHud()
        {
            var go = new GameObject("Health fixture");
            try
            {
                var health = go.AddComponent<CheckmateRPG.Components.HealthComponent>();
                float shown = -1;
                health.OnHealthChanged += (current, max) => shown = current;
                unit.MaxHealth = 520;
                health.Initialise(unit);
                Assert.AreEqual(520, shown);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void MissingEconomyCannotStartEvenWithEnemies()
        {
            stage.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = unit, GridPosition = new Vector2Int(3, 6) });
            stage.EconomyConfigured = false;
            Assert.IsFalse(stage.TryGetReadiness(out _));
        }
        [TestCase("Stage_01_Skirmish")] [TestCase("Stage_02_Ambush")]
        [TestCase("Stage_03_Checkmate")] [TestCase("Stage_04_Encirclement")]
        [TestCase("Stage_05_CheckmateProtocol")]
        [TestCase("CE_01_CreditConvoy")] [TestCase("CE_02_CreditConvoy")] [TestCase("CE_03_CreditConvoy")]
        [TestCase("LS_01_TacticalTraining")] [TestCase("LS_02_TacticalTraining")] [TestCase("LS_03_TacticalTraining")]
        public void ConnectedCampaignCanEnterWithoutChangingEconomy(string id)
        {
            var campaign = Resources.Load<StageData>("Stages/" + id);
            Assert.IsNotNull(campaign);
            Assert.IsTrue(campaign.TryGetReadiness(out var reason), reason);
            var save = new SaveData { Sanity = 999 };
            Assert.IsTrue(EconomyService.CanEnter(save, campaign, System.DateTime.UtcNow, out reason), reason);
        }
        [TestCase(-1, 6)] [TestCase(8, 6)] [TestCase(3, -1)] [TestCase(3, 8)]
        public void OffBoardEnemyCannotStart(int x, int y)
        {
            stage.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = unit, GridPosition = new Vector2Int(x, y) });
            Assert.IsFalse(stage.TryGetReadiness(out _));
        }
        [Test] public void OverlappingEnemiesCannotStart()
        {
            var spawn = new EnemySpawnInfo { EnemyUnit = unit, GridPosition = new Vector2Int(3, 6) };
            stage.EnemySpawns.Add(spawn);
            Assert.IsTrue(stage.TryGetReadiness(out _));
            stage.EnemySpawns.Add(spawn);
            Assert.IsFalse(stage.TryGetReadiness(out _));
        }
        [Test] public void KingObjectiveRequiresKingButAnnihilationDoesNot()
        {
            stage.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = unit, GridPosition = new Vector2Int(3, 6) });
            Assert.IsTrue(stage.TryGetReadiness(out _));
            stage.Objective = ObjectiveType.KingDefeat;
            Assert.IsFalse(stage.TryGetReadiness(out _));
            unit.PieceType = ChessPieceType.King;
            Assert.IsTrue(stage.TryGetReadiness(out _));
        }
    }
}
