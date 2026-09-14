using System.Collections;
using UnityEngine;

public enum CollectibleKind
{
    Pearl,
    Starfish,
    TreasureChest,
    BubbleShield,
    SpeedDash,
    PearlMagnet,
    InvincibilityBubble
}

public class CollectibleItem : MonoBehaviour
{
    public CollectibleKind kind;
    private bool collected;
    private bool magnetPulling;

    private void Start()
    {
        SeaLifeMotion motion = GetComponent<SeaLifeMotion>();
        if (motion == null) motion = gameObject.AddComponent<SeaLifeMotion>();
        motion.Configure(false, true);
    }

    public void BeginMagnetPull(Transform player)
    {
        if (kind != CollectibleKind.Pearl || collected || magnetPulling || player == null) return;
        magnetPulling = true;
        foreach (Collider pickupCollider in GetComponentsInChildren<Collider>())
            pickupCollider.enabled = false;
        SeaLifeMotion motion = GetComponent<SeaLifeMotion>();
        if (motion != null) motion.enabled = false;
        StartCoroutine(PullToPlayer(player));
    }

    private IEnumerator PullToPlayer(Transform player)
    {
        Vector3 start = transform.position;
        Vector3 startingScale = transform.localScale;
        float duration = Mathf.Clamp(Vector3.Distance(start, player.position) * .14f, .22f, .48f);
        float elapsed = 0f;
        while (elapsed < duration && player != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            Vector3 target = player.position + Vector3.up * .45f;
            transform.position = Vector3.Lerp(start, target, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .18f);
            transform.localScale = startingScale * Mathf.Lerp(1f, .35f, eased);
            yield return null;
        }
        if (player != null && GameManager.Instance != null && !GameManager.Instance.gameOver) Collect();
        else Destroy(gameObject);
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;
        if (GameManager.Instance != null)
        {
            switch (kind)
            {
                case CollectibleKind.Pearl: GameManager.Instance.CollectPearls(1); break;
                case CollectibleKind.Starfish: GameManager.Instance.AddScore(2); break;
                case CollectibleKind.TreasureChest: GameManager.Instance.CollectPearls(10); break;
                case CollectibleKind.BubbleShield: GameManager.Instance.GrantPowerUp(0); break;
                case CollectibleKind.SpeedDash: GameManager.Instance.GrantPowerUp(1); break;
                case CollectibleKind.PearlMagnet: GameManager.Instance.GrantPowerUp(2); break;
                case CollectibleKind.InvincibilityBubble: GameManager.Instance.GrantPowerUp(3); break;
            }
        }
        GameAudioManager.Play(kind == CollectibleKind.Pearl ? GameSfx.Pearl : GameSfx.PowerUp);
        Destroy(gameObject);
    }
}
