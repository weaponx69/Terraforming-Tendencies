#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Tests
{
    /// <summary>
    /// Sector win checks. Prefer running on the connected Editor:
    ///   unity command run_tests --mode playmode --filter SectorWinAutomationTests --json
    /// Or drive live Play Mode without the test runner:
    ///   unity command editor_play
    ///   unity command eval "return GameDevTV.RTS.Player.SectorWinAutomation.TryWinCurrentSector();" --json
    /// Do not use <c>unity test</c> while an Editor is already open (OOM risk).
    /// </summary>
    public class SectorWinAutomationTests
    {
        [Test]
        public void SectorWinCards_AreDoubledInDrawPileLogic()
        {
            // Contract: finishing goals are identified so CardDeckController can duplicate them.
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("ATMOSPHERE"));
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("WATER"));
            Assert.IsTrue(TerraformingGoalColors.IsSectorCompletionGoal("TEMPERATURE"));
            Assert.IsFalse(TerraformingGoalColors.IsSectorCompletionGoal("MATERIALS"));
            Assert.IsFalse(TerraformingGoalColors.IsSectorCompletionGoal("EXPLORATION"));
        }

        [Test]
        public void AtmosphereAndWater_HaveDistinctColors()
        {
            Assert.AreNotEqual(
                TerraformingGoalColors.ForGoal("ATMOSPHERE"),
                TerraformingGoalColors.ForGoal("WATER"));
        }

        [UnityTest]
        public IEnumerator PlayMode_MeetGoals_EndsCurrentSectorRound()
        {
            if (!Application.isPlaying)
            {
                Assert.Ignore("Requires Play Mode (start with unity command editor_play, then run_tests playmode).");
            }

            // Wait for planet / generation bootstrap.
            float timeout = Time.realtimeSinceStartup + 30f;
            while ((GenerationManager.Instance == null
                    || SectorManager.Instance == null
                    || SectorManager.Instance.ActiveSector == null)
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.IsNotNull(GenerationManager.Instance, "GenerationManager missing after wait");
            Assert.IsNotNull(SectorManager.Instance?.ActiveSector, "ActiveSector missing after wait");

            var gm = GenerationManager.Instance;
            if (gm.IsBetweenRounds || gm.IsExpansionPhase)
            {
                Assert.Ignore("Not in an active sector terraforming round.");
            }

            string before = SectorWinAutomation.Report();
            Debug.Log(before);

            string result = SectorWinAutomation.TryWinCurrentSector();
            Debug.Log(result);

            Assert.IsTrue(result.Contains("RESULT: PASS") || result.Contains("RESULT: ALREADY_BETWEEN_ROUNDS"),
                "Expected sector round to end after meeting goals.\n" + result);

            Assert.IsTrue(GenerationManager.Instance.IsBetweenRounds,
                "GenerationManager should be between rounds after a successful sector win.");
        }
    }
}
#endif
