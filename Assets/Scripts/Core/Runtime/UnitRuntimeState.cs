using System;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime
{
    [Serializable]
    public sealed class UnitRuntimeState
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
            SP = source.SP;
            Position = source.Position;
            CurrentActionId = source.CurrentActionId;
            RecoveryUntilTick = source.RecoveryUntilTick;
            StatusFlags = source.StatusFlags;
        }

        public Guid UnitId { get; private set; }
        public int HP { get; private set; }
        public int SP { get; private set; }
        public Vector2Int Position { get; private set; }
        public Guid? CurrentActionId { get; private set; }
        public int RecoveryUntilTick { get; private set; }
        public UnitStatusFlags StatusFlags { get; private set; }
        public bool HasBaseline => _hasBaseline;

        public void SeedBaseline(Guid unitId, int hp, int sp, Vector2Int position, Guid? currentActionId, int recoveryUntilTick, UnitStatusFlags statusFlags)
        {
            _hasBaseline = true;
            UnitId = unitId;
            HP = Mathf.Max(0, hp);
            SP = sp;
            Position = position;
            CurrentActionId = currentActionId;
            RecoveryUntilTick = Mathf.Max(0, recoveryUntilTick);
            StatusFlags = statusFlags;
        }

        public void SyncDerivedState(Guid unitId, int sp, UnitStatusFlags statusFlags)
        {
            UnitId = unitId;
            SP = sp;
            StatusFlags = statusFlags;
        }

        public void SetHP(int hp, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.HP);
            HP = Mathf.Max(0, hp);
        }

        public void SetPosition(Vector2Int position, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.Position);
            Position = position;
        }

        public void SetActionState(Guid? currentActionId, int recoveryUntilTick, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.ActionState);
            CurrentActionId = currentActionId;
            RecoveryUntilTick = Mathf.Max(0, recoveryUntilTick);
        }

        public void AddStatusFlag(UnitStatusFlags flag)
        {
            StatusFlags |= flag;
        }
    }
}
