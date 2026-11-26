using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Prologue;
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
        [SerializeField] private Vector2 cardSize = new Vector2(196f, 286f);
        [SerializeField] private float cardSpacing = 14f;
        [SerializeField] private float bottomMargin = 16f;
        [SerializeField] private float leftMargin = 16f;
        [SerializeField] private int visibleCardCount = 5;
        // Cards advanced per mouse-wheel notch (Linux trackpads often send tiny deltas).
        [SerializeField] private float scrollCardsPerNotch = 2.5f;

        private bool isBuilt;
        private Owner owner = Owner.Player1;
        private float scrollOffset;
        private int lastHandFingerprint = int.MinValue;
        private int lastHandCount = -1;
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

        /// <summary>Screen point (Unity, bottom-left origin) for an enabled hand card.</summary>
        public static bool TryGetHandCardScreenPoint(string nameContains, out Vector2 screenBottomLeft)
        {
            screenBottomLeft = default;
            if (instance == null || string.IsNullOrEmpty(nameContains)) return false;
            return instance.TryGetHandCardScreenPointImpl(nameContains, out screenBottomLeft);
        }

        bool TryGetHandCardScreenPointImpl(string nameContains, out Vector2 screenBottomLeft)
        {
            screenBottomLeft = default;
            if (actionButtons == null) return false;
            var deck = CardDeckController.Instance;
            if (deck == null) return false;

            for (int i = 0; i < actionButtons.Length; i++)
            {
                var slot = actionButtons[i];
                if (slot == null || !slot.IsActive) continue;
                int handIndex = slot.HandIndexForPlaythrough;
                if (handIndex < 0 || handIndex >= deck.Hand.Count) continue;
                var card = deck.Hand[handIndex];
                if (card == null || card.cardName == null) continue;
                if (card.cardName.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var rt = slot.GetComponent<RectTransform>();
                return UiPlaythroughUtil.TryScreenCenter(rt, out screenBottomLeft);
            }
            return false;
        }

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
            CardDeckController.OnHandChanged += HandleHandChanged;
            SectorManager.OnActiveSectorChanged += HandleFocusedSectorChanged;
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
            CardDeckController.OnHandChanged -= HandleHandChanged;
            SectorManager.OnActiveSectorChanged -= HandleFocusedSectorChanged;
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
            if (scrollCardsPerNotch < 1.5f) scrollCardsPerNotch = 2.5f;
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

        static void CardFaceSize(out float width, out float height)
        {
            if (PrologueController.IsActive)
            {
                width = PrologueUiLayout.CardWidth;
                height = PrologueUiLayout.CardHeight;
            }
            else
            {
                width = 196f;
                height = 286f;
            }
        }

        float HandCardSpacing() =>
            PrologueController.IsActive ? PrologueUiLayout.CardSpacing : cardSpacing;

        private void ApplyPlayingCardLayout()
        {
            CardFaceSize(out float width, out float height);
            float gap = HandCardSpacing();

            containerRt = transform.parent as RectTransform;
            if (containerRt == null) containerRt = cardsRt;

            // Dock container bottom-left with a fixed viewport size (scroll if more cards).
            int padL = 6, padR = 6, padT = 4, padB = 4;
            viewportWidth = visibleCardCount * width
                + Mathf.Max(0, visibleCardCount - 1) * gap
                + padL + padR;
            float viewportHeight = height + padT + padB;

            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 0f);
            containerRt.pivot = new Vector2(0f, 0f);
            containerRt.anchoredPosition = new Vector2(leftMargin, EffectiveBottomMargin());
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
            // Do not zero X here — preserve scroll; FitLayout re-applies scrollOffset.
            cardsRt.anchoredPosition = new Vector2(scrollOffset, 0f);
            cardsRt.localScale = Vector3.one;

            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.enabled = true;
            hlg.spacing = gap;
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
                    iconRt.anchorMin = new Vector2(0.10f, 0.34f);
                    iconRt.anchorMax = new Vector2(0.90f, 0.68f);
                    iconRt.offsetMin = Vector2.zero;
                    iconRt.offsetMax = Vector2.zero;
                }
            }

            FitLayoutToActiveCards();
        }

        private void FitLayoutToActiveCards()
        {
            if (containerRt == null || cardsRt == null || actionButtons == null) return;

            CardFaceSize(out float width, out float height);
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

            float gap = HandCardSpacing();
            contentWidth = activeSlots <= 0
                ? leftPad + rightPad
                : activeSlots * width
                  + Mathf.Max(0, activeSlots - 1) * gap
                  + leftPad + rightPad;

            viewportWidth = Mathf.Min(
                contentWidth,
                visibleCardCount * width
                + Mathf.Max(0, visibleCardCount - 1) * gap
                + leftPad + rightPad);

            float viewportHeight = height + topPad + bottomPad;
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);
            containerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 0f);
            containerRt.pivot = new Vector2(0f, 0f);
            containerRt.anchoredPosition = new Vector2(leftMargin, EffectiveBottomMargin());

            cardsRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            cardsRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
            ClampScroll();
            ApplyScrollOffset();

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRt);
            // Layout rebuild can clobber the strip position — re-apply after.
            ApplyScrollOffset();
        }

        float EffectiveBottomMargin()
        {
            if (PrologueController.IsActive)
                return PrologueUiLayout.CardBottomMargin;
            return bottomMargin;
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
            RefreshPickedCards();

            if (BuildingSiteSelectionController.IsSelecting) return;

            HandleMouseWheelScroll();

            // Only rebuild when the hand actually changes — polling was fighting scroll
            // and recreating ScriptableObjects every half-second.
            int handPoll = GameDevTV.RTS.Prologue.PrologueController.IsActive ? 120 : 30;
            if (Time.frameCount % handPoll == 0 && HandFingerprintChanged())
                RefreshBar();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            // Keep scroll sticky if any layout pass moved the strip this frame.
            ApplyScrollOffset();
        }

        private void RefreshPickedCards()
        {
            if (actionButtons == null) return;
            int picked = GameDevTV.RTS.Player.PlayerInput.PickedHandIndex;
            bool surveyLive = PrologueDroneScan.Instance != null
                && PrologueDroneScan.Instance.ScanMode
                && !PrologueDroneScan.Instance.IsFlying;
            for (int i = 0; i < actionButtons.Length; i++)
            {
                var slot = actionButtons[i];
                if (slot == null) continue;
                slot.RefreshPicked(picked, surveyLive);
            }
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
            // Prefer vertical wheel; fall back to horizontal trackpad axis.
            float delta = Mathf.Abs(scroll.y) >= Mathf.Abs(scroll.x) ? scroll.y : scroll.x;
            if (Mathf.Abs(delta) < 0.01f) return;

            // Mouse wheels ≈ ±120 per notch. Trackpads send smaller values — scale
            // them gently instead of forcing a full notch (that caused jumpy snaps).
            float notches = Mathf.Abs(delta) >= 20f
                ? delta / 120f
                : delta / 40f;

            float cardStep = cardSize.x + cardSpacing;
            scrollOffset += notches * cardStep * scrollCardsPerNotch;
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

        private void HandleHandChanged()
        {
            // Preserve where the player scrolled. Only clamp after layout — resetting
            // to 0 made the strip snap back whenever RefreshHand / seating fired.
            RefreshBar();
        }

        private void HandleFocusedSectorChanged()
        {
            scrollOffset = 0f;
            RefreshBar();
        }

        private bool HandFingerprintChanged()
        {
            var hand = CardDeckController.Instance?.Hand;
            if (hand == null) return lastHandCount != 0;

            int fingerprint = hand.Count * 397;
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                fingerprint = unchecked(fingerprint * 31 + (card != null ? (int)EntityId.ToULong(card.GetEntityId()) : 0));
            }
            var focus = SectorManager.Instance?.ActiveSector;
            fingerprint = unchecked(fingerprint * 31 + (focus != null ? focus.GetHashCode() : 0));
            fingerprint = unchecked(fingerprint * 31 + PrologueMineralsOnHand());

            if (fingerprint == lastHandFingerprint && hand.Count == lastHandCount)
                return false;

            lastHandFingerprint = fingerprint;
            lastHandCount = hand.Count;
            return true;
        }

        public void ApplyPrologueCompactLayout()
        {
            if (!isBuilt) return;
            ApplyPlayingCardLayout();
            RefreshBar();
        }

        public void RefreshBar()
        {
            if (!isBuilt || actionButtons == null) return;
            if (BuildingSiteSelectionController.IsSelecting) return;

            var hand = CardDeckController.Instance?.Hand;
            if (hand == null) return;

            // Prologue hands are tiny — never leave the strip scrolled off-screen.
            if (GameDevTV.RTS.Prologue.PrologueController.IsActive && hand.Count <= 6)
                scrollOffset = 0f;

            // Visible subset for the focused sector — real hand indices stay on the commands.
            var visible = new List<(BlueprintCardSO card, int handIndex)>(hand.Count);
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card == null) continue;
                if (!CardDeckController.IsCardRelevantInFocusedSector(card)) continue;
                visible.Add((card, i));
            }

            lastHandCount = hand.Count;
            int fingerprint = visible.Count * 397;
            for (int i = 0; i < visible.Count; i++)
                fingerprint = unchecked(fingerprint * 31 + (int)EntityId.ToULong(visible[i].card.GetEntityId()));
            var focus = SectorManager.Instance?.ActiveSector;
            fingerprint = unchecked(fingerprint * 31 + (focus != null ? focus.GetHashCode() : 0));
            fingerprint = unchecked(fingerprint * 31 + PrologueMineralsOnHand());
            lastHandFingerprint = fingerprint;

            EnsureHandSlotCount(Mathf.Max(visible.Count, 1));

            int cardsToShow = Mathf.Min(visible.Count, actionButtons.Length);

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] == null) continue;

                if (i < cardsToShow && visible[i].card != null)
                {
                    var card = visible[i].card;
                    int cardIndex = visible[i].handIndex;
                    string sectorGoal = TerraformingGoalColors.GetSectorGoalForCard(card);

                    if (card is UnlockBuildingCardSO unlockCard && unlockCard.buildingToUnlock != null)
                    {
                        var buildCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                        buildCmd.Building = unlockCard.buildingToUnlock;
                        buildCmd.Name = string.IsNullOrEmpty(unlockCard.cardName)
                            ? unlockCard.buildingToUnlock.Name
                            : unlockCard.cardName;
                        buildCmd.Icon = BlueprintCardIcon.Resolve(unlockCard);
                        buildCmd.Slot = i;
                        buildCmd.HandIndex = cardIndex;
                        buildCmd.GhostPrefab = FindGhostPrefabForBuilding(unlockCard.buildingToUnlock);

                        actionButtons[i].EnableFor(buildCmd, null, () =>
                        {
                            PlayBuildingCard(cardIndex, unlockCard.buildingToUnlock);
                        }, sectorGoal, unlockCard.GetMaterialsPlayCost());
                        ApplyPrologueMineralLock(actionButtons[i], card);
                        actionButtons[i].BindSlot(cardIndex, false);
                    }
                    else
                    {
                        var playCmd = ScriptableObject.CreateInstance<PlayCardCommand>();
                        playCmd.Name = card.cardName;
                        playCmd.Icon = BlueprintCardIcon.Resolve(card);
                        playCmd.Slot = i;
                        playCmd.HandIndex = cardIndex;
                        playCmd.MaterialsCost = card.GetMaterialsPlayCost();

                        int capturedIndex = cardIndex;
                        var capturedCard = card;
                        actionButtons[i].EnableFor(playCmd, null, () =>
                        {
                            // Prologue Survey Team: play immediately even while time is paused.
                            // PlayerInput.ActivateAction used to swallow PlayCard at timeScale=0.
                            if (GameDevTV.RTS.Prologue.PrologueController.IsActive
                                && capturedCard is ScoutingCardSO
                                && CardDeckController.Instance != null)
                            {
                                CardDeckController.Instance.PlayCard(capturedIndex);
                                return;
                            }
                            Bus<CommandSelectedEvent>.Raise(owner, new CommandSelectedEvent(playCmd));
                        }, sectorGoal, playCmd.MaterialsCost);
                        ApplyPrologueMineralLock(actionButtons[i], card);
                        bool survey = card is ScoutingCardSO scout
                            && scout.scoutingType == ScoutingCardSO.ScoutingType.SurveyDrone;
                        actionButtons[i].BindSlot(cardIndex, survey);
                    }
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }

            FitLayoutToActiveCards();
        }

        /// <summary>
        /// Clone UIActionButton slots so a large scrollable hand is never truncated by
        /// the original 9–12 prefab buttons.
        /// </summary>
        private void EnsureHandSlotCount(int needed)
        {
            if (actionButtons == null || actionButtons.Length == 0) return;
            int target = Mathf.Max(needed, actionButtons.Length);
            // Cap runaway clones; handSize is 24.
            target = Mathf.Min(target, 32);
            if (actionButtons.Length >= target) return;

            var template = actionButtons[0];
            if (template == null) return;

            var list = new List<UIActionButton>(actionButtons);
            while (list.Count < target)
            {
                var clone = Instantiate(template, transform);
                clone.name = list.Count.ToString();
                clone.gameObject.SetActive(true);
                list.Add(clone);
            }
            actionButtons = list.ToArray();
            ApplyPlayingCardLayout();
        }

        static int PrologueMineralsOnHand()
        {
            var colony = GameDevTV.RTS.Prologue.PrologueController.Instance?.Colony;
            if (!GameDevTV.RTS.Prologue.PrologueController.IsActive || colony == null) return 0;
            return colony.GetStock(GameDevTV.RTS.Prologue.PrologueResourceId.Minerals);
        }

        static void ApplyPrologueMineralLock(UIActionButton button, BlueprintCardSO card)
        {
            if (!GameDevTV.RTS.Prologue.PrologueController.IsActive || button == null) return;
            int cost = GameDevTV.RTS.Prologue.PrologueCardMap.MineralCost(card);
            button.ApplyPrologueMinerals(cost, PrologueMineralsOnHand());
        }

