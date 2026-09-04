using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        [SerializeField] private int handSize = 10;

        private List<BlueprintCardSO> drawPile = new();
        private List<BlueprintCardSO> discardPile = new();
        private List<BlueprintCardSO> hand = new();

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
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
            GenerationManager.OnGenerationStarted += HandleGenerationStarted;
            GenerationManager.OnGenerationEnded += HandleGenerationEnded;
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
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
            GenerationManager.OnGenerationStarted -= HandleGenerationStarted;
            GenerationManager.OnGenerationEnded -= HandleGenerationEnded;
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

        /// <summary>
        /// After auto-colonizing a sector and the player acknowledges the handoff UI,
        /// seat Solar + unmet climate/primary tools so the new Command Post is immediately playable.
        /// Exception to FIFO only at this handoff moment.
        /// </summary>
        public void PrepareHandForColonizedSector()
        {
            DiscardUnplayableFromHand();

            EnsureBootstrapUnlockInHand("Solar");

            // Mining drones become useful as soon as the new CP exists.
            EnsureNamedCardInHand(c =>
                c is SpawnUnitCardSO spawn &&
                spawn.cardName != null &&
                spawn.cardName.Contains("Mining Drone", StringComparison.OrdinalIgnoreCase),
                requirePlayable: true);

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

            EnsureSolarPrereqInHand();
            FillHandInternal();
            EnsureSolarPrereqInHand();

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
                && c.IsGateMet()
                && ShouldKeepInHand(c));

            if (found == null) return;

            MakeHandRoomForHandoffCard(found);
            if (hand.Count >= handSize) return;

            hand.Add(found);
            Debug.Log($"[CardDeckController] Seated unmet sector-goal card '{found.cardName}' ({goal}) for handoff.");
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
            int dropIdx = hand.FindIndex(c =>
                c != null
                && TerraformingGoalColors.GetSectorGoalForCard(c) == null
                && !IsSolarUnlockCard(c));
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

            hand.Add(found);
            Debug.Log($"[CardDeckController] Restored bootstrap card '{found.cardName}' after planet gen.");
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

            // 3. Always seed Command Post + Solar into the opening hand. RebuildDeck often
            //    runs before planet pads exist, so CanApply would falsely hold them out.
            //    Mining Drone waits until a Command Post exists (via FillHand / discard purge).
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
            if (droneCard != null && IsPlayableNow(droneCard))
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
            EnsureSolarPrereqInHand();
            FillHandInternal();
            EnsureSolarPrereqInHand();
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
            EnsureSolarPrereqInHand();
            FillHandInternal();
            EnsureSolarPrereqInHand();
            OnHandChanged?.Invoke();
        }

        /// <summary>
        /// Climate/paired buildings (water, atmosphere, etc.) sit in hand while unmet even
        /// before their cluster has solar. That used to fill the hand and bury Solar in
        /// discard so the player could never power the pad. Whenever an open solar site
        /// exists, force Solar Panel back into the hand (make room if needed).
        /// </summary>
        private void EnsureSolarPrereqInHand()
        {
            if (IsSolarUnlockCardInHand()) return;

            BuildingSO solarSO = BlueprintDraftManager.GetBuildingSOByName("Solar Panel");
            if (solarSO == null) return;
            if (!ReservedSiteBuildUtility.CanBuildAtReservedSite(solarSO, Owner.Player1, out _, requireUnlocked: false))
                return;

            // Prefer injecting when a paired/climate unlock is already waiting on solar,
            // or whenever open solar pads exist and Solar is simply missing from hand.
            bool blockedPairedWaiting = hand.OfType<UnlockBuildingCardSO>().Any(NeedsSolarPoweredPad);
            if (!blockedPairedWaiting && hand.Count >= handSize)
            {
                // Hand full of playable cards — still try to seat Solar by swapping a
                // pad-blocked unlock if one exists; otherwise leave hand alone.
                return;
            }

            if (hand.Count >= handSize)
            {
                int dropIdx = hand.FindIndex(c =>
                    c is UnlockBuildingCardSO unlock
                    && !IsSolarUnlockCard(c)
                    && NeedsSolarPoweredPad(unlock));
                if (dropIdx < 0)
                {
                    dropIdx = hand.FindIndex(c =>
                        c is UnlockBuildingCardSO unlock
                        && !IsSolarUnlockCard(c)
                        && !unlock.CanApply());
                }

                if (dropIdx < 0) return;

                discardPile.Add(hand[dropIdx]);
                hand.RemoveAt(dropIdx);
                Debug.Log($"[CardDeckController] Made room for Solar Panel (hand was full of pad-blocked unlocks).");
            }

            EnsureBootstrapUnlockInHand("Solar");
        }

        private static bool IsSolarUnlockCard(BlueprintCardSO card)
        {
            return card is UnlockBuildingCardSO unlock
                && unlock.buildingToUnlock != null
                && BuildingSiteRegistry.IsSolarBuilding(unlock.buildingToUnlock);
        }

        private bool IsSolarUnlockCardInHand() => hand.Any(IsSolarUnlockCard);

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
        /// Move any hand cards that are no longer playable to discard.
        /// Call <see cref="RefreshHand"/> after builds, sector unlocks, and supply changes.
        /// </summary>
        public void DiscardUnplayableFromHand()
        {
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                var card = hand[i];
                if (ShouldKeepInHand(card)) continue;

                hand.RemoveAt(i);
                if (card != null) discardPile.Add(card);
                Debug.Log($"[CardDeckController] Discarded unplayable card '{card?.cardName}' from hand.");
            }
        }

        private static bool IsPlayableNow(BlueprintCardSO card)
        {
            return card != null && card.IsGateMet() && card.CanApply();
        }

        /// <summary>
        /// Cards eligible to enter the hand: playable now, or building unlocks waiting for a pad.
        /// </summary>
        private static bool IsDrawableNow(BlueprintCardSO card) => ShouldKeepInHand(card);

        /// <summary>
        /// Keep cards that are playable now, plus building unlocks waiting on a pad or
        /// materials when they advance an unmet sector terraforming goal.
        /// </summary>
        private static bool ShouldKeepInHand(BlueprintCardSO card)
        {
            if (card == null) return false;

            string sectorGoal = TerraformingGoalColors.GetSectorGoalForCard(card);
            if (!string.IsNullOrEmpty(sectorGoal)
                && GenerationManager.IsUnmetSectorGoal(sectorGoal)
                && card is UnlockBuildingCardSO unlock)
            {
                if (unlock.buildingToUnlock == null || unlock.buildingToUnlock.Prefab == null)
                    return false;
                if (card is TerraformingCardSO terra && !terra.PassesClimateRequirements())
                    return false;
                return true;
            }

            if (!card.IsGateMet()) return false;
            if (card.CanApply()) return true;
            return card is UnlockBuildingCardSO;
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
                    if (mgr != null && scouting.scoutingType == ScoutingCardSO.ScoutingType.OrbitalScan)
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
                else
                {
                    Debug.LogWarning($"[CardDeckController] Card '{played.cardName}' cannot be played right now.");
                }
                return;
            }

            Debug.Log($"[CardDeckController] Playing card: '{played.cardName}' (index {handIndex})");

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
                if (card == null) continue;
                drawPile.Add(card);
            }

            // Second pass: duplicate win-path cards without mutating shared assets.
            int extras = 0;
            foreach (var card in masterDeck)
            {
                if (card == null) continue;
                if (TerraformingGoalColors.GetSectorGoalForCard(card) == null) continue;

                BlueprintCardSO copy = UnityEngine.Object.Instantiate(card);
                copy.name = $"{card.name} (Sector Copy)";
                drawPile.Add(copy);
                extras++;
            }

            // Extra Solar Panel copies: infrastructure prerequisite for every paired
            // climate building. Sector-win doubling alone is not enough while POWER is
            // not the active milestone (Solar sits behind climate unlocks in FIFO).
            BlueprintCardSO solarTemplate = masterDeck.FirstOrDefault(c =>
                c is UnlockBuildingCardSO u
                && u.buildingToUnlock != null
                && BuildingSiteRegistry.IsSolarBuilding(u.buildingToUnlock));
            if (solarTemplate != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    BlueprintCardSO extraSolar = UnityEngine.Object.Instantiate(solarTemplate);
                    extraSolar.name = $"{solarTemplate.name} (Infra Copy {i + 1})";
                    drawPile.Add(extraSolar);
                    extras += 1;
                }
            }

            Debug.Log($"[CardDeckController] Draw pile ready: {drawPile.Count} cards " +
                      $"({masterDeck.Count} base + {extras} sector-win/infra duplicates).");
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
    }
}
