using UnityEngine;
using UnityEngine.UI;

public class AdaptiveUILayout : MonoBehaviour
{
    public CanvasScaler scaler;
    public RectTransform dpad;
    public StageCarousel3D carousel;
    private bool wasLandscape;

    private void Start()
    {
        wasLandscape = Screen.width > Screen.height;
        Apply();
    }

    private void Update()
    {
        bool landscape = Screen.width > Screen.height;
        if (landscape == wasLandscape) return;
        wasLandscape = landscape;
        Apply();
    }

    private void Apply()
    {
        bool landscape = Screen.width > Screen.height;
        if (scaler != null) scaler.referenceResolution = landscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
        if (carousel != null)
        {
            carousel.radiusX = landscape ? 500f : 350f;
            carousel.radiusY = landscape ? 70f : 110f;
        }
        if (dpad != null)
        {
            dpad.anchorMin = dpad.anchorMax = landscape ? new Vector2(0.87f, 0.25f) : new Vector2(0.78f, 0.14f);
            dpad.anchoredPosition = Vector2.zero;
        }
    }
}
