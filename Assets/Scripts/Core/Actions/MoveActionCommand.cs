using System;
using UnityEngine;

namespace CheckmateRPG.Core.Actions
{
    public sealed class MoveActionCommand : BaseActionCommand
    {
        public MoveActionCommand(
            Guid actorId,
            Vector2Int from,
            Vector2Int to,
            int startTick,
            ActionSpeedTier speedTier,
            int recoveryDurationTicks = 1,
            bool isInterruptible = true,
            ActionLockType intentLockType = ActionLockType.MovementLock,
            ActionConcurrencyPolicy concurrencyPolicy = ActionConcurrencyPolicy.Reject)
            : base(
                actorId,
                startTick,
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier),
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier) + Math.Max(0, recoveryDurationTicks),
                speedTier,
                isInterruptible,
                intentLockType: intentLockType,
                concurrencyPolicy: concurrencyPolicy)
        {
            From = from;
            To = to;
        }

        public Vector2Int From { get; }
        public Vector2Int To { get; }
    }
}
