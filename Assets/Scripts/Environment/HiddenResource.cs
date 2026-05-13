using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(GatherableSupply))]
    public class HiddenResource : MonoBehaviour
    {
        public bool IsDiscovered { get; private set; } = false;

        private void Start()
        {
            SetVisibleState(false);
        }

        public void Discover()
        {
            if (IsDiscovered) return;
            
            IsDiscovered = true;
            SetVisibleState(true);

            Debug.Log($"Resource Discovered at {transform.position}!");
        }

        private void SetVisibleState(bool state)
        {
            // We disable/enable visual and physics interaction so it remains hidden 
            // from the player's interaction and the Gather commands until discovered.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = state;

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = state;
            
            // The object itself remains Active so ProbeLogic can find it via FindObjectsOfType
        }
    }
}
