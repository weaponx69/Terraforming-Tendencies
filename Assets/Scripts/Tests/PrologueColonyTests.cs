using System.Collections.Generic;
using NUnit.Framework;
using GameDevTV.RTS.Prologue;
using UnityEngine;

namespace GameDevTV.RTS.Tests
{
    public class PrologueColonyTests
    {
        [Test]
        public void BuildingInfo_HabitatHasTenBedsAndOneBuildWeek()
        {
            var info = PrologueData.Get(PrologueBuildingId.Habitat);
            Assert.AreEqual(10, info.Beds);
            Assert.AreEqual(1, info.BuildWeeks);
        }

        [Test]
        public void LandingOxygen_IsFifty()
        {
            Assert.AreEqual(50, PrologueData.LandingOxygen);
        }

        [Test]
        public void O2Need_StartColonists_IsSix()
        {
            var c = PrologueColony.CreateLanding();
            Assert.AreEqual(6, c.O2Need());
        }

        [Test]
        public void Energy_CampFillsPool_OrbitalSpends()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            Assert.GreaterOrEqual(c.Energy, PrologueData.OrbitalScanEnergyCost);
            Assert.IsTrue(c.TrySpendEnergy(PrologueData.OrbitalScanEnergyCost));
            int after = c.Energy;
            Assert.IsFalse(c.TrySpendEnergy(c.Energy + 1));
            Assert.AreEqual(after, c.Energy);
        }

