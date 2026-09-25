using System;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Combolands-style run spine: fixed named Acts (independent of sectors).
    /// Act clear = Colony Score + planet climate gains. Geography expands via Command Posts.
    /// Run win = all Acts cleared AND every sector terraformed.
    /// </summary>
    public class ColonyActManager : MonoBehaviour
    {
        public static ColonyActManager Instance { get; private set; }

        public static event Action OnActStateChanged;
        public static event Action<int> OnActCleared; // act index 1-based
        /// <summary>Fired after an Act clears when another Act remains — open the between-Act shop.</summary>
        public static event Action OnBetweenActShopRequested;
        public static event Action OnRunVictory;
        public static event Action OnActFailed;

        public struct ActDef
        {
            public string Name;
            public int TargetScore;
            public int WeekBudget;
        }

        private readonly System.Collections.Generic.List<ActDef> acts = new();

        /// <summary>Habitability points needed for full Living look (cumulative).</summary>
        private const float HabitabilityForLiving = 80f;
        private const float ScoreCarryFraction = 0.25f;
        private const int AdjacentBonus = 3;
        private const int SameTagBonus = 6;
        private const int PowerConsumerBonus = 7;
        private const int AnchorBonus = 5;
        private const int ClimatePairBonus = 8;
        private const int LifeSynergyBonus = 6;
        private const int AdjacencySoftCap = 36;
        private const int PowerGeneratorScoreBonus = 4;
        private const int GeologyMatchBonus = 8;

        /// <summary>Once-per-Act climate-pair pulses (sectorIndex:tagA:tagB).</summary>
        private readonly HashSet<string> climatePairPulseKeysThisAct = new(StringComparer.Ordinal);

        private int actIndex; // 0-based
        private int colonyScore;
        private int weeksRemaining;
        private float habitability;
        private bool runEnded;
        private bool started;
        private int terraCoins;
        private float scoreMultiplier = 1f;
        private int adjacencyBonusExtra;
        private int geologyBonusExtra;
        private int pendingWeekBonus;
        private int powerScoreBonusExtra;

        private float baselineTemperature = -60f;
        private float baselineAtmosphere = 0.01f;
        private float baselineWater = 0f;

        /// <summary>
        /// Per-sector climate contributed this Act (Temp °C, Atmos atm, Water %).
        /// Each sector may supply at most (ActDelta / sectorCount) so one Air farm
        /// cannot clear multiple sectors' worth of the Act meters.
        /// </summary>
        private readonly Dictionary<int, Vector3> sectorClimateContributed = new();
        private readonly Dictionary<int, float> sectorOxygenContributed = new();

        public float BaselineTemperature => baselineTemperature;
        public float BaselineAtmosphere => baselineAtmosphere;
        public float BaselineWater => baselineWater;

        /// <summary>Hard ceiling for planet meters this Act (baseline + required delta).</summary>
        public float ActAtmosphereCeiling
        {
            get
            {
                GetActClimateRequirements(out _, out float needA, out _);
                return baselineAtmosphere + needA;
            }
        }
        public float ActTemperatureCeiling
        {
            get
            {
                GetActClimateRequirements(out float needT, out _, out _);
                return baselineTemperature + needT;
            }
        }
        public float ActWaterCeiling
        {
            get
            {
                GetActClimateRequirements(out _, out _, out float needW);
                return baselineWater + needW;
            }
        }

        private string statusBanner = string.Empty;
        private float statusBannerUntil;

        public static event Action<int> OnTerraCoinsChanged;

        public int CurrentAct => actIndex + 1;
        public int TotalActs => Mathf.Max(1, acts.Count);
        public string CurrentActName => CurrentActDef.Name;
        /// <summary>Name of the Act the between-Act shop is preparing for (still on cleared Act index).</summary>
        public string UpcomingActName
        {
            get
            {
                int next = actIndex + 1;
                if (next < 0 || next >= acts.Count) return string.Empty;
                return acts[next].Name;
            }
        }
        public int UpcomingActNumber => actIndex + 2;
        public int ColonyScore => colonyScore;
        public int TargetScore => CurrentActDef.TargetScore;
        public int WeeksRemaining => weeksRemaining;
        public int TerraCoins => terraCoins;
        /// <summary>Deprecated Act↔sector coupling — camera/sector focus is player-driven (Q/E).</summary>
        public int FocusSectorIndex => 0;
        public float Habitability => habitability;
        public float HabitabilityProgress => Mathf.Clamp01(habitability / HabitabilityForLiving);
        public bool IsRunEnded => runEnded;
        /// <summary>True while Colony Acts are the active win/lose spine (ignore mining depletion).</summary>
        public bool IsRunActive => started && !runEnded;
        public bool IsBetweenActs { get; private set; }
        public bool IsScoreMet => colonyScore >= TargetScore;
        public bool IsClimateMet => GetClimateProgress(out _, out _, out _) >= 0.999f;
        public bool IsActComplete => IsScoreMet && IsClimateMet
            && (actIndex < acts.Count - 1 || AreAllSectorsTerraformed());

        private ActDef CurrentActDef
        {
            get
            {
                if (acts.Count == 0)
                    return new ActDef { Name = "Establish", TargetScore = 30, WeekBudget = 18 };
                return acts[Mathf.Clamp(actIndex, 0, acts.Count - 1)];
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(ColonyActManager));
            go.AddComponent<ColonyActManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            Supplies.OnTemperatureChanged += HandleClimateChanged;
            Supplies.OnAtmosphereChanged += HandleClimateChanged;
            Supplies.OnWaterChanged += HandleClimateChanged;
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            Supplies.OnTemperatureChanged -= HandleClimateChanged;
            Supplies.OnAtmosphereChanged -= HandleClimateChanged;
            Supplies.OnWaterChanged -= HandleClimateChanged;
        }

        private void Start()
        {
            if (!started && PlanetGenerator.Instance != null)
                BeginRun();
        }

        private void HandlePlanetGenerated() => BeginRun();

        private void HandleClimateChanged(Owner owner, float _)
        {
            if (owner != Owner.Player1) return;
            OnActStateChanged?.Invoke();
            TryResolveWeekExhaustion();
        }

        public void BeginRun()
        {
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count == 0)
                SectorManager.Instance.InitializeSectors();

            BuildFixedActLadder();
            actIndex = 0;
            colonyScore = 0;
            habitability = 0f;
            runEnded = false;
            IsBetweenActs = false;
            terraCoins = 0;
            scoreMultiplier = 1f;
            adjacencyBonusExtra = 0;
            geologyBonusExtra = 0;
            pendingWeekBonus = 0;
            powerScoreBonusExtra = 0;
            weeksRemaining = CurrentActDef.WeekBudget;
            started = true;
            RecordClimateBaselines();
            RevealAllSectorFeatures();
            CardDeckController.Instance?.NotifyActClimateComboReset();
            GameDevTV.RTS.Utilities.SectorMiningDroneBootstrap.ResetForNewRun();
            CardDeckController.Instance?.RefreshHand();
            Debug.Log($"[ColonyActManager] Act 1/{TotalActs} {CurrentActName}: score 0/{TargetScore}, weeks {weeksRemaining} (Acts ≠ sectors)");
            OnActStateChanged?.Invoke();
            OnTerraCoinsChanged?.Invoke(terraCoins);
            ClimateVisualStages.Instance?.NotifyHabitabilityChanged();
        }

        /// <summary>Fixed Combolands-style Act ladder — independent of map sector count.</summary>
        private void BuildFixedActLadder()
        {
            acts.Clear();
            acts.Add(new ActDef { Name = "Establish", TargetScore = 30, WeekBudget = 18 });
            acts.Add(new ActDef { Name = "Survive", TargetScore = 140, WeekBudget = 16 });
            acts.Add(new ActDef { Name = "Settle", TargetScore = 220, WeekBudget = 16 });
            acts.Add(new ActDef { Name = "Expand", TargetScore = 300, WeekBudget = 16 });
            acts.Add(new ActDef { Name = "Thrive", TargetScore = 400, WeekBudget = 18 });
        }

        private static void RevealAllSectorFeatures()
        {
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return;
            for (int i = 0; i < sm.Sectors.Count; i++)
            {
                var sector = sm.Sectors[i];
                if (sector == null) continue;
                sector.IsExplored = true;
                sector.IsDiscovered = true;
                DiscoverySystem.RevealFeaturesForSector(sector);
            }
        }

        /// <summary>Climate tags present anywhere on the planet (unlocked/claimed buildable sectors).</summary>
        public void GetFocusSectorClimatePresence(out bool heat, out bool air, out bool water)
        {
            heat = air = water = false;
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;

                GetTileValues(b.ResolvedBuildingSO, out _, out _, out string tag);
                if (tag == "Heat") heat = true;
                else if (tag == "Air") air = true;
                else if (tag == "Water") water = true;
            }
        }

        /// <summary>Sector is terraformed when it has a player CP and Heat+Air+Water tiles.</summary>
        public static bool IsSectorTerraformed(SectorManager.Sector sector)
        {
            if (sector == null) return false;
            if (!GameDevTV.RTS.Utilities.SectorColonization.SectorHasCommandPost(sector)) return false;

            bool heat = false, air = false, water = false;
            var sm = SectorManager.Instance;
            if (sm == null) return false;

            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (sm.GetNearestSector(b.transform.position) != sector) continue;

                GetTileValues(b.ResolvedBuildingSO, out _, out _, out string tag);
                if (tag == "Heat") heat = true;
                else if (tag == "Air") air = true;
                else if (tag == "Water") water = true;
            }

            return heat && air && water;
        }

        public static bool AreAllSectorsTerraformed()
        {
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null || sm.Sectors.Count == 0) return false;
            for (int i = 0; i < sm.Sectors.Count; i++)
            {
                if (!IsSectorTerraformed(sm.Sectors[i])) return false;
            }
            return true;
        }

        public static int CountTerraformedSectors(out int total)
        {
            total = 0;
            int done = 0;
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null) return 0;
            total = sm.Sectors.Count;
            for (int i = 0; i < sm.Sectors.Count; i++)
            {
                if (IsSectorTerraformed(sm.Sectors[i])) done++;
            }
            return done;
        }

        private void RecordClimateBaselines()
        {
            baselineTemperature = -60f;
            baselineAtmosphere = 0.01f;
            baselineWater = 0f;
            if (Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float t))
                baselineTemperature = t;
            if (Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a))
                baselineAtmosphere = a;
            if (Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float w))
                baselineWater = w;

            sectorClimateContributed.Clear();
            sectorOxygenContributed.Clear();
            climatePairPulseKeysThisAct.Clear();
        }

        /// <summary>
        /// How many sectors share the published climate reference deltas (at least 1).
        /// Per-sector contribution cap = reference / N. Never replace this with claimed-only counts.
        /// </summary>
        public static int ClimateBudgetSectorCount
        {
            get
            {
                int n = SectorManager.Instance?.Sectors?.Count ?? 0;
                return Mathf.Max(1, n);
            }
        }

        /// <summary>
        /// Max Temp/Atmos/Water one sector may contribute this Act (= reference Δ / N).
        /// This per-sector share is the locked design — do not retune the 1/N split.
        /// </summary>
        public void GetPerSectorClimateBudgets(out float maxTemp, out float maxAtmos, out float maxWater)
        {
            int n = ClimateBudgetSectorCount;
            maxTemp = GenerationManager.SectorTemperatureDelta / n;
            maxAtmos = GenerationManager.SectorAtmosphereDelta / n;
            maxWater = GenerationManager.SectorWaterDelta / n;
        }

        /// <summary>
        /// Climate gains needed to clear this Act = one sector's share (Δ / N).
        /// More sectors → smaller Act climate need; one filled sector can clear climate.
        /// </summary>
        public void GetActClimateRequirements(out float needTemp, out float needAtmos, out float needWater)
        {
            GetPerSectorClimateBudgets(out needTemp, out needAtmos, out needWater);
        }

        /// <summary>
        /// Clamp proposed climate adds to the building's sector remaining 1/N budget
        /// and the Act need (also 1/N). Returns false when nothing can be applied.
        /// </summary>
        public bool TryApplySectorClimateContribution(
            Vector3 worldPos,
            ref float tempAdd,
            ref float atmosAdd,
            ref float waterAdd)
        {
            if (!started || runEnded || IsBetweenActs)
            {
                tempAdd = atmosAdd = waterAdd = 0f;
                return false;
            }

            GetPerSectorClimateBudgets(out float maxT, out float maxA, out float maxW);
            int sectorIndex = ResolveSectorIndex(worldPos);
            if (!sectorClimateContributed.TryGetValue(sectorIndex, out Vector3 used))
                used = Vector3.zero;

            GetActClimateRequirements(out float needT, out float needA, out float needW);
            GetClimateGains(out float tGain, out float aGain, out float wGain);
            float actRemainT = Mathf.Max(0f, needT - tGain);
            float actRemainA = Mathf.Max(0f, needA - aGain);
            float actRemainW = Mathf.Max(0f, needW - wGain);

            float remainT = Mathf.Min(Mathf.Max(0f, maxT - used.x), actRemainT);
            float remainA = Mathf.Min(Mathf.Max(0f, maxA - used.y), actRemainA);
            float remainW = Mathf.Min(Mathf.Max(0f, maxW - used.z), actRemainW);

            tempAdd = Mathf.Clamp(tempAdd, 0f, remainT);
            atmosAdd = Mathf.Clamp(atmosAdd, 0f, remainA);
            waterAdd = Mathf.Clamp(waterAdd, 0f, remainW);

            if (tempAdd <= 0f && atmosAdd <= 0f && waterAdd <= 0f)
                return false;

            used.x += tempAdd;
            used.y += atmosAdd;
            used.z += waterAdd;
            sectorClimateContributed[sectorIndex] = used;
            return true;
        }

        /// <summary>
        /// Oxygen is flavor HUD (0–100%). Each sector may fill at most 100/N % of the planet meter.
        /// </summary>
        public bool TryApplySectorOxygenContribution(Vector3 worldPos, ref float oxygenAdd)
        {
            if (oxygenAdd <= 0f) return false;
            int n = ClimateBudgetSectorCount;
            float maxPerSector = 100f / n;
            int sectorIndex = ResolveSectorIndex(worldPos);

            // Track oxygen in the dictionary's unused w channel via a parallel map.
            if (!sectorOxygenContributed.TryGetValue(sectorIndex, out float used))
                used = 0f;

            float remain = Mathf.Max(0f, maxPerSector - used);
            // Also don't push planet Oxygen past 100.
            float cur = Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
            remain = Mathf.Min(remain, Mathf.Max(0f, 100f - cur));

            oxygenAdd = Mathf.Clamp(oxygenAdd, 0f, remain);
            if (oxygenAdd <= 0f) return false;

            sectorOxygenContributed[sectorIndex] = used + oxygenAdd;
            return true;
        }

        private static int ResolveSectorIndex(Vector3 worldPos)
        {
            var sm = SectorManager.Instance;
            if (sm?.Sectors == null || sm.Sectors.Count == 0) return 0;
            var nearest = sm.GetNearestSector(worldPos);
            if (nearest == null) return 0;
            for (int i = 0; i < sm.Sectors.Count; i++)
            {
                if (sm.Sectors[i] == nearest) return i;
            }
            return 0;
        }

        /// <summary>
        /// 0–1 bottleneck of Temp / Atmos / Water toward this Act's need (Δ / N each).
        /// One sector filling its 1/N share clears Act climate; more map sectors → smaller need.
        /// </summary>
        public float GetClimateProgress(out float tempProgress, out float atmosProgress, out float waterProgress)
        {
            GetClimateGains(out float tempGain, out float atmosGain, out float waterGain);
            GetActClimateRequirements(out float needT, out float needA, out float needW);
            tempProgress = DeltaProgressFromGain(tempGain, needT);
            atmosProgress = DeltaProgressFromGain(atmosGain, needA);
            waterProgress = DeltaProgressFromGain(waterGain, needW);
            return Mathf.Min(tempProgress, Mathf.Min(atmosProgress, waterProgress));
        }

        /// <summary>Absolute gains this Act from baselines (Temp °C, Atmos atm, Water %).</summary>
        public void GetClimateGains(out float tempGain, out float atmosGain, out float waterGain)
        {
            float temp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float t)
                ? t : baselineTemperature;
            float atmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a)
                ? a : baselineAtmosphere;
            float water = Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float w)
                ? w : baselineWater;

            tempGain = temp - baselineTemperature;
            atmosGain = atmos - baselineAtmosphere;
            waterGain = water - baselineWater;
        }

        private static float DeltaProgressFromGain(float gained, float requiredDelta)
        {
            if (requiredDelta <= 0.0001f) return 1f;
            if (gained + 0.0005f >= requiredDelta) return 1f;
            return Mathf.Clamp01(gained / requiredDelta);
        }

        public void ShowStatusBanner(string richText, float seconds = 5f)
        {
            statusBanner = richText ?? string.Empty;
            statusBannerUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
            OnActStateChanged?.Invoke();
        }

        public void ClearStatusBanner()
        {
            statusBanner = string.Empty;
            statusBannerUntil = 0f;
            OnActStateChanged?.Invoke();
        }

        private void Update()
        {
            // Drone builds may finish (or be cancelled) after weeks hit 0 — resolve then.
            TryResolveWeekExhaustion();
        }

        /// <summary>Spend one week when a hand card is committed (played / placed).</summary>
        public void SpendWeek() => SpendWeeks(1);

        /// <summary>Spend multiple Act weeks for a card play (clamped to remaining).</summary>
        public void SpendWeeks(int weeks)
        {
            if (!started || runEnded || IsBetweenActs) return;
            if (weeksRemaining <= 0 || weeks <= 0) return;

            int spent = Mathf.Min(weeks, weeksRemaining);
            weeksRemaining -= spent;
            // Climate tiles produce once per spent week (not real-time).
            ApplyWeeklyClimateFromBoard(spent);
            Debug.Log($"[ColonyActManager] Spent {spent} week(s) — {weeksRemaining} left (score {colonyScore}/{TargetScore})");
            OnActStateChanged?.Invoke();
            TryResolveWeekExhaustion();
        }

        /// <summary>
        /// Under Colony Acts, terraforming buildings generate on week spend.
        /// Config Temperature/Atmosphere/WaterGeneration are amounts <b>per week</b>
        /// (× efficiency × adjacency combo), applied once per spent week.
        /// </summary>
        private static void ApplyWeeklyClimateFromBoard(int weeks)
        {
            if (weeks <= 0) return;
            var buildings = BaseBuilding.ActiveBuildings;
            if (buildings == null || buildings.Count == 0) return;

            // One discrete week at a time so per-sector 1/N budgets share fairly across tiles.
            for (int w = 0; w < weeks; w++)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    BaseBuilding building = buildings[i];
                    if (building == null) continue;
                    building.TickClimateGeneration(1f);
                }
            }
        }

        /// <summary>
        /// Legacy hook after hand fill — combo offers now require edge adjacency on place only.
        /// </summary>
        public void TryRefreshClimateComboFromPresence()
        {
            // Intentionally empty: presence-based unlocks removed (stacking required).
        }

        /// <summary>Grant score when a building finishes (base + adjacency stacking).</summary>
        public void GrantTileScore(BuildingSO building) => GrantTileScore(building, null);

        public void GrantTileScore(BaseBuilding placed)
        {
            if (placed == null) return;
            GrantTileScore(placed.ResolvedBuildingSO, placed);
        }

        private void GrantTileScore(BuildingSO building, BaseBuilding placed)
        {
            if (!started || runEnded || IsBetweenActs) return;
            GetTileValues(building, out int score, out float hab, out string tag);
            int adjBonus = 0;
            int neighbors = 0;
            int geoBonus = 0;
            int powerBonus = 0;
            if (placed != null)
            {
                adjBonus = CalculateAdjacencyBonus(placed, tag, out neighbors);
                adjBonus += adjacencyBonusExtra * Mathf.Max(0, neighbors);
                TryOfferComboCardsFromAdjacency(placed, tag);
                TryClimatePairPulse(placed, tag);
                geoBonus = TryApplyGeologyPlacementRewards(placed, building, tag);
                if (tag == "Power")
                    powerBonus = PowerGeneratorScoreBonus + powerScoreBonusExtra;
            }

            int raw = score + adjBonus + geoBonus + powerBonus;
            int total = Mathf.Max(0, Mathf.RoundToInt(raw * scoreMultiplier));
            if (total <= 0 && hab <= 0f) return;

            colonyScore += total;
            habitability += hab;
            if (placed != null)
                GameDevTV.RTS.UI.PlacementScorePopup.Spawn(placed.transform.position, total);

            Debug.Log($"[ColonyActManager] +{total} score (base {score} adj {adjBonus} geo {geoBonus} power {powerBonus} ×{scoreMultiplier:F2}) → {colonyScore}/{TargetScore}");
            OnActStateChanged?.Invoke();
            ClimateVisualStages.Instance?.NotifyHabitabilityChanged();
            TryResolveWeekExhaustion();
        }

        /// <summary>
        /// Mine / aquifer / geothermal on matching deposits: extra score + terraforming supplies.
        /// </summary>
        private int TryApplyGeologyPlacementRewards(BaseBuilding placed, BuildingSO building, string tag)
        {
            if (placed == null || building == null) return 0;
            Vector3 pos = placed.transform.position;
            int bonus = 0;

            if (BuildingSiteRegistry.IsMineBuilding(building)
                && DiscoverySystem.IsOnDiscoveredMineDeposit(building, pos))
            {
                bonus += GeologyMatchBonus + geologyBonusExtra;
                if (DiscoverySystem.TryGetMineResourceType(building, out string resourceType))
                    GrantGeologyResourcePulse(resourceType, pos);
            }

            string name = building.Name ?? string.Empty;
            bool isAquifer = name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSubglacial = name.IndexOf("Subglacial", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isGeo = name.IndexOf("Geothermal", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isLavaTubeBld = name.IndexOf("Lava Tube", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFaultBld = name.IndexOf("Magnetic Shield", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Sector Command", System.StringComparison.OrdinalIgnoreCase) >= 0;

            var nearest = SectorManager.Instance?.GetNearestSector(pos);
            if (nearest != null && nearest.Feature != SectorManager.SectorFeature.None)
            {
                if (isAquifer && nearest.Feature == SectorManager.SectorFeature.WaterDeposit)
                {
                    bonus += GeologyMatchBonus + geologyBonusExtra;
                    GrantGeologyResourcePulse("Water", pos);
                }
                else if (isSubglacial && nearest.Feature == SectorManager.SectorFeature.Glacier)
                {
                    bonus += GeologyMatchBonus + geologyBonusExtra;
                    GrantGeologyResourcePulse("Water", pos);
                }
                else if (isGeo && nearest.Feature == SectorManager.SectorFeature.Volcano)
                {
                    bonus += GeologyMatchBonus + geologyBonusExtra;
                    GrantGeologyResourcePulse("Heat", pos);
                }
                else if (isLavaTubeBld && nearest.Feature == SectorManager.SectorFeature.LavaTube)
                {
                    bonus += GeologyMatchBonus + geologyBonusExtra;
                    GrantGeologyResourcePulse("Heat", pos);
                }
                else if (isFaultBld && nearest.Feature == SectorManager.SectorFeature.FaultLine)
                {
                    bonus += GeologyMatchBonus + geologyBonusExtra;
                    GrantGeologyResourcePulse("Heat", pos);
                }
            }

            return bonus;
        }

        private void GrantGeologyResourcePulse(string resourceType, Vector3 worldPos)
        {
            if (string.IsNullOrEmpty(resourceType)) return;

            float tempAdd = 0f;
            float atmosAdd = 0f;
            float waterAdd = 0f;

            if (resourceType.IndexOf("Mineral", System.StringComparison.OrdinalIgnoreCase) >= 0
                || resourceType.IndexOf("Iron", System.StringComparison.OrdinalIgnoreCase) >= 0
                || resourceType.IndexOf("Regolith", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tempAdd = 1.5f;
            }
            else if (resourceType.IndexOf("Gas", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                atmosAdd = 0.03f;
            }
            else if (resourceType.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                waterAdd = 1.5f;
            }
            else if (resourceType.IndexOf("Heat", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tempAdd = 2f;
            }
            else return;

            if (!TryApplySectorClimateContribution(worldPos, ref tempAdd, ref atmosAdd, ref waterAdd))
                return;

            if (tempAdd > 0f)
            {
                float t = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float tv) ? tv : -60f;
                Supplies.UpdateTemperature(Owner.Player1, t + tempAdd);
            }
            if (atmosAdd > 0f)
            {
                float a = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float av) ? av : 0.01f;
                Supplies.UpdateAtmosphere(Owner.Player1, a + atmosAdd);
            }
            if (waterAdd > 0f)
            {
                float w = Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float wv) ? wv : 0f;
                Supplies.UpdateWater(Owner.Player1, w + waterAdd);
            }
        }

        /// <summary>
        /// Climate generation multiplier from orthogonal neighbors.
        /// Does not change 1/N sector budgets — only fills them faster.
        /// </summary>
        public static float GetClimateComboRateMultiplier(BaseBuilding building)
        {
            if (building == null) return 1f;
            GetTileValues(building.ResolvedBuildingSO, out _, out _, out string tag);
            if (tag != "Heat" && tag != "Air" && tag != "Water") return 1f;

            var neighbors = new List<BaseBuilding>(4);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(
                ColonyTileGrid.WorldToCell(building.transform.position), building.Owner, neighbors);

            bool sameTag = false;
            bool climatePair = false;
            bool hasHeat = tag == "Heat";
            bool hasAir = tag == "Air";
            bool hasWater = tag == "Water";
            bool powerNeighbor = false;

            foreach (var other in neighbors)
            {
                if (other == null || other == building) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                if (otherTag == tag) sameTag = true;
                if (IsClimatePair(tag, otherTag)) climatePair = true;
                if (otherTag == "Heat") hasHeat = true;
                if (otherTag == "Air") hasAir = true;
                if (otherTag == "Water") hasWater = true;
                if (otherTag == "Power") powerNeighbor = true;
            }

            float mult = 1f;
            bool miniTrio = hasHeat && hasAir && hasWater;
            if (miniTrio) mult = 2.25f;
            else if (climatePair) mult = 1.75f;
            else if (sameTag) mult = 1.35f;

            if (powerNeighbor) mult += 0.25f;
            return Mathf.Min(mult, 2.5f);
        }

        /// <summary>
        /// Edge-adjacency combo offers (once per Act per recipe). Presence unlocks removed.
        /// </summary>
        private void TryOfferComboCardsFromAdjacency(BaseBuilding placed, string tag)
        {
            if (placed == null || CardDeckController.Instance == null) return;
            if (string.IsNullOrEmpty(tag)) return;

            var neighbors = new List<BaseBuilding>(4);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(
                ColonyTileGrid.WorldToCell(placed.transform.position), Owner.Player1, neighbors);

            foreach (var other in neighbors)
            {
                if (other == null || other == placed) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                if (string.IsNullOrEmpty(otherTag)) continue;

                // Climate trio: missing third.
                string third = ThirdClimateTag(tag, otherTag);
                if (!string.IsNullOrEmpty(third))
                    TryQueueComboOffer(ClimateTagToGoalKey(third), tag, otherTag);

                // Power + Industry → Life
                if ((tag == "Power" && otherTag == "Industry") || (tag == "Industry" && otherTag == "Power"))
                    TryQueueComboOffer("OXYGEN", "Power", "Industry");

                // Anchor + Power → Industry if focused sector has a mine deposit, else Heat
                if ((tag == "Anchor" && otherTag == "Power") || (tag == "Power" && otherTag == "Anchor"))
                {
                    string goal = FocusSectorHasMineableDeposit() ? "MATERIALS" : "TEMPERATURE";
                    TryQueueComboOffer(goal, "Anchor", "Power");
                }

                // Life + Water → housing / population
                if ((tag == "Life" && otherTag == "Water") || (tag == "Water" && otherTag == "Life"))
                    TryQueueComboOffer("POPULATION", "Life", "Water");
            }
        }

        private static bool FocusSectorHasMineableDeposit()
        {
            var focus = SectorManager.Instance?.ActiveSector;
            if (focus == null) return false;
            foreach (var hr in UnityEngine.Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (!DiscoverySystem.IsMineableResourceType(hr.ResourceTypeName)) continue;
                if (SectorManager.Instance.GetNearestSector(hr.transform.position) == focus)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// First climate-pair adjacency this Act/sector: pulse ~7% of remaining sector budget.
        /// Still clamped by TryApplySectorClimateContribution (1/N unchanged).
        /// </summary>
        private void TryClimatePairPulse(BaseBuilding placed, string tag)
        {
            if (placed == null) return;
            if (tag != "Heat" && tag != "Air" && tag != "Water") return;

            var neighbors = new List<BaseBuilding>(4);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(
                ColonyTileGrid.WorldToCell(placed.transform.position), Owner.Player1, neighbors);

            foreach (var other in neighbors)
            {
                if (other == null || other == placed) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                if (!IsClimatePair(tag, otherTag)) continue;

                int sectorIndex = ResolveSectorIndex(placed.transform.position);
                string keyA = string.CompareOrdinal(tag, otherTag) <= 0 ? tag : otherTag;
                string keyB = string.CompareOrdinal(tag, otherTag) <= 0 ? otherTag : tag;
                string pulseKey = $"{sectorIndex}:{keyA}:{keyB}";
                if (!climatePairPulseKeysThisAct.Add(pulseKey)) continue;

                GetPerSectorClimateBudgets(out float maxT, out float maxA, out float maxW);
                if (!sectorClimateContributed.TryGetValue(sectorIndex, out Vector3 used))
                    used = Vector3.zero;

                float tempAdd = Mathf.Max(0f, maxT - used.x) * 0.07f;
                float atmosAdd = Mathf.Max(0f, maxA - used.y) * 0.07f;
                float waterAdd = Mathf.Max(0f, maxW - used.z) * 0.07f;

                // Only pulse channels involved in this pair.
                if (tag != "Heat" && otherTag != "Heat") tempAdd = 0f;
                if (tag != "Air" && otherTag != "Air") atmosAdd = 0f;
                if (tag != "Water" && otherTag != "Water") waterAdd = 0f;

                Vector3 pos = placed.transform.position;
                if (!TryApplySectorClimateContribution(pos, ref tempAdd, ref atmosAdd, ref waterAdd))
                    continue;

                if (tempAdd > 0f)
                {
                    float t = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float tv) ? tv : -60f;
                    Supplies.UpdateTemperature(Owner.Player1, t + tempAdd);
                }
                if (atmosAdd > 0f)
                {
                    float a = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float av) ? av : 0.01f;
                    Supplies.UpdateAtmosphere(Owner.Player1, a + atmosAdd);
                }
                if (waterAdd > 0f)
                {
                    float w = Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float wv) ? wv : 0f;
                    Supplies.UpdateWater(Owner.Player1, w + waterAdd);
                }

                ShowStatusBanner(
                    $"<color=#8FE7FF><b>CLIMATE COMBO</b></color> {tag}+{otherTag} — faster terraforming pulse",
                    4f);
                break;
            }
        }

        private void TryQueueComboOffer(string goalKey, string a, string b)
        {
            if (string.IsNullOrEmpty(goalKey) || CardDeckController.Instance == null) return;
            string offeredName = CardDeckController.Instance.QueueClimateComboOffer(goalKey);
            if (string.IsNullOrEmpty(offeredName)) return;

            ShowStatusBanner(
                $"<color=#8FE7FF><b>COMBO</b></color> {a}+{b} → <color=#7CFF9A>{offeredName}</color> offered",
                5f);
        }

        /// <summary>
        /// Combolands stacking: orthogonal tile-edge neighbors only (same grid as placement).
        /// </summary>
        private static int CalculateAdjacencyBonus(BaseBuilding placed, string tag, out int neighborCount)
        {
            neighborCount = 0;
            if (placed == null) return 0;
            return CalculateAdjacencyBonusAt(
                ColonyTileGrid.WorldToCell(placed.transform.position),
                placed.ResolvedBuildingSO,
                tag,
                placed,
                out neighborCount);
        }

        private static int CalculateAdjacencyBonusAt(
            Vector2Int cell,
            BuildingSO placing,
            string tag,
            BaseBuilding ignoreSelf,
            out int neighborCount)
        {
            neighborCount = 0;
            var neighbors = new List<BaseBuilding>(6);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(cell, Owner.Player1, neighbors);

            int bonus = 0;
            float placedUpkeep = PowerGridManager.GetBuildingPowerUpkeep(placing);
            bool placedIsPower = tag == "Power";

            foreach (var other in neighbors)
            {
                if (other == null || other == ignoreSelf) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;

                neighborCount++;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                bonus += AdjacentBonus;

                if (!string.IsNullOrEmpty(tag) && tag == otherTag)
                    bonus += SameTagBonus;

                bool otherIsPower = otherTag == "Power";
                float otherUpkeep = PowerGridManager.GetBuildingPowerUpkeep(other.ResolvedBuildingSO);
                if ((placedIsPower && otherUpkeep > 0f) || (otherIsPower && placedUpkeep > 0f))
                    bonus += PowerConsumerBonus;

                if (tag == "Anchor" || otherTag == "Anchor")
                    bonus += AnchorBonus;

                if (IsClimatePair(tag, otherTag))
                    bonus += ClimatePairBonus;

                if ((tag == "Life" && (otherTag == "Water" || otherTag == "Anchor"))
                    || (otherTag == "Life" && (tag == "Water" || tag == "Anchor")))
                    bonus += LifeSynergyBonus;
            }

            return Mathf.Min(bonus, AdjacencySoftCap);
        }

        /// <summary>
        /// Hover preview for card placement: estimated score, climate rate, and per-neighbor combo roles.
        /// </summary>
        public PlacementComboPreview PreviewPlacement(BuildingSO building, Vector3 worldPos)
        {
            var preview = new PlacementComboPreview
            {
                Building = building,
                WorldPos = worldPos,
                Cell = ColonyTileGrid.WorldToCell(worldPos)
            };
            if (building == null) return preview;

            GetTileValues(building, out int baseScore, out _, out string tag);
            preview.Tag = tag;
            preview.BaseScore = baseScore;
            preview.IsClimateTile = tag == "Heat" || tag == "Air" || tag == "Water";

            int neighbors;
            preview.AdjScore = CalculateAdjacencyBonusAt(
                preview.Cell, building, tag, null, out neighbors);
            Instance?.ApplyExtraAdjToPreview(preview, neighbors);

            if (tag == "Power")
                preview.PowerBonus = PowerGeneratorScoreBonus + (Instance != null ? Instance.powerScoreBonusExtra : 0);

            // Geology match (mine on deposit / feature lock) — same rules as place rewards.
            if (PreviewGeologyBonus(building, worldPos, out int geo))
            {
                preview.AdjScore += geo; // fold into displayed adj total for simplicity
                preview.Links.Add(new PlacementLink(null, PlacementLinkKind.Geology, $"+{geo} geology", geo));
            }

            FillPreviewLinks(preview, building, tag);
            preview.ClimateRateMult = PreviewClimateRateMult(preview.Cell, tag);
            FillClimateNumbers(preview, building, tag, worldPos);
            int raw = preview.BaseScore + preview.AdjScore + preview.PowerBonus;
            float mult = Instance != null ? Instance.scoreMultiplier : 1f;
            preview.EstimatedTotal = Mathf.Max(0, Mathf.RoundToInt(raw * mult));
            return preview;
        }

        /// <summary>
        /// Dry-run of weekly climate rates (applied on week spend) + on-place geology/pair pulses.
        /// </summary>
        private static void FillClimateNumbers(
            PlacementComboPreview preview, BuildingSO building, string tag, Vector3 worldPos)
        {
            ResolveClimateBaseRates(building, tag, out float tempRate, out float atmosRate, out float waterRate);
            float efficiency = PreviewProductionEfficiency(building, preview.Cell);
            preview.ProductionEfficiency = efficiency;
            preview.WillBePowered = efficiency >= 0.99f;

            float combo = preview.ClimateRateMult;
            if (tag != "Heat" && tag != "Air" && tag != "Water")
                combo = 1f;

            // Config rates are per week under Colony Acts.
            preview.TempRatePerSec = tempRate * efficiency * combo;
            preview.AtmosRatePerSec = atmosRate * efficiency * combo;
            preview.WaterRatePerSec = waterRate * efficiency * combo;

            // Instant pulses (clamped to remaining budget, without committing).
            float pulseT = 0f, pulseA = 0f, pulseW = 0f;
            if (PreviewGeologyBonus(building, worldPos, out _))
                PreviewGeologyClimatePulse(building, worldPos, ref pulseT, ref pulseA, ref pulseW);
            PreviewClimatePairPulse(preview.Cell, tag, worldPos, ref pulseT, ref pulseA, ref pulseW);

            GetRemainingClimateBudget(worldPos, out float remainT, out float remainA, out float remainW);
            preview.RemainTemp = remainT;
            preview.RemainAtmos = remainA;
            preview.RemainWater = remainW;

            preview.InstantTemp = Mathf.Min(pulseT, remainT);
            preview.InstantAtmos = Mathf.Min(pulseA, remainA);
            preview.InstantWater = Mathf.Min(pulseW, remainW);
        }

        private static void ResolveClimateBaseRates(
            BuildingSO building, string tag, out float tempRate, out float atmosRate, out float waterRate)
        {
            tempRate = atmosRate = waterRate = 0f;
            if (building?.BuildingConfig == null && string.IsNullOrEmpty(tag)) return;

            var config = building?.BuildingConfig;
            if (config != null)
            {
                tempRate = config.TemperatureGeneration;
                atmosRate = config.AtmosphereGeneration;
                waterRate = config.WaterGeneration;
            }

            // Same name fallbacks / soft caps as BaseBuilding.TickClimateGeneration (per-week under Acts).
            string n = building != null ? building.Name : null;
            if (!string.IsNullOrEmpty(n))
            {
                if (atmosRate <= 0f
                    && (n.IndexOf("Atmospheric Condenser", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Carbon Dioxide Import", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("GHG", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    atmosRate = 0.012f;
                if (tempRate <= 0f
                    && (n.IndexOf("GHG", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Geothermal", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Methanogenic", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    tempRate = 0.2f;
                if (waterRate <= 0f
                    && (n.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Subglacial", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || (n.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0
                            && n.IndexOf("Processor", System.StringComparison.OrdinalIgnoreCase) < 0)))
                    waterRate = 0.1f;
            }

            // Tag defaults so Heat/Air/Water cards always preview a number even with zeroed configs.
            if (tempRate <= 0f && tag == "Heat") tempRate = 0.2f;
            if (atmosRate <= 0f && tag == "Air") atmosRate = 0.012f;
            if (waterRate <= 0f && tag == "Water") waterRate = 0.1f;

            tempRate = Mathf.Min(tempRate, 0.3f);
            atmosRate = Mathf.Min(atmosRate, 0.015f);
            waterRate = Mathf.Min(waterRate, 0.12f);
        }

        private static float PreviewProductionEfficiency(BuildingSO building, Vector2Int cell)
        {
            if (building == null) return 0f;
            if (BuildingSiteRegistry.IsPowerGeneratorBuilding(building))
                return 1f;
            float upkeep = PowerGridManager.GetBuildingPowerUpkeep(building);
            if (upkeep <= 0.0001f) return 1f;

            // Adjacent completed power nodes auto-link on place → treat as powered.
            var neighbors = new List<BaseBuilding>(6);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(cell, Owner.Player1, neighbors);
            foreach (var other in neighbors)
            {
                if (other == null) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                var node = other.GetComponent<PowerNode>();
                if (node != null && node.IsPowered) return 1f;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                if (otherTag == "Power") return 1f;
            }

            return BaseBuilding.UnpoweredProductionEfficiency;
        }

        private static void GetRemainingClimateBudget(
            Vector3 worldPos, out float remainT, out float remainA, out float remainW)
        {
            remainT = remainA = remainW = 0f;
            var acts = Instance;
            if (acts == null || !acts.started || acts.runEnded || acts.IsBetweenActs) return;

            acts.GetPerSectorClimateBudgets(out float maxT, out float maxA, out float maxW);
            int sectorIndex = ResolveSectorIndex(worldPos);
            if (!acts.sectorClimateContributed.TryGetValue(sectorIndex, out Vector3 used))
                used = Vector3.zero;

            acts.GetActClimateRequirements(out float needT, out float needA, out float needW);
            acts.GetClimateGains(out float tGain, out float aGain, out float wGain);
            float actRemainT = Mathf.Max(0f, needT - tGain);
            float actRemainA = Mathf.Max(0f, needA - aGain);
            float actRemainW = Mathf.Max(0f, needW - wGain);

            remainT = Mathf.Min(Mathf.Max(0f, maxT - used.x), actRemainT);
            remainA = Mathf.Min(Mathf.Max(0f, maxA - used.y), actRemainA);
            remainW = Mathf.Min(Mathf.Max(0f, maxW - used.z), actRemainW);
        }

        private static void PreviewGeologyClimatePulse(
            BuildingSO building, Vector3 worldPos, ref float tempAdd, ref float atmosAdd, ref float waterAdd)
        {
            if (building == null) return;
            string resourceType = null;

            if (BuildingSiteRegistry.IsMineBuilding(building)
                && DiscoverySystem.IsOnDiscoveredMineDeposit(building, worldPos)
                && DiscoverySystem.TryGetMineResourceType(building, out string mineType))
            {
                resourceType = mineType;
            }
            else
            {
                string name = building.Name ?? string.Empty;
                var nearest = SectorManager.Instance?.GetNearestSector(worldPos);
                if (nearest != null && nearest.Feature != SectorManager.SectorFeature.None)
                {
                    bool isAquifer = name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isSubglacial = name.IndexOf("Subglacial", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isGeo = name.IndexOf("Geothermal", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isLavaTubeBld = name.IndexOf("Lava Tube", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isFaultBld = name.IndexOf("Magnetic Shield", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Sector Command", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if ((isAquifer && nearest.Feature == SectorManager.SectorFeature.WaterDeposit)
                        || (isSubglacial && nearest.Feature == SectorManager.SectorFeature.Glacier))
                        resourceType = "Water";
                    else if ((isGeo && nearest.Feature == SectorManager.SectorFeature.Volcano)
                        || (isLavaTubeBld && nearest.Feature == SectorManager.SectorFeature.LavaTube)
                        || (isFaultBld && nearest.Feature == SectorManager.SectorFeature.FaultLine))
                        resourceType = "Heat";
                }
            }

            if (string.IsNullOrEmpty(resourceType)) return;

            if (resourceType.IndexOf("Mineral", System.StringComparison.OrdinalIgnoreCase) >= 0
                || resourceType.IndexOf("Iron", System.StringComparison.OrdinalIgnoreCase) >= 0
                || resourceType.IndexOf("Regolith", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tempAdd += 1.5f;
            else if (resourceType.IndexOf("Gas", System.StringComparison.OrdinalIgnoreCase) >= 0)
                atmosAdd += 0.03f;
            else if (resourceType.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
                waterAdd += 1.5f;
            else if (resourceType.IndexOf("Heat", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tempAdd += 2f;
        }

        private static void PreviewClimatePairPulse(
            Vector2Int cell, string tag, Vector3 worldPos,
            ref float tempAdd, ref float atmosAdd, ref float waterAdd)
        {
            if (tag != "Heat" && tag != "Air" && tag != "Water") return;
            var acts = Instance;
            if (acts == null || !acts.started || acts.runEnded || acts.IsBetweenActs) return;

            var neighbors = new List<BaseBuilding>(6);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(cell, Owner.Player1, neighbors);

            int sectorIndex = ResolveSectorIndex(worldPos);
            acts.GetPerSectorClimateBudgets(out float maxT, out float maxA, out float maxW);
            if (!acts.sectorClimateContributed.TryGetValue(sectorIndex, out Vector3 used))
                used = Vector3.zero;

            foreach (var other in neighbors)
            {
                if (other == null) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                if (!IsClimatePair(tag, otherTag)) continue;

                string key = $"{sectorIndex}:{ClimatePairKey(tag, otherTag)}";
                if (acts.climatePairPulseKeysThisAct.Contains(key)) continue;

                float t = Mathf.Max(0f, maxT - used.x) * 0.07f;
                float a = Mathf.Max(0f, maxA - used.y) * 0.07f;
                float w = Mathf.Max(0f, maxW - used.z) * 0.07f;
                if (tag != "Heat" && otherTag != "Heat") t = 0f;
                if (tag != "Air" && otherTag != "Air") a = 0f;
                if (tag != "Water" && otherTag != "Water") w = 0f;

                tempAdd += t;
                atmosAdd += a;
                waterAdd += w;
                break; // first pair only, matching TryClimatePairPulse
            }
        }

        private static string ClimatePairKey(string a, string b)
        {
            if (string.CompareOrdinal(a, b) <= 0) return $"{a}:{b}";
            return $"{b}:{a}";
        }

        private void ApplyExtraAdjToPreview(PlacementComboPreview preview, int neighbors)
        {
            preview.AdjScore += adjacencyBonusExtra * Mathf.Max(0, neighbors);
        }

        private static bool PreviewGeologyBonus(BuildingSO building, Vector3 worldPos, out int bonus)
        {
            bonus = 0;
            if (building == null) return false;
            int extra = Instance != null ? Instance.geologyBonusExtra : 0;
            int match = GeologyMatchBonus + extra;

            if (BuildingSiteRegistry.IsMineBuilding(building)
                && DiscoverySystem.IsOnDiscoveredMineDeposit(building, worldPos))
            {
                bonus = match;
                return true;
            }

            string name = building.Name ?? string.Empty;
            var nearest = SectorManager.Instance?.GetNearestSector(worldPos);
            if (nearest == null || nearest.Feature == SectorManager.SectorFeature.None) return false;

            bool isAquifer = name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isSubglacial = name.IndexOf("Subglacial", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isGeo = name.IndexOf("Geothermal", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isLavaTubeBld = name.IndexOf("Lava Tube", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFaultBld = name.IndexOf("Magnetic Shield", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Sector Command", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if ((isAquifer && nearest.Feature == SectorManager.SectorFeature.WaterDeposit)
                || (isSubglacial && nearest.Feature == SectorManager.SectorFeature.Glacier)
                || (isGeo && nearest.Feature == SectorManager.SectorFeature.Volcano)
                || (isLavaTubeBld && nearest.Feature == SectorManager.SectorFeature.LavaTube)
                || (isFaultBld && nearest.Feature == SectorManager.SectorFeature.FaultLine))
            {
                bonus = match;
                return true;
            }

            return false;
        }

        private static void FillPreviewLinks(PlacementComboPreview preview, BuildingSO placing, string tag)
        {
            var neighbors = new List<BaseBuilding>(6);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(preview.Cell, Owner.Player1, neighbors);
            float placedUpkeep = PowerGridManager.GetBuildingPowerUpkeep(placing);
            bool placedIsPower = tag == "Power";

            foreach (var other in neighbors)
            {
                if (other == null) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);

                PlacementLinkKind kind = PlacementLinkKind.Neighbor;
                int pts = AdjacentBonus;
                string label = $"+{AdjacentBonus}";

                if (!string.IsNullOrEmpty(tag) && tag == otherTag)
                {
                    kind = PlacementLinkKind.SameTag;
                    pts += SameTagBonus;
                    label = $"same {tag} +{pts}";
                }

                bool otherIsPower = otherTag == "Power";
                float otherUpkeep = PowerGridManager.GetBuildingPowerUpkeep(other.ResolvedBuildingSO);
                if ((placedIsPower && otherUpkeep > 0f) || (otherIsPower && placedUpkeep > 0f))
                {
                    kind = PlacementLinkKind.Power;
                    pts += PowerConsumerBonus;
                    label = $"power +{pts}";
                }

                if (IsClimatePair(tag, otherTag))
                {
                    kind = PlacementLinkKind.ClimatePair;
                    pts += ClimatePairBonus;
                    label = $"{tag}↔{otherTag} +{pts}";
                }

                if (tag == "Anchor" || otherTag == "Anchor")
                {
                    if (kind == PlacementLinkKind.Neighbor || kind == PlacementLinkKind.SameTag)
                    {
                        kind = PlacementLinkKind.Anchor;
                        pts += AnchorBonus;
                        label = $"anchor +{pts}";
                    }
                    else
                    {
                        pts += AnchorBonus;
                        label += $" · anchor";
                    }
                }

                if ((tag == "Life" && (otherTag == "Water" || otherTag == "Anchor"))
                    || (otherTag == "Life" && (tag == "Water" || tag == "Anchor")))
                {
                    kind = PlacementLinkKind.Life;
                    pts += LifeSynergyBonus;
                    label = $"life +{pts}";
                }

                preview.Links.Add(new PlacementLink(other, kind, label, pts));
            }
        }

        private static float PreviewClimateRateMult(Vector2Int cell, string tag)
        {
            if (tag != "Heat" && tag != "Air" && tag != "Water") return 1f;

            var neighbors = new List<BaseBuilding>(6);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(cell, Owner.Player1, neighbors);

            bool sameTag = false;
            bool climatePair = false;
            bool hasHeat = tag == "Heat";
            bool hasAir = tag == "Air";
            bool hasWater = tag == "Water";
            bool powerNeighbor = false;

            foreach (var other in neighbors)
            {
                if (other == null) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                if (otherTag == tag) sameTag = true;
                if (IsClimatePair(tag, otherTag)) climatePair = true;
                if (otherTag == "Heat") hasHeat = true;
                if (otherTag == "Air") hasAir = true;
                if (otherTag == "Water") hasWater = true;
                if (otherTag == "Power") powerNeighbor = true;
            }

            float mult = 1f;
            if (hasHeat && hasAir && hasWater) mult = 2.25f;
            else if (climatePair) mult = 1.75f;
            else if (sameTag) mult = 1.35f;
            if (powerNeighbor) mult += 0.25f;
            return Mathf.Min(mult, 2.5f);
        }

        private static bool IsClimatePair(string a, string b)
        {
            return (a == "Heat" && b == "Air") || (a == "Air" && b == "Heat")
                || (a == "Air" && b == "Water") || (a == "Water" && b == "Air")
                || (a == "Water" && b == "Heat") || (a == "Heat" && b == "Water");
        }

        /// <summary>Missing third of the Heat/Air/Water trio when a and b form a climate pair.</summary>
        private static string ThirdClimateTag(string a, string b)
        {
            if (!IsClimatePair(a, b)) return null;
            bool heat = a == "Heat" || b == "Heat";
            bool air = a == "Air" || b == "Air";
            bool water = a == "Water" || b == "Water";
            if (heat && air && !water) return "Water";
            if (air && water && !heat) return "Heat";
            if (water && heat && !air) return "Air";
            return null;
        }

        private static string ClimateTagToGoalKey(string tag)
        {
            return tag switch
            {
                "Heat" => "TEMPERATURE",
                "Air" => "ATMOSPHERE",
                "Water" => "WATER",
                _ => null
            };
        }

        public void GrantCardScore(BlueprintCardSO card)
        {
            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
            {
                // Building score is granted on CompleteConstruction to avoid double-counting.
                return;
            }

            // Non-building cards: small survival score.
            if (!started || runEnded || IsBetweenActs) return;
            colonyScore += 2;
            OnActStateChanged?.Invoke();
            TryResolveWeekExhaustion();
        }

        /// <summary>
        /// Clear Act when score + climate deltas are met; fail when weeks are exhausted
        /// and nothing pending can still change the outcome.
        /// </summary>
        private void TryResolveWeekExhaustion()
        {
            if (!started || runEnded || IsBetweenActs) return;

            if (IsActComplete)
            {
                ClearCurrentAct();
                return;
            }

            if (weeksRemaining <= 0 && !HasPendingPlayerConstructionScores())
                FailCurrentAct();
        }

        /// <summary>True while a Player1 pad build can still grant Colony Score.</summary>
        private static bool HasPendingPlayerConstructionScores()
        {
            if (BaseBuilding.HasPendingDeferredColonyActScores())
                return true;

            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || building.Owner != Owner.Player1) continue;
                var state = building.Progress.State;
                if (state == BuildingProgress.BuildingState.Building
                    || state == BuildingProgress.BuildingState.Paused)
                    return true;
            }

            return false;
        }

        private void ClearCurrentAct()
        {
            if (IsBetweenActs || runEnded) return;
            IsBetweenActs = true;

            int cleared = CurrentAct;
            int earned = AwardTerraCoinsForClearedAct();
            Debug.Log($"[ColonyActManager] Act {cleared} ({CurrentActName}) cleared! +{earned} Terra-Coins (bank {terraCoins}).");
            OnActCleared?.Invoke(cleared);

            if (actIndex >= acts.Count - 1)
            {
                runEnded = true;
                OnRunVictory?.Invoke();
                int terraDone = CountTerraformedSectors(out int terraTotal);
                GameOverManager.LastOutcomeDetail =
                    $"All {TotalActs} Acts cleared and {terraDone}/{terraTotal} sectors terraformed.";
                if (GenerationManager.Instance != null)
                    GenerationManager.Instance.NotifyColonyActVictory();
                else if (GameOverManager.Instance != null)
                    GameOverManager.Instance.TriggerVictory();
                OnActStateChanged?.Invoke();
                return;
            }

            statusBanner = $"<color=#7CFF9A><b>ACT CLEARED!</b></color>  +{earned} Terra-Coins — upgrade at the Depot before {acts[actIndex + 1].Name}.";
            statusBannerUntil = Time.unscaledTime + 8f;
            OnActStateChanged?.Invoke();
            OnBetweenActShopRequested?.Invoke();
            if (!GameDevTV.RTS.UI.BetweenActShopUI.IsOpen)
                GameDevTV.RTS.UI.BetweenActShopUI.Instance?.Open();
        }

        /// <summary>15 + floor(score/10) + floor(excess/5); coins carry across Acts.</summary>
        private int AwardTerraCoinsForClearedAct()
        {
            int excess = Mathf.Max(0, colonyScore - TargetScore);
            int earned = 15 + (colonyScore / 10) + (excess / 5);
            terraCoins += Mathf.Max(0, earned);
            OnTerraCoinsChanged?.Invoke(terraCoins);
            return earned;
        }

        public bool TrySpendTerraCoins(int cost)
        {
            if (cost <= 0) return true;
            if (terraCoins < cost) return false;
            terraCoins -= cost;
            OnTerraCoinsChanged?.Invoke(terraCoins);
            OnActStateChanged?.Invoke();
            return true;
        }

        public void PurchaseUpgrade(ShopUpgradeId id)
        {
            switch (id)
            {
                case ShopUpgradeId.ExtraWeeks:
                    pendingWeekBonus += 2;
                    break;
                case ShopUpgradeId.ScorePercent:
                    scoreMultiplier += 0.10f;
                    break;
                case ShopUpgradeId.GeologyBonus:
                    geologyBonusExtra += 5;
                    break;
                case ShopUpgradeId.AdjacencyBump:
                    adjacencyBonusExtra += 1;
                    break;
                case ShopUpgradeId.PowerScoreBoost:
                    powerScoreBonusExtra += 3;
                    break;
                case ShopUpgradeId.SeatClimateCard:
                    CardDeckController.Instance?.QueueClimateComboOffer("WATER");
                    break;
            }
            OnActStateChanged?.Invoke();
        }

        public enum ShopUpgradeId
        {
            ExtraWeeks,
            ScorePercent,
            GeologyBonus,
            AdjacencyBump,
            PowerScoreBoost,
            SeatClimateCard
        }

        /// <summary>
        /// Called by <see cref="GameDevTV.RTS.UI.BetweenActShopUI"/> after the player finishes shopping.
        /// Advances to the next named Act (does not unlock sectors — play Command Posts to expand).
        /// </summary>
        public void CompleteBetweenActShopAndAdvance()
        {
            if (runEnded || !IsBetweenActs) return;
            if (actIndex >= acts.Count - 1) return;

            CardDeckController.Instance?.GrantSectorTransitionBootstrap();

            int excess = Mathf.Max(0, colonyScore - TargetScore);
            int carried = Mathf.RoundToInt(colonyScore * ScoreCarryFraction) + excess;
            actIndex++;
            colonyScore = carried;
            weeksRemaining = CurrentActDef.WeekBudget + pendingWeekBonus;
            pendingWeekBonus = 0;
            RecordClimateBaselines();
            IsBetweenActs = false;
            CardDeckController.Instance?.NotifyActClimateComboReset();
            statusBanner = $"<color=#7CFF9A><b>NEXT ACT</b></color>  {CurrentActName} — score + climate. Terra-Coins banked: {terraCoins}.";
            statusBannerUntil = Time.unscaledTime + 6f;

            Debug.Log($"[ColonyActManager] Act {CurrentAct}/{TotalActs} {CurrentActName}: start score {colonyScore}/{TargetScore}, weeks {weeksRemaining}, coins {terraCoins}");
            OnActStateChanged?.Invoke();

            if (IsActComplete)
                ClearCurrentAct();
        }

        private void FailCurrentAct()
        {
            if (runEnded) return;
            runEnded = true;
            Debug.Log($"[ColonyActManager] Act {CurrentAct} failed — weeks exhausted (score {colonyScore}/{TargetScore}, climate {(IsClimateMet ? "met" : "short")}).");
            OnActFailed?.Invoke();
            OnActStateChanged?.Invoke();
            if (GameOverManager.Instance != null)
            {
                string missing = "";
                if (!IsScoreMet) missing += $"Score {colonyScore}/{TargetScore}";
                if (!IsClimateMet)
                {
                    if (missing.Length > 0) missing += " · ";
                    missing += "climate short of Temp/Atmos/Water";
                }
                GameOverManager.LastOutcomeDetail =
                    $"Act {CurrentAct} ({CurrentActName}) failed — weeks ran out.\nNeeded: score target AND climate deltas.\nStill missing: {missing}.";
                GameOverManager.Instance.TriggerGameOver(GameOverManager.GameOverReason.ColonyActFailed);
            }
        }

        public static void GetTileValues(BuildingSO building, out int baseScore, out float habitabilityGain, out string tag)
        {
            baseScore = 2;
            habitabilityGain = 0f;
            tag = "Tile";

            if (building == null) return;

            string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);
            string name = building.Name ?? string.Empty;

            switch (goal)
            {
                case "COMMAND POST":
                    baseScore = 6;
                    tag = "Anchor";
                    break;
                case "POWER":
                    baseScore = 2;
                    tag = "Power";
                    break;
                case "MATERIALS":
                    baseScore = 3;
                    tag = "Industry";
                    break;
                case "POPULATION":
                    baseScore = 5;
                    tag = "Anchor";
                    break;
                case "TEMPERATURE":
                    baseScore = 3;
                    habitabilityGain = 8f;
                    tag = "Heat";
                    break;
                case "ATMOSPHERE":
                    baseScore = 3;
                    habitabilityGain = 8f;
                    tag = "Air";
                    break;
                case "WATER":
                    baseScore = 3;
                    habitabilityGain = 8f;
                    tag = "Water";
                    break;
                case "OXYGEN":
                    baseScore = 2;
                    habitabilityGain = 3f;
                    tag = "Life";
                    break;
                default:
                    if (name.IndexOf("drone", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        baseScore = 2;
                        tag = "Labor";
                    }
                    else
                    {
                        baseScore = 2;
                        tag = "Tile";
                    }
                    break;
            }
        }

        public string BuildObjectivesText()
        {
            if (!started)
                return "<color=#C8D0D8>Waiting for planet…</color>";

            int terraDone = CountTerraformedSectors(out int terraTotal);

            if (runEnded && IsActComplete && actIndex >= acts.Count - 1)
                return $"<color=#7CFF9A><b>YOU WIN</b></color>\nAll Acts cleared · {terraDone}/{terraTotal} sectors terraformed.";

            if (runEnded)
                return "<color=#FF8A8A><b>YOU LOSE</b></color>\nWeeks ran out before score + climate goals.";

            float climate = GetClimateProgress(out _, out _, out _);
            GetFocusSectorClimatePresence(out bool hasHeat, out bool hasAir, out bool hasWater);
            var sb = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(statusBanner) && Time.unscaledTime < statusBannerUntil)
                sb.AppendLine(statusBanner);

            string verdict;
            string verdictColor;
            if (IsActComplete)
            {
                verdict = "CLEARING ACT…";
                verdictColor = "#7CFF9A";
            }
            else if (actIndex >= acts.Count - 1 && IsScoreMet && IsClimateMet && !AreAllSectorsTerraformed())
            {
                verdict = $"Need all sectors terraformed ({terraDone}/{terraTotal})";
                verdictColor = "#FFE08A";
            }
            else if (weeksRemaining <= 2 && (!IsScoreMet || !IsClimateMet))
            {
                verdict = "AT RISK — weeks almost gone";
                verdictColor = "#FF8A8A";
            }
            else if (IsScoreMet && !IsClimateMet)
            {
                verdict = "Need planet climate gains";
                verdictColor = "#FFE08A";
            }
            else if (!IsScoreMet && IsClimateMet)
            {
                verdict = "Need more Colony Score";
                verdictColor = "#FFE08A";
            }
            else
            {
                verdict = "Score + climate to clear Act";
                verdictColor = "#8FE7FF";
            }

            sb.AppendLine($"<color={verdictColor}><b>{verdict}</b></color>");
            sb.AppendLine($"<color=#8FE7FF><b>Act {CurrentAct}/{TotalActs} — {CurrentActName}</b></color>");
            sb.AppendLine();

            // Score + climate lead the panel (larger type) so goals read at a glance.
            string scoreMark = IsScoreMet ? "✓" : "○";
            string scoreColor = IsScoreMet ? "#7CFF9A" : "#FFE08A";
            sb.AppendLine(
                $"<size=130%><color={scoreColor}><b>{scoreMark}  SCORE  {colonyScore} / {TargetScore}</b></color></size>");
            sb.AppendLine($"  <color=#FFE08A><size=110%>{ProgressBar(colonyScore, TargetScore, 14)}</size></color>");

            GetClimateGains(out float tempGain, out float atmosGain, out float waterGain);
            GetActClimateRequirements(out float tempCap, out float atmosCap, out float waterCap);
            tempGain = Mathf.Min(tempGain, tempCap);
            atmosGain = Mathf.Min(atmosGain, atmosCap);
            waterGain = Mathf.Min(waterGain, waterCap);
            string climateMark = IsClimateMet ? "✓" : "○";
            string climateColor = IsClimateMet ? "#7CFF9A" : "#FFE08A";
            sb.AppendLine(
                $"<size=130%><color={climateColor}><b>{climateMark}  TERRAFORM  {climate:P0}</b></color></size>");
            sb.AppendLine(
                $"  <color=#8FE7FF><size=110%>{ProgressBar(Mathf.RoundToInt(climate * 100f), 100, 14)}</size></color>");
            sb.AppendLine(FormatClimateChannelLine(
                "TEMP", "TEMPERATURE", tempGain, tempCap, "°C", 1));
            sb.AppendLine(FormatClimateChannelLine(
                "ATMOS", "ATMOSPHERE", atmosGain, atmosCap, "atm", 2));
            sb.AppendLine(FormatClimateChannelLine(
                "WATER", "WATER", waterGain, waterCap, "%", 1));

            float absTemp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner.Player1, out float t) ? t : -60f;
            float absAtmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float at) ? at : 0.01f;
            float absWater = Supplies.Water != null && Supplies.Water.TryGetValue(Owner.Player1, out float wt) ? wt : 0f;
            sb.AppendLine(
                $"  <color=#A8B0B8>Planet now:</color> " +
                $"<color={TerraformingGoalColors.ToHex(TerraformingGoalColors.Temperature)}><b>{absTemp:F1}°C</b></color>  " +
                $"<color={TerraformingGoalColors.ToHex(TerraformingGoalColors.Atmosphere)}><b>{absAtmos:F2} atm</b></color>  " +
                $"<color={TerraformingGoalColors.ToHex(TerraformingGoalColors.Water)}><b>{absWater:F1}%</b></color>");

            float oxy = Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
            string oxyHex = TerraformingGoalColors.ToHex(TerraformingGoalColors.Oxygen);
            sb.AppendLine($"  <color={oxyHex}><b>OXYGEN</b>  {oxy:F1}%</color>  <color=#A8B0B8>(flavor)</color>");

            string weekColor = weeksRemaining <= 2 ? "#FF8A8A" : (weeksRemaining <= 4 ? "#FFE08A" : "#C8D0D8");
            sb.AppendLine(
                $"<size=115%><color={weekColor}><b>WEEKS LEFT  {weeksRemaining}</b></color></size>");
            sb.AppendLine($"<color=#FFE08A>TERRA-COINS  {terraCoins}</color>  <color=#A8B0B8>(shop on Act clear)</color>");

            string h = hasHeat ? "<color=#7CFF9A>Heat✓</color>" : "<color=#FF8A8A>Heat○</color>";
            string a = hasAir ? "<color=#7CFF9A>Air✓</color>" : "<color=#FF8A8A>Air○</color>";
            string w = hasWater ? "<color=#7CFF9A>Water✓</color>" : "<color=#FF8A8A>Water○</color>";
            sb.AppendLine($"  <color=#A8B0B8>On planet:</color> {h}  {a}  {w}");

            if (!hasWater)
            {
                if (hasHeat && hasAir)
                    sb.AppendLine("  <color=#8FE7FF>Water unlock: edge-join Heat+Air → Water card offer</color>");
                else if (!hasHeat && !hasAir)
                    sb.AppendLine("  <color=#FFE08A>Need Heat (GHG) and Air (Condenser) tiles — join them for combos</color>");
                else if (!hasHeat)
                    sb.AppendLine("  <color=#FFE08A>Need a Heat tile joined to Air for the Water combo</color>");
                else
                    sb.AppendLine("  <color=#FFE08A>Need an Air tile joined to Heat for the Water combo</color>");
            }

            sb.AppendLine();
            sb.AppendLine("<color=#7A8490>── Tips ──</color>");
            sb.AppendLine($"<color=#7A8490>WIN: all Acts + terraform every sector ({terraDone}/{terraTotal}). LOSE: weeks hit 0.</color>");
            sb.AppendLine("<color=#7A8490>Acts ≠ sectors. Q/E jump sectors. CP expands map.</color>");
            sb.AppendLine("<color=#7A8490>Power = full climate rate (else 20%). No Materials gate on cards.</color>");
            sb.AppendLine(
                $"<color=#7A8490>Act climate need = 1/{ClimateBudgetSectorCount} of +15°C / +0.25 atm / +5% (per-sector share/cap).</color>");
            sb.AppendLine("<color=#7A8490>Stack Heat/Air/Water (+ Power) for climate rate combos.</color>");
            sb.AppendLine("<color=#7A8490>Edge: Heat+Air→Water · Air+Water→Heat · Water+Heat→Air · Power+Industry→Life</color>");
            sb.AppendLine("<color=#7A8490>Card week costs vary (0–2). Check the card chip.</color>");
            return sb.ToString();
        }

        private static string ProgressBar(int value, int max, int width = 10)
        {
            if (max <= 0) return new string('█', width);
            float t = Mathf.Clamp01(value / (float)max);
            int filled = Mathf.RoundToInt(t * width);
            return new string('█', filled) + new string('░', width - filled);
        }

        /// <summary>One high-contrast climate channel line for Active Objectives.</summary>
        private static string FormatClimateChannelLine(
            string shortLabel, string goalKey, float gain, float target, string unit, int decimals)
        {
            string hex = TerraformingGoalColors.ToHex(TerraformingGoalColors.ForGoal(goalKey));
            bool met = target <= 0.0001f || gain >= target - 0.0005f;
            string valueHex = met
                ? TerraformingGoalColors.ToHex(TerraformingGoalColors.MetValue)
                : TerraformingGoalColors.ToHex(TerraformingGoalColors.UnmetValue);
            string gainText = FormatGain(gain, decimals);
            string targetText = decimals <= 0
                ? $"+{target:F0}"
                : $"+{target.ToString($"F{decimals}")}";
            int barMax = 100;
            int barVal = target > 0.0001f
                ? Mathf.RoundToInt(Mathf.Clamp01(gain / target) * barMax)
                : barMax;
            return $"  <color={hex}><b>{shortLabel}</b></color>  " +
                   $"<color={valueHex}>{gainText} / {targetText} {unit}</color>  " +
                   $"<color={hex}>{ProgressBar(barVal, barMax, 8)}</color>";
        }

        /// <summary>Fixed-width signed gain so HUD lines don't jitter as values change.</summary>
        private static string FormatGain(float gain, int decimals)
        {
            string body = decimals <= 0
                ? Mathf.Abs(gain).ToString("F0")
                : Mathf.Abs(gain).ToString($"F{decimals}");
            return (gain < -0.0005f ? "-" : "+") + body;
        }
    }
}
