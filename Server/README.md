# Possession 内容服务器

《Possession》是**单机游戏**，本服务只承担两项在线职责：

1. **UGC 内容平台**：地图 / 怪物模版的上传、下载、列表、搜索、订阅、评分；
2. **精英怪 BD 快照投放**：拉取**其他玩家 BD（构筑）过的怪物**作为精英怪投放候选，靠波次差保证越级难度；配套**战果回传聚合**（荣誉殿堂「异步战绩」数据源）（设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》、Meta_Progression §6）。

> 战斗、存档等其余玩法不依赖本服务；原联机对局功能已按 Canonical（`PC 单机`）移除。

## 核心特性

- **纯 HTTP JSON API**：无 WS / KCP / Protobuf，Unity 用 `UnityWebRequest` 即可对接；内置 CORS 支持（MonsterBuildEditor 为浏览器页面，直连本服务需跨源许可）
- **无账号体系**：客户端本地持久化匿名 ID（UGC 侧 GUID；精英怪侧设备特征码）
- **精英怪四步筛选**：BD 数量门槛 → 波次差（越级难度，由客户端指定）→ 玩家隔离 → TOP_BAND 高档内加权随机；候选为空时三级兜底（放宽波次差 → 最高波次档最大 BD → 本波不投放）
- **种子数据**：首次启动空库时自动注入预设快照（7 Sin × 2 虚拟玩家），保障首位玩家也能遇到精英怪；真实数据积累后自然 FIFO 淘汰（见下方「种子文件说明」）
- **战果回传**：精英在他人游戏中的五类战果事件（`spawned` / `fatal` / `possessed` / `bodyFatal` / `runFail`，设计依据：Meta_Progression §6.5）批量上报 `POST /api/elite/events`，按构筑主人 `(ownerPlayerId, ownerRunId, sin)` 聚合计数；`GET /api/elite/stats?playerId=` 查询聚合（荣誉殿堂「异步战绩」数据出口）；无主事件（本地 Preset）与非法 sin/type 逐条跳过，不整批失败
- **MonsterBuildEditor 构筑导入**：工具在线上传走 `POST /api/user-bd`；`-userBDDir` 目录内导出的 JSON 每次启动自动导入（内容指纹幂等去重，重复不占额度）
- **透传存储原则**：`bdData` 结构、`sourceWave` 语义由前台定义，后台只存不解析
- **容量治理**：多局独立保留；全局 FIFO 上限 + 每玩家上限（参数可配）
- **健康检查**：`GET /api/health` 供客户端启动探活，不可达时 UI 提示"精英服务器离线"
- **可扩展**：存储接口化（SQLite → MySQL 零改动业务），文件存储可换 OSS

## HTTP 接口（12 个）

| 方法 | 路径 | 用途 |
|------|------|------|
| POST | `/api/creations` | UGC 内容上传 |
| GET | `/api/creations` | UGC 列表（分页/类型过滤/排序） |
| GET | `/api/creations/search` | UGC 关键词搜索 |
| GET | `/api/creations/{id}/download` | UGC 文件下载（自动计数） |
| POST | `/api/creations/{id}/subscribe` | 订阅 / 取消订阅 |
| POST | `/api/creations/{id}/rate` | 评分（自动算均值） |
| POST | `/api/bd-snapshots` | 精英 BD 快照批量滚动上传（upsert） |
| POST | `/api/elite/pick` | 第 N 波精英投放请求（四步筛选 + 三级兜底；`snapshot=null` 为正常"不投放"分支） |
| POST | `/api/elite/events` | 战果回传批量上报（按构筑主人聚合） |
| GET | `/api/elite/stats?playerId=` | 异步战绩聚合查询（playerId = 构筑主人） |
| POST | `/api/user-bd` | MonsterBuildEditor 工具在线上传构筑（严格校验，实时入池） |
| GET | `/api/health` | 健康检查（客户端启动探活） |

请求/响应字段以 `internal/server/server.go` 的 handler 与 DTO 定义为准。

## 快速开始

环境要求：Go 1.22+（`net/http` 路由模式匹配需要）。

```bash
go build -o server.exe .
./server.exe          # 默认 HTTP :8080，数据落 data/
```

首次启动时，若 `data/game.db` 为空且存在种子文件，自动注入种子快照：

