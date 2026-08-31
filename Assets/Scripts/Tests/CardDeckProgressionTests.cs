#if UNITY_INCLUDE_TESTS
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
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("BIOMASS"));
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("COMMAND POST"));
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
            Assert.AreEqual(
                TerraformingGoalColors.ForMilestone(MilestoneType.Biomass),
                TerraformingGoalColors.ForGoal("BIOMASS"));
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
#endif
