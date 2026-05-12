using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Actions
{
    public readonly record struct ActionCostReservationHooks(
        Func<bool> CanReserve,
        Action<int, Guid> Commit,
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
                if (availableAp + Mathf.Epsilon < cost.APCost)
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
                return true;

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
