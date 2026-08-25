// Package server 装配层：依赖注入 + HTTP 接口（路由 / handler / DTO / 中间件）。
//
// 《Possession》为单机游戏，本服务承担两块后端职责：
//   - UGC 内容平台（ugc 包）：上传、下载、列表、搜索、订阅、评分
//   - 精英怪投放（elite 包）：BD 快照存储筛选 + 战果回传聚合
//
// 存储实现统一在 store 包（SQLite 单连接实现两域接口）。
package server

import (
	"net/http"

	"demo/server/elite"
	"demo/server/store"
	"demo/server/tools/logx"
	"demo/server/ugc"
)

// Config 服务配置。
type Config struct {
	HTTPAddr  string             // HTTP 监听地址
	DBPath    string             // SQLite 文件路径
	UploadDir string             // UGC 文件上传目录
	Elite     elite.EliteConfig  // 精英怪 BD 快照投放参数（TUNABLE）
	SeedFile  string             // 精英怪种子快照文件路径（空=不 seed）
	UserBDDir string             // 用户 BD 构筑导入目录（MonsterBuildEditor 工具导出 JSON；每次启动导入，upsert 幂等去重）
}

// Server UGC 内容服务 + 精英怪投放服务。
type Server struct {
	cfg        Config
	store      *store.SQLiteStore
	contentSvc *ugc.ContentService
	eliteSvc   *elite.EliteService
}

// New 创建服务。
func New(cfg Config) (*Server, error) {
	st, err := store.NewSQLite(cfg.DBPath)
	if err != nil {
		return nil, err
	}

	eliteCfg := cfg.Elite
	if eliteCfg == (elite.EliteConfig{}) {
		eliteCfg = elite.DefaultEliteConfig()
	}

	eliteSvc := elite.NewEliteService(st, eliteCfg)

	// 种子数据注入：候选库为空时加载预设快照（首位玩家体验保障）
	if cfg.SeedFile != "" {
		if err := eliteSvc.SeedIfEmpty(cfg.SeedFile); err != nil {
			logx.Event("seed error (non-fatal): %v", err)
		}
	}

	// 用户 BD 导入：每次启动扫描 userBD 目录（MonsterBuildEditor 工具导出构筑），
	// upsert 入库——重复导入由唯一键 (playerId, runId, sin) 幂等去重，不产生重复行
	if cfg.UserBDDir != "" {
		if err := eliteSvc.ImportUserBD(cfg.UserBDDir); err != nil {
			logx.Event("userBD import error (non-fatal): %v", err)
		}
	}

	return &Server{
		cfg:        cfg,
		store:      st,
		contentSvc: ugc.NewContentService(st, cfg.UploadDir),
		eliteSvc:   eliteSvc,
	}, nil
}

// Close 关闭服务。
func (s *Server) Close() {
	_ = s.store.Close()
}

// Handler 返回 HTTP 路由（named 标记 handler 函数名，供访问日志显示）。
func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	mux.Handle("POST /api/creations", named("handleUpload", s.handleUpload))
	mux.Handle("GET /api/creations", named("handleList", s.handleList))
	mux.Handle("GET /api/creations/search", named("handleSearch", s.handleSearch))
	mux.Handle("GET /api/creations/{id}/download", named("handleDownload", s.handleDownload))
	mux.Handle("POST /api/creations/{id}/subscribe", named("handleSubscribe", s.handleSubscribe))
	mux.Handle("POST /api/creations/{id}/rate", named("handleRate", s.handleRate))

	// 精英怪 BD 快照（他人 BD 怪物投放）
	mux.Handle("POST /api/bd-snapshots", named("handleSnapshotUpload", s.handleSnapshotUpload))
	mux.Handle("POST /api/elite/pick", named("handleElitePick", s.handleElitePick))
	mux.Handle("POST /api/user-bd", named("handleUserBDUpload", s.handleUserBDUpload))

	// 战果回传（策划案 §6.5：精英在他人游戏中的战果 → 按构筑主人聚合）
	mux.Handle("POST /api/elite/events", named("handleEliteEvents", s.handleEliteEvents))
	mux.Handle("GET /api/elite/stats", named("handleEliteStats", s.handleEliteStats))
	// 荣誉殿堂排行榜（§5.4/§5.8）：击杀玩家次数最多的 Top N BD 怪物
	mux.Handle("GET /api/elite/leaderboard", named("handleEliteLeaderboard", s.handleEliteLeaderboard))
	mux.Handle("GET /api/health", named("handleHealth", s.handleHealth))
	return logRequests(mux)
}

// Run 启动服务。
func (s *Server) Run() error {
	logx.Event("listening on %s · upload dir=%s", s.cfg.HTTPAddr, s.cfg.UploadDir)
	return http.ListenAndServe(s.cfg.HTTPAddr, s.Handler())
}

// handleHealth 健康检查（客户端启动探活用）。
func (s *Server) handleHealth(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{"ok": true})
}
