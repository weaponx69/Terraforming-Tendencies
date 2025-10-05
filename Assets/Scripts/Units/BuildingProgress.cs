using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [System.Serializable]
    public struct BuildingProgress
    {
        public enum BuildingState
        {
            Building,
            Paused,
            Completed,
            Destroyed
        }
        [field: SerializeField] public float StartTime { get; private set; }
        [field: SerializeField] public float Progress { get; private set; }
        [field: SerializeField] public BuildingState State { get; private set; }

        public BuildingProgress(BuildingState state, float startTime, float progress)
        {
            State = state;
            StartTime = startTime;
            Progress = progress;
        }
    }
}