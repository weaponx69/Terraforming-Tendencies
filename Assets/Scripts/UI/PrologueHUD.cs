using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Prologue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// On-screen Prologue chrome: top banner + bottom hint. Anchored to screen edges so it cannot scroll off.
    /// </summary>
    public class PrologueHUD : MonoBehaviour
    {
        public static PrologueHUD Instance { get; private set; }

        TextMeshProUGUI objectiveText;
        TextMeshProUGUI comingUpText;
        TextMeshProUGUI airText;
        TextMeshProUGUI statsText;
        TextMeshProUGUI weekReportText;
        TextMeshProUGUI hintText;
        TextMeshProUGUI timeLabel;
        TextMeshProUGUI fpsText;
        Button endWeekButton;
        Button scanButton;
        Button pauseButton;
        Button slowButton;
        Button normalButton;
        Button fastButton;
        Button fasterButton;
        Image weekProgressFill;
        ScrollRect statsScroll;
        RectTransform statsContent;
        GameObject rootPanel;
        GameObject bannerChromeGo;
        GameObject statsChromeGo;
        GameObject chromeChipGo;
        GameObject fpsChromeGo;
        Button chromeToggleButton;
        Button chipEndWeekButton;
        TextMeshProUGUI chromeToggleLabel;
        Button statsPanelToggleButton;
        TextMeshProUGUI statsPanelToggleLabel;
        GameObject statsScrollGo;
        bool chromeRetracted;
        bool statsDetailExpanded = true;
        PrologueBriefingPanel briefing;
        float reportClearAt;
        float nextHudRefreshTime;
        string lastStatsSnapshot;
        bool actsChromeSuppressed;
        const float HudRefreshMinInterval = 0.33f;
        static float StatsPanelWidth => PrologueUiLayout.StatsPanelWidth;
        static float BannerHeight => PrologueUiLayout.BannerHeight;
        const float StatsHeaderHeight = 92f;
        static float BannerRightReserve => PrologueUiLayout.StatsPanelWidth + PrologueUiLayout.BannerRightReserveExtra;
        const int HudLayoutVersion = 18;
        const float LeftHudInset = 22f;
        static float AboveCardDockY => PrologueUiLayout.AboveCardDockY;
        static int builtHudLayoutVersion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Object.FindAnyObjectByType<PrologueHUD>() != null) return;
            var go = new GameObject("PrologueHUD");
            DontDestroyOnLoad(go);
            go.AddComponent<PrologueHUD>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (builtHudLayoutVersion != HudLayoutVersion || rootPanel == null)
            {
                ClearBuiltUi();
                BuildUi();
                builtHudLayoutVersion = HudLayoutVersion;
            }
        }

        void ClearBuiltUi()
        {
            statsScroll = null;
            statsContent = null;
            statsScrollGo = null;
            statsPanelToggleButton = null;
            statsPanelToggleLabel = null;
            if (rootPanel != null)
            {
                Destroy(rootPanel);
                rootPanel = null;
            }
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!PrologueController.IsActive)
            {
                if (rootPanel != null) rootPanel.SetActive(false);
                return;
            }

            if (rootPanel != null) rootPanel.SetActive(true);
            if (fpsChromeGo != null) fpsChromeGo.SetActive(true);

            if (!actsChromeSuppressed)
            {
                actsChromeSuppressed = true;
                SuppressActsUi();
                ForceHideActsResourceChrome();
            }

            if (Time.unscaledTime >= nextHudRefreshTime)
            {
                nextHudRefreshTime = Time.unscaledTime + HudRefreshMinInterval;
                RefreshTimeControls();
                UpdateWeekProgressBar();
                var colony = PrologueController.Instance?.Colony;
                if (colony != null)
                    RefreshInternal(colony);
            }

            SyncChipEndWeekState();

            if (weekReportText != null && Time.unscaledTime > reportClearAt)
            {
                weekReportText.text = "";
                weekReportText.gameObject.SetActive(false);
            }
        }

        public void Refresh(PrologueColony colony) => Refresh(colony, force: false);

        /// <param name="force">Bypass throttle (prologue boot only).</param>
        public void Refresh(PrologueColony colony, bool force)
        {
            if (!force && Time.unscaledTime < nextHudRefreshTime) return;
            nextHudRefreshTime = Time.unscaledTime + HudRefreshMinInterval;
            RefreshInternal(colony);
        }

        void RefreshInternal(PrologueColony colony)
        {
            if (colony == null || objectiveText == null) return;

            string obj = colony.ObjectiveText(out PrologueBuildingId suggest, out bool wantDrone);
            bool won = !colony.ContractCancelled
                        && colony.PrologueStep >= PrologueData.PrologueSteps;
            objectiveText.text = PrologueUiText.SanitizeForTmp(colony.ContractCancelled
                ? "CONTRACT CANCELLED\n" + colony.ContractMessage
                : won
                    ? "PROLOGUE COMPLETE\nContract held. The corporation signs off on the opening mission."
                    : $"PROLOGUE - Step {colony.PrologueStep}\n{PrologueUiText.TrimBannerObjective(obj)}");
            if (endWeekButton != null)
                endWeekButton.interactable = !colony.ContractCancelled && !won;
            if (won)
                PrologueController.Instance?.TryCelebratePrologueVictory();

            var coming = new StringBuilder();
            if (!colony.ShuttleLanded && colony.ShuttleLandWeek >= 0)
            {
                int w = Mathf.Max(0, colony.ShuttleLandWeek - colony.Week);
                coming.Append($"Shuttle in {w} wk");
            }
            if (colony.StormLandWeek >= 0)
            {
                if (coming.Length > 0) coming.Append(" · ");
                int w = Mathf.Max(0, colony.StormLandWeek - colony.Week);
                coming.Append(colony.DustStormActive ? "DUST STORM" : $"Storm in {w} wk");
            }
            string comingLine = coming.Length > 0
                ? $"Coming up: {coming}"
                : "";
            if (comingUpText != null)
                comingUpText.gameObject.SetActive(false);

            int need = colony.O2Need();
            double runway = colony.O2Runway();
            string runwayStr = runway > 1000 ? "inf" : runway.ToString("0.0");
            string crew = colony.ColonistsWorking ? "crew working" : "crew stopped";
            var meters = new StringBuilder();
            meters.Append("SAT ").Append(colony.Satisfaction)
                .Append(" · Faith ").Append(colony.Faith)
                .Append(" · AIR ").Append(colony.Oxygen).Append('/').Append(need).Append(" wk")
                .Append(" · ").Append(runwayStr).Append(" wk left")
                .Append(" · beds ").Append(colony.TotalBeds()).Append('/').Append(colony.Colonists)
                .Append(" · ").Append(PowerStatus(colony));
            meters.Append(FormatTypedStocks(colony));
            if (!string.IsNullOrEmpty(comingLine))
                meters.Append("\n").Append(comingLine);
            airText.text = PrologueUiText.SanitizeForTmp(meters.ToString());

            if (statsText != null)
            {
                string activity = null;
                var mission = PrologueSurveyMission.Instance;
                if (mission != null && mission.IsSearching)
                    activity = $"Surveying · day {Mathf.Min(mission.DaysElapsed, mission.DaysRequired):0.0}/{mission.DaysRequired}";
                string body = colony.StatsText(activity);
                if (body != lastStatsSnapshot)
                {
                    lastStatsSnapshot = body;
                    statsText.text = PrologueUiText.SanitizeForTmp(body);
                    statsText.ForceMeshUpdate();
                    float bodyH = Mathf.Max(160f, statsText.preferredHeight + 8f);
                    var textRt = statsText.rectTransform;
                    if (textRt != null)
                        textRt.sizeDelta = new Vector2(-8f, bodyH);
                    if (statsContent != null)
                        statsContent.sizeDelta = new Vector2(0f, bodyH);
                    if (statsScroll != null)
                        statsScroll.verticalNormalizedPosition = 1f;
                }
            }

            if (scanButton != null)
                scanButton.gameObject.SetActive(false);

            if (hintText != null)
            {
                var mission = PrologueSurveyMission.Instance;
                if (mission != null && mission.IsSearching)
                {
                    bool paused = Time.timeScale <= 0.01f;
                    hintText.text = PrologueUiText.SanitizeForTmp(paused
                        ? $"Survey Team is in the field (day {Mathf.Min(mission.DaysElapsed + 0.01f, mission.DaysRequired):0.0}/{mission.DaysRequired}) — time is PAUSED. Hit 1x or Space to let the days run."
                        : $"Survey Team searching… day {Mathf.Min(mission.DaysElapsed + 0.01f, mission.DaysRequired):0.0}/{mission.DaysRequired}.");
                }
                else
                {
                    hintText.text = PrologueUiText.SanitizeForTmp(
                        PrologueGuidance.ActionHint(colony, out _, out _));
                }
            }

        }

        public bool BriefingBlocksClicks => briefing != null && briefing.IsBlocking;

        public void RefreshTimeControls()
        {
            var clock = PrologueTimeLapse.Instance;
            if (timeLabel != null)
            {
                timeLabel.text = PrologueUiText.SanitizeForTmp(clock != null
                    ? $"TIME  {clock.SpeedLabel()}   week {(PrologueController.Instance?.Colony?.Week ?? 0)}"
                    : "TIME  —");
            }

            if (fpsText != null)
            {
                int fps = ProloguePerformanceBudget.SmoothedFps();
                bool paused = clock != null && clock.IsPaused;
                fpsText.text = PrologueUiText.SanitizeForTmp(fps > 0
                    ? (paused ? $"PAUSED · {fps} fps" : $"FPS {fps}")
                    : (paused ? "PAUSED" : "FPS —"));
                fpsText.color = fps >= 45
                    ? new Color(0.55f, 0.95f, 0.65f, 1f)
                    : fps >= 30
                        ? new Color(0.95f, 0.88f, 0.45f, 1f)
                        : new Color(1f, 0.45f, 0.4f, 1f);
            }

            bool ended = clock != null && clock.RunEnded;
            SetSpeedInteractable(pauseButton, !ended);
            SetSpeedInteractable(slowButton, !ended);
            SetSpeedInteractable(normalButton, !ended);
            SetSpeedInteractable(fastButton, !ended);
            SetSpeedInteractable(fasterButton, !ended);
            if (endWeekButton != null && PrologueController.Instance?.Colony != null)
                endWeekButton.interactable = !PrologueController.Instance.Colony.ContractCancelled;

            HighlightSpeedButton(PrologueTimeLapse.Speed.Pause, pauseButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Slow, slowButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Normal, normalButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Fast, fastButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Faster, fasterButton);
        }

        static void SetSpeedInteractable(Button btn, bool interactable)
        {
            if (btn != null) btn.interactable = interactable;
        }

        void HighlightSpeedButton(PrologueTimeLapse.Speed speed, Button btn)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;
            bool on = PrologueTimeLapse.Instance != null && PrologueTimeLapse.Instance.CurrentSpeed == speed;
            img.color = on
                ? new Color(0.20f, 0.65f, 0.35f, 0.98f)
                : new Color(0.12f, 0.42f, 0.62f, 0.98f);
        }

        void UpdateWeekProgressBar()
        {
            if (weekProgressFill == null) return;
            var clock = PrologueTimeLapse.Instance;
            float p = clock != null ? clock.WeekProgress01 : 0f;
            weekProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
        }

        public bool ChromeRetracted => chromeRetracted;

        public void ToggleChromeRetracted() => SetChromeRetracted(!chromeRetracted);

        public void ToggleStatsDetail() => SetStatsDetailExpanded(!statsDetailExpanded);

        public void SetStatsDetailExpanded(bool expanded)
        {
            if (statsDetailExpanded == expanded && statsChromeGo != null) return;
            statsDetailExpanded = expanded;
            ApplyStatsDetailLayout();
        }

        void ApplyStatsDetailLayout()
        {
            if (statsChromeGo == null) return;
            var statsRt = statsChromeGo.GetComponent<RectTransform>();
            if (statsRt != null)
            {
                if (statsDetailExpanded)
                {
                    statsRt.anchorMin = new Vector2(1f, 0f);
                    statsRt.anchorMax = new Vector2(1f, 1f);
                    statsRt.pivot = new Vector2(1f, 1f);
                    statsRt.anchoredPosition = Vector2.zero;
                    statsRt.sizeDelta = Vector2.zero;
                    statsRt.offsetMin = new Vector2(-(StatsPanelWidth + 12f), PrologueUiLayout.StatsPanelBottomInset);
                    statsRt.offsetMax = new Vector2(-12f, -(BannerHeight + 12f));
                }
                else
                {
                    statsRt.anchorMin = statsRt.anchorMax = new Vector2(1f, 1f);
                    statsRt.pivot = new Vector2(1f, 1f);
                    statsRt.anchoredPosition = new Vector2(-12f, -(BannerHeight + 12f));
                    statsRt.sizeDelta = new Vector2(StatsPanelWidth, PrologueUiLayout.StatsPanelCollapsedHeight);
                }
            }
            if (statsScrollGo != null)
                statsScrollGo.SetActive(statsDetailExpanded);
            if (statsPanelToggleLabel != null)
                statsPanelToggleLabel.text = statsDetailExpanded ? "Less" : "More";
            if (endWeekButton != null)
            {
                var endRt = endWeekButton.GetComponent<RectTransform>();
                if (endRt != null)
                {
                    if (statsDetailExpanded)
                    {
                        endRt.anchorMin = endRt.anchorMax = new Vector2(1f, 1f);
                        endRt.pivot = new Vector2(1f, 1f);
                        endRt.anchoredPosition = new Vector2(-10f, -40f);
                        endRt.sizeDelta = new Vector2(176f, 46f);
                    }
                    else
                    {
                        endRt.anchorMin = endRt.anchorMax = new Vector2(1f, 0.5f);
                        endRt.pivot = new Vector2(1f, 0.5f);
                        endRt.anchoredPosition = new Vector2(-10f, 0f);
                        endRt.sizeDelta = new Vector2(196f, 42f);
                    }
                }
                var endLabel = endWeekButton.GetComponentInChildren<TextMeshProUGUI>();
                if (endLabel != null)
                    endLabel.fontSize = statsDetailExpanded ? 22f : 24f;
            }
        }

        public void SetChromeRetracted(bool retracted)
        {
            if (chromeRetracted == retracted && bannerChromeGo != null) return;
            chromeRetracted = retracted;
            ApplyChromeRetracted();
        }

        void ApplyChromeRetracted()
        {
            bool show = !chromeRetracted;
            if (bannerChromeGo != null) bannerChromeGo.SetActive(show);
            if (statsChromeGo != null) statsChromeGo.SetActive(show);
            if (hintText != null) hintText.gameObject.SetActive(show);
            if (chromeToggleLabel != null)
                chromeToggleLabel.text = chromeRetracted ? "Show UI" : "Hide UI";
            if (chromeChipGo != null)
            {
                var chipRt = chromeChipGo.GetComponent<RectTransform>();
                chipRt.anchorMin = chipRt.anchorMax = new Vector2(0f, 1f);
                chipRt.pivot = new Vector2(0f, 1f);
                chipRt.anchoredPosition = new Vector2(LeftHudInset, -(BannerHeight + 10f));
                chipRt.sizeDelta = chromeRetracted
                    ? new Vector2(296f, 40f)
                    : new Vector2(124f, 32f);
                LayoutChromeChipButtons();
                chromeChipGo.transform.SetAsLastSibling();
            }
            SyncChipEndWeekState();
        }

        void SyncChipEndWeekState()
        {
            if (chipEndWeekButton == null) return;
            var colony = PrologueController.Instance?.Colony;
            bool canEnd = colony != null && !colony.ContractCancelled
                && colony.PrologueStep < PrologueData.PrologueSteps;
            chipEndWeekButton.gameObject.SetActive(chromeRetracted && canEnd);
            chipEndWeekButton.interactable = canEnd;
        }

        void BuildChromeChip(Transform parent)
        {
            chromeChipGo = new GameObject("ChromeChip");
            chromeChipGo.transform.SetParent(parent, false);
            var chipImg = chromeChipGo.AddComponent<Image>();
            chipImg.color = new Color(0.03f, 0.05f, 0.08f, 0.82f);
            chipImg.raycastTarget = true;
            var chipRt = chromeChipGo.GetComponent<RectTransform>();
            chipRt.anchorMin = chipRt.anchorMax = new Vector2(0f, 1f);
            chipRt.pivot = new Vector2(0f, 1f);
            chipRt.anchoredPosition = new Vector2(LeftHudInset, -(BannerHeight + 10f));
            chipRt.sizeDelta = new Vector2(124f, 32f);

            chromeToggleButton = MakeSizedButton(chromeChipGo.transform, "Hide UI",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(116f, 34f),
                ToggleChromeRetracted, new Vector2(0f, 0.5f));
            chromeToggleLabel = chromeToggleButton.GetComponentInChildren<TextMeshProUGUI>();

            chipEndWeekButton = MakeSizedButton(chromeChipGo.transform, "End Week",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(122f, 0f), new Vector2(164f, 34f),
                () => PrologueController.Instance?.EndWeek(), new Vector2(0f, 0.5f));
            chipEndWeekButton.gameObject.SetActive(false);
            LayoutChromeChipButtons();
        }

        void LayoutChromeChipButtons()
        {
            if (chromeToggleButton == null) return;
            var toggleRt = chromeToggleButton.GetComponent<RectTransform>();
            if (chromeRetracted)
            {
                toggleRt.anchorMin = new Vector2(0f, 0.5f);
                toggleRt.anchorMax = new Vector2(0f, 0.5f);
                toggleRt.pivot = new Vector2(0f, 0.5f);
                toggleRt.anchoredPosition = new Vector2(4f, 0f);
                toggleRt.sizeDelta = new Vector2(116f, 34f);
                if (chipEndWeekButton != null)
                {
                    var endRt = chipEndWeekButton.GetComponent<RectTransform>();
                    endRt.anchorMin = endRt.anchorMax = new Vector2(0f, 0.5f);
                    endRt.pivot = new Vector2(0f, 0.5f);
                    endRt.anchoredPosition = new Vector2(122f, 0f);
                    endRt.sizeDelta = new Vector2(164f, 34f);
                }
            }
            else
            {
                toggleRt.anchorMin = Vector2.zero;
                toggleRt.anchorMax = Vector2.one;
                toggleRt.pivot = new Vector2(0.5f, 0.5f);
                toggleRt.offsetMin = new Vector2(4f, 3f);
                toggleRt.offsetMax = new Vector2(-4f, -3f);
                toggleRt.anchoredPosition = Vector2.zero;
            }
        }

        public void ShowOpeningBriefing()
        {
            SetChromeRetracted(false);
            if (PrologueController.Instance?.Colony != null)
                briefing?.PresentOpening(PrologueController.Instance.Colony);
        }

        public void ShowWeekReport(List<string> log)
        {
            SetChromeRetracted(false);
            if (weekReportText != null)
                weekReportText.text = "";
            if (PrologueController.Instance?.Colony != null)
                briefing?.Present(PrologueController.Instance.Colony, log);
        }

        /// <summary>A short line in the corner. Does not bring a person on screen.</summary>
        public void ShowNote(List<string> log)
        {
            if (weekReportText == null) return;
            weekReportText.text = PrologueUiText.SanitizeForTmp(log == null || log.Count == 0
                ? "Week ended."
                : string.Join("\n", log));
            reportClearAt = Time.unscaledTime + 8f;
            weekReportText.gameObject.SetActive(true);
        }

        public void BeginDroneScanFromUi()
        {
            var mission = PrologueSurveyMission.Instance
                          ?? new GameObject("PrologueSurveyMission").AddComponent<PrologueSurveyMission>();
            mission.BeginFromCard(PrologueSurveyMission.DefaultTileBudget, PrologueSurveyMission.DefaultDays);
        }

        static string FormatTypedStocks(PrologueColony colony)
        {
            if (colony == null) return "";
            var sb = new StringBuilder();
            foreach (var id in PrologueResourceIdUtil.All)
            {
                int n = colony.GetStock(id);
                if (n <= 0 && !colony.KnowsResource(id)) continue;
                sb.Append("  ·  ");
                sb.Append(PrologueResourceIdUtil.ToTypeName(id).ToLowerInvariant());
                sb.Append(' ');
                sb.Append(n);
            }
            return sb.ToString();
        }

        static string PowerStatus(PrologueColony colony)
        {
            if (colony == null) return "—";
            int processors = 0;
            int powered = 0;
            foreach (var t in colony.Board.Tiles)
            {
                if (t.Building != PrologueBuildingId.Oxygen || t.Broken) continue;
                processors++;
                if (colony.IsPowered(t.Index)) powered++;
            }
            if (processors == 0)
                return colony.SolarDarkened ? "storm (camp dark)" : $"camp · {colony.Energy} energy";
            if (colony.SolarDarkened)
                return powered > 0
                    ? $"grid · {colony.Energy} energy"
                    : "NONE — connect Wind within 2 tiles";
            return powered > 0
                ? $"camp · {colony.Energy} energy"
                : $"NONE · {colony.Energy} energy";
        }

        static void SuppressActsUi()
        {
            var weeks = Object.FindAnyObjectByType<Containers.WeeksLeftUI>(FindObjectsInactive.Include);
            if (weeks != null) weeks.gameObject.SetActive(false);
            var acts = Object.FindAnyObjectByType<Containers.ActiveObjectivesUI>(FindObjectsInactive.Include);
            if (acts != null) acts.gameObject.SetActive(false);
            var shop = Object.FindAnyObjectByType<BetweenActShopUI>(FindObjectsInactive.Include);
            if (shop != null) shop.gameObject.SetActive(false);

            // RuntimeUI also hides its chrome; this is a hard backup every frame.
        }

        static void ForceHideActsResourceChrome()
        {
            var runtime = Object.FindAnyObjectByType<RuntimeUI>(FindObjectsInactive.Include);
            if (runtime == null) return;
            Canvas canvas = runtime.GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : runtime.transform;
            string[] panels =
            {
                "Top Resource Bar", "Supplies Bar", "Active Objectives Panel", "Weeks Left Panel",
                "Generation Summary UI Controller", "Foundry Starvation UI", "Expansion Proposal UI",
                "Center Container Debug Box"
            };
            foreach (string name in panels)
            {
                Transform t = root.Find(name) ?? FindChildNamed(root, name);
                if (t != null && t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
            }
        }

        static Transform FindChildNamed(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindChildNamed(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("PrologueHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            rootPanel = new GameObject("Root");
            rootPanel.transform.SetParent(canvasGo.transform, false);
            var rootRt = rootPanel.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Top banner: stretch left-right, fixed height under the top edge.
            bannerChromeGo = new GameObject("Banner");
            bannerChromeGo.transform.SetParent(rootPanel.transform, false);
            var bannerGo = bannerChromeGo;
            var bannerImg = bannerGo.AddComponent<Image>();
            bannerImg.color = new Color(0.04f, 0.06f, 0.09f, 0.68f);
            bannerImg.raycastTarget = false;
            var banner = bannerGo.GetComponent<RectTransform>();
            banner.anchorMin = new Vector2(0f, 1f);
            banner.anchorMax = new Vector2(1f, 1f);
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(0f, -8f);
            banner.sizeDelta = new Vector2(-(BannerRightReserve + 12f), BannerHeight);

            objectiveText = MakeBannerRow(bannerGo.transform, "Objective", 19, 6f, 44f, true,
                TextOverflowModes.Truncate);
            comingUpText = MakeBannerRow(bannerGo.transform, "ComingUp", 14, 52f, 1f, false);
            comingUpText.gameObject.SetActive(false);
            airText = MakeBannerRow(bannerGo.transform, "Air", 14, 52f, 40f, false);
            timeLabel = MakeBannerRow(bannerGo.transform, "TimeLabel", 14, 94f, 16f, false);

            const float speedTop = 112f;
            const float speedStep = 48f;
            var speedSize = new Vector2(42f, 26f);
            pauseButton = MakeCornerButton(bannerGo.transform, "II", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(LeftHudInset, -speedTop), speedSize,
                () => PrologueTimeLapse.Instance?.TogglePause());
            slowButton = MakeCornerButton(bannerGo.transform, "0.5x", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(LeftHudInset + speedStep, -speedTop), speedSize,
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Slow));
            normalButton = MakeCornerButton(bannerGo.transform, "1x", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(LeftHudInset + speedStep * 2f, -speedTop), speedSize,
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Normal));
            fastButton = MakeCornerButton(bannerGo.transform, "2x", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(LeftHudInset + speedStep * 3f, -speedTop), speedSize,
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Fast));
            fasterButton = MakeCornerButton(bannerGo.transform, "4x", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(LeftHudInset + speedStep * 4f, -speedTop), speedSize,
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Faster));

            // Week progress bar under the speed buttons.
            var barBgGo = new GameObject("WeekProgressBg");
            barBgGo.transform.SetParent(bannerGo.transform, false);
            var barBg = barBgGo.AddComponent<Image>();
            barBg.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
            barBg.raycastTarget = false;
            var barBgRt = barBg.rectTransform;
            barBgRt.anchorMin = new Vector2(0f, 1f);
            barBgRt.anchorMax = new Vector2(0f, 1f);
            barBgRt.pivot = new Vector2(0f, 1f);
            barBgRt.anchoredPosition = new Vector2(LeftHudInset, -140f);
            barBgRt.sizeDelta = new Vector2(320f, 10f);

            var barFillGo = new GameObject("WeekProgressFill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            weekProgressFill = barFillGo.AddComponent<Image>();
            weekProgressFill.color = new Color(0.35f, 0.85f, 0.55f, 1f);
            weekProgressFill.raycastTarget = false;
            var fillRt = weekProgressFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0f, 0.5f);

            statsChromeGo = new GameObject("ColonyStats");
            statsChromeGo.transform.SetParent(rootPanel.transform, false);
            var statsGo = statsChromeGo;
            var statsImg = statsGo.AddComponent<Image>();
            statsImg.color = new Color(0.04f, 0.06f, 0.09f, 0.72f);
            statsImg.raycastTarget = false;
            var statsRt = statsGo.GetComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(1f, 0f);
            statsRt.anchorMax = new Vector2(1f, 1f);
            statsRt.pivot = new Vector2(1f, 1f);
            statsRt.offsetMin = new Vector2(-(StatsPanelWidth + 12f), PrologueUiLayout.StatsPanelBottomInset);
            statsRt.offsetMax = new Vector2(-12f, -(BannerHeight + 12f));

            statsPanelToggleButton = MakeCornerButton(statsGo.transform, "Less", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, -40f), new Vector2(84f, 38f), ToggleStatsDetail, new Vector2(0f, 1f));
            statsPanelToggleLabel = statsPanelToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (statsPanelToggleLabel != null)
                statsPanelToggleLabel.fontSize = 17f;

            endWeekButton = MakeCornerButton(statsGo.transform, "End Week", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-10f, -40f), new Vector2(176f, 46f),
                () => PrologueController.Instance?.EndWeek());
            var endWeekLabel = endWeekButton.GetComponentInChildren<TextMeshProUGUI>();
            if (endWeekLabel != null)
                endWeekLabel.fontSize = 22f;

            var scrollGo = new GameObject("StatsScroll");
            statsScrollGo = scrollGo;
            scrollGo.transform.SetParent(statsGo.transform, false);
            statsScroll = scrollGo.AddComponent<ScrollRect>();
            statsScroll.horizontal = false;
            statsScroll.vertical = true;
            statsScroll.movementType = ScrollRect.MovementType.Clamped;
            statsScroll.scrollSensitivity = 24f;
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(8f, 8f);
            scrollRt.offsetMax = new Vector2(-8f, -(StatsHeaderHeight + 4f));

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            statsScroll.viewport = viewportRt;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            statsContent = contentGo.AddComponent<RectTransform>();
            statsContent.anchorMin = new Vector2(0f, 1f);
            statsContent.anchorMax = new Vector2(1f, 1f);
            statsContent.pivot = new Vector2(0.5f, 1f);
            statsContent.anchoredPosition = Vector2.zero;
            statsContent.sizeDelta = new Vector2(0f, 280f);
            statsScroll.content = statsContent;

            var scrollBar = CreateStatsScrollbar(scrollGo.transform);
            statsScroll.verticalScrollbar = scrollBar;
            statsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            var statsLabelGo = new GameObject("Stats");
            statsLabelGo.transform.SetParent(contentGo.transform, false);
            statsText = statsLabelGo.AddComponent<TextMeshProUGUI>();
            statsText.fontSize = 20;
            statsText.lineSpacing = 6f;
            statsText.alignment = TextAlignmentOptions.TopLeft;
            statsText.color = Color.white;
            statsText.textWrappingMode = TextWrappingModes.Normal;
            statsText.raycastTarget = false;
            var statsTextRt = statsText.rectTransform;
            statsTextRt.anchorMin = new Vector2(0f, 1f);
            statsTextRt.anchorMax = new Vector2(1f, 1f);
            statsTextRt.pivot = new Vector2(0f, 1f);
            statsTextRt.anchoredPosition = Vector2.zero;
            statsTextRt.sizeDelta = new Vector2(-8f, 280f);
            // Legacy button kept hidden — Survey Team is played from the hand card.
            scanButton = MakeButton(bannerGo.transform, "Survey Team", new Vector2(1f, 1f), new Vector2(-100f, -96f),
                BeginDroneScanFromUi);
            scanButton.gameObject.SetActive(false);

            // Yellow "DO THIS" — above the card dock (was at y=268 and covered the hand).
            hintText = MakeLabel(rootPanel.transform, "Hint", 16, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            hintText.color = new Color(1f, 0.92f, 0.55f, 0.95f);
            hintText.overflowMode = TextOverflowModes.Overflow;
            hintText.enableAutoSizing = false;
            var hintRt = hintText.rectTransform;
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.offsetMin = new Vector2(LeftHudInset, AboveCardDockY);
            hintRt.offsetMax = new Vector2(-(StatsPanelWidth + 24f), AboveCardDockY + 54f);

            weekReportText = MakeLabel(rootPanel.transform, "WeekReport", 15, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, AboveCardDockY + 44f), new Vector2(480f, 40f));
            weekReportText.gameObject.SetActive(false);

            var briefingGo = new GameObject("Briefing");
            briefingGo.transform.SetParent(rootPanel.transform, false);
            briefing = briefingGo.AddComponent<PrologueBriefingPanel>();
            briefing.Build(rootPanel.transform);

            // FPS: own pill above the card strip — never under stats scroll / End Week.
            fpsChromeGo = new GameObject("FpsHud");
            fpsChromeGo.transform.SetParent(rootPanel.transform, false);
            var fpsGo = fpsChromeGo;
            var fpsBg = fpsGo.AddComponent<Image>();
            fpsBg.color = new Color(0.02f, 0.05f, 0.08f, 0.88f);
            fpsBg.raycastTarget = false;
            var fpsRt = fpsGo.GetComponent<RectTransform>();
            // Above the card dock (286px cards + ~48px bottom margin), not on the leftmost card.
            fpsRt.anchorMin = new Vector2(0f, 0f);
            fpsRt.anchorMax = new Vector2(0f, 0f);
            fpsRt.pivot = new Vector2(0f, 0f);
            fpsRt.anchoredPosition = new Vector2(StatsPanelWidth + 36f, AboveCardDockY);
            fpsRt.sizeDelta = new Vector2(132f, 28f);
            fpsText = MakePinnedLabel(fpsGo.transform, "FpsLabel", 16, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 24f));
            fpsText.fontStyle = FontStyles.Bold;

            BuildChromeChip(rootPanel.transform);
            chromeRetracted = false;
            statsDetailExpanded = true;
            ApplyStatsDetailLayout();
            ApplyChromeRetracted();

            RefreshTimeControls();
        }

        static TextMeshProUGUI MakeBannerRow(
            Transform parent, string name, float fontSize, float topInset, float height, bool bold,
            TextOverflowModes overflow = TextOverflowModes.Ellipsis)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = overflow;
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(LeftHudInset, -(topInset + height));
            rt.offsetMax = new Vector2(-BannerRightReserve, -topInset);
            return tmp;
        }

        static TextMeshProUGUI MakePinnedLabel(
            Transform parent, string name, float fontSize, TextAlignmentOptions align,
            Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = align switch
            {
                TextAlignmentOptions.TopRight => new Vector2(1f, 1f),
                TextAlignmentOptions.Center => new Vector2(0.5f, 0.5f),
                _ => new Vector2(0f, 1f)
            };
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return tmp;
        }

        static TextMeshProUGUI MakeLabel(
            Transform parent, string name, float fontSize, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, anchorMin.y > 0.5f ? 1f : 0f);
            if (anchorMin == anchorMax)
            {
                rt.pivot = align == TextAlignmentOptions.Center
                    ? new Vector2(0.5f, anchorMin.y)
                    : new Vector2(anchorMin.x, anchorMin.y > 0.5f ? 1f : 0f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = sizeDelta;
            }
            else
            {
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = sizeDelta;
            }
            return tmp;
        }

        static Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            return MakeSizedButton(parent, label, anchor, anchoredPos, new Vector2(170f, 42f), onClick);
        }

        static Scrollbar CreateStatsScrollbar(Transform parent)
        {
            var go = new GameObject("StatsScrollbar");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-2f, 0f);
            rt.sizeDelta = new Vector2(10f, 0f);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);
            var sb = go.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(go.transform, false);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(0.4f, 0.65f, 0.85f, 0.95f);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = new Vector2(2f, 2f);
            handleRt.offsetMax = new Vector2(-2f, -2f);
            sb.handleRect = handleRt;
            sb.targetGraphic = handleImg;
            return sb;
        }

        static Button MakeCornerButton(
            Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
            Vector2 size, UnityEngine.Events.UnityAction onClick, Vector2? pivot = null)
        {
            var p = pivot ?? (anchorMin == anchorMax && anchorMin.x >= 0.5f ? new Vector2(1f, 1f) : new Vector2(0f, 1f));
            return MakeSizedButton(parent, label, anchorMin, anchorMax, anchoredPos, size, onClick, p);
        }

        static Button MakeSizedButton(
            Transform parent, string label, Vector2 anchor, Vector2 anchoredPos, Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            return MakeSizedButton(parent, label, anchor, anchor, anchoredPos, size, onClick,
                new Vector2(0.5f, 0.5f));
        }

        static Button MakeSizedButton(
            Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size,
            UnityEngine.Events.UnityAction onClick, Vector2 pivot)
        {
            var go = new GameObject(label.Replace(" ", "") + "Button");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.42f, 0.62f, 0.98f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = Mathf.Clamp(size.y * 0.46f, 15f, 24f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var trt = tmp.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            return btn;
        }

        public bool TryGetBriefingContinueScreenPoint(out Vector2 screenBottomLeft)
        {
            screenBottomLeft = default;
            if (briefing == null || !briefing.IsBlocking) return false;
            return briefing.TryGetContinueScreenPoint(out screenBottomLeft);
        }

#if UNITY_EDITOR
        public void PlaytestDismissBriefing() => briefing?.Dismiss();

        public void PlaytestRebuildHud()
        {
            builtHudLayoutVersion = 0;
            ClearBuiltUi();
            BuildUi();
            builtHudLayoutVersion = HudLayoutVersion;
            chromeRetracted = false;
            statsDetailExpanded = true;
            ApplyStatsDetailLayout();
            ApplyChromeRetracted();
            var colony = PrologueController.Instance?.Colony;
            if (colony != null)
                Refresh(colony, force: true);
        }
#endif

        public bool TryGetEndWeekScreenPoint(out Vector2 screenBottomLeft) =>
            UiPlaythroughUtil.TryScreenCenter(endWeekButton, out screenBottomLeft);

        public bool TryGetSpeedButtonScreenPoint(string label, out Vector2 screenBottomLeft)
        {
            screenBottomLeft = default;
            Button btn = label switch
            {
                "1x" => normalButton,
                "2x" => fastButton,
                "4x" => fasterButton,
                "0.5x" => slowButton,
                "Pause" or "||" => pauseButton,
                _ => null
            };
            return UiPlaythroughUtil.TryScreenCenter(btn, out screenBottomLeft);
        }
    }
}
