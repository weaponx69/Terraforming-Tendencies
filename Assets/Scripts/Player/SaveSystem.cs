using UnityEngine;
using GameDevTV.RTS.Units;
using System.Collections.Generic;
using System.IO;

namespace GameDevTV.RTS.Player
{
    [System.Serializable]
    public class SaveData
    {
        public int biomass;
        public float oxygen;
    }

    public static class SaveSystem
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "Saves");

        public static void SaveGame(int slot)
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            SaveData data = new SaveData();

            if (Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner.Player1, out int biomass))
            {
                data.biomass = biomass;
            }

            if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float oxygen))
            {
                data.oxygen = oxygen;
            }

            string json = JsonUtility.ToJson(data, true);
            string filePath = GetFilePath(slot);
            File.WriteAllText(filePath, json);
            Debug.Log($"Game Saved to Slot {slot} at {filePath}");
        }

        public static bool HasSave(int slot)
        {
            return File.Exists(GetFilePath(slot));
        }

        public static void LoadGame(int slot)
        {
            if (!HasSave(slot)) return;

            string filePath = GetFilePath(slot);
            string json = File.ReadAllText(filePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (Supplies.Instance != null)
            {
                if (Supplies.Biomass.ContainsKey(Owner.Player1))
                    Supplies.Biomass[Owner.Player1] = data.biomass;
                
                Supplies.UpdateOxygen(Owner.Player1, data.oxygen);
                
                // Refresh UI
                Supplies.RaiseBiomassChanged(Owner.Player1, data.biomass);
            }
            Debug.Log($"Game Loaded from Slot {slot}");
        }

        private static string GetFilePath(int slot)
        {
            return Path.Combine(SavePath, $"save_slot_{slot}.json");
        }
    }
}
