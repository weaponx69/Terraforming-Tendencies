using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// A simple world-space popup that floats upwards and fades out.
    /// Used for "juice" feedback when collecting or depositing resources.
    /// </summary>
    public class FloatingPopup : MonoBehaviour
    {
        [SerializeField] private float duration = 1.5f;
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0.5f, 0.2f, 1.2f);

        private TextMeshPro textMesh;
        private float startTime;
        private Vector3 startPos;
        private Transform camTransform;

        public static void Create(Vector3 position, string text, Color color)
        {
            GameObject go = new GameObject("ResourcePopup", typeof(TextMeshPro), typeof(FloatingPopup));
            go.transform.position = position;
            
            var popup = go.GetComponent<FloatingPopup>();
            popup.Setup(text, color);
        }

        private void Setup(string text, Color color)
        {
            textMesh = GetComponent<TextMeshPro>();
            
            // Ensure font is assigned
            if (textMesh.font == null)
            {
                textMesh.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            textMesh.text = text;
            textMesh.color = color;
            textMesh.fontSize = 6;
            textMesh.alignment = TextAlignmentOptions.Center;
            
            // Ensure it renders in front
            textMesh.sortingOrder = 100;
            
            // Add FaceCamera logic if not already present
            if (gameObject.GetComponent<FaceCamera>() == null)
            {
                gameObject.AddComponent<FaceCamera>();
            }

            startTime = Time.time;
            startPos = transform.position;
            
            if (Camera.main != null)
                camTransform = Camera.main.transform;
        }

        private void Update()
        {
            float elapsed = Time.time - startTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Float Up
            transform.position = startPos + Vector3.up * (floatSpeed * elapsed);

            // Fade Out
            Color c = textMesh.color;
            c.a = alphaCurve.Evaluate(t);
            textMesh.color = c;

            // Scale Juice
            float scale = scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;
        }
    }
}
