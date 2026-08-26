// Schema Migration（版本化管理）。
//
// 每个版本一个 migration（建表 / 加列 / 索引），按 version 升序执行未应用的版本，
// 应用成功后写入 schema_migrations 表。存量库（版本表引入前直接建表的旧 data/game.db）
// 通过 V1 的 IF NOT EXISTS 幂等语句无损收敛到版本 1。
//
// 新增 schema 变更的规则：只追加新版本（version 递增），不修改已发布的历史版本语句。
package sqlite

import (
	"fmt"
	"time"
)

// migration 单个 schema 版本：version 唯一递增 + DDL 语句列表。
type migration struct {
	version int
	desc    string   // 变更描述（日志与维护用）
	stmts   []string // DDL 语句（同一事务内按序执行）
}

// migrations 全部 schema 版本（升序）。V1 = 初始建表（与版本化引入前的 migrate 输出一致）。
var migrations = []migration{
	{
		version: 1,
		desc:    "initial schema: ugc + elite tables",
		// UGC 域表（见 ugc.go）
		// 精英域表（见 elite.go）
		stmts: append(append([]string{}, ugcMigrateStmts...), eliteMigrateStmts...),
	},
}

// migrate 执行未应用的 schema 版本（幂等：已应用版本跳过）。
func (s *SQLiteStore) migrate() error {
	if _, err := s.db.Exec(`CREATE TABLE IF NOT EXISTS schema_migrations (
		version    INTEGER PRIMARY KEY,
		applied_at INTEGER NOT NULL
	)`); err != nil {
		return fmt.Errorf("migrate: create schema_migrations: %w", err)
	}

	var current int
	if err := s.db.QueryRow(`SELECT COALESCE(MAX(version), 0) FROM schema_migrations`).Scan(&current); err != nil {
		return fmt.Errorf("migrate: read schema version: %w", err)
	}

	for _, m := range migrations {
		if m.version <= current {
			continue
		}
		if err := s.applyMigration(m); err != nil {
			return err
		}
	}
	return nil
}

// applyMigration 在单个事务内执行一个版本的 DDL 并记录版本号（失败整体回滚）。
func (s *SQLiteStore) applyMigration(m migration) error {
	tx, err := s.db.Begin()
	if err != nil {
		return fmt.Errorf("migrate v%d: %w", m.version, err)
	}
	defer tx.Rollback()

	for _, stmt := range m.stmts {
		if _, err := tx.Exec(stmt); err != nil {
			return fmt.Errorf("migrate v%d (%s): %w", m.version, m.desc, err)
		}
	}
	if _, err := tx.Exec(
		`INSERT INTO schema_migrations (version, applied_at) VALUES (?, ?)`,
		m.version, time.Now().Unix(),
	); err != nil {
		return fmt.Errorf("migrate v%d: record version: %w", m.version, err)
	}
	return tx.Commit()
}
