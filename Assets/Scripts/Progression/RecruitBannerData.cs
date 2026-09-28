using CheckmateRPG.Data;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    [System.Serializable]
    public class RecruitEntry
    {
        public UnitData Unit;
        [Range(3,5)] public int Stars = 3;
    }
    [CreateAssetMenu(menuName="CheckmateRPG/Progression/Recruit Banner")]
    public class RecruitBannerData : ScriptableObject
    {
        public string BannerId;
        public bool IsPickup;
        public UnitData PickupUnit;
        public RecruitEntry[] Entries;
        [Tooltip("Optional UTC end time in ISO 8601. Empty means no expiry.")]
        public string EndUtc;
    }
}
