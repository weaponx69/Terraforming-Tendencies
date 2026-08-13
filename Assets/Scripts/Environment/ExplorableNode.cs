using UnityEngine;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.EventBus;

namespace GameDevTV.RTS.Environment
{
    public class ExplorableNode : MonoBehaviour
    {
        public SectorNode NodeData;
        public int SectorIndex;

        public void TryExplore()
        {
            if (NodeData == null || NodeData.isExplored) return;

            // Check if player has at least 1 energy
            float currentEnergy = Supplies.Energy != null && Supplies.Energy.TryGetValue(Units.Owner.Player1, out float e) ? e : 0f;
            if (currentEnergy >= 1f)
            {
                // Deduct 1 energy
                Supplies.UpdateEnergy(Units.Owner.Player1, currentEnergy - 1f);

                // Explore it
                ExplorationManager.Instance?.ExploreNode(NodeData, SectorIndex);

                // Notify GameFlowManager
                GameFlowManager.Instance?.PlayerActed();
            }
            else
            {
                Debug.Log("[ExplorableNode] Not enough Energy to explore!");
            }
        }
    }
}
