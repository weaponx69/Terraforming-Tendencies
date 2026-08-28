using GameDevTV.RTS.Environment;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Shared NavMesh placement for units, especially air drones that must spawn on the
    /// elevated FlyZone mesh rather than the ground mesh.
    /// </summary>
    public static class NavMeshSpawnUtility
    {
        public const float DefaultSampleRadius = 25f;

        public static bool TryGetSpawnPosition(Vector3 approximatePosition, int agentTypeId, out Vector3 spawnPosition, float sampleRadius = DefaultSampleRadius)
        {
            spawnPosition = approximatePosition;

            if (TrySamplePosition(approximatePosition, agentTypeId, sampleRadius, out NavMeshHit hit))
            {
                spawnPosition = hit.position;
                return true;
            }

            if (agentTypeId == 0) return false;

            float flightHeight = PlanetGenerator.Instance != null ? PlanetGenerator.Instance.AirUnitFlightHeight : 4f;
            if (TrySamplePosition(approximatePosition + Vector3.up * flightHeight, agentTypeId, sampleRadius, out hit))
            {
                spawnPosition = hit.position;
                return true;
            }

            return false;
        }

        public static bool EnsureAgentOnNavMesh(NavMeshAgent agent, float sampleRadius = DefaultSampleRadius)
        {
            if (agent == null) return false;

            Vector3 position = agent.transform.position;
            if (!TryGetSpawnPosition(position, agent.agentTypeID, out Vector3 meshPosition, sampleRadius))
            {
                return false;
            }

            bool needsWarp = !agent.isOnNavMesh || !agent.enabled
                || Vector3.Distance(agent.transform.position, meshPosition) > 0.5f;

            if (!needsWarp)
            {
                return true;
            }

            agent.enabled = false;
            agent.transform.position = meshPosition;
            agent.enabled = true;
            agent.Warp(meshPosition);
            agent.transform.position = meshPosition;
            return agent.isOnNavMesh;
        }

        public static bool TrySamplePosition(Vector3 position, int agentTypeId, float radius, out NavMeshHit hit)
        {
            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeId,
                areaMask = NavMesh.AllAreas
            };
            return NavMesh.SamplePosition(position, out hit, radius, filter);
        }
    }
}
