using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;

namespace GameDevTV.RTS.Environment
{
    public class DecayStarter : AbstractCommandable
    {
        private float _nextTick;

        public static void EnsureSpawned()
        {
            if (GameObject.Find("DecayStarter") != null) return;
            var go = new GameObject("DecayStarter", typeof(DecayStarter));
            go.AddComponent<LifeSupportNode>().Radius = 0.001f;
            go.transform.position = new Vector3(-1000f, -1000f, -1000f);
            go.transform.localScale = Vector3.zero;
        }

        private void Awake()
        {
            MaxHealth = 500;
            CurrentHealth = 500;
            Owner = Owner.Player1;
            _availableCommands = System.Array.Empty<BaseCommand>();
            _nextTick = Time.time + 0.1f;
        }

private void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 0.1f;

            TakeDamage(5);
            float ratio = (float)CurrentHealth / MaxHealth;
            GameDevTV.RTS.Player.Supplies.UpdateIntegrity(Owner, ratio * 100f);

            Debug.Log($"[DecayStarter] Tick t={Time.time:F2}s | HP: {CurrentHealth}/{MaxHealth} | Integrity→ {ratio * 100f:F1}% | Owner: {Owner}");

            if (CurrentHealth <= 0)
                Destroy(gameObject);
        }

        public void Die() { if (gameObject != null) Destroy(gameObject); }
        public override void Select() { }
        public override void Deselect() { }
    }
}
