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
        [SerializeField] private TextMeshProUGUI mineralsText;
        [SerializeField] private TextMeshProUGUI gasText;
        [SerializeField] private TextMeshProUGUI populationText;

        [SerializeField] private SupplySO mineralsSO;
        [SerializeField] private SupplySO gasSO;

        public static Dictionary<Owner, int> Minerals { get; private set; }
        public static Dictionary<Owner, int> Gas { get; private set; }
        public static Dictionary<Owner, int> Population { get; private set; }
        public static Dictionary<Owner, int> PopulationLimit { get; private set; }

        private void Awake()
        {
            Minerals = new Dictionary<Owner, int>();
            Gas = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();

            foreach(Owner owner in Enum.GetValues(typeof(Owner)))
            {
                Minerals.Add(owner, 0);
                Gas.Add(owner, 0);
                Population.Add(owner, 0);
                PopulationLimit.Add(owner, 0);
            }

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
        }

        private void OnDestroy()
        {
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply.Equals(mineralsSO))
            {
                Minerals[evt.Owner] += evt.Amount;
                if (Owner.Player1 == evt.Owner)
                {
                    mineralsText.SetText(Minerals[evt.Owner].ToString());
                }
            }
            else if (evt.Supply.Equals(gasSO))
            {
                Gas[evt.Owner] += evt.Amount;
                if (Owner.Player1 == evt.Owner)
                {
                    gasText.SetText(Gas[evt.Owner].ToString());
                }
            }
        }
    }
}
