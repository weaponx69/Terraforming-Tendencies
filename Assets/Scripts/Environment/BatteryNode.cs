using UnityEngine;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Battery backup for a building's power node.
    /// Mathf.Max/Min charge logic stays in C#; VS reads state and calls
    /// <see cref="Drain"/> / <see cref="Charge"/> as atomic operations.
    /// </summary>
    [IncludeInSettings(true)]
    public class BatteryNode : MonoBehaviour
    {
        /// <summary>Maximum charge capacity in seconds of runtime.</summary>
        [Inspectable]
        public float MaxCharge = 180f;

        /// <summary>Current charge level in seconds of runtime.</summary>
        [Inspectable]
        public float CurrentCharge = 180f;

        /// <summary>Normalised charge ratio [0, 1]. 1 = full, 0 = depleted.</summary>
        [Inspectable]
        public float ChargeRatio =>
            MaxCharge > 0f ? Mathf.Clamp01(CurrentCharge / MaxCharge) : 0f;

        /// <summary>True while any charge remains.</summary>
        [Inspectable]
        public bool HasCharge => CurrentCharge > 0;

        /// <summary>Drains the battery by the given amount. Callable from a Flow Graph.</summary>
        [Inspectable]
        public void Drain(float amount)
        {
            CurrentCharge = Mathf.Max(0, CurrentCharge - amount);
        }

        /// <summary>Charges the battery by the given amount. Callable from a Flow Graph.</summary>
        [Inspectable]
        public void Charge(float amount)
        {
            CurrentCharge = Mathf.Min(MaxCharge, CurrentCharge + amount);
        }
    }
}
