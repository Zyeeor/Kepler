# Possession 内容服务器

《Possession》是**单机游戏**，本服务只承担两项在线职责：

1. **UGC 内容平台**：地图 / 怪物模版的上传、下载、列表、搜索、订阅、评分；
2. **精英怪 BD 快照投放**：拉取**其他玩家 BD（构筑）过的怪物**作为精英怪投放候选，靠投放序号差保证越级难度；配套**战果回传聚合**（荣誉殿堂「异步战绩」数据源）（设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》、Meta_Progression §6）。
   > 投放模型（前台现状）：精英投放为定时模型——每 60s 周期第 40s 投 1 只，Boss 前共约 7 次投放。投放请求的 `wave` = **第几次投放**（投放序号，1-based）；上传快照的 `sourceWave` = **本局第几次选卡**（选卡会话计数，含精英奖励选卡；Owner 2026-08-26 决策）——两者为同量纲进度计数，服务器按 `sourceWave >= wave + waveGap` 越级筛选。

> 战斗、存档等其余玩法不依赖本服务；原联机对局功能已按 Canonical（`PC 单机`）移除。

## 核心特性

- **纯 HTTP JSON API**：无 WS / KCP / Protobuf，Unity 用 `UnityWebRequest` 即可对接；内置 CORS 支持（MonsterBuildEditor 为浏览器页面，直连本服务需跨源许可）
- **无账号体系**：客户端本地持久化匿名 ID（UGC 侧 GUID；精英怪侧设备特征码）
- **精英怪四步筛选**：BD 数量门槛 → 投放序号差（越级难度，由客户端指定；序号 = 第几次投放精英怪）→ 玩家隔离 → TOP_BAND 高档内加权随机；候选为空时三级兜底（放宽投放序号差 → 最高投放序号档最大 BD → 本次不投放）
- **种子数据**：首次启动空库时自动注入预设快照（7 Sin × 2 虚拟玩家），保障首位玩家也能遇到精英怪；真实数据积累后自然 FIFO 淘汰（见下方「种子文件说明」）
- **战果回传**：精英在他人游戏中的五类战果事件（`spawned` / `fatal` / `possessed` / `bodyFatal` / `runFail`，设计依据：Meta_Progression §6.5）批量上报 `POST /api/elite/events`，按构筑主人 `(ownerPlayerId, ownerRunId, sin)` 聚合计数；`GET /api/elite/stats?playerId=` 查询聚合（荣誉殿堂「异步战绩」数据出口）；无主事件（本地 Preset）与非法 sin/type 逐条跳过，不整批失败
- **MonsterBuildEditor 构筑导入**：工具在线上传走 `POST /api/user-bd`（严格校验通过后保存到 `repo/{playerId}/bd-{runId}.json` 并实时入池）；`repo/`（`-userBDDir`）内的构筑 JSON 每次启动自动导入（内容指纹幂等去重，重复不占额度）
- **透传存储原则**：`bdData` 结构、`sourceWave` 语义由前台定义，后台只存不解析（当前语义 = 本局第几次选卡，见投放模型说明）
- **服务防护（P1）**：HTTP 全链路超时（Read 30s / Write 60s / Idle 120s）+ SIGINT/SIGTERM 优雅停机（10s 排空）；全局请求体上限 16MB（UGC 上传文件 ≤1MB `.json` 白名单、缩略图 ≤2MB PNG 魔数校验）；战果事件按 `(playerId, eventId)` 内存去重（10 分钟窗口，旧客户端空 eventId 跳过）
- **限流（B1）**：按客户端 IP × 路由组的内存令牌桶限流（UGC 上传 / 订阅评分 / 快照上传 / 精英投放 / 战果上报 / 工具上传 / GET 类各一条规则，默认阈值远宽于正常游戏行为），超限 `429 + Retry-After`；`-rateLimit=false` 可关（规则见 `internal/httpapi/ratelimit.go`，TUNABLE）
- **战果防滥用（B2）**：单请求批量上限 50 条；`owner == reporter` 自刷逐条跳过；单 reporter 进程内累计配额 10000 条（防脚本持续灌，重启清零的最终一致防护）
- **存储防护（P2）**：SQLite WAL + busy_timeout 5s（外部工具并发访问不再 `database is locked`）；列表/搜索分页 pageSize 上限 100；搜索关键词 LIKE 通配符按字面匹配（`%`/`_` 转义）；userBD 文件原子写（tmp + rename，崩溃不留半写 JSON）
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
| POST | `/api/elite/pick` | 第 N 次投放精英怪请求（N = 投放序号；四步筛选 + 三级兜底；`snapshot=null` 为正常"不投放"分支） |
| POST | `/api/elite/events` | 战果回传批量上报（按构筑主人聚合；`eventId` 幂等去重——重试重放的同一事件窗口内只计一次） |
| GET | `/api/elite/stats?playerId=` | 异步战绩聚合查询（playerId = 构筑主人） |
| GET | `/api/elite/leaderboard?limit=` | 荣誉殿堂排行榜：击杀玩家（bodyFatal）最多的 Top N BD 怪物（默认 20，上限 100；按 owner+run+sin 关联快照，悬空聚合行不上榜） |
| POST | `/api/user-bd` | MonsterBuildEditor 工具在线上传构筑（严格校验，实时入池） |
| GET | `/api/health` | 健康检查（客户端启动探活） |

