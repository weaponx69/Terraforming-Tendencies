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
        [SerializeField] private new Camera camera;
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
        private MeshRenderer ghostRenderer;
        private bool wasMouseDownOnUI;
        private CinemachineFollow cinemachineFollow;
        private float zoomStartTime;
        private float rotationStartTime;
        private Vector3 startingFollowOffset;
        private float maxRotationAmount;
        private HashSet<AbstractUnit> aliveUnits = new(100);
        private HashSet<AbstractUnit> addedUnits = new(24);
        private List<ISelectable> selectedUnits = new(12);

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int FRESNEL = Shader.PropertyToID("_FresnelColor");

        private void Awake()
        {
            if (!cinemachineCamera.TryGetComponent(out cinemachineFollow))
            {
                Debug.LogError("Cinemachine Camera did not have CinemachineFollow. Zoom functionality will not work!");
            }

            startingFollowOffset = cinemachineFollow.FollowOffset;
            maxRotationAmount = Mathf.Abs(cinemachineFollow.FollowOffset.z);

            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] += HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] += HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] += HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;
        }

        private void OnDestroy()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] -= HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] -= HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] -= HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
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
            else if (activeCommand.GhostPrefab != null)
            {
                ghostInstance = Instantiate(activeCommand.GhostPrefab);
                ghostRenderer = ghostInstance.GetComponentInChildren<MeshRenderer>();
            }
        }

        private void Update()
        {
            HandlePanning();
            HandleZooming();
            HandleRotation();
            HandleGhost();
            HandleRightClick();
            HandleDragSelect();
        }

        private void HandleGhost()
        {
            if (ghostInstance == null) return;

            if (Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
                activeCommand = null;
                return;
            }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, floorLayers))
            {
                ghostInstance.transform.position = hit.point;

                bool allRestrictionsPass = activeCommand.AllRestrictionsPass(hit.point);

                ghostRenderer.material.SetColor(TINT, allRestrictionsPass ? availableToPlaceTintColor : errorTintColor);
                ghostRenderer.material.SetColor(FRESNEL,
                    allRestrictionsPass ? availableToPlaceFresnelColor : errorFresnelColor
                );
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
                Vector2 unitPosition = camera.WorldToScreenPoint(unit.transform.position);

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

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

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
            if (camera == null) { return ; }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (activeCommand == null
                && Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, selectableUnitsLayers)
                && hit.collider.TryGetComponent(out ISelectable selectable))
            {
                selectable.Select();
            }
            else if (activeCommand != null
                && !EventSystem.current.IsPointerOverGameObject()
                && Physics.Raycast(cameraRay, out hit, float.MaxValue, interactableLayers | floorLayers))
            {
                ActivateAction(hit);
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
            if (ShouldSetZoomStartTime())
            {
                zoomStartTime = Time.time;
            }

            float zoomTime = Mathf.Clamp01((Time.time - zoomStartTime) * cameraConfig.ZoomSpeed);
            Vector3 targetFollowOffset;

            if (Keyboard.current.endKey.isPressed)
            {
                targetFollowOffset = new Vector3(
                    cinemachineFollow.FollowOffset.x,
                    cameraConfig.MinZoomDistance,
                    cinemachineFollow.FollowOffset.z
                );
            }
            else
            {
                targetFollowOffset = new Vector3(
                    cinemachineFollow.FollowOffset.x,
                    startingFollowOffset.y,
                    cinemachineFollow.FollowOffset.z
                );
            }

            cinemachineFollow.FollowOffset = Vector3.Slerp(
                cinemachineFollow.FollowOffset,
                targetFollowOffset,
                zoomTime
            );
        }

        private bool ShouldSetZoomStartTime()
        {
            return Keyboard.current.endKey.wasPressedThisFrame
                || Keyboard.current.endKey.wasReleasedThisFrame;
        }

        private void HandlePanning()
        {
            Vector2 moveAmount = GetKeyboardMoveAmount();
            moveAmount += GetMouseMoveAmount();

            cameraTarget.linearVelocity = new Vector3(moveAmount.x, 0, moveAmount.y);
        }

        private Vector2 GetMouseMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;

            if (!cameraConfig.EnableEdgePan) { return moveAmount; }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

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

            if (Keyboard.current.upArrowKey.isPressed)
            {
                moveAmount.y += cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                moveAmount.x -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.downArrowKey.isPressed)
            {
                moveAmount.y -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.rightArrowKey.isPressed)
            {
                moveAmount.x += cameraConfig.KeyboardPanSpeed;
            }

            return moveAmount;
        }
    }
}
