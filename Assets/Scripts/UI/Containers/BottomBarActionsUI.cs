using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Persistent bottom-center action bar that shows ALL unlockable actions from
    /// the BlueprintDraftManager — all buildings that have been unlocked via draft cards,
    /// plus any commands from existing units/buildings.
    ///
    /// Can either self-assemble its own panel at runtime, or use a pre-assigned panelRoot
    /// (drag a GameObject into the Inspector) so you can position it visually in the editor.
    ///
    /// Refreshes whenever units/buildings are selected, die, or spawn, and also
    /// on a periodic tick to catch draft completions and construction completions.
    /// </summary>
    public class BottomBarActionsUI : MonoBehaviour
    {
        [Header("Panel Assignment (leave empty to self-assemble)")]
        [Tooltip("Assign a GameObject to use as the panel root. If empty, the bar will self-assemble at runtime.")]
        [SerializeField] private GameObject panelRootOverride;

        [Header("Self-Assembly Settings")]
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private int buttonCount = 12;

        private GameObject panelRoot;
        private UIActionButton[] actionButtons;
        private bool isBuilt = false;
        private Owner owner = Owner.Player1;

        // Cached template restrictions to avoid searching every time
        private static BuildingRestrictionSO[] cachedTemplateRestrictions;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleRefresh;
            Bus<BuildingDeathEvent>.OnEvent[owner] += HandleRefresh;
            Bus<BuildingSpawnEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] += HandleRefresh;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<BuildingDeathEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<BuildingSpawnEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] -= HandleRefresh;

            // Clean up dynamically created buttons
            if (actionButtons != null)
            {
                for (int i = 0; i < actionButtons.Length; i++)
                {
                    if (actionButtons[i] != null)
                    {
                        Destroy(actionButtons[i].gameObject);
                        actionButtons[i] = null;
                    }
                }
                actionButtons = null;
            }
        }

        private void Awake()
        {
            BuildPanel();
        }

        public void Initialize()
        {
            BuildPanel();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            // Periodic refresh every ~0.5s to catch newly completed buildings
            if (Time.frameCount % 30 == 0)
            {
                RefreshBar();
            }
        }


        private void HandleRefresh(UnitSelectedEvent evt) { RefreshBar(); }
        private void HandleRefresh(UnitDeselectedEvent evt) { RefreshBar(); }
        private void HandleRefresh(UnitDeathEvent evt) { RefreshBar(); }
        private void HandleRefresh(BuildingDeathEvent evt) { RefreshBar(); }
        private void HandleRefresh(BuildingSpawnEvent evt) { RefreshBar(); }
        private void HandleRefresh(UpgradeResearchedEvent evt) { RefreshBar(); }

        private void BuildPanel()
        {
            if (isBuilt) return;

            // If a panel root override is assigned, use it directly (for visual positioning in editor)
            if (panelRootOverride != null)
            {
                panelRoot = panelRootOverride;
                ApplyPanelSetup(panelRoot);
                BuildButtons();
                isBuilt = true;
                panelRoot.SetActive(true);
                RefreshBar();
                Debug.Log($"[BottomBarActionsUI] Using pre-assigned panel root: {panelRoot.name}");
                return;
            }

            // Otherwise, self-assemble the panel
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[BottomBarActionsUI] No Canvas found.");
                return;
            }

            // Create the bottom bar panel
            panelRoot = new GameObject("Bottom Action Bar");
            panelRoot.transform.SetParent(canvas.transform, false);

            // Set up RectTransform for the self-assembled panel
            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 30f);
            panelRect.sizeDelta = new Vector2(0f, 60f);

            ApplyPanelSetup(panelRoot);

            BuildButtons();
            isBuilt = true;
            panelRoot.SetActive(true);
            RefreshBar();
            Debug.Log($"[BottomBarActionsUI] Self-assembled bottom bar with {buttonCount} slots.");
        }

        /// <summary>
        /// Apply standard setup (RectTransform, background, layout) to a panel root.
        /// Used by both the override path and the self-assemble path.
        /// </summary>
        private void ApplyPanelSetup(GameObject targetPanel)
        {
            // Add background if not present
            Image bg = targetPanel.GetComponent<Image>();
            if (bg == null)
            {
                bg = targetPanel.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.10f, 0.13f, 0.9f);
            }

            // Add layout group if not present
            HorizontalLayoutGroup hlg = targetPanel.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = targetPanel.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 6f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.padding = new RectOffset(8, 8, 6, 6);
            }
        }

        /// <summary>
        /// Create button slots under the current panelRoot.
        /// </summary>
        private void BuildButtons()
        {
            // Clean up existing buttons
            if (actionButtons != null)
            {
                for (int i = 0; i < actionButtons.Length; i++)
                {
                    if (actionButtons[i] != null)
                    {
                        Destroy(actionButtons[i].gameObject);
                        actionButtons[i] = null;
                    }
                }
            }

            // Find existing button children (non-procedural approach)
            actionButtons = panelRoot.GetComponentsInChildren<UIActionButton>(true);

            // If no buttons exist, create them manually
            if (actionButtons == null || actionButtons.Length == 0)
            {
                actionButtons = new UIActionButton[buttonCount];
                for (int i = 0; i < buttonCount; i++)
                {
                    GameObject btnGo = new GameObject($"Action Slot {i}");
                    btnGo.transform.SetParent(panelRoot.transform, false);
                    btnGo.layer = LayerMask.NameToLayer("UI");

                    RectTransform btnRect = btnGo.AddComponent<RectTransform>();
                    btnRect.sizeDelta = new Vector2(52, 52);

                    Image img = btnGo.AddComponent<Image>();
                    img.color = new Color(0.2f, 0.25f, 0.3f, 1f);
                    img.raycastTarget = true;

                    Button btn = btnGo.AddComponent<Button>();
                    btn.targetGraphic = img;

                    UIActionButton actionBtn = btnGo.AddComponent<UIActionButton>();

                    // Set the icon field via reflection so EnableFor can use it
                    var iconField = typeof(UIActionButton).GetField("icon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    iconField?.SetValue(actionBtn, img);

                    actionButtons[i] = actionBtn;
                }
            }

            isBuilt = true;
            panelRoot.SetActive(true);

            // Do an immediate refresh to populate
            RefreshBar();

            Debug.Log($"[BottomBarActionsUI] Built bottom bar with {actionButtons.Length} slots.");
        }

        /// <summary>
        /// Collect ALL unlockable actions — buildings from BlueprintDraftManager plus
        /// commands from existing units/buildings — and display them.
        /// </summary>
        private void RefreshBar()
        {
            if (!isBuilt || actionButtons == null) return;

            var allCommands = CollectAllCommands();

            // Deduplicate by command name (same command from multiple buildings = one button)
            var seen = new HashSet<string>();
            var uniqueCommands = new List<BaseCommand>();
            foreach (var cmd in allCommands)
            {
                if (cmd != null && !seen.Contains(cmd.Name))
                {
                    seen.Add(cmd.Name);
                    uniqueCommands.Add(cmd);
                }
            }

            // Populate buttons
            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] == null) continue;

                if (i < uniqueCommands.Count)
                {
                    var cmd = uniqueCommands[i];
                    var ctx = new CommandContext(owner, null, new RaycastHit());
                    actionButtons[i].EnableFor(cmd, null, () =>
                    {
                        var buildingName = cmd is BuildBuildingCommand bbc ? bbc.Building?.Name : "N/A";
                        Debug.Log($"[BottomBarActionsUI] Button clicked: {cmd.Name}, Building={buildingName}, Restrictions={cmd.Restrictions?.Length ?? 0}");
                        Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(cmd));
                    });
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        /// <summary>
        /// Collect all unlockable actions:
        /// 1. All buildings unlocked via BlueprintDraftManager (from draft cards)
        /// 2. All commands from existing units/buildings (production, research, etc.)
        /// </summary>
        private List<BaseCommand> CollectAllCommands()
        {
            var commands = new List<BaseCommand>();
            var addedBuildingNames = new HashSet<string>();

            // 1. Add BuildBuildingCommand for ALL buildings unlocked via draft
            // Find template restrictions from existing BuildBuildingCommand assets
            BuildingRestrictionSO[] templateRestrictions = FindTemplateRestrictionsFromAssets();
            Debug.Log($"[BottomBarActionsUI] Template restrictions: {(templateRestrictions != null ? templateRestrictions.Length.ToString() : "null")}");

            var unlockedBuildingNames = BlueprintDraftManager.GetUnlockedBuildingNames();
            Debug.Log($"[BottomBarActionsUI] Unlocked buildings: {string.Join(", ", unlockedBuildingNames)}");
            foreach (var buildingName in unlockedBuildingNames)
            {
                if (string.IsNullOrEmpty(buildingName)) continue;
                if (addedBuildingNames.Contains(buildingName)) continue;

                var buildingSO = BlueprintDraftManager.GetBuildingSOByName(buildingName);
                if (buildingSO == null) continue;

                // Create a BuildBuildingCommand for this unlocked building
                var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                buildCmd.Name = "Build " + buildingSO.Name;
                buildCmd.Building = buildingSO;
                buildCmd.Icon = buildingSO.Icon;
                buildCmd.Slot = FindFreeSlot(commands);

                // Copy restrictions and ghost prefab from template
                if (templateRestrictions != null)
                {
                    var restrictionsField = typeof(BaseCommand).GetField("<Restrictions>k__BackingField",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    restrictionsField?.SetValue(buildCmd, templateRestrictions);

                    // Copy GhostPrefab from the first template command that has one
                    var templateCommand = FindFirstTemplateCommand();
                    if (templateCommand != null)
                    {
                        var ghostField = typeof(BaseCommand).GetField("<GhostPrefab>k__BackingField",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        ghostField?.SetValue(buildCmd, templateCommand.GhostPrefab);
                    }
                }

                commands.Add(buildCmd);
                addedBuildingNames.Add(buildingName);
            }

            // 2. Gather commands from all owned units (workers, etc.)
            var allUnits = FindObjectsByType<AbstractCommandable>();
            foreach (var unit in allUnits)
            {
                if (unit == null) continue;
                if (unit.Owner != owner) continue;

                if (unit.AvailableCommands != null)
                {
                    foreach (var cmd in unit.AvailableCommands)
                    {
                        if (cmd == null) continue;
                        // Skip BuildBuildingCommands — we already added those from the draft
                        if (cmd is BuildBuildingCommand) continue;
                        commands.Add(cmd);
                    }
                }
            }

            // 3. Gather non-building commands from owned buildings (production, research, etc.)
            if (BaseBuilding.ActiveBuildings != null)
            {
                foreach (var building in BaseBuilding.ActiveBuildings)
                {
                    if (building == null) continue;
                    if (building.Owner != owner) continue;

                    if (building.AvailableCommands != null)
                    {
                        foreach (var cmd in building.AvailableCommands)
                        {
                            if (cmd == null) continue;
                            // Skip BuildBuildingCommands — we already added those from the draft
                            if (cmd is BuildBuildingCommand) continue;
                            commands.Add(cmd);
                        }
                    }
                }
            }

            return commands;
        }

        /// <summary>
        /// Find template restrictions from BuildBuildingCommand assets in the project.
        /// This is needed because dynamically created BuildBuildingCommands need Restrictions
        /// for AllRestrictionsPass to work correctly.
        /// </summary>
        private BuildingRestrictionSO[] FindTemplateRestrictionsFromAssets()
        {
            // Return cached restrictions if available
            if (cachedTemplateRestrictions != null)
            {
                return cachedTemplateRestrictions;
            }

            // First try to find from scene buildings' AvailableCommands (runtime objects)
            var allBuildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            foreach (var building in allBuildings)
            {
                if (building == null || building.Owner != owner) continue;
                if (building.AvailableCommands != null)
                {
                    foreach (var cmd in building.AvailableCommands)
                    {
                        if (cmd is BuildBuildingCommand bbc && bbc.Restrictions != null && bbc.Restrictions.Length > 0)
                        {
                            cachedTemplateRestrictions = bbc.Restrictions;
                            return cachedTemplateRestrictions;
                        }
                    }
                }
            }

            // No template found — restrictions will be null, which means AllRestrictionsPass
            // will skip collision checks (acceptable for bottom bar commands)
            Debug.Log("[BottomBarActionsUI] No template restrictions found!");
            return null;
        }

        /// <summary>
        /// Find the first BuildBuildingCommand with a GhostPrefab set (for copying to dynamically created commands).
        /// </summary>
        private BuildBuildingCommand FindFirstTemplateCommand()
        {
            var allBuildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            foreach (var building in allBuildings)
            {
                if (building == null || building.Owner != owner) continue;
                if (building.AvailableCommands != null)
                {
                    foreach (var cmd in building.AvailableCommands)
                    {
                        if (cmd is BuildBuildingCommand bbc && bbc.GhostPrefab != null)
                        {
                            return bbc;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Find the next available slot index for a command.
        /// </summary>
        private int FindFreeSlot(List<BaseCommand> commands)
        {
            var usedSlots = new HashSet<int>();
            foreach (var cmd in commands)
            {
                if (cmd != null) usedSlots.Add(cmd.Slot);
            }
            for (int i = 0; i < 8; i++)
            {
                if (!usedSlots.Contains(i)) return i;
            }
            return -1;
        }

        private GameObject CreateDefaultButton(Transform parent, int index)
        {
            GameObject btnGo = new GameObject($"Action Slot {index}");
            btnGo.transform.SetParent(parent, false);
            btnGo.layer = LayerMask.NameToLayer("UI");

            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(52, 52);

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
            iconRt.offsetMin = new Vector2(8, 8);
            iconRt.offsetMax = new Vector2(-8, -8);
            iconGo.AddComponent<Image>();

            // Add UIActionButton disabled, wire icon, then enable
            UIActionButton actionBtn = btnGo.AddComponent<UIActionButton>();
            actionBtn.enabled = false;
            Image iconImage = iconGo.GetComponent<Image>();
            var iconField = typeof(UIActionButton).GetField("icon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            iconField?.SetValue(actionBtn, iconImage);
            actionBtn.enabled = true;

            return btnGo;
        }
    }
}
