using UnityEngine;
using GameDevTV.RTS.Units;

[CreateAssetMenu(menuName = "Terraforming/Game Configuration", fileName = "GameConfiguration")]
public class GameConfiguration : ScriptableObject
{
    private static GameConfiguration instance;
    public static GameConfiguration Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameConfiguration>("GameConfiguration");
            }
            return instance;
        }
    }

    [Header("Core Prefabs")]
    public GameObject CommandPostPrefab;
    
    [Header("Unit & Building Data")]
    public BuildingSO CommandPostSO;
    public AbstractUnitSO WorkerUnitSO;
    public BuildingSO AirportSO;
    public AbstractUnitSO MiningDroneUnitSO;
}
