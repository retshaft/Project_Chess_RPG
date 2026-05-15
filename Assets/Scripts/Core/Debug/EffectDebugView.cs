using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.DebugOverlay
{
    public sealed class EffectDebugView : MonoBehaviour
    {
        private const int WindowId = 9103;

        [SerializeField] private KeyCode _toggleKey = KeyCode.F12;
        [SerializeField] private bool _visibleOnStart;
        [SerializeField] private Rect _windowRect = new Rect(552f, 408f, 520f, 340f);
        [SerializeField] private int _maxRows = 50;

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

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Effect Debug View");
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
            IReadOnlyDictionary<string, IReadOnlyEffectRuntimeState> activeEffects = runtime?.ActiveEffects;

            int count = activeEffects?.Count ?? 0;
            GUILayout.Label($"active effect: {count}");

            _scroll = GUILayout.BeginScrollView(_scroll);

            if (activeEffects != null)
            {
                int shown = 0;
                foreach (KeyValuePair<string, IReadOnlyEffectRuntimeState> entry in activeEffects)
                {
                    if (shown >= _maxRows)
                    {
                        GUILayout.Label($"... {count - shown} more");
                        break;
                    }

                    IReadOnlyEffectRuntimeState effect = entry.Value;
                    if (effect == null)
                        continue;

                    int expirationTick = tick + effect.RemainingTick;
                    GUILayout.Label(
                        $"[{effect.EffectId}] stack count={effect.StackCount}, expiration tick={expirationTick}, source action={effect.SourceId:N}");
                    shown++;
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}
