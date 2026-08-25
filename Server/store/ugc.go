// UGC 域 SQLite 实现（实现 ugc.Store）。
package store

import (
	"database/sql"
	"encoding/json"
	"errors"
	"time"

	"demo/server/ugc"
)

// ugcMigrateStmts UGC 域建表语句（由 sqlite.go 的 migrate 统一执行）。
var ugcMigrateStmts = []string{
	// UGC 创作表
	`CREATE TABLE IF NOT EXISTS creations (
		id            TEXT PRIMARY KEY,
		creator_id    TEXT NOT NULL,
		creator_name  TEXT NOT NULL,
		type          TEXT NOT NULL,
		name          TEXT NOT NULL,
		description   TEXT,
		tags          TEXT,  -- JSON array
		file_url      TEXT,
		thumbnail_url TEXT,
		status        TEXT DEFAULT 'draft',
		downloads     INTEGER DEFAULT 0,
		likes         INTEGER DEFAULT 0,
		rating        REAL DEFAULT 0,
		version       INTEGER DEFAULT 1,
		created_at    INTEGER NOT NULL,
		updated_at    INTEGER NOT NULL
	)`,
	`CREATE INDEX IF NOT EXISTS idx_creations_type_status ON creations(type, status)`,
	`CREATE INDEX IF NOT EXISTS idx_creations_creator ON creations(creator_id)`,

	// 订阅表
	`CREATE TABLE IF NOT EXISTS subscriptions (
		id          INTEGER PRIMARY KEY AUTOINCREMENT,
		player_id   TEXT NOT NULL,
		creation_id TEXT NOT NULL,
		created_at  INTEGER NOT NULL,
		UNIQUE(player_id, creation_id)
	)`,

	// 评分表
	`CREATE TABLE IF NOT EXISTS creation_reviews (
		id          INTEGER PRIMARY KEY AUTOINCREMENT,
		creation_id TEXT NOT NULL,
		player_id   TEXT NOT NULL,
		rating      INTEGER NOT NULL,
		comment     TEXT,
		created_at  INTEGER NOT NULL,
		UNIQUE(creation_id, player_id)
	)`,
}

// CreateCreation 创建 UGC 内容。
func (s *SQLiteStore) CreateCreation(c *ugc.Creation) error {
	tagsJSON, _ := json.Marshal(c.Tags)
	now := time.Now().Unix()
	_, err := s.db.Exec(`
		INSERT INTO creations (id, creator_id, creator_name, type, name, description, tags, file_url, thumbnail_url, status, downloads, likes, rating, version, created_at, updated_at)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, 0, 0, 1, ?, ?)`,
		c.ID, c.CreatorID, c.CreatorName, c.Type, c.Name, c.Description, string(tagsJSON), c.FileURL, c.ThumbnailURL, c.Status, now, now,
	)
	return err
}

// GetCreation 获取 UGC 内容。
func (s *SQLiteStore) GetCreation(id string) (*ugc.Creation, error) {
	var c ugc.Creation
	var tagsJSON string
	err := s.db.QueryRow(`
		SELECT id, creator_id, creator_name, type, name, description, tags, file_url, thumbnail_url, status, downloads, likes, rating, version, created_at, updated_at
		FROM creations WHERE id = ?`, id).Scan(
		&c.ID, &c.CreatorID, &c.CreatorName, &c.Type, &c.Name, &c.Description, &tagsJSON, &c.FileURL, &c.ThumbnailURL, &c.Status, &c.Downloads, &c.Likes, &c.Rating, &c.Version, &c.CreatedAt, &c.UpdatedAt,
	)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, ugc.ErrNotFound
		}
		return nil, err
	}
	_ = json.Unmarshal([]byte(tagsJSON), &c.Tags)
	return &c, nil
}

// ListCreations 列表查询。
func (s *SQLiteStore) ListCreations(filter *ugc.CreationFilter) ([]*ugc.Creation, int, error) {
	where := "status = 'published'"
	args := []any{}
	if filter.Type != "" {
		where += " AND type = ?"
		args = append(args, filter.Type)
	}

	// 查询总数
	var total int
	countSQL := "SELECT COUNT(*) FROM creations WHERE " + where
	if err := s.db.QueryRow(countSQL, args...).Scan(&total); err != nil {
		return nil, 0, err
	}

	// 排序（sortBy 来自客户端 query，必须走白名单防注入）
	sortColumns := map[string]bool{
		"downloads":  true,
		"rating":     true,
		"created_at": true,
	}
	orderBy := "created_at DESC"
	if sortColumns[filter.SortBy] {
		dir := "ASC"
		if filter.Descending {
			dir = "DESC"
		}
		orderBy = filter.SortBy + " " + dir
	}

	// 分页
	if filter.PageSize <= 0 {
		filter.PageSize = 20
	}
	if filter.Page <= 0 {
		filter.Page = 1
	}
	offset := (filter.Page - 1) * filter.PageSize

	query := "SELECT id, creator_id, creator_name, type, name, description, tags, file_url, thumbnail_url, status, downloads, likes, rating, version, created_at, updated_at FROM creations WHERE " + where + " ORDER BY " + orderBy + " LIMIT ? OFFSET ?"
	args = append(args, filter.PageSize, offset)

	rows, err := s.db.Query(query, args...)
	if err != nil {
		return nil, 0, err
	}
	defer rows.Close()

	return s.scanCreations(rows, total)
}

