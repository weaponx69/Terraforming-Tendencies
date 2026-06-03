using System.Collections;
using UnityEngine;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class SurvivalManager : MonoBehaviour
    {
        [SerializeField] private float tickRate = 1f;
        
        private float biomassDrainRate = 1f;
        private float integrityDrainRate = 0.5f;
        private Owner monitoredOwner = Owner.Player1;

        private void Start()
        {
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                biomassDrainRate = PlanetGenerator.Instance.Config.BiomassDrainRate;
                integrityDrainRate = PlanetGenerator.Instance.Config.IntegrityDrainRate;
            }

            monitoredOwner = GameOverManager.MonitoredOwner;
            StartCoroutine(SurvivalLoop());
        }

        private IEnumerator SurvivalLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(tickRate);

                // Drain Biomass
                if (Supplies.Biomass != null && Supplies.Biomass.ContainsKey(monitoredOwner))
                {
                    int currentBiomass = Supplies.Biomass[monitoredOwner];
                    int drain = Mathf.CeilToInt(biomassDrainRate * tickRate);
                    Supplies.Biomass[monitoredOwner] = Mathf.Max(0, currentBiomass - drain);
                    Supplies.RaiseBiomassChanged(monitoredOwner, Supplies.Biomass[monitoredOwner]);
                }

                // Recalculate Integrity based on total unit/building health
                float calculatedIntegrity = Supplies.CalculateIntegrity(monitoredOwner);
                Supplies.UpdateIntegrity(monitoredOwner, calculatedIntegrity);
            }
        }
    }
}
