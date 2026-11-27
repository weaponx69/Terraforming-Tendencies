using System.Collections.Generic;
using NUnit.Framework;
using GameDevTV.RTS.MainGame;
using UnityEngine;

namespace GameDevTV.RTS.Tests
{
    public class MainGameColonyTests
    {
        [Test]
        public void BuildingInfo_HabitatHasTenBedsAndOneBuildWeek()
        {
            var info = MainGameData.Get(MainGameBuildingId.Habitat);
            Assert.AreEqual(10, info.Beds);
            Assert.AreEqual(1, info.BuildWeeks);
        }

        [Test]
        public void LandingOxygen_IsFifty()
        {
            Assert.AreEqual(50, MainGameData.LandingOxygen);
        }

        [Test]
        public void O2Need_StartColonists_IsSix()
        {
            var c = MainGameColony.CreateLanding();
            Assert.AreEqual(6, c.O2Need());
        }

        [Test]
        public void Energy_CampFillsPool_OrbitalSpends()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            Assert.GreaterOrEqual(c.Energy, MainGameData.OrbitalScanEnergyCost);
            Assert.IsTrue(c.TrySpendEnergy(MainGameData.OrbitalScanEnergyCost));
            int after = c.Energy;
            Assert.IsFalse(c.TrySpendEnergy(c.Energy + 1));
            Assert.AreEqual(after, c.Energy);
        }

