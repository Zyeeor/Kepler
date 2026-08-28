// Run Analytics（整局运行数据）存储：run_stats 主表 + run_stats_per_sin 子表。
//
// 与精英快照库分开（不同生命周期：对局记录长期保留，不参与 FIFO 淘汰——
// RunStats_后端对接文档.md §4.2-3）。幂等：主表 (player_id, run_id) 主键
// upsert 覆盖重写；perSin 子表同事务内删旧插新。
package sqlite

import (
	"fmt"

	"possession/server/internal/runstats"
)

// runStatsMigrateStmts V2：Run Analytics 两表（RunStats_后端对接文档.md §4.2）。
var runStatsMigrateStmts = []string{
	`CREATE TABLE IF NOT EXISTS run_stats (
		player_id               TEXT    NOT NULL,
		run_id                  TEXT    NOT NULL,
		schema_version          INTEGER NOT NULL DEFAULT 1,
		started_at_unix         INTEGER NOT NULL DEFAULT 0,
		ended_at_unix           INTEGER NOT NULL DEFAULT 0,
		run_duration_seconds    REAL    NOT NULL DEFAULT 0,
		won                     INTEGER NOT NULL DEFAULT 0,
		end_phase               TEXT    NOT NULL DEFAULT '',
		reached_wave_index      INTEGER NOT NULL DEFAULT -1,
		final_reached           INTEGER NOT NULL DEFAULT 0,
		final_completed         INTEGER NOT NULL DEFAULT 0,
		total_possessions       INTEGER NOT NULL DEFAULT 0,
		voluntary_releases      INTEGER NOT NULL DEFAULT 0,
		death_relays            INTEGER NOT NULL DEFAULT 0,
		soul_enters             INTEGER NOT NULL DEFAULT 0,
		shrine_recovers         INTEGER NOT NULL DEFAULT 0,
		low_health_releases     INTEGER NOT NULL DEFAULT 0,
		bullet_time_count       INTEGER NOT NULL DEFAULT 0,
		bullet_time_total_seconds REAL  NOT NULL DEFAULT 0,
		elite_fatal_count       INTEGER NOT NULL DEFAULT 0,
		elite_possession_count  INTEGER NOT NULL DEFAULT 0,
		distinct_sins_used      INTEGER NOT NULL DEFAULT 0,
		total_kills             INTEGER NOT NULL DEFAULT 0,
		uploaded_at_unix        INTEGER NOT NULL,
		PRIMARY KEY (player_id, run_id)
	)`,
	`CREATE INDEX IF NOT EXISTS idx_run_stats_player_time ON run_stats(player_id, uploaded_at_unix DESC)`,
	`CREATE TABLE IF NOT EXISTS run_stats_per_sin (
		player_id              TEXT    NOT NULL,
		run_id                 TEXT    NOT NULL,
		sin                    TEXT    NOT NULL,
		control_seconds        REAL    NOT NULL DEFAULT 0,
		possession_count       INTEGER NOT NULL DEFAULT 0,
		movement_count         INTEGER NOT NULL DEFAULT 0,
		attack_count           INTEGER NOT NULL DEFAULT 0,
		special_count          INTEGER NOT NULL DEFAULT 0,
		card_investment_count  INTEGER NOT NULL DEFAULT 0,
		kills                  INTEGER NOT NULL DEFAULT 0,
		PRIMARY KEY (player_id, run_id, sin)
	)`,
}

// UpsertRun 幂等写入一局 RunStats（主表 upsert + perSin 覆盖重写，同一事务）。
func (s *SQLiteStore) UpsertRun(rec *runstats.Record) error {
	tx, err := s.db.Begin()
	if err != nil {
		return fmt.Errorf("runstats upsert: begin: %w", err)
	}
	defer tx.Rollback()

	r := &rec.UploadRequest
	if _, err := tx.Exec(`INSERT INTO run_stats (
		player_id, run_id, schema_version,
		started_at_unix, ended_at_unix, run_duration_seconds,
		won, end_phase, reached_wave_index, final_reached, final_completed,
		total_possessions, voluntary_releases, death_relays, soul_enters,
		shrine_recovers, low_health_releases, bullet_time_count,
		bullet_time_total_seconds, elite_fatal_count, elite_possession_count,
		distinct_sins_used, total_kills, uploaded_at_unix
	) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
	ON CONFLICT(player_id, run_id) DO UPDATE SET
		schema_version          = excluded.schema_version,
		started_at_unix         = excluded.started_at_unix,
		ended_at_unix           = excluded.ended_at_unix,
		run_duration_seconds    = excluded.run_duration_seconds,
		won                     = excluded.won,
		end_phase               = excluded.end_phase,
		reached_wave_index      = excluded.reached_wave_index,
		final_reached           = excluded.final_reached,
		final_completed         = excluded.final_completed,
		total_possessions       = excluded.total_possessions,
		voluntary_releases      = excluded.voluntary_releases,
		death_relays            = excluded.death_relays,
		soul_enters             = excluded.soul_enters,
		shrine_recovers         = excluded.shrine_recovers,
		low_health_releases     = excluded.low_health_releases,
		bullet_time_count       = excluded.bullet_time_count,
		bullet_time_total_seconds = excluded.bullet_time_total_seconds,
		elite_fatal_count       = excluded.elite_fatal_count,
		elite_possession_count  = excluded.elite_possession_count,
		distinct_sins_used      = excluded.distinct_sins_used,
		total_kills             = excluded.total_kills,
		uploaded_at_unix        = excluded.uploaded_at_unix`,
		r.PlayerID, r.RunID, r.SchemaVersion,
		r.StartedAtUnix, r.EndedAtUnix, r.RunDurationSeconds,
		r.Won, r.EndPhase, r.ReachedWaveIndex, r.FinalReached, r.FinalCompleted,
		r.TotalPossessions, r.VoluntaryReleases, r.DeathRelays, r.SoulEnters,
		r.ShrineRecovers, r.LowHealthReleases, r.BulletTimeCount,
		r.BulletTimeTotalSeconds, r.EliteFatalCount, r.ElitePossessionCount,
		r.DistinctSinsUsed, r.TotalKills, rec.UploadedAtUnix,
	); err != nil {
		return fmt.Errorf("runstats upsert: main: %w", err)
	}

	// perSin 覆盖重写：先删旧再插新（同 runId 重传时子表整体替换）
	if _, err := tx.Exec(`DELETE FROM run_stats_per_sin WHERE player_id=? AND run_id=?`,
		r.PlayerID, r.RunID); err != nil {
		return fmt.Errorf("runstats upsert: clear perSin: %w", err)
	}
	for _, ps := range r.PerSin {
		if _, err := tx.Exec(`INSERT INTO run_stats_per_sin (
			player_id, run_id, sin,
			control_seconds, possession_count, movement_count, attack_count,
			special_count, card_investment_count, kills
		) VALUES (?,?,?,?,?,?,?,?,?,?)`,
			r.PlayerID, r.RunID, ps.Sin,
			ps.ControlSeconds, ps.PossessionCount, ps.MovementCount, ps.AttackCount,
			ps.SpecialCount, ps.CardInvestmentCount, ps.Kills,
		); err != nil {
			return fmt.Errorf("runstats upsert: perSin(%s): %w", ps.Sin, err)
		}
	}

	return tx.Commit()
}
