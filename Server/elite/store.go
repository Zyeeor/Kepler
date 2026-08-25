// 精英域存储抽象与数据模型。
//
// 透传原则（策划案 §8.4/§8.6）：bdData 结构、sourceWave 语义与 Final 编码均由前台定义，
// 后台只存储不解析；筛选只依赖 bdCount 与 sourceWave 两个数值。
package elite

// BuildSnapshot BD 快照：其他玩家构筑出的怪物，作为精英怪投放候选。
type BuildSnapshot struct {
	ID          int64
	PlayerID    string // 来源玩家（客户端设备特征码，透传）
	RunID       string // 来源局 ID（upsert 唯一键组成，透传）
	Sin         string // 七宗罪类型标识（upsert 唯一键组成，透传）
	MonsterType string // 怪物种类（透传，客户端注入用）
	BDData      string // BD 数据 JSON 原文（卡 ID + 层数，结构由前台定义，后台不解析）
	BDCount     int    // BD 数量（代表 BD 深度；由客户端计算上报，后台不解析 bdData）
	SourceWave  int    // 该 BD 数据拍摄时的投放序号 = 上传者当时第几次投放精英怪（透传数值；语义/编码由前台决定，后台只做数值比较）
	GameTime    int64  // 游戏时间（透传，供名人堂统计，筛选不读取）
	Stats       string // 可选统计字段 JSON 原文（名人堂预留，筛选不读取）
	CreatedAt   int64
	UpdatedAt   int64
}

// EliteEvent 单条战果事件（客户端埋点上报）。
type EliteEvent struct {
	OwnerPlayerID string // 构筑主人（快照来源玩家，聚合键）
	OwnerRunID    string // 构筑主人的 Run ID（聚合键）
	Sin           string // 七宗罪 wire 名（聚合键）
	Type          string // spawned / fatal / possessed / bodyFatal / runFail
	SnapshotID    int64  // 投放命中的快照 ID（观测用，不参与聚合键）
	ReporterID    string // 回报玩家（观测用，不参与聚合键）
	Wave          int    // 事件发生时的投放序号 = 当时第几次投放精英怪（观测用，透传）
	GameTime      int64  // 事件发生游戏时间（观测用，透传）
}

// EliteBuildStats 构筑主人的异步战绩聚合（荣誉殿堂「异步战绩」字段的数据源，§5.4/§5.8）。
type EliteBuildStats struct {
	OwnerPlayerID string
	OwnerRunID    string
	Sin           string
	Deployed      int   // 被投放次数（spawned）
	Fatal         int   // 被其他玩家击杀次数（fatal）
	Possessed     int   // 被其他玩家 Possess 次数（possessed）
	BodyFatal     int   // 造成 Body Fatal 次数（bodyFatal）
	RunFail       int   // 直接导致 Run Fail 次数（runFail）
	UpdatedAt     int64
}

// LeaderboardEntry 荣誉殿堂排行榜条目：BD 快照（怪物与构筑）+ 聚合战绩
// （elite_build_stats INNER JOIN monster_build_snapshots 的结果行）。
type LeaderboardEntry struct {
	SnapshotID    int64
	OwnerPlayerID string
	OwnerRunID    string
	Sin           string
	MonsterType   string
	BDData        string // BD 数据 JSON 原文（透传，前台展示构筑用）
	BDCount       int
	SourceWave    int
	Deployed      int
	Fatal         int
	Possessed     int
	BodyFatal     int // 排序主键：击杀玩家（Body Fatal）次数
	RunFail       int
	UpdatedAt     int64
}

// EliteStore 精英怪 BD 快照存储接口。
type EliteStore interface {
	// UpsertSnapshots 批量 upsert：同 (player_id, run_id, sin) 后波覆盖前波（§8.1）。
	UpsertSnapshots(snaps []*BuildSnapshot) error
	// CountSnapshots 候选库总条数。
	CountSnapshots() (int, error)
	// TrimOldestSnapshots 全局 FIFO：保留最新 keep 条，返回删除条数（§8.2）。
	TrimOldestSnapshots(keep int) (int, error)
	// CountSnapshotsByPlayer 指定玩家快照条数。
	CountSnapshotsByPlayer(playerID string) (int, error)
	// TrimOldestSnapshotsByPlayer 每玩家上限：保留该玩家最新 keep 条，返回删除条数（§8.4）。
	TrimOldestSnapshotsByPlayer(playerID string, keep int) (int, error)
	// PickCandidates 筛选候选（§3 Step 1–3）：bdCount >= minBD 且 sourceWave >= minWave
	// 且非请求者，按 bdCount 降序。LIMIT 为 Demo 规模保护（TOP_BAND 只关心最高档）。
	PickCandidates(minBD, minWave int, excludePlayerID string) ([]*BuildSnapshot, error)
	// TopWaveCandidates 兜底候选（§5）：全库（排除请求者、bdCount >= minBD）中
	// sourceWave 等于全库最高值的条目，按 bdCount 降序。
	TopWaveCandidates(minBD int, excludePlayerID string) ([]*BuildSnapshot, error)
	// ListAllSnapshots 全库快照（按 id 升序；userBD 目录导入的内容指纹去重用，
	// 受全局容量上限约束，规模可控）。
	ListAllSnapshots() ([]*BuildSnapshot, error)

	// RecordEliteEvents 战果回传（策划案 §6.5）：精英在他人游戏中的战果事件，
	// 按构筑主人 (owner_player_id, owner_run_id, sin) 聚合计数。返回聚合写入条数。
	RecordEliteEvents(events []*EliteEvent) (int, error)
	// GetEliteBuildStats 查询构筑主人的异步战绩聚合（荣誉殿堂 §5.4 字段的数据源）。
	GetEliteBuildStats(ownerPlayerID string) ([]*EliteBuildStats, error)
	// Leaderboard 荣誉殿堂排行榜（§5.4/§5.8）：按击杀玩家次数（body_fatal）降序取
	// Top limit，INNER JOIN 快照表（悬空聚合行不上榜）。
	Leaderboard(limit int) ([]*LeaderboardEntry, error)
}