```text
2026/08/24 12:17:08 seeded 14 snapshots from data/seed_snapshots.json (pool was empty)
```

### 种子文件说明

`data/` 下除种子快照配置外的运行时数据（数据库/日志/UGC 文件/userBD 导入）均被 `Server/.gitignore` 忽略；**`data/seed_snapshots.json` 随仓库分发**（策划可编辑，改动直接提交）——clone 后即可正常种子注入。种子注入仅在空库时执行一次（幂等）；删除 `data/seed_snapshots.json` 或传 `-seedFile ""` 可禁用。

### 启动参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-addr` | `:8080` | HTTP 监听地址 |
| `-db` | `data/game.db` | SQLite 数据库路径 |
| `-upload` | `data/ugc` | UGC 文件存储目录 |
| `-log` | `data/server.log` | 日志文件路径（控制台+文件双写，追加；传空 `""` 禁用文件日志） |
| `-seedFile` | `data/seed_snapshots.json` | 种子快照文件路径（空 `""` = 不 seed；库非空或文件不存在时自动跳过） |
| `-userBDDir` | `data/userBD` | 用户 BD 构筑导入目录（MonsterBuildEditor 工具导出 JSON；每次启动导入，重复导入自动去重） |
| `-minBd` | `1` | 精英怪筛选：最低 BD 数量门槛（MIN_BD） |
| `-waveGap` | `0` | 精英怪筛选：服务端兜底波次差（正常由客户端 pick 请求指定 waveGap） |
| `-topBandMode` | `percent` | TOP_BAND 模式：`percent`（前 X%）/ `topk`（前 K 条） |
| `-topBandPercent` | `0.2` | percent 模式高分档比例 |
| `-topBandTopK` | `5` | topk 模式高分档条数 |
| `-maxSnapshots` | `10000` | 候选库全局上限（FIFO 淘汰最早快照） |
| `-maxSnapshotsPerPlayer` | `100` | 每玩家快照上限 |

精英怪参数均为首版 Baseline（TUNABLE），需 Playable 验证后调整。

## 日志

日志同时输出到**控制台**与日志文件（默认 `data/server.log`，追加写入，不轮转——Demo 阶段人工管理即可）。

版式约定：顶层事件顶格输出，隶属事件的明细行以 `├` 树形前缀缩进，`·` 为字段分隔符。

### 上传日志

```text
2026/08/24 15:58:43 upload player=device-xxx run=run-xxx · entries=3 stored=3 skipped=0
2026/08/24 15:58:43   ├ stored · sin=lust monsterType=色欲-灵念师 bdCount=3 sourceWave=6 gameTime=280 (upsert device-xxx/run-xxx/lust)
2026/08/24 15:58:43   ├ stored · sin=pride monsterType=pride bdCount=2 sourceWave=6 gameTime=280 (upsert device-xxx/run-xxx/pride)
2026/08/24 15:58:43   ├ capacity · player=device-xxx 5/100 (within limit)
2026/08/24 15:58:43   ├ capacity · global 12/10000 (within limit)
```

### 筛选日志

```text
2026/08/24 15:13:40 pick wave=5 player=device-xxx gap=1 · minBD=1 band=percent/0.20 topK=5
2026/08/24 15:13:40   ├ query · bdCount>=1 AND sourceWave>=6 AND player!=self → candidates=3
2026/08/24 15:13:40   ├ cand #1  · id=1    lust      bd=3  wave=7   by=device-seed-default-a run=run-seed-001
2026/08/24 15:13:40   ├ cand #2  · id=3    envy      bd=2  wave=8   by=device-seed-default-b run=run-seed-002
2026/08/24 15:13:40   ├ band · mode=percent size=1/3 (percent=0.20, topK=5)
2026/08/24 15:13:40   ├ band · size<=1 → pick top id=1 sin=lust bdCount=3
2026/08/24 15:13:40 pick result wave=5 player=device-xxx → sin=lust bdCount=3 sourceWave=7 by=device-seed-default-a (path=main, 3 candidates)
```

### 兜底路径日志

