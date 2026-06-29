using UnityEngine;
using UnityEngine.UI;

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
