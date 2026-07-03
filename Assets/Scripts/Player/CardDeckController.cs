using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;

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
        public List<BlueprintCardSO> MasterDeck => masterDeck;
        [SerializeField] private int handSize = 4;

        private List<BlueprintCardSO> drawPile = new();
        private List<BlueprintCardSO> discardPile = new();

        /// <summary>Fired when the draft phase begins. Carries the offered hand.</summary>
        public static event Action<List<BlueprintCardSO>> OnDraftStarted;

        private void Awake()
        {
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

        public void DrawCard()
        {
            if (masterDeck == null || masterDeck.Count == 0)
            {
                Debug.LogWarning($"[CardDeckController] DrawCard called but masterDeck is {(masterDeck == null ? "NULL" : "empty")}! Has {masterDeck?.Count} cards.");
                return;
            }

            Debug.Log($"[CardDeckController] DrawCard called. masterDeck has {masterDeck.Count} cards total.");

            // Filter only by climate gates — do NOT filter by unlock state.
            // Cards are consumable actions; drawing them should always work
            // regardless of which buildings are currently unlocked.
            var validCards = masterDeck.Where(c => c.IsGateMet()).ToList();

            Debug.Log($"[CardDeckController] After climate gate filtering: {validCards.Count} valid cards.");
            foreach (var c in validCards)
            {
                Debug.Log($"[CardDeckController]   Valid card: '{c.cardName}' (type: {c.GetType().Name})");
            }

            if (validCards.Count == 0)
            {
                Debug.LogWarning("[CardDeckController] No valid cards left to draw! All cards failed climate gates.");
                return;
            }

            var chosen = validCards[UnityEngine.Random.Range(0, validCards.Count)];
            Debug.Log($"[CardDeckController] DRAW RESULT: '{chosen.cardName}' (type: {chosen.GetType().Name})");
            chosen.Apply();

            // Refresh UI actions bar
            GameDevTV.RTS.EventBus.Bus<GameDevTV.RTS.Events.UpgradeResearchedEvent>.Raise(Owner.Player1, new GameDevTV.RTS.Events.UpgradeResearchedEvent(Owner.Player1, null));
        }

        /// <summary>Trigger a draft immediately (use from Inspector button or milestone code).</summary>
        public void TriggerDraft()
        {
            var hand = GetCuratedHand();
            if (hand == null || hand.Count == 0) return;

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
            // Chosen card is consumed
            discardPile.Add(chosen);

            BlueprintDraftManager.CompleteDraft(chosen); // Applies effect + unpauses + fires OnDraftCompleted
        }

        // ── Draft Curation ──────────────────────────────────────────────────────────

        /// <summary>
        /// Build a curated draft hand:
        /// - 3 cards from the master deck, filtered by what's relevant in explored sectors
        /// - Guarantees at least 1 scouting card if sectors remain locked
        /// - Emergency Caches is always a 4th extra option
        /// </summary>
        private List<BlueprintCardSO> GetCuratedHand()
        {
            if (masterDeck == null || masterDeck.Count == 0) return null;

            // Find the Emergency Caches card (guaranteed 4th option)
            BlueprintCardSO emergencyCaches = masterDeck.FirstOrDefault(c =>
                c is ScoutingCardSO s && s.scoutingType == ScoutingCardSO.ScoutingType.EmergencyCaches);

            // Filter the deck to only include valid cards for current game state
            var curatedPool = masterDeck
                .Where(c => c != emergencyCaches) // Exclude emergency card from normal pool
                .Where(c => c.IsGateMet())        // Climate gates, discovery prerequisites
                .ToList();

            if (curatedPool.Count == 0)
            {
                // Fallback: if nothing is valid, just use emergency caches
                var fallbackHand = new List<BlueprintCardSO>();
                if (emergencyCaches != null) fallbackHand.Add(emergencyCaches);
                return fallbackHand;
            }

            // Shuffle the curated pool
            curatedPool = curatedPool.OrderBy(_ => UnityEngine.Random.value).ToList();

            // Separate scouting cards from other types
            var scoutingCards = curatedPool.Where(c => c is ScoutingCardSO).ToList();
            var otherCards = curatedPool.Where(c => !(c is ScoutingCardSO)).ToList();

            var hand = new List<BlueprintCardSO>();

            // If sectors remain locked, guarantee at least 1 scouting card
            bool hasLockedSectors = SectorManager.Instance != null &&
                                    SectorManager.Instance.GetNextLockedSectorIndex() >= 0;

            if (hasLockedSectors && scoutingCards.Count > 0)
            {
                hand.Add(scoutingCards[0]);
                scoutingCards.RemoveAt(0);
            }

            // Fill remaining slots from the mixed pool
            var mixedPool = scoutingCards.Concat(otherCards).OrderBy(_ => UnityEngine.Random.value).ToList();
            int slotsRemaining = handSize - hand.Count;
            for (int i = 0; i < slotsRemaining && i < mixedPool.Count; i++)
            {
                hand.Add(mixedPool[i]);
            }

            // Add Emergency Caches as guaranteed 4th option
            if (emergencyCaches != null && !hand.Contains(emergencyCaches))
            {
                hand.Add(emergencyCaches);
            }

            return hand;
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
