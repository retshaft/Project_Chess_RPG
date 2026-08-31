using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public sealed class NotationProgressState
    {
        private readonly HashSet<NotationNodeData> _unlockedNodes = new();

        public int RemainingTP { get; private set; }
        public int TotalMaxTP { get; private set; }
        public int SpentTP { get; private set; }
        public int ResonanceBonusTP { get; private set; }
        public string BoundUnitId { get; private set; }
        public NotationTreeData BoundTree { get; private set; }

        public IEnumerable<NotationNodeData> UnlockedNodes => _unlockedNodes;

        public NotationProgressState(int initialTP)
        {
            TotalMaxTP = Mathf.Max(0, initialTP);
            RemainingTP = TotalMaxTP;
            SpentTP = 0;
            ResonanceBonusTP = 0;
        }

        public static NotationProgressState CreateForUnit(string unitId, NotationTreeData tree, int? overrideResonanceStage = null)
        {
            int baseTP = tree != null ? tree.BaseNotationTP : 10;
            int stage = 1;

            UnitProgressionSaveData unitSave = null;
            var saveManager = SaveManager.EnsureInstance();
            if (saveManager != null && saveManager.CurrentData != null)
            {
                unitSave = saveManager.CurrentData.UnitProgressions.Find(u => u.UnitId == unitId);
                if (unitSave == null)
                {
                    unitSave = new UnitProgressionSaveData { UnitId = unitId, ResonanceStage = 1 };
                    saveManager.CurrentData.UnitProgressions.Add(unitSave);
                }
                stage = unitSave.ResonanceStage;
            }

            if (overrideResonanceStage.HasValue)
            {
                stage = overrideResonanceStage.Value;
            }

            int bonusTP = ResonanceSystem.GetNotationTP(stage);
            var state = new NotationProgressState(baseTP + bonusTP);
            state.ResonanceBonusTP = bonusTP;
            state.BoundUnitId = unitId;
            state.BoundTree = tree;

            // Restore saved unlocked nodes
            if (unitSave != null && tree != null && unitSave.UnlockedNodeIds != null)
            {
                List<NotationNodeData> allNodes = new List<NotationNodeData>(tree.Nodes);
                if (tree.RootNode != null && !allNodes.Contains(tree.RootNode))
                    allNodes.Add(tree.RootNode);

                foreach (string id in unitSave.UnlockedNodeIds)
                {
                    var match = allNodes.Find(n => n != null && n.NodeId == id);
                    if (match != null && !state._unlockedNodes.Contains(match))
                    {
                        state._unlockedNodes.Add(match);
                        state.SpentTP += match.TPCost;
                    }
                }
                state.RemainingTP = Mathf.Max(0, state.TotalMaxTP - state.SpentTP);
            }

            Debug.Log($"[NotationProgressState] Rehydrated tree for Unit '{unitId}': BaseTP({baseTP}) + ResonanceBonusTP({bonusTP}) = TotalMaxTP({state.TotalMaxTP}) | Spent: {state.SpentTP}, Remaining: {state.RemainingTP}");
            return state;
        }

        public bool IsUnlocked(NotationNodeData node)
        {
            return node != null && _unlockedNodes.Contains(node);
        }

        public bool CanUnlock(NotationNodeData node)
        {
            if (node == null || _unlockedNodes.Contains(node))
                return false;

            if (RemainingTP < node.TPCost)
                return false;

            if (node.Prerequisites == null || node.Prerequisites.Count == 0)
                return true;

            for (int i = 0; i < node.Prerequisites.Count; i++)
            {
                NotationNodeData prerequisite = node.Prerequisites[i];
                if (prerequisite != null && !_unlockedNodes.Contains(prerequisite))
                    return false;
            }

            return true;
        }

        public bool TryUnlock(NotationNodeData node)
        {
            if (!CanUnlock(node))
                return false;

            int cost = Mathf.Max(0, node.TPCost);
            RemainingTP -= cost;
            SpentTP += cost;
            _unlockedNodes.Add(node);

            SyncToSaveManager();
            return true;
        }

        public void ResetNotation()
        {
            _unlockedNodes.Clear();
            SpentTP = 0;
            RemainingTP = TotalMaxTP;
            SyncToSaveManager();
            Debug.Log($"[NotationProgressState] Notation reset complete for unit '{BoundUnitId}'. TP refunded to {RemainingTP}.");
        }

        private void SyncToSaveManager()
        {
            var saveManager = SaveManager.EnsureInstance();
            if (string.IsNullOrEmpty(BoundUnitId) || saveManager == null || saveManager.CurrentData == null)
                return;

            var unitSave = saveManager.CurrentData.UnitProgressions.Find(u => u.UnitId == BoundUnitId);
            if (unitSave == null)
            {
                unitSave = new UnitProgressionSaveData { UnitId = BoundUnitId, ResonanceStage = 1 };
                saveManager.CurrentData.UnitProgressions.Add(unitSave);
            }

            unitSave.UnlockedNodeIds.Clear();
            foreach (var n in _unlockedNodes)
            {
                if (n != null) unitSave.UnlockedNodeIds.Add(n.NodeId);
            }

            saveManager.SaveGame();
        }
    }

    [Serializable]
    public class MetaProgressionLoadout
    {
        public List<NotationNodeData> UnlockedNotationNodes = new();
        public ResonanceProfileData ResonanceProfile;
        [Min(0)] public int ResonanceStage;
        public List<EdictData> ActiveEdicts = new();
        public SyncCapacityData SyncCapacity;
        [Min(0)] public int BonusSyncCapacity;
    }

    public static class MetaProgressionBuilder
    {
        public static MetaProgressionLoadout BuildUnitLoadout(
            UnitProgressionSaveData saveData, 
            ResonanceProfileData profile, 
            List<NotationNodeData> allNodes)
        {
            var loadout = new MetaProgressionLoadout();
            if (saveData == null) return loadout;

            loadout.ResonanceStage = saveData.ResonanceStage;
            loadout.ResonanceProfile = profile;
            
            if (allNodes != null && saveData.UnlockedNodeIds != null)
            {
                foreach (var id in saveData.UnlockedNodeIds)
                {
                    var node = allNodes.Find(n => n != null && n.NodeId == id);
                    if (node != null) loadout.UnlockedNotationNodes.Add(node);
                }
            }
            return loadout;
        }

        public static MetaProgressionLoadout BuildKingLoadout(
            KingProgressionSaveData saveData, 
            SyncCapacityData syncCapacity, 
            EdictData absoluteEdict, 
            List<EdictData> generalEdicts)
        {
            var loadout = new MetaProgressionLoadout();
            if (saveData == null) return loadout;

            loadout.SyncCapacity = syncCapacity;
            
            int capacity = syncCapacity != null ? syncCapacity.BaseCapacity : 0;
            PolarityType currentPolarity = syncCapacity != null ? syncCapacity.InnatePolarity : PolarityType.Neutral;

            if (absoluteEdict != null && absoluteEdict.Kind == EdictKind.Absolute)
            {
                loadout.ActiveEdicts.Add(absoluteEdict);
                
                int absoluteBonus = absoluteEdict.SyncCost;
                if (absoluteEdict.Polarity != PolarityType.Neutral && absoluteEdict.Polarity == currentPolarity)
                {
                    absoluteBonus *= 2; 
                }
                loadout.BonusSyncCapacity += absoluteBonus;
            }

            var syncState = new SyncCapacityState(capacity + loadout.BonusSyncCapacity, absoluteEdict);
            
            if (generalEdicts != null)
            {
                foreach (var edict in generalEdicts)
                {
                    if (edict != null && edict.Kind == EdictKind.General)
                    {
                        if (syncState.TryAssign(edict))
                        {
                            loadout.ActiveEdicts.Add(edict);
                        }
                        else
                        {
                            Debug.LogWarning($"[MetaProgressionBuilder] Sync Capacity exceeded. Dropping Edict: {edict.EdictName}");
                        }
                    }
                }
            }
            return loadout;
        }
    }

    public static class UnitSkillRuntime
    {
        public static int GetSelectedSkillIndex(string unitId)
        {
            var saveManager = SaveManager.EnsureInstance();
            if (saveManager != null && saveManager.CurrentData != null)
            {
                var unitSave = saveManager.CurrentData.UnitProgressions.Find(u => u.UnitId == unitId);
                if (unitSave != null)
                {
                    return unitSave.SelectedSkillIndex;
                }
            }
            return 0;
        }

        public static void SetSelectedSkillIndex(string unitId, int index)
        {
            var saveManager = SaveManager.EnsureInstance();
            if (saveManager != null && saveManager.CurrentData != null)
            {
                var unitSave = saveManager.CurrentData.UnitProgressions.Find(u => u.UnitId == unitId);
                if (unitSave == null)
                {
                    unitSave = new UnitProgressionSaveData { UnitId = unitId };
                    saveManager.CurrentData.UnitProgressions.Add(unitSave);
                }
                unitSave.SelectedSkillIndex = index;
            }
        }
    }

    public sealed class SyncCapacityState
    {
        public int Capacity { get; }
        public int Used { get; private set; }
        public PolarityType CurrentPolarity { get; private set; }
        private EdictData _equippedAbsoluteEdict;

        public SyncCapacityState(int capacity, EdictData absoluteEdict = null)
        {
            Capacity = Mathf.Max(0, capacity);
            Used = 0;
            CurrentPolarity = PolarityType.Neutral;
            _equippedAbsoluteEdict = absoluteEdict;
            if (absoluteEdict != null && absoluteEdict.Polarity != PolarityType.Neutral)
            {
                CurrentPolarity = absoluteEdict.Polarity;
            }
        }

        public int GetRequiredCapacity(EdictData edict)
        {
            if (edict == null)
                return 0;

            if (edict.Kind == EdictKind.Absolute)
                return 0;

            return PolarityCalculator.CalculateEdictCost(edict, _equippedAbsoluteEdict);
        }

        public bool TryAssign(EdictData edict)
        {
            if (edict == null)
                return false;

            int required = GetRequiredCapacity(edict);
            if (Used + required > Capacity)
                return false;

            Used += required;
            if (edict.Polarity != PolarityType.Neutral)
                CurrentPolarity = edict.Polarity;

            return true;
        }
    }
}
