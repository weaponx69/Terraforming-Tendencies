using UnityEngine;
using UnityEngine.UI;

public class Supplies : MonoBehaviour
{
    public GameObject healthBar; // Reference to the health bar UI

    void Start()
    {
        // Assign the health bar prefab in the Unity Editor
        // healthBar = Instantiate(ProgressBarPrefab); // Optional if instantiating at runtime
    }

    // Method to update health bar when supplies change
    public void UpdateHealthBar(float currentIntegrity)
    {
        if (healthBar != null)
        {
            healthBar.GetComponent<HealthBar>().currentHealth = currentIntegrity;
        }
    }
}