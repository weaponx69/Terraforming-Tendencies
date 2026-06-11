using UnityEngine;
using UnityEngine.AI;

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
        [SerializeField] private bool snapToNavMeshHeight = true;
        [SerializeField] private float navMeshSampleDistance = 5f;

        private NavMeshAgent agent;
        private AbstractUnit unit;
        private WorkerBrainController brain;

        private Vector2 pendingMove;
        private bool isManuallyControlled;

        /// <summary>True while the player is actively piloting this drone with WASD.</summary>
        public bool IsBeingManuallyControlled => isManuallyControlled;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            unit = GetComponent<AbstractUnit>();
            brain = GetComponent<WorkerBrainController>();
        }

        private void Start()
        {
            DecoupleAgent();
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
        }

        private void ApplyMovement(Vector3 dir)
        {
            Vector3 targetPos = transform.position + dir * (moveSpeed * Time.deltaTime);

            // Keep our freely-moved XZ; only adopt the NavMesh height so the drone follows
            // terrain/flight-zone elevation. Snapping XZ to the nearest NavMesh point would pin
            // air units (whose NavMesh is a small elevated patch) back in place.
            if (snapToNavMeshHeight
                && NavMesh.SamplePosition(targetPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                targetPos.y = hit.position.y;
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

            // Halt the Worker brain coroutines (gather/build loops) and clear any active unit command
            // so AI logic stops issuing SetDestination calls.
            if (brain != null) brain.Halt();
            if (unit != null) unit.Stop();

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
            // Intentionally keep the agent decoupled; the drone simply hovers where the player
            // left it. Re-coupling would teleport the transform back to the agent's internal position.
        }
    }
}
