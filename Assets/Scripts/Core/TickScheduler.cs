using System;
using UnityEngine;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// Global tick driver for all time-based runtime systems.
    /// </summary>
    public sealed class TickScheduler : MonoBehaviour
    {
        public const float TickDurationSeconds = ActionTimelineFormula.TickMilliseconds / 1000f;

        public static TickScheduler Instance { get; private set; }

        public int CurrentTick { get; private set; }

        public event Action<int> OnTick;

        private float _accumulator;

        public static TickScheduler EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("TickScheduler");
            return go.AddComponent<TickScheduler>();
        }

        public static int SecondsToTicks(float seconds)
        {
            if (seconds <= 0f)
                return 0;
            return Mathf.CeilToInt(seconds / TickDurationSeconds);
        }

        public static float TicksToSeconds(int ticks)
        {
            return Mathf.Max(0, ticks) * TickDurationSeconds;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CurrentTick = 0;
            _accumulator = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void FixedUpdate()
        {
            Advance(Time.fixedDeltaTime);
        }

        private void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _accumulator += deltaTime;
            while (_accumulator >= TickDurationSeconds)
            {
                _accumulator -= TickDurationSeconds;
                CurrentTick++;
                OnTick?.Invoke(CurrentTick);
            }
        }
    }
}
