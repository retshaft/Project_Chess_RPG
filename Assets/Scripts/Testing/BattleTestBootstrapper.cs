// BattleTestBootstrapper.cs
// Self-contained battle-test setup for Assets/Scenes/Test/BattleTest.unity.
//
// Attach this MonoBehaviour to the "BattleTestBootstrapper" GameObject in the scene.
// On Awake it:
//   1. Creates a GridSystem instance if none exists yet.
//   2. Spawns 6 chess units (3 blue / 3 red) on the grid and registers their occupancy.

using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.Progression;
using CheckmateRPG.Units;

namespace CheckmateRPG.Testing
{
    /// <summary>
    /// Creates a minimal playable battle scenario at runtime.
    /// </summary>
    public class BattleTestBootstrapper : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _enableAPDebugLogger = true;

        [Header("Meta Progression Sample")]
        [SerializeField] private bool _applyMetaToPlayerKnight = true;
        [SerializeField] private NotationNodeData _sampleNotationNode;
        [SerializeField] private ResonanceProfileData _sampleResonanceProfile;
        [SerializeField] private EdictData _sampleEdict;
        [SerializeField] private SyncCapacityData _sampleSyncCapacity;
        [SerializeField] [Min(0)] private int _sampleResonanceStage = 1;
        [SerializeField] [Min(0)] private int _sampleInitialTP = 2;

        private NotationNodeData _runtimeNotationNode;
        private ResonanceProfileData _runtimeResonanceProfile;
        private EdictData _runtimeEdict;
        private SyncCapacityData _runtimeSyncCapacity;

        // ─── Spawn Table ──────────────────────────────────────────────────────────

