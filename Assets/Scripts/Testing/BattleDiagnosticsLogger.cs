using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Testing
{
    /// <summary>
    /// Opt-in diagnostics logger for runtime damage and movement traces.
    /// </summary>
    public sealed class BattleDiagnosticsLogger : MonoBehaviour
    {
        [Header("Damage Logs")]
        [SerializeField] private bool _enableDamageDebug;

        [Header("Movement Logs")]
        [SerializeField] private bool _enableMovementDebug;
        [SerializeField] private bool _movementDebugOnlyKnight = true;

        [Header("Formatting")]
        [SerializeField] private bool _includeUnitName = true;
        [SerializeField] private float _unitCacheRefreshInterval = 0.5f;

        private readonly Dictionary<Guid, UnitBrain> _unitsById = new();
        private IEventBus _eventBus;
        private bool _isSubscribed;
        private float _nextUnitCacheRefreshTime;

        public void Configure(
            bool enableDamageDebug,
            bool enableMovementDebug,
            bool movementDebugOnlyKnight,
            bool includeUnitName)
        {
            _enableDamageDebug = enableDamageDebug;
            _enableMovementDebug = enableMovementDebug;
            _movementDebugOnlyKnight = movementDebugOnlyKnight;
            _includeUnitName = includeUnitName;
            ApplySettings();
        }

        private void OnEnable()
        {
            ApplySettings();
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (_isSubscribed && _eventBus != null)
                _eventBus.Unsubscribe<DamageAppliedEvent>(HandleDamageApplied);

            _eventBus = null;
            _isSubscribed = false;
            _unitsById.Clear();
        }

        private void Update()
        {
            if (!_isSubscribed)
                TrySubscribe();
        }

        private void OnValidate()
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            BattleDiagnostics.Configure(
                _enableDamageDebug,
                _enableMovementDebug,
                _movementDebugOnlyKnight,
                _includeUnitName);
        }

        private void TrySubscribe()
        {
            if (_isSubscribed)
                return;

            ActionRuntimeController runtime = ActionRuntimeController.Instance ?? ActionRuntimeController.EnsureExists();
            _eventBus = runtime?.EventBus;
            if (_eventBus == null)
                return;

            _eventBus.Subscribe<DamageAppliedEvent>(HandleDamageApplied);
            RefreshUnitCache();
            _isSubscribed = true;
        }

        private void HandleDamageApplied(DamageAppliedEvent gameEvent)
        {
            if (!BattleDiagnostics.EnableDamageDebug ||
                gameEvent is not IResolvableGameEvent resolvable ||
                resolvable.Phase != EventPhase.Resolve)
            {
                return;
            }

            DamageAppliedPayload payload = gameEvent.Payload;
            int tick = ActionRuntimeController.Instance?.Scheduler?.CurrentTick ?? 0;
            string sourceText = FormatUnitLabel(payload.SourceId);
            string targetText = FormatUnitLabel(payload.TargetId);
            string damageType = payload.IsTrueDamage ? "True" : payload.DamageType.ToString();

            Debug.Log(
                $"[DamageDebug] Tick={tick}, Type={damageType}, Source={sourceText}, Target={targetText}, " +
                $"Damage={payload.Damage}, RemainingHP={payload.RemainingHp}, Critical={payload.IsCritical}");
        }

        private string FormatUnitLabel(Guid unitId)
        {
            if (unitId == Guid.Empty)
                return "unknown";

            string idText = unitId.ToString("N");
            if (!_includeUnitName)
                return idText;

            if (!TryGetUnitById(unitId, out UnitBrain unit) || unit == null)
                return idText;

            return $"{idText}({unit.name})";
        }

        private bool TryGetUnitById(Guid unitId, out UnitBrain unit)
        {
            if (_unitsById.TryGetValue(unitId, out unit) && unit != null)
                return true;

            if (Time.unscaledTime < _nextUnitCacheRefreshTime)
                return false;

            RefreshUnitCache();
            _nextUnitCacheRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, _unitCacheRefreshInterval);
            return _unitsById.TryGetValue(unitId, out unit) && unit != null;
        }

        private void RefreshUnitCache()
        {
            UnitBrain[] allUnits = FindObjectsByType<UnitBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                UnitBrain unit = allUnits[i];
                if (unit == null || unit.ActorId == Guid.Empty)
                    continue;

                _unitsById[unit.ActorId] = unit;
            }
        }
    }
}
