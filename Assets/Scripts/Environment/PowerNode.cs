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

        /// <summary>
        /// Effective power state: true if grid-powered OR has battery backup.
        /// Read by Flow Graphs to branch on power-loss events.
        /// </summary>
        [Inspectable]
        public bool IsPowered
        {
            get
            {
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
            // Optionally, we could try to auto-connect to very close nodes here,
            // but manual connection is preferred per Option 3.
        }

        private void OnDestroy()
        {
            PowerGridManager.UnregisterNode(this);
            foreach(var node in ConnectedNodes.ToList())
            {
                if (node != null)
                {
                    node.ConnectedNodes.Remove(this);
                    // Also destroy visual cord if we had a two-way dict of cords
                }
            }
            PowerGridManager.RecalculateGrids();
        }

        /// <summary>
        /// Connects this power node to another, creating a visual cord and
        /// recalculating the power grid. Heavy LineRenderer math stays in C#.
        /// Callable from a Flow Graph to wire power dynamically.
        /// </summary>
        [Inspectable]
        public void ConnectTo(PowerNode other)
        {
            if (other == null || other == this) return;
            if (!ConnectedNodes.Contains(other)) ConnectedNodes.Add(other);
            if (!other.ConnectedNodes.Contains(this)) other.ConnectedNodes.Add(this);
            
            PowerGridManager.RecalculateGrids();
            
            // Create Visual Cord
            GameObject cordGO = new GameObject($"PowerCord_{this.name}_{other.name}");
            if (PowerGridManager.Instance != null)
            {
                cordGO.transform.SetParent(PowerGridManager.Instance.transform);
            }
            var lr = cordGO.AddComponent<LineRenderer>();
            
            // Set up LineRenderer
            lr.positionCount = 10;
            lr.startWidth = 0.5f;
            lr.endWidth = 0.5f;
            
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat == null) mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = Color.black; // Black cord
            lr.material = mat;
            
            // Generate cord laying on the ground
            Vector3 startPoint = transform.position + Vector3.up * 0.2f; // Barely above ground
            Vector3 endPoint = other.transform.position + Vector3.up * 0.2f;
            
            // Add a little bit of noise to make it look like a wire tossed on the ground
            for(int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                Vector3 p = Vector3.Lerp(startPoint, endPoint, t);
                
                // Add horizontal jitter to inner points to make it look slightly snakey
                if (i > 0 && i < 9)
                {
                    Vector3 direction = (endPoint - startPoint).normalized;
                    if (direction == Vector3.zero) direction = Vector3.forward;
                    Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                    if (right == Vector3.zero) right = Vector3.right;
                    float jitter = Mathf.Sin(t * Mathf.PI * 3f) * 0.3f; // Slight wave
                    p += right * jitter;
                }
                
                lr.SetPosition(i, p);
            }
        }
    }
}
