using System.Collections;
using UnityEngine;

public enum SeaObstacleType
{
    Coral,
    Squid,
    Crab,
    Jellyfish,
    Pufferfish,
    Shark
}

public class SeaObstacle : MonoBehaviour
{
    public SeaObstacleType type;
    private float nextMovingHitTime;
    private bool sharkBiting;

    public void Initialize(SeaObstacleType newType)
    {
        Initialize(newType, true);
    }

    public void Initialize(SeaObstacleType newType, bool applyFallbackTint)
    {
        type = newType;
        name = newType.ToString();
        SeaLifeMotion motion = GetComponent<SeaLifeMotion>();
        if (motion == null) motion = gameObject.AddComponent<SeaLifeMotion>();
        motion.Configure(GetComponent<MovingSeaObstacle>() != null);
        // Imported animal prefabs often contain renderer-sized or authoring
        // colliders that cover neighbouring lanes. Every obstacle gets the same
        // predictable one-tile gameplay footprint in every mode.
        ConfigureSingleTileCollider();
        if (!applyFallbackTint) return;
        Color color = type switch
        {
            SeaObstacleType.Coral => new Color32(246, 121, 105, 255),
            SeaObstacleType.Squid => new Color32(116, 74, 159, 255),
            SeaObstacleType.Crab => new Color32(240, 91, 70, 255),
            SeaObstacleType.Jellyfish => new Color32(115, 211, 255, 255),
            SeaObstacleType.Pufferfish => new Color32(255, 205, 92, 255),
            _ => new Color32(43, 68, 84, 255)
        };
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.material.color = color;
    }

    public bool Interact(PlayerController player)
    {
        if (GameManager.Instance == null) return true;
        SeaLifeMotion motion = GetComponent<SeaLifeMotion>();
        switch (type)
        {
            case SeaObstacleType.Crab:
                motion?.Shake(0.9f, 2.1f);
                break;
            case SeaObstacleType.Coral:
                motion?.Shake(0.8f, 1.8f);
                break;
            case SeaObstacleType.Squid:
                if (GameSession.Mode != FishGameMode.Tutorial)
                    motion?.SpinOnce(0.78f, 1f);
                break;
            case SeaObstacleType.Pufferfish:
                motion?.SpinOnce(0.78f, 1f);
                break;
            case SeaObstacleType.Jellyfish:
                if (GameSession.Mode != FishGameMode.Tutorial)
                    motion?.ReactTo(player.transform);
                break;
            default:
                motion?.ReactTo(player.transform);
                break;
        }
        GameManager.Instance.NotifyObstacleEncountered(type);

        if (GameManager.Instance.IsInvincible || GameManager.Instance.TryUseShield())
        {
            GameAudioManager.Play(GameSfx.ProtectedHit);
            // Disable the whole obstacle immediately so a moving animal cannot
            // apply a second hit before its destruction animation finishes.
            foreach (Collider hitbox in GetComponentsInChildren<Collider>(true))
                hitbox.enabled = false;
            MovingSeaObstacle traffic = GetComponent<MovingSeaObstacle>();
            if (traffic != null) traffic.enabled = false;
            if (type == SeaObstacleType.Shark && GameSession.Mode == FishGameMode.Tutorial)
                GameManager.Instance.NotifyTutorialInvincibleSharkHit();
            Destroy(gameObject, 0.15f);
            return false;
        }

        GameAudioManager.Play(type switch
        {
            SeaObstacleType.Crab => GameSfx.Crab,
            SeaObstacleType.Pufferfish => GameSfx.Pufferfish,
            SeaObstacleType.Jellyfish => GameSfx.Jellyfish,
            SeaObstacleType.Squid => GameSfx.Squid,
            SeaObstacleType.Shark => GameSfx.Shark,
            _ => GameSfx.Obstacle
        });

        // Without protection, pufferfish remain moving walls.
        if (type == SeaObstacleType.Pufferfish)
        {
            GameManager.Instance.ShowStatus("PUFFER BLOCK! WAIT OR CHOOSE ANOTHER LANE", 1.1f);
            return true;
        }

        switch (type)
        {
            case SeaObstacleType.Coral:
                GameManager.Instance.ShowStatus("CORAL BLOCK! CHOOSE ANOTHER LANE", 1f);
                return true;
            case SeaObstacleType.Squid:
                GameManager.Instance.ShowInkCloud();
                if (GameSession.Mode == FishGameMode.Tutorial)
                    Destroy(gameObject, .35f);
                return false;
            case SeaObstacleType.Crab:
                GameManager.Instance.BeginCrabEscape(player, this);
                return true;
            case SeaObstacleType.Jellyfish:
                player.Stun(1.25f);
                GameManager.Instance.ShowJellyfishZap();
                GameManager.Instance.ShowStatus("ZAP! STUNNED", 1.25f);
                if (GameSession.Mode == FishGameMode.Tutorial)
                    Destroy(gameObject, .35f);
                return false;
            case SeaObstacleType.Shark:
                if (GameManager.Instance.RetryTutorialSharkLesson()) return true;
                if (!sharkBiting) StartCoroutine(SharkBite(player));
                return true;
            default:
                return true;
        }
    }

