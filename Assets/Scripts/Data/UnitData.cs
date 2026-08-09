// UnitData.cs
// ScriptableObject that holds design-time data for a unit archetype.
// Create instances via Assets > Create > CheckmateRPG > Unit Data.
// Units reference this asset at runtime to avoid hardcoded values.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using CheckmateRPG.Core.Abilities;
using CheckmateRPG.Core;

namespace CheckmateRPG.Data
{
    public enum ChessPieceType
    {
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    public enum MovePatternType
    {
        Pawn,
        Knight,
        King,
        SlidingOrthogonal,
        SlidingDiagonal,
        SlidingOmni
    }

    public enum CombatStyle
    {
        Melee,
        Range
    }

    public enum ResonanceRoleType
    {
        Offensive, // 공격형 (AD/치명/관통 특화)
        Defensive, // 수비형 (HP/방어/CC저항 특화)
        Support    // 지원형 (쿨타임단축/SP지원/CC지속 특화)
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Unit Data", fileName = "NewUnitData")]
    public class UnitData : ScriptableObject
    {
        // ─── Identity ─────────────────────────────────────────────────────────────

        [Header("AI")]
        [Tooltip("Profile defining how the AI behaves if this unit is an enemy.")]
        public EnemyAIProfile AIProfile;

        [Header("Identity")]
        [Tooltip("Display name of the unit archetype (e.g. Knight, Archer).")]
        public string UnitName = "Unit";

        [Tooltip("2D Portrait/Icon for UI display.")]
        public Sprite Icon;

        public GameObject Prefab;

        [Header("Chess")]
        [Tooltip("Chess piece archetype used for role defaults and promotion checks.")]
        public ChessPieceType PieceType = ChessPieceType.Pawn;

        [Tooltip("Injected move pattern type resolved by MovementComponent at runtime.")]
        public MovePatternType MovePattern = MovePatternType.Pawn;

        [Tooltip("Default class role for this chess piece.")]
        public string BaseRole = "척후대";

        [Tooltip("Specific subclass role defined in GDD Part 3.")]
        public UnitSubclassType Subclass = UnitSubclassType.None;

        [Tooltip("Resonance preset role type (Offensive, Defensive, Support).")]
        public ResonanceRoleType ResonanceRole = ResonanceRoleType.Offensive;

        // ─── Health ───────────────────────────────────────────────────────────────

        [Header("Health")]
        [Tooltip("Maximum hit points.")]
        [Min(1f)] public float MaxHealth = 100f;


        // ─── Defense ──────────────────────────────────────────────────────────────

        [Header("Defense")]
        [Tooltip("Physical damage reduction (Absolute value. e.g. 10 reduces physical damage by 10).")]
        [Min(0f)] public float Defense = 0f; 

        [Tooltip("Magical damage reduction percentage (0 = none, 1 = 100% immune).")]
        [Range(0f, 1f)] public float Resistance = 0.1f;

        // ─── Combat ───────────────────────────────────────────────────────────────

        [Header("Combat")]
        [Tooltip("전투 스타일 (근접/원거리 모션 구분용)")]
        public CombatStyle Style = CombatStyle.Melee;

        [Tooltip("기본 공격 시의 타격 횟수")]
        [Min(1)] public int AttackCount = 1;

        [Tooltip("타격당 공격력 반영 비율 (예: 65%면 0.65)")]
        [Min(0f)] public float AttackDamageRatio = 1f;

        [Tooltip("Damage dealt per attack.")]
        [Min(0f)] public float AttackDamage = 10f;

        [Tooltip("Minimum seconds between two attacks.")]
        [Min(0.1f)] public float AttackCooldown = 1f;

        [Tooltip("Maximum grid distance at which the unit can attack (Chebyshev distance).")]
        [Min(1)] public int AttackRange = 1;

        [Tooltip("Damage type used by this unit's basic attack.")]
        public DamageType BasicAttackDamageType = DamageType.Physical;

        [Tooltip("AI scoring value awarded when this unit is defeated.")]
        [Min(0f)] public float KillValue = 10f;

        [Header("Resources")]
        [Tooltip("SP 회복 방식 (자동/타격/피격)")]
        public SPChargeType ChargeType = SPChargeType.Auto;
        
        [Tooltip("Skill Points")]
        [Min(1f)] public float MaxSP = 24f;
        [Min(0f)] public float InitSP = 4f;
        [Tooltip("기본 공격에 사용하는 AbilityData(평타 SP 획득량 등 참조). (구버전)")]
        public AbilityDataSO BasicAttackAbilityData;

        [Tooltip("기본 공격에 사용하는 스킬 정의 (M7 신규)")]
        public AbilityDefinition BasicAttackAbility;

        // ─── Deployment & Action Costs ───────────────────────────────────────────

        [Header("Deployment")]
        [Tooltip("Cost required to deploy this unit in the deck.")]
        [Min(0)] public int DeploymentCost = 2;

        [Header("Action Costs")]
        [Tooltip("AP cost to perform one move action (before multipliers).")]
        [FormerlySerializedAs("MoveAPCost")]
        [Min(0f)] public float MoveCostAP = 8f;

