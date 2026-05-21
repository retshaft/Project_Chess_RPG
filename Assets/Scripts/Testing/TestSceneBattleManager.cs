using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.Testing
{
    public class TestSceneBattleManager : MonoBehaviour
    {
        [Header("Unit Data")]
        [SerializeField] private UnitData _pawnData;
        [SerializeField] private UnitData _knightData;

        [Header("Debug")]
        [SerializeField] private bool _enableAPDebugLogger = true;
        [SerializeField] private bool _spawnOnAwake = true;
        [SerializeField] private bool _enableMouseInputAdapter = true;
        [SerializeField] private bool _logQueuedInputCommands = true;
        [SerializeField] private Camera _inputCamera;
        [SerializeField] private LayerMask _inputRaycastMask = ~0;

        private readonly Dictionary<Team, List<UnitRecord>> _teams = new();
        private readonly List<UnitRecord> _allUnits = new();
        private const string DebugPushAbilityId = "debug_force_push";
        private const string DebugAoeChainAbilityId = "debug_aoe_explosion_chain";
        private static readonly Vector2Int[] KnightOffsets =
        {
            new(1, 2),
            new(2, 1),
            new(-1, 2),
            new(-2, 1),
            new(1, -2),
            new(2, -1),
            new(-1, -2),
            new(-2, -1)
        };

        private bool _debugEventHookAttached;
        private Guid _pendingPushActionId;
        private UnitBrain _pendingPushTarget;
        private Vector2Int _pendingPushDirection;
        private int _pendingPushForce;
        private bool _battleEnded;
        private UnitBrain _selectedUnit;
        private BattleSelectionOverlayController _selectionOverlay;

        private enum Team
        {
            Blue,
            Red
        }

        private sealed class UnitRecord
        {
            public UnitBrain Brain;
            public Team Team;
        }

        private void Awake()
        {
            EnsureSelectionOverlay();

            if (!_spawnOnAwake)
                return;

            BootstrapBattle();
        }

        private void Update()
        {
            HandleDebugScenarioHotkeys();

            if (_battleEnded)
                return;

            HandleMouseInputAdapter();
            UpdateTargets();
            EvaluateBattleState();
        }

        private void OnDestroy()
        {
            DetachDebugRuntimeHooks();
        }

        private void BootstrapBattle()
        {
            EnsureGridSystem();
            EnsureAPManager(_enableAPDebugLogger);
            SpawnUnits();
        }

        private void SpawnUnits()
        {
            if (_pawnData == null || _knightData == null)
            {
                Debug.LogError("[TestSceneBattleManager] Pawn/Knight UnitData assets are not assigned.");
                return;
            }

            SpawnUnit("Blue Pawn", _pawnData, new Vector2Int(1, 0), Team.Blue);
            SpawnUnit("Blue Knight", _knightData, new Vector2Int(3, 0), Team.Blue);
            SpawnUnit("Red Pawn", _pawnData, new Vector2Int(6, 7), Team.Red);
            SpawnUnit("Red Knight", _knightData, new Vector2Int(4, 7), Team.Red);
        }

        private void HandleDebugScenarioHotkeys()
        {
            if (!Input.GetKeyDown(KeyCode.F1) &&
                !Input.GetKeyDown(KeyCode.F2) &&
                !Input.GetKeyDown(KeyCode.F3))
            {
                return;
            }

            EnsureDebugRuntimeHooks();

            if (Input.GetKeyDown(KeyCode.F1))
                ExecuteMultiCollisionScenario();

            if (Input.GetKeyDown(KeyCode.F2))
                ExecuteKnightJumpScenario();

            if (Input.GetKeyDown(KeyCode.F3))
                ExecuteReactionThrottleScenario();
        }

        private void EnsureDebugRuntimeHooks()
        {
            if (_debugEventHookAttached)
                return;

            ActionRuntimeController runtime = ActionRuntimeController.Instance ?? ActionRuntimeController.EnsureExists();
            runtime.EventBus.Subscribe<AbilityActionResolvedEvent>(HandleDebugAbilityResolved);
            _debugEventHookAttached = true;
        }

        private void DetachDebugRuntimeHooks()
        {
            if (!_debugEventHookAttached)
                return;

            ActionRuntimeController runtime = ActionRuntimeController.Instance;
            runtime?.EventBus.Unsubscribe<AbilityActionResolvedEvent>(HandleDebugAbilityResolved);
            _debugEventHookAttached = false;
        }

        private void ExecuteMultiCollisionScenario()
        {
            if (!TryGetRuntime(out ActionRuntimeController runtime))
                return;

            UnitBrain actor = FindFirstLivingFriendlyUnit();
            UnitBrain target = FindFirstLivingEnemyUnit();
            if (actor == null || target == null || actor.Movement == null || target.Movement == null || target.StatusEffects == null)
            {
                Debug.LogWarning("[TestSceneBattleManager][F1] Required units/components are missing.");
                return;
            }

            const int pushRow = 4;
            Vector2Int actorCell = new(1, pushRow);
            Vector2Int targetCell = new(0, pushRow);
            if (!TryForceSetUnitCell(actor, actorCell) || !TryForceSetUnitCell(target, targetCell))
            {
                Debug.LogWarning("[TestSceneBattleManager][F1] Failed to place actor/target for push scenario.");
                return;
            }

            target.StatusEffects.ApplyStatusEffect(StatusEffectType.Stagger, duration: 8f, stacks: 1, isSecondary: false, sourceActorId: actor.ActorId);
            runtime.SyncRuntimeState(target);

            int weightBasedForce = Mathf.Max(1, target.Movement.Weight);
            var ability = BuildDebugAbilityDefinition(
                DebugPushAbilityId,
                AbilityTargetingRule.SingleTarget,
                effects: null,
                castSpeed: ActionSpeedTier.Fast);

            if (!runtime.TryEnqueueAbility(actor, ability, new[] { target.ActorId }))
            {
                Debug.LogWarning("[TestSceneBattleManager][F1] Failed to enqueue push ability.");
                return;
            }

            List<IActionCommand> activeActions = new List<IActionCommand>(runtime.Scheduler.GetActiveActions());
            
            Guid queuedActionId = Guid.Empty;
            for (int i = activeActions.Count - 1; i >= 0; i--)
            {
                if (activeActions[i] is AbilityActionCommand abilityAction &&
                    abilityAction.ActorId == actor.ActorId &&
                    string.Equals(abilityAction.AbilityId, DebugPushAbilityId, StringComparison.Ordinal))
                {
                    queuedActionId = abilityAction.ActionId;
                    break;
                }
            }

            _pendingPushActionId = queuedActionId;
            _pendingPushTarget = target;
            _pendingPushDirection = Vector2Int.left;
            _pendingPushForce = weightBasedForce;

            Debug.Log($"[TestSceneBattleManager][F1] Enqueued push action. Actor={actor.name}, Target={target.name}, Force={_pendingPushForce}, TargetWeight={target.Movement.Weight}.");
        }

        private void ExecuteKnightJumpScenario()
        {
            if (!TryGetRuntime(out ActionRuntimeController runtime))
                return;

            UnitBrain knight = FindFirstLivingFriendlyKnight();
            if (knight == null || knight.Movement == null)
            {
                Debug.LogWarning("[TestSceneBattleManager][F2] Friendly knight not found.");
                return;
            }

            if (!TryBuildKnightSanctuaryJump(out Vector2Int source, out Vector2Int destination, out bool spikesBypassed))
            {
                Debug.LogWarning("[TestSceneBattleManager][F2] Could not build a valid knight jump scenario.");
                return;
            }

            if (!TryForceSetUnitCell(knight, source))
            {
                Debug.LogWarning("[TestSceneBattleManager][F2] Failed to place knight on jump source cell.");
                return;
            }

            runtime.SyncRuntimeState(knight);
            bool queued = knight.QueueMoveAction(destination);
            if (!queued)
            {
                Debug.LogWarning($"[TestSceneBattleManager][F2] Failed to enqueue MoveActionCommand from {source} to {destination}.");
                return;
            }

            Debug.Log(
                $"[TestSceneBattleManager][F2] Enqueued knight jump move from {source} to sanctuary {destination}. " +
                $"SpikesBypassedContext={spikesBypassed}.");
        }

        private void ExecuteReactionThrottleScenario()
        {
            if (!TryGetRuntime(out ActionRuntimeController runtime))
                return;

            List<UnitBrain> units = GatherLivingUnits(limit: 4);
            if (units.Count < 4)
            {
                Debug.LogWarning("[TestSceneBattleManager][F3] Need at least 4 living units.");
                return;
            }

            Vector2Int center = new(GridSystem.GridWidth / 2, GridSystem.GridHeight / 2);
            if (GridSystem.Instance == null || !GridSystem.Instance.IsValidCell(center))
            {
                Debug.LogWarning("[TestSceneBattleManager][F3] Center cell is invalid.");
                return;
            }

            if (!TryForceSetUnitCell(units[0], center))
            {
                Debug.LogWarning("[TestSceneBattleManager][F3] Failed to place primary unit at center.");
                return;
            }

            for (int i = 1; i < units.Count; i++)
            {
                Vector3 offset = new((i - 1) * 0.07f, 0f, (i % 2 == 0 ? -1f : 1f) * 0.07f);
                units[i].transform.position = GridSystem.Instance.GridToWorld(center) + offset;
            }

            var effectList = new List<AbilityEffectDefinition>
            {
                new()
                {
                    EffectId = "Debug:Damage:Damage",
                    DurationTicks = 1,
                    TickInterval = 1,
                    InitialTickIn = 1,
                    StackCount = 1,
                    StackPolicy = EffectStackPolicy.Replace,
                    Magnitude = 14f
                },
                new()
                {
                    EffectId = StatusEffectType.Poison.ToString(),
                    DurationTicks = 3,
                    TickInterval = 1,
                    InitialTickIn = 1,
                    StackCount = 1,
                    StackPolicy = EffectStackPolicy.Refresh,
                    Magnitude = 1f
                }
            };

            var ability = BuildDebugAbilityDefinition(
                DebugAoeChainAbilityId,
                AbilityTargetingRule.MultiTarget,
                effectList,
                castSpeed: ActionSpeedTier.Fast);
            UnitBrain caster = units[0];
            var targetIds = new List<Guid>(units.Count);
            for (int i = 0; i < units.Count; i++)
                targetIds.Add(units[i].ActorId);

            bool queued = runtime.TryEnqueueAbility(caster, ability, targetIds);
            if (!queued)
            {
                Debug.LogWarning("[TestSceneBattleManager][F3] Failed to enqueue AoE explosion/spread ability.");
                return;
            }

            // Force an over-depth probe so ReactionDepthGuard emits the chain-throttle warning log format.
            _ = new ReactionDepthGuard(5).IsDepthAllowed(6, $"{DebugAoeChainAbilityId}_LoopProbe");

            Debug.Log(
                $"[TestSceneBattleManager][F3] Enqueued AoE cast at {center} with 4 overlapped debug targets. " +
                "Triggered ReactionDepthGuard over-depth probe log.");
        }

        private void HandleDebugAbilityResolved(AbilityActionResolvedEvent gameEvent)
        {
            if (gameEvent == null)
                return;

            AbilityActionResolvedPayload payload = gameEvent.Payload;
            if (_pendingPushTarget != null &&
                _pendingPushTarget.Movement != null &&
                !string.IsNullOrWhiteSpace(payload.AbilityId) &&
                string.Equals(payload.AbilityId, DebugPushAbilityId, StringComparison.Ordinal) &&
                (_pendingPushActionId == Guid.Empty || payload.ActionId == _pendingPushActionId))
            {
                _pendingPushTarget.Movement.ApplyKnockback(_pendingPushDirection, _pendingPushForce, applySplatDamage: true);
                Debug.Log(
                    $"[TestSceneBattleManager][F1] Applied queued knockback. Target={_pendingPushTarget.name}, " +
                    $"Direction={_pendingPushDirection}, Force={_pendingPushForce}.");

                _pendingPushTarget = null;
                _pendingPushActionId = Guid.Empty;
                _pendingPushDirection = Vector2Int.zero;
                _pendingPushForce = 0;
            }
        }

        private static AbilityDefinition BuildDebugAbilityDefinition(
            string abilityId,
            AbilityTargetingRule targetingRule,
            List<AbilityEffectDefinition> effects,
            ActionSpeedTier castSpeed)
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            definition.name = abilityId;
            definition.Cost = 0f;
            definition.Cooldown = 0;
            definition.CastSpeed = castSpeed;
            definition.TargetingRule = targetingRule;
            definition.EffectList = effects ?? new List<AbilityEffectDefinition>();
            return definition;
        }

        private static bool TryGetRuntime(out ActionRuntimeController runtime)
        {
            runtime = ActionRuntimeController.Instance ?? ActionRuntimeController.EnsureExists();
            if (runtime != null && runtime.Scheduler != null)
                return true;

            Debug.LogWarning("[TestSceneBattleManager] Runtime controller/scheduler is not ready.");
            return false;
        }

        private UnitBrain FindFirstLivingEnemyUnit()
        {
            if (!_teams.TryGetValue(Team.Red, out List<UnitRecord> redUnits))
                return null;

            foreach (UnitRecord record in redUnits)
            {
                if (record.Brain != null && !record.Brain.IsDead)
                    return record.Brain;
            }

            return null;
        }

        private UnitBrain FindFirstLivingFriendlyKnight()
        {
            if (!_teams.TryGetValue(Team.Blue, out List<UnitRecord> blueUnits))
                return null;

            foreach (UnitRecord record in blueUnits)
            {
                if (record.Brain == null || record.Brain.IsDead || record.Brain.UnitData == null)
                    continue;
                if (record.Brain.UnitData.PieceType == ChessPieceType.Knight)
                    return record.Brain;
            }

            return null;
        }

        private List<UnitBrain> GatherLivingUnits(int limit)
        {
            var units = new List<UnitBrain>(Mathf.Max(1, limit));
            foreach (UnitRecord record in _allUnits)
            {
                if (record.Brain == null || record.Brain.IsDead)
                    continue;

                units.Add(record.Brain);
                if (units.Count >= limit)
                    break;
            }

            return units;
        }

        private bool TryBuildKnightSanctuaryJump(out Vector2Int source, out Vector2Int destination, out bool spikesBypassed)
        {
            source = default;
            destination = default;
            spikesBypassed = false;

            GridSystem grid = GridSystem.Instance;
            if (grid == null)
                return false;

            var sanctuaries = new List<Vector2Int>();
            var spikes = new List<Vector2Int>();
            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    Vector2Int cell = new(x, y);
                    TileType type = grid.GetTileType(cell);
                    if (type == TileType.Sanctuary)
                        sanctuaries.Add(cell);
                    else if (type == TileType.Spikes)
                        spikes.Add(cell);
                }
            }

            for (int i = 0; i < sanctuaries.Count; i++)
            {
                Vector2Int sanctuary = sanctuaries[i];
                if (!grid.IsCellFree(sanctuary))
                    continue;

                for (int j = 0; j < KnightOffsets.Length; j++)
                {
                    Vector2Int candidateSource = sanctuary - KnightOffsets[j];
                    if (!grid.IsValidCell(candidateSource))
                        continue;

                    bool hasSpikeInContext = false;
                    int minX = Mathf.Min(candidateSource.x, sanctuary.x);
                    int maxX = Mathf.Max(candidateSource.x, sanctuary.x);
                    int minY = Mathf.Min(candidateSource.y, sanctuary.y);
                    int maxY = Mathf.Max(candidateSource.y, sanctuary.y);
                    for (int s = 0; s < spikes.Count; s++)
                    {
                        Vector2Int spike = spikes[s];
                        if (spike.x >= minX && spike.x <= maxX && spike.y >= minY && spike.y <= maxY)
                        {
                            hasSpikeInContext = true;
                            break;
                        }
                    }

                    source = candidateSource;
                    destination = sanctuary;
                    spikesBypassed = hasSpikeInContext;
                    return true;
                }
            }

            return false;
        }

        private static bool TryForceSetUnitCell(UnitBrain unit, Vector2Int cell)
        {
            if (unit == null || unit.Movement == null || GridSystem.Instance == null || !GridSystem.Instance.IsValidCell(cell))
                return false;

            return unit.Movement.ApplyResolvedMovement(cell);
        }

        private void UpdateTargets()
        {
            foreach (UnitRecord record in _allUnits)
            {
                if (record.Brain == null || record.Brain.IsDead)
                    continue;

                UnitBrain target = FindNearestEnemy(record);
                if (target != null)
                    record.Brain.SetTarget(target.gameObject);
                else
                    record.Brain.ClearTarget();
            }
        }

        private void EvaluateBattleState()
        {
            bool blueAlive = HasLivingUnits(Team.Blue);
            bool redAlive = HasLivingUnits(Team.Red);

            if (!blueAlive || !redAlive)
            {
                _battleEnded = true;
                string result = blueAlive == redAlive
                    ? "Draw"
                    : (blueAlive ? "Blue Victory" : "Red Victory");
                Debug.Log($"[TestSceneBattleManager] Battle ended: {result}");
            }
        }

        private bool HasLivingUnits(Team team)
        {
            if (!_teams.TryGetValue(team, out List<UnitRecord> roster))
                return false;

            foreach (UnitRecord record in roster)
            {
                if (record.Brain != null && !record.Brain.IsDead)
                    return true;
            }

            return false;
        }

        private UnitBrain FindNearestEnemy(UnitRecord source)
        {
            if (source.Brain == null || source.Brain.Movement == null)
                return null;

            Team enemyTeam = source.Team == Team.Blue ? Team.Red : Team.Blue;
            if (!_teams.TryGetValue(enemyTeam, out List<UnitRecord> roster))
                return null;

            Vector2Int origin = source.Brain.Movement.GridPosition;
            int bestDistance = int.MaxValue;
            UnitBrain bestTarget = null;

            foreach (UnitRecord candidate in roster)
            {
                if (candidate.Brain == null || candidate.Brain.IsDead || candidate.Brain.Movement == null)
                    continue;

                int distance = ManhattanDistance(origin, candidate.Brain.Movement.GridPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidate.Brain;
                }
            }

            return bestTarget;
        }

        private void RegisterUnit(UnitBrain brain, Team team)
        {
            if (brain == null)
                return;

            if (!_teams.TryGetValue(team, out List<UnitRecord> roster))
            {
                roster = new List<UnitRecord>();
                _teams[team] = roster;
            }

            var record = new UnitRecord { Brain = brain, Team = team };
            roster.Add(record);
            _allUnits.Add(record);

            if (_selectedUnit == null && team == Team.Blue && !brain.IsDead)
                SetSelectedUnit(brain);
        }

        private void HandleUnitDeath(UnitBrain brain)
        {
            if (brain == null)
                return;

            if (GridSystem.Instance != null)
                GridSystem.Instance.ClearOccupant(brain.gameObject);

            brain.gameObject.SetActive(false);
        }

        private static void EnsureGridSystem()
        {
            if (GridSystem.Instance != null)
                return;

            var gridGO = new GameObject("GridSystem");
            gridGO.AddComponent<GridSystem>();
        }

        private static APManager EnsureAPManager(bool enableDebugLogging)
        {
            if (APManager.Instance != null)
                return APManager.Instance;

            var apGO = new GameObject("APManager");
            var manager = apGO.AddComponent<APManager>();

            if (enableDebugLogging)
                apGO.AddComponent<APDebugLogger>();

            return manager;
        }

        private void SpawnUnit(string unitName, UnitData data, Vector2Int cell, Team team)
        {
            var go = new GameObject(unitName);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.65f, 0.5f, 0.65f);

            Destroy(visual.GetComponent<Collider>());

            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(rend.sharedMaterial);
                var color = team == Team.Red
                    ? new Color(0.90f, 0.20f, 0.20f)
                    : new Color(0.20f, 0.45f, 0.90f);
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                rend.material = mat;
            }

            var health = go.AddComponent<HealthComponent>();
            go.AddComponent<MovementComponent>();
            go.AddComponent<CombatComponent>();
            go.AddComponent<StatusEffectComponent>();
            var teamComponent = go.AddComponent<TeamComponent>();
            teamComponent.SetIsEnemy(team == Team.Red);
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.8f, 0f);
            collider.height = 1.6f;
            collider.radius = 0.4f;

            var brain = go.AddComponent<UnitBrain>();
            brain.Prepare(data, cell);

            health.OnDeath += () => HandleUnitDeath(brain);

            RegisterUnit(brain, team);
        }

        private void HandleMouseInputAdapter()
        {
            if (!_enableMouseInputAdapter || !Input.GetMouseButtonDown(0))
                return;

            if (GridSystem.Instance == null)
                return;

            Camera cameraToUse = _inputCamera != null ? _inputCamera : Camera.main;
            if (cameraToUse == null)
                return;

            Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, _inputRaycastMask))
                return;

            Vector2Int targetCell = GridSystem.Instance.WorldToGrid(hit.point);
            if (!GridSystem.Instance.IsValidCell(targetCell))
                return;

            if (!TryHandleSelectionAtCell(targetCell))
                TryHandleActionAtCell(targetCell);
        }

        private bool TryHandleSelectionAtCell(Vector2Int cell)
        {
            UnitBrain candidate = GetUnitAtCell(cell);
            if (candidate == null || candidate.IsDead || !IsPlayerTeam(candidate))
                return false;

            SetSelectedUnit(candidate);
            return true;
        }

        private void TryHandleActionAtCell(Vector2Int cell)
        {
            if (_selectedUnit == null || _selectedUnit.IsDead || _selectedUnit.Movement == null)
            {
                SetSelectedUnit(FindFirstLivingFriendlyUnit());
                if (_selectedUnit == null)
                    return;
            }

            UnitBrain targetUnit = GetUnitAtCell(cell);
            bool hasEnemyTarget = targetUnit != null && !targetUnit.IsDead && !IsSameTeam(_selectedUnit, targetUnit);

            bool queued = hasEnemyTarget
                ? _selectedUnit.QueueAttackAction(targetUnit.gameObject)
                : _selectedUnit.QueueMoveAction(cell);

            if (!queued)
                return;

            AbilityActionCommand command = BuildInputAbilityCommand(_selectedUnit, cell, targetUnit);
            APDebugLogger.RecordQueuedCommand(command);

            if (_logQueuedInputCommands && command != null)
                Debug.Log($"[TestSceneBattleManager] Enqueued input command: {APDebugLogger.LastQueuedCommandSummary}");
        }

        private void SetSelectedUnit(UnitBrain unit)
        {
            _selectedUnit = unit;
            APDebugLogger.SetCurrentTurnUnit(unit);

            if (unit == null)
                _selectionOverlay.ClearSelection();
            else
                _selectionOverlay.SetSelectedUnit(unit);
        }

        private BattleSelectionOverlayController EnsureSelectionOverlay()
        {
            if (!TryGetComponent(out _selectionOverlay))
                _selectionOverlay = gameObject.AddComponent<BattleSelectionOverlayController>();

            _selectionOverlay.SetUseInputSelection(false);

            return _selectionOverlay;
        }

        private UnitBrain FindFirstLivingFriendlyUnit()
        {
            if (!_teams.TryGetValue(Team.Blue, out List<UnitRecord> blueUnits))
                return null;

            foreach (UnitRecord record in blueUnits)
            {
                if (record.Brain != null && !record.Brain.IsDead)
                    return record.Brain;
            }

            return null;
        }

        private static UnitBrain GetUnitAtCell(Vector2Int cell)
        {
            if (GridSystem.Instance == null || !GridSystem.Instance.IsValidCell(cell))
                return null;

            GameObject occupant = GridSystem.Instance.GetOccupant(cell);
            if (occupant == null || !occupant.TryGetComponent(out UnitBrain brain))
                return null;

            return brain;
        }

        private static bool IsPlayerTeam(UnitBrain unit)
        {
            return unit != null &&
                   unit.TryGetComponent(out TeamComponent team) &&
                   team.IsPlayer;
        }

        private static bool IsSameTeam(UnitBrain a, UnitBrain b)
        {
            if (a == null || b == null)
                return false;

            if (!a.TryGetComponent(out TeamComponent aTeam) ||
                !b.TryGetComponent(out TeamComponent bTeam))
            {
                return false;
            }

            return aTeam.IsEnemy == bTeam.IsEnemy;
        }

        private static AbilityActionCommand BuildInputAbilityCommand(UnitBrain actor, Vector2Int targetCell, UnitBrain targetUnit)
        {
            if (actor == null)
                return null;

            ActionRuntimeController runtimeController = ActionRuntimeController.Instance ?? ActionRuntimeController.EnsureExists();
            int startTick = runtimeController?.Scheduler != null
                ? runtimeController.Scheduler.CurrentTick + 1
                : 1;

            bool isAttack = targetUnit != null && !targetUnit.IsDead;
            IReadOnlyList<Guid> targetIds = isAttack
                ? new[] { targetUnit.ActorId }
                : Array.Empty<Guid>();
            IReadOnlyList<Vector2Int> targetCells = new[] { targetCell };

            int apCost = 0;
            if (actor.UnitData != null)
            {
                float sourceCost = isAttack ? actor.UnitData.AttackCostAP : actor.UnitData.MoveCostAP;
                apCost = Mathf.Max(0, Mathf.RoundToInt(sourceCost));
            }

            return new AbilityActionCommand(
                actor.ActorId,
                isAttack ? "basic_attack" : "move",
                targetIds,
                startTick,
                ActionSpeedTier.Normal,
                targetCells: targetCells,
                apCost: apCost);
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
