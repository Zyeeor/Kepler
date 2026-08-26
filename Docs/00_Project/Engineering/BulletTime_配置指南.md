# 子弹时间配置指南

> 面向：策划、程序、技术美术
>
> 配置入口：场景中的 `GameManager` → `BulletTimeController`
>
> 当前基线：持续时间 `2s`，世界时间倍率 `0.2`，附身 Body 获得 `0.5s` 免伤 Effect，后处理使用 `RadialBlur`。

## 一、功能概览

子弹时间是以玩家为中心的时间域：

- 非玩家来源的怪物、怪物技能、投掷物、子弹、召唤物和常规战斗 VFX 跟随 `Time.timeScale`，在子弹时间内减缓；
- 玩家来源的部分战斗逻辑使用 Unscaled Time，避免玩家当前 Body 的操作和玩家来源投射物一起变慢；
- 启动时给当前附身 Body 施加可配置的临时免伤 Effect；
- 启动时切换场景中的 Panda 后处理效果，结束时恢复启动前的后处理状态；
- 持续时间使用实时计时，不会因为世界时间变慢而延长两倍或更多。

设计真源要求子弹时间清楚表达以下信息：局部场边界、启动、结束、非玩家来源变慢、玩家来源保持速度，以及 Current Body 的耐久压力。具体 Shader 和后处理材质属于表现实现，可继续调校。

## 二、当前触发流程

当前仓库实现的主要流程如下：

```text
PossessionManager.CommitPossession
        ↓
PossessionManager.TriggerBulletTime()
        ↓
BulletTimeController.Trigger(CurrentBody)
        ↓
GameManager.SwitchState(BulletTime)
        ↓
TimeScaleManager.Push(BulletTime, timeScale)
        ├─ ApplyDamageImmunity(CurrentBody)
        └─ ActivatePostProcess()
```

附身成功提交后会自动调用 `TriggerBulletTime()`。`PossessionManager.TriggerBulletTime()` 仍是公开入口，调试或后续输入层可以直接调用它。

Canonical 将 `Q` 定义为子弹时间的设计输入，但当前仓库脚本中未接入独立的 `Q` 输入绑定。若改为手动触发，应由输入层调用 `PossessionManager.TriggerBulletTime()`，不要在输入层直接修改 `Time.timeScale`。

结束时，`BulletTimeController` 会：

1. 使用 `WaitForSecondsRealtime` 等待配置时长；
2. 恢复此前激活的后处理效果；
3. 将 `GameManager` 状态恢复为 `Possessed`；
4. 通过 `TimeScaleManager` 移除 `BulletTime` 时间请求。

主动离身、换身、当前 Body 死亡、Game Over 或系统重置时，`PossessionManager` 会提前停止子弹时间并清理时间请求。

## 三、配置入口

### 3.1 场景配置

以 `Assets/Scenes/EnemyAiTest.unity` 为例：

1. 选中场景中的 `GameManager`。
2. 找到 `BulletTimeController` 组件。
3. 修改配置后保存场景。
4. 确认 `postProcessSwitcher` 指向带有 `PandaPostProcessSwitcher` 的 `Global Volume`。

当前场景已经完成以下接线：

| 项目 | 当前值 |
|---|---|
| `duration` | `2` 秒 |
| `timeScale` | `0.2` |
| `damageImmunityEffect` | 空，按 Tag 自动解析 |
| `effectCatalog` | 空，优先回退到 `CardManager` |
| `damageImmunityEffectTag` | `Effect.Defense.DamageImmune` |
| `damageImmunityDuration` | `0.5` 秒 |
| `postProcessEffectName` | `RadialBlur` |
| `postProcessSwitcher` | `Global Volume` 上的 `PandaPostProcessSwitcher` |
| `overridePostProcessParams` | 关闭 |

`GameManager.Awake()` 仍会调用 `BulletTimeController.EnsureInstance()`。因此，没有预置控制器的场景也会创建运行时实例，但该实例只能使用脚本默认值，无法直接在 Inspector 中配置。正式战斗场景建议预置一个 `BulletTimeController`。

### 3.2 Bullet Time 字段

