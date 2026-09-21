using System;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime
{
    [Serializable]
    public sealed class UnitRuntimeState : IReadOnlyUnitRuntimeState
    {
        [SerializeField] private bool _hasBaseline;
        [SerializeField] private Vector2Int _lastNormalMoveFromCell;
        [SerializeField] private int _lastNormalMoveTick = -1;

        public UnitRuntimeState()
        {
        }

        public UnitRuntimeState(UnitRuntimeState source)
        {
            if (source == null)
                return;

            _hasBaseline = source._hasBaseline;
            UnitId = source.UnitId;
            HP = source.HP;
            CurrentSP = source.CurrentSP;
            MaxSP = source.MaxSP;
            Position = source.Position;
            CurrentActionId = source.CurrentActionId;
            RecoveryUntilTick = source.RecoveryUntilTick;
            StatusFlags = source.StatusFlags;
            _lastNormalMoveFromCell = source._lastNormalMoveFromCell;
            _lastNormalMoveTick = source._lastNormalMoveTick;
        }

        public Guid UnitId { get; private set; }
        public int HP { get; private set; }
        public int CurrentSP { get; private set; }
        public int MaxSP { get; private set; }
        public int SP => CurrentSP;
        public Vector2Int Position { get; private set; }
        public Guid? CurrentActionId { get; private set; }
        public int RecoveryUntilTick { get; private set; }
        public UnitStatusFlags StatusFlags { get; private set; }
        public bool HasBaseline => _hasBaseline;
        public Vector2Int LastNormalMoveFromCell => _lastNormalMoveFromCell;
        public int LastNormalMoveTick => _lastNormalMoveTick;

        internal void SeedBaseline(Guid unitId, int hp, int currentSP, int maxSP, Vector2Int position, Guid? currentActionId, int recoveryUntilTick, UnitStatusFlags statusFlags)
        {
            _hasBaseline = true;
            UnitId = unitId;
            HP = Mathf.Max(0, hp);
            int clampedMaxSp = Mathf.Max(0, maxSP);
            MaxSP = clampedMaxSp;
            CurrentSP = Mathf.Clamp(currentSP, 0, clampedMaxSp);
            Position = position;
            CurrentActionId = currentActionId;
            RecoveryUntilTick = Mathf.Max(0, recoveryUntilTick);
            StatusFlags = statusFlags;
            _lastNormalMoveFromCell = Vector2Int.zero;
            _lastNormalMoveTick = -1;
        }

        internal void SyncDerivedState(Guid unitId, UnitStatusFlags statusFlags)
        {
            UnitId = unitId;
            StatusFlags = statusFlags;
        }

        internal bool SetSP(int currentSP, int maxSP, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.SP);
            int clampedMaxSp = Mathf.Max(0, maxSP);
            int clampedCurrent = Mathf.Clamp(currentSP, 0, clampedMaxSp);
            bool changed = clampedCurrent != CurrentSP || clampedMaxSp != MaxSP;
            CurrentSP = clampedCurrent;
            MaxSP = clampedMaxSp;
            return changed;
        }

        internal void SetHP(int hp, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.HP);
            HP = Mathf.Max(0, hp);
        }

        internal void SetPosition(Vector2Int position, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.Position);
            if (Position != position)
            {
                // 위치가 실제로 바뀌면(정상 이동 포함, 넉백 등 강제 이동 포함) 이전 정상 이동 기록은
                // 더 이상 유닛의 "직전 상태"를 대표하지 않는다. 정상 이동은 적용 프로세서가
                // SetPosition 직후 다시 기록하므로 여기서 무효화해도 정상 이동 유예는 유지된다.
                _lastNormalMoveFromCell = default;
                _lastNormalMoveTick = -1;
            }
            Position = position;
        }

        /// <summary>
        /// 마지막 정상 이동(넉백·강제 이동 제외)의 직전 셀과 논리 좌표 확정 틱을 기록한다.
        /// 점유 상태는 변경하지 않는다 — 이동 유예 판정용 최소 기록이다.
        /// </summary>
        internal void RecordNormalMove(Vector2Int fromCell, int tick, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.Position);
            _lastNormalMoveFromCell = fromCell;
            _lastNormalMoveTick = tick;
        }

        internal void SetActionState(Guid? currentActionId, int recoveryUntilTick, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.ActionState);
            CurrentActionId = currentActionId;
            RecoveryUntilTick = Mathf.Max(0, recoveryUntilTick);
        }

        internal void AddStatusFlag(UnitStatusFlags flag)
        {
            StatusFlags |= flag;
        }
    }
}
