// Package store — SQLite 存储实现：单连接同时实现 ugc.Store 与 elite.EliteStore。
package store

import (
	"database/sql"
	"fmt"
	"os"
	"path/filepath"

	_ "modernc.org/sqlite"
)

// SQLiteStore SQLite 存储实现（单连接，SetMaxOpenConns(1) 规避写锁竞争）。
type SQLiteStore struct {
	db *sql.DB
}

// NewSQLite 创建 SQLite 存储。
func NewSQLite(path string) (*SQLiteStore, error) {
	if dir := filepath.Dir(path); dir != "." && dir != "" {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return nil, fmt.Errorf("create data dir: %w", err)
		}
	}
	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, fmt.Errorf("open db: %w", err)
	}
	db.SetMaxOpenConns(1)

	s := &SQLiteStore{db: db}
	if err := s.migrate(); err != nil {
		db.Close()
		return nil, err
	}
	return s, nil
}

func (s *SQLiteStore) migrate() error {
	stmts := []string{}

	// UGC 域表（见 ugc.go）
	stmts = append(stmts, ugcMigrateStmts...)

	// 精英域表（见 elite.go）
	stmts = append(stmts, eliteMigrateStmts...)

	for _, stmt := range stmts {
		if _, err := s.db.Exec(stmt); err != nil {
			return fmt.Errorf("migrate: %w", err)
		}
	}
	return nil
}

// Close 关闭数据库。
func (s *SQLiteStore) Close() error { return s.db.Close() }
