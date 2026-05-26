using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        [SerializeField] private UnitData _kingData;

        [Header("Debug")]
        [SerializeField] private bool _enableAPDebugLogger = true;
        [SerializeField] private bool _enableDamageDebug;
        [SerializeField] private bool _enableMovementDebug;
        [SerializeField] private bool _movementDebugOnlyKnight = true;
        [SerializeField] private bool _includeUnitNameInDebugLogs = true;
        [SerializeField] private bool _spawnOnAwake = true;
        [SerializeField] private bool _enableMouseInputAdapter = true;
        [SerializeField] private bool _logQueuedInputCommands = true;
        [SerializeField] private bool _preferKingDefeatVictory = true;
        [SerializeField] private Camera _inputCamera;
        [SerializeField] private LayerMask _inputRaycastMask = ~0;

        private readonly Dictionary<Team, List<UnitRecord>> _teams = new();
        private readonly List<UnitRecord> _allUnits = new();
        private const string DebugPushAbilityId = "debug_force_push";
        private const string DebugAoeChainAbilityId = "debug_aoe_explosion_chain";
        private const string DebugSkillPlaceholderAbilityId = "debug_skill_placeholder";
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
        private BattleDiagnosticsLogger _diagnosticsLogger;
        private UnitData _runtimeKingData;
        private Canvas _commandCanvas;
        private GameObject _commandPanel;
        private Button _moveModeButton;
        private Button _attackModeButton;
        private Button _skillModeButton;
        private InputMode _currentInputMode = InputMode.Move;

        private enum InputMode
        {
            Move,
            Attack,
            Skill
        }

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
            EnsureBattleDiagnosticsLogger();
            EnsureCommandModeUI();

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

            UnitData kingData = ResolveKingData();
            if (kingData == null)
            {
                Debug.LogError("[TestSceneBattleManager] King UnitData could not be resolved.");
                return;
            }

            SpawnUnit("Blue Pawn", _pawnData, new Vector2Int(1, 0), Team.Blue);
            SpawnUnit("Blue Knight", _knightData, new Vector2Int(3, 0), Team.Blue);
            SpawnUnit("Blue King", kingData, new Vector2Int(5, 0), Team.Blue);
            SpawnUnit("Red Pawn", _pawnData, new Vector2Int(6, 7), Team.Red);
            SpawnUnit("Red Knight", _knightData, new Vector2Int(4, 7), Team.Red);
            SpawnUnit("Red King", kingData, new Vector2Int(2, 7), Team.Red);

            Debug.Log($"[TestSceneBattleManager] Victory condition: {(_preferKingDefeatVictory ? "King Defeat" : "Team Elimination")}.");
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
            if (ShouldLogMovementFor(knight))
            {
                Debug.Log(
                    $"[MovementDebug][F2] Requested knight jump. Actor={knight.ActorId:N}, From={source}, " +
                    $"RequestedDestination={destination}, SpikesBypassed={spikesBypassed}");
            }

            bool queued = knight.QueueMoveAction(destination);
            if (!queued)
            {
                Debug.LogWarning($"[TestSceneBattleManager][F2] Failed to enqueue MoveActionCommand from {source} to {destination}.");
                return;
            }

            LogScheduledMoveCommand(runtime, knight, destination, "F2");

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
            bool blueKingAlive = HasLivingKing(Team.Blue);
            bool redKingAlive = HasLivingKing(Team.Red);

            if (_preferKingDefeatVictory && (!blueKingAlive || !redKingAlive))
            {
                _battleEnded = true;
                string result = blueKingAlive == redKingAlive
                    ? "Draw (Both Kings Down)"
                    : (blueKingAlive ? "Blue Victory (Red King Down)" : "Red Victory (Blue King Down)");
                Debug.Log($"[TestSceneBattleManager] Battle ended: {result}");
                return;
            }

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

        private bool HasLivingKing(Team team)
        {
            if (!_teams.TryGetValue(team, out List<UnitRecord> roster))
                return false;

            foreach (UnitRecord record in roster)
            {
                if (record.Brain == null || record.Brain.IsDead || record.Brain.UnitData == null)
                    continue;

                if (record.Brain.UnitData.PieceType == ChessPieceType.King)
                    return true;
            }

            return false;
        }

        private UnitData ResolveKingData()
        {
            if (_kingData != null)
                return _kingData;

            if (_runtimeKingData != null)
                return _runtimeKingData;

            UnitData source = _knightData != null ? _knightData : _pawnData;
            if (source == null)
                return null;

            _runtimeKingData = ScriptableObject.CreateInstance<UnitData>();
            _runtimeKingData.UnitName = "Runtime King";
            _runtimeKingData.BaseRole = "King";
            _runtimeKingData.PieceType = ChessPieceType.King;
            _runtimeKingData.SyncDefaultChessMetadata();
            _runtimeKingData.MaxHealth = Mathf.Max(1f, source.MaxHealth * 1.35f);
            _runtimeKingData.Defense = Mathf.Clamp01(source.Defense + 0.08f);
            _runtimeKingData.Resistance = Mathf.Clamp01(source.Resistance + 0.08f);
            _runtimeKingData.AttackDamage = Mathf.Max(1f, source.AttackDamage * 1.2f);
            _runtimeKingData.AttackCooldown = Mathf.Max(0.25f, source.AttackCooldown);
            _runtimeKingData.AttackRange = Mathf.Max(1, source.AttackRange);
            _runtimeKingData.KillValue = Mathf.Max(source.KillValue, 20f);
            _runtimeKingData.MaxSP = Mathf.Max(source.MaxSP, 100f);
            _runtimeKingData.MoveCostAP = Mathf.Max(1f, source.MoveCostAP + 2f);
            _runtimeKingData.AttackCostAP = Mathf.Max(1f, source.AttackCostAP + 2f);
            _runtimeKingData.ActionSpeed = Mathf.Max(0.5f, source.ActionSpeed);
            _runtimeKingData.MoveRange = 1;
            _runtimeKingData.MoveSpeed = source.MoveSpeed;
            _runtimeKingData.Weight = Mathf.Max(source.Weight, 3);
            _runtimeKingData.IsBoss = true;
            return _runtimeKingData;
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

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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

            if (_selectedUnit != null && ShouldLogMovementFor(_selectedUnit))
            {
                Debug.Log(
                    $"[MovementDebug][Input] ClickedCell={targetCell}, HitPoint={hit.point}, " +
                    $"SelectedActor={_selectedUnit.ActorId:N}");
            }

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
            bool queued = false;

            switch (_currentInputMode)
            {
                case InputMode.Move:
                    queued = _selectedUnit.QueueMoveAction(cell);
                    break;
                case InputMode.Attack:
                    if (!hasEnemyTarget)
                    {
                        Debug.Log("[TestSceneBattleManager][AttackMode] No enemy target on clicked cell.");
                        return;
                    }

                    if (_selectedUnit.Combat == null || !_selectedUnit.Combat.IsTargetInAttackRange(targetUnit.gameObject))
                    {
                        Debug.Log(
                            $"[TestSceneBattleManager][AttackMode] Target out of range. " +
                            $"Attacker={_selectedUnit.name}, Target={targetUnit.name}, " +
                            $"Piece={_selectedUnit.UnitData?.PieceType}, Range={_selectedUnit.UnitData?.AttackRange}");
                        return;
                    }
                    queued = _selectedUnit.QueueAttackAction(targetUnit.gameObject);
                    break;
                case InputMode.Skill:
                    if (!TryQueueSkillPlaceholder(_selectedUnit, targetUnit, cell))
                    {
                        Debug.Log(
                            $"[TestSceneBattleManager][SkillMode] Queue failed. Actor={_selectedUnit.name}, Cell={cell}");
                        return;
                    }
                    Debug.Log(
                        $"[TestSceneBattleManager][SkillMode] Queued placeholder skill. Actor={_selectedUnit.name}, " +
                        $"Target={(targetUnit != null ? targetUnit.name : "Self")}, Cell={cell}");
                    queued = true;
                    return;
            }

            if (!hasEnemyTarget && ShouldLogMovementFor(_selectedUnit))
            {
                Debug.Log(
                    $"[MovementDebug][InputQueue] RequestedCell={cell}, Actor={_selectedUnit.ActorId:N}, " +
                    $"Queued={queued}");
            }

            if (!queued)
            {
                Debug.Log(
                    $"[TestSceneBattleManager][InputQueue] Action queue failed. Mode={_currentInputMode}, " +
                    $"Actor={_selectedUnit.name}, Cell={cell}");
                return;
            }

            AbilityActionCommand command = BuildInputAbilityCommand(_selectedUnit, cell, targetUnit);
            APDebugLogger.RecordQueuedCommand(command);

            if (!hasEnemyTarget && TryGetRuntime(out ActionRuntimeController runtime))
                LogScheduledMoveCommand(runtime, _selectedUnit, cell, "Input");

            if (_logQueuedInputCommands && command != null)
                Debug.Log($"[TestSceneBattleManager] Enqueued input command: {APDebugLogger.LastQueuedCommandSummary}");
        }

        private void SetSelectedUnit(UnitBrain unit)
        {
            _selectedUnit = unit;
            APDebugLogger.SetCurrentTurnUnit(unit);
            if (_commandPanel != null)
                _commandPanel.SetActive(unit != null && !unit.IsDead);

            if (unit == null)
                _selectionOverlay.ClearSelection();
            else
                _selectionOverlay.SetSelectedUnit(unit);

            SyncOverlayMode();
        }

        private BattleSelectionOverlayController EnsureSelectionOverlay()
        {
            if (!TryGetComponent(out _selectionOverlay))
                _selectionOverlay = gameObject.AddComponent<BattleSelectionOverlayController>();

            _selectionOverlay.SetUseInputSelection(false);

            return _selectionOverlay;
        }

        private void EnsureCommandModeUI()
        {
            if (_commandCanvas != null)
                return;

            EnsureEventSystem();

            GameObject canvasObject = new GameObject("BattleCommandCanvas");
            canvasObject.transform.SetParent(transform, false);
            _commandCanvas = canvasObject.AddComponent<Canvas>();
            _commandCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            _commandPanel = new GameObject("CommandPanel");
            _commandPanel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = _commandPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 24f);
            panelRect.sizeDelta = new Vector2(520f, 72f);

            HorizontalLayoutGroup layout = _commandPanel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(12, 12, 12, 12);

            Image panelBackground = _commandPanel.AddComponent<Image>();
            panelBackground.color = new Color(0f, 0f, 0f, 0.45f);

            _moveModeButton = CreateModeButton(_commandPanel.transform, "Move", new Color(0.2f, 0.55f, 0.95f, 0.92f), () => SetInputMode(InputMode.Move));
            _attackModeButton = CreateModeButton(_commandPanel.transform, "Attack", new Color(0.92f, 0.2f, 0.2f, 0.92f), () => SetInputMode(InputMode.Attack));
            _skillModeButton = CreateModeButton(_commandPanel.transform, "Skill", new Color(0.45f, 0.45f, 0.45f, 0.92f), () => SetInputMode(InputMode.Skill));

            _commandPanel.SetActive(false);
            RefreshModeButtonVisuals();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private Button CreateModeButton(Transform parent, string label, Color color, Action onClick)
        {
            GameObject buttonObject = new GameObject(label + "Button");
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 48f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObject = new GameObject(label + "Text");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 20;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            return button;
        }

        private void SetInputMode(InputMode mode)
        {
            _currentInputMode = mode;
            RefreshModeButtonVisuals();
            SyncOverlayMode();
        }

        private void RefreshModeButtonVisuals()
        {
            SetButtonAlpha(_moveModeButton, _currentInputMode == InputMode.Move ? 1f : 0.55f);
            SetButtonAlpha(_attackModeButton, _currentInputMode == InputMode.Attack ? 1f : 0.55f);
            SetButtonAlpha(_skillModeButton, _currentInputMode == InputMode.Skill ? 1f : 0.55f);
        }

        private static void SetButtonAlpha(Button button, float alpha)
        {
            if (button == null || button.targetGraphic == null)
                return;

            Color c = button.targetGraphic.color;
            c.a = Mathf.Clamp01(alpha);
            button.targetGraphic.color = c;
        }

        private void SyncOverlayMode()
        {
            if (_selectionOverlay == null)
                return;

            BattleSelectionOverlayController.OverlayMode overlayMode = _currentInputMode switch
            {
                InputMode.Attack => BattleSelectionOverlayController.OverlayMode.Attack,
                InputMode.Skill => BattleSelectionOverlayController.OverlayMode.Skill,
                _ => BattleSelectionOverlayController.OverlayMode.Move
            };
            _selectionOverlay.SetOverlayMode(overlayMode);
        }

        private BattleDiagnosticsLogger EnsureBattleDiagnosticsLogger()
        {
            if (!TryGetComponent(out _diagnosticsLogger))
                _diagnosticsLogger = gameObject.AddComponent<BattleDiagnosticsLogger>();

            _diagnosticsLogger.Configure(
                _enableDamageDebug,
                _enableMovementDebug,
                _movementDebugOnlyKnight,
                _includeUnitNameInDebugLogs);
            return _diagnosticsLogger;
        }

        private bool ShouldLogMovementFor(UnitBrain unit)
        {
            if (!_enableMovementDebug || unit == null)
                return false;
            if (!_movementDebugOnlyKnight)
                return true;
            return unit.UnitData != null && unit.UnitData.PieceType == ChessPieceType.Knight;
        }

        private void LogScheduledMoveCommand(ActionRuntimeController runtime, UnitBrain actor, Vector2Int requestedCell, string sourceTag)
        {
            if (runtime == null || runtime.Scheduler == null || actor == null || !ShouldLogMovementFor(actor))
                return;

            MoveActionCommand latestMove = null;
            foreach (IActionCommand action in runtime.Scheduler.GetActiveActions())
            {
                if (action is MoveActionCommand move && move.ActorId == actor.ActorId)
                {
                    if (latestMove == null || move.QueuedTick >= latestMove.QueuedTick)
                        latestMove = move;
                }
            }

            if (latestMove == null)
                return;

            Debug.Log(
                $"[MovementDebug][{sourceTag}Scheduled] Actor={actor.ActorId:N}, RequestedCell={requestedCell}, " +
                $"ScheduledFrom={latestMove.From}, ScheduledTo={latestMove.To}, ActionId={latestMove.ActionId:N}, " +
                $"QueuedTick={latestMove.QueuedTick}, ResolveTick={latestMove.ResolveTick}");
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

        private bool TryQueueSkillPlaceholder(UnitBrain actor, UnitBrain targetUnit, Vector2Int targetCell)
        {
            if (actor == null)
                return false;
            if (!TryGetRuntime(out ActionRuntimeController runtime))
                return false;

            AbilityDefinition definition = BuildDebugAbilityDefinition(
                DebugSkillPlaceholderAbilityId,
                AbilityTargetingRule.SingleTarget,
                effects: null,
                castSpeed: ActionSpeedTier.Normal);

            IReadOnlyList<Guid> targetIds = targetUnit != null
                ? new[] { targetUnit.ActorId }
                : new[] { actor.ActorId };

            bool queued = runtime.TryEnqueueAbility(actor, definition, targetIds);
            if (!queued)
                return false;

            if (_logQueuedInputCommands)
            {
                Debug.Log(
                    $"[TestSceneBattleManager] Enqueued skill placeholder action. " +
                    $"Actor={actor.ActorId:N}, TargetCell={targetCell}, TargetCount={targetIds.Count}");
            }

            return true;
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