| Inspector 字段 | 默认值 | 作用与建议 |
|---|---:|---|
| `Duration` | `2` | 子弹时间持续秒数。使用实时计时；建议先在 `1.5–3` 秒内调节。 |
| `Time Scale` | `0.2` | 非玩家世界时间倍率。`0.05–1` 范围内有效；越小减速越明显。 |
| `Damage Immunity Effect` | 空 | 直接指定一个 `GameplayEffectDefinition`。指定后优先使用该资产。 |
| `Effect Catalog` | 空 | 指定 `GameplayTagCatalog`，按 Effect Tag 查找免伤 Effect。 |
| `Damage Immunity Effect Tag` | `Effect.Defense.DamageImmune` | 没有直接资产时的查找 Tag。必须与目录 / `CardManager` 注册值一致。 |
| `Damage Immunity Duration` | `0.5` | 当前附身 Body 的免伤持续时间。设为 `0` 会关闭本次 Apply。 |

### 3.3 Post Process 字段

| Inspector 字段 | 默认值 | 作用与建议 |
|---|---:|---|
| `Post Process Effect Name` | `RadialBlur` | 按 Panda 效果组件名或材质名查找目标效果。 |
| `Post Process Switcher` | 可空 | 推荐显式引用场景中的 `PandaPostProcessSwitcher`；为空时自动查找。 |
| `Override Post Process Params` | 关闭 | 关闭时使用场景中该 Panda 效果自身的参数；开启时临时覆盖，结束自动还原。 |
| `Post Step Factor` | `0.5` | 开启 Override 后生效。 |
| `Post Main Alpha` | `1` | 后处理整体透明度。 |
| `Post Blur Factor` | `0.47` | 径向模糊强度。 |
| `Post Line UV Scale` | `2.24` | 画面径向拉伸尺度。 |
| `Post Chromatic` | `0.18` | 色散强度。 |
| `Post Frequency` / `Post Amplitude` | `0` | 后处理振频 / 振幅。 |
| `Post Vignette Power` / `Post Vignette Scale` | `1` / `0` | 黑边形状与强度。 |

推荐优先调整场景中的 `RadialBlur` 组件。如果只想让子弹时间切换现有效果，不需要覆盖参数，保持 `Override Post Process Params` 关闭。

## 四、后处理接线规则

`BulletTimeController` 使用 `PandaPostProcessSwitcher` 管理效果，不直接操作材质共享资产。

### 4.1 添加或替换效果

1. 选中场景中的 `Global Volume`。
2. 确认存在 `PandaPostProcessSwitcher`。
3. 在同一对象上添加或配置 `PandaPostProcess`。
4. 给 `PandaPostProcess` 指定材质，例如 `RadialBlur.mat`。
5. 在 `BulletTimeController.Post Process Effect Name` 中填写组件名或材质名。

运行时启动子弹时间时，控制器会刷新 Switcher 效果列表，找到名称匹配的条目并激活；结束时恢复启动前的 `ActiveEffectIndex`。

### 4.2 常见问题

- **没有视觉效果**：检查 `postProcessEffectName` 是否与材质名完全匹配，检查 `postProcessSwitcher` 是否指向正确的 `Global Volume`。
- **Console 提示效果未找到**：说明当前场景的 Switcher 上没有同名 `PandaPostProcess`，或者材质没有被接线到 Switcher 对象。
- **结束后后处理没有恢复**：检查场景是否存在多个 Switcher；同一战斗相机建议只保留一个负责这组效果的 Switcher。
- **参数改了但没有变化**：如果 `overridePostProcessParams` 关闭，控制器不会使用下面一排覆盖参数，应直接修改场景中的 `PandaPostProcess` 参数。

## 五、免伤 Effect 配置

### 5.1 Effect 解析优先级

启动子弹时间时，控制器按以下顺序查找免伤 Effect：

1. `damageImmunityEffect` 直接引用的资产；
2. `effectCatalog` 中以 `damageImmunityEffectTag` 注册的 Effect；
3. `CardManager` 使用同一个 Tag 提供的 Effect；
4. 上述来源都找不到时，运行时创建一个临时回退 Effect。

