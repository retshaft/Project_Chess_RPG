namespace CheckmateRPG.Core.Actions
{
    public enum ActionAdmissionStatus
    {
        Scheduled,
        Deferred,
        Rejected
    }

    public enum ActionAdmissionRejectionReason
    {
        None,
        InvalidAction,
        DuplicateAction,
        InvalidTimeline,
        CastLocked,
        RecoveryLocked,
        MovementLocked,
        GlobalLocked,
        ReplaceNotAllowed,
        ParallelNotAllowed
    }

    public readonly record struct ActionAdmissionResult(
        ActionAdmissionStatus Status,
        ActionAdmissionRejectionReason Reason,
        ActionLockType CurrentLock)
    {
        public static ActionAdmissionResult Scheduled() => new(ActionAdmissionStatus.Scheduled, ActionAdmissionRejectionReason.None, ActionLockType.None);
        public static ActionAdmissionResult Deferred(ActionLockType currentLock) => new(ActionAdmissionStatus.Deferred, ActionAdmissionRejectionReason.None, currentLock);
        public static ActionAdmissionResult Rejected(ActionAdmissionRejectionReason reason, ActionLockType currentLock) => new(ActionAdmissionStatus.Rejected, reason, currentLock);
    }
}
