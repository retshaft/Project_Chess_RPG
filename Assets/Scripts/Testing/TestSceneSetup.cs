using System.Collections.Generic;
using CheckmateRPG.Grid;
using UnityEngine;

public class TestSceneSetup : MonoBehaviour
{
    [Header("Graybox Map")]
    [SerializeField] private bool _buildGrayboxOnAwake = true;
    [SerializeField] private GameObject _primaryTilePrefab; // 체크무늬 타일 A (예: 밝은 색)
    [SerializeField] private GameObject _secondaryTilePrefab; // 체크무늬 타일 B (예: 어두운 색)
    [SerializeField] private float _tileHeight = 0.08f;
    [SerializeField] private float _tileInset = 0.05f;
    [SerializeField] private float _boardYOffset = -0.02f;

    private readonly Dictionary<TileType, Material> _tileMaterials = new();
    private Material _boardMaterial;
    private GameObject _mapRoot;

    private void Awake()
    {
        if (!_buildGrayboxOnAwake)
            return;

        EnsureGridSystem();
        BuildGrayboxMap();
    }

    private void OnDestroy()
    {
        foreach (Material material in _tileMaterials.Values)
        {
            if (material != null)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
        }

        _tileMaterials.Clear();

        if (_boardMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(_boardMaterial);
            else
                DestroyImmediate(_boardMaterial);
        }
    }

    private static void EnsureGridSystem()
    {
        if (GridSystem.Instance != null)
            return;

        var grid = FindObjectOfType<GridSystem>();
        if (grid != null)
            return;

        var gridGO = new GameObject("GridSystem");
        gridGO.AddComponent<GridSystem>();
    }

    [ContextMenu("Build Graybox Map")]
    public void BuildGrayboxMap()
    {
        EnsureGridSystem();

        GridSystem grid = GridSystem.Instance;
        if (grid == null)
            grid = FindObjectOfType<GridSystem>();
            
        if (grid == null)
        {
            Debug.LogError("[TestSceneSetup] GridSystem could not be found or created.");
            return;
        }

        Transform existingMap = transform.Find("GrayboxMap");
        if (existingMap != null)
        {
            if (Application.isPlaying)
                Destroy(existingMap.gameObject);
            else
                DestroyImmediate(existingMap.gameObject);
        }

        _mapRoot = new GameObject("GrayboxMap");
        _mapRoot.transform.SetParent(transform, false);

        CreateBoardPlane(grid, _mapRoot.transform);
        CreateTileCubes(grid, _mapRoot.transform);
        Debug.Log("[TestSceneSetup] Graybox map generated.");
    }

    [ContextMenu("Clear Graybox Map")]
    public void ClearGrayboxMap()
    {
        Transform existingMap = transform.Find("GrayboxMap");
        if (existingMap != null)
        {
            if (Application.isPlaying)
                Destroy(existingMap.gameObject);
            else
                DestroyImmediate(existingMap.gameObject);
        }
    }

    private void CreateBoardPlane(GridSystem grid, Transform parent)
    {
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "GrayboxBoardPlane";
        plane.transform.SetParent(parent, false);

        float tileSize = Mathf.Max(0.01f, grid.TileSize);
        float boardWidth = GridSystem.GridWidth * tileSize;
        float boardHeight = GridSystem.GridHeight * tileSize;

        Vector3 bottomLeft = grid.GridToWorld(0, 0) - new Vector3(tileSize * 0.5f, 0f, tileSize * 0.5f);
        Vector3 center = bottomLeft + new Vector3(boardWidth * 0.5f, _boardYOffset, boardHeight * 0.5f);

        plane.transform.position = center;
        plane.transform.localScale = new Vector3(boardWidth / 10f, 1f, boardHeight / 10f);

        _boardMaterial ??= CreateMaterial(new Color(0.18f, 0.18f, 0.18f));
        if (plane.TryGetComponent(out Renderer renderer))
            renderer.material = _boardMaterial;
    }

    private void CreateTileCubes(GridSystem grid, Transform parent)
    {
        float tileSize = Mathf.Max(0.01f, grid.TileSize);
        float cubeHeight = Mathf.Max(0.01f, _tileHeight);
        float inset = Mathf.Clamp(_tileInset, 0f, tileSize * 0.45f);
        float tileScale = tileSize - inset * 2f;

        for (int x = 0; x < GridSystem.GridWidth; x++)
        {
            for (int y = 0; y < GridSystem.GridHeight; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                GameObject tile;
                
                // (x + y)의 짝/홀수 여부로 체스판 패턴 결정
                bool isPrimary = (x + y) % 2 == 0;
                GameObject prefabToUse = isPrimary ? _primaryTilePrefab : _secondaryTilePrefab;

                if (prefabToUse != null)
                {
                    // 커스텀 프리팹(2mx2m)을 사용할 경우, 별도 Scale이나 Inset을 적용하지 않고 원본 그대로 배치합니다.
                    tile = Instantiate(prefabToUse, parent);
                    tile.name = $"Tile_{x}_{y}";
                    tile.transform.position = grid.GridToWorld(cell);
                    
                    // 타일이 너무 일률적으로 보이는 것을 방지하기 위해 90도 단위로 무작위 회전을 줍니다.
                    int rotationSteps = UnityEngine.Random.Range(0, 4);
                    tile.transform.rotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);
                }
                else
                {
                    // 프리팹이 하나라도 할당되지 않은 경우, 기존의 회색박스 큐브를 생성합니다.
                    tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile_{x}_{y}";
                    tile.transform.SetParent(parent, false);
                    tile.transform.position = grid.GridToWorld(cell) + new Vector3(0f, cubeHeight * 0.5f, 0f);
                    tile.transform.localScale = new Vector3(tileScale, cubeHeight, tileScale);

                    if (tile.TryGetComponent(out Renderer renderer))
                    {
                        TileType tileType = grid.GetTileType(cell);
                        renderer.material = GetOrCreateTileMaterial(tileType);
                        
                        // 그레이박스 상태일 때도 격자 무늬가 보이게 색상을 살짝 다르게 줍니다.
                        if (tileType == TileType.Normal)
                        {
                            renderer.material.color = isPrimary ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.12f, 0.12f, 0.12f);
                        }
                    }
                }
            }
        }
    }

    private Material GetOrCreateTileMaterial(TileType tileType)
    {
        if (_tileMaterials.TryGetValue(tileType, out Material material) && material != null)
            return material;

        Color color = tileType switch
        {
            TileType.Swamp => new Color(0.72f, 0.38f, 0.92f),
            TileType.Spikes => new Color(0.92f, 0.2f, 0.2f),
            TileType.Sanctuary => new Color(0.98f, 0.9f, 0.2f),
            _ => Color.white
        };

        material = CreateMaterial(color);
        _tileMaterials[tileType] = material;
        return material;
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        return material;
    }
}
