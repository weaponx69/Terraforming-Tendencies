using UnityEngine;
using GameDevTV.RTS.Units;
using System.Collections.Generic;

namespace GameDevTV.RTS.Player
{
    public static class SaveSystem
    {
        private const string BIOMASS_KEY = "Save_Biomass";
        private const string OXYGEN_KEY = "Save_Oxygen";
        private const string HAS_SAVE_KEY = "Save_Exists";

        public static void SaveGame()
        {
            if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int biomass))
            {
                PlayerPrefs.SetInt(BIOMASS_KEY, biomass);
            }

            if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float oxygen))
            {
                PlayerPrefs.SetFloat(OXYGEN_KEY, oxygen);
            }

            PlayerPrefs.SetInt(HAS_SAVE_KEY, 1);
            PlayerPrefs.Save();
        }

        public static bool HasSave()
        {
            return PlayerPrefs.GetInt(HAS_SAVE_KEY, 0) == 1;
        }

        public static void LoadGame()
        {
            if (!HasSave()) return;

            int biomass = PlayerPrefs.GetInt(BIOMASS_KEY);
            float oxygen = PlayerPrefs.GetFloat(OXYGEN_KEY);

            if (Supplies.Instance != null)
            {
                if (Supplies.Biomass.ContainsKey(Owner.Player1))
                    Supplies.Biomass[Owner.Player1] = biomass;
                
                Supplies.UpdateOxygen(Owner.Player1, oxygen);
                
                // Refresh UI
                Supplies.RaiseBiomassChanged(Owner.Player1, biomass);
            }
        }
    }
}
