using System;
using UnityEngine;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;
using CheckmateRPG.Units;

namespace CheckmateRPG.Testing
{
    public class MasterGDDPart4Tester : MonoBehaviour
    {
        [Header("Simulation Units")]
        [SerializeField] private UnitBrain _kiaraUnit;  // Offensive Preset
        [SerializeField] private UnitBrain _vestaUnit;  // Defensive Preset
        [SerializeField] private UnitBrain _supportUnit; // Support Preset

        private string _testLog = "마스터 기획서 Part 4 시스템 검증 콘솔 ready...";
        private int _simulatedResonanceStage = 3;

        private void OnGUI()
        {
            GUI.Box(new Rect(10, Screen.height - 310, Screen.width - 20, 300), "🏆 [Phase 10] 마스터 기획서 Part 4 샌드박스 테스터 (AI 성향 & 다중 공명 프리셋 & 칙령 극성)");

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            textStyle.normal.textColor = Color.yellow;

            if (GUI.Button(new Rect(30, Screen.height - 270, 260, 45), "🛡️ 3대 공명 프리셋 테스트 (Stage 1~5)", btnStyle))
            {
                RunResonancePresetTests();
            }
            if (GUI.Button(new Rect(300, Screen.height - 270, 260, 45), "🧠 AI 3대 성향 & Kill Value 실증", btnStyle))
            {
                RunAIArchetypeTests();
            }
            if (GUI.Button(new Rect(570, Screen.height - 270, 260, 45), "👑 킹 칙령 수용량 & 극성 할인 연산", btnStyle))
            {
                RunKingEdictPolarityTests();
            }
            if (GUI.Button(new Rect(840, Screen.height - 270, 200, 45), $"돌파 변경: STG {_simulatedResonanceStage} -> {(_simulatedResonanceStage % 5) + 1}", btnStyle))
            {
                _simulatedResonanceStage = (_simulatedResonanceStage % 5) + 1;
                ResonanceSystem.SetDebugResonanceStage(_simulatedResonanceStage);
                _testLog = $"[System] 시뮬레이션 돌파(Resonance) 단계가 [Stage {_simulatedResonanceStage}] (으)로 설정되었습니다. (부활 없음 규칙 가동)";
                Debug.Log(_testLog);
            }

            GUI.Label(new Rect(30, Screen.height - 210, Screen.width - 60, 190), _testLog, textStyle);
        }

        public void RunResonancePresetTests()
        {
            ResonanceSystem.SetDebugResonanceStage(_simulatedResonanceStage);

            // Create temporary mock UnitData if physical instances are absent
            var offData = ScriptableObject.CreateInstance<UnitData>();
            offData.UnitName = "키아라 (Mock)";
            offData.PieceType = ChessPieceType.Knight;
            offData.ResonanceRole = ResonanceRoleType.Offensive;

            var defData = ScriptableObject.CreateInstance<UnitData>();
            defData.UnitName = "베스타 (Mock)";
            defData.PieceType = ChessPieceType.Rook;
            defData.ResonanceRole = ResonanceRoleType.Defensive;

            var supData = ScriptableObject.CreateInstance<UnitData>();
            supData.UnitName = "책사 (Mock)";
            supData.PieceType = ChessPieceType.Bishop;
            supData.ResonanceRole = ResonanceRoleType.Support;

            GameObject go1 = new GameObject("Off_Unit");
            var offBrain = go1.AddComponent<UnitBrain>();
            offBrain.UnitData = offData;

            GameObject go2 = new GameObject("Def_Unit");
            var defBrain = go2.AddComponent<UnitBrain>();
            defBrain.UnitData = defData;

            GameObject go3 = new GameObject("Sup_Unit");
            var supBrain = go3.AddComponent<UnitBrain>();
            supBrain.UnitData = supData;

            float offAttack = ResonanceSystem.GetAttackMultiplier(offBrain);
            float defHP = ResonanceSystem.GetMaxHealthMultiplier(defBrain);
            float supCDR = ResonanceSystem.GetCooldownReduction(supBrain);
            float offSP = ResonanceSystem.GetInitialSPBonus(offBrain);
            float defSP = ResonanceSystem.GetInitialSPBonus(defBrain);
            float defResist = ResonanceSystem.GetStatusResistanceBonus(defBrain);
            int tpBonus = ResonanceSystem.GetNotationTP(_simulatedResonanceStage);

            _testLog = $"[3대 공명 프리셋 시연 결과 (Stage {_simulatedResonanceStage}) - 부활 불가능 규칙 적용]\n" +
                       $"🗡️ [공격형 - 키아라]: 공격력 배율 {offAttack:F2}x | 초기 SP +{offSP} | 관통 보너스 +{ResonanceSystem.GetArmorPenetrationBonus(offBrain) * 100}%\n" +
                       $"🛡️ [수비형 - 베스타]: 최대 체력(HP) {defHP:F2}x | 초기 SP +{defSP} | 방어력 {ResonanceSystem.GetDefenseMultiplier(defBrain):F2}x | CC 저항 +{defResist * 100}%\n" +
                       $"🪄 [지원형 - 책사]: 스킬 쿨타임 단축 -{supCDR * 100}% | 초기 SP +{ResonanceSystem.GetInitialSPBonus(supBrain)} | CC 지속시간 {ResonanceSystem.GetStatusDurationMultiplier(supBrain):F2}x\n" +
                       $"🧩 기보 풀이 보조 TP: +{tpBonus} TP 지급 (기획서 3~4 TP 부족 설계 완충)";

            Debug.Log(_testLog);
            DestroyImmediate(go1);
            DestroyImmediate(go2);
            DestroyImmediate(go3);
            DestroyImmediate(offData);
            DestroyImmediate(defData);
            DestroyImmediate(supData);
        }

