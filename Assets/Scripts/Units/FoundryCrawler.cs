using UnityEngine;

/// <summary>
/// Core logistics engine for the Foundry Crawler.
/// Manages movement, resource processing, thermal dynamics, and structural integrity.
/// </summary>
public class FoundryCrawler : MonoBehaviour
{
    [Header("Resource Capacities")]
    public float maxRegolith = 100f;
    public float maxIron = 100f;
    
    [Header("Current Resources")]
    [SerializeField] private float currentRegolith = 0f;
    [SerializeField] private float currentIron = 0f;
    [SerializeField] private int pipeBuffer = 0;

    [Header("Thermal Dynamics")]
    [Tooltip("Current engine heat, from 0 to 100")]
    [Range(0f, 100f)]
    [SerializeField] private float engineTemperature = 0f;
    private const float PASSIVE_HEAT_RATE = 1.0f; // +1% heat per second
    private const float DRY_ICE_COOLING_FACTOR = 10.0f; // -10% heat per unit of dry ice

    [Header("Movement & Pathing")]
    public float movementSpeed = 5.0f;
    public Vector3 targetPosition;
    public bool isOnPipeline = true;

    [Header("Structural Integrity")]
    public float maxHealth = 1000f;
    [SerializeField] private float currentHealth;
    private const float OFFLINE_DAMAGE_RATE = 50.0f; // Damage taken per second when off pipeline

    [Header("Production Settings")]
    private const float REGOLITH_COST = 5.0f;
    private const float IRON_COST = 2.0f;
    private const float PRODUCTION_CYCLE_TIME = 3.0f;
    
    private float productionTimer = 0f;
    private bool isProducing = false;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Update()
    {
        HandleMovement();
        HandleThermalDynamics();
        HandleStructuralIntegrity();
        HandleProduction();
    }

    /// <summary>
    /// Moves the Crawler toward its target position at a constant speed.
    /// </summary>
    private void HandleMovement()
    {
        if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position += direction * (movementSpeed * Time.deltaTime);
            
            // Optional: Rotate to face target
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    /// <summary>
    /// Increases heat over time. Triggers game over if maximum temperature is reached.
    /// </summary>
    private void HandleThermalDynamics()
    {
        engineTemperature += PASSIVE_HEAT_RATE * Time.deltaTime;
        
        if (engineTemperature >= 100f)
        {
            engineTemperature = 100f;
            GameOver("Critical Meltdown: Engine temperature exceeded maximum tolerance.");
        }
    }

    /// <summary>
    /// Punishes the crawler for leaving the logistics pipeline network.
    /// </summary>
    private void HandleStructuralIntegrity()
    {
        if (!isOnPipeline)
        {
            currentHealth -= OFFLINE_DAMAGE_RATE * Time.deltaTime;
            
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                GameOver("Structural Failure: Crawler destroyed due to lack of pipeline support.");
            }
        }
    }

    /// <summary>
    /// Consumes raw resources over a set interval to generate output pipes.
    /// </summary>
    private void HandleProduction()
    {
        // Check if we have enough resources to start or sustain a cycle
        bool hasResources = currentRegolith >= REGOLITH_COST && currentIron >= IRON_COST;

        if (hasResources)
        {
            isProducing = true;
            productionTimer += Time.deltaTime;

            if (productionTimer >= PRODUCTION_CYCLE_TIME)
            {
                // Consume resources
                currentRegolith -= REGOLITH_COST;
                currentIron -= IRON_COST;
                
                // Increment output buffer
                pipeBuffer++;
                
                // Reset timer for the next batch
                productionTimer = 0f;
                
                // Note: user prefers no console logs normally, but requested a debug log for the cooling method.
                // I will keep the logs minimal or remove them as per rules, except the one explicitly requested.
            }
        }
        else
        {
            isProducing = false;
            productionTimer = 0f; // Reset progress if starved of resources
        }
    }

    /// <summary>
    /// Injects coolant into the engine to reduce temperature.
    /// </summary>
    /// <param name="amount">Units of dry ice consumed.</param>
    public void ConsumeDryIce(float amount)
    {
        if (amount <= 0) return;

        float coolingProvided = amount * DRY_ICE_COOLING_FACTOR;
        engineTemperature = Mathf.Max(0f, engineTemperature - coolingProvided);
        
        Debug.Log("Venting vapor!");
    }

    /// <summary>
    /// Handles the failure state of the Crawler.
    /// </summary>
    private void GameOver(string reason)
    {
        // Disable crawler operations
        enabled = false;
        
        // TODO: Hook into a central GameManager event system here
    }

    // --- Helper Methods for external scripts to load resources ---
    
    public void AddRegolith(float amount)
    {
        currentRegolith = Mathf.Clamp(currentRegolith + amount, 0, maxRegolith);
    }

    public void AddIron(float amount)
    {
        currentIron = Mathf.Clamp(currentIron + amount, 0, maxIron);
    }
}
