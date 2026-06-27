using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.TechTree
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Card", menuName = "Tech Tree/Card", order = 200)]
    public class CardSO : ScriptableObject
    {
        [Header("Identity")]
        [Inspectable]
        [field: SerializeField] public string CardName { get; set; } = "New Card";

        [Inspectable]
        [field: SerializeField] public Sprite Icon { get; set; }

        [Inspectable]
        [field: SerializeField] public string Description { get; set; } = "";

        [Header("Unlock Link")]
        [Tooltip("The UpgradeSO this card unlocks. Must be set for the card to have an unlock effect.")]
        [Inspectable]
        [field: SerializeField] public UpgradeSO WrappedUpgrade { get; set; }

        [Header("Draw Properties")]
        [Inspectable]
        [field: SerializeField] public CardRarity Rarity { get; set; } = CardRarity.Common;

        [Inspectable]
        [field: SerializeField] public float DrawWeight { get; set; } = 1f;

        [Header("Play Cost")]
        [Tooltip("Materials cost to play this card.")]
        [Inspectable]
        [field: SerializeField] public int PlayCost { get; set; } = 50;

        [Header("Direct Effect")]
        [Tooltip("Supply modified immediately when this card is played.")]
        [Inspectable]
        [field: SerializeField] public CardEffectType EffectType { get; set; } = CardEffectType.None;

        [Inspectable]
        [field: SerializeField] public float EffectAmount { get; set; } = 0f;
    }
}