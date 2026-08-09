using System;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct KnockbackMutation(
        Guid MutationId,
        Guid TargetId,
        Vector2Int Direction,
        int Force,
        bool ApplySplatDamage = true,
        MutationContext Context = default) : IRuntimeMutation;
}
