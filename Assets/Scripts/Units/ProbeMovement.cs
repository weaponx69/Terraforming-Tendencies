using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Fully self-sufficient autonomous exploration for Probes.
    ///
    /// Probes are "Air Units" that move by driving the NavMeshAgent DIRECTLY
    /// (NOT through AbstractUnit.MoveTo / the BehaviorGraph). The BehaviorGraph is
    /// disabled because it is a Worker gather/build tree that throws "No Animator"
    /// errors and would otherwise reset the path every frame, leaving the probe idle.
    ///
    /// This component also self-heals a NavMeshAgent that spawned disabled or off the
    /// air NavMesh (BaseBuilding can leave it disabled if its spawn sample fails).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ProbeMovement : MonoBehaviour
    {
        private NavMeshAgent agent;
        private float mapWidth;
        private float mapHeight;
        private float repathTimer;
        private bool colorApplied;
        private bool loggedStart;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            // Forcefully disable the Worker behavior graph immediately. 
            // Probes are managed entirely by this script to avoid Worker logic and Animator errors.
            if (TryGetComponent(out Unity.Behavior.BehaviorGraphAgent graph))
            {
                graph.enabled = false;
            }
        }

        private void Start()
        {
            InitializeMapDimensions();
        }

        private void InitializeMapDimensions()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                mapWidth = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                mapHeight = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
            }
        }

        private void ColorizeProbe()
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                // The drone body uses a Shader Graph Toon material whose ONLY colour input is
                // _EmissionColor — there is no _BaseColor/_Color tint (it is textured). Drive
                // emission so probes glow cyan and read clearly distinct from the yellow drones.
                Material mat = r.material; // instance (only a few probes, so this is fine)
                
                if (r.name.Contains("Vision"))
                {
                    // Tint the vision cone to match
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0f, 1f, 1f, 0.4f));
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0f, 1f, 1f, 0.4f));
                    continue;
                }

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    // Extreme intensity to drown out the yellow texture
                    mat.SetColor("_EmissionColor", new Color(0f, 1f, 1f) * 4.0f); 
                }
            }
            if (renderers.Length > 0) colorApplied = true;
        }

        private void Update()
        {
            // Apply color once the mesh hierarchy is ready.
            if (!colorApplied) ColorizeProbe();

            if (mapWidth <= 0 || mapHeight <= 0)
            {
                InitializeMapDimensions();
                return;
            }

            if (agent == null) return;

            // Self-heal: agent may have spawned disabled or off the air NavMesh.
            if (!agent.enabled)
            {
                agent.enabled = true;
                return; // give it a frame to initialize
            }
            if (!agent.isOnNavMesh)
            {
                WarpOntoNavMesh();
                return;
            }
            if (agent.isStopped) agent.isStopped = false;

            repathTimer += Time.deltaTime;

            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 1f;
            bool noPath = !agent.hasPath && !agent.pathPending;

            if (arrived || noPath || repathTimer > 10f)
            {
                MoveToRandom();
                repathTimer = 0f;
            }
        }

        private void WarpOntoNavMesh()
        {
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = NavMesh.AllAreas
            };
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 30f, filter))
            {
                agent.Warp(hit.position);
            }
        }

        private void MoveToRandom()
        {
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = NavMesh.AllAreas
            };

            for (int i = 0; i < 20; i++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(2f, mapWidth - 2f),
                    transform.position.y,
                    Random.Range(2f, mapHeight - 2f));

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 25f, filter))
                {
                    agent.SetDestination(hit.position); // direct control — no BehaviorGraph
                    return;
                }
            }
        }
    }
}
