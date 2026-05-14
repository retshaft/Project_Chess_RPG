using System;
using CheckmateRPG.Core.Runtime.Mutations;
using UnityEngine;

namespace CheckmateRPG.Core.Effects
{
    public sealed class EffectMutationFactory
    {
        public IRuntimeMutation CreateDamage(
            EffectMutationContext context,
            int amount,
            DamageType damageType = DamageType.Magical,
            bool isCritical = false)
        {
            Guid targetId = ResolveTargetId(context);
            if (targetId == Guid.Empty)
                return null;

            return new DamageMutation(
                SeededRandomProvider.Shared.NextGuid(),
                targetId,
                ResolveSourceId(context),
                Mathf.Max(0, amount),
                isCritical,
                BuildContext(context, targetId),
                damageType);
        }

        public IRuntimeMutation CreateHeal(EffectMutationContext context, int amount)
        {
            Guid targetId = ResolveTargetId(context);
            if (targetId == Guid.Empty)
                return null;

            return new HealMutation(
                SeededRandomProvider.Shared.NextGuid(),
                targetId,
                ResolveSourceId(context),
                Mathf.Max(0, amount),
                BuildContext(context, targetId));
        }

        public IRuntimeMutation CreateMove(EffectMutationContext context, Vector2Int from, Vector2Int to)
        {
            Guid targetId = ResolveTargetId(context);
            if (targetId == Guid.Empty)
                return null;

            return new MoveMutation(
                SeededRandomProvider.Shared.NextGuid(),
                targetId,
                from,
                to,
                BuildContext(context, targetId));
        }

        public IRuntimeMutation CreateResource(
            EffectMutationContext context,
            ResourceMutationType resourceType,
            int delta)
        {
            Guid targetId = ResolveTargetId(context);
            if (targetId == Guid.Empty)
                return null;

            return new ResourceMutation(
                SeededRandomProvider.Shared.NextGuid(),
                targetId,
                resourceType,
                delta,
                context.Reason ?? string.Empty,
                BuildContext(context, targetId));
        }

        public IRuntimeMutation CreateReservation(
            EffectMutationContext context,
            string reservationKey,
            ReservationMutationOperation operation,
            string scope = "")
        {
            Guid targetId = ResolveTargetId(context);
            if (targetId == Guid.Empty || string.IsNullOrWhiteSpace(reservationKey))
                return null;

            return new ReservationMutation(
                SeededRandomProvider.Shared.NextGuid(),
                targetId,
                reservationKey,
                operation,
                context.Tick,
                scope ?? string.Empty,
                BuildContext(context, targetId));
        }

        private static MutationContext BuildContext(EffectMutationContext context, Guid targetId)
        {
            return new MutationContext(
                context.Tick,
                Guid.Empty,
                targetId,
                context.Reason ?? string.Empty);
        }

        private static Guid ResolveSourceId(EffectMutationContext context)
        {
            if (context.SourceUnit.Exists && context.SourceUnit.UnitId != Guid.Empty)
                return context.SourceUnit.UnitId;

            return context.SourceEffect?.SourceId ?? Guid.Empty;
        }

        private static Guid ResolveTargetId(EffectMutationContext context)
        {
            if (context.TargetUnit.Exists && context.TargetUnit.UnitId != Guid.Empty)
                return context.TargetUnit.UnitId;

            return context.SourceEffect?.TargetId ?? Guid.Empty;
        }
    }
}
