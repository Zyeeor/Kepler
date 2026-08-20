// Package server 装配 UGC 内容服务：依赖注入 + HTTP 接口。
//
// 《Possession》为单机游戏，本服务只承担 UGC 内容平台职责：
// 地图/怪物模版的 上传、下载、列表、搜索、订阅、评分。
package server

import (
	"encoding/json"
	"errors"
	"log"
	"net/http"
	"strconv"
	"time"

	"demo/server/internal/service"
	"demo/server/internal/store"
)

// Config 服务配置。
type Config struct {
	HTTPAddr  string              // HTTP 监听地址
	DBPath    string              // SQLite 文件路径
	UploadDir string              // UGC 文件上传目录
	Elite     service.EliteConfig // 精英怪 BD 快照投放参数（TUNABLE）
	SeedFile  string              // 精英怪种子快照文件路径（空=不 seed）
}

// Server UGC 内容服务 + 精英怪投放服务。
type Server struct {
	cfg        Config
	store      store.Store
	contentSvc *service.ContentService
	eliteSvc   *service.EliteService
}

// New 创建服务。
func New(cfg Config) (*Server, error) {
	st, err := store.NewSQLite(cfg.DBPath)
	if err != nil {
		return nil, err
	}

	eliteCfg := cfg.Elite
	if eliteCfg == (service.EliteConfig{}) {
		eliteCfg = service.DefaultEliteConfig()
	}

	eliteSvc := service.NewEliteService(st, eliteCfg)

	// 种子数据注入：候选库为空时加载预设快照（首位玩家体验保障）
	if cfg.SeedFile != "" {
		if err := eliteSvc.SeedIfEmpty(cfg.SeedFile); err != nil {
			log.Printf("[elite] seed error (non-fatal): %v", err)
		}
	}

	return &Server{
		cfg:        cfg,
		store:      st,
		contentSvc: service.NewContentService(st, cfg.UploadDir),
		eliteSvc:   eliteSvc,
	}, nil
}

// Close 关闭服务。
func (s *Server) Close() {
	_ = s.store.Close()
}

// Handler 返回 HTTP 路由。
func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("POST /api/creations", s.handleUpload)
	mux.HandleFunc("GET /api/creations", s.handleList)
	mux.HandleFunc("GET /api/creations/search", s.handleSearch)
	mux.HandleFunc("GET /api/creations/{id}/download", s.handleDownload)
	mux.HandleFunc("POST /api/creations/{id}/subscribe", s.handleSubscribe)
	mux.HandleFunc("POST /api/creations/{id}/rate", s.handleRate)

	// 精英怪 BD 快照（他人 BD 怪物投放）
	mux.HandleFunc("POST /api/bd-snapshots", s.handleSnapshotUpload)
	mux.HandleFunc("POST /api/elite/pick", s.handleElitePick)
	mux.HandleFunc("GET /api/health", s.handleHealth)
	return logRequests(mux)
}

// ============================================================================
// 日志
// ============================================================================

// statusWriter 捕获响应状态码的 ResponseWriter 包装。
type statusWriter struct {
	http.ResponseWriter
	status int
}

func (w *statusWriter) WriteHeader(code int) {
	w.status = code
	w.ResponseWriter.WriteHeader(code)
}

// logRequests 访问日志中间件：记录每个请求的方法、路径、状态码、耗时、来源地址。
func logRequests(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		sw := &statusWriter{ResponseWriter: w}
		next.ServeHTTP(sw, r)
		log.Printf("[http] %s %s -> %d %s %s",
			r.Method, r.URL.Path, sw.status,
			time.Since(start).Round(time.Millisecond), r.RemoteAddr)
	})
}

// Run 启动服务。
func (s *Server) Run() error {
	log.Printf("ugc server listening on %s (upload dir=%s)", s.cfg.HTTPAddr, s.cfg.UploadDir)
	return http.ListenAndServe(s.cfg.HTTPAddr, s.Handler())
}

// ============================================================================
// 辅助函数
// ============================================================================

func writeJSON(w http.ResponseWriter, code int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(v)
}

func writeErr(w http.ResponseWriter, code int, msg string) {
	if code >= 500 {
		log.Printf("[error] http %d: %s", code, msg) // 5xx 落服务端日志；4xx 属正常业务分支，访问日志已覆盖
	}
	writeJSON(w, code, map[string]any{"code": code, "msg": msg})
}

func decodeJSON(r *http.Request, v any) error {
	return json.NewDecoder(r.Body).Decode(v)
}

// creationTypes 合法的内容类型。
var creationTypes = map[string]bool{
	"map":      true,
	"monster":  true,
	"template": true,
}

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

