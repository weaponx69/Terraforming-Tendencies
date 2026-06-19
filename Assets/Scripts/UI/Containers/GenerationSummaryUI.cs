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
        [SerializeField] private Button viewTechTreeButton;
        [SerializeField] private TechTreeUI techTreeUI;

        private void OnEnable()
        {
            Debug.Log("[GenerationSummaryUI] OnEnable called. Subscribing to OnGenerationEnded.");
            GenerationManager.OnGenerationEnded += ShowSummary;
            if (nextGenerationButton != null)
            {
                nextGenerationButton.onClick.AddListener(OnNextClicked);
            }
            if (viewTechTreeButton != null)
            {
                viewTechTreeButton.onClick.AddListener(OnViewTechTreeClicked);
            }
        }

        private void OnDisable()
        {
            Debug.Log("[GenerationSummaryUI] OnDisable called. Unsubscribing from OnGenerationEnded.\nStackTrace: " + System.Environment.StackTrace);
            GenerationManager.OnGenerationEnded -= ShowSummary;
            if (nextGenerationButton != null)
            {
                nextGenerationButton.onClick.RemoveListener(OnNextClicked);
            }
            if (viewTechTreeButton != null)
            {
                viewTechTreeButton.onClick.RemoveListener(OnViewTechTreeClicked);
            }
        }

        private void Awake()
        {
            if (panel == null)
            {
                var t = transform.Find("Panel") ?? transform.Find("Summary Panel") ?? transform.Find("Generation Summary Panel");
                if (t != null) panel = t.gameObject;
                else if (transform.childCount > 0) panel = transform.GetChild(0).gameObject;
            }

            if (nextGenerationButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    if (b.name.Contains("Next", System.StringComparison.OrdinalIgnoreCase) ||
                        b.name.Contains("Gen", System.StringComparison.OrdinalIgnoreCase))
                    {
                        nextGenerationButton = b;
                        break;
                    }
                }
            }

            if (viewTechTreeButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    if (b.name.Contains("Tech", System.StringComparison.OrdinalIgnoreCase) ||
                        b.name.Contains("Tree", System.StringComparison.OrdinalIgnoreCase))
                    {
                        viewTechTreeButton = b;
                        break;
                    }
                }
            }

            if (techTreeUI == null)
            {
                techTreeUI = FindObjectOfType<TechTreeUI>(true);
            }
        }

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void ShowSummaryDirect(int earnedTC, int totalTC)
        {
            ShowSummary(earnedTC, totalTC);
        }

        private void ShowSummary(int earnedTC, int totalTC)
        {
            Debug.Log($"[GenerationSummaryUI] ShowSummary called! Earned TC: {earnedTC}, Total: {totalTC}");
            if (panel != null) 
            {
                panel.SetActive(true);
                Debug.Log("[GenerationSummaryUI] Panel set to active.");
            }
            else
            {
                Debug.LogError("[GenerationSummaryUI] Panel reference is NULL! Please assign it in the Inspector.");
            }

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

        private void OnViewTechTreeClicked()
        {
            if (techTreeUI != null)
            {
                if (panel != null) panel.SetActive(false);
                techTreeUI.Open(panel); // Pass the child panel so Close() reactivates it

                if (!techTreeUI.gameObject.activeInHierarchy)
                {
                    Debug.LogError("[GenerationSummaryUI] CRITICAL: TechTreeUI GameObject is inactive in the hierarchy after Open() was called! Check its parent GameObjects.");
                }
            }
            else
            {
                Debug.LogWarning("[GenerationSummaryUI] TechTreeUI reference is missing!");
            }
        }
    }
}
