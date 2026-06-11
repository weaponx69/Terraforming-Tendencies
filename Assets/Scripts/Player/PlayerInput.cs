using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Player
{
    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private Rigidbody cameraTarget;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private CameraConfig cameraConfig;
        [SerializeField] private LayerMask selectableUnitsLayers;
        [SerializeField] private LayerMask interactableLayers;
        [SerializeField] private LayerMask floorLayers;
        [Header("Hero Drone (Mobile Command Center)")]
        [Tooltip("When enabled, WASD pilots the assigned Hero Drone instead of panning the camera. The mouse (edge-pan, click, drag-select) stays fully free.")]
        [SerializeField] private bool useHeroControlMode = true;
        [Tooltip("The Hero Drone piloted with WASD. Drag the Hero Drone from the hierarchy here.")]
        [SerializeField] private GameDevTV.RTS.Units.HeroDroneController heroDrone;
        [SerializeField] private RectTransform selectionBox;
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color errorTintColor = Color.red;
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color errorFresnelColor = new (4, 1.7f, 0, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color availableToPlaceTintColor = new (0.2f, 0.65f, 1, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color availableToPlaceFresnelColor = new(4, 1.7f, 0, 2);

        private Vector2 startingMousePosition;

        private BaseCommand activeCommand;
        private GameObject ghostInstance;
        private Renderer ghostRenderer;
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

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int FRESNEL = Shader.PropertyToID("_FresnelColor");

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponent<Camera>();
            }

            if (cameraTarget != null)
            {
                cameraTarget.isKinematic = true;
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
        }

        private void Start()
        {
            CenterCameraOnMap();
            globalCommander = FindAnyObjectByType<GlobalCommander>();
        }

        private GlobalCommander GetGlobalCommander()
        {
            if (globalCommander == null)
            {
                globalCommander = FindAnyObjectByType<GlobalCommander>();
            }
            return globalCommander;
        }

        private bool hasCameraBeenFocused = false;
        private bool hasCameraSnappedToCommandPost = false;

        private void CenterCameraOnMap()
        {
            if (cameraTarget == null) return;
            if (hasCameraBeenFocused) return;
            
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

        private void OnDestroy()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] -= HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] -= HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] -= HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
            
            GameDevTV.RTS.Environment.PlanetGenerator.OnPlanetGenerated -= CenterCameraOnMap;
        }

        private bool isFollowingSelectedCrawler = false;

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (!selectedUnits.Contains(evt.Unit))
            {
                selectedUnits.Add(evt.Unit);
            }
            
            if (evt.Unit is FoundryCrawler)
            {
                isFollowingSelectedCrawler = true;
            }
        }
        private void HandleUnitDeselected(UnitDeselectedEvent evt)
        {
            selectedUnits.Remove(evt.Unit);
            if (evt.Unit is FoundryCrawler)
            {
                isFollowingSelectedCrawler = false;
            }
        }
        private void HandleUnitSpawn(UnitSpawnEvent evt) => aliveUnits.Add(evt.Unit);
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
            if (evt.Unit is FoundryCrawler)
            {
                isFollowingSelectedCrawler = false;
            }
        }

        private void HandleActionSelected(CommandSelectedEvent evt)
        {
            activeCommand = evt.Command;

            // Auto-place logic for Command Posts: automatically build in the nearest unoccupied sector
            if (activeCommand is BuildBuildingCommand commandPostBbc && commandPostBbc.Building != null && commandPostBbc.Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
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
                            .Where(s => !s.IsOccupied)
                            .OrderBy(s => Vector3.Distance(refPos, s.Center))
                            .FirstOrDefault();

                        if (nearestUnoccupied != null)
                        {
                            // Move camera to the auto-placement site so the player can see the construction begin
                            if (cameraTarget != null)
                            {
                                Vector3 targetCameraPos = nearestUnoccupied.Center;
                                targetCameraPos.y = cameraTarget.position.y; // Maintain current camera height/zoom
                                cameraTarget.position = targetCameraPos;
                                hasCameraBeenFocused = true;
                            }

                            RaycastHit simulatedHit = new RaycastHit();
                            simulatedHit.point = nearestUnoccupied.Center;
                            
                            ActivateAction(simulatedHit);
                            return;
                        }
                    }
                }
            }

            if (!activeCommand.RequiresClickToActivate)
            {
                ActivateAction(new RaycastHit());
            }
            else 
            {
GameObject prefabToInstantiate = activeCommand.GhostPrefab;
                
                // If this is a building command, completely ignore the assigned GhostPrefab and just use 
                // the actual building prefab. This guarantees the preview shape matches the final shape!
                if (activeCommand is BuildBuildingCommand bbc && bbc.Building != null && bbc.Building.Prefab != null)
                {
                    prefabToInstantiate = bbc.Building.Prefab;
                }

                if (prefabToInstantiate != null)
                {
                    ghostInstance = Instantiate(prefabToInstantiate);

                    if (ghostInstance.TryGetComponent(out BaseBuilding bb))
                    {
                        bb.InitializeAsGhost(null, Owner.Player1);
                    }

                    ghostRenderer = ghostInstance.GetComponentInChildren<Renderer>();
                    
                    // We only want the visuals for the ghost, so strip any colliders/navmesh obstacles
                    // to prevent it from interfering with the game while dragging!
                    foreach (var col in ghostInstance.GetComponentsInChildren<Collider>()) Destroy(col);
                    foreach (var nav in ghostInstance.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) Destroy(nav);
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
                return;
            }

            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            if (!hasMouseMoved && (currentMousePos - lastMousePosition).sqrMagnitude > 100f)
            {
                hasMouseMoved = true;
            }
            lastMousePosition = currentMousePos;

            HandlePanning();
            HandleZooming();
            HandleRotation();
            HandleGhost();
            HandleRightClick();
            HandleDragSelect();
            HandleBasePaging();
            HandleCameraFollow();
        }

        private void HandleCameraFollow()
        {
            if (!isFollowingSelectedCrawler || cameraTarget == null) return;
            
            if (selectedUnits.Count == 1 && selectedUnits[0] is FoundryCrawler crawler)
            {
                // If the player tries to manually move the camera, break the lock so they aren't trapped!
                Vector2 keyboardMove = GetKeyboardMoveAmount();
                Vector2 mouseMove = GetMouseMoveAmount();
                if (keyboardMove.sqrMagnitude > 0.001f || mouseMove.sqrMagnitude > 0.001f)
                {
                    isFollowingSelectedCrawler = false;
                    return;
                }

                // Snap the camera target to the crawler's position, preserving current zoom height
                Vector3 targetPos = crawler.transform.position;
                targetPos.y = cameraTarget.position.y;
                cameraTarget.position = targetPos;
            }
        }

        private void HandleBasePaging()
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                PageBases(-1);
            }
            else if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                PageBases(1);
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

            var crawlers = Object.FindObjectsByType<FoundryCrawler>(FindObjectsInactive.Exclude);
            foreach (var crawler in crawlers)
            {
                if (crawler != null && crawler.Owner == Owner.Player1)
                {
                    commandPosts.Add(crawler);
                }
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

        private void HandleGhost()
        {
            if (ghostInstance == null || ghostRenderer == null) return;

            if (Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
                activeCommand = null;
                return;
            }

            Ray cameraRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Vector3? hitPos = null;

            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, floorLayers))
            {
                hitPos = hit.point;
            }
            // Fallback: if floorLayers is missing or misconfigured, try hitting ANYTHING
            else if (Physics.Raycast(cameraRay, out hit, float.MaxValue))
            {
                hitPos = hit.point;
            }

            if (hitPos.HasValue)
            {
                // Snap to sector if it's a command center
                if (activeCommand is BuildBuildingCommand bbc && bbc.Building != null && bbc.Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
                {
                    hitPos = bbc.SnapToNearestSector(hitPos.Value);
                }

                // Snap to NavMesh to ensure the ghost isn't floating on top of large rock colliders
                UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = UnityEngine.AI.NavMesh.AllAreas };
                if (UnityEngine.AI.NavMesh.SamplePosition(hitPos.Value, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
                {
                    hitPos = navHit.position;
                }

                ghostInstance.transform.position = hitPos.Value;

                bool allRestrictionsPass = activeCommand.AllRestrictionsPass(hitPos.Value);

                if (ghostRenderer != null && ghostRenderer.material != null)
                {
                    ghostRenderer.material.SetColor(TINT, allRestrictionsPass ? availableToPlaceTintColor : errorTintColor);
                    ghostRenderer.material.SetColor(FRESNEL,
                        allRestrictionsPass ? availableToPlaceFresnelColor : errorFresnelColor
                    );
                }
            }
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
            if (!wasMouseDownOnUI && activeCommand == null && !Keyboard.current.shiftKey.isPressed)
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
            if (!wasMouseDownOnUI && activeCommand == null && selectedUnits.Count == 0)
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

                    if (vetoHit.collider.TryGetComponent<BaseBuilding>(out var building))
                    {
                        if (building.Progress.State != BuildingProgress.BuildingState.Completed)
                        {
                            building.Die();
                            return;
                        }
                    }
                    else if (vetoHit.collider.transform.parent != null && vetoHit.collider.transform.parent.TryGetComponent<BaseBuilding>(out var parentBuilding))
                    {
                        if (parentBuilding.Progress.State != BuildingProgress.BuildingState.Completed)
                        {
                            parentBuilding.Die();
                            return;
                        }
                    }
                }
            }

            if (selectedUnits.Count == 0) { return; }

            Ray cameraRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Mouse.current.rightButton.wasReleasedThisFrame
                && Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, interactableLayers | floorLayers))
            {
                List<AbstractUnit> abstractUnits = new (selectedUnits.Count);
                foreach(ISelectable selectable in selectedUnits)
                {
                    if (selectable is AbstractUnit unit)
                    {
                        abstractUnits.Add(unit);
                    }
                }

                for(int i = 0; i < abstractUnits.Count; i++)
                {
                    CommandContext context = new(abstractUnits[i], hit, i, MouseButton.Right);

                    foreach(ICommand command in GetAvailableCommands(abstractUnits[i]))
                    {
                        if (command.CanHandle(context))
                        {
                            command.Handle(context);
                            if (command.IsSingleUnitCommand)
                            {
                                return;
                            }
                            break;
                        }
                    }
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

            if (activeCommand == null)
            {
                if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, selectableUnitsLayers)
                    && hit.collider.TryGetComponent(out ISelectable selectable))
                {
                    selectable.Select();
                }
                else if (Physics.Raycast(cameraRay, out RaycastHit hitFallback, float.MaxValue, ~floorLayers))
                {
                    ISelectable fallbackSelectable = hitFallback.collider.GetComponentInParent<ISelectable>();
                    if (fallbackSelectable != null)
                    {
                        fallbackSelectable.Select();
                    }
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
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }

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

            List<AbstractCommandable> abstractCommandables = selectedUnits
.Where((unit) => unit is AbstractCommandable)
                                .Cast<AbstractCommandable>()
                                .ToList();

            // Fallback for Global Commands: If no units are selected, the command is coming from the GlobalCommander
            if (abstractCommandables.Count == 0)
            {
                GlobalCommander commander = GetGlobalCommander();
                if (commander != null)
                {
                    abstractCommandables.Add(commander);
                }
            }

            for (int i = 0; i < abstractCommandables.Count; i++)
            {
                CommandContext context = new(abstractCommandables[i], hit, i);
                if (activeCommand.CanHandle(context))
                {
                    activeCommand.Handle(context);
                    if (activeCommand.IsSingleUnitCommand)
                    {
                        break;
                    }
                }
            }

            activeCommand = null;
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

            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Normalize scroll to get consistent zoom speeds across all operating systems and mice
                float scrollSign = Mathf.Sign(scroll);
                
                // Invert scroll so scrolling up zooms in. A fixed step of 2.0 units per notch times ZoomSpeed
                targetZoomDistance -= scrollSign * cameraConfig.ZoomSpeed * 2.0f;
                // Clamp distance to keep from zooming through the floor or too far out
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, cameraConfig.MinZoomDistance, startingFollowOffset.y * 4f);
            }

            Vector3 targetFollowOffset = new Vector3(
                cinemachineFollow.FollowOffset.x,
                targetZoomDistance,
                cinemachineFollow.FollowOffset.z
            );

            cinemachineFollow.FollowOffset = Vector3.Lerp(
                cinemachineFollow.FollowOffset,
                targetFollowOffset,
                Time.deltaTime * 10f
            );
        }

        private bool ShouldSetZoomStartTime()
        {
            // Now handled entirely by continuous scrolling
            return false;
        }

        private void HandlePanning()
        {
            bool heroActive = useHeroControlMode && heroDrone != null;

            // In hero mode WASD pilots the drone, so it no longer contributes to camera panning.
            // Mouse edge-pan is always honored.
            Vector2 moveAmount = heroActive ? Vector2.zero : GetKeyboardMoveAmount();
            moveAmount += GetMouseMoveAmount();

            Vector3 velocity = new Vector3(moveAmount.x, 0, moveAmount.y);
            
            if (cameraTarget != null)
            {
                // Use Space.Self so panning respects the new camera rotation angle!
                cameraTarget.transform.Translate(velocity * Time.deltaTime, Space.Self);
            }

            if (heroActive)
            {
                HandleHeroControl();
            }
        }

        /// <summary>
        /// Assigns the Hero Drone at runtime (called by HeroDroneSpawner after it spawns the drone).
        /// </summary>
        public void AssignHeroDrone(GameDevTV.RTS.Units.HeroDroneController hero)
        {
            heroDrone = hero;
        }

        private void HandleHeroControl()
        {
            Vector2 wasd = GetRawWasd();
            Vector3 worldDir = Vector3.zero;

            if (wasd.sqrMagnitude > 0.0001f && cameraTarget != null)
            {
                // Convert WASD into a camera-relative world direction so 'W' always moves the
                // drone away from the camera, matching the feel of Space.Self camera panning.
                Vector3 forward = cameraTarget.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 right = cameraTarget.transform.right;
                right.y = 0f;
                right.Normalize();
                worldDir = (forward * wasd.y + right * wasd.x).normalized;
            }

            heroDrone.SetMoveInput(new Vector2(worldDir.x, worldDir.z));

            // The moment a movement key is pressed, snap the camera back onto the Hero Drone.
            if (worldDir.sqrMagnitude > 0.0001f && cameraTarget != null)
            {
                Vector3 targetPos = heroDrone.transform.position;
                targetPos.y = cameraTarget.position.y; // preserve current zoom height
                cameraTarget.position = targetPos;
            }
        }

        private Vector2 GetRawWasd()
        {
            Vector2 move = Vector2.zero;

            if (!Application.isFocused) return move;

            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) move.y += 1f;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) move.y -= 1f;
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) move.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) move.x += 1f;

            return move;
        }

        private Vector2 GetMouseMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;

            // Stop scrolling immediately if the application is not focused or the mouse hasn't moved yet
            if (!cameraConfig.EnableEdgePan || !Application.isFocused || !hasMouseMoved) { return moveAmount; }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            // Stop scrolling immediately if the mouse is outside the window bounds.
            // We use a small epsilon to catch the mouse as it hits or crosses the border.
            if (mousePosition.x < 1f || mousePosition.x >= screenWidth - 1f || 
                mousePosition.y < 1f || mousePosition.y >= screenHeight - 1f)
            {
                return moveAmount;
            }

            if (mousePosition.x <= cameraConfig.EdgePanSize)
            {
                moveAmount.x -= cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.x >= screenWidth - cameraConfig.EdgePanSize)
            {
                moveAmount.x += cameraConfig.MousePanSpeed;
            }

            if (mousePosition.y >= screenHeight - cameraConfig.EdgePanSize)
            {
                moveAmount.y += cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.y <= cameraConfig.EdgePanSize)
            {
                moveAmount.y -= cameraConfig.MousePanSpeed;
            }

            return moveAmount;
        }

        private Vector2 GetKeyboardMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;
            
            // Explicitly check focus for keyboard input to prevent "stuck" keys from scrolling
            // the camera when the user alt-tabs or moves the mouse out of the window.
            if (!Application.isFocused) return moveAmount;

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
