using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Audio;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.Containers;

namespace GameDevTV.RTS.Player
{
    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private CameraConfig cameraConfig;
        [SerializeField] private LayerMask selectableUnitsLayers;
        [SerializeField] private LayerMask interactableLayers;
        [SerializeField] private LayerMask floorLayers;
        [SerializeField] private bool highlightTrace;
        [SerializeField] private RectTransform selectionBox;
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color errorTintColor = Color.red;
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color errorFresnelColor = new (4, 1.7f, 0, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color availableToPlaceTintColor = new (0.2f, 0.65f, 1, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color availableToPlaceFresnelColor = new(4, 1.7f, 0, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color joinedTileTintColor = new (0.15f, 1.1f, 0.35f, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color joinedTileFresnelColor = new (0.4f, 4f, 1.2f, 2);

        private Vector2 startingMousePosition;

        private BaseCommand activeCommand;
        private List<ISelectable> commandTargetUnits = new(12);
        private GameObject ghostInstance;
        private Renderer ghostRenderer;
        private GameObject tileFootprint;
        private readonly List<LineRenderer> joinLines = new();
        private readonly List<BaseBuilding> joinNeighbors = new();
        private Vector2Int? tileGhostStickyCell;
        private int lastJoinCount = -1;
        private bool lastGhostRestrictionsPass = true;
        private int restrictionAgreeFrames;
        private bool displayedRestrictionsPass = true;
        private Material tileFootprintMaterial;
        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int FRESNEL = Shader.PropertyToID("_FresnelColor");
        private static readonly RaycastHit[] ghostRayHits = new RaycastHit[24];
        private const int RestrictionHysteresisFrames = 4;
        private bool wasMouseDownOnUI;
        private CinemachineFollow cinemachineFollow;
        private float zoomStartTime;
        private float rotationStartTime;
        private Vector3 startingFollowOffset;
        private float targetZoomDistance;
        private float maxRotationAmount;
        private HashSet<AbstractUnit> aliveUnits = new(100);
        private HashSet<AbstractUnit> addedUnits = new(24);
        private List<ISelectable> selectedUnits = new(12);

        private bool hasMouseMoved;
        private Vector2 lastMousePosition;
        private int currentBaseIndex = -1;
        private GlobalCommander globalCommander;
        private GameDevTV.RTS.Environment.HexGridManager.HexTile currentHex;
        private GameDevTV.RTS.Environment.HexGridManager.HexTile hoveredHex;

        public static PlayerInput Instance { get; private set; }

        /// <summary>Pan the follow target over a world point (keeps current camera height).</summary>
        public static void FocusCameraOnWorldPosition(Vector3 worldPosition)
        {
            if (Instance == null || Instance.cameraTarget == null) return;
            worldPosition.y = Instance.cameraTarget.position.y;
            Instance.cameraTarget.position = worldPosition;
            Instance.currentHex?.SetHighlighted(false);
            Instance.currentHex = HexGridManager.Instance?.GetNearestHex(worldPosition);
            Instance.currentHex?.SetHighlighted(true);
        }

        /// <summary>Current camera follow target on the map (XZ focus point).</summary>
        public static Vector3 GetCameraFocusPosition()
        {
            if (Instance?.cameraTarget != null) return Instance.cameraTarget.position;
            return Vector3.zero;
        }

        private void Awake()
        {
            Instance = this;

            // Scene-serialized true floods the console; keep hover/selection visuals, drop log spam.
            highlightTrace = false;
            HexGridManager.SetHighlightTrace(highlightTrace);

            if (playerCamera == null)
            {
                playerCamera = GetComponent<Camera>();
            }

            if (cameraTarget != null)
            {
                var rb = cameraTarget.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
            }

            lastMousePosition = Mouse.current.position.ReadValue();
            hasMouseMoved = false;

            if (!cinemachineCamera.TryGetComponent(out cinemachineFollow))
            {
                Debug.LogError("Cinemachine Camera did not have CinemachineFollow. Zoom functionality will not work!");
            }
            else
            {
                startingFollowOffset = cinemachineFollow.FollowOffset;
                targetZoomDistance = startingFollowOffset.y;
                maxRotationAmount = Mathf.Abs(cinemachineFollow.FollowOffset.z);
            }

            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] += HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] += HandleUnitSpawn;
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += HandleBuildingSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] += HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;

            GameDevTV.RTS.Environment.PlanetGenerator.OnPlanetGenerated += CenterCameraOnMap;
            HexGridManager.OnStartingAreaRevealed += HandleStartingAreaRevealed;
        }

        private void Start()
        {
            // FORCE re-find the Camera Target to overwrite any corrupted serialization
            // caused by changing the variable type from Rigidbody to Transform!
            var camTargetObj = GameObject.Find("Camera Target");
            if (camTargetObj != null)
            {
                cameraTarget = camTargetObj.transform;
                Debug.Log($"[PlayerInput] Found Camera Target: {cameraTarget.name} at position {cameraTarget.position}");
                
                // Ensure the Camera Target's Rigidbody is kinematic so panning works
                var rb = cameraTarget.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    Debug.Log($"[PlayerInput] Set Rigidbody isKinematic=true, was: false");
                }
                else
                {
                    Debug.Log("[PlayerInput] No Rigidbody found on Camera Target");
                }

                // Keep the follow target out of gameplay raycasts.
                foreach (var col in camTargetObj.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
            }
            else
            {
                Debug.LogError("[PlayerInput] 'Camera Target' GameObject could not be found! Panning will not work!");
                // Create a fallback Camera Target if it doesn't exist
                GameObject fallbackTarget = new GameObject("Camera Target");
                fallbackTarget.transform.position = new Vector3(0, 10, 0);
                cameraTarget = fallbackTarget.transform;
                Debug.Log($"[PlayerInput] Created fallback Camera Target at position {cameraTarget.position}");
                
                // Ensure the fallback Camera Target's Rigidbody is kinematic
                var rb = cameraTarget.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    Debug.Log($"[PlayerInput] Set fallback Rigidbody isKinematic=true");
                }
                else
                {
                    Debug.Log("[PlayerInput] No Rigidbody found on fallback Camera Target");
                }
            }

            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.HasGenerated)
            {
                CenterCameraOnMap();
            }
            
            // Critical Failsafe 1: Ensure Main Camera has a CinemachineBrain!
            if (playerCamera != null)
            {
                if (!playerCamera.TryGetComponent<Unity.Cinemachine.CinemachineBrain>(out _))
                {
                    playerCamera.gameObject.AddComponent<Unity.Cinemachine.CinemachineBrain>();
                    Debug.LogWarning("[PlayerInput] Repaired missing CinemachineBrain on Main Camera!");
                }
            }

