# 系统设计文档 — Possession 内容服务器

> 版本：1.0
> 日期：2026-08-19
> 模块：`Server/`（Go 实现）
> 设计依据：`精英怪筛选-他人BD怪物投放-策划案.md`（2026-08-19 四轮对齐完毕）、`.vibe/doc/Canonical/01_DESIGN_CANONICAL.md` §16/§17/§23、`Content/Encounter_CardOffer_Baseline_v1.0.md` §7

---

## 1. 系统定位

《Possession》是**单机游戏**。本服务器只承担两项在线职责：

| 职责 | 说明 |
|---|---|
| **UGC 内容平台** | 地图 / 怪物模版的上传、下载、列表、搜索、订阅、评分 |
| **精英怪 BD 快照投放** | 拉取**其他玩家 BD（构筑）过的怪物**作为精英怪投放候选，靠波次差保证越级难度 |

游戏其余部分（战斗、存档、UGC 浏览之外的玩法）不依赖本服务。原联机对局功能（匹配 / 房间 / WS / KCP 战斗同步 / 服务器权威怪物 / 登录 / 战绩 / 排行榜）已按 Canonical（`PC 单机`）移除；"仅精英怪数据来源依赖网络"为 2026-08-19 Owner 拍板（策划案 §8.3）。

**核心承诺（精英怪系统）**：

1. 玩家遇到的精英怪**必然是其他玩家真正 BD 过的怪物**（`bdCount ≥ MIN_BD` 且非本人快照）；
2. 优先投放 **BD 更深**的怪物（`bdCount` 主排序键）；
3. 靠**波次差**制造越级压力（`sourceWave ≥ N + WAVE_GAP`）。

---

## 2. 技术选型与运行要求

- **语言**：Go 1.22+（`net/http` 方法级路由模式匹配需要）
- **协议**：纯 HTTP + JSON（UTF-8），无 WS / KCP / Protobuf / GraphQL
- **存储**：SQLite（`modernc.org/sqlite` 纯 Go 驱动，无 CGO，交叉编译友好）
- **鉴权**：无账号体系、无 token。身份靠客户端本地持久化的匿名 ID（UGC 侧 `creatorId`/`playerId`；精英怪侧为设备特征码 `sourcePlayerId`，同设备 = 一个玩家）

```bash
go build -o server.exe .
./server.exe                 # 默认 HTTP :8080，数据落 data/
```

---

## 3. 总体架构

三层架构 + 依赖注入，业务与存储解耦：

```text
main.go                     入口：命令行参数 → 装配 → 启动
  └── internal/server       装配层：HTTP 路由 + handler + JSON 编解码
        ├── internal/service   业务层（无 HTTP 依赖）
        │     ├── content.go     ContentService  UGC 全生命周期
        │     └── elite.go       EliteService    快照上传 + 筛选投放引擎
        └── internal/store     存储层（接口抽象，业务零改动可换 MySQL）
              ├── store.go       Store 接口 + creations/subscriptions/reviews 表
              └── elite.go       EliteStore 接口 + monster_build_snapshots 表
```

依赖方向：`server → service → store`。`SQLiteStore` 同时实现 `Store` 与 `EliteStore` 两接口，单实例注入两个业务服务；建表迁移集中在 `migrate()`（UGC 三表 + 快照表 + 索引）。

```text
test/ugctest/               UGC 端到端测试（需先启动服务器）
test/elitepicktest/         精英怪投放端到端测试（自包含：进程内起独立实例 + 临时库）
data/                       运行时数据：game.db + ugc/{creationId}/<file>
```

---

## 4. 模块设计

### 4.1 server（装配层）

- `Config`：`HTTPAddr` / `DBPath` / `UploadDir` / `Elite`（精英怪参数，零值时取默认）。
- 8 条路由：UGC 六条 + 精英怪两条（见接口文档）。
- 统一 JSON 约定：对外字段 camelCase；二进制字段（UGC `fileData`/`thumbnail`）走 base64；错误体 `{"code": <http状态码>, "msg": "<原因>"}`。

### 4.2 ContentService（UGC 业务层）

- **Upload**：生成 creationId → 文件落盘（`uploadDir/{creationId}/`，文件名剥目录成分防路径穿越）→ 元数据入库（status=published）。
- **Download**：查元数据 → 读文件 → 下载数 +1。
- **List / Search**：分页、类型过滤、排序白名单（`downloads|rating|created_at`，防 SQL 注入）、关键词 LIKE 搜索。
- **Subscribe / Rate**：订阅关系唯一约束；评分 upsert 覆盖 + 自动重算平均分。

### 4.3 EliteService（精英怪业务层）——见 §6

---

## 5. 数据模型

### 5.1 UGC 三表（`creations` / `subscriptions` / `creation_reviews`）

| 表 | 关键约束 | 用途 |
|---|---|---|
| `creations` | `id` PK | UGC 元数据：type（map/monster/template 白名单）、tags(JSON)、downloads、rating、version… |
| `subscriptions` | `UNIQUE(player_id, creation_id)` | 订阅关系 |
| `creation_reviews` | `UNIQUE(creation_id, player_id)` | 评分评论，1–5 星覆盖更新，均值回写 creations |

