using System;
using UnityEngine;
using CheckmateRPG.Components;

namespace CheckmateRPG.Core.Passives
{
    public interface IPassiveEffect
    {
        void Execute(Guid actorId, Guid targetId, int currentTick);
    }

    [Serializable]
    public class ApplyBuffPassiveEffect : IPassiveEffect
    {
        [Tooltip("부여할 StatModifierProfile의 ID")]
        public string StatModifierProfileId;

        [Tooltip("해당 레벨에서 특정 스탯 수치만 덮어씌울 경우 추가")]
        public System.Collections.Generic.List<CheckmateRPG.Core.StatModifiers.StatModifierOverride> ValueOverrides = new();

        public void Execute(Guid actorId, Guid targetId, int currentTick)
        {
            if (string.IsNullOrWhiteSpace(StatModifierProfileId)) return;

            var profiles = Resources.LoadAll<CheckmateRPG.Core.StatModifiers.StatModifierProfileSO>("StatModifiers");
            foreach (var profile in profiles)
            {
                if (profile.EffectId == StatModifierProfileId)
                {
                    if (ActionRuntimeController.Instance.TryGetUnitBrain(actorId, out var brain))
                    {
                        var modComp = brain.GetComponent<CheckmateRPG.Core.StatModifiers.UnitStatModifierComponent>();
                        if (modComp != null)
                        {
                            var finalModifiers = profile.Modifiers;
                            
                            // 오버라이드 덮어쓰기
                            if (ValueOverrides != null && ValueOverrides.Count > 0)
                            {
                                finalModifiers = new System.Collections.Generic.List<CheckmateRPG.Core.StatModifiers.StatModifierEntry>(profile.Modifiers.Count);
                                for (int i = 0; i < profile.Modifiers.Count; i++)
                                {
                                    var entry = profile.Modifiers[i];
                                    float val = entry.Value;
                                    
                                    foreach (var over in ValueOverrides)
                                    {
                                        if (over.TargetType == entry.Type)
                                        {
                                            val = over.OverriddenValue;
                                            break;
                                        }
                                    }
                                    
                                    finalModifiers.Add(new CheckmateRPG.Core.StatModifiers.StatModifierEntry
                                    {
                                        Type = entry.Type,
                                        Value = val,
                                        ConsumptionPolicy = entry.ConsumptionPolicy
                                    });
                                }
                            }
                            
                            modComp.ApplyModifiers(profile.EffectId, finalModifiers, 1);
                        }
                    }
                    break;
                }
            }
        }
    }

    [Serializable]
    public class ApplyStatusPassiveEffect : IPassiveEffect
    {
        [Tooltip("타겟에게 부여할 상태 이상 시스템의 Effect ID (예: Bleed)")]
        public string EffectId;
        public int DurationTicks = 2;
        public int StackCount = 1;
        public float Magnitude = 300f;

        [Tooltip("해당 레벨에서의 지속 시간 (0이면 원본 사용)")]
        public int OverrideDurationTicks = 0;

        [Tooltip("해당 레벨에서의 틱당 대미지 크기 (0이면 원본 사용)")]
        public float OverrideMagnitude = 0f;

        public void Execute(Guid actorId, Guid targetId, int currentTick)
        {
            if (string.IsNullOrWhiteSpace(EffectId) || currentTick < 0) return;
            ActionRuntimeController.Instance.ApplyEffectToUnit(
                targetId, 
                EffectId, 
                actorId, 
                OverrideDurationTicks > 0 ? OverrideDurationTicks : DurationTicks, 
                StackCount, 
                OverrideMagnitude > 0f ? OverrideMagnitude : Magnitude);
        }
    }

    [Serializable]
    public class ReduceCooldownPassiveEffect : IPassiveEffect
    {
        [Tooltip("자신의 평타/스킬 쿨다운 감소 (초)")]
        [Min(0f)] public float CooldownSeconds;

        public void Execute(Guid actorId, Guid targetId, int currentTick)
        {
            if (CooldownSeconds <= 0f) return;
            ActionRuntimeController.Instance.ReduceCooldown(actorId, string.Empty, CooldownSeconds);
        }
    }
}
