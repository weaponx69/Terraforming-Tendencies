using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Movement Config", menuName = "Units/Movement Config", order = 8)]
    public class MovementConfigSO : ScriptableObject
    {
        [Tooltip("The movement speed of this unit on the NavMesh.")]
        [Range(0, 50)]
        [FormerlySerializedAs("<Speed>k__BackingField")]
        [SerializeField] private float speed = 5f;

        public float Speed => speed;
    }
}
