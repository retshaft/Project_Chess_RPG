using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public readonly record struct ActionResolutionResult
    {
        public ActionResolutionResult(
            bool success,
            IReadOnlyList<IRuntimeMutation> runtimeMutations,
            IReadOnlyList<IGameEvent> events)
        {
            Success = success;
            RuntimeMutations = runtimeMutations ?? Array.Empty<IRuntimeMutation>();
            Events = events ?? Array.Empty<IGameEvent>();
        }

        public bool Success { get; }
        public IReadOnlyList<IRuntimeMutation> RuntimeMutations { get; }
        public IReadOnlyList<IGameEvent> Events { get; }

        public static ActionResolutionResult Failed() =>
            new(false, Array.Empty<IRuntimeMutation>(), Array.Empty<IGameEvent>());
    }
}
