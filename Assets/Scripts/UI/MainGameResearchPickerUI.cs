using System.Collections.Generic;
using GameDevTV.RTS.MainGame;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Research card overlay — no pause menu; pick a project and OK to close.
    /// </summary>
    public class MainGameResearchPickerUI : MonoBehaviour
    {
        public static MainGameResearchPickerUI Instance { get; private set; }

        GameObject panel;
        TextMeshProUGUI headerLabel;
        TextMeshProUGUI hintLabel;
        readonly List<MainGameResearchId> rowIds = new();
        readonly List<Button> rowButtons = new();
        readonly List<TextMeshProUGUI> rowLabels = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureHook()
        {
            if (MainGameHUD.Instance != null)
                MainGameHUD.Instance.EnsureResearchPicker();
        }

        public void Build(Transform parent)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            panel = new GameObject("ResearchPickerPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.1f, 0.96f);
            bg.raycastTarget = true;

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(480f, 520f);
            rt.anchoredPosition = Vector2.zero;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(20, 20, 18, 18);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            headerLabel = MakeLabel(panel.transform, "Choose research", 22f, FontStyles.Bold);
            hintLabel = MakeLabel(panel.transform, "", 15f, FontStyles.Italic);
            hintLabel.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            AddRow(MainGameResearchId.None, tier1: true);
            AddRow(MainGameResearchId.FasterBuild, tier1: false);
            AddRow(MainGameResearchId.LongerPowerReach, tier1: false);
            AddRow(MainGameResearchId.ExtraScanType, tier1: false);
            AddRow(MainGameResearchId.ExtendedSurvey, tier1: false);

            var okGo = new GameObject("OK", typeof(RectTransform), typeof(Image), typeof(Button));
            okGo.transform.SetParent(panel.transform, false);
            var okLe = okGo.AddComponent<LayoutElement>();
            okLe.minHeight = 48f;
            okLe.preferredHeight = 52f;
            var okImg = okGo.GetComponent<Image>();
            okImg.color = new Color(0.15f, 0.45f, 0.28f, 1f);
            var okBtn = okGo.GetComponent<Button>();
            okBtn.targetGraphic = okImg;
            var okLabel = MakeLabel(okGo.transform, "OK", 20f, FontStyles.Bold);
            okLabel.alignment = TextAlignmentOptions.Center;
            Stretch(okLabel.rectTransform);
            okBtn.onClick.AddListener(Close);

            panel.SetActive(false);
        }

        void AddRow(MainGameResearchId id, bool tier1)
        {
            var row = new GameObject(tier1 ? "Tier1" : id.ToString(), typeof(RectTransform));
            row.transform.SetParent(panel.transform, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 48f;

            var img = row.AddComponent<Image>();
            img.color = new Color(0.11f, 0.14f, 0.2f, 0.98f);
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = img;

            var label = MakeLabel(row.transform, "", 16f, FontStyles.Normal);
            Stretch(label.rectTransform, 10f, 6f);

            rowIds.Add(id);
            rowButtons.Add(btn);
            rowLabels.Add(label);

            int capture = rowIds.Count - 1;
            btn.onClick.AddListener(() => OnRowClicked(capture));
        }

        public void Open()
        {
            if (panel == null) return;
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        void Refresh()
        {
            var research = MainGameController.Instance?.Colony?.Research;
            if (research == null) return;

            bool canStart = research.Active == MainGameResearchId.None;
            if (hintLabel != null)
            {
                hintLabel.text = canStart
                    ? "Tap a project to begin. OK closes this panel — time keeps running."
                    : $"In progress: {MainGameResearch.DisplayName(research.Active)} — {research.WeeksRemaining} wk left";
            }

            float overDays = research.SurveyOverageDaysPerHex();
            if (headerLabel != null)
                headerLabel.text = $"Research  (survey overage: {overDays:0.##} day/hex)";

            for (int i = 0; i < rowIds.Count; i++)
            {
                var id = rowIds[i];
                var label = rowLabels[i];
                var btn = rowButtons[i];
                if (label == null || btn == null) continue;

                if (id == MainGameResearchId.None)
                {
                    label.text = "Connected Power (Tier 1) — unlocked";
                    label.color = new Color(0.55f, 0.95f, 0.65f, 1f);
                    btn.interactable = false;
                    continue;
                }

                int weeks = MainGameData.ResearchWeeks(id);
                string name = MainGameResearch.DisplayName(id);
                if (id == MainGameResearchId.ExtendedSurvey)
                    name += " — cheaper survey overage";

                if (research.IsUnlocked(id))
                {
                    label.text = $"{name} — complete";
                    label.color = new Color(0.55f, 0.95f, 0.65f, 1f);
                    btn.interactable = false;
                }
                else if (research.Active == id)
                {
                    label.text = $"{name} — in progress ({research.WeeksRemaining} wk)";
                    label.color = new Color(0.95f, 0.85f, 0.4f, 1f);
                    btn.interactable = false;
                }
                else
                {
                    label.text = $"{name} — {weeks} wk";
                    label.color = new Color(0.82f, 0.86f, 0.94f, 1f);
                    btn.interactable = canStart;
                }
            }
        }

        void OnRowClicked(int index)
        {
            if (index < 0 || index >= rowIds.Count) return;
            var id = rowIds[index];
            if (id == MainGameResearchId.None) return;

            var colony = MainGameController.Instance?.Colony;
            if (colony?.Research == null) return;

            if (!colony.Research.TryBegin(id, out var reason))
            {
                MainGameHUD.Instance?.ShowNote(new List<string> { reason ?? "Cannot research" });
                Refresh();
                return;
            }

            CardDeckController.Instance?.ConsumeResearchCardFromHand();
            MainGameHUD.Instance?.ShowNote(new List<string>
            {
                $"Research started: {MainGameResearch.DisplayName(id)} ({MainGameData.ResearchWeeks(id)} wk)."
            });
            Refresh();
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string text, float size, FontStyles style)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 8f;
            le.preferredHeight = size + 12f;
            return tmp;
        }

        static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}