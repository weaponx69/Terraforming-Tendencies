using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.UI.Components
{
    public class ResourceHoverLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string resourceName;
        [SerializeField] private GameObject labelPrefab;
        [SerializeField] private Vector3 offset = new Vector3(0, 2.5f, 0);

        private GameObject activeLabel;
        private TextMeshPro labelText;

        private void Start()
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                resourceName = gameObject.name.Replace("(Clone)", "").Trim();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (activeLabel == null)
            {
                activeLabel = new GameObject("Hover Label", typeof(TextMeshPro));
                activeLabel.transform.SetParent(transform);
                activeLabel.transform.localPosition = offset;
                
                labelText = activeLabel.GetComponent<TextMeshPro>();
                labelText.text = resourceName;
                labelText.fontSize = 5;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.color = Color.yellow;
                
                // Make it face the camera
                activeLabel.AddComponent<FaceCamera>();
            }
            activeLabel.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (activeLabel != null)
            {
                activeLabel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (activeLabel != null)
            {
                Destroy(activeLabel);
            }
        }
    }

    public class FaceCamera : MonoBehaviour
    {
        private Transform camTransform;

        private void Start()
        {
            if (Camera.main != null)
                camTransform = Camera.main.transform;
        }

        private void LateUpdate()
        {
            if (camTransform != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - camTransform.position);
            }
        }
    }
}