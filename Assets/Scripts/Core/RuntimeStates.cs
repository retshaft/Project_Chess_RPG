using System;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core
{
    [Flags]
    public enum UnitStatusFlags
    {
        None = 0,
        Dead = 1 << 0,
        MoveLocked = 1 << 1,
        AttackLocked = 1 << 2
    }

    [Serializable]
    public sealed class AbilityRuntimeState
    {
        public AbilityRuntimeState()
        {
        }

        public AbilityRuntimeState(string abilityId, int charges = 0)
        {
            AbilityId = abilityId ?? string.Empty;
            Charges = Mathf.Max(0, charges);
        }

        public string AbilityId { get; private set; } = string.Empty;
        public int CooldownRemaining { get; private set; }
        public int CooldownEndTick { get; private set; }
        public int Charges { get; private set; }
        public bool Locked { get; private set; }
        public Guid? PendingActionId { get; private set; }
        public int LastCommittedTick { get; private set; }

        public void SetIdentity(string abilityId, int charges)
        {
            AbilityId = abilityId ?? string.Empty;
            Charges = Mathf.Max(0, charges);
        }

        public void CommitQueuedAction(Guid actionId, int currentTick, int cooldownTicks, string actionOwnerName, string cooldownOwnerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(actionOwnerName, OwnershipStateKeys.ActionState);
            OwnershipValidationService.Default.EnsureAuthorized(cooldownOwnerName, OwnershipStateKeys.Cooldown);

            PendingActionId = actionId == Guid.Empty ? null : actionId;
            Locked = true;
            CooldownEndTick = Mathf.Max(currentTick, currentTick + Mathf.Max(0, cooldownTicks));
            CooldownRemaining = Mathf.Max(0, CooldownEndTick - currentTick);
            LastCommittedTick = currentTick;
        }

        public void CompleteQueuedAction(Guid actionId, int currentTick, string actionOwnerName, string cooldownOwnerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(actionOwnerName, OwnershipStateKeys.ActionState);
            OwnershipValidationService.Default.EnsureAuthorized(cooldownOwnerName, OwnershipStateKeys.Cooldown);

            if (PendingActionId == actionId)
                PendingActionId = null;

            CooldownRemaining = Mathf.Max(0, CooldownEndTick - currentTick);
            if (!PendingActionId.HasValue && CooldownRemaining == 0)
                Locked = false;
        }

        public void UpdateCooldown(int currentTick, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.Cooldown);

            CooldownRemaining = Mathf.Max(0, CooldownEndTick - currentTick);
            if (!PendingActionId.HasValue && CooldownRemaining == 0)
                Locked = false;
        }

        public void SetCooldownSnapshot(int cooldownRemaining, bool locked, string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.Cooldown);

            CooldownRemaining = Mathf.Max(0, cooldownRemaining);
            Locked = locked;
        }
    }
}
