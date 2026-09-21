using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Data;
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
        private readonly Func<Guid, int> _attackCountLookup;
        private readonly Func<Guid, float> _attackDamageRatioLookup;
        private readonly Func<Guid, int> _attackGraceTicksLookup;
        private readonly Func<Guid, ChessPieceType> _attackPieceTypeLookup;
        private readonly Func<Guid, bool> _attackIsEnemyLookup;

        /// <param name="runtime">The cloned runtime to query.</param>
        /// <param name="isCellValid">
        /// Returns <c>true</c> when a board cell is within grid bounds.
        /// Pass <c>_ => true</c> when no board constraint is required.
        /// </param>
        /// <param name="attackRangeLookup">
        /// Returns the attack range (in Chebyshev distance) for a given unit id.
        /// Pass <c>_ => 1</c> for the default melee range.
        /// </param>
        /// <param name="attackCountLookup">Returns attack count for a given unit id.</param>
        /// <param name="attackDamageRatioLookup">Returns attack damage ratio for a given unit id.</param>
        /// <param name="criticalDamageMultiplier">Multiplier applied to critical hits.</param>
        /// <param name="attackGraceTicksLookup">Returns the move-attack grace ticks for a given unit id.</param>
        /// <param name="attackPieceTypeLookup">
        /// Returns the attacker's <see cref="ChessPieceType"/>. Provided together with
        /// <paramref name="attackIsEnemyLookup"/>, range checks use the same piece rules as the
        /// real battle context (<see cref="CombatPatternRules"/>) against clone positions.
        /// When either is null the legacy Chebyshev fallback is used (headless callers).
        /// </param>
        /// <param name="attackIsEnemyLookup">Returns whether the attacker is on the enemy team.</param>
        public PredictionBattleContext(
            SimulationRuntime runtime,
            Func<Vector2Int, bool> isCellValid,
            Func<Guid, int> attackRangeLookup,
            Func<Guid, int> attackCountLookup = null,
            Func<Guid, float> attackDamageRatioLookup = null,
            int criticalDamageMultiplier = 2,
            Func<Guid, int> attackGraceTicksLookup = null,
            Func<Guid, ChessPieceType> attackPieceTypeLookup = null,
            Func<Guid, bool> attackIsEnemyLookup = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _isCellValid = isCellValid ?? throw new ArgumentNullException(nameof(isCellValid));
            _attackRangeLookup = attackRangeLookup ?? throw new ArgumentNullException(nameof(attackRangeLookup));
            _attackCountLookup = attackCountLookup;
            _attackDamageRatioLookup = attackDamageRatioLookup;
            _attackGraceTicksLookup = attackGraceTicksLookup;
            _attackPieceTypeLookup = attackPieceTypeLookup;
            _attackIsEnemyLookup = attackIsEnemyLookup;
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
                    state.StatusFlags,
                    state.LastNormalMoveFromCell,
                    state.LastNormalMoveTick);
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

            return IsWithinAttackRange(attackerId, attacker.Position, target.Position);
        }

        public bool IsTargetInAttackRangeWithGrace(Guid attackerId, Guid targetId, int attackQueuedTick, int currentTick)
        {
            if (!TryGetUnit(attackerId, out BattleUnitSnapshot attacker))
                return false;
            if (!TryGetUnit(targetId, out BattleUnitSnapshot target))
                return false;

            int graceTicks = _attackGraceTicksLookup != null ? _attackGraceTicksLookup(attackerId) : 0;
            return AttackGraceRules.IsHitAllowed(
                attacker.Position,
                target.Position,
                new AttackGraceRules.TargetMoveRecord(target.LastNormalMoveFromCell, target.LastNormalMoveTick),
                attacker.LastNormalMoveTick,
                attackQueuedTick,
                currentTick,
                graceTicks,
                (attackerCell, targetCell) => IsWithinAttackRange(attackerId, attackerCell, targetCell));
        }

        /// <summary>
        /// 실전(RuntimeBattleContext)과 같은 기물 사거리 규칙으로 판정한다. 기물·팀 룩업이 주어지면
        /// <see cref="CombatPatternRules"/>를 클론 점유 샘플러와 함께 쓰고, 없으면 기존 체비셰프
        /// 거리 폴백(헤드리스 호환)을 유지한다.
        /// </summary>
        private bool IsWithinAttackRange(Guid attackerId, Vector2Int attackerCell, Vector2Int targetCell)
        {
            int range = Math.Max(1, _attackRangeLookup(attackerId));

            if (_attackPieceTypeLookup != null && _attackIsEnemyLookup != null)
            {
                Func<Vector2Int, Vector2Int, bool> judge = AttackGraceRules.BuildPieceReachJudge(
                    _attackPieceTypeLookup(attackerId),
                    _attackIsEnemyLookup(attackerId),
                    range,
                    sampleCell =>
                    {
                        IReadOnlyList<IReadOnlyUnitRuntimeState> occupants = _runtime.GetUnitsAtPosition(sampleCell);
                        return occupants != null && occupants.Count > 0;
                    });
                return judge(attackerCell, targetCell);
            }

            // 헤드리스 폴백 — 기존 예측 규칙(기물 구분 없는 최대축 거리).
            int distance = Math.Max(
                Math.Abs(attackerCell.x - targetCell.x),
                Math.Abs(attackerCell.y - targetCell.y));

            return distance <= range;
        }

        public int GetAttackCount(Guid unitId)
        {
            return _attackCountLookup != null ? Math.Max(1, _attackCountLookup(unitId)) : 1;
        }

        public float GetAttackDamageRatio(Guid unitId)
        {
            return _attackDamageRatioLookup != null ? Math.Max(0f, _attackDamageRatioLookup(unitId)) : 1f;
        }

        public float GetDefPenetrationRatio(Guid attackerId, Guid targetId)
        {
            // Prediction 환경에서는 패시브 상태 등을 직접 가져오기 어렵기 때문에,
            // 별도의 Lookup Delegate를 추가하거나 임시로 0f를 반환한다.
            // 추후 필요시 _defPenetrationLookup 등을 통해 주입받을 수 있다.
            return 0f;
        }

        public float GetAttackDamageMultiplier(Guid unitId)
        {
            return 1f;
        }

        public void NotifyAttackPerformed(Guid unitId)
        {
            // Prediction 환경에서는 실제 소모를 처리하지 않음
        }

        public void NotifyAttackStart(Guid attackerId, Guid targetId)
        {
            // Prediction 환경에서는 패시브 이벤트를 발생시키지 않음
        }

        public int GetOnHitEffectCount(Guid unitId, CheckmateRPG.Core.StatusEffectType type)
        {
            return 0;
        }
    }
}
