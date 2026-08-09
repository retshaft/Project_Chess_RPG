using UnityEngine;
using UnityEngine.InputSystem;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.PlayerInput
{
    public class GridRaycaster : MonoBehaviour
    {
        public static GridRaycaster Instance { get; private set; }

        [SerializeField] private LayerMask _gridLayerMask = ~0;
        private Camera _mainCamera;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            _mainCamera = Camera.main;
        }

        public Vector2Int GetCellUnderMouse()
        {
            if (_mainCamera == null || Mouse.current == null)
                return new Vector2Int(-1, -1);

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, _gridLayerMask))
            {
                if (GridSystem.Instance != null)
                {
                    return GridSystem.Instance.WorldToGrid(hit.point);
                }
            }
            
            // Fallback: If raycast hits nothing but we have a flat plane at Y=0
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 point = ray.GetPoint(distance);
                if (GridSystem.Instance != null)
                {
                    return GridSystem.Instance.WorldToGrid(point);
                }
            }

            return new Vector2Int(-1, -1);
        }

        public GameObject GetOccupantUnderMouse()
        {
            Vector2Int cell = GetCellUnderMouse();
            if (GridSystem.Instance != null && GridSystem.Instance.IsValidCell(cell))
            {
                return GridSystem.Instance.GetOccupant(cell);
            }
            return null;
        }

        public UnitBrain GetUnitUnderMouse()
        {
            GameObject occupant = GetOccupantUnderMouse();
            if (occupant != null)
            {
                return occupant.GetComponent<UnitBrain>();
            }
            return null;
        }
    }
}
