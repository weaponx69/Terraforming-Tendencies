using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Manages the player's deck and 5-card hand. The hand is shown in the
    /// bottom action bar. Playing a card removes it and draws a replacement
    /// from the deck. If the deck runs out, the discard pile reshuffles.
    ///
    /// Auto-spawns on scene load — no manual scene setup needed.
    /// </summary>
    public class CardDeckController : MonoBehaviour
    {
        public static CardDeckController Instance { get; private set; }

        [Header("Deck Configuration")]
        [SerializeField] private List<BlueprintCardSO> masterDeck = new();
        public List<BlueprintCardSO> MasterDeck => masterDeck;
        [SerializeField] private int handSize = 10;

        private List<BlueprintCardSO> drawPile = new();
        private List<BlueprintCardSO> discardPile = new();
        private List<BlueprintCardSO> hand = new();

        /// <summary>The player's current hand of cards (max handSize).</summary>
        public IReadOnlyList<BlueprintCardSO> Hand => hand;

        /// <summary>Fired when the hand changes (card played, drawn, etc.).</summary>
        public static event Action OnHandChanged;

        /// <summary>Fired when the draft phase begins. Carries the offered hand.</summary>
        public static event Action<List<BlueprintCardSO>> OnDraftStarted;

        // ── Auto-initialization ──────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            GameObject go = new GameObject("CardDeckController");
            DontDestroyOnLoad(go);
            go.AddComponent<CardDeckController>();
        }

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

        // Don't fill the hand in Start() — BlueprintDraftUI may not have
        // populated the deck yet. Instead, RebuildDeck() is called by
        // BlueprintDraftUI after it finishes InitializeDefaultPool().
        private void Start() { }

        /// <summary>
        /// Called by BlueprintDraftUI after the deck is populated.
        /// Shuffles the deck, fills the hand, and guarantees starting cards.
        /// </summary>
        public void RebuildDeck()
        {
            ShuffleDeck();
            FillHand();
            GuaranteeStartingCards();
        }

        // ── Guaranteed starting cards ───────────────────────────────────────
        // After the initial FillHand, ensure Command Post, Mining Drone, and
        // Solar Panel are in the hand. If not, swap them with the last cards.
        private void GuaranteeStartingCards()
        {
            if (masterDeck == null || masterDeck.Count == 0)
            {
                Debug.LogError("[CardDeckController] GuaranteeStartingCards: masterDeck is empty!");
                return;
            }

            Debug.Log($"[CardDeckController] Searching {masterDeck.Count} cards for guaranteed starters...");

            BlueprintCardSO cmdPostCard = null;
            BlueprintCardSO droneCard = null;
            BlueprintCardSO solarCard = null;
            foreach (var c in masterDeck)
            {
                if (c is UnlockBuildingCardSO unlock)
                {
                    string buildingName = unlock.buildingToUnlock != null ? unlock.buildingToUnlock.Name : "NULL";
                    Debug.Log($"[CardDeckController]   Unlock card: '{unlock.cardName}' -> building '{buildingName}'");
                    // Match "Command Post" exactly — not "Sector Command Center" or other "Command" buildings
                    if (unlock.buildingToUnlock != null && unlock.buildingToUnlock.Name == "Command Post")
                        cmdPostCard = c;
                    if (unlock.buildingToUnlock != null && unlock.buildingToUnlock.Name == "Solar Panel")
                        solarCard = c;
                }
                if (c is SpawnUnitCardSO spawn)
                {
                    Debug.Log($"[CardDeckController]   Spawn card: '{spawn.cardName}'");
                    if (spawn.cardName == "Mining Drone")
                        droneCard = c;
                }
            }

            Debug.Log($"[CardDeckController] Found: cmdPost={(cmdPostCard != null ? cmdPostCard.cardName : "NULL")} drone={(droneCard != null ? droneCard.cardName : "NULL")} solar={(solarCard != null ? solarCard.cardName : "NULL")}");

            // Push each guaranteed card into the hand (at the front)
            EnsureInHand(cmdPostCard);
            EnsureInHand(droneCard);
            EnsureInHand(solarCard);

            Debug.Log($"[CardDeckController] Hand after guarantee: {hand.Count} cards. First={(hand.Count > 0 ? hand[0].cardName : "empty")}");

            OnHandChanged?.Invoke();

            // Force the bottom bar to refresh so the cards appear immediately
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
        }

        private void EnsureInHand(BlueprintCardSO card)
        {
            if (card == null || hand.Contains(card)) return;
            if (hand.Count >= handSize) hand.RemoveAt(hand.Count - 1);
            hand.Insert(0, card);
            drawPile.Remove(card);
        }

        // ── Hand Management ──────────────────────────────────────────────────

        /// <summary>
        /// Fill the hand to handSize by drawing from the draw pile.
        /// If the draw pile runs out, reshuffle the discard pile.
        /// </summary>
        public void FillHand()
        {
            if (masterDeck == null || masterDeck.Count == 0) return;

            while (hand.Count < handSize)
            {
                if (drawPile.Count == 0)
                {
                    if (discardPile.Count == 0) break; // No cards left at all
                    Reshuffle();
                }

                // Filter by climate gates
                var valid = drawPile.Where(c => c != null && c.IsGateMet()).ToList();
                if (valid.Count == 0) break;

                // Move top valid card from drawPile to hand
                BlueprintCardSO drawn = valid[0];
                drawPile.Remove(drawn);
                hand.Add(drawn);
            }

            OnHandChanged?.Invoke();
        }

        /// <summary>
        /// Play the card at the given hand index: apply its effect,
        /// remove from hand, discard it, and draw a replacement.
        /// </summary>
        public void PlayCard(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;
            if (hand[handIndex] == null) return;

            BlueprintCardSO played = hand[handIndex];
            Debug.Log($"[CardDeckController] Playing card: '{played.cardName}' (index {handIndex})");

            // Apply the card's effect
            played.Apply();

            // Move played card to discard
            hand.RemoveAt(handIndex);
            discardPile.Add(played);

            // Draw a replacement
            FillHand();

            // Refresh UI
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
        }

        /// <summary>Old DrawCard kept for backward compatibility — now draws into the hand instead of auto-applying.</summary>
        public void DrawCard()
        {
            FillHand();
        }

        // ── Draft UI ─────────────────────────────────────────────────────────

        /// <summary>Trigger a draft immediately (use from Inspector button or milestone code).</summary>
        public void TriggerDraft()
        {
            var curatedHand = GetCuratedHand();
            if (curatedHand == null || curatedHand.Count == 0) return;

            Time.timeScale = 0f;
            OnDraftStarted?.Invoke(curatedHand);
        }

        /// <summary>Called by the UI when the player selects a card.</summary>
        public void SelectCard(BlueprintCardSO chosen, List<BlueprintCardSO> fullHand)
        {
            foreach (var card in fullHand)
            {
                drawPile.Remove(card);
                if (card != chosen)
                    discardPile.Add(card);
            }
            discardPile.Add(chosen);
            BlueprintDraftManager.CompleteDraft(chosen);
        }

        // ── Draft Curation ───────────────────────────────────────────────────

        private List<BlueprintCardSO> GetCuratedHand()
        {
            if (masterDeck == null || masterDeck.Count == 0) return null;

            BlueprintCardSO emergencyCaches = masterDeck.FirstOrDefault(c =>
                c is ScoutingCardSO s && s.scoutingType == ScoutingCardSO.ScoutingType.EmergencyCaches);

            var curatedPool = masterDeck
                .Where(c => c != emergencyCaches)
                .Where(c => c.IsGateMet())
                .ToList();

            if (curatedPool.Count == 0)
            {
                var fallbackHand = new List<BlueprintCardSO>();
                if (emergencyCaches != null) fallbackHand.Add(emergencyCaches);
                return fallbackHand;
            }

            curatedPool = curatedPool.OrderBy(_ => UnityEngine.Random.value).ToList();
            var scoutingCards = curatedPool.Where(c => c is ScoutingCardSO).ToList();
            var otherCards = curatedPool.Where(c => !(c is ScoutingCardSO)).ToList();

            var hand = new List<BlueprintCardSO>();

            bool hasLockedSectors = SectorManager.Instance != null &&
                                    SectorManager.Instance.GetNextLockedSectorIndex() >= 0;

            if (hasLockedSectors && scoutingCards.Count > 0)
            {
                hand.Add(scoutingCards[0]);
                scoutingCards.RemoveAt(0);
            }

            var mixedPool = scoutingCards.Concat(otherCards).OrderBy(_ => UnityEngine.Random.value).ToList();
            int slotsRemaining = handSize - hand.Count;
            for (int i = 0; i < slotsRemaining && i < mixedPool.Count; i++)
            {
                hand.Add(mixedPool[i]);
            }

            if (emergencyCaches != null && !hand.Contains(emergencyCaches))
            {
                hand.Add(emergencyCaches);
            }

            return hand;
        }

        // ── Private Helpers ──────────────────────────────────────────────────

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
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            drawPile = drawPile.OrderBy(_ => UnityEngine.Random.value).ToList();
        }
    }
}
