using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Simulation;

namespace CheckmateRPG.Core
{
    public enum ReactionTriggerEventType
    {
        OnDamaged,
        OnMoveCompleted,
        OnAbilityResolved,
        OnUnitKilled
    }

    public readonly record struct ReactionEvaluationContext(
        int CurrentTick,
        IReadOnlySimulationRuntime Runtime,
        IGameEvent TriggerEvent);

    public readonly record struct ReactionExecutionPlan(
        string ReactionId,
        ActionSpeedTier SourceActionSpeedLevel,
        IReadOnlyList<IRuntimeMutation> Mutations,
        IReadOnlyList<IGameEvent> Events);

    /// <summary>
    /// Event-driven reaction trigger contract.
    /// Implementations must not mutate runtime directly.
    /// </summary>
    public interface IReactionTrigger
    {
        string ReactionId { get; }
        ReactionTriggerEventType TriggerType { get; }
        bool Supports(IGameEvent gameEvent);
        bool TryBuildReaction(IGameEvent gameEvent, ReactionEvaluationContext context, out ReactionExecutionPlan plan);
    }
}
