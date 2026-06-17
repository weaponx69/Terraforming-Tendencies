using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public class BatteryNode : MonoBehaviour
    {
        public float MaxCharge = 180f; // 3 minutes by default
        public float CurrentCharge = 180f;

        public void Drain(float amount)
        {
            CurrentCharge = Mathf.Max(0, CurrentCharge - amount);
        }

        public void Charge(float amount)
        {
            CurrentCharge = Mathf.Min(MaxCharge, CurrentCharge + amount);
        }

        public bool HasCharge => CurrentCharge > 0;
    }
}
