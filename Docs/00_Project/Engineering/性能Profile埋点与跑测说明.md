# Possession 性能 Profile 埋点与跑测说明

## 目的

本轮先建立可对照的运行时采样点，不根据静态猜测继续改战斗逻辑。所有埋点使用 Unity 内置 `Unity.Profiling.ProfilerMarker`，不新增 Package，不修改 Prefab、Scene、ProjectSettings 或 Packages。

## 已加入的 Marker

| Marker | 关注内容 |
|---|---|
| `Kepler.Actor.SlideMove` | 灵魂/怪物滑动移动与碰撞查询 |
| `Kepler.Combat.MonsterActor.Update` | 怪物每帧控制、按钮和移动入口 |
| `Kepler.Combat.MonsterActor.FixedUpdate` | AI 怪物 FixedUpdate 移动入口 |
| `Kepler.Combat.MonsterActor.LateUpdate` | Animator、血条布局和 Billboard |
| `Kepler.Combat.Projectile.Update` / `CheckHit` | 投射物移动、墙体检测和周期命中查询 |
| `Kepler.Combat.Mine.Update` / `Explode` | 地雷触发扫描与爆炸结算 |
| `Kepler.Combat.VfxPool.Spawn` / `Release` / `StopAndClearParticles` | VFX 池租借、归还和粒子清理 |
| `Kepler.AI.MonsterPathfinder.TryGetMoveDirection` | 怪物寻路入口及直接路线判断 |
| `Kepler.AI.MonsterPathfinder.BuildPath` | A* 开表、节点扩展和回溯 |
| `Kepler.AI.MonsterPathfinder.RefreshHazardCache` | 危险区缓存刷新 |
| `Kepler.AI.BossCombatBrain.Update` / `ChooseAbility` | Boss 决策节拍和候选技能评分 |
| `Kepler.MapStreaming.Update` / `Tick` / `ProcessJobQueue` | A/B/C/D 区块计算与任务队列 |
| `Kepler.MapStreaming.TerrainEffectTile.FixedUpdate` / `ScanOccupants` | 地块轮询和占用者扫描 |

Marker 只包住原有方法，不改变时序、伤害、冷却或查询语义。Profiler 未开启时不会生成业务数组或日志文件；Marker 本身的开销应以最终采样为准。

## 请按以下方式跑一次

1. 使用你平时能稳定复现“几分钟后帧率明显下降”的场景和玩法路径。尽量固定同一个世界种子、画质、分辨率和窗口尺寸。
2. Unity 菜单打开 `Window > Analysis > Profiler`，选择 `CPU Usage`，开启 `Record`。不要开启 Deep Profile，避免改变问题本身的耗时分布。
3. 先正常运行 30 秒作为热身，再继续玩到出现明显低帧。低帧出现后至少保持 10–20 秒，不要立刻切场景或暂停。
4. 记录三个区间：
   - 热身后稳定区间；
   - 低帧前约 10 秒；
   - 低帧持续区间。
5. 在 CPU Usage 的 Timeline 或 Hierarchy 中搜索上表的 `Kepler.` Marker，重点记录每个 Marker 的 `Total`、`Self`、调用次数，以及对应区间的 `GC Alloc`。
6. 同时查看 `Physics`、`Rendering`、`Memory` 模块：确认瓶颈属于脚本、物理、渲染还是内存增长。记录低帧区间的 `Main Thread`、`Scripts`、`Physics`、`GC Alloc/frame` 和 `GC.CollectionCount` 变化。
7. 使用 Profiler 窗口的 Save 保存 `.data` 文件；如果文件不便上传，至少提供三个区间的截图或手抄表格，并注明 Unity Editor / Development Build、平台、分辨率和目标帧率。

## 建议回传格式

```text
场景 / Seed：
运行方式：Editor 或 Development Build
平台 / 分辨率 / 目标帧率：
低帧开始时间（从本次运行开始计）：
稳定区间 FPS / Main Thread / GC Alloc：
低帧前 FPS / Main Thread / GC Alloc：
低帧持续 FPS / Main Thread / GC Alloc：

Marker（低帧持续区间）：
Kepler.MapStreaming.ProcessJobQueue: Total=, Self=, Calls=
Kepler.AI.MonsterPathfinder.BuildPath: Total=, Self=, Calls=
Kepler.Combat.Projectile.CheckHit: Total=, Self=, Calls=
Kepler.Combat.MonsterActor.Update: Total=, Self=, Calls=
其他明显靠前 Marker：
```

## 数据到手后的第二阶段

拿到 `.data` 或区间统计后，我会按以下顺序处理：

1. 先确认主线程瓶颈归属，区分 Script / Physics / Rendering / GC，避免把渲染问题误改成代码问题。
2. 对 Marker 的 `Self` 和调用次数做乘积分析，锁定“单次贵”与“调用过密”两类问题。
3. 对照稳定区间和低帧区间，确认是随怪物、投射物、地块数量增长，还是某个流送/技能事件造成尖峰。
4. 只改有数据支持的热点，并保持固定 Seed、玩法路径和战斗语义不变。
5. 优化后重复同一跑测，给出优化前后 P50/P95/P99 帧时间、GC Alloc、脚本/物理耗时和调用次数对比。
