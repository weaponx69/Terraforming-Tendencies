using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>Prologue suggest ghost — maps the visible drill mesh to the board tile you can build on.</summary>
    public class PrologueRevealGhostPad : MonoBehaviour
    {
        public int BoardTileIndex = -1;

        public static bool TryGetPadNear(Vector3 world, float maxHorizontalDist, out int boardTile, out Vector3 padWorld)
        {
            boardTile = -1;
            padWorld = default;
            float bestSq = maxHorizontalDist * maxHorizontalDist;
            var pads = Object.FindObjectsByType<PrologueRevealGhostPad>(FindObjectsInactive.Exclude);
            for (int i = 0; i < pads.Length; i++)
            {
                var pad = pads[i];
                if (pad == null || pad.BoardTileIndex < 0) continue;
                float sq = ColonyTileGrid.HorizontalDistSq(world, pad.transform.position);
                if (sq > bestSq) continue;
                bestSq = sq;
                boardTile = pad.BoardTileIndex;
                padWorld = pad.transform.position;
            }
            return boardTile >= 0;
        }
    }
}