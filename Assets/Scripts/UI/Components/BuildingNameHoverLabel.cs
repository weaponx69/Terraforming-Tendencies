using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Hides prefab "Building Label" / "Label Canvas" nameplates by default and
    /// shows them only while the pointer is over this building. Forces 14pt.
    /// </summary>
    [RequireComponent(typeof(BaseBuilding))]
    public class BuildingNameHoverLabel : MonoBehaviour
    {
        private const float LabelFontSize = 14f;
        private const float MaxHoverDistance = 120f;

        private BaseBuilding building;
        private GameObject labelRoot;
        private TextMeshProUGUI labelTmp;
        private bool visible;

        private void Awake()
        {
            building = GetComponent<BaseBuilding>();
        }

        private void Start()
        {
            if (building == null || IsGhostOrInvalid())
            {
                enabled = false;
                return;
            }

            CacheLabel();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (building == null || IsGhostOrInvalid())
            {
                SetVisible(false);
                return;
            }

            if (labelRoot == null)
                CacheLabel();
            if (labelRoot == null) return;

            bool want = IsPointerOverThisBuilding();
            if (want != visible)
                SetVisible(want);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private bool IsGhostOrInvalid()
        {
            if (building == null) return true;
            string n = building.gameObject.name;
            return n.StartsWith("Ghost_", System.StringComparison.Ordinal)
                || n.StartsWith("GhostPreview_", System.StringComparison.Ordinal);
        }

        private void CacheLabel()
        {
            Transform canvas = FindChildRecursive(transform, "Label Canvas");
            if (canvas != null)
            {
                labelRoot = canvas.gameObject;
                labelTmp = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            else
            {
                Transform label = FindChildRecursive(transform, "Building Label");
                if (label == null) return;
                labelRoot = label.gameObject;
                labelTmp = label.GetComponent<TextMeshProUGUI>();
            }

            ApplyFont();
            if (labelRoot.GetComponentInParent<FaceCamera>() == null
                && labelRoot.GetComponent<FaceCamera>() == null
                && labelRoot.name == "Label Canvas")
            {
                labelRoot.AddComponent<FaceCamera>();
            }
        }

        private void ApplyFont()
        {
            if (labelTmp == null) return;
            labelTmp.enableAutoSizing = false;
            labelTmp.fontSize = LabelFontSize;
            labelTmp.fontSizeMin = LabelFontSize;
            labelTmp.fontSizeMax = LabelFontSize;
            labelTmp.raycastTarget = false;
        }

        private void SetVisible(bool on)
        {
            visible = on;
            if (labelRoot != null && labelRoot.activeSelf != on)
                labelRoot.SetActive(on);
            if (on) ApplyFont();
        }

        private bool IsPointerOverThisBuilding()
        {
            if (Camera.main == null) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxHoverDistance))
                return false;

            var hitBuilding = hit.collider.GetComponentInParent<BaseBuilding>();
            return hitBuilding == building;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
