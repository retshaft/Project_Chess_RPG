using UnityEngine;

namespace CheckmateRPG.Progression
{
    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Sync Capacity", fileName = "SyncCapacity")]
    public class SyncCapacityData : ScriptableObject
    {
        [Min(0)] public int BaseCapacity = 2;
        public PolarityType InnatePolarity = PolarityType.Neutral;
    }
}
