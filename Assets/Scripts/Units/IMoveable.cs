using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public interface IMoveable
    {
        void MoveTo(Vector3 position);
        void MoveTo(Transform transform);
        void Stop();
    }
}