请求/响应字段以 `server/` 包的 handler 与 DTO 定义为准。

## 快速开始

环境要求：Go 1.26.5+（以 `go.mod` 为准；`net/http` 方法路由模式匹配需要 1.22+）。

**一键启动**（自动检测 Go、构建到 `bin/` 并运行；额外参数原样透传给服务器）：

```bash
start-server.bat        # Windows（也可直接双击）
./start-server.sh       # Linux / macOS（首次先 chmod +x start-server.sh）

# 带参示例（两平台相同）：start-server.bat -addr :9000  /  ./start-server.sh -addr :9000
```

**手动构建运行**：

```bash
go build -o server.exe ./cmd/server
./server.exe          # 默认 HTTP :8080，数据库落 data/，文件落 repo/，日志按天落 log/
```

首次启动时，若 `data/game.db` 为空且存在种子文件，自动注入种子快照：

```text
2026/08/24 12:17:08 seeded 14 snapshots from config/seed_snapshots.json (pool was empty)
```

### 种子文件说明

运行时数据目录（`data/` 数据库、`repo/` 文件仓库、`log/` 日志）均被 `Server/.gitignore` 忽略；**`config/seed_snapshots.json` 随仓库分发**（策划可编辑，改动直接提交）——clone 后即可正常种子注入。种子注入仅在空库时执行一次（幂等）；删除 `config/seed_snapshots.json` 或传 `-seedFile ""` 可禁用。

### 启动参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-config` | `config/server.json` | JSON 配置文件路径（键名与 flag 一致；优先级：显式 flag > 文件 > 默认；传空 `""` 禁用；文件不存在时静默跳过） |
| `-addr` | `:8080` | HTTP 监听地址 |
| `-db` | `data/game.db` | SQLite 数据库路径 |
| `-upload` | `repo/ugc` | UGC 文件存储目录 |
| `-log` | `log` | 日志目录（控制台+文件双写，按天 `YYYY-MM-DD.log`，追加；传空 `""` 禁用文件日志） |
| `-detail` | `true` | Detail 级日志开关（stored/skip/cand/容量检查等明细行；长跑减噪可 `-detail=false`，Event 级始终输出） |
| `-rateLimit` | `true` | 按 IP 限流总开关（规则见 `internal/httpapi/ratelimit.go`；反代部署时全站共享同一桶，阈值需按整体流量评估） |
| `-seedFile` | `config/seed_snapshots.json` | 种子快照文件路径（空 `""` = 不 seed；库非空或文件不存在时自动跳过） |
| `-userBDDir` | `repo` | 用户 BD 构筑导入目录（MonsterBuildEditor 工具导出 JSON；每次启动导入，重复导入自动去重） |
| `-minBd` | `1` | 精英怪筛选：最低 BD 数量门槛（MIN_BD） |
| `-waveGap` | `0` | 精英怪筛选：服务端兜底投放序号差（正常由客户端 pick 请求指定 waveGap；序号 = 第几次投放精英怪） |
| `-topBandMode` | `percent` | TOP_BAND 模式：`percent`（前 X%）/ `topk`（前 K 条） |
| `-topBandPercent` | `0.2` | percent 模式高分档比例 |
| `-topBandTopK` | `5` | topk 模式高分档条数 |
| `-maxSnapshots` | `10000` | 候选库全局上限（FIFO 淘汰最早快照） |
| `-maxSnapshotsPerPlayer` | `100` | 每玩家快照上限 |

