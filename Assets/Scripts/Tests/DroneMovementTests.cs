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
    }
}
#endif
