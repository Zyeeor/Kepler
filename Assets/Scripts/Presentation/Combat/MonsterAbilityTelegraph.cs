using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Runtime presentation for an AI monster ability wind-up. The ability owns the gameplay
/// timing; this component renders the existing range indicator and a screen-space ability
/// HUD cloned from AbilityCooldownUI while the wind-up is active.
/// </summary>
public sealed class MonsterAbilityTelegraph : MonoBehaviour
{
    private const float IndicatorEdgeRadius = 0.84f;
    private const string IndicatorMaterialResourcePath = "MonsterTelegraph/MonsterTelegraphIndicator";
    private static readonly int IndicatorColorId = Shader.PropertyToID("_IndicatorColor");
    private static readonly int IndicatorIntensityId = Shader.PropertyToID("_IndicatorIntensity");
    private static readonly int IndicatorProgressId = Shader.PropertyToID("_IndicatorProgress");
    private static readonly int IndicatorShapeId = Shader.PropertyToID("_ShapeType");
    private static readonly int IndicatorSectorAngleId = Shader.PropertyToID("_SectorAngle");

    private GameObject _indicatorObject;
    private Material _indicatorMaterial;
    private Renderer _indicatorRenderer;
    private MaterialPropertyBlock _indicatorPropertyBlock;
    private bool _usesSharedIndicatorMaterial;
    private bool _ownsIndicatorMaterial;
    private static Material _sharedIndicatorMaterial;

    private EnemyAbility _ability;
    private MonsterActor _owner;
    private bool _isShowing;
    private float _currentProgress;

    private Canvas _hudCanvas;
    private RectTransform _hudCanvasRect;
    private Camera _worldCamera;
    private RectTransform _hudRoot;
    private Image _hudIcon;
    private Image _hudCooldownOverlay;
    private AbilityCooldownUI _cooldownTemplate;
    private MonsterSkillIconConfig _iconConfig;
    private Color _hudIconReadyColor = Color.white;
    private Vector3 _hudBaseScale = Vector3.one;
    private float _hudTemplateSize = 80f;

    /// <summary>Legacy circle-only entry point (kept for compatibility).</summary>
    public void Begin(EnemyAbility ability, Vector3 indicatorCenter, float indicatorRadius, bool showIndicator)
    {
        Begin(ability, new EnemyTelegraphGeometry
        {
            shape = EnemyIndicatorShape.Circle,
            center = indicatorCenter,
            radius = indicatorRadius,
            isValid = showIndicator && indicatorRadius > 0f
        }, showIndicator);
    }

    public void Begin(EnemyAbility ability, EnemyTelegraphGeometry geometry, bool showIndicator)
    {
        if (ability == null) return;

        _ability = ability;
        _owner = ability.OwnerMonster;
        _isShowing = true;

        EnsureIndicator(showIndicator);
        ApplyIndicatorGeometry(geometry, showIndicator);

        EnsureHud();
        ApplyHudIcon(ability);
        if (_hudRoot != null)
        {
            _hudRoot.gameObject.SetActive(true);
            _hudRoot.SetAsLastSibling();
        }

        SetProgress(0f);
    }

    /// <summary>应用一次完整的 indicator 几何（含颜色/强度/进度重置）。Begin 时调用。</summary>
    private void ApplyIndicatorGeometry(EnemyTelegraphGeometry geometry, bool showIndicator)
    {
        if (showIndicator && _indicatorObject != null && _indicatorMaterial != null && geometry.isValid)
        {
            _indicatorObject.SetActive(true);
            ApplyIndicatorTransform(geometry);
            SetIndicatorColor(_ability.enemyIndicatorColor);
            SetIndicatorFloat(IndicatorIntensityId, Mathf.Max(0f, _ability.enemyIndicatorIntensity));
            SetIndicatorFloat(IndicatorProgressId, 0f);
        }
        else if (_indicatorObject != null)
        {
            _indicatorObject.SetActive(false);
        }
    }

