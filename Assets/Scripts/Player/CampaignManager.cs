using UnityEngine;

namespace GameDevTV.RTS.Player
{
    public class CampaignManager : MonoBehaviour
    {
        public static CampaignManager Instance { get; private set; }

        [SerializeField] private GameDevTV.RTS.Environment.PlanetConfig[] levelConfigs;
        public int CurrentLevelIndex { get; private set; } = 0;

        public GameDevTV.RTS.Environment.PlanetConfig CurrentPlanet => 
            levelConfigs != null && CurrentLevelIndex < levelConfigs.Length 
                ? levelConfigs[CurrentLevelIndex] 
                : null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void AdvanceLevel()
        {
            CurrentLevelIndex++;
            if (CurrentLevelIndex >= levelConfigs.Length)
            {
                Debug.Log("Campaign Completed!");
            }
            else
            {
                // In a real game, this would load the next scene.
                // For MVP, we might just regenerate the planet in the current scene.
            }
        }
    }
}
