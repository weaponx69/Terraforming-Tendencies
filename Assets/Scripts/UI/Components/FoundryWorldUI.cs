using TMPro;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Displays world-space metrics for Regolith, Iron, and Pipes next to the Foundry Crawler.
    /// </summary>
    public class FoundryWorldUI : MonoBehaviour
    {
        [SerializeField] private FoundryCrawler crawler;
        [SerializeField] private TextMeshPro regolithText;
        [SerializeField] private TextMeshPro ironText;
        [SerializeField] private TextMeshPro pipeText;

        private void Start()
        {
            if (crawler == null)
            {
                crawler = GetComponentInParent<FoundryCrawler>();
            }

            // Ensure we have a FaceCamera component on this object or its children
            if (GetComponent<FaceCamera>() == null && GetComponentInChildren<FaceCamera>() == null)
            {
                gameObject.AddComponent<FaceCamera>();
            }
        }

        private void Update()
        {
            if (crawler == null) return;

            if (regolithText != null)
                regolithText.text = $"Regolith: {crawler.CurrentRegolith:F0}";

            if (ironText != null)
                ironText.text = $"Iron: {crawler.CurrentIron:F0}";

            if (pipeText != null)
                pipeText.text = $"Pipes: {crawler.PipeBuffer}";
        }
    }
}
