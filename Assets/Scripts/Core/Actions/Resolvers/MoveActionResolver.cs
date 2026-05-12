using System;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public sealed class MoveActionResolver : IActionResolver<MoveActionCommand>
    {
        public ActionResolutionResult Resolve(
            MoveActionCommand action,
            IBattleContext battleContext)
        {
            if (action == null || battleContext == null)
                return ActionResolutionResult.Failed();
            if (!battleContext.TryGetUnit(action.ActorId, out BattleUnitSnapshot actor))
                return ActionResolutionResult.Failed();
            if ((actor.StatusFlags & UnitStatusFlags.Dead) != 0)
                return ActionResolutionResult.Failed();
            if ((actor.StatusFlags & UnitStatusFlags.MoveLocked) != 0)
                return ActionResolutionResult.Failed();
            if (actor.Position != action.From)
                return ActionResolutionResult.Failed();
            if (!battleContext.IsCellValid(action.To))
                return ActionResolutionResult.Failed();

            MovementMutation mutation = new(SeededRandomProvider.Shared.NextGuid(), action.ActorId, action.From, action.To);
            MoveActionResolvedEvent resolvedEvent = new(
                new MoveActionResolvedPayload(action.ActionId, action.ActorId, action.From, action.To),
                action.ActionId.ToString("N"),
                action.ActorId.ToString("N"));

            return new ActionResolutionResult(
                true,
                new IRuntimeMutation[] { mutation },
                new IGameEvent[] { resolvedEvent });
        }
    }
}
