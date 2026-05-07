using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public sealed class NotationProgressState
    {
        private readonly HashSet<NotationNodeData> _unlockedNodes = new();

        public int RemainingTP { get; private set; }

        public IEnumerable<NotationNodeData> UnlockedNodes => _unlockedNodes;

        public NotationProgressState(int initialTP)
        {
            RemainingTP = Mathf.Max(0, initialTP);
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

            if (node.Prerequisites == null)
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

            RemainingTP -= Mathf.Max(0, node.TPCost);
            _unlockedNodes.Add(node);
            return true;
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

    public sealed class SyncCapacityState
    {
        public int Capacity { get; }
        public int Used { get; private set; }
        public PolarityType CurrentPolarity { get; private set; }

        public SyncCapacityState(int capacity)
        {
            Capacity = Mathf.Max(0, capacity);
            Used = 0;
            CurrentPolarity = PolarityType.Neutral;
        }

        public int GetRequiredCapacity(EdictData edict)
        {
            if (edict == null)
                return 0;

            int required = Mathf.Max(0, edict.SyncCost);
            bool polarityConflict = CurrentPolarity != PolarityType.Neutral
                                    && edict.Polarity != PolarityType.Neutral
                                    && CurrentPolarity != edict.Polarity;

            if (polarityConflict)
                required += 1;

            return required;
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
