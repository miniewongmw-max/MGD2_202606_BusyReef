using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuBehaviour : MonoBehaviour
{
    public GameObject shopMenu, settingsMenu, modeSelectionMenu;
    private readonly List<RectTransform> pages = new List<RectTransform>();
    private readonly List<Image> navTiles = new List<Image>();
    private RectTransform pageArea;
    private TMP_Text shopWalletText, homeText, stageTitle, stageDescription, feedback, characterFeedback, touchLabel;
    private TMP_Text soundLabel, bgmValueLabel, sfxValueLabel;
    private Slider bgmSlider, sfxSlider;
    private GameObject aboutPanel;
    private GameObject powerShop, characterShop;
    private StageCarousel3D carousel;
    private Button stagePrevious, stageNext;
    private readonly Image[] readyEquipmentIcons = new Image[5];
    private readonly Vector2[] readySlotMin = new Vector2[5];
    private readonly Vector2[] readySlotMax = new Vector2[5];
    private readonly Vector2[] readySlotPosition = new Vector2[5];
    private readonly Vector2[] readySlotSize = new Vector2[5];
    private Coroutine slideRoutine;
    private int currentPage = 2;
    private RectTransform navigationBar;
    private RectTransform navigationBleed;
    private Image fullPageBackdrop;
    private RawImage splitPageBackdrop;
    private bool lastLandscape;
    private int slideFromPage = 2;
    private bool built;
    private bool gameplayHost;
    private Canvas hubCanvas;
    private const int SealCost = 120;
    public bool IsPlayPage => currentPage == 2;
    private static readonly string[] PowerNames = { "Bubble Shield", "Speed Dash", "Pearl Magnet", "Invincibility" };
    private static readonly string[] PowerInfo = { "Blocks one obstacle", "Moves forward twice", "Pulls nearby pearls", "Ignores hazards briefly" };
    private static readonly int[] PowerCosts = { 15, 10, 50, 80 };
    private static readonly FishGameMode[] CarouselModes =
        { FishGameMode.Tutorial, FishGameMode.Standard, FishGameMode.TimeAttack, FishGameMode.Riptide };
    private static readonly string[] ModeDescriptions =
    {
        "Learn movement, pearls, animals and every buff step by step",
        "Endless reef traffic - survive and set a new best score",
        "Cross as many animal lanes as possible before time runs out",
        "Hard mode - denser, faster animal traffic with fewer pearls"
    };

    private void Start()
    {
        if (built) return;
        Time.timeScale = 1f;
        BindHubCanvas(false);
    }

    public void BuildOnGameplay()
    {
        if (built) return;
        gameplayHost = true;
        BindHubCanvas(true);
    }

    public void BuildHubForEditor(bool overGameplay) => BuildHub(overGameplay);

    private void BuildHub(bool overGameplay)
    {
        built = true;
        Canvas canvas = OceanUI.CreateCanvas("Busy Reef Bottom Hub", !overGameplay);
        hubCanvas = canvas;
        if (overGameplay) canvas.sortingOrder = 60;
        RectTransform root = OceanUI.SafeRoot(canvas);
        if (!overGameplay) OceanUI.AddOceanBackground(root);
        AdaptiveUILayout adaptive = root.gameObject.AddComponent<AdaptiveUILayout>();
        adaptive.scaler = canvas.GetComponent<CanvasScaler>();
        GameObject backdropObject = OceanUI.CreateObject("Full Screen Page Backdrop", canvas.transform);
        fullPageBackdrop = backdropObject.AddComponent<Image>();
        fullPageBackdrop.raycastTarget = false;
        OceanUI.Stretch(backdropObject.GetComponent<RectTransform>(), 0f);
        backdropObject.transform.SetAsFirstSibling();
        splitPageBackdrop = OceanUI.CreateObject("Split Page Backdrop", backdropObject.transform).AddComponent<RawImage>();
        splitPageBackdrop.raycastTarget = false;
        OceanUI.Stretch(splitPageBackdrop.rectTransform, 0f);
        splitPageBackdrop.gameObject.SetActive(false);

        pageArea = OceanUI.CreateObject("Sliding Pages", root).GetComponent<RectTransform>();
        OceanUI.SetRect(pageArea, new Vector2(0f, 0.145f), new Vector2(1f, 0.91f), Vector2.zero, Vector2.zero);
        pageArea.gameObject.AddComponent<RectMask2D>();
        pages.Add(BuildStagePage());
        pages.Add(BuildShopPage());
        pages.Add(BuildHomePage());
        pages.Add(BuildCharacterPage());
        pages.Add(BuildSettingsPage());
        adaptive.carousel = carousel;
        BuildNavigation(root);
        ApplyEditableVisualTheme(root);
        Canvas.ForceUpdateCanvases();
        lastLandscape = Screen.width > Screen.height;
        ApplyHubOrientation();
        PositionPages();
        SetRestingPageVisibility();
        RefreshAll();
    }

    private void BindHubCanvas(bool overGameplay)
    {
        hubCanvas = FindSceneCanvas("Busy Reef Bottom Hub");
        if (hubCanvas == null)
        {
            Debug.LogError("Editable bottom-menu Canvas is missing. Run Tools/Busy Reef/Bake Editable Gameplay Canvas.");
            return;
        }

        gameplayHost = overGameplay;
        hubCanvas.gameObject.SetActive(true);
        if (overGameplay) hubCanvas.sortingOrder = 60;
        Transform root = FindDeepChild(hubCanvas.transform, "SafeArea");
        if (root == null) root = hubCanvas.transform;

        pageArea = ComponentAt<RectTransform>(root, "Sliding Pages");
        navigationBar = ComponentAt<RectTransform>(root, "Five Button Navigation");
        navigationBleed = ComponentAt<RectTransform>(hubCanvas.transform, "Navigation Edge Fill");
        fullPageBackdrop = ComponentAt<Image>(hubCanvas.transform, "Full Screen Page Backdrop");
        splitPageBackdrop = ComponentAt<RawImage>(hubCanvas.transform, "Split Page Backdrop");
        carousel = ComponentAt<StageCarousel3D>(root, "3D Circular Mode Carousel");
        stagePrevious = ComponentAt<Button>(root, "Previous");
        stageNext = ComponentAt<Button>(root, "Next");
        stageTitle = ComponentAt<TMP_Text>(root, "Selected Mode Title");
        stageDescription = ComponentAt<TMP_Text>(root, "Selected Mode Description");
        homeText = ComponentAt<TMP_Text>(root, "Home Summary");
        shopWalletText = ComponentAt<TMP_Text>(root, "Shop Wallet Text");
        feedback = ComponentAt<TMP_Text>(root, "Shop Feedback");
        characterFeedback = ComponentAt<TMP_Text>(root, "Character Feedback");
        touchLabel = ComponentAt<TMP_Text>(root, "Touch Pad Value");
        soundLabel = ComponentAt<TMP_Text>(root, "Sound Value");
        bgmValueLabel = ComponentAt<TMP_Text>(root, "BGM Value");
        sfxValueLabel = ComponentAt<TMP_Text>(root, "SFX Value");
        bgmSlider = ComponentAt<Slider>(root, "BGM Slider");
        sfxSlider = ComponentAt<Slider>(root, "SFX Slider");
        aboutPanel = FindDeepChild(root, "About Panel")?.gameObject;
        powerShop = FindDeepChild(root, "Power-up Stock")?.gameObject;
        characterShop = FindDeepChild(root, "Character Stock")?.gameObject;
        EnsureReadyEquipmentIcons(root);

        pages.Clear();
        AddPage(root, "Mode Selection Page");
        AddPage(root, "Shop Page");
        AddPage(root, "Play Page");
        AddPage(root, "Character Selection Page");
        AddPage(root, "Settings Page");

        navTiles.Clear();
        string[] labels = { "MODE", "SHOP", "PLAY", "CHARACTER", "SETTINGS" };
        for (int i = 0; i < labels.Length; i++)
        {
            Button button = ComponentAt<Button>(navigationBar, labels[i]);
            int page = i;
            WireButton(button, () => Navigate(page));
            if (button != null && button.TryGetComponent(out Image tile)) navTiles.Add(tile);
        }

        if (carousel != null && carousel.cards != null && carousel.cards.Length == CarouselModes.Length)
        {
            carousel.Configure(carousel.cards, (int)GameSession.Mode);
            carousel.SelectionChanged = i =>
            {
                GameSession.Mode = CarouselModes[Mathf.Clamp(i, 0, CarouselModes.Length - 1)];
                GameSession.SelectedStage = 0;
                GameManager.Instance?.PreviewSelectedStage();
                RefreshAll();
            };
        }
        WireButton(stagePrevious, carousel != null ? carousel.Previous : null);
        WireButton(stageNext, carousel != null ? carousel.Next : null);
        WireButton(ComponentAt<Button>(root, "Power Category"), () => ShopCategory(true));
        WireButton(ComponentAt<Button>(root, "Character Category"), () => ShopCategory(false));
        for (int i = 0; i < PowerNames.Length; i++)
        {
            Transform card = FindDeepChild(powerShop != null ? powerShop.transform : null, PowerNames[i]);
            Button buy = ComponentAt<Button>(card, "Buy");
            int item = i;
            WireButton(buy, () => BuyPower(item));
        }
        WireButton(ComponentAt<Button>(characterShop != null ? characterShop.transform : null, "Buy Seal"), BuySeal);
        Transform characterPage = FindDeepChild(root, "Character Selection Page");
        WireButton(ComponentAt<Button>(FindDeepChild(characterPage, "TURTLE"), "Choose"), () => SelectCharacter(0));
        WireButton(ComponentAt<Button>(FindDeepChild(characterPage, "SEAL"), "Choose"), () => SelectCharacter(1));
        WireButton(ComponentAt<Button>(root, "SOUND Toggle"), () => { GameAudioManager.ToggleMute(); RefreshAll(); });
        WireButton(ComponentAt<Button>(root, "TOUCH PAD Toggle"), ToggleTouch);
        WireButton(ComponentAt<Button>(root, "ABOUT Open"), () => SetAboutVisible(true));
        WireButton(ComponentAt<Button>(aboutPanel != null ? aboutPanel.transform : null, "ABOUT Close"), () => SetAboutVisible(false));
        WireSlider(bgmSlider, value => { GameAudioManager.SetBgmVolume(value); RefreshAll(); });
        WireSlider(sfxSlider, value => { GameAudioManager.SetSfxVolume(value); RefreshAll(); });
        SetAboutVisible(false);

        AdaptiveUILayout adaptive = root.GetComponent<AdaptiveUILayout>();
        if (adaptive != null)
        {
            adaptive.scaler = hubCanvas.GetComponent<CanvasScaler>();
            adaptive.carousel = carousel;
        }
        currentPage = slideFromPage = 2;
        built = true;
        lastLandscape = Screen.width > Screen.height;
        ApplyHubOrientation();
        Canvas.ForceUpdateCanvases();
        PositionPages();
        SetRestingPageVisibility();
        ShopCategory(true);
        RefreshAll();
    }

    private void ApplyEditableVisualTheme(Transform root)
    {
        Color[] palette = { OceanUI.Coral, OceanUI.Sand, OceanUI.Aqua };
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Image image = buttons[i].GetComponent<Image>();
            if (image == null) continue;
            if (OceanUI.MakeRoundedIfDefault(image)) image.color = palette[i % palette.Length];
        }

        foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
        {
            Image track = slider.GetComponent<Image>();
            if (track != null)
            {
                if (OceanUI.MakeRoundedIfDefault(track)) track.color = OceanUI.Coral;
            }
            Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null)
            {
                if (OceanUI.MakeRoundedIfDefault(fill)) fill.color = OceanUI.Aqua;
            }
            Image handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (handle != null)
            {
                if (OceanUI.MakeRoundedIfDefault(handle)) handle.color = OceanUI.Sand;
            }
        }

        Image edgeFill = navigationBleed != null ? navigationBleed.GetComponent<Image>() : null;
        if (edgeFill != null) edgeFill.color = OceanUI.Panel;

        // Opaque menu pages use the pastel palette instead of deep-blue cards.
        // The play page stays transparent so the live reef remains visible.
        for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            if (pageIndex == 2 || pages[pageIndex] == null) continue;
            int panelIndex = pageIndex;
            Color[] pagePalette = pageIndex == 1
                ? new[] { OceanUI.Coral, OceanUI.Aqua }
                : palette;
            foreach (Image panel in pages[pageIndex].GetComponentsInChildren<Image>(true))
            {
                if (panel.name == "Opaque Page Background" || panel.GetComponent<Button>() != null ||
                    panel.GetComponentInParent<Slider>() != null) continue;
                if (OceanUI.MakeRoundedIfDefault(panel)) panel.color = pagePalette[panelIndex++ % pagePalette.Length];
            }
            if (pageIndex == 1)
            {
                int shopButtonIndex = 0;
                foreach (Button shopButton in pages[pageIndex].GetComponentsInChildren<Button>(true))
                {
                    Image buttonImage = shopButton.GetComponent<Image>();
                    if (buttonImage != null) buttonImage.color = shopButtonIndex++ % 2 == 0 ? OceanUI.Coral : OceanUI.Aqua;
                }
            }
            foreach (TMP_Text label in pages[pageIndex].GetComponentsInChildren<TMP_Text>(true))
                label.color = OceanUI.Deep;
        }
    }

    private void AddPage(Transform root, string objectName)
    {
        RectTransform page = ComponentAt<RectTransform>(root, objectName);
        if (page != null) pages.Add(page);
    }

    private static Canvas FindSceneCanvas(string objectName)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            if (canvas.name == objectName) return canvas;
        return null;
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), objectName);
            if (found != null) return found;
        }
        return null;
    }

    private static T ComponentAt<T>(Transform root, string objectName) where T : Component
    {
        return FindDeepChild(root, objectName)?.GetComponent<T>();
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => GameAudioManager.Play(GameSfx.Button));
        if (action != null) button.onClick.AddListener(action);
    }

    private static void WireSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null) return;
        slider.onValueChanged.RemoveAllListeners();
        if (action != null) slider.onValueChanged.AddListener(action);
    }

    private void Update()
    {
        if (!built) return;
        bool landscape = Screen.width > Screen.height;
        if (landscape == lastLandscape) return;
        lastLandscape = landscape;
        ApplyHubOrientation();
        Canvas.ForceUpdateCanvases();
        PositionPages();
    }

    private RectTransform Page(string name)
    {
        RectTransform page = OceanUI.CreateObject(name, pageArea).GetComponent<RectTransform>();
        OceanUI.Stretch(page, 0f);
        return page;
    }

    private void AddTitle(Transform parent, string title, string subtitle)
    {
        TMP_Text a = OceanUI.CreateText(title, parent, 52f, OceanUI.Sand);
        TMP_Text b = OceanUI.CreateText(subtitle, parent, 25f, OceanUI.Muted);
        OceanUI.SetRect(a.rectTransform, new Vector2(.06f, .86f), new Vector2(.94f, .99f), Vector2.zero, Vector2.zero);
        OceanUI.SetRect(b.rectTransform, new Vector2(.06f, .79f), new Vector2(.94f, .88f), Vector2.zero, Vector2.zero);
    }

    private RectTransform BuildHomePage()
    {
        RectTransform page = Page("Play Page");
        TMP_Text title = OceanUI.CreateText("BUSY REEF", page, 76f, OceanUI.Sand);
        OceanUI.SetRect(title.rectTransform, new Vector2(.06f, .68f), new Vector2(.94f, .92f), Vector2.zero, Vector2.zero);
        TMP_Text sub = OceanUI.CreateText("CROSS THE OCEAN. FIND YOUR FAMILY.", page, 31f, OceanUI.Foam);
        OceanUI.SetRect(sub.rectTransform, new Vector2(.08f, .58f), new Vector2(.92f, .70f), Vector2.zero, Vector2.zero);
        homeText = OceanUI.CreateText("", page, 28f, OceanUI.Muted);
        homeText.name = "Home Summary";
        OceanUI.SetRect(homeText.rectTransform, new Vector2(.08f, .40f), new Vector2(.92f, .54f), Vector2.zero, Vector2.zero);
        CreateReadyEquipmentIcons(page);
        TMP_Text begin = OceanUI.CreateText("TOUCH ANYWHERE TO BEGIN", page, 43f, OceanUI.Sand);
        OceanUI.SetRect(begin.rectTransform, new Vector2(.12f, .18f), new Vector2(.88f, .32f), Vector2.zero, Vector2.zero);
        return page;
    }

    private RectTransform BuildStagePage()
    {
        RectTransform page = Page("Mode Selection Page");
        AddOpaquePageBackground(page, new Color32(3, 45, 66, 255));
        AddSplitPageArtwork(page, true);
        AddTitle(page, "MODE SELECTION", "Swipe the circular carousel and tap a mode");
        GameObject holder = OceanUI.CreateObject("3D Circular Mode Carousel", page);
        OceanUI.SetRect(holder.GetComponent<RectTransform>(), new Vector2(.06f, .30f), new Vector2(.94f, .77f), Vector2.zero, Vector2.zero);
        carousel = holder.AddComponent<StageCarousel3D>();
        RectTransform[] cards = new RectTransform[CarouselModes.Length];
        Color[] colors = { OceanUI.Aqua, OceanUI.Sand, OceanUI.Coral, new Color32(108, 204, 239, 255) };
        for (int i = 0; i < CarouselModes.Length; i++)
        {
            string modeName = DisplayModeName(CarouselModes[i]);
            Image card = OceanUI.CreatePanel(modeName, holder.transform, colors[i]);
            card.gameObject.AddComponent<Button>();
            cards[i] = card.rectTransform;
            OceanUI.SetCentered(cards[i], Vector2.zero, new Vector2(430f, 270f));
            TMP_Text name = OceanUI.CreateText(modeName, card.transform, 39f, OceanUI.Deep);
            OceanUI.SetRect(name.rectTransform, new Vector2(.06f, .50f), new Vector2(.94f, .88f), Vector2.zero, Vector2.zero);
            TMP_Text description = OceanUI.CreateText(ModeDescriptions[i], card.transform, 21f, OceanUI.Deep);
            OceanUI.SetRect(description.rectTransform, new Vector2(.08f, .10f), new Vector2(.92f, .52f), Vector2.zero, Vector2.zero);
        }
        carousel.Configure(cards, (int)GameSession.Mode);
        carousel.SelectionChanged = i =>
        {
            GameSession.Mode = CarouselModes[Mathf.Clamp(i, 0, CarouselModes.Length - 1)];
            GameSession.SelectedStage = 0;
            GameManager.Instance?.PreviewSelectedStage();
            RefreshAll();
        };
        stagePrevious = OceanUI.CreateButton("Previous", "<", page, OceanUI.Panel, carousel.Previous);
        stageNext = OceanUI.CreateButton("Next", ">", page, OceanUI.Panel, carousel.Next);
        OceanUI.SetRect(stagePrevious.GetComponent<RectTransform>(), new Vector2(.02f, .47f), new Vector2(.14f, .61f), Vector2.zero, Vector2.zero);
        OceanUI.SetRect(stageNext.GetComponent<RectTransform>(), new Vector2(.86f, .47f), new Vector2(.98f, .61f), Vector2.zero, Vector2.zero);
        stageTitle = OceanUI.CreateText("", page, 31f, OceanUI.Foam);
        stageDescription = OceanUI.CreateText("", page, 23f, OceanUI.Muted);
        stageTitle.name = "Selected Mode Title";
        stageDescription.name = "Selected Mode Description";
        OceanUI.SetRect(stageTitle.rectTransform, new Vector2(.08f, .18f), new Vector2(.92f, .28f), Vector2.zero, Vector2.zero);
        OceanUI.SetRect(stageDescription.rectTransform, new Vector2(.08f, .07f), new Vector2(.92f, .19f), Vector2.zero, Vector2.zero);
        return page;
    }

    private RectTransform BuildShopPage()
    {
        RectTransform page = Page("Shop Page");
        AddOpaquePageBackground(page, new Color32(3, 45, 66, 255));
        AddSplitPageArtwork(page, false);
        AddTitle(page, "PEARL SHOP", "Power-ups and characters have separate categories");
        Button powers = OceanUI.CreateButton("Power Category", "POWER-UPS", page, OceanUI.Aqua, () => ShopCategory(true));
        Button chars = OceanUI.CreateButton("Character Category", "CHARACTERS", page, OceanUI.Sand, () => ShopCategory(false));
        OceanUI.SetRect(powers.GetComponent<RectTransform>(), new Vector2(.10f, .63f), new Vector2(.49f, .72f), Vector2.zero, Vector2.zero);
        OceanUI.SetRect(chars.GetComponent<RectTransform>(), new Vector2(.51f, .63f), new Vector2(.90f, .72f), Vector2.zero, Vector2.zero);
        Image shopWallet = OceanUI.CreatePanel("Shop Wallet", page, new Color(0.02f, 0.20f, 0.29f, 0.96f));
        OceanUI.SetRect(shopWallet.rectTransform, new Vector2(.67f, .74f), new Vector2(.93f, .82f), Vector2.zero, Vector2.zero);
        shopWalletText = OceanUI.CreateText("", shopWallet.transform, 28f, OceanUI.Sand, TextAlignmentOptions.Right);
        shopWalletText.name = "Shop Wallet Text";
        powerShop = OceanUI.CreateObject("Power-up Stock", page);
        OceanUI.SetRect(powerShop.GetComponent<RectTransform>(), new Vector2(.04f, .12f), new Vector2(.96f, .61f), Vector2.zero, Vector2.zero);
        for (int i = 0; i < 4; i++) BuildPowerCard(powerShop.transform, i);
        characterShop = OceanUI.CreateObject("Character Stock", page);
        OceanUI.SetRect(characterShop.GetComponent<RectTransform>(), new Vector2(.04f, .12f), new Vector2(.96f, .61f), Vector2.zero, Vector2.zero);
        Image seal = OceanUI.CreatePanel("Seal", characterShop.transform, new Color(.08f, .42f, .51f, .96f));
        OceanUI.SetRect(seal.rectTransform, new Vector2(.16f, .18f), new Vector2(.84f, .86f), Vector2.zero, Vector2.zero);
        TMP_Text sealName = OceanUI.CreateText("SEAL", seal.transform, 54f, OceanUI.Sand);
        OceanUI.SetRect(sealName.rectTransform, new Vector2(.05f, .52f), new Vector2(.95f, .63f), Vector2.zero, Vector2.zero);
        TMP_Text sealInfo = OceanUI.CreateText("A playful new ocean explorer", seal.transform, 28f, OceanUI.Foam);
        OceanUI.SetRect(sealInfo.rectTransform, new Vector2(.08f, .35f), new Vector2(.92f, .52f), Vector2.zero, Vector2.zero);
        Button buy = OceanUI.CreateButton("Buy Seal", $"BUY  {SealCost} PEARLS", seal.transform, OceanUI.Sand, BuySeal);
        OceanUI.SetRect(buy.GetComponent<RectTransform>(), new Vector2(.17f, .08f), new Vector2(.83f, .32f), Vector2.zero, Vector2.zero);
        feedback = OceanUI.CreateText("", page, 24f, OceanUI.Foam);
        feedback.name = "Shop Feedback";
        OceanUI.SetRect(feedback.rectTransform, new Vector2(.08f, .04f), new Vector2(.92f, .13f), Vector2.zero, Vector2.zero);
        ShopCategory(true);
        return page;
    }

    private void BuildPowerCard(Transform parent, int i)
    {
        float x = i % 2 == 0 ? 0f : .51f, y = i < 2 ? .52f : .03f;
        Image card = OceanUI.CreatePanel(PowerNames[i], parent, new Color(.04f, .35f, .44f, .96f));
        OceanUI.SetRect(card.rectTransform, new Vector2(x, y), new Vector2(x + .49f, y + .45f), Vector2.zero, Vector2.zero);
        TMP_Text title = OceanUI.CreateText(PowerNames[i].ToUpperInvariant(), card.transform, 27f, OceanUI.Sand);
        TMP_Text info = OceanUI.CreateText(PowerInfo[i], card.transform, 21f, OceanUI.Foam);
        OceanUI.SetRect(title.rectTransform, new Vector2(.04f, .42f), new Vector2(.96f, .58f), Vector2.zero, Vector2.zero);
        OceanUI.SetRect(info.rectTransform, new Vector2(.06f, .29f), new Vector2(.94f, .42f), Vector2.zero, Vector2.zero);
        int item = i;
        Button buy = OceanUI.CreateButton("Buy", $"{PowerCosts[i]} PEARLS", card.transform, OceanUI.Aqua, () => BuyPower(item));
        OceanUI.SetRect(buy.GetComponent<RectTransform>(), new Vector2(.12f, .05f), new Vector2(.88f, .28f), Vector2.zero, Vector2.zero);
        buy.GetComponentInChildren<TMP_Text>().fontSize = 22f;
    }

    private RectTransform BuildCharacterPage()
    {
        RectTransform page = Page("Character Selection Page");
        AddOpaquePageBackground(page, new Color32(4, 52, 72, 255));
        AddTitle(page, "CHARACTERS", "Select who crosses the Busy Reef");
        CharacterCard(page, "TURTLE", "DEFAULT", 0, .07f, OceanUI.Aqua);
        CharacterCard(page, "SEAL", $"{SealCost} PEARLS", 1, .52f, OceanUI.Sand);
        characterFeedback = OceanUI.CreateText("", page, 29f, OceanUI.Foam);
        characterFeedback.name = "Character Feedback";
        OceanUI.SetRect(characterFeedback.rectTransform, new Vector2(.08f, .15f), new Vector2(.92f, .28f), Vector2.zero, Vector2.zero);
        return page;
    }

    private void CharacterCard(Transform parent, string name, string note, int index, float x, Color color)
    {
        Image card = OceanUI.CreatePanel(name, parent, new Color(.04f, .31f, .41f, .96f));
        OceanUI.SetRect(card.rectTransform, new Vector2(x, .35f), new Vector2(x + .41f, .73f), Vector2.zero, Vector2.zero);
        TMP_Text icon = OceanUI.CreateText("", card.transform, 78f, color);
        TMP_Text title = OceanUI.CreateText(name, card.transform, 32f, OceanUI.Foam);
        OceanUI.SetRect(icon.rectTransform, new Vector2(.1f, .43f), new Vector2(.9f, .94f), Vector2.zero, Vector2.zero);
        OceanUI.SetRect(title.rectTransform, new Vector2(.05f, .28f), new Vector2(.95f, .50f), Vector2.zero, Vector2.zero);
        Button choose = OceanUI.CreateButton("Choose", note, card.transform, color, () => SelectCharacter(index));
        OceanUI.SetRect(choose.GetComponent<RectTransform>(), new Vector2(.1f, .05f), new Vector2(.9f, .27f), Vector2.zero, Vector2.zero);
        choose.GetComponentInChildren<TMP_Text>().fontSize = 22f;
    }

    private RectTransform BuildSettingsPage()
    {
        RectTransform page = Page("Settings Page");
        AddOpaquePageBackground(page, new Color32(3, 39, 61, 255));
        AddTitle(page, "SETTINGS", "Audio and controls are saved automatically");
        Button sound = Setting(page, "SOUND", "", .66f, () => { GameAudioManager.ToggleMute(); RefreshAll(); });
        soundLabel = sound.GetComponentInChildren<TMP_Text>();
        soundLabel.name = "Sound Value";
        bgmSlider = CreateVolumeSetting(page, "BGM", "BGM Slider", "BGM Value", .51f, GameAudioManager.BgmVolume, out bgmValueLabel);
        sfxSlider = CreateVolumeSetting(page, "SFX", "SFX Slider", "SFX Value", .37f, GameAudioManager.SfxVolume, out sfxValueLabel);
        bgmSlider.onValueChanged.AddListener(value => { GameAudioManager.SetBgmVolume(value); RefreshAll(); });
        sfxSlider.onValueChanged.AddListener(value => { GameAudioManager.SetSfxVolume(value); RefreshAll(); });
        Button touch = Setting(page, "TOUCH PAD", "", .22f, ToggleTouch);
        touchLabel = touch.GetComponentInChildren<TMP_Text>();
        touchLabel.name = "Touch Pad Value";
        Button about = OceanUI.CreateButton("ABOUT Open", "ABOUT", page, OceanUI.ButtonFrame, () => SetAboutVisible(true));
        OceanUI.SetRect(about.GetComponent<RectTransform>(), new Vector2(.28f, .07f), new Vector2(.72f, .17f), Vector2.zero, Vector2.zero);
        BuildAboutPanel(page);
        return page;
    }

    private Slider CreateVolumeSetting(Transform parent, string label, string sliderName,
        string valueName, float y, float initialValue, out TMP_Text valueLabel)
    {
        TMP_Text name = OceanUI.CreateText(label, parent, 34f, OceanUI.Foam, TextAlignmentOptions.Left);
        OceanUI.SetRect(name.rectTransform, new Vector2(.10f, y), new Vector2(.30f, y + .10f), Vector2.zero, Vector2.zero);

        Image track = OceanUI.CreatePanel(sliderName, parent, new Color32(42, 130, 153, 255));
        OceanUI.MakeRounded(track);
        OceanUI.SetRect(track.rectTransform, new Vector2(.31f, y + .025f), new Vector2(.78f, y + .075f), Vector2.zero, Vector2.zero);
        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        Image fill = OceanUI.CreatePanel("Fill", track.transform, OceanUI.Aqua);
        OceanUI.MakeRounded(fill);
        OceanUI.SetRect(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        Image handle = OceanUI.CreatePanel("Handle", track.transform, OceanUI.Sand);
        OceanUI.MakeRounded(handle);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(54f, 68f);
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(initialValue);

        valueLabel = OceanUI.CreateText("", parent, 28f, OceanUI.Sand, TextAlignmentOptions.Right);
        valueLabel.name = valueName;
        OceanUI.SetRect(valueLabel.rectTransform, new Vector2(.79f, y), new Vector2(.91f, y + .10f), Vector2.zero, Vector2.zero);
        return slider;
    }

    private void BuildAboutPanel(RectTransform page)
    {
        Image panel = OceanUI.CreatePanel("About Panel", page, new Color32(5, 51, 72, 255));
        OceanUI.Stretch(panel.rectTransform, 0f);
        aboutPanel = panel.gameObject;

        TMP_Text title = OceanUI.CreateText("ABOUT BUSY REEF", panel.transform, 48f, OceanUI.Sand);
        OceanUI.SetRect(title.rectTransform, new Vector2(.06f, .87f), new Vector2(.72f, .98f), Vector2.zero, Vector2.zero);
        Button close = OceanUI.CreateButton("ABOUT Close", "BACK", panel.transform, OceanUI.ButtonFrame, () => SetAboutVisible(false));
        OceanUI.SetRect(close.GetComponent<RectTransform>(), new Vector2(.75f, .88f), new Vector2(.94f, .97f), Vector2.zero, Vector2.zero);

        Image viewportImage = OceanUI.CreatePanel("About Scroll View", panel.transform, new Color32(11, 76, 96, 255));
        OceanUI.SetRect(viewportImage.rectTransform, new Vector2(.06f, .06f), new Vector2(.94f, .85f), Vector2.zero, Vector2.zero);
        Mask mask = viewportImage.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = true;
        scroll.scrollSensitivity = 45f;
        scroll.viewport = viewportImage.rectTransform;

        RectTransform content = OceanUI.CreateObject("About Scroll Content", viewportImage.transform).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 1700f);
        scroll.content = content;

        const string template =
            "WHAT IS BUSY REEF?\n" +
            "[Write a short description of the game, its goal, and the underwater journey here.]\n\n" +
            "TEAM MEMBERS & ROLES\n" +
            "1. [Member One] — [Role / responsibilities]\n\n" +
            "2. [Member Two] — [Role / responsibilities]\n\n" +
            "3. [Member Three] — [Role / responsibilities]\n\n" +
            "4. [Member Four] — [Role / responsibilities]\n\n" +
            "THIRD-PARTY ASSETS\n" +
            "• [Asset name] — [Creator / source / licence]\n" +
            "• [Asset name] — [Creator / source / licence]\n" +
            "• Creamy Chicken font — [Add creator and licence]\n\n" +
            "TOOLS & ACKNOWLEDGEMENTS\n" +
            "[List software, libraries, audio, tutorials, and other credits.]\n\n" +
            "VERSION\n" +
            "[Version number and date]";
        TMP_Text body = OceanUI.CreateText(template, content, 30f, OceanUI.Foam, TextAlignmentOptions.TopLeft);
        body.name = "About Content Text";
        OceanUI.Stretch(body.rectTransform, 34f);
        body.overflowMode = TextOverflowModes.Overflow;
        aboutPanel.SetActive(false);
    }

    private Button Setting(Transform parent, string label, string value, float y, Action action)
    {
        TMP_Text name = OceanUI.CreateText(label, parent, 36f, OceanUI.Foam, TextAlignmentOptions.Left);
        OceanUI.SetRect(name.rectTransform, new Vector2(.12f, y), new Vector2(.48f, y + .11f), Vector2.zero, Vector2.zero);
        Button b = OceanUI.CreateButton(label + " Toggle", value, parent, OceanUI.Aqua, action);
        OceanUI.SetRect(b.GetComponent<RectTransform>(), new Vector2(.53f, y), new Vector2(.87f, y + .11f), Vector2.zero, Vector2.zero);
        return b;
    }

    private void BuildNavigation(RectTransform root)
    {
        GameObject bleedObject = OceanUI.CreateObject("Navigation Edge Fill", hubCanvas.transform);
        Color navigationColor = OceanUI.Panel;
        Image bleedImage = bleedObject.AddComponent<Image>();
        bleedImage.color = navigationColor;
        bleedImage.raycastTarget = false;
        navigationBleed = bleedObject.GetComponent<RectTransform>();
        bleedObject.transform.SetSiblingIndex(1);

        Image bar = OceanUI.CreatePanel("Five Button Navigation", root, navigationColor);
        bar.sprite = null;
        bar.type = Image.Type.Simple;
        bar.color = Color.clear;
        navigationBar = bar.rectTransform;
        OceanUI.SetRect(bar.rectTransform, new Vector2(.015f, .015f), new Vector2(.985f, .14f), Vector2.zero, Vector2.zero);
        string[] labels = { "MODE", "SHOP", "PLAY", "CHARACTER", "SETTINGS" };
        for (int i = 0; i < 5; i++)
        {
            int page = i;
            Color tileColor = i % 3 == 0 ? OceanUI.Coral : i % 3 == 1 ? OceanUI.Sand : OceanUI.Aqua;
            Button b = OceanUI.CreateButton(labels[i], labels[i], bar.transform, tileColor, () => Navigate(page));
            float x0 = i / 5f + .008f, x1 = (i + 1) / 5f - .008f;
            OceanUI.SetRect(b.GetComponent<RectTransform>(), new Vector2(x0, .10f), new Vector2(x1, .94f), Vector2.zero, Vector2.zero);
            b.GetComponentInChildren<TMP_Text>().fontSize = i == 3 ? 18f : 22f;
            navTiles.Add(b.GetComponent<Image>());
        }
    }

    private void Navigate(int index)
    {
        if (index == currentPage) return;
        slideFromPage = currentPage;
        currentPage = index;
        for (int i = 0; i < pages.Count; i++) pages[i].gameObject.SetActive(i == slideFromPage || i == currentPage);
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(Slide());
        RefreshAll();
    }

    private IEnumerator Slide()
    {
        bool landscape = Screen.width > Screen.height;
        float distance = Mathf.Max(1f, landscape ? pageArea.rect.height : pageArea.rect.width), elapsed = 0f;
        Vector2[] start = new Vector2[pages.Count];
        for (int i = 0; i < pages.Count; i++) start[i] = pages[i].anchoredPosition;
        while (elapsed < .26f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / .26f), 3f);
            for (int i = 0; i < pages.Count; i++)
            {
                Vector2 target = landscape ? new Vector2(0f, (currentPage - i) * distance) : new Vector2((i - currentPage) * distance, 0f);
                pages[i].anchoredPosition = Vector2.Lerp(start[i], target, t);
            }
            yield return null;
        }
        PositionPages();
        SetRestingPageVisibility();
        slideRoutine = null;
    }

    private void PositionPages()
    {
        bool landscape = Screen.width > Screen.height;
        float distance = Mathf.Max(1f, landscape ? pageArea.rect.height : pageArea.rect.width);
        for (int i = 0; i < pages.Count; i++)
            pages[i].anchoredPosition = landscape ? new Vector2(0f, (currentPage - i) * distance) : new Vector2((i - currentPage) * distance, 0f);
    }

    private void SetRestingPageVisibility()
    {
        for (int i = 0; i < pages.Count; i++) pages[i].gameObject.SetActive(i == currentPage);
    }

    private void ApplyHubOrientation()
    {
        if (navigationBar == null) return;
        bool landscape = Screen.width > Screen.height;
        if (landscape)
        {
            float boundary = LandscapeContentBoundary();
            OceanUI.SetRect(navigationBleed, new Vector2(0f, 0f), new Vector2(boundary, 1f), Vector2.zero, Vector2.zero);
            OceanUI.SetRect(fullPageBackdrop.rectTransform, new Vector2(boundary, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            OceanUI.SetRect(navigationBar, new Vector2(0f, .10f), new Vector2(.135f, .89f), Vector2.zero, Vector2.zero);
            OceanUI.SetRect(pageArea, new Vector2(.14f, .02f), new Vector2(.995f, .91f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < navTiles.Count; i++)
            {
                float yMin = 1f - (i + 1) / 5f + .025f;
                float yMax = 1f - i / 5f - .025f;
                OceanUI.SetRect(navTiles[i].rectTransform, new Vector2(.07f, yMin), new Vector2(.93f, yMax), Vector2.zero, Vector2.zero);
            }
        }
        else
        {
            float boundary = PortraitContentBoundary();
            OceanUI.SetRect(navigationBleed, new Vector2(0f, 0f), new Vector2(1f, boundary), Vector2.zero, Vector2.zero);
            OceanUI.SetRect(fullPageBackdrop.rectTransform, new Vector2(0f, boundary), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            OceanUI.SetRect(navigationBar, new Vector2(0f, 0f), new Vector2(1f, .14f), Vector2.zero, Vector2.zero);
            OceanUI.SetRect(pageArea, new Vector2(0f, .145f), new Vector2(1f, .91f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < navTiles.Count; i++)
            {
                float xMin = i / 5f + .008f;
                float xMax = (i + 1) / 5f - .008f;
                OceanUI.SetRect(navTiles[i].rectTransform, new Vector2(xMin, .10f), new Vector2(xMax, .94f), Vector2.zero, Vector2.zero);
            }
        }
    }

    private float LandscapeContentBoundary()
    {
        Rect safe = Screen.safeArea;
        if (Screen.width <= 0) return .15f;
        return Mathf.Clamp01((safe.xMin + safe.width * .14f) / Screen.width);
    }

    private float PortraitContentBoundary()
    {
        Rect safe = Screen.safeArea;
        if (Screen.height <= 0) return .16f;
        return Mathf.Clamp01((safe.yMin + safe.height * .145f) / Screen.height);
    }

    private void AddOpaquePageBackground(RectTransform page, Color color)
    {
        Image background = OceanUI.CreatePanel("Opaque Page Background", page, color);
        // Full-page color is rendered once at canvas level. This transparent
        // raycast layer keeps the page interactive without double-color overlap.
        background.color = Color.clear;
        OceanUI.Stretch(background.rectTransform, 0f);
        background.transform.SetAsFirstSibling();
    }

    private void AddSplitPageArtwork(RectTransform page, bool upperHalf)
    {
        RawImage artwork = OceanUI.CreateObject("Split Page Artwork", page).AddComponent<RawImage>();
        artwork.texture = Resources.Load<Sprite>(upperHalf ? "UI/ModeShopBackground" : "UI/ShopLowerBackground")?.texture;
        artwork.uvRect = upperHalf ? new Rect(0f, .5f, 1f, .5f) : new Rect(0f, 0f, 1f, 1f);
        artwork.raycastTarget = false;
        OceanUI.Stretch(artwork.rectTransform, 0f);
        artwork.transform.SetSiblingIndex(1);
    }

    private void ShopCategory(bool powers)
    {
        if (powerShop != null) powerShop.SetActive(powers);
        if (characterShop != null) characterShop.SetActive(!powers);
        if (feedback != null) feedback.text = powers ? "POWER-UPS ARE USED ON A FUTURE DIVE" : "THE SEAL STAYS UNLOCKED FOREVER";
    }

    private void BuyPower(int i)
    {
        if (GameSession.PowerUpCount(i) > 0)
            feedback.text = "ALREADY READY FOR NEXT DIVE";
        else
            feedback.text = GameSession.BuyPowerUp(i, PowerCosts[i])
                ? PowerNames[i].ToUpperInvariant() + " EQUIPPED FOR NEXT DIVE"
                : "NOT ENOUGH PEARLS";
        RefreshAll();
    }

    private void BuySeal()
    {
        bool bought = GameSession.BuySkin(1, SealCost);
        if (bought) GameSession.EquipSkin(1);
        if (bought) FindAnyObjectByType<PlayerController>()?.RefreshCharacter();
        feedback.text = bought ? "SEAL UNLOCKED AND SELECTED" : "NOT ENOUGH PEARLS";
        RefreshAll();
    }

    private void SelectCharacter(int index)
    {
        if (!GameSession.OwnsSkin(index)) { characterFeedback.text = $"BUY THE SEAL IN SHOP FOR {SealCost} PEARLS"; return; }
        GameSession.EquipSkin(index);
        FindAnyObjectByType<PlayerController>()?.RefreshCharacter();
        characterFeedback.text = GameSession.EquippedCharacterName.ToUpperInvariant() + " SELECTED";
        RefreshAll();
    }

    private void ToggleTouch()
    {
        GameSession.ShowTouchControls = !GameSession.ShowTouchControls;
        GameManager.Instance?.RefreshTouchControls();
        RefreshAll();
    }

    private void SetAboutVisible(bool visible)
    {
        if (aboutPanel != null) aboutPanel.SetActive(visible);
    }

    private static string DisplayModeName(FishGameMode mode)
    {
        return mode switch
        {
            FishGameMode.TimeAttack => "TIME ATTACK",
            FishGameMode.Riptide => "RIPTIDE",
            _ => mode.ToString().ToUpperInvariant()
        };
    }

    private void RefreshAll()
    {
        if (fullPageBackdrop != null)
        {
            bool opaquePage = currentPage == 0 || currentPage == 1 || currentPage == 3 || currentPage == 4;
            fullPageBackdrop.gameObject.SetActive(opaquePage);
            Image pageArtwork = opaquePage && currentPage < pages.Count
                ? FindDeepChild(pages[currentPage], "Opaque Page Background")?.GetComponent<Image>()
                : null;
            RawImage splitArtwork = opaquePage && currentPage < pages.Count
                ? FindDeepChild(pages[currentPage], "Split Page Artwork")?.GetComponent<RawImage>()
                : null;
            bool hasSplitArtwork = splitArtwork != null && splitArtwork.texture != null;
            if (splitPageBackdrop != null)
            {
                splitPageBackdrop.gameObject.SetActive(hasSplitArtwork);
                if (hasSplitArtwork)
                {
                    splitPageBackdrop.texture = splitArtwork.texture;
                    splitPageBackdrop.uvRect = splitArtwork.uvRect;
                }
            }
            bool hasArtwork = pageArtwork != null && pageArtwork.sprite != null && pageArtwork.color.a > .5f;
            fullPageBackdrop.sprite = !hasSplitArtwork && hasArtwork ? pageArtwork.sprite : null;
            fullPageBackdrop.type = Image.Type.Simple;
            fullPageBackdrop.color = hasSplitArtwork ? Color.clear : hasArtwork ? Color.white :
                currentPage == 0 ? new Color32(155, 220, 240, 255) :
                currentPage == 1 ? new Color32(174, 226, 242, 255) :
                currentPage == 3 ? new Color32(255, 169, 199, 255) : new Color32(171, 226, 242, 255);
        }
        RefreshWallet();
        if (homeText != null) homeText.text = $"{DisplayModeName(GameSession.Mode)} MODE\n{GameSession.EquippedCharacterName.ToUpperInvariant()} SELECTED";
        if (touchLabel != null) touchLabel.text = GameSession.ShowTouchControls ? "ON" : "OFF";
        if (soundLabel != null) soundLabel.text = GameAudioManager.Muted ? "OFF" : "ON";
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(GameAudioManager.BgmVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameAudioManager.SfxVolume);
        if (bgmValueLabel != null) bgmValueLabel.text = Mathf.RoundToInt(GameAudioManager.BgmVolume * 100f) + "%";
        if (sfxValueLabel != null) sfxValueLabel.text = Mathf.RoundToInt(GameAudioManager.SfxVolume * 100f) + "%";
        if (characterFeedback != null && string.IsNullOrEmpty(characterFeedback.text)) characterFeedback.text = "CURRENT: " + GameSession.EquippedCharacterName.ToUpperInvariant();
        RefreshShopAndCharacterButtons();
        RefreshReadyEquipmentIcons();
        for (int i = 0; i < navTiles.Count; i++)
        {
            Color tileColor = i % 3 == 0 ? OceanUI.Coral : i % 3 == 1 ? OceanUI.Sand : OceanUI.Aqua;
            navTiles[i].color = i == currentPage ? Color.Lerp(tileColor, Color.white, 0.38f) : tileColor;
        }
        if (stageTitle != null)
        {
            int i = Mathf.Clamp((int)GameSession.Mode, 0, CarouselModes.Length - 1);
            string modeName = DisplayModeName(GameSession.Mode);
            stageTitle.text = modeName + " MODE";
            stageDescription.text = ModeDescriptions[i];
        }
        carousel?.SetTutorialLocked(false);
        if (stagePrevious != null) stagePrevious.gameObject.SetActive(true);
        if (stageNext != null) stageNext.gameObject.SetActive(true);
    }

    private void RefreshShopAndCharacterButtons()
    {
        Transform root = hubCanvas != null ? OceanUI.SafeRoot(hubCanvas) : null;
        for (int i = 0; i < PowerNames.Length; i++)
        {
            Transform card = FindDeepChild(powerShop != null ? powerShop.transform : null, PowerNames[i]);
            Button buyPower = ComponentAt<Button>(card, "Buy");
            if (buyPower == null) continue;
            bool ready = GameSession.PowerUpCount(i) > 0;
            SetButtonText(buyPower, ready ? "SOLD - READY" : $"{PowerCosts[i]} PEARLS");
            buyPower.interactable = !ready;
        }
        Button sealBuy = ComponentAt<Button>(root, "Buy Seal");
        bool ownsSeal = GameSession.OwnsSkin(1);
        if (sealBuy != null)
        {
            SetButtonText(sealBuy, ownsSeal ? "SOLD" : $"BUY  {SealCost} PEARLS");
            sealBuy.interactable = !ownsSeal;
        }

        Transform characterPage = FindDeepChild(root, "Character Selection Page");
        Button turtle = ComponentAt<Button>(FindDeepChild(characterPage, "TURTLE"), "Choose");
        Button seal = ComponentAt<Button>(FindDeepChild(characterPage, "SEAL"), "Choose");
        SetButtonText(turtle, GameSession.EquippedCharacter == 0 ? "EQUIPPED" : "EQUIP");
        SetButtonText(seal, !ownsSeal ? "LOCKED" : GameSession.EquippedCharacter == 1 ? "EQUIPPED" : "EQUIP");
        if (turtle != null) turtle.interactable = GameSession.EquippedCharacter != 0;
        if (seal != null) seal.interactable = ownsSeal && GameSession.EquippedCharacter != 1;
    }

    private static void SetButtonText(Button button, string value)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    private void CreateReadyEquipmentIcons(Transform page)
    {
        Image backing = OceanUI.CreatePanel("Ready Equipment Backing", page, new Color(.08f, .34f, .49f, .82f));
        backing.raycastTarget = false;
        OceanUI.SetRect(backing.rectTransform, new Vector2(.17f, .31f), new Vector2(.83f, .41f), Vector2.zero, Vector2.zero);
        for (int i = 0; i < readyEquipmentIcons.Length; i++)
            readyEquipmentIcons[i] = CreateReadyEquipmentIcon(page, i);
        RememberReadyEquipmentSlots();
    }

    private void EnsureReadyEquipmentIcons(Transform root)
    {
        Transform playPage = FindDeepChild(root, "Play Page");
        for (int i = 0; i < readyEquipmentIcons.Length; i++)
        {
            string iconName = i == 0 ? "Ready Character Icon" : "Ready Power " + i;
            readyEquipmentIcons[i] = ComponentAt<Image>(playPage, iconName);
            if (readyEquipmentIcons[i] == null)
                Debug.LogError("Editable Canvas is missing " + iconName + ". Re-bake the Gameplay Canvas.");
        }
        RememberReadyEquipmentSlots();
    }

    private void RememberReadyEquipmentSlots()
    {
        for (int i = 0; i < readyEquipmentIcons.Length; i++)
        {
            if (readyEquipmentIcons[i] == null) continue;
            RectTransform rect = readyEquipmentIcons[i].rectTransform;
            readySlotMin[i] = rect.anchorMin;
            readySlotMax[i] = rect.anchorMax;
            readySlotPosition[i] = rect.anchoredPosition;
            readySlotSize[i] = rect.sizeDelta;
        }
    }

    private Image CreateReadyEquipmentIcon(Transform parent, int index)
    {
        string iconName = index == 0 ? "Ready Character Icon" : "Ready Power " + index;
        Image icon = OceanUI.CreatePanel(iconName, parent, GameManager.EquipmentPlaceholderColor(index));
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        float width = 0.105f;
        float gap = 0.018f;
        float left = 0.19f + index * (width + gap);
        OceanUI.SetRect(icon.rectTransform, new Vector2(left, .325f), new Vector2(left + width, .395f), Vector2.zero, Vector2.zero);
        TMP_Text label = OceanUI.CreateText("", icon.transform, 18f, OceanUI.Deep);
        label.name = "Equipment Label";
        return icon;
    }

    private void RefreshReadyEquipmentIcons()
    {
        GameManager manager = GameManager.Instance;
        string[] labels = { GameSession.EquippedCharacterName.ToUpperInvariant(), "SHIELD", "DASH", "MAGNET", "INVINCIBLE" };
        Transform characterPage = FindDeepChild(hubCanvas != null ? hubCanvas.transform : null, "Character Selection Page");
        GameObject turtleModel = ComponentAt<UI3DModelPreview>(characterPage, "TURTLE")?.modelPrefab;
        GameObject sealModel = ComponentAt<UI3DModelPreview>(characterPage, "SEAL")?.modelPrefab;
        int nextOccupiedSlot = 0;
        for (int i = 0; i < readyEquipmentIcons.Length; i++)
        {
            Image icon = readyEquipmentIcons[i];
            if (icon == null) continue;
            bool available = i == 0 || GameSession.PowerUpCount(i - 1) > 0;
            icon.gameObject.SetActive(available);
            if (!available) continue;
            // Keep the author's slot positions and pack only the owned powers.
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = readySlotMin[nextOccupiedSlot];
            rect.anchorMax = readySlotMax[nextOccupiedSlot];
            rect.anchoredPosition = readySlotPosition[nextOccupiedSlot];
            rect.sizeDelta = readySlotSize[nextOccupiedSlot];
            nextOccupiedSlot++;
            UI3DModelPreview modelPreview = icon.GetComponent<UI3DModelPreview>();
            if (i == 0 && modelPreview != null)
                modelPreview.modelPrefab = GameSession.EquippedCharacter == 1 ? sealModel : turtleModel;
            bool has3DModel = modelPreview != null && modelPreview.modelPrefab != null;
            Sprite sprite = i == 0 ? manager?.GetCharacterEquipmentIcon() : manager?.GetPowerEquipmentIcon(i - 1);
            icon.sprite = has3DModel ? null : sprite;
            icon.color = has3DModel ? Color.clear : sprite != null ? Color.white : GameManager.EquipmentPlaceholderColor(i);
            TMP_Text label = ComponentAt<TMP_Text>(icon.transform, "Equipment Label");
            if (label != null)
            {
                label.gameObject.SetActive(sprite == null && !has3DModel);
                label.text = i == 0 ? labels[i] : labels[i] + " x" + GameSession.PowerUpCount(i - 1);
            }
        }
    }

    public void SetShopMenu() => Navigate(1);
    public void SetSettingsMenu() => Navigate(4);
    public void SetModeSelectionMenu() => Navigate(0);
    public void HideGameplayHub()
    {
        if (hubCanvas != null) hubCanvas.gameObject.SetActive(false);
    }

    public void ShowGameplayHub()
    {
        if (hubCanvas != null) hubCanvas.gameObject.SetActive(true);
        if (pageArea != null) pageArea.gameObject.SetActive(true);
        if (navigationBar != null) navigationBar.gameObject.SetActive(true);
        if (navigationBleed != null) navigationBleed.gameObject.SetActive(true);
        SetRestingPageVisibility();
        RefreshAll();
    }

    public void RefreshWallet()
    {
        if (shopWalletText != null) shopWalletText.text = $"PEARL  {GameSession.PearlWallet}";
    }

    public void PlayGame()
    {
        if (gameplayHost && GameManager.Instance != null)
        {
            HideGameplayHub();
            GameManager.Instance.StartGame();
            return;
        }
        SceneManager.LoadScene("Gameplay");
    }
}
