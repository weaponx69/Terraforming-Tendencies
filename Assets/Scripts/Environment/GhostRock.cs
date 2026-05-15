using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public class GhostRock : MonoBehaviour
    {
        public Transform TargetRock;

        private void Update()
        {
            if (TargetRock == null)
            {
                Destroy(gameObject);
            }
        }
    }
}
