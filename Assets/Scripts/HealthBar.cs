using UnityEngine;
using UnityEngine.UI;
using GameDevTV.RTS.UI.Components;

public class HealthBar : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;
    public GameObject progressBar;

    void Update()
    {
        float healthPercentage = currentHealth / maxHealth;
        progressBar.GetComponent<ProgressBar>().SetProgress(healthPercentage);
    }
}
