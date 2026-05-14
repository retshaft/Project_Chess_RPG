using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// A read-only <see cref="IBattleContext"/> implementation backed by the cloned
    /// <see cref="SimulationRuntime"/> inside a <see cref="PredictionContext"/>.
    /// <para>
    /// Cell validity and attack range are provided by caller-supplied delegates so that
    /// the prediction sandbox has no dependency on scene singletons such as
    /// <c>GridSystem.Instance</c>.
    /// </para>
    /// </summary>
    internal sealed class PredictionBattleContext : IBattleContext
    {
        private readonly SimulationRuntime _runtime;
        private readonly Func<Vector2Int, bool> _isCellValid;
        private readonly Func<Guid, int> _attackRangeLookup;

        /// <param name="runtime">The cloned runtime to query.</param>
        /// <param name="isCellValid">
        /// Returns <c>true</c> when a board cell is within grid bounds.
        /// Pass <c>_ => true</c> when no board constraint is required.
        /// </param>
        /// <param name="attackRangeLookup">
        /// Returns the attack range (in Chebyshev distance) for a given unit id.
        /// Pass <c>_ => 1</c> for the default melee range.
        /// </param>
        /// <param name="criticalDamageMultiplier">Multiplier applied to critical hits.</param>
        public PredictionBattleContext(
            SimulationRuntime runtime,
            Func<Vector2Int, bool> isCellValid,
            Func<Guid, int> attackRangeLookup,
            int criticalDamageMultiplier = 2)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _isCellValid = isCellValid ?? throw new ArgumentNullException(nameof(isCellValid));
            _attackRangeLookup = attackRangeLookup ?? throw new ArgumentNullException(nameof(attackRangeLookup));
            CriticalDamageMultiplier = Math.Max(1, criticalDamageMultiplier);
        }

        public int CriticalDamageMultiplier { get; }

        public bool TryGetUnit(Guid unitId, out BattleUnitSnapshot unit)
        {
            if (_runtime.TryGetUnit(unitId, out IReadOnlyUnitRuntimeState state))
            {
                unit = new BattleUnitSnapshot(
                    state.UnitId,
                    state.Position,
                    state.HP,
                    state.StatusFlags);
                return true;
            }

            unit = default;
            return false;
        }

        public bool IsCellValid(Vector2Int cell) => _isCellValid(cell);

        public bool IsCellOccupied(Vector2Int cell, Guid ignoredUnitId = default)
        {
            if (!_runtime.IsOccupied(cell))
                return false;

            IReadOnlyList<IReadOnlyUnitRuntimeState> occupants = _runtime.GetUnitsAtPosition(cell);
            if (occupants.Count == 0)
                return false;

            if (ignoredUnitId != Guid.Empty)
            {
                for (int i = 0; i < occupants.Count; i++)
                {
                    if (occupants[i] != null && occupants[i].UnitId != ignoredUnitId)
                        return true;
                }

                return false;
            }

            return true;
        }

        public bool IsTargetInAttackRange(Guid attackerId, Guid targetId)
        {
            if (!TryGetUnit(attackerId, out BattleUnitSnapshot attacker))
                return false;
            if (!TryGetUnit(targetId, out BattleUnitSnapshot target))
                return false;

            int range = Math.Max(1, _attackRangeLookup(attackerId));
            int distance = Math.Max(
                Math.Abs(attacker.Position.x - target.Position.x),
                Math.Abs(attacker.Position.y - target.Position.y));

            return distance <= range;
        }
    }
}
