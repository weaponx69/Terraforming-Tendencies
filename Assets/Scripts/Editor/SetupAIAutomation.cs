#if UNITY_EDITOR
using System.Reflection;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor helper that wires up all the AI terraforming automation in one click.
///
/// Menu: Terraforming → Setup AI Automation
///
/// Creates / updates in the active scene:
///   1. "AI Controller" GameObject  — AIController + GameOverManager
///   2. "Game Over Canvas"          — Full-screen overlay Canvas with GameOverUI
///
/// All serialized references are resolved from known asset paths so no manual
/// Inspector work is needed.
/// </summary>
public static class SetupAIAutomation
{
    // ── Known asset paths ──────────────────────────────────────────────────────
    private const string CMD_POST_PREFAB = "Assets/Units/Buildings/Command Post/Command Post.prefab";
    private const string CMD_POST_SO     = "Assets/Units/Buildings/Command Post/Command Post.asset";
    private const string AIR_TRANSPORT_SO= "Assets/Units/Air Transport/Air Transport.asset";

    [MenuItem("Terraforming/Setup AI Automation")]
    public static void Setup()
    {
        // ── 1. Load assets ─────────────────────────────────────────────────────
        GameObject cmdPostPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(CMD_POST_PREFAB);
        ScriptableObject cmdPostSO  = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CMD_POST_SO);
        ScriptableObject droneUnitSO= AssetDatabase.LoadAssetAtPath<ScriptableObject>(AIR_TRANSPORT_SO);

        ReportMissing("Command Post prefab", cmdPostPrefab, CMD_POST_PREFAB);
        ReportMissing("Command Post SO",     cmdPostSO,     CMD_POST_SO);
        // Air Transport SO is optional — AIController auto-discovers it at runtime.
        if (droneUnitSO == null)
            Debug.Log("[AI Setup] Air Transport SO not found at known path — AIController will auto-discover it at runtime.");

        // ── 2. Create / find "AI Controller" host GameObject ──────────────────
        GameObject aiHost = GameObject.Find("AI Controller");
        if (aiHost == null)
        {
            aiHost = new GameObject("AI Controller");
            Undo.RegisterCreatedObjectUndo(aiHost, "Create AI Controller");
        }

        // ── 3. Wire AIController ───────────────────────────────────────────────
        AIController aiCtrl = aiHost.GetComponent<AIController>()
                           ?? aiHost.AddComponent<AIController>();

        SerializedObject soCtrl = new SerializedObject(aiCtrl);
        SetRef(soCtrl, "commandPostPrefab", cmdPostPrefab);
        SetRef(soCtrl, "commandPostSO",     cmdPostSO);
        // miningDroneUnitSO is optional — left blank so auto-discovery runs at runtime.
        // If you want to pin it explicitly, uncomment the line below:
        // SetRef(soCtrl, "miningDroneUnitSO", droneUnitSO);
        soCtrl.ApplyModifiedProperties();

        // ── 4. Wire GameOverManager ────────────────────────────────────────────
        var gom = aiHost.GetComponent<GameOverManager>()
               ?? aiHost.AddComponent<GameOverManager>();

        EditorUtility.SetDirty(aiHost);
        Debug.Log("[AI Setup] AIController + GameOverManager added to 'AI Controller' GameObject.");

        // ── 5. Build Game Over Canvas ──────────────────────────────────────────
        BuildGameOverCanvas(aiHost);

        // ── 6. Select the new object so the user can see it ───────────────────
        Selection.activeGameObject = aiHost;

