using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// An invisible, decaying starter commandable that gives the player a time window
    /// to place their first real building at the start of the game.
    /// Auto-spawns at startup, decays naturally via GlobalDecayManager
    /// (has no LifeSupportNode). Self-destructs at 0 HP, at which point the
    /// player must already have their own structures or the game will end.
    /// </summary>
    public class DecayStarter : AbstractCommandable
    {
        [SerializeField] private int maxHealth = 500;
        [SerializeField] private int health = 500;

        private bool _hasStarted;

        // Auto-spawn at game start without modifying PlanetGenerator
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SpawnOnStart()
        {
            GameObject go = new GameObject("DecayStarter", typeof(DecayStarter));
            go.transform.position = new Vector3(-1000f, -1000f, -1000f);
            go.transform.localScale = Vector3.zero;
            Object.DontDestroyOnLoad(go);
        }

        protected override void Awake()
        {
            // Set health before base.Awake() so InitializeIfNeeded sees it
            MaxHealth = maxHealth;
            CurrentHealth = health;

            // Null out references we don't want to use
            VisionTransform = null;
            selectionIndicator = null;
            _availableCommands = System.Array.Empty<BaseCommand>();

            // Skip base.Awake() — no UnitSO, no renderer/particle discovery needed
        }

        protected override void Start()
        {
            // Intentionally skip base.Start() — no curved world shader,
            // no vision cone, no upgrade event bus subscription needed.
            _hasStarted = true;
        }

        public void Die()
        {
            if (gameObject != null)
                Destroy(gameObject);
        }

        protected override void OnDestroy()
        {
            // Intentionally skip base.OnDestroy() — no bus subscription to clean up.
        }

        private void Update()
        {
            // Self-destruct when HP reaches 0
            if (_hasStarted && CurrentHealth <= 0)
            {
                Destroy(gameObject);
            }
        }

        public override void Select()
        {
            // Cannot be selected
        }

        public override void Deselect()
        {
            // Cannot be selected
        }
    }
}
