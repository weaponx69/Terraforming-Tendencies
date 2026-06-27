using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.TechTree
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Card Deck", menuName = "Tech Tree/Card Deck", order = 201)]
    public class CardDeckSO : ScriptableObject
    {
        [Header("Master Card List")]
        [Inspectable]
        [field: SerializeField] public List<CardSO> AllCards { get; set; } = new();

        [Header("Draw Settings")]
        [Inspectable]
        [field: SerializeField] public int HandSize { get; set; } = 5;

        [Inspectable]
        [field: SerializeField] public int MaxHandSize { get; set; } = 7;

        [Tooltip("Materials cost to draw a new hand.")]
        [Inspectable]
        [field: SerializeField] public int DrawCost { get; set; } = 50;

        [Header("Lifecycle")]
        [Inspectable]
        [field: SerializeField] public bool RefreshOnNewGeneration { get; set; } = true;

        /// <summary>
        /// Builds the draw pool for the given owner.
        /// Filters to cards whose WrappedUpgrade is unlocked in the tech tree
        /// and not yet researched.
        /// </summary>
        public List<CardSO> BuildDrawPool(Owner owner)
        {
            if (AllCards == null || AllCards.Count == 0) return new List<CardSO>();

            return AllCards
                .Where(card => card != null && card.WrappedUpgrade != null)
                .Where(card =>
                {
                    var unlockable = card.WrappedUpgrade;
                    if (unlockable.TechTree == null) return false;
                    if (!unlockable.TechTree.IsUnlocked(owner, unlockable)) return false;
                    if (unlockable.TechTree.IsResearched(owner, unlockable)) return false;
                    return true;
                })
                .ToList();
        }
    }
}