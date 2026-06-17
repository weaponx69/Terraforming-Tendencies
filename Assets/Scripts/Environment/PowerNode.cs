using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;
using System.Linq;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(BaseBuilding))]
    public class PowerNode : MonoBehaviour
    {
        public List<PowerNode> ConnectedNodes = new List<PowerNode>();
        public BaseBuilding Building { get; private set; }
        
        [SerializeField] private bool isPowered = false;
        public bool IsPowered 
        { 
            get => isPowered; 
            set 
            {
                if (isPowered != value)
                {
                    isPowered = value;
                    OnPowerStateChanged?.Invoke(isPowered);
                }
            } 
        }

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
                    Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                    float jitter = Mathf.Sin(t * Mathf.PI * 3f) * 0.3f; // Slight wave
                    p += right * jitter;
                }
                
                lr.SetPosition(i, p);
            }
        }
    }
}
