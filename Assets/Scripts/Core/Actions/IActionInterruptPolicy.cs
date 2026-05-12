namespace CheckmateRPG.Core.Actions
{
    public interface IActionInterruptPolicy
    {
        bool CanBeInterrupted(IReadOnlyActionState targetAction);
        bool CanInterruptOthers(IReadOnlyActionState sourceAction);
    }
}