    /// <summary>
    /// 只更新 indicator 的世界位置/朝向/形状（不含颜色/强度/进度）。
    /// 供「红条实时对齐实际发射方向」每帧刷新使用。
    /// </summary>
    public void RefreshGeometry(EnemyTelegraphGeometry geometry)
    {
        if (_ability == null || _indicatorObject == null || _indicatorMaterial == null) return;
        if (!geometry.isValid)
        {
            _indicatorObject.SetActive(false);
            return;
        }
        _indicatorObject.SetActive(true);
        ApplyIndicatorTransform(geometry);
    }

    /// <summary>按几何更新 indicator 的世界位置/朝向/形状（Rect / Sector / Circle 三态）。</summary>
    private void ApplyIndicatorTransform(EnemyTelegraphGeometry geometry)
    {
        Vector3 pos = geometry.center + Vector3.up * Mathf.Max(0f, _ability.enemyIndicatorHeight);

        if (geometry.shape == EnemyIndicatorShape.Rect)
        {
            // 矩形预警带：Quad 的 +X（uv.x，长度方向）指向 forward，+Y（uv.y，宽度方向）水平垂直 forward，
            // 法线朝上（Euler(90,0,0) 平躺）。edge 在 shader 里位于 0.84，故世界尺寸 = 目标 / 0.84。
            _indicatorObject.transform.SetPositionAndRotation(
                pos,
                Quaternion.FromToRotation(Vector3.right, geometry.forward) * Quaternion.Euler(90f, 0f, 0f));
            _indicatorObject.transform.localScale = new Vector3(
                geometry.length / IndicatorEdgeRadius,
                geometry.width / IndicatorEdgeRadius,
                1f);
            SetIndicatorFloat(IndicatorShapeId, 1f);
        }
        else if (geometry.shape == EnemyIndicatorShape.Sector)
        {
            // 扇形预警（Pass v1 §13.2）：+X 指向 forward，正方形 scale = 半径 / 0.84，角度由 shader 的 _SectorAngle 控制。
            // 注：length 存的是半径（与 Circle 的 radius 同语义），shader 里 distS=0.84 对应"中心到边中点"，
            // 故 scale 需 ×2（与 Circle 的 radius*2f/0.84 一致），否则扇形半径只有预期一半。
            _indicatorObject.transform.SetPositionAndRotation(
                pos,
                Quaternion.FromToRotation(Vector3.right, geometry.forward) * Quaternion.Euler(90f, 0f, 0f));
            _indicatorObject.transform.localScale = new Vector3(
                geometry.length * 2f / IndicatorEdgeRadius,
                geometry.length * 2f / IndicatorEdgeRadius,
                1f);
            SetIndicatorFloat(IndicatorShapeId, 2f);
            SetIndicatorFloat(IndicatorSectorAngleId, Mathf.Max(1f, geometry.angle));
        }
        else
        {
            _indicatorObject.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
            float diameter = geometry.radius * 2f / IndicatorEdgeRadius;
            _indicatorObject.transform.localScale = new Vector3(diameter, diameter, 1f);
            SetIndicatorFloat(IndicatorShapeId, 0f);
        }
    }

    public void SetProgress(float progress)
    {
        if (!_isShowing) return;

        float value = Mathf.Clamp01(progress);
        _currentProgress = value;
        SetIndicatorFloat(IndicatorProgressId, value);

        SetHudProgress(value);
        UpdateHudPosition();
    }

