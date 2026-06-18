using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Building", menuName = "Buildings/Building")]
    public class BuildingSO : AbstractUnitSO
    {
        [field: SerializeField] public Material PlacementMaterial { get; set; }

        [Tooltip("If true, this building acts as a Life Support node and protects nearby buildings from decay.")]
        [field: SerializeField] public bool IsLifeSupport { get; private set; } = false;

        [Tooltip("Radius of life support coverage (only used when IsLifeSupport is true).")]
        [field: SerializeField] public float LifeSupportRadius { get; private set; } = 25f;

        public override object Clone()
        {
            BuildingSO copy = base.Clone() as BuildingSO;

            copy.SightConfig = SightConfig == null ? null : Instantiate(SightConfig);
            if (BuildingConfig != null)
            {
                copy.BuildingConfig = Instantiate(BuildingConfig);
            }

            return copy;
        }
    }
}