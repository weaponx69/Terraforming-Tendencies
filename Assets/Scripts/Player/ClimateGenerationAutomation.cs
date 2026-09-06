using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Live Play Mode climate checks. Prefer wall-clock watch over simulated ticks.
    /// CLI:
    ///   unity command eval "return GameDevTV.RTS.Player.ClimateGenerationAutomation.StartAtmosphereWatch(8f);" --json
    ///   unity command eval "return GameDevTV.RTS.Player.ClimateGenerationAutomation.AtmosphereWatchStatus();" --json
    ///   unity command eval "return GameDevTV.RTS.Player.ClimateGenerationAutomation.DiagnoseAtmosphere();" --json
    /// </summary>
    public static class ClimateGenerationAutomation
    {
        private static AtmosphereWatchHost watchHost;
        private static readonly List<float> watchSamples = new List<float>(64);
        private static readonly List<float> watchTimes = new List<float>(64);
        private static float watchEndRealtime = -1f;
        private static float watchStartAtmos = float.NaN;
        private static bool watchRunning;

        /// <summary>
        /// Sample Supplies.Atmosphere over real Play Mode time (no manual TickClimateGeneration).
        /// Returns immediately; poll <see cref="AtmosphereWatchStatus"/> until Complete.
        /// </summary>
        public static string StartAtmosphereWatch(float seconds = 8f)
        {
            if (!Application.isPlaying)
                return "FAIL: not in Play Mode";

            watchSamples.Clear();
            watchTimes.Clear();
            watchStartAtmos = ReadAtmos();
            watchEndRealtime = Time.realtimeSinceStartup + Mathf.Max(1f, seconds);
            watchRunning = true;

            if (watchHost == null)
            {
                var go = new GameObject(nameof(AtmosphereWatchHost));
                Object.DontDestroyOnLoad(go);
                watchHost = go.AddComponent<AtmosphereWatchHost>();
            }

            watchHost.enabled = true;
            return $"WATCH_STARTED atmos={watchStartAtmos:F4} for {seconds:F1}s realtime. Poll AtmosphereWatchStatus().";
        }

        public static string AtmosphereWatchStatus()
        {
            if (!Application.isPlaying)
                return "FAIL: not in Play Mode";

            float now = Time.realtimeSinceStartup;
            float remaining = watchEndRealtime - now;
            var sb = new StringBuilder();
            sb.AppendLine(watchRunning
                ? $"WATCH_RUNNING remaining={Mathf.Max(0f, remaining):F1}s samples={watchSamples.Count}"
                : $"WATCH_COMPLETE samples={watchSamples.Count}");

            float current = ReadAtmos();
            sb.AppendLine($"atmos_start={watchStartAtmos:F4} atmos_now={current:F4} delta={(current - watchStartAtmos):F4}");

            if (watchSamples.Count > 0)
            {
                sb.Append("samples=");
                int from = Mathf.Max(0, watchSamples.Count - 8);
                for (int i = from; i < watchSamples.Count; i++)
                {
                    if (i > from) sb.Append(" → ");
                    sb.Append(watchSamples[i].ToString("F4"));
                }
                sb.AppendLine();
            }

            if (!watchRunning && watchSamples.Count >= 2)
            {
                float first = watchSamples[0];
                float last = watchSamples[watchSamples.Count - 1];
                sb.AppendLine(last > first + 0.0001f
                    ? "RESULT: PASS (Atmos rose in real time)"
                    : "RESULT: FAIL (Atmos flat over watch window)");
                sb.AppendLine(DiagnoseAtmosphere());
            }

            return sb.ToString();
        }

        /// <summary>Cap / baseline / producer snapshot for CLI diagnosis.</summary>
        public static string DiagnoseAtmosphere()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- DiagnoseAtmosphere ---");
            float atmos = ReadAtmos();
            Supplies.GetTerraformingCaps(out float maxAtmos, out _, out _, out _, out _);
            sb.AppendLine($"Atmos={atmos:F4} Cap={maxAtmos:F4} headroom={(maxAtmos - atmos):F4}");

            var gm = GenerationManager.Instance;
            if (gm != null)
            {
                sb.AppendLine(
                    $"Gen={gm.CurrentGeneration} baselineAtmos={gm.BaselineAtmosphere:F4} " +
                    $"roundTarget={gm.GetRoundAtmosphereTarget():F4} " +
                    $"need={(gm.GetRoundAtmosphereTarget() - atmos):F4} " +
                    $"betweenRounds={gm.IsBetweenRounds} expansion={gm.IsExpansionPhase}");
                float progress = gm.CalculateCurrentSectorProgress(out string bottleneck);
                sb.AppendLine($"sectorProgress={progress:P0} bottleneck={bottleneck ?? "none"}");
            }
            else
            {
                sb.AppendLine("GenerationManager=null");
            }

            int unlocked = 0;
            int total = SectorManager.Instance?.Sectors?.Count ?? 0;
            if (SectorManager.Instance?.Sectors != null)
            {
                foreach (var s in SectorManager.Instance.Sectors)
                    if (s != null && !s.IsLocked) unlocked++;
            }
            sb.AppendLine($"sectors unlocked={unlocked}/{total}");
            sb.Append(ClimateGenerationTicker.ReportStatus());
            return sb.ToString();
        }

        internal static void WatchTick()
        {
            if (!watchRunning) return;

            float atmos = ReadAtmos();
            float t = Time.realtimeSinceStartup;
            if (watchSamples.Count == 0
                || Mathf.Abs(atmos - watchSamples[watchSamples.Count - 1]) > 0.00005f
                || t - watchTimes[watchTimes.Count - 1] >= 0.5f)
            {
                watchSamples.Add(atmos);
                watchTimes.Add(t);
            }

            if (t >= watchEndRealtime)
            {
                watchRunning = false;
                if (watchHost != null) watchHost.enabled = false;
            }
        }

        private static float ReadAtmos()
        {
            if (Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a))
                return a;
            return float.NaN;
        }

        private sealed class AtmosphereWatchHost : MonoBehaviour
        {
            private void Update() => WatchTick();
        }

        /// <summary>
        /// Places solar + atmosphere (waiveCost) then starts a real-time watch — does not
        /// call TickClimateGeneration manually.
        /// </summary>
        public static string TryVerifyAtmosphereRisesRealtime(float watchSeconds = 6f)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Climate Generation Realtime Verify ===");

            if (!Application.isPlaying)
            {
                sb.AppendLine("FAIL: Editor is not in Play Mode.");
                return sb.ToString();
            }

            if (SectorManager.Instance?.GetClimateFocusSector() == null
                && SectorManager.Instance?.ActiveSector == null)
            {
                sb.AppendLine("FAIL: No climate focus / ActiveSector.");
                return sb.ToString();
            }

            BlueprintDraftManager.Reset();

            BuildingSO solarSo = ResolveBuildingSO("Solar Panel");
            BuildingSO atmosSo = ResolveBuildingSO("Atmospheric Condenser")
                ?? ResolveBuildingSO("Carbon Dioxide Import Laser")
                ?? ResolveBuildingSO("GHG Factory");

            if (solarSo?.Prefab == null || atmosSo?.Prefab == null)
            {
                sb.AppendLine("FAIL: BuildingSO/Prefab missing.");
                return sb.ToString();
            }

            if (!TryEnsurePoweredAtmosphereBuilding(solarSo, atmosSo, out BaseBuilding atmosBuilding, out string setupReason))
            {
                sb.AppendLine($"FAIL: setup — {setupReason}");
                sb.AppendLine(DiagnoseAtmosphere());
                return sb.ToString();
            }

            sb.AppendLine($"Placed {atmosBuilding.name} operating={atmosBuilding.IsOperating}");
            sb.AppendLine(StartAtmosphereWatch(watchSeconds));
            sb.AppendLine("Poll AtmosphereWatchStatus() until WATCH_COMPLETE.");
            return sb.ToString();
        }

        /// <summary>
        /// Meet Temp/Atmos/Water round targets and confirm the sector end trigger fires.
        /// Does not click through Generation Summary UI.
        /// </summary>
        public static string TryVerifySectorCompletesWhenClimateMet()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Sector Complete When Climate Met ===");

            if (!Application.isPlaying)
            {
                sb.AppendLine("FAIL: not in Play Mode");
                return sb.ToString();
            }

            var gm = GenerationManager.Instance;
            if (gm == null)
            {
                sb.AppendLine("FAIL: no GenerationManager");
                return sb.ToString();
            }

            if (gm.IsBetweenRounds)
            {
                sb.AppendLine("SKIP: already between rounds");
                return sb.ToString();
            }

            float tempTarget = gm.GetRoundTemperatureTarget();
            float atmosTarget = gm.GetRoundAtmosphereTarget();
            float waterTarget = gm.GetRoundWaterTarget();

            Supplies.UpdateTemperature(Owner.Player1, tempTarget);
            Supplies.UpdateAtmosphere(Owner.Player1, atmosTarget);
            Supplies.UpdateWater(Owner.Player1, waterTarget);

            if (gm.CurrentMilestoneType == MilestoneType.Power)
            {
                Supplies.UpdatePower(Owner.Player1, gm.CurrentMilestoneTarget + 50f);
            }
            else if (gm.CurrentMilestoneType == MilestoneType.Oxygen)
            {
                float cur = Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                Supplies.UpdateOxygen(Owner.Player1, cur + gm.CurrentMilestoneTarget + 0.05f);
            }
            else if (gm.CurrentMilestoneType == MilestoneType.Population)
            {
                Supplies.UpdatePopulation(Owner.Player1, Mathf.CeilToInt(gm.CurrentMilestoneTarget) + 5);
            }

            float progress = gm.CalculateCurrentSectorProgress(out string bottleneck);
            sb.AppendLine($"After force-meet: progress={progress:P0} bottleneck={bottleneck ?? "none"} between={gm.IsBetweenRounds}");
            sb.AppendLine($"temp→{tempTarget:F1} atmos→{atmosTarget:F2} water→{waterTarget:F0}");
            sb.AppendLine($"atmosMet={!GenerationManager.IsUnmetSectorGoal("ATMOSPHERE")} " +
                          $"tempMet={!GenerationManager.IsUnmetSectorGoal("TEMPERATURE")} " +
                          $"waterMet={!GenerationManager.IsUnmetSectorGoal("WATER")}");

            // One CheckMilestones-equivalent frame: GenerationManager.Update may not run same eval frame.
            if (progress >= 0.999f && !gm.IsBetweenRounds)
            {
                gm.TriggerGenerationEnd();
            }

            sb.AppendLine(gm.IsBetweenRounds
                ? "RESULT: PASS (sector end triggered / between rounds)"
                : $"RESULT: FAIL (progress={gm.CalculateCurrentSectorProgress(out _):P0}, still in round)");

            return sb.ToString();
        }

        public static string TryVerifyAtmosphereRises(float simulateSeconds = 3f)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Climate Generation Automation ===");

            if (!Application.isPlaying)
            {
                sb.AppendLine("FAIL: Editor is not in Play Mode. Run: unity command editor_play");
                return sb.ToString();
            }

            if (SectorManager.Instance?.ActiveSector == null)
            {
                sb.AppendLine("FAIL: No ActiveSector (wait for planet bootstrap).");
                return sb.ToString();
            }

            BlueprintDraftManager.Reset();

            BuildingSO solarSo = ResolveBuildingSO("Solar Panel");
            BuildingSO atmosSo = ResolveBuildingSO("Atmospheric Condenser")
                ?? ResolveBuildingSO("Carbon Dioxide Import Laser")
                ?? ResolveBuildingSO("GHG Factory");

            if (solarSo == null || solarSo.Prefab == null)
            {
                sb.AppendLine("FAIL: Solar Panel BuildingSO/Prefab missing.");
                return sb.ToString();
            }
            if (atmosSo == null || atmosSo.Prefab == null)
            {
                sb.AppendLine("FAIL: No atmosphere BuildingSO/Prefab found (Condenser / CO2 Laser / GHG).");
                return sb.ToString();
            }

            sb.AppendLine($"Using atmosphere building: {atmosSo.Name}");
            sb.AppendLine($"Config atmosRate={atmosSo.BuildingConfig?.AtmosphereGeneration ?? -1f}");

            if (!TryEnsurePoweredAtmosphereBuilding(solarSo, atmosSo, out BaseBuilding atmosBuilding, out string setupReason))
            {
                sb.AppendLine($"FAIL: setup — {setupReason}");
                sb.AppendLine(ClimateGenerationTicker.ReportStatus());
                return sb.ToString();
            }

            sb.AppendLine($"Placed {atmosBuilding.name} operating={atmosBuilding.IsOperating} " +
                          $"inActiveSector={SectorManager.Instance.DoesBuildingCountForActiveClimate(atmosBuilding)}");

            float before = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a0) ? a0 : 0.01f;
            sb.AppendLine($"Atmos before={before:F4}");

            // Deterministic ticks (does not require waiting on Update frames).
            float step = 0.25f;
            for (float t = 0f; t < simulateSeconds; t += step)
            {
                atmosBuilding.TickClimateGeneration(step);
                var buildings = BaseBuilding.ActiveBuildings;
                if (buildings == null) continue;
                for (int i = 0; i < buildings.Count; i++)
                {
                    BaseBuilding b = buildings[i];
                    if (b == null || b == atmosBuilding) continue;
                    b.TickClimateGeneration(step);
                }
            }

            float after = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a1) ? a1 : 0.01f;
            sb.AppendLine($"Atmos after={after:F4} (simulated {simulateSeconds:F1}s)");
            sb.AppendLine(ClimateGenerationTicker.ReportStatus());
            sb.AppendLine(DiagnoseAtmosphere());

            if (after > before + 0.0001f)
            {
                sb.AppendLine("RESULT: PASS");
            }
            else
            {
                sb.AppendLine("RESULT: FAIL (atmosphere did not increase)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Instant-place solar + atmosphere on an ActiveSector cluster (waives cost / drone).
        /// </summary>
        public static bool TryEnsurePoweredAtmosphereBuilding(
            BuildingSO solarSo,
            BuildingSO atmosSo,
            out BaseBuilding atmosBuilding,
            out string reason)
        {
            atmosBuilding = null;
            reason = null;

            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                var def = b.ResolvedBuildingSO;
                float rate = def?.BuildingConfig != null ? def.BuildingConfig.AtmosphereGeneration : 0f;
                if (rate <= 0f && def?.Name != null
                    && (def.Name.Contains("Condenser") || def.Name.Contains("Import") || def.Name.Contains("GHG")))
                {
                    rate = 0.05f;
                }
                if (rate <= 0f) continue;
                if (!SectorManager.Instance.DoesBuildingCountForActiveClimate(b)) continue;

                ReservedSiteBuildUtility.EnsureClusterPowerForBuilding(b);
                PowerGridManager.RecalculateGrids();
                if (!b.IsOperating)
                {
                    var node = b.GetComponent<PowerNode>();
                    if (node != null) node.IsGridPowered = true;
                }

                if (b.IsOperating)
                {
                    atmosBuilding = b;
                    return true;
                }
            }

            SectorManager.Sector sector = SectorManager.Instance.GetClimateFocusSector()
                ?? SectorManager.Instance.ActiveSector;
            BuildingSiteCluster cluster = FindOpenCluster(sector);
            if (cluster == null)
            {
                foreach (var s in SectorManager.Instance.Sectors)
                {
                    if (s == null) continue;
                    cluster = FindOpenCluster(s);
                    if (cluster != null)
                    {
                        sector = s;
                        SectorManager.Instance.BeginTerraformingOn(s);
                        break;
                    }
                }
            }

            if (cluster?.SolarSlot == null || cluster.BuildingSlot == null)
            {
                reason = "No open solar+building cluster on the planet.";
                return false;
            }

            if (cluster.CanPlaceSolar)
            {
                if (!ReservedSiteBuildUtility.TryBuildAtSite(solarSo, Owner.Player1, cluster.SolarSlot, out reason, waiveCost: true))
                {
                    return false;
                }
            }

            if (cluster.SolarBuilding == null)
            {
                reason = "Cluster still has no solar after place attempt.";
                return false;
            }

            if (!cluster.CanPlaceBuilding && cluster.BuildingSlot.IsOccupied)
            {
                atmosBuilding = cluster.BuildingSlot.OccupyingBuilding;
            }
            else
            {
                if (!ReservedSiteBuildUtility.TryBuildAtSite(atmosSo, Owner.Player1, cluster.BuildingSlot, out reason, waiveCost: true))
                {
                    return false;
                }
                atmosBuilding = cluster.BuildingSlot.OccupyingBuilding;
            }

            if (atmosBuilding == null)
            {
                reason = "Atmosphere building missing after place.";
                return false;
            }

            ReservedSiteBuildUtility.EnsureClusterPowerForBuilding(atmosBuilding);
            PowerGridManager.RecalculateGrids();

            if (!atmosBuilding.IsOperating)
            {
                var node = atmosBuilding.GetComponent<PowerNode>();
                if (node != null) node.IsGridPowered = true;
            }

            if (!atmosBuilding.IsOperating)
            {
                reason = $"{atmosBuilding.name} is not operating after power wiring.";
                return false;
            }

            return true;
        }

        private static BuildingSiteCluster FindOpenCluster(SectorManager.Sector sector)
        {
            if (sector?.BuildingClusters == null) return null;
            foreach (var cluster in sector.BuildingClusters)
            {
                if (cluster == null) continue;
                if (cluster.CanPlaceSolar && cluster.BuildingSlot != null && !cluster.BuildingSlot.IsOccupied)
                    return cluster;
                if (cluster.CanPlaceBuilding)
                    return cluster;
            }
            return null;
        }

        private static BuildingSO ResolveBuildingSO(string name)
        {
            BuildingSO so = BlueprintDraftManager.GetBuildingSOByName(name);
            if (so != null) return so;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:BuildingSO {name}");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
                if (asset == null) continue;
                if (!string.Equals(asset.Name, name, System.StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(asset.name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BlueprintDraftManager.RegisterBuildingSO(asset);
                return asset;
            }
#endif
            return null;
        }
    }
}
