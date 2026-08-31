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

namespace CheckmateRPG.Core
{
    public class BattleManager : MonoBehaviour
    {
        [Header("Unit Data")]
        [SerializeField] private UnitData _pawnData;
        [SerializeField] private UnitData _knightData;
        [SerializeField] private UnitData _bishopData;
        [SerializeField] private UnitData _rookData;
        [SerializeField] private UnitData _queenData;
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

        [Header("Deployment & Deck")]
        [SerializeField] private PlayerDeckData _playerDeck;
        [SerializeField] private UnitData _conscriptData;

        public enum BattlePhase
        {
            Deployment,
            Playing,
            Ended
        }
        private BattlePhase _currentPhase = BattlePhase.Deployment;

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
        private Image _apFillImage;
        private Text _apTextLabel;
        private GameObject _gameOverPanel;
        private UnityEngine.UI.Text _gameOverText;
        private InputMode _currentInputMode = InputMode.Move;
        
        // Deployment UI
        private GameObject _deploymentPanel;
        private UnityEngine.UI.Text _deploymentCostText;
        private UnitData _selectedDeployUnit;
        private Button _selectedDeployButtonRef;
        private int _currentDeploymentCost;

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
            EnsureDeploymentUI();

            if (!_spawnOnAwake)
                return;

            BootstrapBattle();
        }

        private void Update()
        {
            HandleDebugScenarioHotkeys();

            if (_apFillImage != null && APManager.Instance != null && _currentPhase == BattlePhase.Playing)
            {
                float fillAmount = Mathf.Clamp01(APManager.Instance.CurrentAP / Mathf.Max(1f, APManager.Instance.MaxAP));
                _apFillImage.rectTransform.anchorMax = new Vector2(fillAmount, 1f);
                if (_apTextLabel != null)
                {
                    _apTextLabel.text = $"AP {Mathf.FloorToInt(APManager.Instance.CurrentAP)} / {Mathf.FloorToInt(APManager.Instance.MaxAP)}";
                }
            }

            if (_battleEnded || _currentPhase == BattlePhase.Ended)
                return;

            if (_currentPhase == BattlePhase.Deployment)
            {
                return;
            }

            HandleMouseInputAdapter();
            UpdateTargets();
            EvaluateBattleState();
        }

        private void OnDestroy()
        {
            DetachDebugRuntimeHooks();
        }

        private AITeamCommander _redTeamCommander;

        private void BootstrapBattle()
        {
            EnsureGridSystem();
            EnsureAPManager(_enableAPDebugLogger);
            
            _redTeamCommander = gameObject.AddComponent<AITeamCommander>();
            
            if (APManager.Instance != null) 
                APManager.Instance.enabled = false; // Pause AP during deployment
            
            SpawnRedTeam();
            
            if (_selectionOverlay != null) _selectionOverlay.enabled = false; // Disable overlay during deployment
            
            if (CheckmateRPG.Progression.DeckManager.Instance != null && CheckmateRPG.Progression.DeckManager.Instance.CurrentDeck.Length > 0)
            {
                _currentPhase = BattlePhase.Deployment;
                // No cost limit in M12 deployment
                _currentDeploymentCost = 9999;
                if (_deploymentPanel != null) _deploymentPanel.SetActive(true);
                if (_commandPanel != null) _commandPanel.SetActive(false);
                UpdateDeploymentCostText();
                
                TickScheduler.EnsureExists().IsPaused = true; // Pause AI and Actions during deployment
            }
            else
            {
                Debug.LogWarning("[BattleManager] No DeckManager or Deck assigned. Skipping deployment.");
                StartBattle();
            }
        }

