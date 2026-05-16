using UnityEngine;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Units;

namespace CheckmateRPG.Testing
{
    public class APDebugLogger : MonoBehaviour
    {
        [SerializeField] private bool _logAPChanges = true;
        [SerializeField] private bool _logInsufficientAP = true;

        private APManager _apManager;
        public static UnitBrain CurrentTurnUnit { get; private set; }
        public static float LastAPDelta { get; private set; }
        public static APChangeReason LastAPChangeReason { get; private set; } = APChangeReason.Initialization;
        public static float LastCurrentAP { get; private set; }
        public static float LastMaxAP { get; private set; }
        public static int LastAPChangeFrame { get; private set; } = -1;
        public static string LastQueuedCommandSummary { get; private set; } = "None";

        public static event System.Action SnapshotChanged;

        private void Awake()
        {
            _apManager = APManager.Instance ?? GetComponent<APManager>();
            if (_apManager != null)
            {
                LastCurrentAP = _apManager.CurrentAP;
                LastMaxAP = _apManager.MaxAP;
            }
        }

        private void OnEnable()
        {
            if (_apManager == null)
                return;

            _apManager.OnAPChanged += HandleAPChanged;
            _apManager.OnInsufficientAP += HandleInsufficientAP;
        }

        private void OnDisable()
        {
            if (_apManager == null)
                return;

            _apManager.OnAPChanged -= HandleAPChanged;
            _apManager.OnInsufficientAP -= HandleInsufficientAP;
        }

        private void HandleAPChanged(float current, float max, float delta, APChangeReason reason)
        {
            LastCurrentAP = current;
            LastMaxAP = max;
            LastAPDelta = delta;
            LastAPChangeReason = reason;
            LastAPChangeFrame = Time.frameCount;
            SnapshotChanged?.Invoke();

            if (!_logAPChanges)
                return;

            Debug.Log($"[APDebugLogger] AP {current:0.0}/{max:0.0} (Δ {delta:0.0}, {reason})");
        }

        private void HandleInsufficientAP(float requested, float current, float missing, APActionReason reason)
        {
            if (!_logInsufficientAP)
                return;

            Debug.Log($"[APDebugLogger] AP 부족: 요청 {requested:0.0}, 보유 {current:0.0}, 부족 {missing:0.0} ({reason})");
        }

        public static void SetCurrentTurnUnit(UnitBrain unit)
        {
            if (CurrentTurnUnit == unit)
                return;

            CurrentTurnUnit = unit;
            SnapshotChanged?.Invoke();
        }

        public static void RecordQueuedCommand(AbilityActionCommand command)
        {
            if (command == null)
                return;

            LastQueuedCommandSummary =
                $"Ability={command.AbilityId}, Actor={command.ActorId:N}, Targets={command.TargetIds.Count}, Cells={command.TargetCells.Count}";
            SnapshotChanged?.Invoke();
        }
    }
}
