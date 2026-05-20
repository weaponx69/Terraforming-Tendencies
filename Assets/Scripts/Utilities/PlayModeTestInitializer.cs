using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    public static class PlayModeTestInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Limit target frame rate to 60 FPS to prevent test runners running at 1000+ FPS 
            // from executing frame-based yield loops too fast for pathfinding and physics.
            Application.targetFrameRate = 60;
        }
    }
}
