using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Actions
{
    public enum InterruptArbitrationReason
    {
        HigherInterruptPriority,
        HigherActionSpeedLevel,
        EarlierScheduledTick,
        DeterministicActionIdOrdering
    }

    public readonly record struct InterruptResult(
        Guid WinningAction,
        IReadOnlyList<Guid> InterruptedActions,
        IReadOnlyList<Guid> IgnoredInterrupts,
        InterruptArbitrationReason Reason)
    {
        public static InterruptResult Empty =>
            new(Guid.Empty, Array.Empty<Guid>(), Array.Empty<Guid>(), InterruptArbitrationReason.DeterministicActionIdOrdering);
    }
}
