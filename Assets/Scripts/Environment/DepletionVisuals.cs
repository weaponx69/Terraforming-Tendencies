using UnityEngine;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Visually scales a resource node's transform proportional to its remaining supply.
    /// <para>
    /// The lerp math is intentionally kept in C# (never moved to graph nodes).
    /// <see cref="DepletionRatio"/> and the two scale-limit fields are decorated with
    /// <c>[Inspectable]</c> so Unity Visual Scripting Flow Graph wrappers can read the
    /// live ratio and tune thresholds — e.g. triggering a particle burst at 50 % or a
    /// warning pulse at 20 % — without duplicating the scale formula.
    /// </para>
    /// </summary>
    [IncludeInSettings(true)]
    [RequireComponent(typeof(GatherableSupply))]
    public class DepletionVisuals : MonoBehaviour
    {
        [Tooltip("Scale multiplier when the supply is fully depleted. " +
                 "Clamped to [0, maxScaleFactor]. Default: 0.2 (20 % of original size).")]
        [Inspectable]
        [SerializeField] private float minScaleFactor = 0.2f;

        [Tooltip("Scale multiplier when the supply is at full capacity. " +
                 "Should be 1.0 in almost all cases.")]
        [Inspectable]
        [SerializeField] private float maxScaleFactor = 1.0f;

        // ── private backing fields ─────────────────────────────────────────────
        private GatherableSupply supply;
        private Vector3          originalScale;
        private float            initialAmount;

        // ── VS-visible live state ──────────────────────────────────────────────

        /// <summary>
        /// Normalised remaining-supply ratio in the range [0, 1].
        /// <para>
        /// Updated every frame in C#. Exposed to Unity Visual Scripting so that
        /// a companion Flow Graph (<c>DepletionVisuals_Flow.graph</c>) can branch
        /// on threshold crossings without re-implementing the ratio formula.
        /// </para>
        /// </summary>
        [Inspectable]
        public float DepletionRatio { get; private set; }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Start()
        {
            supply        = GetComponent<GatherableSupply>();
            originalScale = transform.localScale;

            // Prefer the live Amount; fall back to the SO MaxAmount; final default 1500.
            if      (supply.Amount > 0)      initialAmount = supply.Amount;
            else if (supply.Supply != null)  initialAmount = supply.Supply.MaxAmount;
            else                             initialAmount = 1500f;
        }

        private void Update()
        {
            if (supply == null || initialAmount <= 0f) return;

            // ── Math stays in C# — do NOT replicate this in graph nodes ──────
            DepletionRatio            = Mathf.Clamp01((float)supply.Amount / initialAmount);
            float scaleFactor         = Mathf.Lerp(minScaleFactor, maxScaleFactor, DepletionRatio);
            transform.localScale      = originalScale * scaleFactor;
        }
    }
}
