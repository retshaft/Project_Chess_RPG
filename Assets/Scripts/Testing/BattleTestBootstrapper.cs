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

        private static void SpawnAllUnits()
        {
            foreach (var (pieceType, cell, isEnemy) in SpawnTable)
            {
                UnitData data = CreateUnitData(pieceType, isEnemy);
                SpawnUnit(pieceType, cell, data, isEnemy);
            }

            Debug.Log("[BattleTestBootstrapper] Chess piece units spawned and registered on grid.");
        }

        private static UnitData CreateUnitData(ChessPieceType pieceType, bool isEnemy)
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
            return data;
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

        private static void SpawnUnit(ChessPieceType pieceType, Vector2Int cell, UnitData data, bool isEnemy)
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

    public class BattleSelectionOverlayController : MonoBehaviour
    {
        [SerializeField] private Color _overlayColor = new Color(0.15f, 0.75f, 1f, 0.95f);
        [SerializeField] private float _overlayHeight = 0.035f;
        [SerializeField] private float _overlayWidth = 0.06f;

        private readonly List<GameObject> _overlayTiles = new();
        private readonly List<UnitBrain> _playerUnits = new();
        private Camera _mainCamera;
        private Material _overlayMaterial;
        private UnitBrain _selectedUnit;
        private Vector2Int _lastOverlayCell = new Vector2Int(-1, -1);

        private void Start()
        {
            _mainCamera = Camera.main;
            RefreshPlayerUnits();
            SelectFirstPlayerUnit();
            RebuildOverlay();
        }

        private void Update()
        {
            HandleSelectionInput();
            RefreshSelectionState();
        }

        private void OnDestroy()
        {
            ClearOverlay();

            if (_overlayMaterial != null)
                Destroy(_overlayMaterial);
        }

        private void HandleSelectionInput()
        {
            if (Input.GetMouseButtonDown(0))
                TrySelectFromMouse();

            if (Input.GetKeyDown(KeyCode.Tab))
                SelectNextPlayerUnit();
        }

        private void RefreshSelectionState()
        {
            if (_selectedUnit == null || _selectedUnit.IsDead || _selectedUnit.Movement == null)
            {
                SelectFirstPlayerUnit();
                RebuildOverlay();
                return;
            }

            Vector2Int currentCell = _selectedUnit.Movement.GridPosition;
            if (currentCell != _lastOverlayCell)
                RebuildOverlay();
        }

        private void TrySelectFromMouse()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera == null)
                return;

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            UnitBrain candidate = hit.collider.GetComponent<UnitBrain>() ?? hit.collider.GetComponentInParent<UnitBrain>();
            if (candidate == null || candidate.IsDead)
                return;

            if (!candidate.TryGetComponent(out TeamComponent team) || !team.IsPlayer)
                return;

            SelectUnit(candidate);
        }

        private void SelectFirstPlayerUnit()
        {
            RefreshPlayerUnits();
            foreach (UnitBrain brain in _playerUnits)
            {
                if (brain != null && !brain.IsDead)
                {
                    SelectUnit(brain);
                    return;
                }
            }

            SelectUnit(null);
        }

        private void SelectNextPlayerUnit()
        {
            RefreshPlayerUnits();

            if (_playerUnits.Count == 0)
            {
                SelectUnit(null);
                return;
            }

            if (_selectedUnit == null)
            {
                SelectUnit(_playerUnits[0]);
                return;
            }

            int currentIndex = _playerUnits.IndexOf(_selectedUnit);
            int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % _playerUnits.Count : 0;
            SelectUnit(_playerUnits[nextIndex]);
        }

        private void SelectUnit(UnitBrain unit)
        {
            _selectedUnit = unit;
            _lastOverlayCell = unit != null && unit.Movement != null ? unit.Movement.GridPosition : new Vector2Int(-1, -1);
            RebuildOverlay();
        }

        private void RebuildOverlay()
        {
            ClearOverlay();

            if (_selectedUnit == null || _selectedUnit.IsDead || _selectedUnit.Movement == null || GridSystem.Instance == null)
                return;

            _lastOverlayCell = _selectedUnit.Movement.GridPosition;
            foreach (Vector2Int cell in _selectedUnit.Movement.GetReachableCells())
            {
                _overlayTiles.Add(CreateTileOutline(cell));
            }
        }

        private void ClearOverlay()
        {
            foreach (GameObject tile in _overlayTiles)
            {
                if (tile != null)
                    Destroy(tile);
            }

            _overlayTiles.Clear();
        }

        private GameObject CreateTileOutline(Vector2Int cell)
        {
            var tile = new GameObject($"MoveOverlay_{cell.x}_{cell.y}");
            tile.transform.SetParent(transform, false);

            var line = tile.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = _overlayWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.material = GetOverlayMaterial();
            line.startColor = _overlayColor;
            line.endColor = _overlayColor;

            float halfSize = GridSystem.Instance.TileSize * 0.5f;
            Vector3 center = GridSystem.Instance.GridToWorld(cell);
            float y = center.y + _overlayHeight;
            Vector3 bottomLeft = new Vector3(center.x - halfSize, y, center.z - halfSize);
            Vector3 topLeft = new Vector3(center.x - halfSize, y, center.z + halfSize);
            Vector3 topRight = new Vector3(center.x + halfSize, y, center.z + halfSize);
            Vector3 bottomRight = new Vector3(center.x + halfSize, y, center.z - halfSize);

            line.SetPosition(0, bottomLeft);
            line.SetPosition(1, topLeft);
            line.SetPosition(2, topRight);
            line.SetPosition(3, bottomRight);

            return tile;
        }

        private Material GetOverlayMaterial()
        {
            if (_overlayMaterial != null)
                return _overlayMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            _overlayMaterial = new Material(shader);
            _overlayMaterial.color = _overlayColor;
            return _overlayMaterial;
        }

        private void RefreshPlayerUnits()
        {
            _playerUnits.RemoveAll(unit => unit == null || unit.IsDead || !unit.TryGetComponent(out TeamComponent team) || !team.IsPlayer);

            if (_playerUnits.Count > 0)
                return;

            foreach (UnitBrain brain in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
            {
                if (brain != null && !brain.IsDead && brain.TryGetComponent(out TeamComponent team) && team.IsPlayer)
                    _playerUnits.Add(brain);
            }
        }
    }
}
