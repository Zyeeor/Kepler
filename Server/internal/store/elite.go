// Package store — 精英怪 BD 快照存储（他人 BD 怪物投放候选库）。
//
// 设计依据：《精英怪筛选 — 他人 BD 怪物投放 · 策划案》§2/§4/§5/§8。
// 透传原则（§8.4/§8.6）：bdData 结构、sourceWave 语义与 Final 编码均由前台定义，
// 后台只存储不解析；筛选只依赖 bdCount 与 sourceWave 两个数值。
package store

import (
	"database/sql"
	"time"
)

// BuildSnapshot BD 快照：其他玩家构筑出的怪物，作为精英怪投放候选。
type BuildSnapshot struct {
	ID          int64
	PlayerID    string // 来源玩家（客户端设备特征码，透传）
	RunID       string // 来源局 ID（upsert 唯一键组成，透传）
	Sin         string // 七宗罪类型标识（upsert 唯一键组成，透传）
	MonsterType string // 怪物种类（透传，客户端注入用）
	BDData      string // BD 数据 JSON 原文（卡 ID + 层数，结构由前台定义，后台不解析）
	BDCount     int    // BD 数量（代表 BD 深度；由客户端计算上报，后台不解析 bdData）
	SourceWave  int    // 该 BD 数据所属波次（透传数值；语义/编码由前台决定，后台只做数值比较）
	GameTime    int64  // 游戏时间（透传，供名人堂统计，筛选不读取）
	Stats       string // 可选统计字段 JSON 原文（名人堂预留，筛选不读取）
	CreatedAt   int64
	UpdatedAt   int64
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
}

// ============================================================================
// 战果回传（策划案 §6.5）：精英在他人游戏中的战果事件 → 按构筑主人聚合
// ============================================================================

