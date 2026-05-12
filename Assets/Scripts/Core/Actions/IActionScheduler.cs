using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Actions
{
    public interface IActionScheduler
    {
        ActionAdmissionResult ScheduleAction(IActionCommand action);
        void AdvanceTick();
        void InterruptAction(Guid actionId);
        void InterruptAction(Guid targetActionId, Guid sourceActionId, InterruptPriority priority = InterruptPriority.Normal);
        void CancelAction(Guid actionId);
        IReadOnlyCollection<IActionCommand> GetActiveActions();
    }
}
