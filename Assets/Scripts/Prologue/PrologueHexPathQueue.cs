using System;
using System.Collections.Generic;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Time-sliced hex BFS for survey paint paths — avoids full-board search on the click frame.
    /// </summary>
    public sealed class PrologueHexPathQueue : MonoBehaviour
    {
        public static PrologueHexPathQueue Instance { get; private set; }

        public const int ExpansionsPerFrame = 72;

        sealed class PathJob
        {
            public PrologueController Ctrl;
            public PrologueColony Colony;
            public int Start;
            public int Goal;
            public bool TryAltGoals;
            public Action<List<int>, bool> OnComplete;

            public int[] Prev;
            public int[] Queue;
            public int Head;
            public int Tail;
            public bool Searching;
            public int ActiveGoal;
            public int AltGoalIndex;
            public List<int> AltGoals;
            public int BestAltLen = int.MaxValue;
            public List<int> BestAltPath;
        }

        readonly List<PathJob> jobs = new(4);
        readonly List<Vector2Int> neighborScratch = new(6);

        int previewStart = -1;
        int previewGoal = -1;
        List<int> previewPath;
        bool previewReady;
        PathJob activePreviewJob;
        int pendingMarkTarget = -1;
        PrologueDroneScan pendingScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (UnityEngine.Object.FindAnyObjectByType<PrologueHexPathQueue>() != null) return;
            var go = new GameObject("PrologueHexPathQueue");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<PrologueHexPathQueue>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Pumped from <see cref="PrologueVisionThrottle"/> — no dedicated Update().</summary>
        public void PumpProductionFrame()
        {
            if (jobs.Count > 0)
                Pump(ExpansionsPerFrame);
        }

        /// <summary>Diagnostics / eval: advance time-sliced jobs without waiting for Unity frames.</summary>
        public int Pump(int expansionBudget)
        {
            if (jobs.Count == 0 || expansionBudget <= 0) return 0;

            int budget = expansionBudget;
            for (int j = jobs.Count - 1; j >= 0 && budget > 0; j--)
            {
                var job = jobs[j];
                while (budget > 0 && job.Searching)
                {
                    if (job.Head >= job.Tail)
                    {
                        FinishSearchPhase(job);
                        break;
                    }

                    int cur = job.Queue[job.Head++];
                    if (cur == job.ActiveGoal)
                    {
                        CompleteJob(job, RebuildPath(job, job.ActiveGoal), true);
                        jobs.RemoveAt(j);
                        budget--;
                        break;
                    }

                    if (!job.Ctrl.TryGetCell(cur, out Vector2Int cell)) continue;
                    neighborScratch.Clear();
                    ColonyTileGrid.CollectOrthogonalNeighborCells(cell, neighborScratch);
                    int n = job.Colony.Board.Tiles.Count;

                    for (int i = 0; i < neighborScratch.Count; i++)
                    {
                        if (!job.Ctrl.TryGetTileIndex(
                                ColonyTileGrid.CellToWorldOnGround(neighborScratch[i]), out int next))
                            continue;
                        if (next < 0 || next >= n || job.Prev[next] != -2) continue;
                        job.Prev[next] = cur;
                        job.Queue[job.Tail++] = next;
                    }

                    budget--;
                }
            }

            return expansionBudget - budget;
        }

        public bool HasPendingJobs => jobs.Count > 0;

        public int PumpUntilIdle(int maxExpansions = 500_000)
        {
            int spent = 0;
            while (jobs.Count > 0 && spent < maxExpansions)
                spent += Pump(Mathf.Min(ExpansionsPerFrame * 4, maxExpansions - spent));
            return spent;
        }

        public void RequestPath(
            PrologueController ctrl,
            PrologueColony colony,
            int start,
            int goal,
            Action<List<int>, bool> onComplete,
            bool tryAltGoals = true)
        {
            if (ctrl == null || colony == null || start < 0 || goal < 0)
            {
                onComplete?.Invoke(null, false);
                return;
            }

            var job = BeginJob(ctrl, colony, start, goal, tryAltGoals, onComplete);
            jobs.Insert(0, job);
        }

        public void RequestPreviewPath(PrologueController ctrl, PrologueColony colony, int start, int goal)
        {
            if (ctrl == null || colony == null || start < 0 || goal < 0 || start == goal)
            {
                previewStart = start;
                previewGoal = goal;
                previewPath = null;
                previewReady = false;
                return;
            }

            if (previewReady && previewStart == start && previewGoal == goal && previewPath != null)
                return;
            if (activePreviewJob != null
                && previewStart == start
                && previewGoal == goal)
                return;

            previewStart = start;
            previewGoal = goal;
            previewPath = null;
            previewReady = false;

            if (activePreviewJob != null)
            {
                jobs.Remove(activePreviewJob);
                activePreviewJob = null;
            }

            activePreviewJob = BeginJob(ctrl, colony, start, goal, true, (path, ok) =>
            {
                activePreviewJob = null;
                if (previewStart != start || previewGoal != goal) return;
                previewPath = path;
                previewReady = ok;
            });
            jobs.Add(activePreviewJob);
        }

        /// <summary>Paint path toward a ? mark — completes on a later frame.</summary>
        public void RequestMarkPath(PrologueDroneScan scan, PrologueController ctrl, PrologueColony colony, int start, int target)
        {
            if (scan == null || ctrl == null || colony == null || start < 0 || target < 0) return;

            pendingScan = scan;
            pendingMarkTarget = target;

            RequestPath(ctrl, colony, start, target, (path, ok) =>
            {
                int mark = pendingMarkTarget;
                var s = pendingScan;
                pendingMarkTarget = -1;
                pendingScan = null;
                if (s == null || mark != target) return;
                s.ApplyMarkPath(mark, path, ok);
            }, tryAltGoals: true);
        }

        public bool TryGetPreviewPath(int start, int goal, out List<int> path)
        {
            path = null;
            if (!previewReady || previewStart != start || previewGoal != goal) return false;
            path = previewPath;
            return path != null && path.Count > 1;
        }

        /// <summary>Playtest / tiny-board fallback when the queue is not running.</summary>
        public static bool TryFindPathNow(
            PrologueController ctrl,
            PrologueColony colony,
            int start,
            int goal,
            out List<int> path,
            bool tryAltGoals = true)
        {
            path = null;
            if (ctrl == null || colony == null) return false;
            if (TryBfs(ctrl, colony, start, goal, out path))
                return true;
            if (!tryAltGoals || !ctrl.TryGetCell(goal, out Vector2Int goalCell)) return false;

            var neighbors = new List<Vector2Int>(6);
            ColonyTileGrid.CollectOrthogonalNeighborCells(goalCell, neighbors);
            int bestLen = int.MaxValue;
            List<int> bestPath = null;
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(neighbors[i]), out int alt)) continue;
                if (!TryBfs(ctrl, colony, start, alt, out var altPath)) continue;
                if (altPath.Count >= bestLen) continue;
                bestLen = altPath.Count;
                bestPath = altPath;
            }

            if (bestPath == null) return false;
            path = bestPath;
            return true;
        }

        static bool TryBfs(PrologueController ctrl, PrologueColony colony, int start, int goal, out List<int> path)
        {
            path = null;
            int n = colony.Board.Tiles.Count;
            if (start < 0 || goal < 0 || start >= n || goal >= n) return false;

            var prev = new int[n];
            for (int i = 0; i < n; i++) prev[i] = -2;

            var q = new Queue<int>();
            q.Enqueue(start);
            prev[start] = -1;
            var neighbors = new List<Vector2Int>(6);
            bool found = false;

            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                if (cur == goal)
                {
                    found = true;
                    break;
                }

                if (!ctrl.TryGetCell(cur, out Vector2Int cell)) continue;
                neighbors.Clear();
                ColonyTileGrid.CollectOrthogonalNeighborCells(cell, neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (!ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(neighbors[i]), out int next)) continue;
                    if (next < 0 || next >= n || prev[next] != -2) continue;
                    prev[next] = cur;
                    q.Enqueue(next);
                }
            }

            if (!found) return false;

            var rev = new List<int>();
            for (int cur = goal; cur >= 0; cur = prev[cur])
                rev.Add(cur);
            rev.Reverse();
            path = rev;
            return path.Count > 1;
        }

        static PathJob BeginJob(
            PrologueController ctrl,
            PrologueColony colony,
            int start,
            int goal,
            bool tryAltGoals,
            Action<List<int>, bool> onComplete)
        {
            int n = colony.Board.Tiles.Count;
            var job = new PathJob
            {
                Ctrl = ctrl,
                Colony = colony,
                Start = start,
                Goal = goal,
                TryAltGoals = tryAltGoals,
                OnComplete = onComplete,
                Prev = new int[n],
                Queue = new int[n],
                ActiveGoal = goal,
            };
            for (int i = 0; i < n; i++) job.Prev[i] = -2;
            job.Queue[job.Tail++] = start;
            job.Prev[start] = -1;
            job.Searching = true;

            if (tryAltGoals && ctrl.TryGetCell(goal, out Vector2Int goalCell))
            {
                job.AltGoals = new List<int>(6);
                var neighbors = new List<Vector2Int>(6);
                ColonyTileGrid.CollectOrthogonalNeighborCells(goalCell, neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (ctrl.TryGetTileIndex(ColonyTileGrid.CellToWorldOnGround(neighbors[i]), out int alt))
                        job.AltGoals.Add(alt);
                }
            }

            return job;
        }

        void FinishSearchPhase(PathJob job)
        {
            if (!job.TryAltGoals || job.AltGoals == null || job.AltGoals.Count == 0)
            {
                CompleteJob(job, null, false);
                jobs.Remove(job);
                return;
            }

            job.AltGoalIndex = 0;
            TryNextAltGoal(job);
        }

        void TryNextAltGoal(PathJob job)
        {
            while (job.AltGoalIndex < job.AltGoals.Count)
            {
                int alt = job.AltGoals[job.AltGoalIndex++];
                if (TryBfs(job.Ctrl, job.Colony, job.Start, alt, out var altPath))
                {
                    if (altPath.Count < job.BestAltLen)
                    {
                        job.BestAltLen = altPath.Count;
                        job.BestAltPath = altPath;
                    }
                }
            }

            if (job.BestAltPath != null)
                CompleteJob(job, job.BestAltPath, true);
            else
                CompleteJob(job, null, false);
            jobs.Remove(job);
        }

        static List<int> RebuildPath(PathJob job, int goal)
        {
            var rev = new List<int>();
            for (int cur = goal; cur >= 0; cur = job.Prev[cur])
                rev.Add(cur);
            rev.Reverse();
            return rev;
        }

        static void CompleteJob(PathJob job, List<int> path, bool ok)
        {
            job.OnComplete?.Invoke(path, ok);
        }
    }
}