        [Tooltip("AP cost to perform one attack action (before multipliers).")]
        [FormerlySerializedAs("AttackAPCost")]
        [Min(0f)] public float AttackCostAP = 8f;

        [Tooltip("Base action speed multiplier for movement and attack cooldowns.")]
        [Min(0.1f)] public float ActionSpeed = 1f;

        // ─── Movement ─────────────────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Maximum grid cells moved per action.")]
        [Min(1)] public int MoveRange = 3;

        [Tooltip("Tooltip(\"행동 속도 등급 (5: 가장 빠름 ~ 1: 가장 느림, 0/6 시스템적 최하/최상)\")")]
        [Range(0, 6)] public int SpeedLevel = 1;

        [Tooltip("Target Cell로 Lerp할 때 초당 이동하는 거리(World-units). Level에 따라 자동 계산됩니다.")]
        [Min(0.1f)] public float MoveSpeed = 0.8f;

        [Tooltip("유닛의 공격/스킬 사용 시 발생하는 선딜레이")]
        [Min(0.1f)] public float ActionDelay = 2.5f;

        // ─── Physics ─────────────────────────────────────────────────────────────

        [Header("Physics")]
        [Tooltip("Weight grade used for knockback calculations (0 = light, 4 = heavy).")]
        [Range(0, 4)] public int Weight = 1;

        [Tooltip("Bosses ignore weight reduction from stagger and resist freeze.")] 
        public bool IsBoss = false;

        public void SyncDefaultChessMetadata()
        {
            BaseRole = GetDefaultRole(PieceType);
            MovePattern = GetDefaultMovePattern(PieceType);
        }

        public static string GetDefaultRole(ChessPieceType pieceType)
        {
            return pieceType switch
            {
                ChessPieceType.Pawn => "척후대",
                ChessPieceType.Knight => "돌격기사",
                ChessPieceType.Bishop => "대주교",
                ChessPieceType.Rook => "포트리스",
                ChessPieceType.Queen => "대왕",
                ChessPieceType.King => "군주",
                _ => "징집병"
            };
        }

        public static MovePatternType GetDefaultMovePattern(ChessPieceType pieceType)
        {
            return pieceType switch
            {
                ChessPieceType.Pawn => MovePatternType.Pawn,
                ChessPieceType.Knight => MovePatternType.Knight,
                ChessPieceType.Bishop => MovePatternType.SlidingDiagonal,
                ChessPieceType.Rook => MovePatternType.SlidingOrthogonal,
                ChessPieceType.Queen => MovePatternType.SlidingOmni,
                ChessPieceType.King => MovePatternType.King,
                _ => MovePatternType.Pawn
            };
        }

        private void OnValidate()
        {
            SyncDefaultChessMetadata();
            UpdateMoveSpeed();
        }

        public void UpdateMoveSpeed()
        {
            MoveSpeed = SpeedLevel switch
            {
                0 => 0.5f,
                1 => 0.8f,
                2 => 1.1f,
                3 => 1.5f,
                4 => 2.0f,
                5 => 2.5f,
                6 => 3.0f,
                _ => 1.0f
            };
            ActionDelay = SpeedLevel switch
            {
                0 => 3.0f,
                1 => 2.5f,
                2 => 2.0f,
                3 => 1.5f,
                4 => 1.1f,
                5 => 0.8f,
                6 => 0.5f,
                _ => 1.0f
            };
        }

        // ─── Skills ─────────────────────────────────────────────────────────────
        [Serializable]
        public struct SkillLevelData
        {
            public string SkillId;
            [Min(1)] public int Level;
        }

        [Header("Skills & Passives")]
        [Tooltip("초기 스킬 레벨 설정")]
        public List<SkillLevelData> InitialSkillLevels = new();

        [Tooltip("유닛이 보유한 스킬 정의 목록 (M7 신규)")]
        public List<Core.AbilityDefinition> Abilities = new();

        [Tooltip("이하 중 택 1 - 대원이 선택 및 장착 가능한 액티브 스킬 목록")]
        public List<Core.AbilityDefinition> SelectableActiveSkills = new();

        [Tooltip("현재 선택/장착된 액티브 스킬 인덱스 (0, 1, 2)")]
        public int SelectedSkillIndex = 0;

        public Core.AbilityDefinition GetSelectedActiveSkill()
        {
            if (SelectableActiveSkills != null && SelectableActiveSkills.Count > 0)
            {
                int idx = Mathf.Clamp(SelectedSkillIndex, 0, SelectableActiveSkills.Count - 1);
                return SelectableActiveSkills[idx];
            }
            if (Abilities != null && Abilities.Count > 0)
                return Abilities[0];
            return null;
        }

        public int GetSkillLevel(string skillId)
        {
            if (InitialSkillLevels == null) return 1;
            for (int i = 0; i < InitialSkillLevels.Count; i++)
            {
                if (InitialSkillLevels[i].SkillId == skillId)
                    return InitialSkillLevels[i].Level;
            }
            return 1; // 기본은 1레벨
        }

        // ─── Future Extensions ────────────────────────────────────────────────────
        // faction, visual prefab reference, etc. can be added here without touching
        // runtime component code.
    }
}
