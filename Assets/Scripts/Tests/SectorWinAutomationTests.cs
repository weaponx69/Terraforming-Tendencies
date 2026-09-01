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
        public void TerraformingCaps_AllowCurrentGenerationTargetsWithoutOccupiedSectors()
        {
            var gmObj = new GameObject("GenerationManager");
            var gm = gmObj.AddComponent<GenerationManager>();
            var smObj = new GameObject("SectorManager");
            var sm = smObj.AddComponent<SectorManager>();
            sm.Sectors = new System.Collections.Generic.List<SectorManager.Sector>
            {
                new SectorManager.Sector { IsLocked = false, IsExplored = true, IsOccupied = false },
                new SectorManager.Sector { IsLocked = true, IsExplored = false, IsOccupied = false },
                new SectorManager.Sector { IsLocked = true, IsExplored = false, IsOccupied = false },
                new SectorManager.Sector { IsLocked = true, IsExplored = false, IsOccupied = false },
                new SectorManager.Sector { IsLocked = true, IsExplored = false, IsOccupied = false },
            };
            sm.ActiveSector = sm.Sectors[0];

            // Sync milestone fields used by caps.
            gm.CalculateCurrentSectorProgress(out _);

            Supplies.GetTerraformingCaps(
                out float maxAtmos, out float maxWater, out _, out float maxBio, out float maxTemp);

            float targetAtmos = gm.GetTargetAtmosphere(gm.CurrentGeneration);
            float targetWater = gm.GetTargetWater(gm.CurrentGeneration);
            float targetTemp = gm.GetTargetTemperature(gm.CurrentGeneration);

            Assert.GreaterOrEqual(maxAtmos, targetAtmos, "Atmosphere cap must allow gen target with 0 occupied");
            Assert.GreaterOrEqual(maxWater, targetWater, "Water cap must allow gen target with 0 occupied");
            Assert.GreaterOrEqual(maxTemp, targetTemp, "Temperature ceiling must allow gen target");
            Assert.GreaterOrEqual(maxBio, gm.CurrentMilestoneTarget - 0.001f);

            Object.DestroyImmediate(gmObj);
            Object.DestroyImmediate(smObj);
        }

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
        public void HasUnclaimedUnlockedSector_DetectsEmptyCommandPostPad()
        {
            foreach (var existing in Object.FindObjectsByType<SectorManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var smObj = new GameObject("SectorManager");
            var sm = smObj.AddComponent<SectorManager>();
            var sector = new SectorManager.Sector { IsLocked = false, IsExplored = true, IsOccupied = false };
            var cpSite = new BuildingSiteSlot(BuildingSiteKind.CommandPost, Vector3.zero, sector);
            sector.BuildingSites.Add(cpSite);
            sm.Sectors = new System.Collections.Generic.List<SectorManager.Sector> { sector };

            Assert.AreSame(sm, SectorManager.Instance, "Test SectorManager must be the active singleton.");
            Assert.IsTrue(GameDevTV.RTS.Utilities.SectorColonization.HasUnclaimedUnlockedSector());

            Object.DestroyImmediate(smObj);
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
