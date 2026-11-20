using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Default run owner: Unreal-style Prologue. When present and enabled, Colony Acts must not BeginRun.
    /// </summary>
    public class PrologueController : MonoBehaviour
    {
        public static PrologueController Instance { get; private set; }

        public static bool IsActive => Instance != null && Instance.isActiveAndEnabled;

        public PrologueColony Colony { get; private set; }

        /// <summary>Maps PrologueBoardState tile index → axial hex cell.</summary>
        readonly List<Vector2Int> indexToCell = new();

        readonly Dictionary<Vector2Int, int> cellToIndex = new();

        bool syncedFromPlanet;
        bool handSeeded;
        bool refreshingHand;
        GameObject commandPostVisual;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Object.FindAnyObjectByType<PrologueController>() != null) return;
            var go = new GameObject("PrologueController");
            go.AddComponent<PrologueController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Colony ??= PrologueColony.CreateLanding();
        }

        void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            CardDeckController.OnHandChanged += HandleDeckChanged;
        }

        void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            CardDeckController.OnHandChanged -= HandleDeckChanged;
        }

        void Start()
        {
            Colony ??= PrologueColony.CreateLanding();
            TrySyncWhenReady();
        }

        void Update()
        {
            TrySyncWhenReady();
            TrySeedHand();
        }

        void HandleDeckChanged()
        {
            // BlueprintDraftUI / Acts seating may refill after us — reclaim Prologue hand.
            if (refreshingHand || !IsActive || !syncedFromPlanet || Colony == null) return;
            if (!HandLooksWrongForPrologue()) return;
            RefreshHandUnlocks();
        }

        bool HandLooksWrongForPrologue()
        {
            var deck = CardDeckController.Instance;
            if (deck?.Hand == null || Colony == null) return true;

            Colony.ObjectiveText(out _, out bool wantSurvey);
            bool hasSurvey = false;
            int maxAllowed = Colony.Unlocked.Count + 2; // unlocked buildings + optional scan
            if (deck.Hand.Count > maxAllowed) return true;
            if (deck.Hand.Count == 0 && wantSurvey) return true;

            foreach (var card in deck.Hand)
            {
                if (card == null) continue;
                if (card is ScoutingCardSO scout
                    && scout.scoutingType == ScoutingCardSO.ScoutingType.SurveyDrone)
                {
                    hasSurvey = true;
                    continue;
                }
                // Orbital Scan is not the Prologue Survey Team card.
                if (PrologueCardMap.IsPrologueScanCard(card)) return true;
                if (PrologueCardMap.TryGetPrologueId(card, out var id)
                    && (Colony.Unlocked.Contains(id) || id == PrologueBuildingId.CommandPost))
                    continue;
                return true; // foreign Acts card
            }

            if (wantSurvey && !hasSurvey) return true;
            return false;
        }

        void TrySyncWhenReady()
        {
            if (syncedFromPlanet) return;
            if (PlanetGenerator.Instance == null) return;
            if (SectorManager.Instance == null || SectorManager.Instance.Sectors == null
                || SectorManager.Instance.Sectors.Count == 0)
                return;
            HandlePlanetGenerated();
        }

        void TrySeedHand()
        {
            if (!syncedFromPlanet || Colony == null) return;
            var deck = CardDeckController.Instance;
            if (deck == null || deck.MasterDeck == null || deck.MasterDeck.Count == 0) return;
            if (handSeeded && !HandLooksWrongForPrologue()) return;
            RefreshHandUnlocks();
            handSeeded = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void HandlePlanetGenerated()
        {
            SyncBoardFromPlanet();
            Colony.StartPrologue();
            syncedFromPlanet = Colony.Board.Tiles.Count > 16;
            handSeeded = false;
            if (PrologueDroneScan.Instance == null)
                new GameObject("PrologueDroneScan").AddComponent<PrologueDroneScan>();
            if (PrologueTimeLapse.Instance == null)
                gameObject.AddComponent<PrologueTimeLapse>();
            TrySeedHand();
            GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(Colony);
            HexGridManager.Instance?.RefreshAllOutlineVisibility();
            // Floating "?" over unrevealed ice — without these the objective text is meaningless.
            if (PrologueIceMarkers.Instance == null)
                new GameObject("PrologueIceMarkers").AddComponent<PrologueIceMarkers>();
            if (PrologueSearchedTiles.Instance == null)
                new GameObject("PrologueSearchedTiles").AddComponent<PrologueSearchedTiles>();
            PrologueIceMarkers.Instance.Refresh();
            PrologueSearchedTiles.Instance.RefreshAll();
            ArmResourceFogReapply(4f);
            // Prologue: Command Post only — despawn Acts free gather drones.
            DespawnAutoGatherDronesNearHome();
            GameDevTV.RTS.UI.PrologueHUD.Instance?.RefreshTimeControls();
            Debug.Log($"[PrologueController] Prologue started (tiles={Colony.Board.Tiles.Count}, Acts archived).");
        }

        static void DespawnAutoGatherDronesNearHome()
        {
            Vector3 home = ResolveHomeWorld();
            foreach (var worker in Object.FindObjectsByType<GameDevTV.RTS.Units.Worker>(FindObjectsInactive.Exclude))
            {
                if (worker == null) continue;
                if (worker.Owner != GameDevTV.RTS.Units.Owner.Player1) continue;
                // Survey drone instantiates MiningDroneMesh under PrologueSurveyDrone — do not kill it.
                if (worker.GetComponentInParent<PrologueDroneScan>() != null) continue;
                Transform root = worker.transform.root;
                if (root != null && root.name == "PrologueSurveyDrone") continue;
                if (worker.transform.IsChildOf(root) && root.name.StartsWith("PrologueSurvey")) continue;

                if (Vector3.Distance(worker.transform.position, home) > 60f) continue;
                string n = worker.gameObject.name;
                if (n.IndexOf("Mining", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Drone", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Object.Destroy(worker.gameObject);
            }
        }

        public void SyncBoardFromPlanet()
        {
            Colony ??= PrologueColony.CreateLanding();
            Colony.Board.Tiles.Clear();
            Colony.Board.Sites.Clear();
            Colony.Board.FirstIceTiles.Clear();
            indexToCell.Clear();
            cellToIndex.Clear();

            Vector3 home = ResolveHomeWorld();
            Vector2Int homeCell = ColonyTileGrid.WorldToCell(home);

            // Build a local hex disk for Prologue (enough for ice/drill/oxygen/habitats).
            const int radius = 8;
            for (int dq = -radius; dq <= radius; dq++)
            {
                for (int dr = -radius; dr <= radius; dr++)
                {
                    var cell = new Vector2Int(homeCell.x + dq, homeCell.y + dr);
                    if (ColonyTileGrid.HexDistance(homeCell, cell) > radius) continue;
                    int index = Colony.Board.Tiles.Count;
                    bool isHome = cell == homeCell;
                    Colony.Board.Tiles.Add(new PrologueTile
                    {
                        Index = index,
                        Lit = isHome,
                        Revealed = isHome,
                        Building = isHome ? PrologueBuildingId.CommandPost : PrologueBuildingId.None
                    });
                    indexToCell.Add(cell);
                    cellToIndex[cell] = index;
                }
            }

            Colony.SetDistanceFn((a, b) =>
            {
                if (a < 0 || b < 0 || a >= indexToCell.Count || b >= indexToCell.Count) return 99;
                return ColonyTileGrid.HexDistance(indexToCell[a], indexToCell[b]);
            });

            SeedIceFromSectors(home);
            EnsureCommandPostVisual(home);
            ArmResourceFogReapply(4f);
            Debug.Log($"[PrologueController] Synced {Colony.Board.Tiles.Count} prologue tiles around {homeCell}.");
        }

        /// <summary>
        /// Prologue data marks the home tile as Command Post, but nothing was spawned in-world.
        /// Place the real Command Post prefab so the landing pad is visible (Unreal pad).
        /// </summary>
        void EnsureCommandPostVisual(Vector3 homeWorld)
        {
            // Already have a player Command Post near home?
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.BuildingSO == null) continue;
                if (b.BuildingSO.Name.IndexOf("Command", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (Vector3.Distance(b.transform.position, homeWorld) < 40f)
                {
                    commandPostVisual = b.gameObject;
                    PlayerInput.FocusCameraOnWorldPosition(b.transform.position);
                    return;
                }
            }

            if (commandPostVisual != null) return;

            var def = Resources.Load<BuildingSO>("Buildings/Command Post/Command Post");
            if (def == null || def.Prefab == null)
            {
                Debug.LogWarning("[PrologueController] Command Post BuildingSO/prefab missing — landing pad will be invisible.");
                return;
            }

            Vector3 pos = homeWorld;
            pos.y = 0f;
            var go = Object.Instantiate(def.Prefab, pos, Quaternion.identity);
            go.name = "PrologueCommandPost";
            commandPostVisual = go;
            if (go.TryGetComponent(out BaseBuilding building))
            {
                building.CompleteInstantCardPlace(Owner.Player1, def);
            }
            PlayerInput.FocusCameraOnWorldPosition(pos);
            Debug.Log($"[PrologueController] Spawned Command Post at {pos}");
        }

        static Vector3 ResolveHomeWorld()
        {
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
            {
                var s0 = SectorManager.Instance.Sectors[0];
                if (s0 != null) return s0.Center;
            }
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                float w = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                float h = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
                return new Vector3(w * 0.5f, 0f, h * 0.5f);
            }
            return Vector3.zero;
        }

        void SeedIceFromSectors(Vector3 home)
        {
            if (SectorManager.Instance == null) return;

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector == null || sector.Nodes == null) continue;
                bool sectorIsIce = sector.Feature == SectorManager.SectorFeature.WaterDeposit
                    || sector.Feature == SectorManager.SectorFeature.Glacier;

                foreach (var node in sector.Nodes)
                {
                    bool isIceNode = node.type == SectorNode.NodeType.Feature && sectorIsIce;
                    if (!isIceNode && node.type != SectorNode.NodeType.Feature) continue;
                    if (!sectorIsIce) continue;

                    Vector2Int cell = ColonyTileGrid.WorldToCell(node.position);
                    if (!cellToIndex.TryGetValue(cell, out int index)) continue;

                    var tile = Colony.Board.At(index);
                    tile.Feature = sector.Feature == SectorManager.SectorFeature.Glacier
                        ? PrologueFeature.Glacier
                        : PrologueFeature.Ice;
                    tile.DepositLeft = sector.Feature == SectorManager.SectorFeature.Glacier
                        ? 900
                        : 300;
                    // Stay hidden until drone reveal (except we don't reveal feature here).
                }

                // Also mark feature center if no nodes mapped
                if (sectorIsIce)
                {
                    Vector2Int centerCell = ColonyTileGrid.WorldToCell(sector.Center);
                    if (cellToIndex.TryGetValue(centerCell, out int centerIdx))
                    {
                        var t = Colony.Board.At(centerIdx);
                        if (t.Feature == PrologueFeature.None)
                        {
                            t.Feature = sector.Feature == SectorManager.SectorFeature.Glacier
                                ? PrologueFeature.Glacier
                                : PrologueFeature.Ice;
                            t.DepositLeft = 300;
                        }
                    }
                }
            }

            // Guarantee at least TWO ice deposits so step 4 (second drill) is playable.
            EnsureMinimumIceDeposits(2);
        }

        /// <summary>
        /// Plant unrevealed ice pads away from the Command Post until <paramref name="minCount"/> exist.
        /// </summary>
        public void EnsureMinimumIceDeposits(int minCount)
        {
            if (Colony == null || Colony.Board.Tiles.Count == 0) return;

            int homeIndex = -1;
            var iceTiles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Colony.Board.Tiles.Count; i++)
            {
                var t = Colony.Board.At(i);
                if (t.Building == PrologueBuildingId.CommandPost) homeIndex = t.Index;
                if ((t.Feature == PrologueFeature.Ice || t.Feature == PrologueFeature.Glacier) && t.DepositLeft > 0)
                    iceTiles.Add(i);
            }
            if (homeIndex < 0) homeIndex = 0;

            int need = minCount - iceTiles.Count;
            if (need <= 0) return;

            // Prefer mid-range empty tiles, then any empty, farthest from existing ice.
            for (int n = 0; n < need; n++)
            {
                int best = -1;
                int bestScore = int.MinValue;
                for (int i = 0; i < Colony.Board.Tiles.Count; i++)
                {
                    var t = Colony.Board.At(i);
                    if (t.Building != PrologueBuildingId.None) continue;
                    if (t.Feature == PrologueFeature.Ice || t.Feature == PrologueFeature.Glacier) continue;
                    int dHome = Colony.Dist(homeIndex, i);
                    if (dHome < 3) continue;

                    int minIce = 99;
                    foreach (int ice in iceTiles)
                        minIce = Mathf.Min(minIce, Colony.Dist(ice, i));
                    // Prefer tiles far from other ice and 4–8 from home.
                    int score = minIce * 10 - Mathf.Abs(dHome - 6);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    for (int i = 0; i < Colony.Board.Tiles.Count; i++)
                    {
                        var t = Colony.Board.At(i);
                        if (t.Building != PrologueBuildingId.None) continue;
                        if (t.Feature == PrologueFeature.Ice || t.Feature == PrologueFeature.Glacier) continue;
                        best = i;
                        break;
                    }
                }
                if (best < 0) break;

                var pad = Colony.Board.At(best);
                pad.Feature = PrologueFeature.Ice;
                pad.DepositLeft = 300;
                pad.Revealed = false;
                iceTiles.Add(best);
                Debug.Log($"[PrologueController] Seeded Prologue ice deposit at tile {best}.");
            }

            PrologueIceMarkers.Instance?.Refresh();
        }

        public bool TryGetTileIndex(Vector3 world, out int index)
        {
            Vector2Int cell = ColonyTileGrid.WorldToCell(world);
            return cellToIndex.TryGetValue(cell, out index);
        }

        public bool TryGetCell(int index, out Vector2Int cell)
        {
            if (index < 0 || index >= indexToCell.Count)
            {
                cell = default;
                return false;
            }
            cell = indexToCell[index];
            return true;
        }

        public void EndWeek()
        {
            if (Colony == null) return;
            PrologueTimeLapse.Instance?.NotifyManualEndWeek();
            var log = Colony.EndWeek();
            foreach (string line in log)
                Debug.Log($"[Prologue Week {Colony.Week}] {line}");
            GameDevTV.RTS.UI.PrologueHUD.Instance?.ShowWeekReport(log);
            GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(Colony);
            RefreshHandUnlocks();
            PrologueIceMarkers.Instance?.Refresh();
        }

        public void RefreshHandUnlocks()
        {
            var deck = CardDeckController.Instance;
            if (deck == null || refreshingHand) return;
            refreshingHand = true;
            try
            {
                deck.RebuildPrologueHand(Colony);
                GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(Colony);
            }
            finally
            {
                refreshingHand = false;
            }
        }

        float fogReapplyUntil;

        void LateUpdate()
        {
            // Keep hand synced after step advances from reveal/place.
            if (!IsActive || Colony == null) return;
            if (Time.frameCount % 30 == 0)
                GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(Colony);

            // Planet gen / Acts bootstrap can re-show deposits after the first fog pass.
            if (Time.unscaledTime < fogReapplyUntil && Time.frameCount % 15 == 0)
                PrologueResourceFog.ApplyHiddenState();
        }

        public void ArmResourceFogReapply(float seconds = 3f)
        {
            fogReapplyUntil = Mathf.Max(fogReapplyUntil, Time.unscaledTime + seconds);
            PrologueResourceFog.ApplyHiddenState();
        }
    }
}
