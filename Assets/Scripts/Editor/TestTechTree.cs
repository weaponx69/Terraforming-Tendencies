#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GameDevTV.RTS.UI.Containers;
using GameDevTV.RTS.Player;

public static class TestTechTree
{
    [MenuItem("Terraforming/Test Tech Tree UI NOW", false, 1)]
    public static void ForceOpenTechTree()
    {
        // Must be in Play Mode to test the UI instantiation safely
        if (!Application.isPlaying)
        {
            Debug.LogError("You must click the Play button first! Then click this menu item to instantly test the UI without waiting for a round to end.");
            return;
        }

        TechTreeUI techTree = Object.FindAnyObjectByType<TechTreeUI>(FindObjectsInactive.Include);
        if (techTree == null)
        {
            Debug.LogError("Could not find any TechTreeUI in the scene!");
            return;
        }

        // Give the player some testing money
        if (GenerationManager.Instance != null)
        {
            GenerationManager.Instance.TotalTerraCoins += 5000;
            Debug.Log("Granted 5000 Terra Coins for testing!");
        }
        else
        {
            Debug.LogWarning("GenerationManager instance is missing! Cannot add Terra Coins.");
        }

        Debug.Log("Forcing Tech Tree Open!");
        techTree.Open(null); // Pass null for the parent panel just to test if it appears
    }
}
#endif
