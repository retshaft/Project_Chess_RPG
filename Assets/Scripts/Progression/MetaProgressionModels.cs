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

    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Notation Node", fileName = "NotationNode")]
    public class NotationNodeData : ScriptableObject
    {
        [Tooltip("Stable id for this notation node.")]
        public string NodeId = "N-001";

        [Min(0)]
        [Tooltip("TP consumed when this node is unlocked.")]
        public int TPCost = 1;

        [Tooltip("Nodes that must be unlocked first.")]
        public List<NotationNodeData> Prerequisites = new();

        [Tooltip("Stat bonuses granted by this node.")]
        public List<StatModifierEntry> Bonuses = new();
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Notation Tree", fileName = "NotationTree")]
    public class NotationTreeData : ScriptableObject
    {
        public NotationNodeData RootNode;
        public List<NotationNodeData> Nodes = new();
    }

    [Serializable]
    public class ResonanceStageBonus
    {
        [Min(0)] public int Stage = 1;
        public List<StatModifierEntry> Bonuses = new();
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Resonance Profile", fileName = "ResonanceProfile")]
    public class ResonanceProfileData : ScriptableObject
    {
        public List<ResonanceStageBonus> StageBonuses = new();

        public IEnumerable<StatModifierEntry> GetBonusesUpToStage(int stage)
        {
            if (StageBonuses == null)
                yield break;

            for (int i = 0; i < StageBonuses.Count; i++)
            {
                ResonanceStageBonus entry = StageBonuses[i];
                if (entry == null || entry.Bonuses == null || entry.Stage > stage)
                    continue;

                for (int j = 0; j < entry.Bonuses.Count; j++)
                    yield return entry.Bonuses[j];
            }
        }
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Edict", fileName = "Edict")]
    public class EdictData : ScriptableObject
    {
        public string EdictName = "New Edict";
        public EdictKind Kind = EdictKind.General;
        public PolarityType Polarity = PolarityType.Neutral;

        [Min(0)]
        [Tooltip("Sync capacity consumed while this edict is active.")]
        public int SyncCost = 1;

        public List<StatModifierEntry> Bonuses = new();
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Sync Capacity", fileName = "SyncCapacity")]
    public class SyncCapacityData : ScriptableObject
    {
        [Min(0)] public int BaseCapacity = 2;
    }
}
