using System;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Combolands-style run spine: one Act per map sector. Colony Score + Temp/Atmos/Water
    /// deltas within a week budget. Climate buildings only count in the current focus sector.
    /// </summary>
    public class ColonyActManager : MonoBehaviour
    {
        public static ColonyActManager Instance { get; private set; }

        public static event Action OnActStateChanged;
        public static event Action<int> OnActCleared; // act index 1-based
        /// <summary>Fired after an Act clears when another Act remains — open the between-sector shop.</summary>
        public static event Action OnBetweenActShopRequested;
        public static event Action OnRunVictory;
        public static event Action OnActFailed;

        public struct ActDef
        {
            public string Name;
            public int TargetScore;
            public int WeekBudget;
            public int SectorIndex;
        }

        private readonly System.Collections.Generic.List<ActDef> acts = new();

        /// <summary>Habitability points needed for full Living look (cumulative).</summary>
        private const float HabitabilityForLiving = 80f;
        private const float ScoreCarryFraction = 0.25f;
        private const int AdjacentBonus = 2;
        private const int SameTagBonus = 4;
        private const int PowerConsumerBonus = 5;
        private const int AnchorBonus = 3;
        private const int ClimatePairBonus = 4;
        private const int LifeSynergyBonus = 4;
        private const int AdjacencySoftCap = 20;

        private int actIndex; // 0-based
        private int colonyScore;
        private int weeksRemaining;
        private float habitability;
        private bool runEnded;
        private bool started;

        private float baselineTemperature = -60f;
        private float baselineAtmosphere = 0.01f;
        private float baselineWater = 0f;

        private string statusBanner = string.Empty;
        private float statusBannerUntil;

        public int CurrentAct => actIndex + 1;
        public int TotalActs => Mathf.Max(1, acts.Count);
        public string CurrentActName => CurrentActDef.Name;
        public int ColonyScore => colonyScore;
        public int TargetScore => CurrentActDef.TargetScore;
        public int WeeksRemaining => weeksRemaining;
        public int FocusSectorIndex => CurrentActDef.SectorIndex;
        public float Habitability => habitability;
        public float HabitabilityProgress => Mathf.Clamp01(habitability / HabitabilityForLiving);
        public bool IsRunEnded => runEnded;
        /// <summary>True while Colony Acts are the active win/lose spine (ignore mining depletion).</summary>
        public bool IsRunActive => started && !runEnded;
        public bool IsBetweenActs { get; private set; }
        public bool IsScoreMet => colonyScore >= TargetScore;
        public bool IsClimateMet => GetClimateProgress(out _, out _, out _) >= 0.999f;
        public bool IsActComplete => IsScoreMet && IsClimateMet;

        private ActDef CurrentActDef
        {
            get
            {
                if (acts.Count == 0)
                    return new ActDef { Name = "Survive", TargetScore = 40, WeekBudget = 8, SectorIndex = 0 };
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

            BuildActsFromSectors();
            actIndex = 0;
            colonyScore = 0;
            habitability = 0f;
            runEnded = false;
            IsBetweenActs = false;
            weeksRemaining = CurrentActDef.WeekBudget;
            started = true;
            RecordClimateBaselines();
            ApplyFocusSector(FocusSectorIndex, announce: false);
            CardDeckController.Instance?.NotifyActClimateComboReset();
            // Seat unmet climate tiles (esp. blue Water) now that Act baselines exist.
            CardDeckController.Instance?.RefreshHand();
            Debug.Log($"[ColonyActManager] Act 1/{TotalActs} {CurrentActName}: score 0/{TargetScore}, weeks {weeksRemaining}, climate from sector {FocusSectorIndex}");
            OnActStateChanged?.Invoke();
            ClimateVisualStages.Instance?.NotifyHabitabilityChanged();
        }

        /// <summary>One Act per map sector — planet size drives run length.</summary>
        private void BuildActsFromSectors()
        {
            acts.Clear();
            int sectorCount = SectorManager.Instance != null ? SectorManager.Instance.Sectors.Count : 0;
            int n = Mathf.Max(1, sectorCount);

            for (int i = 0; i < n; i++)
            {
                // Progressive score; scales gently so large maps are longer, not absurdly hard per Act.
                int score = 40 + i * 35;
                int weeks = i == 0 ? 16 : (i >= n - 1 ? 16 : 14);
                string name = BuildActName(i, n);
                acts.Add(new ActDef
                {
                    Name = name,
                    TargetScore = score,
                    WeekBudget = weeks,
                    SectorIndex = i
                });
            }
        }

        private static string BuildActName(int index, int total)
        {
            string baseName;
            if (total == 1) baseName = "Thrive";
            else if (index == 0) baseName = "Survive";
            else if (index == total - 1) baseName = "Thrive";
            else if (index == 1) baseName = "Settle";
            else baseName = $"Expand {index + 1}";

            if (SectorManager.Instance != null
                && index < SectorManager.Instance.Sectors.Count)
            {
                var feature = SectorManager.Instance.Sectors[index].Feature;
                if (feature != SectorManager.SectorFeature.None)
                    return $"{baseName} · {feature}";
            }

            return $"{baseName} · Sector {index + 1}";
        }

        private void ApplyFocusSector(int sectorIndex, bool announce)
        {
            var sm = SectorManager.Instance;
            if (sm == null || sm.Sectors == null || sm.Sectors.Count == 0) return;

            int i = Mathf.Clamp(sectorIndex, 0, sm.Sectors.Count - 1);
            var sector = sm.Sectors[i];
            sm.BeginTerraformingOn(sector);
            // Reaching this Act's sector reveals its geology (WaterDeposit / LavaTube / …).
            DiscoverySystem.RevealFeaturesForSector(sector);

            if (announce)
                PlayerInput.FocusCameraOnWorldPosition(sector.Center);
        }

        /// <summary>Which climate tags are present (completed) in the current focus sector.</summary>
        public void GetFocusSectorClimatePresence(out bool heat, out bool air, out bool water)
        {
            heat = air = water = false;
            var sm = SectorManager.Instance;
            if (sm == null) return;
            var focus = sm.GetClimateFocusSector();
            if (focus == null) return;

            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != Owner.Player1) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (sm.GetNearestSector(b.transform.position) != focus) continue;

                GetTileValues(b.ResolvedBuildingSO, out _, out _, out string tag);
                if (tag == "Heat") heat = true;
                else if (tag == "Air") air = true;
                else if (tag == "Water") water = true;
            }
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
        }

        /// <summary>
        /// 0–1 bottleneck of Temp / Atmos / Water progress toward this Act's deltas
        /// (+15°C / +0.25 atm / +5% from Act baselines). Only focus-sector buildings tick.
        /// </summary>
        public float GetClimateProgress(out float tempProgress, out float atmosProgress, out float waterProgress)
        {
            GetClimateGains(out float tempGain, out float atmosGain, out float waterGain);
            tempProgress = DeltaProgressFromGain(tempGain, GenerationManager.SectorTemperatureDelta);
            atmosProgress = DeltaProgressFromGain(atmosGain, GenerationManager.SectorAtmosphereDelta);
            waterProgress = DeltaProgressFromGain(waterGain, GenerationManager.SectorWaterDelta);
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
            Debug.Log($"[ColonyActManager] Spent {spent} week(s) — {weeksRemaining} left (score {colonyScore}/{TargetScore})");
            TryOfferClimateComboCards(null, null);
            OnActStateChanged?.Invoke();
            TryResolveWeekExhaustion();
        }

        /// <summary>Re-check Heat/Air/Water presence combos (e.g. after hand fill).</summary>
        public void TryRefreshClimateComboFromPresence()
        {
            if (!started || runEnded || IsBetweenActs) return;
            TryOfferClimateComboCards(null, null);
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
            if (placed != null)
            {
                adjBonus = CalculateAdjacencyBonus(placed, tag, out neighbors);
                TryOfferClimateComboCards(placed, tag);
            }

            int total = score + adjBonus;
            if (total <= 0 && hab <= 0f) return;

            colonyScore += total;
            habitability += hab;
            if (adjBonus > 0)
                Debug.Log($"[ColonyActManager] +{score} base +{adjBonus} adj ({neighbors} neighbors, {tag}) → {colonyScore}/{TargetScore}; hab={habitability:F0}");
            else
                Debug.Log($"[ColonyActManager] +{score} score ({tag}) → {colonyScore}/{TargetScore}; hab={habitability:F0}");
            OnActStateChanged?.Invoke();
            ClimateVisualStages.Instance?.NotifyHabitabilityChanged();
            TryResolveWeekExhaustion();
        }

        /// <summary>
        /// Heat↔Air↔Water edge pairs queue the missing third channel as a hand offer (once per Act).
        /// Also offers when both partner tags already exist in the focus sector (not only adjacent).
        /// </summary>
        private void TryOfferClimateComboCards(BaseBuilding placed, string tag)
        {
            if (placed == null || CardDeckController.Instance == null) return;

            if (!string.IsNullOrEmpty(tag) && (tag == "Heat" || tag == "Air" || tag == "Water"))
            {
                var neighbors = new System.Collections.Generic.List<BaseBuilding>(4);
                ColonyTileGrid.CollectOrthogonalNeighborBuildings(
                    ColonyTileGrid.WorldToCell(placed.transform.position), Owner.Player1, neighbors);

                foreach (var other in neighbors)
                {
                    if (other == null || other == placed) continue;
                    if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                    GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                    string third = ThirdClimateTag(tag, otherTag);
                    TryQueueClimateThird(third, tag, otherTag);
                }
            }

            // Presence unlock: both partners in this sector → offer the missing third.
            GetFocusSectorClimatePresence(out bool hasHeat, out bool hasAir, out bool hasWater);
            if (hasHeat && hasAir) TryQueueClimateThird("Water", "Heat", "Air");
            if (hasAir && hasWater) TryQueueClimateThird("Heat", "Air", "Water");
            if (hasWater && hasHeat) TryQueueClimateThird("Air", "Water", "Heat");
        }

        private void TryQueueClimateThird(string thirdTag, string a, string b)
        {
            if (string.IsNullOrEmpty(thirdTag) || CardDeckController.Instance == null) return;
            string goalKey = ClimateTagToGoalKey(thirdTag);
            if (string.IsNullOrEmpty(goalKey)) return;

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

            var neighbors = new System.Collections.Generic.List<BaseBuilding>(4);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(
                ColonyTileGrid.WorldToCell(placed.transform.position), Owner.Player1, neighbors);

            int bonus = 0;
            foreach (var other in neighbors)
            {
                if (other == null || other == placed) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;

                neighborCount++;
                GetTileValues(other.ResolvedBuildingSO, out _, out _, out string otherTag);
                bonus += AdjacentBonus;

                if (!string.IsNullOrEmpty(tag) && tag == otherTag)
                    bonus += SameTagBonus;

                bool placedIsPower = tag == "Power";
                bool otherIsPower = otherTag == "Power";
                float otherUpkeep = PowerGridManager.GetBuildingPowerUpkeep(other.ResolvedBuildingSO);
                float placedUpkeep = PowerGridManager.GetBuildingPowerUpkeep(placed.ResolvedBuildingSO);
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
            Debug.Log($"[ColonyActManager] Act {cleared} ({CurrentActName}) cleared!");
            OnActCleared?.Invoke(cleared);

            if (actIndex >= acts.Count - 1)
            {
                runEnded = true;
                OnRunVictory?.Invoke();
                GameOverManager.LastOutcomeDetail =
                    $"All {TotalActs} sector Acts cleared — each region had score + climate before weeks ran out.";
                if (GenerationManager.Instance != null)
                    GenerationManager.Instance.NotifyColonyActVictory();
                else if (GameOverManager.Instance != null)
                    GameOverManager.Instance.TriggerVictory();
                OnActStateChanged?.Invoke();
                return;
            }

            // Permanent between-sector shop — pause here until Continue.
            statusBanner = $"<color=#7CFF9A><b>SECTOR ACT CLEARED!</b></color>  Visit the Supply Depot before Act {cleared + 1}.";
            statusBannerUntil = Time.unscaledTime + 8f;
            OnActStateChanged?.Invoke();
            OnBetweenActShopRequested?.Invoke();
            // Fallback if the shop listener missed the event (scene load race).
            if (!GameDevTV.RTS.UI.BetweenActShopUI.IsOpen)
                GameDevTV.RTS.UI.BetweenActShopUI.Instance?.Open();
        }

        /// <summary>
        /// Called by <see cref="GameDevTV.RTS.UI.BetweenActShopUI"/> after the player finishes shopping.
        /// Seeds Solar + Command Post, then advances focus to the next sector Act.
        /// </summary>
        public void CompleteBetweenActShopAndAdvance()
        {
            if (runEnded || !IsBetweenActs) return;
            if (actIndex >= acts.Count - 1) return;

            CardDeckController.Instance?.GrantSectorTransitionBootstrap();

            // Carry a fraction of excess into the next act's starting score.
            int excess = Mathf.Max(0, colonyScore - TargetScore);
            int carried = Mathf.RoundToInt(colonyScore * ScoreCarryFraction) + excess;
            actIndex++;
            colonyScore = carried;
            weeksRemaining = CurrentActDef.WeekBudget;
            RecordClimateBaselines();
            IsBetweenActs = false;
            ApplyFocusSector(FocusSectorIndex, announce: true);
            CardDeckController.Instance?.NotifyActClimateComboReset();
            statusBanner = $"<color=#7CFF9A><b>NEXT SECTOR</b></color>  {CurrentActName} — Solar + Command Post ready.";
            statusBannerUntil = Time.unscaledTime + 6f;

            Debug.Log($"[ColonyActManager] Act {CurrentAct}/{TotalActs} {CurrentActName}: start score {colonyScore}/{TargetScore}, weeks {weeksRemaining}, focus sector {FocusSectorIndex}");
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
            baseScore = 5;
            habitabilityGain = 0f;
            tag = "Tile";

            if (building == null) return;

            string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);
            string name = building.Name ?? string.Empty;

            switch (goal)
            {
                case "COMMAND POST":
                    baseScore = 12;
                    tag = "Anchor";
                    break;
                case "POWER":
                    baseScore = 4;
                    tag = "Power";
                    break;
                case "MATERIALS":
                    baseScore = 8;
                    tag = "Industry";
                    break;
                case "POPULATION":
                    baseScore = 10;
                    tag = "Anchor";
                    break;
                case "TEMPERATURE":
                    baseScore = 10;
                    habitabilityGain = 8f;
                    tag = "Heat";
                    break;
                case "ATMOSPHERE":
                    baseScore = 10;
                    habitabilityGain = 8f;
                    tag = "Air";
                    break;
                case "WATER":
                    baseScore = 10;
                    habitabilityGain = 8f;
                    tag = "Water";
                    break;
                case "OXYGEN":
                    baseScore = 6;
                    habitabilityGain = 3f;
                    tag = "Life";
                    break;
                default:
                    if (name.IndexOf("drone", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        baseScore = 3;
                        tag = "Labor";
                    }
                    else
                    {
                        baseScore = 5;
                        tag = "Tile";
                    }
                    break;
            }
        }

        public string BuildObjectivesText()
        {
            if (!started)
                return "<color=#C8D0D8>Waiting for planet…</color>";

            if (runEnded && IsActComplete && actIndex >= acts.Count - 1)
                return "<color=#7CFF9A><b>YOU WIN</b></color>\nAll sector Acts cleared.";

            if (runEnded)
                return "<color=#FF8A8A><b>YOU LOSE</b></color>\nWeeks ran out before this sector’s score + climate goals.";

            float climate = GetClimateProgress(out _, out _, out _);
            GetFocusSectorClimatePresence(out bool hasHeat, out bool hasAir, out bool hasWater);
            var sb = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(statusBanner) && Time.unscaledTime < statusBannerUntil)
                sb.AppendLine(statusBanner);

            string verdict;
            string verdictColor;
            if (IsActComplete)
            {
                verdict = "CLEARING SECTOR…";
                verdictColor = "#7CFF9A";
            }
            else if (weeksRemaining <= 2 && (!IsScoreMet || !IsClimateMet))
            {
                verdict = "AT RISK — weeks almost gone";
                verdictColor = "#FF8A8A";
            }
            else if (IsScoreMet && !IsClimateMet)
            {
                verdict = "Need climate in THIS sector";
                verdictColor = "#FFE08A";
            }
            else if (!IsScoreMet && IsClimateMet)
            {
                verdict = "Need more Colony Score";
                verdictColor = "#FFE08A";
            }
            else
            {
                verdict = "Terraform this sector";
                verdictColor = "#8FE7FF";
            }

            sb.AppendLine($"<color={verdictColor}><b>{verdict}</b></color>");
            sb.AppendLine($"<color=#8FE7FF><b>Act {CurrentAct}/{TotalActs} — {CurrentActName}</b></color>");
            sb.AppendLine($"<color=#A8B0B8>1 Act per sector ({TotalActs} on this planet).</color>");
            sb.AppendLine("<color=#A8B0B8>WIN Act: score AND climate here. Climate only ticks from THIS sector.</color>");
            sb.AppendLine("<color=#A8B0B8>LOSE: weeks hit 0 first.</color>");
            sb.AppendLine();

            string scoreMark = IsScoreMet ? "✓" : "○";
            string scoreColor = IsScoreMet ? "#7CFF9A" : "#FFE08A";
            sb.AppendLine($"<color={scoreColor}>{scoreMark} SCORE  {colonyScore} / {TargetScore}</color>");
            sb.AppendLine($"  <color=#A8B0B8>{ProgressBar(colonyScore, TargetScore)}</color>");

            GetClimateGains(out float tempGain, out float atmosGain, out float waterGain);
            string climateMark = IsClimateMet ? "✓" : "○";
            string climateColor = IsClimateMet ? "#7CFF9A" : "#FFE08A";
            sb.AppendLine($"<color={climateColor}>{climateMark} CLIMATE GAINS  {climate:P0}</color>");
            // Fixed-width monospace so digits don't shove the rest of the line around.
            sb.AppendLine($"  <color=#A8B0B8><mspace=0.55em>Temp  {FormatGain(tempGain, 1)} / +{GenerationManager.SectorTemperatureDelta:F0}.0 °C</mspace></color>");
            sb.AppendLine($"  <color=#A8B0B8><mspace=0.55em>Atmos {FormatGain(atmosGain, 2)} / +{GenerationManager.SectorAtmosphereDelta:F2} atm</mspace></color>");
            sb.AppendLine($"  <color=#A8B0B8><mspace=0.55em>Water {FormatGain(waterGain, 1)} / +{GenerationManager.SectorWaterDelta:F0}.0 %</mspace></color>");
            sb.AppendLine($"  <color=#A8B0B8>Each sector must GAIN these deltas (not absolute planet floors).</color>");

            string h = hasHeat ? "<color=#7CFF9A>Heat✓</color>" : "<color=#FF8A8A>Heat○</color>";
            string a = hasAir ? "<color=#7CFF9A>Air✓</color>" : "<color=#FF8A8A>Air○</color>";
            string w = hasWater ? "<color=#7CFF9A>Water✓</color>" : "<color=#FF8A8A>Water○</color>";
            sb.AppendLine($"  <color=#A8B0B8>In this sector:</color> {h}  {a}  {w}");

            if (!hasWater)
            {
                if (hasHeat && hasAir)
                    sb.AppendLine("  <color=#8FE7FF>Water unlock: Heat+Air in this sector → Water Ice Aquifer card</color>");
                else if (!hasHeat && !hasAir)
                    sb.AppendLine("  <color=#FFE08A>Need Heat (GHG) and Air (Condenser) tiles — together they unlock Water</color>");
                else if (!hasHeat)
                    sb.AppendLine("  <color=#FFE08A>Need a Heat tile (GHG Factory) — with Air it unlocks Water</color>");
                else
                    sb.AppendLine("  <color=#FFE08A>Need an Air tile (Atmospheric Condenser) — with Heat it unlocks Water</color>");
            }
            else
            {
                sb.AppendLine("  <color=#A8B0B8>Trio combo: Heat+Air→Water · Air+Water→Heat · Water+Heat→Air</color>");
            }

            string weekColor = weeksRemaining <= 2 ? "#FF8A8A" : (weeksRemaining <= 4 ? "#FFE08A" : "#C8D0D8");
            sb.AppendLine($"<color={weekColor}>WEEKS LEFT  {weeksRemaining}</color>");
            sb.AppendLine($"  <color=#A8B0B8>Card week costs vary (0–2). Check the card chip.</color>");
            return sb.ToString();
        }

        private static string ProgressBar(int value, int max, int width = 10)
        {
            if (max <= 0) return new string('█', width);
            float t = Mathf.Clamp01(value / (float)max);
            int filled = Mathf.RoundToInt(t * width);
            return new string('█', filled) + new string('░', width - filled);
        }

        /// <summary>Fixed-width signed gain so HUD lines don't jitter as values change.</summary>
        private static string FormatGain(float gain, int decimals)
        {
            string body = decimals <= 0
                ? Mathf.Abs(gain).ToString("F0")
                : Mathf.Abs(gain).ToString($"F{decimals}");
            // Pad so "+12.3" and "+0.0" occupy similar width inside <mspace>.
            string signed = (gain < -0.0005f ? "-" : "+") + body;
            return signed.PadLeft(6 + decimals);
        }
    }
}
