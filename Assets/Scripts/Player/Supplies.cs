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
        public static Dictionary<Owner, float> Oxygen { get; private set; }
        public static Dictionary<Owner, float> Integrity { get; private set; }
        public static event Action<Owner, float> OnOxygenChanged;
        public static event Action<Owner, float> OnIntegrityChanged;

        public static float MineralsToBiomassRateStatic { get; private set; } = 1f;
        public static float GasToBiomassRateStatic { get; private set; } = 1f;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO gasSO;

        public static Dictionary<Owner, int> Biomass { get; private set; }
        public static Dictionary<Owner, int> Population { get; private set; }
        public static Dictionary<Owner, int> PopulationLimit { get; private set; }

        public static event System.Action<Owner, int> OnBiomassChanged;

        public static void RaiseBiomassChanged(Owner owner, int value)
        {
            OnBiomassChanged?.Invoke(owner, value);
        }

        public static Supplies Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // // Debug.LogWarning($"[Supplies] Multiple instances detected. Destroying duplicate on {gameObject.name}");
                Destroy(this);
                return;
            }
            Instance = this;

            // Clear static dictionaries to prevent persistence across Play Mode sessions
            Biomass = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();
            Oxygen = new Dictionary<Owner, float>();
            Integrity = new Dictionary<Owner, float>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                if (!Biomass.ContainsKey(owner)) Biomass.Add(owner, startingBiomass);
                if (!Population.ContainsKey(owner)) Population.Add(owner, 0);
                if (!PopulationLimit.ContainsKey(owner)) PopulationLimit.Add(owner, 0);
                if (!Oxygen.ContainsKey(owner)) Oxygen.Add(owner, 0f);
                if (!Integrity.ContainsKey(owner)) Integrity.Add(owner, 100f); // Default 100% integrity
            }

            MineralsToBiomassRateStatic = mineralsToBiomassRate;
            GasToBiomassRateStatic = gasToBiomassRate;

            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent); 
Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            
            Owner displayOwner = GameOverManager.MonitoredOwner;
            RaiseBiomassChanged(displayOwner, Biomass[displayOwner]);
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
