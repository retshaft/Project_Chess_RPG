#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.Editor
{
    /// <summary>
    /// M2 대표 넉백+Splat 콤보 시연 도구 (Docs/MILESTONES.md M2 "대표 넉백/Splat 콤보 1개를 BattleScene에 배치").
    ///
    /// BattleManager F1 디버그 시나리오(ExecuteMultiCollisionScenario)와 동일한 런타임 경로만 재사용한다:
    /// TryEnqueueAbility(디버그 푸시 능력) → AbilityActionResolvedEvent → KnockbackMutation(ApplySplatDamage: true).
    /// 넉백 거리(힘−실효무게)는 PhysicsRules, Splat 피해(양측 MaxHP 10%)는 MovementMutationProcessor의
    /// 기존 규격이 계산하며, 이 도구는 새 전투 규칙·수식을 추가하지 않는다.
    /// 유닛을 새로 생성하지 않고 기존 유닛을 배치만 바꾸므로 반복 실행해도 중복 오브젝트가 생기지 않는다.
    /// </summary>
    public static class RepresentativeComboSetupEditor
    {
        private const string ComboAbilityId = "debug_force_push_m2";
        private const string LogTag = "[M2Combo]";
        // F1 시나리오(BattleManager.ExecuteMultiCollisionScenario)와 동일한 비틀거림 지속값.
        private const float StaggerDurationSeconds = 8f;
        private const int ComboRow = 4;

        // 해석 대기 중인 푸시 상태. 무장 중 재실행을 막아 같은 전투에서 반복 실행해도 결과가 동일하게 유지된다.
        private static bool _pushArmed;
        private static Guid _pendingActionId = Guid.Empty;
        private static UnitBrain _pendingTarget;
        private static Vector2Int _pendingDirection;
        private static int _pendingForce;
        private static bool _hooksAttached;
        private static IEventBus _subscribedBus;

        [MenuItem("Tools/Checkmate RPG/M2 Representative Combo/1. Place Combo Units (Play Mode)", true)]
        [MenuItem("Tools/Checkmate RPG/M2 Representative Combo/2. Run Knockback+Splat Combo (Play Mode)", true)]
        private static bool IsPlayModeAvailable()
        {
            return Application.isPlaying;
        }

        [MenuItem("Tools/Checkmate RPG/M2 Representative Combo/1. Place Combo Units (Play Mode)", false, 20)]
        private static void PlaceComboUnits()
        {
            if (!TryPrepareComboUnits(out _, out _, out _, out _, out _, out string planSummary))
                return;

            Debug.Log($"{LogTag} 유닛 배치만 완료(푸시 미접수). {planSummary}");
        }

        [MenuItem("Tools/Checkmate RPG/M2 Representative Combo/2. Run Knockback+Splat Combo (Play Mode)", false, 21)]
        private static void RunKnockbackSplatCombo()
        {
            if (!TryPrepareComboUnits(out UnitBrain actor, out UnitBrain target, out _, out Vector2Int direction, out int force, out string planSummary))
                return;

            if (!TryGetRuntime(out ActionRuntimeController runtime))
                return;

            AbilityDefinition ability = BuildComboAbilityDefinition();
            if (!runtime.TryEnqueueAbility(actor, ability, new[] { target.ActorId }))
            {
                Debug.LogWarning($"{LogTag} 푸시 능력 접수 실패. 시전자의 행동 잠금/AP 상태를 확인하라.");
                return;
            }

            AttachResolveHook();
            _pushArmed = true;
            _pendingActionId = FindQueuedActionId(runtime, actor.ActorId);
            _pendingTarget = target;
            _pendingDirection = direction;
            _pendingForce = force;

            Debug.Log($"{LogTag} 푸시 접수 완료. {planSummary} ActionId={_pendingActionId:N}. " +
                      "유닛이 밀려 막히면 Splat(양측 MaxHP 10%, 고정 피해)이 기존 뮤테이션 체인으로 적용된다.");
        }

        // ─── 유닛 배치 ────────────────────────────────────────────────────────────

        private static bool TryPrepareComboUnits(
            out UnitBrain actor,
            out UnitBrain target,
            out UnitBrain wallUnit,
            out Vector2Int pushDirection,
            out int pushForce,
            out string planSummary)
        {
            actor = null;
            target = null;
            wallUnit = null;
            pushDirection = Vector2Int.left;
            pushForce = 0;
            planSummary = string.Empty;

            // Both menu entries must reject re-entry before moving any units.
            if (_pushArmed)
            {
                Debug.LogWarning($"{LogTag} 이전 푸시가 아직 해석 대기 중이다. 해석될 때까지 기다린 뒤 다시 실행하라.");
                return false;
            }

            ActionRuntimeController runtime = ActionRuntimeController.Instance;
            if (runtime == null || runtime.Scheduler == null || GridSystem.Instance == null)
            {
                Debug.LogError($"{LogTag} 런타임/그리드가 준비되지 않았다. BattleScene을 Play 중일 때 실행하라.");
                return false;
            }

            if (TickScheduler.EnsureExists().IsPaused)
            {
                Debug.LogWarning($"{LogTag} 틱이 정지돼 있다(배치 페이즈로 보인다). START BATTLE 이후에 실행하라.");
                return false;
            }

            CollectLivingUnits(out List<UnitBrain> allies, out List<UnitBrain> enemies);
            if (allies.Count == 0 || enemies.Count == 0)
            {
                Debug.LogError($"{LogTag} 생존한 아군/적 유닛이 없다. 유닛 스폰을 확인한 뒤 실행하라.");
                return false;
            }

            actor = allies[0];
            target = enemies[0];
            wallUnit = enemies.Count > 1 ? enemies[1] : null;
            int targetWeight = target.UnitData != null ? target.UnitData.Weight : 0;

            if (wallUnit != null && TryPlaceUnitCollisionCombo(actor, target, wallUnit, targetWeight, out pushDirection, out pushForce, out planSummary))
                return true;

            if (wallUnit != null)
                Debug.LogWarning($"{LogTag} 행 {ComboRow}에 기물 충돌 배치가 가능한 자리가 없어 벽 Splat 배치로 전환한다.");

            if (!TryPlaceWallSplatCombo(actor, target, targetWeight, out pushDirection, out pushForce, out planSummary))
            {
                Debug.LogError($"{LogTag} 벽 Splat 배치도 불가능하다. 행 {ComboRow}의 가장자리 2칸을 비운 뒤 다시 실행하라.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 기물 충돌 Splat 배치: 더미(dx) — 빈 칸(dx+1) — 대상(dx+2) — 시전자(dx+3), 푸시 방향 ←.
        /// 힘=무게+2이면 비틀거림(실효무게 −1)과 보스 면역 어느 쪽이어도 PhysicsRules 거리가 2 이상이라
        /// 대상은 빈 칸 1칸을 지나 더미에 막히고, MovementMutationProcessor 규격대로 양측이 Splat 피해를 받는다.
        /// </summary>
        private static bool TryPlaceUnitCollisionCombo(
            UnitBrain actor,
            UnitBrain target,
            UnitBrain wallUnit,
            int targetWeight,
            out Vector2Int pushDirection,
            out int pushForce,
            out string planSummary)
        {
            pushDirection = Vector2Int.left;
            pushForce = 0;
            planSummary = string.Empty;

            for (int dx = 0; dx + 3 < GridSystem.GridWidth; dx++)
            {
                Vector2Int dummyCell = new(dx, ComboRow);
                Vector2Int pathCell = new(dx + 1, ComboRow);
                Vector2Int targetCell = new(dx + 2, ComboRow);
                Vector2Int actorCell = new(dx + 3, ComboRow);

                if (!CanPlaceUnit(wallUnit, dummyCell) || !CanPlaceUnit(target, targetCell) || !CanPlaceUnit(actor, actorCell))
                    continue;
                if (!IsCellFreeForPush(pathCell, target))
                    continue;

                ApplyPlacement(wallUnit, dummyCell);
                ApplyPlacement(target, targetCell);
                ApplyPlacement(actor, actorCell);
                ApplyStagger(target, actor);

                pushForce = targetWeight + 2;
                pushDirection = Vector2Int.left;
                planSummary = $"Mode=UnitCollision, Actor={actor.name}({actorCell}), Target={target.name}({targetCell}), " +
                              $"Dummy={wallUnit.name}({dummyCell}), Force={pushForce}, TargetWeight={targetWeight}";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 벽 Splat 대체 배치(생존 적이 1뿐일 때): 대상을 보드 가장자리에 세우고 안쪽에서 민다.
        /// 힘=무게+1이면 비틀거림/보스 여부와 무관하게 거리 1 이상이 보장되어 가장자리 밖으로 밀려 벽 Splat이 된다.
        /// </summary>
        private static bool TryPlaceWallSplatCombo(
            UnitBrain actor,
            UnitBrain target,
            int targetWeight,
            out Vector2Int pushDirection,
            out int pushForce,
            out string planSummary)
        {
            pushDirection = Vector2Int.left;
            pushForce = 0;
            planSummary = string.Empty;

            Vector2Int leftTargetCell = new(0, ComboRow);
            Vector2Int leftActorCell = new(1, ComboRow);
            if (CanPlaceUnit(target, leftTargetCell) && CanPlaceUnit(actor, leftActorCell))
            {
                ApplyPlacement(target, leftTargetCell);
                ApplyPlacement(actor, leftActorCell);
                ApplyStagger(target, actor);

                pushDirection = Vector2Int.left;
                pushForce = targetWeight + 1;
                planSummary = $"Mode=WallSplat, Actor={actor.name}({leftActorCell}), Target={target.name}({leftTargetCell}), " +
                              $"Force={pushForce}, TargetWeight={targetWeight}";
                return true;
            }

            Vector2Int rightTargetCell = new(GridSystem.GridWidth - 1, ComboRow);
            Vector2Int rightActorCell = new(GridSystem.GridWidth - 2, ComboRow);
            if (CanPlaceUnit(target, rightTargetCell) && CanPlaceUnit(actor, rightActorCell))
            {
                ApplyPlacement(target, rightTargetCell);
                ApplyPlacement(actor, rightActorCell);
                ApplyStagger(target, actor);

                pushDirection = Vector2Int.right;
                pushForce = targetWeight + 1;
                planSummary = $"Mode=WallSplat, Actor={actor.name}({rightActorCell}), Target={target.name}({rightTargetCell}), " +
                              $"Force={pushForce}, TargetWeight={targetWeight}";
                return true;
            }

            return false;
        }

        private static void ApplyPlacement(UnitBrain unit, Vector2Int cell)
        {
            if (unit == null || unit.Movement == null)
                return;

            unit.Movement.ApplyResolvedMovement(cell);
            ActionRuntimeController.Instance?.SyncRuntimeState(unit);
        }

        private static void ApplyStagger(UnitBrain target, UnitBrain actor)
        {
            if (target == null || target.StatusEffects == null)
                return;

            // F1 시나리오와 동일 규격: 비틀거림은 PhysicsRules 계산에서 실효 무게를 −1한다(보스는 면역).
            target.StatusEffects.ApplyStatusEffect(
                StatusEffectType.Stagger,
                duration: StaggerDurationSeconds,
                stacks: 1,
                isSecondary: false,
                sourceActorId: actor != null ? actor.ActorId : (Guid?)null);
            ActionRuntimeController.Instance?.SyncRuntimeState(target);
        }

        private static bool CanPlaceUnit(UnitBrain unit, Vector2Int cell)
        {
            GridSystem grid = GridSystem.Instance;
            if (grid == null || unit == null || unit.Movement == null || !grid.IsValidCell(cell))
                return false;

            GameObject occupant = grid.GetOccupant(cell);
            return occupant == null || occupant == unit.gameObject;
        }

        private static bool IsCellFreeForPush(Vector2Int cell, UnitBrain mover)
        {
            GridSystem grid = GridSystem.Instance;
            if (grid == null)
                return false;

            GameObject occupant = grid.GetOccupant(cell);
            return occupant == null || (mover != null && occupant == mover.gameObject);
        }

        private static void CollectLivingUnits(out List<UnitBrain> allies, out List<UnitBrain> enemies)
        {
            allies = new List<UnitBrain>();
            enemies = new List<UnitBrain>();

            UnitBrain[] brains = UnityEngine.Object.FindObjectsByType<UnitBrain>(FindObjectsSortMode.None);
            foreach (UnitBrain brain in brains)
            {
                if (brain == null || brain.IsDead || brain.Movement == null || brain.ActorId == Guid.Empty)
                    continue;
                if (!brain.TryGetComponent(out TeamComponent team))
                    continue;

                if (team.IsPlayer)
                    allies.Add(brain);
                else
                    enemies.Add(brain);
            }

            // 실행할 때마다 같은 유닛을 고르도록 ActorId 순으로 정렬한다(결정성).
            allies.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
            enemies.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
        }

        // ─── 푸시 실행(F1과 동일한 능력 접수 → 해석 이벤트 → 넉백 뮤테이션) ───────────

        private static AbilityDefinition BuildComboAbilityDefinition()
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            definition.name = ComboAbilityId;
            definition.AbilityId = ComboAbilityId;
            var level = new AbilityLevelData
            {
                Cost = 0f,
                Cooldown = 0,
                CastSpeed = ActionSpeedTier.Fast,
                TargetingRule = AbilityTargetingRule.SingleTarget,
                Effects = new List<CheckmateRPG.Core.Abilities.IAbilityEffect>()
            };
            definition.Levels.Add(level);
            return definition;
        }

        private static Guid FindQueuedActionId(ActionRuntimeController runtime, Guid actorId)
        {
            List<IActionCommand> activeActions = new List<IActionCommand>(runtime.Scheduler.GetActiveActions());
            for (int i = activeActions.Count - 1; i >= 0; i--)
            {
                if (activeActions[i] is AbilityActionCommand abilityAction &&
                    abilityAction.ActorId == actorId &&
                    string.Equals(abilityAction.AbilityId, ComboAbilityId, StringComparison.Ordinal))
                {
                    return abilityAction.ActionId;
                }
            }

            return Guid.Empty;
        }

        private static void AttachResolveHook()
        {
            if (_hooksAttached)
                return;

            _subscribedBus = ActionRuntimeController.Instance?.EventBus;
            _subscribedBus?.Subscribe<AbilityActionResolvedEvent>(HandleAbilityResolved);
            _subscribedBus?.Subscribe<ActionInterruptedEvent>(HandleActionInterrupted);
            _subscribedBus?.Subscribe<ActionCancelledEvent>(HandleActionCancelled);
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            _hooksAttached = true;
        }

        private static void HandleAbilityResolved(AbilityActionResolvedEvent gameEvent)
        {
            if (!_pushArmed || gameEvent == null)
                return;

            AbilityActionResolvedPayload payload = gameEvent.Payload;
            if (!string.Equals(payload.AbilityId, ComboAbilityId, StringComparison.Ordinal))
                return;
            if (_pendingActionId != Guid.Empty && payload.ActionId != _pendingActionId)
                return;

            ActionRuntimeController runtime = ActionRuntimeController.Instance;
            UnitBrain target = _pendingTarget;
            Vector2Int direction = _pendingDirection;
            int force = _pendingForce;
            ClearPendingState();

            if (runtime == null || target == null)
                return;

            if (!payload.Succeeded)
            {
                Debug.LogWarning($"{LogTag} 푸시 능력이 취소/실패로 해석돼 넉백을 커밋하지 않는다. ActionId={payload.ActionId:N}.");
                return;
            }

            bool committed = runtime.CommitMutation(new KnockbackMutation(
                SeededRandomProvider.Shared.NextGuid(),
                target.ActorId,
                direction,
                force,
                ApplySplatDamage: true,
                Context: new MutationContext(
                    runtime.Scheduler != null ? runtime.Scheduler.CurrentTick : 0,
                    payload.ActionId,
                    target.ActorId,
                    nameof(RepresentativeComboSetupEditor))));

            if (committed)
            {
                Debug.Log($"{LogTag} 넉백 뮤테이션 커밋 완료. Target={target.name}, Direction={direction}, Force={force}. " +
                          "기대 결과: 대상이 막힐 때까지 밀리고, 막힌 칸 기준 Splat 피해(양측 또는 벽 대상 MaxHP 10%) 적용.");
            }
            else
            {
                Debug.LogWarning($"{LogTag} 넉백 뮤테이션 커밋 실패. 대상 생존 여부와 뮤테이션 프로세서 상태를 확인하라.");
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                ClearPendingState();
        }

        private static void HandleActionInterrupted(ActionInterruptedEvent gameEvent)
        {
            if (_pushArmed && gameEvent != null && gameEvent.Payload.InterruptedAction == _pendingActionId)
                ClearPendingState();
        }

        private static void HandleActionCancelled(ActionCancelledEvent gameEvent)
        {
            if (_pushArmed && gameEvent != null && gameEvent.Payload.ActionId == _pendingActionId)
                ClearPendingState();
        }

        private static void ClearPendingState()
        {
            _pushArmed = false;
            _pendingActionId = Guid.Empty;
            _pendingTarget = null;
            _pendingDirection = Vector2Int.zero;
            _pendingForce = 0;

            if (_hooksAttached)
            {
                // Unsubscribe from the original battle even if its singleton was replaced.
                _subscribedBus?.Unsubscribe<AbilityActionResolvedEvent>(HandleAbilityResolved);
                _subscribedBus?.Unsubscribe<ActionInterruptedEvent>(HandleActionInterrupted);
                _subscribedBus?.Unsubscribe<ActionCancelledEvent>(HandleActionCancelled);
                _subscribedBus = null;
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
                _hooksAttached = false;
            }
        }

        private static bool TryGetRuntime(out ActionRuntimeController runtime)
        {
            runtime = ActionRuntimeController.Instance ?? ActionRuntimeController.EnsureExists();
            if (runtime != null && runtime.Scheduler != null)
                return true;

            Debug.LogError($"{LogTag} ActionRuntimeController/스케줄러가 준비되지 않았다.");
            return false;
        }
    }
}
#endif