```text
2026/08/24 15:13:40 pick wave=8 player=device-xxx gap=1 · minBD=1 band=percent/0.20 topK=5
2026/08/24 15:13:40   ├ query · bdCount>=1 AND sourceWave>=9 AND player!=self → candidates=0
2026/08/24 15:13:40   ├ fallback1 · main path empty, relax waveGap → sourceWave>=8 (was >=9)
2026/08/24 15:13:40   ├ fallback1 query · bdCount>=1 AND sourceWave>=8 AND player!=self → candidates=1
2026/08/24 15:13:40   ├ ...
2026/08/24 15:13:40 pick result wave=8 player=device-xxx → ... (path=relaxed:wave-gap=0, 1 candidates)
```

观测要点：`pick result` 的 `path=` 分布（main / relaxed:wave-gap=0 / relaxed:top-wave / none）反映候选库健康度——`relaxed` 占比高说明库的波次覆盖不足，`none` 说明库空。

### 战果回传日志

```text
2026/08/24 15:13:40 events · reporter=device-xxx accepted=2/5
2026/08/24 15:13:40   ├ skip event[0] · type=spawned owner="local-preset" (no real owner)
2026/08/24 15:13:40   ├ skip event[1] · sin="not-a-sin" type="fatal" (invalid)
```

观测要点：`accepted < 上报总数` 说明存在跳过（本地 Preset 无主事件 / 非法 sin 或 type）——属正常防御分支，不影响其余条目聚合；`elite_build_stats` 表可核对聚合结果。

## 验证与测试

```bash
# 终端 1：启动服务器
go run .

# 终端 2：UGC 端到端测试（上传→列表→搜索→下载→订阅→评分；需预启动服务器）
go run ./test/ugctest

# 精英怪投放端到端测试（自包含，无需预启动服务器）
go run ./test/elitepicktest

# 战果回传端到端测试（事件上报 → 校验跳过 → 按构筑主人聚合 → 战绩查询；自包含）
go run ./test/eliteeventtest

# 验证种子数据：删库重启（需 data/seed_snapshots.json 存在，见「种子文件说明」）
rm data/game.db && go run .
# 日志应输出 "seeded 14 snapshots ..."
```

## 项目结构

```
├── main.go                  # 入口（启动参数 + 装配）
├── internal/
│   ├── server/              # 装配层：HTTP 路由 + handler + 种子注入
│   ├── service/             # 业务层：UGC 内容服务 + 精英怪 BD 快照服务（上传/筛选/战果回传）
│   └── store/               # 存储层：Store/EliteStore 接口 + SQLite 实现
├── test/
│   ├── ugctest/             # UGC 端到端测试（需预启动服务器）
│   ├── elitepicktest/       # 精英怪投放端到端测试（自包含）
│   └── eliteeventtest/      # 战果回传端到端测试（自包含）
└── data/                    # 运行时数据（除种子配置外均被 .gitignore 忽略）
    ├── game.db              # SQLite 数据库（运行时生成）
    ├── seed_snapshots.json  # 种子快照配置（策划可编辑，随仓库分发）
    ├── server.log           # 日志文件
    ├── ugc/                 # UGC 上传文件
    └── userBD/              # MonsterBuildEditor 导出构筑（启动时自动导入）
```

## 数据表

| 表 | 用途 |
|----|------|
| `creations` | UGC 元数据（type/name/tags/downloads/rating/version…） |
| `subscriptions` | 订阅关系（player_id + creation_id 唯一） |
| `creation_reviews` | 评分评论（creation_id + player_id 唯一，评分自动算均值） |
| `monster_build_snapshots` | 精英怪 BD 快照候选库（player_id + run_id + sin 唯一 upsert） |
| `elite_build_stats` | 战果回传聚合（owner_player_id + owner_run_id + sin 唯一；deployed/fatal/possessed/body_fatal/run_fail 计数器） |

## 部署

- **局域网**：一台机器跑 `./server.exe`，客户端填该机内网 IP；
- **公网**：`GOOS=linux GOARCH=amd64 go build -o server-linux .`，安全组放行 **8080/TCP**，可选 Nginx 反代 + TLS。

## Roadmap

- [ ] 上传大小限制与文件格式白名单（建议单文件 ≤ 1MB，.json/.png）
- [ ] UGC 内容审核、敏感词过滤
- [x] 荣誉殿堂异步战绩出口（`GET /api/elite/stats`；客户端 HallOfFamePanel 已接入）
- [ ] 快照透传字段（gameTime / stats）的长线消费
- [ ] MySQL / OSS 生产化

## License

MIT