func toCreationJSON(c *store.Creation) creationJSON {
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
// HTTP 接口
// ============================================================================

// handleUpload 上传创作内容。
func (s *Server) handleUpload(w http.ResponseWriter, r *http.Request) {
	var req struct {
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
	if req.CreatorName == "" {
		req.CreatorName = "anonymous"
	}

	creation, err := s.contentSvc.Upload(&service.UploadRequest{
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

	writeJSON(w, http.StatusOK, map[string]any{
		"creationId": creation.ID,
		"fileUrl":    creation.FileURL,
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

	creations, total, err := s.contentSvc.List(&store.CreationFilter{
		Type:       creationType,
		Page:       page,
		PageSize:   pageSize,
		SortBy:     q.Get("sortBy"), // downloads | rating | created_at（store 层白名单）
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
	writeJSON(w, http.StatusOK, map[string]any{"creations": out, "total": total})
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
	writeJSON(w, http.StatusOK, map[string]any{"creations": out, "total": total})
}

// handleDownload 下载创作内容。
func (s *Server) handleDownload(w http.ResponseWriter, r *http.Request) {
	creationID := r.PathValue("id")

	creation, fileData, err := s.contentSvc.Download(creationID)
	if err != nil {
		if errors.Is(err, store.ErrNotFound) {
			writeErr(w, http.StatusNotFound, "creation not found")
			return
		}
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}

	writeJSON(w, http.StatusOK, map[string]any{
		"creationId": creation.ID,
		"type":       creation.Type,
		"name":       creation.Name,
		"fileData":   fileData, // base64
		"version":    creation.Version,
	})
}

// handleSubscribe 订阅/取消订阅。
func (s *Server) handleSubscribe(w http.ResponseWriter, r *http.Request) {
	creationID := r.PathValue("id")

	var req struct {
		PlayerID  string `json:"playerId"` // 客户端本地持久化的匿名 ID
		Subscribe bool   `json:"subscribe"`
	}
	if err := decodeJSON(r, &req); err != nil || req.PlayerID == "" {
		writeErr(w, http.StatusBadRequest, "playerId is required")
		return
	}

	if err := s.contentSvc.Subscribe(req.PlayerID, creationID, req.Subscribe); err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}

// handleRate 评分。
func (s *Server) handleRate(w http.ResponseWriter, r *http.Request) {
	creationID := r.PathValue("id")

	var req struct {
		PlayerID string `json:"playerId"`
		Rating   int    `json:"rating"`  // 1-5
		Comment  string `json:"comment"` // 可选
	}
	if err := decodeJSON(r, &req); err != nil || req.PlayerID == "" {
		writeErr(w, http.StatusBadRequest, "playerId is required")
		return
	}

	if err := s.contentSvc.Rate(req.PlayerID, creationID, req.Rating, req.Comment); err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}

// ============================================================================
// 精英怪 BD 快照（他人 BD 怪物投放）
// ============================================================================

// handleSnapshotUpload 每波选卡后批量滚动上传 BD 快照（策划案 §8.1）。
func (s *Server) handleSnapshotUpload(w http.ResponseWriter, r *http.Request) {
	var req service.UploadSnapshotsRequest
	if err := decodeJSON(r, &req); err != nil {
		writeErr(w, http.StatusBadRequest, "bad request")
		return
	}

	accepted, err := s.eliteSvc.Upload(&req)
	if err != nil {
		writeErr(w, http.StatusBadRequest, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ok": true, "accepted": accepted})
}

// handleElitePick 第 N 波请求精英怪（策划案 §3/§5）。
//
// snapshot == null 表示本波不投放（兜底 3，正常业务分支，前台按不投放处理）。
func (s *Server) handleElitePick(w http.ResponseWriter, r *http.Request) {
	var req struct {
		PlayerID string `json:"playerId"`
		Wave     int    `json:"wave"`    // 当前波次 N（sourceWave 语义/编码由前台决定，透传比较）
		WaveGap  int    `json:"waveGap"` // 越级波次差（客户端难度设置，0=同波次，1=越一级）
	}
	if err := decodeJSON(r, &req); err != nil {
		writeErr(w, http.StatusBadRequest, "bad request")
		return
	}
	if req.PlayerID == "" {
		writeErr(w, http.StatusBadRequest, "playerId is required")
		return
	}
	if req.Wave < 1 {
		writeErr(w, http.StatusBadRequest, "wave must be >= 1")
		return
	}

	snap, relaxed, err := s.eliteSvc.Pick(req.PlayerID, req.Wave, req.WaveGap)
	if err != nil {
		writeErr(w, http.StatusInternalServerError, err.Error())
		return
	}
	if snap == nil {
		writeJSON(w, http.StatusOK, map[string]any{"snapshot": nil, "relaxed": false})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"snapshot": toSnapshotJSON(snap),
		"relaxed":  relaxed, // 命中放宽波次的兜底路径（仅观测）
	})
}

// snapshotJSON 对外（HTTP）的 BD 快照结构，camelCase；bdData/stats 原样透传。
type snapshotJSON struct {
	SnapshotID     int64           `json:"snapshotId"`
	SourcePlayerID string          `json:"sourcePlayerId"`
	RunID          string          `json:"runId"`
	Sin            string          `json:"sin"`
	MonsterType    string          `json:"monsterType"`
	BDData         json.RawMessage `json:"bdData"`
	BDCount        int             `json:"bdCount"`
	SourceWave     int             `json:"sourceWave"`
	GameTime       int64           `json:"gameTime"`
	Stats          json.RawMessage `json:"stats,omitempty"`
}

func toSnapshotJSON(snap *store.BuildSnapshot) snapshotJSON {
	var stats json.RawMessage
	if snap.Stats != "" {
		stats = json.RawMessage(snap.Stats)
	}
	return snapshotJSON{
		SnapshotID:     snap.ID,
		SourcePlayerID: snap.PlayerID,
		RunID:          snap.RunID,
		Sin:            snap.Sin,
		MonsterType:    snap.MonsterType,
		BDData:         json.RawMessage(snap.BDData),
		BDCount:        snap.BDCount,
		SourceWave:     snap.SourceWave,
		GameTime:       snap.GameTime,
		Stats:          stats,
	}
}

// handleHealth 健康检查（客户端启动探活用）。
func (s *Server) handleHealth(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}
