using System;
using System.Collections.Generic;
using CheckmateRPG.Data;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// Runtime orchestrator for command scheduling and action resolution.
    /// Input/AI -> ActionCommand -> Scheduler -> Tick -> Resolve -> EventBus.
    /// </summary>
    public sealed class ActionRuntimeController : MonoBehaviour
    {
        private const string CellEncodingPrefix = "cell:";
        private const float DefaultActionSpeed = 1f;

        public static ActionRuntimeController Instance { get; private set; }

        private readonly Dictionary<string, UnitBrain> _unitsById = new();
        private readonly EventBus _eventBus = new();
        private readonly AbilityExecutionPipeline _abilityExecutionPipeline = new();
        private ActionScheduler _scheduler;
        private TickScheduler _tickScheduler;

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
            _tickScheduler = TickScheduler.EnsureExists();
            _scheduler = new ActionScheduler(_eventBus);
            _eventBus.Subscribe<ActionCompletedEvent>(HandleActionCompleted);
            _tickScheduler.OnTick += HandleTick;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            if (_tickScheduler != null)
                _tickScheduler.OnTick -= HandleTick;
            _eventBus.Unsubscribe<ActionCompletedEvent>(HandleActionCompleted);
            Instance = null;
        }

        private void HandleTick(int tick)
        {
            _scheduler.AdvanceTick();
            IReadOnlyList<IActionCommand> ready = _scheduler.ResolveReadyActions();
            for (int i = 0; i < ready.Count; i++)
                ResolveAction(ready[i]);
            _eventBus.ProcessQueue();
        }

        public void RegisterUnit(UnitBrain unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.ActorId))
                return;

            _unitsById[unit.ActorId] = unit;
            SyncRuntimeState(unit);
        }

        public void UnregisterUnit(UnitBrain unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.ActorId))
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
            IActionCommand queued = _scheduler.ScheduleAction(command);
            BindQueuedAction(actor, queued);
            return true;
        }

        public bool TryEnqueueAttack(UnitBrain actor, GameObject target)
        {
            if (!CanQueueAction(actor))
                return false;

            if (target == null || actor.Combat == null || !actor.Combat.CanAttack)
                return false;

            string targetId = GetActorId(target);
            if (string.IsNullOrEmpty(targetId))
                return false;

            IActionCommand command = CreateBasicAttackCommand(actor, targetId);
            IActionCommand queued = _scheduler.ScheduleAction(command);
            BindQueuedAction(actor, queued);
            return true;
        }

        private bool CanQueueAction(UnitBrain actor)
        {
            if (actor == null || actor.IsDead || actor.RuntimeState == null)
                return false;

            SyncRuntimeState(actor);
            if (!string.IsNullOrEmpty(actor.RuntimeState.CurrentActionId))
                return false;

            return _scheduler.CurrentTick >= actor.RuntimeState.RecoveryUntilTick;
        }

        private IActionCommand CreateMoveCommand(UnitBrain actor, Vector2Int destination)
        {
            int startTick = _scheduler.CurrentTick + 1;
            ActionTimelineDefinition timeline = BuildTimeline(actor.UnitData);
            string actionId = CreateActionId();
            int resolveTick = startTick + timeline.ResolveTickOffset;
            int recoveryEndTick = startTick + timeline.RecoveryEndTickOffset;
            string target = EncodeCell(destination);
            return new MoveAction(
                actionId,
                actor.ActorId,
                new[] { target },
                _scheduler.CurrentTick,
                startTick,
                resolveTick,
                recoveryEndTick,
                ActionCommandState.Queued);
        }

        private IActionCommand CreateBasicAttackCommand(UnitBrain actor, string targetId)
        {
            int startTick = _scheduler.CurrentTick + 1;
            ActionTimelineDefinition timeline = BuildTimeline(actor.UnitData);
            string actionId = CreateActionId();
            int resolveTick = startTick + timeline.ResolveTickOffset;
            int recoveryEndTick = startTick + timeline.RecoveryEndTickOffset;
            return new BasicAttackAction(
                actionId,
                actor.ActorId,
                new[] { targetId },
                _scheduler.CurrentTick,
                startTick,
                resolveTick,
                recoveryEndTick,
                ActionCommandState.Queued);
        }

        private ActionTimelineDefinition BuildTimeline(UnitData unitData)
        {
            ActionSpeedTier speedTier = ToSpeedTier(unitData != null ? unitData.ActionSpeed : DefaultActionSpeed);
            int duration = ActionTimelineFormula.ToActionDurationTicks(speedTier);
            var timeline = new ActionTimelineDefinition(
                duration,
                ResolveTiming: 0,
                RecoveryTiming: duration,
                InterruptWindow: Mathf.Max(1, duration / 2));
            timeline.Validate();
            return timeline;
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

        private static string CreateActionId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private void BindQueuedAction(UnitBrain actor, IActionCommand command)
        {
            UnitRuntimeState state = actor.RuntimeState;
            state.CurrentActionId = command.ActionId;
            state.RecoveryUntilTick = command.RecoveryEndTick;
        }

        private void ResolveAction(IActionCommand action)
        {
            if (!_unitsById.TryGetValue(action.ActorId, out UnitBrain actor) || actor == null || actor.IsDead)
            {
                _scheduler.InterruptAction(action.ActionId);
                return;
            }

            SyncRuntimeState(actor);

            bool resolved = action switch
            {
                MoveAction move => TryResolveMove(actor, move),
                BasicAttackAction attack => TryResolveAttack(actor, attack),
                AbilityAction ability => TryResolveAbility(actor, ability),
                _ => false
            };

            if (!resolved)
                _scheduler.InterruptAction(action.ActionId);
        }

        private bool TryResolveMove(UnitBrain actor, MoveAction action)
        {
            if (actor.Movement == null || action.Targets.Count == 0)
                return false;

            if (!TryDecodeCell(action.Targets[0], out Vector2Int destination))
                return false;

            if (!actor.Movement.CanReachCell(destination))
                return false;

            actor.Movement.MoveTo(destination);
            return true;
        }

        private bool TryResolveAttack(UnitBrain actor, BasicAttackAction action)
        {
            if (actor.Combat == null || action.Targets.Count == 0 || !actor.Combat.CanAttack)
                return false;

            if (!_unitsById.TryGetValue(action.Targets[0], out UnitBrain target) || target == null || target.IsDead)
                return false;

            actor.Combat.Attack(target.gameObject);
            return true;
        }

        private bool TryResolveAbility(UnitBrain actor, AbilityAction action)
        {
            return _abilityExecutionPipeline.TryExecute(action, actor, _unitsById);
        }

        private void HandleActionCompleted(ActionCompletedEvent actionCompletedEvent)
        {
            ActionPhasePayload payload = actionCompletedEvent.Payload;
            if (!_unitsById.TryGetValue(payload.ActorId, out UnitBrain actor) || actor == null)
                return;

            UnitRuntimeState state = actor.RuntimeState;
            if (state == null)
                return;

            if (state.CurrentActionId == payload.ActionId)
                state.CurrentActionId = string.Empty;
            state.RecoveryUntilTick = Mathf.Max(state.RecoveryUntilTick, payload.RecoveryEndTick);
            SyncRuntimeState(actor);
        }

        public void SyncRuntimeState(UnitBrain unit)
        {
            if (unit == null || unit.RuntimeState == null)
                return;

            UnitRuntimeState state = unit.RuntimeState;
            if (unit.Health != null)
                state.HP = unit.Health.CurrentHealth;
            if (unit.StatusEffects != null)
                state.SP = unit.StatusEffects.CurrentSp;
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

        private static string GetActorId(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out UnitBrain brain))
                return string.Empty;
            return brain.ActorId;
        }

        private static string EncodeCell(Vector2Int cell)
        {
            return $"{CellEncodingPrefix}{cell.x},{cell.y}";
        }

        private static bool TryDecodeCell(string encodedCell, out Vector2Int cell)
        {
            cell = default;
            if (string.IsNullOrWhiteSpace(encodedCell) || !encodedCell.StartsWith(CellEncodingPrefix, StringComparison.Ordinal))
                return false;

            ReadOnlySpan<char> coordinates = encodedCell.AsSpan(CellEncodingPrefix.Length);
            int separatorIndex = coordinates.IndexOf(',');
            if (separatorIndex <= 0 || separatorIndex >= coordinates.Length - 1)
                return false;

            ReadOnlySpan<char> xSpan = coordinates.Slice(0, separatorIndex);
            ReadOnlySpan<char> ySpan = coordinates.Slice(separatorIndex + 1);
            if (!int.TryParse(xSpan, out int x) || !int.TryParse(ySpan, out int y))
                return false;

            cell = new Vector2Int(x, y);
            return true;
        }
    }
}
