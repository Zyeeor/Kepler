# ActorVisualFx 配置指南

> 面向：策划、美术、技术美术
>
> 适用对象：怪物 / 可附身载体 Prefab
>
> 配置入口：Prefab 上的 `ActorVisualFx` 组件

## 一、这个组件负责什么

`ActorVisualFx` 负责角色在不同战斗状态下的临时表现：

- 附身状态：身体边缘发光；
- 可附身尸体状态：尸体表面 Rim + 屏幕空间轮廓高亮；
- 精英状态：精英边缘发光与脉冲；
- 受击：短暂闪白；
- 尸体消散：溶解边缘发光。

所有高亮材质都是运行时实例，不会修改项目中的共享材质资产。修改 Prefab 上的参数后，需要保存 Prefab，运行时才会对该怪物生效。

## 二、配置入口与注意事项

1. 打开怪物 Prefab。
2. 选中带有 `MonsterActor` 的根对象。
3. 在同一个根对象上找到 `ActorVisualFx`。
4. 修改对应分组中的参数并保存 Prefab。

`MonsterActor` 会在运行时保证根对象上存在唯一的 `ActorVisualFx`。如果希望每个怪物拥有自己的持久化配置，建议直接把组件加到 Prefab 根对象，而不要依赖运行时自动添加。

如果子物体或外层包装对象上还有重复的 `ActorVisualFx`，运行时会清理重复实例；实际应只配置 `MonsterActor` 根对象上的那一个组件。

## 三、尸体轮廓高亮配置（重点）

尸体进入 `Downed` 且仍可被附身时，系统会自动打开尸体高亮；进入消散、被附身或回收时会自动关闭。

### 3.1 推荐调节顺序

先调轮廓，再调身体表面 Rim，最后决定是否需要脉冲：

1. `Corpse Outline Color`：确定识别色；
2. `Corpse Outline Width`：确定轮廓厚度；
3. `Corpse Outline Intensity`：确定亮度；
4. `Corpse Outline Pulse Speed / Amount`：调整呼吸感；
5. `Corpse Rim Intensity / Power`：补充尸体表面边缘光。

### 3.2 轮廓参数

| Inspector 字段 | 作用 | 默认值 | 调节建议 |
|---|---|---:|---|
| `Corpse Outline Color` | 屏幕空间外轮廓的 HDR 颜色 | 青蓝色 | 建议优先用颜色区分“可附身尸体”；支持 HDR，颜色亮度可超过 1 |
| `Corpse Outline Intensity` | 外轮廓亮度 | 2.4 | 太刺眼先降低此值；设为 `0` 可只保留身体表面 Rim |
| `Corpse Outline Width` | 外轮廓厚度，单位为世界空间 | 0.018 | 太细看不清就增大；模型尺寸差异较大时需要按怪物单独调整 |
| `Corpse Outline Pulse Speed` | 轮廓脉冲频率，单位 Hz | 1.2 | `0` 表示不脉冲，保持常亮 |
| `Corpse Outline Pulse Amount` | 脉冲幅度，占基础亮度的比例 | 0.18 | 建议 `0.1–0.25`；过高会产生明显闪烁 |

颜色字段带有 HDR 色彩选择器。常规做法是先选色，再用 `Intensity` 控制最终亮度；不要只靠把颜色调得很白来增强效果。

### 3.3 尸体表面 Rim 参数

这组参数控制身体表面较宽的边缘发光，与外轮廓不是同一个效果：

