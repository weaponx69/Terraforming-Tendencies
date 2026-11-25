using System.Text;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>Playtest bot: survey-reveal a typed deposit and report grounding for clip checks.</summary>
    public static class PrologueDepositRevealPlaytest
    {
        public static bool TryRunClipAudit(out Vector3 focusWorld, out string report)
        {
            focusWorld = default;
            report = "no deposit";
            if (!PrologueController.IsActive) return false;

            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null) return false;

            SectorNode best = null;
            float bestDist = float.MaxValue;
            int home = 0;
            for (int i = 0; i < colony.Board.Tiles.Count; i++)
            {
                if (colony.Board.At(i).Building == PrologueBuildingId.CommandPost)
                {
                    home = i;
                    break;
                }
            }

            if (!ctrl.TryGetCell(home, out Vector2Int homeCell)) return false;

            var sector = SectorManager.Instance?.ActiveSector;
            if (sector?.Nodes == null) return false;

            foreach (var node in sector.Nodes)
            {
                if (node == null) continue;
                if (node.type is not (SectorNode.NodeType.Minerals or SectorNode.NodeType.Gas
                    or SectorNode.NodeType.Iron or SectorNode.NodeType.Regolith))
                    continue;
                if (!ctrl.TryGetTileIndex(node.position, out int tile)) continue;
                if (!ctrl.TryGetCell(tile, out Vector2Int cell)) continue;
                float d = ColonyTileGrid.HexDistance(homeCell, cell);
                if (d < 2) continue;
                if (d > bestDist) continue;
                bestDist = d;
                best = node;
            }

            if (best == null) return false;

            if (!ctrl.TryGetTileIndex(best.position, out int revealTile) || revealTile < 0)
                return false;
            PrologueResourceFog.RevealDepositFogImmediate(ctrl, new[] { revealTile });

            if (best.visualGO != null
                && best.visualGO.TryGetComponent(out HiddenResource hr))
                hr.ForceDiscover();

            ReservedSiteBuildUtility.SeatVisualsOnGround(best.visualGO, 0.05f);

            var hidden = Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include);
            for (int i = 0; i < hidden.Length; i++)
            {
                if (hidden[i] == null || !hidden[i].IsDiscovered) continue;
                ReservedSiteBuildUtility.SeatVisualsOnGround(hidden[i].gameObject, 0.05f);
            }

            focusWorld = best.position;
            report = BuildGroundReport(best);
            PlayerInput.FocusCameraOnWorldPosition(focusWorld + Vector3.up * 2f);
            return true;
        }

        static string BuildGroundReport(SectorNode node)
        {
            var sb = new StringBuilder();
            sb.Append(node.type).Append(" @ ").Append(node.position.ToString("F1"));
            float ground = ColonyTileGrid.SampleGroundY(node.position);
            sb.Append(" groundY=").Append(ground.ToString("F2"));

            if (node.visualGO != null)
            {
                float minY = float.PositiveInfinity;
                foreach (var r in node.visualGO.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !r.enabled) continue;
                    minY = Mathf.Min(minY, r.bounds.min.y);
                }

                if (!float.IsPositiveInfinity(minY))
                {
                    float gap = minY - ground;
                    sb.Append(" meshMinY=").Append(minY.ToString("F2"));
                    sb.Append(" gap=").Append(gap.ToString("F3"));
                    sb.Append(gap < -0.08f ? " CLIP" : gap > 0.35f ? " FLOAT" : " OK");
                }
            }

            return sb.ToString();
        }
    }
}