using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Manual Camera Offset")]
    [Tooltip("Manual position offset added after the camera follow calculation. Adjust this in the Inspector to shift the framing without breaking player follow.")]
    public Vector3 cameraPositionOffset = Vector3.zero;

    [Tooltip("Manual rotation offset added on top of Isometric Euler. You can adjust this live in Play Mode.")]
    public Vector3 cameraRotationOffset = Vector3.zero;

    public Transform player;

    [Header("Forward Camera")]
    [Tooltip("Constant forward camera drift while the player waits.")]
    public float minimumSpeed = 0.35f;
    [Tooltip("Maximum camera speed while catching a player who is moving ahead quickly.")]
    public float catchUpSpeed = 3f;
    public float maxDistanceAhead = 5f;
    [Tooltip("Extra forward speed per tile once the player is well ahead of the camera.")]
    public float extraCatchUpPerTile = 2.1f;
    [Tooltip("Upper limit for adaptive catch-up; prevents a sudden camera snap.")]
    public float maximumAdaptiveSpeed = 11f;

    [Range(0.15f, 0.6f)]
    [Tooltip("Normal player height measured upward from the bottom of the screen.")]
    public float desiredPlayerScreenY = 0.25f;

    [Tooltip("How smoothly the forward speed changes.")]
    public float smoothness = 3f;

    [Header("Fast Forward Movement")]
    [Tooltip("Two forward moves completed inside this time are treated as fast consecutive movement.")]
    public float fastMoveWindow = 0.28f;
    [Tooltip("Extra camera speed added for a quick chain of forward moves.")]
    public float fastMoveBoost = 1.15f;
    [Tooltip("Maximum number of quick forward moves used to build the speed boost.")]
    public int maxFastMoveChain = 3;
    [Tooltip("How quickly the temporary fast-move boost fades away.")]
    public float fastMoveBoostDecay = 2.8f;

    [Header("Horizontal Camera Follow")]
    [Tooltip("The camera follows horizontal player movement only inside this X range. With a 9-tile map (-4 to 4), -2 and 2 keep the last two tiles from dragging the camera toward the border.")]
    public float horizontalFollowMinX = -2f;
    public float horizontalFollowMaxX = 4f;

    [Tooltip("Faster lateral follow keeps the character responsive while inside the horizontal follow range.")]
    public float lateralSmoothness = 3.5f;

    [Range(0f, 1f)]
    [Tooltip("How much the camera follows when the player moves to the left.")]
    public float leftFollowStrength = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("How much the camera follows when the player moves to the right.")]
    public float rightFollowStrength = 0.65f;

    [Tooltip("Extra camera framing shift toward the right side of the map. This changes what the camera can see, not how tightly it follows the player.")]
    public float rightViewBias = 1.2f;

    [Header("Right Edge Camera Resistance")]
    [Tooltip("When the player reaches this X position while moving right, the camera begins following less and less.")]
    public float rightEdgeSlowStartX = 1f;

    [Range(0f, 1f)]
    [Tooltip("How much horizontal follow remains at the far-right edge. Smaller = camera barely moves near the edge.")]
    public float rightEdgeMinFollowStrength = 0.08f;

    [Tooltip("After reaching the right side, the camera stays almost pinned while the player moves left until the player reaches this X.")]
    public float rightEdgeReturnReleaseX = 1f;

    [Range(0f, 1f)]
    [Tooltip("How much the camera moves while returning left before Right Edge Return Release X is reached.")]
    public float rightEdgeReturnFollowStrength = 0.10f;

    [Header("Camera Position")]
    [Tooltip("Forward checkpoint follow speed. Retreating never pulls the camera backward.")]
    public float longitudinalSmoothness = 4.5f;
    public float cameraDistance = 11f;
    [Tooltip("Additional world-space look-ahead beyond the player's furthest forward row.")]
    public float focusAheadOfPlayer = 0f;
    public Vector3 isometricEuler = new Vector3(48f, -30f, 0f);

    private Camera cameraComponent;
    private bool lastPortrait;
    private float focusZ;
    private float currentSpeed;

    private float lastForwardMoveTime = -999f;
    private int fastMoveChain;
    private float currentFastMoveBoost;

    private float lastPlayerVisualX;
    private bool rightEdgeReturnHold;

    void Start()
    {
        ConfigureForMode();
        currentSpeed = minimumSpeed;
        cameraComponent = GetComponent<Camera>();
        ApplyOrientation();
        SnapToFocus();

        if (player != null)
            lastPlayerVisualX = player.position.x;
    }

    void ConfigureForMode()
    {
        float stageBoost = Mathf.Clamp(GameSession.SelectedStage, 0, 2) * 0.05f;

        switch (GameSession.Mode)
        {
            case FishGameMode.Tutorial:
                minimumSpeed = 0f;
                catchUpSpeed = 2f;
                break;

            case FishGameMode.TimeAttack:
                minimumSpeed = 0.55f + stageBoost;
                catchUpSpeed = 5f;
                break;

            case FishGameMode.Riptide:
                minimumSpeed = 0.45f + stageBoost;
                catchUpSpeed = 3.6f;
                break;

            default:
                minimumSpeed = 0.35f + stageBoost;
                catchUpSpeed = 3f;
                break;
        }
    }

    void ApplyOrientation()
    {
        if (cameraComponent == null) return;

        lastPortrait = Screen.height > Screen.width;
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = lastPortrait ? 7.25f : 4.75f;
        transform.rotation = Quaternion.Euler(isometricEuler + cameraRotationOffset);
    }

    public void PrepareForSelectedRun()
    {
        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        ConfigureForMode();
        currentSpeed = minimumSpeed;
        fastMoveChain = 0;
        currentFastMoveBoost = 0f;
        lastForwardMoveTime = -999f;
        rightEdgeReturnHold = false;

        if (player != null)
            lastPlayerVisualX = player.position.x;

        ApplyOrientation();
        SnapToFocus();
    }

    void SnapToFocus()
    {
        if (player == null) return;

        focusZ = player.position.z + focusAheadOfPlayer;
        Vector3 focus = VisualFocusPoint(focusZ);
        transform.position = CameraPositionForFocus(focus) + cameraPositionOffset;
    }

    public void CenterOnPlayer()
    {
        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        SnapToFocus();
    }

    /// <summary>
    /// PlayerController calls this after a tile move finishes.
    /// A single/slow forward move keeps the normal camera behaviour.
    /// Several forward moves in quick succession build a temporary speed boost.
    /// </summary>
    public void NotifyPlayerMoved(Vector3 direction)
    {
        if (direction.z <= 0f)
        {
            // Sideways/backward movement does not increase forward camera speed.
            return;
        }

        float now = Time.time;

        if (now - lastForwardMoveTime <= fastMoveWindow)
        {
            fastMoveChain = Mathf.Min(fastMoveChain + 1, Mathf.Max(1, maxFastMoveChain));
        }
        else
        {
            // First move in a new chain: no extra speed yet.
            fastMoveChain = 1;
        }

        lastForwardMoveTime = now;

        // One isolated move = 0 boost.
        // Fast consecutive moves progressively increase the boost.
        float chain01 = maxFastMoveChain <= 1
            ? 1f
            : Mathf.Clamp01((fastMoveChain - 1f) / (maxFastMoveChain - 1f));

        currentFastMoveBoost = fastMoveBoost * chain01;
    }

    private Vector3 VisualFocusPoint(float targetZ)
    {
        Vector3 visualCenter = player.position;

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            visualCenter = bounds.center;

        // Horizontal dead-zone / edge protection:
        // On a 9-tile board (-4 ... 4), the camera only follows between -2 and 2.
        // Going to X=-3/-4 or X=3/4 keeps the camera at the same horizontal limit,
        // so the screen does not become mostly border/empty space.
        float clampedPlayerX = Mathf.Clamp(
            visualCenter.x,
            horizontalFollowMinX,
            horizontalFollowMaxX
        );

        float horizontalDelta = clampedPlayerX - lastPlayerVisualX;
        bool movingRight = horizontalDelta > 0.001f;
        bool movingLeft = horizontalDelta < -0.001f;

        float softenedFocusX;

        if (clampedPlayerX < 0f)
        {
            rightEdgeReturnHold = false;
            softenedFocusX = clampedPlayerX * leftFollowStrength;
        }
        else
        {
            float slowStart = Mathf.Clamp(
                rightEdgeSlowStartX,
                0f,
                Mathf.Max(0.01f, horizontalFollowMaxX)
            );

            float maxRight = Mathf.Max(slowStart + 0.01f, horizontalFollowMaxX);

            if (movingRight && clampedPlayerX >= slowStart)
                rightEdgeReturnHold = true;

            if (movingLeft && rightEdgeReturnHold &&
                clampedPlayerX <= rightEdgeReturnReleaseX)
            {
                rightEdgeReturnHold = false;
            }

            if (rightEdgeReturnHold && movingLeft &&
                clampedPlayerX > rightEdgeReturnReleaseX)
            {
                float baseAtSlowStart = slowStart * rightFollowStrength;
                float edgeDistance = maxRight - slowStart;

                float edgeTarget =
                    baseAtSlowStart +
                    edgeDistance *
                    ((rightFollowStrength + rightEdgeMinFollowStrength) * 0.5f);

                float distanceBackFromEdge = maxRight - clampedPlayerX;

                softenedFocusX =
                    edgeTarget -
                    distanceBackFromEdge * rightEdgeReturnFollowStrength;
            }
            else if (clampedPlayerX <= slowStart)
            {
                softenedFocusX = clampedPlayerX * rightFollowStrength;
            }
            else
            {
                float t = Mathf.InverseLerp(
                    slowStart,
                    maxRight,
                    clampedPlayerX
                );

                float currentSlope = Mathf.Lerp(
                    rightFollowStrength,
                    rightEdgeMinFollowStrength,
                    t
                );

                float baseAtSlowStart = slowStart * rightFollowStrength;
                float extraDistance = clampedPlayerX - slowStart;

                float averageSlope =
                    (rightFollowStrength + currentSlope) * 0.5f;

                softenedFocusX =
                    baseAtSlowStart +
                    extraDistance * averageSlope;
            }

            if (horizontalFollowMaxX > 0f)
            {
                float rightAmount = Mathf.Clamp01(
                    clampedPlayerX / horizontalFollowMaxX
                );

                softenedFocusX += rightViewBias * rightAmount;
            }
        }

        lastPlayerVisualX = clampedPlayerX;

        return new Vector3(
            softenedFocusX,
            visualCenter.y,
            targetZ + visualCenter.z - player.position.z
        );
    }

    private Vector3 CameraPositionForFocus(Vector3 focus)
    {
        // A camera aimed directly at the focus puts it at 50% screen height.
        // Raising the camera along its local up axis places the focus lower,
        // at the requested percentage measured from the bottom edge.
        float verticalOffset =
            (0.5f - desiredPlayerScreenY) *
            2f *
            cameraComponent.orthographicSize;

        return focus
            - transform.forward * cameraDistance
            + transform.up * verticalOffset;
    }

    void Update()
    {
        // Apply rotation every frame so Camera Rotation Offset can be edited
        // live from the Inspector while the game is running.
        transform.rotation = Quaternion.Euler(isometricEuler + cameraRotationOffset);

        if (lastPortrait != (Screen.height > Screen.width))
        {
            ApplyOrientation();
        }

        if (player != null &&
            (GameManager.Instance == null ||
             !GameManager.Instance.gameStarted ||
             GameManager.Instance.gameOver))
        {
            // Keep preview / ready-state character anchored.
            SnapToFocus();
            return;
        }

        if (GameManager.Instance == null || player == null)
        {
            return;
        }

        // Fade the temporary quick-move boost back to zero.
        currentFastMoveBoost = Mathf.MoveTowards(
            currentFastMoveBoost,
            0f,
            fastMoveBoostDecay * Time.deltaTime
        );

        // Base Crossy-Road-style catch-up:
        // camera always moves forward, and catches up more when player is ahead.
        float playerFocusZ = player.position.z + focusAheadOfPlayer;
        float forwardGap = Mathf.Max(0f, playerFocusZ - focusZ);

        float catchUpAmount = Mathf.Clamp01(
            forwardGap / Mathf.Max(0.01f, maxDistanceAhead)
        );

        float largeGap = Mathf.Max(0f, forwardGap - 1.5f);

        float targetSpeed =
            Mathf.Lerp(minimumSpeed, catchUpSpeed, catchUpAmount)
            + currentFastMoveBoost
            + largeGap * Mathf.Max(0f, extraCatchUpPerTile);

        // Don't let the temporary boost run away.
        float absoluteMaxSpeed = Mathf.Max(catchUpSpeed + Mathf.Max(0f, fastMoveBoost), maximumAdaptiveSpeed);
        targetSpeed = Mathf.Clamp(targetSpeed, minimumSpeed, absoluteMaxSpeed);

        float speedResponse = Mathf.Max(0.01f, smoothness + largeGap * .7f);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed,
            1f - Mathf.Exp(-speedResponse * Time.deltaTime));

        focusZ += currentSpeed * Time.deltaTime;

        Vector3 focus = VisualFocusPoint(focusZ);
        Vector3 target = CameraPositionForFocus(focus) + cameraPositionOffset;

        float positionResponse = Mathf.Max(0.01f, smoothness + largeGap * .55f);
        Vector3 followed = Vector3.Lerp(transform.position, target,
            1f - Mathf.Exp(-positionResponse * Time.deltaTime));

        followed.x = Mathf.Lerp(
            transform.position.x,
            target.x,
            1f - Mathf.Exp(-Mathf.Max(.01f, lateralSmoothness) * Time.deltaTime)
        );

        followed.z = Mathf.Lerp(
            transform.position.z,
            target.z,
            1f - Mathf.Exp(-Mathf.Max(.01f, longitudinalSmoothness + largeGap * 1.1f) * Time.deltaTime)
        );

        transform.position = followed;
    }
}
