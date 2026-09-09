using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Bottom-left card hand. Cards keep a fixed playing-card size.
    /// When there are more cards than fit (~5), the strip scrolls horizontally
    /// with the mouse wheel (and trackpad horizontal scroll).
    /// </summary>
    public class BottomBarActionsUI : MonoBehaviour
    {
        [Header("Button Wiring")]
        [SerializeField] private UIActionButton[] actionButtons;

        [Header("Card Layout")]
        [SerializeField] private Vector2 cardSize = new Vector2(158f, 220f);
        [SerializeField] private float cardSpacing = 14f;
        [SerializeField] private float bottomMargin = 16f;
        [SerializeField] private float leftMargin = 16f;
        [SerializeField] private int visibleCardCount = 5;
        // Input System wheel deltas are often ~±120 per notch; ~1.5 → one card per tick.
        [SerializeField] private float scrollSpeed = 1.5f;

        private bool isBuilt;
        private Owner owner = Owner.Player1;
        private float scrollOffset;
        private float contentWidth;
        private float viewportWidth;
        private RectTransform cardsRt;
        private RectTransform containerRt;
        private Image dockImage;
        private Color dockIdleColor = new Color(0.04f, 0.07f, 0.12f, 0.40f);
        private Color dockHoverColor = new Color(0.20f, 0.55f, 0.85f, 0.62f);
        private static readonly List<RaycastResult> UiRaycastHits = new List<RaycastResult>(16);
        private static BottomBarActionsUI instance;

        /// <summary>True while the pointer is over the hand strip (camera zoom should yield the wheel).</summary>
        public static bool IsPointerOverHandStrip =>
            instance != null && instance.isBuilt && instance.IsPointerOverHand();

        private void OnEnable()
        {
            instance = this;
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleRefresh;
            Bus<BuildingDeathEvent>.OnEvent[owner] += HandleRefresh;
            Bus<BuildingSpawnEvent>.OnEvent[owner] += HandleRefresh;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] += HandleRefresh;
            CardDeckController.OnHandChanged += RefreshBar;
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
            if (!Application.isPlaying) return;
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<BuildingDeathEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<BuildingSpawnEvent>.OnEvent[owner] -= HandleRefresh;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] -= HandleRefresh;
            CardDeckController.OnHandChanged -= RefreshBar;
            if (instance == this) instance = null;
        }

        private void Awake()
        {
            instance = this;
            cardsRt = transform as RectTransform;
            if (cardsRt == null)
            {
                Debug.LogError("[BottomBarActionsUI] Needs a RectTransform.", this);
                return;
            }

            if (actionButtons == null || actionButtons.Length == 0)
                actionButtons = GetComponentsInChildren<UIActionButton>(true);

            if (actionButtons == null || actionButtons.Length == 0)
            {
                Debug.LogError("[BottomBarActionsUI] No action buttons found.", this);
                return;
            }

            UndoBrokenScrollHierarchy();
            HideChromeBackground();
            // Always use a wheel-friendly speed (serialized prefab values can stick at old 80).
            scrollSpeed = 1.5f;
            ApplyPlayingCardLayout();

            isBuilt = true;
            gameObject.SetActive(true);
            RefreshBar();
            Debug.Log($"[BottomBarActionsUI] Hand visible ({actionButtons.Length} slots).");
        }

        private void Start() => RefreshBar();

        /// <summary>
        /// Previous broken ScrollRect pass reparented buttons under HandContent.
        /// Put them back as direct children of this bar.
        /// </summary>
        private void UndoBrokenScrollHierarchy()
        {
            // Destroy leftover ScrollRect — we scroll by offsetting the content strip.
            var sr = GetComponent<ScrollRect>();
            if (sr != null) Destroy(sr);

            Transform leftoverVp = transform.Find("HandViewport");
            if (leftoverVp != null)
            {
                // Rescue any buttons nested under the viewport/content.
                foreach (var btn in leftoverVp.GetComponentsInChildren<UIActionButton>(true))
                {
                    if (btn != null) btn.transform.SetParent(transform, false);
                }
                Destroy(leftoverVp.gameObject);
            }

            // Also rescue buttons that may sit under a sibling HandContent.
            foreach (var btn in GetComponentsInChildren<UIActionButton>(true))
            {
                if (btn != null && btn.transform.parent != transform)
                    btn.transform.SetParent(transform, false);
            }

            // Refresh wired array from current children order if needed.
            if (actionButtons == null || actionButtons.Length == 0
                || actionButtons[0] == null || actionButtons[0].transform.parent != transform)
            {
                actionButtons = GetComponentsInChildren<UIActionButton>(true);
            }
        }

        private void ApplyPlayingCardLayout()
        {
            float width = Mathf.Max(48f, cardSize.x);
            float height = Mathf.Max(width * 1.25f, cardSize.y);

            containerRt = transform.parent as RectTransform;
            if (containerRt == null) containerRt = cardsRt;

            // Dock container bottom-left with a fixed viewport size (scroll if more cards).
            int padL = 8, padR = 8, padT = 6, padB = 6;
            viewportWidth = visibleCardCount * width
                + Mathf.Max(0, visibleCardCount - 1) * cardSpacing
                + padL + padR;
            float viewportHeight = height + padT + padB;

            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 0f);
            containerRt.pivot = new Vector2(0f, 0f);
            containerRt.anchoredPosition = new Vector2(leftMargin, bottomMargin);
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
            containerRt.SetAsLastSibling();
            containerRt.localScale = Vector3.one;

            // Mask so off-screen cards are clipped.
            if (containerRt.GetComponent<RectMask2D>() == null)
                containerRt.gameObject.AddComponent<RectMask2D>();
            dockImage = containerRt.GetComponent<Image>();
            if (dockImage == null) dockImage = containerRt.gameObject.AddComponent<Image>();
            dockImage.color = dockIdleColor;
            dockImage.raycastTarget = true; // needed so scroll wheel / hover hit the hand
            dockImage.enabled = true;

            // This bar fills the dock; we slide it horizontally for scrolling.
            cardsRt.anchorMin = new Vector2(0f, 0f);
            cardsRt.anchorMax = new Vector2(0f, 1f);
            cardsRt.pivot = new Vector2(0f, 0.5f);
            cardsRt.offsetMin = new Vector2(0f, 0f);
            cardsRt.offsetMax = new Vector2(0f, 0f);
            cardsRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
            cardsRt.anchoredPosition = Vector2.zero;
            cardsRt.localScale = Vector3.one;

            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.enabled = true;
            hlg.spacing = cardSpacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(padL, padR, padT, padB);

            var parentHlg = containerRt.GetComponent<HorizontalLayoutGroup>();
            if (parentHlg != null && containerRt != cardsRt) parentHlg.enabled = false;

            foreach (var slot in actionButtons)
            {
                if (slot == null) continue;
                var rt = slot.transform as RectTransform;
                if (rt == null) continue;
                if (rt.parent != transform) rt.SetParent(transform, false);
                rt.localScale = Vector3.one;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width, height);
                rt.gameObject.SetActive(true);

                var le = slot.GetComponent<LayoutElement>();
                if (le == null) le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = width;
                le.preferredHeight = height;
                le.minWidth = width;
                le.minHeight = height;
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;
                le.ignoreLayout = false;

                Transform icon = slot.transform.Find("Icon");
                if (icon is RectTransform iconRt)
                {
                    iconRt.anchorMin = new Vector2(0.08f, 0.28f);
                    iconRt.anchorMax = new Vector2(0.92f, 0.70f);
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;
                }
            }

            FitLayoutToActiveCards();
        }

        private void FitLayoutToActiveCards()
        {
            if (containerRt == null || cardsRt == null || actionButtons == null) return;

            float width = Mathf.Max(48f, cardSize.x);
            float height = Mathf.Max(width * 1.25f, cardSize.y);
            var hlg = GetComponent<HorizontalLayoutGroup>();
            int leftPad = hlg != null ? hlg.padding.left : 8;
            int rightPad = hlg != null ? hlg.padding.right : 8;
            int topPad = hlg != null ? hlg.padding.top : 6;
            int bottomPad = hlg != null ? hlg.padding.bottom : 6;

            int activeSlots = 0;
            foreach (var slot in actionButtons)
            {
                if (slot == null) continue;
                bool active = slot.IsActive;
                var le = slot.GetComponent<LayoutElement>();
                if (le == null) le = slot.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = !active;
                if (!active) continue;

                activeSlots++;
                le.preferredWidth = width;
                le.preferredHeight = height;
                le.minWidth = width;
                le.minHeight = height;
            }

            contentWidth = activeSlots <= 0
                ? leftPad + rightPad
                : activeSlots * width
                  + Mathf.Max(0, activeSlots - 1) * cardSpacing
                  + leftPad + rightPad;

            viewportWidth = Mathf.Min(
                contentWidth,
                visibleCardCount * width
                + Mathf.Max(0, visibleCardCount - 1) * cardSpacing
                + leftPad + rightPad);

            float viewportHeight = height + topPad + bottomPad;
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 0f);
            containerRt.pivot = new Vector2(0f, 0f);
            containerRt.anchoredPosition = new Vector2(leftMargin, bottomMargin);

            cardsRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            cardsRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
            ClampScroll();
            ApplyScrollOffset();

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRt);
        }

        private void ClampScroll()
        {
            float minOffset = Mathf.Min(0f, viewportWidth - contentWidth);
            scrollOffset = Mathf.Clamp(scrollOffset, minOffset, 0f);
        }

        private void ApplyScrollOffset()
        {
            if (cardsRt == null) return;
            cardsRt.anchoredPosition = new Vector2(scrollOffset, 0f);
        }

        private void HideChromeBackground()
        {
            // Do NOT clear the container dock Image — it must receive scroll/raycasts.
            ClearBackgroundImage(GetComponent<Image>());
        }

        private static void ClearBackgroundImage(Image image)
        {
            if (image == null) return;
            Color c = image.color;
            c.a = 0f;
            image.color = c;
            image.raycastTarget = false;
            image.enabled = false;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            UpdateDockHoverGlow();

            if (BuildingSiteSelectionController.IsSelecting) return;

            HandleMouseWheelScroll();

            if (Time.frameCount % 30 == 0)
                RefreshBar();
        }

        private void UpdateDockHoverGlow()
        {
            if (dockImage == null) return;
            Color target = IsPointerOverHand() ? dockHoverColor : dockIdleColor;
            dockImage.color = Color.Lerp(dockImage.color, target, Time.unscaledDeltaTime * 12f);
        }

        private void HandleMouseWheelScroll()
        {
            if (Mouse.current == null || containerRt == null) return;
            if (contentWidth <= viewportWidth + 0.5f) return;
            if (!IsPointerOverHand()) return;

            Vector2 scroll = Mouse.current.scroll.ReadValue();
            // Vertical wheel scrolls the hand left/right; also accept horizontal axis.
            float delta = scroll.y + scroll.x;
            if (Mathf.Abs(delta) < 0.01f) return;

            scrollOffset += delta * scrollSpeed;
            ClampScroll();
            ApplyScrollOffset();
        }

        private bool IsPointerOverHand()
        {
            if (containerRt == null || Mouse.current == null) return false;

            Vector2 screen = Mouse.current.position.ReadValue();

            // Prefer UI raycasts so hovering any card button counts (rect math alone
            // can miss depending on canvas scaler / Game view letterboxing).
            if (EventSystem.current != null)
            {
                var ped = new PointerEventData(EventSystem.current) { position = screen };
                UiRaycastHits.Clear();
                EventSystem.current.RaycastAll(ped, UiRaycastHits);
                for (int i = 0; i < UiRaycastHits.Count; i++)
                {
                    Transform hit = UiRaycastHits[i].gameObject.transform;
                    if (hit == containerRt || hit.IsChildOf(containerRt) || hit == transform || hit.IsChildOf(transform))
                        return true;
                }
            }

            Camera eventCam = null;
            var canvas = containerRt.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCam = canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(containerRt, screen, eventCam)
                || (cardsRt != null && RectTransformUtility.RectangleContainsScreenPoint(cardsRt, screen, eventCam));
        }

        private void HandleRefresh(UnitSelectedEvent evt) { RefreshBar(); }
        private void HandleRefresh(UnitDeselectedEvent evt) { RefreshBar(); }
        private void HandleRefresh(UnitDeathEvent evt) { RefreshBar(); }
        private void HandleRefresh(BuildingDeathEvent evt) { RefreshBar(); }
        private void HandleRefresh(BuildingSpawnEvent evt) { RefreshBar(); }
        private void HandleRefresh(UpgradeResearchedEvent evt) { RefreshBar(); }

        public void RefreshBar()
        {
            if (!isBuilt || actionButtons == null) return;
            if (BuildingSiteSelectionController.IsSelecting) return;

            var hand = CardDeckController.Instance?.Hand;
            if (hand == null) return;

            int cardsToShow = Mathf.Min(hand.Count, actionButtons.Length);

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] == null) continue;

                if (i < cardsToShow && hand[i] != null)
                {
                    var card = hand[i];
                    int cardIndex = i;
                    string sectorGoal = TerraformingGoalColors.GetSectorGoalForCard(card);

                    if (card is UnlockBuildingCardSO unlockCard && unlockCard.buildingToUnlock != null)
                    {
                        var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                        buildCmd.Name = unlockCard.buildingToUnlock.Name;
                        buildCmd.Building = unlockCard.buildingToUnlock;
                        buildCmd.Icon = unlockCard.buildingToUnlock.Icon;
                        buildCmd.Slot = i;
                        buildCmd.HandIndex = cardIndex;
                        buildCmd.GhostPrefab = FindGhostPrefabForBuilding(unlockCard.buildingToUnlock);

                        actionButtons[i].EnableFor(buildCmd, null, () =>
                        {
                            PlayBuildingCard(cardIndex, unlockCard.buildingToUnlock);
                        }, sectorGoal, unlockCard.GetMaterialsPlayCost());
                    }
                    else
                    {
                        var playCmd = ScriptableObject.CreateInstance<PlayCardCommand>();
                        playCmd.Name = card.cardName;
                        Sprite cardIcon = card.icon;
                        if (cardIcon == null && card is SpawnUnitCardSO spawnCard && spawnCard.unitPrefab != null)
                        {
                            var unit = spawnCard.unitPrefab.GetComponent<AbstractUnit>();
                            if (unit != null) cardIcon = unit.Icon;
                        }
                        playCmd.Icon = cardIcon;
                        playCmd.Slot = i;
                        playCmd.HandIndex = cardIndex;
                        playCmd.MaterialsCost = card.GetMaterialsPlayCost();

                        actionButtons[i].EnableFor(playCmd, null, () =>
                        {
                            Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(playCmd));
                        }, sectorGoal, playCmd.MaterialsCost);
                    }
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }

            FitLayoutToActiveCards();
        }

        private void PlayBuildingCard(int cardIndex, BuildingSO building)
        {
            if (building == null) return;

            if (!PowerGridManager.CanPlayBuildingForPower(building, owner))
            {
                float gen = PowerGridManager.GetBoardPowerGeneration(owner);
                float need = PowerGridManager.GetBuildingPowerUpkeep(building);
                ExplorationManager.NotifyExplorationFailed(
                    $"Not enough power for {building.Name} (needs {need:0.#} upkeep, generating {gen:0.#}). Build more Solar first.");
                return;
            }

            if (BuildingSiteRegistry.IsMineBuilding(building))
            {
                if (!DiscoverySystem.HasDiscoveredMineDeposit(building))
                {
                    DiscoverySystem.TryGetMineResourceType(building, out string type);
                    ExplorationManager.NotifyExplorationFailed(
                        $"No discovered {type ?? "resource"} deposit yet. Explore / discover mines before placing {building.Name}.");
                    return;
                }
            }

            var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
            buildCmd.Name = building.Name;
            buildCmd.Building = building;
            buildCmd.Icon = building.Icon;
            buildCmd.HandIndex = cardIndex;
            buildCmd.GhostPrefab = FindGhostPrefabForBuilding(building);
            Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(buildCmd));
        }

        private GameObject FindGhostPrefabForBuilding(BuildingSO buildingSO)
        {
            if (buildingSO == null) return null;
            var allCommands = Resources.FindObjectsOfTypeAll<BuildBuildingCommand>();
            foreach (var cmd in allCommands)
            {
                if (cmd != null && cmd.Building != null && cmd.Building.Name == buildingSO.Name && cmd.GhostPrefab != null)
                    return cmd.GhostPrefab;
            }
            return buildingSO.Prefab;
        }
    }
}
