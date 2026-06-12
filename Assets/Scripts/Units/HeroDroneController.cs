using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Drives a "Hero" command drone with direct WASD-style input (Riftbreaker style).
    /// Receives a world-space planar movement vector from <see cref="GameDevTV.RTS.Player.PlayerInput"/>.
    /// The Hero Drone is player-piloted only, so the NavMeshAgent is permanently decoupled from the
    /// transform (updatePosition/updateRotation = false): only this controller moves the transform.
    /// The agent is left ENABLED on purpose — AbstractUnit.Update force-re-enables any disabled agent
    /// every frame, so disabling it would just let the re-enabled agent snap the drone back.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class HeroDroneController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("World units per second while piloting with WASD.")]
        [SerializeField] private float moveSpeed = 12f;
        [Tooltip("Degrees per second the drone rotates to face its travel direction.")]
        [SerializeField] private float rotationSpeed = 720f;

        [Header("NavMesh")]
        [Tooltip("Adopt NavMesh height while moving so the drone follows terrain/flight-zone elevation.")]
        [SerializeField] private bool snapToNavMesh = true;
        [SerializeField] private float navMeshSampleDistance = 5f;
        [Tooltip("Fallback hover height above Y=0 when no air NavMesh is found at the sampled position.")]
        [SerializeField] private float fallbackHoverHeight = 4f;

        [Header("Cargo & Interaction")]
        [Tooltip("How often the drone auto-vacuums or auto-deposits (seconds).")]
        [SerializeField] private float interactionCooldown = 0.5f;
        [Tooltip("Radius around the drone to search for resources or drop-off points.")]
        [SerializeField] private float interactionRadius = 4f;
        [SerializeField] private GameDevTV.RTS.Behavior.GatherSuppliesEventChannel gatherEventChannel;

        private NavMeshAgent agent;
        private HeroDrone heroDrone;

        private Vector2 pendingMove;
        private bool isManuallyControlled;
        private float mapWidth;
        private float mapHeight;
        private float interactionTimer;

        /// <summary>True while the player is actively piloting this drone with WASD.</summary>
        public bool IsBeingManuallyControlled => isManuallyControlled;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            heroDrone = GetComponent<HeroDrone>();
            
            if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = true;
            }
        }

        private void Start()
        {
            // Cache map dimensions for boundary clamping.
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                mapWidth = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                mapHeight = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
            }

            if (agent != null)
            {
                agent.enabled = false;
            }

            // Set starting height to the correct hover/air NavMesh height.
            Vector3 startPos = transform.position;
            NavMeshQueryFilter filter = new NavMeshQueryFilter 
            { 
                agentTypeID = agent != null ? agent.agentTypeID : 0, 
                areaMask = NavMesh.AllAreas 
            };
            
            bool gotNavMeshHit = false;
            NavMeshHit hit = default;
            if (snapToNavMesh && NavMesh.SamplePosition(startPos, out hit, navMeshSampleDistance, filter))
            {
                startPos.y = hit.position.y + (agent != null ? agent.baseOffset : 0f);
                gotNavMeshHit = true;
            }
            else
            {
                startPos.y = fallbackHoverHeight;
            }
            transform.position = startPos;

            if (agent != null)
            {
                agent.enabled = true;
                if (gotNavMeshHit)
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    agent.Warp(startPos);
                }
            }

            DecoupleAgent();

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }

        /// <summary>
        /// Receives a world-space planar movement vector (x = world X, y = world Z) from PlayerInput.
        /// Pass <see cref="Vector2.zero"/> to release manual control.
        /// </summary>
        public void SetMoveInput(Vector2 move)
        {
            pendingMove = move;
        }

        private void Update()
        {
            Vector3 dir = new Vector3(pendingMove.x, 0f, pendingMove.y);

            if (dir.sqrMagnitude > 0.0001f)
            {
                BeginManualControl();
                ApplyMovement(dir.normalized);
            }
            else if (isManuallyControlled)
            {
                EndManualControl();
            }

            // Auto-gather and deposit logic (Vacuum effect)
            interactionTimer += Time.deltaTime;
            if (interactionTimer >= interactionCooldown)
            {
                interactionTimer = 0f;
                HandleAutoInteraction();
            }
        }


        private void ApplyMovement(Vector3 dir)
        {
            Vector3 targetPos = transform.position + dir * (moveSpeed * Time.deltaTime);

            NavMeshQueryFilter filter = new NavMeshQueryFilter 
            { 
                agentTypeID = agent != null ? agent.agentTypeID : 0, 
                areaMask = NavMesh.AllAreas 
            };

            // Smoothly interpolate the Y position so the drone doesn't violently snap
            // up and down when terrain elevation changes or NavMesh tracking is briefly lost.
            float targetY;
            if (snapToNavMesh && NavMesh.SamplePosition(targetPos, out NavMeshHit hit, navMeshSampleDistance, filter))
            {
                targetY = hit.position.y + (agent != null ? agent.baseOffset : 0f);
            }
            else
            {
                targetY = fallbackHoverHeight;
            }
            targetPos.y = Mathf.Lerp(transform.position.y, targetY, Time.deltaTime * 10f);

            // Wrap around map edges so the drone seamlessly appears on the opposite side,
            // matching the toroidal world used by MapWrapper and ProbeMovement.
            if (mapWidth > 0f && mapHeight > 0f)
            {
                if (targetPos.x < 0f) targetPos.x += mapWidth;
                else if (targetPos.x > mapWidth) targetPos.x -= mapWidth;

                if (targetPos.z < 0f) targetPos.z += mapHeight;
                else if (targetPos.z > mapHeight) targetPos.z -= mapHeight;
            }

            transform.position = targetPos;

            // Re-assert decoupling (idempotent) and keep the agent's internal position glued to ours
            // so it can never diverge and snap the transform back, and so the off-NavMesh warp guard
            // in AbstractUnit.Update is never triggered.
            if (agent != null)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.nextPosition = transform.position;
                }
            }

            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void BeginManualControl()
        {
            if (isManuallyControlled) return;
            isManuallyControlled = true;

            DecoupleAgent();
        }

        /// <summary>
        /// Stops the NavMeshAgent from driving the transform without disabling it.
        /// </summary>
        private void DecoupleAgent()
        {
            if (agent == null) return;

            agent.updatePosition = false;
            agent.updateRotation = false;

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }

        private void EndManualControl()
        {
            if (!isManuallyControlled) return;
            isManuallyControlled = false;

            // Align agent's position with the final manual position
            if (agent != null && agent.enabled)
            {
                NavMeshQueryFilter filter = new NavMeshQueryFilter 
                { 
                    agentTypeID = agent.agentTypeID, 
                    areaMask = NavMesh.AllAreas 
                };
                if (snapToNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, filter))
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    agent.Warp(transform.position);
                }
            }

            // Intentionally keep the agent decoupled; the drone simply hovers where the player
            // left it. Re-coupling would teleport the transform back to the agent's internal position.
        }

        private void HandleAutoInteraction()
        {
            if (heroDrone == null) return;

            Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius);

            // 1. Try to DEPOSIT if we have cargo
            if (heroDrone.CarriedAmount > 0)
            {
                foreach (Collider hit in hits)
                {
                    // Deposit at Forge (FoundryCrawler)
                    FoundryCrawler crawler = hit.GetComponentInParent<FoundryCrawler>();
                    if (crawler != null)
                    {
                        string soName = heroDrone.CarriedSupply != null ? heroDrone.CarriedSupply.name.ToLower() : "";
                        bool isIron = soName.Contains("iron");
                        bool isRegolith = soName.Contains("regolith");

                        if (isIron && crawler.CurrentIron < crawler.maxIron)
                        {
                            crawler.AddIron(heroDrone.CarriedAmount);
                            heroDrone.ClearCargo();
                            return;
                        }
                        else if (isRegolith && crawler.CurrentRegolith < crawler.maxRegolith)
                        {
                            crawler.AddRegolith(heroDrone.CarriedAmount);
                            heroDrone.ClearCargo();
                            return;
                        }
                    }

                    // Deposit at BaseBuilding (Command Post) for Biomass
                    BaseBuilding baseBuilding = hit.GetComponentInParent<BaseBuilding>();
                    if (baseBuilding != null && baseBuilding.Owner == GameDevTV.RTS.Units.Owner.Player1 && baseBuilding.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        if (gatherEventChannel == null) gatherEventChannel = Resources.Load<GameDevTV.RTS.Behavior.GatherSuppliesEventChannel>("Events/GatherSuppliesEventChannel");
                        
                        if (gatherEventChannel != null)
                        {
                            gatherEventChannel.SendEventMessage(gameObject, heroDrone.CarriedAmount, heroDrone.CarriedSupply);
                            heroDrone.ClearCargo();
                            return;
                        }
                    }
                }
            }

            // 2. Try to GATHER if we have space
            if (heroDrone.CarriedAmount < heroDrone.MaxCapacity)
            {
                foreach (Collider hit in hits)
                {
                    if (hit.TryGetComponent<GatherableSupply>(out var supply) && supply.Amount > 0 && !supply.IsBusy)
                    {
                        // Ensure we only mix the same supply type, or if we are empty
                        if (heroDrone.CarriedSupply == null || heroDrone.CarriedSupply == supply.Supply)
                        {
                            // Safely lock it from other drones while we extract
                            if (supply.BeginGather())
                            {
                                int gathered = supply.EndGather();
                                heroDrone.AddCargo(supply.Supply, gathered);
                                return; // Only gather from one node per tick
                            }
                        }
                    }
                }
            }
        }
    }
}
