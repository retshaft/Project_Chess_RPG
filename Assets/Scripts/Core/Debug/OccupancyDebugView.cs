using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Simulation.Spatial;
using UnityEngine;

namespace CheckmateRPG.Core.DebugOverlay
{
    public sealed class OccupancyDebugView : MonoBehaviour
    {
        private const int WindowId = 9102;

        [SerializeField] private KeyCode _toggleKey = KeyCode.F11;
        [SerializeField] private bool _visibleOnStart;
        [SerializeField] private Rect _windowRect = new Rect(16f, 392f, 520f, 360f);
        [SerializeField] private int _maxTiles = 30;
        [SerializeField] private int _maxConflicts = 20;

        private bool _visible;
        private Vector2 _tileScroll;
        private Vector2 _conflictScroll;

        private void Start()
        {
            _visible = _visibleOnStart;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Occupancy Debug View");
        }

        private void DrawWindow(int id)
        {
            ActionRuntimeController controller = ActionRuntimeController.Instance;
            if (controller == null)
            {
                GUILayout.Label("ActionRuntimeController is not available.");
                GUI.DragWindow();
                return;
            }

            IReadOnlySimulationRuntime runtime = controller.SimulationRuntime;
            PositionReservationSnapshot reservation = controller.PositionReservations;

            IReadOnlyDictionary<Vector2Int, Guid> occupied = runtime?.OccupiedPositions;
            int occupiedCount = occupied?.Count ?? 0;
            GUILayout.Label($"tile ownership: {occupiedCount}");

            _tileScroll = GUILayout.BeginScrollView(_tileScroll, GUILayout.Height(160f));
            if (occupied != null)
            {
                int shown = 0;
                foreach (KeyValuePair<Vector2Int, Guid> pair in occupied)
                {
                    if (shown >= _maxTiles)
                    {
                        GUILayout.Label($"... {occupiedCount - shown} more");
                        break;
                    }

                    GUILayout.Label($"({pair.Key.x},{pair.Key.y}) owner={pair.Value:N}");
                    shown++;
                }
            }
            GUILayout.EndScrollView();

            int conflictCount = reservation?.ReservationLostActions?.Count ?? 0;
            int blockedMovementCount = ResolveBlockedMovementCount(reservation, controller);
            GUILayout.Label($"reservation conflict: {conflictCount}");
            GUILayout.Label($"blocked movement: {blockedMovementCount}");

            _conflictScroll = GUILayout.BeginScrollView(_conflictScroll, GUILayout.Height(120f));
            if (reservation?.ReservationLostActions != null)
            {
                int shown = 0;
                foreach (Guid actionId in reservation.ReservationLostActions)
                {
                    if (shown >= _maxConflicts)
                    {
                        GUILayout.Label($"... {conflictCount - shown} more");
                        break;
                    }

                    GUILayout.Label($"blocked action={actionId:N}");
                    shown++;
                }
            }
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private static int ResolveBlockedMovementCount(PositionReservationSnapshot reservation, ActionRuntimeController controller)
        {
            if (reservation?.ReservationLostActions == null || reservation.ReservationLostActions.Count == 0)
                return 0;

            IReadOnlyCollection<IActionCommand> activeActions = controller?.Scheduler?.GetActiveActions();
            if (activeActions == null || activeActions.Count == 0)
                return 0;

            var lostLookup = new HashSet<Guid>(reservation.ReservationLostActions);
            int count = 0;
            foreach (IActionCommand action in activeActions)
            {
                if (action == null)
                    continue;

                if (!lostLookup.Contains(action.ActionId))
                    continue;

                if (action is MoveActionCommand)
                    count++;
            }

            return count;
        }
    }
}
