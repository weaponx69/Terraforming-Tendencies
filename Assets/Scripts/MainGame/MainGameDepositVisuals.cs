using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>Keep deposit dots / labels on the terrain mesh (slopes, hills).</summary>
    public static class MainGameDepositVisuals
    {
        public static void SeatNode(SectorNode node)
        {
            if (node == null) return;

            Vector2Int cell = ColonyTileGrid.WorldToCell(node.position);
            Vector3 ground = ColonyTileGrid.CellToWorldOnHighestGround(cell);
            node.position = ground;

            if (node.visualGO != null)
            {
                node.visualGO.transform.position = ground;
                FlattenDepositDot(node.visualGO);
                ReservedSiteBuildUtility.SeatVisualsOnGround(node.visualGO, 0.03f);
            }

            if (node.questionMarkGO != null)
            {
                float lift = MainGameIceMarkers.DepositFloatHeight;
                var root = node.questionMarkGO.transform;
                root.position = ground + Vector3.up * lift;
                if (root.TryGetComponent(out MainGameDepositMark mark))
                {
                    mark.GroundPoint = ground;
                    MainGameDepositMark.EnsureClickVolume(root, lift);
                }
            }
        }

        public static void SeatNodesForBoardTile(MainGameController ctrl, int tileIndex)
        {
            if (ctrl == null || !ctrl.TryGetCell(tileIndex, out Vector2Int cell)) return;
            if (SectorManager.Instance?.Sectors == null) return;
            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector?.Nodes == null) continue;
                foreach (var node in sector.Nodes)
                {
                    if (node == null) continue;
                    if (ctrl.TryGetTileIndex(node.position, out int onBoard) && onBoard == tileIndex)
                    {
                        SeatNode(node);
                        continue;
                    }

                    if (ColonyTileGrid.WorldToCell(node.position) == cell)
                        SeatNode(node);
                }
            }
        }

        static void FlattenDepositDot(GameObject dot)
        {
            if (dot == null) return;
            var scale = dot.transform.localScale;
            scale.y = Mathf.Min(scale.y, 0.08f);
            dot.transform.localScale = scale;
        }
    }
}