#if UNITY_EDITOR
        /// <summary>CLI bot: start placement ghost without UI click (works when button is non-interactable).</summary>
        public static bool EditorPlaytestSelectHandCard(string nameContains)
        {
            if (string.IsNullOrEmpty(nameContains) || CardDeckController.Instance == null) return false;
            var hand = CardDeckController.Instance.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card == null) continue;
                string cardName = card.cardName ?? card.name ?? "";
                bool match = cardName.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!match && card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
                {
                    string b = unlock.buildingToUnlock.Name ?? "";
                    match = b.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0
                        || HandCardMatchesStepAlias(nameContains, unlock.buildingToUnlock.Name);
                }
                if (!match && card is ScoutingCardSO)
                    match = nameContains.IndexOf("Survey", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!match) continue;

                if (card is UnlockBuildingCardSO build && build.buildingToUnlock != null)
                {
                    instance?.PlayBuildingCard(i, build.buildingToUnlock);
                    return GameDevTV.RTS.Player.PlayerInput.PlacementGhostActive;
                }

                if (GameDevTV.RTS.Prologue.PrologueController.IsActive)
                {
                    CardDeckController.Instance.PlayCard(i);
                    return true;
                }
            }

            return false;
        }

        static bool HandCardMatchesStepAlias(string stepKey, string buildingName)
        {
            if (string.IsNullOrEmpty(buildingName)) return false;
            if (stepKey.IndexOf("Atmosphere", System.StringComparison.OrdinalIgnoreCase) >= 0
                && (buildingName.IndexOf("Atmosphere Processor", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || buildingName.IndexOf("Oxygen Processor", System.StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            if ((stepKey.IndexOf("Windmill", System.StringComparison.OrdinalIgnoreCase) >= 0
                 || stepKey.IndexOf("Wind", System.StringComparison.OrdinalIgnoreCase) >= 0)
                && (buildingName.IndexOf("Wind", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || buildingName.IndexOf("Windmill", System.StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            return false;
        }
#endif

        public void PlayBuildingCard(int cardIndex, BuildingSO building)
        {
            if (building == null) return;

            // Prologue places via ice-range Preview — skip Acts power / materials / geology click gates.
            bool prologue = GameDevTV.RTS.Prologue.PrologueController.IsActive;
            if (prologue)
            {
                int cost = GameDevTV.RTS.Prologue.PrologueCardMap.MineralCost(building);
                int have = PrologueMineralsOnHand();
                if (cost > 0 && have < cost)
                {
                    ExplorationManager.NotifyPlacementFailed(
                        $"Need {cost} minerals to build {building.Name} (have {have}).",
                        ResolveErrorHintPosition());
                    return;
                }
            }
            bool colonyActs = ColonyActManager.Instance != null;
            if (!prologue && !colonyActs && !PowerGridManager.CanPlayBuildingForPower(building, owner))
            {
                float gen = PowerGridManager.GetBoardPowerGeneration(owner);
                float used = PowerGridManager.GetBoardPowerUpkeep(owner);
                float need = PowerGridManager.GetBuildingPowerUpkeep(building);
                float free = gen - used;
                Vector3 hint = ResolveErrorHintPosition();
                ExplorationManager.NotifyPlacementFailed(
                    $"Not enough spare power for {building.Name} " +
                    $"(needs +{need:0.#}; {gen:0.#} gen / {used:0.#} used / {free:0.#} free). " +
                    "Build more Solar or demolish consumers.",
                    hint);
                return;
            }

            if (!prologue)
            {
                int matCost = ReservedSiteBuildUtility.GetMaterialsCost(building);
                int haveMats = Supplies.Materials != null && Supplies.Materials.TryGetValue(owner, out int m) ? m : 0;
                if (matCost > 0 && haveMats < matCost)
                {
                    ExplorationManager.NotifyPlacementFailed(
                        $"Need {matCost} Materials to place {building.Name} (have {haveMats}).",
                        ResolveErrorHintPosition());
                    return;
                }

                // Geology-gated cards: must already have a matching sector available.
                if (DiscoverySystem.TryGetRequiredSectorFeature(building, out var needFeature)
                    && !DiscoverySystem.IsSectorFeatureDiscovered(needFeature))
                {
                    ExplorationManager.NotifyPlacementFailed(
                        $"Discover a {DiscoverySystem.DescribeSectorFeature(needFeature)} sector before placing {building.Name}.",
                        ResolveErrorHintPosition());
                    return;
                }
            }

            if (!prologue && ColonyActManager.Instance != null)
            {
                int weekCost = CardDeckController.GetWeekCost(building);
                if (weekCost > ColonyActManager.Instance.WeeksRemaining)
                {
                    ExplorationManager.NotifyPlacementFailed(
                        $"Need {weekCost} week{(weekCost == 1 ? "" : "s")} to play {building.Name} (have {ColonyActManager.Instance.WeeksRemaining}).",
                        ResolveErrorHintPosition());
                    return;
                }
            }

            if (BuildingSiteRegistry.IsCommandPostBuilding(building)
                && !SectorColonization.TryResolveCommandPostTargetSector(
                    Camera.main != null ? Camera.main.transform.position : Vector3.zero,
                    out _,
                    out string cpFail))
            {
                ExplorationManager.NotifyPlacementFailed(
                    cpFail ?? "Q/E to an unclaimed sector, then place the Command Post there.",
                    ResolveErrorHintPosition());
                return;
            }

            // Mines: player ghost-places onto a discovered deposit (no auto-build).
            if (BuildingSiteRegistry.IsMineBuilding(building))
            {
                if (!DiscoverySystem.HasDiscoveredMineDeposit(building))
                {
                    DiscoverySystem.TryGetMineResourceType(building, out string type);
                    ExplorationManager.NotifyPlacementFailed(
                        $"No discovered {type ?? "resource"} deposit yet. Survey first, then place {building.Name} on that pad.",
                        ResolveErrorHintPosition());
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

        private static Vector3 ResolveErrorHintPosition()
        {
            if (Camera.main != null && Mouse.current != null)
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                Ray ray = Camera.main.ScreenPointToRay(screen);
                if (Physics.Raycast(ray, out RaycastHit hit, 5000f, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    return hit.point;
            }

            var camTarget = GameObject.Find("Camera Target");
            if (camTarget != null) return camTarget.transform.position + Vector3.up * 2f;
            return Vector3.zero;
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
