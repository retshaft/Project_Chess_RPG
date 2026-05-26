using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.Testing
{
    public class BattleSelectionOverlayController : MonoBehaviour
    {
        public enum OverlayMode
        {
            Move,
            Attack,
            Skill
        }

        private static readonly Vector2Int InvalidCell = new Vector2Int(-1, -1);

        [SerializeField] private bool _useInputToSelect = true;
        [SerializeField] private Color _moveOverlayColor = new Color(0.15f, 0.75f, 1f, 0.95f);
        [SerializeField] private Color _attackOverlayColor = new Color(0.95f, 0.2f, 0.25f, 0.95f);
        [SerializeField] private float _overlayHeight = 0.035f;
        [SerializeField] private float _overlayWidth = 0.06f;

        private readonly List<GameObject> _overlayTiles = new();
        private readonly List<UnitBrain> _playerUnits = new();
        private Camera _mainCamera;
        private Material _overlayMaterial;
        private UnitBrain _selectedUnit;
        private Vector2Int _lastOverlayCell = InvalidCell;
        private OverlayMode _overlayMode = OverlayMode.Move;

        public void SetUseInputSelection(bool useInputToSelect)
        {
            _useInputToSelect = useInputToSelect;
        }

        public void SetSelectedUnit(UnitBrain unit)
        {
            _selectedUnit = unit;
            _lastOverlayCell = unit != null && unit.Movement != null ? unit.Movement.GridPosition : InvalidCell;
            RebuildOverlay();
        }

        public void SetOverlayMode(OverlayMode mode)
        {
            if (_overlayMode == mode)
                return;

            _overlayMode = mode;
            RebuildOverlay();
        }

        public void ClearSelection()
        {
            SetSelectedUnit(null);
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_useInputToSelect)
            {
                RefreshPlayerUnits();
                SelectFirstPlayerUnit();
                RebuildOverlay();
                return;
            }

            if (_selectedUnit != null)
                RebuildOverlay();
        }

        private void Update()
        {
            if (_useInputToSelect)
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
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                TrySelectFromMouse();

            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
                SelectNextPlayerUnit();
        }

        private void RefreshSelectionState()
        {
            if (_selectedUnit == null || _selectedUnit.IsDead || _selectedUnit.Movement == null)
            {
                if (_useInputToSelect)
                    SelectFirstPlayerUnit();
                else
                    ClearOverlay();

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

            if (_mainCamera == null || UnityEngine.InputSystem.Mouse.current == null)
                return;

            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);
            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            UnitBrain candidate = hit.collider.GetComponent<UnitBrain>() ?? hit.collider.GetComponentInParent<UnitBrain>();
            if (candidate == null || candidate.IsDead)
                return;

            if (!candidate.TryGetComponent(out TeamComponent team) || !team.IsPlayer)
                return;

            SetSelectedUnit(candidate);
        }

        private void SelectFirstPlayerUnit()
        {
            RefreshPlayerUnits();
            foreach (UnitBrain brain in _playerUnits)
            {
                if (brain != null && !brain.IsDead)
                {
                    SetSelectedUnit(brain);
                    return;
                }
            }

            SetSelectedUnit(null);
        }

        private void SelectNextPlayerUnit()
        {
            RefreshPlayerUnits();

            if (_playerUnits.Count == 0)
            {
                SetSelectedUnit(null);
                return;
            }

            if (_selectedUnit == null)
            {
                SetSelectedUnit(_playerUnits[0]);
                return;
            }

            int currentIndex = _playerUnits.IndexOf(_selectedUnit);
            int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % _playerUnits.Count : 0;
            SetSelectedUnit(_playerUnits[nextIndex]);
        }

        private void RebuildOverlay()
        {
            ClearOverlay();

            if (_selectedUnit == null || _selectedUnit.IsDead || _selectedUnit.Movement == null || GridSystem.Instance == null)
                return;

            _lastOverlayCell = _selectedUnit.Movement.GridPosition;
            IReadOnlyList<Vector2Int> cells = _overlayMode switch
            {
                OverlayMode.Attack => BuildAttackCells(_selectedUnit),
                OverlayMode.Skill => new List<Vector2Int>(),
                _ => _selectedUnit.Movement.GetReachableCells()
            };

            foreach (Vector2Int cell in cells)
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
            Color overlayColor = GetCurrentOverlayColor();
            line.startColor = overlayColor;
            line.endColor = overlayColor;

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
            _overlayMaterial.color = Color.white;
            return _overlayMaterial;
        }

        private Color GetCurrentOverlayColor()
        {
            return _overlayMode == OverlayMode.Attack ? _attackOverlayColor : _moveOverlayColor;
        }

        private static IReadOnlyList<Vector2Int> BuildAttackCells(UnitBrain selectedUnit)
        {
            var attackCells = new List<Vector2Int>();
            if (selectedUnit == null || selectedUnit.UnitData == null || selectedUnit.Movement == null || GridSystem.Instance == null)
                return attackCells;

            int attackRange = Mathf.Max(1, selectedUnit.UnitData.AttackRange);
            Vector2Int origin = selectedUnit.Movement.GridPosition;
            bool isEnemy = selectedUnit.TryGetComponent(out TeamComponent selectedTeam) && selectedTeam.IsEnemy;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    GameObject occupant = GridSystem.Instance.GetOccupant(cell);
                    if (occupant == null || !occupant.TryGetComponent(out UnitBrain targetBrain) || targetBrain.IsDead)
                        continue;
                    if (!targetBrain.TryGetComponent(out TeamComponent targetTeam))
                        continue;
                    if (targetTeam.IsEnemy == selectedTeam.IsEnemy)
                        continue;

                    bool inRange = CombatPatternRules.IsAttackReachable(
                        selectedUnit.UnitData.PieceType,
                        isEnemy,
                        origin,
                        cell,
                        attackRange,
                        sampleCell =>
                        {
                            if (sampleCell == origin || sampleCell == cell)
                                return false;
                            GameObject block = GridSystem.Instance.GetOccupant(sampleCell);
                            return block != null;
                        });
                    if (!inRange)
                        continue;

                    attackCells.Add(cell);
                }
            }

            return attackCells;
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
