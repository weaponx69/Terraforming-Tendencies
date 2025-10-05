using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    public struct ClosestCommandPostComparer : IComparer<BaseBuilding>
    {
        private Vector3 targetPosition;

        public ClosestCommandPostComparer(Vector3 position)
        {
            targetPosition = position;
        }

        public int Compare(BaseBuilding x, BaseBuilding y)
        {
            return (x.transform.position - targetPosition).sqrMagnitude
                .CompareTo((y.transform.position - targetPosition).sqrMagnitude);
        }
    }
}