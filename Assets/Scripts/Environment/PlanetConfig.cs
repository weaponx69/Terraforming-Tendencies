using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [CreateAssetMenu(menuName = "Terraforming/Planet Config", fileName = "New Planet Config", order = 0)]
    public class PlanetConfig : ScriptableObject
    {
        [Header("Terrain Generation")]
        public int MapWidth = 100;
        public int MapHeight = 100;
        public float NoiseScale = 10f;
        public float HeightMultiplier = 5f;

        [Header("Difficulty Modifiers")]
        public int ResourceCount = 10;
        public float BaseDecayRate = 2f;
        public float ToxicityLevel = 0f;
    }
}
