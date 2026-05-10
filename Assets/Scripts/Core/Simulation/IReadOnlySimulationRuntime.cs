using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation
{
    public interface IReadOnlySimulationRuntime
    {
        int CurrentTick { get; }
        IReadOnlyDictionary<Guid, IReadOnlyUnitRuntimeState> RuntimeStates { get; }
        IReadOnlyDictionary<Guid, IReadOnlyActionState> ActiveActions { get; }
        IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> ActiveEffects { get; }
        IReadOnlyDictionary<Vector2Int, Guid> OccupiedPositions { get; }

        IReadOnlyUnitRuntimeState GetUnit(Guid unitId);
        bool TryGetUnit(Guid unitId, out IReadOnlyUnitRuntimeState unit);
        IReadOnlyActionState GetAction(Guid actionId);
        bool TryGetAction(Guid actionId, out IReadOnlyActionState action);
        bool TryGetEffect(string effectKey, out IReadOnlyEffectRuntimeState effect);
        IReadOnlyList<IReadOnlyUnitRuntimeState> GetUnitsAtPosition(Vector2Int position);
        bool IsOccupied(Vector2Int position);
    }
}
