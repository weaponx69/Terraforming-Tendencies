using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Time-sliced shroud hex reveals and amortized resource-fog sweeps (survey confirm safe).
    /// </summary>
    public sealed class PrologueVisionThrottle : MonoBehaviour
    {
        public static PrologueVisionThrottle Instance { get; private set; }

        public const int MaxHexRevealsPerFrame = 16;
        public const int MaxFogTilesPerFrame = 8;
        public const int HiddenResourceChecksPerFrame = 48;
        public const int SectorNodeChecksPerFrame = 24;

        /// <summary>Tiles revealed synchronously on ✓ confirm — rest stream in via slices.</summary>
        public const int ConfirmImmediateTileBurst = 10;

        readonly HashSet<Vector2Int> pendingHexCells = new();
        readonly Queue<Vector2Int> hexRevealOrder = new();
        readonly HashSet<int> pendingFogTiles = new();
        readonly List<int> fogTilePull = new(16);

        readonly PrologueResourceFog.ResourceFogRevealJob fogJob = new();
        PrologueController fogCtrl;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (UnityEngine.Object.FindAnyObjectByType<PrologueVisionThrottle>() != null) return;
            var go = new GameObject("PrologueVisionThrottle");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<PrologueVisionThrottle>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!PrologueController.IsActive) return;
            TickSlice();
        }

        public bool HasPending =>
            hexRevealOrder.Count > 0
            || pendingFogTiles.Count > 0
            || !IsFogJobIdle()
            || (PrologueHexPathQueue.Instance != null && PrologueHexPathQueue.Instance.HasPendingJobs);

        public static void QueueHexReveal(Vector3 world) => QueueHexRevealCell(ColonyTileGrid.WorldToCell(world));

        public static void QueueHexRevealCell(Vector2Int cell)
        {
            EnsureInstance();
            if (Instance == null) return;
            if (!Instance.pendingHexCells.Add(cell)) return;
            Instance.hexRevealOrder.Enqueue(cell);
        }

        /// <summary>Merge survey tiles into one batched fog job (deduped).</summary>
        public static void QueueSurveyFog(PrologueController ctrl, IList<int> tileIndices)
        {
            if (ctrl == null || tileIndices == null || tileIndices.Count == 0) return;
            EnsureInstance();
            if (Instance == null) return;

            Instance.fogCtrl = ctrl;
            for (int i = 0; i < tileIndices.Count; i++)
                Instance.pendingFogTiles.Add(tileIndices[i]);
        }

        /// <summary>One frame of vision work — matches production Update cost.</summary>
        public static double TickOneSliceMs()
        {
            EnsureInstance();
            if (Instance == null) return 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Instance.TickSlice();
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>Diagnostics: drain with per-frame budgets (no monolithic flush).</summary>
        public static void PumpPendingUntilIdle(int maxSlices = 512)
        {
            EnsureInstance();
            if (Instance == null) return;
            for (int i = 0; i < maxSlices && Instance.HasPending; i++)
                Instance.TickSlice();
        }

        /// <summary>Immediate path — perf soak only.</summary>
        public static void RevealSurveyFogNow(PrologueController ctrl, IList<int> tileIndices)
        {
            if (ctrl == null || tileIndices == null || tileIndices.Count == 0) return;
            PrologueResourceFog.RevealAlongSurveyPath(ctrl, tileIndices);
        }

        /// <summary>Confirm frame: nearest path tiles + shroud + deposit fog, without freezing the trail.</summary>
        public static void FlushSurveyConfirmImmediate(PrologueController ctrl, IList<int> path)
        {
            if (ctrl == null || path == null || path.Count == 0) return;
            EnsureInstance();
            if (Instance == null) return;

            int n = Mathf.Min(ConfirmImmediateTileBurst, path.Count);
            var immediate = new List<int>(n);
            for (int i = 0; i < n; i++)
                immediate.Add(path[i]);

            ctrl.Colony.RevealTiles(immediate);
            PrologueResourceFog.RevealDepositFogImmediate(ctrl, immediate);
            RevealHexShroudForTiles(ctrl, immediate);
            PrologueResourceFog.DropPendingFogTiles(Instance.fogJob, ctrl, immediate);
            for (int i = 0; i < immediate.Count; i++)
                Instance.pendingFogTiles.Remove(immediate[i]);

            PrologueSearchedTiles.Instance?.PaintTrailTiles(immediate, immediateGpu: true);

            const int extraSlices = 3;
            for (int s = 0; s < extraSlices; s++)
                Instance.ProcessFogRevealCore();
        }

        static void RevealHexShroudForTiles(PrologueController ctrl, IList<int> tiles)
        {
            var hex = HexGridManager.Instance;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (!ctrl.TryGetCell(tiles[i], out Vector2Int cell)) continue;
                Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
                if (hex != null)
                    hex.RevealHex(world);
                else
                    QueueHexReveal(world);
            }
        }

        [System.Obsolete("Use TickOneSliceMs or PumpPendingUntilIdle — avoids confirm-frame spikes.")]
        public static void FlushPendingNow() => PumpPendingUntilIdle();

        static void EnsureInstance()
        {
            if (Instance != null) return;
            Ensure();
        }

        void TickSlice()
        {
            PrologueHexPathQueue.Instance?.PumpProductionFrame();
            ProcessHexReveals();
            ProcessFogReveal();
            PrologueSearchedTiles.Instance?.ApplyDeferredOverlay();
            var overlay = ProloguePathGroundOverlay.Instance;
            if (overlay != null && overlay.NeedsGpuUpload)
                overlay.TryApplyNow(force: false);
        }

        void ProcessHexReveals()
        {
            if (hexRevealOrder.Count == 0) return;
            var hex = HexGridManager.Instance;
            if (hex == null) return;

            int n = 0;
            while (n < MaxHexRevealsPerFrame && hexRevealOrder.Count > 0)
            {
                Vector2Int cell = hexRevealOrder.Dequeue();
                pendingHexCells.Remove(cell);
                Vector3 world = ColonyTileGrid.CellToWorld(cell, 0f);
                hex.RevealHex(world);
                n++;
            }
        }

        void ProcessFogReveal() => ProcessFogRevealCore();

        void ProcessFogRevealCore()
        {
            if (fogCtrl == null)
                fogCtrl = PrologueController.Instance;

            if (pendingFogTiles.Count > 0)
            {
                fogTilePull.Clear();
                int ingested = 0;
                foreach (int tile in pendingFogTiles)
                {
                    if (ingested >= MaxFogTilesPerFrame) break;
                    fogTilePull.Add(tile);
                    ingested++;
                }

                for (int i = 0; i < fogTilePull.Count; i++)
                    pendingFogTiles.Remove(fogTilePull[i]);

                PrologueResourceFog.AppendSurveyCenters(fogCtrl, fogJob, fogTilePull);
            }

            if (fogJob.Centers.Count == 0 && fogJob.PendingNewCenters.Count == 0)
                return;

            bool done = PrologueResourceFog.AdvanceRevealJob(
                fogJob,
                fogCtrl,
                HiddenResourceChecksPerFrame,
                SectorNodeChecksPerFrame);

            if (done)
                PrologueResourceFog.ClearRevealJob(fogJob);
        }

        bool IsFogJobIdle() =>
            fogJob.Centers.Count == 0
            && fogJob.PendingNewCenters.Count == 0
            && pendingFogTiles.Count == 0;
    }
}