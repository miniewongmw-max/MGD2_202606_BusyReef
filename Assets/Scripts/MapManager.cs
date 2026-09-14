using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    [Header("World Prefabs")]
    public GameObject tilePrefab;
    public GameObject borderTilePrefab;
    public Transform cameraTransform;

    public int width = 9;
    public int length = 20;

   
    public int rowsBehindPlayer = 5;

    public float recycleDistance = 5f;

    [Header("Starting Safe Zone")]
    [Min(1)] public int safeRowsAheadOfStart = 6;
    [Min(0)] public int pearlRowsAheadOfStart = 2;

    private List<Transform> rows = new List<Transform>();

    private float nextRowZ;

    [Header("Collectible Prefabs")]
    [Tooltip("Normal pearl currency prefab.")]
    public GameObject pearlPrefab;
    public GameObject starfishPrefab;
    public GameObject treasureChestPrefab;

    [Header("Buff Prefabs")]
    public GameObject bubbleShieldPrefab;
    public GameObject speedDashPrefab;
    public GameObject pearlMagnetPrefab;
    public GameObject invincibilityBubblePrefab;

    [Header("Animal And Obstacle Prefabs")]
    public GameObject coralPrefab;
    public GameObject squidPrefab;
    public GameObject crabPrefab;
    public GameObject jellyfishPrefab;
    public GameObject pufferfishPrefab;
    public GameObject sharkPrefab;

    [Header("Unreachable Side Decorations")]
    [Tooltip("Assign four visual-only 3D decoration prefabs. They spawn randomly outside lanes -4 to 4.")]
    public GameObject[] sideDecorationPrefabs = new GameObject[4];
    [Range(0f, 1f)]
    public float sideDecorationChance = .48f;
    [Tooltip("Random scale range applied to each side decoration.")]
    public Vector2 sideDecorationScaleRange = new Vector2(.82f, 1.18f);
    [Tooltip("Fallback surface height if a side border tile has no renderer. Normally the tile's visible top is measured automatically.")]
    public float sideDecorationSurfaceHeight = .10f;
    [Tooltip("Visible-bottom offset above the border tile for each Side Decoration Prefabs slot. Element 0 is Coral; Element 1 is Starfish. Lower the value to lower that asset.")]
    public float[] sideDecorationHeightOffsets = { .05f, .02f, 0f, 0f };
    [Tooltip("Moves side decorations slightly behind the centre of their row.")]
    public float sideDecorationBehindOffset = .24f;

    [Header("Alternating Ground Rows")]
    public bool useAlternatingRowTint = true;
    [Range(.75f, 1f)]
    [Tooltip("Brightness used on every second row. Keep near 1 for a subtle line highlight.")]
    public float deeperRowBrightness = .90f;
    [Tooltip("Third ground colour used specifically for non-tutorial coral rows.")]
    public Color coralRowBlue = new Color32(72, 142, 184, 255);
    [Range(0f, 1f)]
    public float coralRowBlueStrength = .42f;

    [Header("Legacy Prefab Fallbacks")]
    [Tooltip("Kept so existing scene assignments continue working. New artwork should use the named slots above.")]
    public List<GameObject> collectiblePrefabs;
    [Range(0f, 1f)]
    public float collectibleSpawnChance = 0.15f;
    public float collectibleHeight = 0.16f;

    [Header("Per-Prefab Grounding Heights")]
    [Tooltip("Visible bottom height used for pearls. Other power-up collectibles keep using Collectible Height.")]
    public float pearlHeight = 0.13f;
    [Tooltip("Additional visible-bottom offset for Bubble Shield pickups. Negative values lower the sphere without moving its pickup trigger.")]
    public float bubbleShieldHeightOffset = -0.30f;
    [Tooltip("Each value is the requested visible-bottom position, so long tentacles/legs are included when grounding the model.")]
    public float coralGroundHeight = 0.05f;
    public float jellyfishGroundHeight = 0.20f;
    public float squidGroundHeight = 0.11f;
    public float pufferfishGroundHeight = 0.05f;
    public float sharkGroundHeight = 0.08f;
    public float crabGroundHeight = 0.10f;

    [Tooltip("Kept so existing scene assignments continue working. New artwork should use the named slots above.")]
    public List<GameObject> obstaclePrefabs;
    [Range(0f, 1f)]
    public float obstacleSpawnChance = 0.1f;
    public float obstacleHeight = 0.12f;
    public int maximumObstaclesPerRow = 4;
    public LayerMask obstacleLayer;
    [Header("Crossing Traffic Lanes")]
    [Min(8f)] public float animalOffscreenDistance = 12f;

    [Header("Traffic Tuning - Editable In Inspector")]
    [Tooltip("Minimum and maximum movement speed of Squid, Jellyfish, Pufferfish and Shark. X = Min, Y = Max.")]
    public Vector2 animalSpeedRange = new Vector2(0.7f, 1.5f);

    [Tooltip("Seconds between animals in the same wave. X = Min, Y = Max. Larger values spawn animals less frequently.")]
    public Vector2 animalSpawnIntervalRange = new Vector2(3.5f, 5.5f);

    [Tooltip("How many animals appear before the lane takes a longer break. X = Min, Y = Max.")]
    public Vector2Int animalsPerWaveRange = new Vector2Int(1, 2);

    [Tooltip("Pause between traffic waves. X = Min, Y = Max. Larger values give the player a longer crossing window.")]
    public Vector2 animalWavePauseRange = new Vector2(4.0f, 6.0f);

    [Tooltip("Extra speed multiplier used only in Time Attack mode. 1 = same speed as normal mode.")]
    [Range(1f, 2f)]
    public float timeAttackAnimalSpeedMultiplier = 1.15f;
    [Range(3, 5)] public int minimumMovingRowsBeforeCoral = 1;
    [Range(3, 5)] public int maximumMovingRowsBeforeCoral = 5;
    private int pearlPatternLane;
    private int pearlPatternRemaining;
    private int pearlPatternDirection = 1;
    private int pearlPatternCooldown;
    private int tutorialObstacleIndex;
    private int movingRowsUntilCoral;
    private SeaObstacleType lastTrafficType = SeaObstacleType.Coral;
    private float startingPlayerZ;
    private float trafficSpeedMultiplier = 1f;
    private float trafficIntervalMultiplier = 1f;
    private float trafficPauseMultiplier = 1f;
    private int trafficWaveSizeBonus;
    private float pearlPairChance = 0.24f;
    private float bonusCollectibleMultiplier = 1f;

    public float StartingSafeMaxZ => startingPlayerZ + safeRowsAheadOfStart;

    void Start()
    {
        ConfigureDifficulty();
        GenerateStartingMap();
    }

    void ConfigureDifficulty()
    {
        trafficSpeedMultiplier = 1f;
        trafficIntervalMultiplier = 1f;
        trafficPauseMultiplier = 1f;
        trafficWaveSizeBonus = 0;
        pearlPairChance = 0.24f;
        bonusCollectibleMultiplier = 1f;
        movingRowsUntilCoral = Random.Range(minimumMovingRowsBeforeCoral, maximumMovingRowsBeforeCoral + 1);
        int stage = Mathf.Clamp(GameSession.SelectedStage, 0, 2);
        switch (GameSession.Mode)
        {
            case FishGameMode.Tutorial:
                obstacleSpawnChance = 0.18f;
                maximumObstaclesPerRow = 1;
                collectibleSpawnChance = 0.22f;
                break;
            case FishGameMode.TimeAttack:
                obstacleSpawnChance = 0.34f + stage * 0.04f;
                maximumObstaclesPerRow = 2;
                collectibleSpawnChance = 0.30f;
                break;
            case FishGameMode.Riptide:
                obstacleSpawnChance = 0.58f + stage * 0.04f;
                maximumObstaclesPerRow = 4;
                collectibleSpawnChance = 0.06f;
                trafficSpeedMultiplier = 1.45f;
                trafficIntervalMultiplier = 0.50f;
                trafficPauseMultiplier = 0.48f;
                trafficWaveSizeBonus = 2;
                pearlPairChance = 0.03f;
                bonusCollectibleMultiplier = 0.25f;
                movingRowsUntilCoral = Random.Range(5, 9);
                break;
            default:
                obstacleSpawnChance = 0.27f + stage * 0.04f;
                maximumObstaclesPerRow = 2;
                collectibleSpawnChance = 0.25f;
                break;
        }
    }

    void Update()
    {
        RecycleRows();
    }

    void GenerateStartingMap()
    {
        // A centred grid needs an odd tile count. Inspector values such as 30
        // are expanded to 31 at runtime instead of producing -15..14.
        int generatedWidth = Mathf.Max(11, width);
        if (generatedWidth % 2 == 0) generatedWidth++;
        int halfWidth = generatedWidth / 2;
        PlayerController startingPlayer = FindAnyObjectByType<PlayerController>();
        startingPlayerZ = startingPlayer != null ? Mathf.Round(startingPlayer.transform.position.z) : 0f;

        // map don't start from Z = 0
        // example rowsBehindPlayer = 5，start from Z = -5
        int startingZ = -rowsBehindPlayer;

        for (int i = 0; i < length; i++)
        {
            float rowZ = startingZ + i;

            GameObject rowObject =
                new GameObject("Row_" + rowZ);

            rowObject.transform.position =
                new Vector3(0f, 0f, rowZ);

            rowObject.transform.SetParent(transform);

            for (int x = 0; x < generatedWidth; x++)
            {
                float xPosition = x - halfWidth;

                GameObject prefabToSpawn;

                if (xPosition >= -4 && xPosition <= 4)
                {
                    prefabToSpawn = tilePrefab;      //Walkable tile
                }
                else
                {
                    prefabToSpawn = borderTilePrefab;   //Border tile
                }

                GameObject tile = Instantiate(
                    prefabToSpawn,
                    rowObject.transform
                );

                tile.transform.localPosition =
                    new Vector3(xPosition, 0f, 0f);
                ApplyTileRowTint(tile, rowZ);
            }

            // Always provide one unreachable visual lane on both sides. These
            // tiles do not alter the player's -4..4 movement limits.
            EnsureSideBorderTile(rowObject.transform, -5f, -halfWidth, generatedWidth - 1 - halfWidth);
            EnsureSideBorderTile(rowObject.transform, 5f, -halfWidth, generatedWidth - 1 - halfWidth);

            //Obstacles
            GameObject obstacleContainer = new GameObject("Obstacles");
            obstacleContainer.transform.SetParent(rowObject.transform);
            obstacleContainer.transform.localPosition = Vector3.zero;

            //Collectibles
            GameObject collectibleContainer = new GameObject("Collectibles");
            collectibleContainer.transform.SetParent(rowObject.transform);
            collectibleContainer.transform.localPosition = Vector3.zero;

            GameObject decorationContainer = new GameObject("Side Decorations");
            decorationContainer.transform.SetParent(rowObject.transform);
            decorationContainer.transform.localPosition = Vector3.zero;
            SpawnSideDecorations(decorationContainer.transform);

            if (GameSession.Mode != FishGameMode.Tutorial)
            {
                // Use world position rather than the loop index. The scene can
                // override rowsBehindPlayer, and index-based spawning previously
                // put the first traffic lane directly on the player's Z row.
                if (rowZ > StartingSafeMaxZ) SpawnObstacles(rowObject.transform);
                if (rowZ >= startingPlayerZ + pearlRowsAheadOfStart) SpawnCollectibles(rowObject.transform);
            }

            rows.Add(rowObject.transform);

        }

        //Next row start from current map at front
        nextRowZ = startingZ + length;

    }

    public void ClearTutorialContent()
    {
        foreach (Transform row in rows)
        {
            if (row == null) continue;
            ClearContainer(row.Find("Obstacles"));
            ClearContainer(row.Find("Collectibles"));
        }
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (AnimalTrafficLane lane in container.GetComponents<AnimalTrafficLane>())
        {
            lane.enabled = false;
            Destroy(lane);
        }
        foreach (Transform child in container)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    public void SpawnTutorialPearlTrail(float startZ, int lane, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Transform row = FindRow(startZ + i);
            Transform container = row != null ? row.Find("Collectibles") : null;
            if (container != null) SpawnPearl(container, Mathf.Clamp(lane, -4, 4));
        }
    }

    public void SpawnTutorialPearlPattern(float startZ, int centerLane, int count)
    {
        int pearlCount = Mathf.Max(1, count);
        for (int i = 0; i < pearlCount; i++)
        {
            int rowIndex = i / 2;
            int firstLane = Mathf.Clamp(centerLane + (rowIndex % 2 == 0 ? -1 : 0), -4, 4);
            int secondLane = Mathf.Clamp(centerLane + (rowIndex % 2 == 0 ? 1 : 2), -4, 4);
            if (secondLane == firstLane)
                secondLane = firstLane == 4 ? firstLane - 1 : firstLane + 1;
            int lane = i % 2 == 0 ? firstLane : secondLane;
            Transform row = FindRow(startZ + rowIndex);
            Transform container = row != null ? row.Find("Collectibles") : null;
            if (container != null) SpawnPearl(container, lane);
        }
    }

    public void SpawnTutorialPowerUp(CollectibleKind kind, float z, int lane)
    {
        Transform row = FindRow(z);
        Transform container = row != null ? row.Find("Collectibles") : null;
        if (container == null) return;
        CreateBonusCollectible(container, new List<int> { Mathf.Clamp(lane, -4, 4) }, kind);
    }

    public void SpawnTutorialObstacle(SeaObstacleType type, float z, int lane)
    {
        Transform row = FindRow(z);
        Transform container = row != null ? row.Find("Obstacles") : null;
        if (container == null) return;
        if (type == SeaObstacleType.Pufferfish)
        {
            SpawnSlowPufferfishLane(container, true);
            return;
        }
        GameObject obstacle = SpawnObstaclePrefab(type, container);
        if (obstacle == null) return;
        float groundHeight = GetObstacleGroundHeight(type);
        obstacle.transform.localPosition = new Vector3(Mathf.Clamp(lane, -4, 4), groundHeight, 0f);
        PrefabGrounding.AlignVisibleBottom(obstacle, container, groundHeight, 0f);
    }

    public void SpawnTutorialObstacleLine(SeaObstacleType type, float z)
    {
        for (int lane = -4; lane <= 4; lane++) SpawnTutorialObstacle(type, z, lane);
    }

    private Transform FindRow(float z)
    {
        foreach (Transform row in rows)
            if (row != null && Mathf.Abs(row.position.z - Mathf.Round(z)) < 0.1f) return row;
        return null;
    }

    public void ResetForSelectedRun()
    {
        foreach (Transform row in rows)
        {
            if (row == null) continue;
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
        rows.Clear();
        pearlPatternRemaining = 0;
        pearlPatternCooldown = 0;
        tutorialObstacleIndex = 0;
        ConfigureDifficulty();
        GenerateStartingMap();
    }

    void SpawnTutorialRow(Transform row, int index)
    {
        // The first safe rows teach movement. The shield then introduces buffs,
        // followed by one clearly separated example obstacle per row.
        if (index == 10)
        {
            List<int> lane = new List<int> { 0 };
            CreateBonusCollectible(row.Find("Collectibles"), lane, CollectibleKind.BubbleShield);
            return;
        }
        if (index == 13 || index == 15 || index == 17 || index == 19)
        {
            SpawnTutorialObstacle(row, index == 13 ? -2 : index == 15 ? 0 : index == 17 ? 2 : -1);
            return;
        }
        if (index >= 7) SpawnCollectibles(row);
    }

    void SpawnTutorialObstacle(Transform row, int lane)
    {
        Transform container = row.Find("Obstacles");
        SeaObstacleType[] lessons = { SeaObstacleType.Coral, SeaObstacleType.Squid, SeaObstacleType.Jellyfish, SeaObstacleType.Crab };
        SeaObstacleType type = lessons[tutorialObstacleIndex % lessons.Length];
        GameObject obstacle = SpawnObstaclePrefab(type, container);
        if (obstacle == null) return;
        float groundHeight = GetObstacleGroundHeight(type);
        obstacle.transform.localPosition = new Vector3(lane, groundHeight, 0f);
        PrefabGrounding.AlignVisibleBottom(obstacle, container, groundHeight, 0f);
        tutorialObstacleIndex++;
    }

    void RecycleRows()
    {
        if (cameraTransform == null || rows.Count == 0)
        {
            return;
        }

        Transform oldestRow = rows[0];

        if (oldestRow.position.z <
            cameraTransform.position.z - recycleDistance)
        {
            oldestRow.position =
                new Vector3(0f, 0f, nextRowZ);

            RefreshRowTileTint(oldestRow);
            RefreshObstacles(oldestRow);
            RefreshCollectibles(oldestRow);
            RefreshSideDecorations(oldestRow);

            nextRowZ += 1f;

            rows.RemoveAt(0);
            rows.Add(oldestRow);
        }
    }

    // Get the bottom row of the current map
    public float GetBackRowZ()
    {
        if (rows.Count == 0)
        {
            return float.MinValue;
        }

        return rows[0].position.z;
    }

    void SpawnObstacles(Transform row)
    {
        Transform obstacleContainer =
            row.Find("Obstacles");

        if (obstacleContainer == null)
        {
            return;
        }

        if (movingRowsUntilCoral <= 0)
        {
            SpawnCoralRestRow(obstacleContainer);
            movingRowsUntilCoral = Random.Range(minimumMovingRowsBeforeCoral, maximumMovingRowsBeforeCoral + 1);
            return;
        }

        SpawnAnimalTrafficRow(obstacleContainer);
        movingRowsUntilCoral--;
    }

    private void SpawnAnimalTrafficRow(Transform obstacleContainer)
    {
        SeaObstacleType type = RandomMovingAnimalType();
        GameObject prefab = GetObstaclePrefab(type);
        if (prefab == null) return;
        int direction = Random.value < 0.5f ? -1 : 1;
        float modeSpeed = (GameSession.Mode == FishGameMode.TimeAttack
            ? timeAttackAnimalSpeedMultiplier
            : 1f) * trafficSpeedMultiplier;

        float minSpeed = Mathf.Max(0.05f, Mathf.Min(animalSpeedRange.x, animalSpeedRange.y));
        float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(animalSpeedRange.x, animalSpeedRange.y));
        if (type == SeaObstacleType.Pufferfish)
        {
            SpawnSlowPufferfishLane(obstacleContainer, false);
            return;
        }
        float speed = Random.Range(minSpeed, maxSpeed) * modeSpeed;
        float interval = Random.Range(animalSpawnIntervalRange.x, animalSpawnIntervalRange.y) * trafficIntervalMultiplier;
        int waveSize = Random.Range(animalsPerWaveRange.x, animalsPerWaveRange.y + 1) + trafficWaveSizeBonus;
        float wavePause = Random.Range(animalWavePauseRange.x, animalWavePauseRange.y) * trafficPauseMultiplier;
        AnimalTrafficLane lane = obstacleContainer.gameObject.AddComponent<AnimalTrafficLane>();
        lane.Configure(prefab, type, direction, speed, interval, animalOffscreenDistance,
            GetObstacleGroundHeight(type), GetNamedObstaclePrefab(type) == null, waveSize, wavePause);
    }

    private void EnsureSideBorderTile(Transform row, float laneX, float existingMinX, float existingMaxX)
    {
        if (borderTilePrefab == null || laneX >= existingMinX && laneX <= existingMaxX) return;
        GameObject tile = Instantiate(borderTilePrefab, row);
        tile.name = laneX < 0f ? "Left Decoration Border" : "Right Decoration Border";
        tile.transform.localPosition = new Vector3(laneX, 0f, 0f);
        ApplyTileRowTint(tile, row.position.z);
    }

    private void RefreshRowTileTint(Transform row)
    {
        foreach (Transform child in row)
        {
            if (child.name.StartsWith("Obstacles") ||
                child.name.StartsWith("Collectibles") ||
                child.name.StartsWith("Side Decorations")) continue;
            ApplyTileRowTint(child.gameObject, row.position.z);
        }
    }

    private void ApplyTileRowTint(GameObject tile, float rowZ)
    {
        if (tile == null) return;
        bool deeper = useAlternatingRowTint && Mathf.Abs(Mathf.RoundToInt(rowZ)) % 2 == 1;
        float brightness = deeper ? deeperRowBrightness : 1f;
        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null) continue;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            if (material.HasProperty("_BaseColor"))
            {
                Color original = material.GetColor("_BaseColor");
                properties.SetColor("_BaseColor", new Color(
                    original.r * brightness, original.g * brightness,
                    original.b * brightness, original.a));
            }
            if (material.HasProperty("_Color"))
            {
                Color original = material.GetColor("_Color");
                properties.SetColor("_Color", new Color(
                    original.r * brightness, original.g * brightness,
                    original.b * brightness, original.a));
            }
            renderer.SetPropertyBlock(properties);
        }
    }

    private void ApplyCoralRowTint(Transform row)
    {
        if (row == null || GameSession.Mode == FishGameMode.Tutorial) return;
        foreach (Transform child in row)
        {
            if (child.name.StartsWith("Obstacles") ||
                child.name.StartsWith("Collectibles") ||
                child.name.StartsWith("Side Decorations")) continue;
            foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null) continue;
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                if (material.HasProperty("_BaseColor"))
                    properties.SetColor("_BaseColor",
                        Color.Lerp(material.GetColor("_BaseColor"), coralRowBlue, coralRowBlueStrength));
                if (material.HasProperty("_Color"))
                    properties.SetColor("_Color",
                        Color.Lerp(material.GetColor("_Color"), coralRowBlue, coralRowBlueStrength));
                renderer.SetPropertyBlock(properties);
            }
        }
    }

    private void SpawnSideDecorations(Transform container)
    {
        if (container == null) return;
        int generatedWidth = Mathf.Max(11, width);
        if (generatedWidth % 2 == 0) generatedWidth++;
        int outermostLane = generatedWidth / 2;
        int nearEdgeLane = Mathf.Min(8, outermostLane);

        // Spread the same small number of decorations across side tiles.
        // Keep one opportunity near the playable edge, with an occasional
        // farther one, instead of filling every border tile on mobile.
        for (int side = -1; side <= 1; side += 2)
        {
            TrySpawnSideDecoration(container, side * Random.Range(5, nearEdgeLane + 1));
            if (outermostLane > nearEdgeLane && Random.value < .5f)
                TrySpawnSideDecoration(container, side * Random.Range(nearEdgeLane + 1, outermostLane + 1));
        }
    }

    private void TrySpawnSideDecoration(Transform container, float laneX)
    {
        if (Random.value > sideDecorationChance) return;
        int decorationType = Random.Range(0, 4);
        GameObject assignedPrefab = sideDecorationPrefabs != null && decorationType < sideDecorationPrefabs.Length
            ? sideDecorationPrefabs[decorationType]
            : null;
        GameObject decoration = assignedPrefab != null
            ? Instantiate(assignedPrefab, container)
            : CreatePlaceholderDecoration(container, decorationType);
        decoration.name = assignedPrefab != null
            ? "Side Decoration Type " + (decorationType + 1)
            : "Side Decoration Type " + (decorationType + 1) + " Placeholder";
        decoration.transform.localPosition = new Vector3(
            laneX + Random.Range(-.20f, .20f),
            0f,
            sideDecorationBehindOffset + Random.Range(-.12f, .12f));
        decoration.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float minScale = Mathf.Max(.05f, Mathf.Min(sideDecorationScaleRange.x, sideDecorationScaleRange.y));
        float maxScale = Mathf.Max(minScale, Mathf.Max(sideDecorationScaleRange.x, sideDecorationScaleRange.y));
        decoration.transform.localScale *= Random.Range(minScale, maxScale);

        foreach (Collider collider in decoration.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in decoration.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        float heightOffset = sideDecorationHeightOffsets != null && decorationType < sideDecorationHeightOffsets.Length
            ? sideDecorationHeightOffsets[decorationType]
            : 0f;
        float decorationHeight = GetSideBorderSurfaceHeight(container, laneX) + heightOffset;
        PrefabGrounding.AlignVisibleBottom(decoration, container, decorationHeight);
    }

    private float GetSideBorderSurfaceHeight(Transform container, float laneX)
    {
        Transform row = container.parent;
        if (row == null) return sideDecorationSurfaceHeight;

        float highestSurfaceY = float.NegativeInfinity;
        foreach (Transform tile in row)
        {
            if (tile == container || Mathf.Abs(tile.localPosition.x - laneX) > .05f) continue;
            foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true))
                highestSurfaceY = Mathf.Max(highestSurfaceY, renderer.bounds.max.y);
        }
        if (float.IsNegativeInfinity(highestSurfaceY)) return sideDecorationSurfaceHeight;
        float localSurfaceY = container.InverseTransformPoint(new Vector3(0f, highestSurfaceY, 0f)).y;
        return Mathf.Max(sideDecorationSurfaceHeight, localSurfaceY);
    }

    private static GameObject CreatePlaceholderDecoration(Transform parent, int type)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(parent, false);
        Vector3[] shapes =
        {
            new Vector3(.42f, .42f, .42f),
            new Vector3(.52f, .32f, .46f),
            new Vector3(.34f, .58f, .34f),
            new Vector3(.48f, .40f, .30f)
        };
        Color[] colors =
        {
            new Color32(255, 143, 190, 255),
            new Color32(255, 215, 105, 255),
            new Color32(112, 211, 241, 255),
            new Color32(72, 142, 184, 255)
        };
        int index = Mathf.Clamp(type, 0, 3);
        cube.transform.localScale = shapes[index];
        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = colors[index];
        return cube;
    }

    private void RefreshSideDecorations(Transform row)
    {
        Transform oldContainer = row.Find("Side Decorations");
        if (oldContainer != null)
        {
            oldContainer.gameObject.SetActive(false);
            Destroy(oldContainer.gameObject);
        }
        GameObject replacement = new GameObject("Side Decorations");
        replacement.transform.SetParent(row);
        replacement.transform.localPosition = Vector3.zero;
        SpawnSideDecorations(replacement.transform);
    }

    private void SpawnSlowPufferfishLane(Transform obstacleContainer, bool tutorial)
    {
        GameObject prefab = GetObstaclePrefab(SeaObstacleType.Pufferfish);
        if (prefab == null) return;
        int direction = Random.value < .5f ? -1 : 1;
        float hardSpeedInfluence = GameSession.Mode == FishGameMode.Riptide ? 1.12f : 1f;
        float speed = Random.Range(.46f, .62f) * hardSpeedInfluence;
        float interval = Random.Range(2.5f, 3.3f);
        int schoolSize = Random.Range(2, 4);
        float wavePause = tutorial ? 9f : Random.Range(8f, 11f);
        AnimalTrafficLane lane = obstacleContainer.gameObject.AddComponent<AnimalTrafficLane>();
        lane.Configure(prefab, SeaObstacleType.Pufferfish, direction, speed, interval,
            animalOffscreenDistance, GetObstacleGroundHeight(SeaObstacleType.Pufferfish),
            GetNamedObstaclePrefab(SeaObstacleType.Pufferfish) == null, schoolSize, wavePause, tutorial);
    }

    private void SpawnCoralRestRow(Transform obstacleContainer)
    {
        // Rest rows contain only coral and crab. Pufferfish now use their own
        // slow swimming-school traffic lane.
        SeaObstacleType staticType = Random.value < .70f
            ? SeaObstacleType.Coral
            : SeaObstacleType.Crab;

        if (staticType == SeaObstacleType.Coral)
            ApplyCoralRowTint(obstacleContainer.parent);

        int obstacleCount;
        if (staticType == SeaObstacleType.Crab) obstacleCount = 1;
        else
        {
            int minimumCoral = GameSession.Mode == FishGameMode.Riptide ? 4 : 3;
            int maximumCoral = GameSession.Mode == FishGameMode.Riptide ? 6 : 5;
            obstacleCount = Random.Range(minimumCoral, maximumCoral + 1);
        }

        List<int> lanes = new List<int>();
        for (int x = -4; x <= 4; x++) lanes.Add(x);

        for (int i = 0; i < obstacleCount && lanes.Count > 0; i++)
        {
            int choice = Random.Range(0, lanes.Count);
            GameObject obstacle = SpawnObstaclePrefab(staticType, obstacleContainer);
            if (obstacle != null)
            {
                float groundHeight = GetObstacleGroundHeight(staticType);
                obstacle.transform.localPosition = new Vector3(lanes[choice], groundHeight, 0f);
                PrefabGrounding.AlignVisibleBottom(obstacle, obstacleContainer, groundHeight, 0f);
            }
            lanes.RemoveAt(choice);
        }
    }

    private SeaObstacleType RandomMovingAnimalType()
    {
        SeaObstacleType selected = WeightedMovingAnimalType();
        for (int attempt = 0; selected == lastTrafficType && attempt < 4; attempt++)
            selected = WeightedMovingAnimalType();
        lastTrafficType = selected;
        return selected;
    }

    private static SeaObstacleType WeightedMovingAnimalType()
    {
        // Crab remains stationary. Pufferfish receives special slower lane
        // timing after it is selected here.
        float roll = Random.value;
        if (roll < 0.36f) return SeaObstacleType.Squid;
        if (roll < 0.70f) return SeaObstacleType.Jellyfish;
        if (roll < 0.80f) return SeaObstacleType.Pufferfish;
        return SeaObstacleType.Shark;
    }

    void RefreshObstacles(Transform row)
    {
        Transform obstacleContainer =
            row.Find("Obstacles");

        if (obstacleContainer != null)
        {
            obstacleContainer.name = "Obstacles_Old";
            obstacleContainer.SetParent(null);
            obstacleContainer.gameObject.SetActive(false);
            Destroy(obstacleContainer.gameObject);
        }

        GameObject replacement = new GameObject("Obstacles");
        replacement.transform.SetParent(row);
        replacement.transform.localPosition = Vector3.zero;

        if (GameSession.Mode != FishGameMode.Tutorial) SpawnObstacles(row);
    }

    void SpawnCollectibles(Transform row)
    {
        Transform collectibleContainer =
            row.Find("Collectibles");

        if (collectibleContainer == null)
        {
            return;
        }

        HashSet<int> occupied = new HashSet<int>();
        Transform obstacles = row.Find("Obstacles");
        if (obstacles != null)
            foreach (Transform obstacle in obstacles)
                occupied.Add(Mathf.RoundToInt(obstacle.localPosition.x));

        List<int> freeLanes = new List<int>();
        for (int x = -4; x <= 4; x++)
        {
            if (occupied.Contains(x)) continue;
            freeLanes.Add(x);
        }

        if (pearlPatternRemaining <= 0)
        {
            if (pearlPatternCooldown > 0) pearlPatternCooldown--;
            else if (Random.value < collectibleSpawnChance)
            {
                pearlPatternLane = Random.Range(-2, 3);
                pearlPatternDirection = Random.value < 0.5f ? -1 : 1;
                pearlPatternRemaining = Random.Range(3, 6);
            }
        }

        if (pearlPatternRemaining > 0 && freeLanes.Count > 0)
        {
            int lane = ClosestFreeLane(freeLanes, pearlPatternLane);
            SpawnPearl(collectibleContainer, lane);
            // Occasional paired pearl, never more than two on a row.
            int neighbour = lane + pearlPatternDirection;
            if (Random.value < pearlPairChance && freeLanes.Contains(neighbour)) SpawnPearl(collectibleContainer, neighbour);
            pearlPatternLane = Mathf.Clamp(pearlPatternLane + pearlPatternDirection, -3, 3);
            if (Mathf.Abs(pearlPatternLane) >= 3) pearlPatternDirection *= -1;
            pearlPatternRemaining--;
            if (pearlPatternRemaining == 0) pearlPatternCooldown = Random.Range(2, 5);
        }

        if (freeLanes.Count > 0)
        {
            float bonusRoll = Random.value / Mathf.Max(0.01f, bonusCollectibleMultiplier);
            if (bonusRoll < 0.025f) CreateBonusCollectible(collectibleContainer, freeLanes, CollectibleKind.TreasureChest);
            else if (bonusRoll < 0.09f) CreateBonusCollectible(collectibleContainer, freeLanes, CollectibleKind.Starfish);
            else if (bonusRoll < 0.115f) CreateBonusCollectible(collectibleContainer, freeLanes, (CollectibleKind)Random.Range(3, 7));
        }
    }

    int ClosestFreeLane(List<int> lanes, int desired)
    {
        int best = lanes[0];
        foreach (int lane in lanes)
            if (Mathf.Abs(lane - desired) < Mathf.Abs(best - desired)) best = lane;
        return best;
    }

    void SpawnPearl(Transform container, int lane)
    {
        SpawnCollectiblePrefab(CollectibleKind.Pearl, container, lane, pearlHeight);
    }

    private float GetObstacleGroundHeight(SeaObstacleType type)
    {
        return type switch
        {
            SeaObstacleType.Coral => coralGroundHeight,
            SeaObstacleType.Jellyfish => jellyfishGroundHeight,
            SeaObstacleType.Squid => squidGroundHeight,
            SeaObstacleType.Pufferfish => pufferfishGroundHeight,
            SeaObstacleType.Shark => sharkGroundHeight,
            SeaObstacleType.Crab => crabGroundHeight,
            _ => obstacleHeight
        };
    }

    void CreateBonusCollectible(Transform container, List<int> freeLanes, CollectibleKind kind)
    {
        if (container == null) return;
        List<int> vacantLanes = new List<int>();
        foreach (int candidateLane in freeLanes)
            if (!HasCollectibleAtLane(container, candidateLane)) vacantLanes.Add(candidateLane);
        if (vacantLanes.Count == 0) return;
        int lane = vacantLanes[Random.Range(0, vacantLanes.Count)];
        SpawnCollectiblePrefab(kind, container, lane, collectibleHeight + .03f);
    }

    private static bool HasCollectibleAtLane(Transform container, int lane)
    {
        foreach (Transform child in container)
            if (child.gameObject.activeSelf && child.GetComponent<CollectibleItem>() != null &&
                Mathf.Abs(child.localPosition.x - lane) < 0.4f) return true;
        return false;
    }

    private GameObject SpawnObstaclePrefab(SeaObstacleType type, Transform container)
    {
        GameObject prefab = GetObstaclePrefab(type);
        if (prefab == null) return null;
        GameObject obstacle = Instantiate(prefab, container);
        obstacle.name = type.ToString();
        SeaObstacle behaviour = obstacle.GetComponent<SeaObstacle>();
        if (behaviour == null) behaviour = obstacle.AddComponent<SeaObstacle>();
        // Final named art keeps its authored materials. The old generic fallback
        // block is tinted so the game remains readable until all slots are filled.
        behaviour.Initialize(type, GetNamedObstaclePrefab(type) == null);
        return obstacle;
    }

    private GameObject GetObstaclePrefab(SeaObstacleType type)
    {
        GameObject named = GetNamedObstaclePrefab(type);
        if (named != null) return named;
        if (obstaclePrefabs == null || obstaclePrefabs.Count == 0) return null;
        return obstaclePrefabs[(int)type % obstaclePrefabs.Count];
    }

    private GameObject GetNamedObstaclePrefab(SeaObstacleType type)
    {
        return type switch
        {
            SeaObstacleType.Coral => coralPrefab,
            SeaObstacleType.Squid => squidPrefab,
            SeaObstacleType.Crab => crabPrefab,
            SeaObstacleType.Jellyfish => jellyfishPrefab,
            SeaObstacleType.Pufferfish => pufferfishPrefab,
            SeaObstacleType.Shark => sharkPrefab,
            _ => null
        };
    }

    private GameObject SpawnCollectiblePrefab(CollectibleKind kind, Transform container, int lane, float height)
    {
        if (container == null || HasCollectibleAtLane(container, lane)) return null;
        GameObject prefab = GetCollectiblePrefab(kind);
        if (prefab == null) return null;
        if (kind == CollectibleKind.BubbleShield) height += bubbleShieldHeightOffset;
        GameObject collectible = Instantiate(prefab, container);
        collectible.name = kind.ToString();
        collectible.transform.localPosition = new Vector3(lane, height, 0f);
        CollectibleItem item = collectible.GetComponent<CollectibleItem>();
        if (item == null) item = collectible.AddComponent<CollectibleItem>();
        item.kind = kind;
        bool hasTrigger = false;
        foreach (Collider collider in collectible.GetComponentsInChildren<Collider>(true))
            if (collider.isTrigger) hasTrigger = true;
        if (!hasTrigger)
        {
            SphereCollider trigger = collectible.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.48f;
        }
        PrefabGrounding.AlignVisibleBottom(collectible, container, height, 0f);
        ConfigureCollectibleTrigger(collectible, container, lane);
        return collectible;
    }

    private static void ConfigureCollectibleTrigger(GameObject collectible, Transform container, int lane)
    {
        SphereCollider pickup = collectible.GetComponent<SphereCollider>();
        if (pickup == null) pickup = collectible.AddComponent<SphereCollider>();
        foreach (Collider collider in collectible.GetComponentsInChildren<Collider>(true))
            collider.enabled = collider == pickup;

        pickup.isTrigger = true;
        Vector3 pickupWorldPosition = container.TransformPoint(new Vector3(lane, .58f, 0f));
        pickup.center = collectible.transform.InverseTransformPoint(pickupWorldPosition);
        Vector3 scale = collectible.transform.lossyScale;
        float largestScale = Mathf.Max(.001f, Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        pickup.radius = .52f / largestScale;
        pickup.enabled = true;
    }

    private GameObject GetCollectiblePrefab(CollectibleKind kind)
    {
        GameObject named = kind switch
        {
            CollectibleKind.Pearl => pearlPrefab,
            CollectibleKind.Starfish => starfishPrefab,
            CollectibleKind.TreasureChest => treasureChestPrefab,
            CollectibleKind.BubbleShield => bubbleShieldPrefab,
            CollectibleKind.SpeedDash => speedDashPrefab,
            CollectibleKind.PearlMagnet => pearlMagnetPrefab,
            CollectibleKind.InvincibilityBubble => invincibilityBubblePrefab,
            _ => null
        };
        if (named != null) return named;
        if (collectiblePrefabs == null || collectiblePrefabs.Count == 0) return null;
        return collectiblePrefabs[Random.Range(0, collectiblePrefabs.Count)];
    }

    void RefreshCollectibles(Transform row)
    {
        Transform collectibleContainer =
            row.Find("Collectibles");

        if (collectibleContainer != null)
        {
            collectibleContainer.name = "Collectibles_Old";
            collectibleContainer.SetParent(null);
            collectibleContainer.gameObject.SetActive(false);
            Destroy(collectibleContainer.gameObject);
        }

        GameObject replacement = new GameObject("Collectibles");
        replacement.transform.SetParent(row);
        replacement.transform.localPosition = Vector3.zero;

        if (GameSession.Mode != FishGameMode.Tutorial) SpawnCollectibles(row);
    }

}
