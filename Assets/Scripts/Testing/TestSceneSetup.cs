using System.Collections.Generic;
using CheckmateRPG.Grid;
using UnityEngine;

public class TestSceneSetup : MonoBehaviour
{
    [Header("Graybox Map")]
    [SerializeField] private bool _buildGrayboxOnAwake = true;
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
                Destroy(material);
        }

        _tileMaterials.Clear();

        if (_boardMaterial != null)
            Destroy(_boardMaterial);
    }

    private static void EnsureGridSystem()
    {
        if (GridSystem.Instance != null)
            return;

        var gridGO = new GameObject("GridSystem");
        gridGO.AddComponent<GridSystem>();
    }

    private void BuildGrayboxMap()
    {
        GridSystem grid = GridSystem.Instance;
        if (grid == null)
            return;

        if (_mapRoot != null)
            Destroy(_mapRoot);

        _mapRoot = new GameObject("GrayboxMap");
        _mapRoot.transform.SetParent(transform, false);

        CreateBoardPlane(grid, _mapRoot.transform);
        CreateTileCubes(grid, _mapRoot.transform);
        Debug.Log("[TestSceneSetup] Graybox map generated.");
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
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile_{x}_{y}";
                tile.transform.SetParent(parent, false);
                tile.transform.position = grid.GridToWorld(cell) + new Vector3(0f, cubeHeight * 0.5f, 0f);
                tile.transform.localScale = new Vector3(tileScale, cubeHeight, tileScale);

                if (!tile.TryGetComponent(out Renderer renderer))
                    continue;

                TileType tileType = grid.GetTileType(cell);
                renderer.material = GetOrCreateTileMaterial(tileType);
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
