using System;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public class GatherableSupply : MonoBehaviour, IGatherable, IHideable
    {
        [field: SerializeField] public SupplySO Supply { get; private set; }
        [field: SerializeField] public int Amount { get; private set; }
        [field: SerializeField] public bool IsBusy { get; private set; }
        [field: SerializeField] public bool IsVisible { get; private set; }
        public Transform Transform => this == null ? null : transform;
        
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
            Amount = Supply.MaxAmount;
            Bus<SupplySpawnEvent>.Raise(Owner.Unowned, new SupplySpawnEvent(this));
        }

        private void OnDestroy()
        {
            Bus<SupplyDepletedEvent>.Raise(Owner.Unowned, new SupplyDepletedEvent(this));
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
            int amountGathered = Mathf.Min(Supply.AmountPerGather, Amount);
            Amount -= amountGathered;

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