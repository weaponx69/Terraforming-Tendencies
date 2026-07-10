using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Represents a physical pressurized tube connection between two buildings.
    /// Inherits from AbstractCommandable so it has health, can take damage, 
    /// and can be repaired automatically by worker drones.
    /// </summary>
    public class PressurizedTube : AbstractCommandable
    {
        [Header("Tubes Info")]
        public PowerNode NodeA;
        public PowerNode NodeB;

        private LineRenderer lr;
        private CapsuleCollider col;
        private Color baseColor;

        public void Initialize(PowerNode a, PowerNode b, LineRenderer line)
        {
            NodeA = a;
            NodeB = b;
            lr = line;

            bool solid = Player.BlueprintDraftManager.TubesAreSolid;
            MaxHealth = solid ? 200 : 50;
            CurrentHealth = MaxHealth;
            Owner = Owner.Player1;

            if (lr != null && lr.material != null)
            {
                baseColor = lr.material.color;
            }
            else
            {
                baseColor = solid ? new Color(0.75f, 0.75f, 0.75f, 1f) : new Color(0.6f, 0.85f, 0.95f, 0.5f);
            }

            SetupCollider();
        }

        private void SetupCollider()
        {
            if (NodeA == null || NodeB == null) return;

            // Place collider at the midpoint between NodeA and NodeB
            Vector3 posA = NodeA.transform.position;
            Vector3 posB = NodeB.transform.position;
            Vector3 midPoint = (posA + posB) / 2f + Vector3.up * 0.15f;

            // Create collider component
            col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.6f;
            col.height = Vector3.Distance(posA, posB);
            col.direction = 2; // Z-axis direction

            // Align collider rotation and position
            transform.position = midPoint;
            transform.LookAt(posB);

            // Set layer to damageable/interactable
            int targetLayer = LayerMask.NameToLayer("Interactable");
            if (targetLayer == -1) targetLayer = LayerMask.NameToLayer("Default");
            gameObject.layer = targetLayer;
        }

        public void DamageTube(int amount)
        {
            base.TakeDamage(amount);
            UpdateVisualColor();
        }

        public override void Die()
        {
            Debug.Log($"[PressurizedTube] Ruptured and destroyed between {NodeA?.gameObject.name} and {NodeB?.gameObject.name}");
            if (NodeA != null && NodeB != null)
            {
                NodeA.DisconnectFrom(NodeB);
            }
            base.Die();
        }

        private void UpdateVisualColor()
        {
            if (lr == null || lr.material == null) return;
            float healthRatio = (float)CurrentHealth / MaxHealth;
            
            // Interpolate color to red when damaged
            lr.material.color = Color.Lerp(new Color(1f, 0f, 0f, 0.8f), baseColor, healthRatio);
        }
    }
}