回退 Effect 的名称为 `Bullet Time Damage Immunity`，Effect Tag 为 `Effect.Defense.DamageImmune`，并授予 `State.Defense.DamageImmune`。因此当前场景不填写资产也能工作，但正式项目建议使用明确注册的 Effect 资产，便于策划统一管理和后续扩展。

### 5.2 免伤行为

- Effect 只施加给当前附身 Body，不施加给全场 Enemy 或 Soul；
- 默认持续 `0.5` 秒，与子弹时间持续时间独立；
- 如果目标已经有 `State.Defense.DamageImmune`，不会重复施加；
- Apply 使用 `durationOverride`，因此 `BulletTimeController.damageImmunityDuration` 会覆盖 Effect 资产自身的默认时长；
- 免伤结束后，后续伤害按正常 Combat 规则结算。

要更换免伤逻辑，优先新建或调整 `GameplayEffectDefinition`，再将资产拖入 `Damage Immunity Effect`。不要在技能脚本中另写一套布尔免伤状态。

## 六、时间域与来源规则

### 6.1 TimeScaleManager

所有全局时间缩放应通过 `TimeScaleManager`，不要直接写 `Time.timeScale`。

当前时间域优先级如下：

| 时间域 | 优先级 | 说明 |
|---|---:|---|
| `BulletTime` | `10` | 子弹时间，默认倍率由 `BulletTimeController.TimeScale` 提供。 |
| `HitStop` | `20` | 命中顿帧，临时压过子弹时间。 |
| `Pause` | `30` | 选卡 / 暂停。 |
| `GameOver` | `40` | 游戏结束冻结。 |
| `DebugCamera` | `50` | 调试相机冻结。 |

高优先级时间域覆盖低优先级时间域；一个时间域重复 Push 时使用引用计数，必须通过对应的 Pop 释放。

### 6.2 Player-Origin 与 Non-Player-Origin

新接入的投射物或 VFX 必须先明确来源：

- **Player-Origin**：玩家 Soul 或当前玩家控制 Body 发出的输出；
- **Non-Player-Origin**：AI Enemy、未被玩家控制的怪物、世界机关等发出的输出。

当前代码中的主要接入点：

| 输出类型 | 来源处理 |
|---|---|
| `Projectile` | `isPlayerProjectile = true` 使用 `Time.unscaledDeltaTime`，怪物投射物使用 `Time.deltaTime`。 |
| `HookProjectile` | 通过 `useUnscaledTime` 区分玩家来源与怪物来源。 |
| `GreedHandProjectile` | 按发射者是否为玩家控制 Body 选择 `SimulationTime` / `SimulationDeltaTime`。 |
| `SummonActor` | 按召唤者是否为玩家控制者选择 `Time.unscaledTime` / `Time.time`。 |
| 通用技能 VFX | `EnemyAbility` / `PlayerAbility` 生成时调用 `BulletTimeController.MarkVfxOrigin`。 |
| 池化 VFX 延迟回收 | `VfxPool` 根据 `BulletTimeVfxPlayback.IsPlayerOrigin` 选择 scaled 或 unscaled delta。 |

### 6.3 新增投射物 / VFX 的接入模板

#### 投射物

```csharp
projectile.isPlayerProjectile = owner != null && owner.IsPlayerControlled;
projectile.ResetForPoolSpawn();
```

投射物的移动、生命周期和命中检查必须使用与 `Projectile` 相同的来源时间域；不要在自定义 `Update` 中无条件使用 `Time.unscaledDeltaTime`。

#### 池化 VFX

```csharp
GameObject vfx = VfxPool.Instance.Spawn(prefab, position, rotation);
BulletTimeController.MarkVfxOrigin(vfx, playerOrigin);
VfxPool.ReleaseOrDestroy(vfx, duration);
```

怪物来源应传入 `false`，这样延迟回收和挂在 VFX 上的自定义逻辑会随子弹时间减缓。玩家来源传入 `true`。

### 6.4 粒子系统注意事项

`TimeScaleManager` 会让使用默认 Simulation Time 的 `ParticleSystem` 随世界时间变慢。若某个 Player-Origin 粒子必须保持真实速度，需要在该粒子系统的 Main 模块启用 Unscaled Time；不要只依赖 `MarkVfxOrigin`，因为这个标记主要用于自定义逻辑和池化回收时间。

