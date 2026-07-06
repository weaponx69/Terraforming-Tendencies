using System;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    public abstract class AbstractCommandable : MonoBehaviour, ISelectable, IDamageable, IHideable
    {
        public static readonly System.Collections.Generic.List<AbstractCommandable> ActiveCommandables = new();

        protected virtual void OnEnable()
        {
            if (!ActiveCommandables.Contains(this)) ActiveCommandables.Add(this);
        }

        protected virtual void OnDisable()
        {
            ActiveCommandables.Remove(this);
        }

        [field: SerializeField] public bool IsSelected { get; protected set; }
        [field: SerializeField] public int CurrentHealth { get; protected set; }
        [field: SerializeField] public int MaxHealth { get; protected set; }
        [field: SerializeField] public Owner Owner { get; set; }
        [field: SerializeField] public bool IsVisible { get; private set; } = true;
        public Transform Transform => this == null ? null : transform;
        [UnityEngine.Serialization.FormerlySerializedAs("<AvailableCommands>k__BackingField")]
        [SerializeField] protected BaseCommand[] _availableCommands;
        protected BaseCommand[] overrideCommands;
        public virtual BaseCommand[] AvailableCommands
        {
            get => overrideCommands ?? _availableCommands;
            protected set => _availableCommands = value;
        }
        [field: SerializeField] public AbstractUnitSO UnitSO { get; private set; }
        [SerializeField] protected GameObject selectionIndicator;
        [SerializeField] protected Transform VisionTransform;

        public delegate void HealthUpdatedEvent(AbstractCommandable commandable, int lastHealth, int newHealth);
        public event HealthUpdatedEvent OnHealthUpdated;
        
        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private BaseCommand[] initialCommands;
        private bool isAbstractInitialized = false;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();

        public virtual void InitializeIfNeeded()
        {
            if (isAbstractInitialized) return;
            
            if (UnitSO != null)
            {
                UnitSO = UnitSO.Clone() as AbstractUnitSO;
            }

            renderers = GetComponentsInChildren<Renderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();

            initialCommands = AvailableCommands;
            isAbstractInitialized = true;
        }

        protected virtual void Awake()
        {
            InitializeIfNeeded();
        }

        protected virtual void Start()
        {
            if (GameDevTV.RTS.Environment.PlanetGenerator.Instance != null)
            {
                GameDevTV.RTS.Environment.PlanetGenerator.Instance.ApplyCurvedWorldShader(gameObject);
            }

            if (UnitSO != null && UnitSO.SightConfig != null && VisionTransform != null)
            {
                float size = UnitSO.SightConfig.SightRadius * 2;
                VisionTransform.localScale = new Vector3(size, size, size);
                
                // AI units now also reveal the fog
                bool isAI = Owner >= Owner.AI1 && Owner <= Owner.AI7;
                VisionTransform.gameObject.SetActive(Owner == Owner.Player1 || isAI);
            }

            Bus<UpgradeResearchedEvent>.OnEvent[Owner] += HandleUpgradeResearched;
        }

        protected virtual void OnDestroy()
        {
            Bus<UpgradeResearchedEvent>.OnEvent[Owner] -= HandleUpgradeResearched;
        }

        public virtual void Select()
        {
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(true);
            }

            IsSelected = true;
            Bus<UnitSelectedEvent>.Raise(Owner, new UnitSelectedEvent(this));
        }

        public virtual void Deselect()
        {
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
            }

            IsSelected = false;
            SetCommandOverrides(null);

            Bus<UnitDeselectedEvent>.Raise(Owner, new UnitDeselectedEvent(this));
        }

        public void SetCommandOverrides(BaseCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
            {
                overrideCommands = null;
            }
            else
            {
                overrideCommands = commands;
            }

            if (IsSelected)
            {
                Bus<UnitSelectedEvent>.Raise(Owner, new UnitSelectedEvent(this));
            }
        }

        public void TakeDamage(int damage)
        {
            if (this is GlobalCommander) return; // The Universal Command Center (GlobalCommander) is completely invulnerable

            int lastHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, CurrentHealth);

            // Spawn floating damage number above the commandable
            if (damage > 0)
            {
                Vector3 spawnPos = transform.position + Vector3.up * (GetComponent<Collider>() != null
                    ? GetComponent<Collider>().bounds.extents.y + 0.5f
                    : 2f);
                GameDevTV.RTS.UI.Components.DamageNumberUI.Spawn(spawnPos, damage);
            }

            OnHealthUpdated?.Invoke(this, lastHealth, CurrentHealth);
            if (CurrentHealth == 0)
            {
                Die();
            }
        }

        public virtual void Die()
        {
            Debug.Log($"[AbstractCommandable] {gameObject.name} (Owner: {Owner}) is DYING at {transform.position}.");
            Destroy(gameObject);
        }

        public void Heal(int amount)
        {
            int lastHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, MaxHealth);
            OnHealthUpdated?.Invoke(this, lastHealth, CurrentHealth);
        }

        public void SetVisible(bool isVisible)
        {
            if (isVisible == IsVisible) return;

            IsVisible = isVisible;
            OnVisibilityChanged?.Invoke(this, isVisible);

            if (IsVisible)
            {
                OnGainVisibility();
            }
            else
            {
                OnLoseVisibility();
            }
        }

        protected virtual void OnGainVisibility()
        {
            foreach(Renderer renderer in renderers)
            {
                renderer.enabled = true;
            }

            foreach(ParticleSystem particleSystem in particleSystems)
            {
                particleSystem.gameObject.SetActive(true);
            }
        }

        protected virtual void OnLoseVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                particleSystem.gameObject.SetActive(false);
            }
        }

        private void HandleUpgradeResearched(UpgradeResearchedEvent evt)
        {
            if (UnitSO == null || evt.Upgrade == null) return;
            if (evt.Owner == Owner && UnitSO.Upgrades.Contains(evt.Upgrade))
            {
                int oldHealth = UnitSO.Health;
                
                evt.Upgrade.Apply(UnitSO);
                
                // If health was upgraded, adjust MaxHealth and heal by the difference
                if (UnitSO.Health != oldHealth)
                {
                    int diff = UnitSO.Health - oldHealth;
                    MaxHealth = UnitSO.Health;
                    Heal(diff);
                }

                // If sight was upgraded, adjust VisionTransform scale
                if (UnitSO.SightConfig != null && VisionTransform != null)
                {
                    float size = UnitSO.SightConfig.SightRadius * 2;
                    VisionTransform.localScale = new Vector3(size, size, size);
                }

                // If life support radius was upgraded, adjust the active LifeSupportNode component's radius
                if (UnitSO is BuildingSO buildingSO && TryGetComponent<GameDevTV.RTS.Environment.LifeSupportNode>(out var node))
                {
                    bool isCommandPost = buildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                    float targetRadius = buildingSO.LifeSupportRadius;
                    if (buildingSO.BuildingConfig != null && buildingSO.BuildingConfig.LifeSupportRadius != 0)
                    {
                        targetRadius = buildingSO.BuildingConfig.LifeSupportRadius;
                    }
                    node.Radius = isCommandPost ? Mathf.Max(targetRadius, 30f) : targetRadius;
                }

                if (this is AbstractUnit unit && UnitSO.MovementConfig != null && unit.Agent != null)
                {
                    unit.Agent.speed = UnitSO.MovementConfig.Speed;
                }
            }
        }
    }
}
