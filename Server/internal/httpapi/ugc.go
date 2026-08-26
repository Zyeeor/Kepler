// UGC 域 HTTP 接口：路由注册 + handler + DTO（内容上传 / 列表 / 搜索 / 下载 / 订阅 / 评分）。
package httpapi

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"path/filepath"
	"strconv"
	"strings"

	"possession/server/internal/ugc"
)

// ============================================================================
// 路由
// ============================================================================

// registerUGCRoutes 注册 UGC 域路由（named 标记 handler 函数名供访问日志显示；limited 按规则限流）。
func (s *Server) registerUGCRoutes(mux *http.ServeMux) {
	mux.Handle("POST /api/creations", named("handleUpload", s.limited("ugc_upload", s.handleUpload)))
	mux.Handle("GET /api/creations", named("handleList", s.limited("read", s.handleList)))
	mux.Handle("GET /api/creations/search", named("handleSearch", s.limited("read", s.handleSearch)))
	mux.Handle("GET /api/creations/{id}/download", named("handleDownload", s.limited("read", s.handleDownload)))
	mux.Handle("POST /api/creations/{id}/subscribe", named("handleSubscribe", s.limited("ugc_action", s.handleSubscribe)))
	mux.Handle("POST /api/creations/{id}/rate", named("handleRate", s.limited("ugc_action", s.handleRate)))
}

// ============================================================================
// 校验常量
// ============================================================================

// creationTypes 合法的内容类型。
var creationTypes = map[string]bool{
	"map":      true,
	"monster":  true,
	"template": true,
}

// 上传内容大小上限与格式白名单（业务层显式校验，给出明确错误信息；
// 全局 body 上限见 middleware maxRequestBodyBytes）。
const (
	maxUploadFileBytes      = 1 << 20 // 1MB（base64 解码后的业务数据；收紧自 12MB，Roadmap 上线项）
	maxUploadThumbnailBytes = 2 << 20 // 2MB（PNG 缩略图）
)

// pngMagic PNG 文件魔数（缩略图格式校验）。
var pngMagic = []byte{0x89, 0x50, 0x4E, 0x47} // \x89PNG

// validateUploadFile UGC 主文件校验：.json 后缀白名单 + 内容必须是合法 JSON。
func validateUploadFile(fileName string, fileData []byte) error {
	if !strings.EqualFold(filepath.Ext(fileName), ".json") {
		return fmt.Errorf("fileName must end with .json")
	}
	if !json.Valid(fileData) {
		return fmt.Errorf("fileData is not valid JSON")
	}
	return nil
}

// validateUploadThumbnail 缩略图校验：可选；提供时必须是 PNG（魔数校验）。
func validateUploadThumbnail(thumbnail []byte) error {
	if len(thumbnail) == 0 {
		return nil
	}
	if len(thumbnail) < len(pngMagic) || !bytes.Equal(thumbnail[:len(pngMagic)], pngMagic) {
		return fmt.Errorf("thumbnail must be a PNG image")
	}
	return nil
}

// ============================================================================
// DTO
// ============================================================================

// creationJSON 对外（HTTP）的创作元数据结构，camelCase。
type creationJSON struct {
	CreationID   string   `json:"creationId"`
	CreatorID    string   `json:"creatorId"`
	CreatorName  string   `json:"creatorName"`
	Type         string   `json:"type"`
	Name         string   `json:"name"`
	Description  string   `json:"description"`
	Tags         []string `json:"tags"`
	ThumbnailURL string   `json:"thumbnailUrl"`
	Downloads    int      `json:"downloads"`
	Likes        int      `json:"likes"`
	Rating       float64  `json:"rating"`
	Version      int      `json:"version"`
	CreatedAt    int64    `json:"createdAt"`
	UpdatedAt    int64    `json:"updatedAt"`
}

// uploadRequest UGC 上传请求体。
type uploadRequest struct {
	CreatorID   string   `json:"creatorId"`   // 可选；客户端本地持久化的匿名 ID
	CreatorName string   `json:"creatorName"` // 可选，缺省 anonymous
	Type        string   `json:"type"`        // map | monster | template
	Name        string   `json:"name"`
	Description string   `json:"description"`
	Tags        []string `json:"tags"`
	FileName    string   `json:"fileName"`
	FileData    []byte   `json:"fileData"` // JSON 序列化后的地图/怪物数据（base64）
	Thumbnail   []byte   `json:"thumbnail"`
}

// uploadResponse 上传成功响应。
type uploadResponse struct {
	CreationID string `json:"creationId"`
	FileURL    string `json:"fileUrl"`
}

// creationsResponse 列表 / 搜索响应。
type creationsResponse struct {
	Creations []creationJSON `json:"creations"`
	Total     int            `json:"total"`
}

// downloadResponse 下载响应。
type downloadResponse struct {
	CreationID string `json:"creationId"`
	Type       string `json:"type"`
	Name       string `json:"name"`
	FileData   []byte `json:"fileData"` // base64
	Version    int    `json:"version"`
}

// subscribeRequest 订阅 / 取消订阅请求体。
type subscribeRequest struct {
	PlayerID  string `json:"playerId"` // 客户端本地持久化的匿名 ID
	Subscribe bool   `json:"subscribe"`
}

// rateRequest 评分请求体。
type rateRequest struct {
	PlayerID string `json:"playerId"`
	Rating   int    `json:"rating"`  // 1-5
	Comment  string `json:"comment"` // 可选
}

