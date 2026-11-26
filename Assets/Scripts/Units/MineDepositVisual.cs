using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Swaps placeholder mine geometry for an authored industrial mesh (Foundry / O₂ plant, etc.).
    /// </summary>
    public class MineDepositVisual : MonoBehaviour
    {
        [SerializeField] GameObject visualPrefab;
        [SerializeField] Vector3 localOffset = Vector3.zero;
        [SerializeField] Vector3 localScale = Vector3.one;
        [SerializeField] bool hidePlaceholderChildren = true;

        void Awake()
        {
            if (visualPrefab == null) return;

            // Themed buildings already ship a hex-sized SiteMesh — do not stack Foundry GLB geometry.
            if (HasAuthoredBuildingMesh())
                return;

            if (hidePlaceholderChildren)
            {
                foreach (Transform child in transform)
                {
                    string n = child.name;
                    if (n.IndexOf("Placeholder", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Building_02", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Cylinder", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Cube", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Sphere", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            var go = Instantiate(visualPrefab, transform);
            go.name = visualPrefab.name + " (MineVisual)";
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            // Foundry / unit meshes may carry gameplay scripts — keep visuals only.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is MineDepositVisual) continue;
                string typeName = mb.GetType().Name;
                if (typeName.IndexOf("Crawler", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf("WorldUI", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf("FaceCamera", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf("Commandable", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Destroy(mb);
                }
            }
        }

        bool HasAuthoredBuildingMesh()
        {
            foreach (Transform child in transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (child.name.IndexOf("Building Mesh", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    return true;
            }
            return false;
        }
    }
}