        Debug.Log("[AI Setup] Done! Hit Play to run the automated terraforming AI.");
    }

    // ── Canvas builder ─────────────────────────────────────────────────────────
    private static void BuildGameOverCanvas(GameObject aiHost)
    {
        // Reuse existing canvas if already set up
        GameObject existing = GameObject.Find("Game Over Canvas");
        if (existing != null)
        {
            Debug.Log("[AI Setup] 'Game Over Canvas' already exists — skipping canvas creation.");
            WireGameOverUI(existing.GetComponent<GameOverUI>() ?? existing.AddComponent<GameOverUI>(), existing);
            return;
        }

        // ── Canvas root ────────────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("Game Over Canvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Game Over Canvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;                   // Always on top

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Full-screen overlay panel ─────────────────────────────────────────
        GameObject panel = CreateUIElement("Game Over Panel", canvasGO);
        panel.SetActive(false);                      // Hidden until triggered

        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);   // Dark sci-fi blue-black
        StretchFull(panel);

        CanvasGroup cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // ── Vertical layout inside panel ──────────────────────────────────────
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment   = TextAnchor.MiddleCenter;
        layout.spacing          = 28f;
        layout.padding          = new RectOffset(40, 40, 40, 40);
        layout.childControlWidth  = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;

        // ── Headline ──────────────────────────────────────────────────────────
        GameObject headlineGO = CreateUIElement("Headline Text", panel);
        TextMeshProUGUI headline = headlineGO.AddComponent<TextMeshProUGUI>();
        headline.text           = "MISSION FAILED";
        headline.fontSize       = 96;
        headline.fontStyle      = FontStyles.Bold;
        headline.alignment      = TextAlignmentOptions.Center;
        headline.color          = new Color(0.95f, 0.25f, 0.20f, 1f);    // Danger red
        SetHeight(headlineGO, 120f);

        // ── Reason subtitle ────────────────────────────────────────────────────
        GameObject reasonGO = CreateUIElement("Reason Text", panel);
        TextMeshProUGUI reason = reasonGO.AddComponent<TextMeshProUGUI>();
        reason.text      = "The planet's resources are gone.\nTerraforming has ceased.";
        reason.fontSize  = 36;
        reason.alignment = TextAlignmentOptions.Center;
        reason.color     = new Color(0.75f, 0.80f, 0.90f, 1f);           // Soft blue-white
        SetHeight(reasonGO, 100f);

        // ── Button row ────────────────────────────────────────────────────────
        GameObject buttonRow = CreateUIElement("Button Row", panel);
        HorizontalLayoutGroup hLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment    = TextAnchor.MiddleCenter;
        hLayout.spacing           = 40f;
        hLayout.childControlWidth = false;
        hLayout.childForceExpandWidth = false;
        SetHeight(buttonRow, 70f);

        Button restartBtn = CreateButton("Restart Button", buttonRow, "RESTART", new Color(0.15f, 0.55f, 0.95f));
        Button quitBtn    = CreateButton("Quit Button",    buttonRow, "QUIT",    new Color(0.55f, 0.15f, 0.15f));

        // ── GameOverUI component ───────────────────────────────────────────────
        GameOverUI uiComp = canvasGO.AddComponent<GameOverUI>();
        WireGameOverUI(uiComp, panel, headline, reason, restartBtn, quitBtn, cg);

        EditorUtility.SetDirty(canvasGO);
        Debug.Log("[AI Setup] 'Game Over Canvas' created and wired.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void WireGameOverUI(GameOverUI uiComp, GameObject panel,
        TextMeshProUGUI headline = null, TextMeshProUGUI reason = null,
        Button restartBtn = null, Button quitBtn = null, CanvasGroup cg = null)
    {
        SerializedObject so = new SerializedObject(uiComp);
        SetRef(so, "overlayPanel",   panel);
        if (headline   != null) SetRef(so, "headlineText",   headline);
        if (reason     != null) SetRef(so, "reasonText",     reason);
        if (restartBtn != null) SetRef(so, "restartButton",  restartBtn);
        if (quitBtn    != null) SetRef(so, "quitButton",     quitBtn);
        if (cg         != null) SetRef(so, "canvasGroup",    cg);
        so.ApplyModifiedProperties();
    }

    private static void SetRef(SerializedObject so, string propName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null)
            prop.objectReferenceValue = value;
        else
            Debug.LogWarning($"[AI Setup] SerializedProperty '{propName}' not found on {so.targetObject.GetType().Name}");
    }

    private static void ReportMissing(string label, Object asset, string path)
    {
        if (asset == null)
            Debug.LogWarning($"[AI Setup] Could not load {label} at '{path}'. Wire it manually in the Inspector.");
    }

    private static GameObject CreateUIElement(string name, GameObject parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void StretchFull(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetHeight(GameObject go, float h)
    {
        LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
    }

    private static Button CreateButton(string name, GameObject parent, string label, Color bgColor)
    {
        GameObject btnGO = CreateUIElement(name, parent);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260f, 60f);

        Image bg = btnGO.AddComponent<Image>();
        bg.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = bgColor * 1.25f;
        cb.pressedColor     = bgColor * 0.75f;
        btn.colors = cb;

        GameObject textGO = CreateUIElement("Label", btnGO);
        StretchFull(textGO);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return btn;
    }
}
#endif
