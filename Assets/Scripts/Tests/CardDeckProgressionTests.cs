using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Tests
{
    public class CardDeckProgressionTests
    {
        private GameObject suppliesObj;

        [SetUp]
        public void SetUp()
        {
            suppliesObj = new GameObject("Supplies");
            suppliesObj.AddComponent<Supplies>();
        }

        [TearDown]
        public void TearDown()
        {
            if (suppliesObj != null) Object.DestroyImmediate(suppliesObj);
        }

        [Test]
        public void SectorWinCards_AreIdentifiedForDeckDoubling()
        {
            var ghg = UnlockCard("GHG Factory");
            var condenser = UnlockCard("Atmospheric Condenser");
            var aquifer = UnlockCard("Water Ice Aquifer");
            var solar = UnlockCard("Solar Panel");
            var caches = ScriptableObject.CreateInstance<ScoutingCardSO>();
            caches.scoutingType = ScoutingCardSO.ScoutingType.EmergencyCaches;

            Assert.IsNotNull(TerraformingGoalColors.GetSectorGoalForCard(ghg));
            Assert.IsNotNull(TerraformingGoalColors.GetSectorGoalForCard(condenser));
            Assert.IsNotNull(TerraformingGoalColors.GetSectorGoalForCard(aquifer));
            Assert.IsNotNull(TerraformingGoalColors.GetSectorGoalForCard(solar));
            Assert.IsNull(TerraformingGoalColors.GetSectorGoalForCard(caches));
        }

        [Test]
        public void GetCardGoal_ClassifiesSectorTerraformingBuildings()
        {
            Assert.AreEqual("TEMPERATURE", GoalForBuilding("GHG Factory"));
            Assert.AreEqual("TEMPERATURE", GoalForBuilding("Methanogenic Microbe Spreader"));
            Assert.AreEqual("ATMOSPHERE", GoalForBuilding("Atmospheric Condenser"));
            Assert.AreEqual("ATMOSPHERE", GoalForBuilding("Carbon Dioxide Import Laser"));
            Assert.AreEqual("WATER", GoalForBuilding("Water Ice Aquifer"));
            Assert.AreEqual("WATER", GoalForBuilding("Subglacial Water Extractor"));
            Assert.AreEqual("OXYGEN", GoalForBuilding("Oxygen Processor"));
            Assert.AreEqual("POWER", GoalForBuilding("Solar Panel"));
        }

        [Test]
        public void SectorResourceBudget_MeetsMinimumForTypicalSectorLayout()
        {
            var sector = new SectorManager.Sector();
            var minerals = ScriptableObject.CreateInstance<SupplySO>();
            SetSupplyMax(minerals, 250);

            for (int i = 0; i < 9; i++)
            {
                sector.Nodes.Add(new SectorNode(SectorNode.NodeType.Minerals, Vector3.zero, "", "Minerals"));
            }

            int baseYield = SectorResourceBudget.CalculateGatherableYield(sector, minerals, minerals, minerals, minerals);
            Assert.Less(baseYield, SectorResourceBudget.MinGatherableMaterialsPerSector);

            for (int i = 0; i < 8; i++)
            {
                sector.Nodes.Add(new SectorNode(SectorNode.NodeType.Minerals, Vector3.zero, "", "Minerals"));
            }

            int toppedUp = SectorResourceBudget.CalculateGatherableYield(sector, minerals, minerals, minerals, minerals);
            Assert.GreaterOrEqual(toppedUp, SectorResourceBudget.MinGatherableMaterialsPerSector);
        }

        [Test]
        public void WaterAndAtmosphereGoals_AreDistinctFromPowerSolar()
        {
            Assert.AreEqual("WATER", GoalForBuilding("Water Ice Aquifer"));
            Assert.AreEqual("POWER", GoalForBuilding("Solar Panel"));
            Assert.AreNotEqual(
                TerraformingGoalColors.ForGoal("WATER"),
                TerraformingGoalColors.ForGoal("POWER"));
        }

        private static void SetSupplyMax(SupplySO so, int max)
        {
            var field = typeof(SupplySO).GetField(
                "<MaxAmount>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(so, max);
        }

        [Test]
        public void IsCurrentSectorRoundComplete_FalseAtRoundStart()
        {
            var gmObj = new GameObject("GenerationManager");
            var gm = gmObj.AddComponent<GenerationManager>();
            var smObj = new GameObject("SectorManager");
            var sm = smObj.AddComponent<SectorManager>();
            sm.Sectors = new System.Collections.Generic.List<SectorManager.Sector>
            {
                new SectorManager.Sector { IsLocked = false, IsExplored = true },
            };
            sm.ActiveSector = sm.Sectors[0];

            Assert.IsFalse(gm.IsCurrentSectorRoundComplete());
            Assert.Less(gm.CalculateCurrentSectorProgress(out _), 1f);

            Object.DestroyImmediate(gmObj);
            Object.DestroyImmediate(smObj);
        }

        [Test]
        public void IsUnmetSectorGoal_TracksClimateShortfallsAtRoundStart()
        {
            Assert.IsTrue(GenerationManager.IsUnmetSectorGoal("TEMPERATURE"));
            Assert.IsTrue(GenerationManager.IsUnmetSectorGoal("ATMOSPHERE"));
            Assert.IsTrue(GenerationManager.IsUnmetSectorGoal("WATER"));
            Assert.IsFalse(GenerationManager.IsUnmetSectorGoal("EXPLORATION"));
        }

        [Test]
        public void IsSectorCompletionGoal_OnlyCoversSectorTerraforming()
        {
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("TEMPERATURE"));
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("ATMOSPHERE"));
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("WATER"));
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("COMMAND POST"));
            Assert.IsFalse(TerraformingGoalColors.IsSectorCompletionGoal("BIOMASS"));
            Assert.IsFalse(TerraformingGoalColors.IsSectorCompletionGoal("MATERIALS"));
            Assert.IsFalse(TerraformingGoalColors.IsSectorCompletionGoal("EXPLORATION"));
            Assert.IsFalse(TerraformingGoalColors.IsSectorCompletionGoal("MINING"));

            var ghg = UnlockCard("GHG Factory");
            var caches = ScriptableObject.CreateInstance<ScoutingCardSO>();
            caches.scoutingType = ScoutingCardSO.ScoutingType.EmergencyCaches;

            Assert.AreEqual("TEMPERATURE", TerraformingGoalColors.GetSectorGoalForCard(ghg));
            Assert.IsNull(TerraformingGoalColors.GetSectorGoalForCard(caches));
        }

        [Test]
        public void TerraformingGoalColors_MatchDistinctSectorGoals()
        {
            Assert.AreNotEqual(
                TerraformingGoalColors.ForGoal("TEMPERATURE"),
                TerraformingGoalColors.ForGoal("WATER"));
            Assert.AreNotEqual(
                TerraformingGoalColors.ForGoal("ATMOSPHERE"),
                TerraformingGoalColors.ForGoal("WATER"));
            Assert.AreEqual(
                TerraformingGoalColors.ForMilestone(MilestoneType.Temperature),
                TerraformingGoalColors.ForGoal("TEMPERATURE"));
            Assert.AreEqual(TerraformingGoalColors.Neutral, TerraformingGoalColors.ForGoal("BIOMASS"));
            Assert.AreEqual(TerraformingGoalColors.Neutral, TerraformingGoalColors.ForGoal("MATERIALS"));
        }

        [Test]
        public void CanUnlockNextMapSector_RequiresGenerationToAdvance()
        {
            var gmObj = new GameObject("GenerationManager");
            var gm = gmObj.AddComponent<GenerationManager>();
            var smObj = new GameObject("SectorManager");
            var sm = smObj.AddComponent<SectorManager>();
            sm.Sectors = new System.Collections.Generic.List<SectorManager.Sector>
            {
                new SectorManager.Sector { IsLocked = false },
                new SectorManager.Sector { IsLocked = true },
            };

            Assert.IsFalse(GenerationManager.CanUnlockNextMapSector());

            var genField = typeof(GenerationManager).GetField(
                "<CurrentGeneration>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            genField?.SetValue(gm, 2);

            Assert.IsTrue(GenerationManager.CanUnlockNextMapSector());

            Object.DestroyImmediate(gmObj);
            Object.DestroyImmediate(smObj);
        }

        [Test]
        public void Gen2Progress_DoesNotCreditGen1TerraformingAfterBaselines()
        {
            var gmObj = new GameObject("GenerationManager");
            var gm = gmObj.AddComponent<GenerationManager>();
            var smObj = new GameObject("SectorManager");
            var sm = smObj.AddComponent<SectorManager>();
            sm.Sectors = new System.Collections.Generic.List<SectorManager.Sector>
            {
                new SectorManager.Sector { IsLocked = false, IsExplored = true, IsOccupied = true, CompletedGenerationRound = 1, TerraformingCompletionPercent = 1f },
                new SectorManager.Sector { IsLocked = false, IsExplored = true, IsOccupied = true },
            };
            sm.ActiveSector = sm.Sectors[1];

            typeof(GenerationManager).GetMethod(
                    "InitializeDefaultMilestones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(gm, null);

            // Overshoot gen-1 absolute floors — still must not complete gen 2.
            Supplies.UpdateTemperature(Owner.Player1, 20f);
            Supplies.UpdateAtmosphere(Owner.Player1, 2f);
            Supplies.UpdateWater(Owner.Player1, 50f);

            typeof(GenerationManager).GetField(
                    "<CurrentGeneration>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(gm, 2);

            typeof(GenerationManager).GetMethod(
                    "RecordBaselines",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(gm, null);

            float progress = gm.CalculateCurrentSectorProgress(out string bottleneck);
            Assert.Less(progress, 0.05f, $"Gen 2 should start at ~0% after prior gains; bottleneck={bottleneck}");
            Assert.IsFalse(gm.IsCurrentSectorRoundComplete());
            Assert.IsTrue(GenerationManager.IsUnmetSectorGoal("TEMPERATURE"));
            Assert.IsTrue(GenerationManager.IsUnmetSectorGoal("ATMOSPHERE"));
            Assert.IsTrue(GenerationManager.IsUnmetSectorGoal("WATER"));

            Object.DestroyImmediate(gmObj);
            Object.DestroyImmediate(smObj);
        }

        [Test]
        public void Gen2Progress_RequiresFreshClimateDeltaEvenIfPlanetAlreadyWarm()
        {
            var gmObj = new GameObject("GenerationManager");
            var gm = gmObj.AddComponent<GenerationManager>();
            var smObj = new GameObject("SectorManager");
            var sm = smObj.AddComponent<SectorManager>();
            sm.Sectors = new System.Collections.Generic.List<SectorManager.Sector>
            {
                new SectorManager.Sector { IsLocked = false, IsExplored = true, IsOccupied = true },
                new SectorManager.Sector { IsLocked = false, IsExplored = true, IsOccupied = true },
            };
            sm.ActiveSector = sm.Sectors[1];

            typeof(GenerationManager).GetMethod(
                    "InitializeDefaultMilestones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(gm, null);

            typeof(GenerationManager).GetField(
                    "<CurrentGeneration>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(gm, 2);

            Supplies.UpdateTemperature(Owner.Player1, 0f);
            Supplies.UpdateAtmosphere(Owner.Player1, 1f);
            Supplies.UpdateWater(Owner.Player1, 40f);
            typeof(GenerationManager).GetMethod(
                    "RecordBaselines",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(gm, null);

            // Tiny bump — far less than a full sector delta.
            Supplies.UpdateTemperature(Owner.Player1, 1f);
            float progress = gm.CalculateCurrentSectorProgress(out _);
            Assert.Greater(progress, 0f);
            Assert.Less(progress, 0.2f);
            Assert.IsFalse(gm.IsCurrentSectorRoundComplete());

            Object.DestroyImmediate(gmObj);
            Object.DestroyImmediate(smObj);
        }

        [Test]
        public void TerraformingCard_RelaxesAllMinClimateGatesWhenWaterGoalUnmet()
        {
            Supplies.Materials[Owner.Player1] = 9999;

            var card = ScriptableObject.CreateInstance<TerraformingCardSO>();
            card.buildingToUnlock = ScriptableObject.CreateInstance<BuildingSO>();
            card.buildingToUnlock.Name = "Water Ice Aquifer";
            card.buildingToUnlock.Prefab = new GameObject("Water Ice Aquifer Prefab");
            card.minTemperature = -20f;
            card.maxTemperature = 9999f;

            Assert.IsTrue(card.PassesClimateRequirements());
            Assert.IsTrue(card.IsGateMet());
        }

        [Test]
        public void TerraformingCard_RelaxesMinClimateGateWhenGoalUnmet()
        {
            Supplies.Materials[Owner.Player1] = 9999;

            var card = ScriptableObject.CreateInstance<TerraformingCardSO>();
            card.buildingToUnlock = ScriptableObject.CreateInstance<BuildingSO>();
            card.buildingToUnlock.Name = "Atmospheric Condenser";
            card.buildingToUnlock.Prefab = new GameObject("Atmospheric Condenser Prefab");
            card.minAtmosphere = 0.05f;
            card.maxAtmosphere = 9999f;

            Assert.IsTrue(card.IsGateMet());
        }

        [Test]
        public void Integrity_StaysFull_UntilColonyIntegrityStarts()
        {
            Assert.IsFalse(Supplies.ColonyIntegrityActive);
            Assert.AreEqual(100f, Supplies.CalculateIntegrity(Owner.Player1), 0.01f);
        }

        private static string GoalForBuilding(string buildingName)
        {
            return UnlockCard(buildingName).GetCardGoal();
        }

        private static UnlockBuildingCardSO UnlockCard(string buildingName)
        {
            var card = ScriptableObject.CreateInstance<UnlockBuildingCardSO>();
            card.buildingToUnlock = ScriptableObject.CreateInstance<BuildingSO>();
            card.buildingToUnlock.Name = buildingName;
            return card;
        }
    }
}
