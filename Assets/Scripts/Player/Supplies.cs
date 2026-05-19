using System;
using TMPro;
using UnityEngine;

using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using System.Collections.Generic;

namespace GameDevTV.RTS.Player
{
    public class Supplies : MonoBehaviour
    {
        [SerializeField] private SupplySO biomassSO;
        [SerializeField] private float mineralsToBiomassRate = 1f;
        [SerializeField] private float gasToBiomassRate = 1f;
        [SerializeField] private int startingBiomass = 1000;

        // Oxygen (new)
        [SerializeField] private SupplySO oxygenSO;
        public static Dictionary<Owner, int> Oxygen { get; private set; }
        public static event Action<Owner,int> OnOxygenChanged;

        // static copies of rates so other classes (commands) can compute costs
        public static float MineralsToBiomassRateStatic { get; private set; } = 1f;
        public static float GasToBiomassRateStatic { get; private set; } = 1f;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO gasSO;

        public static Dictionary<Owner, int> Biomass { get; private set; }
        public static Dictionary<Owner, int> Population { get; private set; }
        public static Dictionary<Owner, int> PopulationLimit { get; private set; }

        // Events
        public static event System.Action<Owner, int> OnBiomassChanged;
        public static event System.Action OnVictory;

        // Optional helper to centralize raising the event
        public static void RaiseBiomassChanged(Owner owner, int value)
        {
            OnBiomassChanged?.Invoke(owner, value);
        }

        private void Awake()
        {
            Biomass = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();

            // init oxygen dictionary
            Oxygen = new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                Biomass.Add(owner, 0);
                Population.Add(owner, 0);
                PopulationLimit.Add(owner, 0);
                Oxygen.Add(owner, 0);
            }

            // publish selected conversion rates for static use
            MineralsToBiomassRateStatic = mineralsToBiomassRate;
            GasToBiomassRateStatic = gasToBiomassRate;

            // Grant starting biomass to all owners
            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                if (Biomass.ContainsKey(owner))
                    Biomass[owner] = startingBiomass;
            }

            OnOxygenChanged += HandleOxygenChanged;

            if (Biomass.TryGetValue(Owner.Player1, out int initial))
            {
                OnBiomassChanged?.Invoke(Owner.Player1, initial);
            }

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
        }

        private void OnDestroy()
        {
            OnOxygenChanged -= HandleOxygenChanged;
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);

            Biomass?.Clear();
            Oxygen?.Clear();
            Population?.Clear();
            PopulationLimit?.Clear();
        }

        private void HandleOxygenChanged(Owner owner, int value)
        {
            if (owner == Owner.AI1 || owner == Owner.Player1)
            {
                Debug.Log($"[Supplies] HandleOxygenChanged for {owner}: {value}%");
                if (value >= 100)
                {
                    OnVictory?.Invoke();
                }
            }
        }

        public static void UpdateOxygen(Owner owner, int value)
        {
            if (Oxygen != null && Oxygen.ContainsKey(owner))
            {
                Oxygen[owner] = value;
                Debug.Log($"[Supplies] Static UpdateOxygen called for {owner}: {value}%");
                OnOxygenChanged?.Invoke(owner, value);
            }
            else
            {
                Debug.LogWarning($"[Supplies] Static UpdateOxygen failed. Oxygen dict null or key missing for {owner}");
            }
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            // Defensive: evt.Supply may be null in some cases. Ignore if so.
            if (evt.Supply == null) return;
            // Convert minerals/gas supply events to biomass centrally.
            if (evt.Supply == mineralsSO)
            {
                int biomassAmount = Mathf.FloorToInt(evt.Amount * mineralsToBiomassRate);
                Biomass[evt.Owner] += biomassAmount;
                RaiseBiomassChanged(evt.Owner, Biomass[evt.Owner]); // Raise event
                return; // handled centrally - don't modify Minerals/Gas
            }
            else if (evt.Supply == gasSO)
            {
                int biomassAmount = Mathf.FloorToInt(evt.Amount * gasToBiomassRate);
                Biomass[evt.Owner] += biomassAmount;
                RaiseBiomassChanged(evt.Owner, Biomass[evt.Owner]); // Raise event
                return;
            }
            else if (evt.Supply == oxygenSO)
            {
                // oxygen is a separate resource (no conversion)
                Oxygen[evt.Owner] += evt.Amount;

                OnOxygenChanged?.Invoke(evt.Owner, Oxygen[evt.Owner]);
                return;
            }

            // Other supply types (if any) can be handled here in future.
            }
            }
            }