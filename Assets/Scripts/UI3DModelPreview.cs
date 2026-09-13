using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an assigned 3D prefab into an existing Canvas item without changing
/// that item's layout. The temporary camera/model are never saved in the scene.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class UI3DModelPreview : MonoBehaviour
{
    private static int previewLaneCounter;
    [Header("Assign Your 3D Asset Here")]
    public GameObject modelPrefab;
    public Vector3 modelRotation = new Vector3(18f, -28f, 0f);
    [Range(.25f, 3f)] public float modelScale = 1f;
    public Vector3 modelOffset;

    [Header("Preview")]
    public RawImage previewImage;
    public Vector2 previewAnchorMin = Vector2.zero;
    public Vector2 previewAnchorMax = Vector2.one;
    public bool slowlyRotate = true;
    [Range(0f, 80f)] public float rotationSpeed = 18f;
    [Range(.5f, 3f)] public float cameraSize = 1.25f;

    private GameObject previewRoot;
    private Transform modelInstance;
    private Camera previewCamera;
    private RenderTexture renderTexture;
    private GameObject lastPrefab;
    private bool rebuildRequested = true;
    private int previewLaneId;
    private float lastRenderTime;
    private bool needsRender;
    private readonly Vector3[] screenCorners = new Vector3[4];

    private void OnEnable() => rebuildRequested = true;

    private void OnValidate()
    {
        ApplyOutputRect();
        rebuildRequested = true;
    }

    private void Update()
    {
        if (lastPrefab != modelPrefab) rebuildRequested = true;
        if (rebuildRequested) RebuildPreview();
        if (modelInstance == null || previewCamera == null || !IsVisibleOnScreen()) return;
        float now = Time.realtimeSinceStartup;
        if (!needsRender && (!slowlyRotate || now - lastRenderTime < .1f)) return;
        if (slowlyRotate && lastRenderTime > 0f)
            modelInstance.Rotate(Vector3.up, rotationSpeed * Mathf.Min(now - lastRenderTime, .25f), Space.Self);
        previewCamera.Render();
        lastRenderTime = now;
        needsRender = false;
    }

    private bool IsVisibleOnScreen()
    {
        if (previewImage == null || !previewImage.gameObject.activeInHierarchy) return false;
        Canvas canvas = previewImage.canvas;
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return true;
        previewImage.rectTransform.GetWorldCorners(screenCorners);
        float left = Mathf.Min(screenCorners[0].x, screenCorners[2].x);
        float right = Mathf.Max(screenCorners[0].x, screenCorners[2].x);
        float bottom = Mathf.Min(screenCorners[0].y, screenCorners[2].y);
        float top = Mathf.Max(screenCorners[0].y, screenCorners[2].y);
        return right > 0f && left < Screen.width && top > 0f && bottom < Screen.height;
    }

    private void OnDisable() => ReleasePreview();
    private void OnDestroy() => ReleasePreview();

    public void EnsureOutputForEditor()
    {
        if (previewImage == null)
        {
            Transform existing = transform.Find("3D Asset Preview");
            if (existing != null) previewImage = existing.GetComponent<RawImage>();
        }
        if (previewImage == null)
        {
            GameObject output = new GameObject("3D Asset Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            output.transform.SetParent(transform, false);
            output.transform.SetAsFirstSibling();
            previewImage = output.GetComponent<RawImage>();
            previewImage.raycastTarget = false;
            previewImage.color = Color.white;
        }
        ApplyOutputRect();
    }

    private void ApplyOutputRect()
    {
        if (previewImage == null) return;
        RectTransform rect = previewImage.rectTransform;
        rect.anchorMin = previewAnchorMin;
        rect.anchorMax = previewAnchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void RebuildPreview()
    {
        rebuildRequested = false;
        ReleasePreview();
        lastPrefab = modelPrefab;
        EnsureOutputForEditor();
        if (previewImage == null) return;
        previewImage.enabled = modelPrefab != null;
        if (modelPrefab == null) return;

        int textureSize = Application.isPlaying ? 256 : 384;
        renderTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = name + " 3D UI Preview",
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 2
        };
        renderTexture.Create();
        previewImage.texture = renderTexture;

        if (previewLaneId == 0) previewLaneId = ++previewLaneCounter;
        float lane = 10000f + previewLaneId * 12f;
        previewRoot = new GameObject(name + " UI Preview Runtime") { hideFlags = HideFlags.HideAndDontSave };
        previewRoot.SetActive(false);
        previewRoot.transform.position = new Vector3(lane, lane, lane);

        GameObject clone = Instantiate(modelPrefab, previewRoot.transform);
        clone.name = modelPrefab.name;
        clone.hideFlags = HideFlags.HideAndDontSave;
        modelInstance = clone.transform;
        modelInstance.localPosition = Vector3.zero;
        modelInstance.localRotation = Quaternion.Euler(modelRotation);
        DisablePrefabBehaviours(clone);
        SetLayerRecursively(clone, 31);

        GameObject cameraObject = new GameObject("Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(previewRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -6f);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = cameraSize;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << 31;
        previewCamera.targetTexture = renderTexture;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = true;

        GameObject lightObject = new GameObject("Preview Light") { hideFlags = HideFlags.HideAndDontSave };
        lightObject.transform.SetParent(previewRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        light.cullingMask = 1 << 31;
        previewRoot.SetActive(true);
        FitModelToCamera();
        lastRenderTime = 0f;
        needsRender = true;
    }

    private void FitModelToCamera()
    {
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float largest = Mathf.Max(.001f, bounds.size.x, bounds.size.y, bounds.size.z);
        modelInstance.localScale *= (2f * cameraSize * .72f / largest) * modelScale;

        bounds = modelInstance.GetComponentsInChildren<Renderer>(true)[0].bounds;
        renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 localCenter = previewRoot.transform.InverseTransformPoint(bounds.center);
        modelInstance.localPosition += modelOffset - localCenter;
    }

    private static void DisablePrefabBehaviours(GameObject root)
    {
        foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            behaviour.enabled = false;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    private static void SetLayerRecursively(GameObject item, int layer)
    {
        item.layer = layer;
        foreach (Transform child in item.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private void ReleasePreview()
    {
        if (previewImage != null) previewImage.texture = null;
        if (previewRoot != null) DisposePreviewObject(previewRoot);
        if (renderTexture != null)
        {
            renderTexture.Release();
            DisposePreviewObject(renderTexture);
        }
        previewRoot = null;
        modelInstance = null;
        previewCamera = null;
        renderTexture = null;
    }

    private static void DisposePreviewObject(Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }
}