精英怪参数均为首版 Baseline（TUNABLE），需 Playable 验证后调整。

### 配置文件

启动参数可写入 JSON 配置文件（默认 `config/server.json`，`-config` 指定路径，传空禁用），键名与 flag 一致，值为 flag 的去横线 camelCase 形式：

```json
{
  "addr": ":8080",
  "db": "data/game.db",
  "upload": "repo/ugc",
  "log": "log",
  "seedFile": "config/seed_snapshots.json",
  "userBDDir": "repo",
  "detail": true,
  "minBd": 1,
  "waveGap": 0,
  "topBandMode": "percent",
  "topBandPercent": 0.2,
  "topBandTopK": 5,
  "maxSnapshots": 10000,
  "maxSnapshotsPerPlayer": 100
}
```

优先级：**显式 flag > 配置文件 > 代码默认值**（未显式传 flag 时文件值才生效，方便临时覆盖单个参数）。文件不存在时静默跳过；文件存在但 JSON 非法则启动失败。`config/server.json` 不随仓库分发，按需在各部署环境创建。

## 日志

日志同时输出到**控制台**与日志文件（默认目录 `log/`，按天一个文件 `log/YYYY-MM-DD.log`，跨天自动切换；追加写入，不轮转——Demo 阶段人工管理即可）。

版式约定：所有日志行顶格输出，`·` 为字段分隔符；访问日志显示 Server handler 函数名（如 `handleElitePick → 200 · 3ms · 127.0.0.1:51303`）。

### 上传日志

```text
2026/08/24 15:58:43 upload player=device-xxx run=run-xxx · entries=3 stored=3 skipped=0
2026/08/24 15:58:43 stored · sin=lust monsterType=色欲-灵念师 bdCount=3 sourceWave=6 gameTime=280 (upsert device-xxx/run-xxx/lust)
2026/08/24 15:58:43 stored · sin=pride monsterType=pride bdCount=2 sourceWave=6 gameTime=280 (upsert device-xxx/run-xxx/pride)
2026/08/24 15:58:43 capacity · player=device-xxx 5/100 (within limit)
2026/08/24 15:58:43 capacity · global 12/10000 (within limit)
```

### 筛选日志

```text
2026/08/24 15:13:40 pick wave=5 player=device-xxx gap=1 · minBD=1 band=percent/0.20 topK=5
2026/08/24 15:13:40 query · bdCount>=1 AND sourceWave>=6 AND player!=self → candidates=3
2026/08/24 15:13:40 cand #1  · id=1    lust      bd=3  wave=7   by=device-seed-default-a run=run-seed-001
2026/08/24 15:13:40 cand #2  · id=3    envy      bd=2  wave=8   by=device-seed-default-b run=run-seed-002
2026/08/24 15:13:40 band · mode=percent size=1/3 (percent=0.20, topK=5)
2026/08/24 15:13:40 band · size<=1 → pick top id=1 sin=lust bdCount=3
2026/08/24 15:13:40 pick result wave=5 player=device-xxx → sin=lust bdCount=3 sourceWave=7 by=device-seed-default-a (path=main, 3 candidates)
```

### 兜底路径日志

```text
2026/08/24 15:13:40 pick wave=8 player=device-xxx gap=1 · minBD=1 band=percent/0.20 topK=5
2026/08/24 15:13:40 query · bdCount>=1 AND sourceWave>=9 AND player!=self → candidates=0
2026/08/24 15:13:40 fallback1 · main path empty, relax waveGap → sourceWave>=8 (was >=9)
2026/08/24 15:13:40 fallback1 query · bdCount>=1 AND sourceWave>=8 AND player!=self → candidates=1
2026/08/24 15:13:40 ...
2026/08/24 15:13:40 pick result wave=8 player=device-xxx → ... (path=relaxed:wave-gap=0, 1 candidates)
```

