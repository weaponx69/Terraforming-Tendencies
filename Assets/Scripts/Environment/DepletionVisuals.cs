using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [RequireComponent(typeof(GatherableSupply))]
    public class DepletionVisuals : MonoBehaviour
    {
        private GatherableSupply supply;
        private Vector3 originalScale;
        private float initialAmount;

        private void Start()
        {
            supply = GetComponent<GatherableSupply>();
            originalScale = transform.localScale;
            
            // Try to get the initial amount from the SupplySO if current amount is not set
            if (supply.Amount > 0) initialAmount = supply.Amount;
            else if (supply.Supply != null) initialAmount = supply.Supply.MaxAmount;
            else initialAmount = 1500;
        }

        private void Update()
        {
            if (supply == null || initialAmount <= 0) return;

            float ratio = (float)supply.Amount / initialAmount;
            // Scale down to a minimum of 20% of original size
            float scaleFactor = Mathf.Lerp(0.2f, 1.0f, ratio);
            transform.localScale = originalScale * scaleFactor;
        }
    }
}