        [Test]
        public void Energy_RefillsFromCampOnEndWeek_StormCutsCamp()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            while (c.TrySpendEnergy(1)) { }
            Assert.AreEqual(0, c.Energy);
            c.EndWeek();
            Assert.AreEqual(MainGameData.CampEnergyGeneration, c.Energy);
            c.DebugForceStorm(true);
            while (c.TrySpendEnergy(1)) { }
            c.EndWeek();
            Assert.AreEqual(0, c.Energy); // camp dark, no generators
        }

        [Test]
        public void EndWeek_PoweredOxygen_AddsNetOxygen()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugPlaceComplete(2, MainGameBuildingId.Aquifer);
            c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
            c.DebugPlaceComplete(4, MainGameBuildingId.Solar);
            c.SetDistanceFn((a, b) =>
            {
                if ((a == 2 && b == 1) || (a == 1 && b == 2)) return 1;
                if ((a == 3 && b == 4) || (a == 4 && b == 3)) return 1;
                return 99;
            });
            c.Board.At(1).Feature = MainGameFeature.Ice;
            c.Board.At(1).DepositLeft = 100;
            c.Board.At(1).Revealed = true;
            int before = c.Oxygen;
            c.EndWeek();
            Assert.Greater(c.Oxygen, before - 6);
        }

        [Test]
        public void TotalBeds_TwoHabitats_IsTwenty()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugPlaceComplete(5, MainGameBuildingId.Habitat);
            c.DebugPlaceComplete(6, MainGameBuildingId.Habitat);
            Assert.AreEqual(20, c.TotalBeds());
        }

        [Test]
        public void Preview_Aquifer_OnIceDepositHex_Refuses()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            c.Unlocked.Add(MainGameBuildingId.Aquifer);
            c.Board.At(2).Feature = MainGameFeature.Ice;
            c.Board.At(2).DepositLeft = 50;
            c.Board.At(2).Revealed = true;
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            Assert.IsFalse(c.Preview(2, MainGameBuildingId.Aquifer, out string reason));
            StringAssert.Contains("beside", reason);
        }

        [Test]
        public void Preview_AquiferTooFarFromIce_Refuses()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            c.Unlocked.Add(MainGameBuildingId.Aquifer);
            c.Board.At(1).Feature = MainGameFeature.Ice;
            c.Board.At(1).DepositLeft = 50;
            c.Board.At(1).Revealed = true;
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 10 && b == 1) || (a == 1 && b == 10)) return 4;
                return 99;
            });
            Assert.IsFalse(c.Preview(10, MainGameBuildingId.Aquifer, out string reason));
            StringAssert.Contains("3", reason);
        }

        [Test]
        public void Preview_OxygenUnlocked_AllowsPlace()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            c.Unlocked.Add(MainGameBuildingId.Oxygen);
            Assert.IsTrue(c.Preview(3, MainGameBuildingId.Oxygen, out _));
        }

        [Test]
        public void Preview_Aquifer_RefusesWhenShortOnMinerals()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            c.Unlocked.Add(MainGameBuildingId.Aquifer);
            c.Board.At(2).Feature = MainGameFeature.Ice;
            c.Board.At(2).DepositLeft = 40;
            c.Board.At(2).Revealed = true;
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            while (c.GetStock(MainGameResourceId.Minerals) > 0)
                c.TrySpendStock(MainGameResourceId.Minerals, 1);
            Assert.IsFalse(c.Preview(3, MainGameBuildingId.Aquifer, out string reason));
            StringAssert.Contains("materials", reason);
        }

        [Test]
        public void StoryCheck_AfterIceRevealed_AdvancesToStep1AndUnlocksAquifer()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            Assert.AreEqual(0, c.MainGameStep);
            c.Board.At(2).Feature = MainGameFeature.Ice;
            c.Board.At(2).DepositLeft = 40;
            c.Board.At(2).Revealed = true;
            c.Board.FirstIceTiles.Add(2);
            c.StoryCheck(new System.Collections.Generic.List<string>());
            Assert.AreEqual(1, c.MainGameStep);
            Assert.IsTrue(c.Unlocked.Contains(MainGameBuildingId.Aquifer));
        }

        [Test]
        public void RevealTiles_MarksIceAndAdvancesStory()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            c.Board.At(3).Feature = MainGameFeature.Ice;
            c.Board.At(3).DepositLeft = 30;
            c.RevealTiles(new[] { 1, 2, 3 });
            Assert.IsTrue(c.Board.At(3).Revealed);
            Assert.AreEqual(1, c.MainGameStep);
        }

        [Test]
        public void DustStorm_DarkensSolar_WindKeepsOxygenPowered()
        {
            // During storm solar arrays stop conducting; wind within reach keeps oxygen up.
            var c = MainGameColony.CreateLanding();
            c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
            c.DebugPlaceComplete(9, MainGameBuildingId.Solar);
            c.DebugPlaceComplete(2, MainGameBuildingId.WindTurbine);
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 3 && b == 2) || (a == 2 && b == 3)) return 1;
                return 99;
            });
            Assert.IsTrue(c.IsPowered(3));
            c.DebugForceStorm(true);
            Assert.IsTrue(c.IsPowered(3));
            c.Board.At(2).Building = MainGameBuildingId.None;
            Assert.IsFalse(c.IsPowered(3));
        }

        [Test]
        public void IsPowered_CampPowersOxygen_UntilStorm_ThenNeedsAdjacentGenerator()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
            Assert.IsTrue(c.IsPowered(3)); // camp
            c.DebugForceStorm(true);
            Assert.IsFalse(c.IsPowered(3));
            c.DebugPlaceComplete(2, MainGameBuildingId.WindTurbine);
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 3 && b == 2) || (a == 2 && b == 3)) return 1;
                return 99;
            });
            Assert.IsTrue(c.IsPowered(3));
        }

        [Test]
        public void IsPowered_GeneratorTwoHops_ViaConductor()
        {
            // Tile 1 Geo — 2 Habitat — 3 Oxygen; Dist ortho 1 between pairs.
            var c = MainGameColony.CreateLanding();
            c.DebugForceStorm(true);
            c.DebugPlaceComplete(1, MainGameBuildingId.WindTurbine);
            c.DebugPlaceComplete(2, MainGameBuildingId.Habitat);
            c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                int d = System.Math.Abs(a - b);
                return d == 1 ? 1 : 99;
            });
            Assert.IsTrue(c.IsPowered(3)); // path Geo-Habitat-Oxygen
        }

        [Test]
        public void TryPlaceImmediate_AquiferAdvancesToStep2Unlocks()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            c.Board.At(2).Feature = MainGameFeature.Ice;
            c.Board.At(2).DepositLeft = 40;
            c.Board.At(2).Revealed = true;
            c.StoryCheck(new System.Collections.Generic.List<string>());
            Assert.AreEqual(1, c.MainGameStep);
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 3 && b == 2) || (a == 2 && b == 3)) return 1;
                return 99;
            });
            Assert.IsTrue(c.TryPlaceImmediate(3, MainGameBuildingId.Aquifer, out _));
            Assert.AreEqual(2, c.MainGameStep);
            Assert.IsTrue(c.Unlocked.Contains(MainGameBuildingId.Oxygen));
            Assert.IsFalse(c.Unlocked.Contains(MainGameBuildingId.Solar));
        }

        [Test]
        public void EndWeek_MineSource_CreditsMineralsAndDepletesDeposit()
        {
            var c = MainGameColony.CreateLanding();
            int deposit = 100;
            c.RegisterMineSource(MainGameResourceId.Minerals, want =>
            {
                int g = System.Math.Min(want, deposit);
                deposit -= g;
                return g;
            });
            c.EndWeek();
            Assert.AreEqual(MainGameData.MineHarvestPerWeek, c.GetStock(MainGameResourceId.Minerals));
            Assert.AreEqual(100 - MainGameData.MineHarvestPerWeek, deposit);
        }

        [Test]
        public void MarkResourceKnown_TracksTypedDeposits()
        {
            var c = MainGameColony.CreateLanding();
            Assert.IsFalse(c.KnowsResource(MainGameResourceId.Iron));
            c.MarkResourceKnown(MainGameResourceId.Iron);
            Assert.IsTrue(c.KnowsResource(MainGameResourceId.Iron));
            Assert.IsTrue(c.KnownResources.Contains(MainGameResourceId.Iron));
        }

        [Test]
        public void HaulWeeks_GrowsWithDistance_CloserBuildingShortensTheJob()
        {
            Assert.AreEqual(0, MainGameData.HaulWeeks(1));
            Assert.AreEqual(0, MainGameData.HaulWeeks(2));
            Assert.AreEqual(1, MainGameData.HaulWeeks(4));
            Assert.AreEqual(3, MainGameData.HaulWeeks(8));

            var far = MainGameColony.CreateLanding();
            far.Unlocked.Add(MainGameBuildingId.Habitat);
            far.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 5 && b == 0) || (a == 0 && b == 5)) return 8;
                if ((a == 5 && b == 4) || (a == 4 && b == 5)) return 2;
                return 6;
            });
            Assert.IsTrue(far.TryPlace(5, MainGameBuildingId.Habitat, out _));
            Assert.AreEqual(1 + MainGameData.HaulWeeks(8), far.Board.Sites[0].WeeksTotal);
            far.EndWeek();
            Assert.AreEqual(1, far.Board.Sites[0].WeeksDone);
            Assert.AreEqual(4, far.Board.Sites[0].WeeksTotal);

            far.DebugPlaceComplete(4, MainGameBuildingId.WindTurbine);
            far.EndWeek();
            Assert.AreEqual(0, far.Board.Sites.Count);
            Assert.AreEqual(MainGameBuildingId.Habitat, far.Board.At(5).Building);

            var near = MainGameColony.CreateLanding();
            near.Unlocked.Add(MainGameBuildingId.Habitat);
            near.DebugPlaceComplete(4, MainGameBuildingId.WindTurbine);
            near.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 5 && b == 4) || (a == 4 && b == 5)) return 2;
                if ((a == 5 && b == 0) || (a == 0 && b == 5)) return 8;
                return 6;
            });
            Assert.IsTrue(near.TryPlace(5, MainGameBuildingId.Habitat, out _));
            Assert.AreEqual(1, near.Board.Sites[0].WeeksTotal);
        }

        [Test]
        public void ResourceIdUtil_ParsesDepositTypeNames()
        {
            Assert.IsTrue(MainGameResourceIdUtil.TryParse("Minerals", out var m));
            Assert.AreEqual(MainGameResourceId.Minerals, m);
            Assert.IsTrue(MainGameResourceIdUtil.TryParse("Materials", out var mat));
            Assert.AreEqual(MainGameResourceId.Minerals, mat);
            Assert.AreEqual("Materials", MainGameResourceIdUtil.ToTypeName(MainGameResourceId.Minerals));
            Assert.IsTrue(MainGameResourceIdUtil.TryParse("gas", out var g));
            Assert.AreEqual(MainGameResourceId.Gas, g);
            Assert.IsFalse(MainGameResourceIdUtil.TryParse("Ice", out _));
        }

        static MainGameColony CrewJobColony(int satisfaction)
        {
            var c = MainGameColony.CreateLanding();
            c.DebugSetCrew(satisfaction, MainGameData.FaithStart);
            c.SetSupplyDistanceFn(_ => 0);
            c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
            c.DebugSetIce(20);
            c.Board.Sites.Add(new MainGameSite
            {
                Tile = 5,
                Id = MainGameBuildingId.Habitat,
                WeeksDone = 0,
                WeeksTotal = 2
            });
            return c;
        }

        [Test]
        public void EndWeek_SatisfactionBelowThreshold_CrewDoesNotWork()
        {
            var c = CrewJobColony(39);
            int air = c.Oxygen;
            c.EndWeek();
            Assert.AreEqual(0, c.Board.Sites[0].WeeksDone);
            Assert.AreEqual(MainGameBuildingId.None, c.Board.At(5).Building);
            Assert.AreEqual(air - c.O2Need(), c.Oxygen);
            Assert.AreEqual(20, c.IceStock);
        }

        [Test]
        public void EndWeek_SatisfactionAtThreshold_CrewWorks()
        {
            var c = CrewJobColony(40);
            int air = c.Oxygen;
            c.EndWeek();
            Assert.AreEqual(0, c.Board.Sites.Count);
            Assert.AreEqual(MainGameBuildingId.Habitat, c.Board.At(5).Building);
            Assert.AreEqual(air + MainGameData.Get(MainGameBuildingId.Oxygen).OxygenProducedPerWeek - c.O2Need(), c.Oxygen);
        }

        [Test]
        public void EndWeek_CriticalAir_ThenCoveredNeeds_MovesSatisfaction()
        {
            var starved = MainGameColony.CreateLanding();
            starved.DebugSetOxygen(0);
            starved.EndWeek();
            Assert.AreEqual(MainGameData.SatisfactionStart - MainGameData.AirCriticalSatisfactionLoss, starved.Satisfaction);

            var covered = MainGameColony.CreateLanding();
            covered.DebugSetCrew(starved.Satisfaction, MainGameData.FaithStart);
            covered.DebugLandShuttle();
            covered.DebugPlaceComplete(5, MainGameBuildingId.Habitat);
            covered.DebugPlaceComplete(6, MainGameBuildingId.Habitat);
            covered.EndWeek();
            Assert.AreEqual(starved.Satisfaction + MainGameData.NeedsMetSatisfactionGain, covered.Satisfaction);
        }

        [Test]
        public void EndWeek_QuietWeekLosesFaith_FinishedStepGainsFaith()
        {
            var quiet = MainGameColony.CreateLanding();
            quiet.EndWeek();
            Assert.AreEqual(0, quiet.MainGameStep);
            Assert.AreEqual(MainGameData.FaithStart - MainGameData.QuietWeekFaithLoss, quiet.Faith);

            var found = MainGameColony.CreateLanding();
            found.Board.At(1).Feature = MainGameFeature.Ice;
            found.Board.At(1).DepositLeft = 10;
            found.Board.At(1).Revealed = true;
            found.EndWeek();
            Assert.AreEqual(1, found.MainGameStep);
            Assert.AreEqual(MainGameData.FaithStart + MainGameData.StepAdvancedFaithGain, found.Faith);
        }

        [Test]
        public void EndWeek_SatisfactionZero_CancelsBeforeWin()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugSetProgress(5, MainGameData.MainGameStormWeeks);
            c.DebugScheduleStorm(0);
            c.DebugSetOxygen(80);
            c.DebugSetCrew(10, MainGameData.FaithStart);
            c.DebugLandShuttle();
            c.EndWeek();
            Assert.IsTrue(c.ContractCancelled);
            StringAssert.Contains("satisfaction", c.ContractMessage);
            Assert.AreEqual(5, c.MainGameStep);
        }

        [Test]
        public void EndWeek_FaithZero_CancelsAndStaysOnStep()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugSetCrew(MainGameData.SatisfactionStart, 5);
            c.DebugSetOxygen(0);
            c.EndWeek();
            Assert.IsTrue(c.ContractCancelled);
            StringAssert.Contains("faith", c.ContractMessage);
            Assert.AreEqual(0, c.MainGameStep);
        }

        [Test]
        public void EndWeek_FaithZero_WithAirReserves_ExtendsInsteadOfCancel()
        {
            var c = MainGameColony.CreateLanding();
            int need = c.O2Need();
            c.DebugSetCrew(MainGameData.SatisfactionStart, 6);
            c.DebugSetOxygen(MainGameData.AirReserveFaithExtensionThreshold(need) + 10);
            c.EndWeek();
            Assert.IsFalse(c.ContractCancelled);
            Assert.AreEqual(MainGameData.FaithRecoveryFloor, c.Faith);
        }

        [Test]
        public void EndWeek_SteadyWeek_RaisesFaithWhenAirStocked()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugSetProgress(5, 0);
            c.DebugSetCrew(MainGameData.SatisfactionStart, 40);
            c.DebugPlaceComplete(1, MainGameBuildingId.Oxygen);
            c.DebugPlaceComplete(2, MainGameBuildingId.WindTurbine);
            c.DebugSetOxygen(80);
            c.DebugSetIce(20);
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            int before = c.Faith;
            c.EndWeek();
            Assert.Greater(c.Faith, before - MainGameData.QuietWeekFaithLoss);
        }

        [Test]
        public void EndWeek_StormWithNoAir_CancelsContract()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugForceStorm(true);
            c.DebugSetOxygen(0);
            c.EndWeek();
            Assert.IsTrue(c.ContractCancelled);
            StringAssert.Contains("ran out of air", c.ContractMessage);
            Assert.AreNotEqual(MainGameData.MainGameSteps, c.MainGameStep);
        }

        [Test]
        public void EndWeek_StormHoldWithAirAndFaith_ReachesStepSix()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugSetProgress(5, MainGameData.MainGameStormWeeks);
            c.DebugScheduleStorm(0);
            c.DebugSetOxygen(80);
            c.DebugPlaceComplete(3, MainGameBuildingId.Oxygen);
            c.DebugPlaceComplete(4, MainGameBuildingId.WindTurbine);
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            c.EndWeek();
            Assert.IsFalse(c.ContractCancelled);
            Assert.Greater(c.Satisfaction, 0);
            Assert.Greater(c.Faith, 0);
            Assert.AreEqual(MainGameData.MainGameSteps, c.MainGameStep);
        }

        [Test]
        public void Stats_Landing_ShowsTheCrewAndTheFirstGoal()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            string stats = c.StatsText();
            StringAssert.Contains("12 people · working", stats);
            StringAssert.Contains("→ Find ice", stats);
            StringAssert.Contains("Beds 0/20", stats);
            StringAssert.Contains("Air 50 · need 6/wk", stats);
            StringAssert.Contains("No build on the board.", stats);
        }

        [Test]
        public void Stats_StoppedCrew_NamesTheWaitingJob()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugSetCrew(10, 70);
            c.Board.Sites.Add(new MainGameSite
            {
                Tile = 2,
                Id = MainGameBuildingId.Aquifer,
                WeeksDone = 1,
                WeeksTotal = 3
            });
            string stats = c.StatsText();
            StringAssert.Contains("stopped", stats);
            StringAssert.Contains("Waiting on Ice Aquifer · week 1 of 3 done", stats);
        }

        [Test]
        public void Stats_BedsGoal_ShowsHowManyAreReady()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugSetProgress(3, 4);
            c.DebugPlaceComplete(1, MainGameBuildingId.Habitat);
            c.DebugPlaceComplete(2, MainGameBuildingId.Habitat);
            string stats = c.StatsText();
            StringAssert.Contains("→ Beds 20/20", stats);
            StringAssert.Contains("20 are ready", stats);
            StringAssert.Contains("✓ Find ice", stats);
        }

        [Test]
        public void WeekBriefing_Opening_IsMaraAndNamesTheFirstJob()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            var briefing = MainGameWeekBriefing.Opening(c);
            Assert.AreEqual("Mara Voss", briefing.Speaker);
            Assert.AreEqual(MainGameBriefingTone.Progress, briefing.Tone);
            StringAssert.Contains("lander is down", briefing.Speech);
            StringAssert.Contains("Survey Team", briefing.Speech);
        }

        [Test]
        public void WeekBriefing_AirGain_ForemanSaysWhatMovedAndWhatIsNext()
        {
            var c = MainGameColony.CreateLanding();
            var log = new List<string> { "+12 air. Colonists ran the Atmosphere Processor." };
            var before = new MainGameWeekMemory { Satisfaction = 80, Faith = 80, Oxygen = 40, Step = 0, Storm = false };
            var briefing = MainGameWeekBriefing.Compose(c, log, before);
            Assert.AreEqual(MainGameBriefingTone.Progress, briefing.Tone);
            StringAssert.Contains("Mara Voss", briefing.Speaker);
            StringAssert.Contains("Atmosphere Processor", briefing.Speech);
            StringAssert.Contains("Next:", briefing.Speech);
        }

        [Test]
        public void WeekBriefing_AirCritical_ColonistComesOn()
        {
            var c = MainGameColony.CreateLanding();
            var log = new List<string> { "AIR CRITICAL: not enough oxygen" };
            var before = new MainGameWeekMemory { Satisfaction = 80, Faith = 80, Oxygen = 50, Step = 0, Storm = false };
            var briefing = MainGameWeekBriefing.Compose(c, log, before);
            Assert.AreEqual(MainGameBriefingTone.Concern, briefing.Tone);
            StringAssert.Contains("Ivo Pell", briefing.Speaker);
            StringAssert.Contains("air ran short", briefing.Speech);
        }

        [Test]
        public void WeekBriefing_ContractCancel_IsACrisis()
        {
            var c = MainGameColony.CreateLanding();
            c.DebugForceStorm(true);
            c.DebugSetOxygen(0);
            var log = c.EndWeek();
            var before = new MainGameWeekMemory { Satisfaction = 80, Faith = 80, Oxygen = 50, Step = 0, Storm = false };
            var briefing = MainGameWeekBriefing.Compose(c, log, before);
            Assert.AreEqual(MainGameBriefingTone.Crisis, briefing.Tone);
            StringAssert.Contains("Auditor Kline", briefing.Speaker);
            StringAssert.Contains("cancelled the contract", briefing.Speech);
            StringAssert.Contains("mission is over", briefing.Speech);
        }

        [Test]
        public void CancelledContract_StopsTheClock()
        {
            var go = new GameObject("MainGameTimeLapseTest");
            var previousScale = Time.timeScale;
            try
            {
                var clock = go.AddComponent<MainGameTimeLapse>();
                clock.SetSpeed(MainGameTimeLapse.Speed.Fast);
                clock.EndRun();
                clock.SetSpeed(MainGameTimeLapse.Speed.Normal);
                clock.HoldForBriefing(false);
                clock.TogglePause();
                Assert.IsTrue(clock.RunEnded);
                Assert.AreEqual(MainGameTimeLapse.Speed.Pause, clock.CurrentSpeed);
                Assert.LessOrEqual(Time.timeScale, 0.01f);
                StringAssert.Contains("CANCELLED", clock.SpeedLabel());
            }
            finally
            {
                Object.DestroyImmediate(go);
                Time.timeScale = previousScale;
            }
        }

        [Test]
        public void Research_CostsWeeks_ThenUnlocksTier()
        {
            var c = MainGameColony.CreateLanding();
            c.StartMainGame();
            Assert.IsTrue(c.Research.IsTierUnlocked(MainGameResearchTier.Tier1));
            Assert.IsFalse(c.Research.IsUnlocked(MainGameResearchId.FasterBuild));
            Assert.IsFalse(c.Research.IsUnlocked(MainGameResearchId.ExtendedSurvey));
            Assert.IsTrue(c.Research.TryBegin(MainGameResearchId.FasterBuild, out _));
            Assert.AreEqual(MainGameData.ResearchWeeks(MainGameResearchId.FasterBuild), c.Research.WeeksRemaining);
            int need = c.Research.WeeksRemaining;
            for (int i = 0; i < need; i++) c.EndWeek();
            Assert.IsTrue(c.Research.IsUnlocked(MainGameResearchId.FasterBuild));
            Assert.AreEqual(0, c.Research.WeeksRemaining);
        }

        [Test]
        public void LandingPick_PrimaryIceWithinFirstObjectiveRange()
        {
            var center = new Vector2Int(64, 48);
            var picked = LandingSiteResourceLayout.PickLandingCell(center, searchRadius: 5);
            var layout = LandingSiteResourceLayout.Create(picked);
            Assert.LessOrEqual(
                layout.PrimaryIceHexDist(),
                LandingSiteResourceLayout.FirstObjectiveIceMaxHex,
                "First ice must be reachable on opening survey without burning the contract.");
        }

        [Test]
        public void LandingLayout_ForHomeCell_MatchesActiveLanding()
        {
            var center = new Vector2Int(32, 32);
            LandingSiteResourceLayout.PickLandingCell(center, 3);
            var fromActive = LandingSiteResourceLayout.ForHomeCell(center);
            Assert.AreEqual(
                LandingSiteResourceLayout.ActiveLandingCell,
                fromActive.CommandCell);
        }
    }
}