            // Critical Failsafe 2: Ensure the Cinemachine Camera is actually following the Camera Target!
            if (cinemachineCamera != null && cameraTarget != null)
            {
                Debug.Log($"[PlayerInput] CinemachineCamera: {cinemachineCamera}, cameraTarget: {cameraTarget}");
                Debug.Log($"[PlayerInput] CinemachineCamera.Follow: {cinemachineCamera.Follow}, cameraTarget.name: {cameraTarget.name}");
                
                if (cinemachineCamera.Follow == null || cinemachineCamera.Follow != cameraTarget)
                {
                    Debug.LogWarning($"[PlayerInput] CinemachineCamera.Follow is null or doesn't match cameraTarget. Setting Follow to cameraTarget.");
                    cinemachineCamera.Follow = cameraTarget;
                    Debug.LogWarning("[PlayerInput] Repaired broken Cinemachine Camera! It was not following the Camera Target.");
                }
                else
                {
                    Debug.Log($"[PlayerInput] Cinemachine Camera is correctly following Camera Target: {cinemachineCamera.Follow.name}");
                }
            }
            else
            {
                Debug.LogError($"[PlayerInput] Cannot setup Cinemachine follow. cinemachineCamera: {cinemachineCamera}, cameraTarget: {cameraTarget}");
            }

            // Ensure CameraConfig is initialized
            if (cameraConfig == null)
            {
                cameraConfig = new CameraConfig();
                Debug.LogWarning("[PlayerInput] CameraConfig was null, created default instance");
            }
            else
            {
                Debug.Log($"[PlayerInput] CameraConfig initialized with MousePanSpeed={cameraConfig.MousePanSpeed}, KeyboardPanSpeed={cameraConfig.KeyboardPanSpeed}, ZoomSpeed={cameraConfig.ZoomSpeed}");
            }

