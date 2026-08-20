# 接口文档 — Possession 内容服务器 API

> 版本：1.0（全新重写）
> 日期：2026-08-19
> 本文档面向**接口调用方**（Unity 前台 / 测试 / 工具开发）：每个接口给出"何时用、怎么调、响应样例"。
> 配套系统设计见 [system-design.md](system-design.md)。

---

## 目录

1. [基础约定](#一基础约定)
2. [接口总表](#二接口总表)
3. [UGC 接口详情](#三ugc-接口详情)
4. [精英怪 BD 快照接口](#四精英怪-bd-快照接口)
5. [典型调用序列](#五典型调用序列)
6. [错误码](#六错误码)
7. [Unity 对接示例](#七unity-对接示例)

---

## 一、基础约定

- **Base URL**：`http://<host>:8080`（局域网填服务器内网 IP；监听地址由服务端 `-addr` 决定）
- **协议**：全部 HTTP + JSON（UTF-8），请求体与响应体都是 JSON
- **命名**：字段一律 **camelCase**（`creationId`、`bdCount`）
- **无鉴权**：没有登录 / token。身份靠客户端本地持久化的匿名 ID：
  - UGC 侧：首次启动自生成 GUID 存 PlayerPrefs，上传作 `creatorId`，订阅/评分作 `playerId`；
  - 精英怪侧：**设备特征码**（前台生成，同设备 = 一个玩家），上传作 `playerId`（即快照的 `sourcePlayerId`），投放请求作 `playerId`；
  - 服务器不校验任何 ID（无账号体系）
- **二进制字段**：UGC 的 `fileData` / `thumbnail` 为 **base64** 字符串（JSON `[]byte` 标准编解码）
- **透传字段**：精英怪 `bdData` / `stats` 为不透明 JSON 原文，服务器只存不解析
- **时间戳**：Unix 秒，JSON 中为数字
- **错误格式**：`{"code": <http状态码>, "msg": "<原因>"}`

---

## 二、接口总表

| 方法 | 路径 | 用途 |
|------|------|------|
| POST | `/api/creations` | UGC：上传创作（地图 / 怪物包 / 模版） |
| GET | `/api/creations` | UGC：列表查询（分页 / 类型过滤 / 排序） |
| GET | `/api/creations/search` | UGC：按关键词搜索（名称 / 描述） |
| GET | `/api/creations/{id}/download` | UGC：下载文件内容（下载数 +1） |
| POST | `/api/creations/{id}/subscribe` | UGC：订阅 / 取消订阅 |
| POST | `/api/creations/{id}/rate` | UGC：评分（1–5 星，可带评论） |
| POST | `/api/bd-snapshots` | 精英怪：批量上传 BD 快照（每波选卡后滚动 upsert） |
| POST | `/api/elite/pick` | 精英怪：第 N 波请求投放（四步筛选 + 三级兜底） |

---

## 三、UGC 接口详情

### 3.1 上传 `POST /api/creations`

**何时用**：玩家在地图/怪物编辑器里点"发布"。

请求体：

```json
{
  "creatorId": "u9f2c1d0e8a7b4f6d",
  "creatorName": "Alice",
  "type": "map",
  "name": "暗黑地牢 v1.0",
  "description": "一个充满陷阱的地牢地图",
  "tags": ["roguelike", "dungeon", "hard"],
  "fileName": "map.json",
  "fileData": "eyJ2ZXJzaW9uIjoiMS4wIn0=",
  "thumbnail": "<PNG 的 base64，可选>"
}
```

| 字段 | 必填 | 说明 |
|------|------|------|
| creatorId | 否 | 本地匿名 ID；缺省时服务器生成 |
| creatorName | 否 | 缺省 `anonymous` |
| type | 是 | `map` / `monster` / `template` |
| name | 是 | 作品名称 |
| description | 否 | 简介 |
| tags | 否 | 标签数组 |
| fileName | 是 | 原始文件名（服务器剥掉目录成分，防路径穿越） |
| fileData | 是 | 文件内容 base64 |
| thumbnail | 否 | 缩略图 PNG 的 base64 |

响应 `200`：

```json
{ "creationId": "c8f3a1b2...", "fileUrl": "data/ugc/c8f3a1b2.../map.json" }
```

> `fileUrl` 为服务器本地相对路径（Demo 阶段）；下载内容请走 3.4 下载接口，不要直接拼 URL。

### 3.2 列表 `GET /api/creations`

**何时用**：创作中心 / 浏览页。

Query 参数：

| 参数 | 默认 | 说明 |
|------|------|------|
| type | 不过滤 | `map` / `monster` / `template` |
| page | 1 | 页码 |
| pageSize | 20 | 每页数量 |
| sortBy | created_at | `downloads` / `rating` / `created_at`（白名单，其他值按默认处理） |
| descending | true | 传 `descending=false` 则升序 |

响应 `200`：

```json
{
  "creations": [
    {
      "creationId": "c8f3a1b2...",
      "creatorId": "u9f2c...",
      "creatorName": "Alice",
      "type": "map",
      "name": "暗黑地牢 v1.0",
      "description": "一个充满陷阱的地牢地图",
      "tags": ["roguelike", "dungeon", "hard"],
      "thumbnailUrl": "data/ugc/c8f3a1b2.../thumbnail.png",
      "downloads": 12,
      "likes": 0,
      "rating": 4.5,
      "version": 1,
      "createdAt": 1755456000,
      "updatedAt": 1755456000
    }
  ],
  "total": 1
}
```

### 3.3 搜索 `GET /api/creations/search`

**何时用**：搜索框。关键词匹配名称与描述，按下载量降序。

Query：`keyword`（必填）、`type` / `page` / `pageSize` 同 3.2。

响应结构同 3.2。

### 3.4 下载 `GET /api/creations/{id}/download`

**何时用**：玩家选中作品进入游戏前，拉取文件内容。

响应 `200`：

```json
{
  "creationId": "c8f3a1b2...",
  "type": "map",
  "name": "暗黑地牢 v1.0",
  "fileData": "eyJ2ZXJzaW9uIjoiMS4wIn0=",
  "version": 1
}
```

> `fileData` base64 解码后即上传时的原始字节。调用该接口会使 `downloads + 1`。

### 3.5 订阅 `POST /api/creations/{id}/subscribe`

请求体：`{ "playerId": "u9f2c...", "subscribe": true }`（`false` 为取消）

响应 `200`：`{ "ok": true }`

### 3.6 评分 `POST /api/creations/{id}/rate`

请求体：

```json
{ "playerId": "u9f2c...", "rating": 5, "comment": "great map" }
```

- `rating` 必须为 1–5；同一 playerId 对同一作品重复评分为**覆盖更新**；
- 评分后服务器自动重算该作品平均分（列表/搜索里的 `rating` 字段）。

响应 `200`：`{ "ok": true }`

---

## 四、精英怪 BD 快照接口

> 设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》。核心承诺：
> 1. 返回的精英怪**必然是其他玩家真正 BD 过的怪物**；
> 2. 优先投放 **BD 更深**（`bdCount` 更大）的怪物；
> 3. 靠**波次差**（`sourceWave ≥ wave + WAVE_GAP`）制造越级压力。
>
> **透传存储原则**：`bdData` 结构（卡 ID + 层数）、`sourceWave` 语义、Final 是否上传及其编码均由前台定义，后台只存不解析；筛选只依赖 `bdCount` 与 `sourceWave` 两个数值。

### 4.1 批量上传 `POST /api/bd-snapshots`

**何时用**：每波 Card 选择完成后，上传本局当前**所有 `bdCount ≥ 1` 的 Sin 类型**快照（0 投资的类型不上行）。同 `(playerId, runId, sin)` 后波覆盖前波（upsert），库中始终保留该局该 Sin 的最深版本。单机崩溃 / 中途 Fail 无需补传。

请求体：

```json
{
  "playerId": "device-9f2c1d0e",
  "runId": "run-20260819-001",
  "snapshots": [
    {
      "sin": "lust",
      "monsterType": "色欲-灵念师",
      "bdCount": 3,
      "bdData": [{"cardId": "LU-S05", "stack": 2}, {"cardId": "LU-M03", "stack": 1}],
      "sourceWave": 6,
      "gameTime": 420,
      "stats": {"killedCount": 12}
    }
  ]
}
```

| 字段 | 必填 | 说明 |
|------|------|------|
| playerId | 是 | 设备特征码（前台生成并持久化，同设备 = 一个玩家） |
| runId | 是 | 每局一个（前台生成管理），upsert 唯一键组成 |
| snapshots[].sin | 是 | Sin 类型标识，upsert 唯一键组成 |
| snapshots[].monsterType | 是 | 怪物种类（透传，注入端还原用） |
| snapshots[].bdCount | 是 | BD 数量 = bdData 条目数（前台计算，服务器不解析 bdData） |
| snapshots[].bdData | 是 | BD 数据 JSON：卡 ID + 层数（结构由前台定义，原样透传） |
| snapshots[].sourceWave | 是 | 该 BD 数据所属波次（透传数值；Final 编码由前台决定） |
| snapshots[].gameTime | 否 | 游戏时间（透传，名人堂统计用） |
| snapshots[].stats | 否 | 其他统计字段（透传，筛选不读取） |

响应 `200`：`{ "ok": true, "accepted": 1 }`

> `accepted` 为实际入库条数。`sin`/`monsterType` 为空、`bdCount < 1`、`bdData` 缺失或为 `null` 的条目被**静默跳过**。
>
> **数据保留**：多局独立保留（不同 runId 互不覆盖）；每玩家上限与候选库全局 FIFO 上限由服务器参数控制（`-maxSnapshotsPerPlayer` / `-maxSnapshots`），超出按插入序淘汰最早快照。upsert 覆盖已有条目不占新额度。

### 4.2 请求投放 `POST /api/elite/pick`

**何时用**：第 N 波需要精英怪时，携带 `wave` + `playerId` 调用。

请求体：

```json
{ "playerId": "device-8a3b...", "wave": 5 }
```

| 字段 | 必填 | 校验 |
|------|------|------|
| playerId | 是 | 非空 |
| wave | 是 | `≥ 1`（当前波次 N） |

服务器筛选流程（四步）：

```text
(1) bdCount >= MIN_BD                     // 是否 BD 过（服务器参数，默认 1）
(2) sourceWave >= wave + WAVE_GAP         // 越级难度（服务器参数，默认 1）
(3) sourcePlayerId != playerId            // 他人（所有兜底层级均保持）
(4) bdCount 降序 -> TOP_BAND 高档内加权随机取 1
```

候选为空时三级兜底：

```text
1. WAVE_GAP 放宽到 0（允许同波次 BD 怪）          → relaxed: true
2. 全库 sourceWave 最高档中 bdCount 最大的一条     → relaxed: true
3. 本波不投放，返回 snapshot: null                 → 正常业务分支，非错误
```

响应 `200`（命中）：

```json
{
  "snapshot": {
    "snapshotId": 42,
    "sourcePlayerId": "device-9f2c1d0e",
    "runId": "run-20260819-001",
    "sin": "lust",
    "monsterType": "色欲-灵念师",
    "bdData": [{"cardId": "LU-S05", "stack": 2}, {"cardId": "LU-M03", "stack": 1}],
    "bdCount": 3,
    "sourceWave": 6,
    "gameTime": 420
  },
  "relaxed": false
}
```

响应 `200`（无候选）：

```json
{ "snapshot": null, "relaxed": false }
```

| 字段 | 说明 |
|------|------|
| snapshot | 投放快照；`null` = 本波不投放，回退普通波次 |
| snapshot.snapshotId | 快照 ID（数值） |
| snapshot.sourcePlayerId | 来源玩家设备特征码 |
| snapshot.bdData / gameTime / stats | 上传时的原文透传 |
| snapshot.bdCount / sourceWave / sin / monsterType / runId | 同上传定义 |
| relaxed | `true` = 命中放宽波次的兜底路径（仅观测用，前台无需处理） |

**注入端约定**（前台）：

- 解析快照 → 按 `monsterType` 与 `bdData` 还原精英怪；只携带该 Historical Build Snapshot，**不读当前 Run 任何 Card 层**（Canonical §23）；
- Elite 占用 Pressure Budget，Threat Cost 折算由前台 Director 结算；
- 遇未知 Card ID 建议静默跳过该卡（防解析失败，策划案 F11）；
- 抽到当前 Run 未解锁 Sin 的精英怪不算事故，筛选**不加**类型过滤（策划案 §8.4 拍板）。

---

## 五、典型调用序列

### 创作者：发布一张地图（UGC）

```text
编辑器序列化地图 → JSON 字节
→ POST /api/creations（type=map, fileData=base64(json)）
→ 保存返回的 creationId（可选，用于"我的作品"展示）
```

### 玩家：浏览并游玩 UGC 地图

```text
GET /api/creations?type=map&sortBy=downloads&descending=true   # 浏览热门
（或 GET /api/creations/search?keyword=地牢）                   # 搜索
→ 玩家点选 → GET /api/creations/{id}/download
→ base64 解码 fileData → 反序列化 JSON → 生成场景 → 游玩
→ （可选）POST /api/creations/{id}/rate 评分 / subscribe 订阅
```

### 玩家：每波选卡后滚动上传 BD 快照（精英怪）

```text
每波 Card 选择完成
→ 组装本局所有 bdCount >= 1 的 Sin 快照（卡 ID + 层数；0 投资不上行）
→ POST /api/bd-snapshots { playerId, runId, snapshots: [...] }
→ （崩溃 / Fail 无需补传：上一波上传已在库）
```

### 玩家：第 N 波请求精英怪投放

```text
W3+ 波需要 Elite 时（Encounter §7：W1–W2 不注入，W3 起 Eligible）
→ POST /api/elite/pick { playerId, wave: N }
→ snapshot 非空：注入为精英怪（携带 Historical Build Snapshot）
→ snapshot 为 null：本波不投放，回退普通波次
```

---

## 六、错误码

| HTTP 状态 | 场景 | 响应体 |
|-----------|------|--------|
| 400 | JSON 解析失败 / 必填字段缺失 / type 或 rating 非法 / playerId 缺失 / wave < 1 | `{"code":400,"msg":"..."}` |
| 404 | 下载的 creationId 不存在 | `{"code":404,"msg":"creation not found"}` |
| 405 | 方法不匹配（如 GET 打到 POST 路由） | Go 标准响应 |
| 500 | 文件落盘 / 数据库错误 | `{"code":500,"msg":"..."}` |

> `snapshot: null` 不是错误（见 4.2）。

---

## 七、Unity 对接示例

```csharp
// ========== UGC 上传（fileBytes 为地图 JSON 字节）==========
var form = new UploadCreationReq {
    creatorId   = LocalPlayerId,          // 本地持久化的匿名 ID
    creatorName = PlayerPrefs.GetString("PlayerName", "anonymous"),
    type        = "map",
    name        = mapName,
    fileName    = "map.json",
    fileData    = Convert.ToBase64String(fileBytes),
};
var req = new UnityWebRequest($"{baseURL}/api/creations", "POST");
req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(form)));
req.downloadHandler = new DownloadHandlerBuffer();
req.SetRequestHeader("Content-Type", "application/json");
await req.SendWebRequest();
// 注意：JsonUtility 不支持 []byte base64 与数组嵌套，建议用 Newtonsoft.Json

// ========== UGC 下载 ==========
var dl = UnityWebRequest.Get($"{baseURL}/api/creations/{creationId}/download");
await dl.SendWebRequest();
// 解析 JSON 后：byte[] fileBytes = Convert.FromBase64String(resp.fileData);

// ========== 精英怪：每波选卡后批量上传 BD 快照 ==========
var snap = new SnapshotUploadReq {
    playerId  = DeviceFingerprint,        // 设备特征码（同设备 = 一个玩家）
    runId     = CurrentRunId,             // 每局一个
    snapshots = sins.Where(s => s.BdCount >= 1).Select(s => new SnapIn {
        sin         = s.Sin,
        monsterType = s.MonsterType,
        bdCount     = s.Cards.Sum(c => 1),          // bdData 条目数
        bdData      = s.Cards.Select(c => new { cardId = c.Id, stack = c.Stack }),
        sourceWave  = CurrentWave,
        gameTime    = (long)GameTime,
    }).ToList(),
};
var up = new UnityWebRequest($"{baseURL}/api/bd-snapshots", "POST");
up.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snap)));
up.downloadHandler = new DownloadHandlerBuffer();
up.SetRequestHeader("Content-Type", "application/json");
await up.SendWebRequest();

// ========== 精英怪：第 N 波请求投放 ==========
var pickBody = JsonConvert.SerializeObject(new { playerId = DeviceFingerprint, wave = wave });
var pk = new UnityWebRequest($"{baseURL}/api/elite/pick", "POST");
pk.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(pickBody));
pk.downloadHandler = new DownloadHandlerBuffer();
pk.SetRequestHeader("Content-Type", "application/json");
await pk.SendWebRequest();
// resp.snapshot == null → 本波不投放（正常分支）
// resp.snapshot != null → 按 monsterType + bdData 还原精英怪注入
```

---

## 附：运行与测试

```bash
# 启动服务器（Go 1.22+）
go run .
# 或指定精英怪参数（全部 TUNABLE）
go run . -addr :8080 -minBd 1 -waveGap 1 -topBandMode percent -topBandPercent 0.2 -maxSnapshots 10000

# UGC 端到端测试（需先启动服务器）
go run ./test/ugctest

# 精英怪投放端到端测试（自包含，无需预启动服务器）
go run ./test/elitepicktest
```