### 5.2 `monster_build_snapshots`（精英怪候选库）

| 列 | 说明 |
|---|---|
| `id` | 自增 PK，FIFO 淘汰依据（插入序） |
| `player_id` | 来源玩家（设备特征码，透传）——玩家隔离键 |
| `run_id` | 来源局 ID（每局一个，透传） |
| `sin` | Sin 类型标识（透传） |
| `monster_type` | 怪物种类（透传，注入端还原用） |
| `bd_data` | BD 数据 JSON 原文（卡 ID + 层数，**结构由前台定义，后台不解析**） |
| `bd_count` | BD 数量（BD 深度；客户端按 bdData 计算，**服务器不解析 bd_data**） |
| `source_wave` | 该 BD 所属波次（透传数值；语义与 Final 编码由前台决定，后台只做数值比较） |
| `game_time` | 游戏时间（透传，名人堂统计预留，筛选不读取） |
| `stats` | 可选统计字段 JSON（透传，筛选不读取） |

约束与索引：

- `UNIQUE(player_id, run_id, sin)` —— 滚动 upsert 唯一键（§6.2）；
- `idx_snapshots_pick(bd_count, source_wave)` —— 筛选主查询覆盖；
- `idx_snapshots_player(player_id)` —— 每玩家容量治理。

**透传存储原则**（策划案 §8.4/§8.6/F3）：`bdData` schema、`sourceWave` 语义（快照拍摄时刻 vs 最后投资波次）、Final 是否上传及其编码（如 =9）均由前台定义；后台只存储不解释。筛选只依赖 `bdCount` 与 `sourceWave` 两个数值。

---

## 6. 精英怪投放引擎（核心设计）

### 6.1 快照生命周期

```text
[每波选卡后] 客户端组装本局所有 bdCount>=1 的 Sin 快照
    → POST /api/bd-snapshots（批量滚动上传）
    → upsert（同 playerId+runId+sin 后波覆盖前波）→ 容量治理
    → 候选库
[他人第 N 波] POST /api/elite/pick {playerId, wave}
    → 四步筛选 → TOP_BAND 加权随机取 1（或三级兜底）
    → 返回一条「确为他人在更高波次 BD 过」的快照 → 客户端注入为精英怪
```

### 6.2 上传与 upsert（策划案 §8.1）

- 每波 Card 选择完成后上传**当前所有 `bdCount ≥ 1` 的 Sin 类型**（0 投资不上行，等效"局末清除未 BD 怪物"，服务器免清理）；
- 事务内批量 `INSERT … ON CONFLICT(player_id, run_id, sin) DO UPDATE`：库中始终保留该局该 Sin 的**最深版本**；
- 单机崩溃 / Alt+F4 / 中途 Fail 无需补传——上一波上传已在库；
- 服务端防御：`sin`/`monsterType` 空、`bdCount < 1`、`bdData` 空或 `null` 的条目静默跳过，不污染候选库。

### 6.3 四步筛选（策划案 §3）

玩家第 N 波请求时依次执行：

| Step | 规则 | 保障 |
|---|---|---|
| 1 | `bdCount >= MIN_BD` | 必然是 BD 过的怪（bdCount=0 进不了候选） |
| 2 | `sourceWave >= N + WAVE_GAP` | 波次差越级难度 |
| 3 | `sourcePlayerId != 请求者` | 必然是其他玩家的怪 |
| 4 | `bdCount` 降序 → TOP_BAND 高档内加权随机取 1 | BD 更深优先 + 多样性 |

Step 1–3 由单条 SQL 完成（`PickCandidates`，`LIMIT 1000` Demo 规模保护）；Step 4 在服务层完成。

### 6.4 TOP_BAND 双模式加权随机（§3 Step 4 / §8.4）

高档内以 `bdCount` 为权重随机，避免每局精英怪永远是同一只 BD 最满的怪；候选很少（band ≤ 1 或总权重 ≤ 0）时直接取最高。模式可配置：

| 模式 | band 大小 | 参数 |
|---|---|---|
| `percent`（默认） | `ceil(n × TopBandPercent)` | `-topBandPercent 0.2`（前 20%） |
| `topk` | `min(n, TopBandTopK)` | `-topBandTopK 5`（前 5 条） |

### 6.5 三级兜底（策划案 §5）

主路径（Step 2 用 `N + WAVE_GAP`）候选为空时，按优先级回退：

1. **放宽 WAVE_GAP 至 0**（允许同波次 BD 怪，仍要求他人 + bdCount ≥ MIN_BD）；
2. 仍空 → **全库 `sourceWave` 最高档**（排除请求者）中 `bdCount` 最大的一条；
3. 仍空 → **本波不投放**，返回 `snapshot: null`（正常业务分支，客户端回退普通波次）。

设计约束：兜底只放宽**波次维度**；Step 1（BD 门槛）与 Step 3（玩家隔离）在所有兜底层级保持——否则破坏 §1 核心承诺 1。兜底 1/2 命中时响应带 `relaxed: true`（仅观测，前台无需处理）。

