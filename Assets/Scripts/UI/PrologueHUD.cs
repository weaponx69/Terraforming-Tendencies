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
        TextMeshProUGUI weekReportText;
        TextMeshProUGUI hintText;
        TextMeshProUGUI timeLabel;
        Button endWeekButton;
        Button scanButton;
        Button pauseButton;
        Button slowButton;
        Button normalButton;
        Button fastButton;
        Button fasterButton;
        Image weekProgressFill;
        GameObject rootPanel;
        float reportClearAt;

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
            Instance = this;
            BuildUi();
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
            SuppressActsUi();
            ForceHideActsResourceChrome();

            if (PrologueController.Instance?.Colony != null)
                Refresh(PrologueController.Instance.Colony);

            UpdateWeekProgressBar();

            if (weekReportText != null && Time.unscaledTime > reportClearAt)
                weekReportText.text = "";
        }

        public void Refresh(PrologueColony colony)
        {
            if (colony == null || objectiveText == null) return;

            string obj = colony.ObjectiveText(out _, out bool wantDrone);
            objectiveText.text = $"PROLOGUE · Step {colony.PrologueStep}\n{obj}";

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
            comingUpText.text = coming.Length > 0 ? $"Coming up: {coming}" : "Coming up: —";

            int need = colony.O2Need();
            double runway = colony.O2Runway();
            string runwayStr = runway > 1000 ? "∞" : runway.ToString("0.0");
            airText.text =
                $"AIR {colony.Oxygen}  ·  need {need}/wk  ·  {runwayStr} wk left  ·  ice {colony.IceStock}  ·  beds {colony.TotalBeds()}/{colony.Colonists}"
                + FormatTypedStocks(colony);

            if (scanButton != null)
                scanButton.gameObject.SetActive(false);

            if (hintText != null)
            {
                var mission = PrologueSurveyMission.Instance;
                if (mission != null && mission.IsSearching)
                {
                    hintText.text =
                        $"Survey Team searching… day {Mathf.Min(mission.DaysElapsed + 0.01f, mission.DaysRequired):0.0}/{mission.DaysRequired} — hit 1x if paused.";
                }
                else if (wantDrone)
                {
                    hintText.text =
                        "DO THIS: Play Survey Team from your hand → paint up to 5 tiles toward a yellow ? → click green ✓ (takes 3 colony days).";
                }
                else
                {
                    hintText.text = BuildPlaceHint(colony);
                }
            }

            RefreshTimeControls();
        }

        public void RefreshTimeControls()
        {
            var clock = PrologueTimeLapse.Instance;
            if (timeLabel != null)
            {
                timeLabel.text = clock != null
                    ? $"TIME  {clock.SpeedLabel()}   week {(PrologueController.Instance?.Colony?.Week ?? 0)}"
                    : "TIME  —";
            }

            HighlightSpeedButton(PrologueTimeLapse.Speed.Pause, pauseButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Slow, slowButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Normal, normalButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Fast, fastButton);
            HighlightSpeedButton(PrologueTimeLapse.Speed.Faster, fasterButton);
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

        public void ShowWeekReport(List<string> log)
        {
            if (weekReportText == null) return;
            weekReportText.text = log == null || log.Count == 0
                ? "Week ended."
                : string.Join("\n", log);
            reportClearAt = Time.unscaledTime + 8f;
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

        static string BuildPlaceHint(PrologueColony colony)
        {
            if (colony == null) return "Place a card from the bottom-left hand on the ground.";
            return colony.PrologueStep switch
            {
                1 => "Play Water Ice Aquifer from your hand → click ground next to the ice.",
                2 => "Place Oxygen Processor, then Solar Array beside it for power.",
                3 => "Place two Habitat Domes before the shuttle arrives.",
                4 => "Play Survey Team toward the new yellow ?, then place another Aquifer next to the ice.",
                5 => "Place Geothermal (Wind) near Oxygen, then ride out the dust storm.",
                _ => "Space pauses weeks. Use the hand cards, or End Week to skip."
            };
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
            var bannerGo = new GameObject("Banner");
            bannerGo.transform.SetParent(rootPanel.transform, false);
            var bannerImg = bannerGo.AddComponent<Image>();
            bannerImg.color = new Color(0.04f, 0.06f, 0.09f, 0.9f);
            var banner = bannerGo.GetComponent<RectTransform>();
            banner.anchorMin = new Vector2(0f, 1f);
            banner.anchorMax = new Vector2(1f, 1f);
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(0f, -8f);
            banner.sizeDelta = new Vector2(-32f, 220f);

            objectiveText = MakeLabel(bannerGo.transform, "Objective", 18, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -10f), new Vector2(-210f, 64f));
            comingUpText = MakeLabel(bannerGo.transform, "ComingUp", 15, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -78f), new Vector2(-210f, 22f));
            airText = MakeLabel(bannerGo.transform, "Air", 15, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -102f), new Vector2(-210f, 22f));

            // Time-lapse row (Frostpunk-style) under AIR.
            timeLabel = MakeLabel(bannerGo.transform, "TimeLabel", 14, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -128f), new Vector2(-210f, 20f));

            // ASCII labels — TMP fonts often lack ❚ / ▶ glyphs.
            float speedY = -158f;
            float speedX = 48f;
            float speedStep = 58f;
            pauseButton = MakeSizedButton(bannerGo.transform, "II", new Vector2(0f, 1f),
                new Vector2(speedX, speedY), new Vector2(50f, 34f),
                () => PrologueTimeLapse.Instance?.TogglePause());
            slowButton = MakeSizedButton(bannerGo.transform, "0.5x", new Vector2(0f, 1f),
                new Vector2(speedX + speedStep, speedY), new Vector2(54f, 34f),
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Slow));
            normalButton = MakeSizedButton(bannerGo.transform, "1x", new Vector2(0f, 1f),
                new Vector2(speedX + speedStep * 2f, speedY), new Vector2(50f, 34f),
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Normal));
            fastButton = MakeSizedButton(bannerGo.transform, "2x", new Vector2(0f, 1f),
                new Vector2(speedX + speedStep * 3f, speedY), new Vector2(50f, 34f),
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Fast));
            fasterButton = MakeSizedButton(bannerGo.transform, "4x", new Vector2(0f, 1f),
                new Vector2(speedX + speedStep * 4f, speedY), new Vector2(50f, 34f),
                () => PrologueTimeLapse.Instance?.SetSpeed(PrologueTimeLapse.Speed.Faster));

            // Week progress bar under the speed buttons.
            var barBgGo = new GameObject("WeekProgressBg");
            barBgGo.transform.SetParent(bannerGo.transform, false);
            var barBg = barBgGo.AddComponent<Image>();
            barBg.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
            var barBgRt = barBg.rectTransform;
            barBgRt.anchorMin = new Vector2(0f, 1f);
            barBgRt.anchorMax = new Vector2(0f, 1f);
            barBgRt.pivot = new Vector2(0f, 1f);
            barBgRt.anchoredPosition = new Vector2(16f, -196f);
            barBgRt.sizeDelta = new Vector2(320f, 10f);

            var barFillGo = new GameObject("WeekProgressFill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            weekProgressFill = barFillGo.AddComponent<Image>();
            weekProgressFill.color = new Color(0.35f, 0.85f, 0.55f, 1f);
            var fillRt = weekProgressFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0f, 0.5f);

            endWeekButton = MakeButton(bannerGo.transform, "End Week", new Vector2(1f, 1f), new Vector2(-100f, -40f),
                () => PrologueController.Instance?.EndWeek());
            // Legacy button kept hidden — Survey Team is played from the hand card.
            scanButton = MakeButton(bannerGo.transform, "Survey Team", new Vector2(1f, 1f), new Vector2(-100f, -96f),
                BeginDroneScanFromUi);
            scanButton.gameObject.SetActive(false);

            // Hint ABOVE the card strip so it never covers the hand.
            hintText = MakeLabel(rootPanel.transform, "Hint", 17, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 268f), new Vector2(980f, 36f));
            hintText.color = new Color(1f, 0.92f, 0.55f, 1f);

            // Week report above the hand, left side — keep compact so it never eats the map.
            weekReportText = MakeLabel(rootPanel.transform, "WeekReport", 14, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 268f), new Vector2(420f, 56f));

            RefreshTimeControls();
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
            rt.pivot = new Vector2(anchorMin.x < 0.5f ? 0f : 0.5f, anchorMin.y > 0.5f ? 1f : 0f);
            if (anchorMin.x == 0f && anchorMax.x == 1f)
            {
                // Stretched width under banner: use offsets
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = new Vector2(16f, -anchoredPos.y - sizeDelta.y);
                rt.offsetMax = new Vector2(-200f, -anchoredPos.y);
                // anchoredPosition unused when offsets set this way for stretch; keep size via offsets
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

        static Button MakeSizedButton(
            Transform parent, string label, Vector2 anchor, Vector2 anchoredPos, Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label.Replace(" ", "") + "Button");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.42f, 0.62f, 0.98f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = size.x < 60f ? 13f : 18f;
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
    }
}
