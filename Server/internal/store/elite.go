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
