using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class OceanUI
{
    private const string CanvasPrefabPath = "UI/BusyReefCanvas";
    private const string PanelPrefabPath = "UI/BusyReefPanel";
    private const string TextPrefabPath = "UI/BusyReefText";
    private const string ButtonPrefabPath = "UI/BusyReefButton";

    public static readonly Color Deep = new Color32(3, 31, 56, 255);
    public static readonly Color Navy = new Color32(35, 91, 122, 245);
    public static readonly Color Panel = new Color32(104, 194, 224, 240);
    public static readonly Color ButtonFrame = new Color32(116, 207, 235, 255);
    public static readonly Color Aqua = new Color32(139, 231, 246, 255);
    public static readonly Color Foam = new Color32(231, 255, 244, 255);
    public static readonly Color Sand = new Color32(255, 226, 126, 255);
    public static readonly Color Coral = new Color32(255, 143, 183, 255);
    public static readonly Color Muted = new Color32(185, 225, 239, 255);

    private static TMP_FontAsset creamyFont;
    private static Sprite roundedSprite;

    public static Canvas CreateCanvas(string name, bool disableExisting = true)
    {
        if (disableExisting)
        {
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
                canvas.gameObject.SetActive(false);
        }

        EnsureEventSystem();
        GameObject canvasPrefab = Resources.Load<GameObject>(CanvasPrefabPath);
        GameObject go = canvasPrefab != null
            ? UnityEngine.Object.Instantiate(canvasPrefab)
            : new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.name = name;
        if (go.GetComponent<Canvas>() == null) go.AddComponent<Canvas>();
        if (go.GetComponent<CanvasScaler>() == null) go.AddComponent<CanvasScaler>();
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        Canvas canvasResult = go.GetComponent<Canvas>();
        canvasResult.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasResult.sortingOrder = 50;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform existingSafe = go.transform.Find("SafeArea");
        GameObject safe = existingSafe != null ? existingSafe.gameObject : CreateObject("SafeArea", go.transform);
        Stretch(safe.GetComponent<RectTransform>(), 0f);
        if (safe.GetComponent<SafeAreaPanel>() == null) safe.AddComponent<SafeAreaPanel>();
        return canvasResult;
    }

    public static RectTransform SafeRoot(Canvas canvas)
    {
        Transform safe = canvas.transform.Find("SafeArea");
        return safe != null ? safe as RectTransform : canvas.transform.GetChild(0) as RectTransform;
    }

    public static GameObject CreateObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static Image CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panelPrefab = Resources.Load<GameObject>(PanelPrefabPath);
        GameObject go = panelPrefab != null
            ? UnityEngine.Object.Instantiate(panelPrefab, parent, false)
            : CreateObject(name, parent);
        go.name = name;
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = color;
        if (image.sprite == null)
        {
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    public static void MakeRounded(Image image)
    {
        if (image == null) return;
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
    }

    // Runtime styling should improve the generated default UI without replacing
    // artwork that a designer assigns later in the Canvas Inspector.
    public static bool MakeRoundedIfDefault(Image image)
    {
        if (image == null) return false;
        string spriteName = image.sprite != null ? image.sprite.name : string.Empty;
        bool generatedOrBuiltIn = image.sprite == null || spriteName == "UISprite" || spriteName == "Background" ||
            spriteName == "Knob" || spriteName == "BusyReefRounded" || spriteName == "Busy Reef Rounded Sprite";
        if (!generatedOrBuiltIn) return false;
        MakeRounded(image);
        return true;
    }

    public static TMP_Text CreateText(string text, Transform parent, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject textPrefab = Resources.Load<GameObject>(TextPrefabPath);
        GameObject go = textPrefab != null
            ? UnityEngine.Object.Instantiate(textPrefab, parent, false)
            : CreateObject("Text", parent);
        go.name = "Text";
        TMP_Text label = go.GetComponent<TMP_Text>();
        if (label == null) label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        TMP_FontAsset font = CreamyFont();
        if (font != null) label.font = font;
        Stretch(label.rectTransform, 12f);
        return label;
    }

    public static Button CreateButton(string name, string label, Transform parent, Color color, Action onClick)
    {
        if (color == Panel) color = ButtonFrame;
        GameObject buttonPrefab = Resources.Load<GameObject>(ButtonPrefabPath);
        GameObject go = buttonPrefab != null
            ? UnityEngine.Object.Instantiate(buttonPrefab, parent, false)
            : CreatePanel(name, parent, color).gameObject;
        go.name = name;
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = color;
        MakeRounded(image);
        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.72f, 0.9f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => GameAudioManager.Play(GameSfx.Button));
        if (onClick != null) button.onClick.AddListener(() => onClick());
        TMP_Text buttonLabel = go.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel == null) buttonLabel = CreateText(label, image.transform, 40f, Deep);
        buttonLabel.text = label;
        buttonLabel.fontSize = 40f;
        buttonLabel.color = Deep;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.raycastTarget = false;
        TMP_FontAsset font = CreamyFont();
        if (font != null) buttonLabel.font = font;
        Stretch(buttonLabel.rectTransform, 12f);
        StyleBubbleButton(button);
        return button;
    }

    public static void StyleCanvasButtons(Canvas canvas)
    {
        if (canvas == null) return;
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            StyleBubbleButton(button);
    }

    private static void StyleBubbleButton(Button button)
    {
        if (button == null || !button.TryGetComponent(out Image image)) return;
        // The transparent pause glyph is intentionally an unframed control.
        if (image.color.a <= .02f) return;
        MakeRoundedIfDefault(image);
        Color glass = image.color;
        glass.a = Mathf.Min(glass.a, .52f);
        image.color = glass;

        Outline rim = button.GetComponent<Outline>();
        if (rim == null) rim = button.gameObject.AddComponent<Outline>();
        rim.effectColor = new Color(1f, 1f, 1f, .7f);
        rim.effectDistance = new Vector2(2f, -2f);
        rim.useGraphicAlpha = false;

        Shadow depth = null;
        foreach (Shadow candidate in button.GetComponents<Shadow>())
            if (!(candidate is Outline)) { depth = candidate; break; }
        if (depth == null) depth = button.gameObject.AddComponent<Shadow>();
        depth.effectColor = new Color(.02f, .17f, .32f, .23f);
        depth.effectDistance = new Vector2(0f, -4f);
        depth.useGraphicAlpha = false;

        Transform existing = button.transform.Find("Bubble Gloss");
        Image gloss = existing != null ? existing.GetComponent<Image>() : null;
        if (gloss == null)
        {
            GameObject highlight = new GameObject("Bubble Gloss", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            highlight.transform.SetParent(button.transform, false);
            gloss = highlight.GetComponent<Image>();
            gloss.sprite = RoundedSprite();
            gloss.type = Image.Type.Sliced;
            gloss.color = new Color(1f, 1f, 1f, .24f);
            gloss.raycastTarget = false;
            RectTransform rect = gloss.rectTransform;
            rect.anchorMin = new Vector2(.08f, .57f);
            rect.anchorMax = new Vector2(.92f, .91f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        gloss.transform.SetAsFirstSibling();
    }

    private static bool IsInside(Transform child, string ancestorName)
    {
        for (Transform current = child.parent; current != null; current = current.parent)
            if (current.name == ancestorName) return true;
        return false;
    }

    private static void RemoveBubbleStyle(Button button, Image image)
    {
        Transform gloss = button.transform.Find("Bubble Gloss");
        if (gloss == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(gloss.gameObject);
        else UnityEngine.Object.DestroyImmediate(gloss.gameObject);
        foreach (Shadow effect in button.GetComponents<Shadow>())
        {
            bool bubbleRim = effect is Outline &&
                Vector2.Distance(effect.effectDistance, new Vector2(2f, -2f)) < .01f;
            bool bubbleDepth = !(effect is Outline) &&
                Vector2.Distance(effect.effectDistance, new Vector2(0f, -4f)) < .01f;
            if (!bubbleRim && !bubbleDepth) continue;
            if (Application.isPlaying) UnityEngine.Object.Destroy(effect);
            else UnityEngine.Object.DestroyImmediate(effect);
        }
        Color original = image.color;
        original.a = 1f;
        image.color = original;
    }

    public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void AddOceanBackground(Transform parent)
    {
        Image backdrop = CreatePanel("OceanBackdrop", parent, Deep);
        Stretch(backdrop.rectTransform, -8f);
        backdrop.transform.SetAsFirstSibling();

        Color[] colors =
        {
            new Color(0.10f, 0.70f, 0.72f, 0.10f),
            new Color(0.30f, 0.90f, 0.82f, 0.08f),
            new Color(1f, 0.75f, 0.45f, 0.06f)
        };
        for (int i = 0; i < 12; i++)
        {
            Image bubble = CreatePanel($"Bubble{i}", backdrop.transform, colors[i % colors.Length]);
            RectTransform rect = bubble.rectTransform;
            float x = ((i * 37) % 100) / 100f;
            float y = ((i * 61) % 100) / 100f;
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.sizeDelta = Vector2.one * (35f + (i % 4) * 22f);
        }
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null ||
            UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length > 0) return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(go);
    }

    private static TMP_FontAsset CreamyFont()
    {
        if (creamyFont != null) return creamyFont;
        creamyFont = Resources.Load<TMP_FontAsset>("Fonts/Creamy Chicken SDF");
        if (creamyFont != null) return creamyFont;
        Font source = Resources.Load<Font>("Fonts/Creamy Chicken");
        if (source == null) return null;
        creamyFont = TMP_FontAsset.CreateFontAsset(source);
        creamyFont.name = "Creamy Chicken Runtime SDF";
        return creamyFont;
    }

    private static Sprite RoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;
        roundedSprite = Resources.Load<Sprite>("UI/BusyReefRounded");
        if (roundedSprite != null) return roundedSprite;
        const int size = 64;
        const float radius = 29f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Busy Reef Rounded Panel",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = x < radius ? radius : x > size - radius - 1 ? size - radius - 1 : x;
            float cy = y < radius ? radius : y > size - radius - 1 ? size - radius - 1 : y;
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            byte alpha = distance <= radius ? (byte)255 : (byte)0;
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(29f, 29f, 29f, 29f));
        roundedSprite.name = "Busy Reef Rounded Sprite";
        return roundedSprite;
    }
}
