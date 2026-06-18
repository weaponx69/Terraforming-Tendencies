using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Holds the master deck of Blueprint Cards and drives the draft sequence.
    ///
    /// Wire in Inspector:
    ///   - masterDeck : all BlueprintCardSO assets for this run
    ///   - handSize   : how many cards to offer (default 3)
    ///
    /// The draft is triggered automatically when a sector is unlocked (via SectorManager).
    /// It can also be triggered manually via TriggerDraft() for testing.
    /// </summary>
    public class CardDeckController : MonoBehaviour
    {
        public static CardDeckController Instance { get; private set; }

        [Header("Deck Configuration")]
        [SerializeField] private List<BlueprintCardSO> masterDeck = new();
        [SerializeField] private int handSize = 3;

        private List<BlueprintCardSO> drawPile = new();
        private List<BlueprintCardSO> discardPile = new();

        /// <summary>Fired when the draft phase begins. Carries the offered hand.</summary>
        public static event Action<List<BlueprintCardSO>> OnDraftStarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            SectorManager.OnSectorUnlocked += HandleSectorUnlocked;
        }

        private void OnDisable()
        {
            SectorManager.OnSectorUnlocked -= HandleSectorUnlocked;
        }

        private void Start()
        {
            ShuffleDeck();
        }

        // ── Public API ──────────────────────────────────────────────────────────────

        /// <summary>Trigger a draft immediately (use from Inspector button or milestone code).</summary>
        public void TriggerDraft()
        {
            if (drawPile.Count < handSize) Reshuffle();
            if (drawPile.Count == 0) return;

            var hand = drawPile.Take(handSize).ToList();

            Time.timeScale = 0f;
            OnDraftStarted?.Invoke(hand);
        }

        /// <summary>Called by the UI when the player selects a card.</summary>
        public void SelectCard(BlueprintCardSO chosen, List<BlueprintCardSO> fullHand)
        {
            // Move drawn cards from draw pile to discard
            foreach (var card in fullHand)
            {
                drawPile.Remove(card);
                if (card != chosen)
                    discardPile.Add(card); // Unchosen cards go to discard
            }
            // Chosen card is consumed — optionally move to discard too
            discardPile.Add(chosen);

            BlueprintDraftManager.CompleteDraft(chosen); // Applies effect + unpauses + fires OnDraftCompleted
        }

        // ── Private Helpers ─────────────────────────────────────────────────────────

        private void HandleSectorUnlocked()
        {
            TriggerDraft();
        }

        private void ShuffleDeck()
        {
            drawPile = masterDeck.OrderBy(_ => UnityEngine.Random.value).ToList();
            discardPile.Clear();
        }

        private void Reshuffle()
        {
            // Combine remaining draw pile and discards, then reshuffle
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            drawPile = drawPile.OrderBy(_ => UnityEngine.Random.value).ToList();
        }
    }
}
