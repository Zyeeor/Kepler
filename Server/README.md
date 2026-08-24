# Possession 内容服务器

《Possession》是**单机游戏**，本服务只承担两项在线职责：

1. **UGC 内容平台**：地图 / 怪物模版的上传、下载、列表、搜索、订阅、评分；
2. **精英怪 BD 快照投放**：拉取**其他玩家 BD（构筑）过的怪物**作为精英怪投放候选，靠波次差保证越级难度（设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》）。

> 战斗、存档等其余玩法不依赖本服务；原联机对局功能已按 Canonical（`PC 单机`）移除。

## 文档

| 文档 | 内容 |
|---|---|
| [docs/system-design.md](docs/system-design.md) | 系统设计：架构分层、数据模型、精英怪投放引擎、参数与部署 |
| [docs/api-guide.md](docs/api-guide.md) | 接口手册：8 个 HTTP 接口逐个说明 + Unity 对接示例 |

## 核心特性

- **纯 HTTP JSON API**：无 WS / KCP / Protobuf，Unity 用 `UnityWebRequest` 即可对接
- **无账号体系**：客户端本地持久化匿名 ID（UGC 侧 GUID；精英怪侧设备特征码）
- **精英怪四步筛选**：BD 数量门槛 → 波次差（越级难度）→ 玩家隔离 → TOP_BAND 高档内加权随机；候选为空时三级兜底（放宽波次差 → 最高波次档最大 BD → 本波不投放）
- **战果回传**：精英在他人游戏中的五类战果事件（`spawned` / `fatal` / `possessed` / `bodyFatal` / `runFail`，设计依据：Meta_Progression §6.5）批量上报 `POST /api/elite/events`，按构筑主人 `(ownerPlayerId, ownerRunId, sin)` 聚合计数；`GET /api/elite/stats?playerId=` 查询聚合（荣誉殿堂「异步战绩」数据出口）；无主事件（本地 Preset）与非法 sin/type 逐条跳过，不整批失败
- **透传存储原则**：`bdData` 结构、`sourceWave` 语义由前台定义，后台只存不解析
- **容量治理**：多局独立保留；全局 FIFO 上限 + 每玩家上限（参数可配）
- **可扩展**：存储接口化（SQLite → MySQL 零改动业务），文件存储可换 OSS

## 快速开始

环境要求：Go 1.22+（`net/http` 路由模式匹配需要）。

```bash
go build -o server.exe .
./server.exe          # 默认 HTTP :8080，数据落 data/
```

### 启动参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-addr` | `:8080` | HTTP 监听地址 |
| `-db` | `data/game.db` | SQLite 数据库路径 |
| `-upload` | `data/ugc` | UGC 文件存储目录 |
| `-log` | `data/server.log` | 日志文件路径（控制台+文件双写，追加；传空 `""` 禁用文件日志） |
| `-minBd` | `1` | 精英怪筛选：最低 BD 数量门槛（MIN_BD） |
| `-waveGap` | `1` | 精英怪筛选：波次差（WAVE_GAP） |
| `-topBandMode` | `percent` | TOP_BAND 模式：`percent`（前 X%）/ `topk`（前 K 条） |
| `-topBandPercent` | `0.2` | percent 模式高分档比例 |
| `-topBandTopK` | `5` | topk 模式高分档条数 |
| `-maxSnapshots` | `10000` | 候选库全局上限（FIFO 淘汰最早快照） |
| `-maxSnapshotsPerPlayer` | `100` | 每玩家快照上限 |

精英怪参数均为首版 Baseline（TUNABLE），需 Playable 验证后调整。

## 日志

日志同时输出到**控制台**与日志文件（默认 `data/server.log`，追加写入，不轮转——Demo 阶段人工管理即可）。三类内容：

```text
2026/08/20 10:00:00 [http] POST /api/creations -> 200 3ms 127.0.0.1:52046   # 访问日志：方法/路径/状态/耗时/来源
2026/08/20 10:00:01 [elite] upload player=u9f2c run=r-01 entries=2 stored=2 skipped=0    # 业务日志：快照上传
2026/08/20 10:00:02 [elite] pick wave=5 player=u9f2c -> sin=lust bdCount=3 sourceWave=7 by=u1111 (main, candidates=24)  # 业务日志：筛选命中（含路径与候选规模）
2026/08/20 10:00:03 [error] http 500: save file: ...                        # 错误日志：仅 5xx 落服务端日志
```

观测要点：`pick` 日志的 `by=` 路径分布（main / relaxed:wave-gap=0 / relaxed:top-wave / none）反映候选库健康度——`relaxed` 占比高说明库的波次覆盖不足，`none` 说明库空。

## 验证与测试

```bash
# 终端 1：启动服务器
go run .

# 终端 2：UGC 端到端测试（上传→列表→搜索→下载→订阅→评分）
go run ./test/ugctest

# 精英怪投放端到端测试（自包含，无需预启动服务器）
go run ./test/elitepicktest

# 战果回传端到端测试（事件上报 → 校验跳过 → 按构筑主人聚合 → 战绩查询；自包含）
go run ./test/eliteeventtest
```

## 项目结构

```
├── main.go                  # 入口（启动参数 + 装配）
├── internal/
│   ├── server/              # 装配层：HTTP 路由 + handler
│   ├── service/             # 业务层：UGC 内容服务 + 精英怪 BD 快照服务
│   └── store/               # 存储层：Store/EliteStore 接口 + SQLite 实现
├── test/
│   ├── ugctest/             # UGC 端到端测试（HTTP）
│   ├── elitepicktest/       # 精英怪投放端到端测试（自包含）
│   └── eliteeventtest/      # 战果回传端到端测试（自包含）
├── data/                    # 运行时数据（game.db + ugc 文件）
└── docs/                    # system-design.md + api-guide.md
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
- [ ] 名人堂系统（消费快照透传的 gameTime / stats）
- [ ] MySQL / OSS 生产化

## License

MIT
