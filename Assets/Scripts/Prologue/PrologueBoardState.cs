using System.Collections.Generic;

namespace GameDevTV.RTS.Prologue
{
    public sealed class PrologueTile
    {
        public int Index;
        public PrologueFeature Feature;
        public int DepositLeft; // ice amount remaining
        public bool Revealed;
        public bool Lit; // reachable / power-lit ground for drone start
        public PrologueBuildingId Building;
        public bool Broken;
    }

    public sealed class PrologueSite
    {
        public int Tile;
        public PrologueBuildingId Id;
        public int WeeksDone;
        public int WeeksTotal;
    }

    public sealed class PrologueBoardState
    {
        public readonly List<PrologueTile> Tiles = new();
        public readonly List<PrologueSite> Sites = new();
        public readonly HashSet<int> FirstIceTiles = new();
        /// <summary>Non-first ice pads for step 4 (seeded or revealed after step 3).</summary>
        public readonly HashSet<int> SecondIceTiles = new();

        public PrologueTile At(int i) => Tiles[i];

        public int NeighborDistance(int a, int b)
        {
            // Filled by controller using ColonyTileGrid / hex distance.
            // For pure tests, inject via PrologueColony.SetDistanceFn.
            return 0;
        }
    }
}
