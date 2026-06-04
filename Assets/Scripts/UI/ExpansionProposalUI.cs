using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    public class ExpansionProposalUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private UnityEngine.UI.Slider timerSlider;
        [SerializeField] private Transform proposalContainer;
        [SerializeField] private GameObject proposalEntryPrefab;
        [SerializeField] private UnityEngine.UI.Button cancelButton;

        private float currentTimer;
        private float maxTimer;
        private List<GameObject> activeEntries = new List<GameObject>();

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
            
            if (GreedyAIController.Instance != null)
            {
                GreedyAIController.Instance.OnExpansionProposed += ShowProposals;
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(() =>
                {
                    if (GreedyAIController.Instance != null) GreedyAIController.Instance.DeclineProposal();
                    Hide();
                });
            }
        }

        private void OnDestroy()
        {
            if (GreedyAIController.Instance != null)
            {
                GreedyAIController.Instance.OnExpansionProposed -= ShowProposals;
            }
        }

        private void Update()
        {
            // Packages persist until the player decides (Select or Cancel) — AFK friendly.
            // No countdown / auto-selection: nothing is purchased without an explicit choice.
        }

        private void ShowProposals(List<ExpansionProposal> proposals, float duration)
        {
            maxTimer = duration;
            currentTimer = duration;

            // No countdown anymore — the offer waits for the player.
            if (timerText != null) timerText.text = "Choose a package, or Cancel to keep saving resources.";
            if (timerSlider != null) timerSlider.gameObject.SetActive(false);

            // Clear old entries
            foreach (var entry in activeEntries) Destroy(entry);
            activeEntries.Clear();

            // Populate new entries
            foreach (var prop in proposals)
            {
                GameObject entry = Instantiate(proposalEntryPrefab, proposalContainer);
                activeEntries.Add(entry);
                
                var entryScript = entry.GetComponent<ProposalEntry>();
                if (entryScript != null)
                {
                    entryScript.Setup(prop, () => {
                        GreedyAIController.Instance.AcceptProposal(prop);
                        Hide();
                    });
                }
            }

            panel.SetActive(true);
        }

        private void Hide()
        {
            panel.SetActive(false);
        }
    }
}
