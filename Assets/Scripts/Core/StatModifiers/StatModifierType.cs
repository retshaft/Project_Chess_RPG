namespace CheckmateRPG.Core.StatModifiers
{
    public enum StatModifierType
    {
        // ── 전투 스탯 ──
        AttackDamageMultiplier,      // 공격력 배율 (1.5 → +50%)
        AttackCountOverride,         // 공격 횟수 덮어쓰기 (3 → 3타)
        AttackDamageRatioOverride,   // 타격당 피해 비율 덮어쓰기 (0.7 → 70%)
        DefPenetrationFlat,          // 방어 관통 고정값 (0.15 → 15%)

        // ── AP 비용 ──
        APCostFlat,                  // AP 비용 고정 증감 (-4 → 4 감소)
        APCostMultiplier,            // AP 비용 배율 (0.5 → 절반)

        // ── 쿨다운 ──
        AttackCooldownMultiplier,    // 공격 쿨다운 배율 (0.4 → 60% 감소)

        // ── 적중 시 효과 ──
        OnHitApplyBleed,             // 타격 시 출혈 부여 (Value = 스택 수)
        OnHitApplyBurn,              // 타격 시 화상 부여

        // ── 상태 제어 ──
        DefenseMultiplier,           // 방어력 배율
        ResistanceMultiplier,        // 마법 저항력 배율
        ActionSpeedMultiplier,       // 행동 속도 배율
        HealReceivedMultiplier,      // 받는 치유량 배율
        MoveLocked,                  // 이동 불가 (Value > 0 이면 불가)
        AttackLocked                 // 공격 불가 (Value > 0 이면 불가)
    }
}
