using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation
{
    public sealed class SimulationRuntime
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

            RuntimeStates = new ReadOnlyDictionary<Guid, UnitRuntimeState>(_runtimeStates);
            ActiveActions = new ReadOnlyDictionary<Guid, IActionCommand>(_activeActions);
            ActiveEffects = new ReadOnlyDictionary<string, EffectRuntimeState>(_activeEffects);
            OccupiedPositions = new ReadOnlyDictionary<Vector2Int, Guid>(_occupiedPositions);
        }

        public int CurrentTick { get; private set; }
        public IReadOnlyDictionary<Guid, UnitRuntimeState> RuntimeStates { get; }
        public IReadOnlyDictionary<Guid, IActionCommand> ActiveActions { get; }
        public IReadOnlyDictionary<string, EffectRuntimeState> ActiveEffects { get; }
        public IReadOnlyDictionary<Vector2Int, Guid> OccupiedPositions { get; }

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

        public void SetCurrentTick(int tick)
        {
            CurrentTick = tick;
        }

        public UnitRuntimeState GetUnit(Guid unitId)
        {
            if (!TryGetUnit(unitId, out UnitRuntimeState unit))
                throw new KeyNotFoundException($"Unit runtime state not found for '{unitId:N}'.");

            return unit;
        }

        public bool TryGetUnit(Guid unitId, out UnitRuntimeState unit)
        {
            return _runtimeStates.TryGetValue(unitId, out unit) && unit != null;
        }

        public IActionCommand GetAction(Guid actionId)
        {
            if (!_activeActions.TryGetValue(actionId, out IActionCommand action) || action == null)
                throw new KeyNotFoundException($"Action runtime state not found for '{actionId:N}'.");

            return action;
        }

        public bool TryGetAction(Guid actionId, out IActionCommand action)
        {
            return _activeActions.TryGetValue(actionId, out action) && action != null;
        }

        public bool TryGetEffect(string effectKey, out EffectRuntimeState effect)
        {
            return !string.IsNullOrWhiteSpace(effectKey) &&
                   _activeEffects.TryGetValue(effectKey, out effect) &&
                   effect != null;
        }

        public IReadOnlyList<UnitRuntimeState> GetUnitsAtPosition(Vector2Int position)
        {
            if (!_occupiedPositions.TryGetValue(position, out Guid unitId) || !TryGetUnit(unitId, out UnitRuntimeState unit))
                return Array.Empty<UnitRuntimeState>();

            return new[] { unit };
        }

        public bool IsOccupied(Vector2Int position)
        {
            return _occupiedPositions.ContainsKey(position);
        }

        public void RegisterUnit(UnitRuntimeState unitState)
        {
            if (unitState == null || unitState.UnitId == Guid.Empty)
                return;

            _runtimeStates[unitState.UnitId] = new UnitRuntimeState(unitState);
            RefreshUnitPositionOwnership(unitState.UnitId);
        }

        public void RegisterAction(IActionCommand action)
        {
            if (action == null || action.ActionId == Guid.Empty)
                return;

            SimulationActionSnapshot snapshot = SimulationActionSnapshot.From(action);
            if (snapshot == null)
                return;

            _activeActions[action.ActionId] = snapshot;
        }

        public void ReplaceActions(IReadOnlyCollection<IActionCommand> actions)
        {
            _activeActions.Clear();
            if (actions == null || actions.Count == 0)
                return;

            foreach (IActionCommand action in actions)
                RegisterAction(action);
        }

        public void RegisterEffect(EffectRuntimeState effect)
        {
            if (effect == null)
                return;

            RegisterEffect(BuildEffectKey(effect), effect);
        }

        public void RegisterEffect(string effectKey, EffectRuntimeState effect)
        {
            if (string.IsNullOrWhiteSpace(effectKey) || effect == null)
                return;

            _activeEffects[effectKey] = new EffectRuntimeState(effect);
        }

        public void UnregisterUnit(Guid unitId)
        {
            if (unitId == Guid.Empty)
                return;

            CleanupUnitRuntimeArtifacts(unitId);
            _runtimeStates.Remove(unitId);
        }

        public void UnregisterAction(Guid actionId)
        {
            if (actionId == Guid.Empty)
                return;

            _activeActions.Remove(actionId);
        }

        public void UnregisterEffect(string effectKey)
        {
            if (string.IsNullOrWhiteSpace(effectKey))
                return;

            _activeEffects.Remove(effectKey);
        }

        public void SetUnitDerivedState(Guid unitId, int sp, UnitStatusFlags statusFlags)
        {
            if (!TryGetUnit(unitId, out UnitRuntimeState state))
                return;

            state.SyncDerivedState(unitId, sp, statusFlags);
        }

        public void SetUnitHP(Guid unitId, int hp, string ownerName)
        {
            if (!TryGetUnit(unitId, out UnitRuntimeState state))
                return;

            state.SetHP(hp, ownerName);
        }

        public void SetUnitPosition(Guid unitId, Vector2Int position, string ownerName)
        {
            if (!TryGetUnit(unitId, out UnitRuntimeState state))
                return;

            _occupiedPositions.Remove(state.Position);
            state.SetPosition(position, ownerName);
            _occupiedPositions[position] = unitId;
        }

        public void SetUnitActionState(Guid unitId, Guid? currentActionId, int recoveryUntilTick, string ownerName)
        {
            if (!TryGetUnit(unitId, out UnitRuntimeState state))
                return;

            state.SetActionState(currentActionId, recoveryUntilTick, ownerName);
        }

        public void AddUnitStatusFlag(Guid unitId, UnitStatusFlags flag)
        {
            if (!TryGetUnit(unitId, out UnitRuntimeState state))
                return;

            state.AddStatusFlag(flag);
        }

        public void CleanupUnitRuntimeArtifacts(Guid unitId)
        {
            if (!TryGetUnit(unitId, out UnitRuntimeState state))
                return;

            var actionIdsToRemove = new List<Guid>();
            foreach (KeyValuePair<Guid, IActionCommand> pair in _activeActions)
            {
                if (pair.Value != null && pair.Value.ActorId == unitId)
                    actionIdsToRemove.Add(pair.Key);
            }

            for (int i = 0; i < actionIdsToRemove.Count; i++)
                _activeActions.Remove(actionIdsToRemove[i]);

            var effectKeysToRemove = new List<string>();
            foreach (KeyValuePair<string, EffectRuntimeState> pair in _activeEffects)
            {
                if (pair.Value != null && pair.Value.TargetId == unitId)
                    effectKeysToRemove.Add(pair.Key);
            }

            for (int i = 0; i < effectKeysToRemove.Count; i++)
                _activeEffects.Remove(effectKeysToRemove[i]);

            _occupiedPositions.Remove(state.Position);
        }

        public void ApplyDeadUnitLifecycle(Guid unitId, int currentTick, string actionOwnerName)
        {
            SetUnitActionState(unitId, null, currentTick, actionOwnerName);
            AddUnitStatusFlag(unitId, UnitStatusFlags.Dead);
            CleanupUnitRuntimeArtifacts(unitId);
        }

        public SimulationRuntime CreateSnapshot(int tick)
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

            if (TryGetUnit(unitId, out UnitRuntimeState unit))
                _occupiedPositions[unit.Position] = unitId;
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
                SimulationActionSnapshot action = SimulationActionSnapshot.From(pair.Value);
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
    }
}
