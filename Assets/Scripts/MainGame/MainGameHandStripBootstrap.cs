using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.UI.Containers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// <c>MainGame.unity</c> has <see cref="MainGameHUD"/> but no Acts <see cref="RuntimeUI"/> / hand strip.
    /// Build the card dock at runtime so Survey Team + Research are visible and clickable.
    /// </summary>
    public static class MainGameHandStripBootstrap
    {
        const int SlotCount = 8;

        public static void EnsureExists()
        {
            if (!Application.isPlaying || !MainGameController.IsMainGamePlayScene()) return;
            if (Object.FindAnyObjectByType<BottomBarActionsUI>() != null) return;

            EnsureEventSystem();

            Canvas canvas = Object.FindAnyObjectByType<MainGameHUD>()?.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                var canvasGo = new GameObject("MainGameHand_Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 480;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var dockGo = new GameObject("MainGameHandDock", typeof(RectTransform));
            dockGo.transform.SetParent(canvas.transform, false);
            var dockRt = dockGo.GetComponent<RectTransform>();
            dockRt.anchorMin = new Vector2(0f, 0f);
            dockRt.anchorMax = new Vector2(0f, 0f);
            dockRt.pivot = new Vector2(0f, 0f);
            dockRt.anchoredPosition = new Vector2(8f, MainGameUiLayout.CardBottomMargin);
            dockRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 920f);
            dockRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, MainGameUiLayout.CardHeight + 24f);

            var barGo = new GameObject("MainGameHandBar", typeof(RectTransform));
            barGo.transform.SetParent(dockGo.transform, false);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = Vector2.zero;
            barRt.anchorMax = Vector2.one;
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;

            for (int i = 0; i < SlotCount; i++)
                CreateCardSlot(barGo.transform, i);

            var bar = barGo.AddComponent<BottomBarActionsUI>();
            bar.ApplyMainGameCompactLayout();
            Debug.Log("[MainGameHandStripBootstrap] Created runtime card hand for MainGame scene.");
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static void CreateCardSlot(Transform parent, int index)
        {
            var btnGo = new GameObject($"CardSlot{index}", typeof(RectTransform));
            btnGo.transform.SetParent(parent, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(MainGameUiLayout.CardWidth, MainGameUiLayout.CardHeight);

            var plate = btnGo.AddComponent<Image>();
            plate.color = new Color(0.12f, 0.16f, 0.22f, 0.92f);
            plate.raycastTarget = true;

            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = plate;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(btnGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.08f, 0.28f);
            iconRt.anchorMax = new Vector2(0.92f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            btnGo.AddComponent<UIActionButton>();
        }
    }
}