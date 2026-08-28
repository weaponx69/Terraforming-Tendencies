using UnityEngine;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class ExplorableNode : MonoBehaviour
    {
        public SectorNode NodeData;
        public int SectorIndex;

        public void TryExplore()
        {
            if (NodeData == null || NodeData.isExplored) return;

            if (CardDeckController.Instance == null)
            {
                Debug.Log("[ExplorableNode] Card deck is not ready.");
                return;
            }

            CardDeckController.Instance.TryExploreAtNode(NodeData, SectorIndex);
        }
    }
}
