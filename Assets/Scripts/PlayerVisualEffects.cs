using UnityEngine;

/// <summary>World-space hit visuals; kept outside Player so camera loss checks only see the character.</summary>
public sealed class PlayerVisualEffects : MonoBehaviour
{
    private Transform target;
    private GameObject shieldPrefab;
    private Sprite zapSprite;
    private GameObject shield;
    private readonly SpriteRenderer[] bolts = new SpriteRenderer[3];
    private float zapRemaining;
    private float breakRemaining;
    private float shieldDiameter;

    public void Configure(Transform player, GameObject bubblePrefab, Sprite lightning)
    {
        target = player;
        shieldPrefab = bubblePrefab != null ? bubblePrefab : Resources.Load<GameObject>("Effects/BubbleShieldDisplay");
        zapSprite = lightning != null ? lightning : Resources.Load<Sprite>("Effects/Lightning");
    }

    public void SetShieldActive(bool active)
    {
        if (!active)
        {
            breakRemaining = 0f;
            if (shield != null) Destroy(shield);
            shield = null;
            return;
        }
        if (shield != null)
        {
            // A second Shield pickup during the pop animation renews the bubble.
            breakRemaining = 0f;
            shield.transform.localScale = Vector3.one * shieldDiameter;
            return;
        }
        if (target == null || shieldPrefab == null) return;
        shield = Instantiate(shieldPrefab);
        shield.name = "Player Bubble Shield Effect";
        // The source sphere mesh has a unit diameter. Scale to the visible
        // character, with limits so imported model bounds cannot swallow a lane.
        Bounds body = BodyBounds();
        shieldDiameter = Mathf.Clamp(Mathf.Max(body.size.x, body.size.y, body.size.z) * 1.35f, 0.85f, 1.5f);
        shield.transform.localScale = Vector3.one * shieldDiameter;
        shield.transform.position = body.center;
    }

    public void BreakShield()
    {
        if (shield == null) return;
        breakRemaining = 0.18f;
    }

    public void ShowZap()
    {
        if (target == null || zapSprite == null) return;
        zapRemaining = 0.65f;
        for (int i = 0; i < bolts.Length; i++)
        {
            if (bolts[i] != null) continue;
            GameObject bolt = new GameObject($"Player Jellyfish Zap {i + 1}");
            bolts[i] = bolt.AddComponent<SpriteRenderer>();
            bolts[i].sprite = zapSprite;
            bolts[i].sortingOrder = 50;
        }
        PlaceBolts();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        Bounds body = BodyBounds();
        if (shield != null)
        {
            shield.transform.position = body.center;
            if (breakRemaining > 0f)
            {
                breakRemaining -= Time.deltaTime;
                shield.transform.localScale = Vector3.one * shieldDiameter *
                    Mathf.Lerp(1f, 1.35f, 1f - Mathf.Max(0f, breakRemaining) / 0.18f);
                if (breakRemaining <= 0f)
                {
                    Destroy(shield);
                    shield = null;
                }
            }
        }
        if (zapRemaining <= 0f) return;
        zapRemaining -= Time.deltaTime;
        if (zapRemaining <= 0f)
        {
            foreach (SpriteRenderer bolt in bolts)
                if (bolt != null) Destroy(bolt.gameObject);
            for (int i = 0; i < bolts.Length; i++) bolts[i] = null;
            return;
        }
        PlaceBolts();
    }

    private void PlaceBolts()
    {
        Camera view = Camera.main;
        if (view == null) return;
        Bounds body = BodyBounds();
        float height = Mathf.Clamp(body.size.y * 1.2f, 0.75f, 1.3f);
        float unitHeight = Mathf.Max(0.01f, zapSprite.bounds.size.y);
        float flicker = Mathf.Sin(Time.time * 75f) > -0.2f ? 1f : 0.25f;
        Quaternion facing = Quaternion.LookRotation(-view.transform.forward, view.transform.up);
        for (int i = 0; i < bolts.Length; i++)
        {
            SpriteRenderer bolt = bolts[i];
            if (bolt == null) continue;
            float side = i == 0 ? -1f : i == 1 ? 1f : 0f;
            bolt.transform.position = body.center + view.transform.right * (side * 0.28f) +
                view.transform.up * (i == 2 ? 0.27f : -0.08f) - view.transform.forward * 0.23f;
            bolt.transform.rotation = facing * Quaternion.Euler(0f, 0f, side * 22f);
            bolt.transform.localScale = Vector3.one * (height / unitHeight) * (i == 2 ? 0.62f : 0.8f);
            bolt.color = new Color(1f, 1f, 1f, flicker * Mathf.Clamp01(zapRemaining * 5f));
        }
    }

    private Bounds BodyBounds()
    {
        Bounds bounds = new Bounds(target.position + Vector3.up * 0.45f, Vector3.one * 0.65f);
        bool found = false;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private void OnDestroy()
    {
        if (shield != null) Destroy(shield);
        foreach (SpriteRenderer bolt in bolts)
            if (bolt != null) Destroy(bolt.gameObject);
    }
}
