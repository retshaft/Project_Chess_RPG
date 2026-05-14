using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation
{
    public sealed class SimulationSnapshot
    {
        private static readonly IReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState> EmptyRuntimeStates =
            new ReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState>(new Dictionary<Guid, IReadOnlyUnitRuntimeState>());
        private static readonly IReadOnlyDictionary<Guid, IReadOnlyActionState> EmptyActiveActions =
            new ReadOnlyDictionary<Guid, IReadOnlyActionState>(new Dictionary<Guid, IReadOnlyActionState>());
        private static readonly IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> EmptyActiveEffects =
            new ReadOnlyDictionary<string, IReadOnlyEffectRuntimeState>(new Dictionary<string, IReadOnlyEffectRuntimeState>(StringComparer.Ordinal));

        public SimulationSnapshot(int tick)
            : this(tick, EmptyRuntimeStates, EmptyActiveActions, EmptyActiveEffects)
        {
        }

        public SimulationSnapshot(
            int tick,
            IReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState> runtimeStates,
            IReadOnlyDictionary<Guid, IReadOnlyActionState> activeActions,
            IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> activeEffects)
            : this(
                tick,
                CloneRuntimeStates(runtimeStates),
                CloneActiveActions(activeActions),
                CloneActiveEffects(activeEffects))
        {
        }

        private SimulationSnapshot(
            int tick,
            IReadOnlyDictionary<Guid, SimulationUnitSnapshot> runtimeStates,
            IReadOnlyDictionary<Guid, SimulationActionSnapshot> activeActions,
            IReadOnlyDictionary<string, SimulationEffectSnapshot> activeEffects)
        {
            Tick = tick;
            RuntimeStates = runtimeStates ?? throw new ArgumentNullException(nameof(runtimeStates));
            ActiveActions = activeActions ?? throw new ArgumentNullException(nameof(activeActions));
            ActiveEffects = activeEffects ?? throw new ArgumentNullException(nameof(activeEffects));
        }

        public int Tick { get; }
        public IReadOnlyDictionary<Guid, SimulationUnitSnapshot> RuntimeStates { get; }
        public IReadOnlyDictionary<Guid, SimulationActionSnapshot> ActiveActions { get; }
        public IReadOnlyDictionary<string, SimulationEffectSnapshot> ActiveEffects { get; }

        public SimulationSnapshot Clone()
        {
            return new SimulationSnapshot(
                Tick,
                CloneRuntimeStates(RuntimeStates),
                CloneActiveActions(ActiveActions),
                CloneActiveEffects(ActiveEffects));
        }

        private static IReadOnlyDictionary<Guid, SimulationUnitSnapshot> CloneRuntimeStates(
            IReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState> runtimeStates)
        {
            if (runtimeStates == null)
                throw new ArgumentNullException(nameof(runtimeStates));

            var cloned = new SortedDictionary<Guid, SimulationUnitSnapshot>();
            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> pair in runtimeStates)
            {
                SimulationUnitSnapshot snapshot = SimulationUnitSnapshot.From(pair.Key, pair.Value);
                cloned[snapshot.UnitId] = snapshot;
            }

            return new ReadOnlyDictionary<Guid, SimulationUnitSnapshot>(cloned);
        }

        private static IReadOnlyDictionary<Guid, SimulationUnitSnapshot> CloneRuntimeStates(
            IReadOnlyDictionary<Guid, SimulationUnitSnapshot> runtimeStates)
        {
            if (runtimeStates == null)
                throw new ArgumentNullException(nameof(runtimeStates));

            var cloned = new SortedDictionary<Guid, SimulationUnitSnapshot>();
            foreach (KeyValuePair<Guid, SimulationUnitSnapshot> pair in runtimeStates)
            {
                SimulationUnitSnapshot snapshot = pair.Value != null
                    ? new SimulationUnitSnapshot(pair.Value)
                    : new SimulationUnitSnapshot(pair.Key, 0, 0, default, null, 0, UnitStatusFlags.None);
                cloned[pair.Key] = snapshot;
            }

            return new ReadOnlyDictionary<Guid, SimulationUnitSnapshot>(cloned);
        }

        private static IReadOnlyDictionary<Guid, SimulationActionSnapshot> CloneActiveActions(
            IReadOnlyDictionary<Guid, IReadOnlyActionState> activeActions)
        {
            if (activeActions == null)
                throw new ArgumentNullException(nameof(activeActions));

            var cloned = new SortedDictionary<Guid, SimulationActionSnapshot>();
            foreach (KeyValuePair<Guid, IReadOnlyActionState> pair in activeActions)
            {
                SimulationActionSnapshot snapshot = SimulationActionSnapshot.CreateFrom(pair.Value);
                if (snapshot != null)
                    cloned[snapshot.ActionId] = snapshot;
            }

            return new ReadOnlyDictionary<Guid, SimulationActionSnapshot>(cloned);
        }

        private static IReadOnlyDictionary<Guid, SimulationActionSnapshot> CloneActiveActions(
            IReadOnlyDictionary<Guid, SimulationActionSnapshot> activeActions)
        {
            if (activeActions == null)
                throw new ArgumentNullException(nameof(activeActions));

            var cloned = new SortedDictionary<Guid, SimulationActionSnapshot>();
            foreach (KeyValuePair<Guid, SimulationActionSnapshot> pair in activeActions)
            {
                if (pair.Value == null)
                    continue;

                cloned[pair.Key] = new SimulationActionSnapshot(pair.Value);
            }

            return new ReadOnlyDictionary<Guid, SimulationActionSnapshot>(cloned);
        }

        private static IReadOnlyDictionary<string, SimulationEffectSnapshot> CloneActiveEffects(
            IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> activeEffects)
        {
            if (activeEffects == null)
                throw new ArgumentNullException(nameof(activeEffects));

            var cloned = new SortedDictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyEffectRuntimeState> pair in activeEffects)
            {
                SimulationEffectSnapshot snapshot = SimulationEffectSnapshot.From(pair.Key, pair.Value);
                cloned[snapshot.Key] = snapshot;
            }

            return new ReadOnlyDictionary<string, SimulationEffectSnapshot>(cloned);
        }

        private static IReadOnlyDictionary<string, SimulationEffectSnapshot> CloneActiveEffects(
            IReadOnlyDictionary<string, SimulationEffectSnapshot> activeEffects)
        {
            if (activeEffects == null)
                throw new ArgumentNullException(nameof(activeEffects));

            var cloned = new SortedDictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SimulationEffectSnapshot> pair in activeEffects)
            {
                SimulationEffectSnapshot snapshot = pair.Value != null
                    ? new SimulationEffectSnapshot(pair.Value)
                    : new SimulationEffectSnapshot(pair.Key, string.Empty, Guid.Empty, Guid.Empty, 0, 0, 0, 0, 0f);                cloned[pair.Key] = snapshot;
            }

            return new ReadOnlyDictionary<string, SimulationEffectSnapshot>(cloned);
        }
    }

    public sealed class SimulationUnitSnapshot
    {
        public SimulationUnitSnapshot(
            Guid unitId,
            int hp,
            int sp,
            Vector2Int position,
            Guid? currentActionId,
            int recoveryUntilTick,
            UnitStatusFlags statusFlags)
        {
            UnitId = unitId;
            HP = hp;
            SP = sp;
            Position = position;
            CurrentActionId = currentActionId;
            RecoveryUntilTick = recoveryUntilTick;
            StatusFlags = statusFlags;
        }

        public SimulationUnitSnapshot(SimulationUnitSnapshot source)
            : this(
                source?.UnitId ?? Guid.Empty,
                source?.HP ?? 0,
                source?.SP ?? 0,
                source?.Position ?? default,
                source?.CurrentActionId,
                source?.RecoveryUntilTick ?? 0,
                source?.StatusFlags ?? UnitStatusFlags.None)
        {
        }

        public Guid UnitId { get; }
        public int HP { get; }
        public int SP { get; }
        public Vector2Int Position { get; }
        public Guid? CurrentActionId { get; }
        public int RecoveryUntilTick { get; }
        public UnitStatusFlags StatusFlags { get; }

        public static SimulationUnitSnapshot From(Guid fallbackUnitId, IReadOnlyUnitRuntimeState state)
        {
            Guid unitId = state != null && state.UnitId != Guid.Empty ? state.UnitId : fallbackUnitId;
            return new SimulationUnitSnapshot(
                unitId,
                state?.HP ?? 0,
                state?.SP ?? 0,
                state?.Position ?? default,
                state?.CurrentActionId,
                state?.RecoveryUntilTick ?? 0,
                state?.StatusFlags ?? UnitStatusFlags.None);
        }
    }

    public sealed class SimulationActionSnapshot : IActionCommand
    {
        private static readonly IReadOnlyList<Guid> EmptyTargetIds = Array.AsReadOnly(Array.Empty<Guid>());

        public SimulationActionSnapshot(
            Guid actionId,
            Guid actorId,
            string actionType,
            ActionState state,
            int queuedTick,
            int startTick,
            int resolveTick,
            int recoveryEndTick,
            ActionSpeedTier speedTier,
            bool isInterruptible,
            bool isRecoveryInterruptible,
            InterruptPriority interruptPriority,
            InterruptWindow interruptWindow,
            ActionLockType intentLockType,
            ActionConcurrencyPolicy concurrencyPolicy,
            bool canBeInterrupted,
            bool canInterruptOthers,
            ActionDefinition definition,
            ActionInterruptState interruptState,
            ActionInterruptPolicy interruptPolicy,
            Vector2Int? from,
            Vector2Int? to,
            Guid? targetId,
            int? damage,
            bool? isCritical,
            string abilityId,
            IReadOnlyList<Guid> targetIds)
        {
            ActionId = actionId;
            ActorId = actorId;
            ActionType = actionType ?? string.Empty;
            State = state;
            QueuedTick = queuedTick;
            StartTick = startTick;
            ResolveTick = resolveTick;
            RecoveryEndTick = recoveryEndTick;
            SpeedTier = speedTier;
            IsInterruptible = isInterruptible;
            IsRecoveryInterruptible = isRecoveryInterruptible;
            InterruptPriority = interruptPriority;
            InterruptWindow = interruptWindow;
            IntentLockType = intentLockType;
            ConcurrencyPolicy = concurrencyPolicy;
            CanBeInterrupted = canBeInterrupted;
            CanInterruptOthers = canInterruptOthers;
            Definition = definition;
            InterruptState = interruptState;
            InterruptPolicy = interruptPolicy;
            From = from;
            To = to;
            TargetId = targetId;
            Damage = damage;
            IsCritical = isCritical;
            AbilityId = abilityId ?? string.Empty;
            TargetIds = CloneTargetIds(targetIds);
        }

        public SimulationActionSnapshot(SimulationActionSnapshot source)
            : this(
                source?.ActionId ?? Guid.Empty,
                source?.ActorId ?? Guid.Empty,
                source?.ActionType,
                source?.State ?? ActionState.Queued,
                source?.QueuedTick ?? 0,
                source?.StartTick ?? 0,
                source?.ResolveTick ?? 0,
                source?.RecoveryEndTick ?? 0,
                source?.SpeedTier ?? ActionSpeedTier.Normal,
                source?.IsInterruptible ?? true,
                source?.IsRecoveryInterruptible ?? false,
                source?.InterruptPriority ?? InterruptPriority.Normal,
                source?.InterruptWindow ?? InterruptWindow.CastingInterruptible,
                source?.IntentLockType ?? ActionLockType.CastLock,
                source?.ConcurrencyPolicy ?? ActionConcurrencyPolicy.Reject,
                source?.CanBeInterrupted ?? false,
                source?.CanInterruptOthers ?? false,
                source?.Definition,
                source?.InterruptState ?? default,
                source?.InterruptPolicy ?? default,
                source?.From,
                source?.To,
                source?.TargetId,
                source?.Damage,
                source?.IsCritical,
                source?.AbilityId,
                source?.TargetIds)
        {
        }

        public Guid ActionId { get; }
        public Guid ActorId { get; }
        public string ActionType { get; }
        public ActionState State { get; }
        public int QueuedTick { get; }
        public int StartTick { get; }
        public int ResolveTick { get; }
        public int RecoveryEndTick { get; }
        public ActionSpeedTier SpeedTier { get; }
        public bool IsInterruptible { get; }
        public bool IsRecoveryInterruptible { get; }
        public InterruptPriority InterruptPriority { get; }
        public InterruptWindow InterruptWindow { get; }
        public ActionLockType IntentLockType { get; }
        public ActionConcurrencyPolicy ConcurrencyPolicy { get; }
        
        // IReadOnlyActionState 누락 프로퍼티 추가
        public bool CanBeInterrupted { get; }
        public bool CanInterruptOthers { get; }
        public ActionDefinition Definition { get; }
        public ActionInterruptState InterruptState { get; }
        public ActionInterruptPolicy InterruptPolicy { get; }

        public bool IsCompleted => ActionStateMachine.IsTerminal(State);
        public Vector2Int? From { get; }
        public Vector2Int? To { get; }
        public Guid? TargetId { get; }
        public int? Damage { get; }
        public bool? IsCritical { get; }
        public string AbilityId { get; }
        public IReadOnlyList<Guid> TargetIds { get; }

        public static SimulationActionSnapshot CreateFrom(IReadOnlyActionState action)
        {
            if (action == null)
                return null;
            if (action is SimulationActionSnapshot snapshot)
                return new SimulationActionSnapshot(snapshot);

            Vector2Int? from = null;
            Vector2Int? to = null;
            Guid? targetId = null;
            int? damage = null;
            bool? isCritical = null;
            string abilityId = string.Empty;
            IReadOnlyList<Guid> targetIds = EmptyTargetIds;

            switch (action)
            {
                case MoveActionCommand move:
                    from = move.From;
                    to = move.To;
                    break;
                case AttackActionCommand attack:
                    targetId = attack.TargetId;
                    damage = attack.Damage;
                    isCritical = attack.IsCritical;
                    break;
                case AbilityActionCommand ability:
                    abilityId = ability.AbilityId;
                    targetIds = ability.TargetIds;
                    break;
            }

            return new SimulationActionSnapshot(
                action.ActionId,
                action.ActorId,
                action.GetType().Name,
                action.State,
                action.QueuedTick,
                action.StartTick,
                action.ResolveTick,
                action.RecoveryEndTick,
                action.SpeedTier,
                action.IsInterruptible,
                action.IsRecoveryInterruptible,
                action.InterruptPriority,
                action.InterruptWindow,
                action.IntentLockType,
                action.ConcurrencyPolicy,
                action.CanBeInterrupted,     // 매핑 추가
                action.CanInterruptOthers,   // 매핑 추가
                action.Definition,           // 매핑 추가
                action.InterruptState,       // 매핑 추가
                action.InterruptPolicy,      // 매핑 추가
                from,
                to,
                targetId,
                damage,
                isCritical,
                abilityId,
                targetIds);
        }

        private static IReadOnlyList<Guid> CloneTargetIds(IReadOnlyList<Guid> targetIds)
        {
            if (targetIds == null || targetIds.Count == 0)
                return EmptyTargetIds;

            var cloned = new List<Guid>(targetIds.Count);
            for (int i = 0; i < targetIds.Count; i++)
                cloned.Add(targetIds[i]);
            return cloned.AsReadOnly();
        }
    }

    public sealed class SimulationEffectSnapshot
    {
        public SimulationEffectSnapshot(
            string key,
            string effectId,
            Guid sourceId,
            Guid targetId,
            int remainingTick,
            int stackCount,
            int tickInterval,
            int nextTickIn,
            float magnitude,
            CheckmateRPG.Core.Effects.EffectTimingPhase timingPhase = CheckmateRPG.Core.Effects.EffectTimingPhase.OnTickEnd,
            ActionSpeedTier actionSpeedLevel = ActionSpeedTier.Normal,
            bool isReaction = false)
        {
            Key = key ?? string.Empty;
            EffectId = effectId ?? string.Empty;
            SourceId = sourceId;
            TargetId = targetId;
            RemainingTick = remainingTick;
            StackCount = stackCount;
            TickInterval = tickInterval;
            NextTickIn = nextTickIn;
            Magnitude = magnitude;
            TimingPhase = timingPhase;
            ActionSpeedLevel = actionSpeedLevel;
            IsReaction = isReaction;
        }

        public SimulationEffectSnapshot(SimulationEffectSnapshot source)
            : this(
                source?.Key,
                source?.EffectId,
                source?.SourceId ?? Guid.Empty,
                source?.TargetId ?? Guid.Empty,
                source?.RemainingTick ?? 0,
                source?.StackCount ?? 0,
                source?.TickInterval ?? 0,
                source?.NextTickIn ?? 0,
                source?.Magnitude ?? 0f,
                source?.TimingPhase ?? CheckmateRPG.Core.Effects.EffectTimingPhase.OnTickEnd,
                source?.ActionSpeedLevel ?? ActionSpeedTier.Normal,
                source?.IsReaction ?? false)
        {
        }

        public string Key { get; }
        public string EffectId { get; }
        public Guid SourceId { get; }
        public Guid TargetId { get; }
        public int RemainingTick { get; }
        public int StackCount { get; }
        public int TickInterval { get; }
        public int NextTickIn { get; }
        public float Magnitude { get; }
        public CheckmateRPG.Core.Effects.EffectTimingPhase TimingPhase { get; }
        public ActionSpeedTier ActionSpeedLevel { get; }
        public bool IsReaction { get; }

        public static SimulationEffectSnapshot From(string fallbackKey, EffectRuntimeState state)
        {
            return From(fallbackKey, (IReadOnlyEffectRuntimeState)state);
        }

        public static SimulationEffectSnapshot From(string fallbackKey, IReadOnlyEffectRuntimeState state)
        {
            return new SimulationEffectSnapshot(
                string.IsNullOrWhiteSpace(fallbackKey) ? BuildKey(state) : fallbackKey,
                state?.EffectId,
                state?.SourceId ?? Guid.Empty,
                state?.TargetId ?? Guid.Empty,
                state?.RemainingTick ?? 0,
                state?.StackCount ?? 0,
                state?.TickInterval ?? 0,
                state?.NextTickIn ?? 0,
                state?.Magnitude ?? 0f,
                state?.TimingPhase ?? CheckmateRPG.Core.Effects.EffectTimingPhase.OnTickEnd,
                state?.ActionSpeedLevel ?? ActionSpeedTier.Normal,
                state?.IsReaction ?? false);
        }

        private static string BuildKey(IReadOnlyEffectRuntimeState state)
        {
            if (state == null)
                return string.Empty;
            return $"{state.TargetId:N}:{state.EffectId}";
        }
    }
}
