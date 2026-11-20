using NUnit.Framework;
using GameDevTV.RTS.Prologue;

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
            c.Unlocked.Add(PrologueBuildingId.Solar);
            Assert.IsTrue(c.Preview(3, PrologueBuildingId.Oxygen, out _));
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
            var c = PrologueColony.CreateLanding();
            c.DebugPlaceComplete(3, PrologueBuildingId.Oxygen);
            c.DebugPlaceComplete(4, PrologueBuildingId.Solar);
            c.DebugPlaceComplete(2, PrologueBuildingId.WindTurbine);
            c.SetDistanceFn((a, b) =>
            {
                if (a == b) return 0;
                // Touching ring around oxygen tile 3: 2 and 4 are adjacent.
                if ((a == 3 && (b == 2 || b == 4)) || (b == 3 && (a == 2 || a == 4))) return 1;
                return 99;
            });
            Assert.IsTrue(c.IsPowered(3));
            c.DebugForceStorm(true);
            Assert.IsTrue(c.IsPowered(3));
            c.Board.At(2).Building = PrologueBuildingId.None;
            Assert.IsFalse(c.IsPowered(3));
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
            Assert.IsTrue(c.Unlocked.Contains(PrologueBuildingId.Solar));
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
        public void ResourceIdUtil_ParsesDepositTypeNames()
        {
            Assert.IsTrue(PrologueResourceIdUtil.TryParse("Minerals", out var m));
            Assert.AreEqual(PrologueResourceId.Minerals, m);
            Assert.IsTrue(PrologueResourceIdUtil.TryParse("gas", out var g));
            Assert.AreEqual(PrologueResourceId.Gas, g);
            Assert.IsFalse(PrologueResourceIdUtil.TryParse("Ice", out _));
        }
    }
}
