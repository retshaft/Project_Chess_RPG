using System;

namespace CheckmateRPG.Core.Actions
{
    public readonly record struct ActionInterruptState(
        InterruptPriority CurrentInterruptPriority,
        InterruptWindow CurrentInterruptWindow,
        InterruptPriority InterruptProtection,
        Guid InterruptSource);
}
