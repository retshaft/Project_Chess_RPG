namespace CheckmateRPG.Core
{
    /// <summary>
    /// Canonical game event ids.
    /// New ids should follow past-tense naming (e.g. Damaged, Resolved, Expired).
    /// </summary>
    public static class GameEventIds
    {
        public const string MoveStarted = nameof(MoveStarted);
        public const string MoveCompleted = nameof(MoveCompleted);
        public const string AttackStarted = nameof(AttackStarted);
        public const string AttackResolved = nameof(AttackResolved);
        public const string AbilityCastStarted = nameof(AbilityCastStarted);
        public const string AbilityResolved = nameof(AbilityResolved);
        public const string EffectApplied = nameof(EffectApplied);
        public const string EffectExpired = nameof(EffectExpired);
        public const string UnitKilled = nameof(UnitKilled);
        public const string UnitDamaged = nameof(UnitDamaged);
        public const string OnAttackHit = nameof(OnAttackHit);
        public const string AfterAttackHit = nameof(AfterAttackHit);
        public const string BeforeAttackHit = nameof(BeforeAttackHit);
        public const string OnTakeDamage = nameof(OnTakeDamage);
        public const string BeforeTakeDamage = nameof(BeforeTakeDamage);
        public const string AfterTakeDamage = nameof(AfterTakeDamage);
        public const string Promoted = nameof(Promoted);
        public const string OnPromotion = Promoted;
        public const string OnPromotioned = Promoted;
    }
}
