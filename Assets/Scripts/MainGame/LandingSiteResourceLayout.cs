using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Orbital survey picks a landing cell where hydrology and ore clusters fit in one walkable corridor.
    /// No hard "max hex from camp" gameplay rule — distance is set by how the landing basin was laid out.
    /// </summary>
    public sealed class LandingSiteResourceLayout
    {
        /// <summary>Planner puts deposits at most this many hexes from the landing cell (generation only).</summary>
        public const int SurveyCorridorMaxHex = 11;

        /// <summary>Step-0 ice must sit inside one Survey Team leg from camp (5 hex paint + modest overage).</summary>
        public const int FirstObjectiveIceMaxHex = 6;

        static readonly Vector2Int[] HexSteps =
        {
            new(0, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1)
        };

        public static Vector2Int ActiveLandingCell { get; private set; }

        public static bool TryGetActiveLandingWorld(out Vector3 world)
        {
            world = default;
            if (ActiveLandingCell == default) return false;
            world = ColonyTileGrid.CellToWorldOnGround(ActiveLandingCell);
            world.y = ColonyTileGrid.SampleGroundY(world);
            return true;
        }

        public static void SetActiveLandingCell(Vector2Int cell) => ActiveLandingCell = cell;

        /// <summary>
        /// Main game boot when sector nodes already exist (baked planet): pick landing and seat sector-0 pads on it.
        /// </summary>
        public static void EnsureMainGameLandingChosen()
        {
            if (SectorManager.Instance == null || SectorManager.Instance.Sectors.Count == 0)
                return;

            var s0 = SectorManager.Instance.Sectors[0];
            if (s0 == null) return;

            Vector2Int hub = ColonyTileGrid.WorldToCell(s0.Center);
            if (ActiveLandingCell == default)
                PickLandingCell(hub);

            if (!TryGetActiveLandingWorld(out Vector3 land))
                return;

            s0.Center = land;
            foreach (var site in s0.BuildingSites)
            {
                if (site == null || site.Kind != BuildingSiteKind.CommandPost) continue;
                site.Position = ColonyTileGrid.CellToWorldOnGround(ActiveLandingCell);
                site.Position.y = ColonyTileGrid.SampleGroundY(site.Position);
                if (site.MarkerGO != null)
                    site.MarkerGO.transform.position = site.Position;
                break;
            }
        }

        public Vector2Int CommandCell { get; }
        public Vector2Int IcePrimary { get; }
        public Vector2Int IceSecondary { get; }
        public IReadOnlyList<Vector2Int> MineralVein { get; }
        public Vector2Int IronBody { get; }
        public Vector2Int GasFissure { get; }
        public Vector2Int RegolithField { get; }
        public Vector2Int FeatureCell { get; }

        LandingSiteResourceLayout(
            Vector2Int command,
            Vector2Int iceA,
            Vector2Int iceB,
            List<Vector2Int> vein,
            Vector2Int iron,
            Vector2Int gas,
            Vector2Int regolith,
            Vector2Int feature)
        {
            CommandCell = command;
            IcePrimary = iceA;
            IceSecondary = iceB;
            MineralVein = vein;
            IronBody = iron;
            GasFissure = gas;
            RegolithField = regolith;
            FeatureCell = feature;
        }

        /// <summary>
        /// Choose the landing hex in sector 0 where the resource corridor is compact and step-0 ice is reachable.
        /// </summary>
        public static Vector2Int PickLandingCell(Vector2Int sectorCenterCell, int searchRadius = 7)
        {
            float bestScore = float.MaxValue;
            Vector2Int best = sectorCenterCell;
            LandingSiteResourceLayout bestLayout = Create(sectorCenterCell);
            for (int dq = -searchRadius; dq <= searchRadius; dq++)
            {
                for (int dr = -searchRadius; dr <= searchRadius; dr++)
                {
                    var candidate = new Vector2Int(sectorCenterCell.x + dq, sectorCenterCell.y + dr);
                    var layout = Create(candidate);
                    float score = WinabilityScore(layout);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestLayout = layout;
                    }
                }
            }

            SetActiveLandingCell(best);
            if (bestLayout.PrimaryIceHexDist() > FirstObjectiveIceMaxHex)
            {
                Debug.LogWarning(
                    $"[LandingSite] Picked {best} but primary ice is {bestLayout.PrimaryIceHexDist()} hex — " +
                    $"tighten planet search or FirstObjectiveIceMaxHex.");
            }

            return best;
        }

        /// <summary>Lower is better: prioritize close first ice, then overall corridor spread.</summary>
        public static float WinabilityScore(LandingSiteResourceLayout layout)
        {
            if (layout == null) return float.MaxValue;
            int ice = layout.PrimaryIceHexDist();
            int far = layout.FarthestResourceHex();
            if (ice > FirstObjectiveIceMaxHex)
                return 10_000f + ice * 50f + far;
            return ice * 8f + far;
        }

        public static LandingSiteResourceLayout ForHomeCell(Vector2Int homeCell)
        {
            if (ActiveLandingCell != default && homeCell == ActiveLandingCell)
                return Create(ActiveLandingCell);
            if (ActiveLandingCell != default)
                return Create(ActiveLandingCell);
            return Create(homeCell);
        }

        /// <summary>Deterministic from landing cell — planet gen and runtime bootstrap share the same layout.</summary>
        public static LandingSiteResourceLayout Create(Vector2Int commandCell)
        {
            var state = Random.state;
            Random.InitState(commandCell.x * 73856093 ^ commandCell.y * 19349663 ^ 0x5A3C2E1);

            int hydrologySide = Random.Range(0, 6);
            int hydrologyRing = Random.Range(4, FirstObjectiveIceMaxHex + 1);
            Vector2Int iceA = RingCell(commandCell, hydrologyRing, hydrologySide, Random.Range(0, Mathf.Max(1, hydrologyRing)));
            Vector2Int iceB = iceA + HexSteps[(hydrologySide + 1) % 6];
            if (ColonyTileGrid.HexDistance(commandCell, iceB) > SurveyCorridorMaxHex)
                iceB = iceA + HexSteps[(hydrologySide + 5) % 6];

            int oreSide = (hydrologySide + 2) % 6;
            int oreRing = Random.Range(4, SurveyCorridorMaxHex + 1);
            Vector2Int veinStart = RingCell(commandCell, oreRing, oreSide, 0);
            var vein = new List<Vector2Int>(3)
            {
                veinStart,
                veinStart + HexSteps[oreSide],
                veinStart + HexSteps[oreSide] * 2
            };

            Vector2Int iron = vein[2] + HexSteps[(oreSide + 1) % 6];
            int gasSide = (hydrologySide + 4) % 6;
            int gasRing = Random.Range(3, SurveyCorridorMaxHex);
            Vector2Int gas = RingCell(commandCell, gasRing, gasSide, Random.Range(0, Mathf.Max(1, gasRing)));
            int regolithSide = (oreSide + 3) % 6;
            Vector2Int regolith = RingCell(commandCell, Random.Range(3, 7), regolithSide, 0);
            Vector2Int feature = iceA;

            Random.state = state;
            return new LandingSiteResourceLayout(commandCell, iceA, iceB, vein, iron, gas, regolith, feature);
        }

        public int FarthestResourceHex()
        {
            int max = 0;
            max = Mathf.Max(max, HexDist(IcePrimary));
            max = Mathf.Max(max, HexDist(IceSecondary));
            foreach (var v in MineralVein)
                max = Mathf.Max(max, HexDist(v));
            max = Mathf.Max(max, HexDist(IronBody), HexDist(GasFissure), HexDist(RegolithField), HexDist(FeatureCell));
            return max;
        }

        public int PrimaryIceHexDist() => HexDist(IcePrimary);

        int HexDist(Vector2Int cell) => ColonyTileGrid.HexDistance(CommandCell, cell);

        /// <summary>Ensure lazy board tiles exist for camp, ice, and corridor deposits (matches planet gen layout).</summary>
        public void RegisterBasinOnBoard(System.Action<Vector2Int> registerCell)
        {
            if (registerCell == null) return;
            registerCell(CommandCell);
            registerCell(IcePrimary);
            registerCell(IceSecondary);
            foreach (var v in MineralVein)
                registerCell(v);
            registerCell(IronBody);
            registerCell(GasFissure);
            registerCell(RegolithField);
            registerCell(FeatureCell);
        }

        public static Vector2Int RingCell(Vector2Int center, int ring, int startSide, int stepAlongSide)
        {
            if (ring <= 0) return center;
            Vector2Int hex = center + HexSteps[4] * ring;
            for (int i = 0; i < startSide; i++)
            {
                for (int j = 0; j < ring; j++)
                    hex += HexSteps[i];
            }

            stepAlongSide = Mathf.Clamp(stepAlongSide, 0, ring);
            for (int j = 0; j < stepAlongSide; j++)
                hex += HexSteps[startSide];
            return hex;
        }

        public Vector3 WorldAt(Vector2Int cell) => ColonyTileGrid.CellToWorldOnGround(cell);

        public bool IsLandingBasinCell(Vector2Int cell)
        {
            int corridor = FarthestResourceHex() + 1;
            if (ColonyTileGrid.HexDistance(CommandCell, cell) > corridor)
                return false;
            if (cell == IcePrimary || cell == IceSecondary) return true;
            foreach (var v in MineralVein)
                if (v == cell) return true;
            if (cell == IronBody || cell == GasFissure || cell == RegolithField || cell == FeatureCell)
                return true;
            return ColonyTileGrid.HexDistance(cell, IcePrimary) <= 2
                   || ColonyTileGrid.HexDistance(cell, MineralVein[0]) <= 2
                   || ColonyTileGrid.HexDistance(cell, GasFissure) <= 1;
        }

        public void ApplyIcePads(MainGameColony colony, IList<int> existingIceTiles, int minCount)
        {
            if (colony == null) return;
            var targets = new[] { IcePrimary, IceSecondary };
            for (int i = 0; i < targets.Length && existingIceTiles.Count < minCount; i++)
            {
                Vector3 world = WorldAt(targets[i]);
                int tile = RegisterOrGetTile(world);
                if (tile < 0) continue;
                var pad = colony.Board.At(tile);
                if (pad.Feature == MainGameFeature.Ice || pad.Feature == MainGameFeature.Glacier)
                {
                    if (!existingIceTiles.Contains(tile))
                        existingIceTiles.Add(tile);
                    continue;
                }
                if (pad.Building != MainGameBuildingId.None && pad.Building != MainGameBuildingId.CommandPost)
                    continue;
                pad.Feature = MainGameFeature.Ice;
                pad.DepositLeft = 300;
                pad.Revealed = false;
                existingIceTiles.Add(tile);
            }
        }

        static int RegisterOrGetTile(Vector3 world)
        {
            var ctrl = MainGameController.Instance;
            if (ctrl == null || !ctrl.TryGetTileIndex(world, out int tile)) return -1;
            return tile;
        }

        public void SnapGatherableNodes(MainGameController ctrl, IList<SectorNode> nodes)
        {
            if (ctrl == null || nodes == null) return;

            var mineralQueue = new Queue<Vector2Int>(MineralVein);
            foreach (var node in nodes)
            {
                if (node == null || node.isExplored) continue;
                if (node.type == SectorNode.NodeType.Nexus) continue;

                Vector2Int? cell = null;
                switch (node.type)
                {
                    case SectorNode.NodeType.Minerals when mineralQueue.Count > 0:
                        cell = mineralQueue.Dequeue();
                        break;
                    case SectorNode.NodeType.Iron:
                        cell = IronBody;
                        break;
                    case SectorNode.NodeType.Gas:
                        cell = GasFissure;
                        break;
                    case SectorNode.NodeType.Regolith:
                        cell = RegolithField;
                        break;
                    case SectorNode.NodeType.Feature:
                        cell = FeatureCell;
                        break;
                }

                if (!cell.HasValue) continue;
                SnapNodeToTile(ctrl, node, cell.Value);
            }
        }

        static void SnapNodeToTile(MainGameController ctrl, SectorNode node, Vector2Int cell)
        {
            Vector3 world = ColonyTileGrid.CellToWorldOnGround(cell);
            world.y = ColonyTileGrid.SampleGroundY(world);
            node.position = world;
            ctrl.TryGetTileIndex(world, out _);
        }

        public static string LandingFlavorLine =>
            "Dropship on the orbital survey's landing ellipse — water and ore clusters were already mapped in this basin.";
    }
}