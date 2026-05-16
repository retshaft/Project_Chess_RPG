using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Events.EffectEvents;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Testing
{
    /// <summary>
    /// Read-only temporary adapter for graybox VFX/UI validation.
    /// Subscribes to runtime events and mirrors them as immediate visuals.
    /// </summary>
    public sealed class DummyVFXAdapter : MonoBehaviour
    {
        [Header("Floating Text")]
        [SerializeField] private float _floatingDuration = 1f;
        [SerializeField] private float _floatingDistance = 1f;
        [SerializeField] private float _textYOffset = 1.5f;
        [SerializeField] private int _fontSize = 48;
        [SerializeField] private float _unitCacheRefreshInterval = 0.5f;

        private static readonly Dictionary<string, Type> EventTypeCache = new(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingEventTypeCache = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, UnitBrain> _unitsById = new();
        private readonly List<DynamicSubscription> _dynamicSubscriptions = new();

        private IEventBus _eventBus;
        private bool _isSubscribed;
        private float _nextUnitCacheRefreshTime;

        private readonly struct DynamicSubscription
        {
            public DynamicSubscription(Type eventType, Action<IGameEvent> handler)
            {
                EventType = eventType;
                Handler = handler;
            }

            public Type EventType { get; }
            public Action<IGameEvent> Handler { get; }
        }

        private void OnEnable()
        {
            ActionRuntimeController runtime = ActionRuntimeController.Instance ?? ActionRuntimeController.EnsureExists();
            _eventBus = runtime?.EventBus;
            if (_eventBus == null)
                return;

            _eventBus.Subscribe<DamageAppliedEvent>(HandleDamageApplied);
            _eventBus.Subscribe<MoveCompletedEvent>(HandleMoveCompleted);
            _eventBus.Subscribe<EffectTickEvent>(HandleEffectTick);
            RefreshUnitCache();

            // Compatibility subscriptions for requested temporary event names.
            SubscribeByName("DamageTakenEvent", HandleLegacyDamageTaken);
            SubscribeByName("HealedEvent", HandleLegacyHealed);
            SubscribeByName("UnitMovedEvent", HandleLegacyUnitMoved);

            _isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_isSubscribed || _eventBus == null)
                return;

            _eventBus.Unsubscribe<DamageAppliedEvent>(HandleDamageApplied);
            _eventBus.Unsubscribe<MoveCompletedEvent>(HandleMoveCompleted);
            _eventBus.Unsubscribe<EffectTickEvent>(HandleEffectTick);

            for (int i = 0; i < _dynamicSubscriptions.Count; i++)
                _eventBus.Unsubscribe(_dynamicSubscriptions[i].EventType, _dynamicSubscriptions[i].Handler);

            _dynamicSubscriptions.Clear();
            _unitsById.Clear();
            _isSubscribed = false;
        }

        private void HandleDamageApplied(DamageAppliedEvent gameEvent)
        {
            if (!IsResolvePhase(gameEvent))
                return;

            int amount = Mathf.Abs(gameEvent.Payload.Damage);
            if (amount <= 0)
                return;

            ShowFloatingTextForTarget(gameEvent.Payload.TargetId, $"-{amount}", Color.red);
        }

        private void HandleEffectTick(EffectTickEvent gameEvent)
        {
            if (!IsResolvePhase(gameEvent))
                return;

            int deltaHp = gameEvent.Payload.DeltaHp;
            if (deltaHp > 0)
                ShowFloatingTextForTarget(gameEvent.Payload.TargetId, $"+{deltaHp}", Color.green);
            else if (deltaHp < 0)
                ShowFloatingTextForTarget(gameEvent.Payload.TargetId, $"-{Mathf.Abs(deltaHp)}", Color.red);
        }

        private void HandleMoveCompleted(MoveCompletedEvent gameEvent)
        {
            if (!IsResolvePhase(gameEvent))
                return;

            TeleportRendererToCell(gameEvent.Payload.UnitId, gameEvent.Payload.To);
        }

        private void HandleLegacyDamageTaken(IGameEvent gameEvent)
        {
            if (!TryResolveLegacyDamage(gameEvent, out Guid targetId, out int amount))
                return;

            ShowFloatingTextForTarget(targetId, $"-{amount}", Color.red);
        }

        private void HandleLegacyHealed(IGameEvent gameEvent)
        {
            if (!TryResolveLegacyHeal(gameEvent, out Guid targetId, out int amount))
                return;

            ShowFloatingTextForTarget(targetId, $"+{amount}", Color.green);
        }

        private void HandleLegacyUnitMoved(IGameEvent gameEvent)
        {
            if (!TryResolveLegacyMove(gameEvent, out Guid unitId, out Vector2Int destination))
                return;

            TeleportRendererToCell(unitId, destination);
        }

        private void ShowFloatingTextForTarget(Guid targetId, string message, Color color)
        {
            if (targetId == Guid.Empty || string.IsNullOrWhiteSpace(message))
                return;
            if (!TryGetFloatingAnchor(targetId, out Vector3 anchor))
                return;

            StartCoroutine(PlayFloatingText(anchor, message, color));
        }

        private void TeleportRendererToCell(Guid unitId, Vector2Int destinationCell)
        {
            if (unitId == Guid.Empty)
                return;
            if (GridSystem.Instance == null || !GridSystem.Instance.IsValidCell(destinationCell))
                return;

            if (!TryResolveVisualTransform(unitId, out Transform visualTransform, out _))
                return;

            Vector3 targetWorld = GridSystem.Instance.GridToWorld(destinationCell);
            targetWorld.y = visualTransform.position.y;
            visualTransform.position = targetWorld;
        }

        private IEnumerator PlayFloatingText(Vector3 worldAnchor, string message, Color color)
        {
            var textRoot = new GameObject("DummyFloatingText");
            textRoot.transform.position = worldAnchor;

            TextMesh textMesh = textRoot.AddComponent<TextMesh>();
            textMesh.text = message;
            textMesh.color = color;
            textMesh.fontSize = _fontSize;
            textMesh.characterSize = 0.05f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            float elapsed = 0f;
            Vector3 start = worldAnchor;
            Vector3 end = worldAnchor + Vector3.up * Mathf.Max(0f, _floatingDistance);
            float duration = Mathf.Max(0.01f, _floatingDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                textRoot.transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            Destroy(textRoot);
        }

        private void SubscribeByName(string simpleTypeName, Action<IGameEvent> handler)
        {
            if (string.IsNullOrWhiteSpace(simpleTypeName) || handler == null || _eventBus == null)
                return;

            Type eventType = ResolveEventType(simpleTypeName);
            if (eventType == null)
                return;

            _eventBus.Subscribe(eventType, handler);
            _dynamicSubscriptions.Add(new DynamicSubscription(eventType, handler));
        }

        private static Type ResolveEventType(string simpleTypeName)
        {
            if (EventTypeCache.TryGetValue(simpleTypeName, out Type cached))
                return cached;
            if (MissingEventTypeCache.Contains(simpleTypeName))
                return null;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string assemblyName = assemblies[i].GetName().Name;
                if (string.IsNullOrWhiteSpace(assemblyName) || !assemblyName.Contains("Assembly-CSharp", StringComparison.Ordinal))
                    continue;

                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                    continue;

                for (int j = 0; j < types.Length; j++)
                {
                    Type candidate = types[j];
                    if (candidate == null ||
                        !typeof(IGameEvent).IsAssignableFrom(candidate) ||
                        string.IsNullOrWhiteSpace(candidate.FullName) ||
                        !candidate.FullName.StartsWith("CheckmateRPG.", StringComparison.Ordinal) ||
                        !string.Equals(candidate.Name, simpleTypeName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    EventTypeCache[simpleTypeName] = candidate;
                    return candidate;
                }
            }

            MissingEventTypeCache.Add(simpleTypeName);
            return null;
        }

        private bool TryGetFloatingAnchor(Guid unitId, out Vector3 anchor)
        {
            if (TryResolveVisualTransform(unitId, out Transform visualTransform, out Renderer renderer))
            {
                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;
                    anchor = new Vector3(bounds.center.x, bounds.max.y + _textYOffset, bounds.center.z);
                    return true;
                }

                anchor = visualTransform.position + Vector3.up * _textYOffset;
                return true;
            }

            anchor = Vector3.zero;
            return false;
        }

        private bool TryResolveVisualTransform(Guid unitId, out Transform visualTransform, out Renderer renderer)
        {
            visualTransform = null;
            renderer = null;

            if (!TryGetUnitById(unitId, out UnitBrain unit) || unit == null)
                return false;

            renderer = unit.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                visualTransform = renderer.transform;
                return true;
            }

            visualTransform = unit.transform;
            return true;
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
                UnitBrain candidate = allUnits[i];
                if (candidate == null || candidate.ActorId == Guid.Empty)
                    continue;

                _unitsById[candidate.ActorId] = candidate;
            }
        }

        private static bool IsResolvePhase(IGameEvent gameEvent)
        {
            return gameEvent is IResolvableGameEvent resolvable &&
                   resolvable.Phase == EventPhase.Resolve;
        }

        private static bool TryResolveLegacyDamage(IGameEvent gameEvent, out Guid targetId, out int amount)
        {
            targetId = Guid.Empty;
            amount = 0;

            if (!IsResolvePhase(gameEvent) || !TryGetPayload(gameEvent, out object payload))
                return false;
            if (!TryReadGuid(payload, "TargetId", out targetId))
                return false;

            if (!TryReadInt(payload, "Amount", out amount))
                TryReadInt(payload, "Damage", out amount);

            amount = Mathf.Abs(amount);
            return amount > 0;
        }

        private static bool TryResolveLegacyHeal(IGameEvent gameEvent, out Guid targetId, out int amount)
        {
            targetId = Guid.Empty;
            amount = 0;

            if (!IsResolvePhase(gameEvent) || !TryGetPayload(gameEvent, out object payload))
                return false;
            if (!TryReadGuid(payload, "TargetId", out targetId))
                return false;

            if (!TryReadInt(payload, "Amount", out amount))
                TryReadInt(payload, "Heal", out amount);

            amount = Mathf.Abs(amount);
            return amount > 0;
        }

        private static bool TryResolveLegacyMove(IGameEvent gameEvent, out Guid unitId, out Vector2Int destination)
        {
            unitId = Guid.Empty;
            destination = default;

            if (!IsResolvePhase(gameEvent) || !TryGetPayload(gameEvent, out object payload))
                return false;

            if (!TryReadGuid(payload, "UnitId", out unitId))
                return false;

            return TryReadVector2Int(payload, "To", out destination) ||
                   TryReadVector2Int(payload, "Destination", out destination);
        }

        private static bool TryGetPayload(object gameEvent, out object payload)
        {
            payload = null;
            if (gameEvent == null)
                return false;

            PropertyInfo property = gameEvent.GetType().GetProperty("Payload", BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                return false;

            payload = property.GetValue(gameEvent);
            return payload != null;
        }

        private static bool TryReadGuid(object instance, string propertyName, out Guid value)
        {
            value = Guid.Empty;
            if (!TryGetPropertyValue(instance, propertyName, out object raw))
                return false;

            if (raw is Guid guid)
            {
                value = guid;
                return value != Guid.Empty;
            }

            if (raw is string text && Guid.TryParse(text, out Guid parsed))
            {
                value = parsed;
                return value != Guid.Empty;
            }

            return false;
        }

        private static bool TryReadInt(object instance, string propertyName, out int value)
        {
            value = 0;
            if (!TryGetPropertyValue(instance, propertyName, out object raw) || raw == null)
                return false;

            switch (raw)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case float floatValue:
                    value = Mathf.RoundToInt(floatValue);
                    return true;
                case long longValue:
                    value = (int)Mathf.Clamp(longValue, int.MinValue, int.MaxValue);
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case string text when int.TryParse(text, out int parsed):
                    value = parsed;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadVector2Int(object instance, string propertyName, out Vector2Int value)
        {
            value = default;
            if (!TryGetPropertyValue(instance, propertyName, out object raw) || raw == null)
                return false;

            if (raw is Vector2Int v2)
            {
                value = v2;
                return true;
            }

            Type type = raw.GetType();
            PropertyInfo xProp = type.GetProperty("x", BindingFlags.Public | BindingFlags.Instance) ??
                                 type.GetProperty("X", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo yProp = type.GetProperty("y", BindingFlags.Public | BindingFlags.Instance) ??
                                 type.GetProperty("Y", BindingFlags.Public | BindingFlags.Instance);

            if (xProp == null || yProp == null)
                return false;

            if (!TryConvertToInt(xProp.GetValue(raw), out int x) || !TryConvertToInt(yProp.GetValue(raw), out int y))
                return false;

            value = new Vector2Int(x, y);
            return true;
        }

        private static bool TryGetPropertyValue(object instance, string propertyName, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                return false;

            value = property.GetValue(instance);
            return true;
        }

        private static bool TryConvertToInt(object value, out int converted)
        {
            converted = 0;
            if (value == null)
                return false;

            switch (value)
            {
                case int intValue:
                    converted = intValue;
                    return true;
                case float floatValue:
                    converted = Mathf.RoundToInt(floatValue);
                    return true;
                case long longValue:
                    converted = (int)Mathf.Clamp(longValue, int.MinValue, int.MaxValue);
                    return true;
                case short shortValue:
                    converted = shortValue;
                    return true;
                case string text when int.TryParse(text, out int parsed):
                    converted = parsed;
                    return true;
                default:
                    return false;
            }
        }
    }
}
