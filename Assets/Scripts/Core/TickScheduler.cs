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

            Initialise();
        }

        /// <summary>
        /// Awake 본체. 편집 모드(EditMode 테스트)에서는 AddComponent가 Awake를 호출하지 않으므로
        /// 테스트가 동일한 초기화 경로를 명시적으로 실행할 수 있도록 분리했다.
        /// _tickDurationSeconds가 0인 상태로 Advance를 호출하면 while 루프가 끝나지 않으므로 반드시 먼저 초기화한다.
        /// </summary>
        internal void Initialise()
        {
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
