using System;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Combolands-style run spine: Colony Score quotas within a week (card-play) budget.
    /// Sectors and climate tickers are not the win meter.
    /// </summary>
    public class ColonyActManager : MonoBehaviour
    {
        public static ColonyActManager Instance { get; private set; }

        public static event Action OnActStateChanged;
        public static event Action<int> OnActCleared; // act index 1-based
        public static event Action OnRunVictory;
        public static event Action OnActFailed;

        public struct ActDef
        {
            public string Name;
            public int TargetScore;
            public int WeekBudget;
        }

        private static readonly ActDef[] Acts =
        {
            new ActDef { Name = "Survive", TargetScore = 40, WeekBudget = 8 },
            new ActDef { Name = "Settle", TargetScore = 120, WeekBudget = 8 },
            new ActDef { Name = "Habitable", TargetScore = 280, WeekBudget = 10 },
            new ActDef { Name = "Thrive", TargetScore = 500, WeekBudget = 10 },
        };

        /// <summary>Habitability points needed for full Living look (cumulative).</summary>
        private const float HabitabilityForLiving = 80f;
        private const float ScoreCarryFraction = 0.25f;

        private int actIndex; // 0-based
        private int colonyScore;
        private int weeksRemaining;
        private float habitability;
        private bool runEnded;
        private bool started;

        public int CurrentAct => actIndex + 1;
        public int TotalActs => Acts.Length;
        public string CurrentActName => Acts[Mathf.Clamp(actIndex, 0, Acts.Length - 1)].Name;
        public int ColonyScore => colonyScore;
        public int TargetScore => Acts[Mathf.Clamp(actIndex, 0, Acts.Length - 1)].TargetScore;
        public int WeeksRemaining => weeksRemaining;
        public float Habitability => habitability;
        public float HabitabilityProgress => Mathf.Clamp01(habitability / HabitabilityForLiving);
        public bool IsRunEnded => runEnded;
        public bool IsBetweenActs { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(ColonyActManager));
            go.AddComponent<ColonyActManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += HandlePlanetGenerated;
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= HandlePlanetGenerated;
        }

        private void Start()
        {
            if (!started && PlanetGenerator.Instance != null)
                BeginRun();
        }

        private void HandlePlanetGenerated() => BeginRun();

        public void BeginRun()
        {
            actIndex = 0;
            colonyScore = 0;
            habitability = 0f;
            runEnded = false;
            IsBetweenActs = false;
            weeksRemaining = Acts[0].WeekBudget;
            started = true;
            Debug.Log($"[ColonyActManager] Act 1 {Acts[0].Name}: score 0/{Acts[0].TargetScore}, weeks {weeksRemaining}");
            OnActStateChanged?.Invoke();
            ClimateVisualStages.Instance?.NotifyHabitabilityChanged();
        }

        /// <summary>Spend one week when a hand card is committed (played / placed).</summary>
        public void SpendWeek()
        {
            if (!started || runEnded || IsBetweenActs) return;
            if (weeksRemaining <= 0) return;

            weeksRemaining--;
            Debug.Log($"[ColonyActManager] Week spent — {weeksRemaining} left (score {colonyScore}/{TargetScore})");
            OnActStateChanged?.Invoke();

            if (colonyScore >= TargetScore)
            {
                ClearCurrentAct();
                return;
            }

            if (weeksRemaining <= 0)
            {
                FailCurrentAct();
            }
        }

        /// <summary>Grant score when a building finishes (or a non-build card resolves).</summary>
        public void GrantTileScore(BuildingSO building)
        {
            if (!started || runEnded || IsBetweenActs) return;
            GetTileValues(building, out int score, out float hab, out string tag);
            if (score <= 0 && hab <= 0f) return;

            colonyScore += score;
            habitability += hab;
            Debug.Log($"[ColonyActManager] +{score} score ({tag}) → {colonyScore}/{TargetScore}; hab={habitability:F0}");
            OnActStateChanged?.Invoke();
            ClimateVisualStages.Instance?.NotifyHabitabilityChanged();

            if (colonyScore >= TargetScore)
                ClearCurrentAct();
        }

        public void GrantCardScore(BlueprintCardSO card)
        {
            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
            {
                // Building score is granted on CompleteConstruction to avoid double-counting.
                return;
            }

            // Non-building cards: small survival score.
            if (!started || runEnded || IsBetweenActs) return;
            colonyScore += 2;
            OnActStateChanged?.Invoke();
            if (colonyScore >= TargetScore)
                ClearCurrentAct();
        }

        private void ClearCurrentAct()
        {
            if (IsBetweenActs || runEnded) return;
            IsBetweenActs = true;

            int cleared = CurrentAct;
            Debug.Log($"[ColonyActManager] Act {cleared} ({CurrentActName}) cleared!");
            OnActCleared?.Invoke(cleared);

            if (actIndex >= Acts.Length - 1)
            {
                runEnded = true;
                OnRunVictory?.Invoke();
                if (GenerationManager.Instance != null)
                    GenerationManager.Instance.NotifyColonyActVictory();
                else if (GameOverManager.Instance != null)
                    GameOverManager.Instance.TriggerVictory();
                OnActStateChanged?.Invoke();
                return;
            }

            // Carry a fraction of excess into the next act's starting score.
            int excess = Mathf.Max(0, colonyScore - TargetScore);
            int carried = Mathf.RoundToInt(colonyScore * ScoreCarryFraction) + excess;
            actIndex++;
            colonyScore = carried;
            weeksRemaining = Acts[actIndex].WeekBudget;
            IsBetweenActs = false;

            Debug.Log($"[ColonyActManager] Act {CurrentAct} {CurrentActName}: start score {colonyScore}/{TargetScore}, weeks {weeksRemaining}");
            OnActStateChanged?.Invoke();

            if (colonyScore >= TargetScore)
                ClearCurrentAct();
        }

        private void FailCurrentAct()
        {
            if (runEnded) return;
            runEnded = true;
            Debug.Log($"[ColonyActManager] Act {CurrentAct} failed — weeks exhausted (score {colonyScore}/{TargetScore}).");
            OnActFailed?.Invoke();
            OnActStateChanged?.Invoke();
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.TriggerGameOver(GameOverManager.GameOverReason.Resources);
        }

        public static void GetTileValues(BuildingSO building, out int baseScore, out float habitabilityGain, out string tag)
        {
            baseScore = 5;
            habitabilityGain = 0f;
            tag = "Tile";

            if (building == null) return;

            string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);
            string name = building.Name ?? string.Empty;

            switch (goal)
            {
                case "COMMAND POST":
                    baseScore = 12;
                    tag = "Anchor";
                    break;
                case "POWER":
                    baseScore = 4;
                    tag = "Power";
                    break;
                case "MATERIALS":
                    baseScore = 8;
                    tag = "Industry";
                    break;
                case "POPULATION":
                    baseScore = 10;
                    tag = "Anchor";
                    break;
                case "TEMPERATURE":
                    baseScore = 10;
                    habitabilityGain = 8f;
                    tag = "Heat";
                    break;
                case "ATMOSPHERE":
                    baseScore = 10;
                    habitabilityGain = 8f;
                    tag = "Air";
                    break;
                case "WATER":
                    baseScore = 10;
                    habitabilityGain = 8f;
                    tag = "Water";
                    break;
                case "OXYGEN":
                    baseScore = 6;
                    habitabilityGain = 3f;
                    tag = "Life";
                    break;
                default:
                    if (name.IndexOf("drone", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        baseScore = 3;
                        tag = "Labor";
                    }
                    else
                    {
                        baseScore = 5;
                        tag = "Tile";
                    }
                    break;
            }
        }

        public string BuildObjectivesText()
        {
            if (!started)
                return "<color=#C8D0D8>Waiting for planet…</color>";

            if (runEnded && colonyScore >= TargetScore && actIndex >= Acts.Length - 1)
                return "<color=#7CFF9A><b>Colony thrives — victory!</b></color>";

            if (runEnded)
                return "<color=#FF8A8A><b>Act failed — weeks exhausted.</b></color>";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#8FE7FF><b>Act {CurrentAct}/{TotalActs} — {CurrentActName}</b></color>");
            sb.AppendLine($"<color=#C8D0D8>Reach Colony Score before weeks run out.</color>");
            sb.AppendLine();

            bool scoreMet = colonyScore >= TargetScore;
            string scoreColor = scoreMet ? "#7CFF9A" : "#FFE08A";
            sb.AppendLine($"<color={scoreColor}>SCORE  {colonyScore} / {TargetScore}</color>");

            string weekColor = weeksRemaining <= 2 ? "#FF8A8A" : "#C8D0D8";
            sb.AppendLine($"<color={weekColor}>WEEKS  {weeksRemaining}</color>");
            sb.AppendLine($"<color=#A8B0B8>Habitability look  {HabitabilityProgress:P0}</color>");
            return sb.ToString();
        }
    }
}
