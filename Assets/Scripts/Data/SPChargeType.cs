namespace CheckmateRPG.Data
{
    /// <summary>
    /// 스킬 포인트(SP) 충전 방식을 결정합니다.
    /// </summary>
    public enum SPChargeType
    {
        /// <summary>시간 경과에 따라 자동 회복 (예: 초당 1 SP)</summary>
        Auto,
        
        /// <summary>기본 공격을 적중시킬 때마다 회복</summary>
        OnAttack,
        
        /// <summary>적에게 피격당할 때마다 회복</summary>
        OnHit
    }
}
