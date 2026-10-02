using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    public static class UILayers
    {
        public const int Hud = 20, Modal = 100, Dialog = 200, Cinematic = 300, Result = 400;
        private static readonly List<RaycastResult> Hits = new();
        private static PointerEventData pointer;
        private static EventSystem eventSystem;

        public static void SetOverlay(GameObject root, int order)
        {
            var canvas = root.GetComponent<Canvas>();
            if (!canvas) canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            if (!root.GetComponent<GraphicRaycaster>()) root.AddComponent<GraphicRaycaster>();
        }

        public static bool PointerOverUI()
        {
            return Mouse.current != null && PointerOverUI(Mouse.current.position.ReadValue());
        }

        public static bool PointerOverUI(Vector2 screenPosition)
        {
            var system = EventSystem.current;
            if (!system) return false;
            if (eventSystem != system || pointer == null) { eventSystem = system; pointer = new PointerEventData(system); }
            pointer.Reset(); pointer.position = screenPosition;
            Hits.Clear(); system.RaycastAll(pointer, Hits);
            foreach (var hit in Hits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }
    }
}
