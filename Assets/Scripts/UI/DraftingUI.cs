using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Full-screen draft overlay that presents the player with Blueprint Cards to choose from.
    ///
    /// Wire in Inspector:
    ///   - overlayPanel     : root GameObject that is hidden by default
    ///   - canvasGroup      : on overlayPanel for fade animations
    ///   - cardContainer    : HorizontalLayoutGroup that holds the card slots
    ///   - cardSlotPrefab   : a prefab with CardSlotUI component (see below)
    ///   - fadeDuration     : seconds for fade in/out (default 0.4)
    ///   - roundLabel       : (optional) TextMeshProUGUI showing "Round X – Choose a Blueprint"
    /// </summary>
    public class DraftingUI : MonoBehaviour
    {
        /// <summary>True while the drafting overlay panel is visible.</summary>
        public bool IsOverlayVisible => overlayPanel != null && overlayPanel.activeInHierarchy;

        [Header("Overlay")]
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.4f;

        [Header("Cards")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardSlotPrefab;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI roundLabel;

        private List<BlueprintCardSO> currentHand = new();
        private int draftRound = 0;
        private List<GameObject> spawnedSlots = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            CardDeckController.OnDraftStarted += HandleDraftStarted;
        }

        private void OnDisable()
        {
            CardDeckController.OnDraftStarted -= HandleDraftStarted;
        }

        private void Start()
        {
            if (overlayPanel != null)
                overlayPanel.SetActive(false);
        }

        // ── Handlers ───────────────────────────────────────────────────────────────

        private void HandleDraftStarted(List<BlueprintCardSO> hand)
        {
            currentHand = hand;
            draftRound++;

            ShowPanel(hand);
        }

        // ── UI Logic ───────────────────────────────────────────────────────────────

        private void ShowPanel(List<BlueprintCardSO> hand)
        {
            // Clear old card slots
            foreach (var slot in spawnedSlots)
                Destroy(slot);
            spawnedSlots.Clear();

            // Update round label
            if (roundLabel != null)
                roundLabel.SetText($"Round {draftRound} — Choose a Blueprint");

            // Spawn a card slot for each card in the hand
            foreach (var card in hand)
            {
                if (cardSlotPrefab == null || cardContainer == null) break;

                GameObject slotGO = Instantiate(cardSlotPrefab, cardContainer);
                spawnedSlots.Add(slotGO);

                if (slotGO.TryGetComponent(out CardSlotUI slot))
                {
                    slot.Initialize(card, OnCardChosen);
                }
            }

            if (overlayPanel != null)
                overlayPanel.SetActive(true);

            if (canvasGroup != null)
                StartCoroutine(FadeIn());
        }

        private void OnCardChosen(BlueprintCardSO chosen)
        {
            StartCoroutine(FadeOutThenClose(chosen));
        }

        private IEnumerator FadeOutThenClose(BlueprintCardSO chosen)
        {
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }

            if (overlayPanel != null)
                overlayPanel.SetActive(false);

            // Commit the selection to the deck controller (applies card effect + unpauses)
            if (CardDeckController.Instance != null)
                CardDeckController.Instance.SelectCard(chosen, currentHand);
        }

        private IEnumerator FadeIn()
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
    }
}
