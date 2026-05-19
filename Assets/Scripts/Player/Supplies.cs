using System;
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
        [SerializeField] private float mineralsToBiomassRate = 1f;
        [SerializeField] private float gasToBiomassRate = 1f;
        [SerializeField] private int startingBiomass = 1000;

        [SerializeField] private SupplySO oxygenSO;
        public static Dictionary<Owner, int> Oxygen { get; private set; }
        public static event Action<Owner, int> OnOxygenChanged;

        public static float MineralsToBiomassRateStatic { get; private set; } = 1f;
        public static float GasToBiomassRateStatic { get; private set; } = 1f;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO gasSO;

        public static Dictionary<Owner, int> Biomass { get; private set; }
        public static Dictionary<Owner, int> Population { get; private set; }
        public static Dictionary<Owner, int> PopulationLimit { get; private set; }

        public static event System.Action<Owner, int> OnBiomassChanged;
        public static event System.Action OnVictory;

        public static void RaiseBiomassChanged(Owner owner, int value)
        {
            OnBiomassChanged?.Invoke(owner, value);
        }

        public static Supplies Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Supplies] Multiple instances detected. Destroying duplicate on {gameObject.name}");
                Destroy(this);
                return;
            }
            Instance = this;

            if (Biomass == null) Biomass = new Dictionary<Owner, int>();
            if (Population == null) Population = new Dictionary<Owner, int>();
            if (PopulationLimit == null) PopulationLimit = new Dictionary<Owner, int>();
            if (Oxygen == null) Oxygen = new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                if (!Biomass.ContainsKey(owner)) Biomass.Add(owner, startingBiomass);
                if (!Population.ContainsKey(owner)) Population.Add(owner, 0);
                if (!PopulationLimit.ContainsKey(owner)) PopulationLimit.Add(owner, 0);
                if (!Oxygen.ContainsKey(owner)) Oxygen.Add(owner, 0);
            }

            MineralsToBiomassRateStatic = mineralsToBiomassRate;
            GasToBiomassRateStatic = gasToBiomassRate;

            OnOxygenChanged += HandleOxygenChanged;

            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent); 
            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            
            Owner displayOwner = GameOverManager.MonitoredOwner;
            RaiseBiomassChanged(displayOwner, Biomass[displayOwner]);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OnOxygenChanged -= HandleOxygenChanged;
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
        }

        private void HandleOxygenChanged(Owner owner, int value)
        {
            if (owner == Owner.AI1 || owner == Owner.Player1)
            {
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
                OnOxygenChanged?.Invoke(owner, value);
            }
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply == null) 
            {
                Biomass[evt.Owner] += evt.Amount;
                Debug.Log($"[Supplies] {evt.Owner} received direct grant of {evt.Amount} Biomass. Total: {Biomass[evt.Owner]}");
                RaiseBiomassChanged(evt.Owner, Biomass[evt.Owner]);
                return;
            }

            string sName = evt.Supply.name.ToLower();
            bool isMinerals = (mineralsSO != null && evt.Supply == mineralsSO) || sName.Contains("minerals");
            bool isGas = (gasSO != null && evt.Supply == gasSO) || sName.Contains("gas");
            bool isOxygen = (oxygenSO != null && evt.Supply == oxygenSO) || sName.Contains("oxygen");
            
            if (isMinerals)
            {
                int biomassAmount = Mathf.FloorToInt(evt.Amount * mineralsToBiomassRate);
                Biomass[evt.Owner] += biomassAmount;
                Debug.Log($"[Supplies] {evt.Owner} gathered {evt.Amount} minerals -> +{biomassAmount} Biomass. Total: {Biomass[evt.Owner]}");
                RaiseBiomassChanged(evt.Owner, Biomass[evt.Owner]); 
                return;
            }
            else if (isGas)
            {
                int biomassAmount = Mathf.FloorToInt(evt.Amount * gasToBiomassRate);
                Biomass[evt.Owner] += biomassAmount;
                Debug.Log($"[Supplies] {evt.Owner} gathered {evt.Amount} gas -> +{biomassAmount} Biomass. Total: {Biomass[evt.Owner]}");
                RaiseBiomassChanged(evt.Owner, Biomass[evt.Owner]); 
                return;
            }
            else if (isOxygen)
            {
                Oxygen[evt.Owner] += evt.Amount;
                OnOxygenChanged?.Invoke(evt.Owner, Oxygen[evt.Owner]);
                return;
            }
        }
    }
}
