#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Tests
{
    public class ColonyExpansionTests
    {
        private GameObject suppliesObj;
        private GameObject sectorManagerObj;
        private SectorManager sectorManager;
        private GameObject colonyExpansionManagerObj;
        private ColonyExpansionManager colonyExpansionManager;
        private GameObject baseBuildingObj;
        private BaseBuilding baseBuilding;

        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            
            // Clear active buildings to ensure a clean state
            BaseBuilding.ActiveBuildings.Clear();

            // Setup Supplies
            suppliesObj = new GameObject("Supplies");
            suppliesObj.AddComponent<Supplies>();
            Supplies.Biomass[Owner.Player1] = 1000;

            // Setup SectorManager
            sectorManagerObj = new GameObject("SectorManager");
            sectorManager = sectorManagerObj.AddComponent<SectorManager>();

            // Add mock sectors
            var sector0 = new SectorManager.Sector { Center = Vector3.zero, IsOccupied = false };
            var sector1 = new SectorManager.Sector { Center = new Vector3(5f, 0f, 0f), IsOccupied = false };
            sectorManager.Sectors.Add(sector0);
            sectorManager.Sectors.Add(sector1);

            // Setup ColonyExpansionManager
            colonyExpansionManagerObj = new GameObject("ColonyExpansionManager");
            colonyExpansionManager = colonyExpansionManagerObj.AddComponent<ColonyExpansionManager>();

            // Force load prefabs via reflection to guarantee initialization
            var ghostPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Buildings/Command Post/Command Post Ghost Variant.prefab");
            var realPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Buildings/Command Post/Command Post.prefab");
            
            var type = typeof(ColonyExpansionManager);
            type.GetField("ghostPrefab", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(colonyExpansionManager, ghostPrefab);
            type.GetField("realPrefab", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(colonyExpansionManager, realPrefab);

            // Setup Starting Completed Command Post
            baseBuildingObj = Object.Instantiate(realPrefab, Vector3.zero, Quaternion.identity);
            baseBuildingObj.name = "Command Post";
            baseBuilding = baseBuildingObj.GetComponent<BaseBuilding>();

            baseBuilding.Owner = Owner.Player1;
            baseBuilding.CompleteConstruction();
            baseBuilding.enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (suppliesObj != null) Object.DestroyImmediate(suppliesObj);
            if (sectorManagerObj != null) Object.DestroyImmediate(sectorManagerObj);
            if (colonyExpansionManagerObj != null) Object.DestroyImmediate(colonyExpansionManagerObj);
            if (baseBuildingObj != null) Object.DestroyImmediate(baseBuildingObj);

            // Destroy any spawned pipeline segments or command posts in the test
            var spawnedSegments = GameObject.FindGameObjectsWithTag("Untagged");
            foreach (var go in spawnedSegments)
            {
                if (go != null && (go.name.Contains("PipelineSegment") || go.name.Contains("Command Post")))
                {
                    Object.DestroyImmediate(go);
                }
            }

            BaseBuilding.ActiveBuildings.Clear();
        }

        [UnityTest]
        public IEnumerator ColonyExpansion_GeneratesNextCommandPost()
        {
            var sector1 = sectorManager.Sectors[1];
            Debug.Log("[Test] Starting expansion check for sector 1 at " + sector1.Center);

            Assert.IsFalse(sector1.IsOccupied, "Sector 1 should not be occupied initially.");
            Assert.IsFalse(colonyExpansionManager.IsExpandingToSector(sector1), "Should not be expanding to Sector 1 initially.");

            // Start the expansion
            Vector3 targetPosition = sector1.Center;
            colonyExpansionManager.StartExpansion(targetPosition, sector1);

            Assert.IsTrue(colonyExpansionManager.IsExpandingToSector(sector1), "Expansion to Sector 1 should be active.");
            Debug.Log("[Test] Expansion started. Waiting 6 seconds...");

            // Wait for growth and boot-up sequence (which is 5 seconds long)
            yield return new WaitForSeconds(6.0f);

            Debug.Log("[Test] Wait finished. Checking results...");

            // Verify that the expansion completed and was cleared
            Assert.IsFalse(colonyExpansionManager.IsExpandingToSector(sector1), "Expansion should be cleared after completion.");

            // Verify that the new completed Command Post is spawned at Sector 1
            BaseBuilding newCommandPost = null;
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b == baseBuilding) continue;
                if (b.Owner == Owner.Player1 &&
                    (b.name.Contains("Command", System.StringComparison.OrdinalIgnoreCase) ||
                     (b.BuildingSO != null && b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))))
                {
                    newCommandPost = b;
                    break;
                }
            }

            Assert.IsNotNull(newCommandPost, "A new Command Post should have been spawned.");
            Debug.Log("[Test] New Command Post found!");
            Assert.AreEqual(Owner.Player1, newCommandPost.Owner, "New Command Post should belong to Player 1.");
            Assert.AreEqual(BuildingProgress.BuildingState.Completed, newCommandPost.Progress.State, "New Command Post construction should be Completed.");
            Assert.IsTrue(Vector3.Distance(newCommandPost.transform.position, targetPosition) < 0.5f, "New Command Post should be spawned near Sector 1 center.");
            Debug.Log("[Test] Colony expansion SUCCESSFUL.");
        }

        [UnityTest]
        public IEnumerator ColonyExpansion_BuildsProbeDroneFirst()
        {
            var sector1 = sectorManager.Sectors[1];
            
            // Start the expansion
            colonyExpansionManager.StartExpansion(sector1.Center, sector1);

            // Wait for growth and boot-up sequence (5 seconds)
            yield return new WaitForSeconds(6.0f);

            // Verify that the new completed Command Post is spawned and has the probe first
            BaseBuilding newCommandPost = null;
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b == baseBuilding) continue;
                if (b.Owner == Owner.Player1 &&
                    (b.name.Contains("Command", System.StringComparison.OrdinalIgnoreCase) ||
                     (b.BuildingSO != null && b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))))
                {
                    newCommandPost = b;
                    break;
                }
            }

            Assert.IsNotNull(newCommandPost, "A new Command Post should have been spawned.");
            Assert.IsTrue(newCommandPost.IsFirstInQueueProbe(), "The first item in the queue must be the Probe drone.");
            Debug.Log("[Test] Verified: Probe drone is prioritized in the new Command Post.");
        }
    }
}
#endif
