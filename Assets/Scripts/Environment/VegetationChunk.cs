using UnityEngine;
using System.Collections.Generic;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Groups a large number of vegetation items and toggles their visibility
    /// based on distance to the camera to save on rendering performance.
    /// </summary>
    public class VegetationChunk : MonoBehaviour
    {
        [SerializeField] private float visibleDistance = 60f;
        private List<Renderer> renderers = new List<Renderer>();
        private bool isVisible = true;
        private float checkInterval = 0.5f;
        private float timer;

        public void AddItem(GameObject item)
        {
            var r = item.GetComponentInChildren<Renderer>();
            if (r != null) renderers.Add(r);
        }

        private void Start()
        {
            timer = Random.Range(0, checkInterval); // Offset update frames
            UpdateVisibility(true);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= checkInterval)
            {
                timer = 0f;
                CheckDistance();
            }
        }

        private void CheckDistance()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float distSq = (transform.position - cam.transform.position).sqrMagnitude;
            bool shouldBeVisible = distSq < (visibleDistance * visibleDistance);

            if (shouldBeVisible != isVisible)
            {
                UpdateVisibility(shouldBeVisible);
            }
        }

        private void UpdateVisibility(bool visible)
        {
            isVisible = visible;
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        public void SetVisibleDistance(float dist) => visibleDistance = dist;
    }
}
