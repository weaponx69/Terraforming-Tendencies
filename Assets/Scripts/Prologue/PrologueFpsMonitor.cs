using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>In-game FPS averaged over ~1s (unaffected by CLI eval spam).</summary>
    public sealed class PrologueFpsMonitor : MonoBehaviour
    {
        public static PrologueFpsMonitor Instance { get; private set; }
        public static int LastSecondFps { get; private set; }

        /// <summary>Wall-time samples recorded during diagnostic bursts (eval cannot advance frames).</summary>
        public static float AvgFrameTimeMs { get; private set; }
        public static float MaxDispatchSpikeMs { get; private set; }

        static bool captureBurst;
        static float burstSumMs;
        static int burstCount;

        float accum;
        int frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("PrologueFpsMonitor");
            DontDestroyOnLoad(go);
            go.AddComponent<PrologueFpsMonitor>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (captureBurst)
                RecordSyntheticSpike(Time.unscaledDeltaTime * 1000f);

            accum += Time.unscaledDeltaTime;
            frames++;
            if (accum < 1f) return;
            LastSecondFps = Mathf.RoundToInt(frames / accum);
            accum = 0f;
            frames = 0;
        }

        public static void BeginBurstCapture()
        {
            captureBurst = true;
            burstSumMs = 0f;
            burstCount = 0;
            AvgFrameTimeMs = 0f;
            MaxDispatchSpikeMs = 0f;
        }

        public static void RecordSyntheticSpike(double milliseconds)
        {
            if (!captureBurst || milliseconds < 0.0001) return;
            float ms = (float)milliseconds;
            burstSumMs += ms;
            burstCount++;
            MaxDispatchSpikeMs = Mathf.Max(MaxDispatchSpikeMs, ms);
            AvgFrameTimeMs = burstSumMs / burstCount;
        }

        public static void EndBurstCapture()
        {
            captureBurst = false;
            if (burstCount > 0)
                AvgFrameTimeMs = burstSumMs / burstCount;
        }
    }
}