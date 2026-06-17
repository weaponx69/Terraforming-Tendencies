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
            cordGO.transform.SetParent(PowerGridManager.Instance.transform);
            var lr = cordGO.AddComponent<LineRenderer>();
            
            // Set up LineRenderer
            lr.positionCount = 10;
            lr.startWidth = 0.5f;
            lr.endWidth = 0.5f;
            
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat == null) mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(1f, 0.8f, 0f, 1f); // Yellow cord
            lr.material = mat;
            
            // Generate sagging curve
            Vector3 startPoint = transform.position + Vector3.up * 8f; // Top of building
            Vector3 endPoint = other.transform.position + Vector3.up * 8f;
            Vector3 midPoint = (startPoint + endPoint) / 2f;
            float distance = Vector3.Distance(startPoint, endPoint);
            midPoint.y -= distance * 0.1f; // Sag amount proportional to distance
            
            for(int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                // Quadratic bezier
                Vector3 p = Vector3.Lerp(Vector3.Lerp(startPoint, midPoint, t), Vector3.Lerp(midPoint, endPoint, t), t);
                lr.SetPosition(i, p);
            }
        }
    }
}
