using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Seven fixed entries; visual layout is supplied by an independent HUD prefab.</summary>
public sealed class PossessionImprintHUD : MonoBehaviour
{
    public PossessionImprintIcon[] icons = new PossessionImprintIcon[7];
    public PossessionImprintTooltip tooltip;
    public PossessionImprintTutorialPrompt tutorialPrompt;

    PossessionImprintManager manager;
    readonly List<Action> pendingArrivals = new List<Action>();
    readonly List<GameObject> activeMeteors = new List<GameObject>();
    Canvas meteorCanvas;
    RectTransform meteorCanvasRect;
    bool ownsMeteorCanvas;

    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int MeteorColorId = Shader.PropertyToID("_MeteorColor");
    static readonly int TailLengthId = Shader.PropertyToID("_TailLength");
    static readonly int GlowId = Shader.PropertyToID("_Glow");
    const float DefaultMeteorSpeed = 1200f;
    const float DefaultMeteorWidth = 42f;
    const float DefaultMeteorTailLength = 0.34f;
    const float DefaultMeteorGlow = 1.8f;
    const int DefaultMeteorCount = 3;
    const float DefaultMeteorArc = 0.35f;
    const float DefaultMeteorStagger = 0.06f;
    static readonly Color DefaultMeteorColor = new Color(0.25f, 0.85f, 1f, 1f);

