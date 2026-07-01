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

        protected override void Awake()
        {
            base.Awake();
            MaxHealth = 500;
            CurrentHealth = 500;
            Owner = Owner.Player1;
            _availableCommands = System.Array.Empty<BaseCommand>();
            _nextTick = Time.time + 0.1f;
        }

        // Update loop removed. GlobalDecayManager authoritatively governs decay and colony integrity.

        public override void Die() { base.Die(); }
        public override void Select() { }
        public override void Deselect() { }
    }
}
