using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
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
        bool openingBriefingShown;
        bool refreshingHand;
        bool prologueVictoryCelebrated;
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
            if (Application.isPlaying)
                ProloguePerformanceBudget.ApplyIfNeeded();
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
            TryOpeningBriefing();
        }

        void TryOpeningBriefing()
        {
            if (openingBriefingShown || !syncedFromPlanet || Colony == null) return;
            if (GameDevTV.RTS.UI.PrologueHUD.Instance == null) return;
            openingBriefingShown = true;
            GameDevTV.RTS.UI.PrologueHUD.Instance.ShowOpeningBriefing();
        }

        void HandleDeckChanged()
        {
            // BlueprintDraftUI / Acts seating may refill after us — reclaim Prologue hand.
            if (refreshingHand || ignoreNextDeckChanged || !IsActive || !syncedFromPlanet || Colony == null)
                return;
            if (!HandLooksWrongForPrologue()) return;
            RefreshHandUnlocks();
        }

        /// <summary>Set while our own RebuildPrologueHand fires OnHandChanged (avoid feedback loops).</summary>
        internal static bool ignoreNextDeckChanged;

        bool HandLooksWrongForPrologue()
        {
            var deck = CardDeckController.Instance;
            if (deck?.Hand == null || Colony == null) return true;

            Colony.ObjectiveText(out _, out bool wantSurvey);
            bool hasSurvey = false;
            // Survey + Orbital Scan + Research are seated outside unlock/mine lists.
            int maxAllowed = Colony.Unlocked.Count + Colony.KnownResources.Count + 3;
            if (deck.Hand.Count > maxAllowed) return true;
            if (deck.Hand.Count == 0 && wantSurvey) return true;

            bool surveying = PrologueSurveyMission.Instance != null
                && PrologueSurveyMission.Instance.IsSearching;

            foreach (var card in deck.Hand)
            {
                if (card == null) continue;
                if (card is ScoutingCardSO scout
                    && scout.scoutingType == ScoutingCardSO.ScoutingType.SurveyDrone)
                {
                    hasSurvey = true;
                    continue;
                }
                if (card is ScoutingCardSO orbital
                    && orbital.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan)
                    continue;
                if (card is ResearchCardSO)
                    continue;
                if (PrologueCardMap.TryGetPrologueId(card, out var id)
                    && (Colony.Unlocked.Contains(id) || id == PrologueBuildingId.CommandPost))
                    continue;
                // Mine cards for Survey-uncovered typed deposits.
                if (card is UnlockBuildingCardSO unlock
                    && unlock.buildingToUnlock != null
                    && DiscoverySystem.TryGetMineResourceType(unlock.buildingToUnlock, out string mineType)
                    && PrologueResourceIdUtil.TryParse(mineType, out var resId)
                    && Colony.KnowsResource(resId))
                    continue;
                return true; // foreign Acts card
            }

            if (wantSurvey && !hasSurvey && !surveying) return true;
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
            if (handSeeded)
            {
                if (!HandLooksWrongForPrologue()) return;
                // Wrong hand usually means Acts deck wiring caught up once — not every frame.
                if (Time.frameCount == lastHandWrongFrame) return;
                lastHandWrongFrame = Time.frameCount;
            }
            RefreshHandUnlocks();
            handSeeded = true;
        }

        int lastHandWrongFrame = -1;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void HandlePlanetGenerated()
        {
            SyncBoardFromPlanet();
            var homeCell = ResolveHomeCell();
            HexGridManager.Instance?.GenerateLocalHexDisk(homeCell, axialRadius: 20);
            Colony.StartPrologue();
            syncedFromPlanet = Colony.Board.Tiles.Count > 16;
            handSeeded = false;
            if (PrologueDroneScan.Instance == null)
                new GameObject("PrologueDroneScan").AddComponent<PrologueDroneScan>();
            if (PrologueTimeLapse.Instance == null)
                gameObject.AddComponent<PrologueTimeLapse>();
            TrySeedHand();
            ProloguePerformanceBudget.RefreshPlanetRendering();
            GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(Colony);
            Object.FindAnyObjectByType<GameDevTV.RTS.UI.Containers.BottomBarActionsUI>()
                ?.ApplyPrologueCompactLayout();
            HexGridManager.Instance?.RefreshAllOutlineVisibility();
            // Floating "?" over unrevealed ice — without these the objective text is meaningless.
            if (PrologueIceMarkers.Instance == null)
                new GameObject("PrologueIceMarkers").AddComponent<PrologueIceMarkers>();
            if (PrologueSearchedTiles.Instance == null)
                new GameObject("PrologueSearchedTiles").AddComponent<PrologueSearchedTiles>();
            ApplySliceRenderingPerformanceDefaults();
            PrologueSearchedTiles.Instance?.Clear();
            PrologueRevealGhosts.ResetSession();
            PrologueBuildingRegistry.Invalidate();
            PrologueIceMarkers.Instance.RebuildAll();
            PrologueSearchedTiles.Instance.RefreshAll();
            ArmResourceFogReapply(4f);
            // Prologue: Command Post only — despawn Acts free gather drones.
            DespawnAutoGatherDronesNearHome();
            // No wall-clock meteor waves during Prologue.
            GameDevTV.RTS.Environment.NaturalEventManager.StopAssaultIfAny();
            GameDevTV.RTS.UI.PrologueHUD.Instance?.RefreshTimeControls();
            Debug.Log($"[PrologueController] Prologue started (tiles={Colony.Board.Tiles.Count}, Acts archived).");
            if (ProloguePerfScenario.ShouldApply)
                ProloguePerfScenario.Apply(this);
        }

        /// <summary>Baked survey trails, full-mesh reveal ghosts, overlay fit — default for the vertical slice.</summary>
        static void ApplySliceRenderingPerformanceDefaults()
        {
            // Survey trails: one transparent ground quad + per-hex brown tint — not DirtRoad_* cube meshes.
            ProloguePathGroundOverlay.SetEnabled(true);
            PrologueSearchedTiles.UseBakedPathOverlay = true;
            PrologueSearchedTiles.TileTintOnly = true;
            PrologueSearchedTiles.UseCubeRoadMeshes = false;
            PrologueRevealGhosts.SilhouetteFootprintsOnly = false;
            PrologueRevealGhosts.FrustumCullRevealGhosts = false;
            PrologueRevealGhosts.ShowWorldLabels = false;
            PrologueRevealGhosts.ForceResyncMeshes();
            PrologueRevealGhosts.SuppressRedundantWorldLabels();
            PrologueDroneScan.EnableRubberBandPreview = false;
            if (Instance != null)
                ProloguePathGroundOverlay.Instance?.FitBoard(Instance);
        }

        /// <summary>Perf soak: surveyed path, baked dirt overlay, finished buildings (vertical slice).</summary>
        public void BeginFilledPerfRun()
        {
            ProloguePerfScenario.RequestFilledRun = true;
            BeginNewPrologueRunForPlaytest();
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

        public         Vector2Int ResolveHomeCell()
        {
            Vector3 home = ResolveHomeWorld();
            return ColonyTileGrid.WorldToCell(home);
        }

        void SyncBoardFromPlanet()
        {
            Colony ??= PrologueColony.CreateLanding();
            Colony.Board.Tiles.Clear();
            Colony.Board.Sites.Clear();
            Colony.Board.FirstIceTiles.Clear();
            Colony.Board.SecondIceTiles.Clear();
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
                        Lit = false,
                        Revealed = false,
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
            Colony.SetSupplyDistanceFn(PrologueHaul.HexesFromNearestSupply);

            SeedIceFromSectors(home);
            EnsureCommandPostVisual(home);
            ArmResourceFogReapply(4f);
            Debug.Log($"[PrologueController] Synced {Colony.Board.Tiles.Count} prologue tiles around {homeCell}.");
        }

        /// <summary>
        /// Prologue data marks the home tile as Command Post, but nothing was spawned in-world.
        /// Place the real Command Post on the reserved Command Post pad (not sector center).
        /// </summary>
        void EnsureCommandPostVisual(Vector3 homeWorld)
        {
            BuildingSiteSlot pad = FindStartingCommandPad();
            // Seat the building on the hex center. Navmesh XZ sits off that pad.
            Vector3 padPos = pad != null
                ? ColonyTileGrid.CellToWorldOnGround(ColonyTileGrid.WorldToCell(pad.Position))
                : ColonyTileGrid.CellToWorldOnGround(ColonyTileGrid.WorldToCell(homeWorld));

            if (pad != null)
            {
                pad.Position = padPos;
                if (pad.MarkerGO != null)
                    pad.MarkerGO.transform.position = padPos;
            }

            BaseBuilding seated = null;
            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.BuildingSO == null) continue;
                if (b.BuildingSO.Name.IndexOf("Command", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (Vector3.Distance(b.transform.position, padPos) < 80f)
                {
                    seated = b;
                    break;
                }
            }

            if (seated == null && commandPostVisual != null) return;

            if (seated == null)
            {
                var def = Resources.Load<BuildingSO>("Buildings/Command Post/Command Post");
                if (def == null || def.Prefab == null)
                {
                    Debug.LogWarning("[PrologueController] Command Post BuildingSO/prefab missing — landing pad will be invisible.");
                    return;
                }

                var go = Object.Instantiate(def.Prefab, padPos, Quaternion.identity);
                go.name = "PrologueCommandPost";
                if (!go.TryGetComponent(out seated))
                {
                    commandPostVisual = go;
                    return;
                }

                seated.CompleteInstantCardPlace(Owner.Player1, def);
            }

            // GroundBuilding nav-snaps XZ off the hex. Put the mesh back on the disc.
            seated.transform.position = padPos;
            GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.GroundBuilding(seated);
            commandPostVisual = seated.gameObject;
            if (pad != null)
            {
                pad.SetOccupied(seated);
                pad.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
            }
            else
            {
                BuildingSiteRegistry.RegisterOccupancy(seated);
            }

            PlayerInput.FocusCameraOnWorldPosition(padPos);
            Debug.Log($"[PrologueController] Command Post seated on pad hex at {padPos}");
        }

        static BuildingSiteSlot FindStartingCommandPad()
        {
            if (SectorManager.Instance == null || SectorManager.Instance.Sectors.Count == 0)
                return null;

            var sector = SectorManager.Instance.Sectors[0];
            if (sector?.BuildingSites == null) return null;
            foreach (var site in sector.BuildingSites)
            {
                if (site != null && site.Kind == BuildingSiteKind.CommandPost)
                    return site;
            }

            return null;
        }

        /// <summary>Home = sector 0 Command Post pad when planet sites exist; else sector center.</summary>
        static Vector3 ResolveHomeWorld()
        {
            if (SectorManager.Instance != null && SectorManager.Instance.Sectors.Count > 0)
            {
                var s0 = SectorManager.Instance.Sectors[0];
                if (s0?.BuildingSites != null)
                {
                    foreach (var site in s0.BuildingSites)
                    {
                        if (site != null && site.Kind == BuildingSiteKind.CommandPost)
                            return site.Position;
                    }
                }
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
            PrologueSectorDepositBootstrap.BootstrapSectorZero(this, Colony);
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

            Colony?.RefreshSecondIceTiles();
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
            if (Colony.ContractCancelled)
            {
                PrologueTimeLapse.Instance?.EndRun();
                return;
            }
            bool alreadyComplete = Colony.PrologueStep >= PrologueData.PrologueSteps;
            PrologueSurveyMission.Instance?.FinishOnEndWeek();
            PrologueTimeLapse.Instance?.NotifyManualEndWeek();
            var log = Colony.EndWeek();
            PrologueConstruction.NotifyWeekAdvanced();
            foreach (string line in log)
                Debug.Log($"[Prologue Week {Colony.Week}] {line}");
            if (Colony.ContractCancelled)
                PrologueTimeLapse.Instance?.EndRun();
            var hud = GameDevTV.RTS.UI.PrologueHUD.Instance;
            if (alreadyComplete)
                hud?.ShowNote(log);
            else
                hud?.ShowWeekReport(log);
            GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(Colony);
            RefreshHandUnlocks();
            PrologueIceMarkers.Instance?.Refresh();

            TryCelebratePrologueVictory();
        }

        /// <summary>Step 6 reached — show the same victory overlay as Colony Acts (once per run).</summary>
        public void TryCelebratePrologueVictory()
        {
            if (prologueVictoryCelebrated || Colony == null || Colony.ContractCancelled) return;
            if (Colony.PrologueStep < PrologueData.PrologueSteps) return;

            prologueVictoryCelebrated = true;
            PrologueTimeLapse.Instance?.EndRun();
            GameOverManager.LastOutcomeDetail =
                "Corporation contract held — ice, air, housing, and the dust storm. The opening mission is complete.";
            GameOverUI.EnsureAllSubscribed();
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.TriggerVictory();
            else
                Debug.LogError("[PrologueController] GameOverManager missing — cannot show prologue victory.");
        }

        public void RefreshHandUnlocks()
        {
            var deck = CardDeckController.Instance;
            if (deck == null || refreshingHand) return;
            refreshingHand = true;
            ignoreNextDeckChanged = true;
            try
            {
                if (!deck.RebuildPrologueHand(Colony))
                    return;
                GameDevTV.RTS.UI.PrologueHUD.Instance?.Refresh(Colony);
            }
            finally
            {
                ignoreNextDeckChanged = false;
                refreshingHand = false;
            }
        }

        float fogReapplyUntil;

        void LateUpdate()
        {
            if (!IsActive || Colony == null) return;

            PrologueGroundVisualTuning.Tick();

            // Planet gen / Acts bootstrap can re-show deposits after the first fog pass.
            if (Time.unscaledTime < fogReapplyUntil && Time.frameCount % 15 == 0)
                PrologueResourceFog.ApplyHiddenState();
        }

        public void ArmResourceFogReapply(float seconds = 3f)
        {
            fogReapplyUntil = Mathf.Max(fogReapplyUntil, Time.unscaledTime + seconds);
            PrologueResourceFog.ApplyHiddenState();
        }

        /// <summary>
        /// Fresh prologue run for player-style playtests: board sync, deposit bootstrap, fog, ice markers.
        /// </summary>
        public void BeginNewPrologueRunForPlaytest()
        {
            PrologueSurveyMission.Instance?.AbortForPlaytest();
            PrologueDroneScan.Instance?.SetScanMode(false);
            Colony = PrologueColony.CreateLanding();
            syncedFromPlanet = false;
            handSeeded = false;
            openingBriefingShown = false;
            prologueVictoryCelebrated = false;
            PrologueUiPlaythrough.ResetSession();
            if (PlanetGenerator.Instance != null)
                HandlePlanetGenerated();
            else
                TrySyncWhenReady();
            PlayerInput.Instance?.ApplyPrologueDefaultZoom();
            var hand = Object.FindAnyObjectByType<GameDevTV.RTS.UI.Containers.BottomBarActionsUI>();
            hand?.ApplyPrologueCompactLayout();
            Debug.Log("[PrologueController] BeginNewPrologueRunForPlaytest — bootstrap applied.");
        }
    }
}
