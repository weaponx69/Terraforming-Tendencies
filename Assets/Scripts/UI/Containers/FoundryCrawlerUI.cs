using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Containers
{
    public class FoundryCrawlerUI : MonoBehaviour, IUIElement<FoundryCrawler>
    {
        [SerializeField] private TextMeshProUGUI regolithText;
        [SerializeField] private TextMeshProUGUI ironText;
        [SerializeField] private TextMeshProUGUI pipeText;
        
        private FoundryCrawler selectedCrawler;

        public void EnableFor(FoundryCrawler crawler)
        {
            selectedCrawler = crawler;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Disable()
        {
            selectedCrawler = null;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (selectedCrawler != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (selectedCrawler == null) return;

            if (regolithText != null)
                regolithText.SetText($"Regolith: {selectedCrawler.CurrentRegolith:F1} / {selectedCrawler.maxRegolith}");
            
            if (ironText != null)
                ironText.SetText($"Iron: {selectedCrawler.CurrentIron:F1} / {selectedCrawler.maxIron}");
            
            if (pipeText != null)
                pipeText.SetText($"Pipes Ready: {selectedCrawler.PipeBuffer}");
        }
    }
}