using UnityEngine;
using TMPro;
using UnityEngine.UI;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI.Containers
{
    public class GenerationSummaryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI resultsText;
        [SerializeField] private Button nextGenerationButton;

        private void OnEnable()
        {
            GenerationManager.OnGenerationEnded += ShowSummary;
            if (nextGenerationButton != null)
            {
                nextGenerationButton.onClick.AddListener(OnNextClicked);
            }
        }

        private void OnDisable()
        {
            GenerationManager.OnGenerationEnded -= ShowSummary;
            if (nextGenerationButton != null)
            {
                nextGenerationButton.onClick.RemoveListener(OnNextClicked);
            }
        }

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void ShowSummary(int earnedTC, int totalTC)
        {
            if (panel != null) panel.SetActive(true);

            if (GenerationManager.Instance != null)
            {
                int current = GenerationManager.Instance.CurrentGeneration;
                int max = GenerationManager.Instance.MaxGenerations;
                
                if (titleText != null)
                {
                    titleText.text = $"Generation {current} of {max} Complete!";
                }

                if (resultsText != null)
                {
                    resultsText.text = $"Map Depleted!\n\nUnused materials liquidated for: {earnedTC} TC\nTotal TC Available: {totalTC} TC";
                }
            }
        }

        private void OnNextClicked()
        {
            if (panel != null) panel.SetActive(false);
            if (GenerationManager.Instance != null)
            {
                GenerationManager.Instance.StartNextGeneration();
            }
        }
    }
}
