using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Marks a building as providing life-support decay protection within a radius.
    /// </summary>
    [IncludeInSettings(true)]
    public class LifeSupportNode : MonoBehaviour
    {
        public static readonly List<LifeSupportNode> ActiveNodes = new List<LifeSupportNode>();

        private void OnEnable()
        {
            if (!ActiveNodes.Contains(this)) ActiveNodes.Add(this);
        }

        private void OnDisable()
        {
            ActiveNodes.Remove(this);
        }

        /// <summary>Radius within which buildings are protected from decay.</summary>
        [Inspectable]
        [Tooltip("Radius within which buildings are protected from decay.")]
        public float Radius = 15f;

        /// <summary>Total number of active life-support nodes in the scene.</summary>
        [Inspectable]
        public static int ActiveNodeCount => ActiveNodes.Count;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}
