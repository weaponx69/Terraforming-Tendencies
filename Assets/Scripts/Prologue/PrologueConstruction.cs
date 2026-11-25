using System.Collections.Generic;
using System.Diagnostics;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameDevTV.RTS.Prologue
{
    /// <summary>
    /// Prologue card builds finish over their build weeks on the colony clock.
    /// Every building stays on the pad under scaffolding until the weeks are done.
    /// Farther hauls add weeks. Nothing rises out of the ground.
    /// </summary>
    public class PrologueConstruction : MonoBehaviour
    {
        static readonly List<PrologueConstruction> Active = new();

        BaseBuilding building;
        int weeks = 1;
        int baseWeeks = 1;
        bool haulSet;
        int tile = -1;
        bool hasSite;
        int localWeeks;
        bool firstSlice = true;
        float startFraction;
        float shown;
        bool finished;
        bool aquiferJob;
        bool aquiferProximityAtPlacement;
        bool aquiferProximityAtCompletion;
        bool hideMeshesUntilComplete;
        PrologueSite cachedSite;
        bool siteCached;
        struct SavedRenderer
        {
            public Renderer Renderer;
            public Material[] Original;
            public Vector3 LocalPosition;
        }

        readonly List<SavedRenderer> parts = new();
        readonly List<Material[]> faded = new();

        public static void NotifyWeekAdvanced()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var job = Active[i];
                if (job == null) continue;
                job.firstSlice = false;
                job.InvalidateSiteCache();
                if (!job.hasSite)
                    job.localWeeks++;
            }
        }

        public void Begin(Owner owner, int buildWeeks)
        {
            building = GetComponent<BaseBuilding>();
            ColonyGroundMesh.Apply(gameObject);
            baseWeeks = Mathf.Max(1, buildWeeks);
            weeks = baseWeeks;
            CacheParts();
            if (building != null)
            {
                building.Owner = owner;
                building.InitializeAsGhost(null, owner);
            }

            var ctrl = PrologueController.Instance;
            if (ctrl != null && ctrl.TryGetTileIndex(transform.position, out int index))
            {
                tile = index;
                hasSite = FindSite() != null;
            }

            startFraction = PrologueTimeLapse.Instance != null
                ? PrologueTimeLapse.Instance.WeekProgress01
                : 0f;

            aquiferJob = building != null && building.BuildingSO != null
                && building.BuildingSO.Name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (aquiferJob)
            {
                WaterIceAquiferMeshBuilder.Apply(gameObject);
                RunAquiferIceProximityAtPlacement();
            }
            ApplyHaul();
            ConstructionScaffold.Ensure(transform);

            hideMeshesUntilComplete = PrologueController.IsActive;
            if (hideMeshesUntilComplete)
                SetBuildingRenderersVisible(false);
            else
                Apply(0f);

            if (!Active.Contains(this))
                Active.Add(this);
            PrologueBuildingRegistry.Invalidate();
        }

        void OnDestroy()
        {
            Active.Remove(this);
            if (!finished)
                ClearBuildSite();
        }

        const float MinProgressTickInterval = 0.12f;
        static float nextProgressTickUnscaled;
        static int ticksPending;

        /// <summary>Queue at most one construction progress pass per frame.</summary>
        public static void RequestTimeLapseTick() => ticksPending = 1;

        /// <summary>Single tick from <see cref="PrologueTimeLapse"/> — not one Update per building.</summary>
        public static void TickAllFromTimeLapse()
        {
            if (ticksPending <= 0) return;

            var sw = Stopwatch.StartNew();
            int safetyCounter = 0;
            int iterations = 0;
            while (ticksPending > 0 && safetyCounter < 50)
            {
                if (Time.unscaledTime < nextProgressTickUnscaled)
                    break;

                ticksPending--;
                safetyCounter++;
                iterations++;
                nextProgressTickUnscaled = Time.unscaledTime + MinProgressTickInterval;

                for (int i = Active.Count - 1; i >= 0; i--)
                {
                    var job = Active[i];
                    if (job == null) continue;
                    job.TickProgress();
                }
            }

            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds > 2.0)
            {
                Debug.LogError(
                    $"[PrologueConstruction] TickAllFromTimeLapse exceeded 2ms: " +
                    $"{sw.Elapsed.TotalMilliseconds:F2}ms iterations={iterations} " +
                    $"ticksPending={ticksPending} activeJobs={Active.Count}");
            }
        }

        void TickProgress()
        {
            if (finished) return;
            float target = Target01();
            if (hideMeshesUntilComplete)
            {
                if (Ready(target))
                    Finish();
                return;
            }

            float step = Time.unscaledDeltaTime * 1.4f;
            if (Time.timeScale > 0.01f)
                step = Mathf.Max(step, Time.deltaTime / (weeks * PrologueTimeLapse.SecondsPerWeekNormal));
            shown = Mathf.MoveTowards(shown, target, step);
            Apply(shown);
            if (Ready(target))
                Finish();
        }

        float Target01()
        {
            float intra = Intra();
            if (hasSite)
            {
                var site = GetSite();
                if (site == null) return 1f;
                return Mathf.Clamp01((site.WeeksDone + intra) / Mathf.Max(1, site.WeeksTotal));
            }
            return Mathf.Clamp01((localWeeks + intra) / weeks);
        }

        float Intra()
        {
            float frac = PrologueTimeLapse.Instance != null
                ? PrologueTimeLapse.Instance.WeekProgress01
                : 0f;
            if (!firstSlice) return frac;
            float span = Mathf.Max(0.02f, 1f - startFraction);
            return Mathf.Clamp01((frac - startFraction) / span);
        }

        bool Ready(float target)
        {
            if (target < 0.995f) return false;
            if (hasSite) return GetSite() == null;
            return localWeeks >= weeks;
        }

        void InvalidateSiteCache() => siteCached = false;

        PrologueSite GetSite()
        {
            if (siteCached) return cachedSite;
            cachedSite = FindSite();
            siteCached = true;
            return cachedSite;
        }

        PrologueSite FindSite()
        {
            var colony = PrologueController.Instance?.Colony;
            if (colony == null || tile < 0) return null;
            foreach (var site in colony.Board.Sites)
            {
                if (site.Tile == tile) return site;
            }
            return null;
        }

        void RunAquiferIceProximityAtPlacement()
        {
            if (!aquiferJob || aquiferProximityAtPlacement || tile < 0) return;
            aquiferProximityAtPlacement = true;
            TouchAquiferIceRangeCheck();
        }

        void RunAquiferIceProximityAtCompletion()
        {
            if (!aquiferJob || aquiferProximityAtCompletion || tile < 0) return;
            aquiferProximityAtCompletion = true;
            TouchAquiferIceRangeCheck();
        }

        /// <summary>One board pass for ice range — only from placement / completion hooks.</summary>
        void TouchAquiferIceRangeCheck()
        {
            var colony = PrologueController.Instance?.Colony;
            if (colony == null) return;
            _ = colony.AquiferReachesIce(tile);
        }

        void CacheParts()
        {
            parts.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                string n = renderer.gameObject.name.ToLowerInvariant();
                if (n.Contains("label") || n.Contains("canvas") || n.Contains("vision") || n.Contains("selection"))
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }
                parts.Add(new SavedRenderer
                {
                    Renderer = renderer,
                    Original = renderer.sharedMaterials,
                    LocalPosition = renderer.transform.localPosition
                });
            }
        }

        void Apply(float progress)
        {
            EnsureFadedMaterials();
            float alpha = Mathf.Lerp(0.32f, 1f, progress);
            for (int p = 0; p < parts.Count; p++)
            {
                var part = parts[p];
                if (part.Renderer == null) continue;
                part.Renderer.transform.localPosition = part.LocalPosition;
                var mats = faded[p];
                for (int i = 0; i < mats.Length; i++)
                    SetAlpha(mats[i], alpha);
                part.Renderer.shadowCastingMode = progress > 0.85f
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void EnsureFadedMaterials()
        {
            if (faded.Count == parts.Count) return;
            faded.Clear();
            foreach (var part in parts)
            {
                var mats = new Material[part.Original.Length];
                for (int i = 0; i < part.Original.Length; i++)
                    mats[i] = Fade(part.Original[i], 0.32f);
                if (part.Renderer != null)
                    part.Renderer.materials = mats;
                faded.Add(mats);
            }
        }

        static void SetAlpha(Material mat, float alpha)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.GetColor("_Color");
                c.a = alpha;
                mat.SetColor("_Color", c);
            }
            if (alpha >= 0.98f)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1;
            }
        }

        static Material Fade(Material source, float alpha)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = source != null ? new Material(source) : (shader != null ? new Material(shader) : null);
            if (mat == null) return source;
            Color color = Color.white;
            if (mat.HasProperty("_BaseColor")) color = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color")) color = mat.color;
            color.a = alpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (alpha < 0.98f)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            return mat;
        }

        void ApplyHaul()
        {
            var colony = PrologueController.Instance?.Colony;
            if (colony == null || tile < 0) return;
            int want = Mathf.Max(1, baseWeeks + PrologueData.HaulWeeks(colony.SupplyDistance(tile)));
            want = colony.ApplyFasterBuild(want);
            if (hasSite)
            {
                var site = GetSite();
                if (site != null && want < site.WeeksTotal)
                    site.WeeksTotal = Mathf.Max(want, site.WeeksDone);
            }
            else if (!haulSet)
            {
                weeks = want;
                haulSet = true;
            }
            else if (want < weeks)
            {
                weeks = want;
            }
        }

        /// <summary>
        /// Stay on the pad the player chose. NavMesh grounding slides XZ by several meters.
        /// </summary>
        static void SeatWherePlaced(BaseBuilding building)
        {
            if (building == null) return;
            ReservedSiteBuildUtility.GroundBuilding(building);
        }

        void ClearBuildSite()
        {
            ConstructionScaffold.Clear(transform);
        }

        void SetBuildingRenderersVisible(bool on)
        {
            for (int p = 0; p < parts.Count; p++)
            {
                var part = parts[p];
                if (part.Renderer == null) continue;
                part.Renderer.enabled = on;
                if (on)
                {
                    part.Renderer.transform.localPosition = part.LocalPosition;
                    part.Renderer.sharedMaterials = part.Original;
                }
            }
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            Active.Remove(this);
            ApplyHaul();
            if (aquiferJob)
                RunAquiferIceProximityAtCompletion();
            ClearBuildSite();
            SetBuildingRenderersVisible(true);
            foreach (var part in parts)
            {
                if (part.Renderer == null) continue;
                part.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.enabled = true;
            }

            if (building != null)
            {
                building.enabled = true;
                // Keep the drill. Completion used to drop a blue sphere on top of it.
                if (aquiferJob)
                    WaterIceAquiferMeshBuilder.Apply(building.gameObject);
                building.CompleteConstruction();
                SeatWherePlaced(building);
                // Ensure PowerNode + pressurized tubes after final pad seat
                // (CompleteConstruction may have skipped AutoConnect if no node yet).
                building.NotifyProloguePowerLink();
                if (building.BuildingSO != null)
                    PrologueMineExtractor.TryAttachToBuilding(building, building.BuildingSO);
            }

            PrologueBuildingRegistry.Invalidate();
            if (building != null)
                PrologueRevealGhosts.OnPadBuilt(building.transform.position);
            Destroy(this);
        }
    }
}
