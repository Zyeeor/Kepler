// Package service 实现业务逻辑层：UGC 内容服务。
package service

import (
	"crypto/rand"
	"encoding/hex"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"demo/server/internal/logx"
	"demo/server/internal/store"
)

// ContentService UGC 内容服务。
type ContentService struct {
	store     store.Store
	uploadDir string // 文件存储目录
}

// NewContentService 创建内容服务。
func NewContentService(st store.Store, uploadDir string) *ContentService {
	return &ContentService{
		store:     st,
		uploadDir: uploadDir,
	}
}

// UploadRequest 上传创作内容的请求参数。
type UploadRequest struct {
	CreatorID   string // 创作者 ID（客户端自报；为空时服务器生成）
	CreatorName string
	Type        string // map | monster | template
	Name        string
	Description string
	Tags        []string
	FileName    string
	FileData    []byte // 文件内容（JSON 序列化后的地图/怪物数据）
	Thumbnail   []byte // 缩略图（可选，PNG 字节）
}

// Upload 上传创作内容，返回入库后的完整元数据。
func (s *ContentService) Upload(req *UploadRequest) (*store.Creation, error) {
	// 生成创作 ID
	creationID := newCreationID()

	if req.CreatorID == "" {
		req.CreatorID = newPlayerID()
	}

	// 保存文件到本地（demo 阶段用文件系统，生产可换 OSS）
	fileURL, err := s.saveFile(creationID, req.FileName, req.FileData)
	if err != nil {
		return nil, fmt.Errorf("save file: %w", err)
	}

	// 保存缩略图（如果有）
	thumbnailURL := ""
	if len(req.Thumbnail) > 0 {
		thumbnailURL, err = s.saveFile(creationID, "thumbnail.png", req.Thumbnail)
		if err != nil {
			logx.Event("save thumbnail failed: %v", err)
		}
	}

	// 保存元数据到数据库
	creation := &store.Creation{
		ID:           creationID,
		CreatorID:    req.CreatorID,
		CreatorName:  req.CreatorName,
		Type:         req.Type,
		Name:         req.Name,
		Description:  req.Description,
		Tags:         req.Tags,
		FileURL:      fileURL,
		ThumbnailURL: thumbnailURL,
		Status:       "published", // demo 直接发布，生产可加审核
		Version:      1,
	}
	if err := s.store.CreateCreation(creation); err != nil {
		return nil, fmt.Errorf("save metadata: %w", err)
	}

	return creation, nil
}

// Download 下载创作内容，返回元数据与文件字节。
func (s *ContentService) Download(creationID string) (*store.Creation, []byte, error) {
	// 查询元数据
	creation, err := s.store.GetCreation(creationID)
	if err != nil {
		return nil, nil, fmt.Errorf("creation not found: %w", err)
	}

	// 读取文件
	fileData, err := s.readFile(creation.FileURL)
	if err != nil {
		return nil, nil, fmt.Errorf("read file: %w", err)
	}

	// 增加下载数
	_ = s.store.IncrementDownloads(creationID)

	return creation, fileData, nil
}

// List 列表查询。
func (s *ContentService) List(filter *store.CreationFilter) ([]*store.Creation, int, error) {
	return s.store.ListCreations(filter)
}

// Search 搜索。
func (s *ContentService) Search(keyword, creationType string, page, pageSize int) ([]*store.Creation, int, error) {
	return s.store.SearchCreations(keyword, creationType, page, pageSize)
}

// Subscribe 订阅/取消订阅。
func (s *ContentService) Subscribe(playerID, creationID string, subscribe bool) error {
	if subscribe {
		return s.store.Subscribe(playerID, creationID)
	}
	return s.store.Unsubscribe(playerID, creationID)
}

// Rate 评分。
func (s *ContentService) Rate(playerID, creationID string, rating int, comment string) error {
	if rating < 1 || rating > 5 {
		return fmt.Errorf("rating must be 1-5")
	}
	return s.store.RateCreation(playerID, creationID, rating, comment)
}

// ============================================================================
// 辅助函数
// ============================================================================

// saveFile 保存文件到本地。
func (s *ContentService) saveFile(creationID, fileName string, data []byte) (string, error) {
	// 防路径穿越：fileName 来自客户端，必须剥掉所有目录成分，只保留纯文件名。
	safeName := filepath.Base(strings.ReplaceAll(fileName, "\\", "/"))
	if safeName == "." || safeName == ".." || safeName == "" || safeName == "/" {
		return "", fmt.Errorf("invalid file name: %q", fileName)
	}
	dir := filepath.Join(s.uploadDir, creationID)
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return "", err
	}
	filePath := filepath.Join(dir, safeName)
	if err := os.WriteFile(filePath, data, 0o644); err != nil {
		return "", err
	}
	// 返回相对路径（demo 阶段直接用文件路径，生产环境返回 CDN URL）
	return filePath, nil
}

// readFile 读取文件。
func (s *ContentService) readFile(filePath string) ([]byte, error) {
	return os.ReadFile(filePath)
}

// newCreationID 生成创作 ID。
func newCreationID() string {
	return "c" + randomHex()
}

// newPlayerID 生成匿名玩家 ID（无账号体系时的创作者/评分者标识）。
func newPlayerID() string {
	return "u" + randomHex()
}

// randomHex 生成 16 字节随机十六进制串。
func randomHex() string {
	b := make([]byte, 16)
	if _, err := rand.Read(b); err != nil {
		panic(err)
	}
	return hex.EncodeToString(b)
}
