using UnityEngine;

/// <summary>World-space hit visuals; kept outside Player so camera loss checks only see the character.</summary>
public sealed class PlayerVisualEffects : MonoBehaviour
{
    private Transform target;
    private GameObject shieldPrefab;
    private Sprite zapSprite;
    private GameObject shield;
    private readonly SpriteRenderer[] bolts = new SpriteRenderer[3];
    private readonly SpriteRenderer[] rainbowAura = new SpriteRenderer[3];
    private readonly SpriteRenderer[] magnetWaves = new SpriteRenderer[3];
    private readonly SpriteRenderer[] hitStars = new SpriteRenderer[5];
    private SpriteRenderer hitStarRing;
    private static Sprite hitStarSprite;
    private float hitStarRemaining;
    private bool invincibleVisible;
    private bool magnetVisible;
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
        shieldDiameter = Mathf.Clamp(Mathf.Max(body.size.x, body.size.y, body.size.z) * .85f, .52f, .95f);
        shield.transform.localScale = Vector3.one * shieldDiameter;
        shield.transform.position = ShieldCenter(body);
        // The imported bubble material has strong emission/opacity. Keep its
        // protective outline without washing out the reef behind the player.
        foreach (Renderer renderer in shield.GetComponentsInChildren<Renderer>())
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat("_EmissionMultiply", 0f);
            properties.SetFloat("_OpacityOverall", 0.35f);
            renderer.SetPropertyBlock(properties);
        }
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

    public void ShowBlockedHitStars()
    {
        if (target == null) return;
        hitStarRemaining = .78f;
        if (hitStarRing == null)
        {
            GameObject orbit = new GameObject("Blocked Hit Faint Ring");
            hitStarRing = orbit.AddComponent<SpriteRenderer>();
            hitStarRing.sprite = PowerTimerSprite.Get();
            hitStarRing.sortingOrder = 54;
        }
        if (hitStarSprite == null) hitStarSprite = CreateHitStarSprite();
        for (int i = 0; i < hitStars.Length; i++)
        {
            if (hitStars[i] != null) continue;
            GameObject star = new GameObject($"Blocked Hit Star {i + 1}");
            hitStars[i] = star.AddComponent<SpriteRenderer>();
            hitStars[i].sprite = hitStarSprite;
            hitStars[i].sortingOrder = 55;
        }
        PlaceHitStars(BodyBounds());
    }

    public void SetInvincible(bool active)
    {
        if (active == invincibleVisible) return;
        invincibleVisible = active;
        if (!active)
        {
            for (int i = 0; i < rainbowAura.Length; i++)
            {
                if (rainbowAura[i] != null) Destroy(rainbowAura[i].gameObject);
                rainbowAura[i] = null;
            }
            return;
        }
        for (int i = 0; i < rainbowAura.Length; i++)
        {
            GameObject halo = new GameObject($"Player Invincibility Glow {i + 1}");
            rainbowAura[i] = halo.AddComponent<SpriteRenderer>();
            rainbowAura[i].sprite = PowerTimerSprite.Get();
            rainbowAura[i].sortingOrder = 40;
        }
    }

    public void SetMagnetActive(bool active)
    {
        if (active == magnetVisible) return;
        magnetVisible = active;
        for (int i = 0; i < magnetWaves.Length; i++)
        {
            if (magnetWaves[i] != null) Destroy(magnetWaves[i].gameObject);
            magnetWaves[i] = null;
            if (!active) continue;
            GameObject wave = new GameObject($"Player Magnet Wave {i + 1}");
            magnetWaves[i] = wave.AddComponent<SpriteRenderer>();
            magnetWaves[i].sprite = PowerTimerSprite.Get();
            magnetWaves[i].sortingOrder = 42;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;
        Bounds body = BodyBounds();
        if (invincibleVisible) PlaceRainbowAura(body);
        if (magnetVisible) PlaceMagnetWaves(body);
        if (shield != null)
        {
            shield.transform.position = ShieldCenter(body);
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
        if (hitStarRemaining > 0f)
        {
            hitStarRemaining -= Time.deltaTime;
            if (hitStarRemaining > 0f) PlaceHitStars(body);
            else
            {
                if (hitStarRing != null) Destroy(hitStarRing.gameObject);
                hitStarRing = null;
                for (int i = 0; i < hitStars.Length; i++)
                {
                    if (hitStars[i] != null) Destroy(hitStars[i].gameObject);
                    hitStars[i] = null;
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

    private void PlaceRainbowAura(Bounds body)
    {
        Camera view = Camera.main;
        if (view == null) return;
        float diameter = Mathf.Clamp(Mathf.Max(body.size.x, body.size.y) * 1.55f, 1f, 1.7f);
        Quaternion facing = Quaternion.LookRotation(-view.transform.forward, view.transform.up);
        for (int i = 0; i < rainbowAura.Length; i++)
        {
            SpriteRenderer halo = rainbowAura[i];
            if (halo == null) continue;
            float phase = Time.time * 2.3f + i * 2.1f;
            halo.transform.position = body.center - view.transform.forward * (0.14f + i * .008f);
            halo.transform.rotation = facing * Quaternion.Euler(0f, 0f, phase * (i % 2 == 0 ? 26f : -30f));
            halo.transform.localScale = Vector3.one * diameter * (1f + i * .13f + Mathf.Sin(phase) * .055f);
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(Time.time * .23f + i / 3f, 1f), .7f, 1f);
            rainbow.a = .32f + Mathf.Sin(phase) * .08f;
            halo.color = rainbow;
        }
    }

    private void PlaceMagnetWaves(Bounds body)
    {
        Camera view = Camera.main;
        if (view == null) return;
        Quaternion facing = Quaternion.LookRotation(-view.transform.forward, view.transform.up);
        float baseDiameter = Mathf.Clamp(Mathf.Max(body.size.x, body.size.y) * 1.05f, .65f, 1.1f);
        for (int i = 0; i < magnetWaves.Length; i++)
        {
            SpriteRenderer wave = magnetWaves[i];
            if (wave == null) continue;
            float progress = Mathf.Repeat(Time.time * .85f + i / (float)magnetWaves.Length, 1f);
            wave.transform.position = body.center - Vector3.up * .1f - view.transform.forward * (.18f + i * .005f);
            wave.transform.rotation = facing;
            wave.transform.localScale = Vector3.one * baseDiameter * Mathf.Lerp(.7f, 1.55f, progress);
            wave.color = new Color(1f, .13f, .22f, .42f * (1f - progress));
        }
    }

    private static Vector3 ShieldCenter(Bounds body) => body.center - Vector3.up * .12f;

    private void PlaceHitStars(Bounds body)
    {
        Camera view = Camera.main;
        if (view == null) return;
        float fade = Mathf.Clamp01(hitStarRemaining / .45f);
        Vector3 center = body.center + view.transform.up * .23f - view.transform.forward * .24f;
        Quaternion facing = Quaternion.LookRotation(-view.transform.forward, view.transform.up);
        if (hitStarRing != null)
        {
            hitStarRing.transform.position = center;
            hitStarRing.transform.rotation = facing * Quaternion.Euler(0f, 0f, Time.time * 100f);
            hitStarRing.transform.localScale = Vector3.one * .72f;
            hitStarRing.color = new Color(1f, 1f, .9f, .27f * fade);
        }
        for (int i = 0; i < hitStars.Length; i++)
        {
            SpriteRenderer star = hitStars[i];
            if (star == null) continue;
            float angle = Time.time * 6.5f + i * Mathf.PI * 2f / hitStars.Length;
            star.transform.position = center + view.transform.right * (Mathf.Cos(angle) * .35f) +
                view.transform.up * (Mathf.Sin(angle) * .15f);
            star.transform.rotation = facing * Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            star.transform.localScale = Vector3.one * (.13f + (i % 2) * .045f);
            star.color = new Color(1f, .88f, .38f, (.4f + .13f * Mathf.Sin(angle * 2f)) * fade);
        }
    }

    private static Sprite CreateHitStarSprite()
    {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Blocked Hit Star Texture";
        texture.hideFlags = HideFlags.DontSave;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2[] points = new Vector2[10];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.PI * .5f + i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? 21f : 8.5f;
            points[i] = new Vector2(24f + Mathf.Cos(angle) * radius, 24f + Mathf.Sin(angle) * radius);
        }
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool inside = false;
            Vector2 sample = new Vector2(x + .5f, y + .5f);
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                if ((points[i].y > sample.y) != (points[j].y > sample.y) &&
                    sample.x < (points[j].x - points[i].x) * (sample.y - points[i].y) /
                    (points[j].y - points[i].y) + points[i].x) inside = !inside;
            }
            pixels[y * size + x] = inside ? Color.white : Color.clear;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        sprite.name = "Blocked Hit Star";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
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
        foreach (SpriteRenderer halo in rainbowAura)
            if (halo != null) Destroy(halo.gameObject);
        foreach (SpriteRenderer wave in magnetWaves)
            if (wave != null) Destroy(wave.gameObject);
        if (hitStarRing != null) Destroy(hitStarRing.gameObject);
        foreach (SpriteRenderer star in hitStars)
            if (star != null) Destroy(star.gameObject);
    }
}
