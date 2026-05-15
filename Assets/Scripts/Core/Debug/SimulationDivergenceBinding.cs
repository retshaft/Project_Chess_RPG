using System;
using CheckmateRPG.Core.Replay;
using UnityEngine;

namespace CheckmateRPG.Core.DebugOverlay
{
    public static class SimulationDivergenceState
    {
        private static bool _hasMismatch;
        private static int _mismatchCount;
        private static DivergenceEvent _latestMismatch;

        public static event Action StateChanged;

        public static int MismatchCount => _mismatchCount;

        public static bool TryGetLatestMismatch(out DivergenceEvent mismatch)
        {
            mismatch = _latestMismatch;
            return _hasMismatch;
        }

        public static void ReportMismatch(DivergenceEvent mismatch)
        {
            _latestMismatch = mismatch;
            _hasMismatch = true;
            _mismatchCount++;
            StateChanged?.Invoke();
        }

        public static void Clear()
        {
            _hasMismatch = false;
            _mismatchCount = 0;
            _latestMismatch = default;
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Bridges runtime divergence events to editor-facing read-only state.
    /// This component never mutates simulation state.
    /// </summary>
    public sealed class SimulationDivergenceBinding : MonoBehaviour
    {
        [SerializeField] private bool _clearStateOnEnable = true;

        private DivergenceDetector _detector;

        private void OnEnable()
        {
            if (_clearStateOnEnable)
                SimulationDivergenceState.Clear();
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

        public void ReportVerificationResult(ReplayVerificationResult verificationResult)
        {
            if (verificationResult == null)
                return;

            if (_detector == null)
                SetDetector(new DivergenceDetector());

            _detector.Detect(verificationResult);
        }

        private static void HandleDivergence(DivergenceEvent divergence)
        {
            SimulationDivergenceState.ReportMismatch(divergence);
        }
    }
}
