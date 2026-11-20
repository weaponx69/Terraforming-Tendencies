using System.Collections.Generic;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Survey Team job: after paint + ✓, colony days tick on the time-lapse clock while
    /// drones/crew animate over the painted tiles, then the area is revealed.
    /// Spec: .summer/specs/2026-11-19-prologue-survey-team-design.md
    /// </summary>
    public class PrologueSurveyMission : MonoBehaviour
    {
        public static PrologueSurveyMission Instance { get; private set; }

        public const int DefaultTileBudget = 5;
        public const int DefaultDays = 3;

        public bool IsSearching { get; private set; }
        public int DaysRequired { get; private set; }
        public float DaysElapsed { get; private set; }
        public int TileBudget { get; private set; } = DefaultTileBudget;

        readonly List<int> searchPath = new();
        readonly List<GameObject> team = new();
        readonly List<Vector3> waypoints = new();
        readonly List<int> wanderTargets = new();
        float dayAccum;
        GameObject teamRoot;

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
            scan.BeginPaintMission(TileBudget, DaysRequired);
            ShowHint(
                $"Survey Team: paint up to {TileBudget} tiles from lit ground, then click the green ✓. " +
                $"Search takes {DaysRequired} colony day{(DaysRequired == 1 ? "" : "s")} (hit 1x to run time).");
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
            IsSearching = true;

            BuildWaypoints();
            SpawnSearchTeam();
            CardDeckController.Instance?.ConsumePrologueSurveyCard();
            ShowHint(
                $"Survey Team searching ({searchPath.Count} tiles) — {DaysRequired} colony day{(DaysRequired == 1 ? "" : "s")}. " +
                "Hit 1x / Space if paused.");
            GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(
                PrologueController.Instance != null ? PrologueController.Instance.Colony : null);
            return true;
        }

        void Update()
        {
            if (!PrologueController.IsActive || !IsSearching) return;

            AnimateTeam();

            // Colony days share Time.timeScale with the rest of the game.
            if (Time.timeScale <= 0.01f) return;

            float secondsPerDay = PrologueTimeLapse.SecondsPerWeekNormal / 7f;
            dayAccum += Time.deltaTime;
            DaysElapsed = dayAccum / secondsPerDay;

            if (DaysElapsed >= DaysRequired)
                CompleteSearch();
        }

        void CompleteSearch()
        {
            IsSearching = false;
            var ctrl = PrologueController.Instance;
            ClearTeam();

            if (ctrl?.Colony != null && searchPath.Count > 0)
            {
                ctrl.Colony.RevealTiles(searchPath);
                foreach (int tile in searchPath)
                    PrologueResourceFog.RevealNearTile(ctrl, tile);

                foreach (var t in ctrl.Colony.Board.Tiles)
                {
                    if (t.Revealed) t.Lit = true;
                }

                ctrl.RefreshHandUnlocks();
                GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(ctrl.Colony);
                PrologueIceMarkers.Instance?.Refresh();
                PrologueSearchedTiles.Instance?.RefreshAll();

                ctrl.Colony.ObjectiveText(out _, out bool wantMore);
                if (!wantMore)
                    ShowHint("Search complete — ice found! Play Water Ice Aquifer and place it within 3 tiles of the ice.");
                else
                    ShowHint("Search complete — play another Survey Team card to keep exploring.");
            }

            searchPath.Clear();
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
                Vector3 p = ColonyTileGrid.CellToWorld(cell, 0f);
                p.y = 0.2f;
                waypoints.Add(p);
            }
        }

        void SpawnSearchTeam()
        {
            ClearTeam();
            if (waypoints.Count == 0) return;

            teamRoot = new GameObject("SurveySearchTeam");
            DontDestroyOnLoad(teamRoot);

            // 2 aerial drones + 2 ground crew.
            for (int i = 0; i < 2; i++)
            {
                var drone = BuildMiniDrone($"SurveyDrone_{i}");
                drone.transform.SetParent(teamRoot.transform, false);
                Vector3 p = waypoints[i % waypoints.Count];
                p.y = 5.5f + i * 0.8f;
                drone.transform.position = p;
                team.Add(drone);
                wanderTargets.Add(i % waypoints.Count);
            }

            for (int i = 0; i < 2; i++)
            {
                var crew = BuildCrewCapsule($"SurveyCrew_{i}");
                crew.transform.SetParent(teamRoot.transform, false);
                Vector3 p = waypoints[(i + 1) % waypoints.Count];
                p.y = 0.9f;
                crew.transform.position = p;
                team.Add(crew);
                wanderTargets.Add((i + 1) % waypoints.Count);
            }
        }

        void AnimateTeam()
        {
            if (waypoints.Count == 0 || team.Count == 0) return;
            float speed = Time.deltaTime * (Time.timeScale > 0.01f ? 1f : 0f) * 4.5f;
            // When paused, freeze in place (deltaTime may be 0 already via timeScale).
            speed = 4.5f * Time.deltaTime;

            for (int i = 0; i < team.Count; i++)
            {
                var go = team[i];
                if (go == null) continue;
                bool airborne = go.name.StartsWith("SurveyDrone");
                int wi = wanderTargets[i];
                Vector3 goal = waypoints[wi];
                goal.y = airborne ? (5.5f + (i % 2) * 0.8f) : 0.9f;

                Vector3 pos = go.transform.position;
                go.transform.position = Vector3.MoveTowards(pos, goal, speed);
                Vector3 flat = goal - pos;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.Slerp(
                        go.transform.rotation,
                        Quaternion.LookRotation(flat.normalized, Vector3.up),
                        8f * Mathf.Max(Time.deltaTime, 0.01f));

                if (Vector3.Distance(go.transform.position, goal) < 0.35f)
                    wanderTargets[i] = (wi + 1 + (i % 2)) % waypoints.Count;

                // Bob drones slightly.
                if (airborne)
                {
                    var p = go.transform.position;
                    p.y += Mathf.Sin(Time.unscaledTime * 3f + i) * 0.02f;
                    go.transform.position = p;
                }
            }
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

        void ClearTeam()
        {
            for (int i = 0; i < team.Count; i++)
            {
                if (team[i] != null) Destroy(team[i]);
            }
            team.Clear();
            wanderTargets.Clear();
            if (teamRoot != null)
            {
                Destroy(teamRoot);
                teamRoot = null;
            }
        }

        static void ShowHint(string msg)
        {
            GameDevTV.RTS.UI.PrologueHUD.Instance?.ShowWeekReport(new List<string> { msg });
        }
    }
}