观测要点：`pick result` 的 `path=` 分布（main / relaxed:wave-gap=0 / relaxed:top-wave / none）反映候选库健康度——`relaxed` 占比高说明库的进度序号覆盖不足，`none` 说明库空。（日志中的 `wave=` / `sourceWave=` 为字段名，与前台 wire 字段一致；`wave` = 投放序号，`sourceWave` = 上传者第几次选卡。）

### 战果回传日志

```text
2026/08/24 15:13:40 events · reporter=device-xxx accepted=2/5
2026/08/24 15:13:40 skip event[0] · type=spawned owner="local-preset" (no real owner)
2026/08/24 15:13:40 skip event[1] · sin="not-a-sin" type="fatal" (invalid)
```

观测要点：`accepted < 上报总数` 说明存在跳过（本地 Preset 无主事件 / 非法 sin 或 type）——属正常防御分支，不影响其余条目聚合；`elite_build_stats` 表可核对聚合结果。

## 验证与测试

```bash
# 一键全量回归（四套端到端测试均为标准 go test，自包含：独立端口 + 临时数据库 + Cleanup 清理）
go test ./...

# 按域单跑 / 显示断言明细
go test ./tests/ugctest -v          # UGC：上传→列表→搜索→下载→订阅→评分
go test ./tests/elitepicktest -v    # 精英投放：筛选→兜底→隔离→TOP_BAND→容量治理
go test ./tests/eliteeventtest -v   # 战果回传：聚合→防污染→排行榜→幂等去重
go test ./tests/ratelimittest -v    # 限流与防滥用：429 触发→批量上限→防自刷→上传白名单

# 验证种子数据：删库重启（需 config/seed_snapshots.json 存在，见「种子文件说明」）
rm data/game.db && go run ./cmd/server
# 日志应输出 "seeded 14 snapshots ..."
```

## 项目结构

```
├── cmd/
│   └── server/              # 入口（薄 main：配置解析 + 日志初始化 + 装配启动）
├── internal/                # 私有实现（Go internal 规则：仅本模块可 import）
│   ├── config/              # 配置解析：命令行 flag + JSON 配置文件合并
│   ├── httpapi/             # HTTP 装配层：路由（按域注册）+ handler + DTO + 中间件
│   │   ├── server.go        #   Server 装配、生命周期（Run/Close）与健康检查
│   │   ├── middleware.go    #   访问日志 + CORS + 请求体上限
│   │   ├── ratelimit.go     #   按 IP 的令牌桶限流（规则表 + limited 中间件）
│   │   ├── response.go      #   JSON 编解码与统一响应辅助
│   │   ├── ugc.go           #   UGC 域（路由 + handler + DTO）
│   │   └── elite.go         #   精英域（路由 + handler + DTO）
│   ├── ugc/                 # UGC 域：ContentService + Store 接口 + 模型
│   ├── elite/               # 精英域：EliteService（上传/导入/筛选/战果回传/排行榜）+ EliteStore 接口 + 模型
│   ├── storage/sqlite/      # SQLite 实现（实现两域接口，可换 MySQL）
│   │   ├── sqlite.go        #   连接与 PRAGMA（WAL + busy_timeout）
│   │   ├── migrations.go    #   版本化 schema migration
│   │   ├── elite.go         #   精英域表操作
│   │   └── ugc.go           #   UGC 域表操作
│   └── logx/                # 通用日志工具（Event/Detail 版式 + 按天文件 Writer）
├── tests/                   # 端到端测试（自包含：独立端口 + 临时数据库）
│   ├── ugctest/             #   UGC：上传→列表→搜索→下载→订阅→评分
│   ├── elitepicktest/       #   精英投放：筛选→兜底→隔离→TOP_BAND→容量治理
│   └── eliteeventtest/      #   战果回传：聚合→防污染→排行榜→幂等去重
├── start-server.bat         # 一键启动（Windows，可双击）
├── start-server.sh          # 一键启动（Linux / macOS）
├── config/
│   └── seed_snapshots.json  # 种子快照配置（策划可编辑，随仓库分发；server.json 可选配置文件不随仓库分发）
├── data/                    # SQLite 数据库（运行时生成，gitignore；含 schema_migrations 版本表）
├── repo/                    # 文件仓库（运行时，gitignore）：{playerId}/ 构筑导入文件、ugc/ UGC 上传文件
└── log/                     # 按天日志 YYYY-MM-DD.log（运行时，gitignore）
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

服务器是**零依赖单二进制**（SQLite 内嵌、纯 Go 实现、无 CGO），部署 = 拷贝二进制 + `config/` 目录；首次启动自动创建 `data/`、`repo/`、`log/`。

### Windows 部署

```powershell
cd Server
go build -o server.exe ./cmd/server
./server.exe                    # 默认 :8080；改端口：./server.exe -addr :9000
```

1. **防火墙放行**：首次运行会弹窗询问，或命令行添加规则：
   ```powershell
   New-NetFirewallRule -DisplayName "Possession Server" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
   ```
2. **客户端接入**：填本机内网 IP（`ipconfig` 查看），如 `http://192.168.1.10:8080`。
3. **常驻运行**：Demo 规模直接开控制台窗口即可；需开机自启可用任务计划程序或 NSSM 注册为 Windows 服务。

