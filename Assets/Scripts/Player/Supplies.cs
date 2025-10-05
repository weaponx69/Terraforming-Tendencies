using System;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    public class Supplies : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI biomassText;
        [SerializeField] private SupplySO biomassSO;

        // conversion multipliers (inspector-editable)
        [SerializeField] private float mineralsToBiomassRate = 1f;
        [SerializeField] private float gasToBiomassRate = 1f;

    // static copies of rates so other classes (commands) can compute costs
    public static float MineralsToBiomassRateStatic { get; private set; } = 1f;
    public static float GasToBiomassRateStatic { get; private set; } = 1f;

    [SerializeField] private TextMeshProUGUI populationText;

    [SerializeField] private SupplySO mineralsSO;
    [SerializeField] private SupplySO gasSO;

    public static Dictionary<Owner, int> Biomass { get; private set; }
    public static Dictionary<Owner, int> Population { get; private set; }
    public static Dictionary<Owner, int> PopulationLimit { get; private set; }

        private void Awake()
        {

            Biomass = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                Biomass.Add(owner, 0);
                Population.Add(owner, 0);
                PopulationLimit.Add(owner, 0);
            }

            // publish selected conversion rates for static use
            MineralsToBiomassRateStatic = mineralsToBiomassRate;
            GasToBiomassRateStatic = gasToBiomassRate;

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
        }

        private void OnDestroy()
        {
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            // Defensive: evt.Supply may be null in some cases. Ignore if so.
            if (evt.Supply == null) return;
            // Convert minerals/gas supply events to biomass centrally.
            if (evt.Supply == mineralsSO)
            {
                int biomassAmount = Mathf.FloorToInt(evt.Amount * mineralsToBiomassRate);
                Biomass[evt.Owner] += biomassAmount;
                if (Owner.Player1 == evt.Owner && biomassText != null)
                {
                    biomassText.SetText(Biomass[evt.Owner].ToString());
                }
                return; // handled centrally - don't modify Minerals/Gas
            }
            else if (evt.Supply == gasSO)
            {
                int biomassAmount = Mathf.FloorToInt(evt.Amount * gasToBiomassRate);
                Biomass[evt.Owner] += biomassAmount;
                if (Owner.Player1 == evt.Owner && biomassText != null)
                {
                    biomassText.SetText(Biomass[evt.Owner].ToString());
                }
                return;
            }

            // Other supply types (if any) can be handled here in future.
        }
    }
}
