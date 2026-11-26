using System.Collections.Generic;
using GameDevTV.RTS.Prologue;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// A person reports the week: what the crew moved, what they need next,
    /// and a full-screen cutscene when the contract is in real trouble.
    /// </summary>
    public class PrologueBriefingPanel : MonoBehaviour
    {
        static float WeekBriefingBottomInset => PrologueUiLayout.AboveCardDockY + 10f;

        GameObject card;
        GameObject cutscene;
        Image portraitSuit;
        Image portraitHead;
        Image portraitVisor;
        Image cutsceneSuit;
        Image cutsceneHead;
        Image cutsceneVisor;
        TextMeshProUGUI nameText;
        TextMeshProUGUI bodyText;
        TextMeshProUGUI cutsceneName;
        TextMeshProUGUI cutsceneBody;
        TextMeshProUGUI cardContinueLabel;
        TextMeshProUGUI cutsceneContinueLabel;
        RectTransform cardContinueButton;
        RectTransform cutsceneContinueButton;
        PrologueWeekMemory memory;
        bool memoryReady;
        bool gameOver;
        ScrollRect cardBodyScroll;
        ScrollRect crisisBodyScroll;
        static Sprite circle;

        public void Build(Transform parent)
        {
            card = MakeCard(parent, false, out portraitSuit, out portraitHead, out portraitVisor, out nameText, out bodyText, out cardContinueLabel, out cardContinueButton);
            cutscene = MakeCard(parent, true, out cutsceneSuit, out cutsceneHead, out cutsceneVisor, out cutsceneName, out cutsceneBody, out cutsceneContinueLabel, out cutsceneContinueButton);
            card.SetActive(false);
            cutscene.SetActive(false);
        }

        public void PresentOpening(PrologueColony colony)
        {
            if (colony == null || card == null) return;
            Show(colony, PrologueWeekBriefing.Opening(colony));
        }

        public void Present(PrologueColony colony, List<string> log)
        {
            if (colony == null || card == null) return;
            var before = memoryReady
                ? memory
                : new PrologueWeekMemory
                {
                    Satisfaction = colony.Satisfaction,
                    Faith = colony.Faith,
                    Oxygen = colony.Oxygen,
                    Step = colony.PrologueStep,
                    Storm = colony.DustStormActive
                };
            Show(colony, PrologueWeekBriefing.Compose(colony, log, before));
        }

        void Show(PrologueColony colony, PrologueWeekBriefing briefing)
        {
            memory = new PrologueWeekMemory
            {
                Satisfaction = colony.Satisfaction,
                Faith = colony.Faith,
                Oxygen = colony.Oxygen,
                Step = colony.PrologueStep,
                Storm = colony.DustStormActive
            };
            memoryReady = true;

            bool crisis = briefing.Tone == PrologueBriefingTone.Crisis;
            card.SetActive(!crisis);
            cutscene.SetActive(crisis);
            ApplyPortrait(crisis ? cutsceneSuit : portraitSuit, crisis ? cutsceneHead : portraitHead, crisis ? cutsceneVisor : portraitVisor, briefing.Tone);
            TextMeshProUGUI name = crisis ? cutsceneName : nameText;
            TextMeshProUGUI body = crisis ? cutsceneBody : bodyText;
            name.text = PrologueUiText.SanitizeForTmp($"{briefing.Speaker}\n{briefing.Role}");
            body.text = PrologueUiText.SanitizeForTmp(briefing.Speech);
            ResizeSpeechContent(body, crisis ? crisisBodyScroll : cardBodyScroll);
            gameOver = colony.ContractCancelled;
            transform.SetAsLastSibling();
            string buttonLabel = gameOver ? "New game" : "Continue";
            if (cardContinueLabel != null) cardContinueLabel.text = buttonLabel;
            if (cutsceneContinueLabel != null) cutsceneContinueLabel.text = buttonLabel;
            if (gameOver)
                PrologueTimeLapse.Instance?.EndRun();
            else
                PrologueTimeLapse.Instance?.HoldForBriefing(true);
        }

        public bool IsBlocking => (card != null && card.activeSelf) || (cutscene != null && cutscene.activeSelf);

        public bool TryGetContinueScreenPoint(out Vector2 screenBottomLeft)
        {
            screenBottomLeft = default;
            RectTransform rt = cutscene != null && cutscene.activeSelf
                ? cutsceneContinueButton
                : card != null && card.activeSelf ? cardContinueButton : null;
            if (rt == null) return false;
            return UiPlaythroughUtil.TryScreenCenter(rt, out screenBottomLeft);
        }

        public void Dismiss()
        {
            if (gameOver) return;
            if (card != null) card.SetActive(false);
            if (cutscene != null) cutscene.SetActive(false);
            PrologueTimeLapse.Instance?.HoldForBriefing(false);
        }

        void OnContinue()
        {
            if (!gameOver)
            {
                Dismiss();
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        GameObject MakeCard(
            Transform parent,
            bool fullScreen,
            out Image suit,
            out Image head,
            out Image visor,
            out TextMeshProUGUI name,
            out TextMeshProUGUI body,
            out TextMeshProUGUI continueLabel,
            out RectTransform continueButton)
        {
            continueButton = null;
            var root = new GameObject(fullScreen ? "CrisisCutscene" : "WeekBriefing");
            root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            if (fullScreen)
            {
                var dim = new GameObject("Dim");
                dim.transform.SetParent(root.transform, false);
                var dimImg = dim.AddComponent<Image>();
                dimImg.color = new Color(0.02f, 0.03f, 0.05f, 0.82f);
                var dimRt = dim.GetComponent<RectTransform>();
                dimRt.anchorMin = Vector2.zero;
                dimRt.anchorMax = Vector2.one;
                dimRt.offsetMin = Vector2.zero;
                dimRt.offsetMax = Vector2.zero;
            }

            var panel = new GameObject("Card");
            panel.transform.SetParent(root.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = fullScreen
                ? new Color(0.08f, 0.09f, 0.12f, 0.98f)
                : new Color(0.05f, 0.07f, 0.1f, 0.96f);
            var panelRt = panel.GetComponent<RectTransform>();
            if (fullScreen)
            {
                panelRt.anchorMin = new Vector2(0.5f, 0.5f);
                panelRt.anchorMax = new Vector2(0.5f, 0.5f);
                panelRt.pivot = new Vector2(0.5f, 0.5f);
                panelRt.anchoredPosition = Vector2.zero;
                panelRt.sizeDelta = new Vector2(820f, 360f);
            }
            else
            {
                panelRt.anchorMin = new Vector2(0.5f, 0f);
                panelRt.anchorMax = new Vector2(0.5f, 0f);
                panelRt.pivot = new Vector2(0.5f, 0f);
                panelRt.anchoredPosition = new Vector2(0f, WeekBriefingBottomInset);
                panelRt.sizeDelta = new Vector2(820f, 252f);
            }

            var portrait = new GameObject("Portrait");
            portrait.transform.SetParent(panel.transform, false);
            suit = portrait.AddComponent<Image>();
            var portraitRt = portrait.GetComponent<RectTransform>();
            if (fullScreen)
            {
                portraitRt.anchorMin = new Vector2(0f, 0.5f);
                portraitRt.anchorMax = new Vector2(0f, 0.5f);
                portraitRt.pivot = new Vector2(0f, 0.5f);
                portraitRt.anchoredPosition = new Vector2(16f, 0f);
            }
            else
            {
                portraitRt.anchorMin = new Vector2(0f, 1f);
                portraitRt.anchorMax = new Vector2(0f, 1f);
                portraitRt.pivot = new Vector2(0f, 1f);
                portraitRt.anchoredPosition = new Vector2(16f, -10f);
            }
            portraitRt.sizeDelta = fullScreen ? new Vector2(150f, 210f) : new Vector2(92f, 128f);

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(portrait.transform, false);
            head = headGo.AddComponent<Image>();
            head.sprite = CircleSprite();
            var headRt = head.rectTransform;
            headRt.anchorMin = new Vector2(0.5f, 1f);
            headRt.anchorMax = new Vector2(0.5f, 1f);
            headRt.pivot = new Vector2(0.5f, 1f);
            headRt.anchoredPosition = new Vector2(0f, -8f);
            headRt.sizeDelta = fullScreen ? new Vector2(86f, 86f) : new Vector2(52f, 52f);

            var visorGo = new GameObject("Visor");
            visorGo.transform.SetParent(headGo.transform, false);
            visor = visorGo.AddComponent<Image>();
            var visorRt = visor.rectTransform;
            visorRt.anchorMin = new Vector2(0.5f, 0.5f);
            visorRt.anchorMax = new Vector2(0.5f, 0.5f);
            visorRt.sizeDelta = fullScreen ? new Vector2(64f, 16f) : new Vector2(38f, 10f);

            name = MakeAnchoredText(panel.transform, "Name", fullScreen ? 26f : 21f, FontStyles.Bold,
                fullScreen
                    ? (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(190f, -72f), new Vector2(-24f, -10f))
                    : (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(112f, -48f), new Vector2(-16f, -8f)));
            body = MakeSpeechScrollBody(panel.transform, fullScreen, out ScrollRect bodyScroll);
            if (fullScreen)
                crisisBodyScroll = bodyScroll;
            else
                cardBodyScroll = bodyScroll;
            if (bodyScroll != null)
                bodyScroll.verticalNormalizedPosition = 1f;

            var buttonGo = new GameObject("Continue");
            buttonGo.transform.SetParent(panel.transform, false);
            var buttonImg = buttonGo.AddComponent<Image>();
            buttonImg.color = new Color(0.16f, 0.38f, 0.55f, 1f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImg;
            button.onClick.AddListener(OnContinue);
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(1f, 0f);
            buttonRt.anchorMax = new Vector2(1f, 0f);
            buttonRt.pivot = new Vector2(1f, 0f);
            buttonRt.anchoredPosition = new Vector2(-16f, 12f);
            buttonRt.sizeDelta = new Vector2(140f, 36f);
            continueButton = buttonRt;
            var buttonLabel = MakeText(buttonGo.transform, "Label", 20f, FontStyles.Bold, Vector2.zero, new Vector2(140f, 36f));
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.text = "Continue";
            continueLabel = buttonLabel;
            var labelRt = buttonLabel.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            return root;
        }

        static void ApplyPortrait(Image suit, Image head, Image visor, PrologueBriefingTone tone)
        {
            Color suitColor;
            Color skin;
            Color glass;
            if (tone == PrologueBriefingTone.Crisis)
            {
                suitColor = new Color(0.16f, 0.18f, 0.24f, 1f);
                skin = new Color(0.78f, 0.74f, 0.7f, 1f);
                glass = new Color(0.9f, 0.22f, 0.22f, 1f);
            }
            else if (tone == PrologueBriefingTone.Concern)
            {
                suitColor = new Color(0.48f, 0.28f, 0.16f, 1f);
                skin = new Color(0.86f, 0.64f, 0.48f, 1f);
                glass = new Color(0.95f, 0.55f, 0.25f, 1f);
            }
            else
            {
                suitColor = new Color(0.2f, 0.4f, 0.38f, 1f);
                skin = new Color(0.93f, 0.78f, 0.62f, 1f);
                glass = new Color(0.45f, 0.86f, 0.95f, 1f);
            }
            if (suit != null) suit.color = suitColor;
            if (head != null) head.color = skin;
            if (visor != null) visor.color = glass;
        }

        static TextMeshProUGUI MakeSpeechScrollBody(Transform panel, bool fullScreen, out ScrollRect scroll)
        {
            scroll = null;
            var scrollGo = new GameObject("SpeechScroll");
            scrollGo.transform.SetParent(panel, false);
            scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            if (fullScreen)
            {
                scrollRt.anchorMin = new Vector2(0f, 0f);
                scrollRt.anchorMax = new Vector2(1f, 1f);
                scrollRt.offsetMin = new Vector2(190f, 52f);
                scrollRt.offsetMax = new Vector2(-24f, -78f);
            }
            else
            {
                scrollRt.anchorMin = new Vector2(0f, 0f);
                scrollRt.anchorMax = new Vector2(1f, 1f);
                scrollRt.offsetMin = new Vector2(112f, 52f);
                scrollRt.offsetMax = new Vector2(-16f, -54f);
            }

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            scroll.viewport = viewportRt;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 120f);
            scroll.content = contentRt;

            var body = MakeText(contentGo.transform, "Speech", fullScreen ? 22f : 19f, FontStyles.Normal,
                Vector2.zero, new Vector2(0f, 120f));
            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.offsetMin = Vector2.zero;
            body.rectTransform.offsetMax = Vector2.zero;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            return body;
        }

        static void ResizeSpeechContent(TextMeshProUGUI body, ScrollRect scroll)
        {
            if (body == null || scroll?.content == null) return;
            body.ForceMeshUpdate();
            float h = Mathf.Max(80f, body.preferredHeight + 8f);
            scroll.content.sizeDelta = new Vector2(0f, h);
            var rt = body.rectTransform;
            rt.sizeDelta = new Vector2(0f, h);
        }

        static TextMeshProUGUI MakeAnchoredText(
            Transform parent,
            string name,
            float size,
            FontStyles style,
            (Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) layout)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.anchorMin = layout.anchorMin;
            rt.anchorMax = layout.anchorMax;
            rt.offsetMin = layout.offsetMin;
            rt.offsetMax = layout.offsetMax;
            rt.pivot = new Vector2(0f, 1f);
            return tmp;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, float size, FontStyles style, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            return tmp;
        }

        static Sprite CircleSprite()
        {
            if (circle != null) return circle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float radius = size * 0.5f - 1f;
            var center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = d <= radius ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return circle;
        }
    }
}
