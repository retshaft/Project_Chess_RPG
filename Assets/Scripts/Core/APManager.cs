// APManager.cs
// Global action-point (AP) resource controller for battle actions.

using System;
using UnityEngine;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// Manages a global AP pool that regenerates over time and powers all actions.
    /// </summary>
    public class APManager : MonoBehaviour
    {
        // ─── Singleton ───────────────────────────────────────────────────────────

        public static APManager Instance { get; private set; }

        // ─── Configuration ───────────────────────────────────────────────────────

        [Header("AP Settings")]
        [Tooltip("Maximum action points available.")]
        [SerializeField] private float _maxAP = 20f;

        [Tooltip("AP regenerated per second.")]
        [SerializeField] private float _regenPerSecond = 4f;

        [Header("Debug UI")]
        [SerializeField] private bool _createDebugUI = true;

        // ─── State ───────────────────────────────────────────────────────────────

        public float CurrentAP { get; private set; }
        public float MaxAP => _maxAP;
        public bool RegenEnabled => !_regenPaused;
        public APRegenPauseReason RegenPauseReason => _regenPauseReason;

        public event Action<float, float, float, APChangeReason> OnAPChanged;
        public event Action<float, float, float, APActionReason> OnInsufficientAP;

        private bool _regenPaused;
        private APRegenPauseReason _regenPauseReason = APRegenPauseReason.None;

        // ─── Lifecycle ───────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[APManager] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _maxAP = Mathf.Max(0f, _maxAP);
            CurrentAP = _maxAP;

            if (_createDebugUI)
                EnsureDebugUI();

            RaiseAPChanged(0f, APChangeReason.Initialization);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            Regenerate(Time.deltaTime);
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        public bool TrySpend(float amount)
        {
            return TrySpend(new ActionPointCost(amount), out _);
        }

        public bool TrySpend(float amount, APActionReason reason, out float spent)
        {
            return TrySpend(new ActionPointCost(amount, reason), out spent);
        }

        public bool TrySpend(ActionPointCost cost, out float spent)
        {
            spent = 0f;

            if (cost.Amount <= 0f)
                return true;

            if (CurrentAP >= cost.Amount)
            {
                CurrentAP -= cost.Amount;
                spent = cost.Amount;
                RaiseAPChanged(-cost.Amount, APChangeReason.Spend);
                return true;
            }

            float missing = cost.Amount - CurrentAP;
            bool hasListeners = OnInsufficientAP != null;
            OnInsufficientAP?.Invoke(cost.Amount, CurrentAP, missing, cost.Reason);
            if (!hasListeners)
            {
                Debug.LogWarning($"[APManager] AP 부족 ({cost.Reason}) 요청 {cost.Amount:0.0}, " +
                                 $"보유 {CurrentAP:0.0}, 부족 {missing:0.0}");
            }
            return false;
        }

        public float AddAP(float amount, APSource source)
        {
            if (amount <= 0f || _maxAP <= 0f)
                return 0f;

            float previous = CurrentAP;
            float next = Mathf.Min(_maxAP, CurrentAP + amount);
            CurrentAP = next;
            float added = CurrentAP - previous;
            if (!Mathf.Approximately(added, 0f))
                RaiseAPChanged(added, ToChangeReason(source));
            return added;
        }

        public void SetMaxAP(float newMax, bool clampCurrent = true)
        {
            newMax = Mathf.Max(0f, newMax);
            bool maxChanged = !Mathf.Approximately(newMax, _maxAP);
            _maxAP = newMax;

            if (clampCurrent && CurrentAP > _maxAP)
            {
                float previous = CurrentAP;
                CurrentAP = _maxAP;
                RaiseAPChanged(CurrentAP - previous, APChangeReason.MaxChanged);
                return;
            }

            if (maxChanged)
                RaiseAPChanged(0f, APChangeReason.MaxChanged);
        }

        public void SetRegenPaused(bool paused, APRegenPauseReason reason)
        {
            _regenPaused = paused;
            _regenPauseReason = paused ? reason : APRegenPauseReason.None;
        }

        // ─── Internal Logic ───────────────────────────────────────────────────────

        private void Regenerate(float deltaTime)
        {
            if (_regenPaused || _regenPerSecond <= 0f || _maxAP <= 0f || CurrentAP >= _maxAP || deltaTime <= 0f)
                return;

            AddAP(_regenPerSecond * deltaTime, APSource.Regen);
        }

        private void RaiseAPChanged(float delta, APChangeReason reason)
        {
            OnAPChanged?.Invoke(CurrentAP, _maxAP, delta, reason);
        }

        private void EnsureDebugUI()
        {
            if (!TryGetComponent(out APDebugUI _))
                gameObject.AddComponent<APDebugUI>();

            if (!TryGetComponent(out APFailFeedback _))
                gameObject.AddComponent<APFailFeedback>();
        }

        private static APChangeReason ToChangeReason(APSource source)
        {
            return source switch
            {
                APSource.Regen => APChangeReason.Regen,
                APSource.Refund => APChangeReason.Refund,
                APSource.Bonus => APChangeReason.Bonus,
                _ => APChangeReason.Manual
            };
        }
    }
}