当前仓库仍存在少量旧的 `Object.Instantiate` / `Instantiate` 直连 VFX 路径。它们不会自动获得 `BulletTimeVfxPlayback` 标记；新增或修改这些路径时，应改为池化 VFX，或显式补充来源时间处理。

## 七、当前已知边界

以下内容是当前实现事实，不应在配置文档中误认为已经完成：

1. 当前子弹时间是全局 `Time.timeScale` 时间域，不是按半径计算的局部物理场；Canonical 中的“半径”仍属于可调设计项。
2. 当前 `BulletTimeController` 没有独立的冷却字段；重复触发会先停止当前实例，再重新开始。
3. `PossessionManager` 的附身 Body 被动耐久流逝使用 `Time.unscaledDeltaTime`，因此不会因为 `timeScale = 0.2` 自动变慢；若要实现 Canonical 所说的额外耐久压力，应新增明确的可配置倍率，不要直接改写时间域。
4. 场景中的 `BulletTimeController` 配置优先于脚本默认值；场景没有预置组件时，运行时回退实例无法自动获得场景中的后处理引用和策划调参。
5. 后处理属于表现反馈，不能替代 Gameplay 状态、伤害免疫或时间域本身。

## 八、验证清单

在 `EnemyAiTest.unity` 或等价战斗场景中进入 Play Mode，建议按以下顺序验收：

### 启动与配置

- [ ] 成功附身后，`GameManager.currentState` 进入 `BulletTime`。
- [ ] `Time.timeScale` 变为 `BulletTimeController.timeScale`，默认是 `0.2`。
- [ ] 当前附身 Body 立即获得 `State.Defense.DamageImmune`，持续默认 `0.5` 秒。
- [ ] `Global Volume` 切换到 `RadialBlur`，画面能感知子弹时间开始。

### 时间域

- [ ] 已经发射的怪物投掷物 / 子弹移动速度明显减缓。
- [ ] 怪物召唤物的移动、攻击间隔和生命周期按 scaled time 推进。
- [ ] 怪物来源的池化 VFX 播放和延迟回收按 scaled time 推进。
- [ ] 玩家来源的投射物与玩家 Body 自定义逻辑不因 BulletTime 倍率一起减速。
- [ ] Hit Stop / Pause 等高优先级时间域结束后，BulletTime 请求仍能正确恢复或释放。

### 结束与清理

- [ ] 默认约 2 秒实时后，状态恢复为 `Possessed`。
- [ ] 后处理恢复到启动前的效果，不残留 `RadialBlur`。
- [ ] `Time.timeScale` 恢复为进入子弹时间前的有效值；没有其他时间域时应为 `1`。
- [ ] 主动离身、换身、Body 死亡和 Game Over 不会留下 BulletTime 时间请求。
- [ ] 免伤结束后，下一次伤害能够正常扣除 Body 耐久。

## 九、相关文件

| 内容 | 路径 |
|---|---|
| 子弹时间统一配置与运行时控制 | `Assets/Scripts/Combat/Possession/BulletTimeController.cs` |
| 附身触发 / 停止入口 | `Assets/Scripts/Combat/Possession/PossessionManager.cs` |
| 游戏状态与时间域切换 | `Assets/Scripts/Systems/GameFlow/GameManager.cs` |
| 全局时间域管理 | `Assets/Scripts/Systems/GameFlow/TimeScaleManager.cs` |
| 池化 VFX 与延迟回收 | `Assets/Scripts/Combat/Runtime/VfxPool.cs` |
| 普通投射物时间处理 | `Assets/Scripts/Combat/Projectiles/Projectile.cs` |
| Panda 后处理切换器 | `Assets/Art folder/VFX/Plugins/PandaPostProcessSwitcher.cs` |
| 示例场景配置 | `Assets/Scenes/EnemyAiTest.unity` |
| Gameplay Effect 实现 | `Assets/Scripts/Combat/Abilities/CombatAbilityComponent.cs` |

文档版本：2026-08-26。数值和具体表现以场景 Inspector、Effect 资产和当前代码为准；Playable 调参不自动升级为设计真源。
