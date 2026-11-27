using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// Default run owner: Unreal-style MainGame. When present and enabled, Colony Acts must not BeginRun.
    /// </summary>
    public class MainGameController : MonoBehaviour
    {
        public static MainGameController Instance { get; private set; }

        public static bool IsMainGamePlayScene()
        {
            string name = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return name.IndexOf("MainGame", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsActive => Instance != null && Instance.isActiveAndEnabled;

        public MainGameColony Colony { get; private set; }

        /// <summary>Maps MainGameBoardState tile index → axial hex cell.</summary>
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
            if (Object.FindAnyObjectByType<MainGameController>() != null) return;
            var go = new GameObject("MainGameController");
            go.AddComponent<MainGameController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Colony ??= MainGameColony.CreateLanding();
        }

        void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            CardDeckController.OnHandChanged += HandleDeckChanged;
            if (Application.isPlaying)
            {
                MainGameHandStripBootstrap.EnsureExists();
                PlayerInput.EnsureOnMainCameraForSlice();
                MainGamePerformanceBudget.ApplyIfNeeded();
            }
        }

        void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            CardDeckController.OnHandChanged -= HandleDeckChanged;
        }

        void Start()
        {
            Colony ??= MainGameColony.CreateLanding();
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
            if (GameDevTV.RTS.UI.MainGameHUD.Instance == null) return;
            openingBriefingShown = true;
            GameDevTV.RTS.UI.MainGameHUD.Instance.ShowOpeningBriefing();
        }

        void HandleDeckChanged()
        {
            // BlueprintDraftUI / Acts seating may refill after us — reclaim MainGame hand.
            if (refreshingHand || ignoreNextDeckChanged || !IsActive || !syncedFromPlanet || Colony == null)
                return;
            if (!HandLooksWrongForMainGame()) return;
            RefreshHandUnlocks();
        }

        /// <summary>Set while our own RebuildMainGameHand fires OnHandChanged (avoid feedback loops).</summary>
        internal static bool ignoreNextDeckChanged;

        bool HandLooksWrongForMainGame()
        {
            var deck = CardDeckController.Instance;
            if (deck?.Hand == null || Colony == null) return true;

            Colony.ObjectiveText(out _, out bool wantSurvey);
            bool hasSurvey = false;
            // Survey + Orbital Scan + Research are seated outside unlock/mine lists.
            int maxAllowed = Colony.Unlocked.Count + Colony.KnownResources.Count + 3;
            if (deck.Hand.Count > maxAllowed) return true;
            if (deck.Hand.Count == 0 && wantSurvey) return true;

            bool surveying = MainGameSurveyMission.Instance != null
                && MainGameSurveyMission.Instance.IsSearching;

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
                if (MainGameCardMap.TryGetMainGameId(card, out var id)
                    && (Colony.Unlocked.Contains(id) || id == MainGameBuildingId.CommandPost))
                    continue;
                // Mine cards for Survey-uncovered typed deposits.
                if (card is UnlockBuildingCardSO unlock
                    && unlock.buildingToUnlock != null
                    && DiscoverySystem.TryGetMineResourceType(unlock.buildingToUnlock, out string mineType)
                    && MainGameResourceIdUtil.TryParse(mineType, out var resId)
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
                if (!HandLooksWrongForMainGame()) return;
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
            SectorManager.EnsureExists();
            PlanetGenerator.Instance?.EnsureSectorResourceNodesPlaced();
            LandingSiteResourceLayout.EnsureMainGameLandingChosen();
            MainGameHandStripBootstrap.EnsureExists();
            PlayerInput.EnsureOnMainCameraForSlice();
            PlanetGenerator.Instance?.ApplyAuthoredOrGradientSurface();
            PlayerInput.Instance?.RefreshMainGameCameraAfterPlanet();
            SyncBoardFromPlanet();
            var homeCell = ResolveHomeCell();
            int hexRadius = ComputePatchHexRadiusClamped(homeCell, maxVisualRadius: 32, minVisualRadius: 22);
            HexGridManager.Instance?.GenerateLocalHexDisk(homeCell, hexRadius);
            Colony.StartMainGame();
            syncedFromPlanet = Colony.Board.Tiles.Count > 16;
            handSeeded = false;
            if (MainGameDroneScan.Instance == null)
                new GameObject("MainGameDroneScan").AddComponent<MainGameDroneScan>();
            if (MainGameTimeLapse.Instance == null)
                gameObject.AddComponent<MainGameTimeLapse>();
            TrySeedHand();
            MainGamePerformanceBudget.RefreshPlanetRendering();
            GameDevTV.RTS.UI.MainGameHUD.Instance?.Refresh(Colony);
            Object.FindAnyObjectByType<GameDevTV.RTS.UI.Containers.BottomBarActionsUI>()
                ?.ApplyMainGameCompactLayout();
            HexGridManager.Instance?.RefreshAllOutlineVisibility();
            // Floating "?" over unrevealed ice — without these the objective text is meaningless.
            if (MainGameIceMarkers.Instance == null)
                new GameObject("MainGameIceMarkers").AddComponent<MainGameIceMarkers>();
            if (MainGameSearchedTiles.Instance == null)
                new GameObject("MainGameSearchedTiles").AddComponent<MainGameSearchedTiles>();
            ApplySliceRenderingPerformanceDefaults();
            PlanetGenerator.Instance?.ApplyAuthoredOrGradientSurface();
            MainGameSearchedTiles.Instance?.Clear();
            MainGameRevealGhosts.ResetSession();
            MainGameBuildingRegistry.Invalidate();
            MainGameIceMarkers.Instance.RebuildAll();
            MainGameWorldQuestionLabel.UpgradeAllSectorQuestionMarks();
            MainGameIceMarkers.Instance.EnsureMarkersForActiveObjectives();
            MainGameSearchedTiles.Instance.RefreshAll();
            ArmResourceFogReapply(4f);
            // MainGame: Command Post only — despawn Acts free gather drones.
            DespawnAutoGatherDronesNearHome();
            // No wall-clock meteor waves during MainGame.
            GameDevTV.RTS.Environment.NaturalEventManager.StopAssaultIfAny();
            GameDevTV.RTS.UI.MainGameHUD.Instance?.RefreshTimeControls();
            Debug.Log($"[MainGameController] MainGame started (tiles={Colony.Board.Tiles.Count}, Acts archived).");
            GameDevTV.RTS.UI.MainGameHUD.Instance?.ShowNote(new System.Collections.Generic.List<string>
            {
                LandingSiteResourceLayout.LandingFlavorLine
            });
            if (MainGamePerfScenario.ShouldApply)
                MainGamePerfScenario.Apply(this);
            FrameCameraOnCommandPost();
        }

        /// <summary>World point the opening camera should center (seated mesh, not sector centroid).</summary>
        public Vector3 CampCameraWorld()
        {
            if (commandPostVisual != null)
                return commandPostVisual.transform.position + Vector3.up * 2.5f;
            return ResolveHomeWorld();
        }

        public void FrameCameraOnCommandPost()
        {
            Vector3 camp = CampCameraWorld();
            if (PlayerInput.Instance != null)
                PlayerInput.Instance.FrameMainGameStartOnCamp(camp);
            else
                PlayerInput.FocusCameraOnWorldPosition(camp);
        }

        /// <summary>Baked survey trails, full-mesh reveal ghosts, overlay fit — default for the vertical slice.</summary>
        static void ApplySliceRenderingPerformanceDefaults()
        {
            // Survey trails: one transparent ground quad + per-hex brown tint — not DirtRoad_* cube meshes.
            MainGamePathGroundOverlay.SetEnabled(true);
            MainGameSearchedTiles.UseBakedPathOverlay = true;
            MainGameSearchedTiles.TileTintOnly = true;
            MainGameSearchedTiles.UseCubeRoadMeshes = false;
            MainGameRevealGhosts.SilhouetteFootprintsOnly = false;
            MainGameRevealGhosts.FrustumCullRevealGhosts = false;
            MainGameRevealGhosts.ShowWorldLabels = false;
            MainGameRevealGhosts.ForceResyncMeshes();
            MainGameRevealGhosts.SuppressRedundantWorldLabels();
            MainGameDroneScan.EnableRubberBandPreview = false;
            if (Instance != null)
                MainGamePathGroundOverlay.Instance?.FitBoard(Instance);
        }

        /// <summary>Perf soak: surveyed path, baked dirt overlay, finished buildings (vertical slice).</summary>
        public void BeginFilledPerfRun()
        {
            MainGamePerfScenario.RequestFilledRun = true;
            BeginNewMainGameRunForPlaytest();
        }

        static void DespawnAutoGatherDronesNearHome()
        {
            Vector3 home = ResolveHomeWorld();
            foreach (var worker in Object.FindObjectsByType<GameDevTV.RTS.Units.Worker>(FindObjectsInactive.Exclude))
            {
                if (worker == null) continue;
                if (worker.Owner != GameDevTV.RTS.Units.Owner.Player1) continue;
                // Survey drone instantiates MiningDroneMesh under MainGameSurveyDrone — do not kill it.
                if (worker.GetComponentInParent<MainGameDroneScan>() != null) continue;
                Transform root = worker.transform.root;
                if (root != null && root.name == "MainGameSurveyDrone") continue;
                if (worker.transform.IsChildOf(root) && root.name.StartsWith("MainGameSurvey")) continue;

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
            Colony ??= MainGameColony.CreateLanding();
            Colony.Board.Tiles.Clear();
            Colony.Board.Sites.Clear();
            Colony.Board.FirstIceTiles.Clear();
            Colony.Board.SecondIceTiles.Clear();
            indexToCell.Clear();
            cellToIndex.Clear();

            Vector3 home = ResolveHomeWorld();
            Vector2Int homeCell = LandingSiteResourceLayout.ActiveLandingCell != default
                ? LandingSiteResourceLayout.ActiveLandingCell
                : ColonyTileGrid.WorldToCell(home);

            RegisterBoardCell(homeCell, MainGameBuildingId.CommandPost);
            RegisterPatchAnchorCells(homeCell);
            var landingLayout = LandingSiteResourceLayout.ForHomeCell(homeCell);
            landingLayout.RegisterBasinOnBoard(cell => RegisterBoardCell(cell, MainGameBuildingId.None));
            MainGameSectorDepositBootstrap.EnsureSectorNodesOnBoard(this);

            Colony.SetDistanceFn((a, b) =>
            {
                if (a < 0 || b < 0 || a >= indexToCell.Count || b >= indexToCell.Count) return 99;
                return ColonyTileGrid.HexDistance(indexToCell[a], indexToCell[b]);
            });
            Colony.SetSupplyDistanceFn(MainGameHaul.HexesFromNearestSupply);

            SeedIceFromSectors(home);
            EnsureCommandPostVisual(home);
            ArmResourceFogReapply(4f);
            int patchHex = ComputePatchHexRadius(homeCell);
            Debug.Log($"[MainGameController] Synced {Colony.Board.Tiles.Count} board cells (patch ~{patchHex} hex from camp); full torus play via lazy tiles.");
        }

        /// <summary>Hex steps from camp to farthest patch corner (matches planet wrap period).</summary>
        public int ComputePatchHexRadius(Vector2Int homeCell)
        {
            var planet = PlanetGenerator.Instance;
            if (planet == null || !planet.TryGetPatchWorldSize(out float mapW, out float mapH))
                return 8;

            int max = 0;
            for (int q = 0; q <= 1; q++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    var world = planet.transform.TransformPoint(new Vector3(q * mapW, 0f, z * mapH));
                    var cornerCell = ColonyTileGrid.WorldToCell(world);
                    max = Mathf.Max(max, ColonyTileGrid.HexDistance(homeCell, cornerCell));
                }
            }

            for (int m = 0; m <= 1; m++)
            {
                var world = planet.transform.TransformPoint(new Vector3(m * mapW, 0f, 0.5f * mapH));
                max = Mathf.Max(max, ColonyTileGrid.HexDistance(homeCell, ColonyTileGrid.WorldToCell(world)));
                world = planet.transform.TransformPoint(new Vector3(0.5f * mapW, 0f, m * mapH));
                max = Mathf.Max(max, ColonyTileGrid.HexDistance(homeCell, ColonyTileGrid.WorldToCell(world)));
            }

            return Mathf.Max(8, max + 1);
        }

        static int ComputePatchHexRadiusClamped(Vector2Int homeCell, int maxVisualRadius, int minVisualRadius)
        {
            var ctrl = Instance;
            int patch = ctrl != null ? ctrl.ComputePatchHexRadius(homeCell) : minVisualRadius;
            return Mathf.Clamp(patch, minVisualRadius, maxVisualRadius);
        }

        void RegisterPatchAnchorCells(Vector2Int homeCell)
        {
            var planet = PlanetGenerator.Instance;
            if (planet == null || !planet.TryGetPatchWorldSize(out float mapW, out float mapH)) return;

            for (int q = 0; q <= 1; q++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    var world = planet.transform.TransformPoint(new Vector3(q * mapW, 0f, z * mapH));
                    RegisterBoardCell(ColonyTileGrid.WorldToCell(world), MainGameBuildingId.None);
                }
            }
        }

        int RegisterBoardCell(Vector2Int cell, MainGameBuildingId building)
        {
            if (cellToIndex.TryGetValue(cell, out int existing))
                return existing;

            int index = Colony.Board.Tiles.Count;
            Colony.Board.Tiles.Add(new MainGameTile
            {
                Index = index,
                Lit = false,
                Revealed = false,
                Building = building
            });
            indexToCell.Add(cell);
            cellToIndex[cell] = index;
            return index;
        }

        /// <summary>
        /// MainGame data marks the home tile as Command Post, but nothing was spawned in-world.
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

            if (seated == null && commandPostVisual != null)
            {
                bool stale = commandPostVisual == null
                             || Vector3.Distance(commandPostVisual.transform.position, padPos) > 12f;
                if (!stale)
                {
                    FrameCameraOnCommandPost();
                    return;
                }

                commandPostVisual = null;
            }

            if (seated != null && Vector3.Distance(seated.transform.position, padPos) > 12f)
            {
                seated.transform.position = padPos;
                GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.GroundBuilding(seated);
            }

            if (seated == null)
            {
                var def = Resources.Load<BuildingSO>("Buildings/Command Post/Command Post");
                if (def == null || def.Prefab == null)
                {
                    Debug.LogWarning("[MainGameController] Command Post BuildingSO/prefab missing — landing pad will be invisible.");
                    return;
                }

                var go = Object.Instantiate(def.Prefab, padPos, Quaternion.identity);
                go.name = "MainGameCommandPost";
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
            const float commandPostScale = 1.42f;
            seated.transform.localScale = Vector3.one * commandPostScale;
            commandPostVisual = seated.gameObject;
            ColonyGroundMesh.Apply(seated.gameObject);
            MainGamePerformanceBudget.ApplyBuildingVisuals(seated.gameObject);
            MainGamePerformanceBudget.SuppressSightVisionMeshes();
            if (pad != null)
            {
                pad.SetOccupied(seated);
                pad.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
            }
            else
            {
                BuildingSiteRegistry.RegisterOccupancy(seated);
            }

            FrameCameraOnCommandPost();
            Debug.Log($"[MainGameController] Command Post seated on pad hex at {padPos}");
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

        /// <summary>Home = orbital landing pad when set, then sector 0 Command Post site, else sector center.</summary>
        static Vector3 ResolveHomeWorld()
        {
            if (LandingSiteResourceLayout.TryGetActiveLandingWorld(out Vector3 landingPad))
                return landingPad;

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
                int sectorIndex = SectorManager.Instance.Sectors.IndexOf(sector);
                if (sectorIndex == 0)
                {
                    // Sector 0 ice pads come from the pre-survey landing hydrology cluster, not sector-center scatter.
                    continue;
                }

                bool sectorIsIce = sector.Feature == SectorManager.SectorFeature.WaterDeposit
                    || sector.Feature == SectorManager.SectorFeature.Glacier;

                foreach (var node in sector.Nodes)
                {
                    bool isIceNode = node.type == SectorNode.NodeType.Feature && sectorIsIce;
                    if (!isIceNode && node.type != SectorNode.NodeType.Feature) continue;
                    if (!sectorIsIce) continue;

                    Vector2Int cell = ColonyTileGrid.WorldToCell(node.position);
                    if (!cellToIndex.TryGetValue(cell, out int index))
                        index = RegisterBoardCell(cell, MainGameBuildingId.None);

                    var tile = Colony.Board.At(index);
                    tile.Feature = sector.Feature == SectorManager.SectorFeature.Glacier
                        ? MainGameFeature.Glacier
                        : MainGameFeature.Ice;
                    tile.DepositLeft = sector.Feature == SectorManager.SectorFeature.Glacier
                        ? 900
                        : 300;
                    // Stay hidden until drone reveal (except we don't reveal feature here).
                }

                // Also mark feature center if no nodes mapped
                if (sectorIsIce)
                {
                    Vector2Int centerCell = ColonyTileGrid.WorldToCell(sector.Center);
                    if (!cellToIndex.TryGetValue(centerCell, out int centerIdx))
                        centerIdx = RegisterBoardCell(centerCell, MainGameBuildingId.None);
                    if (centerIdx >= 0)
                    {
                        var t = Colony.Board.At(centerIdx);
                        if (t.Feature == MainGameFeature.None)
                        {
                            t.Feature = sector.Feature == SectorManager.SectorFeature.Glacier
                                ? MainGameFeature.Glacier
                                : MainGameFeature.Ice;
                            t.DepositLeft = 300;
                        }
                    }
                }
            }

            // Guarantee at least TWO ice deposits so step 4 (second drill) is playable.
            EnsureMinimumIceDeposits(2);
            MainGameSectorDepositBootstrap.BootstrapSectorZero(this, Colony);
            MainGameSectorDepositBootstrap.BootstrapDistantSectors(this, Colony);
            int homeTile = -1;
            for (int i = 0; i < Colony.Board.Tiles.Count; i++)
                if (Colony.Board.At(i).Building == MainGameBuildingId.CommandPost) homeTile = i;
            if (homeTile >= 0)
                EnforceOpeningIceInCorridor(homeTile, new System.Collections.Generic.List<int>());
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
                if (t.Building == MainGameBuildingId.CommandPost) homeIndex = t.Index;
                if ((t.Feature == MainGameFeature.Ice || t.Feature == MainGameFeature.Glacier) && t.DepositLeft > 0)
                    iceTiles.Add(i);
            }
            if (homeIndex < 0) homeIndex = 0;

            int need = minCount - iceTiles.Count;
            if (need > 0 && TryGetCell(homeIndex, out Vector2Int homeCell))
            {
                var layout = LandingSiteResourceLayout.ForHomeCell(homeCell);
                layout.RegisterBasinOnBoard(cell => RegisterBoardCell(cell, MainGameBuildingId.None));
                layout.ApplyIcePads(Colony, iceTiles, minCount);
                need = minCount - iceTiles.Count;
            }

            if (need <= 0)
            {
                EnforceOpeningIceInCorridor(homeIndex, iceTiles);
                Colony?.RefreshSecondIceTiles();
                MainGameIceMarkers.Instance?.Refresh();
                return;
            }

            int maxIceRing = TryGetCell(homeIndex, out Vector2Int homeCellForIce)
                ? LandingSiteResourceLayout.ForHomeCell(homeCellForIce).FarthestResourceHex() + 2
                : LandingSiteResourceLayout.SurveyCorridorMaxHex;
            for (int n = 0; n < need; n++)
            {
                bool firstPad = iceTiles.Count == 0;
                int ringCap = firstPad
                    ? LandingSiteResourceLayout.FirstObjectiveIceMaxHex
                    : maxIceRing;
                int targetDist = firstPad ? 5 : 6;
                int best = -1;
                int bestScore = int.MinValue;
                for (int i = 0; i < Colony.Board.Tiles.Count; i++)
                {
                    var t = Colony.Board.At(i);
                    if (t.Building != MainGameBuildingId.None) continue;
                    if (t.Feature == MainGameFeature.Ice || t.Feature == MainGameFeature.Glacier) continue;
                    int dHome = Colony.Dist(homeIndex, i);
                    if (dHome < MainGameSectorDepositBootstrap.MinHexDistFromCamp || dHome > ringCap) continue;

                    int minIce = 99;
                    foreach (int ice in iceTiles)
                        minIce = Mathf.Min(minIce, Colony.Dist(ice, i));
                    // First objective ice stays inside the landing survey corridor; second pad spreads out.
                    int score = minIce * 10 - Mathf.Abs(dHome - targetDist);
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
                        if (t.Building != MainGameBuildingId.None) continue;
                        if (t.Feature == MainGameFeature.Ice || t.Feature == MainGameFeature.Glacier) continue;
                        int dHome = Colony.Dist(homeIndex, i);
                        if (dHome < 3 || dHome > maxIceRing) continue;
                        best = i;
                        break;
                    }
                }
                if (best < 0) break;

                var pad = Colony.Board.At(best);
                pad.Feature = MainGameFeature.Ice;
                pad.DepositLeft = 300;
                pad.Revealed = false;
                iceTiles.Add(best);
                Debug.Log($"[MainGameController] Seeded MainGame ice deposit at tile {best}.");
            }

            EnforceOpeningIceInCorridor(homeIndex, iceTiles);
            Colony?.RefreshSecondIceTiles();
            MainGameIceMarkers.Instance?.Refresh();
        }

        /// <summary>Step-0 ice must live on the landing hydrology cell, not scatter fallbacks far from camp.</summary>
        void EnforceOpeningIceInCorridor(int homeIndex, System.Collections.Generic.List<int> iceTiles)
        {
            if (homeIndex < 0 || Colony == null || !TryGetCell(homeIndex, out Vector2Int homeCell))
                return;
            var layout = LandingSiteResourceLayout.ForHomeCell(homeCell);
            layout.RegisterBasinOnBoard(cell => RegisterBoardCell(cell, MainGameBuildingId.None));

            for (int i = 0; i < Colony.Board.Tiles.Count; i++)
            {
                var t = Colony.Board.At(i);
                if (t.Feature != MainGameFeature.Ice && t.Feature != MainGameFeature.Glacier) continue;
                if (t.Revealed || t.Building != MainGameBuildingId.None) continue;
                if (!TryGetCell(i, out Vector2Int c)) continue;
                if (c == layout.IcePrimary || c == layout.IceSecondary) continue;
                if (layout.IsLandingBasinCell(c)
                    && Colony.Dist(homeIndex, i) <= LandingSiteResourceLayout.FirstObjectiveIceMaxHex + 1)
                    continue;
                t.Feature = MainGameFeature.None;
                t.DepositLeft = 0;
                iceTiles.Remove(i);
            }

            iceTiles.Clear();
            for (int i = 0; i < Colony.Board.Tiles.Count; i++)
            {
                var t = Colony.Board.At(i);
                if ((t.Feature == MainGameFeature.Ice || t.Feature == MainGameFeature.Glacier) && t.DepositLeft > 0)
                    iceTiles.Add(i);
            }

            layout.ApplyIcePads(Colony, iceTiles, 2);

            int primaryIndex = RegisterBoardCell(layout.IcePrimary, MainGameBuildingId.None);
            var primaryPad = Colony.Board.At(primaryIndex);
            primaryPad.Feature = MainGameFeature.Ice;
            primaryPad.DepositLeft = 300;
            primaryPad.Revealed = false;
            int secondaryIndex = RegisterBoardCell(layout.IceSecondary, MainGameBuildingId.None);
            var secondaryPad = Colony.Board.At(secondaryIndex);
            if (secondaryPad.Feature == MainGameFeature.None)
            {
                secondaryPad.Feature = MainGameFeature.Ice;
                secondaryPad.DepositLeft = 300;
                secondaryPad.Revealed = false;
            }

            int nearestDist = int.MaxValue;
            for (int i = 0; i < Colony.Board.Tiles.Count; i++)
            {
                var t = Colony.Board.At(i);
                if (t.DepositLeft <= 0) continue;
                if (t.Feature != MainGameFeature.Ice && t.Feature != MainGameFeature.Glacier) continue;
                if (t.Revealed) continue;
                nearestDist = Mathf.Min(nearestDist, Colony.Dist(homeIndex, i));
            }

            if (nearestDist > LandingSiteResourceLayout.FirstObjectiveIceMaxHex)
            {
                Debug.LogError(
                    $"[MainGameController] Nearest ice still {nearestDist} hex from camp after layout seat " +
                    $"(cap {LandingSiteResourceLayout.FirstObjectiveIceMaxHex}).");
            }
        }

        public bool TryGetTileIndex(Vector3 world, out int index)
        {
            Vector2Int cell = ColonyTileGrid.WorldToCell(world);
            if (cellToIndex.TryGetValue(cell, out index))
                return true;
            if (PlanetGenerator.Instance == null)
                return false;
            index = RegisterBoardCell(cell, MainGameBuildingId.None);
            return true;
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
                MainGameTimeLapse.Instance?.EndRun();
                return;
            }
            bool alreadyComplete = Colony.MainGameStep >= MainGameData.MainGameSteps;
            MainGameSurveyMission.Instance?.FinishOnEndWeek();
            MainGameTimeLapse.Instance?.NotifyManualEndWeek();
            var log = Colony.EndWeek();
            MainGameConstruction.NotifyWeekAdvanced();
            foreach (string line in log)
                Debug.Log($"[MainGame Week {Colony.Week}] {line}");
            if (Colony.ContractCancelled)
                MainGameTimeLapse.Instance?.EndRun();
            var hud = GameDevTV.RTS.UI.MainGameHUD.Instance;
            if (alreadyComplete)
                hud?.ShowNote(log);
            else
                hud?.ShowWeekReport(log);
            GameDevTV.RTS.UI.MainGameHUD.Instance?.Refresh(Colony);
            RefreshHandUnlocks();
            MainGameIceMarkers.Instance?.Refresh();

            TryCelebrateMainGameVictory();
        }

        /// <summary>Step 6 reached — show the same victory overlay as Colony Acts (once per run).</summary>
        public void TryCelebrateMainGameVictory()
        {
            if (prologueVictoryCelebrated || Colony == null || Colony.ContractCancelled) return;
            if (Colony.MainGameStep < MainGameData.MainGameSteps) return;

            prologueVictoryCelebrated = true;
            MainGameTimeLapse.Instance?.EndRun();
            GameOverManager.LastOutcomeDetail =
                "Corporation contract held — ice, air, housing, and the dust storm. The opening mission is complete.";
            GameOverUI.EnsureAllSubscribed();
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.TriggerVictory();
            else
                Debug.LogError("[MainGameController] GameOverManager missing — cannot show prologue victory.");
        }

        public void RefreshHandUnlocks()
        {
            var deck = CardDeckController.Instance;
            if (deck == null || refreshingHand) return;
            refreshingHand = true;
            ignoreNextDeckChanged = true;
            try
            {
                if (!deck.RebuildMainGameHand(Colony))
                    return;
                GameDevTV.RTS.UI.MainGameHUD.Instance?.Refresh(Colony);
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

            MainGameGroundVisualTuning.Tick();

            // Planet gen / Acts bootstrap can re-show deposits after the first fog pass.
            if (Time.unscaledTime < fogReapplyUntil && Time.frameCount % 15 == 0)
                MainGameResourceFog.ApplyHiddenState();
        }

        public void ArmResourceFogReapply(float seconds = 3f)
        {
            fogReapplyUntil = Mathf.Max(fogReapplyUntil, Time.unscaledTime + seconds);
            MainGameResourceFog.ApplyHiddenState();
        }

        /// <summary>
        /// Fresh prologue run for player-style playtests: board sync, deposit bootstrap, fog, ice markers.
        /// </summary>
        public void BeginNewMainGameRunForPlaytest()
        {
            MainGameSurveyMission.Instance?.AbortForPlaytest();
            MainGameDroneScan.Instance?.SetScanMode(false);
            Colony = MainGameColony.CreateLanding();
            syncedFromPlanet = false;
            handSeeded = false;
            openingBriefingShown = false;
            prologueVictoryCelebrated = false;
            MainGameUiPlaythrough.ResetSession();
            MainGameIceMarkers.Instance?.ClearAllMarkers();
            MainGameWorldQuestionLabel.UpgradeAllSectorQuestionMarks();
            if (PlanetGenerator.Instance != null)
                HandlePlanetGenerated();
            else
                TrySyncWhenReady();
            PlayerInput.Instance?.ApplyMainGameDefaultZoom();
            var hand = Object.FindAnyObjectByType<GameDevTV.RTS.UI.Containers.BottomBarActionsUI>();
            hand?.ApplyMainGameCompactLayout();
            Debug.Log("[MainGameController] BeginNewMainGameRunForPlaytest — bootstrap applied.");
        }
    }
}
