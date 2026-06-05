using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;
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
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] += HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;
            
            GameDevTV.RTS.Environment.PlanetGenerator.OnPlanetGenerated += CenterCameraOnMap;
        }

        private void Start()
        {
            CenterCameraOnMap();
        }

        private void CenterCameraOnMap()
        {
            if (cameraTarget == null) return;
            
            // Wait for PlanetGenerator to be ready
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

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (!selectedUnits.Contains(evt.Unit))
            {
                selectedUnits.Add(evt.Unit);
            }
        }
        private void HandleUnitDeselected(UnitDeselectedEvent evt) => selectedUnits.Remove(evt.Unit);
        private void HandleUnitSpawn(UnitSpawnEvent evt) => aliveUnits.Add(evt.Unit);
        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            aliveUnits.Remove(evt.Unit);
            selectedUnits.Remove(evt.Unit);
        }

        private void HandleActionSelected(CommandSelectedEvent evt)
        {
            activeCommand = evt.Command;
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
        {
            var commandPosts = BaseBuilding.ActiveBuildings
                .Where(b => b != null && b.Owner == Owner.Player1 &&
                       (b.name.Contains("Command") || (b.BuildingSO != null && b.BuildingSO.Name.Contains("Command"))) &&
                       b.Progress.State == BuildingProgress.BuildingState.Completed)
                .Cast<AbstractCommandable>()
                .ToList();

            var globalCommander = FindAnyObjectByType<GlobalCommander>();
            if (globalCommander != null)
            {
                commandPosts.Add(globalCommander);
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
            if (selectionBox == null) { return; }

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
            selectionBox.gameObject.SetActive(false);
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
            selectionBox.sizeDelta = Vector2.zero;
            selectionBox.gameObject.SetActive(true);
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

            selectionBox.anchoredPosition = startingMousePosition + new Vector2(width / 2, height / 2);
            selectionBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));

            return new Bounds(selectionBox.anchoredPosition, selectionBox.sizeDelta);
        }

        private void HandleRightClick()
        {
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

            if (activeCommand == null
                && Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, selectableUnitsLayers)
                && hit.collider.TryGetComponent(out ISelectable selectable))
            {
                selectable.Select();
            }
            else if (activeCommand != null
                && !EventSystem.current.IsPointerOverGameObject())
            {
                if (Physics.Raycast(cameraRay, out hit, float.MaxValue, interactableLayers | floorLayers))
                {
                    ActivateAction(hit);
                }
                // Fallback: If the user forgot to set their floorLayers mask, try to place it on literally anything
                else if (Physics.Raycast(cameraRay, out hit, float.MaxValue))
                {
                    ActivateAction(hit);
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

            List<AbstractCommandable> abstractCommandables = selectedUnits
                                .Where((unit) => unit is AbstractCommandable)
                                .Cast<AbstractCommandable>()
                                .ToList();

            // Fallback for Global Commands: If no units are selected, the command is coming from the GlobalCommander
            if (abstractCommandables.Count == 0)
            {
                GlobalCommander globalCommander = FindAnyObjectByType<GlobalCommander>();
                if (globalCommander != null)
                {
                    abstractCommandables.Add(globalCommander);
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
            Vector2 moveAmount = GetKeyboardMoveAmount();
            moveAmount += GetMouseMoveAmount();

            Vector3 velocity = new Vector3(moveAmount.x, 0, moveAmount.y);
            
            if (cameraTarget != null)
            {
                cameraTarget.transform.Translate(velocity * Time.deltaTime, Space.World);
            }
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
