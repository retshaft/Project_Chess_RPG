using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Abilities
{
    public enum TargetingShape
    {
        Single,
        Self,
        Cross,
        Grid3x3
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Ability Pipeline/Ability Data", fileName = "NewAbilityData")]
    public sealed class AbilityDataSO : ScriptableObject
    {
        public string AbilityId = string.Empty;
        [Min(0)] public int ApCost = 0;
        [Min(0)] public int CooldownTicks = 0;
        [Min(0)] public int Range = 0;
        public TargetingShape TargetShape = TargetingShape.Single;
        public List<EffectDataSO> Effects = new();
    }
}