// EliteEvent 单条战果事件（客户端埋点上报）。
type EliteEvent struct {
	OwnerPlayerID string // 构筑主人（快照来源玩家，聚合键）
	OwnerRunID    string // 构筑主人的 Run ID（聚合键）
	Sin           string // 七宗罪 wire 名（聚合键）
	Type          string // spawned / fatal / possessed / bodyFatal / runFail
	SnapshotID    int64  // 投放命中的快照 ID（观测用，不参与聚合键）
	ReporterID    string // 回报玩家（观测用，不参与聚合键）
	Wave          int    // 事件发生波次（观测用，透传）
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

// eliteStatsMigrateStmts 战绩聚合表建表语句（由 store.go 的 migrate 统一执行）。
var eliteStatsMigrateStmts = []string{
	`CREATE TABLE IF NOT EXISTS elite_build_stats (
		owner_player_id TEXT NOT NULL,
		owner_run_id    TEXT NOT NULL,
		sin             TEXT NOT NULL,
		deployed        INTEGER NOT NULL DEFAULT 0,
		fatal           INTEGER NOT NULL DEFAULT 0,
		possessed       INTEGER NOT NULL DEFAULT 0,
		body_fatal      INTEGER NOT NULL DEFAULT 0,
		run_fail        INTEGER NOT NULL DEFAULT 0,
		updated_at      INTEGER NOT NULL,
		UNIQUE(owner_player_id, owner_run_id, sin)
	)`,
	`CREATE INDEX IF NOT EXISTS idx_elite_stats_owner ON elite_build_stats(owner_player_id)`,
}

// RecordEliteEvents 批量聚合战果事件（事务）：每条事件按类型对对应计数器 +1，
// 不存在则插入（初值 1）。返回聚合写入条数。
func (s *SQLiteStore) RecordEliteEvents(events []*EliteEvent) (int, error) {
	if len(events) == 0 {
		return 0, nil
	}
	tx, err := s.db.Begin()
	if err != nil {
		return 0, err
	}
	defer tx.Rollback()

	const upsert = `
		INSERT INTO elite_build_stats (owner_player_id, owner_run_id, sin, deployed, fatal, possessed, body_fatal, run_fail, updated_at)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
		ON CONFLICT(owner_player_id, owner_run_id, sin) DO UPDATE SET
			deployed   = deployed + excluded.deployed,
			fatal      = fatal + excluded.fatal,
			possessed  = possessed + excluded.possessed,
			body_fatal = body_fatal + excluded.body_fatal,
			run_fail   = run_fail + excluded.run_fail,
			updated_at = excluded.updated_at`
	stmt, err := tx.Prepare(upsert)
	if err != nil {
		return 0, err
	}
	defer stmt.Close()

	now := time.Now().Unix()
	for _, e := range events {
		var deployed, fatal, possessed, bodyFatal, runFail int
		switch e.Type {
		case "spawned":
			deployed = 1
		case "fatal":
			fatal = 1
		case "possessed":
			possessed = 1
		case "bodyFatal":
			bodyFatal = 1
		case "runFail":
			runFail = 1
		default:
			continue // 未知类型逐条跳过（服务层已校验，存储层防御）
		}
		if _, err := stmt.Exec(
			e.OwnerPlayerID, e.OwnerRunID, e.Sin,
			deployed, fatal, possessed, bodyFatal, runFail, now,
		); err != nil {
			return 0, err
		}
	}
	return len(events), tx.Commit()
}

// GetEliteBuildStats 查询构筑主人的战绩聚合（按更新时间降序）。
func (s *SQLiteStore) GetEliteBuildStats(ownerPlayerID string) ([]*EliteBuildStats, error) {
	rows, err := s.db.Query(`
		SELECT owner_player_id, owner_run_id, sin, deployed, fatal, possessed, body_fatal, run_fail, updated_at
		FROM elite_build_stats
		WHERE owner_player_id = ?
		ORDER BY updated_at DESC`, ownerPlayerID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var out []*EliteBuildStats
	for rows.Next() {
		var st EliteBuildStats
		if err := rows.Scan(
			&st.OwnerPlayerID, &st.OwnerRunID, &st.Sin,
			&st.Deployed, &st.Fatal, &st.Possessed, &st.BodyFatal, &st.RunFail, &st.UpdatedAt,
		); err != nil {
			return nil, err
		}
		out = append(out, &st)
	}
	return out, rows.Err()
}

// snapshotColumns 快照查询列。
const snapshotColumns = `id, player_id, run_id, sin, monster_type, bd_data, bd_count, source_wave, game_time, stats, created_at, updated_at`

// snapshotMigrateStmts 快照表建表语句（由 store.go 的 migrate 统一执行）。
var snapshotMigrateStmts = []string{
	`CREATE TABLE IF NOT EXISTS monster_build_snapshots (
		id           INTEGER PRIMARY KEY AUTOINCREMENT,
		player_id    TEXT NOT NULL,
		run_id       TEXT NOT NULL,
		sin          TEXT NOT NULL,
		monster_type TEXT NOT NULL,
		bd_data      TEXT NOT NULL,
		bd_count     INTEGER NOT NULL,
		source_wave  INTEGER NOT NULL,
		game_time    INTEGER NOT NULL DEFAULT 0,
		stats        TEXT,
		created_at   INTEGER NOT NULL,
		updated_at   INTEGER NOT NULL,
		UNIQUE(player_id, run_id, sin)
	)`,
	`CREATE INDEX IF NOT EXISTS idx_snapshots_pick ON monster_build_snapshots(bd_count, source_wave)`,
	`CREATE INDEX IF NOT EXISTS idx_snapshots_player ON monster_build_snapshots(player_id)`,
}

// UpsertSnapshots 批量 upsert 快照（事务）。
func (s *SQLiteStore) UpsertSnapshots(snaps []*BuildSnapshot) error {
	if len(snaps) == 0 {
		return nil
	}
	tx, err := s.db.Begin()
	if err != nil {
		return err
	}
	defer tx.Rollback()

	stmt, err := tx.Prepare(`
		INSERT INTO monster_build_snapshots
			(player_id, run_id, sin, monster_type, bd_data, bd_count, source_wave, game_time, stats, created_at, updated_at)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		ON CONFLICT(player_id, run_id, sin) DO UPDATE SET
			monster_type = excluded.monster_type,
			bd_data      = excluded.bd_data,
			bd_count     = excluded.bd_count,
			source_wave  = excluded.source_wave,
			game_time    = excluded.game_time,
			stats        = excluded.stats,
			updated_at   = excluded.updated_at`)
	if err != nil {
		return err
	}
	defer stmt.Close()

	now := time.Now().Unix()
	for _, snap := range snaps {
		if _, err := stmt.Exec(
			snap.PlayerID, snap.RunID, snap.Sin, snap.MonsterType,
			snap.BDData, snap.BDCount, snap.SourceWave, snap.GameTime, snap.Stats, now, now,
		); err != nil {
			return err
		}
	}
	return tx.Commit()
}

// CountSnapshots 候选库总条数。
func (s *SQLiteStore) CountSnapshots() (int, error) {
	var count int
	err := s.db.QueryRow(`SELECT COUNT(*) FROM monster_build_snapshots`).Scan(&count)
	return count, err
}

// TrimOldestSnapshots 全局 FIFO：按插入序（id 升序）淘汰最旧快照，保留最新 keep 条。
func (s *SQLiteStore) TrimOldestSnapshots(keep int) (int, error) {
	// ORDER BY id DESC：跳过最新 keep 条，删除其余更早的。
	res, err := s.db.Exec(`
		DELETE FROM monster_build_snapshots WHERE id IN (
			SELECT id FROM monster_build_snapshots ORDER BY id DESC LIMIT -1 OFFSET ?)`, keep)
	if err != nil {
		return 0, err
	}
	n, _ := res.RowsAffected()
	return int(n), nil
}

// CountSnapshotsByPlayer 指定玩家快照条数。
func (s *SQLiteStore) CountSnapshotsByPlayer(playerID string) (int, error) {
	var count int
	err := s.db.QueryRow(`SELECT COUNT(*) FROM monster_build_snapshots WHERE player_id = ?`, playerID).Scan(&count)
	return count, err
}

// TrimOldestSnapshotsByPlayer 每玩家上限：按插入序淘汰该玩家最旧快照，保留最新 keep 条。
func (s *SQLiteStore) TrimOldestSnapshotsByPlayer(playerID string, keep int) (int, error) {
	// ORDER BY id DESC：跳过该玩家最新 keep 条，删除其余更早的。
	res, err := s.db.Exec(`
		DELETE FROM monster_build_snapshots WHERE id IN (
			SELECT id FROM monster_build_snapshots WHERE player_id = ? ORDER BY id DESC LIMIT -1 OFFSET ?)`,
		playerID, keep)
	if err != nil {
		return 0, err
	}
	n, _ := res.RowsAffected()
	return int(n), nil
}

// PickCandidates 筛选候选（§3 Step 1–3）。
func (s *SQLiteStore) PickCandidates(minBD, minWave int, excludePlayerID string) ([]*BuildSnapshot, error) {
	rows, err := s.db.Query(`
		SELECT `+snapshotColumns+` FROM monster_build_snapshots
		WHERE bd_count >= ? AND source_wave >= ? AND player_id != ?
		ORDER BY bd_count DESC, id ASC
		LIMIT 1000`, minBD, minWave, excludePlayerID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	return scanSnapshots(rows)
}

// TopWaveCandidates 兜底候选（§5）：sourceWave 最高档中 bdCount 降序。
func (s *SQLiteStore) TopWaveCandidates(minBD int, excludePlayerID string) ([]*BuildSnapshot, error) {
	rows, err := s.db.Query(`
		SELECT `+snapshotColumns+` FROM monster_build_snapshots
		WHERE bd_count >= ? AND player_id != ?
		  AND source_wave = (SELECT MAX(source_wave) FROM monster_build_snapshots WHERE player_id != ?)
		ORDER BY bd_count DESC, id ASC
		LIMIT 1000`, minBD, excludePlayerID, excludePlayerID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	return scanSnapshots(rows)
}

// ListAllSnapshots 全库快照（按 id 升序）。
func (s *SQLiteStore) ListAllSnapshots() ([]*BuildSnapshot, error) {
	rows, err := s.db.Query(`SELECT ` + snapshotColumns + ` FROM monster_build_snapshots ORDER BY id ASC`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	return scanSnapshots(rows)
}

// scanSnapshots 扫描快照查询结果。
func scanSnapshots(rows *sql.Rows) ([]*BuildSnapshot, error) {
	var out []*BuildSnapshot
	for rows.Next() {
		var snap BuildSnapshot
		if err := rows.Scan(
			&snap.ID, &snap.PlayerID, &snap.RunID, &snap.Sin, &snap.MonsterType,
			&snap.BDData, &snap.BDCount, &snap.SourceWave, &snap.GameTime, &snap.Stats,
			&snap.CreatedAt, &snap.UpdatedAt,
		); err != nil {
			return nil, err
		}
		out = append(out, &snap)
	}
	return out, rows.Err()
}
