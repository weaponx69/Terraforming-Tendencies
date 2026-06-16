using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI.Containers
{
    public class GenerationProgressUI : MonoBehaviour
    {
        [Tooltip("The Image component used for the progress bar fill.")]
        [SerializeField] private Image fillImage;
        
        [Tooltip("Optional text to display the percentage (e.g. 50%).")]
        [SerializeField] private TextMeshProUGUI percentageText;

        private void OnEnable()
        {
            GenerationManager.OnGenerationProgressChanged += UpdateProgress;
        }

        private void OnDisable()
        {
            GenerationManager.OnGenerationProgressChanged -= UpdateProgress;
        }

        private void UpdateProgress(float progress)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = progress;
            }

            if (percentageText != null)
            {
                percentageText.text = $"{(progress * 100f):F0}%";
            }
        }
    }
}
