using System.Collections.Generic;

namespace CheckmateRPG.Progression
{
    /// <summary>
    /// 스테이지 클리어 보상의 단일 지급 경로(R01a).
    /// 첫 클리어와 반복 클리어를 구분해 기록하며, 같은 스테이지 키에 대한 중복 첫 클리어 지급을 구조적으로 막는다.
    /// 지급 값은 호출자가 전달하므로 보상 화폐(TP 등)가 임시 제공품이더라도 교체가 가능하다.
    /// SaveManager 싱글턴에 의존하지 않는 순수 정적 함수로, EditMode에서 실제 SaveData로 검증 가능하다.
    /// </summary>
    public static class BattleRewardService
    {
        /// <summary>
        /// 스테이지 클리어 보상을 지급한다. 첫 클리어면 ClearedStages에 기록하고 첫 클리어 보너스를 더한다.
        /// 이미 기록된 스테이지면 반복 보상만 지급한다.
        /// </summary>
        /// <param name="save">대상 저장 데이터(실제 런타임 저장 또는 테스트용 인스턴스).</param>
        /// <param name="stageKey">스테이지 식별자(스테이지 에셋 이름 권장 — 표시용 이름이 바뀌어도 불변).</param>
        /// <param name="repeatRewardTP">반복 클리어 시 지급 TP(기획 미확정이므로 StageData.RewardTP 등 호출자가 결정).</param>
        /// <param name="firstClearBonusTP">첫 클리어 추가 보너스 TP(기획 미확정 — 기본 0, StageData.FirstClearBonusTP 사용 권장).</param>
        /// <param name="grantedTP">실제 지급된 총 TP.</param>
        /// <param name="firstClear">이번 지급이 첫 클리어인지 여부(UI 표시용).</param>
        /// <returns>지급이 실행되면 true. save나 stageKey가 유효하지 않으면 false.</returns>
        public static bool TryGrantStageClearReward(SaveData save, string stageKey, int repeatRewardTP,
            int firstClearBonusTP, out int grantedTP, out bool firstClear)
        {
            grantedTP = 0;
            firstClear = false;

            if (save == null || string.IsNullOrEmpty(stageKey))
                return false;

            if (repeatRewardTP < 0) repeatRewardTP = 0;
            if (firstClearBonusTP < 0) firstClearBonusTP = 0;

            if (save.ClearedStages == null)
                save.ClearedStages = new List<string>();

            firstClear = !save.ClearedStages.Contains(stageKey);
            if (firstClear)
                save.ClearedStages.Add(stageKey);

            grantedTP = firstClear ? repeatRewardTP + firstClearBonusTP : repeatRewardTP;
            save.TacticalPoints += grantedTP;
            save.KingProgression.PlayTP += grantedTP;
            return true;
        }
    }
}
