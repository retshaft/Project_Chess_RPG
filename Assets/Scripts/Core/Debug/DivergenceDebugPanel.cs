using System.Collections.Generic;
using CheckmateRPG.Core.Replay;
using UnityEngine;

namespace CheckmateRPG.Core.DebugOverlay
{
    public sealed class DivergenceDebugPanel : MonoBehaviour
    {
        private const int WindowId = 9104;
        private const int MaxLog = 128;

        [SerializeField] private KeyCode _toggleKey = KeyCode.F8;
        [SerializeField] private bool _visibleOnStart;
        [SerializeField] private Rect _windowRect = new Rect(1008f, 16f, 520f, 380f);

        private bool _visible;
        private Vector2 _scroll;
        private readonly List<DivergenceEvent> _events = new();
        private DivergenceDetector _detector;

        private void Start()
        {
            _visible = _visibleOnStart;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;
        }

        private void OnDestroy()
        {
            if (_detector != null)
                _detector.DivergenceDetected -= HandleDivergence;
        }

        public void SetDetector(DivergenceDetector detector)
        {
            if (_detector != null)
                _detector.DivergenceDetected -= HandleDivergence;

            _detector = detector;

            if (_detector != null)
                _detector.DivergenceDetected += HandleDivergence;
        }

        private void HandleDivergence(DivergenceEvent divergence)
        {
            if (_events.Count >= MaxLog)
                _events.RemoveAt(0);

            _events.Add(divergence);
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Divergence Debug Panel");
        }

        private void DrawWindow(int id)
        {
            int replayMismatch = 0;
            int orderingMismatch = 0;
            int mutationMismatch = 0;

            for (int i = 0; i < _events.Count; i++)
            {
                DivergenceEvent e = _events[i];
                if (e.Kind == DivergenceKind.RuntimeSnapshot)
                    replayMismatch++;
                else if (e.Kind == DivergenceKind.ActionOrdering)
                    orderingMismatch++;
                else if (e.Kind == DivergenceKind.MutationSequence)
                    mutationMismatch++;
            }

            GUILayout.Label($"replay mismatch: {replayMismatch}");
            GUILayout.Label($"ordering mismatch: {orderingMismatch}");
            GUILayout.Label($"mutation mismatch: {mutationMismatch}");

            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                DivergenceEvent e = _events[i];
                GUILayout.Label($"T{e.Tick} [{e.Kind}] {e.Reason} :: {e.Message}");
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Clear"))
                _events.Clear();

            GUI.DragWindow();
        }
    }
}
