using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.UI.Containers;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Manages the player's deck and hand. Cards are drawn first-in-first-out from
    /// the draw pile; played or skipped cards go to the back of the discard queue.
    /// When the draw pile empties, discard recycles in the same order (no re-shuffle).
    ///
    /// Auto-spawns on scene load — no manual scene setup needed.
    /// </summary>
    public class CardDeckController : MonoBehaviour
    {
        public static CardDeckController Instance { get; private set; }

        [Header("Deck Configuration")]
        [SerializeField] private List<BlueprintCardSO> masterDeck = new();
        public List<BlueprintCardSO> MasterDeck => masterDeck;
        [SerializeField] private int handSize = 24;

        private List<BlueprintCardSO> drawPile = new();
        private List<BlueprintCardSO> discardPile = new();
        private List<BlueprintCardSO> hand = new();
        /// <summary>Old RTS building production — offered into hand after the next week spend.</summary>
        private readonly Queue<BlueprintCardSO> pendingProductionOffers = new();
        /// <summary>Climate combo offers already granted this Act (TEMPERATURE / ATMOSPHERE / WATER).</summary>
        private readonly HashSet<string> climateComboOffersThisAct = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The player's current hand of cards (max handSize).</summary>
        public IReadOnlyList<BlueprintCardSO> Hand => hand;

        /// <summary>Fired when the hand changes (card played, drawn, etc.).</summary>
        public static event Action OnHandChanged;

        /// <summary>Fired when the draft phase begins. Carries the offered hand.</summary>
#pragma warning disable CS0067 // Draft rounds removed; DraftingUI still subscribes for compatibility.
        public static event Action<List<BlueprintCardSO>> OnDraftStarted;
#pragma warning restore CS0067

        // ── Auto-initialization ──────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            GameObject go = new GameObject("CardDeckController");
            DontDestroyOnLoad(go);
            go.AddComponent<CardDeckController>();
        }

        private void Awake()
        {
            Instance = this;
            // Scrollable hand — large hand since the strip scrolls horizontally.
            handSize = 24;
        }

        private void OnEnable()
        {
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += HandleBuildingSpawned;
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] += HandleBuildingDied;
            Supplies.OnMaterialsChanged += HandleSupplyGateChanged;
            Supplies.OnEnergyChanged += HandleSupplyGateChanged;
            Supplies.OnTemperatureChanged += HandleSupplyGateChanged;
            Supplies.OnAtmosphereChanged += HandleSupplyGateChanged;
            Supplies.OnWaterChanged += HandleSupplyGateChanged;
            Supplies.OnOxygenChanged += HandleSupplyGateChanged;
            Supplies.OnBiomassChanged += HandleSupplyGateChanged;
            Supplies.OnPowerChanged += HandleSupplyGateChanged;
            Supplies.OnPopulationChanged += HandleSupplyGateChanged;
            SectorManager.OnSectorUnlocked += HandleSectorUnlocked;
            SectorManager.OnSectorExplored += HandleSectorExplored;
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            GenerationManager.OnGenerationStarted += HandleGenerationStarted;
            GenerationManager.OnGenerationEnded += HandleGenerationEnded;
            DiscoverySystem.OnDiscoveryChanged += HandleDiscoveryChanged;
        }

        private void OnDisable()
        {
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] -= HandleBuildingSpawned;
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] -= HandleBuildingDied;
            Supplies.OnMaterialsChanged -= HandleSupplyGateChanged;
            Supplies.OnEnergyChanged -= HandleSupplyGateChanged;
            Supplies.OnTemperatureChanged -= HandleSupplyGateChanged;
            Supplies.OnAtmosphereChanged -= HandleSupplyGateChanged;
            Supplies.OnWaterChanged -= HandleSupplyGateChanged;
            Supplies.OnOxygenChanged -= HandleSupplyGateChanged;
            Supplies.OnBiomassChanged -= HandleSupplyGateChanged;
            Supplies.OnPowerChanged -= HandleSupplyGateChanged;
            Supplies.OnPopulationChanged -= HandleSupplyGateChanged;
            SectorManager.OnSectorUnlocked -= HandleSectorUnlocked;
            SectorManager.OnSectorExplored -= HandleSectorExplored;
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            GenerationManager.OnGenerationStarted -= HandleGenerationStarted;
            GenerationManager.OnGenerationEnded -= HandleGenerationEnded;
            DiscoverySystem.OnDiscoveryChanged -= HandleDiscoveryChanged;
        }

        private void HandleBuildingSpawned(BuildingSpawnEvent _) => RefreshHand();
        private void HandleBuildingDied(BuildingDeathEvent _) => RefreshHand();
        private void HandleSupplyGateChanged(Owner owner, int _)
        {
            if (owner == Owner.Player1) RefreshHand();
        }
        private void HandleSupplyGateChanged(Owner owner, float _)
        {
            if (owner == Owner.Player1) RefreshHand();
        }
        private void HandleGenerationStarted(int _, int __) => RefreshHand();
        private void HandleGenerationEnded(int _, int __) => RefreshHand();

        private void HandlePlanetGenerated()
        {
            // Pads now exist — pull bootstrap cards back if an early purge dumped them.
            EnsureBootstrapUnlockInHand("Command Post");
            EnsureBootstrapUnlockInHand("Solar");
            RefreshHand();
            OnHandChanged?.Invoke();
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
        }

        private void HandleSectorUnlocked()
        {
            EnsureBootstrapUnlockInHand("Solar");
            // Fallback only — SectorManager auto-places a CP before this event fires.
            if (GameDevTV.RTS.Utilities.SectorColonization.HasUnclaimedUnlockedSector())
                EnsureBootstrapUnlockInHand("Command Post");
            RefreshHand();
        }

        private void HandleSectorExplored(int sectorIndex)
        {
            var sm = SectorManager.Instance;
            if (sm != null && sectorIndex >= 0 && sectorIndex < sm.Sectors.Count)
                DiscoverySystem.RevealFeaturesForSector(sm.Sectors[sectorIndex]);
            RefreshHand();
        }

        private void HandleDiscoveryChanged() => RefreshHand();

        /// <summary>
        /// After auto-colonizing a sector and the player acknowledges the handoff UI,
        /// seat Solar + unmet climate/primary tools so the new Command Post is immediately playable.
        /// Exception to FIFO only at this handoff moment.
        /// </summary>
        public void PrepareHandForColonizedSector()
        {
            DiscardUnplayableFromHand();

            EnsureBootstrapUnlockInHand("Solar");

            // Construction requires drones — keep a Mining Drone card seated after colonization.
            EnsureMiningDroneInHand();

            if (GenerationManager.Instance == null || !GenerationManager.Instance.IsExpansionPhase)
            {
                EnsureUnmetSectorGoalCardInHand("TEMPERATURE");
                EnsureUnmetSectorGoalCardInHand("ATMOSPHERE");
                EnsureUnmetSectorGoalCardInHand("WATER");

                if (GenerationManager.Instance != null)
                {
                    string primary = TerraformingGoalColors.GoalKeyForMilestone(
                        GenerationManager.Instance.CurrentMilestoneType);
                    if (!string.IsNullOrEmpty(primary)
                        && primary != "TEMPERATURE"
                        && primary != "ATMOSPHERE"
                        && primary != "WATER")
                    {
                        EnsureUnmetSectorGoalCardInHand(primary);
                    }
                }
            }

            EnsureSolarPrereqInHand(); // keeps a power generator seated (Solar / Geothermal / …)
            FillHandInternal();
            EnsureSolarPrereqInHand(); // keeps a power generator seated (Solar / Geothermal / …)
            EnsureMiningDroneInHand();
            EnsureMvpClimateGoalsInHand();
            TrimHandToSize();

            OnHandChanged?.Invoke();
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
            Debug.Log($"[CardDeckController] Prepared colonized-sector hand ({hand.Count} cards).");
        }

        private void EnsureUnmetSectorGoalCardInHand(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return;
            if (!GenerationManager.IsUnmetSectorGoal(goal)) return;

            if (hand.Any(c =>
                    string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase)))
                return;

            BlueprintCardSO found = FindCardInPiles(c =>
                string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase)
                && ShouldKeepInHand(c));

            // Fallback: any unlock whose building classifies to this goal (gate already relaxed for MVP).
            if (found == null)
            {
                found = FindCardInPiles(c =>
                    c is UnlockBuildingCardSO unlock
                    && unlock.buildingToUnlock != null
                    && string.Equals(
                        UnlockBuildingCardSO.ClassifyBuildingGoal(unlock.buildingToUnlock),
                        goal,
                        StringComparison.OrdinalIgnoreCase)
                    && ShouldKeepInHand(c));
            }

            if (found == null) return;

            MakeHandRoomForClimateGoal(goal, found);
            if (hand.Count >= handSize) return;

            hand.Add(found);
            Debug.Log($"[CardDeckController] Seated unmet climate card '{found.cardName}' ({goal}).");
        }

        /// <summary>
        /// Make room for a missing climate color. Prefer dropping support cards, then a
        /// duplicate of another climate color — never Mining Drone / Solar / the only copy of a color.
        /// </summary>
        private void MakeHandRoomForClimateGoal(string incomingGoal, BlueprintCardSO incoming)
        {
            if (hand.Count < handSize || incoming == null) return;

            int dropIdx = hand.FindIndex(c =>
                c != null
                && TerraformingGoalColors.GetSectorGoalForCard(c) == null
                && !IsSolarUnlockCard(c)
                && !IsMiningDroneCard(c));

            if (dropIdx < 0)
            {
                // Drop a second copy of Temp/Atmos/Water that is not the only one of its color.
                for (int i = 0; i < hand.Count; i++)
                {
                    string g = TerraformingGoalColors.GetSectorGoalForCard(hand[i]);
                    if (string.IsNullOrEmpty(g)) continue;
                    if (string.Equals(g, incomingGoal, StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsSolarUnlockCard(hand[i]) || IsMiningDroneCard(hand[i])) continue;
                    int copies = hand.Count(c =>
                        string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), g, StringComparison.OrdinalIgnoreCase));
                    if (copies >= 2)
                    {
                        dropIdx = i;
                        break;
                    }
                }
            }

            if (dropIdx < 0)
            {
                dropIdx = hand.FindIndex(c =>
                    c is UnlockBuildingCardSO unlock
                    && !IsSolarUnlockCard(c)
                    && !IsMiningDroneCard(c)
                    && NeedsSolarPoweredPad(unlock));
            }

            if (dropIdx < 0) return;

            discardPile.Add(hand[dropIdx]);
            hand.RemoveAt(dropIdx);
        }

        private void EnsureNamedCardInHand(Func<BlueprintCardSO, bool> predicate, bool requirePlayable)
        {
            if (hand.Any(predicate)) return;

            BlueprintCardSO found = FindCardInPiles(c =>
                predicate(c) && (!requirePlayable || IsPlayableNow(c)));
            if (found == null) return;

            MakeHandRoomForHandoffCard(found);
            if (hand.Count >= handSize) return;

            hand.Add(found);
            Debug.Log($"[CardDeckController] Seated handoff card '{found.cardName}'.");
        }

        private BlueprintCardSO FindCardInPiles(Func<BlueprintCardSO, bool> predicate)
        {
            BlueprintCardSO found = drawPile.FirstOrDefault(predicate);
            if (found != null)
            {
                drawPile.Remove(found);
                return found;
            }

            found = discardPile.FirstOrDefault(predicate);
            if (found != null)
            {
                discardPile.Remove(found);
                return found;
            }

            return null;
        }

        private void MakeHandRoomForHandoffCard(BlueprintCardSO incoming)
        {
            if (hand.Count < handSize || incoming == null) return;

            // Prefer dropping support cards / pad-blocked unlocks that are not sector goals.
            // Never drop Mining Drone — builds require a drone after the first Command Post.
            int dropIdx = hand.FindIndex(c =>
                c != null
                && TerraformingGoalColors.GetSectorGoalForCard(c) == null
                && !IsSolarUnlockCard(c)
                && !IsMiningDroneCard(c));
            if (dropIdx < 0)
            {
                dropIdx = hand.FindIndex(c =>
                    c is UnlockBuildingCardSO unlock
                    && !IsSolarUnlockCard(c)
                    && NeedsSolarPoweredPad(unlock));
            }

            if (dropIdx < 0) return;

            discardPile.Add(hand[dropIdx]);
            hand.RemoveAt(dropIdx);
        }

        /// <summary>
        /// After the between-Act shop, force Solar + Command Post into hand for the next sector
        /// (does not require CanApply — pads may not exist in the new sector yet).
        /// </summary>
        public void GrantSectorTransitionBootstrap()
        {
            ForceBootstrapUnlockIntoHand("Command Post");
            ForceBootstrapUnlockIntoHand("Solar");
            RefreshHand();
            OnHandChanged?.Invoke();
            Debug.Log("[CardDeckController] Granted Solar + Command Post for next sector Act.");
        }

        /// <summary>Add a purchased shop card into the hand (makes room if needed).</summary>
        public void AddCardToHandFromShop(BlueprintCardSO card)
        {
            if (card == null) return;
            if (hand.Contains(card))
            {
                // Already holding this instance — clone a playable copy for the purchase.
                card = CloneCardInstance(card);
            }
            else
            {
                drawPile.Remove(card);
                discardPile.Remove(card);
            }

            if (hand.Count >= handSize)
                MakeHandRoomForHandoffCard(card);
            if (hand.Count >= handSize)
            {
                // Still full — put purchase at front of draw so next fill gets it.
                drawPile.Insert(0, card);
                OnHandChanged?.Invoke();
                return;
            }

            hand.Insert(0, card);
            TrimHandToSize();
            OnHandChanged?.Invoke();
            Debug.Log($"[CardDeckController] Shop purchased '{card.cardName}' into hand.");
        }

        private void ForceBootstrapUnlockIntoHand(string nameContains)
        {
            if (hand.Any(c => c is UnlockBuildingCardSO u &&
                              u.buildingToUnlock != null &&
                              u.buildingToUnlock.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            BlueprintCardSO found = drawPile.FirstOrDefault(c =>
                c is UnlockBuildingCardSO u &&
                u.buildingToUnlock != null &&
                u.buildingToUnlock.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                drawPile.Remove(found);
            }
            else
            {
                found = discardPile.FirstOrDefault(c =>
                    c is UnlockBuildingCardSO u &&
                    u.buildingToUnlock != null &&
                    u.buildingToUnlock.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
                if (found != null) discardPile.Remove(found);
            }

            if (found == null)
            {
                found = masterDeck.FirstOrDefault(c =>
                    c is UnlockBuildingCardSO u &&
                    u.buildingToUnlock != null &&
                    u.buildingToUnlock.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    found = CloneCardInstance(found);
            }

            if (found == null) return;

            if (hand.Count >= handSize)
                MakeHandRoomForHandoffCard(found);
            if (hand.Count >= handSize) return;

            hand.Insert(0, found);
            TrimHandToSize();
        }

        private static BlueprintCardSO CloneCardInstance(BlueprintCardSO source)
        {
            if (source == null) return null;
            if (source is UnlockBuildingCardSO unlock)
            {
                var created = ScriptableObject.CreateInstance<UnlockBuildingCardSO>();
                created.cardName = unlock.cardName;
                created.cardDescription = unlock.cardDescription;
                created.buildingToUnlock = unlock.buildingToUnlock;
                created.icon = unlock.icon;
                created.name = $"{unlock.name}_ShopClone";
                return created;
            }

            return source;
        }

        private void EnsureBootstrapUnlockInHand(string nameContains)
        {
            if (hand.Any(c => c is UnlockBuildingCardSO u &&
                              u.buildingToUnlock != null &&
                              u.buildingToUnlock.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            BlueprintCardSO found = drawPile.FirstOrDefault(c =>
                c is UnlockBuildingCardSO u &&
                u.buildingToUnlock != null &&
                u.buildingToUnlock.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                drawPile.Remove(found);
            }
            else
            {
                found = discardPile.FirstOrDefault(c =>
                    c is UnlockBuildingCardSO u &&
                    u.buildingToUnlock != null &&
                    u.buildingToUnlock.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
                if (found != null) discardPile.Remove(found);
            }

            if (found == null || !found.IsGateMet()) return;
            // Only re-seat if a pad is actually available (otherwise CanApply is still false).
            if (!found.CanApply()) return;

            if (hand.Count >= handSize)
            {
                MakeHandRoomForHandoffCard(found);
            }
            if (hand.Count >= handSize) return;

            hand.Add(found);
            TrimHandToSize();
            Debug.Log($"[CardDeckController] Restored bootstrap card '{found.cardName}' after planet gen.");
        }

        /// <summary>Never allow more than handSize cards — never drop the last Solar.</summary>
        private void TrimHandToSize()
        {
            while (hand.Count > handSize)
            {
                int dropIdx = -1;
                for (int i = hand.Count - 1; i >= 0; i--)
                {
                    if (IsSolarUnlockCard(hand[i]) || IsMiningDroneCard(hand[i])) continue;
                    dropIdx = i;
                    break;
                }
                if (dropIdx < 0) break;
                discardPile.Add(hand[dropIdx]);
                hand.RemoveAt(dropIdx);
            }
        }


        // Don't fill the hand in Start() — BlueprintDraftUI may not have
        // populated the deck yet. Instead, RebuildDeck() is called by
        // BlueprintDraftUI after it finishes InitializeDefaultPool().
        private void Start() { }

        /// <summary>
        /// Called by BlueprintDraftUI after the deck is populated.
        /// 1. Clears the hand.
        /// 2. Direct-adds the three guaranteed starter cards (Command Post, Solar Panel,
        ///    Mining Drone) using hand.Add() so they are seated at indices 0, 1, 2.
        /// 3. Fills the remaining slots from the front of the draw queue.
        /// No shifting or eviction occurs.
        /// </summary>
        public void RebuildDeck()
        {
            // Drop RTS combat / non-colony clutter that may still be in the master list.
            masterDeck.RemoveAll(IsExcludedFromColonyDeck);

            InitializeDrawPile();

            // 1. Clear the hand
            hand.Clear();

            // 2. Find the three guaranteed starter cards in the master deck
            BlueprintCardSO cmdPostCard = null;
            BlueprintCardSO droneCard = null;
            BlueprintCardSO solarCard = null;
            foreach (var c in masterDeck)
            {
                if (c is UnlockBuildingCardSO unlock)
                {
                    if (unlock.buildingToUnlock != null && unlock.buildingToUnlock.Name == "Command Post")
                        cmdPostCard = c;
                    if (unlock.buildingToUnlock != null && unlock.buildingToUnlock.Name == "Solar Panel")
                        solarCard = c;
                }
                if (c is SpawnUnitCardSO spawn)
                {
                    if (spawn.cardName == "Mining Drone")
                        droneCard = c;
                }
            }

            cmdPostCard ??= EnsureStarterCard<UnlockBuildingCardSO>("Cards/CommandPostCard");
            solarCard ??= EnsureStarterCard<UnlockBuildingCardSO>("Cards/SolarPanelCard");
            droneCard ??= EnsureStarterCard<SpawnUnitCardSO>("Cards/MiningDroneCard");

            // 3. Always seed Command Post + Solar + Mining Drone into the opening hand.
            //    RebuildDeck often runs before planet pads / CP exist; those cards stay in hand
            //    (see ShouldKeepInHand) and become playable as soon as their gates are met.
            if (cmdPostCard != null)
            {
                hand.Add(cmdPostCard);
                drawPile.Remove(cmdPostCard);
            }
            if (solarCard != null)
            {
                hand.Add(solarCard);
                drawPile.Remove(solarCard);
            }
            if (droneCard != null)
            {
                hand.Add(droneCard);
                drawPile.Remove(droneCard);
            }

            Debug.Log($"[CardDeckController] Seeded {hand.Count} starter(s). " +
                      $"CmdPost={(cmdPostCard != null && hand.Contains(cmdPostCard) ? "YES" : "held")} " +
                      $"Solar={(solarCard != null && hand.Contains(solarCard) ? "YES" : "held")} " +
                      $"Drone={(droneCard != null && hand.Contains(droneCard) ? "YES" : "held")}");

            // 4. Fill remaining slots from the front of the draw queue.
            FillHand();

            OnHandChanged?.Invoke();

            // Force the bottom bar to refresh so the cards appear immediately
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
        }

        /// <summary>
        /// After a reserved-site build already succeeded, consume the hand card without
        /// re-checking CanApply (the pad is now occupied so CanApply would fail).
        /// Still runs Apply() so unlocks/hazards register.
        /// </summary>
        public void ConsumeCardAfterBuild(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;
            BlueprintCardSO played = hand[handIndex];
            if (played == null) return;

            Debug.Log($"[CardDeckController] Consuming card after build: '{played.cardName}' (index {handIndex})");

            if (played.HazardEventPrefabs != null)
            {
                foreach (var hazard in played.HazardEventPrefabs)
                {
                    if (hazard != null)
                    {
                        NaturalEventManager.RegisterHazard(hazard);
                    }
                }
            }

            played.Apply();

            // Week first, then deferred instant-build score (and non-building card score).
            int weekCost = GetWeekCost(played);
            if (weekCost > 0)
                ColonyActManager.Instance?.SpendWeeks(weekCost);
            BaseBuilding.FlushAllDeferredColonyActScores();
            ColonyActManager.Instance?.GrantCardScore(played);

            GameFlowManager.Instance?.PlayerActed();

            hand.RemoveAt(handIndex);
            discardPile.Add(played);
            FillHand();
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
            OnHandChanged?.Invoke();
        }

        // ── Hand Management ──────────────────────────────────────────────────

        /// <summary>
        /// Drop cards that can no longer be played, then draw replacements from
        /// the front of the FIFO queue.
        /// </summary>
        public void RefreshHand()
        {
            var before = hand.ToArray();
            DiscardUnplayableFromHand();
            EnsureSolarPrereqInHand(); // keeps a power generator seated (Solar / Geothermal / …)
            EnsureMiningDroneInHand();
            EnsureMvpClimateGoalsInHand();
            InjectPendingProductionOffers();
            FillHandInternal();
            EnsureSolarPrereqInHand(); // keeps a power generator seated (Solar / Geothermal / …)
            EnsureMiningDroneInHand();
            EnsureMvpClimateGoalsInHand();
            TrimHandToSize();
            if (before.Length != hand.Count || !before.SequenceEqual(hand))
            {
                OnHandChanged?.Invoke();
            }
        }

        /// <summary>
        /// Fill the hand to handSize by drawing from the front of the draw pile.
        /// Unplayable cards are sent to the back of discard and skipped for now.
        /// </summary>
        public void FillHand()
        {
            StripUndiscoveredGeologicalCardsFromHand();
            StripExcludedColonyCardsFromHand();
            EnsureSolarPrereqInHand(); // keeps a power generator seated (Solar / Geothermal / …)
            EnsureMiningDroneInHand();
            EnsureMvpClimateGoalsInHand();
            InjectPendingProductionOffers();
            FillHandInternal();
            EnsureSolarPrereqInHand(); // keeps a power generator seated (Solar / Geothermal / …)
            EnsureMiningDroneInHand();
            EnsureMvpClimateGoalsInHand();
            TrimHandToSize();
            ColonyActManager.Instance?.TryRefreshClimateComboFromPresence();
            OnHandChanged?.Invoke();
        }

        /// <summary>
        /// When an Act climate channel is still unmet, keep at least one matching card in hand
        /// (need-based — avoids Water famine without flooding the deck).
        /// </summary>
        private void EnsureMvpClimateGoalsInHand()
        {
            var acts = ColonyActManager.Instance;
            if (acts == null || !acts.IsRunActive) return;

            acts.GetClimateGains(out float tempGain, out float atmosGain, out float waterGain);
            if (waterGain + 0.0005f < GenerationManager.SectorWaterDelta)
                EnsureClimateGoalCardInHand("WATER");
            if (atmosGain + 0.0005f < GenerationManager.SectorAtmosphereDelta)
                EnsureClimateGoalCardInHand("ATMOSPHERE");
            if (tempGain + 0.0005f < GenerationManager.SectorTemperatureDelta)
                EnsureClimateGoalCardInHand("TEMPERATURE");
        }

        private void EnsureClimateGoalCardInHand(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return;
            if (hand.Any(c =>
                    string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase)))
                return;

            BlueprintCardSO found = FindCardInPiles(c =>
                string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase)
                && ShouldKeepInHand(c));

            if (found == null)
            {
                found = FindCardInPiles(c =>
                    c is UnlockBuildingCardSO unlock
                    && unlock.buildingToUnlock != null
                    && string.Equals(
                        UnlockBuildingCardSO.ClassifyBuildingGoal(unlock.buildingToUnlock),
                        goal,
                        StringComparison.OrdinalIgnoreCase)
                    && ShouldKeepInHand(c));
            }

            if (found == null)
            {
                // Clone so an unmet channel can never soft-lock the run.
                string preferred = goal switch
                {
                    "WATER" => "Water Ice Aquifer",
                    "ATMOSPHERE" => "Atmospheric Condenser",
                    "TEMPERATURE" => "GHG Factory",
                    _ => null
                };
                BlueprintCardSO template = null;
                if (!string.IsNullOrEmpty(preferred))
                {
                    template = masterDeck.FirstOrDefault(c =>
                        c != null && c.cardName != null
                        && c.cardName.IndexOf(preferred, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                template ??= masterDeck.FirstOrDefault(c =>
                    string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase));
                if (template == null) return;
                found = UnityEngine.Object.Instantiate(template);
                found.name = $"{template.name} (Climate Reserve)";
            }

            MakeHandRoomForClimateGoal(goal, found);
            if (hand.Count >= handSize) return;
            if (drawPile.Contains(found)) drawPile.Remove(found);
            if (discardPile.Contains(found)) discardPile.Remove(found);
            hand.Add(found);
            Debug.Log($"[CardDeckController] Seated unmet climate card '{found.cardName}' ({goal}).");
        }

        /// <summary>Legacy force-seat — disabled so the player freely picks any hand card.</summary>
        private void EnsureMiningDroneInHand()
        {
        }

        private static bool IsMiningDroneCard(BlueprintCardSO card)
        {
            return card is SpawnUnitCardSO spawn
                && spawn.cardName != null
                && spawn.cardName.Contains("Mining Drone", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Always keep at least one power-generator card seated (Solar Panel, Geothermal,
        /// Magnetic Shield, etc.) so the player cannot soft-lock on the power place-gate.
        /// </summary>
        private void EnsureSolarPrereqInHand()
        {
            if (IsPowerGeneratorUnlockCardInHand()) return;
            EnsureBootstrapUnlockInHand("Solar");
            if (IsPowerGeneratorUnlockCardInHand()) return;
            EnsureBootstrapUnlockInHand("Geothermal");
            if (IsPowerGeneratorUnlockCardInHand()) return;

            // Clone from deck so the player never soft-locks.
            BlueprintCardSO powerTemplate = masterDeck.FirstOrDefault(IsPowerGeneratorUnlockCard)
                ?? drawPile.FirstOrDefault(IsPowerGeneratorUnlockCard)
                ?? discardPile.FirstOrDefault(IsPowerGeneratorUnlockCard)
                ?? masterDeck.FirstOrDefault(IsSolarUnlockCard)
                ?? drawPile.FirstOrDefault(IsSolarUnlockCard);
            if (powerTemplate != null)
            {
                BlueprintCardSO clone = UnityEngine.Object.Instantiate(powerTemplate);
                clone.name = $"{powerTemplate.name} (Power Reserve)";
                if (hand.Count >= handSize)
                    MakeHandRoomForHandoffCard(clone);
                if (hand.Count < handSize)
                    hand.Add(clone);
            }
        }

        /// <summary>
        /// When a building completes, queue whatever it used to build from its RTS command
        /// list as hand cards for the next draw (after a week is spent).
        /// </summary>
        public void QueueProductionFromBuilding(BaseBuilding building)
        {
            if (building == null || building.Owner != Owner.Player1) return;
            var cmds = building.GetNativeCommandsForCardOffers();
            if (cmds == null || cmds.Length == 0) return;
            QueueCommandsAsCards(cmds);
        }

        /// <summary>Clear Act-scoped climate combo dedupe (call on BeginRun / Act clear).</summary>
        public void NotifyActClimateComboReset()
        {
            climateComboOffersThisAct.Clear();
        }

        /// <summary>
        /// Heat+Air / Air+Water / Water+Heat adjacency: queue the missing third climate card
        /// into the hand once per Act per channel. Returns offered card name, or null if skipped.
        /// </summary>
        public string QueueClimateComboOffer(string goalKey)
        {
            if (string.IsNullOrEmpty(goalKey)) return null;

            string goal = goalKey.Trim().ToUpperInvariant();
            if (goal != "TEMPERATURE" && goal != "ATMOSPHERE" && goal != "WATER") return null;

            if (climateComboOffersThisAct.Contains(goal)) return null;

            // Always grant the combo tile once per Act — even if a Water/Heat/Atmos
            // card is already seated from need-based draw (that used to silently skip).
            BlueprintCardSO template = FindPreferredClimateComboTemplate(goal);
            if (template == null) return null;

            BlueprintCardSO offer = UnityEngine.Object.Instantiate(template);
            offer.name = $"{template.name} (Climate Combo)";
            if (!string.IsNullOrEmpty(template.cardName))
                offer.cardName = template.cardName;

            climateComboOffersThisAct.Add(goal);
            EnqueueProductionOffer(offer);
            InjectPendingProductionOffers();
            if (hand.Count < handSize)
                FillHandInternal();
            OnHandChanged?.Invoke();

            Debug.Log($"[CardDeckController] Climate combo offered '{offer.cardName}' for {goal}.");
            return string.IsNullOrEmpty(offer.cardName) ? offer.name : offer.cardName;
        }

        private BlueprintCardSO FindPreferredClimateComboTemplate(string goal)
        {
            string preferredName = goal switch
            {
                "TEMPERATURE" => "GHG Factory",
                "ATMOSPHERE" => "Atmospheric Condenser",
                "WATER" => "Water Ice Aquifer",
                _ => null
            };

            BlueprintCardSO Prefer(string nameContains)
            {
                if (string.IsNullOrEmpty(nameContains)) return null;
                bool MatchName(BlueprintCardSO c) =>
                    c != null
                    && c.cardName != null
                    && c.cardName.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0
                    && string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase);

                return masterDeck.FirstOrDefault(MatchName)
                    ?? drawPile.FirstOrDefault(MatchName)
                    ?? discardPile.FirstOrDefault(MatchName)
                    ?? Resources.LoadAll<UnlockBuildingCardSO>("Cards").FirstOrDefault(MatchName);
            }

            BlueprintCardSO preferred = Prefer(preferredName);
            if (preferred != null) return preferred;

            // Fallback: any card classified to this climate goal.
            bool MatchGoal(BlueprintCardSO c) =>
                c != null
                && string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase);

            return masterDeck.FirstOrDefault(MatchGoal)
                ?? drawPile.FirstOrDefault(MatchGoal)
                ?? discardPile.FirstOrDefault(MatchGoal);
        }

        private void QueueCommandsAsCards(BaseCommand[] cmds)
        {
            if (cmds == null) return;
            foreach (var cmd in cmds)
            {
                if (cmd == null) continue;
                if (cmd is GameDevTV.RTS.Commands.OverrideCommandsCommand ov && ov.Commands != null)
                {
                    QueueCommandsAsCards(ov.Commands);
                    continue;
                }

                if (cmd is GameDevTV.RTS.Commands.BuildBuildingCommand bbc && bbc.Building != null)
                {
                    BlueprintCardSO card = FindOrCloneUnlockCardForBuilding(bbc.Building);
                    if (card != null) EnqueueProductionOffer(card);
                }
                else if (cmd is GameDevTV.RTS.Commands.BuildUnitCommand buc && buc.Unit != null)
                {
                    BlueprintCardSO card = FindOrCloneSpawnCardForUnit(buc.Unit);
                    if (card != null) EnqueueProductionOffer(card);
                }
            }
        }

        private void EnqueueProductionOffer(BlueprintCardSO card)
        {
            if (card == null) return;
            if (IsExcludedFromColonyDeck(card)) return;
            // Avoid flooding duplicates already pending / in hand.
            if (hand.Contains(card)) return;
            foreach (var pending in pendingProductionOffers)
            {
                if (pending == card) return;
                if (pending != null && card != null
                    && pending.cardName == card.cardName) return;
            }
            pendingProductionOffers.Enqueue(card);
            Debug.Log($"[CardDeckController] Queued production offer '{card.cardName}' for next hand fill.");
        }

        private void InjectPendingProductionOffers()
        {
            while (pendingProductionOffers.Count > 0 && hand.Count < handSize)
            {
                BlueprintCardSO offer = pendingProductionOffers.Dequeue();
                if (offer == null) continue;
                if (hand.Contains(offer)) continue;
                // Prefer inserting near the front so the player sees new unlocks.
                hand.Insert(0, offer);
                drawPile.Remove(offer);
                discardPile.Remove(offer);
            }
        }

        private BlueprintCardSO FindOrCloneUnlockCardForBuilding(BuildingSO building)
        {
            if (building == null) return null;
            bool Match(BlueprintCardSO c) =>
                c is UnlockBuildingCardSO u
                && u.buildingToUnlock != null
                && u.buildingToUnlock.Name == building.Name;

            BlueprintCardSO found = hand.FirstOrDefault(Match)
                ?? drawPile.FirstOrDefault(Match)
                ?? discardPile.FirstOrDefault(Match)
                ?? masterDeck.FirstOrDefault(Match);
            if (found != null)
            {
                if (hand.Contains(found)) return null; // already available
                return found;
            }

            // Runtime unlock card so production is still playable as a tile.
            var created = ScriptableObject.CreateInstance<UnlockBuildingCardSO>();
            created.cardName = building.Name;
            created.buildingToUnlock = building;
            created.icon = building.Icon;
            created.name = $"Unlock_{building.Name}_FromBuilding";
            return created;
        }

        private BlueprintCardSO FindOrCloneSpawnCardForUnit(AbstractUnitSO unit)
        {
            if (unit == null) return null;
            bool Match(BlueprintCardSO c) =>
                c is SpawnUnitCardSO s
                && s.cardName != null
                && unit.Name != null
                && s.cardName.IndexOf(unit.Name, StringComparison.OrdinalIgnoreCase) >= 0;

            BlueprintCardSO found = drawPile.FirstOrDefault(Match)
                ?? discardPile.FirstOrDefault(Match)
                ?? masterDeck.FirstOrDefault(Match);
            if (found != null) return found;

            var created = ScriptableObject.CreateInstance<SpawnUnitCardSO>();
            created.cardName = unit.Name;
            created.icon = unit.Icon;
            created.name = $"Spawn_{unit.Name}_FromBuilding";
            if (unit.Prefab != null)
                created.unitPrefab = unit.Prefab;
            return created;
        }

        private static bool IsSolarUnlockCard(BlueprintCardSO card)
        {
            return card is UnlockBuildingCardSO unlock
                && unlock.buildingToUnlock != null
                && BuildingSiteRegistry.IsSolarBuilding(unlock.buildingToUnlock);
        }

        private static bool IsPowerGeneratorUnlockCard(BlueprintCardSO card)
        {
            return card is UnlockBuildingCardSO unlock
                && unlock.buildingToUnlock != null
                && BuildingSiteRegistry.IsPowerGeneratorBuilding(unlock.buildingToUnlock)
                && DiscoverySystem.IsBuildingGeologicallyAvailable(unlock.buildingToUnlock);
        }

        private bool IsSolarUnlockCardInHand() => hand.Any(IsSolarUnlockCard);
        private bool IsPowerGeneratorUnlockCardInHand() => hand.Any(IsPowerGeneratorUnlockCard);

        /// <summary>
        /// True for unlock cards that place on a solar-powered cluster pad and cannot
        /// apply yet (typically missing cluster solar).
        /// </summary>
        private static bool NeedsSolarPoweredPad(UnlockBuildingCardSO unlock)
        {
            if (unlock?.buildingToUnlock == null) return false;
            BuildingSO b = unlock.buildingToUnlock;
            if (BuildingSiteRegistry.IsSolarBuilding(b)) return false;
            if (BuildingSiteRegistry.IsCommandBuilding(b)) return false;
            if (BuildingSiteRegistry.IsMineBuilding(b)) return false;
            if (unlock.CanApply()) return false;
            return unlock.IsGateMet();
        }

        private void FillHandInternal()
        {
            if (masterDeck == null || masterDeck.Count == 0) return;

            int safety = drawPile.Count + discardPile.Count + hand.Count + 8;
            while (hand.Count < handSize && safety-- > 0)
            {
                if (drawPile.Count == 0)
                {
                    if (discardPile.Count == 0) break;
                    RecycleDiscardIntoDraw();
                    if (drawPile.Count == 0) break;
                }

                BlueprintCardSO candidate = drawPile[0];
                drawPile.RemoveAt(0);

                if (IsDrawableNow(candidate))
                {
                    hand.Add(candidate);
                }
                else if (candidate != null)
                {
                    discardPile.Add(candidate);
                }
            }
        }

        /// <summary>
        /// Combolands: never auto-purge the hand — player keeps every drawn card and
        /// picks which to play. Soft gates (pads / materials) are checked on click only.
        /// </summary>
        public void DiscardUnplayableFromHand()
        {
            // Intentionally empty.
        }

        private static bool IsPlayableNow(BlueprintCardSO card)
        {
            return card != null && card.IsGateMet() && card.CanApply();
        }

        /// <summary>Any valid card can enter the hand — player chooses when to play it.</summary>
        private static bool IsDrawableNow(BlueprintCardSO card) => ShouldKeepInHand(card);

        /// <summary>
        /// Colony Acts deck: drop RTS combat clutter and one-shot shipment boosts
        /// that aren't placeable tiles (Barracks, Infantry School, ResourceShipment).
        /// Spaceport and Deploy Engineer stay.
        /// </summary>
        public static bool IsExcludedFromColonyDeck(BlueprintCardSO card)
        {
            if (card == null) return true;

            // Instant Temp/Atmos/Water/Materials dumps — not Combolands tiles.
            if (card is ResourceShipmentCardSO) return true;

            string name = card.cardName ?? card.name ?? string.Empty;
            if (NameLooksLikeMilitaryClutter(name)) return true;
            if (NameLooksLikeShipmentClutter(name)) return true;

            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
            {
                string buildingName = unlock.buildingToUnlock.Name ?? string.Empty;
                if (NameLooksLikeMilitaryClutter(buildingName)) return true;
            }

            return false;
        }

        private static bool NameLooksLikeMilitaryClutter(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Barracks", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Infantry", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool NameLooksLikeShipmentClutter(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Shipment", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Culture Serum", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Keep real cards; mines / geology tiles only after discovery.</summary>
        private static bool ShouldKeepInHand(BlueprintCardSO card)
        {
            if (card == null) return false;
            if (IsExcludedFromColonyDeck(card)) return false;
            if (card is UnlockBuildingCardSO unlock)
            {
                if (unlock.buildingToUnlock == null || unlock.buildingToUnlock.Prefab == null)
                    return false;
                if (!DiscoverySystem.IsBuildingGeologicallyAvailable(unlock.buildingToUnlock))
                    return false;
                return true;
            }
            return true;
        }

        private void StripUndiscoveredGeologicalCardsFromHand()
        {
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                if (hand[i] is not UnlockBuildingCardSO unlock) continue;
                if (unlock.buildingToUnlock == null) continue;
                if (DiscoverySystem.IsBuildingGeologicallyAvailable(unlock.buildingToUnlock)) continue;
                discardPile.Add(hand[i]);
                hand.RemoveAt(i);
            }
        }

        private void StripExcludedColonyCardsFromHand()
        {
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                if (!IsExcludedFromColonyDeck(hand[i])) continue;
                discardPile.Add(hand[i]);
                hand.RemoveAt(i);
            }
        }

        /// <summary>
        /// Discards the entire hand and draws a fresh one. 
        /// Triggered during the Draw phase of turn resolution.
        /// </summary>
        public void DiscardHandAndDrawFresh()
        {
            discardPile.AddRange(hand);
            hand.Clear();
            FillHand();
        }

        /// <summary>
        /// Play the card at the given hand index: apply its effect,
        /// remove from hand, discard it, and draw a replacement.
        /// </summary>
        public void PlayCard(int handIndex)
        {
            if (handIndex < 0 || handIndex >= hand.Count) return;
            if (hand[handIndex] == null) return;

            BlueprintCardSO played = hand[handIndex];
            if (!played.IsGateMet())
            {
                Debug.LogWarning($"[CardDeckController] Card '{played.cardName}' cannot be played yet because its requirements are not met.");
                return;
            }

            if (!played.CanApply())
            {
                if (played is ScoutingCardSO scouting)
                {
                    var mgr = ExplorationManager.Instance;
                    string reason = $"Cannot play '{played.cardName}' right now.";
                    if (!played.CanAffordMaterials())
                        reason = $"Need {played.GetMaterialsPlayCost()} Materials to play {played.cardName}.";
                    else if (mgr != null && scouting.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan)
                    {
                        if (!mgr.CanAffordExploration())
                            reason = $"Need {mgr.ExploreEnergyCost:0.#} Energy to play Orbital Scan.";
                        else if (!GenerationManager.CanUnlockNextMapSector())
                            reason = "Finish this sector's terraforming goals before opening the next sector.";
                        else if (SectorManager.Instance != null && SectorManager.Instance.GetNextLockedSectorIndex() < 0)
                            reason = "All sectors are already unlocked.";
                    }
                    ExplorationManager.NotifyExplorationFailed(reason);
                }
                else if (!played.CanAffordMaterials())
                {
                    ExplorationManager.NotifyExplorationFailed(
                        $"Need {played.GetMaterialsPlayCost()} Materials to play {played.cardName}.");
                }
                else
                {
                    Debug.LogWarning($"[CardDeckController] Card '{played.cardName}' cannot be played right now.");
                }
                return;
            }

            Debug.Log($"[CardDeckController] Playing card: '{played.cardName}' (index {handIndex})");

            // Building cards spend materials at pad placement (ReservedSiteBuildUtility).
            // Non-building cards spend here before Apply().
            bool buildingCard = played is UnlockBuildingCardSO;
            if (!buildingCard && !played.TrySpendMaterials())
            {
                ExplorationManager.NotifyExplorationFailed(
                    $"Need {played.GetMaterialsPlayCost()} Materials to play {played.cardName}.");
                return;
            }

            // Register card's hazards if it has any
            if (played.HazardEventPrefabs != null)
            {
                foreach (var hazard in played.HazardEventPrefabs)
                {
                    if (hazard != null)
                    {
                        NaturalEventManager.RegisterHazard(hazard);
                    }
                }
            }

            // Apply the card's effect
            played.Apply();

            // Week first so Act-clear from GrantCardScore cannot leave the week on the next Act.
            int weekCost = GetWeekCost(played);
            if (weekCost > 0)
                ColonyActManager.Instance?.SpendWeeks(weekCost);
            ColonyActManager.Instance?.GrantCardScore(played);

            // Notify GameFlowManager that an action was taken
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.PlayerActed();
            }

            // Move played card to discard
            hand.RemoveAt(handIndex);
            discardPile.Add(played);

            // Draw a replacement
            FillHand();

            // Refresh UI
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
        }

        /// <summary>Old DrawCard kept for backward compatibility — now draws into the hand instead of auto-applying.</summary>
        public void DrawCard()
        {
            FillHand();
        }

        /// <summary>
        /// Explore a specific frontier node by consuming a scouting card from hand plus energy.
        /// </summary>
        public bool TryExploreAtNode(SectorNode node, int sectorIndex)
        {
            if (node == null || ExplorationManager.Instance == null) return false;

            if (!ExplorationManager.Instance.IsValidExploreTarget(node))
            {
                ExplorationManager.NotifyExplorationFailed("That node is not a valid exploration target.");
                return false;
            }

            int handIndex = FindExplorationScoutingCardIndex();
            if (handIndex < 0)
            {
                ExplorationManager.NotifyExplorationFailed("Need an Orbital Scan or Survey Drone card to explore.");
                return false;
            }

            BlueprintCardSO scoutingCard = hand[handIndex];
            if (!scoutingCard.CanApply())
            {
                ExplorationManager.NotifyExplorationFailed($"Cannot play '{scoutingCard.cardName}' right now.");
                return false;
            }

            hand.RemoveAt(handIndex);
            discardPile.Add(scoutingCard);

            if (!ExplorationManager.Instance.TryExploreNode(node, sectorIndex))
            {
                hand.Add(scoutingCard);
                discardPile.Remove(scoutingCard);
                return false;
            }

            FillHand();
            GameFlowManager.Instance?.PlayerActed();
            Bus<UpgradeResearchedEvent>.Raise(Owner.Player1, new UpgradeResearchedEvent(Owner.Player1, null));
            OnHandChanged?.Invoke();
            return true;
        }

        private int FindExplorationScoutingCardIndex()
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] is ScoutingCardSO scouting &&
                    (scouting.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan ||
                     scouting.scoutingType == ScoutingCardSO.ScoutingType.SurveyDrone))
                {
                    return i;
                }
            }

            return -1;
        }

        // ── Draft UI (disabled) ──────────────────────────────────────────────

        /// <summary>
        /// Draft rounds are disabled. The player uses the normal hand/deck instead.
        /// Kept as a no-op so old callers (sector unlock, cheats) do not pause the game.
        /// </summary>
        public void TriggerDraft()
        {
            Debug.Log("[CardDeckController] TriggerDraft skipped — card draft rounds are disabled.");
        }

        /// <summary>Called by the UI when the player selects a card.</summary>
        public void SelectCard(BlueprintCardSO chosen, List<BlueprintCardSO> fullHand)
        {
            foreach (var card in fullHand)
            {
                drawPile.Remove(card);
                if (card != chosen)
                    discardPile.Add(card);
            }
            discardPile.Add(chosen);
            BlueprintDraftManager.CompleteDraft(chosen);
        }

        // ── Draft Curation ───────────────────────────────────────────────────

        private List<BlueprintCardSO> GetCuratedHand()
        {
            if (masterDeck == null || masterDeck.Count == 0) return null;

            BlueprintCardSO emergencyCaches = masterDeck.FirstOrDefault(c =>
                c is ScoutingCardSO s && s.scoutingType == ScoutingCardSO.ScoutingType.EmergencyCaches);

            var curatedPool = masterDeck
                .Where(c => c != emergencyCaches)
                .Where(c => c.IsGateMet())
                .ToList();

            if (curatedPool.Count == 0)
            {
                var fallbackHand = new List<BlueprintCardSO>();
                if (emergencyCaches != null) fallbackHand.Add(emergencyCaches);
                return fallbackHand;
            }

            curatedPool = curatedPool.OrderBy(_ => UnityEngine.Random.value).ToList();
            var scoutingCards = curatedPool.Where(c => c is ScoutingCardSO).ToList();
            var otherCards = curatedPool.Where(c => !(c is ScoutingCardSO)).ToList();

            var hand = new List<BlueprintCardSO>();

            bool hasLockedSectors = SectorManager.Instance != null &&
                                    SectorManager.Instance.GetNextLockedSectorIndex() >= 0;

            if (hasLockedSectors && scoutingCards.Count > 0)
            {
                hand.Add(scoutingCards[0]);
                scoutingCards.RemoveAt(0);
            }

            var mixedPool = scoutingCards.Concat(otherCards).OrderBy(_ => UnityEngine.Random.value).ToList();
            int slotsRemaining = handSize - hand.Count;
            for (int i = 0; i < slotsRemaining && i < mixedPool.Count; i++)
            {
                hand.Add(mixedPool[i]);
            }

            if (emergencyCaches != null && !hand.Contains(emergencyCaches))
            {
                hand.Add(emergencyCaches);
            }

            return hand;
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        /// <summary>Stable deck order — master deck sequence, no random shuffle.
        /// Sector-completion cards (climate + primary milestones) are added a second
        /// time as runtime clones so finishing tools appear twice as often.</summary>
        private void InitializeDrawPile()
        {
            drawPile = new List<BlueprintCardSO>(masterDeck.Count * 2);
            discardPile.Clear();

            foreach (var card in masterDeck)
            {
                if (card == null || IsExcludedFromColonyDeck(card)) continue;
                drawPile.Add(card);
            }

            // Second pass: duplicate win-path cards without mutating shared assets.
            int extras = 0;
            foreach (var card in masterDeck)
            {
                if (card == null || IsExcludedFromColonyDeck(card)) continue;
                if (TerraformingGoalColors.GetSectorGoalForCard(card) == null) continue;

                BlueprintCardSO copy = UnityEngine.Object.Instantiate(card);
                copy.name = $"{card.name} (Sector Copy)";
                drawPile.Add(copy);
                extras++;
            }

            // Extra power infrastructure copies (Solar + Geothermal) so the place-gate
            // stays reachable while climate cards dominate the FIFO front.
            BlueprintCardSO solarTemplate = masterDeck.FirstOrDefault(c =>
                c is UnlockBuildingCardSO u
                && u.buildingToUnlock != null
                && BuildingSiteRegistry.IsSolarBuilding(u.buildingToUnlock));
            if (solarTemplate != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    BlueprintCardSO extraSolar = UnityEngine.Object.Instantiate(solarTemplate);
                    extraSolar.name = $"{solarTemplate.name} (Infra Copy {i + 1})";
                    drawPile.Add(extraSolar);
                    extras += 1;
                }
            }

            BlueprintCardSO geoTemplate = masterDeck.FirstOrDefault(c =>
                c is UnlockBuildingCardSO u
                && u.buildingToUnlock != null
                && u.buildingToUnlock.Name != null
                && u.buildingToUnlock.Name.Contains("Geothermal", StringComparison.OrdinalIgnoreCase));
            if (geoTemplate == null)
                geoTemplate = Resources.Load<UnlockBuildingCardSO>("Cards/GeothermalGeneratorCard");
            if (geoTemplate != null && !IsExcludedFromColonyDeck(geoTemplate))
            {
                if (!masterDeck.Contains(geoTemplate) && !drawPile.Contains(geoTemplate))
                    drawPile.Add(geoTemplate);
                for (int i = 0; i < 4; i++)
                {
                    BlueprintCardSO extraGeo = UnityEngine.Object.Instantiate(geoTemplate);
                    extraGeo.name = $"{geoTemplate.name} (Power Copy {i + 1})";
                    drawPile.Add(extraGeo);
                    extras += 1;
                }
            }

            // Balanced climate extras: enough Water without dominating the hand.
            extras += AddClimateChannelExtras("TEMPERATURE", "GHG Factory", 3);
            extras += AddClimateChannelExtras("ATMOSPHERE", "Atmospheric Condenser", 3);
            extras += AddClimateChannelExtras("WATER", "Water Ice Aquifer", 5);
            extras += AddClimateChannelExtras("WATER", "Subglacial Water Extractor", 3);

            // Pull real climate tiles (esp. blue Water) near the front so the opening
            // hand isn't only Air/Heat while Aquifers sit at the bottom of FIFO.
            PrioritizeClimateCardsNearFront();

            Debug.Log($"[CardDeckController] Draw pile ready: {drawPile.Count} cards " +
                      $"({masterDeck.Count} base + {extras} sector-win/infra duplicates).");
        }

        /// <summary>
        /// Move one Water / Atmos / Temp unlock near the front of the draw pile
        /// so blue Aquifer cards appear early instead of only after dozens of draws.
        /// </summary>
        private void PrioritizeClimateCardsNearFront()
        {
            if (drawPile == null || drawPile.Count < 2) return;

            string[] goals = { "WATER", "ATMOSPHERE", "TEMPERATURE" };
            var pulled = new List<BlueprintCardSO>(goals.Length);
            foreach (string goal in goals)
            {
                int idx = drawPile.FindIndex(c =>
                    c is UnlockBuildingCardSO
                    && string.Equals(
                        TerraformingGoalColors.GetSectorGoalForCard(c),
                        goal,
                        StringComparison.OrdinalIgnoreCase));
                if (idx < 0) continue;
                pulled.Add(drawPile[idx]);
                drawPile.RemoveAt(idx);
            }

            for (int i = pulled.Count - 1; i >= 0; i--)
                drawPile.Insert(0, pulled[i]);
        }

        private int AddClimateChannelExtras(string goal, string preferredCardName, int count)
        {
            if (count <= 0) return 0;
            bool MatchPreferred(BlueprintCardSO c) =>
                c != null
                && c.cardName != null
                && c.cardName.IndexOf(preferredCardName, StringComparison.OrdinalIgnoreCase) >= 0
                && string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase);
            bool MatchGoal(BlueprintCardSO c) =>
                c != null
                && string.Equals(TerraformingGoalColors.GetSectorGoalForCard(c), goal, StringComparison.OrdinalIgnoreCase);

            BlueprintCardSO template = masterDeck.FirstOrDefault(MatchPreferred)
                ?? masterDeck.FirstOrDefault(MatchGoal)
                ?? drawPile.FirstOrDefault(MatchPreferred)
                ?? drawPile.FirstOrDefault(MatchGoal);
            if (template == null) return 0;

            for (int i = 0; i < count; i++)
            {
                BlueprintCardSO extra = UnityEngine.Object.Instantiate(template);
                extra.name = $"{template.name} (Climate Copy {i + 1})";
                drawPile.Add(extra);
            }
            return count;
        }

        /// <summary>Move discard queue onto draw queue, preserving FIFO order.</summary>
        private void RecycleDiscardIntoDraw()
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
        }

        private T EnsureStarterCard<T>(string resourcePath) where T : BlueprintCardSO
        {
            T card = Resources.Load<T>(resourcePath);
            if (card == null)
            {
                throw new InvalidOperationException(
                    $"[CardDeckController] Required opening card is missing at Resources/{resourcePath}. " +
                    "The opening hand must contain Command Post, Mining Drone, and Solar Panel so the player can establish a base, deploy a builder, and generate power. " +
                    "Restore the missing asset or correct its Resources path before starting the game.");
            }

            if (!masterDeck.Contains(card)) masterDeck.Add(card);
            if (!drawPile.Contains(card)) drawPile.Add(card);
            return card;
        }

        /// <summary>
        /// Act week cost for a card play. Varies by tile role (0–2) so budgeting matters —
        /// not everything is exactly 1 week.
        /// </summary>
        public static int GetWeekCost(BlueprintCardSO card)
        {
            if (card == null) return 1;
            if (card is ScoutingCardSO) return 0;

            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
                return GetWeekCost(unlock.buildingToUnlock);

            if (card is SpawnUnitCardSO)
                return 1;

            string name = card.cardName ?? string.Empty;
            if (name.IndexOf("discover", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("scout", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("survey", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("orbital scan", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0;

            return 1;
        }

        public static int GetWeekCost(BuildingSO building)
        {
            if (building == null) return 1;

            string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);
            string name = building.Name ?? string.Empty;

            // Heavier colony investments cost more weeks.
            if (goal == "COMMAND POST"
                || name.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Spaceport", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;

            if (BuildingSiteRegistry.IsMineBuilding(building)
                || goal == "MATERIALS"
                || name.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;

            // Power, climate, life, housing, default tiles: 1 week.
            if (BuildingSiteRegistry.IsPowerGeneratorBuilding(building)
                || BuildingSiteRegistry.IsSolarBuilding(building)
                || goal == "POWER"
                || goal == "TEMPERATURE"
                || goal == "ATMOSPHERE"
                || goal == "WATER"
                || goal == "OXYGEN"
                || goal == "POPULATION")
                return 1;

            return 1;
        }
    }
}
