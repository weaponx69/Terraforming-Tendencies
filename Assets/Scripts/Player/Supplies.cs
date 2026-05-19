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
        [SerializeField] private TextMeshProUGUI biomassText;
        [SerializeField] private SupplySO biomassSO;
        [SerializeField] private float mineralsToBiomassRate = 1f;
        [SerializeField] private float gasToBiomassRate = 1f;
        [SerializeField] private int startingBiomass = 1000;

        // Oxygen (new)
        [SerializeField] private TextMeshProUGUI oxygenText;
        [SerializeField] private SupplySO oxygenSO;
        public static Dictionary<Owner, int> Oxygen { get; private set; }
        public static event Action<Owner,int> OnOxygenChanged;

        // static copies of rates so other classes (commands) can compute costs
        public static float MineralsToBiomassRateStatic { get; private set; } = 1f;
        public static float GasToBiomassRateStatic { get; private set; } = 1f;

        [SerializeField] private TextMeshProUGUI populationText;

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

            OnBiomassChanged += HandleBiomassChanged;
            OnOxygenChanged += HandleOxygenChanged;

            if (biomassText != null && Biomass.TryGetValue(Owner.Player1, out int initial))
            {
                biomassText.SetText(initial.ToString());
                OnBiomassChanged?.Invoke(Owner.Player1, initial);
            }
            if (oxygenText != null && Oxygen.TryGetValue(Owner.Player1, out int oxyInitial))
            {
                oxygenText.SetText(oxyInitial.ToString());
            }

            UpdateHabitabilityUI(0);

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
        }

        private void OnDestroy()
        {
            OnBiomassChanged -= HandleBiomassChanged;
            OnOxygenChanged -= HandleOxygenChanged;
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
        }

        private void HandleOxygenChanged(Owner owner, int value)
        {
            if (owner == Owner.AI1 || owner == Owner.Player1)
            {
                Debug.Log($"[Supplies] HandleOxygenChanged for {owner}: {value}%");
                UpdateHabitabilityUI(value);
                if (value >= 100)
                {
                    OnVictory?.Invoke();
                }
            }
        }

        private void UpdateHabitabilityUI(int percent)
        {
            if (populationText != null)
            {
                Debug.Log($"[Supplies] Updating populationText to {percent}%");
                populationText.SetText($"{percent}%");
            }
            else
            {
                Debug.LogWarning("[Supplies] populationText is NULL in UpdateHabitabilityUI!");
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

        private void HandleBiomassChanged(Owner owner, int value)
        {
            if ((owner == Owner.Player1 || owner == Owner.AI1) && biomassText != null)
            {
                biomassText.SetText(value.ToString());
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

                if (evt.Owner == Owner.Player1 && oxygenText != null)
                    oxygenText.SetText(Oxygen[evt.Owner].ToString());

                OnOxygenChanged?.Invoke(evt.Owner, Oxygen[evt.Owner]);
                return;
            }

            // Other supply types (if any) can be handled here in future.
            }
            }
            }