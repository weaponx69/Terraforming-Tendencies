#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI;

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
        public void TerraformingGoalColors_MatchDistinctGoals()
        {
            Assert.AreNotEqual(
                TerraformingGoalColors.ForGoal("TEMPERATURE"),
                TerraformingGoalColors.ForGoal("WATER"));
            Assert.AreNotEqual(
                TerraformingGoalColors.ForGoal("ATMOSPHERE"),
                TerraformingGoalColors.ForGoal("OXYGEN"));
            Assert.AreEqual(
                TerraformingGoalColors.ForMilestone(MilestoneType.Biomass),
                TerraformingGoalColors.ForGoal("BIOMASS"));
            Assert.AreEqual("#FF732E", TerraformingGoalColors.ToHex(TerraformingGoalColors.Temperature));
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
