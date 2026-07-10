using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;
using System.Linq;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Represents a power-grid connection point on a building.
    /// <para>
    /// Heavy logic (LineRenderer cord generation with Mathf.Sin jitter,
    /// OnDestroy foreach cleanup, BatteryNode charge checks) stays in C#.
    /// VS-visible surface exposes grid state, connection count, and the
    /// <see cref="ConnectTo"/> method for graph-driven power wiring.
    /// </para>
    /// </summary>
    [IncludeInSettings(true)]
    [RequireComponent(typeof(BaseBuilding))]
    public class PowerNode : MonoBehaviour
    {
        /// <summary>All power nodes directly connected to this one.</summary>
        [Inspectable]
        public List<PowerNode> ConnectedNodes = new List<PowerNode>();

        /// <summary>Visual cord GameObjects mapped by connected neighbor node.</summary>
        public Dictionary<PowerNode, GameObject> visualCords = new Dictionary<PowerNode, GameObject>();

        /// <summary>The building this power node is attached to.</summary>
        [Inspectable]
        public BaseBuilding Building { get; private set; }

        /// <summary>Number of directly connected power nodes. VS-friendly alternative to List.Count.</summary>
        [Inspectable]
        public int ConnectedNodeCount => ConnectedNodes.Count;

        /// <summary>True if this node has a BatteryNode with stored charge as backup.</summary>
        [Inspectable]
        public bool HasBatteryBackup =>
            TryGetComponent(out BatteryNode battery) && battery.HasCharge;

        [SerializeField] private bool isGridPowered = false;

        /// <summary>
        /// Whether this node is receiving power from the grid.
        /// Setting this triggers <see cref="OnPowerStateChanged"/> if the
        /// effective <see cref="IsPowered"/> state changes.
        /// </summary>
        [Inspectable]
        public bool IsGridPowered
        {
            get => isGridPowered;
            set
            {
                bool wasPowered = IsPowered;
                isGridPowered = value;
                if (IsPowered != wasPowered)
                {
                    OnPowerStateChanged?.Invoke(IsPowered);
                }
            }
        }

        [Header("Temporary Startup Power")]
        [SerializeField] private float temporaryPowerDuration = 90f; // 90 seconds
        private float temporaryPowerEndTime;
        private bool hasTemporaryPower = false;

        public void StartTemporaryPower()
        {
            hasTemporaryPower = true;
            temporaryPowerEndTime = Time.time + temporaryPowerDuration;
        }

        /// <summary>
        /// Effective power state: true if grid-powered OR has battery backup.
        /// Read by Flow Graphs to branch on power-loss events.
        /// </summary>
        [Inspectable]
        public bool IsPowered
        {
            get
            {
                if (hasTemporaryPower)
                {
                    if (Time.time < temporaryPowerEndTime)
                    {
                        return true;
                    }
                    else
                    {
                        hasTemporaryPower = false;
                    }
                }

                if (isGridPowered) return true;
                if (TryGetComponent(out BatteryNode battery) && battery.HasCharge) return true;
                return false;
            }
        }

        /// <summary>Fires when <see cref="IsPowered"/> changes. Subscribe in C# or via a VS Custom Event listener.</summary>
        public event System.Action<bool> OnPowerStateChanged;

        private void Awake()
        {
            Building = GetComponent<BaseBuilding>();
        }

        private void Start()
        {
            PowerGridManager.RegisterNode(this);
            
            // Command Post starting backup power cells
            if (Building != null && Building.BuildingSO != null && 
                Building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                StartTemporaryPower();
            }
        }

        private void OnDestroy()
        {
            PowerGridManager.UnregisterNode(this);
            foreach (var kvp in visualCords.ToList())
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
                if (kvp.Key != null)
                {
                    kvp.Key.visualCords.Remove(this);
                }
            }
            visualCords.Clear();

            foreach(var node in ConnectedNodes.ToList())
            {
                if (node != null)
                {
                    node.ConnectedNodes.Remove(this);
                }
            }
            PowerGridManager.RecalculateGrids();
        }

        /// <summary>
        /// Severs the connection to another node, cleaning up visual tubes.
        /// </summary>
        public void DisconnectFrom(PowerNode other)
        {
            if (other == null) return;
            if (ConnectedNodes.Contains(other)) ConnectedNodes.Remove(other);
            if (other.ConnectedNodes.Contains(this)) other.ConnectedNodes.Remove(this);

            if (visualCords.TryGetValue(other, out GameObject cord))
            {
                if (cord != null) Destroy(cord);
                visualCords.Remove(other);
            }
            if (other.visualCords.TryGetValue(this, out GameObject otherCord))
            {
                if (otherCord != null) Destroy(otherCord);
                other.visualCords.Remove(this);
            }

            PowerGridManager.RecalculateGrids();
        }

        /// <summary>
        /// Connects this power node to another, creating a pressurized tube connection
        /// and recalculating the power grid.
        /// </summary>
        [Inspectable]
        public void ConnectTo(PowerNode other)
        {
            if (other == null || other == this) return;
            if (!ConnectedNodes.Contains(other)) ConnectedNodes.Add(other);
            if (!other.ConnectedNodes.Contains(this)) other.ConnectedNodes.Add(this);
            
            PowerGridManager.RecalculateGrids();

            if (visualCords.ContainsKey(other)) return; // Avoid double draw
            
            // Create Visual Cord (Thick Pressurized Tube)
            GameObject cordGO = new GameObject($"PowerCord_{this.name}_{other.name}");
            if (PowerGridManager.Instance != null)
            {
                cordGO.transform.SetParent(PowerGridManager.Instance.transform);
            }
            var lr = cordGO.AddComponent<LineRenderer>();
            
            // Set up LineRenderer as a thick tube
            lr.positionCount = 10;
            bool solid = GameDevTV.RTS.Player.BlueprintDraftManager.TubesAreSolid;
            lr.startWidth = solid ? 1.0f : 0.7f;
            lr.endWidth = lr.startWidth;
            
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat == null) mat = new Material(Shader.Find("Unlit/Color"));

            // Light-blue transparent glass color for Inflatable, solid gray color for Solid Tubes
            mat.color = solid ? new Color(0.75f, 0.75f, 0.75f, 1f) : new Color(0.6f, 0.85f, 0.95f, 0.5f);
            lr.material = mat;

            visualCords[other] = cordGO;
            other.visualCords[this] = cordGO;

            var pt = cordGO.AddComponent<PressurizedTube>();
            pt.Initialize(this, other, lr);
            
            // Generate tube lying on the ground
            Vector3 startPoint = transform.position + Vector3.up * 0.15f; 
            Vector3 endPoint = other.transform.position + Vector3.up * 0.15f;
            
            for(int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                Vector3 p = Vector3.Lerp(startPoint, endPoint, t);
                
                // Add minor horizontal jitter to inner points to make it look organic
                if (i > 0 && i < 9)
                {
                    Vector3 direction = (endPoint - startPoint).normalized;
                    if (direction == Vector3.zero) direction = Vector3.forward;
                    Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                    if (right == Vector3.zero) right = Vector3.right;
                    float jitter = Mathf.Sin(t * Mathf.PI * 3f) * 0.15f; // Straighter than wires
                    p += right * jitter;
                }
                
                lr.SetPosition(i, p);
            }
        }
    }
}