        private void SpawnRedTeam()
        {
            if (CheckmateRPG.Progression.StageManager.Instance != null && CheckmateRPG.Progression.StageManager.Instance.CurrentStage != null)
            {
                var stageData = CheckmateRPG.Progression.StageManager.Instance.CurrentStage;
                if (_redTeamCommander != null)
                    _redTeamCommander.ConfigureFromStage(stageData);

                foreach (var spawnInfo in stageData.EnemySpawns)
                {
                    if (spawnInfo.EnemyUnit != null)
                    {
                        var unitToSpawn = spawnInfo.EnemyUnit;
                        if (spawnInfo.OverrideAIBehavior || spawnInfo.EnemyResonanceStage > 0)
                        {
                            unitToSpawn = Instantiate(spawnInfo.EnemyUnit);
                            if (spawnInfo.OverrideAIBehavior)
                            {
                                var customProfile = unitToSpawn.AIProfile != null ? Instantiate(unitToSpawn.AIProfile) : ScriptableObject.CreateInstance<EnemyAIProfile>();
                                customProfile.BehaviorType = spawnInfo.AIBehavior;
                                unitToSpawn.AIProfile = customProfile;
                            }
                            unitToSpawn.ResonanceRole = spawnInfo.EnemyResonanceRole;
                        }

                        RegisterAI(SpawnUnit($"Enemy_{spawnInfo.EnemyUnit.UnitName}", unitToSpawn, spawnInfo.GridPosition, Team.Red));
                    }
                }
            }
            else
            {
                Debug.LogWarning("[BattleManager] StageManager not found. Enemy will not spawn.");
            }
        }

        private UnitBrain RegisterAI(UnitBrain brain)
        {
            if (_redTeamCommander != null && brain != null)
                _redTeamCommander.RegisterUnit(brain);
            return brain;
        }

        private void StartBattle()
        {
            if (_deploymentPanel != null)
                _deploymentPanel.SetActive(false);

            if (_selectionOverlay != null) 
                _selectionOverlay.enabled = true; // Re-enable overlay for playing phase

            _currentPhase = BattlePhase.Playing;
            Debug.Log("[BattleManager] Battle Started!");
            
            TickScheduler.EnsureExists().IsPaused = false; // Resume AI and Actions

            if (_commandPanel != null) _commandPanel.SetActive(true);
            if (APManager.Instance != null) 
            {
                APManager.Instance.ResetToInitialAP();
                APManager.Instance.enabled = true;
            }

            // Phase 7: 킹 싱크로 해방 HUD 버튼 및 시스템 초기화
            var _synchroUI = CheckmateRPG.UI.KingSynchroUI.Instance;

            // Spawn Conscripts in Row 1 for any empty cells
            UnitData conscriptToSpawn = _conscriptData != null ? _conscriptData : _pawnData;
            if (conscriptToSpawn != null)
            {
                for (int x = 0; x < GridSystem.GridWidth; x++)
                {
                    Vector2Int cell = new Vector2Int(x, 1);
                    if (GetUnitAtCell(cell) == null)
                    {
                        SpawnUnit($"Blue Conscript {x}", conscriptToSpawn, cell, Team.Blue);
                    }
                }
            }

            Debug.Log($"[BattleManager] Victory condition: {(_preferKingDefeatVictory ? "King Defeat" : "Team Elimination")}.");
        }

        public bool CanAffordDeployment(UnitData unit) => _currentDeploymentCost >= unit.DeploymentCost;

        public RectTransform DeploymentPanelRect => _deploymentPanel?.GetComponent<RectTransform>();

        private readonly List<GameObject> _deploymentHighlights = new();
        private GameObject _3dGhostUnit;

        public void OnDeployDragStart(UnitData unit)
        {
            ClearDeploymentHighlights();
            int targetRow = unit.PieceType == ChessPieceType.Pawn ? 1 : 0;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                Vector2Int cell = new Vector2Int(x, targetRow);
                if (GetUnitAtCell(cell) == null)
                {
                    _deploymentHighlights.Add(CreateTileHighlight(cell, Color.green));
                }
            }

            // Create 3D Ghost Unit
            _3dGhostUnit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _3dGhostUnit.name = "GhostDeploymentUnit";
            Destroy(_3dGhostUnit.GetComponent<Collider>());
            _3dGhostUnit.transform.localScale = new Vector3(0.65f, 0.5f, 0.65f);
            
            var rend = _3dGhostUnit.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Transparent/Diffuse") ?? Shader.Find("Standard");
                Material ghostMat = new Material(shader);
                Color ghostColor = new Color(0.20f, 0.45f, 0.90f, 0.5f); // Semi-transparent blue
                ghostMat.color = ghostColor;
                
