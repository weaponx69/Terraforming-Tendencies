using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Left-side vertical bar displaying colony structural integrity (0–100%).
    /// Subscribes to Supplies.OnIntegrityChanged and drives a Filled Image.
    /// Color transitions: green → orange → red as integrity drops.
    /// </summary>
    public class ColonyIntegrityBar : MonoBehaviour
    {
        [Header("Bar References")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI valueLabel;

        [Header("Color Thresholds")]
        [SerializeField] private Color healthyColor  = new Color(0.22f, 0.85f, 0.40f, 1f); // green
        [SerializeField] private Color warningColor  = new Color(1.00f, 0.65f, 0.00f, 1f); // orange
        [SerializeField] private Color criticalColor = new Color(0.90f, 0.15f, 0.15f, 1f); // red
        [SerializeField] [Range(0f, 1f)] private float warningThreshold  = 0.50f;
        [SerializeField] [Range(0f, 1f)] private float criticalThreshold = 0.25f;

        [Header("Animation")]
        [SerializeField] private float lerpSpeed = 4f;

        private float _targetFill  = 1f;
        private float _currentFill = 1f;
        private Owner _owner;

        private void Awake()
        {
            _owner = GameOverManager.MonitoredOwner;
        }

        private void OnEnable()
        {
            Supplies.OnIntegrityChanged += HandleIntegrityChanged;

            // Snap to current value immediately on enable
            if (Supplies.Integrity != null && Supplies.Integrity.TryGetValue(_owner, out float cur))
            {
                _targetFill  = Mathf.Clamp01(cur / 100f);
                _currentFill = _targetFill;
                ApplyFill(_currentFill);
            }
        }

        private void OnDisable()
        {
            Supplies.OnIntegrityChanged -= HandleIntegrityChanged;
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentFill, _targetFill)) return;

            _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * lerpSpeed);
            if (Mathf.Abs(_currentFill - _targetFill) < 0.001f)
                _currentFill = _targetFill;

            ApplyFill(_currentFill);
        }

        private void HandleIntegrityChanged(Owner owner, float newValue)
        {
            if (owner != _owner) return;
            _targetFill = Mathf.Clamp01(newValue / 100f);
        }

        private void ApplyFill(float fill)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = fill;
                fillImage.color = GetColorForFill(fill);
            }

            if (valueLabel != null)
                valueLabel.SetText($"{fill * 100f:F0}%");
        }

        private Color GetColorForFill(float fill)
        {
            if (fill <= criticalThreshold)
                return criticalColor;
            if (fill <= warningThreshold)
            {
                float t = (fill - criticalThreshold) / (warningThreshold - criticalThreshold);
                return Color.Lerp(criticalColor, warningColor, t);
            }
            else
            {
                float t = (fill - warningThreshold) / (1f - warningThreshold);
                return Color.Lerp(warningColor, healthyColor, t);
            }
        }
    }
}
