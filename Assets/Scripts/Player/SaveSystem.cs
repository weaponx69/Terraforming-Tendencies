using UnityEngine;
using GameDevTV.RTS.Units;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDevTV.RTS.Player
{
    [System.Serializable]
    public class GameData
    {
        public int materials;
        public float oxygen;
        public float integrity;
        public float generationProgress;
        public int generationRoundCount;
        public float colonyExpansionProgress;
        public int mapSeed;
    }

    public static class SaveSystem
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "Saves");

        public static void SaveGame(int slotIndex)
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            GameData data = new GameData();

            if (Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner.Player1, out int materials))
            {
                data.materials = materials;
            }

            if (Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner.Player1, out float oxygen))
            {
                data.oxygen = oxygen;
            }

            if (Supplies.Integrity != null && Supplies.Integrity.TryGetValue(Owner.Player1, out float integrity))
            {
                data.integrity = integrity;
            }

            if (GenerationManager.Instance != null)
            {
                data.generationRoundCount = GenerationManager.Instance.CurrentGeneration;
            }

            string json = JsonUtility.ToJson(data, true);
            string filePath = GetFilePath(slotIndex);
            File.WriteAllText(filePath, json);
            Debug.Log($"[SaveSystem] Game saved to slot {slotIndex}");
        }

        public static bool HasSave(int slot)
        {
            return File.Exists(GetFilePath(slot));
        }

        public static void LoadGame(int slotIndex)
        {
            string path = GetFilePath(slotIndex);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            GameData data = JsonUtility.FromJson<GameData>(json);

            if (Supplies.Instance != null)
            {
                if (Supplies.Materials.ContainsKey(Owner.Player1))
                    Supplies.Materials[Owner.Player1] = data.materials;
                else
                    Supplies.Materials.Add(Owner.Player1, data.materials);
                
                Supplies.UpdateOxygen(Owner.Player1, data.oxygen);
                
                // Refresh UI
                Supplies.RaiseMaterialsChanged(Owner.Player1, data.materials);
            }
            Debug.Log($"Game Loaded from Slot {slotIndex}");
        }

        private static string GetFilePath(int slot)
        {
            return Path.Combine(SavePath, $"save_slot_{slot}.json");
        }
    }
}
