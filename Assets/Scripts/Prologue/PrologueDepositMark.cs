using UnityEngine;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Click target on a deposit "?". Survey paint uses <see cref="GroundPoint"/>
    /// so a hill in front of the mark does not steal the tile.
    /// </summary>
    public class PrologueDepositMark : MonoBehaviour
    {
        public const string ClickChildName = "DepositClick";

        public Vector3 GroundPoint;

        void OnEnable() => PrologueInteractableIndex.Register(this);
        void OnDisable() => PrologueInteractableIndex.Unregister(this);

        /// <summary>Tall capsule from the deposit ground to the "?" so clicks are not lost to terrain.</summary>
        public static void EnsureClickVolume(Transform markRoot, float floatHeight)
        {
            if (markRoot == null) return;
            Transform click = markRoot.Find(ClickChildName);
            if (click == null)
            {
                var go = new GameObject(ClickChildName);
                go.layer = markRoot.gameObject.layer;
                click = go.transform;
                click.SetParent(markRoot, false);
            }

            foreach (var old in markRoot.GetComponents<SphereCollider>())
                Object.Destroy(old);

            float topLocal = 5f;
            float bottomLocal = -floatHeight;
            float column = topLocal - bottomLocal;
            if (!click.TryGetComponent<CapsuleCollider>(out var cap))
                cap = click.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = 1.5f;
            cap.height = column;
            cap.center = new Vector3(0f, (topLocal + bottomLocal) * 0.5f, 0f);
            cap.isTrigger = false;
        }
    }
}