using System.Collections.Generic;

namespace GameDevTV.RTS.MainGame
{
    public sealed class MainGameTile
    {
        public int Index;
        public MainGameFeature Feature;
        public int DepositLeft; // ice amount remaining
        public bool Revealed;
        public bool Lit; // reachable / power-lit ground for drone start
        public MainGameBuildingId Building;
        public bool Broken;
    }

    public sealed class MainGameSite
    {
        public int Tile;
        public MainGameBuildingId Id;
        public int WeeksDone;
        public int WeeksTotal;
    }

    public sealed class MainGameBoardState
    {
        public readonly List<MainGameTile> Tiles = new();
        public readonly List<MainGameSite> Sites = new();
        public readonly HashSet<int> FirstIceTiles = new();
        /// <summary>Non-first ice pads for step 4 (seeded or revealed after step 3).</summary>
        public readonly HashSet<int> SecondIceTiles = new();

        public MainGameTile At(int i) => Tiles[i];

        public int NeighborDistance(int a, int b)
        {
            // Filled by controller using ColonyTileGrid / hex distance.
            // For pure tests, inject via MainGameColony.SetDistanceFn.
            return 0;
        }
    }
}
