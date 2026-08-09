using UnityEngine;
using CheckmateRPG.Data;
using CheckmateRPG.Units;

namespace CheckmateRPG.Progression
{
    public static class ResonanceSystem
    {
        // Dummy resonance stage for testing. In a real scenario, this would be fetched from save data per unit.
        private static int _debugResonanceStage = 3;

        public static void SetDebugResonanceStage(int stage)
        {
            _debugResonanceStage = Mathf.Max(0, stage);
        }

        public static int GetResonanceStage(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null)
                return 1;

            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return _debugResonanceStage;

            var unitSaves = SaveManager.Instance.CurrentData.UnitProgressions;
            if (unitSaves != null)
            {
                // Since UnitData can be a modified clone, we should check its original name, 
                // but for now we remove the "_Modified" suffix if it exists to match the save id.
                string searchId = unit.UnitData.name;
                if (searchId.EndsWith("_Modified"))
                {
                    searchId = searchId.Substring(0, searchId.Length - "_Modified".Length);
                }

                var save = unitSaves.Find(u => u != null && u.UnitId == searchId);
                if (save != null)
                {
                    return save.ResonanceStage;
                }
            }
            
            return 1;
        }

        /// <summary>
        /// Retrieves the permanent AP discount based on the unit's PieceType and Resonance Stage.
        /// </summary>
        public static float GetAPDiscount(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null)
                return 0f;

            int stage = GetResonanceStage(unit);
            
            // Discounts are applied only when resonance stage reaches 3.
            if (stage < 3)
                return 0f;

            return unit.UnitData.PieceType switch
            {
                ChessPieceType.Pawn => 1f,
                ChessPieceType.Knight => 2f,
                ChessPieceType.Bishop => 2f,
                ChessPieceType.Rook => 5f,
                ChessPieceType.Queen => 5f,
                ChessPieceType.King => 0f,
                _ => 0f
            };
        }

        // ─── Phase 10: Multi-Preset Resonance Effects (Offensive / Defensive / Support) ───
        // NOTE: "부활 없음" (Perma-death) 디자이너 원칙에 따라, 기존 부활 대기시간 감소 대신 역할군별 전투 수치 강화가 적용됩니다.

        public static float GetAttackMultiplier(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 1.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 1 && unit.UnitData.ResonanceRole == ResonanceRoleType.Offensive)
                return 1.10f; // 공격형 1단계: 공격력 10% 증가
            return 1.0f;
        }

        public static float GetMaxHealthMultiplier(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 1.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 1 && unit.UnitData.ResonanceRole == ResonanceRoleType.Defensive)
                return 1.10f; // 수비형 1단계: 부활 철폐 -> 생명력(Max HP) 10% 상승
            return 1.0f;
        }

        public static float GetCooldownReduction(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 0.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 1 && unit.UnitData.ResonanceRole == ResonanceRoleType.Support)
                return 0.10f; // 지원형 1단계: 스킬 가속(쿨타임 단축) 10%
            return 0.0f;
        }

        public static float GetInitialSPBonus(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 0.0f;
            int stage = GetResonanceStage(unit);
            if (stage < 2) return 0.0f;

            return unit.UnitData.ResonanceRole switch
            {
                ResonanceRoleType.Offensive => 10f, // 2단계: 공격형 SP +10
                ResonanceRoleType.Defensive => 5f,  // 2단계: 수비형 SP +5 & 방어력 상향
                ResonanceRoleType.Support => 15f,   // 2단계: 지원형 SP +15 (아군 보조 선점에 특화)
                _ => 0f
            };
        }

        public static float GetDefenseMultiplier(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 1.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 2 && unit.UnitData.ResonanceRole == ResonanceRoleType.Defensive)
                return 1.15f; // 수비형 2단계: 방어력 15% 상향
            return 1.0f;
        }

        public static float GetArmorPenetrationBonus(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 0.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 4 && unit.UnitData.ResonanceRole == ResonanceRoleType.Offensive)
                return 0.10f; // 공격형 4단계: 적 방어/저항 관통 10%
            return 0.0f;
        }

        public static float GetStatusResistanceBonus(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 0.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 4 && unit.UnitData.ResonanceRole == ResonanceRoleType.Defensive)
                return 0.15f; // 수비형 4단계: CC 저항 확률 +15%
            return 0.0f;
        }

        public static float GetStatusDurationMultiplier(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 1.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 4 && unit.UnitData.ResonanceRole == ResonanceRoleType.Support)
                return 1.15f; // 지원형 4단계: 부여 상태이상 지속시간 +15%
            return 1.0f;
        }

        public static bool IsSecondTalentEnhanced(UnitBrain unit)
        {
            if (unit == null) return false;
            return GetResonanceStage(unit) >= 5; // 공통 5단계: 제2재능 최종 강화
        }

        public static int GetNotationTP(int resonanceStage)
        {
            // 중복 획득(돌파 2단계 이상)부터 단계별 기보 퍼즐용 TP +2 지급
            int validStage = Mathf.Clamp(resonanceStage, 1, 5);
            return (validStage - 1) * 2;
        }

        /// <summary>
        /// Handles the logic for pulling a unit from the Recruit (Gacha) system.
        /// Unlocks the unit or increases its resonance stage if already owned.
        /// </summary>
        public static void AddUnitResonance(string unitId)
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;
            var data = SaveManager.Instance.CurrentData;

            // Check if already unlocked
            if (!data.UnlockedUnits.Contains(unitId))
            {
                data.UnlockedUnits.Add(unitId);
                Debug.Log($"[ResonanceSystem] New Unit Unlocked: {unitId}");
            }
            
            // Check or Create Progression Data
            var prog = data.UnitProgressions.Find(u => u.UnitId == unitId);
            if (prog == null)
            {
                prog = new UnitProgressionSaveData { UnitId = unitId, ResonanceStage = 1 };
                data.UnitProgressions.Add(prog);
            }
            else
            {
                // Cap resonance stage at max (e.g. 5) if needed, otherwise just increment
                if (prog.ResonanceStage < 5)
                {
                    prog.ResonanceStage++;
                    Debug.Log($"[ResonanceSystem] {unitId} Resonance Increased to Stage {prog.ResonanceStage}");
                }
                else
                {
                    Debug.Log($"[ResonanceSystem] {unitId} Resonance is already Maxed out. Providing alternative currency.");
                }
            }

            SaveManager.Instance.SaveGame();
        }
    }
}
