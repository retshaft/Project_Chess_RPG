namespace CheckmateRPG.Core
{
    [System.Obsolete("Use typed event classes instead of string ids.")]
    public static class GameEventIds
    {
        public const string MoveStarted = nameof(MoveStarted);
        public const string MoveCompleted = nameof(MoveCompleted);
        public const string AttackResolved = nameof(AttackResolved);
        public const string AbilityResolved = nameof(AbilityResolved);
        public const string EffectApplied = nameof(EffectApplied);
        public const string UnitKilled = nameof(UnitKilled);
        public const string UnitDamaged = nameof(UnitDamaged);
    }
}
