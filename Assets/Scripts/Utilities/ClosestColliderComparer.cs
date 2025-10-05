using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    public struct ClosestColliderComparer : IComparer<Collider>
    {
        private Vector3 targetPosition;

        public ClosestColliderComparer(Vector3 position)
        {
            targetPosition = position;
        }

        public int Compare(Collider x, Collider y)
        {
            return (x.transform.position - targetPosition).sqrMagnitude
                .CompareTo((y.transform.position - targetPosition).sqrMagnitude);
        }
    }
}