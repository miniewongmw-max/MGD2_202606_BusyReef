using UnityEngine;

/// <summary>A tiny shared circular ring sprite for radial Canvas countdown images.</summary>
public static class PowerTimerSprite
{
    private static Sprite cached;

    public static Sprite Get()
    {
        if (cached != null) return cached;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Power Duration Ring Texture";
        texture.hideFlags = HideFlags.DontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size * .5f, size * .5f));
            float outer = Mathf.Clamp01((31f - distance) * 1.4f);
            float inner = Mathf.Clamp01((distance - 23f) * 1.4f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, outer * inner);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        cached = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        cached.name = "Power Duration Ring";
        cached.hideFlags = HideFlags.DontSave;
        return cached;
    }
}
