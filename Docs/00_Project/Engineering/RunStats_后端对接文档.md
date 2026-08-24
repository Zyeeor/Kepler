# 整局运行数据（Run Analytics）后端对接文档

> 版本：v1.0 | 日期：2026-08-21
> 客户端实现：`Assets/Scripts/Save/RunStatsData.cs` / `RunStatsStore.cs` / `RunStatsCollector.cs`
> 设计真源：`.vibe/doc/Canonical/Content/Narrative_Voice_Delivery_Baseline_v1.0.md` §8（First Clear Runtime Data）、`.vibe/doc/Canonical/01_DESIGN_CANONICAL.md` §28（Final/Result）
> 关联后端：`Server/`（Go，现有 UGC + 精英 BD 快照接口，见 `Server/docs/api-guide.md`）

---

## 1. 背景与目标

客户端每局游戏（Run）结束时生成一份**整局运行数据（Run Analytics）**，当前已实现**本地 JSON 落盘**；后续将上传到后端用于分析（对局统计、行为画像、First Clear 评分输入等）。

本文件供后端了解：
- 数据结构与字段语义；
- 本地存储格式（= 将来上传的请求体格式）；
- 客户端预留的上传出口与建议的接口设计。

> 注意：当前后端**尚未**实现对局记录接口。本文档第 4 节是建议方案，具体协议以后端评审为准。

---

## 2. 数据模型

### 2.1 总览

```
RunStatsData
├── 身份 / 时间
│   ├── schemaVersion      数据模型版本（当前 1）
│   ├── runId              对局 ID（每局唯一，客户端生成，如 "run-1f3c..."）
│   ├── playerId           设备匿名 ID（同设备 = 一个玩家）
│   ├── startedAtUnix      对局开始 Unix 秒（UTC）
│   ├── endedAtUnix        对局结束 Unix 秒（UTC）
│   └── runDurationSeconds Run 总时长（秒）
├── 结果
│   ├── won                是否胜利
│   ├── endPhase           结束阶段（Result / Failed / Aborted）
│   ├── reachedWaveIndex   到达的最远波次（0-based，-1=未完成任何波）
│   ├── finalReached       是否到达 Final
│   └── finalCompleted     是否完成 Final
├── Global 计数
│   ├── totalPossessions   总附身次数
│   ├── voluntaryReleases  主动离身次数
│   ├── deathRelays        死亡接力次数（M3 玩法预留，恒 0）
│   ├── soulEnters         灵魂进入自由形态次数
│   ├── shrineRecovers     神龛恢复次数（M3 玩法预留，恒 0）
│   ├── lowHealthReleases  低耐久主动离身次数
│   ├── bulletTimeCount    子弹时间触发次数
│   ├── bulletTimeTotalSeconds 子弹时间总时长（秒）
│   ├── eliteFatalCount    精英怪击杀数
│   ├── elitePossessionCount 精英怪附身数
│   ├── distinctSinsUsed   使用过的不同 Sin 数量
│   └── totalKills         总击杀数（怪物进入尸体态）
├── Per-Sin 数组（perSin）
│   └── PerSinStats { sin, controlSeconds, possessionCount,
│                     movementCount, attackCount, specialCount,
│                     cardInvestmentCount, kills }
└── 上传状态（客户端本地用）
    ├── uploadStatus       0=未上传 1=上传中 2=已上传 -1=失败待重试
    └── lastUploadAttemptUnix 上次上传尝试时间
```

### 2.2 Per-Sin 明细

| 字段 | 类型 | 语义 |
|---|---|---|
| `sin` | 枚举（int） | Sin 类型：None=0 / Pride=1 / Sloth=2 / Gluttony=3 / Envy=4 / Wrath=5 / Greed=6 / Lust=7；**传输时建议用 wire 名**（小写枚举名：`pride` / `sloth` / `gluttony` / `envy` / `wrath` / `greed` / `lust`） |
| `controlSeconds` | float | 玩家控制该 Sin 身体的总时长（秒） |
| `possessionCount` | int | 附身该 Sin 身体的次数 |
| `movementCount` | int | 玩家控制期间使用该 Sin 身体"移动类"能力的次数 |
| `attackCount` | int | 玩家控制期间使用"攻击类"能力的次数 |
| `specialCount` | int | 玩家控制期间使用"技能类"能力的次数 |
| `cardInvestmentCount` | int | 该 Sin 的卡牌投资数量（取得的 MonsterType / TypeGrowth 卡数） |
| `kills` | int | 该 Sin 身体（玩家控制期间）造成的击杀数 |

### 2.3 关键语义约定

1. **只记原始值，不做评分**：主/次倾向、行为描述由后端（或配置化评分器）基于原始数据计算（Canonical §28：精确权重、阈值、句式由配置和 Playable 收口）。
2. **`sin` 传输格式**：建议用 wire 名（与现有精英 BD 快照 `sin` 字段一致，见 `Server/docs/api-guide.md` §4.1），避免枚举值漂移。
3. **`reachedWaveIndex`**：0-based 已开始的最远波次；`-1` = 一局未完成任何波（如开场即退）。
4. **`endPhase`**：`Result`（胜利，`won=true`）、`Failed`（失败，`won=false`）、`Aborted`（中途退出/重开兜底，`won=false`）。
5. **`deathRelays` / `shrineRecovers`**：依赖 M3 玩法（Death Relay / Soul Shrine），当前恒 0，字段已预留。
6. **击杀归属**：`totalKills` 是全局击杀；`perSin[].kills` 只计"玩家附身该 Sin 身体期间"击杀的其它怪。

---

## 3. 本地存储格式

