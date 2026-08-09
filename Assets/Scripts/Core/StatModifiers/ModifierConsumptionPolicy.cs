namespace CheckmateRPG.Core.StatModifiers
{
    public enum ModifierConsumptionPolicy
    {
        Duration,           // 이펙트 지속시간에만 의존 (기본값)
        ConsumeOnAttack,    // 다음 공격 실행 후 소모
        ConsumeOnAction,    // 다음 행동(이동/공격/스킬) 실행 후 소모 (스택 기반)
    }
}