| Inspector 字段 | 作用 | 默认值 | 调节建议 |
|---|---|---:|---|
| `Corpse Rim Color` | 身体表面边缘光颜色 | 青蓝色 | 通常与 `Corpse Outline Color` 同色或使用相近色 |
| `Corpse Rim Intensity` | 身体表面边缘光亮度 | 1.35 | 只想突出轮廓时保持较低；过高会让整个身体变亮 |
| `Corpse Rim Power` | 边缘光的集中程度 | 2.0 | 数值越低，发光向身体正面包得越宽；数值越高，越集中在轮廓边缘 |
| `Corpse Surface Glow Intensity` | 尸体整体表面附加光 | 0.1 | 建议保持低值，用于让尸体在暗处仍可辨认 |
| `Corpse Pulse Speed` | 身体表面光脉冲频率，单位 Hz | 1.1 | `0` 表示不脉冲 |
| `Corpse Pulse Amount` | 身体表面光脉冲幅度 | 0.12 | 建议保持低于轮廓脉冲，避免身体整体闪烁 |

## 四、其他常用配置

### 4.1 附身高亮

| Inspector 字段 | 作用 | 默认值 |
|---|---|---:|
| `Possession Rim Color` | 玩家附身后身体边缘光颜色 | 紫色 |
| `Possession Rim Intensity` | 玩家附身后边缘光亮度 | 1.8 |

如果需要附身后完全不增加发光，将 `Possession Rim Intensity` 设为 `0`。此时不会为了高亮而替换原始材质。

### 4.2 精英高亮

| Inspector 字段 | 作用 | 默认值 |
|---|---|---:|
| `Elite Rim Color` | 精英边缘光颜色 | 紫色 |
| `Elite Rim Intensity` | 精英边缘光基础亮度 | 0.8 |
| `Elite Pulse Speed` | 精英边缘光脉冲频率，单位 Hz | 1.8 |
| `Elite Pulse Amount` | 精英边缘光脉冲幅度 | 0.25 |
| `Elite Rim Power` | 精英边缘光集中程度 | 3.0 |
| `Elite Metallic` | 备用精英材质的金属度 | 0.2 |

通常只需要调整颜色、亮度和脉冲；`Elite Metallic` 只有在使用精英专用材质路径时才有明显影响。

### 4.3 受击闪白

| Inspector 字段 | 作用 | 默认值 |
|---|---|---:|
| `Hit Flash Color` | 受击闪光颜色 | 淡红白 |
| `Hit Flash Duration` | 闪白持续时间，秒 | 0.12 |
| `Hit Flash Peak` | 闪白峰值 | 0.75 |

推荐先保持 `Duration` 在 `0.08–0.16` 秒内；如果受击感太弱，优先增加 `Peak`，不要把持续时间拉得过长。

### 4.4 尸体消散溶解

| Inspector 字段 | 作用 | 默认值 |
|---|---|---:|
| `Dissolve Edge Color` | 溶解边缘烧灼色 | 紫红色 |
| `Dissolve Edge Intensity` | 溶解边缘亮度 | 4.5 |

这组参数只影响尸体进入消散阶段的溶解边缘，不影响尸体等待附身阶段的轮廓。溶解材质模板属于技术美术配置，普通怪物一般不需要指定。

## 五、材质模板怎么处理

以下三个字段通常保持为空：

- `Corpse Material Template`
- `Elite Material Template`
- `FX Material Template`

留空时，系统使用默认的 `Possession/CharacterFX` 路径，并复制原材质的外观参数，再叠加运行时特效。这样可以避免为了高亮而破坏怪物原本的贴图、颜色和材质表现。

只有在技术美术已经准备好对应 Shader、并确认它支持所需属性时，才需要指定模板：

- 尸体轮廓需要支持 `_OutlineColor`、`_OutlineIntensity`、`_OutlineWidth`；
- 尸体表面光需要支持 `_RimColor`、`_RimIntensity`、`_RimPower`；
- 溶解需要支持 `_DissolveAmount`、`_DissolveEdgeColor`、`_DissolveEdgeIntensity`。

如果指定了不支持这些属性的自定义模板，可能出现“身体有变化但没有外轮廓”或“溶解没有边缘光”的情况。遇到此类问题，先清空模板字段，确认默认效果正常，再交给技术美术检查 Shader。

## 六、推荐的三档调参起点

这些数值是调试起点，不是强制规范：

