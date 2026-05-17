using System;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime
{
    [Serializable]
    public sealed class UnitRuntimeState : IReadOnlyUnitRuntimeState
    {
        [SerializeField] private bool _hasBaseline;

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
            Position = position;
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
