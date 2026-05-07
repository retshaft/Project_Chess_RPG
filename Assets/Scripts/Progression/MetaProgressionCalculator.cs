using System.Collections.Generic;
using CheckmateRPG.Data;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public static class MetaProgressionCalculator
    {
        private struct CombinedModifier
        {
            public float Flat;
            public float Percent;
        }

        public static UnitData CreateModifiedUnitData(UnitData source, MetaProgressionLoadout loadout, string nameSuffix = "_Meta")
        {
            if (source == null)
                return null;

            UnitData result = ScriptableObject.CreateInstance<UnitData>();
            Copy(source, result);
            result.name = string.IsNullOrEmpty(nameSuffix) ? source.name : source.name + nameSuffix;

            if (loadout == null)
                return result;

            Dictionary<MetaStatType, CombinedModifier> modifiers = new();
            AddNotationModifiers(loadout.UnlockedNotationNodes, modifiers);
            AddResonanceModifiers(loadout.ResonanceProfile, loadout.ResonanceStage, modifiers);
            AddEdictModifiers(loadout, modifiers);
            ApplyModifiers(result, modifiers);

            return result;
        }

        private static void AddNotationModifiers(List<NotationNodeData> nodes, Dictionary<MetaStatType, CombinedModifier> output)
        {
            if (nodes == null)
                return;

            for (int i = 0; i < nodes.Count; i++)
            {
                NotationNodeData node = nodes[i];
                if (node == null || node.Bonuses == null)
                    continue;

                AddEntries(node.Bonuses, output);
            }
        }

        private static void AddResonanceModifiers(ResonanceProfileData profile, int stage, Dictionary<MetaStatType, CombinedModifier> output)
        {
            if (profile == null)
                return;

            foreach (StatModifierEntry bonus in profile.GetBonusesUpToStage(stage))
                AddEntry(bonus, output);
        }

        private static void AddEdictModifiers(MetaProgressionLoadout loadout, Dictionary<MetaStatType, CombinedModifier> output)
        {
            if (loadout == null || loadout.ActiveEdicts == null)
                return;

            int capacity = loadout.BonusSyncCapacity;
            if (loadout.SyncCapacity != null)
                capacity += loadout.SyncCapacity.BaseCapacity;

            SyncCapacityState syncState = new SyncCapacityState(capacity);
            List<EdictData> sorted = new List<EdictData>(loadout.ActiveEdicts);
            sorted.Sort((a, b) =>
            {
                EdictKind aKind = a != null ? a.Kind : EdictKind.General;
                EdictKind bKind = b != null ? b.Kind : EdictKind.General;
                if (aKind == bKind)
                    return 0;
                return aKind == EdictKind.Absolute ? -1 : 1;
            });

            for (int i = 0; i < sorted.Count; i++)
            {
                EdictData edict = sorted[i];
                if (!syncState.TryAssign(edict) || edict.Bonuses == null)
                    continue;

                AddEntries(edict.Bonuses, output);
            }
        }

        private static void AddEntries(List<StatModifierEntry> entries, Dictionary<MetaStatType, CombinedModifier> output)
        {
            for (int i = 0; i < entries.Count; i++)
                AddEntry(entries[i], output);
        }

        private static void AddEntry(StatModifierEntry entry, Dictionary<MetaStatType, CombinedModifier> output)
        {
            CombinedModifier value = output.TryGetValue(entry.Stat, out CombinedModifier found)
                ? found
                : new CombinedModifier();

            value.Flat += entry.FlatBonus;
            value.Percent += entry.PercentBonus;
            output[entry.Stat] = value;
        }

        private static void ApplyModifiers(UnitData data, Dictionary<MetaStatType, CombinedModifier> modifiers)
        {
            foreach (KeyValuePair<MetaStatType, CombinedModifier> pair in modifiers)
            {
                float baseValue = GetStat(data, pair.Key);
                float modified = (baseValue + pair.Value.Flat) * (1f + pair.Value.Percent);
                SetStat(data, pair.Key, modified);
            }
        }

        private static float GetStat(UnitData data, MetaStatType stat)
        {
            return stat switch
            {
                MetaStatType.MaxHealth => data.MaxHealth,
                MetaStatType.AttackDamage => data.AttackDamage,
                MetaStatType.Defense => data.Defense,
                MetaStatType.Resistance => data.Resistance,
                MetaStatType.MoveRange => data.MoveRange,
                MetaStatType.AttackRange => data.AttackRange,
                MetaStatType.MoveCostAP => data.MoveCostAP,
                MetaStatType.AttackCostAP => data.AttackCostAP,
                MetaStatType.ActionSpeed => data.ActionSpeed,
                MetaStatType.MaxSP => data.MaxSP,
                _ => 0f
            };
        }

        private static void SetStat(UnitData data, MetaStatType stat, float value)
        {
            switch (stat)
            {
                case MetaStatType.MaxHealth:
                    data.MaxHealth = Mathf.Max(1f, value);
                    break;
                case MetaStatType.AttackDamage:
                    data.AttackDamage = Mathf.Max(0f, value);
                    break;
                case MetaStatType.Defense:
                    data.Defense = Mathf.Clamp01(value);
                    break;
                case MetaStatType.Resistance:
                    data.Resistance = Mathf.Clamp01(value);
                    break;
                case MetaStatType.MoveRange:
                    data.MoveRange = Mathf.Max(1, Mathf.RoundToInt(value));
                    break;
                case MetaStatType.AttackRange:
                    data.AttackRange = Mathf.Max(1, Mathf.RoundToInt(value));
                    break;
                case MetaStatType.MoveCostAP:
                    data.MoveCostAP = Mathf.Max(0f, value);
                    break;
                case MetaStatType.AttackCostAP:
                    data.AttackCostAP = Mathf.Max(0f, value);
                    break;
                case MetaStatType.ActionSpeed:
                    data.ActionSpeed = Mathf.Max(0.1f, value);
                    break;
                case MetaStatType.MaxSP:
                    data.MaxSP = Mathf.Max(0f, value);
                    break;
            }
        }

        private static void Copy(UnitData source, UnitData destination)
        {
            destination.UnitName = source.UnitName;
            destination.PieceType = source.PieceType;
            destination.MovePattern = source.MovePattern;
            destination.BaseRole = source.BaseRole;
            destination.MaxHealth = source.MaxHealth;
            destination.Defense = source.Defense;
            destination.Resistance = source.Resistance;
            destination.AttackDamage = source.AttackDamage;
            destination.AttackCooldown = source.AttackCooldown;
            destination.AttackRange = source.AttackRange;
            destination.KillValue = source.KillValue;
            destination.MaxSP = source.MaxSP;
            destination.MoveCostAP = source.MoveCostAP;
            destination.AttackCostAP = source.AttackCostAP;
            destination.ActionSpeed = source.ActionSpeed;
            destination.MoveRange = source.MoveRange;
            destination.MoveSpeed = source.MoveSpeed;
            destination.Weight = source.Weight;
            destination.IsBoss = source.IsBoss;
        }
    }
}
