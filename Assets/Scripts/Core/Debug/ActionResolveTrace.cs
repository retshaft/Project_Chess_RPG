using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Replay;
using UnityEngine;

namespace CheckmateRPG.Core.DebugOverlay
{
    public sealed class ActionResolveTrace : MonoBehaviour
    {
        private const int WindowId = 9101;

        [SerializeField] private KeyCode _toggleKey = KeyCode.F10;
        [SerializeField] private bool _visibleOnStart;
        [SerializeField] private Rect _windowRect = new Rect(472f, 16f, 520f, 380f);
        [SerializeField] private int _maxRows = 40;

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

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Action Resolve Trace");
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
            ActionJournal actionJournal = controller.ActionJournal;
            MutationJournal mutationJournal = controller.MutationJournal;

            GUILayout.Label($"Tick: {tick}");
            GUILayout.Label("Resolve order / Arbitration result / Interrupt result / Commit order");
            _scroll = GUILayout.BeginScrollView(_scroll);

            int rows = 0;
            if (actionJournal != null)
            {
                IReadOnlyList<ActionJournalEntry> actionEntries = actionJournal.GetEntriesForTick(tick);
                for (int i = 0; i < actionEntries.Count && rows < _maxRows; i++)
                {
                    ActionJournalEntry e = actionEntries[i];
                    if (e.EntryType == ActionJournalEntryType.ResolveOrder)
                    {
                        GUILayout.Label($"[ResolveOrder#{e.ResolveOrder}] {e.ActionId:N} {e.Details}");
                        rows++;
                        continue;
                    }

                    if (e.EntryType == ActionJournalEntryType.Reservation)
                    {
                        GUILayout.Label($"[Arbitration] {e.ActionId:N} result={e.ReservationResult} {e.Details}");
                        rows++;
                        continue;
                    }

                    if (e.EntryType == ActionJournalEntryType.ActionResult)
                    {
                        GUILayout.Label($"[Interrupt/Result] {e.ActionId:N} {e.Details}");
                        rows++;
                    }
                }
            }

            if (mutationJournal != null && rows < _maxRows)
            {
                IReadOnlyList<MutationJournalEntry> mutationEntries = mutationJournal.GetEntriesForTick(tick);
                for (int i = 0; i < mutationEntries.Count && rows < _maxRows; i++)
                {
                    MutationJournalEntry e = mutationEntries[i];
                    GUILayout.Label($"[Commit#{e.CommitOrder}] {e.MutationType} target={e.TargetId:N} result={e.Result}");
                    rows++;
                }
            }

            if (rows == 0)
                GUILayout.Label("No trace records for this tick.");

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}
