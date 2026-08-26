// Package httpapi HTTP 装配层：依赖注入 + HTTP 接口（路由 / handler / DTO / 中间件）。
//
// 《Possession》为单机游戏，本服务承担两块后端职责：
//   - UGC 内容平台（ugc 包）：上传、下载、列表、搜索、订阅、评分
//   - 精英怪投放（elite 包）：BD 快照存储筛选 + 战果回传聚合
//
// 文件划分：
//   - server.go：Server 装配、路由挂载、生命周期（Run/Close）与健康检查
//   - middleware.go：访问日志 + CORS + 请求体上限中间件
//   - ratelimit.go：按 IP 的令牌桶限流（规则表 + limited 中间件）
//   - response.go：JSON 编解码与统一错误/成功响应辅助
//   - ugc.go：UGC 域（路由注册 + handler + DTO）
//   - elite.go：精英域（路由注册 + handler + DTO）
//
// 存储实现统一在 internal/storage/sqlite（单连接实现两域接口）。
package httpapi

import (
	"context"
	"fmt"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"possession/server/internal/elite"
	"possession/server/internal/logx"
	"possession/server/internal/storage/sqlite"
	"possession/server/internal/ugc"
)

// Config 服务配置。
type Config struct {
	HTTPAddr          string            // HTTP 监听地址
	DBPath            string            // SQLite 文件路径
	UploadDir         string            // UGC 文件上传目录
	Elite             elite.EliteConfig // 精英怪 BD 快照投放参数（TUNABLE）
	SeedFile          string            // 精英怪种子快照文件路径（空=不 seed）
	UserBDDir         string            // 用户 BD 构筑导入目录（MonsterBuildEditor 工具导出 JSON；每次启动导入，upsert 幂等去重）
	DisableRateLimit  bool              // 禁用限流（测试用；默认启用，见 ratelimit.go）
}

// Server UGC 内容服务 + 精英怪投放服务。
type Server struct {
	cfg        Config
	store      *sqlite.SQLiteStore
	contentSvc *ugc.ContentService
	eliteSvc   *elite.EliteService
	limiters   map[string]*limiter // 路由组 → 限流器（nil 规则 = 限流禁用，见 limited）
}

// New 创建服务。
func New(cfg Config) (*Server, error) {
	st, err := sqlite.New(cfg.DBPath)
	if err != nil {
		return nil, err
	}

	eliteCfg := cfg.Elite
	if eliteCfg == (elite.EliteConfig{}) {
		eliteCfg = elite.DefaultEliteConfig()
	}

	eliteSvc := elite.NewEliteService(st, eliteCfg)

	// 限流器装配：禁用时 limiters 为空 map，limited() 透传（见 ratelimit.go）。
	limiters := make(map[string]*limiter, len(rateRules))
	if !cfg.DisableRateLimit {
		for name, rule := range rateRules {
			limiters[name] = newLimiter(rule)
		}
	}

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
		limiters:   limiters,
	}, nil
}

// Close 关闭服务。
func (s *Server) Close() {
	_ = s.store.Close()
}

// Handler 返回 HTTP 路由（named 标记 handler 函数名，供访问日志显示）。
// 各域路由由 ugc.go / elite.go 分域注册。
func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	s.registerUGCRoutes(mux)
	s.registerEliteRoutes(mux)
	mux.Handle("GET /api/health", named("handleHealth", s.handleHealth))
	return logRequests(mux)
}

// Run 启动服务（阻塞直至出错或收到退出信号）。全链路超时防慢客户端占用连接；
// SIGINT/SIGTERM → Shutdown（10s 排空在途请求）后返回。
func (s *Server) Run() error {
	httpSrv := &http.Server{
		Addr:              s.cfg.HTTPAddr,
		Handler:           s.Handler(),
		ReadHeaderTimeout: 10 * time.Second,
		ReadTimeout:       30 * time.Second,
		WriteTimeout:      60 * time.Second,
		IdleTimeout:       120 * time.Second,
	}

	errCh := make(chan error, 1)
	go func() { errCh <- httpSrv.ListenAndServe() }()

	logx.Event("listening on %s · upload dir=%s", s.cfg.HTTPAddr, s.cfg.UploadDir)

	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, os.Interrupt, syscall.SIGTERM)

	select {
	case err := <-errCh:
		return err // 启动失败（端口占用等）
	case sig := <-sigCh:
		logx.Event("shutdown · signal=%s · draining (10s)", sig)
		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		if err := httpSrv.Shutdown(ctx); err != nil {
			return fmt.Errorf("graceful shutdown: %w", err)
		}
		return nil
	}
}

// handleHealth 健康检查（客户端启动探活用）。
func (s *Server) handleHealth(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, okResponse{OK: true})
}
