using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Actions
{
    /// <summary>
    /// Optional callbacks bound to a reservation for non-AP/SP side effects (e.g. ability cooldown state).
    /// </summary>
    public readonly record struct ActionCostReservationHooks(
        /// <summary>Called during reservation to validate whether side-effect reservation is allowed.</summary>
        Func<bool> CanReserve,
        /// <summary>Called when reserved costs are committed right before resolve.</summary>
        Action<int, Guid> Commit,
        /// <summary>Called when reservation is rolled back due to cancellation/interruption.</summary>
        Action<Guid> Rollback)
    {
        public static ActionCostReservationHooks Empty => new(null, null, null);
    }

    public sealed class ActionCostReservation
    {
        private readonly Dictionary<Guid, ReservationEntry> _reservations = new();
        private readonly Dictionary<Guid, int> _reservedSpByActor = new();
        private float _reservedAp;

        public bool CanAfford(Guid actorId, ActionCostBreakdown cost, Func<Guid, int> getActorSp)
        {
            if (actorId == Guid.Empty)
                return false;

            if (cost.APCost > 0f)
            {
                if (APManager.Instance == null)
                    return false;

                float availableAp = Mathf.Max(0f, APManager.Instance.CurrentAP - _reservedAp);
                if (availableAp + ComparisonTolerance < cost.APCost)
                    return false;
            }

            if (cost.SPCost > 0)
            {
                if (getActorSp == null)
                    return false;

                int currentSp = getActorSp(actorId);
                int alreadyReserved = _reservedSpByActor.TryGetValue(actorId, out int reservedSp) ? reservedSp : 0;
                if (currentSp - alreadyReserved < cost.SPCost)
                    return false;
            }

            return true;
        }

        public bool ReserveCost(Guid actionId, Guid actorId, ActionCostBreakdown cost, ActionCostReservationHooks hooks)
        {
            if (actionId == Guid.Empty || actorId == Guid.Empty)
                return false;
            if (_reservations.ContainsKey(actionId))
                return false;
            if (hooks.CanReserve != null && !hooks.CanReserve())
                return false;

            _reservations[actionId] = new ReservationEntry(actorId, cost, hooks);
            _reservedAp += Mathf.Max(0f, cost.APCost);

            if (cost.SPCost > 0)
                _reservedSpByActor[actorId] = (_reservedSpByActor.TryGetValue(actorId, out int reserved) ? reserved : 0) + cost.SPCost;

            return true;
        }

        public bool Commit(Guid actionId, int currentTick)
        {
            if (!_reservations.TryGetValue(actionId, out ReservationEntry entry))
                return false;

            if (entry.Cost.APCost > 0f)
            {
                if (APManager.Instance == null)
                    return false;
                if (!APManager.Instance.TrySpend(new ActionPointCost(entry.Cost.APCost, entry.Cost.APReason), out _))
                    return false;
            }

            entry.Hooks.Commit?.Invoke(currentTick, actionId);
            ReleaseInternal(actionId, entry);
            return true;
        }

        public bool Rollback(Guid actionId)
        {
            if (!_reservations.TryGetValue(actionId, out ReservationEntry entry))
                return false;

            entry.Hooks.Rollback?.Invoke(actionId);
            ReleaseInternal(actionId, entry);
            return true;
        }

        private void ReleaseInternal(Guid actionId, ReservationEntry entry)
        {
            _reservations.Remove(actionId);

            if (entry.Cost.APCost > 0f)
                _reservedAp = Mathf.Max(0f, _reservedAp - entry.Cost.APCost);

            if (entry.Cost.SPCost > 0 && _reservedSpByActor.TryGetValue(entry.ActorId, out int reserved))
            {
                int updated = Math.Max(0, reserved - entry.Cost.SPCost);
                if (updated == 0)
                    _reservedSpByActor.Remove(entry.ActorId);
                else
                    _reservedSpByActor[entry.ActorId] = updated;
            }
        }

        private sealed class ReservationEntry
        {
            public ReservationEntry(Guid actorId, ActionCostBreakdown cost, ActionCostReservationHooks hooks)
            {
                ActorId = actorId;
                Cost = cost;
                Hooks = hooks;
            }

            public Guid ActorId { get; }
            public ActionCostBreakdown Cost { get; }
            public ActionCostReservationHooks Hooks { get; }
        }
    }
}
        private const float ComparisonTolerance = 0.0001f;
