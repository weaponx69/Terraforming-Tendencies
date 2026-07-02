using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI.Components
{
    public class ColonyIntegrityBar : MonoBehaviour
    {
        [Header("Bar References")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI valueLabel;

        [Header("Color Thresholds")]
        [SerializeField] private Color healthyColor  = new Color(0.22f, 0.85f, 0.40f, 1f);
        [SerializeField] private Color warningColor  = new Color(1.00f, 0.65f, 0.00f, 1f);
        [SerializeField] private Color criticalColor = new Color(0.90f, 0.15f, 0.15f, 1f);
        [SerializeField] [Range(0f, 1f)] private float warningThreshold  = 0.50f;
        [SerializeField] [Range(0f, 1f)] private float criticalThreshold = 0.25f;

        [Header("Animation")]
        [SerializeField] private float lerpSpeed = 4f;

        [Header("Critical Pulse")]
        [SerializeField] private float pulseSpeed = 8f;
        [SerializeField] [Range(0f, 1f)] private float pulseMinAlpha = 0.3f;
        [SerializeField] private float pulseMaxAlpha = 1f;

        private float _targetFill  = 1f;
        private float _currentFill = 1f;
        private Owner _owner;

        public float CurrentFillPercent => _currentFill;
        public bool IsCritical => _currentFill <= criticalThreshold;

        private void Awake()
        {
            _owner = GameOverManager.MonitoredOwner;

            if (fillImage != null && fillImage.sprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                fillImage.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        private void OnEnable()
        {
            Supplies.OnIntegrityChanged += HandleIntegrityChanged;

            if (Supplies.Integrity != null && Supplies.Integrity.TryGetValue(_owner, out float cur))
            {
                _targetFill  = Mathf.Clamp01(cur / 100f);
                _currentFill = _targetFill;
            }
            else
            {
                _targetFill  = 1f;
                _currentFill = 1f;
            }
            ApplyFill(_currentFill);
        }

        private void OnDisable()
        {
            Supplies.OnIntegrityChanged -= HandleIntegrityChanged;
        }

        private void Update()
        {
            if (!Mathf.Approximately(_currentFill, _targetFill))
            {
                _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * lerpSpeed);
                if (Mathf.Abs(_currentFill - _targetFill) < 0.001f)
                    _currentFill = _targetFill;

                ApplyFill(_currentFill);
            }

            if (backgroundImage != null)
            {
                if (IsCritical)
                {
                    float t = Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed));
                    float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);
                    backgroundImage.CrossFadeAlpha(alpha, 0f, true);
                }
                else
                {
                    backgroundImage.CrossFadeAlpha(1f, 0f, true);
                }
            }
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
                // Force the Image into Filled mode, Vertical from Bottom.
                // As fillAmount drops from 1 -> 0, the colored region stays
                // fixed at the bottom and shrinks from the TOP downward.
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Vertical;
                fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                fillImage.fillAmount = fill;
                fillImage.color = GetColorForFill(fill);
            }
            else
            {
                Debug.LogError("[ColonyIntegrityBar] fillImage is NULL!");
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
