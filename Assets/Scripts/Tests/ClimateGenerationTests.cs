using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Tests
{
    /// <summary>
    /// Atmosphere climate generation tests.
    ///
    /// EditMode (fast, synthetic — does NOT start the game):
    ///   unity command run_tests --mode editmode --filter ClimateGenerationTests
    ///
    /// Play Mode (starts/uses a live Play session — what you usually want):
    ///   unity command editor_play
    ///   unity command run_tests --mode playmode --filter ClimateGenerationTests.PlayMode
    /// Or CLI without the test runner:
    ///   unity command eval "return GameDevTV.RTS.Player.ClimateGenerationAutomation.TryVerifyAtmosphereRises();" --json
    /// </summary>
    public class ClimateGenerationTests
    {
        private BuildingConfigSO config;
        private BuildingSO buildingSo;
        private GameObject buildingGo;
        private GameObject sectorManagerGo;

        [SetUp]
        public void SetUp()
        {
            if (Application.isPlaying) return;

            foreach (var existing in Object.FindObjectsByType<SectorManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            Supplies.ResetAllSupplies(Owner.Player1);
            Supplies.UpdateAtmosphere(Owner.Player1, 0.01f);
        }

        [TearDown]
        public void TearDown()
        {
            if (Application.isPlaying) return;

            if (buildingGo != null) Object.DestroyImmediate(buildingGo);
            if (sectorManagerGo != null) Object.DestroyImmediate(sectorManagerGo);
            if (buildingSo != null) Object.DestroyImmediate(buildingSo);
            if (config != null) Object.DestroyImmediate(config);
        }

        [Test]
        public void TickClimateGeneration_IncreasesAtmosphere_WhenAtmosphereBuildingCompleted()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode climate tests require the Editor not to be in Play Mode.");
            }

            config = ScriptableObject.CreateInstance<BuildingConfigSO>();
            config.AtmosphereGeneration = 0.1f;
            config.PowerUpkeep = 0f;

            buildingSo = ScriptableObject.CreateInstance<BuildingSO>();
            buildingSo.Name = "Atmospheric Condenser";
            buildingSo.BuildingConfig = config;
            buildingSo.Health = 100;

            buildingGo = new GameObject("Test Atmospheric Condenser");
            buildingGo.SetActive(false);
            var building = buildingGo.AddComponent<BaseBuilding>();
            building.Owner = Owner.Player1;
            building.BindBuildingDefinition(buildingSo);
            building.CompleteConstruction();
            buildingGo.SetActive(true);

            Assert.AreEqual(
                BuildingProgress.BuildingState.Completed,
                building.Progress.State,
                "Atmosphere building must be completed to tick climate.");
            Assert.IsTrue(building.IsOperating, "Atmosphere building with 0 power upkeep should be operating.");

            float before = Supplies.Atmosphere[Owner.Player1];
            building.TickClimateGeneration(1f);
            float after = Supplies.Atmosphere[Owner.Player1];

            Assert.Greater(
                after,
                before,
                $"Atmosphere should rise after 1s of climate tick (before={before:F4}, after={after:F4}).");
            Assert.AreEqual(
                before + config.AtmosphereGeneration,
                after,
                0.0001f,
                "Atmosphere delta should match AtmosphereGeneration * dt.");
        }

        [Test]
        public void TickClimateGeneration_IncreasesAtmosphere_WhenBuildingInActiveSectorAndPowered()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode climate tests require the Editor not to be in Play Mode.");
            }

            sectorManagerGo = new GameObject("SectorManager");
            var sm = sectorManagerGo.AddComponent<SectorManager>();
            typeof(SectorManager).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(sm, null);

            var sector = new SectorManager.Sector
            {
                Center = Vector3.zero,
                IsLocked = false,
                IsExplored = true,
                IsOccupied = true
            };
            sm.Sectors = new System.Collections.Generic.List<SectorManager.Sector> { sector };
            sm.ActiveSector = sector;

            config = ScriptableObject.CreateInstance<BuildingConfigSO>();
            config.AtmosphereGeneration = 0.05f;
            config.PowerUpkeep = 3f;

            buildingSo = ScriptableObject.CreateInstance<BuildingSO>();
            buildingSo.Name = "Carbon Dioxide Import Laser";
            buildingSo.BuildingConfig = config;
            buildingSo.Health = 100;

            buildingGo = new GameObject("Test CO2 Import Laser");
            buildingGo.transform.position = Vector3.zero;
            buildingGo.SetActive(false);
            var building = buildingGo.AddComponent<BaseBuilding>();
            var power = buildingGo.AddComponent<PowerNode>();
            building.Owner = Owner.Player1;
            building.BindBuildingDefinition(buildingSo);

            var site = new BuildingSiteSlot(BuildingSiteKind.PairedBuilding, Vector3.zero, sector);
            site.SetOccupied(building);
            sector.BuildingSites.Add(site);

            building.CompleteConstruction();
            power.IsGridPowered = true;
            buildingGo.SetActive(true);

            Assert.IsTrue(building.IsOperating, "Powered atmosphere building should be operating.");
            Assert.IsTrue(
                sm.DoesBuildingCountForActiveClimate(building),
                "MVP: any completed climate building must count (whole board).");

            float before = Supplies.Atmosphere[Owner.Player1];
            building.TickClimateGeneration(2f);
            float after = Supplies.Atmosphere[Owner.Player1];

            Assert.Greater(after, before, "Powered active-sector atmosphere building should raise Atmos.");
            Assert.AreEqual(before + 0.1f, after, 0.0001f, "Expected 0.05 atm/s * 2s.");
        }

        [Test]
        public void IsMineBuilding_DoesNotTreatCarbonDioxideImportLaserAsMine()
        {
            if (Application.isPlaying)
            {
                Assert.Ignore("EditMode-only assertion.");
            }

            var so = ScriptableObject.CreateInstance<BuildingSO>();
            so.Name = "Carbon Dioxide Import Laser";
            Assert.IsFalse(
                BuildingSiteRegistry.IsMineBuilding(so),
                "CO2 Import Laser must build on climate/paired pads, not mine pads.");
            Object.DestroyImmediate(so);
        }

        /// <summary>
        /// Live game: wait for bootstrap, place solar + atmosphere building, confirm Atmos rises
        /// over real Play Mode time (ClimateGenerationTicker).
        /// </summary>
        [UnityTest]
        public IEnumerator PlayMode_AtmosphereBuilding_RaisesAtmosphereOverTime()
        {
            if (!Application.isPlaying)
            {
                Assert.Ignore(
                    "Requires Play Mode. Start with: unity command editor_play\n" +
                    "Then: unity command run_tests --mode playmode --filter ClimateGenerationTests.PlayMode\n" +
                    "Or: unity command eval \"return GameDevTV.RTS.Player.ClimateGenerationAutomation.TryVerifyAtmosphereRises();\" --json");
            }

            float timeout = Time.realtimeSinceStartup + 45f;
            while ((GenerationManager.Instance == null
                    || SectorManager.Instance == null
                    || SectorManager.Instance.ActiveSector == null
                    || !BuildingSiteRegistry.HasRegisteredSites())
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsNotNull(GenerationManager.Instance, "GenerationManager missing after wait");
            Assert.IsNotNull(SectorManager.Instance?.ActiveSector, "ActiveSector missing after wait");
            Assert.IsTrue(BuildingSiteRegistry.HasRegisteredSites(), "No reserved building sites after planet gen");

            string result = ClimateGenerationAutomation.TryVerifyAtmosphereRises(simulateSeconds: 3f);
            Debug.Log(result);

            Assert.IsTrue(
                result.Contains("RESULT: PASS"),
                "Expected atmosphere to rise after placing a powered atmosphere building.\n" + result);
        }
    }
}