        public void RunAIArchetypeTests()
        {
            // Test 1: Kill Value hierarchy
            var kingData = ScriptableObject.CreateInstance<UnitData>();
            kingData.PieceType = ChessPieceType.King;
            GameObject kGo = new GameObject("KingMock");
            var kingBrain = kGo.AddComponent<UnitBrain>();
            kingBrain.UnitData = kingData;

            var pawnData = ScriptableObject.CreateInstance<UnitData>();
            pawnData.PieceType = ChessPieceType.Pawn;
            GameObject pGo = new GameObject("PawnMock");
            var pawnBrain = pGo.AddComponent<UnitBrain>();
            pawnBrain.UnitData = pawnData;

            float kingKillVal = TacticalAIEvaluator.GetKillValue(kingBrain);
            float pawnKillVal = TacticalAIEvaluator.GetKillValue(pawnBrain);

            // Test 2: AP Penalty by Archetype (24 AP skill at 30/100 Team AP)
            float berserkerPenalty = TacticalAIEvaluator.CalculateAPPenalty(24f, 30f, 100f, 1.0f, AIBehaviorType.Berserker);
            float tacticianPenalty = TacticalAIEvaluator.CalculateAPPenalty(24f, 30f, 100f, 1.0f, AIBehaviorType.Tactician);
            float defensivePenalty = TacticalAIEvaluator.CalculateAPPenalty(24f, 30f, 100f, 1.0f, AIBehaviorType.Defensive);

            _testLog = $"[AI 의사결정 및 3대 성향 연산 결과 (Team AP 30/100 상태에서 24 AP 스킬 평가)]\n" +
                       $"👑 Kill Value 검증: King({kingKillVal}점) vs Pawn({pawnKillVal}점) -> King이 100배 가치 획득!\n" +
                       $"🔥 [광전사 Berserker]: AP 보존 페널티 = {berserkerPenalty} -> AP 모이는 즉시 시전 강제!\n" +
                       $"♟️ [전략가 Tactician]: AP 보존 페널티 = {tacticianPenalty:F1} -> 70% 이하 AP에서 저축을 위해 시전 억제!\n" +
                       $"🛡️ [수비형 Defensive]: AP 보존 페널티 = {defensivePenalty:F1} & 사거리 외 전진 시 -50점 페널티 부여(니가와 대기 대형)!";

            Debug.Log(_testLog);
            DestroyImmediate(kGo);
            DestroyImmediate(pGo);
            DestroyImmediate(kingData);
            DestroyImmediate(pawnData);
        }

        public void RunKingEdictPolarityTests()
        {
            // Create Mock Absolute Edict (Aura Mod) with Polarity matching King (Order)
            var absEdict = ScriptableObject.CreateInstance<EdictData>();
            absEdict.EdictName = "절대 칙령: 발할라식 강습";
            absEdict.Kind = EdictKind.Absolute;
            absEdict.Polarity = PolarityType.Order;
            absEdict.SyncCost = 15; // Base bonus capacity

            int baseCapacity = 50;
            int playedTP = 20;

            // Test Math: Matched polarity doubles bonus capacity (15 -> 30)
            int maxCapMatched = PolarityCalculator.CalculateMaxSyncCapacity(baseCapacity, playedTP, absEdict, PolarityType.Order);
            int maxCapMismatched = PolarityCalculator.CalculateMaxSyncCapacity(baseCapacity, playedTP, absEdict, PolarityType.Chaos);

            // General Edict cost calculations (11 Cost Edict)
            var genEdict = ScriptableObject.CreateInstance<EdictData>();
            genEdict.EdictName = "일반 칙령: 중화력 개량";
            genEdict.Kind = EdictKind.General;
            genEdict.Polarity = PolarityType.Order;
            genEdict.SyncCost = 11;

            int costMatched = PolarityCalculator.CalculateEdictCost(genEdict, absEdict); // 11 -> 50% rounded -> 6

            absEdict.Polarity = PolarityType.Chaos; // mismatch
            int costMismatched = PolarityCalculator.CalculateEdictCost(genEdict, absEdict); // 11 -> +20% rounded -> 13

            _testLog = $"[킹 싱크로 칙령 및 극성 연산 실증 결과 (슈트 기본 50 + 플레이 TP 20)]\n" +
                       $"👑 절대 칙령 보너스 수용량: 극성 일치(Order-Order) 시 [ {maxCapMatched} ] (보너스 2배!) vs 불일치 시 [ {maxCapMismatched} ]\n" +
                       $"⚡ 일반 칙령(기본 소모 11): 극성 일치 시 50% 반올림 할인 -> [ {costMatched} Cost 소모 ]\n" +
                       $"⚠️ 극성 불일치 페널티: 기본 11 Cost -> +20% 할증 페널티 적용 -> [ {costMismatched} Cost 소모 ]";

            Debug.Log(_testLog);
            DestroyImmediate(absEdict);
            DestroyImmediate(genEdict);
        }
    }
}
