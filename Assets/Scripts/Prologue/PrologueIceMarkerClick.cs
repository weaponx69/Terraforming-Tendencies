using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>Click target on a floating "?" ice marker.</summary>
    public class PrologueIceMarkerClick : MonoBehaviour
    {
        public int TileIndex { get; private set; } = -1;

        public void Setup(int tileIndex) => TileIndex = tileIndex;
    }
}
