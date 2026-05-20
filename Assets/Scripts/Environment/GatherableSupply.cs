using System;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    [SelectionBase]
    public class GatherableSupply : MonoBehaviour, IGatherable, IHideable
{
        [field: SerializeField] public SupplySO Supply { get; set; }
        [field: SerializeField] public int Amount { get; set; }
        [field: SerializeField] public bool IsBusy { get; private set; }
        [field: SerializeField] public bool IsVisible { get; private set; }
        public Transform Transform => this == null ? null : transform;
        
        public static readonly System.Collections.Generic.List<GatherableSupply> ActiveSupplies = new();

        private void OnEnable()
        {
            if (!ActiveSupplies.Contains(this))
            {
                ActiveSupplies.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveSupplies.Remove(this);
        }

        private Placeholder culledVisuals;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();

        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        private void Start()
        {
            Amount = Supply != null ? Supply.MaxAmount : 100;
            Bus<SupplySpawnEvent>.Raise(Owner.Unowned, new SupplySpawnEvent(this));
        }

        private void OnDestroy()
        {
            Bus<SupplyDepletedEvent>.Raise(Owner.Unowned, new SupplyDepletedEvent(this));

            // --- FIX: Destroy the culledVisuals GameObject if it exists ---
            if (culledVisuals != null)
            {
                Destroy(culledVisuals.gameObject);
            }
        }

        public bool BeginGather()
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            return true;
        }

        public int EndGather()
        {
            IsBusy = false;
            int gatherRate = Supply != null ? Supply.AmountPerGather : 10;
            int amountGathered = Mathf.Min(gatherRate, Amount);
            Amount -= amountGathered;

            // --- FIX: Deplete TargetRock if this is a ghost ---
            if (TryGetComponent<GhostRock>(out var ghost) && ghost.TargetRock != null)
            {
                if (ghost.TargetRock.TryGetComponent<GatherableSupply>(out var original))
                {
                    original.Amount -= amountGathered;
                    if (original.Amount <= 0) Destroy(original.gameObject);
                }
            }

            if (Amount <= 0)
            {
                Destroy(gameObject);
            }

            return amountGathered;
        }

        public void AbortGather()
        {
            IsBusy = false;
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

        private void OnGainVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = true;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                particleSystem.gameObject.SetActive(true);
            }

            if (culledVisuals != null)
            {
                culledVisuals.gameObject.SetActive(false);
            }
        }

        private void OnLoseVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                particleSystem.gameObject.SetActive(false);
            }

            if (culledVisuals == null)
            {
                MeshRenderer mainRenderer = GetComponentInChildren<MeshRenderer>();
                if (mainRenderer == null) return; // Cannot create culled visuals without a MeshRenderer
                
                Transform originalRendererTransform = mainRenderer.transform;
GameObject culledGO = new ($"Culled {name} Visuals")
                {
                    layer = LayerMask.GetMask("TransparentFX"),
                    transform =
                    {
                        position = originalRendererTransform.position,
                        rotation = originalRendererTransform.rotation,
                        localScale = originalRendererTransform.localScale
                    }
                };
                culledVisuals = culledGO.AddComponent<Placeholder>();
                culledVisuals.ParentObject = gameObject;
                culledVisuals.Owner = Owner.Unowned;
                MeshFilter meshFilter = culledGO.AddComponent<MeshFilter>();
                meshFilter.mesh = mainRenderer.GetComponent<MeshFilter>().mesh;
                MeshRenderer renderer = culledGO.AddComponent<MeshRenderer>();
                renderer.materials = mainRenderer.materials;
            }
            else
            {
                culledVisuals.gameObject.SetActive(true);
            }
        }
    }
}