### Linux 部署

1. **构建**（二选一）：
   ```powershell
   # 在 Windows 上交叉编译（PowerShell；纯 Go 无 CGO，直接可编）
   $env:GOOS="linux"; $env:GOARCH="amd64"; $env:CGO_ENABLED="0"; go build -o server-linux ./cmd/server; Remove-Item Env:GOOS,Env:GOARCH,Env:CGO_ENABLED
   ```
   ```bash
   # 或在 Linux 上直接构建
   go build -o server ./cmd/server
   ```
2. **上传**：把二进制与 `config/` 目录放到部署目录，如 `/opt/possession/`。
3. **运行**：
   ```bash
   cd /opt/possession && chmod +x server-linux
   ./server-linux
   ```
4. **防火墙**：`sudo ufw allow 8080/tcp`；云服务器还需在控制台安全组放行 **8080/TCP**。
5. **systemd 常驻**（推荐）：
   ```ini
   # /etc/systemd/system/possession.service
   [Unit]
   Description=Possession Server
   After=network.target

   [Service]
   WorkingDirectory=/opt/possession
   ExecStart=/opt/possession/server-linux
   Restart=always

   [Install]
   WantedBy=multi-user.target
   ```
   ```bash
   sudo systemctl enable --now possession
   sudo journalctl -u possession -f   # 查看控制台输出；文件日志在 /opt/possession/log/
   ```

### 部署检查清单

- 二进制同目录带上 **`config/seed_snapshots.json`**——否则首位玩家空库无种子精英可投；
- 默认路径（`data/`、`repo/`、`log/`）是**相对路径**，解析到启动时的工作目录——用 systemd / 脚本启动务必设 `WorkingDirectory`，或用 `-db` / `-upload` / `-userBDDir` / `-log` 传绝对路径；
- 备份只需打包 `data/game.db` 与 `repo/`；
- 公网部署建议 Nginx 反代 + TLS。

## Roadmap

- [x] 上传大小限制与文件格式白名单（文件 ≤1MB + `.json` 后缀 + JSON 内容校验、缩略图 PNG 魔数）
- [x] 按 IP 限流 + 战果回传防滥用（批量上限 / 防自刷 / reporter 累计配额；均为 TUNABLE 常量）
- [ ] UGC 内容审核、敏感词过滤
- [ ] 反作弊强化（设备签名、reporter→owner 定向刷分上限等——当前为无账号体系下的基本防护）
- [x] 荣誉殿堂异步战绩出口（`GET /api/elite/stats`；客户端 HallOfFamePanel 已接入）
- [ ] 快照透传字段（gameTime / stats）的长线消费
- [ ] MySQL / OSS 生产化

## License

MIT
