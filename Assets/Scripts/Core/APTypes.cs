using UnityEngine;

namespace CheckmateRPG.Core
{
    public enum APChangeReason
    {
        Initialization,
        Regen,
        Spend,
        Refund,
        Bonus,
        MaxChanged,
        Manual
    }

    public enum APActionReason
    {
        Move,
        Attack,
        Skill,
        System
    }

    public enum APSource
    {
        Regen,
        Refund,
        Bonus,
        Scripted,
        Debug
    }

    public enum APRegenPauseReason
    {
        None,
        StatusEffect,
        Cutscene,
        Manual,
        Debug
    }

    public readonly struct ActionPointCost
    {
        public float Amount { get; }
        public APActionReason Reason { get; }

        public ActionPointCost(float amount, APActionReason reason = APActionReason.System)
        {
            Amount = Mathf.Max(0f, amount);
            Reason = reason;
        }
    }
}
