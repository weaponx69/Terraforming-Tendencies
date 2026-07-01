using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Player
{
    [IncludeInSettings(true)]
    public class CardDeckManager : MonoBehaviour
    {
        public static CardDeckManager Instance { get; private set; }

        [Header("Deck Configuration")]
        [field: SerializeField] public CardDeckSO DeckSO { get; set; }

        [Header("Runtime State")]
        [field: SerializeField] public List<CardSO> DrawPool { get; private set; } = new();

        [field: SerializeField] public List<CardSO> Hand { get; private set; } = new();

        [field: SerializeField] public List<CardSO> DiscardPile { get; private set; } = new();

        /// <summary>Fired when the hand changes.</summary>
        public event Action<List<CardSO>> OnHandChanged;
        public event Action<CardSO> OnCardPlayed;
        public event Action OnDeckRefreshed;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            GenerationManager.OnGenerationStarted += HandleGenerationStarted;
            GenerationManager.OnGenerationEnded += HandleGenerationEnded;
        }

        private void OnDisable()
        {
            GenerationManager.OnGenerationStarted -= HandleGenerationStarted;
            GenerationManager.OnGenerationEnded -= HandleGenerationEnded;
        }

        private void HandleGenerationStarted(int generation, int maxGenerations)
        {
            if (DeckSO != null && DeckSO.RefreshOnNewGeneration)
            {
                RefreshDeck();
            }
        }

        private void HandleGenerationEnded(int earnedTC, int totalTC)
        {
            Hand.Clear();
            DiscardPile.Clear();
            OnHandChanged?.Invoke(new List<CardSO>(Hand));
        }

        /// <summary>
        /// Rebuilds the draw pool from the tech tree and draws a new hand.
        /// </summary>
        public void RefreshDeck()
        {
            if (DeckSO == null) return;

            BuildDrawPool();
            DrawHand();
            OnDeckRefreshed?.Invoke();
        }

        /// <summary>
        /// Builds the draw pool by filtering AllCards against the tech tree.
        /// </summary>
        public void BuildDrawPool()
        {
            if (DeckSO == null) return;
            DrawPool = DeckSO.BuildDrawPool(Owner.Player1);
        }

        /// <summary>
        /// Draws a new hand of cards using weighted random selection.
        /// Costs DrawCost Materials. Does nothing if hand is already at MaxHandSize.
        /// </summary>
        public void DrawHand()
        {
            if (DeckSO == null) return;

            // Check draw cost
            if (Supplies.Materials.TryGetValue(Owner.Player1, out int currentMats) && currentMats < DeckSO.DrawCost)
            {
                Debug.Log($"[CardDeckManager] Not enough Materials to draw. Need {DeckSO.DrawCost}, have {currentMats}.");
                return;
            }

            // Deduct draw cost
            if (Supplies.Materials.ContainsKey(Owner.Player1))
            {
                Supplies.Materials[Owner.Player1] -= DeckSO.DrawCost;
                Supplies.RaiseMaterialsChanged(Owner.Player1, Supplies.Materials[Owner.Player1]);
            }

            // Clear current hand into discard
            foreach (var card in Hand)
            {
                if (card != null) DiscardPile.Add(card);
            }
            Hand.Clear();

            // Draw up to HandSize cards
            int cardsToDraw = Mathf.Min(DeckSO.HandSize, DeckSO.MaxHandSize);
            List<CardSO> pool = new(DrawPool);

            for (int i = 0; i < cardsToDraw && pool.Count > 0; i++)
            {
                CardSO picked = PickWeightedRandom(pool);
                if (picked != null)
                {
                    Hand.Add(picked);
                    pool.Remove(picked);
                }
            }

            OnHandChanged?.Invoke(new List<CardSO>(Hand));
            Debug.Log($"[CardDeckManager] Drew {Hand.Count} cards. Materials remaining: {Supplies.Materials[Owner.Player1]}");
        }

        /// <summary>
        /// Plays a card from hand. Checks unlock status and resources,
        /// fires the unlock event, and applies the direct effect.
        /// </summary>
        public bool PlayCard(CardSO card)
        {
            if (card == null) return false;
            if (!Hand.Contains(card)) return false;
            if (!CanPlayCard(card)) return false;

            // Deduct play cost
            if (Supplies.Materials.ContainsKey(Owner.Player1))
            {
                Supplies.Materials[Owner.Player1] -= card.PlayCost;
                Supplies.RaiseMaterialsChanged(Owner.Player1, Supplies.Materials[Owner.Player1]);
            }

            // Fire unlock event
            if (card.WrappedUpgrade != null)
            {
                Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, card.WrappedUpgrade));
            }

            // Apply direct effect
            ApplyCardEffect(card);

            // Move to discard
            Hand.Remove(card);
            DiscardPile.Add(card);

            OnCardPlayed?.Invoke(card);
            OnHandChanged?.Invoke(new List<CardSO>(Hand));

            Debug.Log($"[CardDeckManager] Played card: {card.CardName}. Materials remaining: {Supplies.Materials[Owner.Player1]}");
            return true;
        }

        /// <summary>
        /// Checks if a card can be played: unlocked in tech tree, not yet researched, and has enough Materials.
        /// </summary>
        public bool CanPlayCard(CardSO card)
        {
            if (card == null) return false;
            if (card.WrappedUpgrade == null) return false;

            // Check tech tree unlock status
            var unlockable = card.WrappedUpgrade;
            if (unlockable.TechTree == null) return false;
            if (!unlockable.TechTree.IsUnlocked(Owner.Player1, unlockable)) return false;
            if (unlockable.TechTree.IsResearched(Owner.Player1, unlockable)) return false;

            // Check play cost
            if (Supplies.Materials.TryGetValue(Owner.Player1, out int currentMats))
            {
                return currentMats >= card.PlayCost;
            }

            return false;
        }

        /// <summary>
        /// Applies the direct supply effect of a card.
        /// </summary>
        public void ApplyCardEffect(CardSO card)
        {
            if (card == null || card.EffectType == CardEffectType.None) return;

            Owner owner = Owner.Player1;

            switch (card.EffectType)
            {
                case CardEffectType.Biomass:
                    Supplies.UpdateBiomass(owner, Supplies.Biomass.GetValueOrDefault(owner, 0f) + card.EffectAmount);
                    break;
                case CardEffectType.Oxygen:
                    Supplies.UpdateOxygen(owner, Supplies.Oxygen.GetValueOrDefault(owner, 0f) + card.EffectAmount);
                    break;
                case CardEffectType.Power:
                    Supplies.UpdatePower(owner, Supplies.Power.GetValueOrDefault(owner, 0f) + card.EffectAmount);
                    break;
                case CardEffectType.Population:
                    Supplies.UpdatePopulation(owner, Supplies.Population.GetValueOrDefault(owner, 0) + (int)card.EffectAmount);
                    break;
                case CardEffectType.Materials:
                    if (Supplies.Materials.ContainsKey(owner))
                    {
                        Supplies.Materials[owner] += (int)card.EffectAmount;
                        Supplies.RaiseMaterialsChanged(owner, Supplies.Materials[owner]);
                    }
                    break;
                case CardEffectType.Temperature:
                    Supplies.UpdateTemperature(owner, Supplies.Temperature.GetValueOrDefault(owner, -60f) + card.EffectAmount);
                    break;
                case CardEffectType.Atmosphere:
                    Supplies.UpdateAtmosphere(owner, Supplies.Atmosphere.GetValueOrDefault(owner, 0.01f) + card.EffectAmount);
                    break;
                case CardEffectType.Water:
                    Supplies.UpdateWater(owner, Supplies.Water.GetValueOrDefault(owner, 0f) + card.EffectAmount);
                    break;
                case CardEffectType.CommandPost:
                    // CommandPost milestone is counted from active buildings, not a supply value.
                    // The unlock event from the card handles the building unlock.
                    break;
            }
        }

        private CardSO PickWeightedRandom(List<CardSO> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var card in pool)
            {
                if (card != null) totalWeight += Mathf.Max(0.01f, card.DrawWeight);
            }

            if (totalWeight <= 0f) return pool[UnityEngine.Random.Range(0, pool.Count)];

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var card in pool)
            {
                if (card == null) continue;
                cumulative += Mathf.Max(0.01f, card.DrawWeight);
                if (roll <= cumulative) return card;
            }

            return pool[pool.Count - 1];
        }
    }
}