using System.Collections;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Manages passive climate tick-up for Temperature, Atmosphere, and Water.
    /// When a climate card is played (via ResourceShipmentCardSO), it applies an
    /// immediate boost AND sets a target value. This manager then ticks the value
    /// upward over time until it reaches the target, simulating gradual climate change.
    ///
    /// Auto-spawns as a DontDestroyOnLoad singleton.
    /// </summary>
    public class ClimateManager : MonoBehaviour
    {
        public static ClimateManager Instance { get; private set; }

        [Header("Tick Settings")]
        [Tooltip("How often (in seconds) the climate ticks upward.")]
        [SerializeField] private float tickRate = 1f;

        [Tooltip("Temperature gained per tick toward the target (°C).")]
        [SerializeField] private float tempPerTick = 0.5f;

        [Tooltip("Atmosphere gained per tick toward the target (atm).")]
        [SerializeField] private float atmosPerTick = 0.005f;

        [Tooltip("Water gained per tick toward the target (%).")]
        [SerializeField] private float waterPerTick = 0.25f;

        // Current targets — when a card is played, these are set to the card's
        // applied value. The manager ticks toward them until reached.
        private float targetTemperature = float.MinValue;
        private float targetAtmosphere = float.MinValue;
        private float targetWater = float.MinValue;

        private Coroutine tickRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            GameObject go = new GameObject("ClimateManager");
            DontDestroyOnLoad(go);
            go.AddComponent<ClimateManager>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            tickRoutine = StartCoroutine(ClimateTickLoop());
        }

        private void OnDestroy()
        {
            if (tickRoutine != null)
                StopCoroutine(tickRoutine);
        }

        /// <summary>
        /// Called by ResourceShipmentCardSO.Apply() after applying the immediate boost.
        /// Sets the target so the passive tick-up continues toward this value.
        /// </summary>
        public void SetTemperatureTarget(float value)  { targetTemperature = value; }
        public void SetAtmosphereTarget(float value)   { targetAtmosphere = value; }
        public void SetWaterTarget(float value)        { targetWater = value; }

        /// <summary>
        /// Clears leftover card tick-up targets so a new sector round does not
        /// inherit free climate progress from the previous sector's shipments.
        /// </summary>
        public void ClearPendingTargets()
        {
            targetTemperature = float.MinValue;
            targetAtmosphere = float.MinValue;
            targetWater = float.MinValue;
        }

        private IEnumerator ClimateTickLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(tickRate);

                Owner owner = Owner.Player1;

                // ── Temperature ──────────────────────────────────────────────
                if (targetTemperature > float.MinValue)
                {
                    float curTemp = Supplies.Temperature.TryGetValue(owner, out float t) ? t : -60f;
                    if (curTemp < targetTemperature)
                    {
                        float newTemp = Mathf.Min(curTemp + tempPerTick, targetTemperature);
                        Supplies.UpdateTemperature(owner, newTemp);
                    }
                }

                // ── Atmosphere ───────────────────────────────────────────────
                if (targetAtmosphere > float.MinValue)
                {
                    float curAtmos = Supplies.Atmosphere.TryGetValue(owner, out float a) ? a : 0.01f;
                    if (curAtmos < targetAtmosphere)
                    {
                        float newAtmos = Mathf.Min(curAtmos + atmosPerTick, targetAtmosphere);
                        Supplies.UpdateAtmosphere(owner, newAtmos);
                    }
                }

                // ── Water ────────────────────────────────────────────────────
                if (targetWater > float.MinValue)
                {
                    float curWater = Supplies.Water.TryGetValue(owner, out float w) ? w : 0f;
                    if (curWater < targetWater)
                    {
                        float newWater = Mathf.Min(curWater + waterPerTick, targetWater);
                        Supplies.UpdateWater(owner, newWater);
                    }
                }
            }
        }
    }
}