            globalCommander = FindAnyObjectByType<GlobalCommander>();
            Debug.Log($"[PlayerInput] Start() completed. cameraTarget={cameraTarget}, cameraConfig={cameraConfig}, cinemachineCamera={cinemachineCamera}");
        }

        private GlobalCommander GetGlobalCommander()
        {
            if (globalCommander == null)
            {
                globalCommander = FindAnyObjectByType<GlobalCommander>();
                
                // Ensure CameraConfig is initialized
                if (cameraConfig == null)
                {
                    cameraConfig = new CameraConfig();
                    Debug.LogWarning("[PlayerInput] CameraConfig was null, created default instance");
                }
            }
            return globalCommander;
        }

        private bool hasCameraBeenFocused = false;
        private bool hasCameraSnappedToCommandPost = false;

        private void CenterCameraOnMap()
        {
            if (cameraTarget == null) return;
            if (hasCameraBeenFocused) return;
            
            // PlanetGenerator owns the generated starting-sector position.
            var planetGenerator = PlanetGenerator.Instance;
            if (planetGenerator != null && planetGenerator.HasGenerated)
            {
                Vector3 startingPosition = planetGenerator.StartingAreaCenter;
                globalCommander = FindAnyObjectByType<GlobalCommander>();
                if (globalCommander != null)
                {
                    startingPosition.y = globalCommander.transform.position.y;
                    globalCommander.transform.position = startingPosition;
                }

                MoveToStartingHex(startingPosition);
                hasCameraBeenFocused = true;
                return;
            }

            // Prioritize centering on the base if one exists
            var baseBuilding = BaseBuilding.ActiveBuildings.FirstOrDefault(b => b.Owner == Owner.Player1);
            if (baseBuilding != null)
            {
                Vector3 basePos = baseBuilding.transform.position;
                basePos.y = cameraTarget.position.y;
                cameraTarget.position = basePos;
                hasCameraBeenFocused = true;
                return;
            }

            // Fallback: Center on the map
            if (GameDevTV.RTS.Environment.PlanetGenerator.Instance != null && GameDevTV.RTS.Environment.PlanetGenerator.Instance.Config != null)
            {
                float mapWidth = GameDevTV.RTS.Environment.PlanetGenerator.Instance.Config.MapWidth * GameDevTV.RTS.Environment.PlanetGenerator.Instance.CellSize;
                float mapHeight = GameDevTV.RTS.Environment.PlanetGenerator.Instance.Config.MapHeight * GameDevTV.RTS.Environment.PlanetGenerator.Instance.CellSize;
                
                Vector3 pos = cameraTarget.position;
                pos.x = mapWidth / 2f;
                pos.z = mapHeight / 2f;
                cameraTarget.position = pos;
            }
        }

        private void MoveToStartingHex(Vector3 targetPosition)
        {
            if (cameraTarget == null) return;

            targetPosition.y = cameraTarget.position.y;
            cameraTarget.position = targetPosition;
            currentHex?.SetHighlighted(false);
            currentHex = HexGridManager.Instance?.GetNearestHex(cameraTarget.position);
            currentHex?.SetHighlighted(true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] -= HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] -= HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] -= HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
            
            GameDevTV.RTS.Environment.PlanetGenerator.OnPlanetGenerated -= CenterCameraOnMap;
            HexGridManager.OnStartingAreaRevealed -= HandleStartingAreaRevealed;
        }

        private void HandleStartingAreaRevealed()
        {
            currentHex = null;
            hoveredHex = null;
            InitializeCurrentHex();
        }

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (!selectedUnits.Contains(evt.Unit))
            {
                selectedUnits.Add(evt.Unit);
            }
        }
        private void HandleUnitDeselected(UnitDeselectedEvent evt)
        {
            selectedUnits.Remove(evt.Unit);
        }


        private void HandleUnitSpawn(UnitSpawnEvent evt) => aliveUnits.Add(evt.Unit);


        // Maybe I don't want to automatically snap the first command post.
        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            // Auto-snap to the first Command building that appears for the player (e.g. initial base)
            if (!hasCameraSnappedToCommandPost && evt.Building != null && evt.Building.BuildingSO != null 
                && evt.Building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                if (cameraTarget != null)
                {
                    Vector3 targetPos = evt.Building.transform.position;
                    targetPos.y = cameraTarget.position.y;
                    cameraTarget.position = targetPos;
                    hasCameraSnappedToCommandPost = true;
                }
            }
        }


        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            aliveUnits.Remove(evt.Unit);
            selectedUnits.Remove(evt.Unit);
        }

        private void HandleActionSelected(CommandSelectedEvent evt)
        {
            ClearGhostVisuals();
            activeCommand = evt.Command;
            commandTargetUnits = new List<ISelectable>(selectedUnits);

            // Legacy: Command Post select used to pan to the nearest empty sector center.
            // That pulled the first CP far from a Solar the player had just placed.
            // Card plays (HandIndex >= 0) keep the camera where the player is looking.
            if (activeCommand is BuildBuildingCommand commandPostBbc
                && commandPostBbc.HandIndex < 0
                && commandPostBbc.Building != null
                && commandPostBbc.Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                var sectorManager = SectorManager.Instance;
                if (sectorManager != null)
                {
                    // Ensure sectors are ready even at the very start of the game
                    if (sectorManager.Sectors.Count == 0) sectorManager.InitializeSectors();

                    if (sectorManager.Sectors.Count > 0)
                    {
                        // Use the current selection as the reference point for expansion. 
                        // If nothing is selected, fall back to the camera center or starting base.
                        Vector3 refPos = cameraTarget != null ? cameraTarget.position : Vector3.zero;
                        
                        AbstractCommandable firstSelected = selectedUnits.FirstOrDefault() as AbstractCommandable;
                        if (firstSelected != null)
                        {
                            refPos = firstSelected.transform.position;
                        }
                        else
                        {
                            GlobalCommander commander = GetGlobalCommander();
                            if (commander != null) refPos = commander.transform.position;
                        }

                        var nearestUnoccupied = sectorManager.Sectors
                            .Where(s => !s.IsOccupied && !s.IsLocked)
                            .OrderBy(s => Vector3.Distance(refPos, s.Center))
                            .FirstOrDefault();

                        if (nearestUnoccupied != null && cameraTarget != null)
                        {
                            Vector3 targetCameraPos = nearestUnoccupied.Center;
                            targetCameraPos.y = cameraTarget.position.y;
                            cameraTarget.position = targetCameraPos;
                            hasCameraBeenFocused = true;
                        }
                    }
                }
            }

            // if this thing does not require a click to activate...
            // Should probably say requires placement click to activate
            if (!activeCommand.RequiresClickToActivate)
            {
                ActivateAction(new RaycastHit());
            }
            else
            {
                GameObject prefabToInstantiate = activeCommand.GhostPrefab;

                // Fall back to the solid building prefab only if no custom ghost prefab was specified
                if (prefabToInstantiate == null && activeCommand is BuildBuildingCommand bbc && bbc.Building != null && bbc.Building.Prefab !=null)
                {
                    prefabToInstantiate = bbc.Building.Prefab;
                }

                if (prefabToInstantiate != null)
                {
                    ghostInstance = Instantiate(prefabToInstantiate);
                    ghostInstance.name = "Ghost_" + prefabToInstantiate.name;

                    if (ghostInstance.TryGetComponent(out BaseBuilding bb))
                    {
                        // Pass PlacementMaterial so InitializeAsGhost can swap to the ghost
                        // material even if GhostPrefab points to the solid building prefab.
                        Material ghostMat = activeCommand is BuildBuildingCommand bbc2 && bbc2.Building != null
                            ? bbc2.Building.PlacementMaterial
                            : null;
                        bb.InitializeAsGhost(ghostMat, Owner.Player1);
                        // Must leave ActiveBuildings — otherwise the ghost occupies its own
                        // tile cell and snap thrash-loops between neighbors every frame.
                        bb.enabled = false;
                    }

                    ghostRenderer = ghostInstance.GetComponentInChildren<Renderer>();
                    if (ghostRenderer != null && ghostRenderer.material != null)
                    {
                        ghostRenderer.material.SetColor(TINT, availableToPlaceTintColor);
                        ghostRenderer.material.SetColor(FRESNEL, availableToPlaceFresnelColor);
                    }
                    
                    // We only want the visuals for the ghost, so strip any colliders/navmesh obstacles
                    // to prevent it from interfering with the game while dragging!
                    foreach (var col in ghostInstance.GetComponentsInChildren<Collider>()) Destroy(col);
                    foreach (var nav in ghostInstance.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) Destroy(nav);

                    // Kill leftover animation / VFX scripts that can strobe translucent mats.
                    foreach (var mb in ghostInstance.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null || mb is Transform) continue;
                        mb.enabled = false;
                    }
                }
            }
        }

        private void Update()
        {
            // Stop everything if focus is lost. This prevents the camera from scrolling
            // forever if the user alt-tabs or clicks away while moving.
            if (!Application.isFocused) 
            {
                // Optional: hasMouseMoved = false; // Could reset this if desired
                // return; // COMMENTED OUT FOR TESTING: Never block input!
            }

            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            if (!hasMouseMoved && (currentMousePos - lastMousePosition).sqrMagnitude > 100f)
            {
                hasMouseMoved = true;
            }
            lastMousePosition = currentMousePos;

            BuildingSiteSelectionController.ActivatePendingMarkersIfNeeded();
            HandleSiteSelectionCancel();

            InitializeCurrentHex();
            HandleHexHover();
            HandlePanning();
            HandleZooming();
            HandleRotation();
            HandleGhost();
            HandleRightClick();
            HandleDragSelect();
            HandleBasePaging();
            HandleDemolishHotkey();
            HandleCheats();
        }

        /// <summary>Select a building, then Delete/Backspace to scrap it for Materials refund.</summary>
        private void HandleDemolishHotkey()
        {
            if (Keyboard.current == null) return;
            if (Time.timeScale <= 0.01f) return;
            bool pressed = Keyboard.current.deleteKey.wasPressedThisFrame
                || Keyboard.current.backspaceKey.wasPressedThisFrame;
            if (!pressed) return;

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i] is not BaseBuilding building) continue;
                if (building is GlobalCommander) continue;
                if (building.Owner != Owner.Player1) continue;
                building.TryDemolish(refund: true);
                return;
            }
        }



        private void HandleBasePaging()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                if (BuildingSiteSelectionController.IsSelecting)
                {
                    BuildingSiteSelectionController.CycleFocus(-1);
                    return;
                }
                PageBases(-1);
            }
            else if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (BuildingSiteSelectionController.IsSelecting)
                {
                    BuildingSiteSelectionController.CycleFocus(1);
                    return;
                }
                PageBases(1);
            }
        }

        private void HandleCheats()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.kKey.wasPressedThisFrame && GenerationManager.Instance != null)
            {
                if (Keyboard.current.shiftKey.isPressed)
                {
                    GenerationManager.Instance.CheatSkipToExpansion();
                }
                else
                {
                    GenerationManager.Instance.CheatCompleteGeneration();
                }
            }

            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                float currentVal = Supplies.Temperature.TryGetValue(Owner.Player1, out float val) ? val : -60f;
                float change = Keyboard.current.shiftKey.isPressed ? -5f : 5f;
                Supplies.UpdateTemperature(Owner.Player1, currentVal + change);
            }

            if (Keyboard.current.yKey.wasPressedThisFrame)
            {
                float currentVal = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float val) ? val : 0.01f;
                float change = Keyboard.current.shiftKey.isPressed ? -0.05f : 0.05f;
                Supplies.UpdateAtmosphere(Owner.Player1, Mathf.Max(0.01f, currentVal + change));
            }

            if (Keyboard.current.uKey.wasPressedThisFrame)
            {
                float currentVal = Supplies.Water.TryGetValue(Owner.Player1, out float val) ? val : 0f;
                float change = Keyboard.current.shiftKey.isPressed ? -5f : 5f;
                Supplies.UpdateWater(Owner.Player1, Mathf.Clamp(currentVal + change, 0f, 100f));
            }
        }

        private void PageBases(int direction)
        {   // use set builder notation to get all player-owned buildings and foundries in the game, 
            // including ones that are not active. 
            var commandPosts = BaseBuilding.ActiveBuildings
                .Where(b => b != null && b.Owner == Owner.Player1 &&
                       (b.name.Contains("Command") || b.name.Contains("Foundry") || 
                       (b.BuildingSO != null && (b.BuildingSO.Name.Contains("Command") || b.BuildingSO.Name.Contains("Foundry")))) &&
                       b.Progress.State == BuildingProgress.BuildingState.Completed)
                .Cast<AbstractCommandable>()
                .ToList();

            GlobalCommander commander = GetGlobalCommander();
            if (commander != null)
            {
                commandPosts.Add(commander);
            }



            commandPosts = commandPosts.OrderBy(b => b.transform.position.x)
                .ThenBy(b => b.transform.position.z)
                .ToList();

            if (commandPosts.Count == 0) return;

            currentBaseIndex += direction;
            if (currentBaseIndex < 0) currentBaseIndex = commandPosts.Count - 1;
            if (currentBaseIndex >= commandPosts.Count) currentBaseIndex = 0;

            AbstractCommandable target = commandPosts[currentBaseIndex];
            if (target != null && cameraTarget != null)
            {
                Vector3 pos = target.transform.position;
                pos.y = cameraTarget.position.y; // Keep current camera height
                cameraTarget.position = pos;

                // Automatically select the base when paged to.
                DeselectAllUnits();
                target.Select();
            }
        }

        private void HandleSiteSelectionCancel()
        {
            if (!BuildingSiteSelectionController.IsSelecting || Keyboard.current == null) return;

            if (Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                BuildingSiteSelectionController.Cancel();
            }
        }

        private static bool ShouldIgnoreSelectionHit(Collider collider)
        {
            if (collider == null) return true;
            if (collider.gameObject.name == "Selection Indicator") return true;
            if (collider.gameObject.name == "Camera Target") return true;
            if (collider.gameObject.layer == 12) return true; // World Bounds
            if (collider.GetComponentInParent<BuildingSiteMarker>() != null) return true;
            if (collider.gameObject.name.Contains("GhostPreview", System.StringComparison.OrdinalIgnoreCase)) return true;
            // Invisible UCC stand-in must never block unit/building clicks.
            if (collider.GetComponentInParent<GlobalCommander>() != null) return true;
            // Disabled building components on site ghosts must never steal unit selection.
            var building = collider.GetComponentInParent<BaseBuilding>();
            if (building != null && (!building.enabled || !BuildingSiteSlot.IsValidOccupant(building))) return true;
            return false;
        }

        private void HandleGhost()
        {
            if (ghostInstance == null || ghostRenderer == null) return;

            if (Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                ClearGhostVisuals();
                activeCommand = null;
                return;
            }

            bool cardTilePlace = activeCommand is BuildBuildingCommand cardBbc && cardBbc.HandIndex >= 0;
            Vector3? hitPos = cardTilePlace
                ? RaycastGroundForTileGhost()
                : RaycastGhostPoint();

            if (!hitPos.HasValue)
            {
                // Keep last ghost pose — don't thrash visuals when the ray misses a frame.
                return;
            }

            int joinCount = 0;

            if (activeCommand is BuildBuildingCommand bbc && bbc.Building != null
                && bbc.Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                hitPos = bbc.SnapToNearestSector(hitPos.Value);
            }

            if (cardTilePlace)
            {
                Vector2Int? previousSticky = tileGhostStickyCell;
                hitPos = ColonyTileGrid.SnapForPlacement(
                    hitPos.Value, Owner.Player1, ref tileGhostStickyCell, out joinCount);

                // Mines lock to the matching discovered deposit tile (not free ground).
                if (activeCommand is BuildBuildingCommand mineBbc
                    && BuildingSiteRegistry.IsMineBuilding(mineBbc.Building)
                    && DiscoverySystem.TrySnapToMineDeposit(
                        mineBbc.Building, hitPos.Value, out Vector3 mineSnap, out Vector2Int mineCell))
                {
                    hitPos = mineSnap;
                    tileGhostStickyCell = mineCell;
                    joinCount = ColonyTileGrid.CountOrthogonalNeighbors(mineCell, Owner.Player1);
                }

                // Tick when the ghost settles on a new grid cell (louder when joining a neighbor).
                if (tileGhostStickyCell.HasValue
                    && (!previousSticky.HasValue || previousSticky.Value != tileGhostStickyCell.Value))
                {
                    AudioManager.Instance.PlayTileSnapSound(joining: joinCount > 0);
                }
            }

            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter
            {
                agentTypeID = 0,
                areaMask = UnityEngine.AI.NavMesh.AllAreas
            };
            if (UnityEngine.AI.NavMesh.SamplePosition(hitPos.Value, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                if (cardTilePlace)
                    hitPos = new Vector3(hitPos.Value.x, navHit.position.y, hitPos.Value.z);
                else
                    hitPos = navHit.position;
            }

            Vector3 snapTarget = hitPos.Value;
            // Hard snap to the cell — lerp made occupancy thrash look like a strobing animation.
            ghostInstance.transform.position = snapTarget;
            UpdateTileFootprint(snapTarget, cardTilePlace, joinCount);

            bool allRestrictionsPass = activeCommand.AllRestrictionsPass(snapTarget);
            if (cardTilePlace && activeCommand is BuildBuildingCommand powerBbc
                && !PowerGridManager.CanPlayBuildingForPower(powerBbc.Building, Owner.Player1))
            {
                allRestrictionsPass = false;
            }
            if (cardTilePlace && activeCommand is BuildBuildingCommand mineGateBbc
                && BuildingSiteRegistry.IsMineBuilding(mineGateBbc.Building)
                && !DiscoverySystem.IsOnDiscoveredMineDeposit(mineGateBbc.Building, snapTarget))
            {
                allRestrictionsPass = false;
            }
            if (cardTilePlace && activeCommand is BuildBuildingCommand matBbc
                && matBbc.Building != null)
            {
                int need = ReservedSiteBuildUtility.GetMaterialsCost(matBbc.Building);
                int have = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                if (need > 0 && have < need)
                    allRestrictionsPass = false;
            }

            // Hysteresis so NavMesh / overlap edge cases don't strobe red/blue every frame.
            if (allRestrictionsPass == displayedRestrictionsPass)
            {
                restrictionAgreeFrames = RestrictionHysteresisFrames;
            }
            else
            {
                restrictionAgreeFrames++;
                if (restrictionAgreeFrames >= RestrictionHysteresisFrames)
                {
                    displayedRestrictionsPass = allRestrictionsPass;
                    restrictionAgreeFrames = 0;
                }
            }

            if (ghostRenderer != null && ghostRenderer.material != null
                && (displayedRestrictionsPass != lastGhostRestrictionsPass || !cardTilePlace))
            {
                // Join feedback stays on the footprint / lines — not the mesh tint —
                // so the translucent fresnel shader does not strobe green.
                Color tint = displayedRestrictionsPass ? availableToPlaceTintColor : errorTintColor;
                Color fresnel = displayedRestrictionsPass ? availableToPlaceFresnelColor : errorFresnelColor;
                ghostRenderer.material.SetColor(TINT, tint);
                ghostRenderer.material.SetColor(FRESNEL, fresnel);
                lastGhostRestrictionsPass = displayedRestrictionsPass;
            }

            if (cardTilePlace && joinCount > 0)
                UpdateJoinLines(snapTarget, snapTarget);
            else
                ClearJoinLines();
        }

        private Vector3? RaycastGhostPoint()
        {
            Ray cameraRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, floorLayers))
                return hit.point;
            if (Physics.Raycast(cameraRay, out hit, float.MaxValue))
                return hit.point;
            return null;
        }

        /// <summary>
        /// Ground pick that ignores buildings so hovering near existing tiles does not thrash the snap cell.
        /// </summary>
        private Vector3? RaycastGroundForTileGhost()
        {
            Ray cameraRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(cameraRay, out RaycastHit floorHit, float.MaxValue, floorLayers))
                return floorHit.point;

            int count = Physics.RaycastNonAlloc(cameraRay, ghostRayHits, float.MaxValue);
            float bestDist = float.MaxValue;
            Vector3? best = null;
            for (int i = 0; i < count; i++)
            {
                var h = ghostRayHits[i];
                if (h.collider == null) continue;
                if (ShouldIgnoreSelectionHit(h.collider)) continue;
                if (h.collider.GetComponentInParent<BaseBuilding>() != null) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h.point;
                }
            }

            if (best.HasValue) return best;

            // Stable fallback: horizontal plane at last ghost height / camera focus.
            float y = ghostInstance != null ? ghostInstance.transform.position.y : GetCameraFocusPosition().y;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            if (plane.Raycast(cameraRay, out float enter))
                return cameraRay.GetPoint(enter);

            return null;
        }

        private void UpdateTileFootprint(Vector3 center, bool show, int joinCount)
        {
            if (!show)
            {
                if (tileFootprint != null) tileFootprint.SetActive(false);
                return;
            }

            if (tileFootprint == null)
            {
                tileFootprint = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tileFootprint.name = "ColonyTileFootprint";
                Object.Destroy(tileFootprint.GetComponent<Collider>());
                var mr = tileFootprint.GetComponent<MeshRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default");
                tileFootprintMaterial = new Material(shader);
                mr.material = tileFootprintMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            tileFootprint.SetActive(true);
            float s = ColonyTileGrid.TileSize * 0.92f;
            tileFootprint.transform.position = center + Vector3.up * 0.12f;
            tileFootprint.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            tileFootprint.transform.localScale = new Vector3(s, s, 1f);

            if (joinCount != lastJoinCount && tileFootprintMaterial != null)
            {
                Color c = joinCount > 0
                    ? new Color(0.15f, 1f, 0.4f, 0.55f)
                    : new Color(0.25f, 0.75f, 1f, 0.4f);
                if (tileFootprintMaterial.HasProperty("_BaseColor")) tileFootprintMaterial.SetColor("_BaseColor", c);
                else if (tileFootprintMaterial.HasProperty("_Color")) tileFootprintMaterial.SetColor("_Color", c);
                else tileFootprintMaterial.color = c;
                lastJoinCount = joinCount;
            }
        }

        private void UpdateJoinLines(Vector3 snapPos, Vector3 ghostVisualPos)
        {
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(
                ColonyTileGrid.WorldToCell(snapPos), Owner.Player1, joinNeighbors);

            while (joinLines.Count < joinNeighbors.Count)
            {
                var go = new GameObject("TileJoinLine");
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.35f;
                lr.endWidth = 0.35f;
                lr.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                lr.startColor = new Color(0.3f, 1f, 0.5f, 0.9f);
                lr.endColor = new Color(0.3f, 1f, 0.5f, 0.9f);
                lr.useWorldSpace = true;
                joinLines.Add(lr);
            }

            for (int i = 0; i < joinLines.Count; i++)
            {
                if (i < joinNeighbors.Count && joinNeighbors[i] != null)
                {
                    joinLines[i].gameObject.SetActive(true);
                    Vector3 a = ghostVisualPos + Vector3.up * 1.5f;
                    Vector3 b = joinNeighbors[i].transform.position + Vector3.up * 1.5f;
                    joinLines[i].SetPosition(0, a);
                    joinLines[i].SetPosition(1, b);
                }
                else
                {
                    joinLines[i].gameObject.SetActive(false);
                }
            }
        }

        private void ClearJoinLines()
        {
            for (int i = 0; i < joinLines.Count; i++)
            {
                if (joinLines[i] != null)
                    joinLines[i].gameObject.SetActive(false);
            }
        }

        private void ClearGhostVisuals()
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }
            ghostRenderer = null;
            if (tileFootprint != null) tileFootprint.SetActive(false);
            ClearJoinLines();
            tileGhostStickyCell = null;
            lastJoinCount = -1;
            restrictionAgreeFrames = 0;
            displayedRestrictionsPass = true;
            lastGhostRestrictionsPass = true;
        }

        private void HandleDragSelect()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleMouseDown();
            }
            else if (Mouse.current.leftButton.isPressed && !Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleMouseDrag();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                HandleMouseUp();
            }
        }

        private void HandleMouseUp()
        {
            if (BuildingSiteSelectionController.IsSelecting)
            {
                HandleLeftClick();
                if (selectionBox != null)
                {
                    selectionBox.gameObject.SetActive(false);
                }
                return;
            }

            // Don't steal the selection when the press started on UI (e.g. playing a building card).
            if (wasMouseDownOnUI)
            {
                if (selectionBox != null)
                {
                    selectionBox.gameObject.SetActive(false);
                }
                return;
            }

            if (activeCommand == null && !Keyboard.current.shiftKey.isPressed)
            {
                DeselectAllUnits();
            }

            HandleLeftClick();
            foreach (AbstractUnit unit in addedUnits)
            {
                unit.Select();
            }
            if (selectionBox != null)
            {
                selectionBox.gameObject.SetActive(false);
            }

            // If the click landed on empty space (nothing got selected and it wasn't a UI
            // interaction or an active command placement), fall back to selecting the Global Commander.
            if (activeCommand == null && selectedUnits.Count == 0)
            {
                GlobalCommander commander = GetGlobalCommander();
                if (commander != null)
                {
                    commander.Select();
                }
            }
        }

        private void HandleMouseDrag()
        {
            if (activeCommand != null || wasMouseDownOnUI) return;

            Bounds selectionBoxBounds = ResizeSelectionBox();
            foreach (AbstractUnit unit in aliveUnits.Where(aliveUnits => aliveUnits.gameObject.activeInHierarchy))
            {
                Vector2 unitPosition = playerCamera.WorldToScreenPoint(unit.transform.position);

                if (selectionBoxBounds.Contains(unitPosition))
                {
                    addedUnits.Add(unit);
                }
            }
        }

        private void HandleMouseDown()
        {
            if (selectionBox != null)
            {
                selectionBox.sizeDelta = Vector2.zero;
                selectionBox.gameObject.SetActive(true);
            }
            startingMousePosition = Mouse.current.position.ReadValue();
            addedUnits.Clear();
            wasMouseDownOnUI = EventSystem.current.IsPointerOverGameObject();
        }

        private void DeselectAllUnits()
        {
            ISelectable[] currentlySelectedUnits = selectedUnits.ToArray();
            foreach(ISelectable selectable in currentlySelectedUnits)
            {
                selectable.Deselect();
            }
        }

        private Bounds ResizeSelectionBox()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            float width = mousePosition.x - startingMousePosition.x;
            float height = mousePosition.y - startingMousePosition.y;

            Vector2 anchoredPos = startingMousePosition + new Vector2(width / 2, height / 2);
            Vector2 sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));

            if (selectionBox != null)
            {
                selectionBox.anchoredPosition = anchoredPos;
                selectionBox.sizeDelta = sizeDelta;
            }

            return new Bounds(anchoredPos, sizeDelta);
        }

        private void HandleRightClick()
        {
            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                // Draft / pause overlays use full-screen raycast blockers. Without this check,
                // right-clicks pass through UI, issue Move while Time.timeScale==0, and units
                // appear "stuck" (green status, zero deltaTime movement).
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                if (Time.timeScale <= 0.01f)
                {
                    Debug.LogWarning("[PlayerInput] Right-click ignored — game is paused (Time.timeScale=0). Dismiss any draft/summary overlay first.");
                    return;
                }

                Ray vetoRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(vetoRay, out RaycastHit vetoHit, float.MaxValue, ~0, QueryTriggerInteraction.Collide))
                {
                    // Right-clicking an active expansion (its pipeline segments or the ghost
                    // command post) cycles through Pause -> Resume -> Cancel.
                    EnergyPipelineManager pipelineMgr = null;
                    if (vetoHit.collider.TryGetComponent<PipelineSegment>(out var seg))
                    {
                        pipelineMgr = seg.Manager;
                    }
                    if (pipelineMgr == null)
                    {
                        pipelineMgr = vetoHit.collider.GetComponentInParent<EnergyPipelineManager>();
                    }
                    if (pipelineMgr != null)
                    {
                        pipelineMgr.CycleRightClick();
                        return;
                    }

                    BaseBuilding clickedBuilding = vetoHit.collider.GetComponentInParent<BaseBuilding>();
                    if (clickedBuilding != null
                        && clickedBuilding.Owner == Owner.Player1
                        && clickedBuilding is not GlobalCommander)
                    {
                        // Select the building so Delete still works, then open context menu.
                        if (!selectedUnits.Contains(clickedBuilding))
                        {
                            DeselectAllUnits();
                            clickedBuilding.Select();
                        }
                        BuildingContextMenuUI.Instance?.Show(
                            clickedBuilding, Mouse.current.position.ReadValue());
                        return;
                    }

                    if (vetoHit.collider.TryGetComponent<GameDevTV.RTS.Environment.ExplorableNode>(out var explorableNode))
                    {
                        explorableNode.TryExplore();
                        return;
                    }
                }
            }

            if (selectedUnits.Count == 0) { return; }
            if (!Mouse.current.rightButton.wasReleasedThisFrame) { return; }

            Ray cameraRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            // Prefer configured masks, but fall back to any collider (PlanetManager is often not on floorLayers).
            if (!Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, interactableLayers | floorLayers)
                && !Physics.Raycast(cameraRay, out hit, float.MaxValue))
            {
                Debug.LogWarning("[PlayerInput] Right-click move raycast missed everything.");
                return;
            }

            Debug.Log($"[PlayerInput] Right-click hit {hit.collider?.name} at {hit.point} with {selectedUnits.Count} selected.");

            List<AbstractUnit> abstractUnits = new(selectedUnits.Count);
            foreach (ISelectable selectable in selectedUnits)
            {
                if (selectable is AbstractUnit unit)
                {
                    abstractUnits.Add(unit);
                }
            }

            if (abstractUnits.Count == 0)
            {
                Debug.LogWarning("[PlayerInput] Right-click had selection but no AbstractUnit targets.");
                return;
            }

            for (int i = 0; i < abstractUnits.Count; i++)
            {
                CommandContext context = new(abstractUnits[i], hit, i, MouseButton.Right);
                bool handled = false;

                foreach (ICommand command in GetAvailableCommands(abstractUnits[i]))
                {
                    if (command.CanHandle(context))
                    {
                        Debug.Log($"[PlayerInput] Issuing {command.GetType().Name} for {abstractUnits[i].name} -> {hit.point}");
                        command.Handle(context);
                        handled = true;
                        if (command.IsSingleUnitCommand)
                        {
                            return;
                        }
                        break;
                    }
                }

                if (!handled)
                {
                    Debug.LogWarning($"[PlayerInput] No right-click command handled for {abstractUnits[i].name}.");
                }
            }
        }

        private List<BaseCommand> GetAvailableCommands(AbstractUnit unit)
        {
            OverrideCommandsCommand[] overrideCommandsCommands = unit.AvailableCommands
                .Where(command => command is OverrideCommandsCommand)
                .Cast<OverrideCommandsCommand>()
                .ToArray();

            List<BaseCommand> allAvailableCommands = new();
            foreach(OverrideCommandsCommand overrideCommand in overrideCommandsCommands)
            {
                allAvailableCommands.AddRange(overrideCommand.Commands
                    .Where(command => command is not OverrideCommandsCommand)
                );
            }

            allAvailableCommands.AddRange(unit.AvailableCommands
                .Where(command => command is not OverrideCommandsCommand)
            );

            return allAvailableCommands;
        }

        private void HandleLeftClick()
        {
            if (playerCamera == null) { return ; }

            Ray cameraRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (BuildingSiteSelectionController.IsSelecting)
            {
                if (Time.frameCount <= BuildingSiteSelectionController.AcceptClicksAfterFrame)
                {
                    return;
                }

                RaycastHit[] selectionHits = Physics.RaycastAll(
                    cameraRay, float.MaxValue, ~0, QueryTriggerInteraction.Collide);
                foreach (RaycastHit hit in selectionHits.OrderBy(h => h.distance))
                {
                    if (BuildingSiteSelectionController.TryHandleClick(hit))
                    {
                        return;
                    }
                }

                BuildingSiteSelectionController.NotifyMissedClick();
                return;
            }

            if (EventSystem.current.IsPointerOverGameObject()) { return; }

            if (activeCommand == null)
            {
                RaycastHit[] hits = Physics.RaycastAll(cameraRay, float.MaxValue, ~0, QueryTriggerInteraction.Ignore);
                ISelectable best = null;
                float bestScore = float.MaxValue;

                foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
                {
                    if (ShouldIgnoreSelectionHit(hit.collider)) continue;

                    ISelectable selectable = hit.collider.GetComponentInParent<ISelectable>();
                    if (selectable == null) continue;

                    // Prefer real field units over the invisible Universal Command Center /
                    // large building volumes when the ray grazes multiple selectables.
                    float score = hit.distance;
                    if (selectable is GlobalCommander) score += 1000f;
                    else if (selectable is BaseBuilding) score += 50f;
                    else if (selectable is AbstractUnit) score -= 10f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = selectable;
                    }
                }

                if (best != null)
                {
                    if (!Keyboard.current.shiftKey.isPressed)
                    {
                        DeselectAllUnits();
                    }

                    best.Select();
                }
            }
            else if (activeCommand != null
                && !EventSystem.current.IsPointerOverGameObject())
            {
                if (Physics.Raycast(cameraRay, out RaycastHit hitAction, float.MaxValue, interactableLayers | floorLayers))
                {
                    ActivateAction(hitAction);
                }
                // Fallback: If the user forgot to set their floorLayers mask, try to place it on literally anything
                else if (Physics.Raycast(cameraRay, out RaycastHit hitAny, float.MaxValue))
                {
                    ActivateAction(hitAny);
                }
            }
        }

        private void ActivateAction(RaycastHit hit)
        {
            if (Time.timeScale <= 0.01f)
            {
                Debug.LogWarning("[PlayerInput] Command ignored — game is paused (Time.timeScale=0). Dismiss any draft/summary overlay first.");
                return;
            }

            // Commit the cell the ghost/footprint was showing — not a fresh noisy ray hit.
            bool cardTilePlace = activeCommand is BuildBuildingCommand placeBbc && placeBbc.HandIndex >= 0;
            if (cardTilePlace)
            {
                Vector3 placePoint;
                if (tileGhostStickyCell.HasValue)
                {
                    placePoint = ColonyTileGrid.CellToWorld(tileGhostStickyCell.Value, hit.point.y);
                }
                else
                {
                    placePoint = RaycastGroundForTileGhost() ?? hit.point;
                    placePoint = ColonyTileGrid.SnapForPlacement(placePoint, Owner.Player1, out _);
                }

                UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter
                {
                    agentTypeID = 0,
                    areaMask = UnityEngine.AI.NavMesh.AllAreas
                };
                if (UnityEngine.AI.NavMesh.SamplePosition(placePoint, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
                    placePoint = new Vector3(placePoint.x, navHit.position.y, placePoint.z);

                if (activeCommand is BuildBuildingCommand placeMine
                    && BuildingSiteRegistry.IsMineBuilding(placeMine.Building)
                    && DiscoverySystem.TrySnapToMineDeposit(
                        placeMine.Building, placePoint, out Vector3 minePlace, out Vector2Int mineCell))
                {
                    placePoint = new Vector3(minePlace.x, placePoint.y, minePlace.z);
                    tileGhostStickyCell = mineCell;
                }

                hit.point = placePoint;
            }

            ClearGhostVisuals();

            // Snap camera to the build site for building commands
            if (activeCommand is BuildBuildingCommand && hit.point != Vector3.zero)
            {
                if (cameraTarget != null)
                {
                    Vector3 targetCameraPos = hit.point;
                    targetCameraPos.y = cameraTarget.position.y;
                    cameraTarget.position = targetCameraPos;
                    hasCameraBeenFocused = true;
                }
            }

            List<AbstractCommandable> abstractCommandables = GetCommandTargets()
                .Where(unit => unit is AbstractCommandable)
                .Cast<AbstractCommandable>()
                .ToList();

            if (abstractCommandables.Count == 0 && activeCommand is MoveCommand)
            {
                Debug.LogWarning("[PlayerInput] Select a unit before issuing a move order.");
                activeCommand = null;
                commandTargetUnits.Clear();
                return;
            }

            // Fallback for Global Commands: If no units are selected, the command is coming from the GlobalCommander
            if (abstractCommandables.Count == 0)
            {
                GlobalCommander commander = GetGlobalCommander();
                if (commander != null)
                {
                    abstractCommandables.Add(commander);
                }
                else
                {
                    throw new System.InvalidOperationException("[PlayerInput] GlobalCommander is missing! The invulnerable starting base (Universal Command Center) has been destroyed or was not initialized.");
                }
            }

            bool handled = false;
            for (int i = 0; i < abstractCommandables.Count; i++)
            {
                CommandContext context = new(abstractCommandables[i], hit, i);
                if (activeCommand.CanHandle(context))
                {
                    activeCommand.Handle(context);
                    handled = true;
                    if (activeCommand.IsSingleUnitCommand)
                    {
                        break;
                    }
                }
            }

            if (handled && cardTilePlace)
                AudioManager.Instance.PlayPlaceClickSound();
            else if (!handled && cardTilePlace
                && activeCommand is BuildBuildingCommand failedMine
                && BuildingSiteRegistry.IsMineBuilding(failedMine.Building))
            {
                if (!DiscoverySystem.HasDiscoveredMineDeposit(failedMine.Building))
                {
                    DiscoverySystem.TryGetMineResourceType(failedMine.Building, out string type);
                    ExplorationManager.NotifyExplorationFailed(
                        $"Discover a {type ?? "resource"} deposit before placing this mine.");
                }
                else if (!DiscoverySystem.IsOnDiscoveredMineDeposit(failedMine.Building, hit.point))
                {
                    ExplorationManager.NotifyExplorationFailed(
                        "Place this mine on a discovered deposit of the matching resource.");
                }
            }


            if (activeCommand != null && !activeCommand.StaysActive)
            {
                activeCommand = null;
            }

            commandTargetUnits.Clear();
        }

        private IEnumerable<ISelectable> GetCommandTargets()
        {
            if (selectedUnits.Count > 0)
            {
                return selectedUnits;
            }

            return commandTargetUnits.Count > 0 ? commandTargetUnits : selectedUnits;
        }

        private void HandleRotation()
        {
            // Full 360-degree free rotation using Middle Mouse Button
            if (cameraTarget != null && Mouse.current.middleButton.isPressed)
            {
                float rotationInput = Mouse.current.delta.x.ReadValue();
                float rotationSpeed = cameraConfig.RotationSpeed * 0.2f; 
                cameraTarget.transform.Rotate(Vector3.up, rotationInput * rotationSpeed, Space.World);
            }

            if (cinemachineFollow == null) return;

            if (ShouldSetRotationStartTime())
            {
                rotationStartTime = Time.time;
            }

            float rotationTime = Mathf.Clamp01((Time.time - rotationStartTime) * cameraConfig.RotationSpeed);

            Vector3 targetFollowOffset;

            if (Keyboard.current.pageDownKey.isPressed)
            {
                targetFollowOffset = new Vector3(
                    maxRotationAmount,
                    cinemachineFollow.FollowOffset.y,
                    0
                );
            }
            else if (Keyboard.current.pageUpKey.isPressed)
            {
                targetFollowOffset = new Vector3(
                    -maxRotationAmount,
                    cinemachineFollow.FollowOffset.y,
                    0
                );
            }
            else
            {
                targetFollowOffset = new Vector3(
                    startingFollowOffset.x,
                    cinemachineFollow.FollowOffset.y,
                    startingFollowOffset.z
                );
            }

            cinemachineFollow.FollowOffset = Vector3.Slerp(
                cinemachineFollow.FollowOffset,
                targetFollowOffset,
                rotationTime
            );
        }

        private bool ShouldSetRotationStartTime()
        {
            return Keyboard.current.pageUpKey.wasPressedThisFrame
                || Keyboard.current.pageDownKey.wasPressedThisFrame
                || Keyboard.current.pageUpKey.wasReleasedThisFrame
                || Keyboard.current.pageDownKey.wasReleasedThisFrame;
        }

        private void HandleZooming()
        {
            if (cinemachineFollow == null) return;

            // Hand strip owns the wheel while hovered — don't zoom the camera.
            bool handOwnsWheel = BottomBarActionsUI.IsPointerOverHandStrip;

            // Mouse scroll zoom
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (!handOwnsWheel && Mathf.Abs(scroll) > 0.01f)
            {
                // Normalize scroll to get consistent zoom speeds across all operating systems and mice
                float scrollSign = Mathf.Sign(scroll);
                
                // Invert scroll so scrolling up zooms in. A fixed step of 2.0 units per notch times ZoomSpeed
                targetZoomDistance -= scrollSign * cameraConfig.ZoomSpeed * 2.0f;
                // Clamp distance to keep from zooming through the floor or too far out
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, cameraConfig.MinZoomDistance, startingFollowOffset.y * 4f);
            }
            
            // Keyboard zoom (Page Up/Page Down) for CLI testing
            if (Keyboard.current.pageUpKey.isPressed)
            {
                targetZoomDistance -= cameraConfig.ZoomSpeed * Time.deltaTime * 10f;
            }
            else if (Keyboard.current.pageDownKey.isPressed)
            {
                targetZoomDistance += cameraConfig.ZoomSpeed * Time.deltaTime * 10f;
            }
            
            // Clamp distance to keep from zooming through the floor or too far out
            targetZoomDistance = Mathf.Clamp(targetZoomDistance, cameraConfig.MinZoomDistance, startingFollowOffset.y * 4f);

            Vector3 targetFollowOffset = new Vector3(
                cinemachineFollow.FollowOffset.x,
                targetZoomDistance,
                cinemachineFollow.FollowOffset.z
            );

            cinemachineFollow.FollowOffset = Vector3.Lerp(
                cinemachineFollow.FollowOffset,
                targetFollowOffset,
                Time.unscaledDeltaTime * 10f
            );
        }

        private bool ShouldSetZoomStartTime()
        {
            // Now handled entirely by continuous scrolling
            return false;
        }

        private void HandlePanning()
        {
            if (cameraTarget == null || HexGridManager.Instance == null)
            {
                if (highlightTrace) Debug.LogWarning($"[HexHighlight] Keyboard update skipped: cameraTarget={cameraTarget}, grid={HexGridManager.Instance}");
                return;
            }

            InitializeCurrentHex();
            if (currentHex == null) return;

            Vector2Int direction = Vector2Int.zero;
            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame) direction = Vector2Int.up;
            else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame) direction = Vector2Int.down;
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) direction = Vector2Int.left;
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) direction = Vector2Int.right;

            if (direction == Vector2Int.zero) return;

            var destination = HexGridManager.Instance.GetNeighborInDirection(currentHex, direction);
            if (destination == null)
            {
                if (highlightTrace) Debug.Log($"[HexHighlight] No keyboard neighbor from {currentHex.HexCoordinates} in {direction}");
                return;
            }

            currentHex.SetHighlighted(false);
            currentHex = destination;
            currentHex.SetHighlighted(true);

            Vector3 destinationPosition = currentHex.WorldPosition;
            destinationPosition.y = cameraTarget.position.y;
            cameraTarget.position = destinationPosition;
            AudioManager.Instance.PlayHexHoverSound();
            if (highlightTrace) Debug.Log($"[HexHighlight] Keyboard moved {direction}: {currentHex.HexCoordinates} at {destinationPosition}");
        }

        private void HandleHexHover()
        {
            if (playerCamera == null || HexGridManager.Instance == null || Mouse.current == null) return;

            HexGridManager.HexTile nextHoveredHex = null;
            Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, floorLayers, QueryTriggerInteraction.Ignore))
            {
                nextHoveredHex = HexGridManager.Instance.GetNearestRevealedHex(hit.point);
                if (highlightTrace) Debug.Log($"[HexHighlight] Hover floor hit: point={hit.point}, collider={hit.collider.name}, mask={floorLayers.value}");
            }
            else if (Physics.Raycast(ray, out hit, float.MaxValue, ~0, QueryTriggerInteraction.Ignore))
            {
                nextHoveredHex = HexGridManager.Instance.GetNearestRevealedHex(hit.point);
                if (highlightTrace) Debug.Log($"[HexHighlight] Hover fallback hit: point={hit.point}, collider={hit.collider.name}");
            }
            else if (highlightTrace)
            {
                Debug.Log("[HexHighlight] Hover ray missed floor and fallback geometry.");
            }

            if (nextHoveredHex == hoveredHex) return;

            if (highlightTrace) Debug.Log($"[HexHighlight] Hover changed: {hoveredHex?.HexCoordinates.ToString() ?? "NULL"} -> {nextHoveredHex?.HexCoordinates.ToString() ?? "NULL"}");
            hoveredHex?.SetHovered(false);
            hoveredHex = nextHoveredHex;
            hoveredHex?.SetHovered(true);
        }

        private void InitializeCurrentHex()
        {
            if (currentHex != null || cameraTarget == null || HexGridManager.Instance == null) return;

            currentHex = HexGridManager.Instance.GetNearestHex(cameraTarget.position);
            if (highlightTrace) Debug.Log($"[HexHighlight] Current tile initialized at camera {cameraTarget.position}: {currentHex?.HexCoordinates.ToString() ?? "NULL"}");
            currentHex?.SetHighlighted(true);
        }


        private Vector2 GetRawWasd()
        {
            Vector2 move = Vector2.zero;

            // COMMENTED OUT FOR FIXING CAMERA PAN ISSUES:
            // The Application.isFocused check was blocking keyboard input when the Unity Editor window wasn't focused
            // if (!Application.isFocused) return move;

            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) move.y += 1f;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) move.y -= 1f;
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) move.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) move.x += 1f;

            return move;
        }

        private Vector2 GetMouseMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;

            if (!Application.isFocused)
            {
                return moveAmount;
            }
            
            // Add debug logging for mouse edge detection
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            if (mousePosition.x < 0f || mousePosition.x > screenWidth
                || mousePosition.y < 0f || mousePosition.y > screenHeight)
            {
                return moveAmount;
            }
            
            if (mousePosition.x <= cameraConfig.EdgePanSize)
            {
                Debug.Log("[PlayerInput] Mouse near left edge");
                moveAmount.x -= cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.x >= screenWidth - cameraConfig.EdgePanSize)
            {
                Debug.Log("[PlayerInput] Mouse near right edge");
                moveAmount.x += cameraConfig.MousePanSpeed;
            }
            
            if (mousePosition.y >= screenHeight - cameraConfig.EdgePanSize)
            {
                Debug.Log("[PlayerInput] Mouse near top edge");
                moveAmount.y += cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.y <= cameraConfig.EdgePanSize)
            {
                Debug.Log("[PlayerInput] Mouse near bottom edge");
                moveAmount.y -= cameraConfig.MousePanSpeed;
            }
            
            return moveAmount;
        }

        private Vector2 GetKeyboardMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;
            
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
            {
                moveAmount.y += cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
            {
                moveAmount.x -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
            {
                moveAmount.y -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
            {
                moveAmount.x += cameraConfig.KeyboardPanSpeed;
            }
            
            return moveAmount;
        }
    }
}
