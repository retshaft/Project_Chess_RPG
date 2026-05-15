using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Replay;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Simulation.Spatial;
using UnityEngine;

namespace CheckmateRPG.Core.DebugOverlay
{
    public sealed class RuntimeDebugOverlay : MonoBehaviour
    {
        private const int WindowId = 9100;

        [SerializeField] private KeyCode _toggleKey = KeyCode.F9;
        [SerializeField] private bool _visibleOnStart = true;
        [SerializeField] private Rect _windowRect = new Rect(16f, 16f, 440f, 360f);
        [SerializeField] private int _maxActionRows = 20;

        private bool _visible;
        private Vector2 _scroll;

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

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Runtime Debug Overlay");
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

            int tick = controller.Scheduler?.CurrentTick ?? -1;
            IReadOnlySimulationRuntime runtime = controller.SimulationRuntime;
            PositionReservationSnapshot reservation = controller.PositionReservations;
            MutationJournal mutationJournal = controller.MutationJournal;

            GUILayout.Label($"CurrentTick: {tick}");

            IReadOnlyCollection<IActionCommand> activeActions = controller.Scheduler?.GetActiveActions();
            int actionCount = activeActions?.Count ?? 0;
            GUILayout.Label($"ActionQueue: {actionCount} active");

            int winningReservations = reservation?.WinningReservationsByAction?.Count ?? 0;
            int lostReservations = reservation?.ReservationLostActions?.Count ?? 0;
            GUILayout.Label($"Reservation state: win={winningReservations}, lost={lostReservations}");

            int mutationCount = mutationJournal?.GetEntriesForTick(tick)?.Count ?? 0;
            GUILayout.Label($"MutationQueue(tick): {mutationCount}");

            int effectCount = runtime?.ActiveEffects?.Count ?? 0;
            GUILayout.Label($"Active effects: {effectCount}");

            _scroll = GUILayout.BeginScrollView(_scroll);
            int shown = 0;
            if (activeActions != null)
            {
                foreach (IActionCommand action in activeActions)
                {
                    if (shown >= _maxActionRows)
                    {
                        GUILayout.Label($"... {actionCount - shown} more");
                        break;
                    }

                    if (action == null)
                        continue;

                    GUILayout.Label($"[{action.State}] A:{action.ActionId:N} Actor:{action.ActorId:N} S:{action.SpeedTier}");
                    shown++;
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}
