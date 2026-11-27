using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    /// <summary>World Canvas + TMP UGUI labels (?, OK, etc.) — not 3D TextMesh / TextMeshPro.</summary>
    public class MainGameWorldQuestionLabel : MonoBehaviour
    {
        const float WorldCanvasScale = 0.072f;

        /// <summary>Create or upgrade the Question child under a deposit / node mark root.</summary>
        public static MainGameWorldQuestionLabel EnsureOnMarkRoot(
            Transform root,
            string text,
            float fontSize,
            Color color)
        {
            if (root == null) return null;

            foreach (var legacy in root.GetComponents<TextMeshPro>())
                Object.Destroy(legacy);

            Transform question = root.Find("Question");
            if (question == null)
                return CreateChild(root, text, fontSize, color, "Question", WorldCanvasScale, Vector3.zero);

            var label = question.GetComponent<MainGameWorldQuestionLabel>();
            if (label == null)
                label = question.gameObject.AddComponent<MainGameWorldQuestionLabel>();
            var tmp = question.GetComponent<TextMeshProUGUI>();
            ApplyTextStyle(tmp, text, fontSize, color);
            question.localScale = Vector3.one * WorldCanvasScale;
            return label;
        }

        /// <summary>Ice markers and other spawns that only need the glyph child.</summary>
        public static MainGameWorldQuestionLabel Create(Transform parent, float fontSize, Color color) =>
            CreateLabel(parent, "?", fontSize, color);

        public static MainGameWorldQuestionLabel CreateLabel(
            Transform parent,
            string text,
            float fontSize,
            Color color,
            string childName = "Question",
            float canvasScale = -1f,
            Vector3? localPosition = null)
        {
            if (canvasScale < 0f) canvasScale = WorldCanvasScale;
            return CreateChild(parent, text, fontSize, color, childName, canvasScale, localPosition ?? Vector3.zero);
        }

        static MainGameWorldQuestionLabel CreateChild(
            Transform parent,
            string text,
            float fontSize,
            Color color,
            string childName,
            float canvasScale,
            Vector3 localPosition)
        {
            var existing = parent.Find(childName);
            if (existing != null)
                Object.Destroy(existing.gameObject);

            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * canvasScale;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = PlayerInput.Instance != null
                ? PlayerInput.Instance.GameplayCamera
                : Camera.main;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(72f, 72f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            ApplyTextStyle(tmp, text, fontSize, color);
            return go.AddComponent<MainGameWorldQuestionLabel>();
        }

        static void ApplyTextStyle(TextMeshProUGUI tmp, string text, float fontSize, Color color)
        {
            if (tmp == null) return;
            tmp.text = text ?? "?";
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = false;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color(0.05f, 0.02f, 0.08f, 1f);
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
        }

        /// <summary>Convert every sector-node mark and legacy 3D TMP still in the scene.</summary>
        public static void UpgradeAllSectorQuestionMarks()
        {
            if (!MainGameController.IsActive) return;
            var sectors = SectorManager.Instance?.Sectors;
            if (sectors == null) return;
            float font = MainGameIceMarkers.MarkerFontSize;
            var mystery = new Color(1f, 0.92f, 0.2f, 1f);
            for (int s = 0; s < sectors.Count; s++)
            {
                var sector = sectors[s];
                if (sector?.Nodes == null) continue;
                for (int n = 0; n < sector.Nodes.Count; n++)
                {
                    var node = sector.Nodes[n];
                    if (node?.questionMarkGO == null) continue;
                    string label = string.IsNullOrEmpty(node.labelOverride) ? "?" : node.labelOverride;
                    Color c = node.type == SectorNode.NodeType.Feature
                        ? new Color(0.55f, 0.95f, 1f)
                        : mystery;
                    if (label == "?") c = mystery;
                    EnsureOnMarkRoot(node.questionMarkGO.transform, label, font, c);
                }
            }
        }

        void LateUpdate()
        {
            if (!MainGameController.IsActive && !MainGameController.IsMainGamePlayScene()) return;
            Camera cam = PlayerInput.Instance != null ? PlayerInput.Instance.GameplayCamera : null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
                canvas.worldCamera = cam;
            Vector3 dir = transform.position - cam.transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}