                // If using standard shader, force transparency
                if (ghostMat.HasProperty("_Mode"))
                {
                    ghostMat.SetFloat("_Mode", 3); // Transparent mode
                    ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    ghostMat.SetInt("_ZWrite", 0);
                    ghostMat.DisableKeyword("_ALPHATEST_ON");
                    ghostMat.EnableKeyword("_ALPHABLEND_ON");
                    ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    ghostMat.renderQueue = 3000;
                }
                
                if (ghostMat.HasProperty("_BaseColor")) ghostMat.SetColor("_BaseColor", ghostColor);
                rend.material = ghostMat;
            }

            _3dGhostUnit.SetActive(false);
        }

        public void OnDeployDragUpdate(UnitData unit, Vector2 screenPos, bool isOutsideUI)
        {
            if (_3dGhostUnit == null) return;

            if (!isOutsideUI)
            {
                _3dGhostUnit.SetActive(false);
                return;
            }

            Camera cameraToUse = _inputCamera != null ? _inputCamera : Camera.main;
            if (cameraToUse == null) return;

            Ray ray = cameraToUse.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, _inputRaycastMask))
            {
                _3dGhostUnit.SetActive(false);
                return;
            }

            Vector2Int targetCell = GridSystem.Instance.WorldToGrid(hit.point);
            if (!GridSystem.Instance.IsValidCell(targetCell))
            {
                _3dGhostUnit.SetActive(false);
                return;
            }

            if (GetUnitAtCell(targetCell) != null)
            {
                _3dGhostUnit.SetActive(false);
                return;
            }

            if (unit.PieceType == ChessPieceType.Pawn && targetCell.y != 1)
            {
                _3dGhostUnit.SetActive(false);
                return;
            }
            if (unit.PieceType != ChessPieceType.Pawn && targetCell.y != 0)
            {
                _3dGhostUnit.SetActive(false);
                return;
            }

            // Valid cell, show and snap Ghost Unit
            _3dGhostUnit.SetActive(true);
            _3dGhostUnit.transform.position = GridSystem.Instance.GridToWorld(targetCell) + new Vector3(0f, 0.5f, 0f);
        }

        public void OnDeployDragEnd(UnitData unit, Vector2 screenPos, Button sourceBtn)
        {
            ClearDeploymentHighlights();

            if (_3dGhostUnit != null)
            {
                Destroy(_3dGhostUnit);
                _3dGhostUnit = null;
            }

            Camera cameraToUse = _inputCamera != null ? _inputCamera : Camera.main;
            if (cameraToUse == null) return;

            Ray ray = cameraToUse.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, _inputRaycastMask))
                return;

            Vector2Int targetCell = GridSystem.Instance.WorldToGrid(hit.point);
            if (!GridSystem.Instance.IsValidCell(targetCell))
                return;

            if (GetUnitAtCell(targetCell) != null)
                return;

            if (unit.PieceType == ChessPieceType.Pawn && targetCell.y != 1) return;
            if (unit.PieceType != ChessPieceType.Pawn && targetCell.y != 0) return;

            SpawnUnit($"Blue {unit.PieceType} (Deployed)", unit, targetCell, Team.Blue);
            _currentDeploymentCost -= unit.DeploymentCost;
            UpdateDeploymentCostText();
            sourceBtn.interactable = false;
        }

        private GameObject CreateTileHighlight(Vector2Int cell, Color color)
        {
            var tile = new GameObject($"Highlight_{cell.x}_{cell.y}");
            tile.transform.SetParent(transform, false);

            var line = tile.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = 0.15f; // Thicker
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            line.material = new Material(shader) { color = Color.white };
            line.startColor = color;
            line.endColor = color;

            float halfSize = GridSystem.Instance.TileSize * 0.5f;
            Vector3 center = GridSystem.Instance.GridToWorld(cell);
            float y = center.y + 0.8f; // Raised higher as requested
            line.SetPosition(0, new Vector3(center.x - halfSize, y, center.z - halfSize));
            line.SetPosition(1, new Vector3(center.x - halfSize, y, center.z + halfSize));
            line.SetPosition(2, new Vector3(center.x + halfSize, y, center.z + halfSize));
            line.SetPosition(3, new Vector3(center.x + halfSize, y, center.z - halfSize));

            return tile;
        }

        private void ClearDeploymentHighlights()
        {
            foreach (var hl in _deploymentHighlights) if (hl != null) Destroy(hl);
            _deploymentHighlights.Clear();
        }

        private void UpdateDeploymentCostText()
        {
            if (_deploymentCostText != null)
            {
                _deploymentCostText.text = $"Cost Left: {_currentDeploymentCost}";
            }
        }

        private void HandleDebugScenarioHotkeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (!kb.f1Key.wasPressedThisFrame &&
                !kb.f2Key.wasPressedThisFrame &&
                !kb.f3Key.wasPressedThisFrame)
            {
                return;
            }

            EnsureDebugRuntimeHooks();

            if (kb.f1Key.wasPressedThisFrame)
                ExecuteMultiCollisionScenario();

            if (kb.f2Key.wasPressedThisFrame)
                ExecuteKnightJumpScenario();

            if (kb.f3Key.wasPressedThisFrame)
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
                Debug.LogWarning("[BattleManager][F1] Required units/components are missing.");
                return;
            }

            const int pushRow = 4;
            Vector2Int actorCell = new(1, pushRow);
            Vector2Int targetCell = new(0, pushRow);
            if (!TryForceSetUnitCell(actor, actorCell) || !TryForceSetUnitCell(target, targetCell))
            {
                Debug.LogWarning("[BattleManager][F1] Failed to place actor/target for push scenario.");
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
                Debug.LogWarning("[BattleManager][F1] Failed to enqueue push ability.");
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

            Debug.Log($"[BattleManager][F1] Enqueued push action. Actor={actor.name}, Target={target.name}, Force={_pendingPushForce}, TargetWeight={target.Movement.Weight}.");
        }

        private void ExecuteKnightJumpScenario()
        {
            if (!TryGetRuntime(out ActionRuntimeController runtime))
                return;

            UnitBrain knight = FindFirstLivingFriendlyKnight();
            if (knight == null || knight.Movement == null)
            {
                Debug.LogWarning("[BattleManager][F2] Friendly knight not found.");
                return;
            }

            if (!TryBuildKnightSanctuaryJump(out Vector2Int source, out Vector2Int destination, out bool spikesBypassed))
            {
                Debug.LogWarning("[BattleManager][F2] Could not build a valid knight jump scenario.");
                return;
            }

            if (!TryForceSetUnitCell(knight, source))
            {
                Debug.LogWarning("[BattleManager][F2] Failed to place knight on jump source cell.");
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
                Debug.LogWarning($"[BattleManager][F2] Failed to enqueue MoveActionCommand from {source} to {destination}.");
                return;
            }

            LogScheduledMoveCommand(runtime, knight, destination, "F2");

            Debug.Log(
                $"[BattleManager][F2] Enqueued knight jump move from {source} to sanctuary {destination}. " +
                $"SpikesBypassedContext={spikesBypassed}.");
        }

        private void ExecuteReactionThrottleScenario()
        {
            if (!TryGetRuntime(out ActionRuntimeController runtime))
                return;

            List<UnitBrain> units = GatherLivingUnits(limit: 4);
            if (units.Count < 4)
            {
                Debug.LogWarning("[BattleManager][F3] Need at least 4 living units.");
                return;
            }

            Vector2Int center = new(GridSystem.GridWidth / 2, GridSystem.GridHeight / 2);
            if (GridSystem.Instance == null || !GridSystem.Instance.IsValidCell(center))
            {
                Debug.LogWarning("[BattleManager][F3] Center cell is invalid.");
                return;
            }

            if (!TryForceSetUnitCell(units[0], center))
            {
                Debug.LogWarning("[BattleManager][F3] Failed to place primary unit at center.");
                return;
            }

            for (int i = 1; i < units.Count; i++)
            {
                Vector3 offset = new((i - 1) * 0.07f, 0f, (i % 2 == 0 ? -1f : 1f) * 0.07f);
                units[i].transform.position = GridSystem.Instance.GridToWorld(center) + offset;
            }

            var effectList = new List<CheckmateRPG.Core.Abilities.IAbilityEffect>
            {
                new CheckmateRPG.Core.Abilities.ApplyStatusAbilityEffect
                {
                    EffectId = StatusEffectType.Poison.ToString(),
                    DurationTicks = 3,
                    StackCount = 1,
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
                Debug.LogWarning("[BattleManager][F3] Failed to enqueue AoE explosion/spread ability.");
                return;
            }

            // Force an over-depth probe so ReactionDepthGuard emits the chain-throttle warning log format.
            _ = new ReactionDepthGuard(5).IsDepthAllowed(6, $"{DebugAoeChainAbilityId}_LoopProbe");

            Debug.Log(
                $"[BattleManager][F3] Enqueued AoE cast at {center} with 4 overlapped debug targets. " +
                "Triggered ReactionDepthGuard over-depth probe log.");
        }

        private void HandleDebugAbilityResolved(AbilityActionResolvedEvent gameEvent)
        {
            if (gameEvent == null)
                return;

            ActionRuntimeController runtime = ActionRuntimeController.Instance;

            AbilityActionResolvedPayload payload = gameEvent.Payload;
            if (_pendingPushTarget != null &&
                _pendingPushTarget.Movement != null &&
                !string.IsNullOrWhiteSpace(payload.AbilityId) &&
                string.Equals(payload.AbilityId, DebugPushAbilityId, StringComparison.Ordinal) &&
                (_pendingPushActionId == Guid.Empty || payload.ActionId == _pendingPushActionId))
            {
                ActionRuntimeController.Instance.CommitMutation(new CheckmateRPG.Core.Runtime.Mutations.KnockbackMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    _pendingPushTarget.ActorId,
                    _pendingPushDirection,
                    _pendingPushForce,
                    ApplySplatDamage: true,
                    Context: new CheckmateRPG.Core.Runtime.Mutations.MutationContext(
                        runtime != null && runtime.Scheduler != null ? runtime.Scheduler.CurrentTick : 0, 
                        Guid.Empty, 
                        _pendingPushTarget.ActorId, 
                        "DebugPush")));
                Debug.Log(
                    $"[BattleManager][F1] Applied queued knockback. Target={_pendingPushTarget.name}, " +
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
            List<CheckmateRPG.Core.Abilities.IAbilityEffect> effects,
            ActionSpeedTier castSpeed)
        {
            var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            definition.name = abilityId;
            definition.AbilityId = abilityId;
            var level1 = new AbilityLevelData
            {
                Cost = 0f,
                Cooldown = 0,
                CastSpeed = castSpeed,
                TargetingRule = targetingRule,
                Effects = effects ?? new List<CheckmateRPG.Core.Abilities.IAbilityEffect>()
            };
            definition.Levels.Add(level1);
            return definition;
        }

        private static bool TryGetRuntime(out ActionRuntimeController runtime)
        {
            runtime = ActionRuntimeController.Instance ?? ActionRuntimeController.EnsureExists();
            if (runtime != null && runtime.Scheduler != null)
                return true;

            Debug.LogWarning("[BattleManager] Runtime controller/scheduler is not ready.");
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
            if (_teams.Count == 0 || _allUnits.Count == 0) return;

            if (APManager.Instance != null && _teams.TryGetValue(Team.Blue, out List<UnitRecord> blueRoster))
            {
                int totalBlue = 0;
                int stunnedBlue = 0;
                foreach (var record in blueRoster)
                {
                    if (record.Brain != null && !record.Brain.IsDead)
                    {
                        totalBlue++;
                        if (record.Brain.StatusEffects != null && record.Brain.StatusEffects.HasStatus(StatusEffectType.Stun))
                        {
                            stunnedBlue++;
                        }
                    }
                }
                
                float multiplier = 1f;
                if (totalBlue > 0 && stunnedBlue > 0)
                {
                    multiplier = 1f - ((float)stunnedBlue / totalBlue);
                }
                APManager.Instance.RegenMultiplier = multiplier;
            }

            bool blueAlive = HasLivingUnits(Team.Blue);
            bool redAlive = HasLivingUnits(Team.Red);
            bool blueKingAlive = HasLivingKing(Team.Blue);
            bool redKingAlive = HasLivingKing(Team.Red);

            bool isKingDefeatObjective = _preferKingDefeatVictory;
            if (CheckmateRPG.Progression.StageManager.Instance != null && CheckmateRPG.Progression.StageManager.Instance.CurrentStage != null)
            {
                isKingDefeatObjective = (CheckmateRPG.Progression.StageManager.Instance.CurrentStage.Objective == CheckmateRPG.Progression.ObjectiveType.KingDefeat);
            }

            if (isKingDefeatObjective)
            {
                // Blue team always loses if Blue King dies, regardless of objective? 
                // The prompt mentions "상대 유닛을 전부 처치하면 종료되는 스테이지" (Defeat all enemy units).
                // So Blue still loses if Blue King dies? Usually player must protect their king.
                // Let's assume Blue always loses if Blue King dies.
                if (!blueKingAlive || !redKingAlive)
                {
                    _battleEnded = true;
                    string result = blueKingAlive == redKingAlive
                        ? "Draw (Both Kings Down)"
                        : (blueKingAlive ? "Blue Victory" : "Red Victory");
                    Debug.Log($"[BattleManager] Battle ended: {result}");
                    ShowGameOverPanel(result);
                    return;
                }
            }
            else
            {
                // Annihilation objective for Red. 
                // For Blue, let's still enforce King survival as a lose condition, or Annihilation for Blue too.
                // If Blue King is dead, Blue loses immediately.
                if (!blueKingAlive)
                {
                    _battleEnded = true;
                    Debug.Log($"[BattleManager] Battle ended: Red Victory (Blue King died)");
                    ShowGameOverPanel("Red Victory");
                    return;
                }

                // If Red team is annihilated, Blue wins.
                if (!redAlive)
                {
                    _battleEnded = true;
                    Debug.Log($"[BattleManager] Battle ended: Blue Victory (Red Annihilated)");
                    ShowGameOverPanel("Blue Victory");
                    return;
                }
                
                // If Blue team is annihilated, Red wins.
                if (!blueAlive)
                {
                    _battleEnded = true;
                    Debug.Log($"[BattleManager] Battle ended: Red Victory (Blue Annihilated)");
                    ShowGameOverPanel("Red Victory");
                    return;
                }
            }
        }

        private void ShowGameOverPanel(string resultText)
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(true);
            }
            if (_gameOverText != null)
            {
                _gameOverText.text = resultText;
            }

            bool isWin = resultText.Contains("Blue Victory");
            if (isWin)
            {
                CheckmateRPG.UI.CheckmateCinematicUI.Instance.PlayCheckmate(() =>
                {
                    var go = new GameObject("BattleResultUI");
                    var ui = go.AddComponent<CheckmateRPG.UI.BattleResultUI>();
                    ui.ShowResult(true);
                });
            }
            else
            {
                var go = new GameObject("BattleResultUI");
                var ui = go.AddComponent<CheckmateRPG.UI.BattleResultUI>();
                ui.ShowResult(false);
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
        }

        private static void EnsureGridSystem()
        {
            if (GridSystem.Instance != null)
                return;

            var gridGO = new GameObject("GridSystem");
            gridGO.AddComponent<GridSystem>();
        }

        private APManager EnsureAPManager(bool enableDebugLogging)
        {
            if (APManager.Instance != null)
                return APManager.Instance;

            var existing = UnityEngine.Object.FindFirstObjectByType<APManager>();
            if (existing != null)
                return existing;

            var apGO = new GameObject("GlobalAPManager");
            var manager = apGO.AddComponent<APManager>();

            if (enableDebugLogging)
                apGO.AddComponent<APDebugLogger>();

            return manager;
        }

        private UnitBrain SpawnUnit(string unitName, UnitData data, Vector2Int cell, Team team)
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
            return brain;
        }

        private void HandleMouseInputAdapter()
        {
            if (!_enableMouseInputAdapter || UnityEngine.InputSystem.Mouse.current == null || !UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (GridSystem.Instance == null)
                return;

            Camera cameraToUse = _inputCamera != null ? _inputCamera : Camera.main;
            if (cameraToUse == null)
                return;

            Ray ray = cameraToUse.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
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
                        Debug.Log("[BattleManager][AttackMode] No enemy target on clicked cell.");
                        return;
                    }

                    if (_selectedUnit.Combat == null || !_selectedUnit.Combat.IsTargetInAttackRange(targetUnit.gameObject))
                    {
                        Debug.Log(
                            $"[BattleManager][AttackMode] Target out of range. " +
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
                            $"[BattleManager][SkillMode] Queue failed. Actor={_selectedUnit.name}, Cell={cell}");
                        return;
                    }
                    Debug.Log(
                        $"[BattleManager][SkillMode] Queued placeholder skill. Actor={_selectedUnit.name}, " +
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
                    $"[BattleManager][InputQueue] Action queue failed. Mode={_currentInputMode}, " +
                    $"Actor={_selectedUnit.name}, Cell={cell}");
                return;
            }

            AbilityActionCommand command = BuildInputAbilityCommand(_selectedUnit, cell, targetUnit);
            APDebugLogger.RecordQueuedCommand(command);

            if (!hasEnemyTarget && TryGetRuntime(out ActionRuntimeController runtime))
                LogScheduledMoveCommand(runtime, _selectedUnit, cell, "Input");

            if (_logQueuedInputCommands && command != null)
                Debug.Log($"[BattleManager] Enqueued input command: {APDebugLogger.LastQueuedCommandSummary}");
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
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-24f, 24f);
            panelRect.sizeDelta = new Vector2(380f, 80f);

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

            // Create AP Bar
            GameObject apPanel = new GameObject("APPanel");
            apPanel.transform.SetParent(canvasObject.transform, false);
            RectTransform apRect = apPanel.AddComponent<RectTransform>();
            apRect.anchorMin = new Vector2(1f, 0f);
            apRect.anchorMax = new Vector2(1f, 0f);
            apRect.pivot = new Vector2(1f, 0f);
            apRect.anchoredPosition = new Vector2(-24f, 120f);
            apRect.sizeDelta = new Vector2(250f, 40f);
            Image apBg = apPanel.AddComponent<Image>();
            apBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            apBg.raycastTarget = false;

            GameObject apFillGO = new GameObject("Fill");
            apFillGO.transform.SetParent(apPanel.transform, false);
            RectTransform fillRect = apFillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.sizeDelta = Vector2.zero;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image apFill = apFillGO.AddComponent<Image>();
            apFill.color = new Color(0.2f, 0.8f, 0.9f, 1f); // Arknights-like cyan
            apFill.raycastTarget = false;
            _apFillImage = apFill;

            GameObject apTextGO = new GameObject("APLabel");
            apTextGO.transform.SetParent(apPanel.transform, false);
            Text apText = apTextGO.AddComponent<Text>();
            _apTextLabel = apText;
            apText.text = "AP";
            apText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            apText.fontSize = 20;
            apText.alignment = TextAnchor.MiddleCenter;
            apText.color = Color.white;
            apText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            apText.GetComponent<RectTransform>().anchorMax = Vector2.one;
            apText.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            // Create Game Over Panel
            _gameOverPanel = new GameObject("GameOverPanel");
            _gameOverPanel.transform.SetParent(canvasObject.transform, false);
            RectTransform goRect = _gameOverPanel.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.sizeDelta = Vector2.zero;
            Image goBg = _gameOverPanel.AddComponent<Image>();
            goBg.color = new Color(0f, 0f, 0f, 0.85f);
            
            GameObject goTextGO = new GameObject("ResultText");
            goTextGO.transform.SetParent(_gameOverPanel.transform, false);
            _gameOverText = goTextGO.AddComponent<Text>();
            _gameOverText.text = "VICTORY";
            _gameOverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _gameOverText.fontSize = 120;
            _gameOverText.alignment = TextAnchor.MiddleCenter;
            _gameOverText.color = Color.white;
            _gameOverText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 150f);

            GameObject restartBtnGO = new GameObject("RestartButton");
            restartBtnGO.transform.SetParent(_gameOverPanel.transform, false);
            RectTransform btnRect = restartBtnGO.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(300f, 80f);
            btnRect.anchoredPosition = new Vector2(0f, -100f);
            Image btnImg = restartBtnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.6f, 0.2f);
            Button restartBtn = restartBtnGO.AddComponent<Button>();
            restartBtn.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            
            GameObject btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(restartBtnGO.transform, false);
            Text btnText = btnTextGO.AddComponent<Text>();
            btnText.text = "RESTART BATTLE";
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize = 32;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            btnText.GetComponent<RectTransform>().anchorMax = Vector2.one;
            btnText.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            _gameOverPanel.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        private void EnsureDeploymentUI()
        {
            if (_deploymentPanel != null) return;
            if (_commandCanvas == null) EnsureCommandModeUI();

            _deploymentPanel = new GameObject("DeploymentPanel");
            _deploymentPanel.transform.SetParent(_commandCanvas.transform, false);
            RectTransform panelRect = _deploymentPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0f);
            panelRect.anchorMax = new Vector2(0.9f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 24f);
            panelRect.sizeDelta = new Vector2(0f, 100f);

            Image bg = _deploymentPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            HorizontalLayoutGroup layout = _deploymentPanel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childAlignment = TextAnchor.MiddleCenter;

            // Roster Container
            GameObject rosterGO = new GameObject("Roster");
            rosterGO.transform.SetParent(_deploymentPanel.transform, false);
            LayoutElement rosterLE = rosterGO.AddComponent<LayoutElement>();
            rosterLE.flexibleWidth = 3f; // 3/4 of the panel

            HorizontalLayoutGroup rosterLayout = rosterGO.AddComponent<HorizontalLayoutGroup>();
            rosterLayout.spacing = 8f;
            rosterLayout.childForceExpandWidth = false;
            rosterLayout.childControlWidth = true;

            if (CheckmateRPG.Progression.DeckManager.Instance != null && CheckmateRPG.Progression.DeckManager.Instance.CurrentDeck.Length > 0)
            {
                foreach (UnitData unit in CheckmateRPG.Progression.DeckManager.Instance.GetModifiedDeck())
                {
                    if (unit == null) continue;
                    Button btn = CreateModeButton(rosterGO.transform, $"{unit.UnitName}", new Color(0.3f, 0.3f, 0.4f), null);
                    LayoutElement btnLE = btn.gameObject.AddComponent<LayoutElement>();
                    btnLE.minWidth = 80f;
                    btnLE.preferredWidth = 100f;
                    
                    var draggable = btn.gameObject.AddComponent<CheckmateRPG.Testing.UI.DraggableDeploymentButton>();
                    draggable.Init(unit, this);
                }
            }

            // Status Area
            GameObject statusGO = new GameObject("Status");
            statusGO.transform.SetParent(_deploymentPanel.transform, false);
            LayoutElement statusLE = statusGO.AddComponent<LayoutElement>();
            statusLE.flexibleWidth = 1f; // 1/4 of the panel

            VerticalLayoutGroup statusLayout = statusGO.AddComponent<VerticalLayoutGroup>();
            statusLayout.spacing = 8f;
            statusLayout.childForceExpandWidth = true;
            statusLayout.childControlWidth = true;
            
            GameObject textGO = new GameObject("CostText");
            textGO.transform.SetParent(statusGO.transform, false);
            _deploymentCostText = textGO.AddComponent<Text>();
            _deploymentCostText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _deploymentCostText.fontSize = 24;
            _deploymentCostText.color = Color.cyan;
            _deploymentCostText.alignment = TextAnchor.MiddleCenter;

            Button startBtn = CreateModeButton(statusGO.transform, "START BATTLE", new Color(0.8f, 0.2f, 0.2f), StartBattle);
            LayoutElement startLE = startBtn.gameObject.AddComponent<LayoutElement>();
            startLE.minHeight = 40f;

            _deploymentPanel.SetActive(false);
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
                    $"[BattleManager] Enqueued skill placeholder action. " +
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

