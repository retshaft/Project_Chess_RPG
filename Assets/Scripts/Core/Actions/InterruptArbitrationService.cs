using System;
using CheckmateRPG.Core;

namespace CheckmateRPG.Core.Actions
{
    public sealed class InterruptArbitrationService
    {
        public bool CanInterrupt(IReadOnlyActionState target)
        {
            if (target == null)
                return false;

            return ActionStateMachine.CanInterrupt(target.State, target.InterruptWindow);
        }

        public bool ShouldInterrupt(
            InterruptPriority sourcePriority,
            IActionCommand sourceAction,
            IReadOnlyActionState targetAction)
        {
            if (targetAction == null)
                return false;

            InterruptPriority targetPriority = targetAction.InterruptPriority;
            int priorityCompare = sourcePriority.CompareTo(targetPriority);
            if (priorityCompare > 0)
                return true;
            if (priorityCompare < 0)
                return false;

            return CompareActionOrder(sourceAction, targetAction) < 0;
        }

        public bool IsIncomingRequestHigher(
            PendingInterruptRequest currentWinner,
            PendingInterruptRequest incoming)
        {
            int priorityCompare = incoming.Priority.CompareTo(currentWinner.Priority);
            if (priorityCompare != 0)
                return priorityCompare > 0;

            int speedCompare = CompareSpeed(incoming.SourceSpeedTier, currentWinner.SourceSpeedTier);
            if (speedCompare != 0)
                return speedCompare < 0;

            int tickCompare = incoming.ScheduledTick.CompareTo(currentWinner.ScheduledTick);
            if (tickCompare != 0)
                return tickCompare < 0;

            int sourceCompare = incoming.SourceActionId.CompareTo(currentWinner.SourceActionId);
            if (sourceCompare != 0)
                return sourceCompare < 0;

            return incoming.TargetActionId.CompareTo(currentWinner.TargetActionId) < 0;
        }

        public int ComparePendingRequestOrder(PendingInterruptRequest left, PendingInterruptRequest right)
        {
            int priorityCompare = right.Priority.CompareTo(left.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            int speedCompare = CompareSpeed(left.SourceSpeedTier, right.SourceSpeedTier);
            if (speedCompare != 0)
                return speedCompare;

            int tickCompare = left.ScheduledTick.CompareTo(right.ScheduledTick);
            if (tickCompare != 0)
                return tickCompare;

            int targetCompare = left.TargetActionId.CompareTo(right.TargetActionId);
            if (targetCompare != 0)
                return targetCompare;

            return left.SourceActionId.CompareTo(right.SourceActionId);
        }

        public PendingInterruptRequest BuildRequest(
            Guid sourceActionId,
            Guid targetActionId,
            InterruptPriority fallbackPriority,
            int currentTick,
            IActionCommand sourceAction)
        {
            InterruptPriority priority = sourceAction != null
                ? sourceAction.InterruptPriority
                : fallbackPriority;
            ActionSpeedTier sourceSpeedTier = sourceAction?.SpeedTier ?? ActionSpeedTier.Normal;
            int scheduledTick = sourceAction?.StartTick ?? currentTick;

            return new PendingInterruptRequest(
                sourceActionId,
                targetActionId,
                priority,
                sourceSpeedTier,
                scheduledTick,
                currentTick);
        }

        public PendingInterruptRequest BuildFallbackRequest(IReadOnlyActionState targetAction, int currentTick)
        {
            if (targetAction == null)
            {
                return new PendingInterruptRequest(
                    Guid.Empty,
                    Guid.Empty,
                    InterruptPriority.Normal,
                    ActionSpeedTier.Normal,
                    currentTick,
                    currentTick);
            }

            return new PendingInterruptRequest(
                Guid.Empty,
                targetAction.ActionId,
                InterruptPriority.Normal,
                targetAction.SpeedTier,
                targetAction.StartTick,
                currentTick);
        }

        private static int CompareActionOrder(IActionCommand sourceAction, IReadOnlyActionState targetAction)
        {
            ActionSpeedTier sourceSpeedTier = sourceAction?.SpeedTier ?? ActionSpeedTier.Normal;
            int speedCompare = CompareSpeed(sourceSpeedTier, targetAction.SpeedTier);
            if (speedCompare != 0)
                return speedCompare;

            int sourceTick = sourceAction?.StartTick ?? int.MaxValue;
            int tickCompare = sourceTick.CompareTo(targetAction.StartTick);
            if (tickCompare != 0)
                return tickCompare;

            Guid sourceActionId = sourceAction?.ActionId ?? Guid.Empty;
            return sourceActionId.CompareTo(targetAction.ActionId);
        }

        private static int CompareSpeed(ActionSpeedTier left, ActionSpeedTier right)
        {
            // ActionSpeedTier enum is ordered from fastest (0) to slowest (4).
            return ((int)left).CompareTo((int)right);
        }
    }

    public readonly record struct PendingInterruptRequest(
        Guid SourceActionId,
        Guid TargetActionId,
        InterruptPriority Priority,
        ActionSpeedTier SourceSpeedTier,
        int ScheduledTick,
        int RequestedTick);
}
