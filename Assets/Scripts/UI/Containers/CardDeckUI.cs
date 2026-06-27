using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using System.Collections.Generic;

namespace GameDevTV.RTS.UI.Containers
{
    public class CardDeckUI : MonoBehaviour
    {
        [Header("Card Display")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform handContainer;

        [Header("Controls")]
        [SerializeField] private Button drawButton;
        [SerializeField] private TextMeshProUGUI drawCostText;
        [SerializeField] private TextMeshProUGUI materialText;

        [Header("Panel")]
        [SerializeField] private GameObject deckPanel;

        private void OnEnable()
        {
            if (drawButton != null)
            {
                drawButton.onClick.RemoveAllListeners();
                drawButton.onClick.AddListener(OnDrawClicked);
            }

            if (CardDeckManager.Instance != null)
            {
                CardDeckManager.Instance.OnHandChanged += HandleHandChanged;
                CardDeckManager.Instance.OnCardPlayed += HandleCardPlayed;
            }

            Supplies.OnMaterialsChanged += HandleMaterialsChanged;

            UpdateMaterialsDisplay();
            RefreshHand();
        }

        private void OnDisable()
        {
            if (drawButton != null)
            {
                drawButton.onClick.RemoveListener(OnDrawClicked);
            }

            if (CardDeckManager.Instance != null)
            {
                CardDeckManager.Instance.OnHandChanged -= HandleHandChanged;
                CardDeckManager.Instance.OnCardPlayed -= HandleCardPlayed;
            }

            Supplies.OnMaterialsChanged -= HandleMaterialsChanged;
        }

        private void OnDrawClicked()
        {
            if (CardDeckManager.Instance != null)
            {
                CardDeckManager.Instance.DrawHand();
            }
        }

        private void HandleHandChanged(List<CardSO> hand)
        {
            RefreshHand();
        }

        private void HandleCardPlayed(CardSO card)
        {
            UpdateMaterialsDisplay();
        }

        private void HandleMaterialsChanged(Owner owner, int value)
        {
            UpdateMaterialsDisplay();
        }

        private void RefreshHand()
        {
            if (handContainer == null || cardPrefab == null) return;

            // Clear existing cards
            foreach (Transform child in handContainer)
            {
                Destroy(child.gameObject);
            }

            if (CardDeckManager.Instance == null) return;

            // Spawn card UIs
            foreach (var card in CardDeckManager.Instance.Hand)
            {
                if (card == null) continue;
                GameObject cardObj = Instantiate(cardPrefab, handContainer);
                if (cardObj.TryGetComponent<UI.Components.CardUI>(out var cardUI))
                {
                    cardUI.Setup(card);
                }
            }

            UpdateDrawButton();
        }

        private void UpdateDrawButton()
        {
            if (drawButton != null && CardDeckManager.Instance != null && CardDeckManager.Instance.DeckSO != null)
            {
                bool canDraw = true;
                if (Supplies.Materials.TryGetValue(Owner.Player1, out int mats))
                {
                    canDraw = mats >= CardDeckManager.Instance.DeckSO.DrawCost;
                }

                drawButton.interactable = canDraw;

                if (drawCostText != null)
                    drawCostText.text = $"Draw ({CardDeckManager.Instance.DeckSO.DrawCost})";
            }
        }

        private void UpdateMaterialsDisplay()
        {
            if (materialText != null)
            {
                int mats = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                materialText.text = $"Materials: {mats}";
            }
        }

        public void ShowPanel(bool show)
        {
            if (deckPanel != null)
                deckPanel.SetActive(show);
            else
                gameObject.SetActive(show);
        }
    }
}