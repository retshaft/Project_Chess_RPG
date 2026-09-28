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

        public bool TryGetReadiness(out string reason)
        {
            if (!EconomyConfigured)
            { reason = "작전의 전력·보상 설정이 준비되지 않았습니다."; return false; }
            reason = "적 배치가 준비되지 않은 작전입니다.";
            if (EnemySpawns == null || EnemySpawns.Count == 0) return false;
            bool hasKing = false;
            var cells = new HashSet<Vector2Int>();
            foreach (var spawn in EnemySpawns)
            {
                if (spawn.EnemyUnit == null) return false;
                if (spawn.GridPosition.x < 0 || spawn.GridPosition.x >= Grid.GridSystem.GridWidth ||
                    spawn.GridPosition.y < 0 || spawn.GridPosition.y >= Grid.GridSystem.GridHeight ||
                    !cells.Add(spawn.GridPosition))
                { reason = "적 배치 위치를 점검해야 하는 작전입니다."; return false; }
                hasKing |= spawn.EnemyUnit.PieceType == ChessPieceType.King;
            }
            if (Objective == ObjectiveType.KingDefeat && !hasKing)
            { reason = "목표인 적 킹이 배치되지 않은 작전입니다."; return false; }
            reason = "";
            return true;
        }

        [Header("Rewards Foundation")]
        public bool EconomyConfigured;
        public bool OfficialRewardTable;
        [Min(1)] public int SanityCost;
        [Min(0)] public long RewardCredits;
        [Min(0)] public long FirstClearBonusCredits;
        [Min(0)] public int RewardExperienceChips;
        [Header("Official Record Rewards")]
        [Min(0)] public int FirstClearCrystals;
        [Min(0)] public long FirstClearCredits;
        [Min(0)] public int FirstClearChipT1;
        [Min(0)] public int FirstClearChipT2;
        [Min(0)] public int FirstClearChipT3;
        [Min(0)] public int FirstClearChipT4;
        [Min(0)] public int FirstClearProtocolChips;
        [Min(0)] public long RepeatCredits;
        [Min(0)] public int RepeatChipT1;
        [Min(0)] public int RepeatChipT2;
        [Min(0)] public int RepeatChipT3;
        [Min(0)] public int RepeatChipT4;
        [Range(0f,1f)] public float ChanceDropT1;
        [Range(0f,1f)] public float ChanceDropT2;
        [Range(0f,1f)] public float ChanceDropT3;
        [Range(0f,1f)] public float ChanceDropT4;
        [Min(0)] public int ChanceDropCount = 1;
        [Min(1)] public int RequiredSanity = 6;
        public bool IsBossStage;
        // Legacy values are serialized only; the economy no longer awards account TP.
        public int RewardTP = 50;

        [Tooltip("첫 클리어 시에만 추가 지급되는 보너스 TP. 기획 미확정 — 기본 0(반복과 동일).")]
        [Min(0)]
        public int FirstClearBonusTP = 0;

        public List<UnitData> ClearRewards = new List<UnitData>();
    }
}
