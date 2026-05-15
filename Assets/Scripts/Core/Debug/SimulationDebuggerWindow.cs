#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Replay;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Simulation;
using UnityEditor;
using UnityEngine;

namespace CheckmateRPG.Core.DebugOverlay
{
    public sealed class SimulationDebuggerWindow : EditorWindow
    {
        private readonly List<Guid> _unitIds = new();
        private readonly List<string> _unitLabels = new();
        private int _selectedUnitIndex = -1;
        private Vector2 _effectScroll;
        private GUIStyle _warningBoxStyle;

        [MenuItem("Tools/Simulation/Runtime Debug Overlay")]
        public static void Open()
        {
            GetWindow<SimulationDebuggerWindow>("Simulation Debugger");
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
            SimulationDivergenceState.StateChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            SimulationDivergenceState.StateChanged -= Repaint;
        }

        private void HandleEditorUpdate()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode에서 Simulation Runtime이 활성화되면 디버그 정보를 표시합니다.", MessageType.Info);
                return;
            }

            ActionRuntimeController controller = ActionRuntimeController.Instance;
            if (controller == null)
            {
                EditorGUILayout.HelpBox("ActionRuntimeController 인스턴스를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            IReadOnlySimulationRuntime runtime = controller.SimulationRuntime;
            if (runtime == null)
            {
                EditorGUILayout.HelpBox("SimulationRuntime 인스턴스를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            DrawTopPanel(controller, runtime);
            EditorGUILayout.Space(8f);
            DrawUnitPanel(runtime);
            EditorGUILayout.Space(8f);
            DrawDivergencePanel();
        }

        private static void DrawTopPanel(ActionRuntimeController controller, IReadOnlySimulationRuntime runtime)
        {
            EditorGUILayout.LabelField("Top Panel", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            int tick = runtime.CurrentTick;
            EditorGUILayout.LabelField("Current Tick", tick.ToString());

            IReadOnlyCollection<IActionCommand> actions = controller.Scheduler?.GetActiveActions();
            int totalActionCount = actions?.Count ?? 0;

            int queued = 0;
            int casting = 0;
            int resolving = 0;
            int recovery = 0;
            int interrupted = 0;
            int completed = 0;
            int cancelled = 0;

            if (actions != null)
            {
                foreach (IActionCommand action in actions)
                {
                    if (action == null)
                        continue;

                    switch (action.State)
                    {
                        case ActionState.Queued:
                            queued++;
                            break;
                        case ActionState.Casting:
                            casting++;
                            break;
                        case ActionState.Resolving:
                            resolving++;
                            break;
                        case ActionState.Recovery:
                            recovery++;
                            break;
                        case ActionState.Interrupted:
                            interrupted++;
                            break;
                        case ActionState.Completed:
                            completed++;
                            break;
                        case ActionState.Cancelled:
                            cancelled++;
                            break;
                    }
                }
            }

            EditorGUILayout.LabelField("ActionQueue (pending)", queued.ToString());
            EditorGUILayout.LabelField(
                "Action Summary",
                $"total={totalActionCount}, queued={queued}, casting={casting}, resolving={resolving}, recovery={recovery}, interrupted={interrupted}, completed={completed}, cancelled={cancelled}");

            EditorGUILayout.LabelField("MutationQueue (pending)", controller.LastQueuedMutationCount.ToString());
            EditorGUILayout.LabelField("Mutation Summary", controller.LastQueuedMutationSummary);
            EditorGUILayout.EndVertical();
        }

        private void DrawUnitPanel(IReadOnlySimulationRuntime runtime)
        {
            EditorGUILayout.LabelField("Center Panel (Target Unit)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            BuildUnitOptions(runtime);
            _selectedUnitIndex = EditorGUILayout.Popup("Target Unit", _selectedUnitIndex, _unitLabels.ToArray());

            if (_selectedUnitIndex < 0 || _selectedUnitIndex >= _unitIds.Count)
            {
                EditorGUILayout.HelpBox("유닛을 선택하세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            Guid selectedUnitId = _unitIds[_selectedUnitIndex];
            if (!runtime.TryGetUnit(selectedUnitId, out IReadOnlyUnitRuntimeState unit) || unit == null)
            {
                EditorGUILayout.HelpBox("선택한 유닛의 상태를 찾을 수 없습니다.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("UnitId", unit.UnitId.ToString("N"));
            EditorGUILayout.LabelField("HP", unit.HP.ToString());
            EditorGUILayout.LabelField("AP", unit.SP.ToString());

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Active Effects", EditorStyles.boldLabel);
            _effectScroll = EditorGUILayout.BeginScrollView(_effectScroll, GUILayout.Height(180f));
            int effectCount = 0;
            foreach (IReadOnlyEffectRuntimeState effect in runtime.ActiveEffects.Values)
            {
                if (effect == null || effect.TargetId != selectedUnitId)
                    continue;

                effectCount++;
                EditorGUILayout.LabelField(
                    $"- {effect.EffectId} | remaining={effect.RemainingTick} tick | stack={effect.StackCount} | lifecycle={effect.Lifecycle}");
            }

            if (effectCount == 0)
                EditorGUILayout.LabelField("- 없음");

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDivergencePanel()
        {
            EditorGUILayout.LabelField("Bottom Panel (DivergenceDetector)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            if (!SimulationDivergenceState.TryGetLatestMismatch(out DivergenceEvent mismatch))
            {
                EditorGUILayout.HelpBox("현재 감지된 Replay Mismatch가 없습니다.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _warningBoxStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                wordWrap = true
            };

            EditorGUILayout.LabelField(
                $"<color=#ff3b30><b>Mismatch Detected @ Tick {mismatch.Tick}</b></color>",
                _warningBoxStyle);
            EditorGUILayout.LabelField($"Reason: {mismatch.Reason}", _warningBoxStyle);
            EditorGUILayout.LabelField($"Kind: {mismatch.Kind}", _warningBoxStyle);
            EditorGUILayout.LabelField($"Message: {mismatch.Message}", _warningBoxStyle);
            EditorGUILayout.LabelField($"Detected Count: {SimulationDivergenceState.MismatchCount}", _warningBoxStyle);
            EditorGUILayout.EndVertical();
        }

        private void BuildUnitOptions(IReadOnlySimulationRuntime runtime)
        {
            _unitIds.Clear();
            _unitLabels.Clear();

            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> pair in runtime.RuntimeStates)
            {
                if (pair.Value == null)
                    continue;

                _unitIds.Add(pair.Key);
                _unitLabels.Add($"{pair.Key:N} (HP:{pair.Value.HP}, AP:{pair.Value.SP})");
            }

            if (_unitLabels.Count == 0)
            {
                _selectedUnitIndex = -1;
                _unitLabels.Add("(unit 없음)");
                return;
            }

            if (_selectedUnitIndex < 0 || _selectedUnitIndex >= _unitIds.Count)
                _selectedUnitIndex = 0;
        }
    }
}
#endif