- 目录：`<persistentDataPath>/run_stats/`
- 文件：`run_<runId>.json`（每局一个，同 runId 覆盖重写，天然幂等）
- 格式：JSON（pretty），字段 camelCase

### 3.1 本地文件示例

```json
{
  "schemaVersion": 1,
  "runId": "run-1f3c9a2b7e8d4f6a",
  "playerId": "device-9f2c1d0e8a7b4f6d",
  "startedAtUnix": 1755456000,
  "endedAtUnix": 1755457020,
  "runDurationSeconds": 1020.5,
  "won": true,
  "endPhase": "Result",
  "reachedWaveIndex": 7,
  "finalReached": true,
  "finalCompleted": true,
  "totalPossessions": 12,
  "voluntaryReleases": 4,
  "deathRelays": 0,
  "soulEnters": 5,
  "shrineRecovers": 0,
  "lowHealthReleases": 1,
  "bulletTimeCount": 3,
  "bulletTimeTotalSeconds": 6.0,
  "eliteFatalCount": 2,
  "elitePossessionCount": 1,
  "distinctSinsUsed": 4,
  "totalKills": 38,
  "perSin": [
    {
      "sin": 3,
      "controlSeconds": 320.0,
      "possessionCount": 4,
      "movementCount": 12,
      "attackCount": 45,
      "specialCount": 8,
      "cardInvestmentCount": 3,
      "kills": 15
    },
    {
      "sin": 1,
      "controlSeconds": 240.5,
      "possessionCount": 3,
      "movementCount": 9,
      "attackCount": 30,
      "specialCount": 6,
      "cardInvestmentCount": 2,
      "kills": 12
    }
  ],
  "uploadStatus": 0,
  "lastUploadAttemptUnix": 0
}
```

> 说明：`sin` 字段在本地 JSON 中为枚举整数值（JsonUtility 序列化限制）；**上传后端时客户端将转换为 wire 名**（见 §2.3-2），后端无需解析整数。

---

## 4. 建议的后端接口（待评审）

### 4.1 上传对局数据 `POST /api/runs`

**何时调用**：客户端每局结束（Result / Failed / Aborted）后调用一次。

请求体（camelCase，与本地文件一致，`sin` 用 wire 名）：

```json
{
  "schemaVersion": 1,
  "runId": "run-1f3c9a2b7e8d4f6a",
  "playerId": "device-9f2c1d0e8a7b4f6d",
  "startedAtUnix": 1755456000,
  "endedAtUnix": 1755457020,
  "runDurationSeconds": 1020.5,
  "won": true,
  "endPhase": "Result",
  "reachedWaveIndex": 7,
  "finalReached": true,
  "finalCompleted": true,
  "totalPossessions": 12,
  "voluntaryReleases": 4,
  "deathRelays": 0,
  "soulEnters": 5,
  "shrineRecovers": 0,
  "lowHealthReleases": 1,
  "bulletTimeCount": 3,
  "bulletTimeTotalSeconds": 6.0,
  "eliteFatalCount": 2,
  "elitePossessionCount": 1,
  "distinctSinsUsed": 4,
  "totalKills": 38,
  "perSin": [
    { "sin": "gluttony", "controlSeconds": 320.0, "possessionCount": 4,
      "movementCount": 12, "attackCount": 45, "specialCount": 8,
      "cardInvestmentCount": 3, "kills": 15 }
  ]
}
```

响应 `200`：`{ "ok": true }`

### 4.2 建议约束

| 项 | 建议 |
|---|---|
| 幂等 | `runId` 唯一（upsert），客户端重试不产生重复数据 |
| 鉴权 | 与现有接口一致：无账号体系，`playerId` 为设备匿名 ID，服务器不校验 |
| 存储 | 建议独立表（如 `run_stats`），与 `monster_build_snapshots` 分开（不同生命周期，不互相淘汰） |
| 失败语义 | 客户端断网/失败时保留本地文件（`uploadStatus=-1`），下次启动/手动重试补传；服务器重复接收同 `runId` 应幂等覆盖 |
| 版本 | `schemaVersion` 字段预留；后端遇到未知版本建议按字段缺失容忍或返回 4xx 由客户端迁移 |

---

## 5. 客户端实现状态与对接出口

| 项 | 状态 |
|---|---|
| 数据采集（Per-Sin + Global） | ✅ 已实现（`RunStatsCollector` 订阅附身/波次/选卡/子弹时间/击杀/精英事件） |
| 本地落盘（每局一个 JSON） | ✅ 已实现（`RunStatsStore.SaveRunStats`） |
| 上传出口 | 🔲 预留空实现（`RunStatsStore.UploadRunData`）——**待后端接口确定后在此实现**，成功/失败更新 `uploadStatus` / `lastUploadAttemptUnix` |
| 重试机制 | 🔲 未实现（依赖上传接口确定后补：启动时扫描 `uploadStatus != 2` 的文件补传） |

---

## 6. 与现有精英 BD 快照的区别

| 维度 | 本数据（Run Analytics） | 精英 BD 快照（`/api/bd-snapshots`） |
|---|---|---|
| 粒度 | 整局一份（所有 Sin 汇总） | 每局每 Sin 一条（仅 bdCount≥1） |
| 用途 | 对局统计 / First Clear 评分 / 行为分析 | 精英怪投放候选库（筛选 bdCount / sourceWave） |
| 上传时机 | 局末一次 | 每波选卡后滚动 upsert + Final |
| 生命周期 | 长期保留（建议独立存储） | FIFO 候选库（有容量淘汰） |
| 记录内容 | 原始行为统计（时长/次数/击杀） | 构筑（Card ID 清单 + 波次） |
