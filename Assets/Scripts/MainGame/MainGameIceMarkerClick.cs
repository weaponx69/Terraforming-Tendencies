using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>Click target on a floating "?" ice marker.</summary>
    public class MainGameIceMarkerClick : MonoBehaviour
    {
        public int TileIndex { get; private set; } = -1;

        public void Setup(int tileIndex) => TileIndex = tileIndex;

        void OnEnable() => MainGameInteractableIndex.Register(this);
        void OnDisable() => MainGameInteractableIndex.Unregister(this);
    }
}
