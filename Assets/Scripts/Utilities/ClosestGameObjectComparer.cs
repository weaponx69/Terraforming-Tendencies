using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    public struct ClosestGameObjectComparer : IComparer<GameObject>
    {
        private Vector3 targetPosition;

        public ClosestGameObjectComparer(Vector3 position)
        {
            targetPosition = position;
        }

        public int Compare(GameObject x, GameObject y)
        {
            return (x.transform.position - targetPosition).sqrMagnitude
                .CompareTo((y.transform.position - targetPosition).sqrMagnitude);
        }
    }
}