    private IEnumerator SharkBite(PlayerController player)
    {
        sharkBiting = true;
        MovingSeaObstacle traffic = GetComponent<MovingSeaObstacle>();
        if (traffic != null) traffic.enabled = false;

        player.SetInputLocked(true);
        SeaLifeMotion motion = GetComponent<SeaLifeMotion>();
        if (motion != null) motion.enabled = false;
        GameManager.Instance.ShowStatus("SHARK ATTACK!", 0.7f);

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 toPlayer = player.transform.position - startPosition;
        toPlayer.y = 0f;
        Vector3 biteDirection = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;

        // The imported shark mesh faces local -Z. The 180-degree yaw makes its
        // head, rather than its tail, point at the player before the bite lunge.
        Quaternion biteRotation = Quaternion.LookRotation(biteDirection, Vector3.up)
            * Quaternion.Euler(0f, 180f, 0f);
        float lungeDistance = Mathf.Min(0.72f, toPlayer.magnitude * 0.62f);
        Vector3 bitePosition = startPosition + biteDirection * lungeDistance;
        const float biteDuration = 0.58f;
        float elapsed = 0f;
        while (elapsed < biteDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / biteDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.rotation = Quaternion.Slerp(startRotation, biteRotation, eased);
            // Upward-only arc keeps the shark visibly above the seabed.
            transform.position = Vector3.Lerp(startPosition, bitePosition, eased)
                + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.14f);
            yield return null;
        }
        transform.position = bitePosition;
        transform.rotation = biteRotation;
        yield return new WaitForSeconds(0.12f);
        if (GameManager.Instance != null && GameManager.Instance.gameStarted && !GameManager.Instance.gameOver)
            GameManager.Instance.GameOver("Caught by a shark");
    }

    private void ConfigureSingleTileCollider()
    {
        BoxCollider tileCollider = GetComponent<BoxCollider>();
        if (tileCollider == null) tileCollider = gameObject.AddComponent<BoxCollider>();

        // Only this root collider participates in gameplay. Visual child
        // colliders are disabled because imported bounds may span several tiles.
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = collider == tileCollider;

        Vector3 scale = transform.lossyScale;
        float inverseX = 1f / Mathf.Max(.001f, Mathf.Abs(scale.x));
        float inverseY = 1f / Mathf.Max(.001f, Mathf.Abs(scale.y));
        float inverseZ = 1f / Mathf.Max(.001f, Mathf.Abs(scale.z));
        // Keep the physics footprint comfortably inside one grid tile. This
        // leaves a clear gap to both neighbouring lanes even while the model sways.
        float worldFootprint = type == SeaObstacleType.Shark ? .55f : .46f;

        // Squid and jellyfish are pass-through hazards. A solid collider can
        // shove the player's dynamic Rigidbody off its snapped grid position
        // when these models rotate. Their effect is applied by Interact instead.
        tileCollider.isTrigger = type == SeaObstacleType.Squid || type == SeaObstacleType.Jellyfish;
        tileCollider.center = new Vector3(0f, .42f * inverseY, 0f);
        tileCollider.size = new Vector3(
            worldFootprint * inverseX,
            1.25f * inverseY,
            worldFootprint * inverseZ);
        tileCollider.enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Traffic can move in the ready-state preview, but it must never damage
        // the player or put the game into Game Over before the first tap.
        if (GameManager.Instance == null || !GameManager.Instance.gameStarted || GameManager.Instance.gameOver) return;
        if (GetComponent<MovingSeaObstacle>() == null || Time.time < nextMovingHitTime) return;
        PlayerController hitPlayer = other.GetComponentInParent<PlayerController>();
        if (hitPlayer == null) return;
        nextMovingHitTime = Time.time + 0.75f;
        bool blocked = Interact(hitPlayer);
        if (blocked && type == SeaObstacleType.Pufferfish)
            hitPlayer.BlockCurrentMove();
    }
}
