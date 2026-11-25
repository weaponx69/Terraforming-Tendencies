using System.Collections.Generic;
using GameDevTV.RTS.Prologue;
using NUnit.Framework;

namespace GameDevTV.RTS.Tests
{
    public class ProloguePerfScenarioTests
    {
        [Test]
        public void FindPath_ReachesAdjacentGoal()
        {
            var colony = new PrologueColony();
            colony.SetDistanceFn((a, b) => System.Math.Abs(a - b));
            for (int i = 0; i < 5; i++)
                colony.Board.Tiles.Add(new PrologueTile { Index = i });

            var path = ProloguePerfScenario.FindPath(0, 3, colony);
            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(0, path[0]);
            Assert.AreEqual(3, path[3]);
        }

        [Test]
        public void FindPath_EmptyWhenUnreachable()
        {
            var colony = new PrologueColony();
            colony.SetDistanceFn((a, b) => 99);
            colony.Board.Tiles.Add(new PrologueTile { Index = 0 });
            colony.Board.Tiles.Add(new PrologueTile { Index = 1 });

            var path = ProloguePerfScenario.FindPath(0, 1, colony);
            Assert.AreEqual(0, path.Count);
        }
    }
}