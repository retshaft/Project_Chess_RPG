using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Data;

namespace CheckmateRPG.UI
{
    public class TileHighlighter : MonoBehaviour
    {
        public static TileHighlighter Instance { get; private set; }

        [Header("Movement Highlight (Neon Blue)")]
        [SerializeField] private Color _moveColor = new Color(0f, 0.8f, 1f, 1f);
        [SerializeField] private float _lineWidth = 0.05f;

        [Header("Threat Highlight (Red)")]
        [SerializeField] private Color _threatColor = new Color(1f, 0.1f, 0.1f, 0.4f);
        [SerializeField] private Color _threatBorderColor = new Color(1f, 0.1f, 0.1f, 1f);
        
        [Header("Settings")]
        [SerializeField] private float _yOffset = 0.02f;

        private UnitBrain _selectedUnit;
        private List<GameObject> _moveHighlights = new List<GameObject>();
        private List<GameObject> _threatHighlights = new List<GameObject>();
        
        private Material _lineMaterial;
        private Material _quadMaterial;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            CreateMaterials();
        }

        private void Update()
        {
            // Real-time update of threat ranges
            UpdateThreatOverlay();
            
            // Real-time update of move ranges (if a unit is selected and moving)
            if (_selectedUnit != null)
            {
                UpdateMoveOverlay();
            }
        }

        public void OnUnitSelected(UnitBrain unit)
        {
            _selectedUnit = unit;
            UpdateMoveOverlay();
        }

        public void OnUnitDeselected()
        {
            _selectedUnit = null;
            ClearMoveOverlay();
        }

        private void CreateMaterials()
        {
            Shader lineShader = Shader.Find("Hidden/Internal-Colored");
            if (lineShader == null) lineShader = Shader.Find("Sprites/Default");
            
            _lineMaterial = new Material(lineShader);
            _lineMaterial.color = Color.white;

            Shader quadShader = Shader.Find("UI/Default");
            if (quadShader == null) quadShader = Shader.Find("Sprites/Default");
            
            _quadMaterial = new Material(quadShader);
            _quadMaterial.color = Color.white;
        }

        private void UpdateMoveOverlay()
        {
            ClearMoveOverlay();
            if (_selectedUnit == null || _selectedUnit.IsDead || _selectedUnit.Movement == null) return;

            var reachable = _selectedUnit.Movement.GetReachableCells();
            foreach (var cell in reachable)
            {
                _moveHighlights.Add(CreateTileBorder(cell, _moveColor));
            }
        }

        private void ClearMoveOverlay()
        {
            foreach (var go in _moveHighlights)
            {
                if (go != null) Destroy(go);
            }
            _moveHighlights.Clear();
        }

        private void UpdateThreatOverlay()
        {
            ClearThreatOverlay();

            if (GridSystem.Instance == null) return;

            HashSet<Vector2Int> threatenedCells = new HashSet<Vector2Int>();
            
            // Find all enemies
            var allUnits = FindObjectsByType<UnitBrain>(FindObjectsSortMode.None);
            foreach (var unit in allUnits)
            {
                if (unit == null || unit.IsDead) continue;
                if (!unit.TryGetComponent(out TeamComponent team) || team.IsPlayer) continue;

                // For enemies, calculate their current attack range
                int attackRange = unit.UnitData != null ? Mathf.Max(1, unit.UnitData.AttackRange) : 1;
                Vector2Int origin = unit.Movement != null ? unit.Movement.GridPosition : new Vector2Int(Mathf.RoundToInt(unit.transform.position.x), Mathf.RoundToInt(unit.transform.position.z));
                
                for (int x = 0; x < GridSystem.GridWidth; x++)
                {
                    for (int y = 0; y < GridSystem.GridHeight; y++)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        
                        bool inRange = CombatPatternRules.IsAttackReachable(
                            unit.UnitData != null ? unit.UnitData.PieceType : ChessPieceType.Pawn,
                            true, // isEnemy
                            origin,
                            cell,
                            attackRange,
                            sampleCell => {
                                if (sampleCell == origin || sampleCell == cell) return false;
                                GameObject block = GridSystem.Instance.GetOccupant(sampleCell);
                                return block != null;
                            });

                        if (inRange)
                        {
                            threatenedCells.Add(cell);
                        }
                    }
                }
            }

            foreach (var cell in threatenedCells)
            {
                _threatHighlights.Add(CreateTileQuad(cell, _threatColor));
                _threatHighlights.Add(CreateTileBorder(cell, _threatBorderColor));
            }
        }

        private void ClearThreatOverlay()
        {
            foreach (var go in _threatHighlights)
            {
                if (go != null) Destroy(go);
            }
            _threatHighlights.Clear();
        }

        private GameObject CreateTileBorder(Vector2Int cell, Color color)
        {
            var tile = new GameObject($"Border_{cell.x}_{cell.y}");
            tile.transform.SetParent(transform, false);

            var line = tile.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = _lineWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.material = _lineMaterial;
            line.startColor = color;
            line.endColor = color;

            float halfSize = GridSystem.Instance.TileSize * 0.5f;
            Vector3 center = GridSystem.Instance.GridToWorld(cell);
            float y = center.y + _yOffset;
            
            line.SetPosition(0, new Vector3(center.x - halfSize, y, center.z - halfSize));
            line.SetPosition(1, new Vector3(center.x - halfSize, y, center.z + halfSize));
            line.SetPosition(2, new Vector3(center.x + halfSize, y, center.z + halfSize));
            line.SetPosition(3, new Vector3(center.x + halfSize, y, center.z - halfSize));

            return tile;
        }

        private GameObject CreateTileQuad(Vector2Int cell, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Quad_{cell.x}_{cell.y}";
            quad.transform.SetParent(transform, false);
            Destroy(quad.GetComponent<Collider>());

            float halfSize = GridSystem.Instance.TileSize * 0.5f;
            Vector3 center = GridSystem.Instance.GridToWorld(cell);
            
            quad.transform.position = new Vector3(center.x, center.y + _yOffset, center.z);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(GridSystem.Instance.TileSize, GridSystem.Instance.TileSize, 1f);

            var mr = quad.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            
            Material mat = new Material(_quadMaterial);
            mat.color = color;
            mr.material = mat;

            return quad;
        }
    }
}
