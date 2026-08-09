using System;

namespace CheckmateRPG.Core.StatModifiers
{
    [Serializable]
    public class StatModifierEntry
    {
        public StatModifierType Type;
        public float Value;
        public ModifierConsumptionPolicy ConsumptionPolicy = ModifierConsumptionPolicy.Duration;
    }

    [Serializable]
    public struct StatModifierOverride
    {
        [UnityEngine.Tooltip("원본 SO에서 덮어씌울 스탯의 종류")]
        public StatModifierType TargetType;
        [UnityEngine.Tooltip("새로 적용할 수치 (비워두면 0이므로 체크할 때 고려)")]
        public float OverriddenValue;
    }
}
