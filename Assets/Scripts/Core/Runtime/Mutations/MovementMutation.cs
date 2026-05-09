using System;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct MovementMutation(Guid UnitId, Vector2Int From, Vector2Int To) : IRuntimeMutation;
}
