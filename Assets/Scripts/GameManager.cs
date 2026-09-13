using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // Two pearls per row across eight rows makes the magnet pull clearly visible.
    private const int TutorialMagnetPearlGoal = 16;

    private enum TutorialLesson
    {
        Movement, Pearls, Coral, Squid, Jellyfish, Pufferfish, Crab, Shark,
        BubbleShield, SpeedDash, PearlMagnet, Invincibility, Complete
    }
    public static GameManager Instance;

    public bool gameStarted;
    public bool gameOver;
    public Transform player;
    public Camera mainCamera;
    public TMP_Text tapToStartText;
    [Header("Equipment Icon Sprites")]
    [Tooltip("Assign the turtle icon shown in the ready equipment row.")]
    public Sprite turtleEquipmentIcon;
    [Tooltip("Assign the seal icon shown in the ready equipment row.")]
    public Sprite sealEquipmentIcon;
    [Tooltip("Assign in order: Bubble Shield, Speed Dash, Pearl Magnet, Invincibility.")]
    public Sprite[] powerEquipmentIcons = new Sprite[4];
    [Range(0.05f, 0.24f)]
    [Tooltip("Lose when the visible character centre retreats below this height, measured from the bottom.")]
    public float retreatLossScreenY = 0.17f;

    private PlayerController playerController;
    private TMP_Text scoreText;
    private TMP_Text bestScoreText;
    private TMP_Text pearlText;
    private TMP_Text timerText;
    private TMP_Text statusText;
    private TMP_Text powerText;
    private TMP_Text crabText;
    private TMP_Text tutorialObjectiveText;
    private Image inkCloud;
    private GameObject pauseOverlay;
    private GameObject gameOverOverlay;
    private GameObject tutorialCompleteOverlay;
    private Button pauseButton;
    private GameObject pearlChip;
    private GameObject tutorialObjectivePanel;
    private TMP_Text resultText;
    private readonly Image[] gameplayPowerIcons = new Image[4];
    private float remainingTime = 60f;
    private int score;
    private bool paused;
    private bool shieldReady;
    private bool speedDashReady;
    private float magnetUntil;
    private float invincibleUntil;
    private int crabStep = -1;
    private SeaObstacle crabObstacle;
    private float crabSafeUntil;
    private Coroutine statusRoutine;
    private MainMenuBehaviour gameplayHub;
    private bool runPrepared;
    private int tutorialForwardMoves;
    private bool tutorialSideMove;
    private int tutorialPearls;
    private float tutorialTargetZ;
    private MapManager tutorialMap;
    private TutorialLesson tutorialLesson;
    private bool tutorialPowerCollected;
    private bool tutorialInvincibleSharkHit;
    private int tutorialDashForwardMoves;
    private float tutorialDashStartZ;

    public bool HasSpeedDash => gameStarted && speedDashReady;
    public bool HasPearlMagnet => gameStarted && Time.time < magnetUntil;
    public bool IsInvincible => gameStarted && Time.time < invincibleUntil;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        GameAudioManager.EnsureInstance();
        playerController = player != null ? player.GetComponent<PlayerController>() : FindAnyObjectByType<PlayerController>();
        if (player == null && playerController != null) player = playerController.transform;
        if (mainCamera == null) mainCamera = Camera.main;
        BindGameplayUI();
        ApplyStageAtmosphere();
        UpdateHud();
        gameplayHub = gameObject.AddComponent<MainMenuBehaviour>();
        gameplayHub.BuildOnGameplay();
        FindAnyObjectByType<CameraController>()?.PrepareForSelectedRun();
    }

    private void Update()
    {
        if (!gameStarted || gameOver || paused) return;

        if (GameSession.Mode == FishGameMode.TimeAttack)
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                GameOver("Time is up");
            }
        }

        CheckPlayerOutsideCamera();
        UpdateHud();
    }

    private void BuildGameplayUI()
    {
        Canvas canvas = OceanUI.CreateCanvas("Busy Reef Gameplay HUD");
        RectTransform root = OceanUI.SafeRoot(canvas);
        AdaptiveUILayout adaptive = root.gameObject.AddComponent<AdaptiveUILayout>();
        adaptive.scaler = canvas.GetComponent<CanvasScaler>();

        Image gestureSurface = OceanUI.CreatePanel("Swipe Surface", root, Color.clear);
        OceanUI.Stretch(gestureSurface.rectTransform, 0f);
        GameplayGestureInput gestures = gestureSurface.gameObject.AddComponent<GameplayGestureInput>();
        gestures.player = playerController;

        Image pearlPanel = OceanUI.CreatePanel("Pearls", root, Color.clear);
        pearlPanel.raycastTarget = false;
        pearlChip = pearlPanel.gameObject;
        OceanUI.SetRect(pearlPanel.rectTransform, new Vector2(0.025f, 0.885f), new Vector2(0.36f, 0.98f), Vector2.zero, Vector2.zero);
        pearlText = OceanUI.CreateText("PEARL  0", pearlPanel.transform, 46f, OceanUI.Sand, TextAlignmentOptions.Left);
        pearlText.name = "Pearl Text";
        pearlChip.SetActive(false);

        Image scoreChip = OceanUI.CreatePanel("Score", root, Color.clear);
        scoreChip.raycastTarget = false;
        OceanUI.SetRect(scoreChip.rectTransform, new Vector2(0.67f, 0.885f), new Vector2(0.975f, 0.98f), Vector2.zero, Vector2.zero);
        scoreText = OceanUI.CreateText("0", scoreChip.transform, 84f, OceanUI.Foam, TextAlignmentOptions.Right);
        scoreText.name = "Score Text";
        bestScoreText = OceanUI.CreateText("BEST  0", root, 42f, OceanUI.Sand, TextAlignmentOptions.Right);
        bestScoreText.name = "Best Score";
        OceanUI.SetRect(bestScoreText.rectTransform, new Vector2(0.67f, 0.855f), new Vector2(0.975f, 0.90f), Vector2.zero, Vector2.zero);
        timerText = OceanUI.CreateText("", root, 28f, OceanUI.Coral, TextAlignmentOptions.Right);
        timerText.name = "Timer Text";
        timerText.fontSize = 52f;
        timerText.alignment = TextAlignmentOptions.Left;
        OceanUI.SetRect(timerText.rectTransform, new Vector2(.025f, .815f), new Vector2(.43f, .89f), Vector2.zero, Vector2.zero);

        pauseButton = OceanUI.CreateButton("Pause", "II", root, Color.clear, TogglePause);
        TMP_Text pauseLabel = pauseButton.GetComponentInChildren<TMP_Text>();
        pauseLabel.fontSize = 56f;
        pauseLabel.color = OceanUI.Sand;
        OceanUI.SetRect(pauseButton.GetComponent<RectTransform>(), new Vector2(.855f, .805f), new Vector2(.985f, .885f), Vector2.zero, Vector2.zero);
        pauseButton.gameObject.SetActive(false);

        CreateGameplayEquipmentIcons(root);

        powerText = OceanUI.CreateText("", root, 24f, OceanUI.Foam, TextAlignmentOptions.Left);
        powerText.name = "Power Text";
        OceanUI.SetRect(powerText.rectTransform, new Vector2(0.03f, 0.84f), new Vector2(0.65f, 0.895f), Vector2.zero, Vector2.zero);
        powerText.gameObject.SetActive(false);

        BuildDpad(root, adaptive);

        statusText = OceanUI.CreateText("", root, 37f, OceanUI.Sand);
        statusText.name = "Status Text";
        OceanUI.SetRect(statusText.rectTransform, new Vector2(0.10f, 0.63f), new Vector2(0.90f, 0.74f), Vector2.zero, Vector2.zero);

        Image objective = OceanUI.CreatePanel("Tutorial Objective", root, new Color(0.02f, 0.20f, 0.29f, 0.90f));
        tutorialObjectivePanel = objective.gameObject;
        OceanUI.SetRect(objective.rectTransform, new Vector2(0.12f, 0.70f), new Vector2(0.88f, 0.79f), Vector2.zero, Vector2.zero);
        tutorialObjectiveText = OceanUI.CreateText("", objective.transform, 28f, OceanUI.Sand);
        tutorialObjectiveText.name = "Tutorial Objective Text";
        tutorialObjectivePanel.SetActive(false);

        // Ink deliberately ignores the safe area so the effect covers the whole
        // physical display, including the notch and curved-edge padding.
        inkCloud = OceanUI.CreatePanel("Ink Cloud", canvas.transform, new Color(0.03f, 0.01f, 0.08f, 0.88f));
        OceanUI.Stretch(inkCloud.rectTransform, 0f);
        inkCloud.raycastTarget = false;
        inkCloud.gameObject.SetActive(false);

        pauseOverlay = BuildModal(root, "CURRENT PAUSED", "Take a breath. Your turtle is safe here.", "RESUME", TogglePause);
        AddModalSecondaryButton(pauseOverlay.transform, "BOTTOM MENU", GoToMenu);
        pauseOverlay.SetActive(false);
        gameOverOverlay = BuildResultOverlay(canvas);
        gameOverOverlay.SetActive(false);
        tutorialCompleteOverlay = BuildModal(root, "TUTORIAL COMPLETE", "You mastered movement, pearls, obstacles and every buff. You are ready for the reef!", "BACK", GoToMenu);
        tutorialCompleteOverlay.SetActive(false);

        crabText = OceanUI.CreateText("", root, 43f, OceanUI.Foam);
        crabText.name = "Crab Escape Text";
        OceanUI.SetRect(crabText.rectTransform, new Vector2(0.08f, 0.37f), new Vector2(0.92f, 0.62f), Vector2.zero, Vector2.zero);
        crabText.gameObject.SetActive(false);
    }

    // Used only by the editor baker. Runtime binds to this saved Canvas instead
    // of creating UI objects, so artists can replace images and add frames freely.
    public void BuildGameplayUIForEditor()
    {
        playerController = player != null ? player.GetComponent<PlayerController>() : FindAnyObjectByType<PlayerController>();
        BuildGameplayUI();
    }

    private void BindGameplayUI()
    {
        Canvas canvas = FindSceneCanvas("Busy Reef Gameplay HUD");
        if (canvas == null)
        {
            Debug.LogError("Gameplay Canvas is missing. Run Tools/Busy Reef/Bake Editable Gameplay Canvas.");
            return;
        }

        canvas.gameObject.SetActive(true);
        Transform root = FindDeepChild(canvas.transform, "SafeArea");
        if (root == null) root = canvas.transform;

        pearlChip = FindDeepChild(root, "Pearls")?.gameObject;
        pearlText = ComponentAt<TMP_Text>(root, "Pearl Text");
        scoreText = ComponentAt<TMP_Text>(root, "Score Text");
        bestScoreText = ComponentAt<TMP_Text>(root, "Best Score");
        timerText = ComponentAt<TMP_Text>(root, "Timer Text");
        powerText = ComponentAt<TMP_Text>(root, "Power Text");
        if (powerText != null) powerText.gameObject.SetActive(false);
        statusText = ComponentAt<TMP_Text>(root, "Status Text");
        tutorialObjectivePanel = FindDeepChild(root, "Tutorial Objective")?.gameObject;
        tutorialObjectiveText = ComponentAt<TMP_Text>(root, "Tutorial Objective Text");
        pauseButton = ComponentAt<Button>(root, "Pause");
        pauseOverlay = FindDeepChild(root, "CURRENT PAUSED")?.gameObject;
        gameOverOverlay = FindDeepChild(canvas.transform, "Dive Result")?.gameObject;
        tutorialCompleteOverlay = FindDeepChild(root, "TUTORIAL COMPLETE")?.gameObject;
        resultText = ComponentAt<TMP_Text>(canvas.transform, "Result Text");
        crabText = ComponentAt<TMP_Text>(root, "Crab Escape Text");
        inkCloud = ComponentAt<Image>(canvas.transform, "Ink Cloud");
        EnsureGameplayEquipmentIcons(root);

        GameplayGestureInput gestures = ComponentAt<GameplayGestureInput>(root, "Swipe Surface");
        if (gestures != null) gestures.player = playerController;
        foreach (TouchMoveButton move in root.GetComponentsInChildren<TouchMoveButton>(true)) move.player = playerController;
        Transform dpad = FindDeepChild(root, "Touch Direction Pad");
        if (dpad != null) dpad.gameObject.SetActive(GameSession.ShowTouchControls);

        WireButton(pauseButton, TogglePause);
        WireButton(ComponentAt<Button>(pauseOverlay != null ? pauseOverlay.transform : null, "Primary"), TogglePause);
        WireButton(ComponentAt<Button>(pauseOverlay != null ? pauseOverlay.transform : null, "Secondary"), GoToMenu);
        WireButton(ComponentAt<Button>(gameOverOverlay != null ? gameOverOverlay.transform : null, "Retry"), Restart);
        WireButton(ComponentAt<Button>(gameOverOverlay != null ? gameOverOverlay.transform : null, "Hub"), GoToMenu);
        WireButton(ComponentAt<Button>(tutorialCompleteOverlay != null ? tutorialCompleteOverlay.transform : null, "Primary"), GoToMenu);

        SetActive(pearlChip, false);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        SetActive(tutorialObjectivePanel, false);
        if (inkCloud != null) inkCloud.gameObject.SetActive(false);
        SetActive(pauseOverlay, false);
        SetActive(gameOverOverlay, false);
        SetActive(tutorialCompleteOverlay, false);
        if (crabText != null) crabText.gameObject.SetActive(false);
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
        button.onClick.AddListener(action);
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null) target.SetActive(value);
    }

    private void BuildDpad(RectTransform root, AdaptiveUILayout adaptive)
    {
        GameObject dpadObject = OceanUI.CreateObject("Touch Direction Pad", root);
        RectTransform dpad = dpadObject.GetComponent<RectTransform>();
        OceanUI.SetCentered(dpad, Vector2.zero, new Vector2(420f, 420f));
        adaptive.dpad = dpad;
        dpadObject.SetActive(GameSession.ShowTouchControls);
        CreateMoveButton(dpad, "^", Vector3.forward, new Vector2(0f, 125f));
        CreateMoveButton(dpad, "v", Vector3.back, new Vector2(0f, -125f));
        CreateMoveButton(dpad, "<", Vector3.left, new Vector2(-125f, 0f));
        CreateMoveButton(dpad, ">", Vector3.right, new Vector2(125f, 0f));
        Image center = OceanUI.CreatePanel("Pad Center", dpad, new Color(0.13f, 0.65f, 0.68f, 0.35f));
        OceanUI.SetCentered(center.rectTransform, Vector2.zero, new Vector2(112f, 112f));
        center.raycastTarget = false;
    }

    private GameObject BuildResultOverlay(Canvas canvas)
    {
        // The dim layer is outside the safe area so it covers curved edges,
        // notches and the entire physical display. Interactive content stays
        // inside its own safe-area container.
        GameObject overlay = OceanUI.CreateObject("Dive Result", canvas.transform);
        OceanUI.Stretch(overlay.GetComponent<RectTransform>(), 0f);
        Image dim = overlay.AddComponent<Image>();
        dim.color = new Color(0.01f, 0.08f, 0.13f, 0.58f);
        GameObject safeObject = OceanUI.CreateObject("Result SafeArea", overlay.transform);
        RectTransform content = safeObject.GetComponent<RectTransform>();
        OceanUI.Stretch(content, 0f);
        safeObject.AddComponent<SafeAreaPanel>();
        resultText = OceanUI.CreateText("", content, 54f, OceanUI.Foam);
        resultText.name = "Result Text";
        OceanUI.SetRect(resultText.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.72f), Vector2.zero, Vector2.zero);
        Button retry = OceanUI.CreateButton("Retry", "RETRY", content, OceanUI.Sand, Restart);
        OceanUI.SetRect(retry.GetComponent<RectTransform>(), new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.22f), Vector2.zero, Vector2.zero);
        Button hub = OceanUI.CreateButton("Hub", "BOTTOM MENU", content, OceanUI.Panel, GoToMenu);
        OceanUI.SetRect(hub.GetComponent<RectTransform>(), new Vector2(0.34f, 0.045f), new Vector2(0.66f, 0.105f), Vector2.zero, Vector2.zero);
        hub.GetComponentInChildren<TMP_Text>().fontSize = 24f;
        return overlay;
    }

    private void CreateMoveButton(RectTransform parent, string label, Vector3 direction, Vector2 position)
    {
        Button button = OceanUI.CreateButton("Move " + label, label, parent, new Color(0.24f, 0.86f, 0.82f, 0.88f), null);
        OceanUI.SetCentered(button.GetComponent<RectTransform>(), position, new Vector2(118f, 118f));
        TouchMoveButton input = button.gameObject.AddComponent<TouchMoveButton>();
        input.player = playerController;
        input.direction = direction;
    }

    private GameObject BuildModal(RectTransform root, string titleValue, string bodyValue, string primaryLabel, UnityEngine.Events.UnityAction primaryAction)
    {
        GameObject overlay = OceanUI.CreateObject(titleValue, root);
        OceanUI.Stretch(overlay.GetComponent<RectTransform>(), 0f);
        Image dim = overlay.AddComponent<Image>();
        dim.color = new Color(0.01f, 0.08f, 0.13f, 0.72f);
        Image panel = OceanUI.CreatePanel("Panel", overlay.transform, OceanUI.Panel);
        OceanUI.SetRect(panel.rectTransform, new Vector2(0.10f, 0.28f), new Vector2(0.90f, 0.73f), Vector2.zero, Vector2.zero);
        TMP_Text title = OceanUI.CreateText(titleValue, panel.transform, 52f, OceanUI.Sand);
        OceanUI.SetRect(title.rectTransform, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);
        TMP_Text body = OceanUI.CreateText(bodyValue, panel.transform, 29f, OceanUI.Foam);
        body.name = "Body";
        OceanUI.SetRect(body.rectTransform, new Vector2(0.08f, 0.33f), new Vector2(0.92f, 0.70f), Vector2.zero, Vector2.zero);
        Button primary = OceanUI.CreateButton("Primary", primaryLabel, panel.transform, OceanUI.Aqua, () => primaryAction());
        OceanUI.SetRect(primary.GetComponent<RectTransform>(), new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.29f), Vector2.zero, Vector2.zero);
        return overlay;
    }

    private void AddModalSecondaryButton(Transform overlay, string label, UnityEngine.Events.UnityAction action)
    {
        Transform panel = overlay.Find("Panel");
        Button primary = panel.Find("Primary")?.GetComponent<Button>();
        if (primary != null) OceanUI.SetRect(primary.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.29f), Vector2.zero, Vector2.zero);
        Button secondary = OceanUI.CreateButton("Secondary", label, panel, OceanUI.Coral, () => action());
        OceanUI.SetRect(secondary.GetComponent<RectTransform>(), new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.29f), Vector2.zero, Vector2.zero);
        secondary.GetComponentInChildren<TMP_Text>().fontSize = 28f;
    }

    private string StartDescription()
    {
        string mode = GameSession.Mode switch
        {
            FishGameMode.Tutorial => "TUTORIAL | Slow current\nSwipe or use the arrows to move one tile. Avoid sea creatures and keep ahead of the camera.",
            FishGameMode.TimeAttack => "TIME ATTACK | 60 seconds\nMove forward quickly. Pearls are currency; starfish add bonus score.",
            FishGameMode.Riptide => "RIPTIDE | Hard mode\nFace denser, faster traffic while pearl trails appear less often.",
            _ => "STANDARD | Endless reef\nSurvive, travel farther, collect pearls and beat your best score."
        };
        return mode;
    }

    private void ApplyStageAtmosphere()
    {
        if (mainCamera == null) return;
        Color[] colors =
        {
            new Color32(23, 139, 157, 255),
            new Color32(8, 91, 119, 255),
            new Color32(3, 25, 55, 255)
        };
        mainCamera.backgroundColor = colors[Mathf.Clamp(GameSession.SelectedStage, 0, colors.Length - 1)];
    }

    public void PreviewSelectedStage()
    {
        if (gameStarted || gameOver) return;
        ApplyStageAtmosphere();
        FindAnyObjectByType<MapManager>()?.ResetForSelectedRun();
        FindAnyObjectByType<CameraController>()?.PrepareForSelectedRun();
    }

    public void TryStartFromTap()
    {
        if (gameplayHub != null && !gameplayHub.IsPlayPage) return;
        StartGame();
    }

    public void StartGame()
    {
        if (gameStarted || gameOver) return;
        if (!runPrepared)
        {
            runPrepared = true;
            // The hub already previews the live gameplay world. Starting must not
            // regenerate or reposition it; only activate the selected run items.
            GameSession.BeginRun();
            shieldReady = GameSession.ShieldReady;
        }
        gameStarted = true;
        gameOver = false;
        paused = false;
        gameplayHub?.HideGameplayHub();
        if (pearlChip != null) pearlChip.SetActive(true);
        if (pauseButton != null) pauseButton.gameObject.SetActive(true);
        if (tapToStartText != null) tapToStartText.gameObject.SetActive(false);
        if (GameSession.SpeedDashReady) speedDashReady = true;
        if (GameSession.PearlMagnetReady) magnetUntil = Time.time + 20f;
        if (GameSession.InvincibilityReady) invincibleUntil = Time.time + 10f;
        if (GameSession.Mode == FishGameMode.Tutorial) BeginScriptedTutorial();
        else ShowStatus("GO!", 1.4f);
    }

    public void AddScore(int amount)
    {
        if (!gameStarted || gameOver) return;
        score += Mathf.Max(0, amount);
        UpdateHud();
    }

    public void CollectPearls(int amount)
    {
        GameSession.CollectPearl(amount);
        if (GameSession.Mode == FishGameMode.Tutorial &&
            (tutorialLesson == TutorialLesson.Pearls ||
             (tutorialLesson == TutorialLesson.PearlMagnet && tutorialPowerCollected)))
        {
            tutorialPearls += Mathf.Max(0, amount);
            int goal = tutorialLesson == TutorialLesson.PearlMagnet ? TutorialMagnetPearlGoal : 3;
            string prefix = tutorialLesson == TutorialLesson.PearlMagnet
                ? "USE THE MAGNET TO COLLECT THE PEARLS AHEAD"
                : "COLLECT 3 PEARLS";
            SetTutorialObjective($"{prefix}  {Mathf.Min(tutorialPearls, goal)} / {goal}");
            if (tutorialPearls >= goal) AdvanceTutorial();
        }
        ShowStatus(amount > 1 ? $"TREASURE! +{amount} PEARLS" : "+1 PEARL", 0.8f);
        UpdateHud();
    }

    public void GrantPowerUp(int index)
    {
        GameSession.GrantPowerUp(index);
        switch (index)
        {
            case 0: shieldReady = true; ShowStatus("BUBBLE SHIELD READY", 1.2f); break;
            case 1: speedDashReady = true; ShowStatus("SPEED DASH READY", 1.2f); break;
            case 2: magnetUntil = Mathf.Max(magnetUntil, Time.time) + 15f; ShowStatus("PEARL MAGNET", 1.2f); break;
            case 3: invincibleUntil = Mathf.Max(invincibleUntil, Time.time) + 8f; ShowStatus("INVINCIBLE BUBBLE", 1.2f); break;
        }
        if (GameSession.Mode == FishGameMode.Tutorial && TutorialPowerIndex(tutorialLesson) == index)
            BeginTutorialPowerTrial(index);
    }

    public void NotifyPlayerMoved(Vector3 direction)
    {
        if (GameSession.Mode != FishGameMode.Tutorial || !gameStarted) return;
        if (tutorialLesson == TutorialLesson.Movement)
        {
            if (direction.z > 0f) tutorialForwardMoves++;
            if (Mathf.Abs(direction.x) > 0f) tutorialSideMove = true;
            SetTutorialObjective($"MOVE: TAP FORWARD {Mathf.Min(tutorialForwardMoves, 3)} / 3  |  SWIPE SIDE {(tutorialSideMove ? 1 : 0)} / 1");
            if (tutorialForwardMoves >= 3 && tutorialSideMove) AdvanceTutorial();
            return;
        }

        if (IsObstacleLesson(tutorialLesson) && player != null && player.position.z > tutorialTargetZ + 0.1f)
            AdvanceTutorial();

        if (tutorialPowerCollected && tutorialLesson == TutorialLesson.SpeedDash)
        {
            // The move that collected the pickup may finish after GrantPowerUp.
            // Count only the two new forward tiles traveled beyond that row.
            if (direction.z > 0f && player != null && player.position.z > tutorialDashStartZ + 0.1f)
                tutorialDashForwardMoves++;
            SetTutorialObjective($"TAP FORWARD TO TRY THE SPEED DASH  {Mathf.Min(tutorialDashForwardMoves, 2)} / 2");
            if (tutorialDashForwardMoves >= 2) AdvanceTutorial();
            return;
        }

        if (tutorialPowerCollected && player != null &&
            tutorialLesson == TutorialLesson.BubbleShield &&
            player.position.z >= tutorialTargetZ - .1f)
            AdvanceTutorial();

        if (tutorialPowerCollected && tutorialInvincibleSharkHit && player != null &&
            tutorialLesson == TutorialLesson.Invincibility &&
            player.position.z >= tutorialTargetZ + .9f)
            AdvanceTutorial();
    }

    public void NotifyObstacleEncountered(SeaObstacleType type)
    {
        if (GameSession.Mode != FishGameMode.Tutorial || !IsObstacleLesson(tutorialLesson)) return;
        string hint = type switch
        {
            SeaObstacleType.Coral => "CORAL BLOCKS YOU - CHANGE LANE",
            SeaObstacleType.Squid => "SQUID RELEASES INK - RECOVER AND GO AROUND",
            SeaObstacleType.Jellyfish => "JELLYFISH STUNS - WAIT, THEN DODGE",
            SeaObstacleType.Pufferfish => "PUFFERFISH BLOCKS THE PATH - DODGE",
            SeaObstacleType.Crab => "ESCAPE THE CRAB: TAP, TAP, SWIPE",
            _ => "SHARKS ARE DEADLY - NEVER TOUCH THEM"
        };
        ShowStatus(hint, 2.5f);
    }

    private void BeginScriptedTutorial()
    {
        tutorialMap = FindAnyObjectByType<MapManager>();
        tutorialForwardMoves = 0;
        tutorialSideMove = false;
        tutorialPearls = 0;
        tutorialLesson = TutorialLesson.Movement;
        tutorialObjectivePanel.SetActive(true);
        tutorialMap?.ClearTutorialContent();
        SetTutorialObjective("MOVE: TAP FORWARD 0 / 3  |  SWIPE SIDE 0 / 1");
        ShowStatus("LESSON 1: LEARN THE CONTROLS", 2f);
    }

    private void AdvanceTutorial()
    {
        if (tutorialLesson == TutorialLesson.Complete) return;
        tutorialLesson++;
        PrepareTutorialLesson();
    }

    private void PrepareTutorialLesson()
    {
        tutorialMap?.ClearTutorialContent();
        int lane = player != null ? Mathf.RoundToInt(player.position.x) : 0;
        float nextZ = player != null ? Mathf.Round(player.position.z) + 2f : 2f;

        switch (tutorialLesson)
        {
            case TutorialLesson.Pearls:
                tutorialPearls = 0;
                tutorialMap?.SpawnTutorialPearlTrail(nextZ, lane, 3);
                SetTutorialObjective("COLLECT 3 PEARLS  0 / 3");
                ShowStatus("LESSON 2: PEARLS ARE YOUR CURRENCY", 2.4f);
                break;
            case TutorialLesson.Coral: PrepareObstacleLesson(SeaObstacleType.Coral, nextZ, lane, "DODGE THE CORAL AND MOVE PAST ITS ROW"); break;
            case TutorialLesson.Squid: PrepareObstacleLesson(SeaObstacleType.Squid, nextZ, lane, "DODGE THE SQUID AND MOVE PAST ITS ROW"); break;
            case TutorialLesson.Jellyfish: PrepareObstacleLesson(SeaObstacleType.Jellyfish, nextZ, lane, "DODGE THE JELLYFISH AND MOVE PAST ITS ROW"); break;
            case TutorialLesson.Pufferfish: PrepareObstacleLesson(SeaObstacleType.Pufferfish, nextZ, lane, "DODGE THE PUFFERFISH AND MOVE PAST ITS ROW"); break;
            case TutorialLesson.Crab: PrepareObstacleLesson(SeaObstacleType.Crab, nextZ, lane, "ESCAPE OR DODGE THE CRAB, THEN PASS ITS ROW"); break;
            case TutorialLesson.Shark: PrepareObstacleLesson(SeaObstacleType.Shark, nextZ, lane, "AVOID THE SHARK AND MOVE PAST ITS ROW"); break;
            case TutorialLesson.BubbleShield: PreparePowerLesson(CollectibleKind.BubbleShield, nextZ, lane, "COLLECT THE BUBBLE SHIELD"); break;
            case TutorialLesson.SpeedDash: PreparePowerLesson(CollectibleKind.SpeedDash, nextZ, lane, "COLLECT THE SPEED DASH"); break;
            case TutorialLesson.PearlMagnet: PreparePowerLesson(CollectibleKind.PearlMagnet, nextZ, lane, "COLLECT THE PEARL MAGNET"); break;
            case TutorialLesson.Invincibility: PreparePowerLesson(CollectibleKind.InvincibilityBubble, nextZ, lane, "COLLECT THE INVINCIBILITY BUBBLE"); break;
            case TutorialLesson.Complete:
                GameSession.MarkTutorialComplete();
                GameSession.BankRunPearls();
                gameStarted = false;
                playerController?.SetInputLocked(true);
                if (pauseButton != null) pauseButton.gameObject.SetActive(false);
                if (pearlChip != null) pearlChip.SetActive(false);
                if (tutorialObjectivePanel != null) tutorialObjectivePanel.SetActive(false);
                if (tutorialCompleteOverlay != null) tutorialCompleteOverlay.SetActive(true);
                break;
        }
    }

    private void PrepareObstacleLesson(SeaObstacleType type, float z, int lane, string objective)
    {
        tutorialTargetZ = z;
        tutorialMap?.SpawnTutorialObstacle(type, z, lane);
        SetTutorialObjective(objective);
        ShowStatus($"OBSTACLE LESSON: {type.ToString().ToUpperInvariant()}", 2f);
    }

    private void PreparePowerLesson(CollectibleKind kind, float z, int lane, string objective)
    {
        tutorialPowerCollected = false;
        tutorialMap?.SpawnTutorialPowerUp(kind, z, lane);
        SetTutorialObjective(objective);
        ShowStatus("BUFF LESSON: " + kind.ToString().ToUpperInvariant(), 2f);
    }

    private void BeginTutorialPowerTrial(int index)
    {
        tutorialPowerCollected = true;
        tutorialMap?.ClearTutorialContent();
        int lane = player != null ? Mathf.RoundToInt(player.position.x) : 0;
        float nextZ = player != null ? Mathf.Round(player.position.z) + 2f : 2f;

        switch (index)
        {
            case 0:
                tutorialTargetZ = nextZ;
                tutorialMap?.SpawnTutorialObstacleLine(SeaObstacleType.Coral, nextZ);
                SetTutorialObjective("USE THE SHIELD TO BREAK THROUGH THE CORAL WALL");
                ShowStatus("MOVE FORWARD - THE SHIELD WILL BREAK ONE CORAL", 2.5f);
                break;
            case 1:
                tutorialDashForwardMoves = 0;
                tutorialDashStartZ = player != null ? Mathf.Ceil(player.position.z) : 0f;
                SetTutorialObjective("TAP FORWARD TO TRY THE SPEED DASH  0 / 2");
                ShowStatus("DASH FORWARD TWO TILES", 2f);
                break;
            case 2:
                tutorialPearls = 0;
                tutorialMap?.SpawnTutorialPearlPattern(Mathf.Round(nextZ - 1f), lane, TutorialMagnetPearlGoal);
                SetTutorialObjective($"USE THE MAGNET TO COLLECT THE PEARLS AHEAD  0 / {TutorialMagnetPearlGoal}");
                ShowStatus("FOLLOW THE PEARL TRAIL - COLLECT IT BEFORE CONTINUING", 2.5f);
                break;
            case 3:
                tutorialInvincibleSharkHit = false;
                tutorialTargetZ = nextZ;
                tutorialMap?.SpawnTutorialObstacleLine(SeaObstacleType.Shark, nextZ);
                SetTutorialObjective("USE INVINCIBILITY ON A SHARK");
                ShowStatus("HIT ONE SHARK WHILE INVINCIBLE", 2.5f);
                break;
        }
    }

    public void NotifyTutorialInvincibleSharkHit()
    {
        if (GameSession.Mode != FishGameMode.Tutorial ||
            tutorialLesson != TutorialLesson.Invincibility || !tutorialPowerCollected) return;
        tutorialInvincibleSharkHit = true;
        SetTutorialObjective("SHARK CLEARED - MOVE FORWARD ONE MORE ROW");
        ShowStatus("INVINCIBILITY CLEARED THE SHARK! KEEP GOING", 2f);
    }

    public bool RetryTutorialSharkLesson()
    {
        if (GameSession.Mode != FishGameMode.Tutorial ||
            tutorialLesson != TutorialLesson.Shark || !gameStarted) return false;

        tutorialMap?.ClearTutorialContent();
        int lane = player != null ? Mathf.RoundToInt(player.position.x) : 0;
        float nextZ = player != null ? Mathf.Round(player.position.z) + 2f : 2f;
        PrepareObstacleLesson(SeaObstacleType.Shark, nextZ, lane, "AVOID THE SHARK AND MOVE PAST ITS ROW");
        ShowStatus("TRY AGAIN: CHANGE LANE BEFORE THE SHARK", 2.5f);
        return true;
    }

    private void SetTutorialObjective(string objective)
    {
        if (tutorialObjectiveText != null) tutorialObjectiveText.text = "OBJECTIVE  |  " + objective;
    }

    private static bool IsObstacleLesson(TutorialLesson lesson) =>
        lesson >= TutorialLesson.Coral && lesson <= TutorialLesson.Shark;

    private static int TutorialPowerIndex(TutorialLesson lesson)
    {
        return lesson switch
        {
            TutorialLesson.BubbleShield => 0,
            TutorialLesson.SpeedDash => 1,
            TutorialLesson.PearlMagnet => 2,
            TutorialLesson.Invincibility => 3,
            _ => -1
        };
    }

    public bool TryUseShield()
    {
        if (!shieldReady) return false;
        shieldReady = false;
        ShowStatus("BUBBLE SHIELD SAVED YOU!", 1.4f);
        return true;
    }

    public bool TryConsumeSpeedDash()
    {
        if (!gameStarted || !speedDashReady) return false;
        speedDashReady = false;
        ShowStatus("SPEED DASH!", 1f);
        UpdateHud();
        return true;
    }

    public void ShowInkCloud()
    {
        if (IsInvincible) return;
        StartCoroutine(InkRoutine());
    }

    private IEnumerator InkRoutine()
    {
        inkCloud.gameObject.SetActive(true);
        ShowStatus("SQUID INK!", 1f);
        yield return new WaitForSeconds(2.4f);
        inkCloud.gameObject.SetActive(false);
    }

    public void BeginCrabEscape(PlayerController trappedPlayer, SeaObstacle obstacle)
    {
        if (crabStep >= 0 || Time.time < crabSafeUntil) return;
        crabStep = 0;
        crabObstacle = obstacle;
        trappedPlayer.SetInputLocked(true);
        crabText.gameObject.SetActive(true);
        crabText.text = "CRAB GRAB!\nTAP | TAP | SWIPE\nTAP NOW";
    }

    public bool RegisterEscapeInput(bool tap)
    {
        if (crabStep < 0) return false;
        bool expectedTap = crabStep < 2;
        if (tap == expectedTap) crabStep++;
        else crabStep = 0;

        if (crabStep >= 3)
        {
            crabStep = -1;
            crabSafeUntil = Time.time + 4f;
            crabText.gameObject.SetActive(false);
            playerController?.SetInputLocked(false);
            if (crabObstacle != null) Destroy(crabObstacle.gameObject);
            ShowStatus("ESCAPED!", 1f);
        }
        else
        {
            crabText.text = crabStep == 0 ? "CRAB GRAB!\nTAP | TAP | SWIPE\nTAP NOW" : crabStep == 1 ? "GOOD!\nTAP AGAIN" : "NOW SWIPE!";
        }
        return true;
    }

    public void ShowStatus(string message, float duration)
    {
        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(StatusRoutine(message, duration));
    }

    private IEnumerator StatusRoutine(string message, float duration)
    {
        statusText.text = message;
        yield return new WaitForSecondsRealtime(duration);
        statusText.text = "";
        statusRoutine = null;
    }

    private void UpdateHud()
    {
        if (scoreText == null) return;
        scoreText.text = score.ToString();
        if (bestScoreText != null)
        {
            int savedBest = PlayerPrefs.GetInt($"Fishfish.HighScore.{GameSession.Mode}", 0);
            bestScoreText.text = $"BEST  {Mathf.Max(savedBest, score)}";
        }
        if (pearlText != null) pearlText.text = $"PEARL  {GameSession.PearlWallet + GameSession.RunPearls}";
        timerText.text = GameSession.Mode == FishGameMode.TimeAttack ? $"TIME  {Mathf.CeilToInt(remainingTime)}" : "";
        // The top-right icon row replaces the old equipment word list.
        if (powerText != null) powerText.text = "";
        bool[] activePowers = { shieldReady, HasSpeedDash, HasPearlMagnet, IsInvincible };
        for (int i = 0; i < gameplayPowerIcons.Length; i++)
        {
            Image icon = gameplayPowerIcons[i];
            if (icon == null) continue;
            bool has3DModel = icon.TryGetComponent(out UI3DModelPreview preview) && preview.modelPrefab != null;
            icon.sprite = has3DModel ? null : GetPowerEquipmentIcon(i);
            icon.color = has3DModel ? Color.clear : icon.sprite != null ? Color.white : EquipmentPlaceholderColor(i);
            icon.gameObject.SetActive(gameStarted && activePowers[i]);
        }
    }

    public Sprite GetCharacterEquipmentIcon()
    {
        return GameSession.EquippedCharacter == 1 ? sealEquipmentIcon : turtleEquipmentIcon;
    }

    public Sprite GetPowerEquipmentIcon(int index)
    {
        return powerEquipmentIcons != null && index >= 0 && index < powerEquipmentIcons.Length
            ? powerEquipmentIcons[index]
            : null;
    }

    public static Color EquipmentPlaceholderColor(int index)
    {
        return index % 3 == 0 ? OceanUI.Coral : index % 3 == 1 ? OceanUI.Sand : OceanUI.Aqua;
    }

    private void CreateGameplayEquipmentIcons(RectTransform root)
    {
        for (int i = 0; i < gameplayPowerIcons.Length; i++)
            gameplayPowerIcons[i] = CreateEquipmentIcon(root, "Equipped Power " + (i + 1), i);
    }

    private void EnsureGameplayEquipmentIcons(Transform root)
    {
        for (int i = 0; i < gameplayPowerIcons.Length; i++)
        {
            gameplayPowerIcons[i] = ComponentAt<Image>(root, "Equipped Power " + (i + 1));
            if (gameplayPowerIcons[i] == null)
                Debug.LogError("Editable Canvas is missing Equipped Power " + (i + 1) + ". Re-bake the Gameplay Canvas.");
            else
                gameplayPowerIcons[i].gameObject.SetActive(false);
        }
    }

    private Image CreateEquipmentIcon(Transform root, string iconName, int index)
    {
        Image icon = OceanUI.CreatePanel(iconName, root, EquipmentPlaceholderColor(index));
        icon.raycastTarget = false;
        float top = .795f - index * .067f;
        OceanUI.SetRect(icon.rectTransform, new Vector2(.905f, top - .058f), new Vector2(.978f, top), Vector2.zero, Vector2.zero);
        icon.preserveAspect = true;
        return icon;
    }

    private void CheckPlayerOutsideCamera()
    {
        if (player == null || mainCamera == null) return;
        Vector3 visibleCenter = player.position;
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (hasBounds) visibleCenter = bounds.center;
        Vector3 viewport = mainCamera.WorldToViewportPoint(visibleCenter);
        if (viewport.y < retreatLossScreenY) GameOver("Left behind by the current");
    }

    public void GameOver() => GameOver("The reef got too busy");

    public void GameOver(string reason)
    {
        if (gameOver || !gameStarted) return;
        GameAudioManager.Play(GameSfx.GameOver);
        gameOver = true;
        gameStarted = false;
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
        if (pearlChip != null) pearlChip.SetActive(false);
        if (tutorialObjectivePanel != null) tutorialObjectivePanel.SetActive(false);
        playerController?.SetInputLocked(true);
        GameSession.BankRunPearls();
        string key = $"Fishfish.HighScore.{GameSession.Mode}";
        int best = Mathf.Max(score, PlayerPrefs.GetInt(key, 0));
        PlayerPrefs.SetInt(key, best);
        PlayerPrefs.Save();
        resultText.text = $"{reason.ToUpperInvariant()}\n\nSCORE\n{score}\n\nBEST  {best}     PEARLS  {GameSession.PearlWallet}";
        gameOverOverlay.SetActive(true);
    }

    private void TogglePause()
    {
        if (gameOver || !gameStarted) return;
        paused = !paused;
        pauseOverlay.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Gameplay");
    }
}
