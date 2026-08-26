// 精英域 SQLite 实现（实现 elite.EliteStore）：BD 快照候选库 + 战果回传聚合。
package sqlite

import (
	"database/sql"
	"time"

	"possession/server/internal/elite"
)

// eliteMigrateStmts 精英域建表语句（由 sqlite.go 的 migrate 统一执行）。
var eliteMigrateStmts = []string{
	// 精英怪 BD 快照表（他人 BD 怪物投放候选库）
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

	// 精英怪战果回传聚合表（策划案 §6.5）
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
	// 排行榜查询（§5.4/§5.8）：部分索引完全覆盖 WHERE body_fatal > 0 + ORDER BY 四键，
	// Top-N 免排序扫描（驱动表按索引取前 N 行再按唯一键 JOIN 快照表）。
	`CREATE INDEX IF NOT EXISTS idx_elite_stats_lb
		ON elite_build_stats(body_fatal DESC, run_fail DESC, deployed DESC, updated_at DESC)
		WHERE body_fatal > 0`,
}

// snapshotColumns 快照查询列。
const snapshotColumns = `id, player_id, run_id, sin, monster_type, bd_data, bd_count, source_wave, game_time, stats, created_at, updated_at`

// UpsertSnapshots 批量 upsert 快照（事务）。
func (s *SQLiteStore) UpsertSnapshots(snaps []*elite.BuildSnapshot) error {
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
func (s *SQLiteStore) PickCandidates(minBD, minWave int, excludePlayerID string) ([]*elite.BuildSnapshot, error) {
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

// TopWaveCandidates 兜底候选（§5）：sourceWave（投放序号）最高档中 bdCount 降序。
func (s *SQLiteStore) TopWaveCandidates(minBD int, excludePlayerID string) ([]*elite.BuildSnapshot, error) {
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
func (s *SQLiteStore) ListAllSnapshots() ([]*elite.BuildSnapshot, error) {
	rows, err := s.db.Query(`SELECT ` + snapshotColumns + ` FROM monster_build_snapshots ORDER BY id ASC`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	return scanSnapshots(rows)
}

// scanSnapshots 扫描快照查询结果。
func scanSnapshots(rows *sql.Rows) ([]*elite.BuildSnapshot, error) {
	out := make([]*elite.BuildSnapshot, 0, 64)
	for rows.Next() {
		var snap elite.BuildSnapshot
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

// ============================================================================
// 战果回传（策划案 §6.5）：精英在他人游戏中的战果事件 → 按构筑主人聚合
// ============================================================================

// RecordEliteEvents 批量聚合战果事件（事务）：每条事件按类型对对应计数器 +1，
// 不存在则插入（初值 1）。返回聚合写入条数。
func (s *SQLiteStore) RecordEliteEvents(events []*elite.EliteEvent) (int, error) {
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
func (s *SQLiteStore) GetEliteBuildStats(ownerPlayerID string) ([]*elite.EliteBuildStats, error) {
	rows, err := s.db.Query(`
		SELECT owner_player_id, owner_run_id, sin, deployed, fatal, possessed, body_fatal, run_fail, updated_at
		FROM elite_build_stats
		WHERE owner_player_id = ?
		ORDER BY updated_at DESC`, ownerPlayerID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	var out []*elite.EliteBuildStats
	for rows.Next() {
		var st elite.EliteBuildStats
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

// Leaderboard 荣誉殿堂排行榜（§5.4/§5.8 Top N 视图）：elite_build_stats 按
// (owner, run, sin) 业务键 JOIN monster_build_snapshots，按击杀玩家次数
// （body_fatal）降序取 Top limit。INNER JOIN——被容量治理淘汰的悬空聚合行不上榜
// （怪物与构筑信息已不可考）。tie-break：run_fail → deployed → updated_at。
func (s *SQLiteStore) Leaderboard(limit int) ([]*elite.LeaderboardEntry, error) {
	rows, err := s.db.Query(`
		SELECT s.id, s.player_id, s.run_id, s.sin, s.monster_type, s.bd_data, s.bd_count, s.source_wave,
		       e.deployed, e.fatal, e.possessed, e.body_fatal, e.run_fail, e.updated_at
		FROM elite_build_stats e
		JOIN monster_build_snapshots s
		  ON s.player_id = e.owner_player_id AND s.run_id = e.owner_run_id AND s.sin = e.sin
		WHERE e.body_fatal > 0
		ORDER BY e.body_fatal DESC, e.run_fail DESC, e.deployed DESC, e.updated_at DESC
		LIMIT ?`, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	out := make([]*elite.LeaderboardEntry, 0, 32)
	for rows.Next() {
		var e elite.LeaderboardEntry
		if err := rows.Scan(
			&e.SnapshotID, &e.OwnerPlayerID, &e.OwnerRunID, &e.Sin, &e.MonsterType,
			&e.BDData, &e.BDCount, &e.SourceWave,
			&e.Deployed, &e.Fatal, &e.Possessed, &e.BodyFatal, &e.RunFail, &e.UpdatedAt,
		); err != nil {
			return nil, err
		}
		out = append(out, &e)
	}
	return out, rows.Err()
}
