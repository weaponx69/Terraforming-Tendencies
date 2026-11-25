using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Survey Team job: after paint + ✓, colony days tick on the time-lapse clock while
    /// drones/crew walk the painted path <b>forward only</b> (no ping-pong), revealing as they go.
    /// Spec: docs/specs/2026-11-19-prologue-survey-team-design.md
    /// </summary>
    public class PrologueSurveyMission : MonoBehaviour
    {
        public static PrologueSurveyMission Instance { get; private set; }

        /// <summary>Prologue board is small — high cap so the player is not stopped at 5 hexes.</summary>
        public const int DefaultTileBudget = 99;
        public const int DefaultDays = 3;

        public bool IsSearching { get; private set; }
        public int DaysRequired { get; private set; }
        public float DaysElapsed { get; private set; }
        public int TileBudget { get; private set; } = DefaultTileBudget;

        readonly List<int> searchPath = new();
        readonly List<GameObject> team = new();
        readonly List<Vector3> waypoints = new();
        /// <summary>Next waypoint index along the painted path (0…Count-1). Never wraps backward.</summary>
        readonly List<int> pathIndex = new();
        int revealedThrough = -1;
        float dayAccum;
        GameObject teamRoot;
        readonly List<int> litVisualQueue = new(64);
        Coroutine litSpreadRoutine;
        const int LitVisualSpreadFrames = 3;
        const int LitVisualMinChunk = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Object.FindAnyObjectByType<PrologueSurveyMission>() != null) return;
            var go = new GameObject("PrologueSurveyMission");
            DontDestroyOnLoad(go);
            go.AddComponent<PrologueSurveyMission>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearTeam();
        }

        /// <summary>Called when the Survey Team card is played — enter paint mode with budget.</summary>
        public void BeginFromCard(int tileBudget, int days)
        {
            TileBudget = Mathf.Max(2, tileBudget);
            DaysRequired = Mathf.Max(1, days);
            if (IsSearching)
            {
                ShowHint("A survey team is already in the field — wait for them to finish.");
                return;
            }

            var scan = PrologueDroneScan.Instance
                       ?? new GameObject("PrologueDroneScan").AddComponent<PrologueDroneScan>();
            scan.ArmSurveyFromCard(TileBudget, DaysRequired);
        }

        /// <summary>Paint confirmed — start the day timer + team animation.</summary>
        public bool StartSearch(List<int> path, int days)
        {
            if (IsSearching || path == null || path.Count == 0) return false;

            searchPath.Clear();
            searchPath.AddRange(path);
            DaysRequired = Mathf.Max(1, days);
            DaysElapsed = 0f;
            dayAccum = 0f;
            revealedThrough = -1;
            IsSearching = true;

            BuildWaypoints();
            // Crew and drones stay off. The search still takes its colony days,
            // then the road and the reveal land together.
            // SpawnSearchTeam();
            // First tile is underfoot at spawn.
            RevealPathThrough(0);
            var ctrl = PrologueController.Instance;
            if (ctrl != null)
                PrologueVisionThrottle.FlushSurveyConfirmImmediate(ctrl, searchPath);
            CardDeckController.Instance?.ConsumePrologueSurveyCard();
            PrologueTimeLapse.Instance?.ResumeAfterSurveyConfirm();
            ShowHint(
                $"Survey Team searching ({searchPath.Count} tiles) — {DaysRequired} colony day{(DaysRequired == 1 ? "" : "s")}.");
            GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(
                PrologueController.Instance != null ? PrologueController.Instance.Colony : null);
            return true;
        }

        void Update()
        {
            if (!PrologueController.IsActive || !IsSearching) return;

            if (team.Count > 0)
                AnimateTeam();

            // Colony days share Time.timeScale with the rest of the game.
            if (Time.timeScale <= 0.01f) return;

            float secondsPerDay = PrologueTimeLapse.SecondsPerWeekNormal / 7f;
            dayAccum += Time.deltaTime;
            DaysElapsed = dayAccum / secondsPerDay;

            if (searchPath.Count > 0 && DaysRequired > 0f)
            {
                float t = Mathf.Clamp01(DaysElapsed / DaysRequired);
                int lead = Mathf.Clamp(Mathf.CeilToInt(t * searchPath.Count) - 1, 0, searchPath.Count - 1);
                RevealPathThrough(lead);
            }

            if (DaysElapsed >= DaysRequired)
                CompleteSearch();
        }

        /// <summary>End Week skips remaining colony-day timer — survey finishes immediately.</summary>
        public void FinishOnEndWeek()
        {
            if (!IsSearching) return;
            CompleteSearch();
        }

        /// <summary>Fresh playtest run — clear in-flight survey so bootstrap does not resume mid-search.</summary>
        public void AbortForPlaytest()
        {
            IsSearching = false;
            searchPath.Clear();
            revealedThrough = -1;
            DaysElapsed = 0;
            dayAccum = 0;
            ClearTeam();
            PrologueDroneScan.Instance?.SetScanMode(false);
        }

        /// <summary>Diagnostics: simulate crew partway along the painted path (vision / road churn).</summary>
        public void AutomationSimulateRevealProgress(int waypointIndex) =>
            RevealPathThrough(waypointIndex);

        void CompleteSearch()
        {
            IsSearching = false;
            var ctrl = PrologueController.Instance;
            ClearTeam();

            if (ctrl?.Colony != null && searchPath.Count > 0)
            {
                ctrl.Colony.RevealTiles(searchPath);
                RevealIceDepositsNearPath(ctrl, searchPath);
                PrologueVisionThrottle.QueueSurveyFog(ctrl, searchPath);

            EnqueueLitAndTrailVisuals(searchPath, flushAllImmediately: true);

            ctrl.RefreshHandUnlocks();
                GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(ctrl.Colony);
                foreach (int tile in searchPath)
                    PrologueIceMarkers.Instance?.OnTileRevealed(tile);

                if (ctrl.Colony.HasSurveyedIce())
                    DiscoverySystem.MarkTypeKnownWithoutMassReveal("Ice");

                ctrl.Colony.ObjectiveText(out _, out bool wantMore);
                if (!wantMore)
                    ShowHint("Search complete — ice found! Play Water Ice Aquifer and place it within 3 tiles of the ice.");
                else
                    ShowHint("Search complete — play another Survey Team card to keep exploring.");
            }

            searchPath.Clear();
            litVisualQueue.Clear();
            if (litSpreadRoutine != null)
            {
                StopCoroutine(litSpreadRoutine);
                litSpreadRoutine = null;
            }
            PrologueDroneScan.Instance?.NotifySearchFinished();
        }

        void BuildWaypoints()
        {
            waypoints.Clear();
            var ctrl = PrologueController.Instance;
            if (ctrl == null) return;
            foreach (int tile in searchPath)
            {
                if (!ctrl.TryGetCell(tile, out Vector2Int cell)) continue;
                Vector3 p = ColonyTileGrid.CellToWorldOnGround(cell);
                p.y += 0.2f;
                waypoints.Add(p);
            }
        }

        void SpawnSearchTeam()
        {
            ClearTeam();
            if (waypoints.Count == 0) return;

            teamRoot = new GameObject("SurveySearchTeam");
            DontDestroyOnLoad(teamRoot);

            // Stagger along the route so the team reads as a column walking out, not a swarm.
            // 2 aerial drones + 2 ground crew — all start near the first painted tile.
            Vector3 start = waypoints[0];
            for (int i = 0; i < 2; i++)
            {
                var drone = BuildMiniDrone($"SurveyDrone_{i}");
                drone.transform.SetParent(teamRoot.transform, false);
                Vector3 p = start + new Vector3((i - 0.5f) * 1.2f, 5.5f + i * 0.8f, -1.2f * i);
                drone.transform.position = p;
                team.Add(drone);
                pathIndex.Add(0);
            }

            for (int i = 0; i < 2; i++)
            {
                var crew = BuildCrewCapsule($"SurveyCrew_{i}");
                crew.transform.SetParent(teamRoot.transform, false);
                Vector3 p = start + new Vector3((i - 0.5f) * 0.9f, 0.9f, -0.8f - i * 0.7f);
                crew.transform.position = p;
                team.Add(crew);
                pathIndex.Add(0);
            }
        }

        void AnimateTeam()
        {
            if (waypoints.Count == 0 || team.Count == 0) return;
            // Progressive patrol: walk painted waypoints in order; hold at the last (no wrap / ping-pong).
            float speed = 4.5f * Time.deltaTime;
            int lead = 0;

            for (int i = 0; i < team.Count; i++)
            {
                var go = team[i];
                if (go == null) continue;
                bool airborne = go.name.StartsWith("SurveyDrone");
                int wi = Mathf.Clamp(pathIndex[i], 0, waypoints.Count - 1);
                Vector3 goal = waypoints[wi];
                float groundY = ColonyTileGrid.SampleGroundY(goal);
                goal.y = airborne ? (groundY + 5.5f + (i % 2) * 0.8f) : (groundY + 0.9f);

                Vector3 pos = go.transform.position;
                go.transform.position = Vector3.MoveTowards(pos, goal, speed);
                Vector3 flat = goal - pos;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.Slerp(
                        go.transform.rotation,
                        Quaternion.LookRotation(flat.normalized, Vector3.up),
                        8f * Mathf.Max(Time.deltaTime, 0.01f));

                if (Vector3.Distance(go.transform.position, goal) < 0.35f && wi < waypoints.Count - 1)
                    pathIndex[i] = wi + 1;

                lead = Mathf.Max(lead, pathIndex[i]);

                if (airborne)
                {
                    var p = go.transform.position;
                    p.y += Mathf.Sin(Time.unscaledTime * 3f + i) * 0.02f;
                    go.transform.position = p;
                }
            }

            RevealPathThrough(lead);
        }

        void RevealPathThrough(int waypointIndex)
        {
            if (waypointIndex <= revealedThrough) return;
            var ctrl = PrologueController.Instance;
            if (ctrl?.Colony == null) return;

            int from = revealedThrough + 1;
            int to = Mathf.Min(waypointIndex, searchPath.Count - 1);
            var batch = new List<int>();
            for (int i = from; i <= to; i++)
                batch.Add(searchPath[i]);
            ctrl.Colony.RevealTiles(batch);
            PrologueVisionThrottle.QueueSurveyFog(ctrl, batch);
            revealedThrough = to;

            EnqueueLitAndTrailVisuals(batch, flushAllImmediately: false);
            foreach (int tile in batch)
                PrologueIceMarkers.Instance?.OnTileRevealed(tile);
        }

        void EnqueueLitAndTrailVisuals(IReadOnlyList<int> tiles, bool flushAllImmediately)
        {
            if (tiles == null || tiles.Count == 0) return;
            if (flushAllImmediately)
            {
                var ctrl = PrologueController.Instance;
                if (ctrl?.Colony != null)
                {
                    LitTiles(ctrl.Colony, tiles);
                    PrologueSearchedTiles.Instance?.PaintTrailTiles(tiles, immediateGpu: true);
                    PrologueSearchedTiles.Instance?.RefreshForTiles(tiles);
                }
                return;
            }

            for (int i = 0; i < tiles.Count; i++)
                litVisualQueue.Add(tiles[i]);
            if (litSpreadRoutine == null)
                litSpreadRoutine = StartCoroutine(SpreadLitAndTrailVisuals());
        }

        IEnumerator SpreadLitAndTrailVisuals()
        {
            var ctrl = PrologueController.Instance;
            var colony = ctrl?.Colony;
            if (colony == null)
            {
                litVisualQueue.Clear();
                litSpreadRoutine = null;
                yield break;
            }

            int framesLeft = LitVisualSpreadFrames;
            while (litVisualQueue.Count > 0 && framesLeft > 0)
            {
                int chunk = Mathf.Max(LitVisualMinChunk,
                    Mathf.CeilToInt((float)litVisualQueue.Count / framesLeft));
                chunk = Mathf.Min(chunk, litVisualQueue.Count);
                var slice = litVisualQueue.GetRange(0, chunk);
                litVisualQueue.RemoveRange(0, chunk);
                LitTiles(colony, slice);
                PrologueSearchedTiles.Instance?.PaintTrailTiles(slice, immediateGpu: true);
                PrologueSearchedTiles.Instance?.RefreshForTiles(slice);
                framesLeft--;
                if (litVisualQueue.Count > 0)
                    yield return null;
            }

            if (litVisualQueue.Count > 0)
            {
                var tail = new List<int>(litVisualQueue);
                litVisualQueue.Clear();
                LitTiles(colony, tail);
                PrologueSearchedTiles.Instance?.PaintTrailTiles(tail, immediateGpu: true);
                PrologueSearchedTiles.Instance?.RefreshForTiles(tail);
            }

            litSpreadRoutine = null;
        }

        static GameObject BuildMiniDrone(string name)
        {
            var root = new GameObject(name);
            var mat = Unlit(new Color(0.2f, 0.9f, 1f));
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;

            for (int a = 0; a < 4; a++)
            {
                float x = (a < 2 ? 1f : -1f) * 0.7f;
                float z = (a % 2 == 0 ? 1f : -1f) * 0.7f;
                var rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rotor.name = $"Rotor_{a}";
                rotor.transform.SetParent(root.transform, false);
                rotor.transform.localPosition = new Vector3(x, 0.15f, z);
                rotor.transform.localScale = new Vector3(0.7f, 0.03f, 0.7f);
                Object.Destroy(rotor.GetComponent<Collider>());
                rotor.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return root;
        }

        static GameObject BuildCrewCapsule(string name)
        {
            var root = new GameObject(name);
            var mat = Unlit(new Color(0.95f, 0.85f, 0.55f));
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.55f, 0.7f, 0.55f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return root;
        }

        static Material Unlit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.color = c;
            return m;
        }

        static void RevealIceDepositsNearPath(PrologueController ctrl, List<int> path)
        {
            var colony = ctrl?.Colony;
            if (colony == null || path == null || path.Count == 0) return;
            int end = path[path.Count - 1];
            var toReveal = new List<int>();
            foreach (var t in colony.Board.Tiles)
            {
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != PrologueFeature.Ice && t.Feature != PrologueFeature.Glacier) continue;
                if (colony.PrologueStep >= 4 && colony.Board.FirstIceTiles.Contains(t.Index)) continue;
                if (colony.Dist(end, t.Index) > PrologueData.HarvestRange) continue;
                if (!t.Revealed)
                    toReveal.Add(t.Index);
            }
            if (toReveal.Count > 0)
                colony.RevealTiles(toReveal);
        }

        void ClearTeam()
        {
            for (int i = 0; i < team.Count; i++)
            {
                if (team[i] != null) Destroy(team[i]);
            }
            team.Clear();
            pathIndex.Clear();
            if (teamRoot != null)
            {
                Destroy(teamRoot);
                teamRoot = null;
            }
        }

        static void LitTiles(PrologueColony colony, IReadOnlyList<int> indices)
        {
            if (colony == null || indices == null) return;
            for (int i = 0; i < indices.Count; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= colony.Board.Tiles.Count) continue;
                colony.Board.At(idx).Lit = true;
            }
        }

        static void ShowHint(string msg)
        {
            GameDevTV.RTS.UI.PrologueHUD.Instance?.ShowNote(new List<string> { msg });
        }
    }
}
