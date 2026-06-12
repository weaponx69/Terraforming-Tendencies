using System;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Represents the player's unique mobile command center unit.
    /// Inherits directly from AbstractCommandable to integrate with RTS selection and health,
    /// bypassing the automated logic and BehaviorGraph requirements of standard worker drones.
    /// </summary>
    public class HeroDrone : AbstractCommandable
    {
        [Header("Cargo Capacity")]
        [SerializeField] private int maxCapacity = 25;
        
        public GameDevTV.RTS.Environment.SupplySO CarriedSupply { get; private set; }
        public int CarriedAmount { get; private set; }
        
        public int MaxCapacity => maxCapacity;

        public void AddCargo(GameDevTV.RTS.Environment.SupplySO supply, int amount)
        {
            if (CarriedSupply == null || CarriedSupply != supply)
            {
                CarriedSupply = supply;
                CarriedAmount = 0;
            }
            CarriedAmount += amount;
        }

        public void ClearCargo()
        {
            CarriedSupply = null;
            CarriedAmount = 0;
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeIfNeeded();
        }
        
        public override void InitializeIfNeeded()
        {
            base.InitializeIfNeeded();
            // Fallback health initialization if not already set by inspector or SO
            if (MaxHealth == 0)
            {
                MaxHealth = 500;
                CurrentHealth = 500;
            }
        }
    }
}