        [Test]
        public void Energy_RefillsFromCampOnEndWeek_StormCutsCamp()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            while (c.TrySpendEnergy(1)) { }
            Assert.AreEqual(0, c.Energy);
            c.EndWeek();
            Assert.AreEqual(PrologueData.CampEnergyGeneration, c.Energy);
            c.DebugForceStorm(true);
            while (c.TrySpendEnergy(1)) { }
            c.EndWeek();
            Assert.AreEqual(0, c.Energy); // camp dark, no generators
        }

        [Test]
        public void EndWeek_PoweredOxygen_AddsNetOxygen()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugPlaceComplete(2, PrologueBuildingId.Aquifer);
            c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
            c.DebugPlaceComplete(4, PrologueBuildingId.Solar);
            c.SetDistanceFn((a, b) =>
            {
                if ((a == 2 && b == 1) || (a == 1 && b == 2)) return 1;
                if ((a == 3 && b == 4) || (a == 4 && b == 3)) return 1;
                return 99;
            });
            c.Board.At(1).Feature = PrologueFeature.Ice;
            c.Board.At(1).DepositLeft = 100;
            c.Board.At(1).Revealed = true;
            int before = c.Oxygen;
            c.EndWeek();
            Assert.Greater(c.Oxygen, before - 6);
        }

        [Test]
        public void TotalBeds_TwoHabitats_IsTwenty()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugPlaceComplete(5, PrologueBuildingId.Habitat);
            c.DebugPlaceComplete(6, PrologueBuildingId.Habitat);
            Assert.AreEqual(20, c.TotalBeds());
        }

        [Test]
        public void Preview_Aquifer_OnIceDepositHex_Refuses()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            c.Unlocked.Add(PrologueBuildingId.Aquifer);
            c.Board.At(2).Feature = PrologueFeature.Ice;
            c.Board.At(2).DepositLeft = 50;
            c.Board.At(2).Revealed = true;
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            Assert.IsFalse(c.Preview(2, PrologueBuildingId.Aquifer, out string reason));
            StringAssert.Contains("beside", reason);
        }

        [Test]
        public void Preview_AquiferTooFarFromIce_Refuses()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            c.Unlocked.Add(PrologueBuildingId.Aquifer);
            c.Board.At(1).Feature = PrologueFeature.Ice;
            c.Board.At(1).DepositLeft = 50;
            c.Board.At(1).Revealed = true;
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 10 && b == 1) || (a == 1 && b == 10)) return 4;
                return 99;
            });
            Assert.IsFalse(c.Preview(10, PrologueBuildingId.Aquifer, out string reason));
            StringAssert.Contains("3", reason);
        }

        [Test]
        public void Preview_OxygenUnlocked_AllowsPlace()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            c.Unlocked.Add(PrologueBuildingId.Oxygen);
            Assert.IsTrue(c.Preview(3, PrologueBuildingId.Oxygen, out _));
        }

        [Test]
        public void Preview_Aquifer_RefusesWhenShortOnMinerals()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            c.Unlocked.Add(PrologueBuildingId.Aquifer);
            c.Board.At(2).Feature = PrologueFeature.Ice;
            c.Board.At(2).DepositLeft = 40;
            c.Board.At(2).Revealed = true;
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            while (c.GetStock(PrologueResourceId.Minerals) > 0)
                c.TrySpendStock(PrologueResourceId.Minerals, 1);
            Assert.IsFalse(c.Preview(3, PrologueBuildingId.Aquifer, out string reason));
            StringAssert.Contains("minerals", reason);
        }

        [Test]
        public void StoryCheck_AfterIceRevealed_AdvancesToStep1AndUnlocksAquifer()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            Assert.AreEqual(0, c.PrologueStep);
            c.Board.At(2).Feature = PrologueFeature.Ice;
            c.Board.At(2).DepositLeft = 40;
            c.Board.At(2).Revealed = true;
            c.Board.FirstIceTiles.Add(2);
            c.StoryCheck(new System.Collections.Generic.List<string>());
            Assert.AreEqual(1, c.PrologueStep);
            Assert.IsTrue(c.Unlocked.Contains(PrologueBuildingId.Aquifer));
        }

        [Test]
        public void RevealTiles_MarksIceAndAdvancesStory()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            c.Board.At(3).Feature = PrologueFeature.Ice;
            c.Board.At(3).DepositLeft = 30;
            c.RevealTiles(new[] { 1, 2, 3 });
            Assert.IsTrue(c.Board.At(3).Revealed);
            Assert.AreEqual(1, c.PrologueStep);
        }

        [Test]
        public void DustStorm_DarkensSolar_WindKeepsOxygenPowered()
        {
            // During storm solar arrays stop conducting; wind within reach keeps oxygen up.
            var c = PrologueColony.CreateLanding();
            c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
            c.DebugPlaceComplete(9, PrologueBuildingId.Solar);
            c.DebugPlaceComplete(2, PrologueBuildingId.WindTurbine);
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 3 && b == 2) || (a == 2 && b == 3)) return 1;
                return 99;
            });
            Assert.IsTrue(c.IsPowered(3));
            c.DebugForceStorm(true);
            Assert.IsTrue(c.IsPowered(3));
            c.Board.At(2).Building = PrologueBuildingId.None;
            Assert.IsFalse(c.IsPowered(3));
        }

        [Test]
        public void IsPowered_CampPowersOxygen_UntilStorm_ThenNeedsAdjacentGenerator()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
            Assert.IsTrue(c.IsPowered(3)); // camp
            c.DebugForceStorm(true);
            Assert.IsFalse(c.IsPowered(3));
            c.DebugPlaceComplete(2, PrologueBuildingId.WindTurbine);
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
            var c = PrologueColony.CreateLanding();
            c.DebugForceStorm(true);
            c.DebugPlaceComplete(1, PrologueBuildingId.WindTurbine);
            c.DebugPlaceComplete(2, PrologueBuildingId.Habitat);
            c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
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
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            c.Board.At(2).Feature = PrologueFeature.Ice;
            c.Board.At(2).DepositLeft = 40;
            c.Board.At(2).Revealed = true;
            c.StoryCheck(new System.Collections.Generic.List<string>());
            Assert.AreEqual(1, c.PrologueStep);
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 3 && b == 2) || (a == 2 && b == 3)) return 1;
                return 99;
            });
            Assert.IsTrue(c.TryPlaceImmediate(3, PrologueBuildingId.Aquifer, out _));
            Assert.AreEqual(2, c.PrologueStep);
            Assert.IsTrue(c.Unlocked.Contains(PrologueBuildingId.Oxygen));
            Assert.IsFalse(c.Unlocked.Contains(PrologueBuildingId.Solar));
        }

        [Test]
        public void EndWeek_MineSource_CreditsMineralsAndDepletesDeposit()
        {
            var c = PrologueColony.CreateLanding();
            int deposit = 100;
            c.RegisterMineSource(PrologueResourceId.Minerals, want =>
            {
                int g = System.Math.Min(want, deposit);
                deposit -= g;
                return g;
            });
            c.EndWeek();
            Assert.AreEqual(PrologueData.MineHarvestPerWeek, c.GetStock(PrologueResourceId.Minerals));
            Assert.AreEqual(100 - PrologueData.MineHarvestPerWeek, deposit);
        }

        [Test]
        public void MarkResourceKnown_TracksTypedDeposits()
        {
            var c = PrologueColony.CreateLanding();
            Assert.IsFalse(c.KnowsResource(PrologueResourceId.Iron));
            c.MarkResourceKnown(PrologueResourceId.Iron);
            Assert.IsTrue(c.KnowsResource(PrologueResourceId.Iron));
            Assert.IsTrue(c.KnownResources.Contains(PrologueResourceId.Iron));
        }

        [Test]
        public void HaulWeeks_GrowsWithDistance_CloserBuildingShortensTheJob()
        {
            Assert.AreEqual(0, PrologueData.HaulWeeks(1));
            Assert.AreEqual(0, PrologueData.HaulWeeks(2));
            Assert.AreEqual(1, PrologueData.HaulWeeks(4));
            Assert.AreEqual(3, PrologueData.HaulWeeks(8));

            var far = PrologueColony.CreateLanding();
            far.Unlocked.Add(PrologueBuildingId.Habitat);
            far.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 5 && b == 0) || (a == 0 && b == 5)) return 8;
                if ((a == 5 && b == 4) || (a == 4 && b == 5)) return 2;
                return 6;
            });
            Assert.IsTrue(far.TryPlace(5, PrologueBuildingId.Habitat, out _));
            Assert.AreEqual(1 + PrologueData.HaulWeeks(8), far.Board.Sites[0].WeeksTotal);
            far.EndWeek();
            Assert.AreEqual(1, far.Board.Sites[0].WeeksDone);
            Assert.AreEqual(4, far.Board.Sites[0].WeeksTotal);

            far.DebugPlaceComplete(4, PrologueBuildingId.WindTurbine);
            far.EndWeek();
            Assert.AreEqual(0, far.Board.Sites.Count);
            Assert.AreEqual(PrologueBuildingId.Habitat, far.Board.At(5).Building);

            var near = PrologueColony.CreateLanding();
            near.Unlocked.Add(PrologueBuildingId.Habitat);
            near.DebugPlaceComplete(4, PrologueBuildingId.WindTurbine);
            near.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                if ((a == 5 && b == 4) || (a == 4 && b == 5)) return 2;
                if ((a == 5 && b == 0) || (a == 0 && b == 5)) return 8;
                return 6;
            });
            Assert.IsTrue(near.TryPlace(5, PrologueBuildingId.Habitat, out _));
            Assert.AreEqual(1, near.Board.Sites[0].WeeksTotal);
        }

        [Test]
        public void ResourceIdUtil_ParsesDepositTypeNames()
        {
            Assert.IsTrue(PrologueResourceIdUtil.TryParse("Minerals", out var m));
            Assert.AreEqual(PrologueResourceId.Minerals, m);
            Assert.IsTrue(PrologueResourceIdUtil.TryParse("gas", out var g));
            Assert.AreEqual(PrologueResourceId.Gas, g);
            Assert.IsFalse(PrologueResourceIdUtil.TryParse("Ice", out _));
        }

        static PrologueColony CrewJobColony(int satisfaction)
        {
            var c = PrologueColony.CreateLanding();
            c.DebugSetCrew(satisfaction, PrologueData.FaithStart);
            c.SetSupplyDistanceFn(_ => 0);
            c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
            c.DebugSetIce(20);
            c.Board.Sites.Add(new PrologueSite
            {
                Tile = 5,
                Id = PrologueBuildingId.Habitat,
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
            Assert.AreEqual(PrologueBuildingId.None, c.Board.At(5).Building);
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
            Assert.AreEqual(PrologueBuildingId.Habitat, c.Board.At(5).Building);
            Assert.AreEqual(air + PrologueData.Get(PrologueBuildingId.Oxygen).OxygenProducedPerWeek - c.O2Need(), c.Oxygen);
        }

        [Test]
        public void EndWeek_CriticalAir_ThenCoveredNeeds_MovesSatisfaction()
        {
            var starved = PrologueColony.CreateLanding();
            starved.DebugSetOxygen(0);
            starved.EndWeek();
            Assert.AreEqual(PrologueData.SatisfactionStart - PrologueData.AirCriticalSatisfactionLoss, starved.Satisfaction);

            var covered = PrologueColony.CreateLanding();
            covered.DebugSetCrew(starved.Satisfaction, PrologueData.FaithStart);
            covered.DebugLandShuttle();
            covered.DebugPlaceComplete(5, PrologueBuildingId.Habitat);
            covered.DebugPlaceComplete(6, PrologueBuildingId.Habitat);
            covered.EndWeek();
            Assert.AreEqual(starved.Satisfaction + PrologueData.NeedsMetSatisfactionGain, covered.Satisfaction);
        }

        [Test]
        public void EndWeek_QuietWeekLosesFaith_FinishedStepGainsFaith()
        {
            var quiet = PrologueColony.CreateLanding();
            quiet.EndWeek();
            Assert.AreEqual(0, quiet.PrologueStep);
            Assert.AreEqual(PrologueData.FaithStart - PrologueData.QuietWeekFaithLoss, quiet.Faith);

            var found = PrologueColony.CreateLanding();
            found.Board.At(1).Feature = PrologueFeature.Ice;
            found.Board.At(1).DepositLeft = 10;
            found.Board.At(1).Revealed = true;
            found.EndWeek();
            Assert.AreEqual(1, found.PrologueStep);
            Assert.AreEqual(PrologueData.FaithStart + PrologueData.StepAdvancedFaithGain, found.Faith);
        }

        [Test]
        public void EndWeek_SatisfactionZero_CancelsBeforeWin()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugSetProgress(5, PrologueData.PrologueStormWeeks);
            c.DebugScheduleStorm(0);
            c.DebugSetOxygen(80);
            c.DebugSetCrew(10, PrologueData.FaithStart);
            c.DebugLandShuttle();
            c.EndWeek();
            Assert.IsTrue(c.ContractCancelled);
            StringAssert.Contains("satisfaction", c.ContractMessage);
            Assert.AreEqual(5, c.PrologueStep);
        }

        [Test]
        public void EndWeek_FaithZero_CancelsAndStaysOnStep()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugSetCrew(PrologueData.SatisfactionStart, 5);
            c.DebugSetOxygen(0);
            c.EndWeek();
            Assert.IsTrue(c.ContractCancelled);
            StringAssert.Contains("faith", c.ContractMessage);
            Assert.AreEqual(0, c.PrologueStep);
        }

        [Test]
        public void EndWeek_FaithZero_WithAirReserves_ExtendsInsteadOfCancel()
        {
            var c = PrologueColony.CreateLanding();
            int need = c.O2Need();
            c.DebugSetCrew(PrologueData.SatisfactionStart, 6);
            c.DebugSetOxygen(PrologueData.AirReserveFaithExtensionThreshold(need) + 10);
            c.EndWeek();
            Assert.IsFalse(c.ContractCancelled);
            Assert.AreEqual(PrologueData.FaithRecoveryFloor, c.Faith);
        }

        [Test]
        public void EndWeek_SteadyWeek_RaisesFaithWhenAirStocked()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugSetProgress(5, 0);
            c.DebugSetCrew(PrologueData.SatisfactionStart, 40);
            c.DebugPlaceComplete(1, PrologueBuildingId.Oxygen);
            c.DebugPlaceComplete(2, PrologueBuildingId.WindTurbine);
            c.DebugSetOxygen(80);
            c.DebugSetIce(20);
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            int before = c.Faith;
            c.EndWeek();
            Assert.Greater(c.Faith, before - PrologueData.QuietWeekFaithLoss);
        }

        [Test]
        public void EndWeek_StormWithNoAir_CancelsContract()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugForceStorm(true);
            c.DebugSetOxygen(0);
            c.EndWeek();
            Assert.IsTrue(c.ContractCancelled);
            StringAssert.Contains("ran out of air", c.ContractMessage);
            Assert.AreNotEqual(PrologueData.PrologueSteps, c.PrologueStep);
        }

        [Test]
        public void EndWeek_StormHoldWithAirAndFaith_ReachesStepSix()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugSetProgress(5, PrologueData.PrologueStormWeeks);
            c.DebugScheduleStorm(0);
            c.DebugSetOxygen(80);
            c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
            c.DebugPlaceComplete(4, PrologueBuildingId.WindTurbine);
            c.SetDistanceFn((a, b) => a == b ? 0 : 1);
            c.EndWeek();
            Assert.IsFalse(c.ContractCancelled);
            Assert.Greater(c.Satisfaction, 0);
            Assert.Greater(c.Faith, 0);
            Assert.AreEqual(PrologueData.PrologueSteps, c.PrologueStep);
        }

        [Test]
        public void Stats_Landing_ShowsTheCrewAndTheFirstGoal()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
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
            var c = PrologueColony.CreateLanding();
            c.DebugSetCrew(10, 70);
            c.Board.Sites.Add(new PrologueSite
            {
                Tile = 2,
                Id = PrologueBuildingId.Aquifer,
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
            var c = PrologueColony.CreateLanding();
            c.DebugSetProgress(3, 4);
            c.DebugPlaceComplete(1, PrologueBuildingId.Habitat);
            c.DebugPlaceComplete(2, PrologueBuildingId.Habitat);
            string stats = c.StatsText();
            StringAssert.Contains("→ Beds 20/20", stats);
            StringAssert.Contains("20 are ready", stats);
            StringAssert.Contains("✓ Find ice", stats);
        }

        [Test]
        public void WeekBriefing_Opening_IsMaraAndNamesTheFirstJob()
        {
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            var briefing = PrologueWeekBriefing.Opening(c);
            Assert.AreEqual("Mara Voss", briefing.Speaker);
            Assert.AreEqual(PrologueBriefingTone.Progress, briefing.Tone);
            StringAssert.Contains("lander is down", briefing.Speech);
            StringAssert.Contains("Survey Team", briefing.Speech);
        }

        [Test]
        public void WeekBriefing_AirGain_ForemanSaysWhatMovedAndWhatIsNext()
        {
            var c = PrologueColony.CreateLanding();
            var log = new List<string> { "+12 air. Colonists ran the Atmosphere Processor." };
            var before = new PrologueWeekMemory { Satisfaction = 80, Faith = 80, Oxygen = 40, Step = 0, Storm = false };
            var briefing = PrologueWeekBriefing.Compose(c, log, before);
            Assert.AreEqual(PrologueBriefingTone.Progress, briefing.Tone);
            StringAssert.Contains("Mara Voss", briefing.Speaker);
            StringAssert.Contains("Atmosphere Processor", briefing.Speech);
            StringAssert.Contains("Next:", briefing.Speech);
        }

        [Test]
        public void WeekBriefing_AirCritical_ColonistComesOn()
        {
            var c = PrologueColony.CreateLanding();
            var log = new List<string> { "AIR CRITICAL: not enough oxygen" };
            var before = new PrologueWeekMemory { Satisfaction = 80, Faith = 80, Oxygen = 50, Step = 0, Storm = false };
            var briefing = PrologueWeekBriefing.Compose(c, log, before);
            Assert.AreEqual(PrologueBriefingTone.Concern, briefing.Tone);
            StringAssert.Contains("Ivo Pell", briefing.Speaker);
            StringAssert.Contains("air ran short", briefing.Speech);
        }

        [Test]
        public void WeekBriefing_ContractCancel_IsACrisis()
        {
            var c = PrologueColony.CreateLanding();
            c.DebugForceStorm(true);
            c.DebugSetOxygen(0);
            var log = c.EndWeek();
            var before = new PrologueWeekMemory { Satisfaction = 80, Faith = 80, Oxygen = 50, Step = 0, Storm = false };
            var briefing = PrologueWeekBriefing.Compose(c, log, before);
            Assert.AreEqual(PrologueBriefingTone.Crisis, briefing.Tone);
            StringAssert.Contains("Auditor Kline", briefing.Speaker);
            StringAssert.Contains("cancelled the contract", briefing.Speech);
            StringAssert.Contains("mission is over", briefing.Speech);
        }

        [Test]
        public void CancelledContract_StopsTheClock()
        {
            var go = new GameObject("PrologueTimeLapseTest");
            var previousScale = Time.timeScale;
            try
            {
                var clock = go.AddComponent<PrologueTimeLapse>();
                clock.SetSpeed(PrologueTimeLapse.Speed.Fast);
                clock.EndRun();
                clock.SetSpeed(PrologueTimeLapse.Speed.Normal);
                clock.HoldForBriefing(false);
                clock.TogglePause();
                Assert.IsTrue(clock.RunEnded);
                Assert.AreEqual(PrologueTimeLapse.Speed.Pause, clock.CurrentSpeed);
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
            var c = PrologueColony.CreateLanding();
            c.StartPrologue();
            Assert.IsTrue(c.Research.IsTierUnlocked(PrologueResearchTier.Tier1));
            Assert.IsFalse(c.Research.IsUnlocked(PrologueResearchId.FasterBuild));
            Assert.IsTrue(c.Research.TryBegin(PrologueResearchId.FasterBuild, out _));
            Assert.AreEqual(PrologueData.ResearchWeeks(PrologueResearchId.FasterBuild), c.Research.WeeksRemaining);
            int need = c.Research.WeeksRemaining;
            for (int i = 0; i < need; i++) c.EndWeek();
            Assert.IsTrue(c.Research.IsUnlocked(PrologueResearchId.FasterBuild));
            Assert.AreEqual(0, c.Research.WeeksRemaining);
        }
    }
}
