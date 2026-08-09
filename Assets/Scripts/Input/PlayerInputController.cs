using UnityEngine;
using UnityEngine.InputSystem;
using CheckmateRPG.Units;
using CheckmateRPG.Components;

namespace CheckmateRPG.PlayerInput
{
    public class PlayerInputController : MonoBehaviour
    {
        public enum InputState
        {
            Idle,
            Selected
        }

        public InputState CurrentState { get; private set; } = InputState.Idle;
        public UnitBrain SelectedUnit { get; private set; }
        public event System.Action<UnitBrain> OnSelectionChanged;

        private void Update()
        {
            if (Mouse.current == null) return;

            // Left Click: Select Allied Unit
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleLeftClick();
            }

            // Right Click: Move or Attack
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                HandleRightClick();
            }
        }

        private void HandleLeftClick()
        {
            if (GridRaycaster.Instance == null) return;

            UnitBrain clickedUnit = GridRaycaster.Instance.GetUnitUnderMouse();
            
            if (clickedUnit != null)
            {
                var team = clickedUnit.GetComponent<TeamComponent>();
                if (team != null && team.IsPlayer)
                {
                    SelectUnit(clickedUnit);
                    return;
                }
            }

            // Clicking empty space or enemy deselects
            DeselectUnit();
        }

        private void HandleRightClick()
        {
            if (CurrentState != InputState.Selected || SelectedUnit == null) return;
            if (GridRaycaster.Instance == null) return;

            Vector2Int clickedCell = GridRaycaster.Instance.GetCellUnderMouse();
            if (clickedCell.x < 0) return; // Invalid cell

            UnitBrain clickedUnit = GridRaycaster.Instance.GetUnitUnderMouse();

            if (clickedUnit != null)
            {
                var team = clickedUnit.GetComponent<TeamComponent>();
                if (team != null && !team.IsPlayer)
                {
                    // Attack
                    Debug.Log($"[PlayerInputController] Ordering {SelectedUnit.name} to Attack {clickedUnit.name}");
                    SelectedUnit.QueueAttackAction(clickedUnit.gameObject);
                    
                    // Optional: Deselect after action if desired. For now, keep selected to show AP change.
                }
            }
            else
            {
                // Move
                Debug.Log($"[PlayerInputController] Ordering {SelectedUnit.name} to Move to {clickedCell}");
                SelectedUnit.QueueMoveAction(clickedCell);
            }
        }

        public void SelectUnit(UnitBrain unit)
        {
            SelectedUnit = unit;
            CurrentState = InputState.Selected;
            Debug.Log($"[PlayerInputController] Selected {unit.name}");
            
            OnSelectionChanged?.Invoke(unit);

            if (CheckmateRPG.UI.TileHighlighter.Instance != null)
            {
                CheckmateRPG.UI.TileHighlighter.Instance.OnUnitSelected(unit);
            }
        }

        public void DeselectUnit()
        {
            if (SelectedUnit != null)
            {
                Debug.Log($"[PlayerInputController] Deselected {SelectedUnit.name}");
            }
            SelectedUnit = null;
            CurrentState = InputState.Idle;
            
            OnSelectionChanged?.Invoke(null);

            if (CheckmateRPG.UI.TileHighlighter.Instance != null)
            {
                CheckmateRPG.UI.TileHighlighter.Instance.OnUnitDeselected();
            }
        }
    }
}
