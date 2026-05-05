namespace CheckmateRPG.Core
{
    public enum DamageType
    {
        Physical,
        Magical,
        True
    }

    public enum ElementType
    {
        None,
        Fire,
        Lightning,
        Cold,
        Nature
    }

    public enum StatusEffectType
    {
        Stagger,
        Bleed,
        Wound,
        Burn,
        Ignite,
        Overload,
        Superconduct,
        Paralysis,
        Chill,
        Freeze,
        Necrosis,
        Poison,
        Virus,
        GrabVulnerability,
        FrozenBossDebuff
    }

    public enum UnitActionType
    {
        Move,
        Attack,
        Skill
    }
}