    public void End()
    {
        _isShowing = false;
        _currentProgress = 0f;
        if (_indicatorObject != null) _indicatorObject.SetActive(false);
        if (_hudRoot != null) _hudRoot.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!_isShowing) return;
        UpdateHudPosition();
        if (_ability != null && _ability.enemyTelegraphLiveAim)
        {
            // 引导剩余时间超过「提前锁定秒数」时才继续追踪；最后 lockLead 秒锁定方向，给玩家反应时间。
            float remaining = _ability.enemyCastLeadTime * (1f - _currentProgress);
            if (remaining > _ability.enemyTelegraphAimLockLead)
                RefreshGeometry(_ability.GetEnemyTelegraphGeometry());
        }
    }

    private void EnsureHud()
    {
        if (_hudRoot != null) return;

        _cooldownTemplate = FindObjectOfType<AbilityCooldownUI>(true);
        if (_cooldownTemplate != null)
        {
            _hudCanvas = _cooldownTemplate.GetComponentInParent<Canvas>();
            _iconConfig = _cooldownTemplate.iconConfig;
        }

        if (_hudCanvas == null)
            _hudCanvas = FindHudCanvas();
        if (_hudCanvas == null) return;

        _hudCanvasRect = _hudCanvas.transform as RectTransform;
        if (_hudCanvasRect == null) return;

        if (_cooldownTemplate != null && _cooldownTemplate.skillIconRoot != null)
        {
            _hudRoot = Instantiate(_cooldownTemplate.skillIconRoot, _hudCanvas.transform, false);
            _hudRoot.name = "MonsterCastHud";
            _hudRoot.gameObject.hideFlags = HideFlags.DontSave;
            _hudBaseScale = _hudRoot.localScale;
            _hudTemplateSize = Mathf.Max(1f, _cooldownTemplate.skillIconRoot.rect.width);

            _hudIcon = FindCloneImage(_cooldownTemplate.skillIconRoot, _cooldownTemplate.skillIconImage);
            _hudCooldownOverlay = FindCloneImage(_cooldownTemplate.skillIconRoot, _cooldownTemplate.skillCooldownOverlay);

            Transform keyHint = FindCloneTransform(_cooldownTemplate.skillIconRoot, _cooldownTemplate.skillKeyHint != null
                ? _cooldownTemplate.skillKeyHint.transform
                : null);
            if (keyHint != null) keyHint.gameObject.SetActive(false);
        }
        else
        {
            CreateFallbackHud();
        }

        if (_iconConfig == null)
            _iconConfig = Resources.Load<MonsterSkillIconConfig>("UI/MonsterSkillIconConfig");
    }

    private void CreateFallbackHud()
    {
        GameObject rootObject = new GameObject("MonsterCastHud", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(_hudCanvas.transform, false);
        rootObject.hideFlags = HideFlags.DontSave;
        _hudRoot = rootObject.GetComponent<RectTransform>();
        _hudRoot.sizeDelta = new Vector2(_hudTemplateSize, _hudTemplateSize);
        _hudBaseScale = Vector3.one;

        GameObject overlayObject = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(_hudRoot, false);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        _hudCooldownOverlay = overlayObject.GetComponent<Image>();
        _hudCooldownOverlay.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        _hudCooldownOverlay.type = Image.Type.Filled;
        _hudCooldownOverlay.fillMethod = Image.FillMethod.Radial360;
        _hudCooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
        _hudCooldownOverlay.fillClockwise = false;
        if (_cooldownTemplate != null && _cooldownTemplate.skillCooldownOverlay != null)
            _hudCooldownOverlay.sprite = _cooldownTemplate.skillCooldownOverlay.sprite;
        _hudIcon = rootObject.GetComponent<Image>();
    }

    private void ApplyHudIcon(EnemyAbility ability)
    {
        if (_hudIcon == null) return;

        Sprite icon = null;
        Color iconColor = Color.white;
        if (_iconConfig != null)
        {
            SinType sin = ResolveSkillIconSin(ability.OwnerMonster);
            MonsterSkillIconConfig.MonsterSlot slot = ResolveMonsterSlot(ability);
            _iconConfig.TryGetMonsterIcon(sin, slot, out icon, out iconColor);
        }

        if (icon == null && _cooldownTemplate != null)
        {
            Image fallbackImage = ResolveFallbackIcon(ability);
            if (fallbackImage != null) icon = fallbackImage.sprite;
        }

        if (icon == null && _iconConfig != null)
        {
            Sprite identityIcon;
            Color identityColor;
            if (_iconConfig.TryGetMonsterIdentity(ResolveSkillIconSin(ability.OwnerMonster), out identityIcon, out identityColor))
            {
                icon = identityIcon;
                iconColor = identityColor;
            }
        }

        _hudIcon.sprite = icon;
        _hudIconReadyColor = iconColor;
        _hudIcon.color = MutedColor(iconColor);
    }

    private Image ResolveFallbackIcon(EnemyAbility ability)
    {
        if (_cooldownTemplate == null) return null;
        switch (ability.type)
        {
            case EnemyAbility.AbilityType.BasicAttack:
                return _cooldownTemplate.basicIconImage;
            case EnemyAbility.AbilityType.Mobility:
                return _cooldownTemplate.possessIconImage;
            default:
                return _cooldownTemplate.skillIconImage;
        }
    }

    private static MonsterSkillIconConfig.MonsterSlot ResolveMonsterSlot(EnemyAbility ability)
    {
        switch (ability.type)
        {
            case EnemyAbility.AbilityType.BasicAttack:
                return MonsterSkillIconConfig.MonsterSlot.BasicAttack;
            case EnemyAbility.AbilityType.Mobility:
                return MonsterSkillIconConfig.MonsterSlot.Mobility;
            default:
                return MonsterSkillIconConfig.MonsterSlot.Skill;
        }
    }

    private static SinType ResolveSkillIconSin(MonsterActor monster)
    {
        if (monster == null) return SinType.None;
        if (monster.sinType != SinType.Gluttony) return monster.sinType;

        GluttonyBodyState state = monster.GetComponent<GluttonyBodyState>();
        if (state != null && state.HasCopiedSkill && state.CopiedSkillSourceSin != SinType.None)
            return state.CopiedSkillSourceSin;
        return monster.sinType;
    }

    private void SetHudProgress(float progress)
    {
        if (_hudIcon != null)
            _hudIcon.color = Color.Lerp(MutedColor(_hudIconReadyColor), _hudIconReadyColor, progress);
        if (_hudCooldownOverlay != null)
            _hudCooldownOverlay.fillAmount = 1f - progress;
    }

    private static Color MutedColor(Color source)
    {
        return new Color(0.32f, 0.32f, 0.32f, source.a);
    }

    private void UpdateHudPosition()
    {
        if (!_isShowing || _hudRoot == null || _hudCanvas == null || _hudCanvasRect == null || _owner == null || _ability == null)
            return;

        if (_worldCamera == null || !_worldCamera.isActiveAndEnabled)
        {
            _worldCamera = _hudCanvas.renderMode == RenderMode.ScreenSpaceCamera
                ? _hudCanvas.worldCamera
                : Camera.main;
        }
        if (_worldCamera == null)
        {
            _hudRoot.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPosition = _worldCamera.WorldToScreenPoint(_owner.transform.position);
        float halfSize = Mathf.Max(8f, _ability.enemyCastHudSize) * 0.5f;
        bool visible = screenPosition.z > 0f
            && screenPosition.x >= -halfSize
            && screenPosition.x <= Screen.width + halfSize
            && screenPosition.y >= -halfSize
            && screenPosition.y <= Screen.height + halfSize;
        if (!visible)
        {
            _hudRoot.gameObject.SetActive(false);
            return;
        }

        Camera eventCamera = _hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _worldCamera;
        Vector3 hudWorldPosition;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _hudCanvasRect, screenPosition, eventCamera, out hudWorldPosition))
        {
            _hudRoot.gameObject.SetActive(false);
            return;
        }

        _hudRoot.gameObject.SetActive(true);
        Vector2 configuredOffset = _ability.enemyCastHudScreenOffset;
        Vector3 offsetWorld = _hudCanvasRect.TransformVector(new Vector3(configuredOffset.x, configuredOffset.y, 0f));
        _hudRoot.position = hudWorldPosition + offsetWorld;
        float size = Mathf.Max(8f, _ability.enemyCastHudSize);
        _hudRoot.localScale = _hudBaseScale * (size / _hudTemplateSize);
    }

    private void EnsureIndicator(bool needed)
    {
        if (!needed || _indicatorObject != null) return;

        _indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _indicatorObject.name = "MonsterCastIndicator";
        _indicatorObject.hideFlags = HideFlags.DontSave;
        Collider collider = _indicatorObject.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        _indicatorRenderer = _indicatorObject.GetComponent<Renderer>();
        ConfigureRenderer(_indicatorRenderer);

        Material template = Resources.Load<Material>(IndicatorMaterialResourcePath);
        Shader shader = Shader.Find("Possession/MonsterTelegraphIndicator");
        if (template != null || shader != null)
        {
            _usesSharedIndicatorMaterial = GameManager.SharedMaterialOptimizationEnabled;
            if (_usesSharedIndicatorMaterial)
            {
                _indicatorMaterial = template != null ? template : GetSharedIndicatorMaterial(shader);
                _ownsIndicatorMaterial = false;
                _indicatorPropertyBlock = new MaterialPropertyBlock();
            }
            else
            {
                _indicatorMaterial = template != null ? new Material(template) : new Material(shader);
                _indicatorMaterial.hideFlags = HideFlags.DontSave;
                _ownsIndicatorMaterial = true;
            }
            if (_indicatorRenderer != null) _indicatorRenderer.sharedMaterial = _indicatorMaterial;
        }
    }

    private static Material GetSharedIndicatorMaterial(Shader shader)
    {
        if (_sharedIndicatorMaterial != null || shader == null) return _sharedIndicatorMaterial;
        _sharedIndicatorMaterial = new Material(shader)
        {
            name = "SharedMonsterTelegraphIndicator",
            hideFlags = HideFlags.HideAndDontSave
        };
        if (SystemInfo.supportsInstancing) _sharedIndicatorMaterial.enableInstancing = true;
        return _sharedIndicatorMaterial;
    }

    private void SetIndicatorColor(Color color)
    {
        if (_indicatorMaterial == null || _indicatorRenderer == null) return;
        if (!_usesSharedIndicatorMaterial)
        {
            _indicatorMaterial.SetColor(IndicatorColorId, color);
            return;
        }

        if (_indicatorPropertyBlock == null) _indicatorPropertyBlock = new MaterialPropertyBlock();
        _indicatorRenderer.GetPropertyBlock(_indicatorPropertyBlock);
        _indicatorPropertyBlock.SetColor(IndicatorColorId, color);
        _indicatorRenderer.SetPropertyBlock(_indicatorPropertyBlock);
    }

    private void SetIndicatorFloat(int propertyId, float value)
    {
        if (_indicatorMaterial == null || _indicatorRenderer == null) return;
        if (!_usesSharedIndicatorMaterial)
        {
            _indicatorMaterial.SetFloat(propertyId, value);
            return;
        }

        if (_indicatorPropertyBlock == null) _indicatorPropertyBlock = new MaterialPropertyBlock();
        _indicatorRenderer.GetPropertyBlock(_indicatorPropertyBlock);
        _indicatorPropertyBlock.SetFloat(propertyId, value);
        _indicatorRenderer.SetPropertyBlock(_indicatorPropertyBlock);
    }

    private Image FindCloneImage(Transform sourceRoot, Image sourceImage)
    {
        Transform cloneTransform = FindCloneTransform(sourceRoot, sourceImage != null ? sourceImage.transform : null);
        return cloneTransform != null ? cloneTransform.GetComponent<Image>() : null;
    }

    private Transform FindCloneTransform(Transform sourceRoot, Transform sourceTarget)
    {
        if (sourceRoot == null || sourceTarget == null || _hudRoot == null) return null;
        if (sourceTarget == sourceRoot) return _hudRoot;
        string path = sourceTarget.name;
        Transform current = sourceTarget.parent;
        while (current != null && current != sourceRoot)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return current == sourceRoot ? _hudRoot.Find(path) : null;
    }

    private static Canvas FindHudCanvas()
    {
        GameObject namedCanvas = GameObject.Find("UICanvas");
        if (namedCanvas != null)
        {
            Canvas canvas = namedCanvas.GetComponent<Canvas>();
            if (canvas != null) return canvas;
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                return canvases[i];
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private static void ConfigureRenderer(Renderer renderer)
    {
        if (renderer == null) return;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void OnDisable()
    {
        End();
    }

    private void OnDestroy()
    {
        if (_ownsIndicatorMaterial && _indicatorMaterial != null) Destroy(_indicatorMaterial);
        if (_indicatorObject != null) Destroy(_indicatorObject);
        if (_hudRoot != null) Destroy(_hudRoot.gameObject);
    }
}
