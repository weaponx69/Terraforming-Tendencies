using System.Collections.Generic;
using GameDevTV.RTS.MainGame;
using NUnit.Framework;

namespace GameDevTV.RTS.Tests
{
    public class MainGamePerfScenarioTests
    {
        [Test]
        public void FindPath_ReachesAdjacentGoal()
        {
            var colony = new MainGameColony();
            colony.SetDistanceFn((a, b) => System.Math.Abs(a - b));
            for (int i = 0; i < 5; i++)
                colony.Board.Tiles.Add(new MainGameTile { Index = i });

            var path = MainGamePerfScenario.FindPath(0, 3, colony);
            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(0, path[0]);
            Assert.AreEqual(3, path[3]);
        }

        [Test]
        public void FindPath_EmptyWhenUnreachable()
        {
            var colony = new MainGameColony();
            colony.SetDistanceFn((a, b) => 99);
            colony.Board.Tiles.Add(new MainGameTile { Index = 0 });
            colony.Board.Tiles.Add(new MainGameTile { Index = 1 });

            var path = MainGamePerfScenario.FindPath(0, 1, colony);
            Assert.AreEqual(0, path.Count);
        }
    }
}