using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation
{
    public sealed class SimulationRuntime : IReadOnlySimulationRuntime
    {
        private static readonly IReadOnlyDictionary<Guid, UnitRuntimeState> EmptyRuntimeStates =
            new ReadOnlyDictionary<Guid, UnitRuntimeState>(new Dictionary<Guid, UnitRuntimeState>());
        private static readonly IReadOnlyDictionary<Guid, IActionCommand> EmptyActiveActions =
            new ReadOnlyDictionary<Guid, IActionCommand>(new Dictionary<Guid, IActionCommand>());
        private static readonly IReadOnlyDictionary<string, EffectRuntimeState> EmptyActiveEffects =
            new ReadOnlyDictionary<string, EffectRuntimeState>(new Dictionary<string, EffectRuntimeState>(StringComparer.Ordinal));
        private static readonly IReadOnlyDictionary<Vector2Int, Guid> EmptyOccupiedPositions =
            new ReadOnlyDictionary<Vector2Int, Guid>(new Dictionary<Vector2Int, Guid>());

        private readonly Dictionary<Guid, UnitRuntimeState> _runtimeStates;
        private readonly Dictionary<Guid, IActionCommand> _activeActions;
        private readonly Dictionary<string, EffectRuntimeState> _activeEffects;
        private readonly Dictionary<Vector2Int, Guid> _occupiedPositions;
        private IReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState> _runtimeStateViewCache;
        private IReadOnlyDictionary<Guid, IReadOnlyActionState> _activeActionViewCache;
        private IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> _activeEffectViewCache;
        private IReadOnlyDictionary<Vector2Int, Guid> _occupiedPositionViewCache;
        private bool _runtimeStateViewDirty = true;
        private bool _activeActionViewDirty = true;
        private bool _activeEffectViewDirty = true;
        private bool _occupiedPositionViewDirty = true;

        public SimulationRuntime(int currentTick)
            : this(currentTick, EmptyRuntimeStates, EmptyActiveActions, EmptyActiveEffects, EmptyOccupiedPositions)
        {
        }

        public SimulationRuntime(
            int currentTick,
            IReadOnlyDictionary<Guid, UnitRuntimeState> runtimeStates,
            IReadOnlyDictionary<Guid, IActionCommand> activeActions,
            IReadOnlyDictionary<string, EffectRuntimeState> activeEffects,
            IReadOnlyDictionary<Vector2Int, Guid> occupiedPositions)
        {
            CurrentTick = currentTick;
            _runtimeStates = CloneRuntimeStates(runtimeStates ?? throw new ArgumentNullException(nameof(runtimeStates)));
            _activeActions = CloneActiveActions(activeActions ?? throw new ArgumentNullException(nameof(activeActions)));
            _activeEffects = CloneActiveEffects(activeEffects ?? throw new ArgumentNullException(nameof(activeEffects)));
            _occupiedPositions = CloneOccupiedPositions(occupiedPositions ?? throw new ArgumentNullException(nameof(occupiedPositions)));
        }

        public int CurrentTick { get; private set; }

        public IReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState> RuntimeStates =>
            _runtimeStateViewDirty || _runtimeStateViewCache == null
                ? _runtimeStateViewCache = BuildRuntimeStateView()
                : _runtimeStateViewCache;

        public IReadOnlyDictionary<Guid, IReadOnlyActionState> ActiveActions =>
            _activeActionViewDirty || _activeActionViewCache == null
                ? _activeActionViewCache = BuildActiveActionView()
                : _activeActionViewCache;

        public IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> ActiveEffects =>
            _activeEffectViewDirty || _activeEffectViewCache == null
                ? _activeEffectViewCache = BuildActiveEffectView()
                : _activeEffectViewCache;

        public IReadOnlyDictionary<Vector2Int, Guid> OccupiedPositions =>
            _occupiedPositionViewDirty || _occupiedPositionViewCache == null
                ? _occupiedPositionViewCache = BuildOccupiedPositionView()
                : _occupiedPositionViewCache;

        public static string BuildEffectKey(EffectRuntimeState effect)
        {
            if (effect == null)
                return string.Empty;

            return BuildEffectKey(effect.TargetId, effect.EffectId);
        }

        public static string BuildEffectKey(Guid targetId, string effectId)
        {
            return $"{targetId:N}:{effectId ?? string.Empty}";
        }

        internal void SetCurrentTick(int tick)
        {
            CurrentTick = tick;
        }

        public IReadOnlyUnitRuntimeState GetUnit(Guid unitId)
        {
            if (!TryGetUnit(unitId, out IReadOnlyUnitRuntimeState unit))
                throw new KeyNotFoundException($"Unit runtime state not found for '{unitId:N}'.");

            return unit;
        }

        public bool TryGetUnit(Guid unitId, out IReadOnlyUnitRuntimeState unit)
        {
            if (unitId == Guid.Empty)
            {
                unit = null;
                return false;
            }

            return RuntimeStates.TryGetValue(unitId, out unit);
        }

        internal bool TryGetMutableUnit(Guid unitId, out UnitRuntimeState unit)
        {
            return _runtimeStates.TryGetValue(unitId, out unit) && unit != null;
        }

        internal UnitRuntimeState GetMutableUnit(Guid unitId)
        {
            if (!TryGetMutableUnit(unitId, out UnitRuntimeState unit))
                throw new KeyNotFoundException($"Unit runtime state not found for '{unitId:N}'.");

            return unit;
        }

        public IReadOnlyActionState GetAction(Guid actionId)
        {
            if (!TryGetAction(actionId, out IReadOnlyActionState action))
                throw new KeyNotFoundException($"Action runtime state not found for '{actionId:N}'.");

            return action;
        }

        public bool TryGetAction(Guid actionId, out IReadOnlyActionState action)
        {
            if (actionId == Guid.Empty)
            {
                action = null;
                return false;
            }

            return ActiveActions.TryGetValue(actionId, out action);
        }

        public bool TryGetEffect(string effectKey, out IReadOnlyEffectRuntimeState effect)
        {
            if (string.IsNullOrWhiteSpace(effectKey))
            {
                effect = null;
                return false;
            }

            return ActiveEffects.TryGetValue(effectKey, out effect);
        }

        /// <summary>
        /// Builds and returns an <see cref="EffectContainer"/> scoped to <paramref name="unitId"/>,
        /// populated with every active effect whose <see cref="IReadOnlyEffectRuntimeState.TargetId"/>
        /// matches.  The container is a snapshot; it is not kept in sync with subsequent mutations.
        /// </summary>
        public EffectContainer GetEffectContainer(Guid unitId)
        {
            var container = new EffectContainer(unitId);
            foreach (KeyValuePair<string, EffectRuntimeState> pair in _activeEffects)
            {
                if (pair.Value != null && pair.Value.TargetId == unitId)
                    container.Add(pair.Key, pair.Value);
            }

            return container;
        }

        internal bool TryGetMutableEffect(string effectKey, out EffectRuntimeState effect)
        {
            if (string.IsNullOrWhiteSpace(effectKey))
            {
                effect = null; // ���� ��Ȳ������ out �Ű������� ���� �ݵ�� �Ҵ�
                return false;
            }

            return _activeEffects.TryGetValue(effectKey, out effect) && effect != null;
        }

        public IReadOnlyList<IReadOnlyUnitRuntimeState> GetUnitsAtPosition(Vector2Int position)
        {
            if (!OccupiedPositions.TryGetValue(position, out Guid unitId) ||
                !TryGetUnit(unitId, out IReadOnlyUnitRuntimeState unit))
            {
                return Array.Empty<IReadOnlyUnitRuntimeState>();
            }

            return new IReadOnlyUnitRuntimeState[] { unit };
        }

        public bool IsOccupied(Vector2Int position)
        {
            return _occupiedPositions.ContainsKey(position);
        }

        internal void RegisterUnit(UnitRuntimeState unitState)
        {
            if (unitState == null || unitState.UnitId == Guid.Empty)
                return;

            _runtimeStates[unitState.UnitId] = new UnitRuntimeState(unitState);
            RefreshUnitPositionOwnership(unitState.UnitId);
            MarkRuntimeStateViewDirty();
        }

        internal void RegisterAction(IActionCommand action)
        {
            if (action == null || action.ActionId == Guid.Empty)
                return;

            SimulationActionSnapshot snapshot = SimulationActionSnapshot.CreateFrom(action);
            if (snapshot == null)
                return;

            _activeActions[action.ActionId] = snapshot;
            MarkActiveActionViewDirty();
        }

        internal void ReplaceActions(IReadOnlyCollection<IActionCommand> actions)
        {
            _activeActions.Clear();
            try
            {
                if (actions == null || actions.Count == 0)
                    return;

                foreach (IActionCommand action in actions)
                {
                    if (action == null || action.ActionId == Guid.Empty)
                        continue;

                    SimulationActionSnapshot snapshot = SimulationActionSnapshot.CreateFrom(action);
                    if (snapshot == null)
                        continue;

                    _activeActions[action.ActionId] = snapshot;
                }
            }
            finally
            {
                MarkActiveActionViewDirty();
            }
        }

        internal void RegisterEffect(EffectRuntimeState effect)
        {
            if (effect == null)
                return;

            RegisterEffect(BuildEffectKey(effect), effect);
        }

        internal void RegisterEffect(string effectKey, EffectRuntimeState effect)
        {
            if (string.IsNullOrWhiteSpace(effectKey) || effect == null)
                return;

            _activeEffects[effectKey] = new EffectRuntimeState(effect);
            MarkActiveEffectViewDirty();
        }

        internal void UnregisterUnit(Guid unitId)
        {
            if (unitId == Guid.Empty)
                return;

            CleanupUnitRuntimeArtifacts(unitId);
            _runtimeStates.Remove(unitId);
            MarkRuntimeStateViewDirty();
        }

        internal void UnregisterAction(Guid actionId)
        {
            if (actionId == Guid.Empty)
                return;

            _activeActions.Remove(actionId);
            MarkActiveActionViewDirty();
        }

        internal void UnregisterEffect(string effectKey)
        {
            if (string.IsNullOrWhiteSpace(effectKey))
                return;

            _activeEffects.Remove(effectKey);
            MarkActiveEffectViewDirty();
        }

        internal void SetUnitDerivedState(Guid unitId, int sp, UnitStatusFlags statusFlags)
        {
            if (!TryGetMutableUnit(unitId, out UnitRuntimeState state))
                return;

            state.SyncDerivedState(unitId, sp, statusFlags);
            MarkRuntimeStateViewDirty();
        }

        internal void SetUnitHP(Guid unitId, int hp, string ownerName)
        {
            if (!TryGetMutableUnit(unitId, out UnitRuntimeState state))
                return;

            state.SetHP(hp, ownerName);
            MarkRuntimeStateViewDirty();
        }

        internal void SetUnitPosition(Guid unitId, Vector2Int position, string ownerName)
        {
            if (!TryGetMutableUnit(unitId, out UnitRuntimeState state))
                return;

            _occupiedPositions.Remove(state.Position);
            state.SetPosition(position, ownerName);
            _occupiedPositions[position] = unitId;
            MarkRuntimeStateViewDirty();
            MarkOccupiedPositionViewDirty();
        }

        internal void SetUnitActionState(Guid unitId, Guid? currentActionId, int recoveryUntilTick, string ownerName)
        {
            if (!TryGetMutableUnit(unitId, out UnitRuntimeState state))
                return;

            state.SetActionState(currentActionId, recoveryUntilTick, ownerName);
            MarkRuntimeStateViewDirty();
        }

        internal void AddUnitStatusFlag(Guid unitId, UnitStatusFlags flag)
        {
            if (!TryGetMutableUnit(unitId, out UnitRuntimeState state))
                return;

            state.AddStatusFlag(flag);
            MarkRuntimeStateViewDirty();
        }

        internal void CleanupUnitRuntimeArtifacts(Guid unitId)
        {
            if (!TryGetMutableUnit(unitId, out UnitRuntimeState state))
                return;

            var actionIdsToRemove = new List<Guid>();
            foreach (KeyValuePair<Guid, IActionCommand> pair in _activeActions)
            {
                if (pair.Value != null && pair.Value.ActorId == unitId)
                    actionIdsToRemove.Add(pair.Key);
            }

            for (int i = 0; i < actionIdsToRemove.Count; i++)
                _activeActions.Remove(actionIdsToRemove[i]);
            if (actionIdsToRemove.Count > 0)
                MarkActiveActionViewDirty();

            var effectKeysToRemove = new List<string>();
            foreach (KeyValuePair<string, EffectRuntimeState> pair in _activeEffects)
            {
                if (pair.Value != null && pair.Value.TargetId == unitId)
                    effectKeysToRemove.Add(pair.Key);
            }

            for (int i = 0; i < effectKeysToRemove.Count; i++)
                _activeEffects.Remove(effectKeysToRemove[i]);
            if (effectKeysToRemove.Count > 0)
                MarkActiveEffectViewDirty();

            _occupiedPositions.Remove(state.Position);
            MarkOccupiedPositionViewDirty();
        }

        internal void ApplyDeadUnitLifecycle(Guid unitId, int currentTick, string actionOwnerName)
        {
            SetUnitActionState(unitId, null, currentTick, actionOwnerName);
            AddUnitStatusFlag(unitId, UnitStatusFlags.Dead);
            CleanupUnitRuntimeArtifacts(unitId);
        }

        internal SimulationRuntime CreateSnapshot(int tick)
        {
            return new SimulationRuntime(
                tick,
                _runtimeStates,
                _activeActions,
                _activeEffects,
                _occupiedPositions);
        }

        private void RefreshUnitPositionOwnership(Guid unitId)
        {
            var positionsToClear = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, Guid> pair in _occupiedPositions)
            {
                if (pair.Value == unitId)
                    positionsToClear.Add(pair.Key);
            }

            for (int i = 0; i < positionsToClear.Count; i++)
                _occupiedPositions.Remove(positionsToClear[i]);

            if (TryGetMutableUnit(unitId, out UnitRuntimeState unit))
                _occupiedPositions[unit.Position] = unitId;

            MarkOccupiedPositionViewDirty();
        }

        private IReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState> BuildRuntimeStateView()
        {
            var projected = new Dictionary<Guid, IReadOnlyUnitRuntimeState>();
            foreach (KeyValuePair<Guid, UnitRuntimeState> pair in _runtimeStates)
            {
                if (pair.Value == null)
                    continue;

                projected[pair.Key] = new ReadOnlyUnitRuntimeStateView(pair.Value);
            }

            _runtimeStateViewDirty = false;
            return new ReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState>(projected);
        }

        private IReadOnlyDictionary<Guid, IReadOnlyActionState> BuildActiveActionView()
        {
            var projected = new Dictionary<Guid, IReadOnlyActionState>();
            foreach (KeyValuePair<Guid, IActionCommand> pair in _activeActions)
            {
                if (pair.Value == null)
                    continue;

                projected[pair.Key] = SimulationActionSnapshot.CreateFrom(pair.Value);
            }

            _activeActionViewDirty = false;
            return new ReadOnlyDictionary<Guid, IReadOnlyActionState>(projected);
        }

        private IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> BuildActiveEffectView()
        {
            var projected = new Dictionary<string, IReadOnlyEffectRuntimeState>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, EffectRuntimeState> pair in _activeEffects)
            {
                if (pair.Value == null)
                    continue;

                projected[pair.Key] = new ReadOnlyEffectRuntimeStateView(pair.Value);
            }

            _activeEffectViewDirty = false;
            return new ReadOnlyDictionary<string, IReadOnlyEffectRuntimeState>(projected);
        }

        private IReadOnlyDictionary<Vector2Int, Guid> BuildOccupiedPositionView()
        {
            _occupiedPositionViewDirty = false;
            return new ReadOnlyDictionary<Vector2Int, Guid>(CloneOccupiedPositions(_occupiedPositions));
        }

        private void MarkRuntimeStateViewDirty()
        {
            _runtimeStateViewDirty = true;
            _runtimeStateViewCache = null;
        }

        private void MarkActiveActionViewDirty()
        {
            _activeActionViewDirty = true;
            _activeActionViewCache = null;
        }

        private void MarkActiveEffectViewDirty()
        {
            _activeEffectViewDirty = true;
            _activeEffectViewCache = null;
        }

        private void MarkOccupiedPositionViewDirty()
        {
            _occupiedPositionViewDirty = true;
            _occupiedPositionViewCache = null;
        }

        private static Dictionary<Guid, UnitRuntimeState> CloneRuntimeStates(IReadOnlyDictionary<Guid, UnitRuntimeState> source)
        {
            var clone = new SortedDictionary<Guid, UnitRuntimeState>();
            foreach (KeyValuePair<Guid, UnitRuntimeState> pair in source)
            {
                UnitRuntimeState state = pair.Value != null ? new UnitRuntimeState(pair.Value) : new UnitRuntimeState();
                if (state.UnitId == Guid.Empty)
                    state.SyncDerivedState(pair.Key, state.SP, state.StatusFlags);
                clone[pair.Key] = state;
            }

            return new Dictionary<Guid, UnitRuntimeState>(clone);
        }

        private static Dictionary<Guid, IActionCommand> CloneActiveActions(IReadOnlyDictionary<Guid, IActionCommand> source)
        {
            var clone = new SortedDictionary<Guid, IActionCommand>();
            foreach (KeyValuePair<Guid, IActionCommand> pair in source)
            {
                SimulationActionSnapshot action = SimulationActionSnapshot.CreateFrom(pair.Value);
                if (action != null)
                    clone[pair.Key] = action;
            }

            return new Dictionary<Guid, IActionCommand>(clone);
        }

        private static Dictionary<string, EffectRuntimeState> CloneActiveEffects(IReadOnlyDictionary<string, EffectRuntimeState> source)
        {
            var clone = new SortedDictionary<string, EffectRuntimeState>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, EffectRuntimeState> pair in source)
            {
                if (pair.Value == null)
                    continue;

                clone[pair.Key] = new EffectRuntimeState(pair.Value);
            }

            return new Dictionary<string, EffectRuntimeState>(clone, StringComparer.Ordinal);
        }

        private static Dictionary<Vector2Int, Guid> CloneOccupiedPositions(IReadOnlyDictionary<Vector2Int, Guid> source)
        {
            var clone = new Dictionary<Vector2Int, Guid>();
            foreach (KeyValuePair<Vector2Int, Guid> pair in source)
                clone[pair.Key] = pair.Value;
            return clone;
        }

        private sealed class ReadOnlyUnitRuntimeStateView : IReadOnlyUnitRuntimeState
        {
            public ReadOnlyUnitRuntimeStateView(UnitRuntimeState source)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                UnitId = source.UnitId;
                HP = source.HP;
                SP = source.SP;
                Position = source.Position;
                CurrentActionId = source.CurrentActionId;
                RecoveryUntilTick = source.RecoveryUntilTick;
                StatusFlags = source.StatusFlags;
                HasBaseline = source.HasBaseline;
            }

            public Guid UnitId { get; }
            public int HP { get; }
            public int SP { get; }
            public Vector2Int Position { get; }
            public Guid? CurrentActionId { get; }
            public int RecoveryUntilTick { get; }
            public UnitStatusFlags StatusFlags { get; }
            public bool HasBaseline { get; }
        }

        private sealed class ReadOnlyEffectRuntimeStateView : IReadOnlyEffectRuntimeState
        {
            public ReadOnlyEffectRuntimeStateView(EffectRuntimeState source)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                EffectId = source.EffectId;
                SourceId = source.SourceId;
                TargetId = source.TargetId;
                RemainingTick = source.RemainingTick;
                StackCount = source.StackCount;
                TickInterval = source.TickInterval;
                NextTickIn = source.NextTickIn;
                Magnitude = source.Magnitude;
                IsExpired = source.IsExpired;
                RemainingDuration = source.RemainingDuration;
                AppliedTick = source.AppliedTick;
                Lifecycle = source.Lifecycle;
                TimingPhase = source.TimingPhase;
                ActionSpeedLevel = source.ActionSpeedLevel;
                IsReaction = source.IsReaction;
                IsHidden = source.IsHidden;
                StackPolicy = source.StackPolicy;
                MaxStackCap = source.MaxStackCap;
                MaxApplicationsPerTick = source.MaxApplicationsPerTick;
            }

            public string EffectId { get; }
            public Guid SourceId { get; }
            public Guid TargetId { get; }
            public int RemainingTick { get; }
            public int StackCount { get; }
            public int TickInterval { get; }
            public int NextTickIn { get; }
            public float Magnitude { get; }
            public bool IsExpired { get; }
            public int RemainingDuration { get; }
            public int AppliedTick { get; }
            public EffectLifecycle Lifecycle { get; }
            public EffectTimingPhase TimingPhase { get; }
            public ActionSpeedTier ActionSpeedLevel { get; }
            public bool IsReaction { get; }
            public bool IsHidden { get; }
            public EffectStackPolicy StackPolicy { get; }
            public int MaxStackCap { get; }
            public int MaxApplicationsPerTick { get; }
        }
    }
}
