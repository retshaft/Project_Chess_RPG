using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// Action speed tiers converted to action-duration ticks (100 ms per tick).
    /// </summary>
    public enum ActionSpeedTier
    {
        VeryFast,
        Fast,
        Normal,
        Slow,
        VerySlow
    }

    public static class ActionTimelineFormula
    {
        public const int TickMilliseconds = 100;

        public static int ToActionDurationTicks(ActionSpeedTier speedTier)
        {
            return speedTier switch
            {
                ActionSpeedTier.VeryFast => 7,
                ActionSpeedTier.Fast => 10,
                ActionSpeedTier.Normal => 15,
                ActionSpeedTier.Slow => 20,
                ActionSpeedTier.VerySlow => 25,
                _ => 15
            };
        }
    }

    /// <summary>
    /// Canonical action timeline definition.
    /// All values are expressed in ticks (100 ms).
    /// </summary>
    public readonly record struct ActionTimelineDefinition(
        int ActionDuration,
        int ResolveTiming,
        int RecoveryTiming,
        int InterruptWindow)
    {
        public int ResolveTickOffset => ResolveTiming;
        public int RecoveryEndTickOffset => ResolveTiming + RecoveryTiming;

        public void Validate()
        {
            if (ActionDuration <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(ActionDuration), "ActionDuration must be greater than 0.");

            if (ResolveTiming < 0)
                throw new System.ArgumentOutOfRangeException(nameof(ResolveTiming), "ResolveTiming must be 0 or greater.");

            if (RecoveryTiming < 0)
                throw new System.ArgumentOutOfRangeException(nameof(RecoveryTiming), "RecoveryTiming must be 0 or greater.");

            if (InterruptWindow < 0)
                throw new System.ArgumentOutOfRangeException(nameof(InterruptWindow), "InterruptWindow must be 0 or greater.");

            if (ResolveTiming > ActionDuration)
                throw new System.ArgumentOutOfRangeException(nameof(ResolveTiming), "ResolveTiming must not exceed ActionDuration.");

            if (RecoveryEndTickOffset > ActionDuration)
                throw new System.ArgumentOutOfRangeException(nameof(RecoveryTiming), "ResolveTiming + RecoveryTiming must not exceed ActionDuration.");

            if (InterruptWindow > ActionDuration)
                throw new System.ArgumentOutOfRangeException(nameof(InterruptWindow), "InterruptWindow must not exceed ActionDuration.");
        }
    }

    public enum ActionCommandState
    {
        Queued,
        Executing,
        Resolving,
        Recovery,
        Completed,
        Cancelled,
        Interrupted
    }

    public interface IActionCommand
    {
        string ActionId { get; }
        string ActorId { get; }
        IReadOnlyList<string> Targets { get; }
        int QueuedTick { get; }
        int StartTick { get; }
        int ResolveTick { get; }
        int RecoveryEndTick { get; }
        ActionCommandState State { get; }
    }

    public abstract record ActionCommandBase(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State) : IActionCommand;

    public sealed record MoveAction(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State)
        : ActionCommandBase(ActionId, ActorId, Targets, QueuedTick, StartTick, ResolveTick, RecoveryEndTick, State);

    public sealed record BasicAttackAction(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State)
        : ActionCommandBase(ActionId, ActorId, Targets, QueuedTick, StartTick, ResolveTick, RecoveryEndTick, State);

    public sealed record AbilityAction(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State)
        : ActionCommandBase(ActionId, ActorId, Targets, QueuedTick, StartTick, ResolveTick, RecoveryEndTick, State);
}