        private static readonly (ChessPieceType PieceType, Vector2Int Cell, bool IsEnemy)[] SpawnTable =
        {
            (ChessPieceType.Pawn,   new Vector2Int(0, 1), false),
            (ChessPieceType.Knight, new Vector2Int(2, 1), false),
            (ChessPieceType.Bishop, new Vector2Int(4, 1), false),
            (ChessPieceType.Rook,   new Vector2Int(0, 6), true),
            (ChessPieceType.Queen,  new Vector2Int(2, 6), true),
            (ChessPieceType.King,   new Vector2Int(4, 6), true),
        };

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureGridSystem();
            EnsureAPManager(_enableAPDebugLogger);
            SpawnAllUnits();
            EnsureSelectionOverlay();
        }

        // ─── Setup Helpers ────────────────────────────────────────────────────────

        private static void EnsureGridSystem()
        {
            if (GridSystem.Instance != null)
                return;

            var gridGO = new GameObject("GridSystem");
            gridGO.AddComponent<GridSystem>();
            Debug.Log("[BattleTestBootstrapper] GridSystem created.");
        }

        private void SpawnAllUnits()
        {
            foreach (var (pieceType, cell, isEnemy) in SpawnTable)
            {
                UnitData data = CreateUnitData(pieceType, isEnemy);
                SpawnUnit(pieceType, cell, data, isEnemy);
            }

            Debug.Log("[BattleTestBootstrapper] Chess piece units spawned and registered on grid.");
        }

        private UnitData CreateUnitData(ChessPieceType pieceType, bool isEnemy)
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            data.name = pieceType + "_Data";
            data.UnitName = (isEnemy ? "Enemy " : "Player ") + pieceType;
            data.PieceType = pieceType;
            data.SyncDefaultChessMetadata();

            data.MaxHealth = pieceType switch
            {
                ChessPieceType.Pawn => 80f,
                ChessPieceType.Knight => 110f,
                ChessPieceType.Bishop => 95f,
                ChessPieceType.Rook => 130f,
                ChessPieceType.Queen => 150f,
                ChessPieceType.King => 170f,
                _ => 100f
            };

            data.Defense = pieceType switch
            {
                ChessPieceType.Pawn => 0.05f,
                ChessPieceType.Rook => 0.2f,
                ChessPieceType.King => 0.2f,
                _ => 0.1f
            };

            data.Resistance = pieceType switch
            {
                ChessPieceType.Bishop => 0.2f,
                ChessPieceType.Queen => 0.15f,
                _ => 0.1f
            };

            data.AttackDamage = pieceType switch
            {
                ChessPieceType.Pawn => 8f,
                ChessPieceType.Knight => 14f,
                ChessPieceType.Bishop => 13f,
                ChessPieceType.Rook => 16f,
                ChessPieceType.Queen => 20f,
                ChessPieceType.King => 18f,
                _ => 10f
            };

            data.AttackCooldown = 1.1f;
            data.AttackRange = 1;
            data.KillValue = pieceType switch
            {
                ChessPieceType.Pawn => 1f,
                ChessPieceType.Knight => 3f,
                ChessPieceType.Bishop => 3f,
                ChessPieceType.Rook => 5f,
                ChessPieceType.Queen => 9f,
                ChessPieceType.King => 20f,
                _ => 1f
            };
            data.MaxSP = 100f;
            data.MoveCostAP = 4f;
            data.AttackCostAP = 6f;
            data.ActionSpeed = 1f;
            data.MoveRange = 7;
            data.MoveSpeed = 5f;
            data.Weight = pieceType is ChessPieceType.Rook or ChessPieceType.King ? 3 : 1;
            data.IsBoss = false;

            if (!isEnemy && pieceType == ChessPieceType.Knight && _applyMetaToPlayerKnight)
                data = ApplyMetaSample(data);

            return data;
        }

        private UnitData ApplyMetaSample(UnitData source)
        {
            NotationNodeData notationNode = GetOrCreateNotationNode();
            ResonanceProfileData resonance = GetOrCreateResonanceProfile();
            EdictData edict = GetOrCreateEdict();
            SyncCapacityData syncCapacity = GetOrCreateSyncCapacity();

            var notationState = new NotationProgressState(_sampleInitialTP);
            notationState.TryUnlock(notationNode);

            var loadout = new MetaProgressionLoadout
            {
                ResonanceProfile = resonance,
                ResonanceStage = Mathf.Max(0, _sampleResonanceStage),
                SyncCapacity = syncCapacity
            };

            foreach (NotationNodeData unlocked in notationState.UnlockedNodes)
                loadout.UnlockedNotationNodes.Add(unlocked);

            loadout.ActiveEdicts.Add(edict);

            UnitData modified = MetaProgressionCalculator.CreateModifiedUnitData(source, loadout);
            if (modified != null)
            {
                modified.UnitName = source.UnitName + " [Meta]";
                Debug.Log($"[BattleTestBootstrapper] Meta sample applied to {source.UnitName} -> HP {modified.MaxHealth}, ATK {modified.AttackDamage}, MoveAP {modified.MoveCostAP}");
                return modified;
            }

            return source;
        }

        private NotationNodeData GetOrCreateNotationNode()
        {
            if (_sampleNotationNode != null)
                return _sampleNotationNode;

            if (_runtimeNotationNode != null)
                return _runtimeNotationNode;

            _runtimeNotationNode = ScriptableObject.CreateInstance<NotationNodeData>();
            _runtimeNotationNode.NodeId = "Sample_Notation_01";
            _runtimeNotationNode.TPCost = 1;
            _runtimeNotationNode.Bonuses = new List<StatModifierEntry>
            {
                new StatModifierEntry { Stat = MetaStatType.MaxHealth, FlatBonus = 20f, PercentBonus = 0f },
                new StatModifierEntry { Stat = MetaStatType.AttackDamage, FlatBonus = 2f, PercentBonus = 0f }
            };
            return _runtimeNotationNode;
        }

        private ResonanceProfileData GetOrCreateResonanceProfile()
        {
            if (_sampleResonanceProfile != null)
                return _sampleResonanceProfile;

            if (_runtimeResonanceProfile != null)
                return _runtimeResonanceProfile;

            _runtimeResonanceProfile = ScriptableObject.CreateInstance<ResonanceProfileData>();
            _runtimeResonanceProfile.StageBonuses = new List<ResonanceStageBonus>
            {
                new ResonanceStageBonus
                {
                    Stage = 1,
                    Bonuses = new List<StatModifierEntry>
                    {
                        new StatModifierEntry { Stat = MetaStatType.ActionSpeed, FlatBonus = 0f, PercentBonus = 0.1f }
                    }
                }
            };
            return _runtimeResonanceProfile;
        }

        private EdictData GetOrCreateEdict()
        {
            if (_sampleEdict != null)
                return _sampleEdict;

            if (_runtimeEdict != null)
                return _runtimeEdict;

            _runtimeEdict = ScriptableObject.CreateInstance<EdictData>();
            _runtimeEdict.EdictName = "Sample Absolute Edict";
            _runtimeEdict.Kind = EdictKind.Absolute;
            _runtimeEdict.Polarity = PolarityType.Order;
            _runtimeEdict.SyncCost = 1;
            _runtimeEdict.Bonuses = new List<StatModifierEntry>
            {
                new StatModifierEntry { Stat = MetaStatType.MoveCostAP, FlatBonus = -1f, PercentBonus = 0f }
            };
            return _runtimeEdict;
        }

        private SyncCapacityData GetOrCreateSyncCapacity()
        {
            if (_sampleSyncCapacity != null)
                return _sampleSyncCapacity;

            if (_runtimeSyncCapacity != null)
                return _runtimeSyncCapacity;

            _runtimeSyncCapacity = ScriptableObject.CreateInstance<SyncCapacityData>();
            _runtimeSyncCapacity.BaseCapacity = 2;
            return _runtimeSyncCapacity;
        }

        private static APManager EnsureAPManager(bool enableDebugLogging)
        {
            if (APManager.Instance != null)
                return APManager.Instance;

            var apGO = new GameObject("APManager");
            var manager = apGO.AddComponent<APManager>();

            if (enableDebugLogging)
                apGO.AddComponent<APDebugLogger>();

            Debug.Log("[BattleTestBootstrapper] APManager created.");
            return manager;
        }

        private void SpawnUnit(ChessPieceType pieceType, Vector2Int cell, UnitData data, bool isEnemy)
        {
            string unitName = (isEnemy ? "Enemy_" : "Player_") + pieceType;

            // Parent GameObject – holds game-logic components
            var go = new GameObject(unitName);

            // Visual child: a simple primitive so the unit is visible in the Game view
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.65f, 0.5f, 0.65f);

            // Remove the collider – physics are grid-based, not physics-engine-based
            Destroy(visual.GetComponent<Collider>());

            // Team colour (works with both Built-in and URP pipelines)
            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(rend.sharedMaterial);
                var color = isEnemy
                    ? new Color(0.90f, 0.20f, 0.20f)
                    : new Color(0.20f, 0.45f, 0.90f);
                mat.color = color;
                // URP uses _BaseColor; setting both is harmless
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                rend.material = mat;
            }

            // Add logic components before UnitBrain so Awake() can cache them
            go.AddComponent<HealthComponent>();
            go.AddComponent<MovementComponent>();
            go.AddComponent<CombatComponent>();
            go.AddComponent<StatusEffectComponent>();
            var team = go.AddComponent<TeamComponent>();
            team.SetIsEnemy(isEnemy);
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.8f, 0f);
            collider.height = 1.6f;
            collider.radius = 0.4f;

            // UnitBrain.Awake() runs here and caches the components added above
            var brain = go.AddComponent<UnitBrain>();

            // Set data fields before Start() runs and initialises the components
            brain.Prepare(data, cell);

            Debug.Log($"[BattleTestBootstrapper] Spawned {unitName} ({data.BaseRole}) at {cell}.");
        }

        private void EnsureSelectionOverlay()
        {
            if (!TryGetComponent(out BattleSelectionOverlayController _))
                gameObject.AddComponent<BattleSelectionOverlayController>();
        }
    }
}
