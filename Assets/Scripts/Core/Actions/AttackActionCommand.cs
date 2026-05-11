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
            bool isInterruptible = true)
            : base(
                actorId,
                startTick,
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier),
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier) + Math.Max(0, recoveryDurationTicks),
                speedTier,
                isInterruptible)
        {
            if (targetId == Guid.Empty)
                throw new ArgumentException("TargetId must not be empty.", nameof(targetId));

            TargetId = targetId;
            Damage = damage;
            IsCritical = isCritical;
        }

        public Guid TargetId { get; }
        public int Damage { get; }
        public bool IsCritical { get; }
    }
}