### 6.6 容量治理（策划案 §8.2/§8.4）

上传事务提交后执行，两级并行生效：

1. **每玩家上限**（`-maxSnapshotsPerPlayer`）：该玩家按插入序 FIFO 淘汰最早快照；
2. **全局上限**（`-maxSnapshots`）：全库按插入序 FIFO 淘汰最早快照。

特性：

- **多局独立保留**：不同 runId 互不覆盖（upsert 唯一键含 runId）；
- **upsert 覆盖不占新额度**：更新已有条目只刷新内容，不新增行；
- FIFO 依据 `id`（插入序）：老快照受 Card 改版影响，价值随时间衰减，FIFO 即自然淘汰（§8.5 拍板：不做快照版本化）。

---

## 7. 参数配置（TUNABLE）

| 参数 | 默认值 | 对应策划案 | 说明 |
|---|---|---|---|
| `-addr` | `:8080` | — | HTTP 监听地址 |
| `-db` | `data/game.db` | — | SQLite 路径 |
| `-upload` | `data/ugc` | — | UGC 文件目录 |
| `-minBd` | `1` | §6 MIN_BD | 最低 BD 数量门槛（体验偏严可设 2） |
| `-waveGap` | `1` | §6 WAVE_GAP | 波次差（1 = 第 5 波刷别人第 6 波 BD 出的怪） |
| `-topBandMode` | `percent` | §8.4 | TOP_BAND 模式：`percent` / `topk` |
| `-topBandPercent` | `0.2` | §6 TOP_BAND | percent 模式高分档比例 |
| `-topBandTopK` | `5` | §6 TOP_BAND | topk 模式高分档条数 |
| `-maxSnapshots` | `10000` | §8.2 | 候选库全局上限（FIFO） |
| `-maxSnapshotsPerPlayer` | `100` | §8.4 | 每玩家快照上限 |

数值均为首版 Baseline，需 Playable 验证后调整，不代表数值冻结。服务层对非法配置做归一化钳制（如 `MinBD < 1 → 1`，`TopBandMode` 非法值回落 `percent`）。

---

## 8. 横切设计

### 8.1 防御性设计

| 风险 | 对策 | 位置 |
|---|---|---|
| 路径穿越 | 上传文件名剥掉全部目录成分，仅保留纯文件名 | `content.go saveFile` |
| SQL 注入 | 全量参数化查询；`sortBy` 走白名单 | `store.go` |
| 非法输入 | type/rating/wave 范围校验；快照条目字段级跳过 | 各 handler / `elite.go Upload` |
| 请求体过大 | 快照查询 `LIMIT 1000`（Demo 规模保护）；上传大小限制为 Roadmap | store / 待办 |

### 8.2 可扩展性

- `Store` / `EliteStore` 为接口：SQLite → MySQL 切换零改动业务层；
- UGC 文件存储本地磁盘，生产可换 OSS（`saveFile` 单点封装）；
- 快照 `stats` 字段透传预留，名人堂系统后续直接读取，不影响筛选。

### 8.3 与 Canonical 的边界

- 精英怪注入后仍遵守 Canonical §23：**只携带自身 BD 快照，不读取当前 Run 的 Card 层**（Basic / Global / Monster-Type / Type Growth）；
- 精英怪占用 Pressure Budget，不作为免费额外密度（Encounter §4/§7）；Threat Cost 折算归**前台** Director 结算（策划案 §8.6）；
- 本服务器只负责"从上传快照中筛出候选并返回一条"；客户端注入路径、Budget 结算、Elite 表现按 Canonical 与前台实现执行。

---

## 9. 部署

- **局域网**：单机 `./server.exe`，客户端填该机内网 IP；
- **公网**：`GOOS=linux GOARCH=amd64 go build -o server-linux .`，安全组放行 **8080/TCP**，可选 Nginx 反代 + TLS；
- 数据目录 `data/`（SQLite + UGC 文件）需纳入备份。

---

## 10. 测试策略

| 测试 | 方式 | 覆盖 |
|---|---|---|
| `test/ugctest/` | 黑盒 HTTP（需先 `go run .`） | 上传→列表→搜索→下载→订阅→评分 |
| `test/elitepicktest/` | 自包含（进程内起独立实例 + 临时库 + 独立端口） | 空库兜底 3 / bdCount=0 防御 / 玩家隔离 / 主路径命中 / 兜底 1（relaxed）/ 兜底 2 / upsert 覆盖 / TOP_BAND percent+topk / 每玩家上限 / 全局 FIFO / upsert 不占额度 |

精英怪测试不依赖预启动服务器、不污染 `data/game.db`，可重复执行。

---

## 11. Roadmap（未实现，非承诺）

- 上传大小限制与文件格式白名单（建议单文件 ≤ 1MB，.json/.png）；
- UGC 内容审核、敏感词过滤；
- 名人堂系统（消费 `gameTime` / `stats` 透传字段）；
- MySQL / OSS 生产化。