| 风格 | Outline Intensity | Outline Width | Pulse Speed | Pulse Amount | 适用场景 |
|---|---:|---:|---:|---:|---|
| 克制 | 1.5 | 0.012 | 0 | 0 | 画面已经很亮、只需要状态提示 |
| 标准 | 2.4 | 0.018 | 1.2 | 0.18 | 默认推荐，清晰但不抢主体 |
| 强提示 | 3.2–4.0 | 0.024–0.032 | 1.5 | 0.20–0.30 | 黑暗场景、尸体容易被遮挡 |

颜色建议：尸体可使用青蓝或蓝绿色；精英使用紫色或高饱和暖色；受击使用白色偏红。具体颜色应服从关卡整体可读性和美术风格。

## 七、预览与验收方法

### 在运行时预览

1. 打开包含该怪物的测试场景并进入 Play Mode。
2. 让怪物进入可附身尸体状态，观察外轮廓是否清晰。
3. 依次修改 `Outline Color`、`Width`、`Intensity`，确认轮廓在不同背景上都能识别。
4. 观察尸体被附身、等待时间结束进入消散、以及回收后，轮廓是否正确关闭。

Play Mode 下修改参数时，当前激活的高亮会实时应用；退出 Play Mode 后，仍需把最终参数保存回 Prefab。

### 验收标准

- 尸体在亮背景和暗背景上都能被快速识别；
- 外轮廓不会明显遮挡或改变怪物原始贴图；
- 轮廓厚度不会因轻微镜头移动产生明显跳动或闪烁；
- 轮廓在附身、消散、回池后都会关闭；
- 同一材质被多个怪物共用时，调节单个怪物不会改变其他怪物。

## 八、常见问题排查

### 看不到外轮廓

按以下顺序检查：

1. 怪物是否真的进入“可附身尸体”状态，而不是普通死亡后直接消散；
2. `Corpse Outline Intensity` 是否大于 `0`；
3. `Corpse Outline Width` 是否大于 `0`；
4. 配置是否改在 `MonsterActor` 根对象的 `ActorVisualFx` 上；
5. 是否指定了不支持 `_Outline*` 属性的自定义 `Corpse Material Template`。

### 只有身体表面变亮，没有清晰轮廓

这通常表示 `Corpse Rim` 生效、但轮廓 pass 没有生效。先清空 `Corpse Material Template`，使用默认 `Possession/CharacterFX` 验证；如果默认效果正常，再检查自定义材质的 Shader 属性。

### 轮廓太亮或太刺眼

优先降低 `Corpse Outline Intensity`，其次降低 HDR 颜色亮度；如果只是动态变化太强，降低 `Corpse Outline Pulse Amount`。

### 轮廓太厚或太薄

调整 `Corpse Outline Width`。该参数是世界空间厚度，因此同一数值在不同尺寸的怪物上观感可能不同，应按模型尺寸微调。

### 修改材质后其他怪物也变了

不要直接修改共享材质资产来做尸体高亮。`ActorVisualFx` 的高亮路径会创建运行时材质实例；如果问题仍然存在，应检查是否有其他脚本或美术流程直接改写了共享材质。

## 九、给策划 / 美术的最简操作清单

1. 在怪物 Prefab 根对象找到 `ActorVisualFx`。
2. 只调整 `Possessable Corpse Highlight` 中的尸体参数。
3. 尸体外轮廓优先调整：颜色 → 厚度 → 亮度 → 脉冲。
4. `Corpse Material Template` 默认不要填。
5. 在测试场景中验证亮 / 暗背景、附身、消散和回池流程。
6. 保存 Prefab，并记录特殊怪物的非默认参数，便于后续统一调色。

## 十、实现参考

- `Assets/Scripts/Presentation/Combat/ActorVisualFx.cs`
- `Assets/Scripts/Combat/Actors/MonsterActor.cs`
- `Assets/Shaders/PossessionCharacterFX.shader`
