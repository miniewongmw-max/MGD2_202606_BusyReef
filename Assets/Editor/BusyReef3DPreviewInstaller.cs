using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class BusyReef3DPreviewInstaller
{
    private const string GameplayScene = "Assets/Scenes/Gameplay.unity";
    private static readonly string[] IconSlots =
    {
        "Ready Character Icon", "Ready Power 1", "Ready Power 2", "Ready Power 3", "Ready Power 4",
        "Equipped Power 1", "Equipped Power 2", "Equipped Power 3", "Equipped Power 4"
    };
    private static readonly string[] CharacterSlots = { "TURTLE", "SEAL", "Seal" };
    private static readonly string[] ShopSlots = { "Bubble Shield", "Speed Dash", "Pearl Magnet", "Invincibility" };

    [MenuItem("Tools/Busy Reef/Add 3D UI Preview Slots (Keep Layout)")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameplayScene)
        {
            Debug.LogWarning("Open Assets/Scenes/Gameplay.unity before adding 3D UI preview slots.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Busy Reef 3D UI preview slots");
        foreach (string name in IconSlots) AddSlots(name, Vector2.zero, Vector2.one);
        foreach (string name in CharacterSlots)
            AddSlots(name, name == "Seal" ? new Vector2(.18f, .63f) : new Vector2(.18f, .52f),
                name == "Seal" ? new Vector2(.82f, .97f) : new Vector2(.82f, .94f));
        foreach (string name in ShopSlots) AddSlots(name, new Vector2(.18f, .58f), new Vector2(.82f, .96f));
        PositionRequestedHudItems();
        ApplyRequestedPlayerMotion();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("BUSY_REEF_3D_UI_SLOTS_READY: assign Model Prefab on each UI 3D Model Preview component.");
    }

    [InitializeOnLoadMethod]
    private static void InstallAfterCompile()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != GameplayScene) return;
            ApplyTimeAttackTimerLayoutIfLegacy();
            Transform ready = FindFirst("Ready Character Icon");
            if (ready == null || ready.GetComponent<UI3DModelPreview>() != null) return;
            Install();
        };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.delayCall += () =>
        {
            if (SceneManager.GetActiveScene().path != GameplayScene) return;
            ApplyTimeAttackTimerLayoutIfLegacy();
            Transform ready = FindFirst("Ready Character Icon");
            if (ready != null && ready.GetComponent<UI3DModelPreview>() == null) Install();
        };
    }

    private static void ApplyTimeAttackTimerLayoutIfLegacy()
    {
        Transform timerTransform = FindFirst("Timer Text");
        TMP_Text timer = timerTransform != null ? timerTransform.GetComponent<TMP_Text>() : null;
        RectTransform rect = timerTransform as RectTransform;
        if (timer == null || rect == null || rect.anchorMin.x < .5f) return;

        Undo.RecordObjects(new Object[] { timer, rect }, "Move Time Attack timer under pearls");
        rect.anchorMin = new Vector2(.025f, .815f);
        rect.anchorMax = new Vector2(.43f, .89f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        timer.fontSize = 52f;
        timer.alignment = TextAlignmentOptions.Left;
        EditorUtility.SetDirty(timer);
        EditorUtility.SetDirty(rect);
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AddSlots(string objectName, Vector2 anchorMin, Vector2 anchorMax)
    {
        foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
        {
            if (rect.name != objectName) continue;
            UI3DModelPreview preview = rect.GetComponent<UI3DModelPreview>();
            if (preview == null) preview = Undo.AddComponent<UI3DModelPreview>(rect.gameObject);
            preview.previewAnchorMin = anchorMin;
            preview.previewAnchorMax = anchorMax;
            preview.EnsureOutputForEditor();
            EditorUtility.SetDirty(preview);
        }
    }

    private static void PositionRequestedHudItems()
    {
        RectTransform pause = FindFirst("Pause") as RectTransform;
        SetRect(pause, new Vector2(.855f, .805f), new Vector2(.985f, .885f));
        for (int i = 0; i < 4; i++)
        {
            RectTransform icon = FindFirst("Equipped Power " + (i + 1)) as RectTransform;
            float top = .795f - i * .067f;
            SetRect(icon, new Vector2(.905f, top - .058f), new Vector2(.978f, top));
        }
    }

    private static void ApplyRequestedPlayerMotion()
    {
        foreach (PlayerController player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(player, "Reduce turtle movement hop");
            player.moveHopHeight = .055f;
            EditorUtility.SetDirty(player);
        }
    }

    private static Transform FindFirst(string objectName)
    {
        foreach (Transform item in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (item.name == objectName) return item;
        return null;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        if (rect == null) return;
        Undo.RecordObject(rect, "Position Busy Reef HUD item");
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        EditorUtility.SetDirty(rect);
    }
}
