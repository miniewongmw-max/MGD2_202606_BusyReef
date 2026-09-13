using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Playable Character Prefabs")]
    [Tooltip("Optional replacement for the turtle model already in the scene.")]
    public GameObject turtleCharacterPrefab;
    [Tooltip("Assign the final seal model prefab here.")]
    public GameObject sealCharacterPrefab;

    public float tileSize = 1f;
    public float moveDuration = 0.12f;
    [Tooltip("Small hop height used to make each tile move readable.")]
    public float moveHopHeight = 0.055f;
    public float minX = -4f;
    public float maxX = 4f;
    public MapManager mapManager;

    [Header("Check Obstacles")]
    public LayerMask obstacleLayer;
    public float obstacleCheckHeight = 0.5f;
    public float obstacleCheckRadius = 0.3f;

    private bool isMoving;
    private bool movingObstacleBlocked;
    private bool inputLocked;
    private float stunnedUntil;
    private float nextMagnetScan;
    private float furthestScoredZ;
    private CameraController cameraController;
    private readonly Queue<Vector3> moveQueue = new Queue<Vector3>();

    private void Start()
    {
        // Keep imported turtle models grounded; this is intentionally only a tiny visual lift.
        moveHopHeight = Mathf.Min(moveHopHeight, .055f);
        minX = Mathf.Max(minX, -4f);
        maxX = Mathf.Min(maxX, 4f);
        Vector3 alignedPosition = transform.position;
        alignedPosition.x = Mathf.Round(alignedPosition.x / tileSize) * tileSize;
        alignedPosition.z = Mathf.Round(alignedPosition.z / tileSize) * tileSize;
        transform.position = alignedPosition;
        furthestScoredZ = transform.position.z;
        cameraController = FindAnyObjectByType<CameraController>();
        RefreshCharacter();
    }

    private void Update()
    {
        if (!CanAcceptInput()) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) QueueMove(Vector3.forward);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) QueueMove(Vector3.back);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) QueueMove(Vector3.left);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) QueueMove(Vector3.right);

        if (GameManager.Instance != null && GameManager.Instance.HasPearlMagnet && Time.time >= nextMagnetScan)
        {
            nextMagnetScan = Time.time + 0.18f;
            foreach (CollectibleItem item in FindObjectsByType<CollectibleItem>())
            {
                if (item.kind == CollectibleKind.Pearl && Vector3.Distance(transform.position, item.transform.position) < 3.2f)
                    item.Collect();
            }
        }
    }

    private bool CanAcceptInput()
    {
        return GameManager.Instance != null && GameManager.Instance.gameStarted && !GameManager.Instance.gameOver && !inputLocked && Time.time >= stunnedUntil;
    }

    public void QueueMove(Vector3 direction)
    {
        if (!CanAcceptInput() || direction == Vector3.zero || moveQueue.Count >= 3) return;
        moveQueue.Enqueue(direction);
        if (direction == Vector3.forward && moveQueue.Count < 3 && GameManager.Instance.TryConsumeSpeedDash())
            moveQueue.Enqueue(direction);
        if (!isMoving) StartCoroutine(MovePlayer());
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (locked) moveQueue.Clear();
    }

    public void Stun(float duration)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
        moveQueue.Clear();
    }

    public void BlockCurrentMove()
    {
        if (!isMoving) return;
        movingObstacleBlocked = true;
        moveQueue.Clear();
    }

    private IEnumerator MovePlayer()
    {
        isMoving = true;
        while (moveQueue.Count > 0)
        {
            if (!CanAcceptInput())
            {
                moveQueue.Clear();
                break;
            }

            Vector3 direction = moveQueue.Dequeue();
            Vector3 target = transform.position + direction * tileSize;
            if (target.x < minX || target.x > maxX) continue;

            SeaObstacle obstacle = FindObstacle(target);
            if (obstacle != null && obstacle.Interact(this))
            {
                moveQueue.Clear();
                continue;
            }

            Vector3 start = transform.position;
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            float timer = 0f;
            bool cancelledByTraffic = false;
            movingObstacleBlocked = false;
            while (timer < moveDuration)
            {
                if (GameManager.Instance == null || GameManager.Instance.gameOver || inputLocked)
                {
                    moveQueue.Clear();
                    isMoving = false;
                    yield break;
                }
                if (movingObstacleBlocked)
                {
                    transform.position = start;
                    transform.rotation = startRotation;
                    movingObstacleBlocked = false;
                    cancelledByTraffic = true;
                    break;
                }
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / moveDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                transform.position = Vector3.Lerp(start, target, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * moveHopHeight);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                yield return null;
            }
            if (cancelledByTraffic) continue;
            transform.position = target;
            transform.rotation = targetRotation;
            GameAudioManager.Play(GameSfx.Move);
            GameManager.Instance?.NotifyPlayerMoved(direction);
            cameraController?.NotifyPlayerMoved(direction);
            if (direction.z > 0f && transform.position.z > furthestScoredZ + 0.01f)
            {
                int newlyReachedRows = Mathf.Max(1, Mathf.RoundToInt((transform.position.z - furthestScoredZ) / tileSize));
                furthestScoredZ = transform.position.z;
                GameManager.Instance.AddScore(newlyReachedRows);
            }
            CheckBackRow();
        }
        isMoving = false;
    }

    private SeaObstacle FindObstacle(Vector3 futurePosition)
    {
        // Stationary tutorial/rest-row obstacles are grid blockers. Check the
        // root tile rather than imported collider bounds, which may extend into
        // neighbouring lanes.
        foreach (SeaObstacle candidate in FindObjectsByType<SeaObstacle>())
        {
            if (!candidate.isActiveAndEnabled || candidate.GetComponent<MovingSeaObstacle>() != null) continue;
            Vector3 delta = candidate.transform.position - futurePosition;
            if (Mathf.Abs(delta.x) <= tileSize * 0.42f && Mathf.Abs(delta.z) <= tileSize * 0.42f)
                return candidate;
        }

        Vector3 checkPosition = futurePosition + Vector3.up * 0.75f;
        Vector3 halfExtents = new Vector3(0.4f, 0.75f, 0.4f);
        int mask = obstacleLayer.value == 0 ? Physics.AllLayers : obstacleLayer.value;
        // Moving traffic uses trigger colliders so it can detect a stationary
        // player. Include those triggers in the destination-tile check, then
        // filter below to SeaObstacle only.
        Collider[] hits = Physics.OverlapBox(checkPosition, halfExtents, Quaternion.identity, mask, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            SeaObstacle obstacle = hit.GetComponentInParent<SeaObstacle>();
            // Static obstacles were handled by their exact grid coordinate
            // above. Only moving traffic uses live physics overlap here.
            if (obstacle != null && obstacle.GetComponent<MovingSeaObstacle>() != null) return obstacle;
        }
        return null;
    }

    private void CheckBackRow()
    {
        if (mapManager == null || GameManager.Instance == null) return;
        if (transform.position.z <= mapManager.GetBackRowZ() + 0.01f)
            GameManager.Instance.GameOver("The reef moved on without you");
    }

    private void OnTriggerEnter(Collider other)
    {
        CollectibleItem item = other.GetComponentInParent<CollectibleItem>();
        if (item != null)
        {
            item.Collect();
            return;
        }
        if (other.CompareTag("Collectible"))
        {
            GameManager.Instance?.CollectPearls(1);
            Destroy(other.gameObject);
        }
    }

    public void RefreshCharacter()
    {
        Transform oldRuntimeCharacter = transform.Find("Runtime Character");
        if (oldRuntimeCharacter != null)
        {
            oldRuntimeCharacter.gameObject.SetActive(false);
            Destroy(oldRuntimeCharacter.gameObject);
        }
        Transform oldSeal = transform.Find("Runtime Seal");
        if (oldSeal != null)
        {
            oldSeal.gameObject.SetActive(false);
            Destroy(oldSeal.gameObject);
        }
        int selectedCharacter = GameSession.EquippedCharacter;
        GameObject selectedPrefab = selectedCharacter == 1 ? sealCharacterPrefab : turtleCharacterPrefab;
        Transform sceneTurtle = transform.Find("Turtle");
        Transform sceneSeal = transform.Find("Seal");
        // The authored models are children of Player, not the optional prefab
        // slots. Keep their active state in sync whenever selection changes.
        if (sceneTurtle != null) sceneTurtle.gameObject.SetActive(selectedPrefab == null && selectedCharacter == 0);
        if (sceneSeal != null) sceneSeal.gameObject.SetActive(selectedPrefab == null && selectedCharacter == 1);
        Transform selectedSceneModel = selectedCharacter == 1 ? sceneSeal : sceneTurtle;
        if (selectedPrefab == null && selectedSceneModel != null)
            foreach (Renderer renderer in selectedSceneModel.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
        if (selectedPrefab != null)
        {
            GameObject model = Instantiate(selectedPrefab, transform);
            model.name = "Runtime Character";
            model.SetActive(true);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Collider modelCollider in model.GetComponentsInChildren<Collider>(true)) modelCollider.enabled = false;
            foreach (Rigidbody modelBody in model.GetComponentsInChildren<Rigidbody>(true)) modelBody.isKinematic = true;
            EnableCharacterShadows();
            return;
        }
        if (selectedCharacter == 1)
        {
            if (sceneSeal == null)
                Debug.LogWarning("Seal character prefab and scene model are missing. Using the existing player visual as a fallback.", this);
        }
        Color[] palettes =
        {
            new Color32(101, 190, 129, 255),
            new Color32(255, 134, 104, 255),
            new Color32(82, 206, 220, 255),
            new Color32(116, 92, 174, 255)
        };
        Color color = palettes[Mathf.Clamp(GameSession.EquippedSkin, 0, palettes.Length - 1)];
        // Only tint a primitive fallback. Recolouring imported scene models
        // would replace their authored materials when switching characters.
        if (sceneTurtle == null && sceneSeal == null)
        {
            foreach (Renderer renderer in GetComponents<Renderer>())
            {
                renderer.enabled = true;
                if (renderer.material.HasProperty("_BaseColor")) renderer.material.SetColor("_BaseColor", color);
                else if (renderer.material.HasProperty("_Color")) renderer.material.color = color;
            }
        }
        EnableCharacterShadows();
    }

    private void EnableCharacterShadows()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

}
