using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public class GrowingVegetation : MonoBehaviour
    {
        [SerializeField] private float growthDuration = 300f; // 5 minutes to full size
        [SerializeField] private Vector3 targetScale = Vector3.one;
        
        private float growthProgress = 0f;
        private Vector3 initialScale;

        private void Start()
        {
            // Randomize duration and target scale slightly
            growthDuration *= Random.Range(0.8f, 1.2f);
            targetScale *= Random.Range(0.7f, 1.3f);
            
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (growthProgress < 1f)
            {
                growthProgress += Time.deltaTime / growthDuration;
                transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, growthProgress);
            }
            else
            {
                // Once grown, we can disable this script
                enabled = false;
            }
        }
    }
}
