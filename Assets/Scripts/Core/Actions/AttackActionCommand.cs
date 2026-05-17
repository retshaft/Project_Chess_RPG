using System;

namespace CheckmateRPG.Core.Actions
{
    public sealed class AttackActionCommand : BaseActionCommand
    {
        public AttackActionCommand(
            Guid actorId,
            Guid targetId,
            int damage,
            bool isCritical,
            int startTick,
            ActionSpeedTier speedTier,
            int recoveryDurationTicks = 1,
            bool isInterruptible = true,
            ActionDefinition? definition = null,
            ActionLockType intentLockType = ActionLockType.CastLock,
            ActionConcurrencyPolicy concurrencyPolicy = ActionConcurrencyPolicy.Reject,
            int spGain = 0)
            : base(
                actorId,
                startTick,
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier),
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier) + Math.Max(0, recoveryDurationTicks),
                speedTier,
                isInterruptible,
                definition: definition,
                intentLockType: intentLockType,
                concurrencyPolicy: concurrencyPolicy)
        {
            if (targetId == Guid.Empty)
                throw new ArgumentException("TargetId must not be empty.", nameof(targetId));

            TargetId = targetId;
            Damage = damage;
            IsCritical = isCritical;
            SPGain = Math.Max(0, spGain);
        }

        public Guid TargetId { get; }
        public int Damage { get; }
        public bool IsCritical { get; }
        public int SPGain { get; }
    }
}
