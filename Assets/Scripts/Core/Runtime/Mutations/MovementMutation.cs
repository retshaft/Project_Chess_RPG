using System;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct MovementMutation(
        Guid MutationId,
        Guid TargetId,
        Vector2Int From,
        Vector2Int To,
        MutationContext Context = default,
        bool UseDirectDestinationResolution = false) : IRuntimeMutation;
}
