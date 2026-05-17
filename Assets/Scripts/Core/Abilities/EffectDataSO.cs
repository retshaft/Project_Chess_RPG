using UnityEngine;

namespace CheckmateRPG.Core.Abilities
{
    public enum ElementType
    {
        Physical,
        Frost,
        Nature,
        Magic,
        None
    }

    public enum EffectType
    {
        Damage,
        Heal,
        Buff,
        Dot,
        // Crowd-control category (e.g., freeze, poison-linked disable, virus-type impairments).
        Cc
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Ability Pipeline/Effect Data", fileName = "NewEffectData")]
    public sealed class EffectDataSO : ScriptableObject
    {
        public EffectType Type = EffectType.Damage;
        public ElementType Element = ElementType.None;
        public bool AppliesStatusEffect;
        public CheckmateRPG.Core.StatusEffectType StatusEffect = CheckmateRPG.Core.StatusEffectType.Stagger;
        public bool IsPhysicalCC;
        public bool IsHidden;
        [Min(0f)] public float BaseValue = 0f;
        [Min(0)] public int DurationTicks = 0;
        [Min(1)] public int MaxStacks = 1;
    }
}