    void Awake()
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null && rectTransform.localScale.sqrMagnitude <= 0.0001f)
            rectTransform.localScale = Vector3.one;
    }

    void OnEnable()
    {
        manager = PossessionImprintManager.EnsureInstance();
        manager.OnImprintChanged += RefreshChanged;
        manager.OnImprintGainRequested += PlayImprintGain;
        Debug.Log($"[PossessionImprintHUD] Enabled: object='{name}', canvas={(GetComponent<Canvas>() != null ? GetComponent<Canvas>().renderMode.ToString() : "NULL")}, canvasScale={(GetComponent<Canvas>() != null ? GetComponent<Canvas>().scaleFactor.ToString("F3") : "NA")}, icons={(icons != null ? icons.Length : 0)}.");
        RefreshAll();
        ShowPendingTutorial();
    }
    void OnDisable()
    {
        while (pendingArrivals.Count > 0)
            pendingArrivals[pendingArrivals.Count - 1]?.Invoke();
        StopAllCoroutines();
        for (int i = activeMeteors.Count - 1; i >= 0; i--)
        {
            GameObject meteor = activeMeteors[i];
            if (meteor != null)
            {
                RawImage image = meteor.GetComponent<RawImage>();
                if (image != null && image.material != null) Destroy(image.material);
                Destroy(meteor);
            }
        }
        activeMeteors.Clear();
        if (ownsMeteorCanvas && meteorCanvas != null) Destroy(meteorCanvas.gameObject);
        meteorCanvas = null;
        meteorCanvasRect = null;
        ownsMeteorCanvas = false;
        if (manager != null)
        {
            manager.OnImprintChanged -= RefreshChanged;
            manager.OnImprintGainRequested -= PlayImprintGain;
        }
        if (tooltip != null) tooltip.Hide();
    }
    void RefreshChanged(SinType sin, int stacks)
    {
        Refresh(sin, stacks);
        if (tutorialPrompt != null && !manager.HasSeenTutorial(sin))
        {
            tutorialPrompt.Show(sin);
            manager.MarkTutorialSeen(sin);
        }
    }
    void RefreshAll()
    {
        SinType[] order = { SinType.Pride, SinType.Wrath, SinType.Gluttony, SinType.Greed, SinType.Envy, SinType.Lust, SinType.Sloth };
        for (int i = 0; i < order.Length; i++) Refresh(order[i], manager.GetDisplayedStacks(order[i]));
    }

    void ShowPendingTutorial()
    {
        if (tutorialPrompt == null || manager.IsRestoredRun) return;
        SinType[] order = { SinType.Pride, SinType.Wrath, SinType.Gluttony, SinType.Greed, SinType.Envy, SinType.Lust, SinType.Sloth };
        for (int i = 0; i < order.Length; i++)
        {
            SinType sin = order[i];
            if (manager.GetDisplayedStacks(sin) <= 0 || manager.HasSeenTutorial(sin)) continue;
            tutorialPrompt.Show(sin);
            manager.MarkTutorialSeen(sin);
            break;
        }
    }
    void Refresh(SinType sin, int stacks)
    {
        PossessionImprintIcon icon = GetIcon(sin);
        if (icon == null) return;
        icon.sin = sin;
        icon.Refresh(stacks);
    }

    PossessionImprintIcon GetIcon(SinType sin)
    {
        int index = sin == SinType.Pride ? 0 : sin == SinType.Wrath ? 1 : sin == SinType.Gluttony ? 2
            : sin == SinType.Greed ? 3 : sin == SinType.Envy ? 4 : sin == SinType.Lust ? 5 : 6;
        return icons != null && index >= 0 && index < icons.Length ? icons[index] : null;
    }

    void PlayImprintGain(SinType sin, MonsterActor body, int gainedStacks, Action onArrived)
    {
        Debug.Log($"[PossessionImprintHUD] Gain event received: sin={sin}, body={(body != null ? body.name : "NULL")}, bodyWorld={(body != null ? body.transform.position.ToString() : "NA")}, gained={gainedStacks}, pendingBefore={pendingArrivals.Count}, callback={(onArrived != null ? "SET" : "NULL")}.");
        Action completion = null;
        bool completed = false;
        completion = () =>
        {
            if (completed) return;
            completed = true;
            pendingArrivals.Remove(completion);
            onArrived?.Invoke();
        };
        pendingArrivals.Add(completion);
        Debug.Log($"[PossessionImprintHUD] Gain event queued: sin={sin}, body={(body != null ? body.name : "NULL")}, pendingAfter={pendingArrivals.Count}.");
        StartCoroutine(PlayImprintGainAfterCameraUpdate(sin, body, Mathf.Max(1, gainedStacks), completion));
    }

    IEnumerator PlayImprintGainAfterCameraUpdate(SinType sin, MonsterActor body, int gainedStacks, Action onArrived)
    {
        // CameraDirector switches its Follow target during the possession transaction, while
        // Cinemachine writes the real camera pose later in the frame. Sample after that pose
        // has been applied so projection uses the camera that actually rendered the body.
        yield return new WaitForEndOfFrame();

        PossessionImprintIcon targetIcon = GetIcon(sin);
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        Camera sourceCamera = ResolveSourceCamera(canvas);
        ActorVisualFx actorVisualFx = body != null ? body.GetComponent<ActorVisualFx>() : null;
        if (actorVisualFx == null && body != null)
            actorVisualFx = body.GetComponentInParent<ActorVisualFx>();
        float meteorSpeed = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorSpeed : DefaultMeteorSpeed;
        float meteorWidth = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorWidth : DefaultMeteorWidth;
        float meteorTailLength = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorTailLength : DefaultMeteorTailLength;
        float meteorGlow = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorGlow : DefaultMeteorGlow;
        Color meteorColor = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorColor : DefaultMeteorColor;
        int configuredMeteorCount = Mathf.Clamp(actorVisualFx != null ? actorVisualFx.possessionImprintMeteorCount : DefaultMeteorCount, 1, 8);
        int meteorCount = Mathf.Max(configuredMeteorCount, gainedStacks);
        float meteorArc = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorArc : DefaultMeteorArc;
        float meteorStagger = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorStagger : DefaultMeteorStagger;
        if (gainedStacks > 1) meteorStagger = Mathf.Max(0.2f, meteorStagger);
        Debug.Log($"[PossessionImprintHUD] Projection inputs: sin={sin}, body={(body != null ? body.name : "NULL")}, bodyActive={(body != null && body.isActiveAndEnabled)}, bodyWorld={(body != null ? body.transform.position.ToString() : "NA")}, targetIcon={(targetIcon != null ? targetIcon.name : "NULL")}, targetActive={(targetIcon != null && targetIcon.isActiveAndEnabled)}, canvas={(canvas != null ? canvas.name : "NULL")}, canvasMode={(canvas != null ? canvas.renderMode.ToString() : "NA")}, sourceCamera={(sourceCamera != null ? sourceCamera.name : "NULL")}, sourceCameraPos={(sourceCamera != null ? sourceCamera.transform.position.ToString() : "NA")}, sourceCameraPixel={(sourceCamera != null ? sourceCamera.pixelWidth + "x" + sourceCamera.pixelHeight : "NA")}.");
        if (targetIcon == null || body == null || canvas == null || sourceCamera == null || meteorWidth <= 0f)
        {
            Debug.LogWarning($"[PossessionImprintHUD] Gain fallback before projection: sin={sin}, body={(body != null ? body.name : "NULL")}, targetIcon={(targetIcon != null ? targetIcon.name : "NULL")}, canvas={(canvas != null ? canvas.name : "NULL")}, sourceCamera={(sourceCamera != null ? sourceCamera.name : "NULL")}, meteorWidth={meteorWidth:F2}.");
            onArrived?.Invoke();
            yield break;
        }

        RectTransform targetRect = targetIcon.transform as RectTransform;
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (targetRect == null || canvasRect == null || !targetIcon.isActiveAndEnabled)
        {
            onArrived?.Invoke();
            yield break;
        }

        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (canvasCamera == null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvasCamera = sourceCamera;
        // Actor roots are gameplay pivots, not visual anchors.  Different monsters have
        // different root heights and child-model offsets, so a fixed world-space offset
        // makes later possessions appear to emit from an unrelated point on screen.
        Vector3 sourceWorld = GetMeteorWorldOrigin(body);
        Vector3 sourceScreen3 = sourceCamera.WorldToScreenPoint(sourceWorld);
        Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera,
            targetRect.TransformPoint(targetRect.rect.center));
        Debug.Log($"[PossessionImprintHUD] Projection result: sin={sin}, body='{body.name}', sourceWorld={sourceWorld}, sourceScreen={sourceScreen3}, targetScreen={targetScreen}, canvasCamera={(canvasCamera != null ? canvasCamera.name : "NULL")}, targetRectWorld={targetRect.position}, targetRect={targetRect.rect}.");
        if (sourceScreen3.z <= 0f)
        {
            Debug.LogWarning($"[PossessionImprintHUD] Gain fallback because source is behind camera: sin={sin}, body='{body.name}', sourceScreen={sourceScreen3}, sourceWorld={sourceWorld}, camera='{sourceCamera.name}'.");
            onArrived?.Invoke();
            yield break;
        }

        RectTransform flightCanvasRect = EnsureMeteorCanvas(canvas);
        if (flightCanvasRect == null)
        {
            onArrived?.Invoke();
            yield break;
        }

        Vector2 startLocal;
        Vector2 endLocal;
        Camera flightCanvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (flightCanvasCamera == null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            flightCanvasCamera = sourceCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(flightCanvasRect, sourceScreen3, flightCanvasCamera, out startLocal)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(flightCanvasRect, targetScreen, flightCanvasCamera, out endLocal))
        {
            Debug.LogWarning($"[PossessionImprintHUD] Gain fallback because screen-to-canvas conversion failed: sin={sin}, body='{body.name}', sourceScreen={sourceScreen3}, targetScreen={targetScreen}, flightCanvas='{flightCanvasRect.name}'.");
            onArrived?.Invoke();
            yield break;
        }
        // ScreenPointToLocalPointInRectangle returns coordinates relative to the
        // RectTransform pivot.  A child whose anchors are both zero is positioned
        // relative to rect.min, so convert both endpoints to that child coordinate
        // space before assigning anchoredPosition.  Without this offset, a centered
        // HUD places the whole meteor path roughly half a screen toward the lower-left.
        Vector2 childAnchorOrigin = flightCanvasRect.rect.min;
        startLocal -= childAnchorOrigin;
        endLocal -= childAnchorOrigin;
        Debug.Log($"[PossessionImprintHUD] Flight coordinates resolved: sin={sin}, body='{body.name}', flightCanvas='{flightCanvasRect.name}', flightCanvasMode={canvas.renderMode}, flightCanvasRect={flightCanvasRect.rect}, childAnchorOrigin={childAnchorOrigin}, flightCanvasScale={flightCanvasRect.lossyScale}, startLocal={startLocal}, endLocal={endLocal}, distance={Vector2.Distance(startLocal, endLocal):F2}.");

        Shader shader = actorVisualFx != null ? actorVisualFx.possessionImprintMeteorShader : null;
        if (shader == null) shader = Shader.Find("UI/PossessionImprintMeteor");
        if (shader == null) shader = Resources.Load<Shader>("MonsterTelegraph/PossessionImprintMeteor");
        bool hasMeteorShader = shader != null;
        if (!hasMeteorShader)
        {
            shader = Shader.Find("UI/Default");
            Debug.LogWarning("[PossessionImprintHUD] 流星 Shader 未加载，使用可见的 UI 兜底；请确认 PossessionImprintMeteor.shader 已导入。");
        }
        if (shader == null)
        {
            Debug.LogWarning($"[PossessionImprintHUD] Gain fallback because no shader is available: sin={sin}, body='{body.name}'.");
            onArrived?.Invoke();
            yield break;
        }

        Vector2 path = endLocal - startLocal;
        float distance = path.magnitude;
        if (distance <= 0.01f)
        {
            Debug.Log($"[PossessionImprintHUD] Zero-distance flight completes immediately: sin={sin}, body='{body.name}', startLocal={startLocal}, endLocal={endLocal}.");
            onArrived?.Invoke();
            yield break;
        }

        Vector2 flightDir = path / distance;
        Vector2 perpendicular = new Vector2(-flightDir.y, flightDir.x);
        float arcHeight = distance * Mathf.Clamp(meteorArc, 0f, 0.8f);
        float tailPx = distance * Mathf.Clamp(meteorTailLength, 0.08f, 0.75f);

        for (int i = 0; i < meteorCount; i++)
        {
            GameObject meteorObject = new GameObject("PossessionImprintMeteor", typeof(RectTransform), typeof(RawImage));
            meteorObject.transform.SetParent(flightCanvasRect, false);
            meteorObject.transform.SetAsLastSibling();
            activeMeteors.Add(meteorObject);

            RectTransform meteorRect = meteorObject.transform as RectTransform;
            meteorRect.anchorMin = Vector2.zero;
            meteorRect.anchorMax = Vector2.zero;
            meteorRect.pivot = new Vector2(0.5f, 0.5f);
            meteorRect.anchoredPosition = startLocal;
            meteorRect.sizeDelta = new Vector2(tailPx, meteorWidth);
            meteorRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(path.y, path.x) * Mathf.Rad2Deg);

            RawImage meteorImage = meteorObject.GetComponent<RawImage>();
            meteorImage.texture = Texture2D.whiteTexture;
            meteorImage.raycastTarget = false;
            Material meteorMaterial = new Material(shader);
            if (hasMeteorShader)
            {
                meteorMaterial.SetColor(MeteorColorId, meteorColor);
                meteorMaterial.SetFloat(TailLengthId, 1f); // 尾巴占满短矩形（矩形长度已按 tailLength 缩短）
                meteorMaterial.SetFloat(GlowId, meteorGlow);
                meteorMaterial.SetFloat(ProgressId, 1f);   // 头部固定在矩形末端，由 C# 沿抛物线移动
            }
            else
            {
                meteorImage.color = meteorColor;
            }
            meteorImage.material = meteorMaterial;

            Action arrived = i == meteorCount - 1 ? onArrived : null;
            StartCoroutine(AnimateMeteorParabola(meteorObject, meteorRect, meteorMaterial, startLocal, endLocal, path, perpendicular, arcHeight, distance, meteorSpeed, i * meteorStagger, arrived));
        }

        Debug.Log($"[PossessionImprintHUD] Meteors spawned: count={meteorCount}, sin={sin}, body='{body.name}', arc={arcHeight:F1}, tailPx={tailPx:F1}, stagger={meteorStagger:F2}, duration={distance / Mathf.Max(1f, meteorSpeed):F3}s.");
    }

    RectTransform EnsureMeteorCanvas(Canvas targetCanvas)
    {
        if (targetCanvas == null) return null;
        if (meteorCanvas == targetCanvas && meteorCanvasRect != null) return meteorCanvasRect;

        if (ownsMeteorCanvas && meteorCanvas != null)
            Destroy(meteorCanvas.gameObject);

        // The icon and the meteor must share the exact same CanvasScaler and
        // render mode.  Creating a second 1920x1080 overlay here makes the
        // projection depend on a different RectTransform pivot/scale and was
        // the source of the lower-left drift after subsequent possessions.
        meteorCanvas = targetCanvas;
        meteorCanvasRect = targetCanvas.transform as RectTransform;
        ownsMeteorCanvas = false;
        return meteorCanvasRect;
    }

    static Camera ResolveSourceCamera(Canvas canvas)
    {
        // The possession flow changes CameraDirector.Target.  Use that exact camera when
        // available instead of relying on a tag lookup which can select another active
        // camera in scenes that add auxiliary cameras at runtime.
        CameraDirector director = CameraDirector.Instance;
        Camera camera = director != null ? director.GetComponent<Camera>() : null;
        if (IsUsableGameplayCamera(camera)) return camera;

        camera = Camera.main;
        if (IsUsableGameplayCamera(camera)) return camera;

        if (canvas != null && IsUsableGameplayCamera(canvas.worldCamera))
            return canvas.worldCamera;

        Camera[] cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            camera = cameras[i];
            if (IsUsableGameplayCamera(camera))
                return camera;
        }
        return null;
    }

    static bool IsUsableGameplayCamera(Camera camera)
    {
        return camera != null && camera.isActiveAndEnabled && camera.targetTexture == null
            && camera.cameraType == CameraType.Game;
    }

    static Vector3 GetMeteorWorldOrigin(MonsterActor body)
    {
        Enemy enemy = body as Enemy;
        if (enemy != null && enemy.soulAnchorPoint != null)
            return enemy.soulAnchorPoint.position;

        Renderer[] renderers = body.GetComponentsInChildren<Renderer>();
        Renderer closestBodyRenderer = null;
        float closestBodyDistance = float.MaxValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer))
                continue;

            bool isBodyRenderer = true;
            Transform current = renderer.transform;
            while (current != null)
            {
                if (current.GetComponent<SoulActor>() != null || current.GetComponent<EnemyAbility>() != null
                    || current.GetComponent<Canvas>() != null || current.GetComponent<Light>() != null)
                {
                    isBodyRenderer = false;
                    break;
                }

                string objectName = current.name;
                if (objectName.StartsWith("Boss Reserve Sin Ring", StringComparison.Ordinal)
                    || objectName.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0
                    || objectName.IndexOf("Trail", StringComparison.OrdinalIgnoreCase) >= 0
                    || objectName.IndexOf("Headfire", StringComparison.OrdinalIgnoreCase) >= 0
                    || objectName.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isBodyRenderer = false;
                    break;
                }

                if (current == body.transform) break;
                current = current.parent;
            }

            if (!isBodyRenderer) continue;
            float distance = (renderer.bounds.center - body.transform.position).sqrMagnitude;
            if (distance >= closestBodyDistance) continue;
            closestBodyRenderer = renderer;
            closestBodyDistance = distance;
        }

        if (closestBodyRenderer == null)
            return body.transform.position + Vector3.up * 1.1f * body.CombatScaleMultiplier;

        Bounds bodyBounds = closestBodyRenderer.bounds;
        return bodyBounds.center + Vector3.up * bodyBounds.extents.y * 0.2f;
    }

    IEnumerator AnimateMeteorParabola(GameObject meteorObject, RectTransform meteorRect, Material meteorMaterial, Vector2 startLocal, Vector2 endLocal, Vector2 path, Vector2 perpendicular, float arcHeight, float distance, float meteorSpeed, float startDelay, Action onArrived)
    {
        if (startDelay > 0f)
        {
            float waited = 0f;
            while (waited < startDelay && meteorObject != null)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        float duration = distance / Mathf.Max(1f, meteorSpeed);
        float elapsed = 0f;
        while (elapsed < duration && meteorObject != null && meteorMaterial != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            // 抛物线：沿 path 前进，同时按 4u(1-u) 上抛，形成弧线
            meteorRect.anchoredPosition = Vector2.Lerp(startLocal, endLocal, u)
                + perpendicular * (arcHeight * 4f * u * (1f - u));

            // 切线（速度方向）对齐陨石朝向，让头部始终指向前进方向
            Vector2 tangent = path + perpendicular * (arcHeight * 4f * (1f - 2f * u));
            if (tangent.sqrMagnitude > 0.0001f)
                meteorRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
            yield return null;
        }

        if (meteorObject != null) meteorRect.anchoredPosition = endLocal;
        onArrived?.Invoke();
        if (meteorObject != null)
        {
            activeMeteors.Remove(meteorObject);
            Destroy(meteorObject);
        }
        if (meteorMaterial != null) Destroy(meteorMaterial);
    }
}
