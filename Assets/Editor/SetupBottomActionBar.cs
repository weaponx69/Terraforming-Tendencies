using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.UI.Containers;

namespace GameDevTV.RTS.Editor
{
    /// <summary>
    /// Editor menu action to permanently set up the Bottom Action Bar in the Game scene.
    /// Run once via Tools > Setup > Create Bottom Action Bar.
    /// This creates the panel, buttons, and wires everything into RuntimeUI — serialized into the .unity scene file.
    /// </summary>
    public static class SetupBottomActionBar
    {
        [MenuItem("Tools/Setup/Create Bottom Action Bar")]
        public static void CreateBottomActionBar()
        {
            // Find the Game scene's RuntimeUI
            var runtimeUIs = Object.FindObjectsByType<RuntimeUI>();
            if (runtimeUIs.Length == 0)
            {
                Debug.LogError("[SetupBottomActionBar] No RuntimeUI found in scene. Open the Game scene first.");
                return;
            }

            RuntimeUI runtimeUI = runtimeUIs[0];

            // Check if bottom bar already exists
            var existing = runtimeUI.GetComponentInChildren<BottomBarActionsUI>(true);
            if (existing != null)
            {
                Debug.Log("[SetupBottomActionBar] Bottom Action Bar already exists. Skipping.");
                return;
            }

            // Find the canvas
            Canvas canvas = runtimeUI.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[SetupBottomActionBar] No Canvas found.");
                return;
            }

            // Create the bottom bar panel as a child of the canvas
            GameObject bottomBarGo = new GameObject("Bottom Action Bar");
            bottomBarGo.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(bottomBarGo, "Create Bottom Action Bar");

            RectTransform panelRect = bottomBarGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.02f);
            panelRect.anchorMax = new Vector2(0.8f, 0.12f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;

            // Background
            Image bg = bottomBarGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.15f, 0.85f);

            // Layout
            HorizontalLayoutGroup hlg = bottomBarGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(8, 8, 6, 6);

            ContentSizeFitter csf = bottomBarGo.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Add the BottomBarActionsUI component
            BottomBarActionsUI bottomBarUI = bottomBarGo.AddComponent<BottomBarActionsUI>();

            // Create 9 action button slots (matching the original ActionsUI)
            var buttons = new UIActionButton[9];
            for (int i = 0; i < 9; i++)
            {
                GameObject btnGo = CreateActionButton(bottomBarGo.transform, i);
                RectTransform btnRect = btnGo.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(56, 56);
                buttons[i] = btnGo.GetComponent<UIActionButton>();
            }

            // Use reflection to set the private actionButtons field via the serialized property
            SerializedObject serializedBottomBar = new SerializedObject(bottomBarUI);
            SerializedProperty actionButtonsProp = serializedBottomBar.FindProperty("actionButtons");
            if (actionButtonsProp != null && actionButtonsProp.isArray)
            {
                actionButtonsProp.arraySize = buttons.Length;
                for (int i = 0; i < buttons.Length; i++)
                {
                    actionButtonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                }
                serializedBottomBar.ApplyModifiedProperties();
            }

            // Wire into RuntimeUI
            SerializedObject serializedRuntime = new SerializedObject(runtimeUI);
            SerializedProperty bbProp = serializedRuntime.FindProperty("bottomBarActionsUI");
            if (bbProp != null)
            {
                bbProp.objectReferenceValue = bottomBarUI;
                serializedRuntime.ApplyModifiedProperties();
            }

            // Select the new bottom bar
            Selection.activeGameObject = bottomBarGo;

            Debug.Log("[SetupBottomActionBar] Bottom Action Bar created and wired into RuntimeUI. Save the scene to persist.");
        }

        private static GameObject CreateActionButton(Transform parent, int index)
        {
            GameObject btnGo = new GameObject($"Action Slot {index}");
            btnGo.transform.SetParent(parent, false);
            btnGo.layer = parent.gameObject.layer;

            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(56, 56);

            Image img = btnGo.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.3f, 1f);

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

            // UIActionButton component
            btnGo.AddComponent<UIActionButton>();

            return btnGo;
        }
    }
}
