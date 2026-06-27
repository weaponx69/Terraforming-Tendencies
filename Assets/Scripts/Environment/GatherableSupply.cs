using System;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// A gatherable resource node on the planet surface.
    /// <para>
    /// Heavy logic (gather math, renderer loops, culled-visuals mesh construction,
    /// WorldToScreenPoint labels) is intentionally kept in C#.
    /// The VS-visible surface — <see cref="DepletionRatio"/>, <see cref="IsExhausted"/>,
    /// and the four <c>[Inspectable]</c> interaction methods — lets companion Flow Graphs
    /// react to proximity and depletion events without duplicating any formulas.
    /// </para>
    /// </summary>
    [IncludeInSettings(true)]
    [SelectionBase]
    public class GatherableSupply : MonoBehaviour, IGatherable, IHideable
    {
        // ── Inspector / VS-visible data properties ────────────────────────────

        /// <summary>The ScriptableObject defining this resource's type and parameters.</summary>
        [Inspectable]
        [field: SerializeField] public SupplySO Supply { get; set; }

        /// <summary>Current amount of resource remaining on this node.</summary>
        [Inspectable]
        [field: SerializeField] public int Amount { get; set; }

        /// <summary>True while a drone has an active gather lock on this node.</summary>
        [Inspectable]
        [field: SerializeField] public bool IsBusy { get; private set; }

        /// <summary>True when this node is within a unit's vision radius.</summary>
        [Inspectable]
        [field: SerializeField] public bool IsVisible { get; private set; }

        public Transform Transform => this == null ? null : transform;

        // ── VS-visible computed state ─────────────────────────────────────────

        /// <summary>
        /// Normalised depletion ratio [0, 1]. 1 = full, 0 = exhausted.
        /// Flow Graph nodes read this to branch on threshold crossings without
        /// repeating the Amount / MaxAmount division.
        /// </summary>
        [Inspectable]
        public float DepletionRatio =>
            (Supply != null && Supply.MaxAmount > 0)
                ? Mathf.Clamp01((float)Amount / Supply.MaxAmount)
                : 1f;

        /// <summary>
        /// True the frame the resource reaches zero (before <c>Destroy</c> is called).
        /// Exposed so a companion Flow Graph can fire a one-shot exhaustion event
        /// without polling <see cref="Amount"/> directly.
        /// </summary>
        [Inspectable]
        public bool IsExhausted => Amount <= 0;

        // ── Static registry ───────────────────────────────────────────────────
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
        private Collider[] colliders = Array.Empty<Collider>();

        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();
            colliders = GetComponentsInChildren<Collider>();
        }

        /// <summary>
        /// Enables or disables all colliders on this node.
        /// Callable from a Flow Graph when revealing/hiding the node.
        /// </summary>
        [Inspectable]
        public void ToggleColliders(bool enabled)
        {
            foreach (Collider c in colliders)
            {
                if (c != null) c.enabled = enabled;
            }
        }

        private Vector3 initialScale;

        private void Start()
        {
            initialScale = transform.localScale;
            Amount = Supply != null ? Supply.MaxAmount : 100;
            Bus<SupplySpawnEvent>.Raise(Owner.Unowned, new SupplySpawnEvent(this));

            if (TryGetComponent<HiddenResource>(out var hr) && !hr.IsDiscovered)
            {
                OnLoseVisibility();
            }
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

        /// <summary>
        /// Attempts to acquire a gather lock on this node.
        /// Returns <c>true</c> if the lock was granted; <c>false</c> if already busy.
        /// Callable from a Flow Graph to initiate a gather sequence.
        /// </summary>
        [Inspectable]
        public bool BeginGather()
        {
            if (IsBusy)
            {
                return false;
            }

            IsBusy = true;
            return true;
        }

        /// <summary>
        /// Releases the gather lock, deducts resources, and returns the amount gathered.
        /// Heavy math (Mathf.Min, parent ghost sync, scale ratio) stays in C#.
        /// Callable from a Flow Graph as the terminal node of a gather sequence.
        /// </summary>
        [Inspectable]
        public int EndGather()
        {
            IsBusy = false;
            int gatherRate = Supply != null ? Supply.AmountPerGather : 10;
            int amountGathered = Mathf.Min(gatherRate, Amount);
            Amount -= amountGathered;

            // --- FIX: Deplete parent if this is a ghost ---
            if (transform.parent != null && transform.parent.GetComponent<GatherableSupply>() is GatherableSupply original)
            {
                original.Amount -= amountGathered;
                if (original.Supply != null && original.Supply.MaxAmount > 0)
                {
                    float originalRatio = (float)original.Amount / original.Supply.MaxAmount;
                    original.transform.localScale = original.initialScale * Mathf.Clamp(originalRatio, 0.3f, 1f);
                }
                if (original.Amount <= 0) Destroy(original.gameObject);
            }

            if (Amount <= 0)
            {
                Destroy(gameObject);
            }
            else if (Supply != null && Supply.MaxAmount > 0)
            {
                float ratio = (float)Amount / Supply.MaxAmount;
                transform.localScale = initialScale * Mathf.Clamp(ratio, 0.3f, 1f);
            }

            return amountGathered;
        }

        /// <summary>
        /// Cancels an in-progress gather without deducting resources.
        /// Callable from a Flow Graph on unit death or command override.
        /// </summary>
        [Inspectable]
        public void AbortGather()
        {
            IsBusy = false;
        }

        /// <summary>
        /// Sets fog-of-war visibility on this node.
        /// Renderer toggling and culled-visuals mesh construction remain in C#;
        /// only the boolean intent is exposed to VS.
        /// </summary>
        [Inspectable]
        public void SetVisible(bool isVisible)
        {
            if (TryGetComponent<HiddenResource>(out var hr) && !hr.IsDiscovered)
            {
                isVisible = false;
            }

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
            ToggleColliders(true);
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

            if (TryGetComponent<HiddenResource>(out var hr) && !hr.IsDiscovered)
            {
                ToggleColliders(false);
                if (culledVisuals != null)
                {
                    culledVisuals.gameObject.SetActive(false);
                }
                return;
            }

            if (culledVisuals == null)
            {
                MeshRenderer mainRenderer = GetComponentInChildren<MeshRenderer>();
                if (mainRenderer == null) return; // Cannot create culled visuals without a MeshRenderer
                
                Transform originalRendererTransform = mainRenderer.transform;
                GameObject culledGO = new ($"Culled {name} Visuals")
                {
                    layer = LayerMask.NameToLayer("TransparentFX"),
                };
                culledGO.transform.SetParent(transform);
                culledGO.transform.position = originalRendererTransform.position;
                culledGO.transform.rotation = originalRendererTransform.rotation;
                culledGO.transform.localScale = originalRendererTransform.localScale;

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
        private void OnGUI()
        {
            if (Camera.main == null || Supply == null || !IsVisible) return;

            // Only show name if we're close to the camera
            float dist = Vector3.Distance(transform.position, Camera.main.transform.position);
            if (dist > 45f) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
            
            // If behind the camera, ignore
            if (screenPos.z < 0) return;

            GUIStyle style = new GUIStyle();
            style.fontSize = 16;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;

            // Draw a tiny black drop shadow for readability
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(screenPos.x - 50 + 1, Screen.height - screenPos.y - 20 + 1, 100, 40), Supply.name, style);
            
            style.normal.textColor = Color.cyan;
            GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 20, 100, 40), Supply.name, style);
        }

    }
}