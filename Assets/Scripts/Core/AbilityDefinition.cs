using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Effects;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public enum AbilityTargetingRule
    {
        None,
        Self,
        SingleTarget,
        MultiTarget
    }

    [Serializable]
    public sealed class AbilityEffectDefinition
    {
        public string EffectId = string.Empty;
        [Min(1)] public int DurationTicks = 1;
        [Min(1)] public int TickInterval = 1;
        [Min(1)] public int InitialTickIn = 1;
        [Min(1)] public int StackCount = 1;
        public EffectStackPolicy StackPolicy = EffectStackPolicy.Refresh;
        [Min(0)] public int MaxStackCap = 0;
        [Min(0f)] public float Magnitude = 1f;
        public bool ApplyToCaster;
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Ability Definition", fileName = "NewAbilityDefinition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [Min(0f)] public float Cost = 0f;
        [Min(0)] public int Cooldown = 0;
        public ActionSpeedTier CastSpeed = ActionSpeedTier.Normal;
        public AbilityTargetingRule TargetingRule = AbilityTargetingRule.SingleTarget;
        public List<AbilityEffectDefinition> EffectList = new();
    }
}
