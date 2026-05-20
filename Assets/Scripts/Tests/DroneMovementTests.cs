#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;
using Unity.AI.Navigation;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Tests
{
    public class DroneMovementTests
    {
        private GameObject floor;
        private GameObject flyZone;
        private GameObject groundSurfaceObj;
        private GameObject airSurfaceObj;

        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            // Setup ground floor
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(10, 1, 10);
            
            // Setup ground NavMesh
            groundSurfaceObj = new GameObject("GroundSurface");
            var groundSurface = groundSurfaceObj.AddComponent<NavMeshSurface>();
            groundSurface.agentTypeID = 0; // Humanoid
            groundSurface.collectObjects = CollectObjects.All;
            groundSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            groundSurface.BuildNavMesh();
            
            // Setup elevated flyZone
            flyZone = GameObject.CreatePrimitive(PrimitiveType.Plane);
            flyZone.transform.position = new Vector3(0, 4f, 0);
            flyZone.transform.localScale = new Vector3(10, 1, 10);
            
            // Setup air NavMesh
            airSurfaceObj = new GameObject("AirSurface");
            var airSurface = airSurfaceObj.AddComponent<NavMeshSurface>();
            
            // Get Air Agent ID
            int agentCount = NavMesh.GetSettingsCount();
            int airAgentTypeID = 0;
            for (int i = 0; i < agentCount; i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (id != 0)
                {
                    airAgentTypeID = id;
                    break;
                }
            }
            
            airSurface.agentTypeID = airAgentTypeID;
            airSurface.collectObjects = CollectObjects.All;
            airSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            airSurface.BuildNavMesh();
        }

        [TearDown]
        public void TearDown()
        {
            if (floor != null) Object.DestroyImmediate(floor);
            if (flyZone != null) Object.DestroyImmediate(flyZone);
            if (groundSurfaceObj != null) Object.DestroyImmediate(groundSurfaceObj);
            if (airSurfaceObj != null) Object.DestroyImmediate(airSurfaceObj);
        }

        [UnityTest]
        public IEnumerator GroundAgent_WhenTargetSet_MovesToDestination()
        {
            GameObject agentObj = new GameObject("TestGroundAgent");
            agentObj.transform.position = new Vector3(0, 0.1f, 0);
            NavMeshAgent agent = agentObj.AddComponent<NavMeshAgent>();
            agent.agentTypeID = 0;
            agent.speed = 10f;
            agent.acceleration = 100f;
            
            yield return null; // Wait for agent to initialize on NavMesh
            
            Assert.IsTrue(agent.isOnNavMesh, "Ground agent should be on NavMesh.");
            
            Vector3 startPos = agentObj.transform.position;
            Vector3 targetPosition = new Vector3(2f, startPos.y, 2f);
            
            bool setDestSuccess = agent.SetDestination(targetPosition);
            Assert.IsTrue(setDestSuccess, "Ground agent SetDestination failed; destination was rejected.");
            
            // Wait a few frames for path calculation and physical movement
            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }
            
            Assert.IsTrue(agent.hasPath, "Ground agent has no path.");
            
            float distanceMoved = Vector3.Distance(startPos, agentObj.transform.position);
            Assert.Greater(distanceMoved, 0.05f, "Ground agent GameObject did not move visually (transform remained stationary).");
            
            Object.DestroyImmediate(agentObj);
        }

        [UnityTest]
        public IEnumerator AirAgent_WhenTargetSet_MovesToDestination()
        {
            int agentCount = NavMesh.GetSettingsCount();
            int airAgentTypeID = 0;
            for (int i = 0; i < agentCount; i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (id != 0)
                {
                    airAgentTypeID = id;
                    break;
                }
            }
            
            if (airAgentTypeID == 0)
            {
                Assert.Fail("Air Agent settings not found in project config. The game requires Air Agent settings.");
                yield break;
            }
            
            GameObject agentObj = new GameObject("TestAirAgent");
            agentObj.transform.position = new Vector3(0, 4.1f, 0); // Spawns near elevated flyZone
            NavMeshAgent agent = agentObj.AddComponent<NavMeshAgent>();
            agent.agentTypeID = airAgentTypeID;
            agent.speed = 10f;
            agent.acceleration = 100f;
            
            yield return null; // Wait for agent to initialize on NavMesh
            
            Assert.IsTrue(agent.isOnNavMesh, "Air agent should be on NavMesh.");
            
            Vector3 startPos = agentObj.transform.position;
            Vector3 targetPosition = new Vector3(2f, startPos.y, 2f);
            
            bool setDestSuccess = agent.SetDestination(targetPosition);
            Assert.IsTrue(setDestSuccess, "Air agent SetDestination failed; destination was rejected.");
            
            // Wait a few frames for path calculation and physical movement
            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }
            
            Assert.IsTrue(agent.hasPath, "Air agent has no path.");
            
            float distanceMoved = Vector3.Distance(startPos, agentObj.transform.position);
            Assert.Greater(distanceMoved, 0.05f, "Air agent GameObject did not move visually (transform remained stationary).");
            
            Object.DestroyImmediate(agentObj);
        }

        [UnityTest]
        public IEnumerator AirAgent_SpawnedAtGround_WhenWarpedAndTargetSet_MovesToDestination()
        {
            int agentCount = NavMesh.GetSettingsCount();
            int airAgentTypeID = 0;
            for (int i = 0; i < agentCount; i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (id != 0)
                {
                    airAgentTypeID = id;
                    break;
                }
            }
            
            if (airAgentTypeID == 0)
            {
                Assert.Fail("Air Agent settings not found in project config. The game requires Air Agent settings.");
                yield break;
            }
            
            GameObject agentObj = new GameObject("TestAirAgentSpawnGround");
            agentObj.transform.position = new Vector3(0, 0.1f, 0); // Spawns on ground
            NavMeshAgent agent = agentObj.AddComponent<NavMeshAgent>();
            agent.agentTypeID = airAgentTypeID;
            agent.speed = 10f;
            agent.acceleration = 100f;
            
            yield return null; // Wait 1 frame
            
            // Simulates spawning at ground where there is no air NavMesh
            Assert.IsFalse(agent.isOnNavMesh, "Air agent should NOT be on NavMesh at ground level.");
            
            // Recover/warp logic as done by AIController
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(agentObj.transform.position, out NavMeshHit hit, 25f, filter))
            {
                agent.enabled = false;
                agentObj.transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            
            Assert.IsTrue(agent.isOnNavMesh, "Air agent should be on NavMesh after recovery warp.");
            
            Vector3 startPos = agentObj.transform.position;
            Vector3 targetPosition = new Vector3(2f, startPos.y, 2f);
            
            bool setDestSuccess = agent.SetDestination(targetPosition);
            Assert.IsTrue(setDestSuccess, "Air agent SetDestination failed after recovery; destination was rejected.");
            
            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }
            
            Assert.IsTrue(agent.hasPath, "Air agent has no path after recovery.");
            
            float distanceMoved = Vector3.Distance(startPos, agentObj.transform.position);
            Assert.Greater(distanceMoved, 0.05f, "Air agent GameObject did not move visually after recovery warp.");
            
            Object.DestroyImmediate(agentObj);
        }

        [UnityTest]
        public IEnumerator AirAgent_WhenSpawningWithDisabledAgent_IsSuccessfullyEnabledAndMoves()
        {
            int agentCount = NavMesh.GetSettingsCount();
            int airAgentTypeID = 0;
            for (int i = 0; i < agentCount; i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (id != 0)
                {
                    airAgentTypeID = id;
                    break;
                }
            }
            
            if (airAgentTypeID == 0)
            {
                Assert.Fail("Air Agent settings not found in project config.");
                yield break;
            }
            
            GameObject agentObj = new GameObject("TestAirAgentDisabledSpawn");
            agentObj.transform.position = new Vector3(0, 0.1f, 0); // Spawns on ground
            NavMeshAgent agent = agentObj.AddComponent<NavMeshAgent>();
            agent.agentTypeID = airAgentTypeID;
            agent.speed = 10f;
            agent.acceleration = 100f;
            agent.enabled = false; // Starts disabled as if BaseBuilding couldn't find NavMesh
            
            yield return null;
            
            // Simulates AIController forcing the agent to enable and recover
            if (!agent.enabled)
            {
                agent.enabled = true;
            }
            
            if (!agent.isOnNavMesh)
            {
                NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(agentObj.transform.position, out NavMeshHit hit, 25f, filter))
                {
                    agent.enabled = false;
                    agentObj.transform.position = hit.position;
                    agent.enabled = true;
                    agent.Warp(hit.position);
                }
            }
            
            Assert.IsTrue(agent.isOnNavMesh, "Disabled agent should be on NavMesh after force-enable and recovery warp.");
            Assert.IsTrue(agent.enabled, "Agent should be enabled.");
            
            Vector3 startPos = agentObj.transform.position;
            Vector3 targetPosition = new Vector3(2f, startPos.y, 2f);
            
            bool setDestSuccess = agent.SetDestination(targetPosition);
            Assert.IsTrue(setDestSuccess, "Air agent SetDestination failed after force-enable recovery.");
            
            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }
            
            Assert.IsTrue(agent.hasPath, "Air agent has no path after force-enable recovery.");
            
            float distanceMoved = Vector3.Distance(startPos, agentObj.transform.position);
            Assert.Greater(distanceMoved, 0.05f, "Air agent GameObject did not move visually after force-enable recovery.");
            
            Object.DestroyImmediate(agentObj);
        }

        [UnityTest]
        public IEnumerator MiningDrone_WithCurrentGameSettings_StartsOnNavMesh()
        {
            UnitSO droneSO = Resources.Load<UnitSO>("Units/MiningDrone");
            Assert.IsNotNull(droneSO, "MiningDrone.asset not found in Resources/Units/.");
            Assert.IsNotNull(droneSO.Prefab, "MiningDrone prefab not assigned on UnitSO.");

            GameObject droneObj = Object.Instantiate(droneSO.Prefab);
            
            // Simulating spawn position calculation & warp logic using active project settings and baked NavMesh
            NavMeshAgent agent = droneObj.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent, "MiningDrone prefab has no NavMeshAgent component.");

            // Spawn at ground level (like Command Post)
            droneObj.transform.position = new Vector3(0, 0.1f, 0);

            // Wait 1 frame
            yield return null;

            // Retrieve the active filter matching the actual drone agentTypeID
            int agentTypeID = agent.agentTypeID;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeID, areaMask = NavMesh.AllAreas };
            
            // Query the NavMesh using the actual agentTypeID and project settings
            bool sampleSuccess = NavMesh.SamplePosition(droneObj.transform.position, out NavMeshHit hit, 25f, filter);
            Assert.IsTrue(sampleSuccess, $"NavMesh.SamplePosition failed for agentTypeID {agentTypeID} (Air Agent) at ground level. No valid Air NavMesh found within range.");

            // Apply warp
            agent.enabled = false;
            droneObj.transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);

            yield return null;

            Assert.IsTrue(agent.isOnNavMesh, "MiningDrone agent is not on NavMesh after executing spawn warp flow with active settings.");
            Assert.IsTrue(agent.enabled, "MiningDrone agent component is disabled after spawn warp flow.");

            // Verify actual movement to a destination on the NavMesh
            Vector3 startPos = droneObj.transform.position;
            Vector3 targetPosition = new Vector3(2f, startPos.y, 2f);
            
            bool setDestSuccess = agent.SetDestination(targetPosition);
            Assert.IsTrue(setDestSuccess, "MiningDrone SetDestination failed; destination was rejected.");

            for (int i = 0; i < 15; i++)
            {
                yield return null;
            }

            Assert.IsTrue(agent.hasPath, "MiningDrone agent has no path.");
            float distanceMoved = Vector3.Distance(startPos, droneObj.transform.position);
            Assert.Greater(distanceMoved, 0.05f, "MiningDrone did not move visually (transform remained stationary).");

            Object.DestroyImmediate(droneObj);
        }
    }
}
#endif
