// Package sqlite SQLite 存储实现：单连接同时实现 ugc.Store 与 elite.EliteStore。
// schema 版本化管理见 migrations.go。
package sqlite

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

// New 创建 SQLite 存储。DSN 附带 PRAGMA：WAL（写不阻塞读，外部工具并发访问
// 不再 database is locked）+ busy_timeout 5s（锁竞争等待，与单连接串行化双保险）。
func New(path string) (*SQLiteStore, error) {
	if dir := filepath.Dir(path); dir != "." && dir != "" {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return nil, fmt.Errorf("create data dir: %w", err)
		}
	}
	dsn := path + "?_pragma=journal_mode(WAL)&_pragma=busy_timeout(5000)"
	db, err := sql.Open("sqlite", dsn)
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

// Close 关闭数据库。
func (s *SQLiteStore) Close() error { return s.db.Close() }