// SearchCreations 搜索。
func (s *SQLiteStore) SearchCreations(keyword string, creationType string, page, pageSize int) ([]*ugc.Creation, int, error) {
	where := "status = 'published' AND (name LIKE ? OR description LIKE ?)"
	args := []any{"%" + keyword + "%", "%" + keyword + "%"}
	if creationType != "" {
		where += " AND type = ?"
		args = append(args, creationType)
	}

	var total int
	countSQL := "SELECT COUNT(*) FROM creations WHERE " + where
	if err := s.db.QueryRow(countSQL, args...).Scan(&total); err != nil {
		return nil, 0, err
	}

	if pageSize <= 0 {
		pageSize = 20
	}
	if page <= 0 {
		page = 1
	}
	offset := (page - 1) * pageSize

	query := "SELECT id, creator_id, creator_name, type, name, description, tags, file_url, thumbnail_url, status, downloads, likes, rating, version, created_at, updated_at FROM creations WHERE " + where + " ORDER BY downloads DESC LIMIT ? OFFSET ?"
	args = append(args, pageSize, offset)

	rows, err := s.db.Query(query, args...)
	if err != nil {
		return nil, 0, err
	}
	defer rows.Close()

	return s.scanCreations(rows, total)
}

// scanCreations 扫描查询结果。
func (s *SQLiteStore) scanCreations(rows *sql.Rows, total int) ([]*ugc.Creation, int, error) {
	var out []*ugc.Creation
	for rows.Next() {
		var c ugc.Creation
		var tagsJSON string
		if err := rows.Scan(&c.ID, &c.CreatorID, &c.CreatorName, &c.Type, &c.Name, &c.Description, &tagsJSON, &c.FileURL, &c.ThumbnailURL, &c.Status, &c.Downloads, &c.Likes, &c.Rating, &c.Version, &c.CreatedAt, &c.UpdatedAt); err != nil {
			return nil, 0, err
		}
		_ = json.Unmarshal([]byte(tagsJSON), &c.Tags)
		out = append(out, &c)
	}
	return out, total, rows.Err()
}

// IncrementDownloads 增加下载数。
func (s *SQLiteStore) IncrementDownloads(id string) error {
	_, err := s.db.Exec(`UPDATE creations SET downloads = downloads + 1 WHERE id = ?`, id)
	return err
}

// Subscribe 订阅。
func (s *SQLiteStore) Subscribe(playerID, creationID string) error {
	_, err := s.db.Exec(
		`INSERT OR IGNORE INTO subscriptions (player_id, creation_id, created_at) VALUES (?, ?, ?)`,
		playerID, creationID, time.Now().Unix(),
	)
	return err
}

// Unsubscribe 取消订阅。
func (s *SQLiteStore) Unsubscribe(playerID, creationID string) error {
	_, err := s.db.Exec(`DELETE FROM subscriptions WHERE player_id = ? AND creation_id = ?`, playerID, creationID)
	return err
}

// IsSubscribed 是否已订阅。
func (s *SQLiteStore) IsSubscribed(playerID, creationID string) (bool, error) {
	var count int
	err := s.db.QueryRow(`SELECT COUNT(*) FROM subscriptions WHERE player_id = ? AND creation_id = ?`, playerID, creationID).Scan(&count)
	return count > 0, err
}

// RateCreation 评分。
func (s *SQLiteStore) RateCreation(playerID, creationID string, rating int, comment string) error {
	// 插入或更新评分
	_, err := s.db.Exec(`
		INSERT INTO creation_reviews (creation_id, player_id, rating, comment, created_at)
		VALUES (?, ?, ?, ?, ?)
		ON CONFLICT(creation_id, player_id) DO UPDATE SET rating = ?, comment = ?, created_at = ?`,
		creationID, playerID, rating, comment, time.Now().Unix(), rating, comment, time.Now().Unix(),
	)
	if err != nil {
		return err
	}

	// 更新平均分
	_, err = s.db.Exec(`
		UPDATE creations SET rating = (
			SELECT AVG(rating) FROM creation_reviews WHERE creation_id = ?
		) WHERE id = ?`, creationID, creationID)
	return err
}
