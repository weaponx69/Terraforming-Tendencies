using System.Collections.Generic;
using GameDevTV.RTS.MainGame;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.UI.Containers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    public static class UiPlaythroughUtil
    {
        static readonly List<RaycastResult> PointerHits = new(24);

        /// <summary>
        /// True when the pointer is over UI that should consume clicks (not decorative HUD chrome).
        /// </summary>
        public static bool PointerBlocksWorldInteraction()
        {
            if (EventSystem.current == null) return false;

            if (MainGameController.IsActive && MainGameHUD.Instance != null
                && MainGameHUD.Instance.BriefingPointerBlocksWorldAtPointer())
                return true;

            if (!EventSystem.current.IsPointerOverGameObject()) return false;

            Vector2 pos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;

            PointerHits.Clear();
            var ped = new PointerEventData(EventSystem.current) { position = pos };
            EventSystem.current.RaycastAll(ped, PointerHits);

            if (BottomBarActionsUI.IsPointerOverHandStrip)
                return true;

            for (int i = 0; i < PointerHits.Count; i++)
            {
                var go = PointerHits[i].gameObject;
                if (go == null) continue;
                if (go.GetComponentInParent<Button>() != null) return true;
                if (go.GetComponentInParent<UIActionButton>() != null) return true;
                if (go.GetComponentInParent<UnityEngine.UI.InputField>() != null) return true;
                if (go.GetComponentInParent<Scrollbar>() != null) return true;
                if (go.name.IndexOf("Card", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (go.name.IndexOf("Minimap", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (go.name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }
        public static bool TryScreenCenter(RectTransform rt, out Vector2 screenBottomLeft)
        {
            screenBottomLeft = default;
            if (rt == null) return false;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            screenBottomLeft = (corners[0] + corners[2]) * 0.5f;
            return true;
        }

        public static bool TryScreenCenter(Button button, out Vector2 screenBottomLeft)
        {
            screenBottomLeft = default;
            if (button == null) return false;
            return TryScreenCenter(button.GetComponent<RectTransform>(), out screenBottomLeft);
        }

        /// <summary>
        /// Same UI hit-test path as a player click (Play Mode / connected Editor bot).
        /// Screen position uses Unity's bottom-left origin in the Game view.
        /// </summary>
        public static bool TryPointerClick(Vector2 screenBottomLeft)
        {
            if (EventSystem.current == null) return false;
            var ped = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(screenBottomLeft.x, screenBottomLeft.y),
                button = PointerEventData.InputButton.Left
            };
            var results = new List<RaycastResult>(16);
            EventSystem.current.RaycastAll(ped, results);
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;
                if (ExecuteEvents.Execute(go, ped, ExecuteEvents.pointerClickHandler))
                    return true;
                var button = go.GetComponentInParent<Button>();
                if (button != null && ExecuteEvents.Execute(button.gameObject, ped, ExecuteEvents.pointerClickHandler))
                    return true;
            }
            return false;
        }
    }
}