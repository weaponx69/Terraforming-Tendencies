using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Core logistics engine for the Foundry Crawler.
    /// Manages movement, resource processing, thermal dynamics, and structural integrity.
    /// </summary>
    public class FoundryCrawler : AbstractCommandable
    {
        public enum DroneType
        {
            Mining,
            Construction,
            Warrior
        }

        [Header("Resource Capacities")]
        public float maxRegolith = 100f;
        public float maxIron = 100f;
        
        [Header("Current Resources")]
        [SerializeField] private float currentRegolith = 25f;
        [SerializeField] private float currentIron = 10f;
        [SerializeField] private int pipeBuffer = 0;

        [Header("Drone Production")]
        [SerializeField] private GameObject miningDronePrefab;
        [SerializeField] private GameObject constructionDronePrefab;
        [SerializeField] private GameObject warriorDronePrefab;

        [Header("Drone Costs (in Pipes)")]
        public int miningDronePipeCost = 3;
        public int constructionDronePipeCost = 4;
        public int warriorDronePipeCost = 5;

        [Header("Thermal Dynamics")]
        [Tooltip("Current engine heat, from 0 to 100")]
        [Range(0f, 100f)]
        [SerializeField] private float engineTemperature = 0f;
        private const float PASSIVE_HEAT_RATE = 1.0f; // +1% heat per second
        private const float DRY_ICE_COOLING_FACTOR = 10.0f; // -10% heat per unit of dry ice

        [Header("Movement & Pathing")]
        public float movementSpeed = 0.5f;
        public Vector3 targetPosition;
        public bool isOnPipeline = true;
        public GameDevTV.RTS.Environment.EnergyPipelineManager PipelineManager { get; set; }

        private const float OFFLINE_DAMAGE_RATE = 50.0f; // Damage taken per second when off pipeline
        private float healthFloat;

        [Header("Resource Spawning")]
        private Vector3 lastSpawnPosition;
        private const float SPAWN_DISTANCE_THRESHOLD = 8.0f; // Spawn resources every 8 meters
        private GameObject gasPrefab;
        private GameObject mineralsPrefab;

        [Header("Production Settings")]
        private const float REGOLITH_COST = 5.0f;
        private const float IRON_COST = 2.0f;
        private const float PRODUCTION_CYCLE_TIME = 3.0f;
        
        private float productionTimer = 0f;
        private bool isProducing = false;

        [Header("Starvation Settings")]
        [SerializeField] private float maxStarvationDuration = 60f;
        [SerializeField] private float starvationTimer = 0f;

        protected override void Awake()
        {
            MaxHealth = 1000;
            CurrentHealth = MaxHealth;
            healthFloat = MaxHealth;
            Owner = Owner.Player1;

            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            lastSpawnPosition = transform.position;
            LoadResourcePrefabs();
        }

        private void Update()
        {
            HandleMovement();
            HandleThermalDynamics();
            HandleStructuralIntegrity();
            HandleProduction();
            CheckAndSpawnResources();
            HandleStarvation();
        }

        private void LoadResourcePrefabs()
        {
#if UNITY_EDITOR
            gasPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gatherable Supplies/Gas.prefab");
            mineralsPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gatherable Supplies/Minerals.prefab");
#endif
            if (gasPrefab == null) gasPrefab = Resources.Load<GameObject>("Gatherable Supplies/Gas");
            if (mineralsPrefab == null) mineralsPrefab = Resources.Load<GameObject>("Gatherable Supplies/Minerals");
        }

        private void CheckAndSpawnResources()
        {
            if (Vector3.Distance(transform.position, lastSpawnPosition) >= SPAWN_DISTANCE_THRESHOLD)
            {
                SpawnResourceNode();
                lastSpawnPosition = transform.position;
            }
        }

        private void SpawnResourceNode()
        {
            if (gasPrefab == null || mineralsPrefab == null)
            {
                LoadResourcePrefabs();
            }

            GameObject prefab = Random.value > 0.5f ? gasPrefab : mineralsPrefab;
            if (prefab == null) return;

            // Spawn slightly ahead / to the side of the crawler
            float angle = Random.Range(-45f, 45f); // 90 degree arc in front
            Vector3 forward = transform.forward;
            Vector3 spawnDir = Quaternion.Euler(0, angle, 0) * forward;
            
            float dist = Random.Range(5f, 10f);
            Vector3 spawnPos = transform.position + spawnDir * dist;

            // Project down onto terrain/floor
            Ray ray = new Ray(spawnPos + Vector3.up * 50f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Default", "Terrain")))
            {
                spawnPos.y = hit.point.y;
            }

            GameObject resNode = Instantiate(prefab, spawnPos, Quaternion.identity);
            resNode.name = prefab.name;
        }

        private void HandleStarvation()
        {
            // Starvation only matters while the pipeline is actively expanding/under construction
            if (PipelineManager != null && !PipelineManager.IsCompleted)
            {
                bool hasResources = currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST;
                if (!hasResources)
                {
                    starvationTimer += Time.deltaTime;
                    
                    // Periodically log warning every 5 seconds to let player know
                    if (Mathf.FloorToInt(starvationTimer) % 5 == 0 && Time.frameCount % 45 == 0)
                    {
                        Debug.LogWarning($"[FoundryCrawler] Starvation Warning: Hoppers empty! Crawler halted. Game Over in {maxStarvationDuration - starvationTimer:F0} seconds unless supplied.");
                    }

                    if (starvationTimer >= maxStarvationDuration)
                    {
                        GameOver("Resource Starvation: The Foundry Crawler's hoppers remained empty for too long, halting pipeline construction.");
                    }
                }
                else
                {
                    starvationTimer = 0f; // Reset if supplied
                }
            }
        }

        /// <summary>
        /// Moves the Crawler toward its target position restricted to the already built pipeline segments.
        /// </summary>
        private void HandleMovement()
        {
            // Only move if supplied with materials to keep going
            bool hasResources = currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST;
            if (!hasResources)
            {
                return; // Halted due to lack of supplies
            }

            if (PipelineManager == null)
            {
                // Fallback behavior: move towards targetPosition without restrictions
                if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
                {
                    Vector3 direction = (targetPosition - transform.position).normalized;
                    transform.position += direction * (movementSpeed * Time.deltaTime);
                    
                    if (direction != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
                return;
            }

            // Lock movement to currently built segments
            float maxDistance = PipelineManager.BuiltSegments * PipelineManager.SegmentLength;
            Vector3 startToTarget = (targetPosition - PipelineManager.StartPosition);
            float totalLength = startToTarget.magnitude;
            if (totalLength < 0.1f) return;

            Vector3 dir = startToTarget / totalLength;
            Vector3 currentOffset = transform.position - PipelineManager.StartPosition;
            float currentDist = Vector3.Dot(currentOffset, dir);

            if (currentDist >= maxDistance)
            {
                // Snap to maximum allowed distance along the line so we don't drift past
                transform.position = PipelineManager.StartPosition + dir * maxDistance;
                return;
            }

            if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
            {
                float step = movementSpeed * Time.deltaTime;
                float nextDist = currentDist + step;
                if (nextDist > maxDistance)
                {
                    nextDist = maxDistance;
                }
                transform.position = PipelineManager.StartPosition + dir * nextDist;
                
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        /// <summary>
        /// Increases heat over time. Triggers game over if maximum temperature is reached.
        /// </summary>
        private void HandleThermalDynamics()
        {
            engineTemperature += PASSIVE_HEAT_RATE * Time.deltaTime;
            
            if (engineTemperature >= 100f)
            {
                engineTemperature = 100f;
                GameOver("Critical Meltdown: Engine temperature exceeded maximum tolerance.");
            }
        }

        /// <summary>
        /// Punishes the crawler for leaving the logistics pipeline network.
        /// </summary>
        private void HandleStructuralIntegrity()
        {
            if (!isOnPipeline)
            {
                healthFloat -= OFFLINE_DAMAGE_RATE * Time.deltaTime;
                int nextHealth = Mathf.Max(0, Mathf.FloorToInt(healthFloat));
                if (nextHealth < CurrentHealth)
                {
                    TakeDamage(CurrentHealth - nextHealth);
                }
            }
            else
            {
                healthFloat = CurrentHealth; // Sync up if healed
            }
        }

        public override void Die()
        {
            base.Die();
            GameOver("Structural Failure: Crawler destroyed due to lack of pipeline support.");
        }

        /// <summary>
        /// Consumes raw resources over a set interval to generate output pipes.
        /// </summary>
        private void HandleProduction()
        {
            // Check if we have enough resources to start or sustain a cycle
            bool hasResources = currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST;

            if (hasResources)
            {
                isProducing = true;
                productionTimer += Time.deltaTime;

                if (productionTimer >= PRODUCTION_CYCLE_TIME)
                {
                    // Consume resources
                    currentRegolith -= REGOLITH_COST;
                    currentIron -= IRON_COST;
                    
                    // Increment output buffer
                    pipeBuffer++;
                    
                    // Reset timer for the next batch
                    productionTimer = 0f;
                    OnStatusUpdated?.Invoke();
                }
            }
            else
            {
                isProducing = false;
                productionTimer = 0f; // Reset progress if starved of resources
            }
        }

        /// <summary>
        /// Injects coolant into the engine to reduce temperature.
        /// </summary>
        /// <param name="amount">Units of dry ice consumed.</param>
        public void ConsumeDryIce(float amount)
        {
            if (amount <= 0) return;

            float coolingProvided = amount * DRY_ICE_COOLING_FACTOR;
            engineTemperature = Mathf.Max(0f, engineTemperature - coolingProvided);
            
            Debug.Log("Venting vapor!");
        }

        /// <summary>
        /// Handles the failure state of the Crawler.
        /// </summary>
        private void GameOver(string reason)
        {
            // Disable crawler operations
            enabled = false;
            Debug.LogError("[FoundryCrawler] Game Over: " + reason);

            // Hook into the central GameOverManager losing condition
            var gom = Object.FindAnyObjectByType<GameDevTV.RTS.Player.GameOverManager>();
            if (gom != null)
            {
                gom.TriggerGameOver(GameDevTV.RTS.Player.GameOverManager.GameOverReason.Resources);
            }
        }

        // --- Helper Methods for external scripts to load resources ---
        
        public int PipeBuffer
        {
            get => pipeBuffer;
            set => pipeBuffer = value;
        }

        public float CurrentRegolith
        {
            get => currentRegolith;
            set => currentRegolith = value;
        }

        public float CurrentIron
        {
            get => currentIron;
            set => currentIron = value;
        }

        public bool IsStarving => PipelineManager != null && !PipelineManager.IsCompleted && !(currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST);
        public float StarvationTimer => starvationTimer;
        public float MaxStarvationDuration => maxStarvationDuration;

        public bool HasReachedTarget => Vector3.Distance(transform.position, targetPosition) <= 0.2f;

        public event System.Action OnStatusUpdated;

        /// <summary>
        /// Attempts to build a drone from the pipe buffer.
        /// </summary>
        public bool TryBuildDrone(DroneType type)
        {
            int cost = 0;
            GameObject prefab = null;

            switch (type)
            {
                case DroneType.Mining:
                    cost = miningDronePipeCost;
                    prefab = miningDronePrefab;
                    break;
                case DroneType.Construction:
                    cost = constructionDronePipeCost;
                    prefab = constructionDronePrefab;
                    break;
                case DroneType.Warrior:
                    cost = warriorDronePipeCost;
                    prefab = warriorDronePrefab;
                    break;
            }

            if (prefab == null)
            {
                Debug.LogWarning("[FoundryCrawler] Drone prefab is null for type: " + type);
                return false;
            }

            if (pipeBuffer < cost)
            {
                Debug.LogWarning("[FoundryCrawler] Not enough pipes to build " + type + " Drone! Cost: " + cost + ", Buffer: " + pipeBuffer);
                return false;
            }

            pipeBuffer -= cost;
            OnStatusUpdated?.Invoke();

            // Spawn drone with small offset
            Vector3 spawnOffset = Random.insideUnitSphere * 2.0f;
            spawnOffset.y = 0;
            Vector3 spawnPos = transform.position + spawnOffset;

            GameObject drone = Instantiate(prefab, spawnPos, Quaternion.identity);
            if (drone != null)
            {
                if (drone.TryGetComponent(out AbstractCommandable cmd))
                {
                    cmd.Owner = Owner.Player1;
                }

                if (drone.TryGetComponent(out NavMeshAgent agent))
                {
                    agent.enabled = false;
                    drone.transform.position = spawnPos;
                    agent.enabled = true;
                    agent.Warp(spawnPos);
                }
                return true;
            }

            return false;
        }

        public void AddRegolith(float amount)
        {
            currentRegolith = Mathf.Clamp(currentRegolith + amount, 0, maxRegolith);
            OnStatusUpdated?.Invoke();
        }

        public void AddIron(float amount)
        {
            currentIron = Mathf.Clamp(currentIron + amount, 0, maxIron);
            OnStatusUpdated?.Invoke();
        }
    }
}
