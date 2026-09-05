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
        [SerializeField] private float startingOxygen = 0f;

        public static int StartingMaterials
        {
            get
            {
                if (Instance != null)
                {
                    return Instance.startingMaterials;
                }
                return 1000;
            }
        }

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

        private static Dictionary<Owner, float> _biomass;
        public static Dictionary<Owner, float> Biomass 
        { 
            get 
            {
                EnsureInitialized();
                return _biomass;
            }
            private set => _biomass = value;
        }

        private static Dictionary<Owner, float> _food;
        public static Dictionary<Owner, float> Food 
        { 
            get 
            {
                EnsureInitialized();
                return _food;
            }
            private set => _food = value;
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

        private static Dictionary<Owner, float> _energy;
        public static Dictionary<Owner, float> Energy 
        { 
            get 
            {
                EnsureInitialized();
                return _energy;
            }
            private set => _energy = value;
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

        /// <summary>
        /// False until the player places their first real building. Integrity stays at 100%
        /// and decay does not run until then.
        /// </summary>
        public static bool ColonyIntegrityActive { get; private set; }

        private static Dictionary<Owner, float> _temperature;
        public static Dictionary<Owner, float> Temperature 
        { 
            get 
            {
                EnsureInitialized();
                return _temperature;
            }
            private set => _temperature = value;
        }

        private static Dictionary<Owner, float> _atmosphere;
        public static Dictionary<Owner, float> Atmosphere 
        { 
            get 
            {
                EnsureInitialized();
                return _atmosphere;
            }
            private set => _atmosphere = value;
        }

        private static Dictionary<Owner, float> _water;
        public static Dictionary<Owner, float> Water 
        { 
            get 
            {
                EnsureInitialized();
                return _water;
            }
            private set => _water = value;
        }

        public static event Action<Owner, float> OnOxygenChanged;
        public static event Action<Owner, float> OnTemperatureChanged;
        public static event Action<Owner, float> OnAtmosphereChanged;
        public static event Action<Owner, float> OnWaterChanged;
        public static event Action<Owner, float> OnIntegrityChanged;
        public static event Action<Owner, float> OnPowerChanged;
        public static event Action<Owner, float> OnEnergyChanged;
        public static event Action<Owner, float> OnBiomassChanged;
        public static event Action<Owner, float> OnFoodChanged;

        public static float MineralsToMaterialsRateStatic { get; private set; } = 1f;
        public static float GasToMaterialsRateStatic { get; private set; } = 1f;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO gasSO;

        public static event System.Action<Owner, int> OnMaterialsChanged;

        /// <summary>Fired when the Materials pool hits 0.</summary>
        public static event System.Action OnMaterialsDepleted;

        /// <summary>Whether the colony is in panic mode (upkeep paused, all buildings degraded).</summary>
        public static bool IsPanicMode { get; set; } = false;

        public static void UpdateMaterials(Owner owner, int value)
        {
            if (Materials != null && Materials.ContainsKey(owner))
            {
                Materials[owner] = Mathf.Max(0, value);
                RaiseMaterialsChanged(owner, Materials[owner]);
            }
        }

        public static void RaiseMaterialsChanged(Owner owner, int newValue)
        {
            OnMaterialsChanged?.Invoke(owner, newValue);

            if (newValue <= 0)
            {
                OnMaterialsDepleted?.Invoke();
            }
        }

        public static event Action<Owner, int> OnPopulationChanged;
        public static event Action<Owner, int> OnPopulationLimitChanged;

        public static void UpdatePower(Owner owner, float amount)
        {
            EnsureInitialized();
            Power[owner] = amount;
            OnPowerChanged?.Invoke(owner, amount);
        }

        public static void UpdateEnergy(Owner owner, float amount)
        {
            EnsureInitialized();
            Energy[owner] = amount;
            OnEnergyChanged?.Invoke(owner, amount);
        }

        public static void UpdateBiomass(Owner owner, float value)
        {
            if (Biomass != null && Biomass.ContainsKey(owner))
            {
                GetTerraformingCaps(out _, out _, out _, out float maxBiomass, out _);
                Biomass[owner] = Mathf.Clamp(value, 0f, maxBiomass);
                OnBiomassChanged?.Invoke(owner, Biomass[owner]);
            }
        }

        public static void UpdateFood(Owner owner, float value)
        {
            if (Food != null && Food.ContainsKey(owner))
            {
                Food[owner] = Mathf.Max(0f, value);
                OnFoodChanged?.Invoke(owner, Food[owner]);
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitSceneEvents()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            _materials = null;
            _biomass = null;
            _food = null;
            _power = null;
            _population = null;
            _populationLimit = null;
            _oxygen = null;
            _integrity = null;
            ColonyIntegrityActive = false;
            _temperature = null;
            _atmosphere = null;
            _water = null;
        }

        private static void EnsureInitialized()
        {
            if (_materials != null) return;

            _materials = new Dictionary<Owner, int>();
            _biomass = new Dictionary<Owner, float>();
            _food = new Dictionary<Owner, float>();
            _power = new Dictionary<Owner, float>();
            _energy = new Dictionary<Owner, float>();
            _population = new Dictionary<Owner, int>();
            _populationLimit = new Dictionary<Owner, int>();
            _oxygen = new Dictionary<Owner, float>();
            _integrity = new Dictionary<Owner, float>();
            _temperature = new Dictionary<Owner, float>();
            _atmosphere = new Dictionary<Owner, float>();
            _water = new Dictionary<Owner, float>();

            float initialOxygen = (Instance != null) ? Instance.startingOxygen : 0f;
            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                _materials[owner] = (owner == Owner.Player1) ? 1000 : 0;
                _biomass[owner] = 0f;
                _food[owner] = (owner == Owner.Player1) ? 50f : 0f;
                _power[owner] = 0f;
                _energy[owner] = (owner == Owner.Player1) ? 5f : 0f;
                _population[owner] = 0;
                _populationLimit[owner] = 0;
                _oxygen[owner] = initialOxygen;
                _integrity[owner] = 100f;
                _temperature[owner] = -60f;
                _atmosphere[owner] = 0.01f;
                _water[owner] = 0f;
            }
        }
        private void Awake()
        {
            Instance = this;
            ColonyIntegrityActive = false;

            // Re-initialize to ensure instance settings (startingMaterials) are applied
            _materials = new Dictionary<Owner, int>();
            _biomass = new Dictionary<Owner, float>();
            _food = new Dictionary<Owner, float>();
            _power = new Dictionary<Owner, float>();
            _energy = new Dictionary<Owner, float>();
            _population = new Dictionary<Owner, int>();
            _populationLimit = new Dictionary<Owner, int>();
            _oxygen = new Dictionary<Owner, float>();
            _integrity = new Dictionary<Owner, float>();
            _temperature = new Dictionary<Owner, float>();
            _atmosphere = new Dictionary<Owner, float>();
            _water = new Dictionary<Owner, float>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                _materials.Add(owner, (owner == Owner.Player1) ? startingMaterials : 0);
                _biomass.Add(owner, 0f);
                _food.Add(owner, (owner == Owner.Player1) ? 50f : 0f);
                _power.Add(owner, 0f);
                _energy.Add(owner, (owner == Owner.Player1) ? 5f : 0f);
                _population.Add(owner, 0);
                _populationLimit.Add(owner, 0);
                _oxygen.Add(owner, startingOxygen);
                _integrity.Add(owner, 100f);
                _temperature.Add(owner, -60f);
                _atmosphere.Add(owner, 0.01f);
                _water.Add(owner, 0f);
            }

            MineralsToMaterialsRateStatic = mineralsToMaterialsRate;
            GasToMaterialsRateStatic = gasToMaterialsRate;

            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent); 
            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            
            Owner displayOwner = GameOverManager.MonitoredOwner;
            RaiseMaterialsChanged(displayOwner, Materials[displayOwner]);
            OnEnergyChanged?.Invoke(displayOwner, Energy[displayOwner]);
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
                GetTerraformingCaps(out _, out _, out float maxOxygen, out _, out _);
                Oxygen[owner] = Mathf.Clamp(value, 0f, maxOxygen);
                OnOxygenChanged?.Invoke(owner, Oxygen[owner]);
            }
        }

        public static void UpdateTemperature(Owner owner, float value)
        {
            if (Temperature != null && Temperature.ContainsKey(owner))
            {
                GetTerraformingCaps(out _, out _, out _, out _, out float maxTemperature);
                Temperature[owner] = Mathf.Min(value, maxTemperature);
                OnTemperatureChanged?.Invoke(owner, Temperature[owner]);
            }
        }

        public static void UpdateAtmosphere(Owner owner, float value)
        {
            if (Atmosphere != null && Atmosphere.ContainsKey(owner))
            {
                GetTerraformingCaps(out float maxAtmosphere, out _, out _, out _, out _);
                Atmosphere[owner] = Mathf.Clamp(value, 0f, maxAtmosphere);
                OnAtmosphereChanged?.Invoke(owner, Atmosphere[owner]);
            }
        }

        public static void UpdateWater(Owner owner, float value)
        {
            if (Water != null && Water.ContainsKey(owner))
            {
                GetTerraformingCaps(out _, out float maxWater, out _, out _, out _);
                Water[owner] = Mathf.Max(0f, Mathf.Min(value, maxWater));
                OnWaterChanged?.Invoke(owner, Water[owner]);
            }
        }

        /// <summary>
        /// Climate / terraforming ceilings. Scaled by unlocked sectors, but never below
        /// the current generation's win targets — otherwise gen 1 is soft-locked when
        /// only 1/9 sectors are unlocked (frac≈0.11 atm &lt; 0.25 target → ~42% progress).
        /// </summary>
        public static void GetTerraformingCaps(
            out float maxAtmosphere,
            out float maxWater,
            out float maxOxygen,
            out float maxBiomass,
            out float maxTemperature)
        {
            maxAtmosphere = 1f;
            maxWater = 100f;
            maxOxygen = 100f;
            maxBiomass = 100f;
            maxTemperature = 100f;

            if (SectorManager.Instance?.Sectors != null && SectorManager.Instance.Sectors.Count > 0)
            {
                int total = SectorManager.Instance.Sectors.Count;
                int unlocked = 0;
                foreach (var s in SectorManager.Instance.Sectors)
                {
                    if (s != null && !s.IsLocked) unlocked++;
                }

                unlocked = Mathf.Max(1, unlocked);
                float frac = unlocked / (float)total;
                maxAtmosphere = frac;
                maxWater = frac * 100f;
                maxOxygen = frac * 100f;
                maxBiomass = frac * 100f;
                maxTemperature = frac * 100f;
            }

            // Always floor to at least generation-1 win formulas so caps cannot soft-lock
            // even if GenerationManager is missing or mid-init.
            maxAtmosphere = Mathf.Max(maxAtmosphere, GenerationManager.SectorAtmosphereDelta);
            maxWater = Mathf.Max(maxWater, GenerationManager.SectorWaterDelta);
            maxTemperature = Mathf.Max(maxTemperature, -60f + GenerationManager.SectorTemperatureDelta);

            var gm = GenerationManager.Instance;
            if (gm != null && !gm.IsExpansionPhase)
            {
                int gen = Mathf.Max(1, gm.CurrentGeneration);
                maxAtmosphere = Mathf.Max(maxAtmosphere, gm.GetTargetAtmosphere(gen));
                maxWater = Mathf.Max(maxWater, gm.GetTargetWater(gen));
                maxTemperature = Mathf.Max(maxTemperature, gm.GetTargetTemperature(gen));

                // Round targets are baseline + fixed sector delta — must clear those caps
                // even when the planet is already warm from prior sectors.
                maxAtmosphere = Mathf.Max(maxAtmosphere, gm.GetRoundAtmosphereTarget());
                maxWater = Mathf.Max(maxWater, gm.GetRoundWaterTarget());
                maxTemperature = Mathf.Max(maxTemperature, gm.GetRoundTemperatureTarget());

                // Win-line must not freeze the top-bar meters. Sector completion still uses
                // baselines/deltas; absolute Temp/Atmos/Water may keep rising toward the next
                // generation's cumulative target so powered climate buildings stay visible.
                int nextGen = Mathf.Min(gen + 1, Mathf.Max(gen, gm.MaxGenerations + 1));
                maxAtmosphere = Mathf.Max(maxAtmosphere, gm.GetTargetAtmosphere(nextGen));
                maxWater = Mathf.Max(maxWater, gm.GetTargetWater(nextGen));
                maxTemperature = Mathf.Max(maxTemperature, gm.GetTargetTemperature(nextGen));

                if (_atmosphere != null && _atmosphere.TryGetValue(Owner.Player1, out float curAtmos))
                {
                    float atmosNeed = gm.GetRoundAtmosphereTarget() - curAtmos;
                    if (atmosNeed > 0.0001f)
                        maxAtmosphere = Mathf.Max(maxAtmosphere, gm.GetRoundAtmosphereTarget());
                }

                if (gm.CurrentMilestoneTarget > 0f)
                {
                    if (gm.CurrentMilestoneType == MilestoneType.Oxygen)
                        maxOxygen = Mathf.Max(maxOxygen, gm.CurrentMilestoneTarget);
                }

                if (gm.CurrentMilestoneType == MilestoneType.Temperature)
                    maxTemperature = Mathf.Max(maxTemperature, gm.GetRoundTemperatureTarget());
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

        public static void ResetAllSupplies(Owner owner)
        {
            if (Materials != null && Materials.ContainsKey(owner))
                Materials[owner] = 0;
            if (Biomass != null && Biomass.ContainsKey(owner))
                Biomass[owner] = 0f;
            if (Power != null && Power.ContainsKey(owner))
                Power[owner] = 0f;
            if (Population != null && Population.ContainsKey(owner))
                Population[owner] = 0;
            if (PopulationLimit != null && PopulationLimit.ContainsKey(owner))
                PopulationLimit[owner] = 0;
            if (Oxygen != null && Oxygen.ContainsKey(owner))
                Oxygen[owner] = 0f;
            if (Integrity != null && Integrity.ContainsKey(owner))
                Integrity[owner] = 100f;
            if (Temperature != null && Temperature.ContainsKey(owner))
                Temperature[owner] = -60f;
            if (Atmosphere != null && Atmosphere.ContainsKey(owner))
                Atmosphere[owner] = 0.01f;
            if (Water != null && Water.ContainsKey(owner))
                Water[owner] = 0f;
            
            // Trigger events to notify UI
            OnMaterialsChanged?.Invoke(owner, Materials[owner]);
            OnBiomassChanged?.Invoke(owner, Biomass[owner]);
            OnPowerChanged?.Invoke(owner, Power[owner]);
            OnPopulationChanged?.Invoke(owner, Population[owner]);
            OnPopulationLimitChanged?.Invoke(owner, PopulationLimit[owner]);
            OnOxygenChanged?.Invoke(owner, Oxygen[owner]);
            OnIntegrityChanged?.Invoke(owner, Integrity[owner]);
            OnTemperatureChanged?.Invoke(owner, Temperature[owner]);
            OnAtmosphereChanged?.Invoke(owner, Atmosphere[owner]);
            OnWaterChanged?.Invoke(owner, Water[owner]);
        }

        /// <summary>
        /// Starts colony integrity tracking after the first gameplay-placed building completes.
        /// Editor-placed dummies and the UCC hub are ignored.
        /// </summary>
        public static void BeginColonyIntegrityIfNeeded(AbstractCommandable commandable)
        {
            if (ColonyIntegrityActive || commandable == null) return;
            if (commandable is not BaseBuilding building) return;
            if (!CountsTowardIntegrity(building)) return;
            if (building.Owner != GameOverManager.MonitoredOwner) return;
            if (!building.name.Contains("(Clone)")) return;

            ColonyIntegrityActive = true;
            float integrity = CalculateIntegrity(building.Owner);
            if (integrity <= 0f) integrity = 100f;
            UpdateIntegrity(building.Owner, integrity);
            Debug.Log($"[Supplies] Colony integrity tracking started after '{building.name}' was placed.");
        }

        public static float CalculateIntegrity(Owner owner)
        {
            if (!ColonyIntegrityActive) return 100f;

            var commandables = AbstractCommandable.ActiveCommandables;
            long totalMaxHP = 0;
            long totalCurrentHP = 0;
            bool foundAny = false;

            foreach (var c in commandables)
            {
                if (!CountsTowardIntegrity(c)) continue;
                if (c.Owner == owner)
                {
                    totalMaxHP += c.MaxHealth;
                    totalCurrentHP += c.CurrentHealth;
                    foundAny = true;
                }
            }

            // No commandables found — no structures exist, so integrity is 0%.
            if (!foundAny) return 0f;

            // If no health has been initialized yet (all commandables have 0 MaxHealth),
            // return NaN would crash the bar — treat as full health.
            if (totalMaxHP == 0) return 100f;

            return ((float)totalCurrentHP / totalMaxHP) * 100f;
        }

        /// <summary>
        /// Colony integrity reflects player-built infrastructure only.
        /// Excludes the invulnerable UCC hub and the hidden DecayStarter stand-in.
        /// </summary>
        private static bool CountsTowardIntegrity(AbstractCommandable c)
        {
            if (c == null) return false;
            if (c is GlobalCommander || c is DecayStarter) return false;
            if (c.gameObject.name.Contains("Universal Command Center")) return false;
            if (c.MaxHealth >= 90000) return false;
            return true;
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
            bool isMinerals = (mineralsSO != null && evt.Supply == mineralsSO) || sName.Contains("minerals") || sName.Contains("iron") || sName.Contains("regolith");
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
                float curBiomass = Biomass != null && Biomass.TryGetValue(evt.Owner, out float b) ? b : 0f;
                UpdateBiomass(evt.Owner, curBiomass + evt.Amount);
                return;
            }
        }
    }
}
