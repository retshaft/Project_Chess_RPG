using System;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public sealed class TickScheduler : MonoBehaviour
    {
        [SerializeField] private float _tickDurationSeconds;

        public static TickScheduler Instance { get; private set; }
        public static float DefaultTickDurationSeconds => ActionTimelineFormula.TickMilliseconds / 1000f;

        public int CurrentTick { get; private set; }
        public bool IsPaused { get; set; }
        public event Action<int> OnTick;

        private float _accumulator;

        public static TickScheduler EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("TickScheduler");
            return go.AddComponent<TickScheduler>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (_tickDurationSeconds <= 0f)
                _tickDurationSeconds = DefaultTickDurationSeconds;
            _tickDurationSeconds = Mathf.Max(0.01f, _tickDurationSeconds);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (IsPaused) return;
            Advance(Time.deltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _accumulator += deltaTime;
            while (_accumulator >= _tickDurationSeconds)
            {
                _accumulator -= _tickDurationSeconds;
                CurrentTick++;
                OnTick?.Invoke(CurrentTick);
            }
        }
    }
}
