using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation
{
    public sealed class SimulationRuntime
    {
        private static readonly IReadOnlyDictionary<Guid, UnitRuntimeState> EmptyRuntimeStates = new Dictionary<Guid, UnitRuntimeState>();
        private static readonly IReadOnlyDictionary<Guid, IActionCommand> EmptyActiveActions = new Dictionary<Guid, IActionCommand>();
        private static readonly IReadOnlyDictionary<string, EffectRuntimeState> EmptyActiveEffects = new Dictionary<string, EffectRuntimeState>(StringComparer.Ordinal);
        private static readonly IReadOnlyDictionary<Vector2Int, Guid> EmptyOccupiedPositions = new Dictionary<Vector2Int, Guid>();

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
            RuntimeStates = runtimeStates ?? throw new ArgumentNullException(nameof(runtimeStates));
            ActiveActions = activeActions ?? throw new ArgumentNullException(nameof(activeActions));
            ActiveEffects = activeEffects ?? throw new ArgumentNullException(nameof(activeEffects));
            OccupiedPositions = occupiedPositions ?? throw new ArgumentNullException(nameof(occupiedPositions));
        }

        public int CurrentTick { get; }
        public IReadOnlyDictionary<Guid, UnitRuntimeState> RuntimeStates { get; }
        public IReadOnlyDictionary<Guid, IActionCommand> ActiveActions { get; }
        public IReadOnlyDictionary<string, EffectRuntimeState> ActiveEffects { get; }
        public IReadOnlyDictionary<Vector2Int, Guid> OccupiedPositions { get; }
    }
}
