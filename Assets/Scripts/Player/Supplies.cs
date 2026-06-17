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
        [SerializeField] private float mineralsToMaterialsRate = 1f;
        [SerializeField] private float gasToMaterialsRate = 1f;
        [SerializeField] private int startingMaterials = 1000;

        [SerializeField] private SupplySO oxygenSO;
        private static Dictionary<Owner, int> _materials;
        public static Dictionary<Owner, int> Materials 
        { 
            get 
            {
                EnsureInitialized();
                return _materials;
            }
            private set => _materials = value;
        }

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

        private static Dictionary<Owner, float> _power;
        public static Dictionary<Owner, float> Power 
        { 
            get 
            {
                EnsureInitialized();
                return _power;
            }
            private set => _power = value;
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
        public static event Action<Owner, float> OnPowerChanged;
        public static event Action<Owner, int> OnBiomassChanged;

        public static float MineralsToMaterialsRateStatic { get; private set; } = 1f;
        public static float GasToMaterialsRateStatic { get; private set; } = 1f;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO gasSO;

        public static event System.Action<Owner, int> OnMaterialsChanged;

        public static void RaiseMaterialsChanged(Owner owner, int value)
        {
            OnMaterialsChanged?.Invoke(owner, value);
        }

        public static event Action<Owner, int> OnPopulationChanged;
        public static event Action<Owner, int> OnPopulationLimitChanged;

        public static void UpdatePower(Owner owner, float value)
        {
            if (Power != null && Power.ContainsKey(owner))
            {
                Power[owner] = Mathf.Max(0, value);
                OnPowerChanged?.Invoke(owner, Power[owner]);
            }
        }

        public static void UpdateBiomass(Owner owner, int value)
        {
            if (Biomass != null && Biomass.ContainsKey(owner))
            {
                Biomass[owner] = Mathf.Max(0, value);
                OnBiomassChanged?.Invoke(owner, Biomass[owner]);
            }
        }

        public static void UpdatePopulation(Owner owner, int value)
        {
            if (Population != null && Population.ContainsKey(owner))
            {
                Population[owner] = Mathf.Max(0, value);
                OnPopulationChanged?.Invoke(owner, Population[owner]);
            }
        }

        public static void UpdatePopulationLimit(Owner owner, int value)
        {
            if (PopulationLimit != null && PopulationLimit.ContainsKey(owner))
            {
                PopulationLimit[owner] = Mathf.Max(0, value);
                OnPopulationLimitChanged?.Invoke(owner, PopulationLimit[owner]);
            }
        }

        public static Supplies Instance { get; private set; }

        private static void EnsureInitialized()
        {
            if (_materials != null) return;

            _materials = new Dictionary<Owner, int>();
            _biomass = new Dictionary<Owner, int>();
            _power = new Dictionary<Owner, float>();
            _population = new Dictionary<Owner, int>();
            _populationLimit = new Dictionary<Owner, int>();
            _oxygen = new Dictionary<Owner, float>();
            _integrity = new Dictionary<Owner, float>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                _materials[owner] = 1000;
                _biomass[owner] = 0;
                _power[owner] = 0f;
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

            // Re-initialize to ensure instance settings (startingMaterials) are applied
            _materials = new Dictionary<Owner, int>();
            _biomass = new Dictionary<Owner, int>();
            _power = new Dictionary<Owner, float>();
            _population = new Dictionary<Owner, int>();
            _populationLimit = new Dictionary<Owner, int>();
            _oxygen = new Dictionary<Owner, float>();
            _integrity = new Dictionary<Owner, float>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                _materials.Add(owner, startingMaterials);
                _biomass.Add(owner, 0);
                _power.Add(owner, 0f);
                _population.Add(owner, 0);
                _populationLimit.Add(owner, 0);
                _oxygen.Add(owner, 0f);
                _integrity.Add(owner, 100f);
            }

            MineralsToMaterialsRateStatic = mineralsToMaterialsRate;
            GasToMaterialsRateStatic = gasToMaterialsRate;

            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent); 
            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            
            Owner displayOwner = GameOverManager.MonitoredOwner;
            RaiseMaterialsChanged(displayOwner, Materials[displayOwner]);
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
                Debug.LogWarning($"[Supplies] HandleSupplyEvent received null supply! Amount: {evt.Amount}");
                Materials[evt.Owner] += evt.Amount;
                RaiseMaterialsChanged(evt.Owner, Materials[evt.Owner]);
                return;
            }

            string sName = evt.Supply.name.ToLower();
            bool isMinerals = (mineralsSO != null && evt.Supply == mineralsSO) || sName.Contains("minerals");
            bool isGas = (gasSO != null && evt.Supply == gasSO) || sName.Contains("gas");
            bool isOxygen = (oxygenSO != null && evt.Supply == oxygenSO) || sName.Contains("oxygen");
            bool isBiomass = sName.Contains("biomass") || sName.Contains("food");
            
            Debug.Log($"[Supplies] HandleSupplyEvent for {evt.Owner}: amount={evt.Amount}, isMinerals={isMinerals}, sName={sName}");

            if (isMinerals)
            {
                int matsAmount = Mathf.FloorToInt(evt.Amount * mineralsToMaterialsRate);
                Materials[evt.Owner] += matsAmount;
                RaiseMaterialsChanged(evt.Owner, Materials[evt.Owner]); 
                return;
            }
            else if (isGas)
            {
                int matsAmount = Mathf.FloorToInt(evt.Amount * gasToMaterialsRate);
                Materials[evt.Owner] += matsAmount;
                RaiseMaterialsChanged(evt.Owner, Materials[evt.Owner]); 
                return;
            }
            else if (isOxygen)
            {
                Oxygen[evt.Owner] += evt.Amount;
                OnOxygenChanged?.Invoke(evt.Owner, Oxygen[evt.Owner]);
                return;
            }
            else if (isBiomass)
            {
                int curBiomass = Biomass != null && Biomass.TryGetValue(evt.Owner, out int b) ? b : 0;
                UpdateBiomass(evt.Owner, curBiomass + evt.Amount);
                return;
            }
        }
    }
}
