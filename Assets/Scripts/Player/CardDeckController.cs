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
        /// 1. Clears the hand.
        /// 2. Direct-adds the three guaranteed starter cards (Command Post, Solar Panel,
        ///    Mining Drone) using hand.Add() so they are seated at indices 0, 1, 2.
        /// 3. Fills the remaining slots (3 through 9) with random cards from the draw pile.
        /// No shifting or eviction occurs.
        /// </summary>
        public void RebuildDeck()
        {
            ShuffleDeck();

            // 1. Clear the hand
            hand.Clear();

            // 2. Find the three guaranteed starter cards in the master deck
            BlueprintCardSO cmdPostCard = null;
            BlueprintCardSO droneCard = null;
            BlueprintCardSO solarCard = null;
            foreach (var c in masterDeck)
            {
                if (c is UnlockBuildingCardSO unlock)
                {
                    if (unlock.buildingToUnlock != null && unlock.buildingToUnlock.Name == "Command Post")
                        cmdPostCard = c;
                    if (unlock.buildingToUnlock != null && unlock.buildingToUnlock.Name == "Solar Panel")
                        solarCard = c;
                }
                if (c is SpawnUnitCardSO spawn)
                {
                    if (spawn.cardName == "Mining Drone")
                        droneCard = c;
                }
            }

            cmdPostCard ??= EnsureStarterCard<UnlockBuildingCardSO>("Cards/CommandPostCard");
            solarCard ??= EnsureStarterCard<UnlockBuildingCardSO>("Cards/SolarPanelCard");
            droneCard ??= EnsureStarterCard<SpawnUnitCardSO>("Cards/MiningDroneCard");

            // 3. Direct-add the guaranteed cards in the desired slot order.
            //    Command Post → index 0, Solar Panel → index 1, Mining Drone → index 2.
            if (cmdPostCard != null) { hand.Add(cmdPostCard); drawPile.Remove(cmdPostCard); }
            if (solarCard != null)   { hand.Add(solarCard);   drawPile.Remove(solarCard); }
            if (droneCard != null)   { hand.Add(droneCard);   drawPile.Remove(droneCard); }

            Debug.Log($"[CardDeckController] Seeded {hand.Count} guaranteed starter(s). " +
                      $"CmdPost={(cmdPostCard != null ? "YES" : "NULL")} " +
                      $"Solar={(solarCard != null ? "YES" : "NULL")} " +
                      $"Drone={(droneCard != null ? "YES" : "NULL")}");

            // 4. Fill remaining slots (7 cards → indices 3 through 9)
            FillHand();

            OnHandChanged?.Invoke();

            // Force the bottom bar to refresh so the cards appear immediately
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
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
        /// Discards the entire hand and draws a fresh one. 
        /// Triggered during the Draw phase of turn resolution.
        /// </summary>
        public void DiscardHandAndDrawFresh()
        {
            discardPile.AddRange(hand);
            hand.Clear();
            FillHand();
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
            if (!played.IsGateMet())
            {
                Debug.LogWarning($"[CardDeckController] Card '{played.cardName}' cannot be played yet because its requirements are not met.");
                return;
            }

            if (!played.CanApply())
            {
                if (played is ScoutingCardSO)
                {
                    ExplorationManager.NotifyExplorationFailed($"Cannot play '{played.cardName}' right now.");
                }
                else
                {
                    Debug.LogWarning($"[CardDeckController] Card '{played.cardName}' cannot be played right now.");
                }
                return;
            }

            Debug.Log($"[CardDeckController] Playing card: '{played.cardName}' (index {handIndex})");

            // Register card's hazards if it has any
            if (played.HazardEventPrefabs != null)
            {
                foreach (var hazard in played.HazardEventPrefabs)
                {
                    if (hazard != null)
                    {
                        NaturalEventManager.RegisterHazard(hazard);
                    }
                }
            }

            // Apply the card's effect
            played.Apply();

            // Notify GameFlowManager that an action was taken
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.PlayerActed();
            }

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

        /// <summary>
        /// Explore a specific frontier node by consuming a scouting card from hand plus energy.
        /// </summary>
        public bool TryExploreAtNode(SectorNode node, int sectorIndex)
        {
            if (node == null || ExplorationManager.Instance == null) return false;

            if (!ExplorationManager.Instance.IsValidExploreTarget(node))
            {
                ExplorationManager.NotifyExplorationFailed("That node is not a valid exploration target.");
                return false;
            }

            int handIndex = FindExplorationScoutingCardIndex();
            if (handIndex < 0)
            {
                ExplorationManager.NotifyExplorationFailed("Need an Orbital Scan or Survey Drone card to explore.");
                return false;
            }

            BlueprintCardSO scoutingCard = hand[handIndex];
            if (!scoutingCard.CanApply())
            {
                ExplorationManager.NotifyExplorationFailed($"Cannot play '{scoutingCard.cardName}' right now.");
                return false;
            }

            hand.RemoveAt(handIndex);
            discardPile.Add(scoutingCard);

            if (!ExplorationManager.Instance.TryExploreNode(node, sectorIndex))
            {
                hand.Add(scoutingCard);
                discardPile.Remove(scoutingCard);
                return false;
            }

            FillHand();
            GameFlowManager.Instance?.PlayerActed();
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
            OnHandChanged?.Invoke();
            return true;
        }

        private int FindExplorationScoutingCardIndex()
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is ScoutingCardSO scouting &&
                    (scouting.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan ||
                     scouting.scoutingType == ScoutingCardSO.ScoutingType.SurveyDrone))
                {
                    return i;
                }
            }

            return -1;
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

        private T EnsureStarterCard<T>(string resourcePath) where T : BlueprintCardSO
        {
            T card = Resources.Load<T>(resourcePath);
            if (card == null)
            {
                throw new InvalidOperationException(
                    $"[CardDeckController] Required opening card is missing at Resources/{resourcePath}. " +
                    "The opening hand must contain Command Post, Mining Drone, and Solar Panel so the player can establish a base, deploy a builder, and generate power. " +
                    "Restore the missing asset or correct its Resources path before starting the game.");
            }

            if (!masterDeck.Contains(card)) masterDeck.Add(card);
            if (!drawPile.Contains(card)) drawPile.Add(card);
            return card;
        }
    }
}
