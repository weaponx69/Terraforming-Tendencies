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
        private static Dictionary<Owner, int> _biomass;
        public static Dictionary<Owner, int> Biomass 
        { 
            get 
            {
                EnsureInitialized();
                return _biomass;
            }
            private set => _biomass = value;
        }

        private static Dictionary<Owner, int> _population;
        public static Dictionary<Owner, int> Population 
        { 
            get 
            {
                EnsureInitialized();
                return _population;
            }
            private set => _population = value;
        }

        private static Dictionary<Owner, int> _populationLimit;
        public static Dictionary<Owner, int> PopulationLimit 
        { 
            get 
            {
                EnsureInitialized();
                return _populationLimit;
            }
            private set => _populationLimit = value;
        }

        private static Dictionary<Owner, float> _oxygen;
        public static Dictionary<Owner, float> Oxygen 
        { 
            get 
            {
                EnsureInitialized();
                return _oxygen;
            }
            private set => _oxygen = value;
        }

        private static Dictionary<Owner, float> _integrity;
        public static Dictionary<Owner, float> Integrity 
        { 
            get 
            {
                EnsureInitialized();
                return _integrity;
            }
            private set => _integrity = value;
        }

        public static event Action<Owner, float> OnOxygenChanged;
        public static event Action<Owner, float> OnIntegrityChanged;

        public static float MineralsToBiomassRateStatic { get; private set; } = 1f;
        public static float GasToBiomassRateStatic { get; private set; } = 1f;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO gasSO;

        public static event System.Action<Owner, int> OnBiomassChanged;

        public static void RaiseBiomassChanged(Owner owner, int value)
        {
            OnBiomassChanged?.Invoke(owner, value);
        }

        public static Supplies Instance { get; private set; }

        private static void EnsureInitialized()
        {
            if (_biomass != null) return;

            _biomass = new Dictionary<Owner, int>();
            _population = new Dictionary<Owner, int>();
            _populationLimit = new Dictionary<Owner, int>();
            _oxygen = new Dictionary<Owner, float>();
            _integrity = new Dictionary<Owner, float>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                _biomass[owner] = 1000;
                _population[owner] = 0;
                _populationLimit[owner] = 0;
                _oxygen[owner] = 0f;
                _integrity[owner] = 100f;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // // Debug.LogWarning($"[Supplies] Multiple instances detected. Destroying duplicate on {gameObject.name}");
                Destroy(this);
                return;
            }
            Instance = this;

            // Re-initialize to ensure instance settings (startingBiomass) are applied
            _biomass = new Dictionary<Owner, int>();
            _population = new Dictionary<Owner, int>();
            _populationLimit = new Dictionary<Owner, int>();
            _oxygen = new Dictionary<Owner, float>();
            _integrity = new Dictionary<Owner, float>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                _biomass.Add(owner, startingBiomass);
                _population.Add(owner, 0);
                _populationLimit.Add(owner, 0);
                _oxygen.Add(owner, 0f);
                _integrity.Add(owner, 100f);
            }

            MineralsToBiomassRateStatic = mineralsToBiomassRate;
            GasToBiomassRateStatic = gasToBiomassRate;

            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent); 
            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            
            Owner displayOwner = GameOverManager.MonitoredOwner;
            RaiseBiomassChanged(displayOwner, Biomass[displayOwner]);
        }

        private void Start()
        {
            if (PlayerPrefs.GetInt("LoadGameRequest", 0) == 1)
            {
                PlayerPrefs.SetInt("LoadGameRequest", 0);
                SaveSystem.LoadGame(1); // Default to slot 1 for main menu load
            }
        }

        private void OnDestroy()
{
            if (Instance == this) Instance = null;
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
        }

        public static void UpdateOxygen(Owner owner, float value)
        {
            if (Oxygen != null && Oxygen.ContainsKey(owner))
            {
                float maxOxygen = 100f;
                if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
                {
                    int total = SectorManager.Instance.Sectors.Count;
                    int occupied = 0;
                    foreach (var s in SectorManager.Instance.Sectors) if (s.IsOccupied) occupied++;
                    maxOxygen = ((float)occupied / total) * 100f;
                }

                Oxygen[owner] = Mathf.Min(value, maxOxygen);
                OnOxygenChanged?.Invoke(owner, Oxygen[owner]);
            }
        }

        public static void UpdateIntegrity(Owner owner, float value)
        {
            if (Integrity != null && Integrity.ContainsKey(owner))
            {
                Integrity[owner] = Mathf.Max(0, value);
                OnIntegrityChanged?.Invoke(owner, Integrity[owner]);
            }
        }

        public static float CalculateIntegrity(Owner owner)
        {
            var commandables = AbstractCommandable.ActiveCommandables;
            long totalMaxHP = 0;
            long totalCurrentHP = 0;
            bool foundAny = false;

            foreach (var c in commandables)
            {
                if (c == null) continue;
                if (c.Owner == owner)
                {
                    totalMaxHP += c.MaxHealth;
                    totalCurrentHP += c.CurrentHealth;
                    foundAny = true;
                }
            }

            if (!foundAny) return 0f;
            return ((float)totalCurrentHP / totalMaxHP) * 100f;
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply == null) 
            {
                Biomass[evt.Owner] += evt.Amount;
                // // Debug.Log($"[Supplies] {evt.Owner} received direct grant of {evt.Amount} Biomass. Total: {Biomass[evt.Owner]}");
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
                // // Debug.Log($"[Supplies] {evt.Owner} gathered {evt.Amount} minerals -> {(biomassAmount >= 0 ? "+" : "")}{biomassAmount} Biomass. Total: {Biomass[evt.Owner]}");
                RaiseBiomassChanged(evt.Owner, Biomass[evt.Owner]); 
                return;
            }
            else if (isGas)
            {
                int biomassAmount = Mathf.FloorToInt(evt.Amount * gasToBiomassRate);
                Biomass[evt.Owner] += biomassAmount;
                // // Debug.Log($"[Supplies] {evt.Owner} gathered {evt.Amount} gas -> {(biomassAmount >= 0 ? "+" : "")}{biomassAmount} Biomass. Total: {Biomass[evt.Owner]}");
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
