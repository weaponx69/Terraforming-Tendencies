using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Building", menuName = "Buildings/Building")]
    public class BuildingSO : AbstractUnitSO
    {
        [field: SerializeField] public Material PlacementMaterial { get; private set; }

        public override object Clone()
        {
            BuildingSO copy = base.Clone() as BuildingSO;

            copy.SightConfig = SightConfig == null ? null : Instantiate(SightConfig);

            return copy;
        }
    }
}