using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Data;

namespace CheckmateRPG.Progression
{
    [Serializable]
    public struct EnemySpawnInfo
    {
        public UnitData EnemyUnit;
        public Vector2Int GridPosition;
        
        [Header("AI & Resonance Overrides")]
        public bool OverrideAIBehavior;
        public AIBehaviorType AIBehavior;
        public int EnemyResonanceStage;
        public ResonanceRoleType EnemyResonanceRole;
    }

    public enum ObjectiveType
    {
        Annihilation,
        KingDefeat
    }

    [CreateAssetMenu(fileName = "NewStageData", menuName = "CheckmateRPG/Stage Data")]
    public class StageData : ScriptableObject
    {
        public string StageID;
        public string StageName;
        public ObjectiveType Objective = ObjectiveType.Annihilation;
        public int RecommendedLevel = 1;
        
        [TextArea]
        public string Description;
        
        [TextArea]
        public string TacticalBriefing;

        [Header("Enemy Commander Config")]
        public float InitialEnemyAP = 30f;
        public float EnemyAPRegen = 4f;
        public float MaxEnemyAP = 100f;

        public List<EnemySpawnInfo> EnemySpawns = new List<EnemySpawnInfo>();

        [Header("Rewards Foundation")]
        public int RewardTP = 50;
        public List<UnitData> ClearRewards = new List<UnitData>();
    }
}
