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
    private float renderedAspect;
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
        if (previewImage != null && renderTexture != null &&
            Mathf.Abs(GetOutputAspect() - renderedAspect) > .08f) rebuildRequested = true;
        if (rebuildRequested) RebuildPreview();
        if (modelInstance == null || previewCamera == null || !IsVisibleOnScreen()) return;
        float now = Time.realtimeSinceStartup;
        if (!needsRender && (!slowlyRotate || now - lastRenderTime < .05f)) return;
        if (slowlyRotate && lastRenderTime > 0f)
            modelInstance.RotateAround(previewRoot.transform.TransformPoint(modelOffset), Vector3.up,
                rotationSpeed * Mathf.Min(now - lastRenderTime, .25f));
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
        Canvas.ForceUpdateCanvases();
        Rect outputRect = previewImage.rectTransform.rect;
        if (outputRect.width < 2f || outputRect.height < 2f)
        {
            // Hidden sliding pages can report zero size during their first
            // layout pass. Wait instead of baking a stretched preview texture.
            rebuildRequested = true;
            return;
        }

        int shortSide = Application.isPlaying ? 128 : 192;
        renderedAspect = GetOutputAspect();
        int textureWidth = renderedAspect >= 1f ? Mathf.RoundToInt(shortSide * renderedAspect) : shortSide;
        int textureHeight = renderedAspect < 1f ? Mathf.RoundToInt(shortSide / renderedAspect) : shortSide;
        if (textureWidth > 1024) { textureWidth = 1024; textureHeight = Mathf.RoundToInt(textureWidth / renderedAspect); }
        if (textureHeight > 1024) { textureHeight = 1024; textureWidth = Mathf.RoundToInt(textureHeight * renderedAspect); }
        renderTexture = new RenderTexture(Mathf.Max(16, textureWidth), Mathf.Max(16, textureHeight), 16, RenderTextureFormat.ARGB32)
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
        // Keep the prefab's authored orientation (notably the Seal's imported
        // axis correction), then apply only the Inspector's preview turn.
        modelInstance.localRotation = Quaternion.Euler(modelRotation) * modelInstance.localRotation;
        DisablePrefabBehaviours(clone);
        SetLayerRecursively(clone, 31);

        GameObject cameraObject = new GameObject("Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(previewRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -6f);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = cameraSize;
        previewCamera.aspect = renderTexture.width / (float)renderTexture.height;
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
        PrepareParticlePreview();
        FitModelToCamera();
        lastRenderTime = 0f;
        needsRender = true;
    }

    private void FitModelToCamera()
    {
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        bool particleOnly = true;
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!(renderer is ParticleSystemRenderer)) particleOnly = false;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found) return;

        // Particle-system bounds include future travel and can dwarf the visible
        // bubble. Frame the emitted particle around its actual emitter instead.
        if (particleOnly) bounds = new Bounds(modelInstance.position, Vector3.one * .85f);
        Vector3 size = previewRoot.transform.InverseTransformVector(bounds.size);
        // Allow a full turn without the model clipping when its depth becomes
        // its on-screen width.
        float visibleWidth = Mathf.Max(.001f, Mathf.Abs(size.x), Mathf.Abs(size.z));
        float visibleHeight = Mathf.Max(.001f, Mathf.Abs(size.y));
        // Keep the prefab's native scale and proportions. Move the preview
        // camera instead of enlarging or shrinking the model to fit each slot.
        float heightFit = visibleHeight / (2f * .72f);
        float widthFit = visibleWidth / (2f * .72f * previewCamera.aspect);
        previewCamera.orthographicSize = Mathf.Max(.05f,
            Mathf.Max(heightFit, widthFit) * (cameraSize / 1.25f) / Mathf.Max(.25f, modelScale));
        previewCamera.transform.localPosition = new Vector3(0f, 0f,
            -Mathf.Max(6f, Mathf.Abs(size.z) * 2f + 2f));

        // Centre the visible geometry, including prefabs with off-centre roots.
        if (particleOnly) bounds = new Bounds(modelInstance.position, Vector3.one);
        Vector3 localCenter = previewRoot.transform.InverseTransformPoint(bounds.center);
        modelInstance.localPosition += modelOffset - localCenter;
    }

    private void PrepareParticlePreview()
    {
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
            if (renderer.enabled && !(renderer is ParticleSystemRenderer)) return;

        foreach (ParticleSystem system in modelInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null || !particleRenderer.enabled) continue;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = system.emission;
            emission.enabled = false;
            ParticleSystem.EmitParams bubble = new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                startSize = .8f,
                startLifetime = 1000f
            };
            system.Emit(bubble, 1);
        }
    }

    private float GetOutputAspect()
    {
        if (previewImage == null) return 1f;
        Rect rect = previewImage.rectTransform.rect;
        return rect.width / Mathf.Max(1f, rect.height);
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
