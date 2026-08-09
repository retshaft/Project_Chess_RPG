using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public enum MetaStatType
    {
        MaxHealth,
        AttackDamage,
        Defense,
        Resistance,
        MoveRange,
        AttackRange,
        MoveCostAP,
        AttackCostAP,
        ActionSpeed,
        MaxSP
    }

    public enum EdictKind
    {
        General,
        Absolute
    }

    public enum PolarityType
    {
        Neutral,
        Order,
        Chaos
    }

    [Serializable]
    public struct StatModifierEntry
    {
        public MetaStatType Stat;
        public float FlatBonus;
        public float PercentBonus;
    }

    [Serializable]
    public class ResonanceStageBonus
    {
        [Min(0)] public int Stage = 1;
        public List<StatModifierEntry> Bonuses = new();
    }
}
