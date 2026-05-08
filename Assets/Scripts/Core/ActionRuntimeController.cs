using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Data;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public sealed class ActionRuntimeController : MonoBehaviour
    {
        private const float DefaultActionSpeed = 1f;
        private const int DefaultRecoveryTicks = 1;

        public static ActionRuntimeController Instance { get; private set; }

        private readonly Dictionary<Guid, UnitBrain> _unitsById = new();
        private readonly EventBus _eventBus = new();
        private ActionScheduler _scheduler;

        public ActionScheduler Scheduler => _scheduler;
        public IEventBus EventBus => _eventBus;

        public static ActionRuntimeController EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("ActionRuntimeController");
            return go.AddComponent<ActionRuntimeController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _scheduler = new ActionScheduler(_eventBus);
            _eventBus.Subscribe<ActionCompletedEvent>(HandleActionCompleted);
            _eventBus.Subscribe<ActionInterruptedEvent>(HandleActionInterrupted);
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            _eventBus.Unsubscribe<ActionCompletedEvent>(HandleActionCompleted);
            _eventBus.Unsubscribe<ActionInterruptedEvent>(HandleActionInterrupted);
            Instance = null;
        }

        private void FixedUpdate()
        {
            _scheduler.AdvanceTick();
            IReadOnlyList<IActionCommand> ready = _scheduler.DrainResolveQueue();
            for (int i = 0; i < ready.Count; i++)
                ResolveAction(ready[i]);
            _eventBus.ProcessQueue();
        }

        public void RegisterUnit(UnitBrain unit)
        {
            if (unit == null || unit.ActorId == Guid.Empty)
                return;

            _unitsById[unit.ActorId] = unit;
            SyncRuntimeState(unit);
        }

        public void UnregisterUnit(UnitBrain unit)
        {
            if (unit == null || unit.ActorId == Guid.Empty)
                return;

            _unitsById.Remove(unit.ActorId);
        }

        public bool TryEnqueueMove(UnitBrain actor, Vector2Int destination)
        {
            if (!CanQueueAction(actor))
                return false;
            if (actor.Movement == null || !actor.Movement.CanReachCell(destination))
                return false;

            IActionCommand command = CreateMoveCommand(actor, destination);
            _scheduler.ScheduleAction(command);
            BindQueuedAction(actor, command);
            return true;
        }

        public bool TryEnqueueAttack(UnitBrain actor, GameObject target)
        {
            if (!CanQueueAction(actor))
                return false;
            if (target == null || actor.Combat == null || !actor.Combat.CanAttack)
                return false;
            if (!TryGetActorId(target, out Guid targetId))
                return false;

            IActionCommand command = CreateAttackCommand(actor, targetId);
            _scheduler.ScheduleAction(command);
            BindQueuedAction(actor, command);
            return true;
        }

        public void SyncRuntimeState(UnitBrain unit)
        {
            if (unit == null || unit.RuntimeState == null)
                return;

            UnitRuntimeState state = unit.RuntimeState;
            state.UnitId = unit.ActorId;
            if (unit.Health != null)
                state.HP = Mathf.RoundToInt(unit.Health.CurrentHealth);
            if (unit.StatusEffects != null)
                state.SP = Mathf.RoundToInt(unit.StatusEffects.CurrentSp);
            if (unit.Movement != null)
                state.Position = unit.Movement.GridPosition;

            UnitStatusFlags flags = UnitStatusFlags.None;
            if (unit.IsDead)
                flags |= UnitStatusFlags.Dead;
            if (unit.Movement != null && unit.StatusEffects != null && !unit.StatusEffects.CanMove)
                flags |= UnitStatusFlags.MoveLocked;
            if (unit.Combat != null && unit.StatusEffects != null && !unit.StatusEffects.CanAttack)
                flags |= UnitStatusFlags.AttackLocked;
            state.StatusFlags = flags;
        }

        private bool CanQueueAction(UnitBrain actor)
        {
            if (actor == null || actor.IsDead || actor.RuntimeState == null)
                return false;

            SyncRuntimeState(actor);
            if (actor.RuntimeState.CurrentActionId.HasValue)
                return false;

            return _scheduler.CurrentTick >= actor.RuntimeState.RecoveryUntilTick;
        }

        private IActionCommand CreateMoveCommand(UnitBrain actor, Vector2Int destination)
        {
            int startTick = _scheduler.CurrentTick + 1;
            return new MoveActionCommand(
                actor.ActorId,
                actor.Movement != null ? actor.Movement.GridPosition : actor.RuntimeState.Position,
                destination,
                startTick,
                ToSpeedTier(actor.UnitData != null ? actor.UnitData.ActionSpeed : DefaultActionSpeed),
                DefaultRecoveryTicks);
        }

        private IActionCommand CreateAttackCommand(UnitBrain actor, Guid targetId)
        {
            int startTick = _scheduler.CurrentTick + 1;
            int damage = actor.UnitData != null ? Mathf.RoundToInt(actor.UnitData.AttackDamage) : 0;
            return new AttackActionCommand(
                actor.ActorId,
                targetId,
                damage,
                isCritical: false,
                startTick,
                ToSpeedTier(actor.UnitData != null ? actor.UnitData.ActionSpeed : DefaultActionSpeed),
                DefaultRecoveryTicks);
        }

        private void BindQueuedAction(UnitBrain actor, IActionCommand command)
        {
            UnitRuntimeState state = actor.RuntimeState;
            state.UnitId = actor.ActorId;
            state.CurrentActionId = command.ActionId;
            state.RecoveryUntilTick = command.RecoveryEndTick;
        }

        private void ResolveAction(IActionCommand action)
        {
            if (!_unitsById.TryGetValue(action.ActorId, out UnitBrain actor) || actor == null)
                return;

            SyncRuntimeState(actor);
        }

        private void HandleActionCompleted(ActionCompletedEvent actionCompletedEvent)
        {
            ActionLifecyclePayload payload = actionCompletedEvent.Payload;
            if (!_unitsById.TryGetValue(payload.ActorId, out UnitBrain actor) || actor == null || actor.RuntimeState == null)
                return;

            UnitRuntimeState state = actor.RuntimeState;
            if (state.CurrentActionId == payload.ActionId)
                state.CurrentActionId = null;
            state.RecoveryUntilTick = Mathf.Max(state.RecoveryUntilTick, payload.RecoveryEndTick);
            SyncRuntimeState(actor);
        }

        private void HandleActionInterrupted(ActionInterruptedEvent actionInterruptedEvent)
        {
            ActionLifecyclePayload payload = actionInterruptedEvent.Payload;
            if (!_unitsById.TryGetValue(payload.ActorId, out UnitBrain actor) || actor == null || actor.RuntimeState == null)
                return;

            UnitRuntimeState state = actor.RuntimeState;
            if (state.CurrentActionId == payload.ActionId)
                state.CurrentActionId = null;
            state.RecoveryUntilTick = payload.SchedulerTick;
            SyncRuntimeState(actor);
        }

        private static ActionSpeedTier ToSpeedTier(float actionSpeed)
        {
            if (actionSpeed >= 1.6f)
                return ActionSpeedTier.VeryFast;
            if (actionSpeed >= 1.25f)
                return ActionSpeedTier.Fast;
            if (actionSpeed <= 0.6f)
                return ActionSpeedTier.VerySlow;
            if (actionSpeed <= 0.85f)
                return ActionSpeedTier.Slow;
            return ActionSpeedTier.Normal;
        }

        private static bool TryGetActorId(GameObject target, out Guid actorId)
        {
            actorId = Guid.Empty;
            if (target == null || !target.TryGetComponent(out UnitBrain brain))
                return false;

            actorId = brain.ActorId;
            return actorId != Guid.Empty;
        }
    }
}