func toCreationJSON(c *ugc.Creation) creationJSON {
	return creationJSON{
		CreationID:   c.ID,
		CreatorID:    c.CreatorID,
		CreatorName:  c.CreatorName,
		Type:         c.Type,
		Name:         c.Name,
		Description:  c.Description,
		Tags:         c.Tags,
		ThumbnailURL: c.ThumbnailURL,
		Downloads:    c.Downloads,
		Likes:        c.Likes,
		Rating:       c.Rating,
		Version:      c.Version,
		CreatedAt:    c.CreatedAt,
		UpdatedAt:    c.UpdatedAt,
	}
}

// ============================================================================
// Handler
// ============================================================================

// handleUpload 上传创作内容。
func (s *Server) handleUpload(w http.ResponseWriter, r *http.Request) {
	var req uploadRequest
	if err := decodeJSON(r, &req); err != nil {
		writeErr(w, http.StatusBadRequest, "bad request")
		return
	}

	if req.Name == "" || req.FileName == "" || len(req.FileData) == 0 {
		writeErr(w, http.StatusBadRequest, "name, fileName and fileData are required")
		return
	}
	if !creationTypes[req.Type] {
		writeErr(w, http.StatusBadRequest, "type must be map, monster or template")
		return
	}
	if len(req.FileData) > maxUploadFileBytes {
		writeErr(w, http.StatusBadRequest, "fileData too large (max 1MB)")
		return
	}
	if len(req.Thumbnail) > maxUploadThumbnailBytes {
		writeErr(w, http.StatusBadRequest, "thumbnail too large (max 2MB)")
		return
	}
	if err := validateUploadFile(req.FileName, req.FileData); err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}
	if err := validateUploadThumbnail(req.Thumbnail); err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}
	if req.CreatorName == "" {
		req.CreatorName = "anonymous"
	}

	creation, err := s.contentSvc.Upload(&ugc.UploadRequest{
		CreatorID:   req.CreatorID,
		CreatorName: req.CreatorName,
		Type:        req.Type,
		Name:        req.Name,
		Description: req.Description,
		Tags:        req.Tags,
		FileName:    req.FileName,
		FileData:    req.FileData,
		Thumbnail:   req.Thumbnail,
	})
	if err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}

	writeJSON(w, http.StatusOK, uploadResponse{
		CreationID: creation.ID,
		FileURL:    creation.FileURL,
	})
}

// handleList 列表查询。
func (s *Server) handleList(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query()
	page, _ := strconv.Atoi(q.Get("page"))
	pageSize, _ := strconv.Atoi(q.Get("pageSize"))
	creationType := q.Get("type")
	if creationType != "" && !creationTypes[creationType] {
		writeErr(w, http.StatusBadRequest, "invalid type")
		return
	}

	creations, total, err := s.contentSvc.List(&ugc.CreationFilter{
		Type:       creationType,
		Page:       page,
		PageSize:   pageSize,
		SortBy:     q.Get("sortBy"),                // downloads | rating | created_at（store 层白名单）
		Descending: q.Get("descending") != "false", // 默认降序（与 api-guide 一致），仅 descending=false 时升序
	})
	if err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}

	out := make([]creationJSON, 0, len(creations))
	for _, c := range creations {
		out = append(out, toCreationJSON(c))
	}
	writeJSON(w, http.StatusOK, creationsResponse{Creations: out, Total: total})
}

// handleSearch 搜索。
func (s *Server) handleSearch(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query()
	page, _ := strconv.Atoi(q.Get("page"))
	pageSize, _ := strconv.Atoi(q.Get("pageSize"))
	creationType := q.Get("type")
	if creationType != "" && !creationTypes[creationType] {
		writeErr(w, http.StatusBadRequest, "invalid type")
		return
	}

	creations, total, err := s.contentSvc.Search(q.Get("keyword"), creationType, page, pageSize)
	if err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}

	out := make([]creationJSON, 0, len(creations))
	for _, c := range creations {
		out = append(out, toCreationJSON(c))
	}
	writeJSON(w, http.StatusOK, creationsResponse{Creations: out, Total: total})
}

// handleDownload 下载创作内容。
func (s *Server) handleDownload(w http.ResponseWriter, r *http.Request) {
	creationID := r.PathValue("id")

	creation, fileData, err := s.contentSvc.Download(creationID)
	if err != nil {
		if errors.Is(err, ugc.ErrNotFound) {
			writeErr(w, http.StatusNotFound, "creation not found")
			return
		}
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}

	writeJSON(w, http.StatusOK, downloadResponse{
		CreationID: creation.ID,
		Type:       creation.Type,
		Name:       creation.Name,
		FileData:   fileData, // base64
		Version:    creation.Version,
	})
}

// handleSubscribe 订阅/取消订阅。
func (s *Server) handleSubscribe(w http.ResponseWriter, r *http.Request) {
	creationID := r.PathValue("id")

	var req subscribeRequest
	if err := decodeJSON(r, &req); err != nil || req.PlayerID == "" {
		writeErr(w, http.StatusBadRequest, "playerId is required")
		return
	}

	if err := s.contentSvc.Subscribe(req.PlayerID, creationID, req.Subscribe); err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, okResponse{OK: true})
}

// handleRate 评分。
func (s *Server) handleRate(w http.ResponseWriter, r *http.Request) {
	creationID := r.PathValue("id")

	var req rateRequest
	if err := decodeJSON(r, &req); err != nil || req.PlayerID == "" {
		writeErr(w, http.StatusBadRequest, "playerId is required")
		return
	}

	if err := s.contentSvc.Rate(req.PlayerID, creationID, req.Rating, req.Comment); err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, okResponse{OK: true})
}
