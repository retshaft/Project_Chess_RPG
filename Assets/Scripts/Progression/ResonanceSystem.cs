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
        // NOTE: "부활 없음" (Perma-death) 디자이너 원칙 및 "스킬은 쿨타임이 아닌 SP 시스템" 원칙에 따라, 
        // 기존 부활 쿨타임 감소 및 스킬 쿨타임 단축 대신 역할군별 실전 고유 수치 강화가 적용됩니다.

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
                return 1.10f; // 수비형 1단계: 부활 철폐(영구 사망 원칙) -> 생명력(Max HP) 10% 상승
            return 1.0f;
        }

        public static float GetSupportPowerMultiplier(UnitBrain unit)
        {
            if (unit == null || unit.UnitData == null) return 1.0f;
            int stage = GetResonanceStage(unit);
            if (stage >= 1 && unit.UnitData.ResonanceRole == ResonanceRoleType.Support)
                return 1.10f; // 지원형 1단계: 치유량 및 버프 효과 계수 10% 상승
            return 1.0f;
        }

        // 구버전 호환용 (스킬은 SP 시스템이므로 스킬 쿨타임 개념 없음)
        [System.Obsolete("Skills use the SP system, not cooldowns. Use GetSupportPowerMultiplier instead.")]
        public static float GetCooldownReduction(UnitBrain unit)
        {
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

        public static int GetNotationTP(int resonanceStage) => EconomyService.ResonanceTP(resonanceStage);

        // Compatibility entry point: only configured rarity can grant duplicate certificates.
        public static void AddUnitResonance(string unitId)
        {
            var manager=SaveManager.Instance;
            if(manager?.CurrentData==null || string.IsNullOrEmpty(unitId))return;
            foreach(var banner in Resources.LoadAll<RecruitBannerData>("RecruitBanners"))
            {
                if(banner.Entries==null)continue;
                foreach(var entry in banner.Entries)
                    if(entry?.Unit!=null&&entry.Unit.name==unitId&&entry.Stars>=3&&entry.Stars<=5)
                    { RecruitmentService.GrantUnit(manager.CurrentData,unitId,entry.Stars);manager.SaveGame();return; }
            }
            Debug.LogWarning("[ResonanceSystem] 대원 등급 설정이 없어 획득을 처리하지 않았습니다: "+unitId);
        }
    }
}