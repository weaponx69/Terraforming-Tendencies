using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Persistent bottom-center action bar that mirrors the same commands as the
    /// original ActionsUI panel. Always visible regardless of selection state.
    /// Self-assembles its own panel and button prefabs at runtime — no scene setup required.
    /// Subscribes to selection events independently so it syncs automatically.
    /// </summary>
    public class BottomBarActionsUI : ActionPanelBase
    {
        [Header("Self-Assembly")]
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private int buttonCount = 8;
        [SerializeField] private Sprite buttonSprite;

        private GameObject panelRoot;
        private bool isBuilt = false;
        private Owner owner = Owner.Player1;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleUnitDeath;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleUnitDeath;
        }

        private void Start()
        {
            BuildPanel();
        }

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (evt.Unit is AbstractCommandable cmd)
            {
                var set = new HashSet<AbstractCommandable> { cmd };
                SyncSelection(set);
            }
        }

        private void HandleUnitDeselected(UnitDeselectedEvent evt)
        {
            // Re-sync with whatever is still selected
            var stillSelected = new HashSet<AbstractCommandable>();
            var allUnits = FindObjectsByType<AbstractCommandable>(FindObjectsSortMode.None);
            foreach (var u in allUnits)
            {
                if (u != null && u.IsSelected && u.Owner == owner)
                    stillSelected.Add(u);
            }
            SyncSelection(stillSelected);
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            HandleUnitDeselected(new UnitDeselectedEvent(evt.Unit));
        }

        private void BuildPanel()
        {
            if (isBuilt) return;

            // Find the RuntimeUI canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[BottomBarActionsUI] No Canvas found in scene. Cannot build bottom bar.");
                return;
            }

            // Create the bottom bar panel
            panelRoot = new GameObject("Bottom Action Bar");
            panelRoot.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.02f);
            panelRect.anchorMax = new Vector2(0.8f, 0.12f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;

            // Add background image
            Image bg = panelRoot.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.15f, 0.85f);
            if (buttonSprite != null)
            {
                bg.sprite = buttonSprite;
                bg.type = Image.Type.Sliced;
            }

            // Add horizontal layout
            HorizontalLayoutGroup hlg = panelRoot.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(8, 8, 6, 6);

            // Add content size fitter
            ContentSizeFitter csf = panelRoot.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create button slots
            var buttons = new UIActionButton[buttonCount];

            for (int i = 0; i < buttonCount; i++)
            {
                GameObject btnGo;

                if (buttonPrefab != null)
                {
                    btnGo = Instantiate(buttonPrefab, panelRoot.transform);
                    btnGo.name = $"Action Slot {i}";
                }
                else
                {
                    btnGo = CreateDefaultButton(panelRoot.transform, i);
                }

                // Size the button
                RectTransform btnRect = btnGo.GetComponent<RectTransform>();
                if (btnRect != null)
                    btnRect.sizeDelta = new Vector2(56, 56);

                // Get UIActionButton
                UIActionButton actionBtn = btnGo.GetComponent<UIActionButton>();
                if (actionBtn == null)
                    actionBtn = btnGo.AddComponent<UIActionButton>();

                buttons[i] = actionBtn;
            }

            actionButtons = buttons;
            isBuilt = true;
            panelRoot.SetActive(true);

            Debug.Log($"[BottomBarActionsUI] Built bottom bar with {buttonCount} action slots.");
        }

        private GameObject CreateDefaultButton(Transform parent, int index)
        {
            GameObject btnGo = new GameObject($"Action Slot {index}");
            btnGo.transform.SetParent(parent, false);
            btnGo.layer = parent.gameObject.layer;

            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(56, 56);

            Image img = btnGo.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.3f, 1f);
            if (buttonSprite != null)
            {
                img.sprite = buttonSprite;
                img.type = Image.Type.Sliced;
            }

            Button btn = btnGo.AddComponent<Button>();
            btn.colors = new ColorBlock
            {
                normalColor = new Color(0f, 0.674f, 1f, 1f),
                highlightedColor = new Color(0.275f, 0.758f, 0.99f, 1f),
                pressedColor = new Color(0f, 0.494f, 0.735f, 1f),
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.125f
            };
            btn.targetGraphic = img;

            // Icon child
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(btnGo.transform, false);
            iconGo.layer = btnGo.layer;
            RectTransform iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(10, 10);
            iconRt.offsetMax = new Vector2(-10, -10);
            iconGo.AddComponent<Image>();

            btnGo.AddComponent<UIActionButton>();

            return btnGo;
        }

        /// <summary>
        /// Sync the bottom bar with the current selection.
        /// </summary>
        public void SyncSelection(HashSet<AbstractCommandable> selectedUnits)
        {
            if (!isBuilt) BuildPanel();

            if (selectedUnits != null && selectedUnits.Count > 0)
            {
                base.EnableFor(selectedUnits);
                if (panelRoot != null) panelRoot.SetActive(true);
            }
            else
            {
                base.Disable();
                if (panelRoot != null) panelRoot.SetActive(true);
            }
        }

        /// <summary>
        /// Override Disable to keep the panel active (just clear buttons).
        /// </summary>
        public new void Disable()
        {
            base.Disable();
            if (panelRoot != null) panelRoot.SetActive(true);
        }
    }
}
