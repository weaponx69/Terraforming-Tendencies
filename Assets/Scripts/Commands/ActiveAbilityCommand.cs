using UnityEngine;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using System.Collections.Generic;

namespace GameDevTV.RTS.Commands
{
    public class ActiveAbilityCommand : BaseCommand
    {
        private string _description;
        private float _tempBonus;
        private float _atmosBonus;
        private float _oxyBonus;
        private int _matsBonus;
        private int _bioBonus;
        private float _waterBonus;
        private float _cooldown = 10f; // 10 second cooldown
        private float _lastUsedTime = -999f;

        public ActiveAbilityCommand()
        {
            RequiresClickToActivate = false;
        }

        public void Initialize(string name, string desc, float temp, float atmos, float oxy, int mats, int bio, float water = 0f)
        {
            Name = name;
            _description = desc;
            _tempBonus = temp;
            _atmosBonus = atmos;
            _oxyBonus = oxy;
            _matsBonus = mats;
            _bioBonus = bio;
            _waterBonus = water;
            
            // Try to load a plug icon or use any fallback
            Icon = Resources.Load<Sprite>("PlugIcon");
        }

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable != null && (Time.time - _lastUsedTime >= _cooldown);
        }

        public override void Handle(CommandContext context)
        {
            _lastUsedTime = Time.time;
            Owner owner = context.Owner;

            if (_tempBonus > 0f)
            {
                float cur = Supplies.Temperature.TryGetValue(owner, out float val) ? val : -60f;
                Supplies.UpdateTemperature(owner, cur + _tempBonus);
            }
            if (_atmosBonus > 0f)
            {
                float cur = Supplies.Atmosphere.TryGetValue(owner, out float val) ? val : 0.01f;
                Supplies.UpdateAtmosphere(owner, cur + _atmosBonus);
            }
            if (_oxyBonus > 0f)
            {
                float cur = Supplies.Oxygen.TryGetValue(owner, out float val) ? val : 0f;
                Supplies.UpdateOxygen(owner, cur + _oxyBonus);
            }
            if (_matsBonus > 0)
            {
                int cur = Supplies.Materials.TryGetValue(owner, out int val) ? val : 0;
                Supplies.Materials[owner] = cur + _matsBonus;
                Supplies.RaiseMaterialsChanged(owner, cur + _matsBonus);
            }
            if (_bioBonus > 0)
            {
                float cur = Supplies.Biomass.TryGetValue(owner, out float val) ? val : 0f;
                Supplies.UpdateBiomass(owner, cur + _bioBonus);
            }
            if (_waterBonus > 0f)
            {
                float cur = Supplies.Water.TryGetValue(owner, out float val) ? val : 0f;
                Supplies.UpdateWater(owner, cur + _waterBonus);
            }

            // Trigger re-selection to refresh UI
            if (context.Commandable.IsSelected)
            {
                context.Commandable.Select();
            }
        }

        public override bool IsLocked(CommandContext context)
        {
            if (context.Commandable is BaseBuilding building)
            {
                if (!building.IsOperating) return true;
            }
            return Time.time - _lastUsedTime < _cooldown;
        }
    }
}
