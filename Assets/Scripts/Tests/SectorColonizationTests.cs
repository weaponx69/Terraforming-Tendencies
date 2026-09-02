using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Tests
{
    /// <summary>
    /// Sector unlock should reveal pads and auto-place a Command Post (SectorColonization).
    /// Run: unity command run_tests --mode editmode --filter SectorColonizationTests
    /// </summary>
    public class SectorColonizationTests
    {
        [SetUp]
        public void SetUp()
        {
            foreach (var existing in Object.FindObjectsByType<SectorManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static SectorManager CreateTestSectorManager()
        {
            var smObj = new GameObject("SectorManager");
            var sm = smObj.AddComponent<SectorManager>();
            // EditMode tests do not run Awake automatically; register the singleton explicitly.
            typeof(SectorManager).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(sm, null);
            return sm;
        }

        [Test]
        public void TryAutoPlaceCommandPost_SpawnsCompletedCommandPost()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode colonization tests require the Editor not to be in Play Mode.");
            }

            BlueprintDraftManager.Reset();
            BuildingSO commandPostSo = BlueprintDraftManager.GetBuildingSOByName("Command Post");
            if (commandPostSo == null || commandPostSo.Prefab == null)
            {
                Assert.Inconclusive("Command Post BuildingSO or Prefab missing from Resources.");
            }

            var sm = CreateTestSectorManager();
            var sector = new SectorManager.Sector
            {
                Center = new Vector3(80f, 0f, 80f),
                IsLocked = false,
                IsExplored = true,
                IsOccupied = false
            };
            var cpSite = new BuildingSiteSlot(BuildingSiteKind.CommandPost, sector.Center, sector);
            sector.BuildingSites.Add(cpSite);
            sm.Sectors = new List<SectorManager.Sector> { sector };

            try
            {
                bool placed = SectorColonization.TryAutoPlaceCommandPost(sector, Owner.Player1, out string reason);
                Assert.IsTrue(placed, reason ?? "TryAutoPlaceCommandPost returned false with no reason.");
                Assert.IsTrue(cpSite.IsOccupied);
                Assert.AreEqual(BuildingProgress.BuildingState.Completed, cpSite.OccupyingBuilding.Progress.State);
            }
            finally
            {
                DestroyAllSpawnedBuildings();
                Object.DestroyImmediate(sm.gameObject);
            }
        }

        [Test]
        public void OnFirstNodeExploredInSector_AutoPlacesCommandPost()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode colonization tests require the Editor not to be in Play Mode.");
            }

            BlueprintDraftManager.Reset();
            BuildingSO commandPostSo = BlueprintDraftManager.GetBuildingSOByName("Command Post");
            if (commandPostSo == null || commandPostSo.Prefab == null)
            {
                Assert.Inconclusive("Command Post BuildingSO or Prefab missing from Resources.");
            }

            var sm = CreateTestSectorManager();

            var startingSector = new SectorManager.Sector
            {
                Center = Vector3.zero,
                IsLocked = false,
                IsExplored = true,
                IsOccupied = true
            };
            var newSector = new SectorManager.Sector
            {
                Center = new Vector3(100f, 0f, 100f),
                IsLocked = true,
                IsExplored = false,
                IsOccupied = false
            };
            var cpSite = new BuildingSiteSlot(BuildingSiteKind.CommandPost, newSector.Center, newSector);
            newSector.BuildingSites.Add(cpSite);

            sm.Sectors = new List<SectorManager.Sector> { startingSector, newSector };
            sm.ActiveSector = startingSector;

            try
            {
                Assert.IsTrue(newSector.IsLocked);
                Assert.IsFalse(cpSite.IsOccupied);

                sm.OnFirstNodeExploredInSector(1, Owner.Player1);

                Assert.IsFalse(newSector.IsLocked, "Sector should be unlocked.");
                Assert.IsTrue(newSector.IsExplored, "Sector should be marked explored.");
                Assert.IsTrue(cpSite.IsOccupied,
                    "Command Post pad should be occupied after sector unlock colonization.");
                Assert.IsNotNull(cpSite.OccupyingBuilding, "Pad should reference the spawned building.");
                Assert.IsTrue(newSector.IsOccupied, "Sector should be marked occupied after auto-claim.");

                var building = cpSite.OccupyingBuilding;
                Assert.IsTrue(
                    building.BuildingSO != null &&
                    building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase),
                    "Spawned building should be a Command Post.");
                Assert.AreEqual(
                    BuildingProgress.BuildingState.Completed,
                    building.Progress.State,
                    "Auto-placed Command Post should be completed immediately.");
                Assert.IsFalse(SectorColonization.HasUnclaimedUnlockedSector(),
                    "No unlocked sector should remain unclaimed after auto-placement.");
            }
            finally
            {
                DestroyAllSpawnedBuildings();
                Object.DestroyImmediate(sm.gameObject);
            }
        }

        [Test]
        public void PrepareNewlyUnlockedSector_PlacesCommandPostOnUnlockedSector()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode colonization tests require the Editor not to be in Play Mode.");
            }

            BlueprintDraftManager.Reset();
            if (BlueprintDraftManager.GetBuildingSOByName("Command Post")?.Prefab == null)
            {
                Assert.Inconclusive("Command Post BuildingSO or Prefab missing from Resources.");
            }

            var sm = CreateTestSectorManager();
            var sector = new SectorManager.Sector
            {
                Center = new Vector3(50f, 0f, 50f),
                IsLocked = false,
                IsExplored = true,
                IsOccupied = false
            };
            var cpSite = new BuildingSiteSlot(BuildingSiteKind.CommandPost, sector.Center, sector);
            sector.BuildingSites.Add(cpSite);
            sm.Sectors = new List<SectorManager.Sector> { sector };

            try
            {
                SectorColonization.PrepareNewlyUnlockedSector(sector, Owner.Player1, 0);

                Assert.IsTrue(cpSite.IsOccupied, "Command Post pad should be occupied after PrepareNewlyUnlockedSector.");
                Assert.IsNotNull(cpSite.OccupyingBuilding);
                Assert.IsTrue(sector.IsOccupied);
            }
            finally
            {
                DestroyAllSpawnedBuildings();
                Object.DestroyImmediate(sm.gameObject);
            }
        }

        [Test]
        public void GetClosestLockedSectorIndex_PicksNearestNeighbor()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode colonization tests require the Editor not to be in Play Mode.");
            }

            foreach (var existing in Object.FindObjectsByType<SectorManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var sm = CreateTestSectorManager();
            var origin = new SectorManager.Sector
            {
                Center = Vector3.zero,
                IsLocked = false,
                IsExplored = true,
                IsOccupied = true
            };
            var nearLocked = new SectorManager.Sector
            {
                Center = new Vector3(10f, 0f, 10f),
                IsLocked = true
            };
            var farLocked = new SectorManager.Sector
            {
                Center = new Vector3(100f, 0f, 100f),
                IsLocked = true
            };
            sm.Sectors = new List<SectorManager.Sector> { origin, nearLocked, farLocked };
            sm.ActiveSector = origin;

            try
            {
                Assert.AreEqual(1, sm.GetClosestLockedSectorIndex(Vector3.zero));
                Assert.AreEqual(1, sm.GetClosestSectorNeedingCommandPostIndex(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(sm.gameObject);
            }
        }

        [Test]
        public void TryColonizeClosestSectorNeedingCommandPost_UnlocksNearestAndPlacesCommandPost()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode colonization tests require the Editor not to be in Play Mode.");
            }

            BlueprintDraftManager.Reset();
            if (BlueprintDraftManager.GetBuildingSOByName("Command Post")?.Prefab == null)
            {
                Assert.Inconclusive("Command Post BuildingSO or Prefab missing from Resources.");
            }

            foreach (var existing in Object.FindObjectsByType<SectorManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var sm = CreateTestSectorManager();
            var origin = new SectorManager.Sector
            {
                Center = Vector3.zero,
                IsLocked = false,
                IsExplored = true,
                IsOccupied = true
            };
            var nearLocked = new SectorManager.Sector
            {
                Center = new Vector3(10f, 0f, 10f),
                IsLocked = true,
                IsExplored = false,
                IsOccupied = false
            };
            var farLocked = new SectorManager.Sector
            {
                Center = new Vector3(100f, 0f, 100f),
                IsLocked = true,
                IsExplored = false,
                IsOccupied = false
            };
            var nearCpSite = new BuildingSiteSlot(BuildingSiteKind.CommandPost, nearLocked.Center, nearLocked);
            nearLocked.BuildingSites.Add(nearCpSite);
            var farCpSite = new BuildingSiteSlot(BuildingSiteKind.CommandPost, farLocked.Center, farLocked);
            farLocked.BuildingSites.Add(farCpSite);

            sm.Sectors = new List<SectorManager.Sector> { origin, nearLocked, farLocked };
            sm.ActiveSector = origin;

            try
            {
                Assert.AreSame(sm, SectorManager.Instance, "Test SectorManager must be the active singleton.");
                Assert.AreEqual(1, sm.GetClosestSectorNeedingCommandPostIndex(Vector3.zero));
                Assert.IsTrue(nearLocked.IsLocked);
                Assert.IsTrue(farLocked.IsLocked);

                bool colonized = SectorColonization.TryColonizeClosestSectorNeedingCommandPost(Owner.Player1);

                Assert.IsTrue(colonized, "Closest locked sector should be colonized.");
                Assert.IsFalse(nearLocked.IsLocked, "Nearest sector should be unlocked.");
                Assert.IsTrue(farLocked.IsLocked, "Far sector should remain locked.");
                Assert.IsTrue(nearCpSite.IsOccupied, "Nearest sector should have auto-placed Command Post.");
                Assert.IsFalse(farCpSite.IsOccupied, "Far sector should not be colonized yet.");
                Assert.AreSame(nearLocked, sm.ActiveSector);
            }
            finally
            {
                DestroyAllSpawnedBuildings();
                Object.DestroyImmediate(sm.gameObject);
            }
        }

        private static int CountCompletedCommandPosts()
        {
            int count = 0;
            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || building.BuildingSO == null) continue;
                if (!building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (building.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                count++;
            }

            return count;
        }

        private static void DestroyAllSpawnedBuildings()
        {
            var snapshot = new List<BaseBuilding>(BaseBuilding.ActiveBuildings);
            foreach (var building in snapshot)
            {
                if (building != null)
                {
                    Object.DestroyImmediate(building.gameObject);
                }
            }
        }
    }
}
