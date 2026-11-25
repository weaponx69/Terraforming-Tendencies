using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>Spins windmill blades when the building is completed and powered.</summary>
    public class WindTurbineRotor : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 48f;
        [SerializeField] float stormBoost = 1.35f;

        BaseBuilding host;

        void Awake()
        {
            host = GetComponentInParent<BaseBuilding>();
        }

        void Update()
        {
            float rate = degreesPerSecond;
            var colony = GameDevTV.RTS.Prologue.PrologueController.Instance?.Colony;
            if (colony != null && colony.DustStormActive)
                rate *= stormBoost;
            if (host != null && host.Progress.State != BuildingProgress.BuildingState.Completed)
                rate *= 0.15f;
            transform.Rotate(0f, rate * Time.deltaTime, 0f, Space.Self);
        }
    }

}