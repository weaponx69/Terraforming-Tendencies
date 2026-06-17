using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(AbstractUnit))]
    public class ProbeLogic : MonoBehaviour
    {
        public float ScanRadius = 20f;
        public float AnalysisDuration = 10f; // How long to analyze a site before expanding
        [SerializeField] private GameDevTV.RTS.UI.Components.WorldProgressBar analysisProgressBar;
        
        public void SetAnalysisProgressBar(GameDevTV.RTS.UI.Components.WorldProgressBar bar)
        {
            analysisProgressBar = bar;
        }

        private AbstractUnit unit;
private float nextSectorCheckTime = 0f;
        private SectorManager.Sector currentTargetSector;
        private float analysisTimer = 0f;

        public float AnalysisProgress
        {
            get
            {
                float currentAnalysisDuration = AnalysisDuration;
                if (unit != null && unit.UnitSO != null && unit.UnitSO.ProbeConfig != null)
                {
                    currentAnalysisDuration /= Mathf.Max(0.1f, unit.UnitSO.ProbeConfig.AnalysisTimeMultiplier);
                }
                return currentAnalysisDuration > 0 ? Mathf.Clamp01(analysisTimer / currentAnalysisDuration) : 0f;
            }
        }
        public bool IsAnalyzing => currentTargetSector != null;

        private void Awake()
        {
            unit = GetComponent<AbstractUnit>();
        }

        private void Start()
        {
            // Movement is handled by the ProbeMovement component
        }

        private void Update()
        {
            float currentScanRadius = ScanRadius;
            if (unit != null && unit.UnitSO != null && unit.UnitSO.SightConfig != null)
            {
                currentScanRadius = unit.UnitSO.SightConfig.SightRadius;
            }

            // Performance note: In a larger game, we'd use a LayerMask OverlapSphere or spatial partitioning
            // For MVP, FindObjectsByType is sufficient for a low number of resources
            HiddenResource[] hiddenResources = FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude);
            foreach (var res in hiddenResources)
            {
                if (!res.IsDiscovered)
                {
                    if (Vector3.Distance(transform.position, res.transform.position) <= currentScanRadius)
                    {
                        res.Discover();
                    }
                }
            }

            if (Time.time >= nextSectorCheckTime)
            {
                nextSectorCheckTime = Time.time + 0.5f;

                if (SectorManager.Instance != null && ColonyExpansionManager.Instance != null)
                {
                    var sector = SectorManager.Instance.GetNearestSector(transform.position);
                    
                    bool isExpansionPhase = Player.GenerationManager.Instance != null && Player.GenerationManager.Instance.IsExpansionPhase;

                    // If we are in a valid sector for expansion
                    if (sector != null && !sector.IsOccupied && !ColonyExpansionManager.Instance.IsExpandingToSector(sector) && !ColonyExpansionManager.Instance.IsSectorVetoed(sector) && isExpansionPhase)
                    {
                        if (currentTargetSector != sector)
                        {
                            currentTargetSector = sector;
                            analysisTimer = 0f;
                            // Debug.Log($"[Probe] Starting analysis of sector at {sector.Center}");
                        }
                        
                        analysisTimer += 0.5f; 
                        
                        if (analysisProgressBar != null) analysisProgressBar.SetProgress(AnalysisProgress);

                        float currentAnalysisDuration = AnalysisDuration;
                        if (unit != null && unit.UnitSO != null && unit.UnitSO.ProbeConfig != null)
                        {
                            currentAnalysisDuration /= Mathf.Max(0.1f, unit.UnitSO.ProbeConfig.AnalysisTimeMultiplier);
                        }

                        if (analysisTimer >= currentAnalysisDuration)
                        {
                            TriggerExpansion(sector);
                            analysisTimer = 0f;
                            currentTargetSector = null;
                            if (analysisProgressBar != null) analysisProgressBar.SetProgress(0f);
                        }
                    }
                    else
                    {
                        // Reset if we leave or sector becomes invalid
                        if (currentTargetSector != null)
                        {
                            currentTargetSector = null;
                            analysisTimer = 0f;
                            if (analysisProgressBar != null) analysisProgressBar.SetProgress(0f);
                        }
                    }
}
            }
        }

        private void TriggerExpansion(SectorManager.Sector sector)
        {
            Vector3 buildPos = transform.position;
            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 20f, filter))
            {
                buildPos = hit.position;
            }
            else
            {
                Ray ray = new Ray(transform.position + Vector3.up * 50f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
                {
                    buildPos = groundHit.point;
                }
            }

            // Ensure building position is actually in the target sector, fallback to sector center if not
            if (SectorManager.Instance.GetNearestSector(buildPos) != sector)
            {
                buildPos = sector.Center;
            }

            ColonyExpansionManager.Instance.StartExpansion(buildPos, sector);
        }
}
}
