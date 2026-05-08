using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Actions
{
    public sealed class AbilityActionCommand : BaseActionCommand
    {
        public AbilityActionCommand(
            Guid actorId,
            string abilityId,
            IReadOnlyList<Guid> targetIds,
            int startTick,
            ActionSpeedTier speedTier,
            int recoveryDurationTicks = 1,
            bool isInterruptible = true)
            : base(
                actorId,
                startTick,
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier),
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier) + Math.Max(0, recoveryDurationTicks),
                isInterruptible)
        {
            AbilityId = abilityId ?? string.Empty;
            TargetIds = targetIds ?? Array.Empty<Guid>();
        }

        public string AbilityId { get; }
        public IReadOnlyList<Guid> TargetIds { get; }
    }
}
