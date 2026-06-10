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
        public float maxRegolith = 1000f;
        public float maxIron = 1000f;
        
        [Header("Current Resources")]
        [SerializeField] private float currentRegolith = 100f;
        [SerializeField] private float currentIron = 100f;
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
        private const float PASSIVE_HEAT_RATE = 0.02f; // Even slower passive heat buildup
private const float DRY_ICE_COOLING_FACTOR = 10.0f; // -10% heat per unit of dry ice

        [Header("Movement & Pathing")]
        public float movementSpeed = 0.05f;
        public Vector3 targetPosition;
        public bool isOnPipeline = true;
        public GameDevTV.RTS.Environment.EnergyPipelineManager PipelineManager { get; set; }

        private const float OFFLINE_DAMAGE_RATE = 50.0f; // Damage taken per second when off pipeline
        private float healthFloat;

        [Header("Resource Spawning")]
        private Vector3 lastSpawnPosition;
        private const float SPAWN_DISTANCE_THRESHOLD = 8.0f; // Expose minable deposits every 8 meters

        [Header("Production Settings")]
        private const float REGOLITH_COST = 5.0f;
        private const float IRON_COST = 2.0f;
        private const float PRODUCTION_CYCLE_TIME = 3.0f;
        
        private float productionTimer = 0f;
        private bool isProducing = false;

        [Header("Starvation Settings")]
        [SerializeField] private float maxStarvationDuration = 120f;
        [SerializeField] private float starvationTimer = 0f;

        private bool gameOverTriggered = false;

        protected override void Awake()
        {
            // Force capacities to at least 1000, overriding any old values saved in the prefab's Inspector
            if (maxRegolith < 1000f) maxRegolith = 1000f;
            if (maxIron < 1000f) maxIron = 1000f;

            MaxHealth = 1000;
            CurrentHealth = MaxHealth;
            healthFloat = MaxHealth;
            Owner = Owner.Player1;

            ResetHoppers();
            base.Awake();

            if (selectionIndicator == null)
            {
                // Auto-generate a flat green cylinder as a selection indicator
                selectionIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                selectionIndicator.name = "SelectionIndicator";
                selectionIndicator.transform.SetParent(transform);
                
                // Make it a flat circle under the crawler
                selectionIndicator.transform.localPosition = new Vector3(0, 0.1f, 0);
                selectionIndicator.transform.localScale = new Vector3(3f, 0.05f, 3f);
                
                // Remove collider so it doesn't block clicks/navmesh
                Destroy(selectionIndicator.GetComponent<Collider>());
                
                // Color it a transparent selection green
                Renderer rend = selectionIndicator.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0f, 1f, 0f, 0.5f);
                    mat.SetFloat("_Surface", 1); // Set to Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    rend.material = mat;
                }
                
                selectionIndicator.SetActive(false);
            }
        }

        protected override void Start()
        {
            base.Start();
            lastSpawnPosition = transform.position;
            
            // Ensure we are fueled at start regardless of prefab state.
            if (currentRegolith < REGOLITH_COST || currentIron < IRON_COST)
            {
                ResetHoppers();
            }
        }

        private void Update()
        {
            if (gameOverTriggered) return;

            HandleMovement();
            HandleThermalDynamics();
            HandleStructuralIntegrity();
            HandleProduction();
            CheckAndSpawnResources();
            HandleStarvation();
        }

        private void CheckAndSpawnResources()
        {
            if (PipelineManager == null || PipelineManager.IsCompleted) return;

            if (Vector3.Distance(transform.position, lastSpawnPosition) >= SPAWN_DISTANCE_THRESHOLD)
            {
                PipelineManager.ExposeForgeDeposits();
                lastSpawnPosition = transform.position;
            }
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
                    
                    if (Mathf.FloorToInt(starvationTimer) % 5 == 0 && Time.frameCount % 45 == 0)
                    {
                        float remaining = Mathf.Max(0, maxStarvationDuration - starvationTimer);
                        Debug.LogWarning($"[FoundryCrawler] Starvation Warning: Hoppers empty! Crawler halted. Game Over in {remaining:F0} seconds unless supplied.");
                    }

                    if (starvationTimer >= maxStarvationDuration)
                    {
                        GameOver("Resource Starvation: The Foundry Crawler's hoppers remained empty for too long, halting pipeline construction.");
                    }
                }
                else
                {
                    starvationTimer = 0f;
                }
            }
            else
            {
                // Safety: reset if no longer on a mission
                starvationTimer = 0f;
            }
        }

        private void HandleMovement()
        {
            if (PipelineManager == null || PipelineManager.IsCompleted) return;

            bool hasResources = currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST;
            if (!hasResources) return;

            if (Vector3.Distance(transform.position, targetPosition) <= 0.1f) return;

            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position += direction * (movementSpeed * Time.deltaTime);

            if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);

            float traveled = Vector3.Distance(PipelineManager.StartPosition, transform.position);
            int segmentsNeededByNow = Mathf.FloorToInt(traveled / PipelineManager.SegmentLength);
            int guard = 0;
            while (PipelineManager.BuiltSegments < segmentsNeededByNow && guard < 8)
            {
                if (!PipelineManager.BuildNextSegment()) break;
                guard++;
            }
        }

        public void ResetHoppers()
        {
            currentRegolith = 500f;
            currentIron = 200f;
            starvationTimer = 0f;
            engineTemperature = 0f;
            gameOverTriggered = false;
            Debug.Log($"[FoundryCrawler] Hoppers reset to baseline: Regolith={currentRegolith}, Iron={currentIron}");
        }

        private void HandleThermalDynamics()
        {
            // Engine heat only builds while actively moving on the mission.
            // If the crawler is stationary (e.g. out of fuel, reached target, or waiting), it cools down.
            bool hasResources = currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST;
            bool isMoving = hasResources && !HasReachedTarget;
            
            if (PipelineManager != null && !PipelineManager.IsCompleted && isOnPipeline && isMoving)
            {
                engineTemperature += PASSIVE_HEAT_RATE * Time.deltaTime;
            }
            else
            {
                // Cool down naturally when stationary, out of resources, or mission completed
                engineTemperature = Mathf.Max(0f, engineTemperature - (PASSIVE_HEAT_RATE * 2.0f * Time.deltaTime));
            }
            
            if (engineTemperature >= 100f)
            {
                engineTemperature = 100f;
                GameOver("Critical Meltdown: Engine temperature exceeded maximum tolerance.");
            }
        }

        private void HandleStructuralIntegrity()
        {
            if (!isOnPipeline)
            {
                healthFloat -= OFFLINE_DAMAGE_RATE * Time.deltaTime;
                int nextHealth = Mathf.Max(0, Mathf.FloorToInt(healthFloat));
                if (nextHealth < CurrentHealth) TakeDamage(CurrentHealth - nextHealth);
            }
            else healthFloat = CurrentHealth;
        }

        public override void Die()
        {
            base.Die();
            GameOver("Structural Failure: Crawler destroyed due to lack of pipeline support.");
        }

        private void HandleProduction()
        {
            if (PipelineManager == null || PipelineManager.IsCompleted) { isProducing = false; return; }

            bool hasResources = currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST;
            if (hasResources)
            {
                isProducing = true;
                productionTimer += Time.deltaTime;

                if (productionTimer >= PRODUCTION_CYCLE_TIME)
                {
                    currentRegolith -= REGOLITH_COST;
                    currentIron -= IRON_COST;
                    pipeBuffer++;
                    productionTimer = 0f;
                    OnStatusUpdated?.Invoke();
                }
            }
            else
            {
                isProducing = false;
                productionTimer = 0f;
            }
        }

        public void ConsumeDryIce(float amount)
        {
            if (amount <= 0) return;
            engineTemperature = Mathf.Max(0f, engineTemperature - (amount * DRY_ICE_COOLING_FACTOR));
            Debug.Log("Venting vapor!");
        }

        private void GameOver(string reason)
        {
            if (gameOverTriggered) return;
            gameOverTriggered = true;

            enabled = false;
            Debug.LogError("[FoundryCrawler] Game Over: " + reason);

            var gom = Object.FindAnyObjectByType<GameDevTV.RTS.Player.GameOverManager>();
            if (gom != null)
            {
                var gameReason = reason.IndexOf("meltdown", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? GameDevTV.RTS.Player.GameOverManager.GameOverReason.MachineryFailure 
                    : GameDevTV.RTS.Player.GameOverManager.GameOverReason.Resources;
                
                Debug.Log($"[FoundryCrawler] Notifying GameOverManager with reason: {gameReason}");
                gom.TriggerGameOver(gameReason);
            }
        }

        public int PipeBuffer { get => pipeBuffer; set { pipeBuffer = value; OnStatusUpdated?.Invoke(); } }
        public float CurrentRegolith { get => currentRegolith; set { currentRegolith = value; OnStatusUpdated?.Invoke(); } }
        public float CurrentIron { get => currentIron; set { currentIron = value; OnStatusUpdated?.Invoke(); } }
public bool IsStarving => PipelineManager != null && !PipelineManager.IsCompleted && !(currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST);
        public float StarvationTimer => starvationTimer;
        public float MaxStarvationDuration => maxStarvationDuration;
        public bool HasReachedTarget => Vector3.Distance(transform.position, targetPosition) <= 0.2f;

        public event System.Action OnStatusUpdated;

        public bool TryBuildDrone(DroneType type)
        {
            int cost = 0;
            GameObject prefab = null;
            switch (type)
            {
                case DroneType.Mining: cost = miningDronePipeCost; prefab = miningDronePrefab; break;
                case DroneType.Construction: cost = constructionDronePipeCost; prefab = constructionDronePrefab; break;
                case DroneType.Warrior: cost = warriorDronePipeCost; prefab = warriorDronePrefab; break;
            }

            if (prefab == null || pipeBuffer < cost) return false;

            pipeBuffer -= cost;
            OnStatusUpdated?.Invoke();

            Vector3 spawnOffset = Random.insideUnitSphere * 2.0f;
            spawnOffset.y = 0;
            Vector3 spawnPos = transform.position + spawnOffset;

            GameObject drone = Instantiate(prefab, spawnPos, Quaternion.identity);
            if (drone != null)
            {
                if (drone.TryGetComponent(out AbstractCommandable cmd)) cmd.Owner = Owner.Player1;
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
