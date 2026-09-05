using System.Text;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Ticks climate generation for all completed buildings each frame.
    /// Prefabs often ship with <see cref="BaseBuilding"/> disabled, and some subclasses
    /// replace Update — this keeps atmosphere/temp/water progressing regardless.
    /// </summary>
    public class ClimateGenerationTicker : MonoBehaviour
    {
        public static ClimateGenerationTicker Instance { get; private set; }

        private float nextDiagTime;
        private float lastAtmosSample = float.NaN;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(ClimateGenerationTicker));
            DontDestroyOnLoad(go);
            go.AddComponent<ClimateGenerationTicker>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var buildings = BaseBuilding.ActiveBuildings;
            if (buildings == null) return;

            for (int i = 0; i < buildings.Count; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null) continue;
                building.TickClimateGeneration(dt);
            }

            if (Time.time >= nextDiagTime)
            {
                nextDiagTime = Time.time + 5f;
                MaybeLogAtmosphereStall();
            }
        }

        /// <summary>One-shot status dump for Unity CLI / debugging.</summary>
        public static string ReportStatus()
        {
            var sb = new StringBuilder();
            float atmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a)
                ? a : float.NaN;
            sb.AppendLine($"Atmos={atmos:F4} ticker={(Instance != null)} activeBuildings={BaseBuilding.ActiveBuildings?.Count ?? 0}");

            var focus = SectorManager.Instance?.GetClimateFocusSector();
            var active = SectorManager.Instance?.ActiveSector;
            sb.AppendLine($"ClimateFocus center={(focus != null ? focus.Center.ToString() : "null")} ActiveSector={(active != null ? active.Center.ToString() : "null")}");

            if (BaseBuilding.ActiveBuildings == null) return sb.ToString();

            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null) continue;
                var def = b.ResolvedBuildingSO;
                if (def?.BuildingConfig == null) continue;
                float atmosRate = def.BuildingConfig.AtmosphereGeneration;
                float tempRate = def.BuildingConfig.TemperatureGeneration;
                float waterRate = def.BuildingConfig.WaterGeneration;
                if (atmosRate <= 0f && tempRate <= 0f && waterRate <= 0f
                    && def.Name != null
                    && (def.Name.Contains("Condenser") || def.Name.Contains("Import") || def.Name.Contains("GHG")))
                {
                    atmosRate = 0.05f;
                }
                if (atmosRate <= 0f && tempRate <= 0f && waterRate <= 0f) continue;

                bool inSector = SectorManager.Instance == null
                    || SectorManager.Instance.DoesBuildingCountForActiveClimate(b);
                var node = b.GetComponent<PowerNode>();
                sb.AppendLine(
                    $"- {b.name}: state={b.Progress.State} operating={b.IsOperating} " +
                    $"powered={(node != null && node.IsPowered)} inActiveSector={inSector} " +
                    $"rates T/A/W={tempRate:F3}/{atmosRate:F3}/{waterRate:F3} " +
                    $"def={(def != null ? def.Name : "null")}");
            }

            return sb.ToString();
        }

        private void MaybeLogAtmosphereStall()
        {
            float atmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a)
                ? a : float.NaN;
            if (float.IsNaN(atmos)) return;

            bool hasAtmosProducer = false;
            bool anyBlocked = false;
            if (BaseBuilding.ActiveBuildings != null)
            {
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
                    hasAtmosProducer = true;
                    if (!b.IsOperating
                        || (SectorManager.Instance != null
                            && !SectorManager.Instance.DoesBuildingCountForActiveClimate(b)))
                    {
                        anyBlocked = true;
                    }
                }
            }

            if (!hasAtmosProducer) 
            {
                lastAtmosSample = atmos;
                return;
            }

            if (!float.IsNaN(lastAtmosSample) && atmos <= lastAtmosSample + 0.0001f && anyBlocked)
            {
                Debug.LogWarning("[ClimateGenerationTicker] Atmosphere producers present but Atmos is not rising.\n" + ReportStatus());
            }

            lastAtmosSample = atmos;
        }
    }
}
