using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public class LifeSupportNode : MonoBehaviour
    {
        [Tooltip("Radius within which buildings are protected from decay.")]
        public float Radius = 15f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}
