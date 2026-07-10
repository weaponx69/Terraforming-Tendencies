using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Represents a human colonist on Mars.
    /// Can enter buildings to stay safe, and needs oxygen/spacesuit when outside.
    /// </summary>
    public class MartianColonist : MonoBehaviour
    {
        public static MartianColonist Instance { get; private set; }

        [Header("Survival Settings")]
        public float MaxOxygen = 100f;
        public float CurrentOxygen = 100f;
        public float OxygenDepletionRate = 12f; // Drains in ~8 seconds without suit/LS
        public float OxygenRefillRate = 30f;    // Refills quickly inside

        [Header("Equipment")]
        public bool HasSpacesuit = false;

        private AbstractCommandable commandable;
        private NavMeshAgent agent;
        private Collider col;
        private BaseBuilding currentBuilding;
        private bool isInside = false;

        private void Awake()
        {
            Instance = this;
            commandable = GetComponent<AbstractCommandable>();
            agent = GetComponent<NavMeshAgent>();
            col = GetComponent<Collider>();
        }

        private void Start()
        {
            // Scale the unit down significantly so it represents a micro-human fitting inside buildings
            transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            
            if (commandable != null)
            {
                commandable.gameObject.name = "Colony Commander";
                commandable.Owner = Owner.Player1;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (isInside)
            {
                // Refill oxygen
                CurrentOxygen = Mathf.Min(CurrentOxygen + OxygenRefillRate * Time.deltaTime, MaxOxygen);
                return;
            }

            // Outside survival logic:
            bool inLifeSupport = false;
            var nodes = FindObjectsByType<GameDevTV.RTS.Environment.LifeSupportNode>(FindObjectsInactive.Exclude);
            foreach (var node in nodes)
            {
                if (node != null && Vector3.Distance(transform.position, node.transform.position) <= node.Radius)
                {
                    inLifeSupport = true;
                    break;
                }
            }

            if (!inLifeSupport && !HasSpacesuit)
            {
                CurrentOxygen = Mathf.Max(CurrentOxygen - OxygenDepletionRate * Time.deltaTime, 0f);
                if (CurrentOxygen <= 0f && commandable != null)
                {
                    // Suffocation damage (20 HP per second)
                    commandable.TakeDamage(Mathf.RoundToInt(20f * Time.deltaTime));
                }
            }
            else
            {
                // Stable or slow refill
                CurrentOxygen = Mathf.Min(CurrentOxygen + 5f * Time.deltaTime, MaxOxygen);
            }

            // Enter building detection:
            // Check if we are close to a completed building that we walked to
            if (agent != null && agent.isActiveAndEnabled && agent.hasPath)
            {
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (var b in buildings)
                {
                    if (b != null && b.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        float distToBuilding = Vector3.Distance(transform.position, b.transform.position);
                        // If we are close (within 2.2 units) and have arrived near it
                        if (distToBuilding <= 2.2f && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                        {
                            EnterBuilding(b);
                            break;
                        }
                    }
                }
            }
        }

        public void EnterBuilding(BaseBuilding building)
        {
            if (building == null) return;
            currentBuilding = building;
            isInside = true;

            // Stop movement
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }

            if (col != null) col.enabled = false;

            // Hide visuals
            SetVisualsActive(false);

            Debug.Log($"[MartianColonist] Entered building: {building.gameObject.name}");
        }

        public void ExitBuilding()
        {
            if (!isInside || currentBuilding == null) return;

            isInside = false;

            // Position outside the building
            Vector3 spawnPos = currentBuilding.transform.position + Vector3.forward * 3f;
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }
            transform.position = spawnPos;

            if (agent != null)
            {
                agent.enabled = true;
            }
            if (col != null) col.enabled = true;

            // Show visuals
            SetVisualsActive(true);

            currentBuilding = null;
            Debug.Log("[MartianColonist] Exited building onto Martian surface.");
        }

        private void SetVisualsActive(bool active)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // Ignore selection outline rings/indicators
                string lowerName = r.gameObject.name.ToLower();
                if (lowerName.Contains("indicator") || lowerName.Contains("selection") || r is SpriteRenderer) continue;
                r.enabled = active;
            }
        }

        public bool IsAlive => commandable != null && commandable.CurrentHealth > 0;
        public bool IsInside => isInside;
        public BaseBuilding CurrentBuilding => currentBuilding;
    }
}
